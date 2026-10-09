"""Explore generic arithmetic expressions against pinned native LOD observations."""
from pathlib import Path
import json, math, struct, hashlib
from datetime import datetime, timezone
W = Path('F:/OpenRPG_ImportWork')
SOURCE = W/'rocks-firepit-native-lod-boundary-v2/NATIVE-RESULT.v1.json'
OUT = W/'lod-projection-arithmetic-diagnosis-v1'
def sha(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def f32(x): return struct.unpack('<f',struct.pack('<f',x))[0]
def bits(x): return struct.unpack('<I',struct.pack('<f',x))[0]
def main():
    assert not OUT.exists()
    source_sha = sha(SOURCE)
    run=json.loads((SOURCE.parent/'RUN-RESULT.v1.json').read_text('utf-8-sig'))
    assert source_sha == run['native_result_sha256']
    samples=json.loads(SOURCE.read_text('utf-8-sig'))['samples']
    modes={}
    for factor in ('projection', 'tan_f64', 'tan_f32_angle', 'tan_f32_all'):
      for sizeorder in ('multiply_half', 'half_multiply', 'double_precision'):
       for compare in ('height', 'distance', 'squared_divide', 'squared_multiply', 'squared_product', 'f64_squared'):
        for inclusive in (False,True):
         key='.'.join((factor,sizeorder,compare,'inclusive' if inclusive else 'strict'))
         mismatches=0;height_match=0;examples=[]
         for s in samples:
          fields={v['name']:v for v in s['native_fields']}
          if factor=='projection': fac=s['camera_projection_matrix_row_major'][5]
          elif factor=='tan_f64': fac=f32(1/math.tan(s['field_of_view']*math.pi/360))
          elif factor=='tan_f32_angle': fac=f32(1/math.tan(f32(s['field_of_view']*math.pi/360)))
          else: fac=f32(1/f32(math.tan(f32(f32(s['field_of_view']*f32(math.pi/180))*0.5))))
          size=fields['worldSpaceSize']['float_value']
          if sizeorder=='multiply_half': radius=f32(f32(size*fac)*0.5)
          elif sizeorder=='half_multiply': radius=f32(f32(size*0.5)*fac)
          else: radius=size*fac*0.5
          diffs=[f32(a-b) for a,b in zip(s['camera_position'],s['world_reference_point'])]
          d2=f32(f32(f32(diffs[0]*diffs[0])+f32(diffs[1]*diffs[1]))+f32(diffs[2]*diffs[2]))
          dist=f32(math.sqrt(d2)); height=f32(radius/dist)
          height_match += bits(height)==bits(fields['activeRelativeScreenSize']['float_value'])
          thresholds=[]
          # The exact authored heights are represented by the captured boundary identity rows.
          for i in range(len(s['native_threshold_distances'])):
           row=next(v for v in samples if v['prefab_path']==s['prefab_path'] and v['node_path']==s['node_path'] and v['field_of_view']==s['field_of_view'] and v['source_level_index']==i)
           thresholds.append(row['source_threshold'])
          chosen=-1
          for i,t in enumerate(thresholds):
           if compare=='height': lhs,rhs=height,t
           elif compare=='distance': lhs,rhs=f32(radius/t),dist
           elif compare=='squared_divide': lhs,rhs=f32(f32(radius*radius)/f32(t*t)),d2
           elif compare=='squared_multiply': lhs,rhs=f32(radius*radius),f32(d2*f32(t*t))
           elif compare=='squared_product': lhs,rhs=f32(radius*radius),f32(f32(d2*t)*t)
           else: lhs,rhs=radius*radius,d2*t*t
           if lhs>=rhs if inclusive else lhs>rhs:
            chosen=i;break
          native=fields['activeLODLevel']['integer_value']
          if chosen!=native:
           mismatches+=1
           if len(examples)<3: examples.append(dict(prefab=s['prefab_path'],node=s['node_path'],fov=s['field_of_view'],level=s['source_level_index'],delta=s['neighbor_delta'],native=native,candidate=chosen))
         modes[key]=dict(decision_mismatches=mismatches,height_f32_exact_matches=height_match,examples=examples)
    assert sha(SOURCE)==source_sha
    ranked=sorted(modes.items(),key=lambda v:(v[1]['decision_mismatches'],-v[1]['height_f32_exact_matches']))
    OUT.mkdir()
    result=dict(status='projection_arithmetic_candidates_measured_no_runtime_qualification',finished_utc=datetime.now(timezone.utc).isoformat(),samples=len(samples),source_sha256=source_sha,script_sha256=sha(Path(__file__)),candidates=modes,best_candidates=ranked[:12],forge_lod_qualified=False,full_import=False)
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(samples=len(samples),best_candidates=ranked[:4])))
if __name__=='__main__': main()
