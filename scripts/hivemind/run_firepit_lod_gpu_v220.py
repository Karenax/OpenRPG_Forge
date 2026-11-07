"""Render every authored Firepit LOD separately through the current common host.

Explicit diagnostic selection is not an automatic LOD or effects qualification.
All outputs and compiler scratch stay on F; previous fixtures remain immutable.
"""
from pathlib import Path
import datetime, hashlib, json, os, shutil, struct, subprocess, sys, traceback

W = Path('F:/OpenRPG_ImportWork')
R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
B = W / 'firepit-source-shadow-origin-native-v1'
O = W / 'firepit-current-lod-gpu-v220'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
TARGET = W / 'firepit-static-glb-product-check-v2/target'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()
def save(v):
    p = O / 'RUN-RESULT.v1.json'
    tmp = p.with_suffix('.tmp'); tmp.write_text(json.dumps(v, indent=2), 'utf-8'); tmp.replace(p)
def check(pins):
    for p, h in pins.items(): assert sha(p) == h, p

TAIL = r'''
 app.world_mut().insert_resource(bevy::light::GlobalAmbientLight{brightness:0.,..default()});
 let direction=reflection.transform_vector3(Vec3::new(0.8,-0.4,0.6)).normalize();
 app.world_mut().spawn((DirectionalLight{illuminance:case["source_light"]["intensity"].as_f64().unwrap() as f32,shadow_maps_enabled:false,..default()},Transform::from_translation(Vec3::Y*5.).looking_to(direction,Vec3::Y)));
 let target=app.world_mut().resource_mut::<Assets<Image>>().add(Image::new_target_texture(512,512,bevy::render::render_resource::TextureFormat::Rgba16Float,None));
 app.world_mut().spawn((Camera3d::default(),bevy::camera::Hdr,Camera{clear_color:ClearColorConfig::Custom(Color::linear_rgb(0.025,0.035,0.05)),..default()},bevy::camera::RenderTarget::Image(target.clone().into()),Msaa::Off,bevy::core_pipeline::tonemapping::Tonemapping::None,bevy::camera::Exposure{ev100:10.},Projection::Perspective(PerspectiveProjection{fov:30_f32.to_radians(),near:0.01,far:(radius*12.).max(10.),aspect_ratio:1.,..default()}),Transform::from_translation(camera_eye).looking_at(camera_target,Vec3::Y),bevy::core_pipeline::prepass::DepthPrepass,bevy::core_pipeline::prepass::NormalPrepass));
 let groups=glb.document()["extras"]["original_lod_groups"].as_array().ok_or("LOD declarations absent")?;assert_eq!(groups.len(),1);
 let levels=groups[0]["levels"].as_array().ok_or("LOD levels absent")?;assert_eq!(levels.len(),4);
 let mut captures=Vec::new();
 for (level,declaration) in levels.iter().enumerate(){
  let paths=declaration["renderer_node_paths"].as_array().ok_or("LOD renderer paths absent")?;
  let mut visible=Vec::new();
  for draw in &draws {let parent=app.world().get::<ChildOf>(*draw).unwrap().parent();let index=*nodes.iter().find(|(_,e)|**e==parent).unwrap().0;let path=&glb.document()["nodes"][index]["extras"]["unity_source_node"]["path"];let selected=paths.contains(path);app.world_mut().entity_mut(*draw).insert(if selected{Visibility::Inherited}else{Visibility::Hidden});if selected{visible.push(index);}}
  assert_eq!(visible.len(),1,"Each declared LOD must bind exactly one mesh");
  app.world_mut().entity_mut(root).insert(Visibility::Inherited);
  host.update_runtime_frame(app.world_mut(),&frame)?;
  for _ in 0..90{app.update();std::thread::sleep(Duration::from_millis(15));}
  let mut pipeline_rows=Vec::new();
  let cache=app.get_sub_app(RenderApp).unwrap().world().resource::<bevy::render::render_resource::PipelineCache>();
  for p in cache.pipelines(){let bevy::render::render_resource::PipelineDescriptor::RenderPipelineDescriptor(d)=&p.descriptor else{continue;};let known=materials.values().any(|h|{let k=app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.program_key;let ids=[k.vertex,k.fragment,k.prepass_vertex,k.prepass_fragment].map(|id|bevy::asset::AssetId::<Shader>::Uuid{uuid:bevy::asset::uuid::Uuid::from_u128(id)});ids.contains(&d.vertex.shader.id())||d.fragment.as_ref().is_some_and(|f|ids.contains(&f.shader.id()))});if known{let(state,error)=match &p.state{bevy::render::render_resource::CachedPipelineState::Ok(_)=>("ready",None),bevy::render::render_resource::CachedPipelineState::Err(e)=>("error",Some(e.to_string())),_=>("waiting",None)};pipeline_rows.push(serde_json::json!({"state":state,"error":error}));}}
  std::fs::write(folder.join(format!("LOD-{level}-PIPELINES.json")),serde_json::to_vec_pretty(&pipeline_rows)?)?;
  assert!(!pipeline_rows.is_empty()&&pipeline_rows.iter().all(|p|p["state"]=="ready"),"Source pipelines not ready");
  for repeat in 0..2{
   let raw_path=folder.join(format!("lod-{level}-{repeat}.rgba16f"));let meta_path=folder.join(format!("lod-{level}-{repeat}.metadata.json"));assert!(!raw_path.exists()&&!meta_path.exists());let a=raw_path.clone();let b=meta_path.clone();
   app.world_mut().spawn(bevy::render::view::window::screenshot::Screenshot::image(target.clone())).observe(move|e:On<bevy::render::view::window::screenshot::ScreenshotCaptured>|{let d=&e.image.texture_descriptor;let data=e.image.data.as_ref().unwrap();std::fs::write(&a,data).unwrap();std::fs::write(&b,serde_json::to_vec_pretty(&serde_json::json!({"format":format!("{:?}",d.format),"width":d.size.width,"height":d.size.height,"bytes":data.len(),"gpu_readback":true})).unwrap()).unwrap();});
   let deadline=Instant::now()+Duration::from_secs(30);while !meta_path.exists(){app.update();if Instant::now()>deadline{return Err("LOD screenshot timeout".into());}std::thread::sleep(Duration::from_millis(15));}
   let meta:serde_json::Value=serde_json::from_slice(&std::fs::read(&meta_path)?)?;assert_eq!(meta["format"],"Rgba16Float");assert_eq!(meta["bytes"],512*512*8);
   captures.push(serde_json::json!({"lod":level,"repeat":repeat,"visible_nodes":visible,"raw_path":raw_path,"metadata_path":meta_path,"threshold":declaration["screen_relative_height"]}));
  }
 }
 let report=serde_json::json!({"status":"current_common_host_authored_lod_diagnostic_gpu_captures","adapter":adapter,"nodes":expected_nodes,"primitives":expected_primitives,"gpu_images":gpu_image_rows,"captures":captures,"automatic_lod_selection_verified":false,"effects_rendered":false,"source_lighting_parity":false,"shadows_qualified":false,"studio_verified":false,"full_import":false});
 std::fs::write(&args[2],serde_json::to_vec_pretty(&report)?)?;Ok(())
}
fn host_basis(glb:&forge_assets::source_graph_glb::SourceGraphGlb<'_>)->Result<source_graph_context::ObjectBasis,String>{source_graph_context::ObjectBasis::from_document(glb.document())?.ok_or("Declared source basis absent".into())}
'''

