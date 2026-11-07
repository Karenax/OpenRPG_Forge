use super::*;

fn fixture() -> (BTreeMap<String, Vec<u8>>, AssetId, ContentHash) {
    // An aligned minimal GLB exercises admission, not geometry or rendering.
    let mut json = br#"{"asset":{"version":"2.0"}}"#.to_vec();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut model = b"glTF".to_vec();
    model.extend_from_slice(&2_u32.to_le_bytes());
    model.extend_from_slice(&u32::try_from(20 + json.len()).unwrap().to_le_bytes());
    model.extend_from_slice(&u32::try_from(json.len()).unwrap().to_le_bytes());
    model.extend_from_slice(b"JSON");
    model.extend_from_slice(&json);
    let pin = bytes_sha256(&model);
    let report = serde_json::json!({
        "schema":"forge.blender-asset-kit-build",
        "schema_version":1, "asset_kit_id":"test-kit", "asset_kit_revision":1,
        "generator":{"input_manifest_sha256":bytes_sha256(b"inputs")},
        "provenance":{"author":"Declared kit author", "source_kind":"procedural_original", "license":"LicenseRef-OpenRPG-Forge-Project-Generated",
            "cadle_content_used":false,"commercial_game_content_used":false},
        "qualification":{"technical_export_validated":true},
        "assets":[{"id":"tree", "kind":"tree", "glb":{"path":"assets/tree.glb","sha256":pin}}]
    });
    let raw = serde_json::to_vec(&report).unwrap();
    let browser = AssetBrowser::new(serde_json::from_slice(&raw).unwrap()).unwrap();
    let id = browser.reference("tree").unwrap().0;
    (
        BTreeMap::from([
            ("forge-kit/build.json".into(), raw),
            ("forge-kit/assets/tree.glb".into(), model),
        ]),
        id,
        pin,
    )
}

fn capture(
    files: &BTreeMap<String, Vec<u8>>,
    pins: &[(AssetId, ContentHash)],
    maximum: u64,
) -> Result<AvailableExportSnapshot, String> {
    export_available_models(pins, "2026-10-23", maximum, |path, limit| {
        assert!(path.starts_with("forge-kit/"));
        let value = files.get(path).cloned();
        assert!(value.as_ref().is_none_or(|bytes| bytes.len() <= limit));
        Ok(value)
    })
}

#[test]
fn exact_pin_retains_payload_proof_private_rights_and_only_unknown_ids_fall_through() {
    let (files, id, pin) = fixture();
    let unchanged = files.clone();
    let unknown = (
        AssetId::deterministic("not-in-kit"),
        bytes_sha256(b"external"),
    );
    let captured = capture(&files, &[(id, pin), unknown], MAX_EXPORT_BYTES).unwrap();
    assert_eq!(files, unchanged);
    assert_eq!(captured.unresolved, vec![unknown]);
    let asset = &captured.snapshot.catalog.assets[0];
    assert_eq!((asset.id, asset.payload.sha256), (id, pin));
    assert_eq!(
        captured.snapshot.files[&asset.payload.path],
        files["forge-kit/assets/tree.glb"]
    );
    assert!(
        matches!(&asset.durable_source,Some(DurableAssetSource::AuthoredFile{file}) if file==&asset.payload)
    );
    assert_eq!(captured.snapshot.files.len(), 2);
    assert_eq!(asset.provenance.author, "Declared kit author");
    assert_eq!(
        asset.provenance.rights.redistribution,
        RedistributionRights::Forbidden
    );
    assert!(
        captured
            .snapshot
            .catalog
            .validate_for_publication()
            .is_err()
    );
    let proof: serde_json::Value =
        serde_json::from_slice(&captured.snapshot.files[&asset.provenance.evidence]).unwrap();
    assert_eq!(
        proof["manifest_sha256"],
        bytes_sha256(&files["forge-kit/build.json"]).to_string()
    );
    let raw_manifest = proof["manifest_utf8"].as_str().unwrap();
    assert_eq!(raw_manifest.as_bytes(), files["forge-kit/build.json"]);
    let manifest: serde_json::Value = serde_json::from_str(raw_manifest).unwrap();
    assert_eq!(manifest["provenance"]["license"], asset.provenance.license);
    assert_eq!(proof["package_copy_acquired_on"], "2026-10-23");
}

