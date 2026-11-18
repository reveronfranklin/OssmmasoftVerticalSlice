#!/usr/bin/env python3
"""R56: invoke a published analysis endpoint with a token from local settings.
The secret is supplied to curl through stdin, never through process arguments.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from urllib.parse import urlsplit

OPERATIONS = {"conexiones", "verificarConexion", "objetos", "describirObjeto", "fuente",
              "erroresCompilacion", "argumentos", "dependencias"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", required=True, help="API base, for example https://qa.example/api")
    parser.add_argument("--settings", type=Path, required=True, help="Local JSON file containing OracleAnalysis.ServiceAccess.Token")
    parser.add_argument("--operation", choices=sorted(OPERATIONS), default="conexiones")
    parser.add_argument("--body", default="{}", help="JSON object; no token here")
    parser.add_argument("--allow-http", action="store_true", help="Explicit HTTP allowance for a private QA environment")
    args = parser.parse_args()
    url = urlsplit(args.url)
    if url.scheme not in ({"https", "http"} if args.allow_http else {"https"}) or not url.netloc or url.username or url.password or url.query or url.fragment:
        parser.error("Use an HTTPS base URL without credentials, query or fragment (or --allow-http for QA).")
    try:
        settings = json.loads(args.settings.read_text(encoding="utf-8-sig"))
        token = os.environ.get("OracleAnalysis__ServiceAccess__Token") or settings["OracleAnalysis"]["ServiceAccess"]["Token"]
        body = json.loads(args.body)
        if not isinstance(body, dict):
            raise ValueError()
        if not isinstance(token, str) or len(token) < 32 or any(c in token for c in "\r\n\0"):
            raise ValueError()
    except (OSError, ValueError, KeyError, TypeError):
        parser.error("Cannot read a valid local token/settings or JSON body. No request was sent.")
    endpoint = args.url.rstrip("/") + "/OracleAnalisis/" + args.operation
    # JSON quoting is compatible with curl's double-quoted config syntax for these strings.
    lines = ["silent", "show-error", "fail-with-body", "connect-timeout = 10", "max-time = 60",
             'request = "POST"', "url = " + json.dumps(endpoint, ensure_ascii=False),
             'header = "Content-Type: application/json"',
             "header = " + json.dumps("X-Oracle-Analysis-Token: " + token, ensure_ascii=False),
             "data = " + json.dumps(json.dumps(body, ensure_ascii=False), ensure_ascii=False)]
    try:
        result = subprocess.run(["curl", "--config", "-"], input="\n".join(lines) + "\n", text=True, check=False)
        return result.returncode
    except FileNotFoundError:
        parser.error("curl is not installed.")


if __name__ == "__main__":
    sys.exit(main())
