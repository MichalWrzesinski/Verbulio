"""Exercise the development learning API against an isolated PostgreSQL database."""

import concurrent.futures
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
            assert response.status == status, (path, response.status, payload)
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

                stop(server)
                server = subprocess.Popen(command, env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
                wait_ready(server, base)
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

    print("PostgreSQL learning integration passed: authorization, restart, duplicate submission, completion and partial progress.")


if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) > 1 else "dotnet")
