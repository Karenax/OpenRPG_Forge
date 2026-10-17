use forge_assets::governed_import::{
    AssetProvenance, AssetRights, AssetTransformation, DurableAssetSource, GOVERNED_IMPORT_SCHEMA,
    GOVERNED_IMPORT_VERSION, GovernedAssetFile, GovernedAssetKind, GovernedImportAsset,
    GovernedImportCatalog, GovernedImportError, MAX_IMPORT_ASSETS, MAX_IMPORT_FILE_BYTES,
    MAX_IMPORT_THUMBNAIL_BYTES, MAX_IMPORT_TOTAL_BYTES, RedistributionRights,
};
use forge_core::{AssetId, ContentHash, bytes_sha256};
use std::collections::BTreeMap;

fn file(path: &str, bytes: &[u8]) -> GovernedAssetFile {
    GovernedAssetFile {
        path: path.into(),
        sha256: bytes_sha256(bytes),
        bytes: u64::try_from(bytes.len()).unwrap(),
    }
}

fn asset(name: &str, kind: GovernedAssetKind) -> GovernedImportAsset {
    GovernedImportAsset {
        id: AssetId::deterministic(format!("governed-import-test/{name}")),
        name: name.into(),
        kind,
        payload: file(&format!("assets/{name}.bin"), name.as_bytes()),
        provenance: AssetProvenance {
            source: "https://example.invalid/original".into(),
            author: "Fixture author".into(),
            license: "CC0-1.0".into(),
            evidence: "licenses/fixture-cc0.txt".into(),
            acquired_on: "2026-10-15".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Allowed,
                redistribution_evidence: Some("licenses/fixture-cc0.txt".into()),
            },
        },
        derived_from: None,
        transformations: Vec::new(),
        authored_changes: false,
        durable_source: None,
        thumbnail: None,
    }
}

fn catalog() -> GovernedImportCatalog {
    GovernedImportCatalog {
        schema: GOVERNED_IMPORT_SCHEMA.into(),
        schema_version: GOVERNED_IMPORT_VERSION,
        assets: vec![
            asset("mesh", GovernedAssetKind::Mesh),
            asset("texture", GovernedAssetKind::Texture),
            asset("audio", GovernedAssetKind::Audio),
        ],
    }
}

fn asset_error(result: Result<(), GovernedImportError>, expected: &'static str) {
    let error = result.expect_err("expected a contextual asset error");
    assert!(
        matches!(error, GovernedImportError::Asset { field, .. } if field == expected),
        "expected a contextual asset error for {expected}, got {error}"
    );
}

#[test]
fn three_media_kinds_round_trip_without_becoming_render_or_import_qualification() {
    let original = catalog();
    original.validate_for_local_use().unwrap();
    original.validate_for_publication().unwrap();
    let wire = serde_json::to_vec(&original).unwrap();
    let reopened: GovernedImportCatalog = serde_json::from_slice(&wire).unwrap();
    assert_eq!(reopened, original);
    assert!(
        !String::from_utf8(wire)
            .unwrap()
            .contains("runtime_qualified")
    );
}

#[test]
fn declared_license_never_grants_redistribution_or_local_use() {
    for rights in [
        RedistributionRights::Unknown,
        RedistributionRights::Forbidden,
    ] {
        let mut value = catalog();
        value.assets[0].provenance.rights.redistribution = rights;
        value.assets[0].provenance.rights.redistribution_evidence = None;
        value.validate_for_local_use().unwrap();
        asset_error(value.validate_for_publication(), "redistribution");
    }
    let mut value = catalog();
    value.assets[0].provenance.rights.local_use = false;
    asset_error(value.validate_for_local_use(), "local_use");
    asset_error(value.validate_for_publication(), "local_use");
}

#[test]
fn publication_requires_evidence_and_checks_every_ancestor() {
    let mut value = catalog();
    value.assets[0].provenance.rights.redistribution_evidence = None;
    asset_error(value.validate_for_publication(), "redistribution_evidence");
    value.assets[0].provenance.rights.redistribution_evidence = Some("  ".into());
    asset_error(value.validate_for_publication(), "redistribution_evidence");

    let mut value = catalog();
    value.assets[1].derived_from = Some(value.assets[0].id);
    value.assets[1].transformations.push(AssetTransformation {
        tool: "Fixture converter".into(),
        version: "1".into(),
        parameters_sha256: bytes_sha256(b"conversion recipe"),
    });
    value.assets[0].provenance.rights.redistribution = RedistributionRights::Forbidden;
    asset_error(value.validate_for_publication(), "redistribution");
}

