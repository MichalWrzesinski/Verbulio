"""Verify the public runtime with disposable web assets and no database."""

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


def request(path, headers=FORWARDED, method="GET"):
    connection = http.client.HTTPConnection("127.0.0.1", 5444, timeout=2)
    try:
        connection.request(method, path, headers=headers)
        response = connection.getresponse()
        return response.status, response.read()
    finally:
        connection.close()


def verify():
    assert request("/") == (200, b"<h1>Public Verbulio fixture</h1>")
    assert request("/test") == request("/")
    assert request("/app.css") == (200, b"body {}")
    assert request("/missing.js")[0] == 404
    assert request("/api/v1/local-registration/accounts", method="POST")[0] == 404
    assert request("/api/v1/local-account/settings/account")[0] == 404
    assert request("/health", {"Host": HOST})[0] == 400
    assert request("/health", {**FORWARDED, "Host": "other.example.invalid"})[0] == 400
    assert request("/health", {**FORWARDED, "X-Forwarded-Proto": "http"})[0] == 400
    assert request("/health", {"Host": HOST, "X-Forwarded-Proto": "https"})[0] == 400


def main():
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        assets = root / "wwwroot"
        assets.mkdir()
        (assets / "index.html").write_text("<h1>Public Verbulio fixture</h1>", encoding="utf-8")
        (assets / "app.css").write_text("body {}", encoding="utf-8")
        env = {key: value for key, value in os.environ.items()
               if not key.startswith(("ConnectionStrings__", "ASPNETCORE_", "DOTNET_ENVIRONMENT"))}
        env["ASPNETCORE_ENVIRONMENT"] = "Public"
        with (root / "server.log").open("w+") as log:
            with subprocess.Popen([sys.argv[1] if len(sys.argv) > 1 else "dotnet", str(API)],
                                  cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT) as server:
                try:
                    for _ in range(50):
                        if server.poll() is not None:
                            raise RuntimeError("The public server exited.")
                        try:
                            if request("/health")[0] == 200:
                                break
                        except OSError:
                            pass
                        time.sleep(0.1)
                    else:
                        raise RuntimeError("The public server did not start.")
                    verify()
                except Exception:
                    log.seek(0)
                    print(log.read(), file=sys.stderr)
                    raise
                finally:
                    server.terminate()
                    server.wait(timeout=5)

    print("Public runtime passed: static site, SPA fallback, closed account API and HTTPS forwarding guard.")


if __name__ == "__main__":
    main()
