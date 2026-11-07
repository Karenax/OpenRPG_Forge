use super::*;
use crate::gpu_readiness::assets::GpuAssets;
use crate::source_surface::nature::{self, NatureExtension, NatureMaterial};
use bevy::{
    ecs::system::SystemState,
    render::{render_resource::AsBindGroup, storage::ShaderBuffer},
};

#[derive(Asset, TypePath, AsBindGroup, Clone, Debug)]
pub(super) struct SourceMaterial {
    #[texture(0)]
    #[sampler(1)]
    #[dependency]
    pub image: Handle<Image>,
    #[storage(2, read_only)]
    #[dependency]
    pub buffer: Handle<ShaderBuffer>,
}
impl Material for SourceMaterial {}

#[test]
fn fish_material_exposes_all_images_and_buffers_to_gpu_inventory() {
    use crate::source_fish::material::{self, SurfaceParameters};
    let mut buffers = Assets::<ShaderBuffer>::default();
    let mut images = Assets::<Image>::default();
    let mut fish = material::prepare_scene(
        SurfaceParameters {
            pattern_and_seed: 0.0,
            source_length_m: 1.0,
            flags: [0.0; 4],
        },
        Handle::default(),
        &crate::source_surface::tests::lighting(),
        0.0,
    )
    .unwrap();
    let mut buffer = || buffers.add(ShaderBuffer::from(vec![Vec4::ZERO]));
    fish.clock = buffer();
    fish.skin = buffer();
    fish.scene_buffer = buffer();
    fish.local_lights = buffer();
    fish.live_shadows = buffer();
    fish.underwater_map_frame = buffer();
    let mut image = || images.add(Image::default());
    fish.environment = image();
    fish.hill_shadow = image();
    fish.object_shadow = image();
    fish.cloud_shadow = Some(image());
    fish.underwater_level = image();
    fish.ground_bounce = Some(image());
    let expected = [
        fish.clock.id().untyped(),
        fish.skin.id().untyped(),
        fish.scene_buffer.id().untyped(),
        fish.local_lights.id().untyped(),
        fish.live_shadows.id().untyped(),
        fish.underwater_map_frame.id().untyped(),
        fish.environment.id().untyped(),
        fish.hill_shadow.id().untyped(),
        fish.object_shadow.id().untyped(),
        fish.cloud_shadow.as_ref().unwrap().id().untyped(),
        fish.underwater_level.id().untyped(),
        fish.ground_bounce.as_ref().unwrap().id().untyped(),
    ];
    let mut actual = Vec::new();
    visit_material_dependencies(&fish, &mut |id| actual.push(id));
    assert_eq!(actual.len(), expected.len());
    assert!(expected.iter().all(|id| actual.contains(id)));
}

pub(super) struct Fixture {
    pub app: App,
    pub mesh: Handle<Mesh>,
    pub material: Handle<SourceMaterial>,
    pub buffer: Handle<ShaderBuffer>,
    pub image: Handle<Image>,
    pub owner: EntityId,
    pub identity: ContentHash,
}
impl Fixture {
    pub(super) fn new() -> Self {
        let mut app = App::new();
        app.add_plugins(RenderInventoryPlugin);
        let world = app.world_mut();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<SourceMaterial>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let mesh = world
            .resource_mut::<Assets<Mesh>>()
            .add(Mesh::from(Cuboid::new(1.0, 1.0, 1.0)));
        let image = world.resource_mut::<Assets<Image>>().add(Image::default());
        let buffer = world
            .resource_mut::<Assets<ShaderBuffer>>()
            .add(ShaderBuffer::from(vec![Vec4::ONE]));
        let material = world
            .resource_mut::<Assets<SourceMaterial>>()
            .add(SourceMaterial {
                image: image.clone(),
                buffer: buffer.clone(),
            });
        Self {
            app,
            mesh,
            material,
            buffer,
            image,
            owner: EntityId::deterministic("render-inventory/owner"),
            identity: ContentHash::from_bytes([7; 32]),
        }
    }
    pub(super) fn request(&mut self, generation: u64) -> Result<(), String> {
        request(
            self.app.world_mut(),
            self.owner,
            self.identity,
            generation,
            std::slice::from_ref(&self.mesh),
            std::slice::from_ref(&self.material),
        )
    }
}

