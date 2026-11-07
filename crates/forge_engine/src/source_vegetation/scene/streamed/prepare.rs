use super::super::{
    Group, Level, LevelDescription, Record, Vegetation, mesh_from_geometry, read_vectors,
};
use super::{LayerPin, Pin, Prepared, content};
use bevy::prelude::{Mat4, Vec4};
use forge_assets::source_vegetation_package::scene::GroupDescription;
use forge_assets::{
    source_vegetation_geometry::SourceVegetationGeometry,
    source_vegetation_package::{PlantPackage, geometry::PlantGeometry},
};
use forge_core::{ContentHash, EntityId, canonical_json_sha256};
use forge_project::zone_authoring::ZoneAuthoringHead;
use forge_world::imported_vegetation::{ImportedPopulation, ImportedTerrainView};
use std::{collections::BTreeMap, sync::Arc};
type AuthoredPins = BTreeMap<EntityId, (Arc<LayerPin>, ContentHash)>;

pub(super) fn prepare(
    package: &PlantPackage,
    mut geometry: PlantGeometry,
    population: &ImportedPopulation,
    head: &ZoneAuthoringHead,
    heights: &[u8],
    terrain: ContentHash,
) -> Result<Prepared, String> {
    let (document, _) = head.effective()?;
    if document.content_hash().map_err(|e| e.to_string())? != terrain {
        return Err("Streamed plant terrain pin differs from its authored head".into());
    }
    let view = population.terrain_view(head, heights)?;
    let head_hash = view.head_hash;
    let mut inputs = source_pins(population, &view)?;
    let packed = super::super::super::canonical::prepare_streamed_view(population, head, view)
        .map_err(|e| e.to_string())?;
    let descriptor = package.scene_descriptor()?;
    let mut descriptors: BTreeMap<_, _> = descriptor
        .groups
        .into_iter()
        .map(|group| (group.name.clone(), group))
        .collect();
    let mut groups = Vec::new();
    let mut pins = BTreeMap::new();
    let mut levels = Vec::new();
    let mut triangles = Vec::new();
    for (name, ids) in packed.identities {
        let desc = descriptors
            .remove(&name)
            .ok_or("Authored source group absent from the package")?;
        let data = packed
            .streams
            .get(&name)
            .ok_or("Missing packed plant group")?;
        let source_geometry = geometry
            .groups
            .remove(&name)
            .ok_or("Missing decoded source geometry")?;
        let (group, group_triangles) = assemble_group(name, ids, desc, source_geometry, data)?;
        let group_index = groups.len();
        let ids = group
            .identities
            .as_ref()
            .ok_or("Missing packed identities")?;
        levels.push(if ids.is_empty() {
            0
        } else {
            group.levels.len()
        });
        triangles.push(group_triangles);
        for (index, &id) in ids.iter().enumerate() {
            let (layer, content) = inputs
                .remove(&id)
                .ok_or("Packed plant has no authored view identity")?;
            pins.insert(
                id,
                Pin {
                    layer,
                    content,
                    group: group_index,
                    index,
                },
            );
        }
        groups.push(group);
    }
    if !inputs.is_empty() {
        return Err("Packed projection omits authored visible plants".into());
    }
    let identity = canonical_json_sha256(&(
        "forge.streamed-source-plants.v2",
        head_hash,
        terrain,
        package.manifest_sha256(),
    ))
    .map_err(|e| e.to_string())?;
    Ok(Prepared {
        scene: Vegetation {
            head_hash: Some(head_hash),
            groups,
        },
        head: head_hash,
        terrain,
        identity,
        shader: package.file("plant.wgsl").map(forge_core::bytes_sha256),
        population: population
            .document()
            .content_hash()
            .map_err(|error| error.to_string())?,
        pins: Arc::new(pins),
        levels,
        triangles,
    })
}

fn source_pins(
    population: &ImportedPopulation,
    view: &ImportedTerrainView<'_>,
) -> Result<AuthoredPins, String> {
    let mut inputs = BTreeMap::new();
    for layer in &view.layers {
        let source = population
            .layers()
            .iter()
            .find(|source| source.layer_id == layer.layer_id)
            .ok_or("Missing source layer")?;
        let pin = Arc::new(LayerPin {
            layer: layer.layer_id.into(),
            prototype: source.prototype_key.clone(),
            parameters: source.parameter_names.clone(),
        });
        for entity in &layer.instances {
            if inputs
                .insert(entity.id, (Arc::clone(&pin), content(entity)))
                .is_some()
            {
                return Err("Duplicate authored plant identity".into());
            }
        }
    }
    Ok(inputs)
}

fn assemble_group(
    name: String,
    ids: Vec<EntityId>,
    desc: GroupDescription,
    shapes: Vec<SourceVegetationGeometry>,
    data: &BTreeMap<String, Vec<u8>>,
) -> Result<(Group, Vec<usize>), String> {
    let get = |key: &str| data.get(key).ok_or("Missing packed plant stream");
    let positions = read_vectors::<4>(get("iPos")?).map_err(|e| e.to_string())?;
    let values = read_vectors::<4>(get("iDat")?).map_err(|e| e.to_string())?;
    let matrices = read_vectors::<16>(get("matrices")?).map_err(|e| e.to_string())?;
    let radii = read_vectors::<1>(get("qr2")?).map_err(|e| e.to_string())?;
    if [positions.len(), values.len(), matrices.len(), radii.len()]
        .into_iter()
        .any(|count| count != ids.len())
    {
        return Err("Plant identities and packed streams differ".into());
    }
    let models: Vec<_> = matrices.iter().map(Mat4::from_cols_array).collect();
    if models
        .iter()
        .any(|matrix| !matrix.determinant().is_finite() || matrix.determinant().abs() < 1e-12)
    {
        return Err("Singular source plant matrix".into());
    }
    let (levels, triangles) =
        source_levels(shapes, desc.levels.into_iter().map(Into::into).collect())?;
    Ok((
        Group {
            identities: Some(ids),
            name,
            query_radius: desc.query_radius,
            cell_size: desc.cell_size,
            far_exclude_near: desc.far_exclude_near,
            near_range: desc.near_range,
            refresh_distance: desc.refresh_distance,
            sort_far: desc.sort_far,
            records: positions
                .into_iter()
                .zip(values)
                .map(|(position, data)| Record {
                    i_pos: Vec4::from_array(position),
                    i_dat: Vec4::from_array(data),
                })
                .collect(),
            models,
            query_radii: radii.into_iter().map(|radius| radius[0]).collect(),
            levels,
        },
        triangles,
    ))
}

fn source_levels(
    shapes: Vec<SourceVegetationGeometry>,
    descriptions: Vec<LevelDescription>,
) -> Result<(Vec<Level>, Vec<usize>), String> {
    if shapes.len() != descriptions.len() {
        return Err("Source model level count differs".into());
    }
    let mut levels = Vec::new();
    let mut triangles = Vec::new();
    for (shape, description) in shapes.into_iter().zip(descriptions) {
        triangles.push(shape.indices.len() / 3);
        levels.push(Level {
            description,
            mesh: mesh_from_geometry(shape),
        });
    }
    Ok((levels, triangles))
}
