use super::{
    RenderVegetationPrototype, ResolvedVegetationPrototype, VegetationPrototypeResolveError,
    normalized::prepare_render_prototypes, resolve_vegetation_prototype,
};
use bevy::{
    asset::{AssetPath, LoadState, RecursiveDependencyLoadState},
    gltf::{Gltf, GltfMaterial, GltfMesh, GltfNode},
    prelude::*,
};
use forge_assets::{GovernedAssetKitBuildReport, VegetationPrototypeCatalog};
use forge_project::{VegetationDocument, VegetationKind};
use std::collections::BTreeMap;

/// Loaded data only: `Ready` does not mean scatter projection or GPU presentation.
#[derive(Resource, Clone, Debug, PartialEq, Eq)]
pub enum VegetationPrototypeLoadStatus {
    Loading,
    Ready { unique_glbs: usize, layers: usize },
    Failed(String),
}

#[derive(Resource, Default)]
pub struct VegetationPrototypeCache {
    /// Validated immutable presentation recipe; no generated placement copy.
    pub selection_catalog: Option<VegetationPrototypeCatalog>,
    pub layers: BTreeMap<String, ResolvedVegetationPrototype>,
    /// Derived root-normalized meshes shared per unique source mesh/transform, not per placement.
    pub render_layers: BTreeMap<String, RenderVegetationPrototype>,
    /// Existing Bevy PBR sub-assets, shared across layers and LODs. No texture copies.
    pub pbr_materials: BTreeMap<AssetId<GltfMaterial>, Handle<StandardMaterial>>,
}

/// Bevy 0.19's PBR glTF handler creates this labeled sub-asset during GLB loading.
/// Preserve the source identifier (including a named memory source), not just the filename.
fn pbr_material_path(path: &AssetPath<'_>) -> Result<AssetPath<'static>, String> {
    let label = path
        .label()
        .ok_or("glTF material is missing its sub-asset label")?;
    if label != "DefaultMaterial"
        && !label.strip_prefix("Material").is_some_and(|index| {
            !index.is_empty() && index.bytes().all(|byte| byte.is_ascii_digit())
        })
    {
        return Err(format!("unsupported glTF material label {label}"));
    }
    Ok(path.clone_owned().with_label(format!("{label}/std")))
}

#[derive(Resource, Clone)]
struct LoadPlan {
    catalog: VegetationPrototypeCatalog,
    paths: BTreeMap<String, String>,
}

#[derive(Resource)]
struct PendingLoads {
    handles: BTreeMap<String, Handle<Gltf>>,
    started_seconds: f64,
}

#[derive(Clone)]
pub struct VegetationPrototypeLoadPlugin(LoadPlan);

/// Installs the reusable loader before a user has selected a catalog.
/// No source, file request or prototype is fabricated for an empty authoring zone.
pub struct VegetationPrototypeLoaderPlugin;

impl Plugin for VegetationPrototypeLoaderPlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(VegetationPrototypeLoadStatus::Ready {
            unique_glbs: 0,
            layers: 0,
        })
        .init_resource::<VegetationPrototypeCache>()
        .init_resource::<super::VegetationPrototypeWindCache>()
        .add_systems(
            Update,
            (poll_loads, super::wind::prepare_wind_cache).chain(),
        );
    }
}

impl VegetationPrototypeLoadPlugin {
    /// Release a selected catalog while keeping the idle loader installed.
    pub fn clear(world: &mut World) {
        world.remove_resource::<LoadPlan>();
        world.remove_resource::<PendingLoads>();
        world.insert_resource(VegetationPrototypeCache::default());
        world.insert_resource(super::VegetationPrototypeWindCache::default());
        world.insert_resource(VegetationPrototypeLoadStatus::Ready {
            unique_glbs: 0,
            layers: 0,
        });
    }

    /// Host registers `forge-species` and supplies verified bytes at `<sha256>.glb`.
    /// Content addressing prevents a changed asset from reusing a stale Bevy path handle.
    #[must_use]
    pub fn with_species_memory_source(mut self) -> Self {
        for binding in &self.0.catalog.bindings {
            self.0.paths.insert(
                binding.asset_id.clone(),
                format!("forge-species://{}.glb", binding.glb_sha256),
            );
        }
        self
    }

