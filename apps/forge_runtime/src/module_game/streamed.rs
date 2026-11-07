//! Admission and host readiness for installed terrain. No workspace fallback.
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_project::{TerrainDocument, VegetationDocument};
use forge_world::{cell_streaming::CellStreamingPolicy, sampled_terrain::SampledTerrainPlan};
use std::{ffi::OsStr, sync::Arc};

/// Separate identity for the procedural source; no raster is implied.
#[derive(bevy::prelude::Resource, Clone)]
pub struct ProceduralTerrainIdentity {
    pub document_hash: forge_core::ContentHash,
    pub policy: CellStreamingPolicy,
}

pub fn procedural_streaming(
    sampled: bool,
    value: Option<&OsStr>,
    qualification: bool,
) -> Result<bool, String> {
    match value {
        None => Ok(!sampled),
        Some(_) if !qualification => {
            Err("Terrain streaming overrides require a qualification build".into())
        }
        Some(_) if sampled => {
            Err("Procedural streaming qualification requires a procedural terrain".into())
        }
        Some(value) if value == OsStr::new("1") => Ok(true),
        Some(value) if value == OsStr::new("0") => Ok(false),
        Some(_) => Err("Invalid procedural streaming qualification value".into()),
    }
}

pub struct Prepared {
    pub plan: Arc<SampledTerrainPlan>,
    pub policy: CellStreamingPolicy,
}

pub fn prepare_with_imported(
    loaded: &LoadedModuleZone,
    terrain: &TerrainDocument,
    vegetation: Option<&VegetationDocument>,
    imported: Option<&forge_engine::source_vegetation::scene::streamed::host::PreparedHost>,
) -> Result<Option<Prepared>, String> {
    let Some(source) = &terrain.sampled_source else {
        return Ok(None);
    };
    // Do not silently drop authored vegetation or regenerate a procedural population.
    let admitted = imported.is_some_and(|prepared| {
        terrain
            .content_hash()
            .ok()
            .zip(loaded.zone.head().content_hash().ok())
            .is_some_and(|(terrain, head)| prepared.matches(head, terrain))
    });
    if vegetation.is_some() && !admitted {
        return Err(
            "La végétation de ce terrain importé nécessite encore son adapter runtime.".into(),
        );
    }
    let bytes = loaded
        .source_blobs
        .get(&source.samples_sha256)
        .ok_or("La source du terrain importé manque dans le module admis.")?;
    let plan =
        Arc::new(SampledTerrainPlan::from_document(terrain, bytes).map_err(|e| e.to_string())?);
    let policy = policy(plan.raster().source())?;
    Ok(Some(Prepared { plan, policy }))
}

pub(super) fn policy(
    source: &forge_project::sampled_terrain::SampledTerrainSource,
) -> Result<CellStreamingPolicy, String> {
    forge_engine::cell_streaming::sampled_policy(source)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn procedural_streaming_defaults_to_the_admitted_source_in_production() {
        assert!(procedural_streaming(false, None, false).unwrap());
        assert!(!procedural_streaming(true, None, false).unwrap());
        assert!(procedural_streaming(false, None, true).unwrap());
        for value in ["0", "1", "true"] {
            assert!(procedural_streaming(false, Some(OsStr::new(value)), false).is_err());
            assert!(procedural_streaming(true, Some(OsStr::new(value)), true).is_err());
        }
        assert!(procedural_streaming(false, Some(OsStr::new("1")), true).unwrap());
        assert!(!procedural_streaming(false, Some(OsStr::new("0")), true).unwrap());
        assert!(procedural_streaming(false, Some(OsStr::new("true")), true).is_err());
    }
}
