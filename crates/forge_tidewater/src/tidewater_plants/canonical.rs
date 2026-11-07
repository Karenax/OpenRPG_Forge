//! Reference acquisition loading and inventory qualification; packing/execution is common.
use forge_engine::source_vegetation::canonical::{PreparedPopulation, Streams};
use forge_project::VegetationDocument;
use forge_world::imported_vegetation::ImportedPopulation;
use std::{collections::BTreeMap, error::Error};
pub fn streams(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Streams>, Box<dyn Error>> {
    Ok(from_files(files)?.map(|p| p.streams))
}

pub fn from_files(
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<Option<PreparedPopulation>, Box<dyn Error>> {
    let Some(raw) = files.get("canonical-vegetation.json") else {
        return Ok(None);
    };
    let document = VegetationDocument::from_json(std::str::from_utf8(raw)?)?;
    let population = ImportedPopulation::resolve(&document, |hash, size| {
        let bytes = files
            .get(&format!("canonical-{hash}.bin"))
            .ok_or("missing canonical plant blob")?;
        if bytes.len() != size {
            return Err("canonical plant blob size".into());
        }
        Ok(bytes.clone())
    })?;
    let zone = files
        .get("canonical-zone.json")
        .map(|archive| {
            forge_project::zone_authoring::session::ZoneEditSession::from_archive_bytes(archive)
        })
        .transpose()?;
    let authored = zone
        .as_ref()
        .map(|zone| {
            files
                .get("heights.bin")
                .map(|bytes| (zone.head(), bytes.as_slice()))
                .ok_or("missing terrain for plant projection")
        })
        .transpose()?;
    Ok(Some(prepare(&population, authored)?))
}

pub fn prepare(
    population: &ImportedPopulation,
    authored: Option<(&forge_project::zone_authoring::ZoneAuthoringHead, &[u8])>,
) -> Result<PreparedPopulation, Box<dyn Error>> {
    let result = forge_engine::source_vegetation::canonical::prepare(population, authored)?;
    forge_assets::source_vegetation_package::profile::validate_reference_population(
        result.streams.len(),
        population.instance_count(),
    )?;
    Ok(result)
}