    /// Replace an installed loader's plan after the host has verified the new GLB bytes.
    /// Keep the last complete cache alive until all new prototypes resolve successfully.
    /// Returns false for the identical pending/accepted plan; does not rebuild per frame.
    pub fn request_reload(self, world: &mut World) -> Result<bool, String> {
        if !world.contains_resource::<VegetationPrototypeCache>() {
            return Err("Vegetation prototype loader is not installed".into());
        }
        if world.get_resource::<LoadPlan>().is_some_and(|existing| {
            existing.catalog == self.0.catalog && existing.paths == self.0.paths
        }) {
            return Ok(false);
        }
        let server = world
            .get_resource::<AssetServer>()
            .ok_or("Asset server is not installed")?;
        let started_seconds = world
            .get_resource::<Time<Real>>()
            .ok_or("Real clock is not installed")?
            .elapsed_secs_f64();
        let handles = self
            .0
            .paths
            .iter()
            .map(|(id, path)| (id.clone(), server.load(path.clone())))
            .collect();
        world.insert_resource(self.0);
        world.insert_resource(PendingLoads {
            handles,
            started_seconds,
        });
        world.insert_resource(VegetationPrototypeLoadStatus::Loading);
        Ok(true)
    }

    /// The host must verify GLB bytes/hashes before installation, using its existing
    /// filesystem or memory-source preflight. This adapter performs no file IO itself.
    pub fn new(
        catalog: VegetationPrototypeCatalog,
        kit: &GovernedAssetKitBuildReport,
        vegetation: &VegetationDocument,
    ) -> Result<Self, String> {
        vegetation.validate().map_err(|error| error.to_string())?;
        let layers: Vec<_> = vegetation
            .layers
            .iter()
            .filter(|layer| layer.kind == VegetationKind::Tree)
            .map(|layer| layer.id.as_str())
            .collect();
        catalog
            .validate_against(
                vegetation
                    .content_hash()
                    .map_err(|error| error.to_string())?,
                &layers,
                kit,
            )
            .map_err(|error| error.to_string())?;
        let paths = kit
            .assets
            .iter()
            .filter(|asset| {
                catalog
                    .bindings
                    .iter()
                    .any(|binding| binding.asset_id == asset.id)
            })
            .map(|asset| (asset.id.clone(), asset.glb.path.clone()))
            .collect();
        Ok(Self(LoadPlan { catalog, paths }))
    }

    #[must_use]
    pub fn with_memory_asset_source(mut self) -> Self {
        for path in self.0.paths.values_mut() {
            if !path.starts_with("forge-kit://") {
                *path = format!("forge-kit://{path}");
            }
        }
        self
    }
}