def prepare():
    assert not O.exists(); O.mkdir(); (O/'src').mkdir(); (O/'temp').mkdir()
    text=(B/'src/main.rs').read_text('utf-8')
    text=text[text.index('#[allow(dead_code)]'):text.index(' host.install_shadow_routes')]+TAIL
    (O/'src/main.rs').write_text(text,'utf-8')
    cargo=(B/'Cargo.toml').read_text('utf-8').replace('openrpg-firepit-source-ao-scene-v1','openrpg-firepit-lod-gpu-v220')
    (O/'Cargo.toml').write_text(cargo,'utf-8')
    lock=(B/'Cargo.lock').read_text('utf-8').replace('openrpg-firepit-source-ao-scene-v1','openrpg-firepit-lod-gpu-v220')
    (O/'Cargo.lock').write_text(lock,'utf-8'); shutil.copyfile(B/'CASE.v1.json',O/'CASE.v1.json')
    assembly=W/'firepit-complete-static-glb-v3/RESULT.v1.json'; row=read(assembly)['rows'][0]; candidate=Path(row['glb']); assert sha(candidate)==row['sha256']
    raw=candidate.read_bytes();doc=json.loads(raw[20:20+struct.unpack_from('<I',raw,12)[0]])
    groups=doc['extras']['original_lod_groups'];assert len(groups)==1 and len(groups[0]['levels'])==4
    case=read(O/'CASE.v1.json')
    inputs=[Path(__file__),B/'src/main.rs',assembly,candidate,O/'src/main.rs',O/'Cargo.toml',O/'Cargo.lock',O/'CASE.v1.json',R/'Cargo.toml',R/'Cargo.lock']
    for crate in ['forge_engine','forge_assets','forge_core']:
        inputs.extend(p for p in (R/'crates'/crate).rglob('*') if p.is_file() and (p.suffix in ['.rs','.wgsl'] or p.name=='Cargo.toml'))
    inputs.extend((R/'apps/forge_studio/src/authoring_assets').glob('source_graph_*.rs'))
    inputs.extend([Path(case['brdf_lookup_protocol']['inputs']['rgba32f']),Path(case['sky_cube_packet']['path'])])
    pins={str(p):sha(p) for p in inputs};check(pins)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(candidate=row,input_pins=pins,original_lod_groups=groups,diagnostic_only=True,full_import=False),indent=2),'utf-8')
    save(dict(status='prepared_current_common_host_lod_gpu_diagnostic',full_import=False))
    with (O/'controller.log').open('xb') as log:
        p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid)),'utf-8');print(json.dumps(dict(root=str(O),controller_pid=p.pid)))

