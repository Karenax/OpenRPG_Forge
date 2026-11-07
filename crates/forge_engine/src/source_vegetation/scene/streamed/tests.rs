use material::PlantMaterial;
pub(super) mod material;
// Synthetic CPU selection/staging, not source-shader or GPU qualification.
use super::*;
use bevy::{asset::VisitAssetDependencies, prelude::*, render::storage::ShaderBuffer};
use forge_core::CommandId;
use forge_project::{
    HeightSamplingAlgorithm, VegetationDocument, VegetationKind,
    imported_vegetation::{
        ImportedInstanceEncoding, ImportedVegetationBlock, ImportedVegetationSource,
    },
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::{exclusions::VegetationExclusionEdit, session::ZoneEditSession},
};
use forge_world::{
    cell_streaming::{CellDetail, CellKey, CellStreamingPolicy, LoadedCell},
    imported_vegetation::streaming::{self, AdmittedPlantCells, PrototypeEnvelope},
    terrain_hlod::TerrainHlodLayout,
};
use std::sync::OnceLock;
#[path = "../../../../../../crates/forge_assets/tests/support/plant_package.rs"]
mod synthetic;

fn package() -> &'static PlantPackage {
    static PACKAGE: OnceLock<PlantPackage> = OnceLock::new();
    PACKAGE.get_or_init(|| {
        let mut fixture = synthetic::Fixture::new();
        let mut descriptor: serde_json::Value =
            serde_json::from_slice(&fixture.files["vegetation-scene.json"]).unwrap();
        for group in descriptor["groups"].as_array_mut().unwrap() {
            group["query_radius"] = serde_json::json!(256.0);
            group["cell_size"] = serde_json::json!(32.0);
            group["far_exclude_near"] = serde_json::json!(false);
            group["near_range"] = serde_json::json!(80.0);
            group["refresh_distance"] = serde_json::json!(2.0);
            group["sort_far"] = serde_json::json!(true);
            group["streams"] = serde_json::json!({});
            let mut level = group["levels"][0].clone();
            level["near"] = serde_json::json!(true);
            level["capacity"] = serde_json::json!(65_383);
            level["range"] = serde_json::json!([0.0, 80.0, 100.0]);
            level["kind"] = serde_json::json!(2);
            if group["name"] == "canopy" {
                let mut far = level.clone();
                far["near"] = serde_json::json!(false);
                far["range"] = serde_json::json!([70.0, 160.0, 200.0]);
                level["indices"] = serde_json::json!("near-indices.bin");
                group["levels"] = serde_json::json!([level, far]);
            } else {
                group["levels"] = serde_json::json!([level]);
            }
        }
        fixture.files.insert(
            "near-indices.bin".into(),
            [0_u32, 1, 2, 0, 1, 2]
                .into_iter()
                .flat_map(u32::to_le_bytes)
                .collect(),
        );
        fixture.files.insert(
            "vegetation-scene.json".into(),
            serde_json::to_vec(&descriptor).unwrap(),
        );
        let raw = fixture.index();
        PlantPackage::load(&raw, bytes_sha256(&raw), |name, _| {
            Ok(fixture.files[name].clone())
        })
        .unwrap()
    })
}