impl Plugin for VegetationPrototypeLoadPlugin {
    fn build(&self, app: &mut App) {
        if !app.is_plugin_added::<VegetationPrototypeLoaderPlugin>() {
            app.add_plugins(VegetationPrototypeLoaderPlugin);
        }
        app.insert_resource(self.0.clone())
            .insert_resource(VegetationPrototypeLoadStatus::Loading)
            .add_systems(Startup, start_loads);
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn start_loads(
    mut commands: Commands,
    plan: Res<LoadPlan>,
    server: Res<AssetServer>,
    time: Res<Time<Real>>,
) {
    let handles = plan
        .paths
        .iter()
        .map(|(id, path)| (id.clone(), server.load(path.clone())))
        .collect();
    commands.insert_resource(PendingLoads {
        handles,
        started_seconds: time.elapsed_secs_f64(),
    });
}

fn fail(status: &mut VegetationPrototypeLoadStatus, message: String) {
    error!(event_kind = "vegetation_prototype_load_failed", reason = %message);
    *status = VegetationPrototypeLoadStatus::Failed(message);
}

#[derive(bevy::ecs::system::SystemParam)]
struct PrototypeAssets<'w> {
    server: Res<'w, AssetServer>,
    gltfs: Res<'w, Assets<Gltf>>,
    nodes: Res<'w, Assets<GltfNode>>,
    gltf_meshes: Res<'w, Assets<GltfMesh>>,
    meshes: ResMut<'w, Assets<Mesh>>,
    materials: Res<'w, Assets<GltfMaterial>>,
    pbr_materials: Res<'w, Assets<StandardMaterial>>,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn poll_loads(
    plan: Option<Res<LoadPlan>>,
    pending: Option<Res<PendingLoads>>,
    mut assets: PrototypeAssets,
    time: Res<Time<Real>>,
    mut status: ResMut<VegetationPrototypeLoadStatus>,
    mut cache: ResMut<VegetationPrototypeCache>,
) {
    if *status != VegetationPrototypeLoadStatus::Loading {
        return;
    }
    let (Some(plan), Some(pending)) = (plan, pending) else {
        return;
    };
    if time.elapsed_secs_f64() - pending.started_seconds > 120.0 {
        fail(&mut status, "prototype loading exceeded 120 seconds".into());
        return;
    }
    if let Some(error) = load_failure(&pending, &assets.server) {
        fail(&mut status, error);
        return;
    }
    if pending
        .handles
        .values()
        .any(|handle| !assets.server.is_loaded_with_dependencies(handle.id()))
    {
        return;
    }
    let mut resolved = BTreeMap::new();
    for binding in &plan.catalog.bindings {
        let Some(handle) = pending.handles.get(&binding.asset_id) else {
            fail(
                &mut status,
                format!("missing planned prototype {}", binding.asset_id),
            );
            return;
        };
        let Some(gltf) = assets.gltfs.get(handle) else {
            return;
        };
        if !gltf.animations.is_empty() {
            fail(
                &mut status,
                format!("animated prototype {} is unsupported", binding.asset_id),
            );
            return;
        }
        match resolve_vegetation_prototype(
            binding,
            &gltf.nodes,
            &assets.nodes,
            &assets.gltf_meshes,
            &assets.meshes,
            &assets.materials,
        ) {
            Ok(prototype) => {
                resolved.insert(
                    super::prototype_key(plan.catalog.schema_version, binding),
                    prototype,
                );
            }
            Err(VegetationPrototypeResolveError::PendingDependency) => return,
            Err(error) => {
                fail(&mut status, format!("{}: {error}", binding.asset_id));
                return;
            }
        }
    }
    let pbr_materials = match resolve_pbr_handles(&resolved, &assets) {
        Ok(materials) => materials,
        Err(error) => {
            fail(&mut status, error);
            return;
        }
    };
    let (render_layers, normalized_meshes) =
        match prepare_render_prototypes(&resolved, &pbr_materials, &mut assets.meshes) {
            Ok(result) => result,
            Err(error) => {
                fail(&mut status, error);
                return;
            }
        };
    cache.render_layers = render_layers;
    cache.selection_catalog = Some(plan.catalog.clone());
    cache.layers = resolved;
    cache.pbr_materials = pbr_materials;
    *status = VegetationPrototypeLoadStatus::Ready {
        unique_glbs: pending.handles.len(),
        layers: plan
            .catalog
            .bindings
            .iter()
            .map(|binding| &binding.layer_id)
            .collect::<std::collections::BTreeSet<_>>()
            .len(),
    };
    info!(
        event_kind = "vegetation_prototypes_prepared",
        unique_glbs = pending.handles.len(),
        layers = cache.layers.len(),
        shared_pbr_materials = cache.pbr_materials.len(),
        normalized_meshes,
        "prototype cache ready; scatter not replaced"
    );
}

fn load_failure(pending: &PendingLoads, server: &AssetServer) -> Option<String> {
    pending.handles.iter().find_map(|(id, handle)| {
        let failure = match server.get_load_state(handle.id()) {
            Some(LoadState::Failed(error)) => Some(error.to_string()),
            _ => match server.get_recursive_dependency_load_state(handle.id()) {
                Some(RecursiveDependencyLoadState::Failed(error)) => Some(error.to_string()),
                _ => None,
            },
        };
        failure.map(|error| format!("{id}: {error}"))
    })
}

fn resolve_pbr_handles(
    resolved: &BTreeMap<String, ResolvedVegetationPrototype>,
    assets: &PrototypeAssets<'_>,
) -> Result<BTreeMap<AssetId<GltfMaterial>, Handle<StandardMaterial>>, String> {
    let mut pbr_materials = BTreeMap::new();
    for primitive in resolved
        .values()
        .flat_map(|prototype| prototype.lods.iter().flatten())
    {
        if pbr_materials.contains_key(&primitive.material.id()) {
            continue;
        }
        let path = primitive
            .material
            .path()
            .ok_or_else(|| "glTF material has no source path".to_owned())
            .and_then(pbr_material_path);
        let handle = path.and_then(|path| {
            assets
                .server
                .get_handle::<StandardMaterial>(path)
                .filter(|handle| assets.pbr_materials.get(handle).is_some())
                .ok_or_else(|| "loaded GLB has no resident Bevy PBR material".to_owned())
        });
        pbr_materials.insert(primitive.material.id(), handle?);
    }
    Ok(pbr_materials)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pbr_sub_asset_path_preserves_native_and_memory_sources() {
        for (input, expected) in [
            ("trees/oak.glb#Material0", "trees/oak.glb#Material0/std"),
            (
                "forge-kit://oak.glb#Material12",
                "forge-kit://oak.glb#Material12/std",
            ),
            ("oak.glb#DefaultMaterial", "oak.glb#DefaultMaterial/std"),
        ] {
            assert_eq!(
                pbr_material_path(&AssetPath::from(input))
                    .unwrap()
                    .to_string(),
                expected
            );
        }
        for input in [
            "oak.glb",
            "oak.glb#Mesh0",
            "oak.glb#Material",
            "oak.glb#Material0/std",
        ] {
            assert!(pbr_material_path(&AssetPath::from(input)).is_err());
        }
    }
}
