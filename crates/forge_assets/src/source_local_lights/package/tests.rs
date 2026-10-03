use super::*;
use serde_json::json;
use std::{
    future::Future,
    task::{Context, Poll, Waker},
};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const ORIGINAL: &[u8] = b"source";
const LICENSE: &[u8] = b"Original fixture license\n";

struct Fixture {
    manifest: Value,
    members: BTreeMap<String, Vec<u8>>,
}

fn reference(file: &str, bytes: &[u8]) -> Value {
    json!({"file":file,"bytes":bytes.len(),"sha256":bytes_sha256(bytes)})
}

fn fixture(
    change_document: impl FnOnce(&mut Value),
    change_receipt: impl FnOnce(&mut Value),
) -> Fixture {
    let mut document = super::super::tests::fixture();
    document["source_inputs"]
        .as_array_mut()
        .unwrap()
        .push(json!({
            "path":"LICENSE","bytes":LICENSE.len(),"sha256":bytes_sha256(LICENSE)
        }));
    document["lights"][0]["phase"] = json!(0.915_999_999_999_999_9_f64);
    change_document(&mut document);
    let descriptor = serde_json::to_vec(&document).unwrap();
    let descriptor_ref = reference("lights.json", &descriptor);
    let mut acquisition = json!({
        "schema":"forge.source-local-lights.factory-acquisition","version":1,
        "source_commit":document["source_commit"],"descriptor":descriptor_ref,
        "capture_mode":document["capture_mode"],"gpu_initialized":document["gpu_initialized"],
        "browser_required":document["browser_required"],
        "emitters":document["lights"].as_array().unwrap().len(),
        "registration_sets":document["sets"],
        "full_emitter_inventory":document["full_application_inventory"],
        "phase_reacquisition_reproducible":document["phase_reacquisition_reproducible"],
        "full_application_executed":false,"renderer_parity":false,"studio_qualified":false,
        "producer_details":{"uninterpreted_provenance":"retained"}
    });
    change_receipt(&mut acquisition);
    let acquisition = serde_json::to_vec(&acquisition).unwrap();
    let acquisition_ref = reference("acquisition.json", &acquisition);
    let license_ref = reference("source/LICENSE", LICENSE);
    let source_ref = reference("source/src/lighting.js", ORIGINAL);
    let members: BTreeMap<String, Vec<u8>> = BTreeMap::from([
        ("lights.json".into(), descriptor),
        ("acquisition.json".into(), acquisition),
        ("source/LICENSE".into(), LICENSE.to_vec()),
        ("source/src/lighting.js".into(), ORIGINAL.to_vec()),
    ]);
    let entries: BTreeMap<_, _> = members
        .iter()
        .map(|(name, bytes)| {
            (
                name.clone(),
                json!({"bytes":bytes.len(),"sha256":bytes_sha256(bytes)}),
            )
        })
        .collect();
    Fixture {
        manifest: json!({
            "schema":"forge.source-local-lights.package","version":1,"source_commit":COMMIT,
            "descriptor":descriptor_ref,"acquisition":acquisition_ref,"license":license_ref,
            "sources":{"src/lighting.js":source_ref,"LICENSE":license_ref},"files":entries,
            "status":"source-local-light-acquisition-not-runtime-import",
            "renderer_parity":false,"studio_qualified":false
        }),
        members,
    }
}

fn load(fixture: &Fixture) -> Result<SourceLocalLightsPackage, SourceLocalLightsError> {
    let manifest = serde_json::to_vec(&fixture.manifest).unwrap();
    SourceLocalLightsPackage::load(&manifest, bytes_sha256(&manifest), |name, _| {
        fixture
            .members
            .get(name)
            .cloned()
            .ok_or_else(|| format!("absent: {name}"))
    })
}

fn ready<T>(future: impl Future<Output = T>) -> T {
    let mut future = std::pin::pin!(future);
    let mut context = Context::from_waker(Waker::noop());
    match future.as_mut().poll(&mut context) {
        Poll::Ready(result) => result,
        Poll::Pending => panic!("the supplied ready transport unexpectedly suspended"),
    }
}