pub(super) struct Fixture {
    pub(super) population: ImportedPopulation,
    pub(super) zone: ZoneEditSession,
    pub(super) heights: Vec<u8>,
    pub(super) terrain: ContentHash,
    pub(super) policy: CellStreamingPolicy,
}
impl Fixture {
    fn new() -> Self {
        Self::with_canopy_stretch(0.0)
    }
    pub(super) fn with_canopy_stretch(stretch: f64) -> Self {
        let mut vegetation = VegetationDocument::from_json(include_str!(
            "../../../../../../examples/visual_valley/vegetation.v1.json"
        ))
        .unwrap();
        vegetation.schema_version = 2;
        vegetation
            .layers
            .retain(|layer| layer.kind == VegetationKind::Tree);
        vegetation.layers.truncate(1);
        vegetation.layers[0].material_filter = None;
        vegetation.layers[0].density_mask = None;
        vegetation.exclusions.clear();
        let bytes = instance_bytes(stretch);
        let mut parameters: Vec<String> =
            forge_assets::source_vegetation_package::profile::PARAMETERS
                .iter()
                .map(|name| (*name).into())
                .collect();
        parameters.push("source_order".into());
        vegetation.imported_source = Some(ImportedVegetationSource {
            grass: None,
            schema: "forge.imported-vegetation".into(),
            version: 1,
            blocks: vec![ImportedVegetationBlock {
                layer_id: vegetation.layers[0].id.to_string(),
                prototype_key:
                    forge_assets::source_vegetation_package::profile::PlantPrototype::Trees
                        .key()
                        .into(),
                count: 4,
                encoding: ImportedInstanceEncoding::TransformF64LeV1,
                instances_sha256: bytes_sha256(&bytes),
                parameter_names: parameters,
            }],
        });
        let population =
            ImportedPopulation::resolve(&vegetation, |_, _| Ok(bytes.clone())).unwrap();
        let heights: Vec<_> = (0..512 * 512).flat_map(|_| 2.0_f32.to_le_bytes()).collect();
        let mut terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        terrain.schema_version = 4;
        terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        terrain.height_sampling.base_elevation_m = 0.0;
        terrain.height_sampling.sample_spacing_m = 1.0;
        terrain.height_sampling.amplitude_m = 0.0;
        terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
        terrain.height_sampling.octaves = 1;
        terrain.height_sampling.lacunarity = 2.0;
        terrain.height_sampling.persistence = 0.5;
        terrain.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 512,
            height: 512,
            origin_xz_m: [-256.0; 2],
            sample_spacing_m: 1.0,
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: bytes_sha256(&heights),
        });
        let mut zone = ZoneEditSession::from_initial_terrain(terrain).unwrap();
        let mut base = zone.head().base.clone();
        base.vegetation = Some(vegetation);
        zone.execute(
            &zone
                .head()
                .prepare_regeneration(base, CommandId::deterministic("streamed-render/fixture"))
                .unwrap(),
        )
        .unwrap();
        let terrain = zone.head().effective().unwrap().0.content_hash().unwrap();
        Self {
            population,
            zone,
            heights,
            terrain,
            policy: CellStreamingPolicy {
                origin_xz_m: [-256.0; 2],
                cell_size_m: 64.0,
                dimensions: [8; 2],
                detail_radius: 0,
                far_radius: 2,
                max_resident_cells: 25,
                full_cell_bytes: 10,
                hlod_cell_bytes: 5,
                max_resident_bytes: 1000,
                max_staging_bytes: 2000,
            },
        }
    }
    pub(super) fn prepare(&self) -> Prepared {
        Prepared::new(
            package(),
            package().decode_geometry().unwrap(),
            &self.population,
            self.zone.head(),
            &self.heights,
            self.terrain,
        )
        .unwrap()
    }
    pub(super) fn cells(&self) -> BTreeMap<CellKey, LoadedCell> {
        [
            (CellKey { x: 1, z: 1 }, CellDetail::Full, 10),
            (CellKey { x: 3, z: 1 }, CellDetail::Hlod, 5),
        ]
        .into_iter()
        .map(|(key, detail, bytes)| {
            (
                key,
                LoadedCell {
                    key,
                    detail,
                    source: self.terrain,
                    bytes,
                },
            )
        })
        .collect()
    }
}

