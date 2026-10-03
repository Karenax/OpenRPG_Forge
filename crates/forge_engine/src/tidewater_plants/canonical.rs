//! Rebuild GPU inputs from canonical Forge records, without acquired instance buffers.
use forge_project::VegetationDocument;
use forge_world::{
    imported_vegetation::ImportedPopulation,
    tidewater_vegetation::{PackedPlant, pack},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
};
pub type Streams = BTreeMap<String, BTreeMap<String, Vec<u8>>>;
type GroupRecords = BTreeMap<&'static str, BTreeMap<u32, (forge_core::EntityId, PackedPlant)>>;
type ProjectedEntities<'a> =
    BTreeMap<forge_core::EntityId, (&'a str, forge_world::imported_vegetation::ImportedEntity)>;

#[cfg(test)]
mod tests;

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
            .flat_map(|l| {
                l.instances
                    .into_iter()
                    .map(move |e| (e.id, (l.layer_id, e)))
            })
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
    let groups = group_records(population, authored.map(|(head, _)| head), visible.as_ref())?;
    if groups.len() != 6 || population.instance_count() != 65_383 {
        return Err("incomplete canonical island vegetation".into());
    }
    emit_records(groups, visible.as_ref(), head_hash)
}

fn group_records(
    population: &ImportedPopulation,
    head: Option<&forge_project::zone_authoring::ZoneAuthoringHead>,
    visible: Option<&ProjectedEntities<'_>>,
) -> Result<GroupRecords, Box<dyn Error>> {
    let mut groups = GroupRecords::new();
    for layer in population.layers() {
        for entity in layer.instances() {
            let rendered = visible
                .and_then(|v| v.get(&entity.id).map(|(_, entity)| entity))
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
    if let Some(head) = head {
        append_placements(
            &mut groups,
            population,
            head,
            visible.ok_or("missing authored plant view")?,
        )?;
    }
    Ok(groups)
}

fn emit_records(
    groups: GroupRecords,
    visible: Option<&ProjectedEntities<'_>>,
    head_hash: Option<forge_core::ContentHash>,
) -> Result<PreparedPopulation, Box<dyn Error>> {
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
            if visible.is_some_and(|ids| !ids.contains_key(&id)) {
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

fn append_placements(
    groups: &mut GroupRecords,
    population: &ImportedPopulation,
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    visible: &ProjectedEntities<'_>,
) -> Result<(), Box<dyn Error>> {
    let mut appended = BTreeSet::new();
    // Preserve the historical authored-copy order before appending newer source
    // copies. The validated final terrain view decides which copies still exist.
    for placement in head
        .corrections
        .imported_plant_placements
        .as_deref()
        .unwrap_or_default()
    {
        if let Some((layer_id, entity)) = visible.get(&placement.id) {
            append_entity(groups, population, layer_id, entity)?;
            appended.insert(placement.id);
        }
    }
    let source_ids: BTreeSet<_> = population
        .layers()
        .iter()
        .flat_map(|layer| layer.instances().iter().map(|entity| entity.id))
        .collect();
    for (id, (layer_id, entity)) in visible {
        if !source_ids.contains(id) && appended.insert(*id) {
            append_entity(groups, population, layer_id, entity)?;
        }
    }
    Ok(())
}

fn append_entity(
    groups: &mut GroupRecords,
    population: &ImportedPopulation,
    layer_id: &str,
    entity: &forge_world::imported_vegetation::ImportedEntity,
) -> Result<(), Box<dyn Error>> {
    let layer = population
        .layers()
        .iter()
        .find(|layer| layer.layer_id == layer_id)
        .ok_or("Authored plant prototype layer disappeared")?;
    let plant = pack(&layer.prototype_key, &layer.parameter_names, entity)?;
    let records = groups.entry(plant.group).or_default();
    let order = u32::try_from(records.len())?;
    if records.insert(order, (entity.id, plant)).is_some() {
        return Err("duplicate authored plant group order".into());
    }
    Ok(())
}
