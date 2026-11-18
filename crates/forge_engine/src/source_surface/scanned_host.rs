//! Runtime ownership of scan visuals, cascade casters and authored placement collisions.
use super::{
    SourceSurfaceMaterial, debris_collisions,
    scanned_draws::{self, Instance, SOURCE_CASTER_MARGIN_M, SOURCE_POLICY},
    scanned_images,
    scanned_material::{self, ScannedMaterial, TerrainBindings},
    scanned_meshes::{self, PreparedMeshes},
    scanned_scene::ResidentDraws,
    scanned_shadow,
    scene::{self, WaterBindings},
};
use crate::{
    ManualPlacementCollision, ManualPlacementCollisionExpected,
    ManualPlacementCollisionNotRequired, ManualPlacementCollisionStatus, ManualPlacementProjection,
    source_lighting::surfaces::live::{self, SurfaceHostSet},
    source_shadows::casters::textured::MeshBytes,
    source_terrain::{host::TerrainHostSet, lod::LodMaterial, scene::ResidentTerrain},
};
use bevy::{
    camera::primitives::{Frustum, Sphere},
    prelude::*,
};
use forge_assets::{
    source_debris_collisions::CollisionFrames,
    source_scanned_package::{ScannedPackage, geometry, templates},
};
use forge_core::{AssetId, ContentHash, EntityId};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::{collections::BTreeMap, sync::Arc};

pub struct PreparedHost {
    assets: BTreeMap<AssetId, Instance>,
    collisions: BTreeMap<AssetId, Vec<debris_collisions::PreparedPrimitive>>,
    placements: Vec<ManualPlacement>,
    revision: ContentHash,
    meshes: PreparedMeshes,
    images: [Image; 3],
    shader: String,
    depth_shader: Arc<String>,
    depth_meshes: Arc<Vec<MeshBytes>>,
}

pub struct Resident {
    assets: BTreeMap<AssetId, Instance>,
    placements: Vec<ManualPlacement>,
    revision: ContentHash,
    roots: BTreeMap<EntityId, Entity>,
    draws: ResidentDraws,
    material: Handle<ScannedMaterial>,
    depth_shader: Arc<String>,
    depth_meshes: Arc<Vec<MeshBytes>>,
}

#[derive(SystemSet, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub struct ScanHostSet;

#[derive(Resource, Default)]
pub struct Status {
    pub error: Option<String>,
}

/// Identifies a placement owned by the scanned scene and its admitted collision contract.
#[derive(Component)]
pub struct ScannedPlacement {
    asset: u8,
    pub collision_primitives: usize,
}

impl ScannedPlacement {
    #[must_use]
    pub fn resident_prototypes(&self, world: &World) -> usize {
        match world.get_resource::<Host>() {
            Some(Host::Resident { scene, .. }) => scene.draws.prototype_count(world, self.asset),
            _ => 0,
        }
    }
}

#[derive(Resource)]
enum Host {
    Pending {
        input: Box<PreparedHost>,
        water: bool,
    },
    Resident {
        scene: Box<Resident>,
        water: WaterBindings,
        live_water: bool,
    },
    Failed,
}

#[must_use]
pub fn owns(world: &World, entity: Entity) -> bool {
    match world.get_resource::<Host>() {
        Some(Host::Resident { scene, .. }) => scene.draws.owns(world, entity),
        _ => false,
    }
}

