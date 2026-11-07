"""Compare the observed material migration in an unchanged native HDRP fixture."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, shutil, subprocess, sys, traceback, re

S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-glass-sdk-comparison-v342'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,studio_verified=False,images_admitted=0)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')

def prepare():
    assert not O.exists()
    guard_path=W/'pirate-source-thumbnail-batch-0161-0176-guard-v316/RESULT.v1.json';guard=read(guard_path);guard['cases_refused_source_parity']=[c for c in guard['cases_refused_source_parity'] if 'windowglass-01' in c['id']];assert len(guard['cases_refused_source_parity'])==1
    V=Path('F:/OpenRPG_AssetVault');pointer=read(V/'metadata/asset-source-support-registry.v1.json');catalog_path=V/pointer['path'];assert sha(catalog_path)==pointer['sha256'];catalog={a['id']:a for a in read(catalog_path)['assets']};source_pins={str(catalog_path):sha(catalog_path)}
    for case in guard['cases_refused_source_parity']:
        card=catalog[case['id']];assert not card.get('preview') and card['pack_id']=='hivemind.pirate-island'
        for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
            path=V/card[field];assert sha(path)==card[hfield];source_pins[str(path)]=card[hfield]
    O.mkdir();(O/'temp').mkdir()
    base=S/'candidates/SourceLibraryThumbnailBatchV1.cs';text=base.read_text('utf-8-sig')
    replacements={
      'OpenRPGSourceLibraryThumbnailBatchV1':'OpenRPGPirateGlassSdkComparisonV342',
      'public string id,prefab_path,prefab_guid,output;':'public string id,prefab_path,prefab_guid,output; public bool restore_prior_mask_and_stencil;public string comparison_material_guid;public string[] comparison_added_floats;public bool comparison_added_mask;',
      'public string path,sha256;':'public string path,sha256,raw_path,raw_sha256;',
      'GameObject fixture=null,cameraObject=null,lightObject=null,volumeObject=null;':'GameObject fixture=null,cameraObject=null,lightObject=null,volumeObject=null;Material comparisonMaterial=null;string comparisonSourceBefore=null;Material comparisonSource=null;',
      'foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);':'''if(request.restore_prior_mask_and_stencil) {
    string materialPath=AssetDatabase.GUIDToAssetPath(request.comparison_material_guid);
    comparisonSource=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    if(comparisonSource==null)throw new Exception("Retained wood material missing");
    comparisonSourceBefore=EditorJsonUtility.ToJson(comparisonSource,true);
    comparisonMaterial=new Material(comparisonSource);comparisonMaterial.hideFlags=HideFlags.HideAndDontSave;
    comparisonMaterial.SetFloat("_StencilWriteMaskDepth",8);comparisonMaterial.SetFloat("_StencilWriteMaskGBuffer",14);comparisonMaterial.SetFloat("_StencilWriteMaskMV",40);
    var defaults=new Material(comparisonSource.shader);defaults.hideFlags=HideFlags.HideAndDontSave;
    try { foreach(var property in request.comparison_added_floats) {
     if(property=="_DstBlend2") {
      if(comparisonSource.GetFloat("_SurfaceType")!=1||comparisonSource.GetFloat("_BlendMode")!=0||comparisonSource.GetFloat("_DstBlend")!=(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)throw new Exception("Transparent alpha source policy prerequisites absent");
      var policyType=typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.BaseUnlitAPI",true);
      var method=policyType.GetMethod("SetupBaseUnlitKeywords",BindingFlags.Public|BindingFlags.Static);if(method==null)throw new Exception("SDK material blend policy absent");
      var policyMaterial=new Material(comparisonSource);policyMaterial.hideFlags=HideFlags.HideAndDontSave;
      try {
       policyMaterial.SetFloat("_DstBlend2",defaults.GetFloat("_DstBlend2"));method.Invoke(null,new object[]{policyMaterial});
       float derived=policyMaterial.GetFloat("_DstBlend2");if(derived!=(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha||derived!=comparisonSource.GetFloat("_DstBlend2"))throw new Exception("Derived SDK blend factor differs from imported material");
       comparisonMaterial.SetFloat("_DstBlend2",derived);
       policyMaterial.SetFloat("_SurfaceType",0);method.Invoke(null,new object[]{policyMaterial});if(policyMaterial.GetFloat("_DstBlend2")!=0)throw new Exception("Opaque blend negative control failed");
       if(comparisonSource.HasProperty("_TransmissionMaskMap")&&comparisonSource.GetTexture("_TransmissionMaskMap")!=null)throw new Exception("Added transmission texture requires separate source control");
      }finally{UnityEngine.Object.DestroyImmediate(policyMaterial);}
     }else if(comparisonSource.HasProperty(property)&&comparisonSource.GetFloat(property)!=defaults.GetFloat(property))throw new Exception("Added property differs from current shader default: "+property);
    }
    if(request.comparison_added_mask) {if(comparisonSource.GetFloat("_UseMask")!=0||comparisonSource.GetFloat("_UseOpacity")!=0)throw new Exception("Added mask has active selectors; separate source qualification required");comparisonMaterial.SetTexture("_GrayScaleMask",defaults.GetTexture("_GrayScaleMask"));}
    } finally {UnityEngine.Object.DestroyImmediate(defaults);}
    int replaced=0;foreach(var renderer in fixture.GetComponentsInChildren<Renderer>(true)) {
     var materials=renderer.sharedMaterials;for(int slot=0;slot<materials.Length;slot++)if(materials[slot]==comparisonSource){materials[slot]=comparisonMaterial;replaced++;}renderer.sharedMaterials=materials;
    }if(replaced==0)throw new Exception("Source material not bound by this prefab");
   }
   foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);''',
      'raw.Apply();var colors=raw.GetPixels();':'''raw.Apply();byte[] rawBytes=raw.GetRawTextureData();string rawPath=Path.Combine(Path.GetDirectoryName(request.output),"view-"+view+".rgba16f");if(File.Exists(rawPath))throw new Exception("Preserve raw capture");File.WriteAllBytes(rawPath,rawBytes);var colors=raw.GetPixels();''',
      'new Row{path=path,sha256=Hash(bytes),view=view':'new Row{path=path,sha256=Hash(bytes),raw_path=rawPath,raw_sha256=Hash(rawBytes),view=view',
      'if(sourceBefore!=EditorJsonUtility.ToJson(source,true))':'if(comparisonSource!=null&&comparisonSourceBefore!=EditorJsonUtility.ToJson(comparisonSource,true))throw new Exception("Persistent source material changed");\n   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))',
      'EditorSceneManager.ClosePreviewScene(preview);':'if(comparisonMaterial!=null)UnityEngine.Object.DestroyImmediate(comparisonMaterial);EditorSceneManager.ClosePreviewScene(preview);'
    }
    for before,after in replacements.items():
        assert text.count(before)==1,before;text=text.replace(before,after)
    helper=O/'PirateGlassSdkComparisonV342.cs';helper.write_text(text,'utf-8')
    original=read(W/'pirate-source-thumbnail-batch-0161-0176-v315/PREPARED.v1.json')
    added={}
    historical=read(W/'pirate-source-thumbnail-batch-0153-0160-guard-v307/RESULT.v1.json');changes={change['guid']:change for change in historical['changes']+guard['changes']}
    for change in changes.values():
        before=Path(change['prior_snapshot']).read_text('utf-8-sig');after=Path(change['path']).read_text('utf-8-sig');before_fields=set(re.findall(r'^\s+- ([A-Za-z0-9_]+): [-+0-9.eE]+\s*$',before,re.M));after_fields=set(re.findall(r'^\s+- ([A-Za-z0-9_]+): [-+0-9.eE]+\s*$',after,re.M));added[change['guid']]=dict(floats=sorted(after_fields-before_fields),mask='_GrayScaleMask:' not in before and '_GrayScaleMask:' in after)
    selected=[c for c in original['cases'] if c['id'] in {r['id'] for r in guard['cases_refused_source_parity']}];assert len(selected)==1;cases=[];blocked={c['id']:c['material_guids'] for c in guard['cases_refused_source_parity']};assert all(len(blocked[c['id']])==1 for c in selected)
    for i,source in enumerate(selected):
        for label,restore in [('warmup',False),('current',False),('prior-observed-semantics',True),('current-repeat',False)]:
            folder=O/f'case-{i:02d}-{label}';folder.mkdir()
            cases.append(dict(source,id=source['id']+':'+label,output=str(folder/'NATIVE-RESULT.v1.json'),restore_prior_mask_and_stencil=restore,comparison_material_guid=blocked[source['id']][0],comparison_added_floats=added[blocked[source['id']][0]]['floats'],comparison_added_mask=added[blocked[source['id']][0]]['mask']))
    (O/'manifest.json').write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins={str(p):sha(p) for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*') if p.is_file()}
    pins.update(source_pins)
    pins.update({'F:\\OpenRPG_ImportWork\\unity-library-source-thumbnail-pirate-bench-v2\\project\\Library\\PackageCache\\com.unity.render-pipelines.high-definition@fc55a8abadc2\\Runtime\\Material\\Unlit\\BaseUnlitAPI.cs': '885e5abf60c36980e02a8a64b0a22efd42f2e891aa9cdf845659acf09cf32b7f', 'F:\\OpenRPG_ImportWork\\unity-library-source-thumbnail-pirate-bench-v2\\project\\Library\\PackageCache\\com.unity.render-pipelines.high-definition@fc55a8abadc2\\Editor\\Material\\ShaderGraph\\HDSubShaderUtilities.cs': '29654d04a3bc27df057c73edd70c043ee8316543cf3ae39f70db3e20b5b66d19', 'E:\\OpenRPG_Forge\\OpenRPG_Forge-Rust\\scripts\\hivemind\\prepare_pirate_glass_sdk_comparison_v341.py': 'b77751e1bfa38e373066715ec8d759189a9363b32e0cd88f075928b480a2ebc0'})
    for p in [Path(__file__),base,helper,O/'manifest.json',guard_path,
              PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,cases=cases,adaptations=replacements,
        scope='Four pending prefabs, two views each; warmup, current, restored authored stencil fields with added defaults checked, current repeat. No persisted material writes.'),indent=2),'utf-8')
    save(dict(status='pirate_material_migration_render_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,root=str(O),prefabs=1,captures=4,views=8)))

def run():
    state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve other Unity'
        prep=read(O/'PREPARED.v1.json');pins=prep['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        target=PROJECT/'Assets/Editor/PirateGlassSdkComparisonV342.cs';assert not target.exists();shutil.copyfile(O/target.name,target)
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(recipe.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(recipe),'exec'),namespace);broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-job-worker-count','2',
                '-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGPirateGlassSdkComparisonV342.Export',
                '--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='comparing_pirate_migration_in_native_hdrp',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['input_changes']=[dict(path=p,before=h,after=sha(p)) for p,h in pins.items() if sha(p)!=h]
        assert not state['input_changes'] and sha(target)==sha(O/target.name)
        assert state['unity_exit_code']==0
        batch=read(O/'NATIVE-RESULT.v1.json');assert len(batch['cases'])==4
        outputs={};pairs=[];refusals=[]
        for i in range(1):
            cases=batch['cases'][i*4+1:(i+1)*4];receipts=[]
            if not all(case['captured'] for case in cases):
                refusals.append(dict(prefab_id=prep['cases'][i*4+1]['id'].rsplit(':',1)[0],stage='native_migration_comparison',errors=[case['error'] for case in cases if not case['captured']]));continue
            for expected,case in zip(prep['cases'][i*4+1:(i+1)*4],cases):
                assert case['captured'] and case['id']==expected['id'],case
                p=Path(case['receipt']);outputs[str(p)]=sha(p);native=read(p)
                assert native['source_object_unchanged'] and native['gpu']=='Direct3D11' and len(native['images'])==2
                for image in native['images']:
                    for field,h in [('path','sha256'),('raw_path','raw_sha256')]:
                        assert sha(image[field])==image[h];outputs[image[field]]=image[h]
                    assert Path(image['raw_path']).stat().st_size==512*512*8
                receipts.append(native)
            for view in range(2):
                images=[r['images'][view] for r in receipts];raw=[Path(a['raw_path']).read_bytes() for a in images]
                pairs.append(dict(prefab_id=prep['cases'][i*4+1]['id'].rsplit(':',1)[0],view=view,
                    current_repeat_exact=raw[0]==raw[2],migration_raw_exact=raw[0]==raw[1],
                    current_vs_prior_different_bytes=sum(a!=b for a,b in zip(raw[0],raw[1])),
                    raw_paths=[a['raw_path'] for a in images]))
        state.update(status='pirate_migration_native_comparison_completed',comparisons=pairs,output_pins=outputs,comparison_refusals=refusals,successful_prefabs=len(pairs)//2,expected_prefabs=1,
            all_views_repeat_exact=all(p['current_repeat_exact'] for p in pairs),all_views_migration_exact=all(p['migration_raw_exact'] for p in pairs),
            original_version_pipeline_equivalence=False,qualified_scope='Current HDRP thumbnail: source stencil masks, shader defaults and SDK-derived transparent alpha blend policy with opaque negative control; original SDK and OpenRPG renderer equivalence unqualified')
    except Exception as exc:state.update(status='pirate_migration_comparison_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None:namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)

if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
