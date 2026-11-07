"""Stage a browser test of the actual Rust producer; no source asset inputs."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,shutil
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
O=W/'srgb-browser-producer-v99';assert not O.exists();O.mkdir();(O/'src').mkdir();(O/'site').mkdir();(O/'temp').mkdir()
manifest='''[package]
name="openrpg-srgb-browser-probe"
version="0.1.0"
edition="2024"
[workspace]
[lib]
crate-type=["cdylib"]
[dependencies]
bevy={version="=0.19.1",default-features=false,features=["3d","ui","webgpu"]}
wgpu={version="=29.0.4",default-features=false,features=["wgsl","webgpu"]}
wasm-bindgen="=0.2.127"
wasm-bindgen-futures="=0.4.77"
js-sys="=0.3.104"
web-sys={version="=0.3.104",features=["Window","Performance"]}
serde_json={version="=1.0.151",features=["float_roundtrip"]}
'''
(O/'Cargo.toml').write_text(manifest,encoding='utf-8');shutil.copyfile(R/'Cargo.lock',O/'Cargo.lock')
source='''use wasm_bindgen::{prelude::*,JsCast};
use bevy::render::renderer::{RenderDevice,RenderQueue,RenderAdapterInfo,WgpuWrapper};
use std::sync::{Arc,Mutex};
'''
modules=[]
for name,file in [('source_srgb_curve','source_graph_srgb_curve.rs'),('source_srgb_measure','source_graph_srgb_measure.rs'),('source_srgb_async','source_graph_srgb_async.rs')]:
 p=R/'crates/forge_engine/src'/file;modules.append(p);source+=f'#[path="{p.as_posix()}"]\nmod {name};\n'
source+='''
async fn next_frame()->Result<(),JsValue>{
 let promise=js_sys::Promise::new(&mut |resolve,reject|{
  let callback=Closure::once_into_js(move |_time:f64|{let _=resolve.call0(&JsValue::NULL);});
  if let Some(window)=web_sys::window(){if let Err(error)=window.request_animation_frame(callback.unchecked_ref()){let _=reject.call1(&JsValue::NULL,&error);}}
  else{let _=reject.call1(&JsValue::NULL,&JsValue::from_str("Browser window absent"));}
 });
 wasm_bindgen_futures::JsFuture::from(promise).await?;Ok(())
}
#[wasm_bindgen]
pub async fn run_calibration()->Result<String,JsValue>{
 let instance=wgpu::Instance::new(wgpu::InstanceDescriptor{backends:wgpu::Backends::BROWSER_WEBGPU,..Default::default()});
 let adapter=instance.request_adapter(&wgpu::RequestAdapterOptions{power_preference:wgpu::PowerPreference::HighPerformance,force_fallback_adapter:false,..Default::default()}).await.map_err(|e|JsValue::from_str(&e.to_string()))?;
 let info=RenderAdapterInfo(WgpuWrapper::new(adapter.get_info()));
 let (raw_device,raw_queue)=adapter.request_device(&wgpu::DeviceDescriptor::default()).await.map_err(|e|JsValue::from_str(&e.to_string()))?;
 let device=RenderDevice::from(raw_device);let queue=RenderQueue(Arc::new(WgpuWrapper::new(raw_queue)));
 let errors=Arc::new(Mutex::new(Vec::<String>::new()));let sink=errors.clone();
 device.wgpu_device().on_uncaptured_error(Arc::new(move |e|sink.lock().unwrap().push(e.to_string())));
 let key=source_srgb_measure::device_key(&info);
 let mut producer=source_srgb_async::Calibration::new(&device,key.clone());
 let clock=web_sys::window().and_then(|w|w.performance()).ok_or_else(||JsValue::from_str("Browser clock absent"))?;
 let start=clock.now();let mut polls=0;let mut pending=0;let mut longest_poll_ms=0.0f64;
 let curve=loop{
  if clock.now()-start>45000.0{return Err(JsValue::from_str("Browser producer timed out"));}
  let begin=clock.now();let result=producer.poll(&device,&queue).map_err(|e|JsValue::from_str(&e))?;
  longest_poll_ms=longest_poll_ms.max(clock.now()-begin);polls+=1;
  if let Some(curve)=result{break curve;}
  pending+=1;next_frame().await?;
 };
 let bytes=curve.r32f_bytes_for_device(&key).map_err(JsValue::from_str)?;
 let clone_same=device.wgpu_device()==device.clone().wgpu_device();
 let (other,_)=adapter.request_device(&wgpu::DeviceDescriptor::default()).await.map_err(|e|JsValue::from_str(&e.to_string()))?;
 let different_device_refused=device.wgpu_device()!=&other;other.destroy();
 let errors=errors.lock().unwrap().clone();
 if !errors.is_empty()||pending==0||!clone_same||!different_device_refused{return Err(JsValue::from_str("Browser producer validation failed"));}
 Ok(serde_json::json!({"status":"actual_common_rust_producer_calibrated_on_browser_webgpu","backend":key.backend,"adapter":key.adapter,"driver":key.driver,"curve_bytes":bytes,"polls":polls,"pending_frame_polls":pending,"longest_poll_ms":longest_poll_ms,"elapsed_ms":clock.now()-start,"gpu_errors":errors,"cloned_device_identity_equal":clone_same,"second_device_identity_differs":different_device_refused,"native_runtime_device_guard_executed":false,"studio_material_drawn":false,"source_asset_inputs":0,"calibration_file_inputs":0,"browser_gpu_executed":true,"full_import":false}).to_string())
}
'''
(O/'src/lib.rs').write_bytes(source.encode())
(O/'site/index.html').write_text('''<!doctype html><html lang="fr"><meta charset="utf-8"><title>Contrôle sRGB WebGPU</title><body><h1>Contrôle sRGB WebGPU</h1><p id="state">Chargement du producteur Rust…</p><pre id="result"></pre><script type="module">
import init,{run_calibration} from './pkg/openrpg_srgb_browser_probe.js';
let ticks=0,running=true;function beat(){ticks++;if(running)requestAnimationFrame(beat);}requestAnimationFrame(beat);
try{await init();const result=JSON.parse(await run_calibration());result.page_animation_frames=ticks;document.querySelector('#state').textContent='Contrôle terminé';document.querySelector('#result').textContent=JSON.stringify(result,null,2);await fetch('/receipt',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(result)});}
catch(error){document.querySelector('#state').textContent='Contrôle refusé';await fetch('/receipt',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({status:'browser_probe_refusal',error:String(error),page_animation_frames:ticks,full_import:false})});}finally{running=false;}
</script></body></html>''',encoding='utf-8')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
paths=[Path(__file__),R/'Cargo.lock',*modules,O/'Cargo.toml',O/'src/lib.rs',O/'site/index.html']
(O/'PREPARED.v1.json').write_text(json.dumps({'status':'actual_rust_browser_probe_prepared_not_executed','prepared_utc':datetime.now(timezone.utc).isoformat(),'input_pins':{str(p):sha(p) for p in paths},'browser_gpu_executed':False,'full_import':False},indent=2),encoding='utf-8');print(str(O))