#[test]
fn admitted_population_uses_reference_selection_with_overlapping_source_levels() {
    let f = Fixture::new();
    let prepared = f.prepare();
    assert!(
        super::super::super::canonical::prepare(&f.population, Some((f.zone.head(), &f.heights)))
            .is_ok()
    );
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let cells = f.cells();
    let layout = TerrainHlodLayout::new(
        &f.policy,
        &cells
            .iter()
            .map(|(&key, cell)| (key, cell.detail))
            .collect(),
    )
    .unwrap();
    let envelopes = BTreeMap::from([(
        forge_assets::source_vegetation_package::profile::PlantPrototype::Trees
            .key()
            .into(),
        PrototypeEnvelope {
            radius_m: 2.0,
            deformation_m: 0.5,
        },
    )]);
    let selected = streaming::select(
        &f.population,
        &view,
        AdmittedPlantCells {
            policy: &f.policy,
            cells: &cells,
            distant: &layout,
            terrain_hash: f.terrain,
            head_hash: view.head_hash,
        },
        &envelopes,
    )
    .unwrap();
    let plan = prepared
        .admit(&selected)
        .and_then(|admitted| prepared.select(&admitted, Vec3::new(-64.0, 5.0, -192.0)))
        .unwrap();
    assert_eq!(
        plan.indices,
        prepared
            .scene
            .selection_indices(Vec3::new(-64.0, 5.0, -192.0))
            .unwrap()
    );
    // This tree profile has a 75 m per-instance query radius (not 256 m).
    // Only the source at the camera enters the near list.
    // The far list keeps all four, including that overlapping source.
    assert_eq!((plan.detail_instances, plan.group_instances), (1, 4));
    assert_eq!(plan.parts.len(), 5);
    let admitted = prepared.admit(&selected).unwrap();
    let distant = prepared.select(&admitted, Vec3::splat(1000.0)).unwrap();
    assert_eq!((distant.detail_instances, distant.group_instances), (0, 4));
    assert_eq!(admitted.instances.len(), 4);
    let returned = prepared
        .select(&admitted, Vec3::new(-64.0, 5.0, -192.0))
        .unwrap();
    assert_eq!(returned.indices, plan.indices);
    assert_eq!(returned.parts, plan.parts);
    assert!(prepared.select(&admitted, Vec3::splat(f32::NAN)).is_err());
    assert!(plan.parts.keys().all(|(_, lod)| *lod <= 1));
    assert_eq!(
        plan.parts.len(),
        plan.detail_instances + plan.group_instances
    );
    assert_eq!(
        plan.cpu_selected_triangles,
        plan.detail_instances * 2 + plan.group_instances
    );
    // Moving 2 m inside the same admitted terrain cell crosses the actual
    // source radius, without changing the canonical admission or far list.
    let inside = prepared
        .select(&admitted, Vec3::new(10.0, 5.0, -192.0))
        .unwrap();
    let outside = prepared
        .select(&admitted, Vec3::new(12.0, 5.0, -192.0))
        .unwrap();
    assert_eq!((inside.detail_instances, outside.detail_instances), (1, 0));
    assert_eq!((inside.group_instances, outside.group_instances), (4, 4));
}

#[test]
fn changed_raw_pose_is_refused_even_when_the_source_f32_matrix_is_identical() {
    let f = Fixture::new();
    let prepared = f.prepare();
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let cells = f.cells();
    let layout = TerrainHlodLayout::new(
        &f.policy,
        &cells
            .iter()
            .map(|(&key, cell)| (key, cell.detail))
            .collect(),
    )
    .unwrap();
    let envelopes = BTreeMap::from([(
        forge_assets::source_vegetation_package::profile::PlantPrototype::Trees
            .key()
            .into(),
        PrototypeEnvelope {
            radius_m: 2.0,
            deformation_m: 0.5,
        },
    )]);
    let mut selected = streaming::select(
        &f.population,
        &view,
        AdmittedPlantCells {
            policy: &f.policy,
            cells: &cells,
            distant: &layout,
            terrain_hash: f.terrain,
            head_hash: view.head_hash,
        },
        &envelopes,
    )
    .unwrap();
    let plant = &selected.plants[0];
    let original = forge_assets::source_vegetation_package::packing::pack(
        plant.prototype_key,
        plant.parameter_names,
        plant.entity.source.position_m,
        plant.entity.source.rotation_xyzw,
        plant.entity.source.scale,
        &plant.entity.source.parameters,
    )
    .unwrap();
    let mut changed = plant.entity.clone();
    changed.source.position_m[0] += 1e-8;
    let packed = forge_assets::source_vegetation_package::packing::pack(
        plant.prototype_key,
        plant.parameter_names,
        changed.source.position_m,
        changed.source.rotation_xyzw,
        changed.source.scale,
        &changed.source.parameters,
    )
    .unwrap();
    assert_eq!(
        original.matrix.map(f32::to_bits),
        packed.matrix.map(f32::to_bits)
    );
    selected.plants[0].entity = &changed;
    assert!(
        prepared
            .admit(&selected)
            .and_then(|admitted| prepared.select(&admitted, Vec3::ZERO))
            .is_err()
    );
    selected.head_hash = bytes_sha256(b"wrong authored head");
    assert!(
        prepared
            .admit(&selected)
            .and_then(|admitted| prepared.select(&admitted, Vec3::ZERO))
            .is_err()
    );
}

