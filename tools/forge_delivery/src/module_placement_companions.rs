//! Verify transported placement packages against the exact logical carrier bytes.
use crate::Result;
use forge_assets::{
    governed_import::{GovernedAssetKind, GovernedImportCatalog},
    media_validation::MediaLimits,
    source_graph_glb::SourceGraphGlbReadBudget,
    source_particle_package::SourceParticlePackage,
    source_physics_package::SourcePhysicsPackage,
};
use forge_core::{AssetId, ContentHash, bytes_sha256, canonical_json_sha256};
use forge_project::module_package::{
    ModulePackageManifest, ModulePlacementCompanion, ModulePlacementCompanionKind,
};
use serde::Deserialize;
use std::collections::BTreeMap;

pub const MAX_COMPANION_PROOF_BYTES: usize = 128 * 1024;

/// Deterministic internal names; no caller-supplied filesystem path is accepted.
#[must_use]
pub fn export_paths(
    kind: ModulePlacementCompanionKind,
    pin: forge_project::zone_authoring::session::SourceCompanionPin,
) -> (String, String) {
    let (folder, extension) = match kind {
        ModulePlacementCompanionKind::Physics => ("physics", "orpp"),
        ModulePlacementCompanionKind::Particles => ("particles", "orpe"),
    };
    let root = format!(
        "_forge-export/placement-companions/{folder}/{}/{}",
        pin.asset_id, pin.package_sha256
    );
    (
        format!("{root}/package.{extension}"),
        format!("{root}/proof.json"),
    )
}

#[derive(Deserialize)]
struct Proof {
    schema: String,
    kind: ModulePlacementCompanionKind,
    asset_id: AssetId,
    carrier_sha256: ContentHash,
    manifest_sha256: ContentHash,
    package_sha256: ContentHash,
    registry_snapshot_revision: ContentHash,
    selected_details_sha256: ContentHash,
    selected_profile_sha256: ContentHash,
    selected_catalog_entry_sha256: ContentHash,
    metadata: serde_json::Value,
}

fn proof_hash(value: &serde_json::Value, field: &str) -> Result<ContentHash> {
    serde_json::from_value(value[field].clone())
        .map_err(|_| format!("Companion proof {field} is absent or invalid"))
}

fn validate_proof(companion: &ModulePlacementCompanion, bytes: &[u8]) -> Result<()> {
    if bytes.len() > MAX_COMPANION_PROOF_BYTES || bytes_sha256(bytes) != companion.proof.sha256 {
        return Err("Companion proof byte budget or hash differs".into());
    }
    let proof: Proof = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    if proof.schema != "forge.private-vault-companion-proof.v1"
        || proof.kind != companion.kind
        || proof.asset_id != companion.asset_id
        || proof.carrier_sha256 != companion.carrier_sha256
        || proof.manifest_sha256 != companion.manifest_sha256
        || proof.package_sha256 != companion.package.sha256
    {
        return Err("Companion proof subject differs from the package".into());
    }
    let metadata = &proof.metadata;
    if metadata["schema"] != "forge.private-vault-package-proof.v1"
        || proof_hash(metadata, "model_sha256")? != companion.carrier_sha256
        || proof_hash(metadata, "library_revision")? != proof.registry_snapshot_revision
        || canonical_json_sha256(&metadata["metadata"]).map_err(|error| error.to_string())?
            != proof.selected_details_sha256
        || canonical_json_sha256(&metadata["functional_profile"])
            .map_err(|error| error.to_string())?
            != proof.selected_profile_sha256
        || canonical_json_sha256(&metadata["source_catalog_entry"])
            .map_err(|error| error.to_string())?
            != proof.selected_catalog_entry_sha256
    {
        return Err("Companion proof selected metadata differs".into());
    }
    let library_id = metadata["library_id"]
        .as_str()
        .filter(|value| !value.is_empty())
        .ok_or("Companion proof library identity is absent")?;
    if forge_assets::governed_import::vault_asset_id(library_id) != companion.asset_id {
        return Err("Companion proof Vault identity differs from the asset".into());
    }
    let details = &metadata["metadata"];
    let profile = &metadata["functional_profile"];
    let catalog = &metadata["source_catalog_entry"];
    if details["id"].as_str() != Some(library_id)
        || profile["external_id"].as_str() != Some(library_id)
        || catalog["id"].as_str() != Some(library_id)
        || proof_hash(details, "model_sha256")? != companion.carrier_sha256
        || proof_hash(profile, "model_content_sha256")? != companion.carrier_sha256
        || proof_hash(catalog, "sha256")? != companion.carrier_sha256
    {
        return Err("Companion proof asset metadata identity differs".into());
    }
    let field = match companion.kind {
        ModulePlacementCompanionKind::Physics => "source_physics_package",
        ModulePlacementCompanionKind::Particles => "source_particle_package",
    };
    let declaration = &metadata["functional_profile"][field];
    if proof_hash(declaration, "carrier_sha256")? != companion.carrier_sha256
        || proof_hash(declaration, "manifest_sha256")? != companion.manifest_sha256
        || proof_hash(declaration, "sha256")? != companion.package.sha256
    {
        return Err("Companion proof profile pins differ".into());
    }
    for source in [details, catalog] {
        if [
            "/local_use",
            "/rights/local_use",
            "/provenance/rights/local_use",
        ]
        .iter()
        .any(|path| source.pointer(path).and_then(serde_json::Value::as_bool) == Some(false))
        {
            return Err("Companion proof explicitly refuses local use".into());
        }
    }
    Ok(())
}

