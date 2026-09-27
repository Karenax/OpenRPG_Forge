//! Rebuild GPU inputs from canonical Forge records, without acquired instance buffers.
use forge_project::VegetationDocument;
use forge_world::{
    imported_vegetation::ImportedPopulation,
    tidewater_vegetation::{PackedPlant, pack},
};
use std::{collections::BTreeMap, error::Error};
pub type Streams = BTreeMap<String, BTreeMap<String, Vec<u8>>>;

pub struct PreparedPopulation {
    pub streams: Streams,
    pub identities: BTreeMap<String, Vec<forge_core::EntityId>>,
    pub head_hash: Option<forge_core::ContentHash>,
}

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
    let head_hash = authored.map(|(head, _)| head.content_hash()).transpose()?;
    let visible = if let Some((head, heights)) = authored {
        let view = population.terrain_view(head, heights)?;
        let ids = view
            .layers
            .into_iter()
            .flat_map(|l| l.instances.into_iter().map(|e| (e.id, e)))
            .collect::<BTreeMap<_, _>>();
        eprintln!(
            "canonical authored vegetation: visible={}, excluded={}, density_removed={}, head={}",
            ids.len(),
            view.excluded_count,
            view.density_removed_count,
            view.head_hash
        );
        eprintln!("canonical ground projection: moved={}", view.moved_count);
        Some(ids)
    } else {
        None
    };
    let mut groups: BTreeMap<&str, BTreeMap<u32, (forge_core::EntityId, PackedPlant)>> =
        BTreeMap::new();
    for layer in population.layers() {
        for entity in layer.instances() {
            let rendered = visible
                .as_ref()
                .and_then(|v| v.get(&entity.id))
                .unwrap_or(entity);
            let plant = pack(&layer.prototype_key, &layer.parameter_names, rendered)?;
            let order = plant
                .source_order
                .ok_or("canonical render requires source order")?;
            if groups
                .entry(plant.group)
                .or_default()
                .insert(order, (entity.id, plant))
                .is_some()
            {
                return Err("duplicate canonical group order".into());
            }
        }
    }
    if groups.len() != 6 || population.instance_count() != 65_383 {
        return Err("incomplete canonical island vegetation".into());
    }
    let mut result = BTreeMap::new();
    let mut identities = BTreeMap::new();
    for (group, records) in groups {
        if records.keys().copied().ne(0..u32::try_from(records.len())?) {
            return Err("canonical group order gap".into());
        }
        let mut ids = Vec::new();
        let mut matrix = Vec::new();
        let mut position = Vec::new();
        let mut data = Vec::new();
        let mut radius = Vec::new();
        for (id, record) in records.into_values() {
            if visible.as_ref().is_some_and(|ids| !ids.contains_key(&id)) {
                continue;
            }
            ids.push(id);
            for v in record.matrix {
                matrix.extend(v.to_le_bytes());
            }
            for v in record.position {
                position.extend(v.to_le_bytes());
            }
            for v in record.data {
                data.extend(v.to_le_bytes());
            }
            radius.extend(record.query_radius_squared.to_le_bytes());
        }
        identities.insert(group.into(), ids);
        result.insert(
            group.into(),
            BTreeMap::from([
                ("matrices".into(), matrix),
                ("iPos".into(), position),
                ("iDat".into(), data),
                ("qr2".into(), radius),
            ]),
        );
    }
    Ok(PreparedPopulation {
        streams: result,
        identities,
        head_hash,
    })
}
