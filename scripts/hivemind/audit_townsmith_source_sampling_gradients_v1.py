from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re
w=Path('F:/OpenRPG_ImportWork');root=w/'townsmith-texture-derivative-source-audit-v1';assert not root.exists()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
prior=w/'townsmith-graph-precision-source-audit-v1/RESULT.v1.json';old=json.loads(prior.read_text(encoding='utf-8'));generated=w/'townsmith-s-paint-sdk-generated-source-v2/S_Paint.generated.shader.txt';assert sha(generated)==old['input_pins'][str(generated)]
api=w/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache/com.unity.render-pipelines.core@a4c9a9793789/ShaderLibrary/API/D3D11.hlsl';g=generated.read_text(encoding='utf-8');a=api.read_text(encoding='utf-8');pins={str(p):sha(p) for p in [Path(__file__),prior,generated,api]}
macros=[dict(line=i+1,text=line) for i,line in enumerate(a.splitlines()) if re.match(r'#define (?:PLATFORM_)?SAMPLE_TEXTURE2D(?:_GRAD)?\(',line)];assert len(macros)==4 and any('textureName.Sample(samplerName, coord2)' in x['text'] for x in macros)
explicit={term:len(re.findall(r'\b'+term+r'\b',g)) for term in ['SAMPLE_TEXTURE2D_GRAD','ddx_fine','ddy_fine','ddx_coarse','ddy_coarse']}
implicit=len(re.findall(r'\bSAMPLE_TEXTURE2D\(',g));assert implicit>0
normal_rows=[]
for node in ['750ecb50ee4d4ba68c883309163a9cdf','c854d165fd4c419ea1c5c5bb06aecd9e','faecb5655dc44b008b515935f1a3c73d']:
 matching=[dict(line=i+1,text=line) for i,line in enumerate(g.splitlines()) if node in line and 'SAMPLE_TEXTURE2D(' in line]
 assert matching,node
 normal_rows.append(dict(node=node,implicit_sample_calls=len(matching),example=matching[0]))
root.mkdir();snapshots={}
for p in [generated,api]:
 target=root/p.name;target.write_bytes(p.read_bytes());assert sha(target)==pins[str(p)];snapshots[str(p)]=str(target);pins[str(target)]=sha(target)
r=dict(status='generated_source_implicit_sampling_and_sdk_macro_audited',finished_utc=datetime.now(timezone.utc).isoformat(),implicit_sample_calls=implicit,explicit_gradient_tokens=explicit,normal_nodes=normal_rows,d3d11_macros=macros,input_pins=pins,snapshots=snapshots,source_generated_text_pinned_to_prior_proof=True,sdk_macro_current_file_only=True,compiled_gpu_derivative_selection_qualified=False,forced_fine_or_coarse_policy_justified=False,full_import=False,material_fidelity_qualified=False)
(root/'RESULT.v1.json').write_text(json.dumps(r,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in r.items() if k not in ['input_pins','normal_nodes','d3d11_macros','snapshots']}))
