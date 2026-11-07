"""Seal measured House POM parity without promoting partial evidence to conversion."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-pom-checkpoint-v608'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
reference=W/'house-pom-native-reference-v606';comparison=W/'house-pom-gpu-comparison-v607'
run=read(reference/'RUN-RESULT.v1.json');result=read(comparison/'RESULT.v1.json');native=read(reference/'NATIVE-RESULT.v1.json');gpu=read(comparison/'GPU-RESULT.v1.json')
assert run['unity_exit_code']==result['actual_gpu_exit_code']==0 and not run['source_changes'] and not run['private_importer_changes']
assert result['status']=='house_pom_numeric_comparison_pass' and result['samples']==1296 and result['compared_floats']==5184 and result['failed_components']==0
assert all(m['aniso_qualified'] for m in gpu['materials'])
pins={}
for k in ['input_pins','output_pins']:
    for p,h in result[k].items():assert sha(p)==h,p;pins[p]=h
sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core/ShaderLibrary/PerPixelDisplacement.hlsl')
resolved=list((W/'unity-library-source-thumbnail-house-bench-v2/project/Library/PackageCache').glob('com.unity.render-pipelines.core*/ShaderLibrary/PerPixelDisplacement.hlsl'));assert len(resolved)==1 and sha(resolved[0])==sha(sdk)
stats=[]
for m in native['materials']:
    expected=Path(m['raw_output']).read_bytes();actual=(comparison/'outputs'/(m['guid']+'.f32')).read_bytes();assert actual==expected
    v=struct.unpack('<'+str(len(expected)//4)+'f',expected)
    stats.append(dict(material_guid=m['guid'],byte_exact=True,sampled_height_min=min(v[3::4]),sampled_height_max=max(v[3::4]),pixel_depth_min=min(v[2::4]),pixel_depth_max=max(v[2::4]),amplitude_cm=m['amplitude']))
assert sum(s['pixel_depth_max']>0 for s in stats)==4
for p in [Path(__file__),resolved[0],reference/'RUN-RESULT.v1.json',reference/'NATIVE-RESULT.v1.json',comparison/'RESULT.v1.json',comparison/'MISMATCHES.v1.json']:pins[str(p)]=sha(p)
O.mkdir()
proof=dict(status='house_pom_real_height_numeric_parity_checkpointed',utc=datetime.now(timezone.utc).isoformat(),lot='L03.HOUSE.MAIN.01',prefab_cards=24,materials=6,samples=1296,compared_floats=5184,failed_components=0,all_outputs_byte_exact=True,sdk_resolved_package_matches_pinned_builtin=True,rows=stats,input_pins=pins,source_inputs_unchanged=True,complete_conversion_increment=0,material_conversion_qualified=False,actual_generated_graph_precision_verified=False,forge_surface_and_depth_render_verified=False,studio_verified=False,full_import=False,next_steps=['Verify actual generated shader precision and texture transforms','Compose remaining exact material surface nodes','Qualify host depth and assembled prefab rendering/colliders','Integrate and reopen in actual Studio'])
(O/'RESULT.v1.json').write_text(json.dumps(proof,indent=2),'utf-8')
text='''# Lot L03.HOUSE.MAIN.01 — relief parallax GPU

Six matériaux, 24 fiches prefab concernées. Référence SDK Unity en précision float explicite et port WGSL exécutés sur la Radeon RX 7800 XT, D3D11 / DX12.
Les 1 296 cas couvrent 12 UV, six directions de vue et trois échelles inverses. 5 184 valeurs comparées : sorties identiques octet par octet. Textures de hauteur originales décodées par le GPU, mip zéro, filtrage bilinéaire, anisotropie 1 ; paramètres source conservés. Quatre matériaux produisent des profondeurs non nulles ; les deux autres ont une amplitude source nulle.

Cette preuve qualifie la comparaison numérique de la fonction POM dans ce domaine. Elle ne qualifie pas la précision du shader complet généré, les autres nœuds de surface, la profondeur dans le moteur, les colliders ni les prefabs dans Studio. Aucun nouvel asset déclaré entièrement converti.

Suite : précision générée, composition de surface, profondeur moteur, rendu et comportement des 24 prefabs, puis Studio.
'''
(O/'LOT.md').write_text(text,'utf-8')
dashboard=W/'SUIVI_LOTS_HIVEMIND'
header='''## House — comparaison GPU parallax vérifiée (v608)

- Lot L03.HOUSE.MAIN.01 : 6 matériaux / 24 prefabs concernés.
- 1 296 cas, 5 184 valeurs ; résultats Unity SDK float / WGSL identiques octet par octet.
- Sources et matériaux du banc inchangés. Preuves : `F:\\OpenRPG_ImportWork\\house-pom-checkpoint-v608`.
- Incrément de conversions complètes : **0**. Restent précision du graphe complet, surface, profondeur moteur, prefabs/colliders et Studio.

'''
for name in ['AVANCEMENT_ACTUEL.md','POINT_DE_REPRISE.md']:
    path=dashboard/name;before=path.read_bytes();(O/(name+'.before')).write_bytes(before);path.write_text(header+before.decode('utf-8-sig'),'utf-8')
print(json.dumps({k:proof[k] for k in ['status','materials','samples','compared_floats','all_outputs_byte_exact','complete_conversion_increment']}))
