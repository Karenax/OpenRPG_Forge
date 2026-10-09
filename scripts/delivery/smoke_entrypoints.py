"""Probe installed native entry points; never automate or approve UI content."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import time

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delivery", type=Path, required=True)
    parser.add_argument("--installation", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--game-smoke", action="store_true")
    parser.add_argument("--only-game", action="store_true")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    results = []
    cli = args.delivery / "forge-delivery.exe"
    env = dict(os.environ, LOCALAPPDATA=str(args.output / "isolated-profile"))
    for app in (["game"] if args.only_game else ["launcher", "editor", "game"]):
        response = subprocess.run([str(cli), "launch-plan", str(args.installation), app], capture_output=True, text=True, encoding="utf-8", check=True)
        plan = json.loads(response.stdout)
        (args.output / f"{app}-launch-plan.json").write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")
        if app == "game" and not args.game_smoke:
            results.append(dict(app=app, status="LAUNCH_PLAN_VERIFIED_NO_GPU_PROBE"))
            continue
        child_env = env.copy()
        for key, value in plan["environment"].items():
            if value is None:
                child_env.pop(key, None)
            else:
                child_env[key] = value
        child_env["WEBVIEW2_USER_DATA_FOLDER"] = str(args.output / f"webview-{app}")
        arguments = plan["arguments"]
        if app == "game":
            arguments = ["--walk-smoke" if value == "--walk" else value for value in arguments]
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        with (args.output / f"{app}.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen([plan["executable"], *arguments], cwd=plan["working_directory"], env=child_env,
                                       stdin=subprocess.DEVNULL, stdout=log, stderr=log, startupinfo=startup)
            result = dict(app=app, process_id=process.pid)
            if app == "game":
                try:
                    code = process.wait(timeout=120)
                    result.update(status="NATIVE_SMOKE_EXIT_0" if code == 0 else "NATIVE_SMOKE_FAILED", exit_code=code)
                except subprocess.TimeoutExpired:
                    process.terminate()
                    process.wait(timeout=20)
                    result.update(status="NATIVE_SMOKE_TIMEOUT", exit_code=process.returncode)
            else:
                time.sleep(8)
                code = process.poll()
                result.update(status="PROCESS_ALIVE_NO_VISUAL_ACCEPTANCE" if code is None else "STARTUP_EXITED", exit_code=code)
                if code is None:
                    # This exact process was created by this probe; no other app is stopped.
                    process.terminate()
                    process.wait(timeout=20)
            results.append(result)
            print(json.dumps(result), flush=True)
    report = dict(schema="forge.delivery.entrypoint-smoke.v1", results=results, visual_acceptance=False, gate3_closed=False)
    (args.output / "entrypoints.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

if __name__ == "__main__":
    main()