#[test]
fn source_local_lights_package_closes_originals_license_and_receipt_without_reseeding() {
    let input = fixture(|_| {}, |_| {});
    let loaded = load(&input).unwrap();
    let manifest = serde_json::to_vec(&input.manifest).unwrap();
    assert_eq!(loaded.receipt_hash(), bytes_sha256(&manifest));
    assert_eq!(loaded.file_count(), 4);
    assert_eq!(loaded.source_file("src/lighting.js"), Some(ORIGINAL));
    assert_eq!(loaded.license(), LICENSE);
    assert_eq!(loaded.acquisition()["full_application_executed"], false);
    assert_eq!(
        loaded.acquisition()["producer_details"]["uninterpreted_provenance"],
        "retained"
    );
    assert_eq!(
        loaded.descriptor().document().lights[0].phase.to_bits(),
        0.915_999_999_999_999_9_f64.to_bits()
    );
    assert!(!loaded.descriptor().document().runtime_selection_executed);

    let future =
        SourceLocalLightsPackage::load_async(&manifest, bytes_sha256(&manifest), |name, _| {
            std::future::ready(input.members.get(&name).cloned().ok_or(name))
        });
    let mut future = std::pin::pin!(future);
    let mut context = Context::from_waker(Waker::noop());
    let Poll::Ready(result) = future.as_mut().poll(&mut context) else {
        panic!("the supplied ready transport unexpectedly suspended");
    };
    let asynchronous = result.unwrap();
    assert_eq!(asynchronous.receipt_hash(), loaded.receipt_hash());
    assert_eq!(
        asynchronous.descriptor().content_hash(),
        loaded.descriptor().content_hash()
    );
}

#[test]
fn source_local_lights_package_refuses_wrong_pin_before_transport() {
    let mut called = false;
    let result = SourceLocalLightsPackage::load(b"invalid", bytes_sha256(b"different"), |_, _| {
        called = true;
        Ok(Vec::new())
    });
    assert!(matches!(
        result,
        Err(SourceLocalLightsError::PackagePinMismatch)
    ));
    assert!(!called);
}

#[test]
fn source_local_lights_package_refuses_paths_bounds_aliases_and_unclaimed_files_before_reads() {
    for change in 0..10 {
        let mut input = fixture(|_| {}, |_| {});
        match change {
            0 => {
                input.manifest["files"]["../escape"] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x")})
            }
            1 => {
                input.manifest["files"]["source/CON.txt"] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x")})
            }
            2 => {
                input.manifest["files"]["source/trailing. "] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x")})
            }
            3 => {
                input.manifest["files"]["LIGHTS.JSON"] =
                    input.manifest["files"]["lights.json"].clone()
            }
            4 => {
                input.manifest["files"]["source/src/lighting.js"]["bytes"] =
                    json!(MAX_MEMBER_BYTES + 1)
            }
            5 => {
                input.manifest["files"]["unclaimed"] =
                    json!({"bytes":1,"sha256":bytes_sha256(b"x")})
            }
            6 => input.manifest["descriptor"]["sha256"] = json!(bytes_sha256(b"other")),
            7 => input.manifest["license"] = input.manifest["descriptor"].clone(),
            8 => {
                input.manifest["sources"]
                    .as_object_mut()
                    .unwrap()
                    .remove("LICENSE");
            }
            _ => input.manifest["unknown"] = json!(true),
        }
        let manifest = serde_json::to_vec(&input.manifest).unwrap();
        let mut called = false;
        let result = SourceLocalLightsPackage::load(&manifest, bytes_sha256(&manifest), |_, _| {
            called = true;
            Ok(Vec::new())
        });
        assert!(result.is_err(), "case {change}");
        assert!(!called, "case {change} read before admission");
    }
}

#[test]
fn source_local_lights_package_refuses_changed_or_missing_members() {
    for name in [
        "lights.json",
        "acquisition.json",
        "source/LICENSE",
        "source/src/lighting.js",
    ] {
        let mut input = fixture(|_| {}, |_| {});
        input.members.get_mut(name).unwrap()[0] ^= 1;
        assert!(load(&input).is_err(), "changed {name}");
        input.members.remove(name);
        assert!(load(&input).is_err(), "missing {name}");
    }
}

#[test]
fn source_local_lights_package_refuses_descriptor_source_and_receipt_contradictions() {
    for change in 0..8 {
        let input = fixture(
            |document| match change {
                0 => document["source_inputs"][0]["path"] = json!("src/other.js"),
                1 => document["source_inputs"][0]["sha256"] = json!(bytes_sha256(b"other")),
                2 => document["source_commit"] = json!("0000000000000000000000000000000000000000"),
                _ => {}
            },
            |receipt| match change {
                3 => receipt["descriptor"]["sha256"] = json!(bytes_sha256(b"other")),
                4 => receipt["emitters"] = json!(2),
                5 => receipt["registration_sets"]["emitters"]["count"] = json!(2),
                6 => receipt["full_emitter_inventory"] = json!(true),
                7 => receipt["gpu_initialized"] = json!(false),
                _ => {}
            },
        );
        assert!(load(&input).is_err(), "contradiction {change}");
    }
}

