//! Content adapter: pinned GLB scenes projected from the common saved placements.
use super::village::VillageMaterial;
use bevy::{
    ecs::system::SystemParam,
    gltf::GltfAssetLabel,
    prelude::*,
    world_serialization::{WorldAssetRoot, WorldInstanceReady},
};
use forge_core::{AssetId, ContentHash, bytes_sha256};
use forge_project::zone_authoring::{placements::ManualPlacement, session::ZoneEditSession};
use serde::Deserialize;
use std::{
    collections::{BTreeMap, BTreeSet},
    error::Error,
};
#[path = "image_mips.rs"]
mod image_mips;
#[derive(Resource, Default)]
struct PreparedTextures(std::collections::HashSet<bevy::asset::AssetId<Image>>);

#[derive(Resource, Default)]
struct SceneSurfaces {
    meshes: std::collections::HashMap<bevy::asset::AssetId<Mesh>, Handle<Mesh>>,
    materials:
        std::collections::HashMap<bevy::asset::AssetId<StandardMaterial>, Handle<VillageMaterial>>,
}

#[derive(SystemParam)]
struct MaterializeAssets<'w> {
    meshes: ResMut<'w, Assets<Mesh>>,
    materials: Res<'w, Assets<StandardMaterial>>,
    images: ResMut<'w, Assets<Image>>,
    prepared: ResMut<'w, PreparedTextures>,
    templates: Res<'w, super::village::SharedMaterials>,
    scene_materials: ResMut<'w, Assets<VillageMaterial>>,
    surfaces: ResMut<'w, SceneSurfaces>,
}

// The same source-unit light, sky, shadow and grading path as the scene.
// Keep the verified package immutable; this is a content-adapter shader branch.
pub fn shader_source(source: &str) -> String {
    format!("{source}\n{}", include_str!("static_asset_surface.wgsl"))
}