impl PreparedHost {
    /// Prepare admitted source assets using only the consuming area's saved placements.
    pub fn new(
        package: &ScannedPackage,
        geometry: geometry::Geometry,
        collisions: &CollisionFrames,
        shader: String,
        revision: ContentHash,
        placements: Vec<ManualPlacement>,
    ) -> Result<Self, String> {
        PlacementChanges::between(&[], &placements)?;
        if placements.len() > scanned_draws::MAX_PLACEMENTS {
            return Err("Scanned authored placements exceed the renderer budget".into());
        }
        scanned_material::shader_source(&shader)?;
        let owners: BTreeMap<_, _> = geometry
            .instances
            .iter()
            .map(|owner| (templates::asset_id(&owner.source_id), owner))
            .collect();
        let mut assets = BTreeMap::new();
        let mut prepared_collisions = BTreeMap::new();
        for placement in &placements {
            if placement.asset_hash != revision
                || placement.physics.is_some()
                || placement.particles.is_some()
            {
                return Err(
                    "Scanned placement requires its exact revision and built-in collision profile"
                        .into(),
                );
            }
            if assets.contains_key(&placement.asset_id) {
                continue;
            }
            let owner = owners
                .get(&placement.asset_id)
                .ok_or("Scanned variation absent")?;
            let source = &owner.source;
            assets.insert(
                placement.asset_id,
                Instance {
                    asset: source.asset,
                    random_variation: source.rnd,
                    reference_scale: [source.sx, source.sy, source.sz],
                    radius_m: owner.radius_m,
                },
            );
            let collider = collisions
                .owner(&owner.source_id)
                .ok_or("Scanned collider source absent")?;
            prepared_collisions.insert(placement.asset_id, debris_collisions::prepare(collider)?);
        }
        let meshes = scanned_meshes::from_geometry(geometry);
        let depth_meshes = Arc::new(scanned_shadow::geometry(&meshes)?);
        Ok(Self {
            assets,
            collisions: prepared_collisions,
            placements,
            revision,
            meshes,
            images: scanned_images::prepare(package)?,
            shader,
            depth_shader: Arc::new(scanned_shadow::shader_source(package)?),
            depth_meshes,
        })
    }

    #[must_use]
    pub fn placements(&self) -> &[ManualPlacement] {
        &self.placements
    }

    #[must_use]
    pub fn collision_primitive_count(&self, asset: AssetId) -> Option<usize> {
        self.collisions.get(&asset).map(Vec::len)
    }

    /// Adopt the scene through the common terrain, atmosphere and water hosts.
    pub fn install(self, app: &mut App, water: bool) -> Result<(), String> {
        if !live::installed(app.world()) || app.world().contains_resource::<Host>() {
            return Err(
                "Scanned debris requires its exclusive scene host and live source lighting".into(),
            );
        }
        if !app.is_plugin_added::<MaterialPlugin<ScannedMaterial>>() {
            app.add_plugins(MaterialPlugin::<ScannedMaterial>::default());
        }
        app.insert_resource(Host::Pending {
            input: Box::new(self),
            water,
        })
        .init_resource::<Status>()
        .add_systems(
            PostUpdate,
            update
                .in_set(ScanHostSet)
                .after(SurfaceHostSet)
                .after(TerrainHostSet)
                .after(bevy::camera::visibility::VisibilitySystems::UpdateFrusta),
        );
        Ok(())
    }

    /// Publish resources against the resident terrain and lighting of this scene.
    pub fn stage(
        self,
        world: &mut World,
        base: SourceSurfaceMaterial,
        terrain: TerrainBindings,
    ) -> Result<Resident, String> {
        let material =
            scanned_material::stage_material(world, &self.shader, base, terrain, self.images)?;
        let draws = ResidentDraws::stage(
            world,
            self.meshes.prototypes,
            self.placements.len(),
            material.clone(),
        )?;
        let mut roots = BTreeMap::new();
        for placement in &self.placements {
            let root = world
                .spawn((
                    Name::new(placement.name.clone()),
                    crate::placement_transform(placement),
                    Visibility::Inherited,
                    ManualPlacementProjection(placement.clone()),
                ))
                .id();
            let colliders = self.collisions[&placement.asset_id]
                .iter()
                .map(|primitive| {
                    world
                        .spawn((
                            primitive.transform,
                            primitive.collider.clone(),
                            primitive.source.clone(),
                            ManualPlacementCollision(placement.id),
                            ChildOf(root),
                        ))
                        .id()
                })
                .collect::<Vec<_>>();
            if colliders.is_empty() {
                world
                    .entity_mut(root)
                    .insert(ManualPlacementCollisionNotRequired(placement.id));
            }
            world.entity_mut(root).insert((
                ScannedPlacement {
                    asset: self.assets[&placement.asset_id].asset,
                    collision_primitives: colliders.len(),
                },
                ManualPlacementCollisionExpected {
                    placement: placement.id,
                    primitives: colliders,
                },
            ));
            roots.insert(placement.id, root);
        }
        Ok(Resident {
            assets: self.assets,
            placements: self.placements,
            revision: self.revision,
            roots,
            draws,
            material,
            depth_shader: self.depth_shader,
            depth_meshes: self.depth_meshes,
        })
    }
}