pub(super) fn world() -> (World, PlantMaterial) {
    let mut world = World::new();
    world.init_resource::<Assets<Mesh>>();
    world.init_resource::<Assets<Image>>();
    world.init_resource::<Assets<PlantMaterial>>();
    world.init_resource::<Assets<ShaderBuffer>>();
    let image = world.resource_mut::<Assets<Image>>().add(Image::default());
    let buffer = world
        .resource_mut::<Assets<ShaderBuffer>>()
        .add(ShaderBuffer::from(vec![Vec4::ZERO]));
    let material = PlantMaterial {
        params: super::super::super::Params {
            i_pos: Vec4::ZERO,
            i_dat: Vec4::ZERO,
            wind: Vec4::ZERO,
            gust_lod: Vec4::ZERO,
            sun_night: Vec4::ZERO,
            lod_end_frame: Vec4::ZERO,
        },
        detail: image.clone(),
        leaf: image.clone(),
        impostor_a: image.clone(),
        impostor_b: image,
        instances: buffer.clone(),
        models: buffer,
        kind: 0,
    };
    (world, material)
}

fn world_material(
    world: &mut World,
    reference: PlantMaterial,
) -> crate::source_vegetation::world::WorldPlantMaterial {
    use crate::source_lighting::frame::Frame;
    use crate::source_vegetation::world::WorldPlantMaterial;
    world.init_resource::<Assets<WorldPlantMaterial>>();
    crate::source_lighting::local_lights::gpu::install_neutral(world).unwrap();
    let frame = Frame::new(Vec3::Z, Vec4::X, Vec4::ZERO)
        .unwrap()
        .stage(world)
        .unwrap();
    WorldPlantMaterial {
        params: reference.params,
        detail: reference.detail,
        leaf: reference.leaf,
        impostor_a: reference.impostor_a,
        impostor_b: reference.impostor_b,
        instances: reference.instances,
        models: reference.models,
        frame: frame.clone(),
        grass_heights: None,
        grass_mask: None,
        grass_deltas: None,
        grass_field: Vec4::ZERO,
        scene_lighting: frame.clone(),
        live_shadows: frame.clone(),
        local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
        environment: None,
        hill_shadow: None,
        object_shadow: None,
        object_settings: crate::source_shadows::ObjectShadowSettings::default(),
        clouds: [Vec4::ZERO; 5],
        cloud_shadow: None,
        source_lighting: false,
        hill_domain: bevy::prelude::Vec4::new(-1024.0, -1024.0, 2048.0, 0.0),
        kind: reference.kind,
    }
}

#[test]
fn world_material_keeps_shadow_casters_without_inventing_gpu_admission() {
    use bevy::light::NotShadowCaster;
    let f = Fixture::new();
    let (mut world, reference) = world();
    let material = world_material(&mut world, reference);
    let mut dependencies = Vec::new();
    material.visit_dependencies(&mut |id| dependencies.push(id));
    // Inactive scene/shadow bindings still reuse the frame. Local lighting adds
    // exactly its one shared neutral buffer, not a per-material packet copy.
    dependencies.sort();
    dependencies.dedup();
    let expected = [
        material.detail.id().untyped(),
        material.leaf.id().untyped(),
        material.impostor_a.id().untyped(),
        material.impostor_b.id().untyped(),
        material.instances.id().untyped(),
        material.models.id().untyped(),
        material.frame.id().untyped(),
        crate::source_lighting::local_lights::gpu::BUFFER
            .id()
            .untyped(),
    ]
    .into_iter()
    .collect::<std::collections::BTreeSet<_>>();
    assert_eq!(
        dependencies
            .into_iter()
            .collect::<std::collections::BTreeSet<_>>(),
        expected
    );
    let mut prepared = f.prepare();
    for group in &mut prepared.scene.groups {
        for record in &mut group.records {
            record.i_dat.y = 1.0;
        }
    }
    let resident = Resident::stage(
        &mut world,
        prepared,
        &material,
        EntityId::deterministic("streamed-render/world-material"),
    )
    .unwrap();
    assert!(resident.projection_intact(&world));
    assert!(!resident.ready(&world));
    let entity = world
        .query::<(
            Entity,
            &MeshMaterial3d<crate::source_vegetation::world::WorldPlantMaterial>,
        )>()
        .iter(&world)
        .next()
        .unwrap()
        .0;
    assert!(world.get::<NotShadowCaster>(entity).is_none());
    world.entity_mut(entity).insert(NotShadowCaster);
    assert!(!resident.projection_intact(&world));
    world.entity_mut(entity).remove::<NotShadowCaster>();
    assert!(resident.projection_intact(&world));
    resident.retire(&mut world).unwrap();
    assert!(crate::render_inventory::current_owner(&world).is_none());
}