#[test]
fn scoped_owners_coexist_with_the_primary_and_refuse_budget_and_cross_slot_aliases() {
    let mut f = Fixture::new();
    f.request(1).unwrap();
    let mesh = [f.mesh.clone()];
    let material = [f.material.clone()];
    assert!(request_scoped(f.app.world_mut(), f.owner, f.identity, 1, &mesh, &material).is_err());
    for index in 0..MAX_SCOPED_INVENTORIES {
        let owner = EntityId::deterministic(format!("quality-scope-{index}"));
        request_scoped(f.app.world_mut(), owner, f.identity, 1, &mesh, &material).unwrap();
        assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());
    }
    let extra = EntityId::deterministic("quality-scope-overflow");
    assert!(request_scoped(f.app.world_mut(), extra, f.identity, 1, &mesh, &material).is_err());
    assert_eq!(current_owner(f.app.world()), Some(f.owner));
    release_scoped(
        f.app.world_mut(),
        EntityId::deterministic("quality-scope-0"),
    );
    request_scoped(f.app.world_mut(), extra, f.identity, 1, &mesh, &material).unwrap();
    release(f.app.world_mut(), f.owner).unwrap();
    assert!(request(f.app.world_mut(), extra, f.identity, 1, &mesh, &material).is_err());
    assert_eq!(
        f.app.world().resource::<ScopedRequests>().0.len(),
        MAX_SCOPED_INVENTORIES
    );
}

#[test]
fn scoped_source_containment_requires_live_exact_assets_and_material_dependencies() {
    let mut f = Fixture::new();
    let second = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    let owner = EntityId::deterministic("subset-owner");
    let meshes = [f.mesh.clone(), second.clone()];
    let material = [f.material.clone()];
    request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &material).unwrap();
    assert!(scoped_contains_sources(
        f.app.world(),
        owner,
        &meshes[..1],
        &material
    ));
    assert!(scoped_contains_sources(
        f.app.world(),
        owner,
        &meshes[1..],
        &material
    ));
    assert!(!scoped_contains_sources(
        f.app.world(),
        owner,
        &[],
        &material
    ));
    let foreign = f
        .app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::default());
    assert!(!scoped_contains_sources(
        f.app.world(),
        owner,
        &[foreign],
        &material
    ));
    let different_image = f
        .app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::default());
    f.app
        .world_mut()
        .resource_mut::<Assets<SourceMaterial>>()
        .get_mut(&f.material)
        .unwrap()
        .image = different_image;
    assert!(!scoped_contains_sources(
        f.app.world(),
        owner,
        &meshes[..1],
        &material
    ));
    f.app
        .world_mut()
        .resource_mut::<Assets<SourceMaterial>>()
        .get_mut(&f.material)
        .unwrap()
        .image = f.image.clone();
    f.app
        .world_mut()
        .resource_mut::<Assets<Mesh>>()
        .remove(second.id());
    assert!(!scoped_contains_sources(
        f.app.world(),
        owner,
        &meshes[..1],
        &material
    ));
    release_scoped(f.app.world_mut(), owner);
    assert!(!scoped_contains_sources(
        f.app.world(),
        owner,
        &meshes[..1],
        &material
    ));
}

