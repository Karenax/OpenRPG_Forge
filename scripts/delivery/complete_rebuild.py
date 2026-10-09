"""Finish native builds from an existing frozen input inventory.

Commands and logs are retained beside the snapshot; user installations are untouched.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--stage', choices=['prepare-frontend', 'studio', 'game'], required=True)
    parser.add_argument('--cache', type=Path)
    parser.add_argument('--frontend-assets', type=Path)
    args = parser.parse_args()
    base = args.evidence.resolve()
    source = base / 'source'
    manifest = base / 'source-inputs.json'
    pins = json.loads(manifest.read_bytes())['files']
    def verify():
        for name, pin in pins.items():
            if sha(source / name) != pin['sha256']:
                raise ValueError(f'Frozen input changed: {name}')
    verify()
    env = dict(os.environ, CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true', NO_COLOR='true')
    if args.cache:
        env['CARGO_TARGET_DIR'] = str(args.cache.resolve())
    commands = []
    def run(command, cwd, label):
        commands.append({'arguments': list(map(str, command)), 'cwd': str(cwd)})
        write(base / f'{args.stage}-commands.json', commands)
        print(f'Building {label}', flush=True)
        with (base / f'{label}.log').open('w', encoding='utf-8') as log:
            result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, stdout=log, stderr=log)
        if result.returncode:
            raise RuntimeError(f'{label} failed: {result.returncode}; log retained')
    if args.stage == 'prepare-frontend':
        frontend = base / 'frontend'
        (frontend / 'index.html').write_text(
            '<!doctype html>\n<html lang="fr"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width,initial-scale=1">'
            '<title>OpenRPG Forge Studio</title><link rel="stylesheet" href="/styles.css">'
            '<script src="/tidewater-webgpu-compat.js"></script></head>\n'
            '<body><script type="module">import init from "/forge_studio.js"; '
            'await init({module_or_path:"/forge_studio_bg.wasm"});</script></body></html>\n',
            encoding='utf-8')
        if args.frontend_assets:
            asset_pins = {}
            for name in ['forge-kit', 'forge-imports']:
                origin = args.frontend_assets / name
                destination = frontend / name
                shutil.copytree(origin, destination)
                for file in origin.rglob('*'):
                    if file.is_file():
                        relative = file.relative_to(args.frontend_assets).as_posix()
                        pin = sha(file)
                        if sha(frontend / relative) != pin:
                            raise ValueError(f'Static frontend asset changed: {relative}')
                        asset_pins[relative] = pin
            write(base / 'frontend-static-inputs.json', {'origin': str(args.frontend_assets), 'files': asset_pins})
        print('FRONTEND_ASSETS_PREPARED', flush=True)
        return
    if not args.cache:
        raise ValueError('--cache is required for native builds')
    if args.stage == 'studio':
        env['RUSTFLAGS'] = '-C debuginfo=0'
        frontend = base / 'frontend'
        config = base / 'tauri-candidate.conf.json'
        relative = os.path.relpath(frontend, source / 'apps/forge_studio').replace('\\', '/')
        write(config, {'build': {'beforeBuildCommand': None, 'frontendDist': relative},
                       'app': {'windows': [{'label': 'main', 'title': 'OpenRPG Forge Studio · Édition locale',
                                            'url': 'index.html?authoring=1', 'width': 1920, 'height': 1080,
                                            'minWidth': 1100, 'minHeight': 700, 'resizable': True,
                                            'dragDropEnabled': False}]}, 'bundle': {'active': False}})
        # The Tauri CLI rewrites Cargo.toml. Supply its documented build config
        # to the native Cargo build instead, keeping the source snapshot intact.
        env['TAURI_CONFIG'] = config.read_text(encoding='utf-8')
        run(['cargo', 'build', '-p', 'forge_studio', '--bin', 'forge-studio',
             '--features', 'tauri/custom-protocol', '--locked', '--offline', '-j', '1'],
            source / 'apps/forge_studio', 'studio-native-build')
        executable = base / 'forge-studio.exe'
        shutil.copyfile(args.cache / 'debug/forge-studio.exe', executable)
        extra = {'frontend_pins': [{'path': file.relative_to(frontend).as_posix(), 'sha256': sha(file)}
                                  for file in sorted(frontend.rglob('*')) if file.is_file()],
                 'entry_url': 'index.html?authoring=1', 'authoring': True,
                 'configuration': 'native debug; WebAssembly release; navigation worker release'}
    else:
        env.pop('RUSTFLAGS', None)
        run(['cargo', 'rustc', '-p', 'forge_engine', '--example', 'tidewater_plant_probe',
             '--locked', '--offline', '-j', '1',
             '--config', 'profile.dev.package.forge_engine.opt-level=3',
             '--config', 'profile.dev.package.forge_engine.debug=0',
             '--config', 'profile.dev.package.forge_engine.debug-assertions=false',
             '--', '-C', 'opt-level=3', '-C', 'debuginfo=0', '-C', 'debug-assertions=off'],
            source, 'game-native-build')
        executable = base / 'forge-game.exe'
        shutil.copyfile(args.cache / 'debug/examples/tidewater_plant_probe.exe', executable)
        extra = {'configuration': 'Cargo dev SDK, engine and native example opt-level 3, assertions off',
                 'all_workspace_dependencies_built_from_frozen_sources': True}
    verify()
    proof = {'schema': 'forge.delivery.native-build.v1', 'status': 'BUILD_ONLY',
             'recorded_at_utc': datetime.now(timezone.utc).isoformat(),
             'source_manifest_sha256': sha(manifest), 'executable_sha256': sha(executable),
             'source_stable_during_build': True, 'cache': str(args.cache), 'commands': commands,
             'runtime_verified': False, 'gate_qualified': False, **extra}
    write(base / f'{args.stage}-build-proof.json', proof)
    print(json.dumps({'stage': args.stage, 'status': 'BUILD_ONLY', 'executable': str(executable),
                      'sha256': proof['executable_sha256']}), flush=True)


if __name__ == '__main__':
    main()