#[derive(Resource, Default)]
struct SolarCapture {
    frames: u32,
    requested: bool,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Catalog {
    schema_version: u32,
    assets: Vec<Asset>,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Asset {
    id: AssetId,
    hash: ContentHash,
    file: String,
    license: String,
    shadow_positions: String,
    shadow_indices: String,
    // Absence retains this adapter's existing static triangle interpretation.
    #[serde(default, deserialize_with = "declared_collision")]
    collision: Option<StaticCollisionPolicy>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
enum StaticCollisionPolicy {
    StaticTriangleMeshes,
}

fn declared_collision<'de, D: serde::Deserializer<'de>>(
    deserializer: D,
) -> Result<Option<StaticCollisionPolicy>, D::Error> {
    // An explicitly declared null/unknown policy is a refusal, never legacy mode.
    StaticCollisionPolicy::deserialize(deserializer).map(Some)
}
#[derive(Resource)]
pub struct Plan(
    Vec<(ManualPlacement, String)>,
    BTreeMap<AssetId, (Vec<u8>, Vec<u8>)>,
);
impl Plan {
    pub fn shadow_draws(
        &self,
    ) -> Result<Vec<forge_engine::tidewater_object_shadow::OpaqueDraw>, String> {
        self.0
            .iter()
            .map(|(placement, _)| {
                let (positions, indices) = self
                    .1
                    .get(&placement.asset_id)
                    .ok_or("Missing static shadow mesh")?;
                let t = forge_engine::placement_transform(placement);
                forge_engine::tidewater_object_shadow::OpaqueDraw::new(
                    positions.clone(),
                    indices.clone(),
                    t.to_matrix(),
                )
            })
            .collect()
    }
}
#[derive(Resource, Default)]
pub struct Status(pub bool);
#[derive(Resource, Deserialize)]
struct CaptureView {
    position: [f32; 3],
    rotation: [f32; 4],
}
#[derive(Component)]
struct PlacedAsset(ManualPlacement);
#[derive(Component)]
struct Complete(Vec<Entity>);
#[derive(Component)]
struct Failed;

pub fn load(files: &BTreeMap<String, Vec<u8>>) -> Result<Option<Plan>, Box<dyn Error>> {
    let Some(bytes) = files.get("static-assets.json") else {
        return Ok(None);
    };
    let catalog: Catalog = serde_json::from_slice(bytes)?;
    if catalog.schema_version != 1 || catalog.assets.is_empty() || catalog.assets.len() > 64 {
        return Err("Invalid static asset catalog".into());
    }
    let mut seen = BTreeSet::new();
    let mut shadows = BTreeMap::new();
    for asset in &catalog.assets {
        if !seen.insert(asset.id)
            || !std::path::Path::new(&asset.file)
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("glb"))
            || asset.file.contains(['/', '\\', ':'])
            || asset.file.starts_with('.')
            || asset.license.trim().is_empty()
        {
            return Err("Unsafe or duplicate static asset".into());
        }
        let bytes = files
            .get(&asset.file)
            .ok_or("Missing pinned static asset")?;
        if bytes_sha256(bytes) != asset.hash || bytes.get(..4) != Some(b"glTF") {
            return Err("Static asset pin mismatch".into());
        }
        debug!(asset = %asset.id, collision = ?asset.collision,
            "Pinned static asset catalogue collision declaration");
        for file in [&asset.shadow_positions, &asset.shadow_indices] {
            if !std::path::Path::new(file)
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("bin"))
                || file.contains(['/', '\\', ':'])
                || file.starts_with('.')
            {
                return Err("Unsafe static shadow payload".into());
            }
        }
        let positions = files
            .get(&asset.shadow_positions)
            .ok_or("Missing static shadow positions")?
            .clone();
        let indices = files
            .get(&asset.shadow_indices)
            .ok_or("Missing static shadow indices")?
            .clone();
        forge_engine::tidewater_object_shadow::OpaqueDraw::new(
            positions.clone(),
            indices.clone(),
            Mat4::IDENTITY,
        )?;
        shadows.insert(asset.id, (positions, indices));
    }
    let zone = ZoneEditSession::from_archive_bytes(
        files
            .get("canonical-zone.json")
            .ok_or("Static assets require a saved zone")?,
    )?;
    let mut projected = Vec::new();
    for placement in zone
        .head()
        .corrections
        .placements
        .as_ref()
        .ok_or("Missing saved placements")?
    {
        if let Some(asset) = catalog.assets.iter().find(|a| a.id == placement.asset_id) {
            if placement.asset_hash != asset.hash {
                return Err("Stale static placement pin".into());
            }
            placement.transform.validate()?;
            projected.push((placement.clone(), asset.file.clone()));
        }
    }
    if projected.is_empty() {
        return Err("Static asset catalog has no saved instances".into());
    }
    Ok(Some(Plan(projected, shadows)))
}

pub fn install(app: &mut App, plan: Plan) {
    app.insert_resource(plan)
        .init_resource::<Status>()
        .init_resource::<PreparedTextures>()
        .init_resource::<SceneSurfaces>()
        .add_systems(Startup, spawn)
        .add_systems(Update, observe.before(super::capture));
    if std::env::var("FORGE_STATIC_ASSET_SOLAR_CAPTURE").as_deref() == Ok("1") {
        app.init_resource::<SolarCapture>()
            .insert_resource(bevy::winit::WinitSettings::continuous())
            .add_systems(Update, solar_capture);
    }
    // Explicit test input; no camera override is installed by normal gameplay.
    if let Ok(value) = std::env::var("FORGE_STATIC_ASSET_CAPTURE_VIEW") {
        match serde_json::from_str::<CaptureView>(&value) {
            Ok(view)
                if Vec3::from_array(view.position).is_finite()
                    && Quat::from_array(view.rotation).is_finite()
                    && (Quat::from_array(view.rotation).length_squared() - 1.0).abs() < 0.0001 =>
            {
                app.insert_resource(view).add_systems(
                    PostUpdate,
                    capture_view.before(bevy::transform::TransformSystems::Propagate),
                );
            }
            _ => {
                error!("Invalid static asset capture view");
            }
        }
    }
}

fn capture_view(
    view: Res<CaptureView>,
    mut cameras: Query<&mut Transform, With<forge_engine::ForgePlayerCamera>>,
) {
    for mut camera in &mut cameras {
        *camera = Transform::from_translation(Vec3::from_array(view.position))
            .with_rotation(Quat::from_array(view.rotation));
    }
}
fn observe(world: &mut World) {
    let ready = ready(world);
    if ready && !world.resource::<Status>().0 {
        info!(
            instances = world.resource::<Plan>().0.len(),
            "All pinned static placements ready"
        );
    }
    world.resource_mut::<Status>().0 = ready;
}
fn spawn(mut commands: Commands, assets: Res<AssetServer>, plan: Res<Plan>) {
    for (placement, file) in &plan.0 {
        let t = placement.transform;
        commands
            .spawn((
                Name::new(placement.name.clone()),
                PlacedAsset(placement.clone()),
                WorldAssetRoot(assets.load(GltfAssetLabel::Scene(0).from_asset(file.clone()))),
                Transform::from_translation(Vec3::new(
                    t.translation_m.x,
                    t.translation_m.y,
                    t.translation_m.z,
                ))
                .with_rotation(Quat::from_xyzw(
                    t.rotation.x,
                    t.rotation.y,
                    t.rotation.z,
                    t.rotation.w,
                ))
                .with_scale(Vec3::new(t.scale.x, t.scale.y, t.scale.z)),
            ))
            .observe(materialize);
    }
}
fn materialize(
    event: On<WorldInstanceReady>,
    mut commands: Commands,
    children: Query<&Children>,
    roots: Query<&PlacedAsset>,
    parts: Query<(&Mesh3d, &MeshMaterial3d<StandardMaterial>)>,
    assets: MaterializeAssets,
) {
    let MaterializeAssets {
        mut meshes,
        materials,
        mut images,
        mut prepared,
        templates,
        mut scene_materials,
        mut surfaces,
    } = assets;
    let Ok(root) = roots.get(event.entity) else {
        return;
    };
    let mut colliders = Vec::new();
    for entity in children.iter_descendants(event.entity) {
        let Ok((handle, material)) = parts.get(entity) else {
            continue;
        };
        let material_id = material.0.id();
        let Some(material) = materials.get(&material.0) else {
            commands.entity(event.entity).insert(Failed);
            return;
        };
        for (texture, normal) in [
            (&material.base_color_texture, false),
            (&material.normal_map_texture, true),
            (&material.metallic_roughness_texture, false),
            (&material.occlusion_texture, false),
        ] {
            if let Some(texture) = texture
                && prepared.0.insert(texture.id())
            {
                let Some(mut image) = images.get_mut(texture) else {
                    commands.entity(event.entity).insert(Failed);
                    return;
                };
                if let Err(reason) = image_mips::prepare(&mut image, normal) {
                    error!(reason, "Static asset texture admission failed");
                    commands.entity(event.entity).insert(Failed);
                    return;
                }
            }
        }
        let Some(mesh) = meshes.get(&handle.0) else {
            commands.entity(event.entity).insert(Failed);
            return;
        };
        let Ok((collider, _)) = forge_engine::tidewater_assets::scene::surface(mesh) else {
            error!("Static asset collider failed: {}", root.0.id);
            commands.entity(event.entity).insert(Failed);
            return;
        };
        // Glass retains its blend pass. Opaque glTF surfaces use the scene's
        // complete lighting receiver rather than a physical-light approximation.
        if matches!(material.alpha_mode, AlphaMode::Opaque | AlphaMode::Mask(_)) {
            let rendered_mesh = if let Some(cached) = surfaces.meshes.get(&handle.0.id()) {
                cached.clone()
            } else {
                let mesh = scene_mesh(mesh);
                let rendered = meshes.add(mesh);
                surfaces.meshes.insert(handle.0.id(), rendered.clone());
                rendered
            };
            let rendered_material = if let Some(cached) = surfaces.materials.get(&material_id) {
                cached.clone()
            } else {
                let Some(template) = templates.0.first().and_then(|h| scene_materials.get(h))
                else {
                    error!("Static asset scene material template unavailable");
                    commands.entity(event.entity).insert(Failed);
                    return;
                };
                let scene = scene_material(material, template);
                let rendered = scene_materials.add(scene);
                surfaces.materials.insert(material_id, rendered.clone());
                rendered
            };
            commands
                .entity(entity)
                .remove::<MeshMaterial3d<StandardMaterial>>()
                .insert((Mesh3d(rendered_mesh), MeshMaterial3d(rendered_material)));
        }
        commands
            .entity(entity)
            .insert((collider, forge_engine::ManualPlacementPart(root.0.id)));
        colliders.push(entity);
    }
    if colliders.is_empty() {
        commands.entity(event.entity).insert(Failed);
        return;
    }
    info!(placement=%root.0.id,meshes=colliders.len(),"Pinned static asset instantiated with triangle collisions");
    commands.entity(event.entity).insert(Complete(colliders));
}

fn scene_mesh(source: &Mesh) -> Mesh {
    let mut mesh = source.clone();
    let count = mesh.count_vertices();
    let tint = match mesh.attribute(Mesh::ATTRIBUTE_COLOR) {
        Some(bevy::mesh::VertexAttributeValues::Float32x4(values)) => {
            values.iter().map(|v| [v[0], v[1], v[2]]).collect()
        }
        _ => vec![[1.0; 3]; count],
    };
    mesh.insert_attribute(forge_engine::tidewater_mesh::TINT, tint);
    mesh.insert_attribute(forge_engine::tidewater_mesh::DATA, vec![[0.0; 4]; count]);
    mesh
}

fn scene_material(material: &StandardMaterial, template: &VillageMaterial) -> VillageMaterial {
    let mut scene = template.clone();
    scene.kind = 7;
    scene.shared_scene_lighting = true;
    scene.settings.color = Vec4::from_array(material.base_color.to_linear().to_f32_array());
    scene.settings.emissive = Vec4::new(
        material.emissive.red,
        material.emissive.green,
        material.emissive.blue,
        1.0,
    );
    let cutoff = match material.alpha_mode {
        AlphaMode::Mask(v) => v,
        _ => 0.0,
    };
    scene.settings.surface = Vec4::new(
        material.perceptual_roughness,
        material.metallic,
        cutoff,
        1.0,
    );
    let textures = [
        &material.base_color_texture,
        &material.normal_map_texture,
        &material.metallic_roughness_texture,
        &material.occlusion_texture,
    ];
    let mut flags = 0u8;
    for (i, texture) in textures.iter().enumerate() {
        if texture.is_some() {
            flags |= 1 << i;
        }
    }
    scene.settings.flags.w = f32::from(flags);
    scene.first = material.base_color_texture.clone().unwrap_or(scene.first);
    scene.second = material.normal_map_texture.clone().unwrap_or(scene.second);
    scene.third = material
        .metallic_roughness_texture
        .clone()
        .unwrap_or(scene.third);
    scene.fourth = material.occlusion_texture.clone().unwrap_or(scene.fourth);
    scene
}

pub fn ready(world: &mut World) -> bool {
    let Some(plan) = world.get_resource::<Plan>() else {
        return true;
    };
    let expected = plan.0.len();
    let physics = world.contains_resource::<forge_engine::PlayerSimulationReady>();
    let mut roots = world.query::<(&PlacedAsset, Option<&Complete>, Option<&Failed>)>();
    let count = roots.iter(world).count();
    count == expected
        && roots.iter(world).all(|(_, complete, failed)| {
            failed.is_none()
                && complete.is_some_and(|done| {
                    !done.0.is_empty()
                        && done.0.iter().all(|&part| {
                            world
                                .get::<bevy_rapier3d::prelude::Collider>(part)
                                .is_some()
                                && (!physics
                                    || world
                                        .get::<bevy_rapier3d::prelude::RapierColliderHandle>(part)
                                        .is_some())
                        })
                })
        })
}

// Opt-in native evidence only: wait for the requested atmosphere readback,
// the registered colliders and exposure settling before requesting a screenshot.
fn solar_capture(world: &mut World) {
    if !ready(world) || !super::walk::escape_menu::solar_ready(world) {
        return;
    }
    let mut proof = world.resource_mut::<SolarCapture>();
    proof.frames += 1;
    if proof.frames < 240 || proof.requested {
        return;
    }
    proof.requested = true;
    let path = world.resource::<super::Probe>().screenshot.clone();
    let frame = super::walk::escape_menu::solar_frame(world).expect("ready solar frame");
    let count = world.resource::<SceneSurfaces>().materials.len();
    let receipt = serde_json::json!({"hours": frame.hours, "completed_solar_readback": true,
        "scene_materials": count, "static_instances": world.resource::<Plan>().0.len(),
        "registered_colliders": true, "sun": frame.sun.as_vec3().to_array()});
    if let Err(reason) = std::fs::write(
        path.with_extension("solar.json"),
        serde_json::to_vec_pretty(&receipt).expect("solar receipt"),
    ) {
        error!(%reason, "Solar capture receipt failed");
        world.write_message(bevy::app::AppExit::error());
        return;
    }
    world
        .spawn(bevy::render::view::screenshot::Screenshot::primary_window())
        .observe(bevy::render::view::screenshot::save_to_disk(path))
        .observe(
            |_: On<bevy::render::view::screenshot::ScreenshotCaptured>,
             mut exit: MessageWriter<bevy::app::AppExit>| {
                exit.write(bevy::app::AppExit::Success);
            },
        );
}

#[cfg(test)]
mod catalog_policy_tests {
    use super::{Catalog, StaticCollisionPolicy};
    use serde_json::{Value, json};

