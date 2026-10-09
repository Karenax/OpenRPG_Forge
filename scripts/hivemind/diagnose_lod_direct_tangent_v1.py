"""Measure direct-tangent operation orders without changing any source LOD thresholds."""
from pathlib import Path
import json,math,struct,hashlib,itertools
W=Path('F:/OpenRPG_ImportWork');P=W/'rocks-firepit-native-lod-boundary-v3/NATIVE-RESULT.v1.json';OUT=W/'lod-direct-tangent-diagnosis-v1'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x):return struct.unpack('<f',struct.pack('<f',x))[0]
def main():
    assert not OUT.exists();original=sha(P);native=json.loads(P.read_text('utf-8-sig'));samples=native['samples']
    assert original==json.loads((P.parent/'RUN-RESULT.v1.json').read_text('utf-8-sig'))['native_result_sha256']
    thresholds={}
    for s in samples:thresholds.setdefault((s['prefab_path'],s['node_path']),{})[s['source_level_index']]=s['source_threshold']
    records=[]
    for tan_kind in ('projection_inverse','tan_f64_angle','tan_f32_angle','tan_f32_all'):
     for order in itertools.permutations(('distance','tan','two','threshold')):
      for inclusive in (False,True):
       mismatches=0
       for s in samples:
        fields={v['name']:v for v in s['native_fields']};size=fields['worldSpaceSize']['float_value'];distance=fields['activeDistance']['float_value']
        if tan_kind=='projection_inverse':tan=f32(1/s['camera_projection_matrix_row_major'][5])
        elif tan_kind=='tan_f64_angle':tan=f32(math.tan(s['field_of_view']*math.pi/360))
        elif tan_kind=='tan_f32_angle':tan=f32(math.tan(f32(s['field_of_view']*math.pi/360)))
        else:tan=f32(math.tan(f32(f32(s['field_of_view']*f32(math.pi/180))*0.5)))
        chosen=-1
        for level,t in sorted(thresholds[(s['prefab_path'],s['node_path'])].items()):
         values=dict(distance=distance,tan=tan,two=2.0,threshold=t);product=values[order[0]]
         for label in order[1:]:product=f32(product*values[label])
         if size>=product if inclusive else size>product:chosen=level;break
        mismatches+=chosen!=fields['activeLODLevel']['integer_value']
       records.append(dict(tangent=tan_kind,order=list(order),inclusive=inclusive,decision_mismatches=mismatches))
    assert sha(P)==original;OUT.mkdir();result=dict(status='direct_tangent_operation_orders_measured_no_runtime_qualification',samples=len(samples),source_sha256=original,script_sha256=sha(Path(__file__)),candidates=records,forge_lod_qualified=False,full_import=False)
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(sorted(records,key=lambda x:x['decision_mismatches'])[:8]))
if __name__=='__main__':main()
