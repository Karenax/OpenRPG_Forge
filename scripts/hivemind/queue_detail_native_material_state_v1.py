"""Capture Detail's effective properties/passes after the owned Rust tests finish."""
from pathlib import Path
import copy,datetime,hashlib,json,os,subprocess,sys,time
from verify_native_material_snapshot import reconcile
from verify_native_urp_material_snapshot import reconcile as independent_reconcile

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-native-material-state-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
WAIT=W/'source-graph-scalar-transport-native-v5/RUN-RESULT.v1.json'

def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,value):
    p=ROOT/(name+'.tmp');p.write_text(json.dumps(value,indent=2),'utf-8');p.replace(ROOT/name)
def check(pins):
    for p,h in pins.items():assert sha(Path(p))==h,p
def idle():
    count=subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()
    assert count=='0','Preserve another active Unity'

def prepare():
    assert not ROOT.exists();idle()
    guid='abc00000000012210656512993269337'
    source_path=W/'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    source=next(r for r in read(source_path)['rows'] if r['material_identity']['guid']==guid)
    kernel_path=W/'detail-native-default-kernel-v1/RESULT.v1.json';kernel=read(kernel_path)
    assert kernel['actual_naga_exit_code']==0 and len(kernel['source_texture_bindings'])==2
    helper=PROJECT/'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs'
    warm_path=W/'unity-medieval-warm-native-hdrp-v2/PREPARED.v2.json'
    warm=read(warm_path);assert sha(helper)==warm['input_pins'][str(helper)]
    private_mat=PROJECT/source['material_identity']['asset_path']
    assert private_mat.exists()
    envs={k:v for e in source['source_material_properties']['m_SavedProperties']['m_TexEnvs'] for k,v in e.items()}
    bindings_path=W/'nested-prefab-delta-graph-property-bindings-v1/RESULT.v1.json'
    b=next(r for r in read(bindings_path)['rows'] if r['material_identity']['guid']==guid)
    allowed={r['guid'] for r in kernel['source_texture_bindings']}
    textures=[]
    for property in b['bindings']:
        if not property['source_property_object']['m_Type'].endswith('.Texture2DShaderProperty'):continue
        name=property['reference_name'];ref=envs[name]['m_Texture']
        if ref.get('guid') in allowed:
            textures.append(dict(property=name,guid=ref['guid'],file_id=str(ref['fileID'])))
    assert len(textures)==2 and {r['guid'] for r in textures}==allowed
    files=[Path(__file__).resolve(),source_path,kernel_path,bindings_path,helper,warm_path,
        Path(sys.modules[reconcile.__module__].__file__),Path(sys.modules[independent_reconcile.__module__].__file__)]
    files += [PROJECT/r for r in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json']]
    original_pins=dict(kernel['input_pins'])
    for p in files:original_pins[str(p)]=sha(p)
    check(original_pins)
    ROOT.mkdir();(ROOT/'private-material.before.mat').write_bytes(private_mat.read_bytes())
    material=source['source_material_properties']
    save('manifest.v1.json',dict(output_path=str(ROOT/'NATIVE-RESULT.v1.json'),cases=[dict(material_guid=guid,
        asset_path=source['material_identity']['asset_path'],expected_shader_guid=b['graph']['source_shader_guid'],
        expected_shader_file_id=str(material['m_Shader']['fileID']),required_textures=textures)]))
    original_pins[str(ROOT/'manifest.v1.json')]=sha(ROOT/'manifest.v1.json')
    save('PREPARED.v1.json',dict(input_pins=original_pins,private_material=str(private_mat),
        private_material_before_sha256=sha(private_mat),helper=str(helper),source_scripts_executed=False,
        omitted_missing_mask_remains_native_null=True,missing_mask_default_has_separate_aov_receipt=True,
        original_hdrp_package_version_qualified=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,materials=1,required_textures=2,waiting_for_owned_rust_tests=True)))

def run():
    state=dict(status='waiting_for_owned_rust_tests',native_materials_verified=0,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        while not WAIT.exists() or not read(WAIT).get('finished_utc'):time.sleep(15)
        p=read(ROOT/'PREPARED.v1.json');check(p['input_pins']);idle()
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit','-projectPath',str(PROJECT),
                '-executeMethod','OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export',
                '--openrpg-manifest',str(ROOT/'manifest.v1.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_detail_native_material_state',unity_pid=child.pid);save('RUN-RESULT.v1.json',state)
            state['actual_unity_exit_code']=child.wait()
        assert state['actual_unity_exit_code']==0
        manifest=read(ROOT/'manifest.v1.json');native=read(ROOT/'NATIVE-RESULT.v1.json')
        assert len(reconcile(manifest,native))==len(independent_reconcile(manifest,native))==1
        refused=[]
        for name in ['wrong_shader','uncompiled_pass','missing_material','wrong_texture']:
            bad=copy.deepcopy(native)
            if name=='wrong_shader':bad['cases'][0]['shader_guid']='0'*32
            elif name=='uncompiled_pass':bad['cases'][0]['passes'][0]['compiled']=False
            elif name=='missing_material':bad['cases'].pop()
            else:
                property=manifest['cases'][0]['required_textures'][0]['property']
                next(r for r in bad['cases'][0]['properties'] if r['name']==property)['texture_guid']='0'*32
            try:independent_reconcile(manifest,bad)
            except ValueError as e:refused.append(dict(test=name,reason=str(e)))
            else:raise AssertionError('Changed material capture accepted')
        check(p['input_pins'])
        private=Path(p['private_material']);after=sha(private)
        (ROOT/'private-material.after.mat').write_bytes(private.read_bytes())
        save('INDEPENDENT-REOPEN.v1.json',dict(native_materials_verified=1,active_texture_bindings_verified=2,
            native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),refused_mutations=refused,
            private_material_before_sha256=p['private_material_before_sha256'],private_material_after_sha256=after,
            private_material_unchanged=after==p['private_material_before_sha256'],source_pins_unchanged=True,
            original_hdrp_package_version_qualified=False,gpu_render_parity_qualified=False,full_import=False))
        state.update(status='detail_native_material_state_and_two_bindings_reopened',native_materials_verified=1,
            active_texture_bindings_verified=2,native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'))
    except Exception as error:state.update(status='refused_or_unverified',error=repr(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