#[test]
fn scoped_effect_changes_need_a_new_generation_and_preserve_primary_ownership() {
    let mut f = Fixture::new();
    f.request(1).unwrap();
    let quality = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let preset = quality.preset(forge_project::VisualQualityTier::High);
    let effects = RenderEffectRequirements::display(preset.shadows, preset.post_process);
    let owner = EntityId::deterministic("scoped-effects");
    let meshes = [f.mesh.clone()];
    let materials = [f.material.clone()];
    request_scoped_with_effects(
        f.app.world_mut(),
        owner,
        f.identity,
        1,
        &meshes,
        &materials,
        effects.clone(),
    )
    .unwrap();
    request_scoped_with_effects(
        f.app.world_mut(),
        owner,
        f.identity,
        1,
        &meshes,
        &materials,
        effects,
    )
    .unwrap();
    let mut changed = preset.post_process;
    changed.bloom_enabled = !changed.bloom_enabled;
    let effects = RenderEffectRequirements::display(preset.shadows, changed);
    assert!(
        request_scoped_with_effects(
            f.app.world_mut(),
            owner,
            f.identity,
            1,
            &meshes,
            &materials,
            effects.clone()
        )
        .is_err()
    );
    request_scoped_with_effects(
        f.app.world_mut(),
        owner,
        f.identity,
        2,
        &meshes,
        &materials,
        effects,
    )
    .unwrap();
    assert_eq!(current_owner(f.app.world()), Some(f.owner));
    assert_eq!(f.app.world().resource::<Request>().generation, 1);
}

#[test]
fn custom_cpu_sources_include_storage_and_never_acknowledge_gpu_preparation() {
    let mut f = Fixture::new();
    request(
        f.app.world_mut(),
        f.owner,
        f.identity,
        1,
        &[f.mesh.clone(), f.mesh.clone()],
        &[f.material.clone(), f.material.clone()],
    )
    .unwrap();
    let required = f.app.world().resource::<Request>().required.clone();
    let mut state = SystemState::<GpuAssets>::new(f.app.world_mut());
    let evidence = state.get(f.app.world()).unwrap().observe(&required);
    assert_eq!(
        (
            evidence.meshes_expected,
            evidence.materials_expected,
            evidence.images_expected,
            evidence.buffers_expected
        ),
        (1, 1, 1, 1)
    );
    assert_eq!(
        (
            evidence.meshes_prepared,
            evidence.materials_bound,
            evidence.images_prepared,
            evidence.buffers_prepared
        ),
        (0, 0, 0, 0)
    );
    assert!(!evidence.inventory_ready && acquire(f.app.world(), f.owner, f.identity, 1).is_none());
}

struct NatureSources {
    material: Handle<NatureMaterial>,
    images: Vec<Handle<Image>>,
    buffers: Vec<Handle<ShaderBuffer>>,
}

fn nature_sources(f: &mut Fixture) -> NatureSources {
    let world = f.app.world_mut();
    world.init_resource::<Assets<NatureMaterial>>();
    let images: Vec<_> = (0..12)
        .map(|_| world.resource_mut::<Assets<Image>>().add(Image::default()))
        .collect();
    let buffers: Vec<_> = (0..4)
        .map(|_| {
            world
                .resource_mut::<Assets<ShaderBuffer>>()
                .add(ShaderBuffer::from(vec![Vec4::ZERO]))
        })
        .collect();
    let mut base = crate::source_surface::tests::lighting();
    base.first = images[0].clone();
    base.second = images[1].clone();
    base.third = images[2].clone();
    base.fourth = images[3].clone();
    base.environment = images[4].clone();
    base.hill_shadow = images[5].clone();
    base.object_shadow = images[6].clone();
    base.cloud_shadow = Some(images[7].clone());
    base.underwater_level = images[8].clone();
    base.ground_bounce = Some(images[9].clone());
    base.scene_buffer = buffers[0].clone();
    base.local_lights = buffers[1].clone();
    base.live_shadows = buffers[2].clone();
    base.underwater_map_frame = buffers[3].clone();
    let extension = NatureExtension {
        heights: images[10].clone(),
        detail: images[11].clone(),
        terrain_domain: Vec4::new(0.0, 1.0, 2.0, 1.0),
        terrain_sun: Vec4::Y,
    };
    let material = world
        .resource_mut::<Assets<NatureMaterial>>()
        .add(nature::prepare_material(base, extension).unwrap());
    NatureSources {
        material,
        images,
        buffers,
    }
}

