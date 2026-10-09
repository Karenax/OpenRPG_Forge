use super::*;
use forge_core::bytes_sha256;
use serde_json::{Value, json};

fn fixture() -> (SourceLocalLights, Value) {
    let mut value = super::super::tests::fixture();
    let mut second = value["lights"][0].clone();
    second["id"] = json!(bytes_sha256(b"second"));
    second["registration_order"] = json!(1);
    value["lights"].as_array_mut().unwrap().push(second);
    value["sets"]["emitters"]["count"] = json!(2);
    let bytes = serde_json::to_vec(&value).unwrap();
    let descriptor = SourceLocalLights::load(&bytes, bytes_sha256(&bytes)).unwrap();
    let rows: Vec<_> = descriptor
        .document()
        .lights
        .iter()
        .rev()
        .map(|light| {
            json!({
                "light_id":light.id,"owner_frame":"moving","source_binding":{
                    "producer_path":"src/lighting.js","producer_function":"original-function",
                    "collection":"objects","ordinal":0,"source_object_id":"original-object:0",
                    "anchor":"construction-root","evidence":"explicit-source-identity"
                }
            })
        })
        .collect();
    (
        descriptor,
        json!({"editable_ownership":{
            "schema":"forge.source-local-light-owner-observation","version":1,
            "resolved":rows,"unresolved_light_ids":[],"source_binding_complete":true,
            "catalogue_binding_executed":false,"studio_projection_executed":false
        }}),
    )
}

#[test]
fn source_local_lights_ownership_selects_exact_source_identity_in_source_order() {
    let (descriptor, receipt) = fixture();
    let pin = bytes_sha256(b"package");
    let index = SourceLightOwnership::prepare(&descriptor, pin, &receipt).unwrap();
    assert_eq!(index.package_revision(), pin);
    assert_eq!(index.descriptor_revision(), descriptor.content_hash());
    assert_eq!(
        index
            .selection_for_source_object("original-object:0")
            .unwrap(),
        descriptor
            .document()
            .lights
            .iter()
            .map(|light| light.id)
            .collect::<Vec<_>>()
    );
    assert!(
        index
            .selection_for_source_object("Original-object:0")
            .is_none()
    );
    assert!(
        index
            .selection_for_source_object("descriptive-only")
            .is_none()
    );
    assert_eq!(index.records().len(), 2);
    assert!(index.unresolved_light_ids().is_empty());
}

#[test]
fn source_local_lights_ownership_refuses_invalid_partition_and_conflicting_references() {
    let (descriptor, receipt) = fixture();
    for case in 0..16 {
        let mut invalid = receipt.clone();
        let observation = &mut invalid["editable_ownership"];
        match case {
            0 => observation["schema"] = json!("other"),
            1 => observation["version"] = json!(2),
            2 => observation["catalogue_binding_executed"] = json!(true),
            3 => observation["studio_projection_executed"] = json!(true),
            4 => observation["source_binding_complete"] = json!(false),
            5 => {
                observation["resolved"].as_array_mut().unwrap().pop();
            }
            6 => {
                observation["resolved"][0]["light_id"] =
                    observation["resolved"][1]["light_id"].clone();
            }
            7 => observation["resolved"][0]["light_id"] = json!(bytes_sha256(b"unknown")),
            8 => observation["resolved"][0]["owner_frame"] = json!("world"),
            9 => {
                observation["resolved"][0]["source_binding"]["producer_path"] = json!("missing.js");
            }
            10 => observation["resolved"][0]["source_binding"]["anchor"] = json!(""),
            11 => {
                observation["resolved"][0]["source_binding"]["source_object_id"] =
                    json!("invalid\nkey");
            }
            12 => observation["resolved"][0]["source_binding"]["ordinal"] = Value::Null,
            13 => {
                observation["resolved"][0]["source_binding"]["source_object_id"] =
                    json!("different-object");
            }
            14 => observation["unresolved_light_ids"] = json!([descriptor.document().lights[0].id]),
            _ => observation["unexpected"] = json!(true),
        }
        assert!(
            SourceLightOwnership::prepare(&descriptor, bytes_sha256(b"package"), &invalid).is_err(),
            "case {case}"
        );
    }
}

#[test]
fn source_local_lights_ownership_keeps_missing_catalogue_tokens_unresolved_by_identity() {
    let (descriptor, mut receipt) = fixture();
    for row in receipt["editable_ownership"]["resolved"]
        .as_array_mut()
        .unwrap()
    {
        row["source_binding"]
            .as_object_mut()
            .unwrap()
            .remove("source_object_id");
    }
    let index =
        SourceLightOwnership::prepare(&descriptor, bytes_sha256(b"package"), &receipt).unwrap();
    assert!(
        index
            .selection_for_source_object("original-object:0")
            .is_none()
    );
    assert_eq!(
        index.records().len(),
        2,
        "source ownership is distinct from catalogue resolution"
    );
    let removed = receipt["editable_ownership"]["resolved"]
        .as_array_mut()
        .unwrap()
        .pop()
        .unwrap();
    receipt["editable_ownership"]["unresolved_light_ids"] = json!([removed["light_id"]]);
    receipt["editable_ownership"]["source_binding_complete"] = json!(false);
    let partial =
        SourceLightOwnership::prepare(&descriptor, bytes_sha256(b"package"), &receipt).unwrap();
    assert_eq!(partial.unresolved_light_ids().len(), 1);
    receipt["editable_ownership"]["unresolved_light_ids"] = json!([bytes_sha256(b"unknown")]);
    assert!(
        SourceLightOwnership::prepare(&descriptor, bytes_sha256(b"package"), &receipt).is_err()
    );
}
