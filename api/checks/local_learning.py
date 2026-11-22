"""Exercise the development learning API against an isolated PostgreSQL database."""

import concurrent.futures
import csv
import http.cookiejar
import json
import os
from pathlib import Path
import socket
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
TRANSLATIONS = {
    "house": "dom", "dom": "house",
    "window": "okno", "okno": "window",
    "boat": "łódź", "łódź": "boat",
}


class Browser:
    def __init__(self, base):
        self.base = base
        self.client = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
        )

    def request(self, path, status=200, method="GET", body=None, key=None):
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
            return json.loads(payload) if payload else None


def wait_ready(server, base):
    for _ in range(60):
        if server.poll() is not None:
            raise RuntimeError("The ASP.NET process exited before it reported healthy.")
        try:
            Browser(base).request("/health")
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
            source, target = ("polish", "english") if question["direction"] == "PolishToEnglish" else ("english", "polish")
            prompt = question["text"].split(" (", 1)[0]
            dictionary_file = ROOT / "Verbulio.Api/Dictionary/placement-pilot-v1.tsv"
            with dictionary_file.open(encoding="utf-8", newline="") as entries:
                expected = next(row[target] for row in csv.DictReader(entries, delimiter="\t") if row[source] == prompt)
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


def exercise_registration(owner, other, anonymous, resource):
    email_base = uuid.uuid4().hex
    data = registration_data(resource, email_base + "@example.invalid")
    candidate = {**data, "email": "second-" + data["email"]}
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
    assert signature(responses[1 - winner]) == signature(denied)
    assert signature(owner.request(REGISTRATION, 409, "POST", data)) == signature(denied)
    owner.request(resource + "/restart", 404, "POST")

    # A duplicate email must roll back a claim on a different completed test.
    tests = "/api/v1/local-placement/tests"
    second = other.request(tests, method="POST")
    second_resource = f"{tests}/{second['id']}"
    other.request(REGISTRATION, 409, "POST", registration_data(second_resource, candidate["email"]))
    for number in range(1, 21):
        other.request(second_resource + "/answers", 200, "POST",
                      {"questionNumber": number, "skip": True}, str(uuid.uuid4()))
    existing_email = [data, candidate][winner]["email"].upper()
    assert signature(other.request(REGISTRATION, 409, "POST", registration_data(second_resource, existing_email))) == signature(denied)
    second_account = other.request(REGISTRATION, 201, "POST",
                                   registration_data(second_resource, "retry-" + data["email"]))
    assert second_account["startingLevel"] == "A1"
    assert second_account["id"] != account["id"]


def stop(server):
    server.terminate()
    server.wait(timeout=10)


def run(dotnet):
    with socket.socket() as reserve:
        reserve.bind(("127.0.0.1", 0))
        port = reserve.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    with tempfile.TemporaryDirectory(prefix="verbulio-learning-") as directory:
        env = {
            **os.environ,
            "ASPNETCORE_URLS": base,
            "ASPNETCORE_ENVIRONMENT": "Development",
            "LearningKeyDirectory": directory,
        }
        dll = ROOT / "Verbulio.Api/bin/Release/net10.0/Verbulio.Api.dll"
        command = [dotnet, str(dll)]
        subprocess.run([*command, "--migrate"], env=env, cwd=ROOT, check=True)
        with open(Path(directory) / "api.log", "w+", encoding="utf-8") as log:
            server = subprocess.Popen(command, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
            try:
                wait_ready(server, base)
                owner, other, anonymous = Browser(base), Browser(base), Browser(base)
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
                wait_ready(server, base)
                unfinished_data = registration_data(placement[0], uuid.uuid4().hex + "@example.invalid")
                owner.request(REGISTRATION, 409, "POST", unfinished_data)
                finish_placement(owner, *placement)
                exercise_registration(owner, other, anonymous, placement[0])
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
            except Exception:
                log.seek(0)
                print(log.read(), file=sys.stderr)
                raise
            finally:
                stop(server)

    print("PostgreSQL learning, placement and registration integration passed: authorization, restart, duplicate submission, completion and partial progress.")


if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) > 1 else "dotnet")