#[test]
fn world_material_refuses_grass_before_staging_any_source_assets() {
    let f = Fixture::new();
    let (mut world, reference) = world();
    let material = world_material(&mut world, reference);
    let mut prepared = f.prepare();
    prepared.scene.groups[0].levels[0].description.kind = 3;
    let buffers = world.resource::<Assets<ShaderBuffer>>().len();
    assert!(
        Resident::stage(
            &mut world,
            prepared,
            &material,
            EntityId::deterministic("streamed-render/world-grass-refused")
        )
        .is_err()
    );
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
    assert_eq!(
        world
            .resource::<Assets<crate::source_vegetation::world::WorldPlantMaterial>>()
            .len(),
        0
    );
    assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), buffers);
    assert!(crate::render_inventory::current_owner(&world).is_none());
}

#[test]
fn world_material_refuses_zero_canopy_stretch_before_staging() {
    let f = Fixture::new();
    let (mut world, reference) = world();
    let material = world_material(&mut world, reference);
    let mut prepared = f.prepare();
    for group in &mut prepared.scene.groups {
        for record in &mut group.records {
            record.i_dat.y = 0.0;
        }
        for level in &mut group.levels {
            level.description.kind = 2;
        }
    }
    let buffers = world.resource::<Assets<ShaderBuffer>>().len();
    assert!(
        Resident::stage(
            &mut world,
            prepared,
            &material,
            EntityId::deterministic("streamed-render/zero-canopy-refused")
        )
        .is_err()
    );
    assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
    assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), buffers);
    assert!(crate::render_inventory::current_owner(&world).is_none());
}

