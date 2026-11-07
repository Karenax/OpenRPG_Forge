use super::*;
use forge_assets::source_local_lights::SourceLocalLightsPackage;
use forge_project::zone_authoring::placements::{LocalLightAnchor, LocalLightPackageReference};

pub(super) const ORIGINAL: &[u8] =
    b"#!/usr/bin/env node\nthrow new Error('fixture source must remain inert');\n";
pub(super) struct Lights {
    pub manifest: Vec<u8>,
    pub package: SourceLocalLightsPackage,
}
fn reference(name: &str, bytes: &[u8]) -> serde_json::Value {
    json!({"file":name,"bytes":bytes.len(),"sha256":bytes_sha256(bytes)})
}
pub(super) fn lights() -> Lights {
    let commit = "0000000000000000000000000000000000000000";
    let license = b"Original unit-fixture licence.\n";
    let descriptor = json!({"schema":"forge.source-local-lights","version":1,"source_commit":commit,
        "capture_mode":"module-unit-fixture","gpu_initialized":false,"browser_required":false,
        "registration_scope":"fixture","full_application_inventory":false,"missing_owners":["outside-fixture"],
        "phase_reacquisition_reproducible":false,"source_defaults":{"enabled":true,"scale":1.0,"flicker":0.0},
        "coordinate_system":{"units":"metres","up":"+Y","handedness":"right"},
        "frames":[{"id":"lamp","source_object":"fixture-root","parent":null}],
        "sets":{"one":{"start_order":0,"count":1}},
        "source_inputs":[{"path":"src/light.js","bytes":ORIGINAL.len(),"sha256":bytes_sha256(ORIGINAL)},
            {"path":"LICENSE","bytes":license.len(),"sha256":bytes_sha256(license)}],
        "lights":[{"id":bytes_sha256(b"fixture-lamp"),"registration_order":0,"owner_frame":"lamp",
            "source_kind":"fixture","source_function":"fixture","position_local_m":[0.0,2.0,0.0],
            "direction_local":null,"acquired_position_world_m":[0.0,2.0,0.0],"acquired_direction_world":null,
            "color_linear_rgb":[1.0,0.7,0.4],"intensity_at_1m":3.2,"range_m":8.0,
            "cos_inner":null,"cos_outer":null,"enabled":null,"scale":null,"flicker":null,
            "phase":0.915_999_999_999_999_9_f64,"phase_source":"fixture","source_has_pose_update":false}],
        "runtime_selection_executed":false,"renderer_parity":false,"studio_qualified":false});
    let descriptor_bytes = serde_json::to_vec(&descriptor).unwrap();
    let descriptor_pin = reference("lights.json", &descriptor_bytes);
    let receipt=serde_json::to_vec(&json!({"schema":"forge.source-local-lights.factory-acquisition","version":1,
        "source_commit":commit,"descriptor":descriptor_pin,"capture_mode":descriptor["capture_mode"],
        "gpu_initialized":false,"browser_required":false,"emitters":1,"registration_sets":descriptor["sets"],
        "full_emitter_inventory":false,"phase_reacquisition_reproducible":false,
        "full_application_executed":false,"renderer_parity":false,"studio_qualified":false})).unwrap();
    let members = BTreeMap::from([
        ("lights.json".to_owned(), descriptor_bytes),
        ("receipt.json".into(), receipt),
        ("source/LICENSE".into(), license.to_vec()),
        ("source/light.js".into(), ORIGINAL.to_vec()),
    ]);
    let files: BTreeMap<_, _> = members
        .iter()
        .map(|(p, b)| (p, json!({"bytes":b.len(),"sha256":bytes_sha256(b)})))
        .collect();
    let manifest=serde_json::to_vec(&json!({"schema":"forge.source-local-lights.package","version":1,
        "source_commit":commit,"descriptor":descriptor_pin,"acquisition":reference("receipt.json",&members["receipt.json"]),
        "license":reference("source/LICENSE",license),"sources":{"src/light.js":reference("source/light.js",ORIGINAL),"LICENSE":reference("source/LICENSE",license)},
        "files":files,"status":"source-local-light-acquisition-not-runtime-import","renderer_parity":false,"studio_qualified":false})).unwrap();
    let package = SourceLocalLightsPackage::load(&manifest, bytes_sha256(&manifest), |name, _| {
        Ok(members[name].clone())
    })
    .unwrap();
    Lights { manifest, package }
}

pub(super) fn pin(
    lights: &Lights,
    carrier: forge_core::ContentHash,
) -> Box<LocalLightPackageReference> {
    Box::new(LocalLightPackageReference {
        schema_version: 1,
        carrier_sha256: carrier,
        manifest_sha256: lights.package.receipt_hash(),
        descriptor_sha256: lights.package.descriptor().content_hash(),
        light_ids: vec![bytes_sha256(b"fixture-lamp")],
        anchors: vec![LocalLightAnchor {
            frame_id: "lamp".into(),
            transform: Transform::IDENTITY,
        }],
    })
}
