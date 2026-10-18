//! Saved pins authorize exact historical bytes, never the latest package by name.
use forge_core::{AssetId, ContentHash};
use forge_project::zone_authoring::placements::ParticlePackageReference;

pub(in crate::authoring_assets) async fn load_for_model(
    asset_id: AssetId,
    model: ContentHash,
    pin: &ParticlePackageReference,
) -> Result<super::super::source_graph_particles::SourceParticlePayload, String> {
    if pin.schema_version != 1 || pin.carrier_sha256 != model {
        return Err("Référence d’effet incompatible avec le modèle sauvegardé".into());
    }
    let request = crate::asset_library_protocol::LibrarySavedParticleRequest {
        schema_version: 1,
        asset_id,
        carrier_sha256: model,
        manifest_sha256: pin.manifest_sha256,
        package_sha256: pin.package_sha256,
    };
    let _permit = super::MODEL_READ_GATE
        .with(super::super::model_read_gate::ModelReadGate::acquire)?
        .await;
    let wire = super::read_wire_from(
        "read_asset_particles",
        &request,
        forge_assets::source_particle_package::MAX_PACKAGE_BYTES,
    )
    .await?;
    forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
        &wire,
        model,
        pin.manifest_sha256,
        pin.package_sha256,
    )?;
    Ok(
        super::super::source_graph_particles::SourceParticlePayload {
            wire: wire.into(),
            carrier_sha256: model,
            manifest_sha256: pin.manifest_sha256,
            package_sha256: pin.package_sha256,
        },
    )
}