impl Resident {
    #[must_use]
    pub fn placements(&self) -> &[ManualPlacement] {
        &self.placements
    }

    /// Publish the matching visible and shadow draws for the active view.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Source bounds are checked before narrowing to the camera ABI."
    )]
    pub fn synchronize(
        &mut self,
        world: &mut World,
        camera: Vec3,
        frustum: &Frustum,
        base: SourceSurfaceMaterial,
    ) -> Result<(), String> {
        let frame = scanned_draws::prepare(
            &self.placements,
            self.revision,
            SOURCE_POLICY,
            camera.to_array().map(f64::from),
            SOURCE_CASTER_MARGIN_M,
            |asset| self.assets.get(&asset).copied(),
            |_, point, radius| {
                frustum.intersects_sphere(
                    &Sphere {
                        center: Vec3::from_array(point.map(|v| v as f32)).into(),
                        radius: radius as f32,
                    },
                    true,
                )
            },
        )?;
        let shadows = scanned_shadow::prepare_frame(
            world,
            &self.depth_shader,
            &self.depth_meshes,
            &self.placements,
            &frame,
            |asset| self.assets.get(&asset).map(|owner| owner.asset),
        )?;
        for &root in self.roots.values() {
            if let Some(ManualPlacementCollisionStatus::Failed(reason)) =
                world.get::<ManualPlacementCollisionStatus>(root)
            {
                return Err(format!("Scanned collider registration failed: {reason}"));
            }
        }
        self.draws.synchronize(world, &frame, &self.roots)?;
        world.insert_resource(shadows);
        let mut materials = world.resource_mut::<Assets<ScannedMaterial>>();
        let current = materials
            .get(&self.material)
            .ok_or("Scanned material disappeared")?;
        let current_base =
            scanned_material::prepare_material(base, current.extension.clone())?.base;
        if current_base != current.base {
            materials
                .get_mut(&self.material)
                .ok_or("Scanned material disappeared")?
                .base = current_base;
        }
        Ok(())
    }
}

fn update(world: &mut World) {
    let Some(host) = world.remove_resource::<Host>() else {
        return;
    };
    match advance(world, host) {
        Ok(host) => world.insert_resource(host),
        Err(error) => {
            world.resource_mut::<Status>().error = Some(error);
            world.insert_resource(Host::Failed);
        }
    }
}

fn advance(world: &mut World, host: Host) -> Result<Host, String> {
    if let Host::Pending { input, water } = host {
        let Some(terrain) = terrain_bindings(world)? else {
            return Ok(Host::Pending { input, water });
        };
        let bindings = if water {
            scene::required_water(world)?
        } else {
            scene::absent_water(world)?
        };
        let base = scene::lighting_material(world, &bindings)?;
        let scene = input.stage(world, base, terrain)?;
        return Ok(Host::Resident {
            scene: Box::new(scene),
            water: bindings,
            live_water: water,
        });
    }
    let Host::Resident {
        scene: mut resident,
        mut water,
        live_water,
    } = host
    else {
        return Ok(Host::Failed);
    };
    let (camera, position) = crate::scene_camera::primary(world)?;
    let frustum = *world
        .get::<Frustum>(camera)
        .ok_or("Scanned scene camera frustum absent")?;
    if live_water {
        water = scene::required_water(world)?;
    }
    let base = scene::lighting_material(world, &water)?;
    resident.synchronize(world, position, &frustum, base)?;
    Ok(Host::Resident {
        scene: resident,
        water,
        live_water,
    })
}

fn terrain_bindings(world: &World) -> Result<Option<TerrainBindings>, String> {
    let Some(handle) = world
        .get_resource::<ResidentTerrain>()
        .and_then(|terrain| terrain.material.as_ref())
    else {
        return Ok(None);
    };
    let material = world
        .resource::<Assets<LodMaterial>>()
        .get(handle)
        .ok_or("Scanned terrain material disappeared")?;
    Ok(Some(TerrainBindings {
        heights: material.extension.height.clone(),
        domain: material.base.settings.domain,
        sun: material.base.settings.sun,
    }))
}