#[test]
fn provenance_fields_and_real_acquisition_dates_are_required() {
    for field in ["source", "author", "license", "evidence", "acquired_on"] {
        let mut value = catalog();
        let provenance = &mut value.assets[0].provenance;
        match field {
            "source" => provenance.source.clear(),
            "author" => provenance.author.clear(),
            "license" => provenance.license.clear(),
            "evidence" => provenance.evidence.clear(),
            "acquired_on" => provenance.acquired_on.clear(),
            _ => unreachable!(),
        }
        asset_error(value.validate_for_local_use(), field);
    }
    for date in [
        "2026-02-29",
        "2026-13-01",
        "2026-04-31",
        "2026-1-15",
        "0000-01-01",
    ] {
        let mut value = catalog();
        value.assets[0].provenance.acquired_on = date.into();
        asset_error(value.validate_for_local_use(), "acquired_on");
    }
    let mut value = catalog();
    value.assets[0].provenance.acquired_on = "2024-02-29".into();
    value.validate_for_local_use().unwrap();
}

#[test]
fn payload_paths_are_portable_relative_paths_before_any_callback() {
    for path in [
        "../secret",
        "/absolute",
        "C:/absolute",
        "a\\b.glb",
        "a//b",
        "a/./b",
        "a/../b",
        "a/NUL.glb",
        "a/COM1.txt",
        "a/file.",
        "a/file ",
        "a/name:stream",
        "a/b\nc",
        "assets/Élan/mesh-v2.glb",
        "",
        "a/",
    ] {
        let mut value = catalog();
        value.assets[0].payload.path = path.into();
        let mut calls = 0;
        asset_error(
            value.verify_payloads(|_| {
                calls += 1;
                Ok(Vec::new())
            }),
            "payload.path",
        );
        assert_eq!(calls, 0, "unsafe path {path:?} must fail before reading");
    }
    let mut value = catalog();
    value.assets[0].payload.path = "assets/elan/mesh-v2.glb".into();
    value.assets[0].name = "Élan au bord de l’eau".into();
    value.assets[0].provenance.author = "Élodie".into();
    value.validate_for_local_use().unwrap();
}

#[test]
fn file_pins_and_allocation_budgets_are_checked_before_reading() {
    let mut zero_hash = catalog();
    zero_hash.assets[0].payload.sha256 = ContentHash::from_bytes([0; 32]);
    asset_error(zero_hash.validate_for_local_use(), "payload.sha256");
    for bytes in [0, MAX_IMPORT_FILE_BYTES + 1, u64::MAX] {
        let mut value = catalog();
        value.assets[0].payload.bytes = bytes;
        asset_error(value.validate_for_local_use(), "payload.bytes");
    }
    let mut value = catalog();
    value.assets[0].thumbnail = Some(GovernedAssetFile {
        bytes: MAX_IMPORT_THUMBNAIL_BYTES + 1,
        ..file("thumbs/mesh.png", b"preview")
    });
    asset_error(value.validate_for_local_use(), "thumbnail.bytes");
}

#[test]
fn payload_verification_checks_exact_size_hash_and_reader_errors() {
    for bytes in [b"mes".as_slice(), b"fake".as_slice(), b"meshes".as_slice()] {
        let value = catalog();
        let result = value.verify_payloads(|declaration| {
            Ok(if declaration.path == "assets/mesh.bin" {
                bytes.to_vec()
            } else {
                declaration
                    .path
                    .trim_start_matches("assets/")
                    .trim_end_matches(".bin")
                    .as_bytes()
                    .to_vec()
            })
        });
        assert!(
            matches!(result, Err(GovernedImportError::Payload { asset_id, path, .. })
            if asset_id == value.assets[0].id && path == "assets/mesh.bin")
        );
    }
    let value = catalog();
    let error = value
        .verify_payloads(|_| Err("missing in confined store".into()))
        .unwrap_err();
    assert!(error.to_string().contains("missing in confined store"));
    assert!(error.to_string().contains(&value.assets[0].id.to_string()));
    assert!(error.to_string().contains("assets/mesh.bin"));
}

#[test]
fn authored_retouch_requires_durable_source_or_recipe_and_verified_bytes() {
    let mut value = catalog();
    value.assets[0].authored_changes = true;
    asset_error(value.validate_for_local_use(), "durable_source");
    for source in [
        DurableAssetSource::AuthoredFile {
            file: file("sources/mesh.blend", b"authored"),
        },
        DurableAssetSource::Recipe {
            file: file("sources/mesh.recipe.json", b"authored"),
        },
    ] {
        value.assets[0].durable_source = Some(source);
        value.validate_for_local_use().unwrap();
        let mut reads = Vec::new();
        value
            .verify_payloads(|declaration| {
                reads.push(declaration.path.clone());
                Ok(match declaration.path.as_str() {
                    "assets/mesh.bin" => b"mesh".to_vec(),
                    "assets/texture.bin" => b"texture".to_vec(),
                    "assets/audio.bin" => b"audio".to_vec(),
                    _ => b"authored".to_vec(),
                })
            })
            .unwrap();
        assert!(reads.iter().any(|path| path.starts_with("sources/")));
    }
}

