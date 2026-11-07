//! Explicit installed resource binding; no reference-map or workspace fallback.
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_terrain::host::PreparedHost;

pub fn prepare(
    loaded: &LoadedModuleZone,
    sampled: Option<&super::streamed::Prepared>,
    environment: bool,
) -> Result<Option<PreparedHost>, String> {
    let Some(resources) = loaded.terrain()? else {
        return Ok(None);
    };
    if !environment {
        return Err("Le terrain source requiert l'environnement admis de cette carte.".into());
    }
    let plan = sampled
        .ok_or("Le terrain source requiert son raster admis")?
        .plan
        .clone();
    let heights = loaded
        .source_blobs
        .get(&plan.raster().source().samples_sha256)
        .ok_or("Source du terrain admis absente")?
        .clone();
    PreparedHost::new(plan, heights, resources.package, resources.package_sha256).map(Some)
}
