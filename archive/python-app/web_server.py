import os
import re
import sys
import json
import traceback
from http.server import HTTPServer, SimpleHTTPRequestHandler
from core.manager import PackageManager
from web_api import WebAPI

class QuantumWebHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        web_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "web")
        super().__init__(*args, directory=web_dir, **kwargs)

    def do_OPTIONS(self):
        self.send_response(204)
        self.end_headers()

    def do_POST(self):
        if self.path == "/api/invoke":
            try:
                content_len = int(self.headers.get("Content-Length", 0))
                raw_body = self.rfile.read(content_len).decode("utf-8")
                payload = json.loads(raw_body) if raw_body else {}
                cmd = payload.get("cmd")
                args = payload.get("args", {})

                api: WebAPI = self.server.web_api
                if not isinstance(cmd, str) or not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*", cmd) or cmd.startswith("_"):
                    resp_data = {"error": "That command is not available."}
                    status_code = 403
                elif not hasattr(api, cmd):
                    resp_data = {"error": f"Unknown API method: {cmd}"}
                    status_code = 404
                else:
                    func = getattr(api, cmd)
                    if isinstance(args, dict):
                        result = func(**args)
                    elif isinstance(args, list):
                        result = func(*args)
                    elif args is not None:
                        result = func(args)
                    else:
                        result = func()
                    resp_data = result
                    status_code = 200

                out_bytes = json.dumps(resp_data).encode("utf-8")
                self.send_response(status_code)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(out_bytes)))
                self.end_headers()
                self.wfile.write(out_bytes)
            except Exception as e:
                traceback.print_exc()
                err_data = json.dumps({"error": str(e)}).encode("utf-8")
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(err_data)))
                self.end_headers()
                self.wfile.write(err_data)
        else:
            self.send_response(404)
            self.end_headers()

    def end_headers(self):
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
        self.send_header("Pragma", "no-cache")
        self.send_header("Expires", "0")
        super().end_headers()

    def log_message(self, format, *args):
        # Concise logging to avoid spam
        pass

def run(port: int = 8089):
    manager = PackageManager()
    api = WebAPI(manager)
    server_address = ("127.0.0.1", port)
    httpd = HTTPServer(server_address, QuantumWebHandler)
    httpd.web_api = api
    print(f"[Quantum Web Server]: Active at http://localhost:{port}/ with Live WebAPI Bridge")
    sys.stdout.flush()
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()

if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8089
    run(port)
