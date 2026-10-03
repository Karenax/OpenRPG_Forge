//! Pure import binding: library availability alone never creates zone objects.
use forge_core::{AreaId, AssetId, ContentHash, EntityId, Transform, canonical_json_bytes};
use forge_project::zone_authoring::{
    ZoneAuthoringHead,
    placements::{ManualPlacement, PlacementChanges},
};
use forge_project::{HeightSamplingAlgorithm, sampled_terrain::SampledTerrainSource};
use serde::Deserialize;

#[derive(Deserialize)]
struct GrassSource {
    height_sha256: ContentHash,
}

/// The caller supplies the already hash-verified `PlantPackage` descriptor.
/// This pin identifies heights.bin; `source_fixture_sha256` identifies different bytes.
pub(super) fn height_pin(verified_grass: &[u8]) -> Result<ContentHash, String> {
    serde_json::from_slice::<GrassSource>(verified_grass)
        .map(|source| source.height_sha256)
        .map_err(|error| error.to_string())
}

pub(super) fn associated_dataset(
    head: &ZoneAuthoringHead,
    height_pin: Option<ContentHash>,
    verified_geometry: Option<(&SampledTerrainSource, [f32; 2])>,
) -> Result<bool, String> {
    let terrain = &head.base.terrain;
    let (Some(source), Some(pin), Some((expected, extent))) =
        (&terrain.sampled_source, height_pin, verified_geometry)
    else {
        return Ok(false);
    };
    source.validate().map_err(|error| error.to_string())?;
    expected.validate().map_err(|error| error.to_string())?;
    Ok(source.samples_sha256 == pin
        && expected.samples_sha256 == pin
        && source.width == expected.width
        && source.height == expected.height
        && source.encoding == expected.encoding
        && source.origin_xz_m.map(f64::to_bits) == expected.origin_xz_m.map(f64::to_bits)
        && source.sample_spacing_m.to_bits() == expected.sample_spacing_m.to_bits()
        && source.outside_height_m.to_bits() == expected.outside_height_m.to_bits()
        && terrain.extent_m.size_x_m.to_bits() == extent[0].to_bits()
        && terrain.extent_m.size_z_m.to_bits() == extent[1].to_bits()
        && terrain.height_sampling.algorithm == HeightSamplingAlgorithm::SampledRasterV1
        && f64::from(terrain.height_sampling.sample_spacing_m).to_bits()
            == expected.sample_spacing_m.to_bits())
}

pub(super) fn placement(
    area: AreaId,
    key: &str,
    name: String,
    asset_id: AssetId,
    asset_hash: ContentHash,
    transform: Transform,
) -> ManualPlacement {
    ManualPlacement {
        id: EntityId::deterministic(format!("imported-object:{area}:{asset_id}:{key}")),
        name,
        asset_id,
        asset_hash,
        transform,
        locked: false,
    }
}

#[derive(Clone, Copy)]
pub(super) enum SourcePose {
    PackageTransform,
    /// Only the original elevation was derived from the verified source raster.
    TerrainHeight,
}

/// Retained authoring snapshots survive terrain regeneration. Their identity,
/// asset pin and package pose must still attest the same acquired source object.
pub(super) fn select_source(
    head: &ZoneAuthoringHead,
    template: &ManualPlacement,
    inherit: bool,
    pose: SourcePose,
) -> Result<Option<ManualPlacement>, String> {
    let records = head
        .corrections
        .source_object_edits
        .as_deref()
        .unwrap_or_default();
    let Ok(index) = records.binary_search_by_key(&template.id, |record| record.source.id) else {
        return Ok(inherit.then(|| template.clone()));
    };
    let snapshot = &records[index].source;
    PlacementChanges::between(&[], std::slice::from_ref(snapshot))?;
    let mut expected = template.clone();
    if matches!(pose, SourcePose::TerrainHeight) {
        // The persisted value was verified when first acquired. Re-sampling the
        // current terrain would rewrite immutable provenance after regeneration.
        expected.transform.translation_m.y = snapshot.transform.translation_m.y;
    }
    if canonical_json_bytes(&expected).map_err(|error| error.to_string())?
        != canonical_json_bytes(snapshot).map_err(|error| error.to_string())?
    {
        return Err("La provenance de l’objet retouché diffère du package vérifié".into());
    }
    Ok(Some(snapshot.clone()))
}