/// Decode every declared package against its pinned governed mesh, without the Vault.
/// This checks transport and semantic closure, not execution or redistribution rights.
pub(crate) fn validate(
    module: &ModulePackageManifest,
    catalog: &GovernedImportCatalog,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<()> {
    let mut by_asset: BTreeMap<_, Vec<&ModulePlacementCompanion>> = BTreeMap::new();
    for companion in &module.placement_companions {
        let evidence = read(&companion.proof.path, MAX_COMPANION_PROOF_BYTES)?;
        validate_proof(companion, &evidence)?;
        by_asset
            .entry(companion.asset_id)
            .or_default()
            .push(companion);
    }
    let carrier_limit = usize::try_from(MediaLimits::default().max_input_bytes)
        .map_err(|_| "Carrier budget is unavailable on this platform")?;
    let catalog_assets: BTreeMap<_, _> = catalog
        .assets
        .iter()
        .map(|asset| (asset.id, asset))
        .collect();
    let declared_assets: BTreeMap<_, _> = module
        .assets
        .iter()
        .map(|asset| (asset.asset_id, asset))
        .collect();
    for (id, companions) in by_asset {
        let asset = catalog_assets
            .get(&id)
            .filter(|asset| asset.kind == GovernedAssetKind::Mesh)
            .ok_or("Companion carrier is not a governed mesh")?;
        let declared = declared_assets
            .get(&id)
            .ok_or("Companion carrier is absent from the module")?;
        if declared.file_path != asset.payload.path || declared.sha256 != asset.payload.sha256 {
            return Err("Companion carrier and catalog pins disagree".into());
        }
        let carrier = read(&asset.payload.path, carrier_limit)?;
        if carrier.len() as u64 != asset.payload.bytes
            || forge_core::bytes_sha256(&carrier) != asset.payload.sha256
        {
            return Err("Companion carrier bytes disagree with the catalog".into());
        }
        for companion in companions {
            if companion.carrier_sha256 != asset.payload.sha256 {
                return Err("Companion carrier revision differs".into());
            }
            match companion.kind {
                ModulePlacementCompanionKind::Physics => {
                    let wire = read(
                        &companion.package.path,
                        forge_assets::source_physics_package::MAX_PACKAGE_BYTES,
                    )?;
                    let package = SourcePhysicsPackage::decode_pinned(
                        &wire,
                        companion.carrier_sha256,
                        companion.manifest_sha256,
                        companion.package.sha256,
                    )?;
                    package.resolve_for_carrier(&carrier, SourceGraphGlbReadBudget::Standard)?;
                }
                ModulePlacementCompanionKind::Particles => {
                    let wire = read(
                        &companion.package.path,
                        forge_assets::source_particle_package::MAX_PACKAGE_BYTES,
                    )?;
                    let package = SourceParticlePackage::decode_pinned(
                        &wire,
                        companion.carrier_sha256,
                        companion.manifest_sha256,
                        companion.package.sha256,
                    )?;
                    package.resolve_for_carrier(&carrier)?;
                }
            }
        }
    }
    Ok(())
}
