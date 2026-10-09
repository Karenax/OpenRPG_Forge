"""Serve the real existing Studio frontend locally; no IPC or licence emulation."""
import base64
import datetime
import hashlib
import http.server
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import urllib.parse

DIST = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust\apps\forge_studio\dist")
EXE = Path(r"E:\OpenRPG_Forge\.build\cargo-target\debug\forge-studio.exe")
ROOT = Path(r"F:\OpenRPG_ImportWork\asset-library-editor-frontend-review-v2")
FILES = ["index.html", "forge_studio.js", "forge_studio_bg.wasm", "styles.css"]

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def pin(path):
    data = path.read_bytes()
    return {"path": str(path), "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}

def save(name, data):
    target = ROOT / name
    temporary = target.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(data, indent=2), encoding="utf-8")
    os.replace(temporary, target)

def prepare():
    if ROOT.exists():
        raise RuntimeError("existing review preserved; inspect its exact server job")
    with socket.socket() as check:
        check.bind(("127.0.0.1", 1420))
    pins = {name: pin(DIST / name) for name in FILES}
    html = (DIST / "index.html").read_text(encoding="utf-8")
    for tag in re.findall(r"<link[^>]*>", html):
        path = re.search(r'href="/([^"]+)"', tag)
        integrity = re.search(r'integrity="sha384-([^"]+)"', tag)
        if path and integrity:
            name = path.group(1)
            if name not in FILES:
                raise RuntimeError("frontend references an unpinned integrity resource")
            actual = base64.b64encode(hashlib.sha384((DIST / name).read_bytes()).digest()).decode()
            if actual != integrity.group(1):
                raise RuntimeError(f"frontend integrity differs: {name}")
    exe = pin(EXE)
    (ROOT).mkdir(parents=True)
    save("PREPARED.v1.json", {"prepared_utc": now(), "frontend": pins, "executable": exe,
        "url": "http://localhost:1420", "current_source_build_qualified": False,
        "studio_window_observed": False, "library_visually_verified": False, "full_import": False})
    with (ROOT / "server.log").open("ab", buffering=0) as output:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "serve"], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=output, stderr=output, creationflags=subprocess.CREATE_NO_WINDOW)
    save("SERVER-JOB.v1.json", {"pid": child.pid, "created_utc": now(), "script": str(Path(__file__).resolve())})
    print(json.dumps({"prepared": True, "server_pid": child.pid, "root": str(ROOT)}))

def serve():
    proof = json.loads((ROOT / "PREPARED.v1.json").read_bytes())
    content = {}
    for name, expected in proof["frontend"].items():
        data = (DIST / name).read_bytes()
        if hashlib.sha256(data).hexdigest() != expected["sha256"]:
            raise RuntimeError("frontend changed before server startup")
        content[name] = data
    types = {"index.html": "text/html; charset=utf-8", "styles.css": "text/css; charset=utf-8",
             "forge_studio.js": "text/javascript; charset=utf-8", "forge_studio_bg.wasm": "application/wasm"}
    class Handler(http.server.BaseHTTPRequestHandler):
        def do_GET(self):
            path = urllib.parse.urlsplit(self.path).path
            name = "index.html" if path == "/" else path.lstrip("/")
            if name not in content:
                self.send_error(404)
                return
            self.send_response(200)
            self.send_header("Content-Type", types[name])
            self.send_header("Content-Length", str(len(content[name])))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(content[name])
    with http.server.ThreadingHTTPServer(("127.0.0.1", 1420), Handler) as server:
        save("SERVER-READY.v1.json", {"ready_utc": now(), "pid": os.getpid(), "url": proof["url"],
            "frontend_snapshot_in_memory": True, "real_frontend_only": True,
            "ipc_emulated": False, "licence_emulated": False})
        server.serve_forever()

if __name__ == "__main__":
    {"prepare": prepare, "serve": serve}[sys.argv[1]]()
