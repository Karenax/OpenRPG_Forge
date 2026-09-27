"""Focused HTTP coverage for Serve-StudioKitFault; no Studio or browser launch."""
from __future__ import annotations

import http.client
import importlib.util
import json
import sys
import tempfile
import threading
import time
import unittest
from functools import partial
from pathlib import Path


SCRIPT = Path(__file__).with_name("Serve-StudioKitFault.py")
SPEC = importlib.util.spec_from_file_location("studio_kit_fault", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
FAULT = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = FAULT
SPEC.loader.exec_module(FAULT)


class StudioKitFaultHttpTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        assets = self.root / "forge-kit" / "assets"
        assets.mkdir(parents=True)
        self.primary = assets / "primary.glb"
        self.primary.write_bytes(b"glTF" + b"primary-payload" * 512)
        self.alternate = assets / "alternate.glb"
        self.alternate.write_bytes(b"glTF" + b"alternate-payload" * 48)
        self.build = self.root / "forge-kit" / "build.json"
        self.build.write_text(
            json.dumps({
                "assets": [
                    {"id": "primary", "glb": {
                        "path": "assets/primary.glb", "sha256": "0" * 64,
                    }},
                    {"id": "alternate", "glb": {
                        "path": "assets/alternate.glb", "sha256": "1" * 64,
                    }},
                ],
            }),
            encoding="utf-8",
        )
        self.server = None
        self.thread = None

    def tearDown(self) -> None:
        if self.server is not None:
            self.server.shutdown()
            self.server.server_close()
            assert self.thread is not None
            self.thread.join(timeout=2)
        self.temporary.cleanup()

    def start(self, mode: str, **kwargs: object) -> int:
        config = FAULT.FixtureConfig(
            root=self.root.resolve(), mode=mode,
            glb_relative="forge-kit/assets/primary.glb", **kwargs,
        )
        self.server = FAULT.FaultServer(("127.0.0.1", 0), partial(FAULT.Handler, directory=str(self.root)))
        self.server.fixture = config
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        return self.server.server_port

    def request(self, port: int, path: str) -> tuple[int, dict[str, str], bytes]:
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
        connection.request("GET", path)
        response = connection.getresponse()
        try:
            body = response.read()
        finally:
            headers = dict(response.getheaders())
            status = response.status
            connection.close()
        return status, headers, body

    def test_alternate_glb_keeps_binary_headers_and_changes_body(self) -> None:
        port = self.start("glb-alternate", alternate_relative="forge-kit/assets/alternate.glb")
        status, headers, body = self.request(port, "/forge-kit/assets/primary.glb")
        self.assertEqual(status, 200)
        self.assertEqual(headers["Content-Type"], FAULT.GLB_CONTENT_TYPE)
        self.assertEqual(headers["Cache-Control"], "no-store")
        self.assertEqual(headers["X-Forge-Kit-Fault"], "glb-alternate")
        self.assertEqual(body, self.alternate.read_bytes())
        self.assertNotEqual(body, self.primary.read_bytes())

    def test_invalid_glb_uses_an_in_memory_manifest_pinned_to_invalid_bytes(self) -> None:
        before_manifest = self.build.read_bytes()
        before_primary = self.primary.read_bytes()
        port = self.start("glb-invalid")
        status, headers, manifest = self.request(port, "/forge-kit/build.json")
        self.assertEqual(status, 200)
        self.assertEqual(headers["X-Forge-Kit-Fault"], "glb-invalid")
        derived = json.loads(manifest)
        primary = next(asset for asset in derived["assets"] if asset["id"] == "primary")
        expected_hash = "ab7cb3261b3327eca0eed861fd7d0f73b784dbf7f1b4d839b48661a2878d6e63"
        self.assertEqual(
            primary["glb"]["sha256"],
            expected_hash,
        )
        self.assertEqual(FAULT.hashlib.sha256(FAULT.INVALID_GLB_PAYLOAD).hexdigest(), expected_hash)
        alternate = next(asset for asset in derived["assets"] if asset["id"] == "alternate")
        self.assertEqual(alternate["glb"]["sha256"], "1" * 64)
        status, headers, body = self.request(port, "/forge-kit/assets/primary.glb")
        self.assertEqual(status, 200)
        self.assertEqual(headers["Content-Length"], str(len(body)))
        self.assertEqual(headers["X-Forge-Kit-Fault"], "glb-invalid")
        self.assertEqual(body, FAULT.INVALID_GLB_PAYLOAD)
        self.assertGreaterEqual(len(body), 20)
        self.assertEqual(body[:4], b"glTF")
        self.assertEqual(int.from_bytes(body[4:8], "little"), 2)
        self.assertEqual(int.from_bytes(body[8:12], "little"), len(body))
        self.assertEqual(int.from_bytes(body[12:16], "little"), len(body) - 20)
        self.assertEqual(body[16:20], b"JSON")
        self.assertEqual(json.loads(body[20:]), {})
        self.assertEqual(self.build.read_bytes(), before_manifest)
        self.assertEqual(self.primary.read_bytes(), before_primary)

    def test_fixture_ledger_records_non_secret_fault_parameters(self) -> None:
        config = FAULT.FixtureConfig(
            root=self.root.resolve(), mode="glb-interrupted",
            glb_relative="forge-kit/assets/primary.glb",
            alternate_relative=None, chunk_bytes=4096, delay_seconds=0.125,
            truncate_bytes=17,
        )
        ledger = FAULT._fixture_ledger(
            config, port=43123, status="RUNNING",
            stop_path="/__forge_fixture_stop__/test-token",
        )
        self.assertEqual(ledger["glb_path"], "forge-kit/assets/primary.glb")
        self.assertIsNone(ledger["alternate_glb_path"])
        self.assertEqual(ledger["chunk_bytes"], 4096)
        self.assertEqual(ledger["delay_ms"], 125)
        self.assertEqual(ledger["truncate_bytes"], 17)
        self.assertEqual(ledger["response_framing"], "chunked-declared-chunk-cut-short")
        self.assertEqual(ledger["fault_semantics"], "chunked-transfer-interrupted")
        self.assertEqual(ledger["stop_endpoint"], "loopback-only")
        self.assertTrue(ledger["stop_path"].startswith("/__forge_fixture_stop__/"))
        self.assertNotEqual(ledger["stop_path"], "/__forge_fixture_stop__/")

    def test_main_writes_fault_parameters_to_both_lifecycle_ledgers(self) -> None:
        class ImmediateServer:
            def __init__(self, _address: object, _handler: object) -> None:
                self.server_port = 43123

            def serve_forever(self, *, poll_interval: float) -> None:
                self.poll_interval = poll_interval

            def shutdown(self) -> None:
                pass

            def server_close(self) -> None:
                pass

        evidence = self.root / "evidence"
        original_server = FAULT.FaultServer
        original_argv = sys.argv
        try:
            FAULT.FaultServer = ImmediateServer
            sys.argv = [
                str(SCRIPT), "--directory", str(self.root), "--port", "0",
                "--mode", "glb-slow", "--glb-path", "forge-kit/assets/primary.glb",
                "--chunk-bytes", "4096", "--delay-ms", "125", "--truncate-bytes", "17",
                "--evidence-dir", str(evidence),
            ]
            self.assertEqual(FAULT.main(), 0)
        finally:
            FAULT.FaultServer = original_server
            sys.argv = original_argv
        started = json.loads((evidence / "fixture-start.json").read_text(encoding="utf-8"))
        stopped = json.loads((evidence / "fixture-stop.json").read_text(encoding="utf-8"))
        for ledger, status in ((started, "RUNNING"), (stopped, "STOPPED")):
            self.assertEqual(ledger["status"], status)
            self.assertEqual(ledger["chunk_bytes"], 4096)
            self.assertEqual(ledger["delay_ms"], 125)
            self.assertEqual(ledger["truncate_bytes"], 17)
            self.assertEqual(ledger["glb_path"], "forge-kit/assets/primary.glb")
            self.assertIsNone(ledger["alternate_glb_path"])
            self.assertEqual(ledger["response_framing"], "content-length")
            self.assertEqual(ledger["fault_semantics"], "delayed-glb-chunks")
            self.assertEqual(ledger["stop_endpoint"], "loopback-only")
            self.assertTrue(ledger["stop_path"].startswith("/__forge_fixture_stop__/"))
            self.assertNotEqual(ledger["stop_path"], "/__forge_fixture_stop__/")
        self.assertEqual(started["stop_path"], stopped["stop_path"])

    def test_truncated_glb_advertises_original_length_then_ends(self) -> None:
        port = self.start("glb-truncated", truncate_bytes=11)
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
        connection.request("GET", "/forge-kit/assets/primary.glb")
        response = connection.getresponse()
        with self.assertRaises(http.client.IncompleteRead) as result:
            response.read()
        self.assertEqual(response.getheader("X-Forge-Kit-Fault"), "glb-truncated")
        self.assertEqual(result.exception.partial, self.primary.read_bytes()[:11])
        self.assertEqual(response.getheader("Content-Length"), str(self.primary.stat().st_size))
        connection.close()

    def test_interrupted_glb_is_a_distinct_transport_fault(self) -> None:
        port = self.start("glb-interrupted", truncate_bytes=17)
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
        connection.request("GET", "/forge-kit/assets/primary.glb")
        response = connection.getresponse()
        with self.assertRaises(http.client.IncompleteRead) as result:
            response.read()
        self.assertEqual(response.getheader("X-Forge-Kit-Fault"), "glb-interrupted")
        self.assertEqual(response.getheader("Transfer-Encoding"), "chunked")
        self.assertIsNone(response.getheader("Content-Length"))
        # http.client detects the broken chunk before exposing its partial body;
        # this deliberately differs from the Content-Length truncation above.
        self.assertEqual(result.exception.partial, b"")
        connection.close()

    def test_truncation_and_interruption_use_different_http_framing(self) -> None:
        port = self.start("glb-truncated", truncate_bytes=9)
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
        connection.request("GET", "/forge-kit/assets/primary.glb")
        response = connection.getresponse()
        self.assertEqual(response.getheader("Content-Length"), str(self.primary.stat().st_size))
        self.assertIsNone(response.getheader("Transfer-Encoding"))
        with self.assertRaises(http.client.IncompleteRead):
            response.read()
        connection.close()
        self.server.shutdown()
        self.server.server_close()
        assert self.thread is not None
        self.thread.join(timeout=2)
        self.server = None
        self.thread = None

        port = self.start("glb-interrupted", truncate_bytes=9)
        connection = http.client.HTTPConnection("127.0.0.1", port, timeout=3)
        connection.request("GET", "/forge-kit/assets/primary.glb")
        response = connection.getresponse()
        self.assertEqual(response.getheader("Transfer-Encoding"), "chunked")
        self.assertIsNone(response.getheader("Content-Length"))
        with self.assertRaises(http.client.IncompleteRead):
            response.read()
        connection.close()

    def test_slow_stream_delays_chunks_but_preserves_the_glb(self) -> None:
        port = self.start("glb-slow", chunk_bytes=1024, delay_seconds=0.04)
        started = time.monotonic()
        status, headers, body = self.request(port, "/forge-kit/assets/primary.glb")
        elapsed = time.monotonic() - started
        self.assertEqual(status, 200)
        self.assertEqual(headers["X-Forge-Kit-Fault"], "glb-slow")
        self.assertEqual(body, self.primary.read_bytes())
        self.assertGreaterEqual(elapsed, 0.08)

    def test_explicit_loopback_stop_endpoint_shuts_down_cleanly(self) -> None:
        port = self.start("glb-invalid")
        assert self.server is not None
        self.server.stop_path = "/__forge_fixture_stop__/test-token"
        status, headers, body = self.request(port, self.server.stop_path)
        self.assertEqual(status, 204)
        self.assertEqual(headers["Cache-Control"], "no-store")
        self.assertEqual(body, b"")
        assert self.thread is not None
        self.thread.join(timeout=2)
        self.assertFalse(self.thread.is_alive())

    def test_fault_target_cannot_escape_or_overwrite_served_tree(self) -> None:
        with self.assertRaisesRegex(ValueError, "unsafe"):
            FAULT._safe_relative_path("forge-kit/assets/../primary.glb", suffix=".glb")
        with self.assertRaisesRegex(ValueError, "alternate GLB"):
            FAULT.FixtureConfig(
                root=self.root.resolve(), mode="glb-alternate",
                glb_relative="forge-kit/assets/primary.glb",
                alternate_relative="forge-kit/assets/primary.glb",
            )
        self.assertEqual(self.primary.read_bytes()[:4], b"glTF")


if __name__ == "__main__":
    unittest.main(verbosity=2)
