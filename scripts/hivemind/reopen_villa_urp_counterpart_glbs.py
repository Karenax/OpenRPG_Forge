"""Reopen nine frame-bound counterpart GLBs; never reuse their HDRP materials."""
import datetime
import hashlib
import json
from pathlib import Path
import struct
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-urp-counterpart-geometry-reopen-v1'
EQUIVALENCE = W / 'villa-urp-no-png-source-equivalence-v1/RESULT.v1.json'
SEQUENTIAL = W / 'villa-native-sequential-delivery-v2/RUN-RESULT.v1.json'
THIRD = W / 'villa-native-source-frame-bound-l04-villa-03-v1/RUN-RESULT.v1.json'
NODE = Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe')
MODULE = W / '.tools/gltf-validator/node_modules/gltf-validator'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Previous reopening retained'
    equivalent = read(EQUIVALENCE)
    assert equivalent['source_equivalent_cards'] == equivalent['expected_cards'] == 9
    pins = dict(equivalent['input_pins'])
    for path in [EQUIVALENCE, SEQUENTIAL, THIRD, NODE, Path(__file__).resolve(),
                 *[p for p in MODULE.rglob('*') if p.is_file()]]:
        pins[str(path)] = sha(path)
    sequential = read(SEQUENTIAL)
    assert sequential['status'] == 'remaining_147_native_physical_derivatives_reopened_runtime_unqualified'
    receipts = [THIRD]
    for batch in sequential['closed_batches']:
        if batch['batch_id'] in ['L04.VILLA.09', 'L04.VILLA.11']:
            path = Path(batch['frame_receipt'])
            assert sha(path) == batch['frame_receipt_sha256']
            receipts.append(path)
    assert len(receipts) == 3
    counterparts = {}
    for path in receipts:
        receipt = read(path)
        assert receipt['status'] == 'native_original_source_frames_bound_and_reopened'
        pins[str(path)] = sha(path)
        for row in receipt['rows']:
            assert row['asset_id'] not in counterparts, 'Ambiguous derivative identity'
            counterparts[row['asset_id']] = row
    assert all(sha(p) == h for p, h in pins.items()), 'Prepared evidence changed'
    ROOT.mkdir()
    program = "const fs=require('fs');const v=require(process.argv[1]);v.validateBytes(new Uint8Array(fs.readFileSync(process.argv[2])),{uri:process.argv[2],format:'glb',maxIssues:0,writeTimestamp:false}).then(r=>{fs.writeFileSync(process.argv[3],JSON.stringify(r,null,2));process.exitCode=r.issues.numErrors?1:0;}).catch(e=>{console.error(e);process.exitCode=2;});"
    rows = []
    for index, item in enumerate(equivalent['rows'], 1):
        source = counterparts[item['counterpart_id']]
        glb = Path(source['glb'])
        assert sha(glb) == source['sha256'], 'Frame-bound GLB changed'
        pins[str(glb)] = source['sha256']
        with glb.open('rb') as stream:
            header = stream.read(20)
            assert header[:4] == b'glTF' and header[16:20] == b'JSON'
            version, total, length = struct.unpack_from('<III', header, 4)
            assert version == 2 and total == glb.stat().st_size
            doc = json.loads(stream.read(length))
            bin_header = stream.read(8)
            size = struct.unpack_from('<I', bin_header)[0]
            assert bin_header[4:] == b'BIN\x00' and stream.tell() + size == total
            binary_sha = hashlib.file_digest(stream, 'sha256').hexdigest()
        assert binary_sha == source['unchanged_bin_sha256'], 'Frame-bound BIN changed'
        assert source['original_source_vectors_reopened_bitwise_equal']
        report = ROOT / f'{index:02d}.KHRONOS.v1.json'
        process = subprocess.run([str(NODE), '-e', program, str(MODULE), str(glb), str(report)],
                                 capture_output=True, cwd=ROOT, check=False)
        (ROOT / f'{index:02d}.stdout.log').write_bytes(process.stdout)
        (ROOT / f'{index:02d}.stderr.log').write_bytes(process.stderr)
        issues = read(report)['issues'] if report.exists() else None
        rows.append(dict(id=item['id'], counterpart_id=item['counterpart_id'], glb=str(glb),
                         glb_sha256=source['sha256'], bin_sha256=binary_sha,
                         native_frame_receipt_source_vectors_bitwise_equal=True,
                         nodes=len(doc.get('nodes', [])), meshes=len(doc.get('meshes', [])),
                         material_count=len(doc.get('materials', [])),
                         actual_validator_exit_code=process.returncode, issues=issues,
                         validator_report=str(report), validator_report_sha256=sha(report) if report.exists() else None,
                         geometry_derivative_reopened=process.returncode == 0 and issues is not None and issues['numErrors'] == 0,
                         hdrp_material_reuse_authorized=False, variant_geometry_delivered=False))
    changed = [p for p, h in pins.items() if sha(p) != h]
    count = sum(r['geometry_derivative_reopened'] for r in rows) if not changed else 0
    result = dict(status='nine_villa_counterpart_geometry_derivatives_reopened' if count == 9
                  else 'counterpart_geometry_refusals_retained', expected=9, geometry_derivatives_reopened=count,
                  rows=rows, input_pins=pins, changed_inputs=changed,
                  qualification='Existing HDRP counterpart geometry/frame files only; URP material assembly, images and runtime remain pending.',
                  assets_delivered=0, gpu_rendered=False, studio_verified=False, full_import=False,
                  recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], expected=9, geometry_derivatives_reopened=count,
                         validator_errors=sum(r['issues']['numErrors'] for r in rows if r['issues']),
                         validator_warnings=sum(r['issues']['numWarnings'] for r in rows if r['issues']), assets_delivered=0)))


if __name__ == '__main__':
    main()
