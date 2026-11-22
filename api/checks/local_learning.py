"""Exercise the development learning API against an isolated PostgreSQL database."""

import concurrent.futures
import csv
import http.cookiejar
import json
import os
from pathlib import Path
import socket
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import uuid


ROOT = Path(__file__).resolve().parents[1]
SESSION = "/api/v1/local-learning/sessions"
IDENTITY = "/api/v1/local-learning/identity"
REGISTRATION = "/api/v1/local-registration/accounts"
ACCOUNT_LEARNING = "/api/v1/local-account/learning/sessions"
ACCOUNT = "/api/v1/local-account"
TRANSLATIONS = {
    "house": "dom", "dom": "house",
    "window": "okno", "okno": "window",
    "boat": "łódź", "łódź": "boat",
}


class Browser:
    def __init__(self, base, tls):
        self.base = base
        self.tls = tls
        self.cookies = http.cookiejar.CookieJar()
        self.client = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.cookies),
            urllib.request.HTTPSHandler(context=tls),
        )

    def request(self, path, status=200, method="GET", body=None, key=None, include_cookie_headers=False):
        headers = {"X-Verbulio-Client": "local-learning"}
        data = None
        if key:
            headers["Idempotency-Key"] = key
        if body is not None:
            data = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"

        request = urllib.request.Request(self.base + path, data=data, headers=headers, method=method)
        try:
            response = self.client.open(request, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            payload = response.read()
            expected = (status,) if isinstance(status, int) else status
            assert response.status in expected, (path, response.status, payload)
            decoded = json.loads(payload) if payload else None
            if include_cookie_headers:
                return decoded, response.headers.get_all("Set-Cookie", [])
            return decoded


def wait_ready(server, base, tls):
    for _ in range(60):
        if server.poll() is not None:
            raise RuntimeError("The ASP.NET process exited before it reported healthy.")
        try:
            Browser(base, tls).request("/health")
            return
        except (urllib.error.URLError, TimeoutError):
            time.sleep(0.1)
    raise RuntimeError("The ASP.NET process did not become ready.")


def answer(question, correct=True):
    expected = TRANSLATIONS[question["text"]]
    if question["mode"] == "Typed":
        return {"questionNumber": question["number"], "text": expected}
    choices = question["choices"]
    index = choices.index(expected) if correct else next(i for i, choice in enumerate(choices) if choice != expected)
    return {"questionNumber": question["number"], "choice": index}


def start_placement(owner, other, anonymous):
    endpoint = "/api/v1/local-placement/tests"
    anonymous.request(endpoint, 401, "POST")
    initial = owner.request(endpoint, method="POST")
    assert owner.request(endpoint, method="POST") == initial
    old = f"{endpoint}/{initial['id']}"
    old_answer = {"questionNumber": 1, "skip": True}
    for browser, denied in [(anonymous, 401), (other, 404)]:
        browser.request(old, denied)
        browser.request(old + "/restart", denied, "POST")
        browser.request(old + "/answers", denied, "POST", old_answer, str(uuid.uuid4()))
    owner.request(old + "/answers", 400, "POST", {**old_answer, "result": "C2"}, str(uuid.uuid4()))
    assert owner.request(old) == initial

    replacement = owner.request(old + "/restart", method="POST")
    assert replacement["id"] != initial["id"]
    owner.request(old, 404)
    owner.request(old + "/answers", 404, "POST", old_answer, str(uuid.uuid4()))
    resource = f"{endpoint}/{replacement['id']}"
    key = str(uuid.uuid4())
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        responses = [
            pool.submit(owner.request, resource + "/answers", 200, "POST", old_answer, key)
            for _ in range(2)
        ]
    first = responses[0].result()
    assert first == responses[1].result()
    assert first["test"]["answeredCount"] == 1
    assert first["test"]["result"] is None
    owner.request(resource + "/answers", 422, "POST",
                  {"questionNumber": 2, "text": "test"}, key)
    owner.request(resource + "/answers", 409, "POST", old_answer, str(uuid.uuid4()))
    return resource, first, key


def finish_placement(owner, resource, first, key):
    assert owner.request(resource) == first
    duplicate = owner.request(resource + "/answers", 200, "POST",
                              {"questionNumber": 1, "skip": True}, key)
    assert duplicate == first
    current = duplicate["test"]
    for number in range(2, 21):
        question = current["question"]
        assert question["number"] == number
        assert {"number", "text", "direction", "mode", "choices"} == question.keys()
        assert current["result"] is None
        submission = {"questionNumber": number, "skip": True}
        if number == 2:
            owner.request(resource + "/answers", 400, "POST",
                          {"questionNumber": number, "text": "\u0000"}, str(uuid.uuid4()))
            assert owner.request(resource)["test"] == current
            expected, _ = pilot_translation(question)
            submission = {"questionNumber": number, "text": expected}
        response = owner.request(resource + "/answers", 200, "POST",
                                 submission, str(uuid.uuid4()))
        current = response["test"]
        assert {"question", "answeredCount", "totalCount", "result"} == current.keys()
        assert current["answeredCount"] == number
    assert current == {"question": None, "answeredCount": 20, "totalCount": 20, "result": "A1"}
    assert owner.request(resource)["test"] == current
    owner.request(resource + "/answers", 409, "POST", {"questionNumber": 21, "skip": True}, str(uuid.uuid4()))


def registration_data(resource, email):
    return {
        "placementTestId": resource.rsplit("/", 1)[-1],
        "email": email,
        "password": "local fixture password!",
    }


def pilot_item(question):
    source, target = ("polish", "english") if question["direction"] == "PolishToEnglish" else ("english", "polish")
    prompt = question["text"].split(" (", 1)[0]
    dictionary_file = ROOT / "Verbulio.Api/Dictionary/placement-pilot-v1.tsv"
    with dictionary_file.open(encoding="utf-8", newline="") as entries:
        item = next(row for row in csv.DictReader(entries, delimiter="\t") if row[source] == prompt)
    return item, target


def pilot_translation(question):
    item, target = pilot_item(question)
    return item[target], item["level"]


def pilot_answer(question):
    expected, _ = pilot_translation(question)
    if question["mode"] == "Typed":
        return {"questionNumber": question["number"], "text": expected}
    return {"questionNumber": question["number"], "choice": question["choices"].index(expected)}


def skip_placement(browser, resource):
    for number in range(1, 21):
        browser.request(resource + "/answers", 200, "POST",
                        {"questionNumber": number, "skip": True}, str(uuid.uuid4()))


def exercise_registration(owner, other, anonymous, resource):
    email_base = uuid.uuid4().hex
    data = registration_data(resource, email_base + "@example.invalid")
    candidate = {**data, "email": "second-" + data["email"]}
    outcome = f"/api/v1/local-registration/outcomes/{data['placementTestId']}"
    anonymous.request(outcome, 401)
    other.request(outcome, 404)
    owner.request(outcome, 404)
    anonymous.request(REGISTRATION, 401, "POST", data)
    denied = other.request(REGISTRATION, 409, "POST", data)
    def signature(problem):
        return problem["status"], problem["code"], problem["title"]

    assert denied["code"] == "registration_unavailable"
    assert signature(owner.request(REGISTRATION, 409, "POST", {**data, "placementTestId": str(uuid.uuid4())})) == signature(denied)
    owner.request(REGISTRATION, 400, "POST", {**data, "startingLevel": "C2"})
    owner.request(REGISTRATION, 400, "POST", {**data, "password": "short"})
    owner.request(REGISTRATION, 400, "POST", {**data, "email": "Name <person@example.invalid>"})

    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        requests = [pool.submit(owner.request, REGISTRATION, (201, 409), "POST", request)
                    for request in (data, candidate)]
    responses = [request.result() for request in requests]
    assert sum("id" in response for response in responses) == 1
    winner = next(index for index, response in enumerate(responses) if "id" in response)
    account = responses[winner]
    assert {"id", "startingLevel"} == account.keys()
    assert account["startingLevel"] == owner.request(resource)["test"]["result"] == "A1"
    assert owner.request(outcome) == account
    other.request(outcome, 404)
    assert signature(responses[1 - winner]) == signature(denied)
    assert signature(owner.request(REGISTRATION, 409, "POST", data)) == signature(denied)
    owner.request(resource + "/restart", 404, "POST")

    # A duplicate email must roll back a claim on a different completed test.
    tests = "/api/v1/local-placement/tests"
    second = other.request(tests, method="POST")
    second_resource = f"{tests}/{second['id']}"
    other.request(REGISTRATION, 409, "POST", registration_data(second_resource, candidate["email"]))
    skip_placement(other, second_resource)
    assert other.request(second_resource)["test"]["result"] == "A1"
    replacement = other.request(second_resource + "/restart", method="POST")
    assert signature(other.request(REGISTRATION, 409, "POST",
                                   registration_data(second_resource, candidate["email"]))) == signature(denied)
    second_resource = f"{tests}/{replacement['id']}"
    current = replacement["test"]
    for _ in range(20):
        current = other.request(second_resource + "/answers", 200, "POST",
                                pilot_answer(current["question"]), str(uuid.uuid4()))["test"]
    assert current["result"] == "C2"
    existing_email = [data, candidate][winner]["email"].upper()
    assert signature(other.request(REGISTRATION, 409, "POST", registration_data(second_resource, existing_email))) == signature(denied)
    second_account = other.request(REGISTRATION, 201, "POST",
                                   registration_data(second_resource, "retry-" + data["email"]))
    assert second_account["startingLevel"] == "C2"
    assert second_account["id"] != account["id"]
    return [data, candidate][winner]["email"], account, "retry-" + data["email"], second_account


def copy_account_cookie(browser):
    previous = Browser(browser.base, browser.tls)
    for cookie in browser.cookies:
        if cookie.name == "verbulio.local-account":
            previous.cookies.set_cookie(cookie)
    return previous


def start_account_session(browser, guest, anonymous, email, expected):
    browser.request(ACCOUNT + "/session", 401)
    guest.request(ACCOUNT + "/session", 401)
    anonymous.request(ACCOUNT + "/session", 401)
    guest.request(ACCOUNT + "/logout", 401, "POST")
    anonymous.request(ACCOUNT + "/logout", 401, "POST")
    invalid = {"email": email, "password": "other local fixture password!"}
    wrong = browser.request(ACCOUNT + "/login", 401, "POST", invalid)
    unknown = browser.request(ACCOUNT + "/login", 401, "POST", {**invalid, "email": "unknown@example.invalid"})
    for key in ("code", "title", "status"):
        assert wrong[key] == unknown[key]
    login = {"email": email.upper(), "password": "local fixture password!"}
    assert browser.request(ACCOUNT + "/login", 200, "POST", login) == expected
    assert browser.request(ACCOUNT + "/session") == expected
    cookie = next(cookie for cookie in browser.cookies if cookie.name == "verbulio.local-account")
    assert cookie.secure
    return login


def finish_account_session(browser, login, expected, other, other_email, other_account):
    assert browser.request(ACCOUNT + "/session") == expected
    other_login = {"email": other_email, "password": "local fixture password!"}
    assert other.request(ACCOUNT + "/login", 200, "POST", other_login) == other_account
    assert other.request(ACCOUNT + "/session") == other_account
    assert browser.request(ACCOUNT + "/session") == expected

    old_cookie = copy_account_cookie(browser)
    contenders = (browser, copy_account_cookie(browser))
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        attempts = [pool.submit(contender.request, ACCOUNT + "/login", (200, 409), "POST", login,
                                include_cookie_headers=True) for contender in contenders]
    reports = [attempt.result() for attempt in attempts]
    assert sum(report == expected for report, _ in reports) == 1
    assert any(report.get("code") == "session_rotation_conflict" and not headers
               for report, headers in reports)
    winner_index = next(index for index, (report, _) in enumerate(reports) if report == expected)
    winner = contenders[winner_index]
    assert reports[winner_index][1]
    old_cookie.request(ACCOUNT + "/session", 401)
    _, stale_headers = old_cookie.request(ACCOUNT + "/session/reset", 204, "POST", include_cookie_headers=True)
    assert stale_headers and not list(old_cookie.cookies)
    _, active_headers = winner.request(ACCOUNT + "/session/reset", 204, "POST", include_cookie_headers=True)
    assert not active_headers
    assert winner.request(ACCOUNT + "/session") == expected
    revoked = copy_account_cookie(winner)
    winner.request(ACCOUNT + "/logout", 204, "POST")
    winner.request(ACCOUNT + "/session", 401)
    revoked.request(ACCOUNT + "/session", 401)
    assert other.request(ACCOUNT + "/session") == other_account

    if expire := os.environ.get("VERBULIO_EXPIRE_TEST_SESSIONS"):
        assert winner.request(ACCOUNT + "/login", 200, "POST", login) == expected
        subprocess.run([expire, "near-idle"], check=True)
        assert winner.request(ACCOUNT + "/session") == expected
        subprocess.run([expire, "assert-touch"], check=True)
        subprocess.run([expire, "idle"], check=True)
        winner.request(ACCOUNT + "/session", 401)
        assert winner.request(ACCOUNT + "/login", 200, "POST", login) == expected
        subprocess.run([expire, "absolute"], check=True)
        winner.request(ACCOUNT + "/session", 401)
        print("Idle activity and both server expiry conditions verified while cookies were present.")

    bad_login = {"email": login["email"], "password": "invalid fixture password!"}
    results = [winner.request(ACCOUNT + "/login", (401, 429), "POST", bad_login) for _ in range(13)]
    assert any(result and result["code"] == "login_rate_limited" for result in results)
    assert results[-1] is None


def exercise_account_learning(owner, other, anonymous, login, account, other_email, other_account):
    guest = Browser(owner.base, owner.tls)
    guest.request(IDENTITY, 204, "POST")
    guest.request(ACCOUNT_LEARNING, 401, "POST")
    anonymous.request(ACCOUNT_LEARNING, 401, "POST")
    categories_path = ACCOUNT + "/learning/categories"
    guest.request(categories_path, 401)
    anonymous.request(categories_path, 401)
    assert {"id": "jedzenie", "title": "Jedzenie"} in owner.request(categories_path)
    assert owner.request(ACCOUNT_LEARNING + "?category=unknown", 404, "POST")["code"] == "category_not_found"
    assert owner.request(ACCOUNT_LEARNING + "?category=emocje", 409, "POST")["code"] == "no_words_available"

    food_path = ACCOUNT_LEARNING + "?category=jedzenie"
    food = owner.request(food_path, method="POST")
    food_resource = f"{ACCOUNT_LEARNING}/{food['id']}"
    assert food["summary"]["wordCount"] == 5
    assert owner.request(ACCOUNT_LEARNING + "?category=sport", method="POST") == food
    assert owner.request(food_resource) == food
    food_words = set()
    while question := food["question"]:
        assert food["category"] == "jedzenie"
        food_words.add(pilot_item(question)[0]["id"])
        food = owner.request(food_resource + "/answers", 200, "POST",
                             pilot_answer(question), str(uuid.uuid4()))["session"]
    assert food_words == {"a1-03", "a1-06", "a1-10", "a1-14", "a1-15"}, food_words
    assert food["category"] == "jedzenie"
    assert food["summary"] == {"firstTryCorrect": 5, "wordCount": 5, "wordsResolved": 5, "completed": True}
    assert owner.request(food_path, 409, "POST")["code"] == "no_words_available"

    first = owner.request(ACCOUNT_LEARNING, method="POST")
    assert first["summary"]["wordCount"] == 10
    resource = f"{ACCOUNT_LEARNING}/{first['id']}"
    submission = pilot_answer(first["question"])

    assert other.request(ACCOUNT + "/login", 200, "POST",
                         {"email": other_email, "password": login["password"]}) == other_account
    for browser, denied in [(guest, 401), (anonymous, 401), (other, 404)]:
        browser.request(resource, denied)
        browser.request(resource + "/answers", denied, "POST", submission, str(uuid.uuid4()))
        browser.request(resource + "/end", denied, "POST")

    separate = other.request(ACCOUNT_LEARNING, method="POST")
    assert separate["id"] != first["id"]
    assert pilot_translation(separate["question"])[1] == "C2"
    assert separate["summary"]["wordsResolved"] == 0

    first_word = first["question"]["text"]
    assert pilot_translation(first["question"])[1] == "A1"
    result = owner.request(resource + "/answers", 200, "POST", submission, str(uuid.uuid4()))
    assert result["feedback"]["assessment"]["grade"] == "Correct"
    current = result["session"]
    owner.request(ACCOUNT + "/logout", 204, "POST")
    owner.request(resource, 401)
    assert owner.request(ACCOUNT + "/login", 200, "POST", login) == account
    assert owner.request(resource) == current
    assert owner.request(ACCOUNT_LEARNING, method="POST") == current

    seen = {first_word}
    while question := current["question"]:
        assert pilot_translation(question)[1] == "A1"
        assert question["text"] not in seen
        seen.add(question["text"])
        current = owner.request(resource + "/answers", 200, "POST",
                                pilot_answer(question), str(uuid.uuid4()))["session"]
    assert len(seen) == 10
    assert current["summary"] == {"firstTryCorrect": 10, "wordCount": 10, "wordsResolved": 10, "completed": True}
    follow_up = owner.request(ACCOUNT_LEARNING, method="POST")
    assert follow_up["id"] != first["id"]
    assert follow_up["question"]["text"] not in seen
    assert other.request(f"{ACCOUNT_LEARNING}/{separate['id']}") == separate


def stop(server):
    server.terminate()
    server.wait(timeout=10)


def run(dotnet):
    with socket.socket() as reserve:
        reserve.bind(("127.0.0.1", 0))
        port = reserve.getsockname()[1]
    base = f"https://127.0.0.1:{port}"
    with tempfile.TemporaryDirectory(prefix="verbulio-learning-") as directory:
        cert = Path(directory) / "localhost.crt"
        key_file = Path(directory) / "localhost.key"
        config = Path(directory) / "localhost.cnf"
        config.write_text("""[req]
distinguished_name = name
x509_extensions = extensions
prompt = no
[name]
CN = 127.0.0.1
[extensions]
subjectAltName = IP:127.0.0.1
basicConstraints = critical,CA:TRUE
""", encoding="utf-8")
        subprocess.run(
            ["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1",
             "-keyout", str(key_file), "-out", str(cert), "-config", str(config)],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True,
        )
        tls = ssl.create_default_context(cafile=str(cert))
        env = {
            **os.environ,
            "ASPNETCORE_URLS": base,
            "ASPNETCORE_ENVIRONMENT": "Development",
            "LearningKeyDirectory": directory,
            "ASPNETCORE_Kestrel__Certificates__Default__Path": str(cert),
            "ASPNETCORE_Kestrel__Certificates__Default__KeyPath": str(key_file),
        }
        dll = ROOT / "Verbulio.Api/bin/Release/net10.0/Verbulio.Api.dll"
        command = [dotnet, str(dll)]
        subprocess.run([*command, "--migrate"], env=env, cwd=ROOT, check=True)
        with open(Path(directory) / "api.log", "w+", encoding="utf-8") as log:
            server = subprocess.Popen(command, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            try:
                wait_ready(server, base, tls)
                owner, other, anonymous = Browser(base, tls), Browser(base, tls), Browser(base, tls)
                anonymous.request(SESSION, status=401, method="POST")
                owner.request(IDENTITY, status=204, method="POST")
                other.request(IDENTITY, status=204, method="POST")
                session = owner.request(SESSION, method="POST")
                resource = f"{SESSION}/{session['id']}"
                assert owner.request(SESSION, method="POST") == session
                for browser, denied in [(anonymous, 401), (other, 404)]:
                    browser.request(resource, status=denied)
                    browser.request(resource + "/answers", denied, "POST",
                                    answer(session["question"]), str(uuid.uuid4()))
                    browser.request(resource + "/end", denied, "POST")
                owner.request(resource + "/answers", 400, "POST",
                              {**answer(session["question"]), "unexpected": True}, str(uuid.uuid4()))
                assert owner.request(resource) == session

                first = session["question"]
                submission = answer(first, correct=False)
                key = str(uuid.uuid4())
                with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
                    responses = [
                        pool.submit(owner.request, resource + "/answers", 200, "POST", submission, key)
                        for _ in range(2)
                    ]
                accepted = responses[0].result()
                assert accepted == responses[1].result()
                assert accepted["feedback"]["progress"]["changedReview"]["box"] == "OneDay"
                owner.request(resource + "/answers", 422, "POST", answer(first), key)
                owner.request(resource + "/answers", 409, "POST", submission, str(uuid.uuid4()))

                placement = start_placement(owner, other, anonymous)
                stop(server)
                server = subprocess.Popen(command, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                wait_ready(server, base, tls)
                unfinished_data = registration_data(placement[0], uuid.uuid4().hex + "@example.invalid")
                owner.request(REGISTRATION, 409, "POST", unfinished_data)
                finish_placement(owner, *placement)
                email, account, other_email, other_account = exercise_registration(owner, other, anonymous, placement[0])
                login = start_account_session(owner, other, anonymous, email, account)
                exercise_account_learning(owner, other, anonymous, login, account, other_email, other_account)
                assert owner.request(resource) == accepted["session"]
                assert owner.request(resource + "/answers", 200, "POST", submission, key) == accepted

                current = accepted["session"]
                for _ in range(2):
                    current = owner.request(resource + "/answers", 200, "POST",
                                            answer(current["question"]), str(uuid.uuid4()))["session"]
                assert current["question"]["text"] == TRANSLATIONS[first["text"]]
                final = owner.request(resource + "/answers", 200, "POST",
                                      answer(current["question"]), str(uuid.uuid4()))
                assert final["feedback"]["progress"]["changedReview"] is None
                assert final["session"]["summary"] == {
                    "firstTryCorrect": 2, "wordCount": 3, "wordsResolved": 3, "completed": True,
                }
                owner.request(SESSION, 409, "POST")

                partial = other.request(SESSION, method="POST")
                partial_resource = f"{SESSION}/{partial['id']}"
                other.request(partial_resource + "/answers", 200, "POST",
                              answer(partial["question"]), str(uuid.uuid4()))
                ended = other.request(partial_resource + "/end", 200, "POST")
                assert ended["question"] is None
                assert ended["summary"]["wordsResolved"] == 1
                assert not ended["summary"]["completed"]
                assert other.request(SESSION, method="POST")["summary"]["wordCount"] == 2

                stop(server)
                server = subprocess.Popen(command, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                wait_ready(server, base, tls)
                finish_account_session(owner, login, account, other, other_email, other_account)

                if previous_api := os.environ.get("VERBULIO_PREVIOUS_API_DLL"):
                    stop(server)
                    server = subprocess.Popen([dotnet, previous_api], env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                    wait_ready(server, base, tls)
                    claimed_test = placement[0]
                    owner.request(claimed_test + "/restart", 404, "POST")
                    assert owner.request(claimed_test)["test"]["result"] == "A1"
                    print("Previous API rollback check passed on migrated PostgreSQL with a claimed test.")
            except Exception:
                log.seek(0)
                print(log.read(), file=sys.stderr)
                raise
            finally:
                stop(server)
            log.seek(0)
            audit = log.read()
            assert "Account login rejected" in audit
            assert "Account login attempt rate limit reached" in audit
            assert "Account session authorization denied" in audit
            assert email.lower() not in audit.lower()
            assert "local fixture password!" not in audit.lower()

    print("PostgreSQL learning, placement, registration and account-session integration passed: authorization, restart, duplicate submission, completion and partial progress.")


if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) > 1 else "dotnet")
