"""Compare the observed material migration in an unchanged native HDRP fixture."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, os, shutil, subprocess, sys, traceback

S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-wood-material-migration-render-v280'
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
    guard_path=W/'pirate-source-thumbnail-batch-0129-0136-guard-v278/RESULT.v1.json';guard=read(guard_path);assert len(guard['cases_refused_source_parity'])==8
    O.mkdir();(O/'temp').mkdir()
    base=S/'candidates/SourceLibraryThumbnailBatchV1.cs';text=base.read_text('utf-8-sig')
    replacements={
      'OpenRPGSourceLibraryThumbnailBatchV1':'OpenRPGPirateWoodMigrationRenderV280',
      'public string id,prefab_path,prefab_guid,output;':'public string id,prefab_path,prefab_guid,output; public bool restore_prior_mask_and_stencil;public string comparison_material_guid;',
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
    try { foreach(var property in new string[]{"_DstBlend2","_ExcludeFromTUAndAA","_MaterialTypeMask","_PerPixelSorting"}) {
     if(comparisonSource.HasProperty(property)&&comparisonSource.GetFloat(property)!=defaults.GetFloat(property))throw new Exception("Added property differs from current shader default: "+property);
    }
    if(request.comparison_material_guid=="abc00000000002303923814896909187"&&comparisonSource.GetFloat("_UseAOMap")!=defaults.GetFloat("_UseAOMap"))throw new Exception("Added AO selector differs from shader default");
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
    helper=O/'PirateWoodMigrationRenderV280.cs';helper.write_text(text,'utf-8')
    original=read(W/'pirate-source-thumbnail-batch-0129-0136-v277/PREPARED.v1.json')
    selected=original['cases'];cases=[];blocked={c['id']:c['material_guids'] for c in guard['cases_refused_source_parity']};assert all(len(blocked[c['id']])==1 for c in selected)
    for i,source in enumerate(selected):
        for label,restore in [('warmup',False),('current',False),('prior-observed-semantics',True),('current-repeat',False)]:
            folder=O/f'case-{i:02d}-{label}';folder.mkdir()
            cases.append(dict(source,id=source['id']+':'+label,output=str(folder/'NATIVE-RESULT.v1.json'),restore_prior_mask_and_stencil=restore,comparison_material_guid=blocked[source['id']][0]))
    (O/'manifest.json').write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins={str(p):sha(p) for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*') if p.is_file()}
    for p in [Path(__file__),base,helper,O/'manifest.json',guard_path,
              PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,cases=cases,adaptations=replacements,
        scope='Eight prefabs, two views each; warmup, current, restored authored stencil fields with added defaults checked, current repeat. No persisted material writes.'),indent=2),'utf-8')
    save(dict(status='pirate_material_migration_render_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,root=str(O),prefabs=8,captures=32,views=64)))

def run():
    state=read(O/'RUN-RESULT.v1.json');broker=None;namespace=None
    try:
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve other Unity'
        prep=read(O/'PREPARED.v1.json');pins=prep['input_pins']
        for p,h in pins.items():assert sha(p)==h,p
        target=PROJECT/'Assets/Editor/PirateWoodMigrationRenderV280.cs';assert not target.exists();shutil.copyfile(O/target.name,target)
        recipe=S/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(recipe.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=O,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(recipe),'exec'),namespace);broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(O/'temp'),TMP=str(O/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (O/'unity.stdout.log').open('xb') as out,(O/'unity.stderr.log').open('xb') as err:
            proc=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-quit','-force-d3d11','-job-worker-count','2',
                '-upmIpcPath',label,'-projectPath',str(PROJECT),'-executeMethod','OpenRPGPirateWoodMigrationRenderV280.Export',
                '--openrpg-manifest',str(O/'manifest.json'),'-logFile',str(O/'unity.log')],env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='comparing_pirate_migration_in_native_hdrp',unity_pid=proc.pid);save(state);state['unity_exit_code']=proc.wait()
        state['input_changes']=[dict(path=p,before=h,after=sha(p)) for p,h in pins.items() if sha(p)!=h]
        assert not state['input_changes'] and sha(target)==sha(O/target.name)
        assert state['unity_exit_code']==0
        batch=read(O/'NATIVE-RESULT.v1.json');assert len(batch['cases'])==32
        outputs={};pairs=[]
        for i in range(8):
            cases=batch['cases'][i*4+1:(i+1)*4];receipts=[]
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
        state.update(status='pirate_migration_native_comparison_completed',comparisons=pairs,output_pins=outputs,
            all_views_repeat_exact=all(p['current_repeat_exact'] for p in pairs),all_views_migration_exact=all(p['migration_raw_exact'] for p in pairs),
            original_version_pipeline_equivalence=False,qualified_scope='Observed wood stencil changes and added defaults in current HDRP thumbnail fixture only; original SDK pipeline equivalence unqualified')
    except Exception as exc:state.update(status='pirate_migration_comparison_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    finally:
        if broker is not None:namespace['stop_broker'](broker,state)
        state['finished_utc']=datetime.now(timezone.utc).isoformat();save(state)

if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
