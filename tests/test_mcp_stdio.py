import json
import shutil
import subprocess
import tempfile
import unittest
from concurrent.futures import ThreadPoolExecutor, TimeoutError as FutureTimeoutError
from pathlib import Path


class McpStdioTests(unittest.TestCase):
    def test_official_sdk_serves_tools_over_stdio(self) -> None:
        repository = Path(__file__).resolve().parents[1]
        candidates = (
            repository / "apps" / "Forge.McpHost" / "bin" / "Release" / "net10.0" / "Forge.McpHost.dll",
            repository / "apps" / "Forge.McpHost" / "bin" / "Debug" / "net10.0" / "Forge.McpHost.dll",
        )
        host = next((candidate for candidate in candidates if candidate.is_file()), None)
        self.assertIsNotNone(host, "Forge.McpHost must be built before the stdio integration test.")
        source_workspace = repository / "apps" / "Forge.Runtime" / "content" / "crypt-of-ashes"
        temporary = tempfile.TemporaryDirectory(prefix="openrpg-forge-mcp-stdio-")
        workspace = Path(temporary.name) / "workspace"
        shutil.copytree(source_workspace, workspace)
        process = subprocess.Popen(
            ["dotnet", str(host), "--workspace", str(workspace)],
            cwd=repository,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        )
        reader = ThreadPoolExecutor(max_workers=1, thread_name_prefix="mcp-stdio-reader")

        def send(message: dict) -> None:
            assert process.stdin is not None
            process.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
            process.stdin.flush()

        def receive() -> dict:
            assert process.stdout is not None
            pending = reader.submit(process.stdout.readline)
            try:
                line = pending.result(timeout=15)
            except FutureTimeoutError:
                self.fail("The MCP host did not reply over stdio within 15 seconds.")
            self.assertTrue(line, "The MCP host closed stdio before replying.")
            return json.loads(line)

        try:
            send(
                {
                    "jsonrpc": "2.0",
                    "id": 1,
                    "method": "initialize",
                    "params": {
                        "protocolVersion": "2025-06-18",
                        "capabilities": {},
                        "clientInfo": {"name": "forge-test", "version": "1.0"},
                    },
                }
            )
            initialized = receive()
            self.assertEqual("2025-06-18", initialized["result"]["protocolVersion"])
            self.assertEqual("Forge.McpHost", initialized["result"]["serverInfo"]["name"])
            send({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})
            send({"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
            listed = receive()
            tools = {tool["name"] for tool in listed["result"]["tools"]}
            self.assertTrue(
                {
                    "get_project_summary",
                    "get_current_revision",
                    "get_project_diagnostics",
                    "plan_dialogue",
                    "apply_dialogue_plan",
                    "revert_dialogue_plan",
                    "compile_forge_script",
                    "test_forge_script",
                    "plan_script_binding",
                    "get_forge_script_api",
                    "get_forge_script_sources",
                    "plan_forge_script_source",
                    "apply_forge_script_source_plan",
                    "revert_forge_script_source_plan",
                    "get_forge_script_diagnostics",
                    "run_forge_script_tests",
                    "plan_forge_script_event_binding",
                    "apply_forge_script_event_binding_plan",
                    "revert_forge_script_event_binding_plan",
                    "forge_get_content_packages",
        "forge_plan_content_packages",
        "forge_get_localization",
        "forge_plan_localization",
        "forge_get_content_resources",
        "forge_plan_content_resource_aliases",
                }.issubset(tools)
            )
            send(
                {
                    "jsonrpc": "2.0",
                    "id": 3,
                    "method": "tools/call",
                    "params": {"name": "get_project_summary", "arguments": {}},
                }
            )
            summary = receive()
            self.assertFalse(summary["result"].get("isError", False))
            self.assertIn("crypt-of-ashes", json.dumps(summary["result"]))
            dialogue_request = {
                "schemaVersion": 1,
                "baseRevision": 0,
                "dialogueId": "71000000-0000-4000-8000-000000000001",
                "slug": "ai-variant",
                "displayName": "Variante IA",
                "entryNodeId": "71100000-0000-4000-8000-000000000001",
                "nodes": [
                    {
                        "nodeId": "71100000-0000-4000-8000-000000000001",
                        "speakerBlueprintId": "10000000-0000-4000-8000-000000000004",
                        "localizationKey": "dialogue.ai.variant",
                        "fallbackText": "La braise révèle un autre chemin.",
                    }
                ],
            }
            send(
                {
                    "jsonrpc": "2.0",
                    "id": 4,
                    "method": "tools/call",
                    "params": {"name": "plan_dialogue", "arguments": {"request": dialogue_request}},
                }
            )
            planned = json.loads(receive()["result"]["content"][0]["text"])
            self.assertTrue(planned["succeeded"])
            self.assertFalse(planned["applied"])
            self.assertEqual("add", planned["diff"]["operation"])
            self.assertEqual("remove", planned["inverse"]["operation"])
            send(
                {
                    "jsonrpc": "2.0",
                    "id": 5,
                    "method": "tools/call",
                    "params": {
                        "name": "apply_dialogue_plan",
                        "arguments": {"planId": planned["planId"]},
                    },
                }
            )
            applied = json.loads(receive()["result"]["content"][0]["text"])
            self.assertTrue(applied["applied"])
            send(
                {
                    "jsonrpc": "2.0",
                    "id": 6,
                    "method": "tools/call",
                    "params": {
                        "name": "revert_dialogue_plan",
                        "arguments": {"planId": planned["planId"]},
                    },
                }
            )
            reverted = json.loads(receive()["result"]["content"][0]["text"])
            self.assertTrue(reverted["reverted"])
            self.assertNotIn(dialogue_request["dialogueId"], (workspace / "project.json").read_text(encoding="utf-8"))
        finally:
            if process.stdin is not None and not process.stdin.closed:
                process.stdin.close()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            if process.stdout is not None:
                process.stdout.close()
            if process.stderr is not None:
                process.stderr.close()
            reader.shutdown(wait=True, cancel_futures=True)
            temporary.cleanup()


if __name__ == "__main__":
    unittest.main()
