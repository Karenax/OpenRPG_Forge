//! Explicit construction identities, including observed zero-emitter calls.
use forge_assets::source_local_lights::{SourceLightInput, SourceLocalLightsPackage};
use forge_core::ContentHash;
use serde::Deserialize;
use std::collections::BTreeMap;

#[derive(Deserialize)]
struct Receipt {
    building_owner_observation: Option<Observation>,
}
#[derive(Deserialize)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent receipt claims are checked separately."
)]
struct Observation {
    schema: String,
    version: u32,
    source_commit: String,
    producer_path: String,
    source_sha256: ContentHash,
    generator_bodies_changed: bool,
    canonical_checkout_modified: bool,
    unchanged_geometry_terrain_lights_colliders: bool,
    second_construction_refused_before_generator: bool,
    building_invocations: Vec<Invocation>,
}

#[derive(Deserialize)]
pub(super) struct Invocation {
    ordinal: usize,
    pub producer: String,
    source_object_id: Option<String>,
    anchor: Option<String>,
    pub world_from_local: [f64; 16],
    root_scopes: usize,
    pub emitted_lights: usize,
}

pub(super) fn read(
    package: &SourceLocalLightsPackage,
) -> Result<Option<BTreeMap<String, Invocation>>, String> {
    let document = package.descriptor().document();
    decode(
        package.acquisition_bytes(),
        &document.source_commit,
        &document.source_inputs,
        document.lights.len(),
    )
}

fn decode(
    bytes: &[u8],
    source_commit: &str,
    source_inputs: &[SourceLightInput],
    light_count: usize,
) -> Result<Option<BTreeMap<String, Invocation>>, String> {
    let receipt: Receipt = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    let Some(observation) = receipt.building_owner_observation else {
        return Ok(None);
    };
    // Older closed acquisitions did not carry identities for empty calls. They
    // remain readable, but cannot provide a zero-emitter attestation.
    if observation
        .building_invocations
        .iter()
        .all(|row| row.source_object_id.is_none() && row.anchor.is_none())
    {
        return Ok(None);
    }
    if observation.schema != "forge.source-building-light-owner-observation"
        || observation.version != 1
        || observation.source_commit != source_commit
        || observation.generator_bodies_changed
        || observation.canonical_checkout_modified
        || !observation.unchanged_geometry_terrain_lights_colliders
        || !observation.second_construction_refused_before_generator
        || observation.building_invocations.is_empty()
        || observation.building_invocations.len() > 4096
        || !source_inputs.iter().any(|source| {
            source.path == observation.producer_path && source.sha256 == observation.source_sha256
        })
    {
        return Err(
            "Building invocation attestation differs from verified source provenance".into(),
        );
    }
    let mut result = BTreeMap::new();
    for (ordinal, mut invocation) in observation.building_invocations.into_iter().enumerate() {
        let id = invocation
            .source_object_id
            .take()
            .ok_or("Building invocation source identity absent")?;
        if invocation.ordinal != ordinal
            || id.is_empty()
            || id.contains('\0')
            || invocation.anchor.as_deref() != Some("construction-root")
            || invocation.producer.is_empty()
            || invocation.producer.contains('\0')
            || invocation.root_scopes == 0
            || invocation.root_scopes > 4096
            || (invocation.emitted_lights == 0 && invocation.root_scopes != 1)
            || invocation.emitted_lights > light_count
            || !invocation.world_from_local.iter().all(|v| v.is_finite())
        {
            return Err("Building invocation attestation is incomplete or ambiguous".into());
        }
        if result.insert(id, invocation).is_some() {
            return Err("Building invocation source identity is duplicated".into());
        }
    }
    Ok(Some(result))
}

#[cfg(test)]
mod tests {
    use super::*;
    fn fixture() -> (serde_json::Value, Vec<SourceLightInput>) {
        let hash = forge_core::bytes_sha256(b"source");
        let inputs = vec![SourceLightInput {
            path: "source.js".into(),
            sha256: hash,
            bytes: 6,
        }];
        let invocation = |ordinal, id, lights| {
            serde_json::json!({"ordinal":ordinal,"producer":"generate",
            "source_object_id":id,"anchor":"construction-root",
            "world_from_local":bevy::math::DMat4::IDENTITY.to_cols_array(),"root_scopes":1,"emitted_lights":lights})
        };
        (
            serde_json::json!({"building_owner_observation":{
            "schema":"forge.source-building-light-owner-observation","version":1,"source_commit":"revision",
            "producer_path":"source.js","source_sha256":hash,"generator_bodies_changed":false,
            "canonical_checkout_modified":false,"unchanged_geometry_terrain_lights_colliders":true,
            "second_construction_refused_before_generator":true,
            "building_invocations":[invocation(0,"empty",0),invocation(1,"lit",1)]}}),
            inputs,
        )
    }

    #[test]
    fn local_lights_empty_attestation_requires_provenance_and_complete_identity() {
        let (value, inputs) = fixture();
        let read = |value: &serde_json::Value| {
            decode(&serde_json::to_vec(value).unwrap(), "revision", &inputs, 1)
        };
        let valid = read(&value).unwrap().unwrap();
        assert_eq!(valid["empty"].emitted_lights, 0);
        assert_eq!(valid["lit"].emitted_lights, 1);
        let mut multiple_scopes = value.clone();
        multiple_scopes["building_owner_observation"]["building_invocations"][1]["root_scopes"] =
            2.into();
        assert!(
            read(&multiple_scopes).is_ok(),
            "A luminous construction can have additional non-emitting scopes"
        );
        for case in 0..11 {
            let mut value = value.clone();
            let observation = &mut value["building_owner_observation"];
            match case {
                0 => observation["source_commit"] = "other".into(),
                1 => {
                    observation["source_sha256"] =
                        forge_core::bytes_sha256(b"other").to_string().into()
                }
                2 => observation["unchanged_geometry_terrain_lights_colliders"] = false.into(),
                3 => observation["building_invocations"][1]["source_object_id"] = "empty".into(),
                4 => {
                    observation["building_invocations"][1]
                        .as_object_mut()
                        .unwrap()
                        .remove("source_object_id");
                }
                5 => observation["building_invocations"][0]["root_scopes"] = 2.into(),
                6 => observation["building_invocations"][1]["ordinal"] = 0.into(),
                7 => observation["building_invocations"][0]["anchor"] = "other".into(),
                8 => observation["building_invocations"][0]["producer"] = "".into(),
                9 => observation["building_invocations"][0]["emitted_lights"] = 2.into(),
                _ => observation["producer_path"] = "other.js".into(),
            }
            assert!(read(&value).is_err(), "Invalid attestation case {case}");
        }
        let duplicate = serde_json::to_string(&value).unwrap().replace(
            "\"source_object_id\":\"empty\"",
            "\"source_object_id\":\"empty\",\"source_object_id\":\"empty\"",
        );
        assert!(decode(duplicate.as_bytes(), "revision", &inputs, 1).is_err());
        let mut legacy = value;
        for row in legacy["building_owner_observation"]["building_invocations"]
            .as_array_mut()
            .unwrap()
        {
            row.as_object_mut().unwrap().remove("source_object_id");
            row.as_object_mut().unwrap().remove("anchor");
        }
        assert!(
            read(&legacy).unwrap().is_none(),
            "Legacy absence became an attestation"
        );
    }
}
