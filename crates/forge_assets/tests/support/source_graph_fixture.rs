//! Original synthetic source graph shared by media and GPU qualification.
use forge_core::bytes_sha256;
use serde_json::{Value, json};

pub const FORWARD: &str = r"
@vertex fn vertex(@location(0) position: vec3<f32>) -> @builtin(position) vec4<f32> {
    return vec4<f32>(position.xy, 0.5, 1.0);
}
@fragment fn fragment() -> @location(0) vec4<f32> {
    return vec4<f32>(1.0, 0.0, 0.0, 1.0);
}
";
const PREPASS: &str = r"
@vertex fn vertex(@location(0) position: vec3<f32>) -> @builtin(position) vec4<f32> {
    return vec4<f32>(position.xy, 0.5, 1.0);
}
@fragment fn fragment() {}
";

pub fn fixture() -> Result<Vec<u8>, String> {
    fixture_with_stages(FORWARD, PREPASS)
}

#[expect(
    clippy::too_many_lines,
    reason = "Keep the synthetic GLB and its explicit source contract together"
)]
pub fn fixture_with_stages(forward: &str, prepass: &str) -> Result<Vec<u8>, String> {
    let mut bin = Vec::new();
    let mut views = Vec::<Value>::new();
    let mut add = |bytes: &[u8], target: Option<u32>| {
        while !bin.len().is_multiple_of(4) {
            bin.push(0);
        }
        let mut view = json!({"buffer":0,"byteOffset":bin.len(),"byteLength":bytes.len()});
        if let Some(target) = target {
            view["target"] = json!(target);
        }
        views.push(view);
        bin.extend_from_slice(bytes);
    };
    let floats = |values: &[f32]| {
        values
            .iter()
            .flat_map(|v| v.to_le_bytes())
            .collect::<Vec<_>>()
    };
    add(
        &floats(&[-0.8, -0.8, 0., 0.8, -0.8, 0., 0., 0.8, 0.]),
        Some(34962),
    );
    add(&floats(&[0., 0., 1., 0., 0., 1., 0., 0., 1.]), Some(34962));
    add(
        &floats(&[1., 0., 0., 1., 1., 0., 0., 1., 1., 0., 0., 1.]),
        Some(34962),
    );
    add(&floats(&[0., 0., 1., 0., 0.5, 1.]), Some(34962));
    add(&[0, 0, 1, 0, 2, 0], Some(34963));
    add(
        &floats(&[0., 0., 0., 0., 1., 0., 0., 0., 0.5, 1., 0., 0.]),
        Some(34962),
    );
    add(forward.as_bytes(), None);
    add(prepass.as_bytes(), None);
    while !bin.len().is_multiple_of(4) {
        bin.push(0);
    }
    let identity = [
        1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.,
    ];
    let graph = json!({
        "schema_version":4,
        "source_program_sha256":bytes_sha256(forward.as_bytes()),
        "source_material_sha256":bytes_sha256(b"original synthetic scene material"),
        "forward":{"buffer_view":6,"sha256":bytes_sha256(forward.as_bytes())},
        "prepass":{"buffer_view":7,"sha256":bytes_sha256(prepass.as_bytes())},
        "libraries":[],"textures":[],
        "requires_original_vertex_color":false,
        "normal_decode_modes":[null,null,null,null,null,null,null,null],
        "state":{"color_write_mask":15,"color_blend":[1,0],"alpha_blend":[1,0],
            "forward_cull":0,"prepass_cull":0,"depth_compare":4,"write_depth":true,
            "transparent":false,"alpha_test":false,"alpha_to_coverage":false,
            "render_queue":2000,"disabled_passes":[]}
    });
    let document = json!({
        "asset":{"version":"2.0"},
        "extensionsUsed":["OPENRPG_source_graph"],
        "extensionsRequired":["OPENRPG_source_graph"],
        "buffers":[{"byteLength":bin.len()}],"bufferViews":views,
        "accessors":[
            {"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[-0.8,-0.8,0.0],"max":[0.8,0.8,0.0]},
            {"bufferView":1,"componentType":5126,"count":3,"type":"VEC3"},
            {"bufferView":2,"componentType":5126,"count":3,"type":"VEC4"},
            {"bufferView":3,"componentType":5126,"count":3,"type":"VEC2"},
            {"bufferView":4,"componentType":5123,"count":3,"type":"SCALAR"},
            {"bufferView":5,"componentType":5126,"count":3,"type":"VEC4"}
        ],
        "materials":[{"extensions":{"OPENRPG_source_graph":graph}}],
        "meshes":[{"primitives":[{
            "attributes":{"POSITION":0,"NORMAL":1,"TANGENT":2,"TEXCOORD_0":3,
                "_OPENRPG_SOURCE_NORMAL":1,"_OPENRPG_SOURCE_TANGENT":2,"_OPENRPG_SOURCE_UV0":5},
            "indices":4,"material":0
        }]}],
        "nodes":[{"mesh":0,"extras":{"unity_source_node":{
            "path":"fixture","parent_path":"","active_self":true,"active_source_parent_chain":true,
            "renderers":[{"component":{"guid":"00000000000000000000000000000001","file_id":"1",
                "exact_asset_identity_available":true},"enabled":true,"force_rendering_off":false,
                "shadow_casting_mode":0,"receive_shadows":true}]
        }}}],
        "scenes":[{"nodes":[0]}],"scene":0,
        "extras":{"source_frame_binding":{"source_object_from_target_object":identity,
            "target_object_from_source_object":identity,
            "source_world_from_target_world":identity,
            "source_absolute_world_from_target_world":identity},"original_lod_groups":[]}
    });
    let mut json = serde_json::to_vec(&document).map_err(|e| e.to_string())?;
    json.resize(json.len().next_multiple_of(4), b' ');
    let mut bytes = b"glTF".to_vec();
    bytes.extend(2_u32.to_le_bytes());
    bytes.extend(
        u32::try_from(28 + json.len() + bin.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(
        u32::try_from(json.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(b"JSON");
    bytes.extend(json);
    bytes.extend(
        u32::try_from(bin.len())
            .map_err(|e| e.to_string())?
            .to_le_bytes(),
    );
    bytes.extend(b"BIN\0");
    bytes.extend(bin);
    Ok(bytes)
}