#[test]
fn lineage_refuses_missing_parents_self_links_and_cycles() {
    let mut value = catalog();
    value.assets[0].derived_from = Some(AssetId::deterministic("missing"));
    asset_error(value.validate_for_local_use(), "derived_from");
    value.assets[0].derived_from = Some(value.assets[0].id);
    asset_error(value.validate_for_local_use(), "derived_from");
    value.assets[0].derived_from = Some(value.assets[1].id);
    value.assets[1].derived_from = Some(value.assets[2].id);
    value.assets[2].derived_from = Some(value.assets[0].id);
    asset_error(value.validate_for_local_use(), "derived_from");
}

#[test]
fn derivative_requires_a_recorded_transformation_with_valid_pins() {
    let mut value = catalog();
    value.assets[1].derived_from = Some(value.assets[0].id);
    asset_error(value.validate_for_local_use(), "transformations");
    value.assets[1].transformations.push(AssetTransformation {
        tool: "Fixture converter".into(),
        version: "1".into(),
        parameters_sha256: bytes_sha256(b"recipe"),
    });
    value.validate_for_local_use().unwrap();
    value.assets[1].transformations[0].parameters_sha256 = ContentHash::from_bytes([0; 32]);
    asset_error(value.validate_for_local_use(), "transformations");
}

#[test]
fn same_identity_never_implies_adopting_changed_content() {
    let first = catalog();
    let mut changed = first.clone();
    changed.assets[0].payload = file("assets/mesh-v2.bin", b"new mesh");
    first.validate_for_local_use().unwrap();
    changed.validate_for_local_use().unwrap();
    assert_eq!(first.assets[0].id, changed.assets[0].id);
    let saved_pin = (first.assets[0].id, first.assets[0].payload.sha256);
    let available_pin = (changed.assets[0].id, changed.assets[0].payload.sha256);
    assert_ne!(saved_pin, available_pin);
    assert_eq!(first.assets[0].payload.path, "assets/mesh.bin");
    let new_bytes = |declaration: &GovernedAssetFile| {
        Ok(match declaration.path.as_str() {
            "assets/mesh.bin" | "assets/mesh-v2.bin" => b"new mesh".to_vec(),
            "assets/texture.bin" => b"texture".to_vec(),
            "assets/audio.bin" => b"audio".to_vec(),
            _ => panic!("unexpected content pin"),
        })
    };
    assert!(matches!(
        first.verify_payloads(new_bytes),
        Err(GovernedImportError::Payload { .. })
    ));
    changed.verify_payloads(new_bytes).unwrap();
}

#[test]
fn duplicate_identity_and_conflicting_file_aliases_are_rejected() {
    let mut duplicate = catalog();
    duplicate.assets.push(duplicate.assets[0].clone());
    asset_error(duplicate.validate_for_local_use(), "id");
    let mut conflict = catalog();
    conflict.assets[1].payload.path = conflict.assets[0].payload.path.clone();
    asset_error(conflict.validate_for_local_use(), "payload.path");
    let mut case_alias = catalog();
    case_alias.assets[1].payload.path = "ASSETS/MESH.BIN".into();
    asset_error(case_alias.validate_for_local_use(), "payload.path");
    for reverse in [false, true] {
        let mut prefix = catalog();
        prefix.assets[0].payload.path = "assets/root".into();
        prefix.assets[1].payload.path = "assets/root/nested.bin".into();
        if reverse {
            prefix.assets.reverse();
        }
        asset_error(prefix.validate_for_local_use(), "payload.path");
    }
}

#[test]
fn windows_non_ascii_case_aliases_are_rejected_before_payload_reads() {
    let mut value = catalog();
    value.assets[0].payload.path = "assets/σ.bin".into();
    value.assets[1].payload.path = "assets/ς.bin".into();
    let mut calls = 0;
    asset_error(
        value.verify_payloads(|_| {
            calls += 1;
            Ok(Vec::new())
        }),
        "payload.path",
    );
    assert_eq!(calls, 0);
}

#[test]
fn windows_non_ascii_parent_aliases_are_rejected_in_both_orders() {
    for reverse in [false, true] {
        let mut value = catalog();
        value.assets[0].payload.path = "assets/σ".into();
        value.assets[1].payload.path = "assets/ς/nested.bin".into();
        if reverse {
            value.assets.reverse();
        }
        asset_error(value.validate_for_local_use(), "payload.path");
    }
}

