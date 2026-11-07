"""Seal a failed source GPU comparison and expose cases for the next diagnostic."""
from pathlib import Path
import hashlib,json,struct,datetime
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-wind-diagnostic-delivery-v546'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
B=W/'pirate-palm-wind-gpu-comparison-v545'
result=read(B/'RESULT.v1.json');gpu=read(B/'GPU-RESULT.v1.json');failures=read(B/'MISMATCHES.v1.json')
assert result['status']=='gpu_numeric_comparison_failed' and len(failures)==result['failed_components']==4769
assert gpu['backend']=='Dx12' and gpu['samples']==3192
for p,h in result['pins'].items():assert sha(p)==h,p
assert sha(B/'wgsl-output.f32')==result['output_sha256']
assert all(v==0 for k,v in result['category_max'].items() if k.startswith(('normal','tangent')))
inputpath=W/'pirate-palm-wind-native-reference-v541/INPUT.f32'
data=inputpath.read_bytes();rows=[struct.unpack_from('<24f',data,n*96) for n in range(3192)]
failed=set(f['sample'] for f in failures)
regions={}
for n,row in enumerate(rows):
    region='negative_world_x' if row[4]<0 else 'nonnegative_world_x'
    entry=regions.setdefault(region,dict(samples=0,failed_samples=0));entry['samples']+=1;entry['failed_samples']+=n in failed
examples=[]
for n in sorted(failed)[:12]:
    row=rows[n]
    examples.append(dict(sample=n,object_position=list(row[:4]),world_position=list(row[4:8]),uv=list(row[16:20]),time=list(row[20:24]),differences=[f for f in failures if f['sample']==n]))
O.mkdir()
report=dict(status='numeric_mismatch_sealed_noise_intermediates_required',utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
 samples=3192,failed_samples=len(failed),failed_components=4769,regions=regions,max_absolute_error=result['max_absolute_error'],
 exact_normal_tangent=True,tolerance_unchanged=True,tolerance_absolute=1e-5,examples=examples,
 pins={str(p):sha(p) for p in [Path(__file__),B/'RESULT.v1.json',B/'GPU-RESULT.v1.json',B/'MISMATCHES.v1.json',B/'wgsl-output.f32',inputpath]},
 full_import=False,new_admitted_assets=0,studio_verified=False,
 next='Capture matching Unity and WGSL intermediate values: scaled UV, floor/fract, both hash remainder stages, gradient directions and interpolation. Distinguish compiler arithmetic from source translation before changing any production math.')
(O/'RESULT.v1.json').write_text(json.dumps(report,indent=2),'utf-8')
note='''# Reprise — diagnostic GPU palmiers v545 / v546

Banc GPU exécuté : Unity D3D11 contre WGSL/wgpu DX12 sur AMD RX 7800 XT, mêmes 3 192 entrées figées.
Comparaison FAIL : 4 769 composantes sur 38 304 hors tolérance absolue 1e-5, sans tolérance relative.
Normales et tangentes identiques. Écart maximal sur position X : 0,35998535 unité source.
La réparation du coefficient 3 est conservée ; elle ne suffit pas à qualifier le bruit procédural.
Prochaine étape : capturer les valeurs intermédiaires natives/WGSL du bruit, sans relever la tolérance.
Preuve : F:\\OpenRPG_ImportWork\\pirate-palm-wind-diagnostic-delivery-v546\\RESULT.v1.json.
Six variantes de palmiers restent non admises ; aucun nouvel asset totalement converti ni mutation de catalogue.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    p=W/'SUIVI_LOTS_HIVEMIND'/name;old=p.read_bytes();(O/(name+'.before')).write_bytes(old)
    p.write_text(note+old.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:v for k,v in report.items() if k not in ['pins','examples']}))