#[test]
fn nature_inventory_tracks_every_base_and_extension_source() {
    let mut f = Fixture::new();
    let nature = nature_sources(&mut f);
    let source = f
        .app
        .world()
        .resource::<Assets<NatureMaterial>>()
        .get(&nature.material)
        .unwrap();
    let mut actual = Vec::new();
    visit_material_dependencies(source, &mut |id| actual.push(id));
    assert_eq!(actual.len(), nature.images.len() + nature.buffers.len());
    for image in &nature.images {
        assert!(actual.contains(&image.id().untyped()));
    }
    for buffer in &nature.buffers {
        assert!(actual.contains(&buffer.id().untyped()));
    }
    let owner = EntityId::deterministic("nature-render-inventory");
    let meshes = [f.mesh.clone()];
    let materials = [nature.material.clone()];
    let height = nature.images[10].id();
    let removed = f
        .app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(height)
        .unwrap();
    assert!(request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).is_err());
    f.app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .insert(height, removed)
        .unwrap();
    request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).unwrap();
    let required = f.app.world().resource::<ScopedRequests>().0[&owner]
        .required
        .clone();
    let mut state = SystemState::<GpuAssets>::new(f.app.world_mut());
    let evidence = state.get(f.app.world()).unwrap().observe(&required);
    assert_eq!(
        (evidence.images_expected, evidence.buffers_expected),
        (12, 4)
    );
    assert_eq!(
        (evidence.images_prepared, evidence.buffers_prepared),
        (0, 0)
    );
    assert!(!evidence.inventory_ready);
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());

    for image in &nature.images {
        let removed = f
            .app
            .world_mut()
            .resource_mut::<Assets<Image>>()
            .remove(image.id())
            .unwrap();
        assert!(!scoped_sources_current(f.app.world(), owner));
        assert!(
            request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).is_err()
        );
        f.app
            .world_mut()
            .resource_mut::<Assets<Image>>()
            .insert(image.id(), removed)
            .unwrap();
        assert!(scoped_sources_current(f.app.world(), owner));
    }
    for buffer in &nature.buffers {
        let removed = f
            .app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .remove(buffer.id())
            .unwrap();
        assert!(!scoped_sources_current(f.app.world(), owner));
        assert!(
            request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).is_err()
        );
        f.app
            .world_mut()
            .resource_mut::<Assets<ShaderBuffer>>()
            .insert(buffer.id(), removed)
            .unwrap();
        assert!(scoped_sources_current(f.app.world(), owner));
    }
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());
}