#[test]
fn shared_exact_content_is_read_once_and_thumbnails_are_pinned_too() {
    let mut value = catalog();
    value.assets[1].payload = value.assets[0].payload.clone();
    value.assets[0].thumbnail = Some(file("thumbnails/mesh.png", b"thumbnail"));
    let mut calls = BTreeMap::<String, usize>::new();
    value
        .verify_payloads(|declaration| {
            *calls.entry(declaration.path.clone()).or_default() += 1;
            Ok(match declaration.path.as_str() {
                "assets/mesh.bin" => b"mesh".to_vec(),
                "assets/audio.bin" => b"audio".to_vec(),
                "thumbnails/mesh.png" => b"thumbnail".to_vec(),
                _ => panic!("unexpected payload"),
            })
        })
        .unwrap();
    assert_eq!(calls.len(), 3);
    assert!(calls.values().all(|count| *count == 1));
    assert!(matches!(value.verify_payloads(|declaration| {
        Ok(if declaration.path == "thumbnails/mesh.png" { b"bad-thumb".to_vec() }
        else if declaration.path == "assets/audio.bin" { b"audio".to_vec() }
        else { b"mesh".to_vec() })
    }), Err(GovernedImportError::Payload { path, .. }) if path == "thumbnails/mesh.png"));
}

#[test]
fn schema_identity_and_catalogue_bounds_are_enforced() {
    for (schema, version) in [
        ("other", 1),
        (GOVERNED_IMPORT_SCHEMA, 0),
        (GOVERNED_IMPORT_SCHEMA, 2),
    ] {
        let mut value = catalog();
        value.schema = schema.into();
        value.schema_version = version;
        assert!(matches!(
            value.validate_for_local_use(),
            Err(GovernedImportError::Catalog {
                field: "schema",
                ..
            })
        ));
    }
    let mut value = catalog();
    value.assets.clear();
    assert!(matches!(
        value.validate_for_local_use(),
        Err(GovernedImportError::Catalog {
            field: "assets",
            ..
        })
    ));
    value.assets = vec![asset("too-many", GovernedAssetKind::Mesh); MAX_IMPORT_ASSETS + 1];
    assert!(matches!(
        value.validate_for_local_use(),
        Err(GovernedImportError::Catalog {
            field: "assets",
            ..
        })
    ));
}

#[test]
fn aggregate_payload_budget_counts_unique_files_without_allocating_them() {
    let count = usize::try_from(MAX_IMPORT_TOTAL_BYTES / MAX_IMPORT_FILE_BYTES).unwrap();
    let mut value = catalog();
    value.assets = (0..=count)
        .map(|index| {
            let mut entry = asset(&format!("budget-{index}"), GovernedAssetKind::Mesh);
            entry.payload.bytes = MAX_IMPORT_FILE_BYTES;
            entry
        })
        .collect();
    let mut calls = 0;
    assert!(matches!(
        value.verify_payloads(|_| {
            calls += 1;
            Ok(Vec::new())
        }),
        Err(GovernedImportError::Catalog { field: "bytes", .. })
    ));
    assert_eq!(calls, 0);
    value.assets.pop();
    value.validate_for_local_use().unwrap();
}

#[test]
fn invalid_text_and_durable_file_pins_never_reach_payload_readers() {
    for name in ["bad\nname", "bad\u{fffd}name"] {
        let mut value = catalog();
        value.assets[0].name = name.into();
        asset_error(value.validate_for_local_use(), "name");
    }
    let mut value = catalog();
    value.assets[0].authored_changes = true;
    value.assets[0].durable_source = Some(DurableAssetSource::Recipe {
        file: file("../cache/retouched.json", b"source"),
    });
    asset_error(value.validate_for_local_use(), "durable_source.path");
}

#[test]
fn unknown_fields_and_missing_rights_are_rejected_in_serialized_documents() {
    let original = serde_json::to_value(catalog()).unwrap();
    for pointer in [
        "",
        "/assets/0",
        "/assets/0/payload",
        "/assets/0/provenance",
        "/assets/0/provenance/rights",
    ] {
        let mut wire = original.clone();
        wire.pointer_mut(pointer)
            .unwrap()
            .as_object_mut()
            .unwrap()
            .insert("surprise".into(), true.into());
        assert!(serde_json::from_value::<GovernedImportCatalog>(wire).is_err());
    }
    let mut wire = original;
    wire.pointer_mut("/assets/0/provenance")
        .unwrap()
        .as_object_mut()
        .unwrap()
        .remove("rights");
    assert!(serde_json::from_value::<GovernedImportCatalog>(wire).is_err());
}
