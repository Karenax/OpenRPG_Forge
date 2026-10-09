"""Reopen v2 native capture using exact float32 arithmetic, not decimal equality.

No capture rerun, threshold relaxation or replacement of source curves.
"""
from pathlib import Path
import ast
import datetime
import hashlib
import importlib.util
import json
import struct

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_firepit_gradient_boundaries_v1.py'
W = Path('F:/OpenRPG_ImportWork')
CAPTURE = W / 'unity-delta-firepit-gradient-boundaries-v2'
ROOT = W / 'firepit-gradient-boundaries-independent-reopen-v3'

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text('utf-8-sig'))
def f32(v): return struct.unpack('<f', struct.pack('<f', v))[0]

def main():
    report = read(CAPTURE / 'GRADIENT-BOUNDARIES.v1.json')
    rows = [row for case in report['cases'] for row in case['rows']]
    decimal_mismatches = []; bit_mismatches = []
    for row in rows:
        particle = row['particles'][0]
        expected = f32(1 - f32(f32(particle['remaining_lifetime']) / f32(particle['start_lifetime'])))
        if row['normalized_age_from_particle'] != expected:
            decimal_mismatches.append(dict(requested=row['requested_normalized_age'], reported=row['normalized_age_from_particle'], expected=expected))
        if f32(row['normalized_age_from_particle']) != expected: bit_mismatches.append(decimal_mismatches[-1])
    print(json.dumps(dict(rows=len(rows),decimal_representation_differences=len(decimal_mismatches),
        float32_bit_mismatches=len(bit_mismatches),first=decimal_mismatches[:3])))
    assert not ROOT.exists(), 'Preserve earlier evidence'
    ROOT.mkdir()
    initial = dict(status='checking_native_capture_float32',full_import=False,runtime_qualified=False)
    (ROOT/'RUN-RESULT.v1.json').write_text(json.dumps(initial,indent=2),'utf-8')
    try:
        assert not bit_mismatches, 'Actual native float32 ages differ; do not normalize away the error'
        spec=importlib.util.spec_from_file_location('original_gradient_verifier',BASE)
        module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        module.ROOT=CAPTURE
        node=next(n for n in ast.parse(BASE.read_text('utf-8')).body if isinstance(n,ast.FunctionDef) and n.name=='verify')
        code=ast.unparse(node)
        replacements={
            "row['requested_normalized_age'] in manifest['times']":"f32(row['requested_normalized_age']) in manifest['times']",
            "f32(1 - f32(particle['remaining_lifetime'] / particle['start_lifetime']))":"f32(1 - f32(f32(particle['remaining_lifetime']) / f32(particle['start_lifetime'])))",
            "row['normalized_age_from_particle'] == expected_age":"f32(row['normalized_age_from_particle']) == expected_age",
            "time = row['requested_normalized_age']":"time = f32(row['requested_normalized_age'])",
        }
        for old,new in replacements.items():
            assert code.count(old)==1, 'Original assertion shape changed: '+old
            code=code.replace(old,new)
        exec(compile(code,str(Path(__file__).resolve()),'exec'),module.__dict__)
        prepared=read(CAPTURE/'PREPARED.v1.json'); prior=read(CAPTURE/'RUN-RESULT.v1.json')
        assert prior['unity_exit_code']==0 and prior.get('finished_utc')
        pins=dict(prepared['input_pins'])
        for p in [BASE,Path(__file__).resolve(),CAPTURE/'PREPARED.v1.json',CAPTURE/'RUN-RESULT.v1.json',CAPTURE/'GRADIENT-BOUNDARIES.v1.json']:
            pins[str(p)]=sha(p)
        module.probe.job.check(pins)
        result=module.verify(report,prepared)
        # Exercise the repaired actual verifier against a real one-ULP corruption.
        corrupted=json.loads(json.dumps(report)); row=corrupted['cases'][0]['rows'][100]
        bits=struct.unpack('<I',struct.pack('<f',row['normalized_age_from_particle']))[0]
        row['normalized_age_from_particle']=struct.unpack('<f',struct.pack('<I',bits+1))[0]
        refused=False
        try: module.verify(corrupted,prepared)
        except AssertionError: refused=True
        assert refused, 'A one-ULP native age mutation must still refuse'
        module.probe.job.check(pins)
        receipt=dict(result,input_pins=pins,capture_sha256=sha(CAPTURE/'GRADIENT-BOUNDARIES.v1.json'),
            decimal_representation_differences=len(decimal_mismatches),float32_bit_mismatches=0,
            float32_ulp_corruption_refused=True,source_curves_unchanged=True,
            runtime_qualified=False,full_import=False)
        (ROOT/'INDEPENDENT-REOPEN.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
        initial.update(status='native_gradient_boundary_capture_float32_reopened',native_gradient_lookup_probe_verified=True,
            **result,independent_receipt_sha256=sha(ROOT/'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        initial.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    initial['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    (ROOT/'RUN-RESULT.v1.json').write_text(json.dumps(initial,indent=2),'utf-8')
    print(json.dumps(initial))

if __name__=='__main__': main()