    fn catalog(collision: Option<Value>) -> Value {
        let mut asset = json!({
            "id": forge_core::AssetId::deterministic("generic-static-module/model"),
            "hash": forge_core::bytes_sha256(b"pinned model fixture"),
            "file": "model.glb",
            "license": "fixture-license",
            "shadow_positions": "positions.bin",
            "shadow_indices": "indices.bin"
        });
        if let Some(collision) = collision {
            asset["collision"] = collision;
        }
        json!({"schema_version": 1, "assets": [asset]})
    }

    #[test]
    fn absent_collision_keeps_the_legacy_catalogue_contract() {
        let decoded: Catalog = serde_json::from_value(catalog(None)).unwrap();
        assert_eq!(decoded.schema_version, 1);
        assert_eq!(decoded.assets.len(), 1);
        assert_eq!(decoded.assets[0].collision, None);
    }

    #[test]
    fn explicit_static_triangle_policy_is_decoded_without_changing_asset_pins() {
        let value = catalog(Some(json!("static_triangle_meshes")));
        let decoded: Catalog = serde_json::from_value(value).unwrap();
        let ordinary: Catalog = serde_json::from_value(catalog(None)).unwrap();
        assert_eq!(
            decoded.assets[0].collision,
            Some(StaticCollisionPolicy::StaticTriangleMeshes)
        );
        assert_eq!(decoded.assets[0].id, ordinary.assets[0].id);
        assert_eq!(decoded.assets[0].hash, ordinary.assets[0].hash);
        assert_eq!(decoded.assets[0].file, ordinary.assets[0].file);
    }

    #[test]
    fn explicit_null_collision_is_not_an_absent_policy() {
        assert!(serde_json::from_value::<Catalog>(catalog(Some(Value::Null))).is_err());
    }

    #[test]
    fn unknown_collision_names_types_case_and_fields_are_refused() {
        for policy in [
            json!("convex"),
            json!("STATIC_TRIANGLE_MESHES"),
            json!({}),
            json!(true),
        ] {
            assert!(serde_json::from_value::<Catalog>(catalog(Some(policy))).is_err());
        }
        let mut value = catalog(None);
        value["assets"][0]["collision_policy"] = json!("static_triangle_meshes");
        assert!(serde_json::from_value::<Catalog>(value).is_err());
    }
}
