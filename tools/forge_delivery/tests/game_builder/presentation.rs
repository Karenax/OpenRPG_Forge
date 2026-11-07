use super::*;
use base64::{Engine, engine::general_purpose::STANDARD};
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, GOVERNED_IMPORT_SCHEMA, GOVERNED_IMPORT_VERSION,
    GovernedAssetFile, GovernedAssetKind, GovernedImportAsset, GovernedImportCatalog,
    RedistributionRights,
};
use forge_core::AssetId;
use forge_delivery::{
    game_builder::build_candidate_with_presentation, game_presentation::PresentationBundle,
};
use forge_project::workspace::launcher::PresentationAsset;
use std::collections::BTreeMap;

fn image() -> (PresentationAsset, PresentationBundle) {
    // A generated, original 1x1 RGBA PNG; no external art or licence dependency.
    let png = STANDARD.decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGOQi1rwHwADygIY2ofr+QAAAABJRU5ErkJggg==").unwrap();
    let proof = b"Private fixture image. Redistribution forbidden for admission regression.";
    let evidence = format!("licenses/{}.txt", bytes_sha256(proof));
    let payload = format!("payloads/{}.png", bytes_sha256(&png));
    let id = AssetId::deterministic("game-presentation-test-image");
    let asset = GovernedImportAsset {
        id,
        name: "Image de qualification".into(),
        kind: GovernedAssetKind::Texture,
        payload: GovernedAssetFile {
            path: payload.clone(),
            sha256: bytes_sha256(&png),
            bytes: png.len() as u64,
        },
        provenance: AssetProvenance {
            source: "Generated test fixture".into(),
            author: "OpenRPG Forge".into(),
            license: "LicenseRef-Qualification".into(),
            evidence: evidence.clone(),
            acquired_on: "2026-11-07".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Forbidden,
                redistribution_evidence: None,
            },
        },
        derived_from: None,
        transformations: vec![],
        authored_changes: false,
        durable_source: None,
        thumbnail: None,
    };
    (
        PresentationAsset {
            asset_id: id,
            content_hash: asset.payload.sha256,
        },
        PresentationBundle {
            catalog: GovernedImportCatalog {
                schema: GOVERNED_IMPORT_SCHEMA.into(),
                schema_version: GOVERNED_IMPORT_VERSION,
                assets: vec![asset],
            },
            files: BTreeMap::from([(payload, png), (evidence, proof.to_vec())]),
        },
    )
}

#[test]
fn presentation_is_pinned_licensed_installed_and_projected_without_source_paths() {
    let fixture = fixture::Fixture::new();
    let kit_root = fixture.root().join("presentation-kit");
    let (_, bytes) = kit(&kit_root);
    let kit = VerifiedGameKit::read(&kit_root, &bytes).unwrap();
    let (pin, mut bundle) = image();
    let (game, workspace, module) = inputs_with_image(&fixture.first, Some(pin));
    let output = fixture.root().join("presentation-candidates");
    assert!(
        build_candidate(
            &output,
            &kit,
            &game,
            workspace,
            std::slice::from_ref(&module)
        )
        .is_err()
    );
    let proof = bundle.catalog.assets[0].provenance.evidence.clone();
    let proof_bytes = bundle.files.remove(&proof).unwrap();
    assert!(bundle.validate(game.configuration()).is_err());
    bundle.files.insert(proof, proof_bytes);
    let payload = bundle.catalog.assets[0].payload.path.clone();
    let image_bytes = bundle.files[&payload].clone();
    bundle.files.get_mut(&payload).unwrap()[0] ^= 1;
    assert!(bundle.validate(game.configuration()).is_err());
    bundle.files.insert(payload, image_bytes.clone());
    let candidate = build_candidate_with_presentation(
        &output,
        &kit,
        &game,
        workspace,
        &[module],
        Some(&bundle),
    )
    .unwrap();
    assert_eq!(candidate.rights, Rights::PrivateLocalOnly);
    let installed = fixture.installation("presentation");
    let prepared = prepare_candidate_install(
        &candidate.directory,
        &candidate.directory.join(candidate.installer_file),
        &installed,
    )
    .unwrap();
    apply_candidate_install(&prepared).unwrap();
    let view = standalone::game_view(&installed).unwrap();
    let expected = format!("data:image/png;base64,{}", STANDARD.encode(image_bytes));
    assert_eq!(view.presentation.icon.as_deref(), Some(expected.as_str()));
    assert_eq!(view.presentation.logo, view.presentation.icon);
    assert_eq!(view.presentation.background, view.presentation.icon);
    let state = install::state(&installed).unwrap();
    let application = install::package_root(&installed, &state.active.core).unwrap();
    let image_path = application
        .join("presentation")
        .join(&bundle.catalog.assets[0].payload.path);
    fs::write(image_path, b"changed after installation").unwrap();
    assert!(standalone::game_view(&installed).is_err());
}