def run():
    state=read(O/'RUN-RESULT.v1.json')
    try:
        prep=read(O/'PREPARED.v1.json');check(prep['input_pins'])
        env=os.environ.copy();env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(TARGET),CARGO_BUILD_JOBS='1',CARGO_INCREMENTAL='0',TEMP=str(O/'temp'),TMP=str(O/'temp'))
        with (O/'cargo.log').open('xb') as log:
            p=subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml')],cwd=O,env=env,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='building_current_common_host_lod_gpu_diagnostic',cargo_pid=p.pid);save(state);state['cargo_exit_code']=p.wait()
        assert state['cargo_exit_code']==0,'See cargo.log';check(prep['input_pins'])
        exe=TARGET/'debug/openrpg-firepit-lod-gpu-v220.exe';shutil.copyfile(exe,O/'capture.exe');state['exe_sha256']=sha(O/'capture.exe')
        with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
            p=subprocess.Popen([str(O/'capture.exe'),prep['candidate']['glb'],str(O/'NATIVE-RESULT.v1.json')],cwd=O,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='executing_current_common_host_lod_gpu_diagnostic',capture_pid=p.pid);save(state);state['capture_exit_code']=p.wait()
        assert state['capture_exit_code']==0,'See stderr.log';check(prep['input_pins'])
        result=read(O/'NATIVE-RESULT.v1.json');assert len(result['captures'])==8
        state.update(status='current_common_host_four_lod_eight_gpu_captures_retained',input_pins=prep['input_pins'],native_sha256=sha(O/'NATIVE-RESULT.v1.json'),capture_pins={r['raw_path']:sha(r['raw_path']) for r in result['captures']},visual_review_pending=True,automatic_lod_selection_verified=False,effects_rendered=False,full_import=False)
    except Exception as exc:state.update(status='current_common_host_lod_gpu_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save(state)

if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
