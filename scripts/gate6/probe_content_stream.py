"""One diagnostic stream of the saved synthetic request; never applies sources.

Reasoning is counted and discarded. Only final plan text and transport metrics
are retained. This probe is separate from the native provider qualification.
"""
import argparse
import datetime
import json
from pathlib import Path
import time
import urllib.error
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--request", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError("Existing evidence preserved")
    body = json.loads(args.request.read_text("utf-8-sig"))
    if body["chat_template_kwargs"]["enable_thinking"] is not True:
        raise ValueError("This probe requires explicit reasoning")
    body["stream"] = True
    body["stream_options"] = {"include_usage": True}
    started = time.monotonic()
    report = {
        "utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "purpose": "same full creation brief over SSE; not native or UI qualification",
        "model": body["model"], "reasoning_requested": True,
        "status": None, "events": 0, "reasoning_bytes_discarded": 0,
        "content_bytes": 0, "first_event_seconds": None,
        "first_content_seconds": None, "finish_reason": None,
        "usage": None, "done": False, "error": None,
        "gate_closed": False,
    }
    content = []
    last_progress = started
    endpoint = "http://192.168.1.41:8080/v1/chat/completions"
    request = urllib.request.Request(endpoint, data=json.dumps(body).encode(),
                                     headers={"Content-Type": "application/json"})
    try:
        with urllib.request.build_opener(urllib.request.ProxyHandler({})).open(request, timeout=480) as response:
            report["status"] = response.status
            report["content_type"] = response.headers.get("Content-Type")
            total_bytes = 0
            while True:
                if time.monotonic() - started > 480:
                    raise TimeoutError("480-second total diagnostic bound")
                line = response.readline(65538)
                if not line:
                    break
                total_bytes += len(line)
                if len(line) > 65536 or total_bytes > 2 * 1024 * 1024:
                    raise ValueError("Stream exceeds its bounded byte budget")
                if not line.startswith(b"data:"):
                    continue
                data = line[5:].strip()
                if data == b"[DONE]":
                    report["done"] = True
                    break
                chunk = json.loads(data)
                if "error" in chunk:
                    raise ValueError("Server emitted an error event; body withheld")
                report["events"] += 1
                elapsed = round(time.monotonic() - started, 3)
                if report["first_event_seconds"] is None:
                    report["first_event_seconds"] = elapsed
                if chunk.get("usage"):
                    report["usage"] = chunk["usage"]
                for choice in chunk.get("choices", []):
                    delta = choice.get("delta") or {}
                    for key in ("reasoning", "reasoning_content"):
                        report["reasoning_bytes_discarded"] += len((delta.get(key) or "").encode())
                    text = delta.get("content") or ""
                    if text:
                        if report["first_content_seconds"] is None:
                            report["first_content_seconds"] = elapsed
                        report["content_bytes"] += len(text.encode())
                        if report["content_bytes"] > 128 * 1024:
                            raise ValueError("Proposal exceeds 128 KiB")
                        content.append(text)
                    if choice.get("finish_reason"):
                        report["finish_reason"] = choice["finish_reason"]
                if time.monotonic() - last_progress >= 30:
                    print(json.dumps({key: report[key] for key in (
                        "events", "content_bytes", "reasoning_bytes_discarded", "finish_reason")}), flush=True)
                    last_progress = time.monotonic()
    except urllib.error.HTTPError as error:
        report["status"] = error.code
        report["error"] = "HTTP error; response body withheld"
    except (ValueError, TimeoutError, OSError) as error:
        report["error"] = str(error)
    report["elapsed_seconds"] = round(time.monotonic() - started, 3)
    text = "".join(content)
    try:
        report["proposal"] = json.loads(text)
    except ValueError:
        report["partial_content"] = text
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
    print(json.dumps({key: value for key, value in report.items()
                      if key not in ("proposal", "partial_content")}), flush=True)


if __name__ == "__main__":
    main()
