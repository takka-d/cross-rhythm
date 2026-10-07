"""Cross Rhythm local ADTOF companion. Audio stays on this computer."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit, parse_qs
import argparse, importlib.util, json, os, secrets, shutil, subprocess, sys, tempfile, threading, time

ORIGINS = {"https://takka-d.github.io", "https://takka-note.com", "http://localhost:8775", "http://127.0.0.1:8775"}
MAX_BYTES = 256 * 1024 * 1024
JOBS = {}
LOCK = threading.Lock()
ROOT = Path(tempfile.mkdtemp(prefix="cross-rhythm-adtof-"))

def installed():
    return importlib.util.find_spec("adtof_pytorch") is not None

def read_job(job):
    result = job["dir"] / "result.json"
    if result.exists():
        return json.loads(result.read_text(encoding="utf-8"))
    if job.get("cancelled"):
        return {"status": "cancelled"}
    progress = job["dir"] / "progress.json"
    if job["process"].poll() is not None:
        return {"status": "error", "error": "Analysis stopped. See the companion log."}
    try:
        return json.loads(progress.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"status": "running", "progress": 0.02, "stage": "Loading model"}

class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    def allowed(self):
        host = self.headers.get("Host", "").split(":")[0]
        origin = self.headers.get("Origin")
        return host in ("127.0.0.1", "localhost") and (origin is None or origin in ORIGINS)
    def respond(self, code, obj):
        data = json.dumps(obj, ensure_ascii=False).encode()
        self.send_response(code)
        origin = self.headers.get("Origin")
        if origin in ORIGINS:
            self.send_header("Access-Control-Allow-Origin", origin)
            self.send_header("Vary", "Origin")
            self.send_header("Access-Control-Allow-Private-Network", "true")
            self.send_header("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(data)
        self.close_connection = True
    def do_OPTIONS(self):
        self.respond(200 if self.allowed() else 403, {})
    def do_GET(self):
        if not self.allowed(): return self.respond(403, {"error": "Origin not allowed"})
        path = urlsplit(self.path).path
        if path == "/health":
            return self.respond(200, {"service": "cross-rhythm-adtof", "protocol": 1, "adtof_available": installed(), "engine": "ADTOF-pytorch CPU"})
        key = path.removeprefix("/jobs/")
        with LOCK: job = JOBS.get(key)
        if path.startswith("/jobs/") and job:
            return self.respond(200, read_job(job))
        self.respond(404, {"error": "Job not found"})
    def do_POST(self):
        if not self.allowed(): return self.respond(403, {"error": "Origin not allowed"})
        url = urlsplit(self.path)
        if url.path != "/jobs": return self.respond(404, {"error": "Not found"})
        if not installed(): return self.respond(503, {"error": "Run Setup ADTOF first"})
        try: size = int(self.headers.get("Content-Length", "0"))
        except ValueError: size = 0
        if size < 44 or size > MAX_BYTES: return self.respond(413, {"error": "Audio must be 44 bytes to 256 MB"})
        # Only binary requests are accepted; a third-party HTML form cannot launch jobs.
        if self.headers.get("Content-Type", "").split(";")[0] != "application/octet-stream":
            return self.respond(415, {"error": "Binary audio required"})
        with LOCK:
            for key, old in list(JOBS.items()):
                if old["process"].poll() is not None and time.time() - old["created"] > 600:
                    shutil.rmtree(old["dir"]); del JOBS[key]
            if any(j["process"].poll() is None for j in JOBS.values()):
                return self.respond(409, {"error": "Another analysis is running"})
            key = secrets.token_hex(16); folder = ROOT / key; folder.mkdir()
            name = parse_qs(url.query).get("name", ["audio.wav"])[0]
            extension = Path(name).suffix.lower()
            if extension not in (".wav", ".mp3", ".flac", ".ogg", ".aif", ".aiff", ".m4a"): extension = ".audio"
            path = folder / ("audio" + extension)
            self.connection.settimeout(60)
            try:
                with path.open("wb") as stream:
                    left = size
                    while left:
                        chunk = self.rfile.read(min(left, 1024 * 1024))
                        if not chunk: raise IOError("Incomplete audio")
                        stream.write(chunk); left -= len(chunk)
                log = (folder / "worker.log").open("wb")
                process = subprocess.Popen([sys.executable, str(Path(__file__).with_name("worker.py")), str(path)], stdout=log, stderr=subprocess.STDOUT, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
                log.close()
                JOBS[key] = {"dir": folder, "process": process, "created": time.time()}
            except Exception:
                shutil.rmtree(folder); return self.respond(400, {"error": "Could not receive audio"})
        self.respond(202, {"id": key, "status": "running"})
    def do_DELETE(self):
        if not self.allowed(): return self.respond(403, {"error": "Origin not allowed"})
        key = urlsplit(self.path).path.removeprefix("/jobs/")
        with LOCK:
            job = JOBS.get(key)
            if not job: return self.respond(404, {"error": "Job not found"})
            if job["process"].poll() is None:
                job["process"].terminate(); job["process"].wait(timeout=10)
            shutil.rmtree(job["dir"]); del JOBS[key]
        self.respond(200, {"status": "cancelled"})

def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    print(f"Cross Rhythm ADTOF: http://127.0.0.1:{args.port} (local only)", flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally:
        server.server_close()
        for job in JOBS.values():
            if job["process"].poll() is None: job["process"].terminate(); job["process"].wait(timeout=10)
        shutil.rmtree(ROOT)
if __name__ == "__main__": main()
