"""Use the existing local Khronos validator without downloading dependencies."""
import json
from pathlib import Path
import subprocess

from verify_medieval_skin_positions import sha

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'medieval-native-skin-khronos-validation-v1'
MODULE = W / '.tools/gltf-validator/node_modules/gltf-validator'
NODE = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe')


def main():
    assert not OUT.exists(), 'Prior validation preserved'
    receipt_path = W / 'medieval-native-skin-glb-reopen-v1/RESULT.v1.json'
    receipt = json.loads(receipt_path.read_text('utf-8-sig'))
    glb = Path(receipt['glb'])
    pins = dict(receipt['input_pins'])
    for path in [receipt_path, NODE, Path(__file__).resolve(), *[p for p in MODULE.rglob('*') if p.is_file()]]:
        pins[str(path)] = sha(path)
    assert (MODULE / 'index.js').is_file()
    assert all(sha(p) == expected for p, expected in pins.items())
    OUT.mkdir()
    report = OUT / 'KHRONOS.v1.json'
    program = "const fs=require('fs');const v=require(process.argv[1]);v.validateBytes(new Uint8Array(fs.readFileSync(process.argv[2])),{uri:process.argv[2],format:'glb',maxIssues:0,writeTimestamp:false}).then(r=>{fs.writeFileSync(process.argv[3],JSON.stringify(r,null,2));process.exitCode=r.issues.numErrors?1:0;}).catch(e=>{console.error(e);process.exitCode=2;});"
    child = subprocess.run([str(NODE), '-e', program, str(MODULE), str(glb), str(report)],
                           capture_output=True, cwd=OUT, check=False)
    (OUT / 'stdout.log').write_bytes(child.stdout)
    (OUT / 'stderr.log').write_bytes(child.stderr)
    issues = json.loads(report.read_text('utf-8'))['issues'] if report.exists() else None
    unchanged = all(sha(p) == expected for p, expected in pins.items())
    result = dict(status=('native_skin_glb_khronos_validated' if child.returncode == 0 and unchanged
                          else 'validator_refusal_preserved'), actual_exit_code=child.returncode,
                  issues=issues, input_pins=pins, input_pins_unchanged=unchanged,
                  glb=str(glb), report=str(report), gpu_verified=False,
                  material_qualification_pending=True, studio_verified=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('input_pins', 'issues')}))
    if issues:
        print(json.dumps(issues))


if __name__ == '__main__':
    main()