#[cfg(feature = "physics-rapier")]
#[test]
fn source_fragment_inventory_tracks_base_and_all_extension_textures() {
    use crate::source_fragment::{SourceFragmentExtension, SourceFragmentMaterial};

    let mut f = Fixture::new();
    f.app
        .world_mut()
        .init_resource::<Assets<SourceFragmentMaterial>>();
    let images: Vec<_> = (0..7)
        .map(|_| {
            f.app
                .world_mut()
                .resource_mut::<Assets<Image>>()
                .add(Image::default())
        })
        .collect();
    let material = f
        .app
        .world_mut()
        .resource_mut::<Assets<SourceFragmentMaterial>>()
        .add(SourceFragmentMaterial {
            base: StandardMaterial {
                base_color_texture: Some(images[0].clone()),
                normal_map_texture: Some(images[1].clone()),
                ..default()
            },
            extension: SourceFragmentExtension {
                rgb_rows: [Vec4::ZERO; 3],
                metallic: Vec4::ZERO,
                occlusion: Vec4::ZERO,
                roughness: Vec4::ZERO,
                scalar_offsets_normal_strength: Vec4::ZERO,
                backface: Vec4::ZERO,
                world_params: Vec4::ZERO,
                world_mask_uv: Vec4::ZERO,
                world_tints: [Vec4::ZERO; 3],
                world_axes: [Vec4::ZERO; 3],
                world_control: Vec4::ZERO,
                source_orm: images[2].clone(),
                source_normal: Some(images[3].clone()),
                source_blend_mask: Some(images[4].clone()),
                source_noise: Some(images[5].clone()),
                source_noise_mask: Some(images[6].clone()),
            },
        });
    let owner = EntityId::deterministic("source-fragment-inventory");
    let meshes = [f.mesh.clone()];
    let materials = [material.clone()];
    request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).unwrap();
    let required = f.app.world().resource::<ScopedRequests>().0[&owner]
        .required
        .clone();
    let mut gpu = SystemState::<GpuAssets>::new(f.app.world_mut());
    let evidence = gpu.get(f.app.world()).unwrap().observe(&required);
    assert_eq!((evidence.images_expected, evidence.images_prepared), (7, 0));
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());

    f.app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(images[4].id());
    assert!(!scoped_sources_current(f.app.world(), owner));
    assert!(request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).is_err());

    f.app
        .world_mut()
        .resource_mut::<Assets<SourceFragmentMaterial>>()
        .get_mut(&material)
        .unwrap()
        .extension
        .source_blend_mask = None;
    assert!(request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).is_err());
    request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).unwrap();
    assert!(scoped_sources_current(f.app.world(), owner));
}

#[cfg(feature = "physics-rapier")]
#[test]
fn source_graph_inventory_tracks_texture_and_state_changes_without_replacing_the_material() {
    use crate::source_graph::{SourceGraphMaterial, SourceGraphStateBufferBinding};
    use crate::source_particle_host::tests as particle_tests;

    let (source_world, _, source_handle) = particle_tests::setup(false);
    let mut source = source_world
        .resource::<Assets<SourceGraphMaterial>>()
        .get(&source_handle)
        .unwrap()
        .clone();
    let mut f = Fixture::new();
    f.app
        .world_mut()
        .init_resource::<Assets<SourceGraphMaterial>>();
    let second_image = f
        .app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::default());
    let state = SourceGraphStateBufferBinding::new(
        f.buffer.clone(),
        f.app.world().resource::<Assets<ShaderBuffer>>(),
    )
    .unwrap();
    source.extension.set_source_state_buffer(&state);
    source.extension.texture_0 = Some(f.image.clone());
    source.extension.texture_9 = Some(second_image.clone());
    let material = f
        .app
        .world_mut()
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .add(source);
    let owner = EntityId::deterministic("source-graph-inventory");
    let meshes = [f.mesh.clone()];
    let materials = [material.clone()];
    request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).unwrap();
    let required = f.app.world().resource::<ScopedRequests>().0[&owner]
        .required
        .clone();
    let mut gpu = SystemState::<GpuAssets>::new(f.app.world_mut());
    let evidence = gpu.get(f.app.world()).unwrap().observe(&required);
    assert_eq!(
        (evidence.images_expected, evidence.buffers_expected),
        (2, 1)
    );
    assert_eq!(
        (evidence.images_prepared, evidence.buffers_prepared),
        (0, 0)
    );
    assert!(acquire_scoped(f.app.world(), owner, f.identity, 1).is_none());

    f.app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .remove(second_image.id());
    assert!(!scoped_sources_current(f.app.world(), owner));
    assert!(request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).is_err());
    f.app
        .world_mut()
        .resource_mut::<Assets<SourceGraphMaterial>>()
        .get_mut(&material)
        .unwrap()
        .extension
        .texture_9 = None;
    assert!(request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).is_err());
    request_scoped(f.app.world_mut(), owner, f.identity, 2, &meshes, &materials).unwrap();
    assert!(scoped_sources_current(f.app.world(), owner));
    f.app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .remove(f.buffer.id());
    assert!(!scoped_sources_current(f.app.world(), owner));
}

