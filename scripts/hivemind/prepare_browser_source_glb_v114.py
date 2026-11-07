"""Port the actual workspace GLB probe to async Web; purchased bytes stay on F."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
O = W / 'browser-source-glb-v114'
S = R / 'scripts/hivemind'
assert not O.exists()
O.mkdir()
(O / 'source-before').mkdir()
for path in [R / 'Cargo.lock', R / 'crates/forge_engine/Cargo.toml']:
    shutil.copy2(path, O / 'source-before' / (path.parent.name + '.' + path.name))
native = R / 'crates/forge_engine/examples/verify_source_glb_common_native_srgb_v91.rs'
source = native.read_text(encoding='utf-8')
start = source.index('    let args: Vec<_>')
end = source.index('    let glb = forge_assets::source_graph_glb::read_source_graph_glb', start)
vector = source[source.index('    let vector = |field:', start):source.index('    let raw = std::fs::read', start)]
source = source[:start] + '''    let root = Path::new("local-capture-keys");
    let mut captures = std::collections::BTreeMap::<String, Vec<u8>>::new();
    let input: serde_json::Value = serde_json::from_str(&input_json)?;
    let context = input["context"].clone();
''' + vector + source[end:]
for name in ['brdf_bytes', 'sky_bytes']:
    start = source.index(f'    let {name} = std::fs::read(')
    end = source.index(')?;', start) + 3
    source = source[:start] + source[end:]
assert 'std::fs::read' not in source
source = source.replace('std::fs::write(', 'capture(&mut captures, ')
source = source.replace('fn main() -> Result<(), Box<dyn std::error::Error>> {',
                        'async fn run_inner(input_json: String, raw: Vec<u8>, brdf_bytes: Vec<u8>, sky_bytes: Vec<u8>) -> Result<String, Box<dyn std::error::Error>> {')
source = source.replace('time::{Duration, Instant}', 'time::Duration')
source = source.replace('Backends::DX12', 'Backends::BROWSER_WEBGPU')
for delay in [10, 20]:
    source = source.replace(f'std::thread::sleep(Duration::from_millis({delay}));', 'next_frame().await?;')
source = source.replace('    device.wgpu_device().poll(PollType::Poll)?;\n', '')
assert 'std::thread' not in source and '.poll(PollType' not in source
anchor = '''    app.world_mut().insert_resource(held_device);
    app.world_mut().run_schedule(Last);'''
assert source.count(anchor) == 1
source = source.replace(anchor, '''    app.world_mut().insert_resource(held_device);
    let recovery_deadline = Instant::now() + Duration::from_secs(30);
    loop {
        app.world_mut().run_schedule(Last);
        let current = app.world().resource::<Assets<SourceGraphMaterial>>().get(&material_handle).ok_or("Recovery material absent")?;
        if forge_engine::source_graph::SourceGraphKey::from(&current.extension).native_srgb_encoder_ready { break; }
        if Instant::now() > recovery_deadline { return Err("Asynchronous runtime recovery timed out".into()); }
        next_frame().await?;
    }''')
anchor = '    capture(&mut captures, root.join("FORGE-PACKED.rg11b10"), &captured_image)?;'
assert source.count(anchor) == 1
source = source.replace(anchor, '''    // Require fresh stable GPU frames after the actual runtime recovery.
    pixels.lock().unwrap().take();
    let recovery_draw_deadline = Instant::now() + Duration::from_secs(90);
    let mut recovery_frames = 0u32;
    let mut recovery_stable = 0u32;
    let mut recovery_previous: Option<Vec<u8>> = None;
    loop {
        app.update(); recovery_frames += 1;
        let pending = app.get_sub_app(RenderApp).unwrap().world().resource::<PipelineCache>().waiting_pipelines().count();
        if recovery_frames > 30 && pending == 0 {
            if let Some(bytes) = pixels.lock().unwrap().take() {
                if bytes.len() != 512 * 512 * 4 { return Err("Recovery GPU image size differs".into()); }
                if recovery_previous.as_ref() == Some(&bytes) { recovery_stable += 1; } else { recovery_stable = 0; }
                recovery_previous = Some(bytes.clone());
                if recovery_stable >= 2 {
                    if bytes != captured_image { return Err("Recovered draw differs from the same-device baseline".into()); }
                    break;
                }
            }
        }
        if Instant::now() > recovery_draw_deadline { return Err("Recovered material did not produce stable GPU frames".into()); }
        next_frame().await?;
    }
    if !errors.lock().unwrap().is_empty() { return Err("GPU errors after recovery draw".into()); }
''' + anchor)
source = source.replace('"gpu_frame_after_recovery_verified":false', '"gpu_frame_after_recovery_verified":true')
anchor = '    Ok(())\n}'
assert source.count(anchor) == 1
source = source.replace(anchor, '''    let bindings: serde_json::Value = serde_json::from_slice(&captures["RESOURCE-RESULT.v1.json"])?;
    let draw: serde_json::Value = serde_json::from_slice(&captures["NATIVE-RESULT.v1.json"])?;
    Ok(serde_json::json!({"status":"actual_common_browser_glb_draw_binding_and_recovery_verified",
        "browser_gpu_executed":true,"resource_receipt":bindings,"draw_receipt":draw,
        "packed_image":captures["FORGE-PACKED.rg11b10"],
        "material_curve":captures["MATERIAL-CURVE.r32f"],
        "recovered_material_curve":captures["RECOVERED-MATERIAL-CURVE.r32f"],
        "recovery_frames":recovery_frames,"recovery_consecutive_equal_readbacks":recovery_stable+1,
        "recovery_pixels_byte_equal":true,"gpu_errors":errors.lock().unwrap().clone(),
        "browser_material_draw_verified":true,"editor_visual_verified":false,"full_import":false}).to_string())
}''')
helpers = '''
use bevy::platform::time::Instant;
use wasm_bindgen::{prelude::*, JsCast};

fn capture(path_map: &mut std::collections::BTreeMap<String, Vec<u8>>, path: impl AsRef<Path>, bytes: impl AsRef<[u8]>) -> Result<(), std::io::Error> {
    let key = path.as_ref().file_name().unwrap().to_string_lossy().into_owned();
    path_map.insert(key, bytes.as_ref().to_vec()); Ok(())
}
async fn next_frame() -> Result<(), JsValue> {
    let promise = js_sys::Promise::new(&mut |resolve, reject| {
        let callback = Closure::once_into_js(move |_time:f64| { let _ = resolve.call0(&JsValue::NULL); });
        if let Some(window) = web_sys::window() {
            if let Err(error) = window.request_animation_frame(callback.unchecked_ref()) { let _ = reject.call1(&JsValue::NULL, &error); }
        } else { let _ = reject.call1(&JsValue::NULL, &JsValue::from_str("Browser window absent")); }
    });
    wasm_bindgen_futures::JsFuture::from(promise).await?; Ok(())
}
#[wasm_bindgen]
pub async fn run_glb_probe(input: String, raw: Vec<u8>, brdf: Vec<u8>, sky: Vec<u8>) -> Result<String, JsValue> {
    console_error_panic_hook::set_once();
    run_inner(input, raw, brdf, sky).await.map_err(|error| JsValue::from_str(&error.to_string()))
}
'''
example = R / 'crates/forge_engine/examples/verify_browser_source_glb_v114.rs'
assert not example.exists()
example.write_bytes(('//! Async Web test of the actual common loader; no purchased bytes embedded.\n'
                    '#[cfg(target_arch = "wasm32")]\nmod browser {\n' + source + helpers + '\n}\n').encode())
sha = lambda path: hashlib.sha256(path.read_bytes()).hexdigest()
(O / 'PORT-PREPARED.v1.json').write_text(json.dumps(dict(
    status='actual_glb_probe_port_written_not_compiled', prepared_utc=datetime.now(timezone.utc).isoformat(),
    native_source=str(native), native_source_sha256=sha(native), web_example=str(example),
    web_example_sha256=sha(example), file_input_removed=True, common_loader_and_material_retained=True,
    authored_images_and_mips_retained=True, recovery_gpu_redraw_required=True,
    browser_gpu_executed=False, full_import=False,
), indent=2) + '\n', encoding='utf-8')
print(str(example))
