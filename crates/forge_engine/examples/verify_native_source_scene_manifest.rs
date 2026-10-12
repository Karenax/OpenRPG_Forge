// Verify immutable native source scenes using the current canonical reader.

use forge_assets::source_graph_glb::{
    SourceGraphGlb, SourceGraphGlbReadBudget, read_source_graph_glb_with_budget,
};
use forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry;
use serde_json::{Value, json};
fn raw_hash<const N: usize>(values: &[[f32; N]]) -> String {
    let data: Vec<u8> = values
        .iter()
        .flat_map(|row| row.iter().flat_map(|v| v.to_le_bytes()))
        .collect();
    forge_core::bytes_sha256(&data).to_string()
}
fn changed_json(raw: &[u8], mutate: impl FnOnce(&mut Value)) -> Vec<u8> {
    let n = u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
    let mut doc: Value = serde_json::from_slice(&raw[20..20 + n]).unwrap();
    mutate(&mut doc);
    let mut encoded = serde_json::to_vec(&doc).unwrap();
    while !encoded.len().is_multiple_of(4) {
        encoded.push(b' ');
    }
    let mut result = b"glTF".to_vec();
    result.extend(2u32.to_le_bytes());
    result.extend(
        u32::try_from(raw.len() - n + encoded.len())
            .expect("GLB byte length must fit its u32 field")
            .to_le_bytes(),
    );
    result.extend(
        u32::try_from(encoded.len())
            .expect("GLB JSON chunk length must fit its u32 field")
            .to_le_bytes(),
    );
    result.extend(b"JSON");
    result.extend(encoded);
    result.extend(&raw[20 + n..]);
    result
}
fn verify_negative_controls(
    raw: &[u8],
    glb: &SourceGraphGlb<'_>,
    budget: SourceGraphGlbReadBudget,
) -> usize {
    let (index, material) = glb
        .materials
        .iter()
        .find(|(_, m)| !m.textures.is_empty())
        .unwrap();
    let storage = material.textures[0].imported_storage.as_ref().unwrap();
    let view = &glb.document()["bufferViews"][storage.storage.buffer_view];
    let json_len = u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
    let at = 28
        + json_len
        + usize::try_from(view["byteOffset"].as_u64().unwrap_or(0))
            .expect("Buffer view byte offset must fit usize");
    let mut changed = raw.to_vec();
    changed[at] ^= 1;
    assert!(read_source_graph_glb_with_budget(&changed, budget).is_err());
    let changed = changed_json(raw, |d| {
        d["materials"][*index]["extensions"]["OPENRPG_source_graph"]["textures"][0]["imported_storage"]
            ["capture_case_index"] = json!(999_999);
    });
    assert!(read_source_graph_glb_with_budget(&changed, budget).is_err());
    let changed = changed_json(raw, |d| {
        d["materials"][*index]["extensions"]["OPENRPG_source_graph"]["normal_decode_modes"] =
            json!([null, null, null, null, null, null, null, null]);
    });
    assert!(read_source_graph_glb_with_budget(&changed, budget).is_err());
    3
}
fn main() {
    let mut args = std::env::args().skip(1);
    let path = args
        .next()
        .expect("Explicit immutable input manifest required");
    let budget = match args.next().as_deref() {
        None => SourceGraphGlbReadBudget::Standard,
        Some("--large-desktop") => SourceGraphGlbReadBudget::LargeDesktop,
        Some(_) => panic!("Usage: verify_native_source_scene_manifest MANIFEST [--large-desktop]"),
    };
    assert!(args.next().is_none(), "Unexpected reader argument");
    let rows: Vec<Value> = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    let mut scenes = 0;
    let mut geometries = 0;
    let mut textures = 0;
    let mut mips = 0;
    let mut negative = 0;
    for row in rows {
        let file = row["glb"].as_str().unwrap();
        let byte_length = usize::try_from(std::fs::metadata(file).unwrap().len())
            .expect("Carrier length must fit usize");
        budget.validate_byte_length(byte_length).unwrap();
        let raw = std::fs::read(file).unwrap();
        assert_eq!(
            forge_core::bytes_sha256(&raw).to_string(),
            row["sha256"].as_str().unwrap()
        );
        let glb = read_source_graph_glb_with_budget(&raw, budget)
            .unwrap()
            .expect("Source materials must be declared");
        let plan = prepare_source_graph_scene_geometry(&glb, None).unwrap();
        let basis = &glb.document()["extras"]["source_frame_binding"];
        let reflected = json!([-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        assert_eq!(basis["target_object_from_source_object"], reflected);
        assert_eq!(basis["source_object_from_target_object"], reflected);
        for geometry in plan.source_geometries.values() {
            assert_eq!(
                raw_hash(&geometry.source.original_normals),
                basis["source_normal_sha256"].as_str().unwrap()
            );
            assert_eq!(
                raw_hash(geometry.source.original_tangents.as_ref().unwrap()),
                basis["source_tangent_sha256"].as_str().unwrap()
            );
            geometries += 1;
        }
        for (_, material) in &glb.materials {
            assert_eq!(material.schema_version, 3);
            for texture in &material.textures {
                let storage = texture
                    .imported_storage
                    .as_ref()
                    .expect("No PNG-only fallback");
                storage.validate(&glb.views).unwrap();
                textures += 1;
                mips += storage.mips.len();
            }
        }
        if scenes == 0 {
            negative += verify_negative_controls(&raw, &glb, budget);
        }
        scenes += 1;
        println!(
            "{}",
            json!({"asset_id":row["asset_id"],"canonical_integrity_and_scene_admitted":true,"raw_captured_source_vectors_equal":true})
        );
    }
    assert!(scenes > 0);
    assert_eq!(negative, 3);
    println!(
        "{}",
        json!({"canonical_scenes":scenes,"source_geometries":geometries,"texture_associations":textures,"mip_associations":mips,"negative_controls_passed":negative,"carrier_budget":format!("{budget:?}"),"gpu_rendered":false,"studio_verified":false,"full_import":false})
    );
}