#[cfg(feature = "physics-rapier")]
#[test]
fn shared_vegetation_buffer_is_required_by_scoped_and_global_inventories() {
    let mut f = Fixture::new();
    f.app
        .world_mut()
        .init_resource::<Assets<crate::VisualValleyVegetationMaterial>>();
    let material = f
        .app
        .world_mut()
        .resource_mut::<Assets<crate::VisualValleyVegetationMaterial>>()
        .add(crate::VisualValleyVegetationMaterial {
            base: StandardMaterial {
                base_color_texture: Some(f.image.clone()),
                ..default()
            },
            extension: crate::VisualValleyVegetationExtension {
                temporal_lod_buffer: f.buffer.clone(),
                ..default()
            },
        });
    f.app
        .world_mut()
        .spawn((Mesh3d(f.mesh.clone()), MeshMaterial3d(material.clone())));
    let owner = EntityId::deterministic("vegetation-inventory");
    let meshes = [f.mesh.clone()];
    let materials = [material];
    request_scoped(f.app.world_mut(), owner, f.identity, 1, &meshes, &materials).unwrap();
    let scoped = f.app.world().resource::<ScopedRequests>().0[&owner]
        .required
        .clone();
    let global = crate::gpu_readiness::assets::collect(f.app.world_mut());
    let mut gpu = SystemState::<GpuAssets>::new(f.app.world_mut());
    for required in [&scoped, &global] {
        let evidence = gpu.get(f.app.world()).unwrap().observe(required);
        assert_eq!(
            (evidence.images_expected, evidence.buffers_expected),
            (1, 1)
        );
        assert_eq!(
            (evidence.images_prepared, evidence.buffers_prepared),
            (0, 0)
        );
        assert!(!evidence.inventory_ready);
    }
    f.app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .remove(f.buffer.id());
    assert!(!scoped_sources_current(f.app.world(), owner));
    let global = crate::gpu_readiness::assets::collect(f.app.world_mut());
    let evidence = gpu.get(f.app.world()).unwrap().observe(&global);
    assert_eq!(evidence.unsupported_dependencies, 1);
    assert!(!evidence.inventory_ready);
}

#[test]
fn changed_material_dependencies_and_resized_buffers_revoke_the_source_snapshot() {
    let mut f = Fixture::new();
    f.request(1).unwrap();
    let old = f.app.world().resource::<Request>().required.clone();
    let image = f
        .app
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(Image::default());
    f.app
        .world_mut()
        .resource_mut::<Assets<SourceMaterial>>()
        .get_mut(&f.material)
        .unwrap()
        .image = image;
    assert!(!old.sources_exist::<SourceMaterial>(f.app.world()));
    assert!(f.request(1).is_err());
    f.request(2).unwrap();
    let old = f.app.world().resource::<Request>().required.clone();
    f.app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .get_mut(&f.buffer)
        .unwrap()
        .set_data(vec![Vec4::ONE; 2]);
    assert!(!old.sources_exist::<SourceMaterial>(f.app.world()));
    assert!(f.request(2).is_err());
    f.request(3).unwrap();
    f.app
        .world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .remove(f.buffer.id());
    assert!(f.request(4).is_err());
}

#[test]
fn ownership_generations_and_missing_sources_fail_without_overwriting_another_request() {
    let mut f = Fixture::new();
    assert!(f.request(0).is_err());
    f.request(2).unwrap();
    assert!(f.request(1).is_err());
    let other = EntityId::deterministic("render-inventory/other");
    assert!(
        request(
            f.app.world_mut(),
            other,
            f.identity,
            3,
            std::slice::from_ref(&f.mesh),
            std::slice::from_ref(&f.material)
        )
        .is_err()
    );
    assert!(release(f.app.world_mut(), other).is_err());
    assert_eq!(f.app.world().resource::<Request>().owner, f.owner);
    release(f.app.world_mut(), f.owner).unwrap();
    assert!(f.app.world().get_resource::<Request>().is_none());
}
