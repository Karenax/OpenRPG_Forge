"""Seed only unused regenerable Library caches from an existing exact package lock.

Sources, Packages, project settings and capture helpers are never changed.
Each cache is published only after an exact source/target inventory and hashes.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'queued-unity-package-cache-seeding-v1'
SOURCE = W / 'unity-dungeon-original-shadergraph-compiler-v1/project'
PS = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
TARGETS = [
    W / 'unity-medieval-cloth-material-source-v1/project',
    W / 'unity-villa-normal-default-api-observation-v3/project',
    W / 'unity-delta-null-material-defaults-v2/native-american-village/project',
    W / 'unity-delta-null-material-defaults-v2/pirate-island/project',
]


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(value, indent=2), 'utf-8')
    for attempt in range(40):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(.25)


def idle(project):
    escaped = str(project).replace("'", "''")
    command = "$ErrorActionPreference='Stop';@(Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf('" + escaped + "',[System.StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count"
    result = subprocess.run([str(PS), '-NoProfile', '-Command', command], capture_output=True, text=True, check=True)
    assert result.stdout.strip() == '0', 'Project is active; preserve its existing cache'


def validate_project(project):
    assert project.resolve().is_relative_to(W.resolve()) and project.is_dir()
    idle(project)
    for relative in ['Packages/manifest.json', 'Packages/packages-lock.json']:
        assert sha(project / relative) == sha(SOURCE / relative), 'Different package manifest/lock; do not seed'
    assert not (project / 'Library/PackageCache').exists(), 'Existing cache preserved'


def prepare():
    assert not ROOT.exists(), 'Prior cache seeding preserved'
    idle(SOURCE)
    for project in TARGETS:
        validate_project(project)
    ROOT.mkdir()
    with (ROOT / 'controller.log').open('xb') as output:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'],
            cwd=ROOT, stdin=subprocess.DEVNULL, stdout=output, stderr=output,
            creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', dict(pid=child.pid, started_utc=now()))
    print(json.dumps(dict(pid=child.pid, project_caches_expected=4, caches_verified=0)))


def run():
    state = dict(status='inventorying_existing_official_package_cache', source=str(SOURCE),
                 projects=[], caches_verified=0, full_import=False, asset_sources_modified=False)
    save('RUN-RESULT.v1.json', state)
    try:
        idle(SOURCE)
        cache = SOURCE / 'Library/PackageCache'
        paths = sorted(p for p in cache.rglob('*') if p.is_file())
        assert paths and not any(p.relative_to(cache).parts[0].startswith('.') for p in paths)
        assert not any(p.stat().st_file_attributes & 0x400 for p in paths), 'Reparse dependency excluded'
        inventory = {p.relative_to(cache).as_posix(): dict(sha256=sha(p), bytes=p.stat().st_size) for p in paths}
        for name in ['com.unity.render-pipelines.high-definition', 'com.unity.render-pipelines.universal', 'com.unity.shadergraph']:
            matches = list(cache.glob(name + '@*/package.json'))
            assert len(matches) == 1 and json.loads(matches[0].read_text('utf-8'))['version'] == '17.5.0'
        stable = {str(p): sha(p) for p in [Path(__file__).resolve(), SOURCE / 'Packages/manifest.json',
                                         SOURCE / 'Packages/packages-lock.json']}
        save('SOURCE-INVENTORY.v1.json', dict(files=inventory, input_pins=stable))
        for index, project in enumerate(TARGETS):
            row = dict(project=str(project), files_copied=0, verified=False)
            state['projects'].append(row)
            stage = ROOT / 'staging' / str(index) / 'PackageCache'
            try:
                validate_project(project)
                stage.mkdir(parents=True)
                state['status'] = 'copying_and_reopening_private_cache'
                for relative, pin in inventory.items():
                    origin, target = cache / relative, stage / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    assert sha(origin) == pin['sha256'], 'Source cache changed'
                    shutil.copy2(origin, target)
                    assert target.stat().st_size == pin['bytes'] and sha(target) == pin['sha256'], 'Copied cache differs'
                    row['files_copied'] += 1
                    if row['files_copied'] % 256 == 0:
                        save('RUN-RESULT.v1.json', state)
                actual = {p.relative_to(stage).as_posix() for p in stage.rglob('*') if p.is_file()}
                assert actual == set(inventory), 'Cache file coverage differs'
                assert all(sha(cache / name) == pin['sha256'] for name, pin in inventory.items())
                assert all(sha(p) == expected for p, expected in stable.items())
                validate_project(project)
                destination = project / 'Library/PackageCache'
                assert stage.resolve().is_relative_to(ROOT.resolve()) and destination.resolve().is_relative_to(project.resolve())
                destination.parent.mkdir(exist_ok=True)
                # Same-drive atomic rename; a concurrently created cache causes
                # refusal rather than replacement or deletion of existing files.
                stage.rename(destination)
                row.update(verified=True, status='exact_package_cache_seeded', files_verified=len(inventory),
                           destination=str(destination), no_asset_or_settings_changes=True)
                state['caches_verified'] += 1
            except Exception as error:
                row.update(status='refused_existing_or_different_project_preserved', error=str(error))
            save('RUN-RESULT.v1.json', state)
        state['status'] = 'four_exact_package_caches_seeded' if state['caches_verified'] == 4 else 'partial_cache_seeding_preserved'
    except Exception as error:
        state.update(status='cache_seeding_refused', error=str(error))
    state['finished_utc'] = now()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    prepare() if len(sys.argv) == 1 else run()
