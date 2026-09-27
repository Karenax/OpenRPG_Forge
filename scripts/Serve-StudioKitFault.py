"""Loopback-only HTTP fault fixture for the governed Studio kit.

The served directory is read-only. Faults are injected only into the selected
``/forge-kit/assets/*.glb`` response (or the two historical build-report cases).
Evidence is written to an explicit fresh directory on E:, never beside the kit.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import secrets
import time
from dataclasses import dataclass
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path, PurePosixPath
from threading import Event, Thread
from urllib.parse import unquote, urlsplit


MAX_GLB_BYTES = 64 * 1024 * 1024
MAX_STREAM_SECONDS = 60.0
GLB_CONTENT_TYPE = "model/gltf-binary"
# A complete GLB container with a JSON chunk.  Its JSON is syntactically valid
# but intentionally lacks the required ``asset.version`` glTF declaration, so
# a governed client can pass transport/hash/header/chunk checks before its
# JSON/schema validation rejects it.
INVALID_GLB_PAYLOAD = (
    b"glTF"                              # magic
    b"\x02\x00\x00\x00"                  # version 2
    b"\x18\x00\x00\x00"                  # total length: 24 bytes
    b"\x04\x00\x00\x00"                  # JSON chunk length: 4 bytes
    b"JSON"                              # JSON chunk type
    b"{}  "                              # valid JSON, invalid glTF schema
)


def _safe_relative_path(value: str, *, suffix: str) -> str:
    path = PurePosixPath(value.replace("\\", "/"))
    if (
        path.is_absolute()
        or ".." in path.parts
        or not path.parts
        or any(part in ("", ".") for part in path.parts)
        or path.suffix.lower() != suffix
    ):
        raise ValueError(f"unsafe {suffix} path: {value!r}")
    return path.as_posix()


def _contained_file(root: Path, relative: str) -> Path:
    candidate = (root / relative).resolve(strict=True)
    try:
        candidate.relative_to(root)
    except ValueError as error:
        raise ValueError(f"path escapes served directory: {relative!r}") from error
    if not candidate.is_file() or candidate.is_symlink():
        raise ValueError(f"fixture input is not a regular file: {relative!r}")
    if candidate.stat().st_size > MAX_GLB_BYTES:
        raise ValueError(f"fixture GLB exceeds {MAX_GLB_BYTES} bytes: {relative!r}")
    return candidate


def _derived_invalid_build_manifest(config: "FixtureConfig") -> bytes:
    """Return a manifest pinned to the deliberately invalid response bytes.

    The fixture never alters the candidate kit.  This only changes the response
    for the target asset so the Studio reaches GLB preflight after hash checking.
    """
    assert config.glb_relative is not None
    manifest_path = config.root / "forge-kit" / "build.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict):
        raise ValueError("build manifest must be a JSON object")
    assets = manifest.get("assets")
    if not isinstance(assets, list):
        raise ValueError("build manifest has no assets array")
    expected_path = config.glb_relative.removeprefix("forge-kit/")
    digest = hashlib.sha256(INVALID_GLB_PAYLOAD).hexdigest()
    matches = 0
    for asset in assets:
        if not isinstance(asset, dict):
            continue
        glb = asset.get("glb")
        if isinstance(glb, dict) and glb.get("path") == expected_path:
            glb["sha256"] = digest
            matches += 1
    if matches != 1:
        raise ValueError(f"build manifest target count is {matches}, expected 1")
    return json.dumps(manifest, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


@dataclass(frozen=True)
class FixtureConfig:
    root: Path
    mode: str
    glb_relative: str | None = None
    alternate_relative: str | None = None
    chunk_bytes: int = 64 * 1024
    delay_seconds: float = 0.10
    truncate_bytes: int = 32

    def __post_init__(self) -> None:
        valid_modes = {
            "timeout", "oversize", "glb-alternate", "glb-invalid", "glb-truncated",
            "glb-interrupted", "glb-slow",
        }
        if self.mode not in valid_modes:
            raise ValueError(f"unsupported fault mode: {self.mode}")
        if not 1024 <= self.chunk_bytes <= 1024 * 1024:
            raise ValueError("chunk bytes must be between 1024 and 1048576")
        if not 0.001 <= self.delay_seconds <= 1.0:
            raise ValueError("delay must be between 1 and 1000 milliseconds")
        if self.mode.startswith("glb-"):
            if self.glb_relative is None:
                raise ValueError("GLB modes require --glb-path")
            _contained_file(self.root, self.glb_relative)
        if self.mode == "glb-alternate":
            if self.alternate_relative is None:
                raise ValueError("glb-alternate requires --alternate-glb")
            alternate = _contained_file(self.root, self.alternate_relative)
            target = _contained_file(self.root, self.glb_relative or "")
            if alternate.samefile(target):
                raise ValueError("alternate GLB must differ from the requested GLB")
        if self.mode == "glb-slow":
            size = _contained_file(self.root, self.glb_relative or "").stat().st_size
            if math.ceil(size / self.chunk_bytes) * self.delay_seconds > MAX_STREAM_SECONDS:
                raise ValueError("slow stream would exceed the 60 second fixture limit")
        if self.mode in {"glb-truncated", "glb-interrupted"} and self.truncate_bytes < 1:
            raise ValueError("truncate bytes must be positive")

    @property
    def request_path(self) -> str | None:
        return None if self.glb_relative is None else "/" + self.glb_relative


class FaultServer(ThreadingHTTPServer):
    allow_reuse_address = True
    daemon_threads = True


class Handler(SimpleHTTPRequestHandler):
    server: FaultServer

    def log_message(self, format: str, *args: object) -> None:
        print("http: " + (format % args), flush=True)

    def do_GET(self) -> None:
        requested = unquote(urlsplit(self.path).path)
        config: FixtureConfig = self.server.fixture
        if requested == getattr(self.server, "stop_path", None):
            self.send_response(204)
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", "0")
            self.end_headers()
            print("fixture: graceful loopback stop requested", flush=True)
            Thread(target=self.server.shutdown, daemon=True).start()
            return
        if requested == "/forge-kit/build.json" and config.mode in {
            "timeout", "oversize", "glb-invalid",
        }:
            self._serve_build_fault(config)
            return
        if requested == config.request_path:
            self._serve_glb_fault(config)
            return
        super().do_GET()

    def _begin_fault(self, *, length: int, mode: str) -> None:
        self.send_response(200)
        self.send_header("Content-Type", GLB_CONTENT_TYPE)
        self.send_header("Content-Length", str(length))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Forge-Kit-Fault", mode)
        self.end_headers()

    def _serve_build_fault(self, config: FixtureConfig) -> None:
        if config.mode == "glb-invalid":
            try:
                payload = _derived_invalid_build_manifest(config)
            except (OSError, ValueError, json.JSONDecodeError) as error:
                self.send_error(500, f"could not derive invalid fixture manifest: {error}")
                return
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Forge-Kit-Fault", config.mode)
            self.end_headers()
            self.wfile.write(payload)
            self.wfile.flush()
            print("fault: served in-memory manifest pinned to invalid GLB bytes", flush=True)
            return
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Forge-Kit-Fault", config.mode)
        self.end_headers()
        if config.mode == "timeout":
            self.wfile.write(b"{")
            self.wfile.flush()
            print("fault: build report starts then stalls for 25 seconds", flush=True)
            Event().wait(25)
            return
        try:
            for _ in range(32):
                self.wfile.write(b" " * (64 * 1024))
                self.wfile.flush()
            print("fault: build report supplies 2 MiB against the 1 MiB limit", flush=True)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            print("fault: client cancelled oversized build report", flush=True)

    def _serve_glb_fault(self, config: FixtureConfig) -> None:
        assert config.glb_relative is not None
        source = _contained_file(config.root, config.glb_relative)
        if config.mode == "glb-alternate":
            assert config.alternate_relative is not None
            source = _contained_file(config.root, config.alternate_relative)
            self._begin_fault(length=source.stat().st_size, mode=config.mode)
            self._copy(source)
            return
        if config.mode == "glb-invalid":
            self._begin_fault(length=len(INVALID_GLB_PAYLOAD), mode=config.mode)
            self.wfile.write(INVALID_GLB_PAYLOAD)
            self.wfile.flush()
            return
        source_size = source.stat().st_size
        if config.mode == "glb-truncated":
            sent = min(config.truncate_bytes, source_size)
            self._begin_fault(length=source_size, mode=config.mode)
            with source.open("rb") as stream:
                self.wfile.write(stream.read(sent))
                self.wfile.flush()
            self.close_connection = True
            print(f"fault: {config.mode} after {sent}/{source_size} bytes", flush=True)
            return
        if config.mode == "glb-interrupted":
            sent = min(config.truncate_bytes, source_size)
            self._begin_interrupted_fault(source_size, config.mode)
            with source.open("rb") as stream:
                self.wfile.write(stream.read(sent))
                self.wfile.flush()
            self.close_connection = True
            print(
                f"fault: chunked {config.mode} after {sent}/{source_size} bytes",
                flush=True,
            )
            return
        self._begin_fault(length=source_size, mode=config.mode)
        self._copy(
            source,
            delay_seconds=config.delay_seconds if config.mode == "glb-slow" else 0.0,
            chunk_bytes=config.chunk_bytes,
        )

    def _begin_interrupted_fault(self, length: int, mode: str) -> None:
        """Start a chunked response then cut its declared chunk short.

        This is intentionally different from the Content-Length mismatch used by
        ``glb-truncated``: clients can assert the transfer encoding and exercise
        their chunked-decoder interruption path.
        """
        self.send_response(200)
        self.send_header("Content-Type", GLB_CONTENT_TYPE)
        self.send_header("Transfer-Encoding", "chunked")
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Forge-Kit-Fault", mode)
        self.end_headers()
        self.wfile.write(f"{length:X}\r\n".encode("ascii"))
        self.wfile.flush()

    def _copy(self, source: Path, *, delay_seconds: float = 0.0, chunk_bytes: int = 64 * 1024) -> None:
        try:
            with source.open("rb") as stream:
                while chunk := stream.read(chunk_bytes):
                    self.wfile.write(chunk)
                    self.wfile.flush()
                    if delay_seconds:
                        time.sleep(delay_seconds)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            print("fault: client cancelled GLB download", flush=True)


def _evidence_directory(value: str) -> Path:
    directory = Path(value).resolve()
    if directory.drive.upper() != "E:":
        raise ValueError("fixture evidence must stay on E:")
    if directory.exists():
        raise ValueError("fixture evidence directory must be fresh")
    return directory


def _write_json(path: Path, payload: dict[str, object]) -> None:
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _fixture_ledger(
    config: FixtureConfig, *, port: int, status: str, stop_path: str
) -> dict[str, object]:
    """Return reproducible fixture parameters and the local stop capability."""
    framing = {
        "glb-interrupted": "chunked-declared-chunk-cut-short",
        "glb-truncated": "content-length-original-connection-close-early",
    }.get(config.mode, "content-length")
    semantics = {
        "timeout": "build-json-prefix-then-stall",
        "oversize": "build-json-exceeds-client-limit",
        "glb-alternate": "serve-valid-alternate-glb",
        "glb-invalid": "pinned-valid-glb-container-json-schema-invalid",
        "glb-truncated": "content-length-transfer-truncated",
        "glb-interrupted": "chunked-transfer-interrupted",
        "glb-slow": "delayed-glb-chunks",
    }[config.mode]
    return {
        "status": status,
        "mode": config.mode,
        "port": port,
        "directory": str(config.root),
        "request_path": config.request_path,
        "glb_path": config.glb_relative,
        "alternate_glb_path": config.alternate_relative,
        "chunk_bytes": config.chunk_bytes,
        "delay_ms": round(config.delay_seconds * 1000),
        "truncate_bytes": config.truncate_bytes,
        "response_framing": framing,
        "fault_semantics": semantics,
        "stop_endpoint": "loopback-only",
        "stop_path": stop_path,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", required=True, help="read-only candidate web root")
    parser.add_argument("--port", type=int, required=True, help="loopback port (0 selects one)")
    parser.add_argument("--mode", required=True, choices=(
        "timeout", "oversize", "glb-alternate", "glb-invalid", "glb-truncated",
        "glb-interrupted", "glb-slow",
    ))
    parser.add_argument("--glb-path", help="candidate-relative target, e.g. forge-kit/assets/tree.glb")
    parser.add_argument("--alternate-glb", help="candidate-relative valid GLB for glb-alternate")
    parser.add_argument("--chunk-bytes", type=int, default=64 * 1024)
    parser.add_argument("--delay-ms", type=int, default=100)
    parser.add_argument("--truncate-bytes", type=int, default=32)
    parser.add_argument("--evidence-dir", required=True, help="fresh output directory on E:")
    args = parser.parse_args()
    if not 0 <= args.port <= 65535:
        parser.error("port must be between 0 and 65535")
    try:
        root = Path(args.directory).resolve(strict=True)
        if not root.is_dir() or root.is_symlink():
            raise ValueError("directory must be a regular served directory")
        glb_relative = None if args.glb_path is None else _safe_relative_path(args.glb_path, suffix=".glb")
        alternate_relative = None if args.alternate_glb is None else _safe_relative_path(args.alternate_glb, suffix=".glb")
        config = FixtureConfig(
            root=root, mode=args.mode, glb_relative=glb_relative,
            alternate_relative=alternate_relative, chunk_bytes=args.chunk_bytes,
            delay_seconds=args.delay_ms / 1000.0, truncate_bytes=args.truncate_bytes,
        )
        evidence = _evidence_directory(args.evidence_dir)
    except ValueError as error:
        parser.error(str(error))
    evidence.mkdir(parents=True)
    server = FaultServer(("127.0.0.1", args.port), partial(Handler, directory=str(root)))
    server.fixture = config
    server.stop_path = "/__forge_fixture_stop__/" + secrets.token_urlsafe(24)
    started = _fixture_ledger(
        config, port=server.server_port, status="RUNNING", stop_path=server.stop_path
    )
    started["started_utc"] = time.time()
    _write_json(evidence / "fixture-start.json", started)
    print(f"loopback fixture {server.server_port}: {config.mode}", flush=True)
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        print("fixture: stop requested", flush=True)
    finally:
        server.shutdown()
        server.server_close()
        started["status"] = "STOPPED"
        started["stopped_utc"] = time.time()
        _write_json(evidence / "fixture-stop.json", started)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