#[test]
#[expect(
    clippy::too_many_lines,
    reason = "Exercise staging, source exclusion, refused publication and ownership loss on the same resident"
)]
fn generic_material_resources_are_declared_and_cpu_warm_entities_cannot_publish() {
    let f = Fixture::new();
    let prepared = f.prepare();
    let view = f
        .population
        .terrain_view(f.zone.head(), &f.heights)
        .unwrap();
    let mut dependencies = Vec::new();
    let (mut world, base) = world();
    base.visit_dependencies(&mut |id| dependencies.push(id));
    assert_eq!(dependencies.len(), 6);
    let owner = EntityId::deterministic("streamed-render/owner");
    let mut resident = Resident::stage(&mut world, prepared, &base, owner).unwrap();
    assert_eq!(resident.entity_count(), 2);
    assert!(resident.projection_intact(&world));
    assert!(!resident.ready(&world));
    let parameters = f.population.layers()[0].parameter_names.as_slice();
    let selected = StreamedPlantSelection {
        head_hash: view.head_hash,
        terrain_hash: f.terrain,
        plants: view
            .layers
            .iter()
            .flat_map(|layer| {
                layer
                    .instances
                    .iter()
                    .map(move |entity| streaming::SelectedPlant {
                        entity,
                        layer_id: layer.layer_id,
                        prototype_key:
                            forge_assets::source_vegetation_package::profile::PlantPrototype::Trees
                                .key(),
                        parameter_names: parameters,
                        representation: PlantRepresentation::Detail,
                    })
            })
            .collect(),
        collisions: Vec::new(),
    };
    let admitted = resident.admit(&selected).unwrap();
    let replaced = [selected.plants[0].entity.id].into();
    let replacement = resident
        .select_excluding(&admitted, Vec3::ZERO, &replaced)
        .unwrap();
    assert!(
        replacement
            .parts
            .keys()
            .all(|(id, _)| !replaced.contains(id))
    );
    assert_eq!(
        replacement.parts.len(),
        replacement
            .indices
            .iter()
            .flatten()
            .map(Vec::len)
            .sum::<usize>()
    );
    assert_eq!(resident.entity_count(), 2);
    assert!(
        resident
            .select_excluding(
                &admitted,
                Vec3::ZERO,
                &[EntityId::deterministic("foreign-replacement")].into()
            )
            .is_err()
    );
    assert!(resident.publish(&mut world, replacement, None).is_err());
    assert!(resident.projection_intact(&world));
    let plan = resident.select(&admitted, Vec3::ZERO).unwrap();
    assert!(resident.publish(&mut world, plan, None).is_err());
    assert_eq!(resident.entity_count(), 2);
    assert_eq!(resident.generation(), 1);
    let material = world
        .query::<&MeshMaterial3d<PlantMaterial>>()
        .iter(&world)
        .next()
        .unwrap()
        .0
        .clone();
    let kind = world
        .resource::<Assets<PlantMaterial>>()
        .get(&material)
        .unwrap()
        .kind;
    world
        .resource_mut::<Assets<PlantMaterial>>()
        .get_mut(&material)
        .unwrap()
        .kind = (kind + 1) % 3;
    assert!(!resident.projection_intact(&world));
    world
        .resource_mut::<Assets<PlantMaterial>>()
        .get_mut(&material)
        .unwrap()
        .kind = kind;
    assert!(resident.projection_intact(&world));
    world
        .resource_mut::<Assets<PlantMaterial>>()
        .get_mut(&material)
        .unwrap()
        .params
        .gust_lod
        .z += 1.0;
    assert!(!resident.projection_intact(&world));
    resident.retire(&mut world).unwrap();
    assert!(crate::render_inventory::current_owner(&world).is_none());
}

#[test]
fn an_authored_empty_population_does_not_fabricate_a_gpu_receipt() {
    let mut f = Fixture::new();
    let exclusions = vec![forge_project::VegetationExclusion::PolylineCorridor {
        id: forge_project::VegetationExclusionId::new("all"),
        points_xz_m: vec![[-256.0, 0.0], [256.0, 0.0]],
        half_width_m: 256.0,
    }];
    f.zone
        .execute(
            &f.zone
                .head()
                .prepare_vegetation_exclusions(
                    VegetationExclusionEdit::Replace { exclusions },
                    CommandId::deterministic("streamed-render/empty"),
                )
                .unwrap(),
        )
        .unwrap();
    f.terrain = f.zone.head().effective().unwrap().0.content_hash().unwrap();
    let prepared = f.prepare();
    assert!(prepared.pins.is_empty());
    let (mut world, base) = world();
    let owner = EntityId::deterministic("streamed-render/empty");
    let mut resident = Resident::stage(&mut world, prepared, &base, owner).unwrap();
    let selected = StreamedPlantSelection {
        head_hash: f.zone.head().content_hash().unwrap(),
        terrain_hash: f.terrain,
        plants: Vec::new(),
        collisions: Vec::new(),
    };
    resident
        .publish(
            &mut world,
            resident
                .admit(&selected)
                .and_then(|admitted| resident.select(&admitted, Vec3::ZERO))
                .unwrap(),
            None,
        )
        .unwrap();
    assert!(resident.ready(&world));
    assert!(
        crate::render_inventory::acquire(&world, owner, resident.identity(), resident.generation())
            .is_none()
    );
}

#[test]
fn staging_refuses_a_debug_surface_or_an_already_selected_cell_mode() {
    let f = Fixture::new();
    let (mut world, mut base) = world();
    let owner = EntityId::deterministic("streamed-render/control-refusal");
    base.params.lod_end_frame.w = 1.0;
    assert!(Resident::stage(&mut world, f.prepare(), &base, owner).is_err());
    base.params.lod_end_frame.w = 0.0;
    base.params.lod_end_frame.z = 1.0;
    assert!(Resident::stage(&mut world, f.prepare(), &base, owner).is_err());
    assert!(crate::render_inventory::current_owner(&world).is_none());
    assert!(world.query::<&Mesh3d>().iter(&world).next().is_none());
}

