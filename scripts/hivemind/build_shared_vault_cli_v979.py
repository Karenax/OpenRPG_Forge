"""Build the shared CLI after the Hub job, with immutable local evidence."""
import os
from pathlib import Path
import subprocess
import sys

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'scripts/delivery'))
from complete_rebuild import shared_source_inputs, sha, write

ROOT = Path('F:/OpenRPG_ImportWork/shared-vault-cli-build-v979')
TARGET = Path('F:/OpenRPG_ImportWork/delivery-external-vault-v974/target')
TOOLS = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')

def main():
    hub_receipt = Path('F:/OpenRPG_ImportWork/shared-content-manager-build-v975/RESULT.v1.json')
    if not hub_receipt.is_file():
        raise ValueError('Wait for the current Hub job before compiling in its shared cache')
    ROOT.mkdir(exist_ok=False)
    before = shared_source_inputs(REPO)
    write(ROOT / 'source-inputs.json', before)
    (ROOT / 'temp').mkdir()
    env = dict(os.environ, CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0',
               RUSTC=str(TOOLS / 'rustc.exe'), RUSTDOC=str(TOOLS / 'rustdoc.exe'),
               TEMP=str(ROOT / 'temp'), TMP=str(ROOT / 'temp'), NO_COLOR='true')
    command = [str(TOOLS / 'cargo.exe'), 'build', '--release', '--manifest-path',
               str(REPO / 'tools/forge_delivery/Cargo.toml'), '--locked', '--offline']
    with (ROOT / 'build.log').open('w', encoding='utf-8') as log:
        process = subprocess.Popen(command, cwd=REPO, env=env, stdout=log, stderr=log)
        write(ROOT / 'JOB.v1.json', {'pid': process.pid, 'command': command})
        code = process.wait()
    after = shared_source_inputs(REPO)
    write(ROOT / 'source-after.json', after)
    delivery = lambda value: {k:v for k,v in value['files'].items()
                              if k.startswith('tools/forge_delivery/')}
    stable = delivery(before) == delivery(after)
    output = TARGET / 'release/forge_delivery.exe'
    write(ROOT / 'RESULT.v1.json', {'status': 'BUILD_ONLY' if code == 0 and stable else 'FAIL',
          'exit_code': code, 'delivery_inputs_stable': stable,
          'all_checkout_inputs_stable': before == after, 'shared_working_tree': True,
          'executable': str(output), 'executable_sha256': sha(output) if code == 0 else None,
          'source_manifest_sha256': sha(ROOT / 'source-inputs.json'),
          'installed': False, 'runtime_verified': False, 'full_import': False})
    if code != 0 or not stable:
        raise RuntimeError('CLI build failed or its source inputs changed')

if __name__ == '__main__':
    main()
