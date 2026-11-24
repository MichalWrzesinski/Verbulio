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


def request(path, headers=FORWARDED, method="GET", include_headers=False, port=5444):
    connection = http.client.HTTPConnection("127.0.0.1", port, timeout=2)
    try:
        connection.request(method, path, headers=headers)
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
def site(port, revision):
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        assets = root / "wwwroot"
        assets.mkdir()
        (assets / "index.html").write_text("<h1>Public Verbulio fixture</h1>", encoding="utf-8")
        (assets / "app.css").write_text("body {}", encoding="utf-8")
        (root / "REVISION").write_text(revision + "\n", encoding="utf-8")
        env = {key: value for key, value in os.environ.items()
               if not key.startswith(("ConnectionStrings__", "ASPNETCORE_", "DOTNET_ENVIRONMENT", "PublicSite__"))}
        env["ASPNETCORE_ENVIRONMENT"] = "Public"
        if port is not None:
            env["PublicSite__Port"] = str(port)
        with (root / "server.log").open("w+") as log:
            with subprocess.Popen([sys.argv[1] if len(sys.argv) > 1 else "dotnet", str(API)],
                                  cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT) as server:
                try:
                    for _ in range(50):
                        if server.poll() is not None:
                            raise RuntimeError("The public server exited.")
                        try:
                            status, _, headers = request("/health", include_headers=True, port=port or 5444)
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
    current = "a" * 40
    candidate = "b" * 40
    with site(None, current), site(5445, candidate):
        verify()
        for port, revision in ((5444, current), (5445, candidate)):
            status, _, headers = request("/health", include_headers=True, port=port)
            assert status == 200 and headers["x-verbulio-revision"] == revision
            assert request("/api/v1/local-registration/accounts", method="POST", port=port)[0] == 404
            assert request("/health", {"Host": HOST}, port=port)[0] == 400
            assert request("/", port=port) == (200, b"<h1>Public Verbulio fixture</h1>")

    print("Public runtime passed: two isolated loopback instances, revision-aware health and closed account API.")


if __name__ == "__main__":
    main()
