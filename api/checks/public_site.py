"""Verify the public runtime with disposable web assets and no database."""

from contextlib import contextmanager
import http.client
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time

API = Path(__file__).resolve().parents[1] / "Verbulio.Api/bin/Release/net10.0/Verbulio.Api.dll"
HOST = "verbulio.gitpush.app"
FORWARDED = {"Host": HOST, "X-Forwarded-For": "192.0.2.10", "X-Forwarded-Proto": "https"}


def request(path, headers=FORWARDED, method="GET", include_headers=False, body=None):
    connection = http.client.HTTPConnection("127.0.0.1", 5444, timeout=2)
    try:
        connection.request(method, path, body=body, headers=headers)
        response = connection.getresponse()
        payload = response.read()
        if include_headers:
            return response.status, payload, {key.lower(): value for key, value in response.getheaders()}
        return response.status, payload
    finally:
        connection.close()


def verify():
    status, payload, headers = request("/", include_headers=True)
    assert (status, payload) == (200, b"<h1>Public Verbulio fixture</h1>")
    assert headers["strict-transport-security"] == "max-age=31536000"
    assert "frame-ancestors 'none'" in headers["content-security-policy"]
    assert headers["x-content-type-options"] == "nosniff"
    assert headers["referrer-policy"] == "no-referrer"
    assert request("/test") == request("/")
    assert request("/app.css") == (200, b"body {}")
    assert request("/missing.js")[0] == 404
    assert request("/api/v1/local-registration/accounts", method="POST")[0] == 404
    assert request("/api/v1/local-account/settings/account")[0] == 404
    assert request("/health", {"Host": HOST})[0] == 400
    assert request("/health", {**FORWARDED, "Host": "other.example.invalid"})[0] == 400
    assert request("/health", {**FORWARDED, "X-Forwarded-Proto": "http"})[0] == 400
    assert request("/health", {"Host": HOST, "X-Forwarded-Proto": "https"})[0] == 400


@contextmanager
def site(revision, staging_connection=None, journal_connection=None):
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        assets = root / "wwwroot"
        assets.mkdir()
        (assets / "index.html").write_text("<h1>Public Verbulio fixture</h1>", encoding="utf-8")
        (assets / "app.css").write_text("body {}", encoding="utf-8")
        (root / "REVISION").write_text(revision + "\n", encoding="utf-8")
        env = {key: value for key, value in os.environ.items()
               if not key.startswith(("ConnectionStrings__", "ASPNETCORE_", "DOTNET_ENVIRONMENT", "PublicSite__"))}
        env["ASPNETCORE_ENVIRONMENT"] = "Staging" if staging_connection else "Public"
        command = [sys.argv[1] if len(sys.argv) > 1 else "dotnet", str(API)]
        if staging_connection:
            env["ConnectionStrings__StagingLearning"] = staging_connection
            if journal_connection:
                env["ConnectionStrings__StagingDeletionJournal"] = journal_connection
            env["LearningKeyDirectory"] = str(root / "keys")
            subprocess.run([*command, "--migrate"], cwd=root, env=env, check=True, timeout=30)
        with (root / "server.log").open("w+") as log:
            with subprocess.Popen(command,
                                  cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT) as server:
                try:
                    for _ in range(50):
                        if server.poll() is not None:
                            raise RuntimeError("The public server exited.")
                        try:
                            status, _, headers = request("/health", include_headers=True)
                            if status == 200 and headers.get("x-verbulio-revision") == revision:
                                break
                        except OSError:
                            pass
                        time.sleep(0.1)
                    else:
                        raise RuntimeError("The expected public release did not start.")
                    yield
                except Exception:
                    log.seek(0)
                    print(log.read(), file=sys.stderr)
                    raise
                finally:
                    server.terminate()
                    server.wait(timeout=5)


def main():
    with site("a" * 40):
        verify()
    print("Public runtime passed: loopback, revision-aware health and closed account API.")
    if connection := os.environ.get("ConnectionStrings__Learning"):
        journal = None
        if container := os.environ.get("VERBULIO_POSTGRES_CONTAINER"):
            subprocess.run(["docker", "exec", container, "createdb", "-U", "postgres",
                            "verbulio_staging_test_deletions"], check=True, timeout=30)
            journal = connection.replace("Database=verbulio;", "Database=verbulio_staging_test_deletions;")
            assert journal != connection
        with site("b" * 40, connection, journal):
            assert request("/health", {"Host": HOST})[0] == 400
            assert request("/api/v1/local-account/session")[0] == 401
            if journal:
                assert request("/api/v1/local-account/delete", method="POST", body="{}",
                               headers=FORWARDED | {"Content-Type": "application/json"})[0] == 401
            status, _, headers = request("/api/v1/local-learning/identity",
                                         headers=FORWARDED | {"X-Verbulio-Client": "local-learning"},
                                         method="POST", include_headers=True)
            assert status == 204 and "; secure" in headers["set-cookie"].lower()
            cookie = headers["set-cookie"].split(";", 1)[0]
            assert request("/api/v1/local-placement/tests", method="POST",
                           headers=FORWARDED | {"Cookie": cookie, "X-Verbulio-Client": "local-learning"})[0] == 200
        print("Staging runtime passed: trusted HTTPS, Secure guest cookie, placement API and account authorization.")



if __name__ == "__main__":
    main()
