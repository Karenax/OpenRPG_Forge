//! Shared preparation for a catalogue click or drop; no direct document writes.
use super::{ManualPlacement, PlacementEdit, Transform, Vec3, ZoneAuthoringHead};
use forge_core::{AssetId, CommandId, ContentHash, EntityId};
use forge_project::authoring::AuthoringRequest;
use forge_world::placement_snap::PlacementSnap;

pub(in crate::authoring_assets) fn prepare(
    head: &ZoneAuthoringHead,
    name: &str,
    reference: (AssetId, ContentHash),
    settings: (Transform, PlacementSnap),
    point: [f32; 2],
) -> Result<AuthoringRequest, String> {
    prepare_with_packages(head, name, reference, settings, point, (None, None))
}

pub(in crate::authoring_assets) fn prepare_with_packages(
    head: &ZoneAuthoringHead,
    name: &str,
    reference: (AssetId, ContentHash),
    settings: (Transform, PlacementSnap),
    point: [f32; 2],
    packages: (
        Option<Box<forge_project::zone_authoring::placements::PhysicsPackageReference>>,
        Option<Box<forge_project::zone_authoring::placements::ParticlePackageReference>>,
    ),
) -> Result<AuthoringRequest, String> {
    let terrain = crate::authoring_scene::terrain(head)?;
    let mut transform = settings.0;
    transform.translation_m = Vec3::new(point[0], 0.0, point[1]);
    let transform = crate::authoring_raster::align(&terrain, settings.1, transform)?;
    let key = serde_json::to_string(&(head.content_hash()?, reference, transform))
        .map_err(|error| error.to_string())?;
    let edit = PlacementEdit::Insert {
        placement: ManualPlacement {
            particles: packages.1,
            physics: packages.0,
            id: EntityId::deterministic(format!("manual-placement:{key}")),
            name: name.to_owned(),
            asset_id: reference.0,
            asset_hash: reference.1,
            transform,
            locked: false,
        },
    };
    let key =
        serde_json::to_string(&(head.content_hash()?, &edit)).map_err(|error| error.to_string())?;
    super::super::objects::edit(head, edit, CommandId::deterministic(key))
        .map(|request| AuthoringRequest::PreviewZone { request })
}
