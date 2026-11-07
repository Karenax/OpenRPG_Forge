"""Compare BC and decoded-float Unity frames with the existing WGSL frames."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,struct
W=Path('F:/OpenRPG_ImportWork');N=W/'house-float-transport-reference-v632';B=W/'house-hdrp-bias-surface-reference-v623';G=W/'house-hdrp-bias-surface-gpu-comparison-v624';O=W/'house-float-transport-comparison-v633'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def values(p):return struct.unpack('<16384f',Path(p).read_bytes())
assert not O.exists()
run=read(N/'RUN-RESULT.v1.json');assert run['unity_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
pins={}
for proof in [read(N/'PREPARED.v1.json'),read(G/'RESULT.v1.json')]:
    for key in ['input_pins','output_pins']:
        for p,h in proof.get(key,{}).items():assert sha(p)==h,p;pins[p]=h
rows=[];outputpins={}
for frame in read(N/'NATIVE-RESULT.v1.json')['frames']:
    key=frame['material_guid']+'/'+Path(frame['folder']).name
    for mode,path in enumerate(frame['planes']):
        bc=B/'frames'/key/('plane-'+str(mode)+'.f32');gpu=G/'outputs'/key/('plane-'+str(mode)+'.f32')
        outputpins[path]=sha(path);a=values(path);b=values(bc);g=values(gpu);limit=0 if mode==5 else 1e-5
        assert all(math.isfinite(x) for x in a)
        for label,expected in [('native_bc_vs_native_float',b),('native_float_vs_wgsl_float',g)]:
            errors=[abs(x-y) for x,y in zip(expected,a)];rows.append(dict(key=key,plane=mode,comparison=label,max_absolute_error=max(errors),failed_components=sum(e>limit for e in errors),byte_exact=expected==a))
summaries=[dict(comparison=label,failed_components=sum(r['failed_components'] for r in rows if r['comparison']==label),max_absolute_error=max(r['max_absolute_error'] for r in rows if r['comparison']==label)) for label in ['native_bc_vs_native_float','native_float_vs_wgsl_float']]
transport_confirmed=summaries[0]['failed_components']>0 and summaries[1]['failed_components']==0
for p in [Path(__file__),N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json',G/'RESULT.v1.json']:pins[str(p)]=sha(p)
O.mkdir();(O/'PLANE-COMPARISONS.v1.json').write_text(json.dumps(rows,indent=2),'utf-8')
r=dict(status='house_float_transport_comparison_complete',utc=datetime.now(timezone.utc).isoformat(),comparisons=summaries,transport_difference_confirmed_in_fixture=transport_confirmed,materials=6,frames=48,planes=288,absolute_tolerance=1e-5,input_pins=pins,output_pins=outputpins,source_inputs_unchanged=True,full_import=False,complete_conversion_increment=0,next_steps=['Retain original BC mip blocks and compare WGSL with native BC resources','Qualify depth/host, 24 prefab renders and Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2),'utf-8');print(json.dumps({k:r[k] for k in ['status','comparisons','transport_difference_confirmed_in_fixture']}))