#[test]
fn changed_pin_corruption_missing_payload_and_invalid_media_never_fall_through() {
    let (mut files, id, pin) = fixture();
    let changed = bytes_sha256(b"different-version");
    assert!(
        capture(&files, &[(id, changed)], MAX_EXPORT_BYTES)
            .err()
            .unwrap()
            .contains("differs from its pinned")
    );
    let model = files.remove("forge-kit/assets/tree.glb").unwrap();
    assert!(
        capture(&files, &[(id, pin)], MAX_EXPORT_BYTES)
            .err()
            .unwrap()
            .contains("absent")
    );
    files.insert("forge-kit/assets/tree.glb".into(), b"corrupt".to_vec());
    assert!(
        capture(&files, &[(id, pin)], MAX_EXPORT_BYTES)
            .err()
            .unwrap()
            .contains("empreinte")
    );
    let invalid = b"not a GLB".to_vec();
    let invalid_pin = bytes_sha256(&invalid);
    let mut report: serde_json::Value =
        serde_json::from_slice(&files["forge-kit/build.json"]).unwrap();
    report["assets"][0]["glb"]["sha256"] = invalid_pin.to_string().into();
    files.insert(
        "forge-kit/build.json".into(),
        serde_json::to_vec(&report).unwrap(),
    );
    files.insert("forge-kit/assets/tree.glb".into(), invalid);
    assert!(capture(&files, &[(id, invalid_pin)], MAX_EXPORT_BYTES).is_err());
    assert_ne!(model, b"corrupt");
}

#[test]
fn missing_kit_is_optional_but_unsafe_manifest_and_unqualified_provenance_are_errors() {
    let (files, id, pin) = fixture();
    let absent = capture(&BTreeMap::new(), &[(id, pin)], MAX_EXPORT_BYTES).unwrap();
    assert_eq!(absent.unresolved, vec![(id, pin)]);
    assert!(absent.snapshot.catalog.assets.is_empty());
    for pointer in [
        "/assets/0/glb/path",
        "/provenance/commercial_game_content_used",
        "/qualification/technical_export_validated",
    ] {
        let mut altered = files.clone();
        let mut report: serde_json::Value =
            serde_json::from_slice(&files["forge-kit/build.json"]).unwrap();
        *report.pointer_mut(pointer).unwrap() = match pointer {
            "/assets/0/glb/path" => "../outside.glb".into(),
            "/provenance/commercial_game_content_used" => true.into(),
            _ => false.into(),
        };
        altered.insert(
            "forge-kit/build.json".into(),
            serde_json::to_vec(&report).unwrap(),
        );
        assert!(capture(&altered, &[(id, pin)], MAX_EXPORT_BYTES).is_err());
    }
}

#[test]
fn complete_snapshot_budget_includes_proof_and_duplicate_id_is_rejected_before_reads() {
    let (files, id, pin) = fixture();
    let captured = capture(&files, &[(id, pin)], MAX_EXPORT_BYTES).unwrap();
    let total = captured
        .snapshot
        .files
        .values()
        .map(|bytes| bytes.len() as u64)
        .sum();
    assert!(capture(&files, &[(id, pin)], total).is_ok());
    assert!(
        capture(&files, &[(id, pin)], total - 1)
            .err()
            .unwrap()
            .contains("Budget")
    );
    assert!(
        export_available_models(
            &[(id, pin), (id, pin)],
            "2026-10-23",
            MAX_EXPORT_BYTES,
            |_, _| panic!("No read before duplicate rejection")
        )
        .is_err()
    );
    assert!(
        export_available_models(
            &[(id, pin)],
            "invalid-date",
            MAX_EXPORT_BYTES,
            |path, _| Ok(files.get(path).cloned())
        )
        .is_err()
    );
}

#[test]
fn source_graph_kit_export_keeps_exact_bytes_and_refuses_tampered_program() {
    let (mut files, id, _) = fixture();
    let mut model = crate::source_graph_test_fixture::fixture().unwrap();
    let mut report: serde_json::Value =
        serde_json::from_slice(&files["forge-kit/build.json"]).unwrap();
    let pin = bytes_sha256(&model);
    report["assets"][0]["glb"]["sha256"] = pin.to_string().into();
    files.insert(
        "forge-kit/build.json".into(),
        serde_json::to_vec(&report).unwrap(),
    );
    files.insert("forge-kit/assets/tree.glb".into(), model.clone());
    let original = files.clone();
    let captured = capture(&files, &[(id, pin)], MAX_EXPORT_BYTES).unwrap();
    let asset = &captured.snapshot.catalog.assets[0];
    assert_eq!(captured.snapshot.files[&asset.payload.path], model);
    assert_eq!(files, original);

    let offset = model
        .windows(crate::source_graph_test_fixture::FORWARD.len())
        .position(|window| window == crate::source_graph_test_fixture::FORWARD.as_bytes())
        .unwrap();
    model[offset] ^= 1;
    let changed_pin = bytes_sha256(&model);
    report["assets"][0]["glb"]["sha256"] = changed_pin.to_string().into();
    files.insert(
        "forge-kit/build.json".into(),
        serde_json::to_vec(&report).unwrap(),
    );
    files.insert("forge-kit/assets/tree.glb".into(), model);
    assert!(capture(&files, &[(id, changed_pin)], MAX_EXPORT_BYTES).is_err());
}
