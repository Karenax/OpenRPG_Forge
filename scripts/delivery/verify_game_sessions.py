"""Run the installed launcher's real game command twice, preserving old captures.

This checks native startup and sustained runtime, not human visual acceptance.
Only process handles obtained from the two returned launch receipts are stopped.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import time

from build_delivery import digest, write


class OwnedProcess:
    def __init__(self, pid, expected_executable):
        self.api = ctypes.WinDLL('kernel32', use_last_error=True)
        self.api.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        self.api.OpenProcess.restype = wintypes.HANDLE
        self.api.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        self.api.WaitForSingleObject.restype = wintypes.DWORD
        self.api.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
        self.api.TerminateProcess.argtypes = [wintypes.HANDLE, wintypes.UINT]
        self.api.CloseHandle.argtypes = [wintypes.HANDLE]
        self.handle = self.api.OpenProcess(0x100000 | 0x1000 | 1, False, pid)
        if not self.handle:
            raise RuntimeError(f'Game {pid} exited before verification: {ctypes.get_last_error()}')
        buffer = ctypes.create_unicode_buffer(32768)
        size = wintypes.DWORD(len(buffer))
        if not self.api.QueryFullProcessImageNameW(self.handle, 0, buffer, ctypes.byref(size)):
            self.api.CloseHandle(self.handle)
            raise RuntimeError('Cannot confirm test game executable')
        if Path(buffer.value).resolve() != expected_executable.resolve():
            self.api.CloseHandle(self.handle)
            raise RuntimeError('Receipt PID is not the expected installed game; do not stop it')

    def alive(self):
        return self.api.WaitForSingleObject(self.handle, 0) == 258

    def close(self):
        if self.alive():
            if not self.api.TerminateProcess(self.handle, 0):
                raise RuntimeError('Could not stop test-owned game')
            self.api.WaitForSingleObject(self.handle, 10000)
        self.api.CloseHandle(self.handle)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--seconds', type=int, default=60)
    args = parser.parse_args()
    root = args.root.resolve()
    args.evidence.mkdir(parents=True, exist_ok=False)
    head = json.loads((root/'installation.json').read_bytes())
    user = Path(head['user_root'])
    captures = user/'captures'
    previous = {str(p): digest(p) for p in captures.rglob('*') if p.is_file()}
    write(args.evidence/'previous-captures.json', previous)
    env = dict(os.environ, FORGE_DELIVERY_USER_ROOT=str(user), RUST_LOG='info')
    plan_run = subprocess.run([str(root/'forge-delivery.exe'), 'launch-plan', str(root), 'game'],
                              env=env, capture_output=True, text=True, encoding='utf-8', check=True)
    plan = json.loads(plan_run.stdout)
    if plan['arguments'][-1] != '--walk' or Path(plan['arguments'][1]).name == 'game.png':
        raise RuntimeError('Interactive game recipe was not corrected')
    write(args.evidence/'game-plan.json', plan)
    results = []
    for index in (1, 2):
        old_logs = set((user/'logs').glob('game-*.log'))
        old_outputs = set(captures.iterdir())
        print(f'Launching real Tidewater session {index}', flush=True)
        command = [str(root/'Forge.exe'), '--verify-game-launch', '--root', str(root)]
        # A surviving Windows child may inherit a parent's pipe write handle.
        # Read a durable file after the launcher exits instead of awaiting EOF
        # from communicate() while the game remains running.
        stdout_path = args.evidence/f'launcher-{index}.stdout'
        stderr_path = args.evidence/f'launcher-{index}.stderr'
        with stdout_path.open('w', encoding='utf-8') as stdout, stderr_path.open('w', encoding='utf-8') as stderr:
            launcher = subprocess.Popen(command, env=env, stdout=stdout, stderr=stderr)
            launcher.wait()
        receipt = json.loads(stdout_path.read_text(encoding='utf-8'))
        write(args.evidence/f'launcher-{index}.json', receipt)
        if launcher.returncode or receipt.get('kind') != 'launch-started':
            raise RuntimeError(f'Launcher rejected actual game launch: {receipt}')
        process = OwnedProcess(receipt['process_id'], Path(plan['executable']))
        try:
            deadline = time.monotonic() + args.seconds
            while time.monotonic() < deadline:
                if not process.alive():
                    raise RuntimeError(f'Game session {index} exited during startup/runtime')
                time.sleep(.5)
            new_logs = set((user/'logs').glob(f'game-{launcher.pid}-*.log')) - old_logs
            if len(new_logs) != 1:
                raise RuntimeError('Cannot identify the single test game log')
            log = new_logs.pop()
            log_text = log.read_text(encoding='utf-8', errors='replace')
            (args.evidence/f'game-{index}.log').write_text(log_text, encoding='utf-8')
            if 'AdapterInfo' not in log_text or 'Creating new window' not in log_text:
                raise RuntimeError('GPU adapter and native window creation are not both evidenced')
            if any(error in log_text for error in ('Error:', ' ERROR ', 'panicked at', 'output already exists')):
                raise RuntimeError(f'Game session {index} reports an error; inspect {log}')
            outputs = sorted(p for p in set(captures.iterdir()) - old_outputs if p.name.startswith(f'game-{launcher.pid}-'))
            if not outputs or len({p.stem.split('.')[0] for p in outputs}) != 1:
                raise RuntimeError('Missing or ambiguous per-session outputs')
            results.append(dict(session=index, process_id=receipt['process_id'], log=str(log),
                                native_window_created=True, gpu_adapter_initialized=True,
                                interactive_walk=True, remained_running_seconds=args.seconds,
                                output_prefix=outputs[0].stem.split('.')[0],
                                outputs=[dict(path=str(p), bytes=p.stat().st_size) for p in outputs if p.is_file()]))
        finally:
            process.close()
        print(f'Tidewater session {index} passed native startup and {args.seconds}s runtime', flush=True)
    if results[0]['output_prefix'] == results[1]['output_prefix']:
        raise RuntimeError('Session output names collided')
    if any(not Path(p).is_file() or digest(Path(p)) != pin for p, pin in previous.items()):
        raise RuntimeError('A previous capture was modified or deleted')
    result = dict(status='PASS_TWO_INSTALLED_INTERACTIVE_GAME_SESSIONS', root=str(root),
                  launcher_sha256=digest(root/'Forge.exe'), game_sha256=digest(Path(plan['executable'])),
                  sessions=results, previous_captures_preserved=len(previous),
                  test_logging='RUST_LOG=info',
                  launch_command_is_renderer_request_launch=True,
                  physical_button_click=False, human_visual_acceptance=False)
    write(args.evidence/'verification.json', result)
    print(json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
