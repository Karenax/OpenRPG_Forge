"""Compare exact arithmetic hypotheses; no acceptance or runtime correction."""
from pathlib import Path
import datetime
import hashlib
import json
import struct

W=Path('F:/OpenRPG_ImportWork')
CAPTURE=W/'unity-delta-firepit-gradient-boundaries-v2/GRADIENT-BOUNDARIES.v1.json'
ROOT=W/'firepit-native-age-arithmetic-diagnosis-v1'
def f32(v): return struct.unpack('<f',struct.pack('<f',v))[0]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    assert not ROOT.exists()
    data=json.loads(CAPTURE.read_text('utf-8-sig'))
    hypotheses={name:dict(exact=0,examples=[]) for name in ['division_rounded','reciprocal_product_rounded','division_extended','reciprocal_product_extended']}
    count=0
    for case in data['cases']:
        for row in case['rows']:
            p=row['particles'][0]; remaining=f32(p['remaining_lifetime']); lifetime=f32(p['start_lifetime'])
            actual=f32(row['normalized_age_from_particle'])
            values=dict(division_rounded=f32(1-f32(remaining/lifetime)),
                reciprocal_product_rounded=f32(1-f32(remaining*f32(1/lifetime))),
                division_extended=f32(1-remaining/lifetime),
                reciprocal_product_extended=f32(1-remaining*f32(1/lifetime)))
            for name,value in values.items():
                stats=hypotheses[name]; stats['exact']+=int(value==actual)
                if value!=actual and len(stats['examples'])<3:
                    stats['examples'].append(dict(node=case['node_path'],requested=row['requested_normalized_age'],
                        actual=actual,expected=value,remaining=remaining,lifetime=lifetime))
            count+=1
    result=dict(observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),rows=count,
        hypotheses=hypotheses,input_pins={str(CAPTURE):sha(CAPTURE),str(Path(__file__).resolve()):sha(Path(__file__).resolve())},
        native_arithmetic_cause_qualified=False,native_capture_accepted=False,runtime_correction_installed=False,full_import=False)
    ROOT.mkdir(); (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(result))
if __name__=='__main__': main()