fn instance_bytes(stretch: f64) -> Vec<u8> {
    let mut bytes = Vec::new();
    for (index, (x, z)) in [
        (-192.0_f64, -192.0_f64),
        (-64.0, -192.0),
        (192.0, 192.0),
        (256.0, 256.0),
    ]
    .into_iter()
    .enumerate()
    {
        bytes.extend([u8::try_from(index + 1).unwrap(); 32]);
        for value in [
            x,
            5.0,
            z,
            0.0,
            0.0,
            0.0,
            1.0,
            1.0,
            1.0,
            1.0,
            0.0,
            stretch,
            1.0,
            0.25,
            1.0,
            f64::from(u32::try_from(index).unwrap()),
        ] {
            bytes.extend(value.to_le_bytes());
        }
    }
    bytes
}

#[test]
fn reference_refresh_budget_is_shared_and_remains_radial() {
    use crate::source_vegetation_selection::needs_refresh;
    assert!(needs_refresh(None, [0.0; 3]));
    assert!(!needs_refresh(Some([0.0; 3]), [0.999, 0.0, 0.0]));
    assert!(needs_refresh(Some([0.0; 3]), [1.0, 0.0, 0.0]));
    assert!(needs_refresh(Some([0.0; 3]), [0.0, -1.0, 0.0]));
    assert!(!needs_refresh(
        Some([10.0, 0.0, -192.0]),
        [10.5, 0.0, -192.0]
    ));
    assert!(needs_refresh(
        Some([10.0, 0.0, -192.0]),
        [12.0, 0.0, -192.0]
    ));
}

#[test]
fn editor_and_game_prepare_the_same_small_authored_population() {
    let f = Fixture::new();
    let expected = f.prepare();
    assert!(f.population.instance_count() < 65_383);
    let scene = crate::source_vegetation::scene::from_population(
        package(),
        &f.population,
        f.zone.head(),
        &f.heights,
    )
    .unwrap();
    assert_eq!(scene.head_hash, Some(f.zone.head().content_hash().unwrap()));
    assert_eq!(
        scene.groups.iter().map(|g| g.records.len()).sum::<usize>(),
        f.population.instance_count()
    );
    assert_eq!(
        scene
            .groups
            .iter()
            .map(|g| g.identities.clone())
            .collect::<Vec<_>>(),
        expected
            .scene
            .groups
            .iter()
            .map(|g| g.identities.clone())
            .collect::<Vec<_>>()
    );
    assert_eq!(
        scene.selection_indices(Vec3::ZERO).unwrap(),
        expected.scene.selection_indices(Vec3::ZERO).unwrap()
    );
}

#[test]
fn plant_host_uses_the_actual_cell_source_in_editor_and_game() {
    use std::sync::Arc;
    for fault in 0..3 {
        let f = Fixture::new();
        let mut document = f.zone.head().effective().unwrap().0;
        if fault == 1 {
            document.height_sampling.base_elevation_m += 0.25;
        }
        let plan = Arc::new(
            forge_world::sampled_terrain::SampledTerrainPlan::from_document(&document, &f.heights)
                .unwrap(),
        );
        let policy = crate::cell_streaming::sampled_policy(plan.raster().source()).unwrap();
        let mut app = App::new();
        app.init_resource::<Assets<StandardMaterial>>();
        if fault == 0 {
            // Legacy game metadata alone cannot admit vegetation without a real cell owner.
            app.insert_resource(crate::StreamedTerrainIdentity {
                document_hash: f.terrain,
                raster_hash: plan.raster().source().samples_sha256,
                raster_bytes: u64::try_from(f.heights.len()).unwrap(),
                policy,
            });
        } else {
            app.add_plugins(
                crate::cell_streaming::SampledCellStreamingPlugin::new(
                    plan,
                    policy,
                    EntityId::deterministic("cell-owner/terrain"),
                    StandardMaterial::default(),
                    [0.0; 2],
                )
                .unwrap(),
            );
            assert!(
                !app.world()
                    .contains_resource::<crate::StreamedTerrainIdentity>()
            );
        }
        let result = host::admit_terrain(app.world(), f.terrain);
        assert_eq!(result.is_ok(), fault == 2, "{result:?}");
    }
}
