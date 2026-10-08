"""Dialogue review tool. Run:  python tools/dialogue_review/serve.py
Then open http://127.0.0.1:8765 (it opens by itself). Edits are written straight to
game/dialogue/*.yaml and the brand/show name files; commit them like any other change."""
import json
import os
import sys
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

sys.path.insert(0, os.path.dirname(__file__))
import store  # noqa: E402

PORT = int(os.environ.get("PORT", "8765"))
PAGE = os.path.join(os.path.dirname(__file__), "index.html")


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, body, ctype="application/json"):
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype + "; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path in ("/", "/index.html"):
            with open(PAGE, "rb") as f:
                return self._send(200, f.read(), "text/html")
        if self.path == "/api/all":
            return self._send(200, {"lines": store.load_lines(), "names": store.load_names()})
        self._send(404, {"error": "not found"})

    def do_POST(self):
        try:
            body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
            if self.path == "/api/line":
                hit = store.update_line(body["file"], body["id"], text=body.get("text"), status=body.get("status"), notes=body.get("notes"))
                return self._send(200, hit)
            if self.path == "/api/name":
                hit = store.update_name(body["kind"], body["id"], name=body.get("name"), status=body.get("status"), notes=body.get("notes"))
                return self._send(200, hit)
            self._send(404, {"error": "not found"})
        except (KeyError, ValueError) as e:
            self._send(400, {"error": str(e)})

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    url = f"http://127.0.0.1:{PORT}"
    print(f"Dialogue review running at {url}  (Ctrl+C to stop)")
    if "--no-browser" not in sys.argv:
        webbrowser.open(url)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
