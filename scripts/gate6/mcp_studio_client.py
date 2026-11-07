"""Real stdio client for the isolated Studio recipe; UI approvals stay manual.

Starts the shared Studio binary as an MCP client would. It never calls a UI
approval API, edits Forge files or generates a plan with an LLM. The synthetic
proposal and the exact native host archives are explicitly identified as such.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import threading
import time


def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--executable", type=Path, required=True)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--run-id", default="001")
    parser.add_argument("--name", default="Village MCP")
    args = parser.parse_args()
    if not (len(args.run_id) == 3 and args.run_id.isascii() and args.run_id.isdigit()):
        raise ValueError("Use a three-digit evidence run ID")
    if not args.name.strip() or len(args.name) > 128:
        raise ValueError("Use a bounded synthetic target name")
    executable = args.executable.resolve(strict=True)
    root = args.root.resolve(strict=True)
    evidence = args.evidence.resolve(strict=True)
    if root.is_symlink() or not re.fullmatch(r"gate6-mcp-studio-ui(?:-[0-9]{3})?", root.name):
        raise ValueError("Use the dedicated synthetic MCP qualification fixture")
    workspace_file = root / "workspace-v1/authoring.json"
    before_workspace = json.loads(workspace_file.read_bytes())
    reference = before_workspace["head"]["projects"][0]["modules"][0]["zones"][0]
    objects = root / "workspace-v1/objects"
    before_archive = json.loads((objects / (reference["archive_hash"] + ".json")).read_bytes())
    prefix = "mcp-studio-client-" + args.run_id
    transcript = (evidence / (prefix + ".jsonl")).open("x", encoding="utf-8")
    errors = (evidence / (prefix + ".stderr.log")).open("xb")

    def record(kind, **fields):
        transcript.write(json.dumps({"utc": utc(), "kind": kind, **fields}, ensure_ascii=False) + "\n")
        transcript.flush()

    environment = os.environ.copy()
    environment["FORGE_AUTHORING_ROOT"] = str(root)
    process = subprocess.Popen(
        [str(executable), "--mcp-stdio"], cwd=executable.parent, env=environment,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
    )
    record("started", pid=process.pid, executable=str(executable), root=str(root),
           executable_sha256=hashlib.sha256(executable.read_bytes()).hexdigest(),
           client_kind="synthetic MCP test driver; no LLM generation")
    print(json.dumps({"phase": "started", "pid": process.pid}), flush=True)
    replies = queue.Queue()

    def read_replies():
        while True:
            line = process.stdout.readline(512 * 1024 + 2)
            if not line:
                replies.put(RuntimeError("Studio stdout closed"))
                return
            try:
                if len(line) > 512 * 1024:
                    raise ValueError("Oversized Studio response")
                replies.put(json.loads(line))
            except (ValueError, UnicodeError) as error:
                replies.put(error)
                return

    threading.Thread(target=read_replies, daemon=True).start()
    identifier = 0

    def rpc(method, params):
        nonlocal identifier
        identifier += 1
        request = {"jsonrpc": "2.0", "id": identifier, "method": method, "params": params}
        process.stdin.write(json.dumps(request, ensure_ascii=False).encode("utf-8") + b"\n")
        process.stdin.flush()
        reply = replies.get(timeout=30)
        if isinstance(reply, Exception):
            raise reply
        assert reply.get("id") == identifier and reply.get("jsonrpc") == "2.0", reply
        record("rpc", request=request, response=reply)
        return reply

    try:
        initialized = rpc("initialize", {"protocolVersion": "2025-11-25", "capabilities": {},
                                         "clientInfo": {"name": "forge-gate6-qualification", "version": "1"}})
        assert initialized["result"]["protocolVersion"] == "2025-11-25"
        process.stdin.write(b'{"jsonrpc":"2.0","method":"notifications/initialized"}\n')
        process.stdin.flush()
        assert rpc("tools/list", {})["result"]["tools"] == []
        print(json.dumps({"phase": "waiting_ui_scope", "seconds_maximum": 600}), flush=True)
        deadline = time.monotonic() + 600
        while time.monotonic() < deadline:
            tools = rpc("tools/list", {})["result"]["tools"]
            if tools:
                break
            time.sleep(3)
        else:
            raise TimeoutError("Authorize the displayed selection in Studio to continue")
        assert {tool["name"] for tool in tools} == {"forge_inspect_selection", "forge_propose_edits"}
        context = rpc("tools/call", {"name": "forge_inspect_selection", "arguments": {}})["result"]["structuredContent"]
        assert context["base_hash"] == reference["head_hash"]
        assert context["zone_properties"] and not context["placements"] and not context["objects"] and not context["dialogue_lines"]
        schema = next(tool for tool in tools if tool["name"] == "forge_propose_edits")["inputSchema"]
        proposal = {"context_hash": schema["properties"]["context_hash"]["enum"][0],
                    "summary": "Synthetic MCP qualification: rename only",
                    "missing_capabilities": [], "operations": [{"operation": "rename_zone", "name": args.name}]}
        staged = rpc("tools/call", {"name": "forge_propose_edits", "arguments": proposal})["result"]["structuredContent"]
        repeated = rpc("tools/call", {"name": "forge_propose_edits", "arguments": proposal})["result"]["structuredContent"]
        assert staged["command_id"] == repeated["command_id"] and not staged["applied"]
        assert workspace_file.read_bytes() == json.dumps(before_workspace, separators=(",", ":")).encode() or json.loads(workspace_file.read_bytes()) == before_workspace
        assert rpc("tools/call", {"name": "apply", "arguments": {}})["error"]["code"] == -32602
        print(json.dumps({"phase": "waiting_ui_apply_and_save", "command_id": staged["command_id"]}), flush=True)
        deadline = time.monotonic() + 600
        while time.monotonic() < deadline:
            workspace = json.loads(workspace_file.read_bytes())
            current = workspace["head"]["projects"][0]["modules"][0]["zones"][0]
            archive = json.loads((objects / (current["archive_hash"] + ".json")).read_bytes())
            if archive["head"]["corrections"]["name"] == args.name:
                break
            time.sleep(2)
        else:
            raise TimeoutError("Review, approve, apply and save the MCP proposal in Studio")
        assert archive["head"]["base"] == before_archive["head"]["base"]
        for key, value in before_archive["head"]["corrections"].items():
            if key != "name":
                assert archive["head"]["corrections"][key] == value, key
        receipts = [receipt for receipt in archive["receipts"] if receipt["request"]["command_id"] == staged["command_id"]]
        assert len(receipts) == 1 and receipts[0]["audit"]["actor"] == {"kind": "ai", "subject": "mcp-proposal"}
        assert rpc("tools/list", {})["result"]["tools"] == []
        record("source_verified", head_hash=current["head_hash"], command_id=staged["command_id"],
               unchanged_except_name=True, single_source_receipt=True)
        print(json.dumps({"phase": "source_verified_close_studio", "head_hash": current["head_hash"]}), flush=True)
        exit_code = process.wait(timeout=600)
        assert exit_code == 0, exit_code
        report = {"schema": "forge.gate6.mcp-studio-client.v1", "utc": utc(), "pid": process.pid,
                  "client_kind": "synthetic MCP test driver; no LLM generation", "stdio_process_tested": True,
                  "ui_authorization_required": True, "ui_apply_required": True, "same_proposal_reused": True,
                  "mcp_apply_tool_rejected": True, "unchanged_except_name": True, "single_source_receipt": True,
                  "head_hash": current["head_hash"], "command_id": staged["command_id"], "exit_code": exit_code,
                  "target_name": args.name,
                  "gate_closed": False}
        with (evidence / (prefix + ".result.json")).open("x", encoding="utf-8") as stream:
            json.dump(report, stream, indent=2)
            stream.write("\n")
        record("completed", **report)
        print(json.dumps({"phase": "completed", "gate_closed": False}), flush=True)
    except Exception as error:
        record("failed", error=str(error), pid=process.pid, process_exit_code=process.poll(),
               process_preserved=True)
        raise
    finally:
        transcript.close()
        errors.close()


if __name__ == "__main__":
    main()