#[test]
fn source_local_lights_package_refuses_duplicate_json_map_keys() {
    let input = fixture(|_| {}, |_| {});
    let manifest = serde_json::to_string(&input.manifest).unwrap();
    for (key, entry) in [
        ("lights.json", &input.manifest["files"]["lights.json"]),
        (
            "src/lighting.js",
            &input.manifest["sources"]["src/lighting.js"],
        ),
    ] {
        let original = format!("\"{key}\":{}", serde_json::to_string(entry).unwrap());
        let duplicate = manifest.replacen(&original, &format!("{original},{original}"), 1);
        assert_ne!(duplicate, manifest);
        let result = SourceLocalLightsPackage::load(
            duplicate.as_bytes(),
            bytes_sha256(duplicate.as_bytes()),
            |_, _| panic!("duplicate JSON keys must be refused before transport"),
        );
        assert!(matches!(result, Err(SourceLocalLightsError::Decode(_))));
    }
}

#[test]
fn source_local_lights_package_async_refuses_before_io_and_stops_on_transport_failure() {
    let input = fixture(|_| {}, |_| {});
    let manifest = serde_json::to_vec(&input.manifest).unwrap();
    let mut reads = Vec::new();
    let wrong = ready(SourceLocalLightsPackage::load_async(
        &manifest,
        bytes_sha256(b"different"),
        |name, _| {
            reads.push(name);
            std::future::ready(Err("transport must not run".into()))
        },
    ));
    assert!(matches!(
        wrong,
        Err(SourceLocalLightsError::PackagePinMismatch)
    ));
    assert!(reads.is_empty());
    let failed = ready(SourceLocalLightsPackage::load_async(
        &manifest,
        bytes_sha256(&manifest),
        |name, _| {
            reads.push(name);
            std::future::ready(Err("first member unavailable".into()))
        },
    ));
    assert!(failed.is_err());
    assert_eq!(reads, vec!["acquisition.json"]);
}

#[test]
fn source_local_lights_package_refuses_unicode_aliases_dos_and_aggregate_limits() {
    for change in 0..3 {
        let mut input = fixture(|_| {}, |_| {});
        let entry = json!({"bytes":1,"sha256":bytes_sha256(b"x")});
        match change {
            0 => {
                input.manifest["files"]["source/é.js"] = entry.clone();
                input.manifest["files"]["source/É.js"] = entry;
            }
            1 => input.manifest["files"]["source/COM¹.js"] = entry,
            _ => {
                for index in 0..9 {
                    input.manifest["files"][format!("payload-{index}")] =
                        json!({"bytes":MAX_MEMBER_BYTES,"sha256":bytes_sha256(b"x")});
                }
            }
        }
        let manifest = serde_json::to_vec(&input.manifest).unwrap();
        let result = SourceLocalLightsPackage::load(&manifest, bytes_sha256(&manifest), |_, _| {
            panic!("unsafe paths and aggregate bounds must be refused before reads")
        });
        let Err(SourceLocalLightsError::Package(message)) = result else {
            panic!("expected a package admission failure");
        };
        assert!(message.contains(if change == 2 { "aggregate" } else { "path" }));
    }
}

#[test]
fn source_local_lights_package_exposes_source_execution_only_as_a_receipt_claim() {
    let input = fixture(
        |_| {},
        |receipt| receipt["full_application_executed"] = json!(true),
    );
    let package = load(&input).unwrap();
    assert!(package.source_application_executed_claim());
    assert!(!package.descriptor().document().runtime_selection_executed);
    assert!(!package.descriptor().document().renderer_parity);
    assert!(!package.descriptor().document().studio_qualified);
}

#[test]
fn source_local_lights_package_refuses_oversized_transport_and_duplicate_receipt_sets() {
    let input = fixture(|_| {}, |_| {});
    let manifest = serde_json::to_vec(&input.manifest).unwrap();
    let result = SourceLocalLightsPackage::load(&manifest, bytes_sha256(&manifest), |name, _| {
        let mut member = input.members[name].clone();
        member.push(0);
        Ok(member)
    });
    assert!(result.is_err());

    let mut input = fixture(|_| {}, |_| {});
    let original = format!(
        "\"emitters\":{}",
        serde_json::to_string(&json!({"start_order":0,"count":1})).unwrap()
    );
    let receipt = String::from_utf8(input.members["acquisition.json"].clone()).unwrap();
    let duplicate = receipt
        .replacen(&original, &format!("{original},{original}"), 1)
        .into_bytes();
    assert_ne!(duplicate, input.members["acquisition.json"]);
    let pin = reference("acquisition.json", &duplicate);
    input.manifest["acquisition"] = pin.clone();
    input.manifest["files"]["acquisition.json"] =
        json!({"bytes":duplicate.len(),"sha256":pin["sha256"]});
    input.members.insert("acquisition.json".into(), duplicate);
    assert!(matches!(
        load(&input),
        Err(SourceLocalLightsError::Decode(_))
    ));
}
