//! Shared model presentation over corrected imported plant identities.
//!
//! Hosts supply streaming selections and verified model caches. Publication,
//! renderer-resource readiness and source replacement remain explicit; hosts
//! must also admit shadows and collisions before enabling gameplay.
use forge_assets::{VegetationPrototypeBinding, VegetationPrototypeCatalog};
use forge_core::{ContentHash, EntityId};
use forge_project::{VegetationDocument, VegetationKind};
use forge_world::imported_vegetation::{ImportedEntity, streaming::PlantView};
use std::collections::{BTreeMap, BTreeSet};
use std::sync::Arc;

use bevy::camera::{primitives::Aabb, visibility::NoAutoAabb};
use bevy::prelude::*;
mod audit;

struct PreparedPrimitive {
    model: super::WindVegetationPrimitive,
    bounds: Aabb,
    triangles: usize,
}

fn mesh_triangles(meshes: &Assets<Mesh>, handle: &Handle<Mesh>) -> Result<usize, String> {
    let mesh = meshes.get(handle).ok_or("Species mesh absent")?;
    Ok(mesh
        .indices()
        .map_or_else(|| mesh.count_vertices(), bevy::mesh::Indices::len)
        / 3)
}

struct PreparedTree {
    transform: Transform,
    tag: bevy::mesh::MeshTag,
    prototype: Arc<PreparedPrototype>,
    deformation_extra: f32,
}

impl PreparedTree {
    fn bounds(&self, bounds: Aabb) -> Aabb {
        if self.deformation_extra > 0.0 {
            let extra = Vec3::splat(self.deformation_extra);
            Aabb::from_min_max(
                Vec3::from(bounds.min()) - extra,
                Vec3::from(bounds.max()) + extra,
            )
        } else {
            bounds
        }
    }
}

struct PreparedPrototype {
    lods: [Vec<PreparedPrimitive>; 3],
}

fn forward_recipes<'a>(
    prototypes: impl Iterator<Item = &'a PreparedPrototype>,
    ranges: &crate::vegetation::VegetationLodRanges,
) -> Arc<Vec<crate::source_shadows::casters::governed::ForwardRecipe>> {
    let mut forward = BTreeMap::new();
    for prototype in prototypes {
        for (lod, primitives) in prototype.lods.iter().enumerate() {
            let range = [&ranges.near, &ranges.medium, &ranges.hlod][lod];
            let crossfade = !range.start_margin.is_empty() || !range.end_margin.is_empty();
            for primitive in primitives {
                let model = &primitive.model;
                forward
                    .entry((model.mesh.id(), model.material.id(), crossfade))
                    .or_insert_with(|| crate::source_shadows::casters::governed::ForwardRecipe {
                        mesh: model.mesh.clone(),
                        material: model.material.clone().untyped(),
                        material_type: std::any::TypeId::of::<crate::VisualValleyVegetationMaterial>(),
                        crossfade,
                    });
            }
        }
    }
    Arc::new(forward.into_values().collect())
}

struct ShadowSelection {
    camera: [f32; 3],
    packets: Vec<(
        Handle<crate::vegetation::VisualValleyVegetationMaterial>,
        crate::source_shadows::casters::governed::Packet,
        f32,
    )>,
}

#[derive(Component, PartialEq, Eq)]
struct OwnedPrimitive {
    owner: EntityId,
    head: ContentHash,
    tree: EntityId,
    lod: u8,
    primitive: usize,
}

/// Derived model presentation owned by one host. The host supplies its admitted
/// visible identities and LODs; this adapter generates no scatter or collisions.
/// Publication here is CPU-only and grants no GPU or gameplay readiness.
pub struct ImportedTreePresentation {
    audit: std::sync::Mutex<Option<audit::Audit>>,
    owner: EntityId,
    head: ContentHash,
    identity: ContentHash,
    generation: u64,
    publication_frame: Option<u32>,
    requested_generation: Option<u64>,
    visibility: Visibility,
    trees: BTreeMap<EntityId, PreparedTree>,
    prototypes: Vec<(EntityId, Arc<PreparedPrototype>)>,
    ranges: crate::vegetation::VegetationLodRanges,
    entities: BTreeMap<(EntityId, u8, usize), Entity>,
    shadows: Arc<crate::source_shadows::casters::governed::Receipt>,
    shadow_selection: std::sync::Mutex<Option<ShadowSelection>>,
    forward: Arc<Vec<crate::source_shadows::casters::governed::ForwardRecipe>>,
    simulation_admission: std::sync::Mutex<Option<SimulationResourceAdmission>>,
}

struct SimulationResourceAdmission {
    owner: EntityId,
    head: ContentHash,
    identity: ContentHash,
    generation: u64,
    bootstrap_frame: u32,
    token: crate::render_inventory::RenderInventoryToken,
    context: crate::render_inventory::GraphicsContextReceipt,
    forward: crate::source_shadows::casters::governed::ForwardResourcesReceipt,
}

/// Independent readiness results observed under one immutable world borrow.
/// This value is an observation, not a cached admission or a renderer token.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[allow(
    clippy::struct_excessive_bools,
    reason = "Independent integrity, GPU publication, shadows and simulation resource observations"
)]
pub struct ImportedTreeReadiness {
    pub intact: bool,
    pub gpu_ready: bool,
    pub shadows_ready: bool,
    pub simulation_resources_ready: bool,
}

impl ImportedTreePresentation {
    pub fn prepare(
        owner: EntityId,
        head: ContentHash,
        bindings: &[ImportedTreeBinding<'_>],
        cache: &super::VegetationPrototypeWindCache,
        vegetation: &VegetationDocument,
        meshes: &Assets<Mesh>,
    ) -> Result<Self, String> {
        if owner.as_uuid().is_nil() {
            return Err("Imported tree presentation requires an owner".into());
        }
        vegetation.validate().map_err(|error| error.to_string())?;
        let vegetation_hash = vegetation
            .content_hash()
            .map_err(|error| error.to_string())?;
        if cache
            .selection_catalog
            .as_ref()
            .is_none_or(|catalog| catalog.vegetation_sha256 != vegetation_hash)
        {
            return Err("Imported tree presentation catalog differs from its vegetation".into());
        }
        let profile = vegetation.presentation;
        let margin = Vec3::splat(crate::vegetation::maximum_deformation_m(profile));
        let mut bounds = crate::vegetation::ProjectionBounds::new(meshes);
        let mut trees = BTreeMap::new();
        let mut prototypes: BTreeMap<(&str, &str), (EntityId, Arc<PreparedPrototype>)> =
            BTreeMap::new();
        for binding in bindings {
            let bytes = binding.entity.source.source_key.as_bytes();
            let phase = f32::from(u16::from_le_bytes([bytes[0], bytes[1]])) / f32::from(u16::MAX)
                * std::f32::consts::TAU;
            // Verify every assignment against the live pinned catalog, even
            // when its immutable geometry recipe is already shared.
            let rendered = binding.render_lods(cache)?;
            let key = (
                binding.prototype.layer_id.as_str(),
                binding.prototype.asset_id.as_str(),
            );
            let prototype = if let Some((_, prototype)) = prototypes.get(&key) {
                Arc::clone(prototype)
            } else {
                let mut lods: [Vec<PreparedPrimitive>; 3] = std::array::from_fn(|_| Vec::new());
                for (target, source) in lods.iter_mut().zip(rendered) {
                    for primitive in source {
                        let source_bounds = bounds.get(primitive.mesh.id())?;
                        target.push(PreparedPrimitive {
                            model: primitive.clone(),
                            triangles: mesh_triangles(meshes, &primitive.mesh)?,
                            bounds: Aabb::from_min_max(
                                Vec3::from(source_bounds.min()) - margin,
                                Vec3::from(source_bounds.max()) + margin,
                            ),
                        });
                    }
                }
                let prototype = Arc::new(PreparedPrototype { lods });
                prototypes.insert(key, (binding.entity.id, Arc::clone(&prototype)));
                prototype
            };
            let transform = binding.render_transform()?;
            // Wind is displaced in world XZ by max(scale.x, scale.z), even for
            // a tilted tree. A local isotropic margin must survive the inverse
            // nonuniform scale; uniform instances retain the original bound.
            let deformation_extra = if margin.x > 0.0 {
                margin.x
                    * (transform.scale.x.max(transform.scale.z) / transform.scale.min_element()
                        - 1.0)
                        .max(0.0)
            } else {
                0.0
            };
            if !deformation_extra.is_finite() {
                return Err("Imported tree deformation bound overflows the render adapter".into());
            }
            let tree = PreparedTree {
                transform,
                tag: crate::vegetation::mesh_tag_for(phase, 1.0),
                prototype,
                deformation_extra,
            };
            if trees.insert(binding.entity.id, tree).is_some() {
                return Err("Duplicate imported tree presentation identity".into());
            }
        }
        let ranges = crate::vegetation::VegetationLodRanges::from_governed_profile(profile);
        let forward = forward_recipes(
            prototypes.values().map(|(_, prototype)| prototype.as_ref()),
            &ranges,
        );
        Ok(Self {
            audit: std::sync::Mutex::default(),
            owner,
            head,
            identity: forge_core::canonical_json_sha256(&(head, &cache.selection_catalog))
                .map_err(|error| error.to_string())?,
            generation: 0,
            publication_frame: None,
            requested_generation: None,
            visibility: Visibility::Visible,
            trees,
            prototypes: prototypes.into_values().collect(),
            ranges,
            entities: BTreeMap::new(),
            shadows: Arc::default(),
            shadow_selection: std::sync::Mutex::default(),
            forward,
            simulation_admission: std::sync::Mutex::default(),
        })
    }

    /// Validate the complete host selection before changing any owned entity.
    /// Repeated publication reuses entities; changing LOD retires old primitives.
    pub fn select(
        &mut self,
        world: &mut World,
        expected_head: ContentHash,
        selected: &[(EntityId, u8)],
    ) -> Result<(), String> {
        let _profile = crate::cpu_timing::Span::new("species.select");
        if expected_head != self.head {
            return Err("Imported tree presentation head changed".into());
        }
        let mut ids = BTreeSet::new();
        let mut draws = Vec::new();
        for &(id, lod) in selected {
            if !ids.insert((id, lod)) || lod > 2 {
                return Err("Duplicate imported tree selection or invalid LOD".into());
            }
            let tree = self
                .trees
                .get(&id)
                .ok_or("Unknown imported tree selection")?;
            for (index, primitive) in tree.prototype.lods[usize::from(lod)].iter().enumerate() {
                draws.push((
                    (id, lod, index),
                    primitive.model.clone(),
                    tree.bounds(primitive.bounds),
                    tree.transform,
                    tree.tag.clone(),
                ));
            }
            if draws.len() as u64 > crate::MAX_VEGETATION_PROJECTION_ENTITIES {
                return Err("Imported tree selection exceeds the engine draw budget".into());
            }
        }
        if !self.is_intact(world) {
            return Err("Imported tree presentation changed outside its owner".into());
        }
        let desired: BTreeSet<_> = draws.iter().map(|(key, ..)| *key).collect();
        if self.publication_frame.is_some() && desired.iter().eq(self.entities.keys()) {
            return Ok(());
        }
        if self.audit.get_mut().expect("species audit lock").is_none() {
            *self.audit.get_mut().expect("species audit lock") = Some(audit::Audit::new(world));
        }
        // Immutable meshes/materials retain their inventory generation across
        // camera selections. Visibility changes require a new completed frame.
        let next = self.generation.max(1);
        let publication_frame = world
            .get_resource::<bevy::diagnostic::FrameCount>()
            .map_or(0, |frame| frame.0)
            .checked_add(1)
            .ok_or("Imported tree publication frame overflow")?;
        let mut previous = std::mem::take(&mut self.entities);
        for (key @ (tree, lod, primitive), model, bounds, transform, tag) in draws {
            if let Some(entity) = previous.remove(&key) {
                // As in the source plant resident, an audited retained key
                // already has its immutable pose, ranges and projected material.
                // Preserve the receiver and its component change ticks.
                self.entities.insert(key, entity);
                continue;
            }
            let entity = world.spawn_empty().id();
            let mut target = world.entity_mut(entity);
            target
                .remove::<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>()
                .remove::<crate::source_lighting::surfaces::host::Origin>();
            target.insert((
                Mesh3d(model.mesh),
                MeshMaterial3d(model.material),
                transform,
                tag,
                self.visibility,
                NoAutoAabb,
                bounds,
                match lod {
                    0 => self.ranges.near.clone(),
                    1 => self.ranges.medium.clone(),
                    _ => self.ranges.hlod.clone(),
                },
                OwnedPrimitive {
                    owner: self.owner,
                    head: self.head,
                    tree,
                    lod,
                    primitive,
                },
                crate::source_vegetation::scene::ImportedPlant {
                    id: tree,
                    head_hash: Some(self.head),
                },
            ));
            if lod == 2 {
                target.insert(bevy::light::NotShadowCaster);
            } else {
                target.remove::<bevy::light::NotShadowCaster>();
            }
            self.entities.insert(key, entity);
        }
        for entity in previous.into_values() {
            world.despawn(entity);
        }
        self.generation = next;
        self.publication_frame = Some(publication_frame);
        Ok(())
    }

    /// Prepare an initially hidden candidate while the source remains live.
    /// Hidden geometry is not proof of pipeline warming; the GPU barrier may
    /// remain pending until the host's renderer preparation is complete.
    pub fn stage_hidden(
        &mut self,
        world: &mut World,
        expected_head: ContentHash,
        selected: &[(EntityId, u8)],
    ) -> Result<(), String> {
        if self.publication_frame.is_some() {
            return Err("Imported tree candidate is already staged".into());
        }
        self.visibility = Visibility::Hidden;
        self.select(world, expected_head, selected)
    }

    /// Once candidate resources are GPU-admitted, atomically publish its models
    /// and retire their source primitives. The host must still gate gameplay on
    /// a subsequent `gpu_ready` and its independent physics/shadow barriers.
    /// A pending GPU receipt leaves the visible source untouched.
    pub fn commit_source_replacement<M: crate::source_vegetation::world::SourceMaterial>(
        &mut self,
        world: &mut World,
        source: &mut crate::source_vegetation::scene::resident::Resident<M>,
        expected_head: ContentHash,
        view: crate::source_vegetation::scene::View,
    ) -> Result<bool, String> {
        if expected_head != self.head
            || self.visibility != Visibility::Hidden
            || view.visibility != Visibility::Visible
        {
            return Err("Imported tree replacement is stale or already visible".into());
        }
        if source.head_hash() != Some(self.head) || !source.is_intact(world) {
            return Err("Source plant presentation changed before replacement".into());
        }
        let replaced = self.trees.keys().copied().collect();
        let selection = source.selection_indices_excluding(view.camera, &replaced)?;
        let Some(publication_frame) = self.prepare_replacement(world, view.camera)? else {
            return Ok(false);
        };
        source.select_indices(world, &selection, view.visibility)?;
        self.reveal(world, publication_frame);
        Ok(true)
    }

    /// The streamed host retains its admitted selection, ownership audit and
    /// primary inventory. Replacement changes only its visual source indices.
    pub fn commit_streamed_replacement<M: crate::source_vegetation::world::SourceMaterial>(
        &mut self,
        world: &mut World,
        source: &mut crate::source_vegetation::scene::streamed::Resident<M>,
        admitted: &crate::source_vegetation::scene::streamed::Admission,
        camera: Vec3,
        token: Option<&crate::render_inventory::RenderInventoryToken>,
    ) -> Result<bool, String> {
        if self.visibility != Visibility::Hidden
            || source.head_hash() != self.head
            || !source.projection_intact(world)
        {
            return Err("Streamed plant replacement is stale or already visible".into());
        }
        let selection =
            source.select_excluding(admitted, camera, &self.replacement_identities())?;
        let Some(publication_frame) = self.prepare_replacement(world, camera)? else {
            return Ok(false);
        };
        if token.is_none() && source.entity_count() != 0 {
            return Ok(false);
        }
        source.publish(world, selection, token)?;
        self.reveal(world, publication_frame);
        Ok(true)
    }

    fn prepare_replacement(
        &mut self,
        world: &mut World,
        camera: Vec3,
    ) -> Result<Option<u32>, String> {
        // Representatives warm recipes; the actual view must also have a
        // completed hidden publication before any source entity is retired.
        let selected = self.selection_for_camera(camera)?;
        self.select(world, self.head, &selected)?;
        self.request_gpu(world)?;
        if !self.gpu_ready(world) || !self.source_shadows_ready(world) {
            return Ok(None);
        }
        world
            .get_resource::<bevy::diagnostic::FrameCount>()
            .map_or(0, |frame| frame.0)
            .checked_add(1)
            .map(Some)
            .ok_or_else(|| "Imported tree publication frame overflow".into())
    }

    fn reveal(&mut self, world: &mut World, publication_frame: u32) {
        for &entity in self.entities.values() {
            world.entity_mut(entity).insert(Visibility::Visible);
        }
        self.visibility = Visibility::Visible;
        self.publication_frame = Some(publication_frame);
    }

    /// Register immutable model resources in the existing scoped GPU barrier.
    /// The owning host must warm all specializations through the real renderer.
    pub fn request_gpu(&mut self, world: &mut World) -> Result<(), String> {
        if self.publication_frame.is_none() || !self.is_intact(world) {
            return Err("Imported tree inventory requires an intact published selection".into());
        }
        if self.requested_generation == Some(self.generation) {
            if self.is_empty() || crate::render_inventory::scoped_sources_current(world, self.owner)
            {
                return Ok(());
            }
            // Source receivers can acquire their final environment images or
            // resize scene buffers after the models have staged. Retain the
            // models, but require a new inventory and its real renderer fence.
            self.generation = self
                .generation
                .checked_add(1)
                .ok_or("Imported tree inventory generation overflow")?;
        }
        let (meshes, materials) = self.handles();
        if !self.is_empty() {
            if crate::source_lighting::surfaces::host::vegetation_lighting_installed(world) {
                let Some(materials) = crate::source_lighting::surfaces::host::vegetation_materials(
                    world, &materials,
                )?
                else {
                    return Ok(());
                };
                crate::render_inventory::request_scoped(
                    world,
                    self.owner,
                    self.identity,
                    self.generation,
                    &meshes,
                    &materials,
                )?;
            } else {
                crate::render_inventory::request_scoped(
                    world,
                    self.owner,
                    self.identity,
                    self.generation,
                    &meshes,
                    &materials,
                )?;
            }
        }
        if self
            .requested_generation
            .is_some_and(|old| old != self.generation)
        {
            info!(
                event_kind = "imported_species_inventory_renewed",
                generation = self.generation,
                "Imported species renderer dependencies changed; waiting for a new GPU receipt"
            );
        }
        self.requested_generation = Some(self.generation);
        Ok(())
    }

    /// CPU entities alone are insufficient: require a current renderer token
    /// whose queue completion covers this exact visibility publication.
    pub fn gpu_ready(&self, world: &World) -> bool {
        let _profile = crate::cpu_timing::Span::new("species.gpu_ready");
        self.gpu_ready_after(world, 0)
    }

    /// Observe integrity once, then reuse that local CPU result while the same
    /// immutable world borrow protects all three independent readiness checks.
    /// Every call revalidates the sources, renderer receipts and their epochs.
    #[must_use]
    pub fn readiness(&self, world: &World, expected_head: ContentHash) -> ImportedTreeReadiness {
        let intact = self.is_intact(world);
        let gpu_ready = self.gpu_ready_after_observed(world, 0, intact);
        let shadows_ready = self.source_shadows_ready_observed(world, intact);
        let simulation_resources_ready = self.simulation_resources_ready_observed(
            world,
            expected_head,
            intact,
            Some((gpu_ready, shadows_ready)),
        );
        ImportedTreeReadiness {
            intact,
            gpu_ready,
            shadows_ready,
            simulation_resources_ready,
        }
    }

    /// Admission of already completed immutable resources only. This never
    /// certifies the latest visibility publication, pixels, or scene physics.
    /// The host must independently verify its owned collision/backend services.
    /// Bootstrap requires the first fully admitted VISIBLE selection; any stale
    /// source, request, device, view or specialization revokes that admission.
    #[must_use]
    pub fn simulation_resources_ready(&self, world: &World, expected_head: ContentHash) -> bool {
        let intact = self.is_intact(world);
        self.simulation_resources_ready_observed(world, expected_head, intact, None)
    }

    // `intact` and optional strict results belong only to this immutable-world
    // observation. They must never be stored or reused by a later observation.
    fn simulation_resources_ready_observed(
        &self,
        world: &World,
        expected_head: ContentHash,
        intact: bool,
        strict: Option<(bool, bool)>,
    ) -> bool {
        use crate::source_shadows::casters::{governed, graph};
        let Ok(mut admitted) = self.simulation_admission.lock() else {
            return false;
        };
        if expected_head != self.head
            || self.visibility != Visibility::Visible
            || self.is_empty()
            || self.requested_generation != Some(self.generation)
            || !intact
            || !crate::render_inventory::scoped_sources_current(world, self.owner)
            || self.shadows.error().is_some()
        {
            *admitted = None;
            return false;
        }
        // Keep the same shadow owner and actual resident target. This checks
        // resource admission, without certifying the new visibility's encoding.
        let shadow_resources = world.get_resource::<graph::Target>().is_some_and(|target| {
            (target.settings.enabled <= 0.5
                || target.ready.load(std::sync::atomic::Ordering::Acquire))
                && world
                    .get_resource::<governed::Frame>()
                    .is_some_and(|frame| {
                        Arc::ptr_eq(&frame.receipt, &self.shadows)
                            && frame.target == target.image
                            && world
                                .get_resource::<bevy::diagnostic::FrameCount>()
                                .is_some_and(|stamp| {
                                    stamp.0 == frame.stamp
                                        || frame.stamp.checked_add(1) == Some(stamp.0)
                                })
                    })
        });
        if !shadow_resources {
            *admitted = None;
            return false;
        }
        if admitted.as_ref().is_some_and(|old| {
            old.owner == self.owner
                && old.head == self.head
                && old.identity == self.identity
                && old.generation == self.generation
                && old
                    .token
                    .is_current(world, self.owner, self.identity, self.generation)
                && old.token.covers_frame(world, old.bootstrap_frame)
                && old.context.is_current(world)
                && self
                    .shadows
                    .forward_resources_current(world, &old.context, &old.forward)
        }) {
            return true;
        }
        *admitted = None;
        // The bootstrap is intentionally strict. In particular a reset cannot
        // relatch from CPU handles or from a receipt predating this publication.
        let (gpu_ready, shadows_ready) = strict.unwrap_or_else(|| {
            (
                self.gpu_ready_after_observed(world, 0, intact),
                self.source_shadows_ready_observed(world, intact),
            )
        });
        if !gpu_ready || !shadows_ready {
            return false;
        }
        let Some(token) = crate::render_inventory::acquire_scoped(
            world,
            self.owner,
            self.identity,
            self.generation,
        ) else {
            return false;
        };
        let Some(context) = token.graphics_context(world) else {
            return false;
        };
        let Some(forward) = self.shadows.forward_resources(world, &context) else {
            return false;
        };
        let Some(bootstrap_frame) = self.publication_frame else {
            return false;
        };
        *admitted = Some(SimulationResourceAdmission {
            owner: self.owner,
            head: self.head,
            identity: self.identity,
            generation: self.generation,
            bootstrap_frame,
            token,
            context,
            forward,
        });
        true
    }

    fn gpu_ready_after(&self, world: &World, required: u32) -> bool {
        let intact = self.publication_frame.is_some() && self.is_intact(world);
        self.gpu_ready_after_observed(world, required, intact)
    }

    // Requires the integrity result freshly obtained under this same &World.
    fn gpu_ready_after_observed(&self, world: &World, required: u32, intact: bool) -> bool {
        self.publication_frame.is_some_and(|frame| {
            intact
                && if self.is_empty() {
                    // No model geometry needs a renderer inventory. The host
                    // still admits its source scene, frame and physics; never
                    // fabricate a model generation or GPU receipt for emptiness.
                    self.requested_generation == Some(self.generation)
                        && world
                            .get_resource::<bevy::diagnostic::FrameCount>()
                            .is_some_and(|stamp| stamp.0 >= frame.max(required))
                } else {
                    crate::render_inventory::acquire_scoped(
                        world,
                        self.owner,
                        self.identity,
                        self.generation,
                    )
                    .is_some_and(|token| token.covers_frame(world, frame.max(required)))
                }
        })
    }

    /// Prepare the actual normalized near-model casters on the existing source
    /// cascade graph. Hidden candidates warm resources without adding shadows
    /// to the still-visible source. No scatter or collision is generated.
    pub fn publish_source_shadows(
        &self,
        world: &mut World,
        expected_head: ContentHash,
        camera: Vec3,
    ) -> Result<bool, String> {
        use crate::source_shadows::casters::{governed, graph};
        let _profile = crate::cpu_timing::Span::new("species.shadow_frame");
        if let Some(error) = self.shadows.error() {
            return Err(error);
        }
        if expected_head != self.head || !camera.is_finite() || !self.is_intact(world) {
            return Err("Imported species shadow source is stale".into());
        }
        let Some(target) = world.get_resource::<graph::Target>() else {
            return Ok(false);
        };
        let shadow_enabled = target.settings.enabled > 0.5;
        let target = target.image.clone();
        self.shadows.bind_target(target.id());
        let (view_entity, _) = crate::scene_camera::primary(world)?;
        if world
            .get_resource::<governed::Frame>()
            .is_some_and(|frame| !Arc::ptr_eq(&frame.receipt, &self.shadows))
        {
            return Err("Imported species shadow graph already has a different owner".into());
        }
        let times = crate::vegetation::vegetation_frame_times(world.resource::<Time>());
        let materials = world
            .get_resource::<Assets<crate::vegetation::VisualValleyVegetationMaterial>>()
            .ok_or("Imported species shadow materials are absent")?;
        let packets = if shadow_enabled {
            self.shadow_packets(materials, camera, times)?
        } else {
            Vec::new()
        };
        let stamp = world
            .get_resource::<bevy::diagnostic::FrameCount>()
            .ok_or("Imported species shadow frame is absent")?
            .0;
        stamp
            .checked_add(2)
            .ok_or("Imported species shadow frame overflow")?;
        let forward =
            if crate::source_lighting::surfaces::host::vegetation_lighting_installed(world) {
                let originals: Vec<_> = self
                    .forward
                    .iter()
                    .map(|recipe| {
                        recipe
                            .material
                            .clone()
                            .typed::<crate::VisualValleyVegetationMaterial>()
                    })
                    .collect();
                let Some(materials) = crate::source_lighting::surfaces::host::vegetation_materials(
                    world, &originals,
                )?
                else {
                    return Ok(false);
                };
                Arc::new(
                    self.forward
                        .iter()
                        .zip(materials)
                        .map(|(recipe, material)| governed::ForwardRecipe {
                            mesh: recipe.mesh.clone(),
                            material: material.untyped(),
                            material_type: std::any::TypeId::of::<
                                crate::source_lighting::surfaces::VegetationMaterial,
                            >(),
                            crossfade: recipe.crossfade,
                        })
                        .collect(),
                )
            } else {
                self.forward.clone()
            };
        world.insert_resource(governed::Frame {
            target,
            stamp,
            enabled: shadow_enabled && self.visibility == Visibility::Visible,
            packets,
            receipt: self.shadows.clone(),
            camera: Some(view_entity),
            forward,
        });
        Ok(true)
    }

    fn shadow_packets(
        &self,
        materials: &Assets<crate::vegetation::VisualValleyVegetationMaterial>,
        camera: Vec3,
        times: [f32; 2],
    ) -> Result<Vec<crate::source_shadows::casters::governed::Packet>, String> {
        use crate::source_shadows::casters::governed;
        let mut cached = self
            .shadow_selection
            .lock()
            .map_err(|_| "Imported species shadow selection lock failed")?;
        let refresh = cached.as_ref().is_none_or(|cached| {
            crate::source_vegetation_selection::needs_refresh(
                Some(cached.camera),
                camera.to_array(),
            ) || cached.packets.iter().any(|(handle, _, cutoff)| {
                materials.get(handle).is_none_or(|material| {
                    material.extension.shadow_fade_distances.y.to_bits() != cutoff.to_bits()
                })
            })
        });
        if refresh {
            let mut packets = BTreeMap::new();
            for tree in self.trees.values() {
                for primitive in &tree.prototype.lods[0] {
                    let model = &primitive.model;
                    let material = materials
                        .get(&model.material)
                        .ok_or("Imported species shadow material disappeared")?;
                    let key = (model.mesh.id(), model.material.id());
                    if let std::collections::btree_map::Entry::Vacant(entry) = packets.entry(key) {
                        entry.insert((
                            model.material.clone(),
                            governed::Packet::new(model.mesh.clone(), material, camera)?,
                            material.extension.shadow_fade_distances.y,
                        ));
                    }
                    let (_, packet, cutoff) =
                        packets.get_mut(&key).expect("inserted shadow recipe");
                    packet.capacity += 1;
                    // The shared one-metre refresh policy needs that same
                    // conservative margin; the original GPU fade stays exact.
                    if camera.distance(tree.transform.translation) < *cutoff + 1.0 {
                        Arc::make_mut(&mut packet.instances).push(governed::Instance {
                            model: tree.transform.to_matrix(),
                            tag: tree.tag.0,
                            pad0: 0,
                            pad1: 0,
                            pad2: 0,
                        });
                        let bounds = tree.bounds(primitive.bounds);
                        // Retain a small conservative margin at clip planes;
                        // the existing bounds already include maximum deformation.
                        let bounds = Aabb::from_min_max(
                            Vec3::from(bounds.min()) - Vec3::splat(0.001),
                            Vec3::from(bounds.max()) + Vec3::splat(0.001),
                        );
                        Arc::make_mut(packet.bounds.get_or_insert_with(Arc::default)).push(bounds);
                    }
                }
            }
            if packets
                .values()
                .map(|(_, packet, _)| packet.capacity as u64)
                .sum::<u64>()
                > crate::MAX_VEGETATION_PROJECTION_ENTITIES
            {
                return Err(
                    "Imported species shadow instances exceed the engine projection budget".into(),
                );
            }
            *cached = Some(ShadowSelection {
                camera: camera.to_array(),
                packets: packets.into_values().collect(),
            });
        }
        cached
            .as_ref()
            .expect("prepared shadow selection")
            .packets
            .iter()
            .map(|(handle, packet, _)| {
                let mut packet = packet.clone();
                packet.refresh(
                    materials
                        .get(handle)
                        .ok_or("Imported species shadow material disappeared")?,
                    camera,
                )?;
                packet.set_frame_times(times);
                Ok(packet)
            })
            .collect()
    }

    /// Preparation is distinct from encoding, and both remain distinct from
    /// queue completion. Visible publication also requires the scoped GPU fence.
    #[must_use]
    pub fn source_shadows_ready(&self, world: &World) -> bool {
        let intact = self.is_intact(world);
        self.source_shadows_ready_observed(world, intact)
    }

    // Hidden candidates retain their existing shadow preparation semantics;
    // visible candidates also require the locally audited GPU publication.
    fn source_shadows_ready_observed(&self, world: &World, intact: bool) -> bool {
        use crate::source_shadows::casters::{governed, graph};
        if self.shadows.error().is_some() {
            return false;
        }
        let Some(target) = world.get_resource::<graph::Target>() else {
            return false;
        };
        let Some(frame) = world.get_resource::<governed::Frame>().filter(|frame| {
            Arc::ptr_eq(&frame.receipt, &self.shadows)
                && frame.target == target.image
                && world
                    .get_resource::<bevy::diagnostic::FrameCount>()
                    .is_some_and(|stamp| {
                        stamp.0 == frame.stamp || frame.stamp.checked_add(1) == Some(stamp.0)
                    })
        }) else {
            return false;
        };
        if !self.publication_frame.is_some_and(|required| {
            self.shadows
                .forward_prepared_frame()
                .is_some_and(|prepared| prepared >= required)
        }) {
            return false;
        }
        if target.settings.enabled <= 0.5 {
            return self.visibility == Visibility::Hidden
                || self.gpu_ready_after_observed(world, 0, intact);
        }
        if !target.ready.load(std::sync::atomic::Ordering::Acquire) {
            return false;
        }
        self.publication_frame.is_some_and(|required| {
            self.shadows
                .prepared_frame()
                .is_some_and(|prepared| prepared >= required)
                && (self.visibility == Visibility::Hidden
                    || frame.enabled
                        && self
                            .shadows
                            .encoded_frame()
                            .is_some_and(|encoded| encoded >= required)
                        && self.shadows.first_encoded_frame().is_some_and(|first| {
                            self.gpu_ready_after_observed(world, first, intact)
                        }))
        })
    }

    fn handles(
        &self,
    ) -> (
        Vec<Handle<Mesh>>,
        Vec<Handle<crate::vegetation::VisualValleyVegetationMaterial>>,
    ) {
        let mut meshes = BTreeMap::new();
        let mut materials = BTreeMap::new();
        for primitive in self
            .prototypes
            .iter()
            .flat_map(|(_, prototype)| prototype.lods.iter().flatten())
        {
            meshes
                .entry(primitive.model.mesh.id())
                .or_insert_with(|| primitive.model.mesh.clone());
            materials
                .entry(primitive.model.material.id())
                .or_insert_with(|| primitive.model.material.clone());
        }
        (
            meshes.into_values().collect(),
            materials.into_values().collect(),
        )
    }

    fn is_intact(&self, world: &World) -> bool {
        let _profile = crate::cpu_timing::Span::new("species.ownership_audit");
        self.audit
            .lock()
            .is_ok_and(|mut audit| match audit.as_mut() {
                Some(audit) => audit.intact(world, self),
                None => self.entities.is_empty(),
            })
    }

    fn complete_intact(&self, world: &World) -> bool {
        self.entities.iter().all(|(&(tree, lod, index), &entity)| {
            let prepared = &self.trees[&tree];
            let primitive = &prepared.prototype.lods[usize::from(lod)][index];
            let bounds_expected = prepared.bounds(primitive.bounds);
            let range = match lod {
                0 => &self.ranges.near,
                1 => &self.ranges.medium,
                _ => &self.ranges.hlod,
            };
            world.get::<OwnedPrimitive>(entity)
                == Some(&OwnedPrimitive {
                    owner: self.owner,
                    head: self.head,
                    tree,
                    lod,
                    primitive: index,
                })
                && world
                    .get::<Mesh3d>(entity)
                    .is_some_and(|mesh| mesh.0 == primitive.model.mesh)
                && crate::source_lighting::surfaces::host::matches_vegetation(
                    world,
                    entity,
                    &primitive.model.material,
                )
                && world.get::<Transform>(entity) == Some(&prepared.transform)
                && world
                    .get::<bevy::mesh::MeshTag>(entity)
                    .is_some_and(|tag| tag.0 == prepared.tag.0)
                && world.get::<Aabb>(entity).is_some_and(|bounds| {
                    bounds.min() == bounds_expected.min() && bounds.max() == bounds_expected.max()
                })
                && world
                    .get::<bevy::camera::visibility::VisibilityRange>(entity)
                    .is_some_and(|actual| {
                        actual.start_margin == range.start_margin
                            && actual.end_margin == range.end_margin
                            && actual.use_aabb == range.use_aabb
                    })
                && world.get::<NoAutoAabb>(entity).is_some()
                && world.get::<Visibility>(entity) == Some(&self.visibility)
                && world.get::<ChildOf>(entity).is_none()
                && world
                    .get::<bevy::camera::visibility::NoFrustumCulling>(entity)
                    .is_none()
                && world.get::<bevy::light::NotShadowCaster>(entity).is_some() == (lod == 2)
                && world
                    .get::<crate::source_vegetation::scene::ImportedPlant>(entity)
                    .is_some_and(|plant| plant.id == tree && plant.head_hash == Some(self.head))
        })
    }

    #[must_use]
    pub fn entity_count(&self) -> usize {
        self.entities.len()
    }

    /// The entire corrected assignment is empty, not just outside this camera.
    #[must_use]
    pub fn is_empty(&self) -> bool {
        self.trees.is_empty()
    }

    #[must_use]
    pub fn is_visible(&self) -> bool {
        self.visibility == Visibility::Visible
    }

    /// Borrow the actual admitted primitives for the shared canonical ID pass.
    /// Geometry, material coverage, wind tags and corrected poses stay identical
    /// to the visible presentation; a pending or altered projection cannot pick.
    pub fn picking_draws(
        &self,
        world: &World,
        expected_head: ContentHash,
    ) -> Result<Vec<crate::source_vegetation::picking::governed::Draw>, String> {
        use crate::source_vegetation::picking::governed::Draw;
        use crate::vegetation::VisualValleyVegetationMaterial;
        if self.head != expected_head
            || !self.is_visible()
            || !self.gpu_ready(world)
            || !self.source_shadows_ready(world)
        {
            return Err("Model picking requires the current admitted presentation".into());
        }
        let materials = world
            .get_resource::<Assets<VisualValleyVegetationMaterial>>()
            .ok_or("Model picking materials unavailable")?;
        let mut copies = BTreeMap::new();
        self.entities
            .iter()
            .map(|(&(tree, lod, index), &entity)| {
                let prepared = &self.trees[&tree];
                let primitive = &prepared.prototype.lods[usize::from(lod)][index];
                let material_id = primitive.model.material.id();
                if let std::collections::btree_map::Entry::Vacant(entry) = copies.entry(material_id)
                {
                    entry.insert(Arc::new(
                        materials
                            .get(material_id)
                            .ok_or("Model picking material disappeared")?
                            .clone(),
                    ));
                }
                let range =
                    [&self.ranges.near, &self.ranges.medium, &self.ranges.hlod][usize::from(lod)];
                Ok(Draw {
                    entity,
                    id: tree,
                    pick_id: 0,
                    mesh: primitive.model.mesh.clone(),
                    material_id,
                    material: Arc::clone(&copies[&material_id]),
                    tag: prepared.tag.0,
                    transform: prepared.transform,
                    bounds: prepared.bounds(primitive.bounds),
                    range: range.clone(),
                })
            })
            .collect()
    }

    #[must_use]
    pub fn replacement_identities(&self) -> BTreeSet<EntityId> {
        self.trees.keys().copied().collect()
    }

    /// Conservative world radii of the actual normalized LODs and their wind
    /// bounds, centered on the rendered canonical pivot. No scatter is sampled.
    #[must_use]
    pub fn streaming_radii(&self) -> BTreeMap<EntityId, f64> {
        self.trees
            .iter()
            .map(|(&id, tree)| {
                let radius = tree
                    .prototype
                    .lods
                    .iter()
                    .flatten()
                    .map(|primitive| {
                        let bound = tree.bounds(primitive.bounds);
                        Vec3::from(bound.min())
                            .abs()
                            .max(Vec3::from(bound.max()).abs())
                            .as_dvec3()
                            .length()
                    })
                    .fold(0.0_f64, f64::max)
                    * f64::from(tree.transform.scale.max_element());
                (
                    id,
                    (radius * (1.0 + 512.0 * f64::from(f32::EPSILON)) + 0.001).next_up(),
                )
            })
            .collect()
    }

    #[must_use]
    pub fn selected_triangles(&self) -> usize {
        self.entities
            .keys()
            .map(|&(id, lod, primitive)| {
                self.trees[&id].prototype.lods[usize::from(lod)][primitive].triangles
            })
            .sum()
    }

    #[must_use]
    pub fn gpu_evidence<'a>(
        &self,
        world: &'a World,
    ) -> Option<&'a crate::render_inventory::RenderInventoryEvidence> {
        if self.is_empty() {
            return None;
        }
        crate::render_inventory::scoped_evidence(world, self.owner)
    }

    /// Select existing corrected identities using Bevy's same governed range
    /// predicate, including both LODs throughout their transition. This creates
    /// no population; streamed hosts further intersect with their admitted IDs.
    pub fn selection_for_camera(&self, camera: Vec3) -> Result<Vec<(EntityId, u8)>, String> {
        if !camera.is_finite() {
            return Err("Imported tree selection requires a finite camera".into());
        }
        let ranges = [&self.ranges.near, &self.ranges.medium, &self.ranges.hlod];
        let mut selected = Vec::new();
        for (&id, tree) in &self.trees {
            let distance = camera.distance(tree.transform.translation);
            for (lod, range) in ranges.iter().enumerate() {
                if range.is_visible_at_all(distance) {
                    selected.push((id, u8::try_from(lod).expect("three tree LODs")));
                }
            }
        }
        Ok(selected)
    }

    /// One representative of each distinct mesh/material recipe per LOD.
    /// Staging resource preparation never expands the full canonical population.
    #[must_use]
    pub fn staging_selection(&self) -> Vec<(EntityId, u8)> {
        let mut seen = BTreeSet::new();
        let mut selected = Vec::new();
        for (id, prototype) in &self.prototypes {
            for (lod, primitives) in prototype.lods.iter().enumerate() {
                let recipe: Vec<_> = primitives
                    .iter()
                    .map(|primitive| (primitive.model.mesh.id(), primitive.model.material.id()))
                    .collect();
                // PreparedTree always has exactly three levels.
                let lod = u8::try_from(lod).expect("three prepared tree LODs");
                if seen.insert((lod, recipe)) {
                    selected.push((*id, lod));
                }
            }
        }
        selected
    }

    /// Retire only entities whose private ownership marker still matches.
    pub fn retire(self, world: &mut World) {
        if world
            .get_resource::<crate::source_shadows::casters::governed::Frame>()
            .is_some_and(|frame| Arc::ptr_eq(&frame.receipt, &self.shadows))
        {
            world.remove_resource::<crate::source_shadows::casters::governed::Frame>();
        }
        if let Some(generation) = self.requested_generation {
            crate::render_inventory::release_scoped_matching(
                world,
                self.owner,
                self.identity,
                generation,
            );
        }
        for ((tree, lod, primitive), entity) in self.entities {
            if world.get::<OwnedPrimitive>(entity)
                == Some(&OwnedPrimitive {
                    owner: self.owner,
                    head: self.head,
                    tree,
                    lod,
                    primitive,
                })
            {
                world.despawn(entity);
            }
        }
    }
}

/// A borrowed corrected instance and its selected model. The full imported
/// quaternion and nonuniform scale remain on `entity.source`; prototype pivot
/// and size normalization are owned by the existing prototype loader.
pub struct ImportedTreeBinding<'a> {
    pub layer_id: &'a str,
    pub entity: &'a ImportedEntity,
    pub prototype: &'a VegetationPrototypeBinding,
}

impl ImportedTreeBinding<'_> {
    /// Borrow the existing normalized wind meshes, retaining all three LODs.
    /// A reload must not reuse this assignment under a changed model or pivot.
    pub fn render_lods<'a>(
        &self,
        cache: &'a super::VegetationPrototypeWindCache,
    ) -> Result<&'a [Vec<super::WindVegetationPrimitive>; 3], String> {
        if let Some(error) = &cache.failure {
            return Err(error.clone());
        }
        let catalog = cache
            .selection_catalog
            .as_ref()
            .ok_or("Imported tree models are not loaded")?;
        let selected = catalog
            .select_binding(self.layer_id, self.entity.id)
            .map_err(|error| error.to_string())?
            .ok_or("Imported tree layer is absent from the loaded catalog")?;
        if selected != self.prototype {
            return Err("Imported tree model changed after its assignment".into());
        }
        let lods = cache.for_placement(self.layer_id, self.entity.id)?;
        if lods.iter().any(Vec::is_empty) {
            return Err("Imported tree model has an incomplete LOD chain".into());
        }
        Ok(lods)
    }

    /// The loader already baked pivot and size normalization into the meshes.
    /// Apply the complete corrected instance pose once, with no yaw reduction.
    pub fn render_transform(&self) -> Result<bevy::prelude::Transform, String> {
        use bevy::math::{DQuat, DVec3};
        let pose = &self.entity.source;
        let rotation = DQuat::from_array(pose.rotation_xyzw);
        if self.entity.id.as_uuid().is_nil()
            || pose
                .position_m
                .iter()
                .chain(&pose.rotation_xyzw)
                .chain(&pose.scale)
                .any(|value| !value.is_finite())
            || (rotation.length_squared() - 1.0).abs() > 1e-9
            || pose
                .scale
                .iter()
                .any(|value| *value <= 0.0 || *value > 10_000.0)
        {
            return Err("Invalid imported tree render pose".into());
        }
        let transform = bevy::prelude::Transform {
            translation: DVec3::from_array(pose.position_m).as_vec3(),
            rotation: rotation.as_quat().normalize(),
            scale: DVec3::from_array(pose.scale).as_vec3(),
        };
        if !transform.translation.is_finite()
            || !transform.scale.is_finite()
            || transform.scale.min_element() <= 0.0
        {
            return Err("Imported tree pose overflows the render adapter".into());
        }
        Ok(transform)
    }
}

/// Resolve choices against an already validated catalog and the exact authored
/// terrain view. This neither spawns instances nor modifies the source view.
pub fn bind_imported_trees<'a>(
    view: &'a (impl PlantView + ?Sized),
    expected_head: ContentHash,
    vegetation: &VegetationDocument,
    catalog: &'a VegetationPrototypeCatalog,
) -> Result<Vec<ImportedTreeBinding<'a>>, String> {
    if view.head_hash() != expected_head {
        return Err("Imported tree view differs from its authored head".into());
    }
    if catalog.vegetation_sha256 != vegetation.content_hash().map_err(|e| e.to_string())? {
        return Err("Imported tree catalog differs from its vegetation document".into());
    }
    let tree_layers: BTreeSet<_> = vegetation
        .layers
        .iter()
        .filter(|layer| layer.kind == VegetationKind::Tree)
        .map(|layer| layer.id.as_str())
        .collect();
    if catalog
        .bindings
        .iter()
        .any(|binding| !tree_layers.contains(binding.layer_id.as_str()))
    {
        return Err("Imported tree catalog references a non-tree layer".into());
    }
    let mut layers = BTreeSet::new();
    let mut ids = BTreeSet::<EntityId>::new();
    let mut result = Vec::new();
    for (layer_id, instances) in view.layers() {
        if !layers.insert(layer_id)
            || !vegetation
                .layers
                .iter()
                .any(|source| source.id.as_str() == layer_id)
        {
            return Err("Imported tree view has a duplicate or unknown layer".into());
        }
        for entity in instances {
            if !ids.insert(entity.id) {
                return Err("Imported tree view has a duplicate identity".into());
            }
            if !tree_layers.contains(layer_id) {
                continue;
            }
            if let Some(prototype) = catalog
                .select_binding(layer_id, entity.id)
                .map_err(|e| e.to_string())?
            {
                result.push(ImportedTreeBinding {
                    layer_id,
                    entity,
                    prototype,
                });
            }
        }
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::imported_vegetation::ImportedVegetationInstance;
    use forge_world::imported_vegetation::{ImportedTerrainLayer, ImportedTerrainView};

    fn assert_world_wind_enclosed(bounds: Aabb, transform: Transform, displacement: f32) {
        let min = Vec3::from(bounds.min());
        let max = Vec3::from(bounds.max());
        let model = transform.to_matrix();
        let mut world_min = Vec3::splat(f32::INFINITY);
        let mut world_max = Vec3::splat(f32::NEG_INFINITY);
        for x in [min.x, max.x] {
            for y in [min.y, max.y] {
                for z in [min.z, max.z] {
                    let point = model.transform_point3(Vec3::new(x, y, z));
                    world_min = world_min.min(point);
                    world_max = world_max.max(point);
                }
            }
        }
        assert!(displacement > 0.0);
        // Original 2x6x4 cuboid corners, displaced along either world wind axis.
        for x in [-1.0, 1.0] {
            for y in [-3.0, 3.0] {
                for z in [-2.0, 2.0] {
                    let base = model.transform_point3(Vec3::new(x, y, z));
                    for direction in [Vec3::X, Vec3::NEG_X, Vec3::Z, Vec3::NEG_Z] {
                        let point = base + direction * displacement;
                        assert!(
                            point.cmpge(world_min - Vec3::splat(0.0001)).all()
                                && point.cmple(world_max + Vec3::splat(0.0001)).all(),
                            "{point:?} outside {world_min:?}..{world_max:?}"
                        );
                    }
                }
            }
        }
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "One admitted-tree lifecycle preserves full pose, shared recipes, hidden staging and LOD transitions in order."
    )]
    fn selected_model_keeps_corrected_identity_and_complete_pose() {
        let vegetation = VegetationDocument::from_json(include_str!(
            "../../../../examples/visual_valley/vegetation.v1.json"
        ))
        .unwrap();
        let layer = vegetation
            .layers
            .iter()
            .find(|l| l.kind == VegetationKind::Tree)
            .unwrap();
        let head = ContentHash::from_bytes([7; 32]);
        let entity = ImportedEntity {
            id: EntityId::deterministic("imported-corrected-tree"),
            source: ImportedVegetationInstance {
                source_key: ContentHash::from_bytes([4; 32]),
                position_m: [12.0, 3.0, -9.0],
                rotation_xyzw: [0.5; 4],
                scale: [1.0, 2.0, 3.0],
                parameters: vec![0.25],
            },
        };
        let mut copy = entity.clone();
        copy.id = EntityId::deterministic("second-imported-corrected-tree");
        copy.source.position_m[0] += 4.0;
        let view = ImportedTerrainView {
            head_hash: head,
            excluded_count: 1,
            density_removed_count: 2,
            moved_count: 1,
            placed_count: 0,
            layers: vec![ImportedTerrainLayer {
                layer_id: layer.id.as_str(),
                instances: vec![entity.clone(), copy.clone()],
            }],
        };
        let catalog = VegetationPrototypeCatalog {
            schema: "forge.vegetation-prototype-catalog".into(),
            schema_version: 2,
            vegetation_sha256: vegetation.content_hash().unwrap(),
            asset_kit_id: "qualification".into(),
            asset_kit_revision: 1,
            bindings: vec![VegetationPrototypeBinding {
                layer_id: layer.id.to_string(),
                asset_id: "tree".into(),
                glb_sha256: ContentHash::from_bytes([9; 32]),
                source_pivot_m: [0.0; 3],
                source_height_m: 2.0,
                target_height_m: 3.0,
            }],
        };
        let bindings = bind_imported_trees(&view, head, &vegetation, &catalog).unwrap();
        assert_eq!(bindings.len(), 2);
        assert_eq!(bindings[0].entity, &entity);
        assert_eq!(bindings[0].prototype.asset_id, "tree");
        let snapshot = ImportedTerrainView {
            head_hash: head,
            excluded_count: 1,
            density_removed_count: 2,
            moved_count: 1,
            placed_count: 0,
            layers: vec![ImportedTerrainLayer {
                layer_id: layer.id.as_str(),
                instances: vec![entity.clone(), copy.clone()],
            }],
        }
        .into_snapshot();
        let frozen = bind_imported_trees(&snapshot, head, &vegetation, &catalog).unwrap();
        assert_eq!(frozen.len(), bindings.len());
        for (frozen, borrowed) in frozen.iter().zip(&bindings) {
            assert_eq!(frozen.entity, borrowed.entity);
            assert_eq!(frozen.prototype, borrowed.prototype);
        }
        let transform = bindings[0].render_transform().unwrap();
        assert_eq!(
            transform.translation.to_array().map(f32::to_bits),
            [12.0_f32, 3.0, -9.0].map(f32::to_bits)
        );
        assert_eq!(
            transform.rotation.to_array().map(f32::to_bits),
            [0.5_f32; 4].map(f32::to_bits)
        );
        assert_eq!(
            transform.scale.to_array().map(f32::to_bits),
            [1.0_f32, 2.0, 3.0].map(f32::to_bits)
        );
        let key = super::super::prototype_key(catalog.schema_version, &catalog.bindings[0]);
        let mut meshes = Assets::<Mesh>::default();
        let primitive = super::super::WindVegetationPrimitive {
            mesh: meshes.add(Mesh::from(Cuboid::new(2.0, 6.0, 4.0))),
            material: bevy::prelude::Handle::default(),
        };
        let mut cache = super::super::VegetationPrototypeWindCache {
            selection_catalog: Some(catalog.clone()),
            layers: [(key, std::array::from_fn(|_| vec![primitive.clone()]))].into(),
            failure: None,
        };
        assert_eq!(bindings[0].render_lods(&cache).unwrap()[0][0], primitive);
        let mut presentation = ImportedTreePresentation::prepare(
            EntityId::deterministic("imported-presentation-owner"),
            head,
            &bindings,
            &cache,
            &vegetation,
            &meshes,
        )
        .unwrap();
        assert_eq!(presentation.prototypes.len(), 1);
        assert!(Arc::ptr_eq(
            &presentation.trees[&entity.id].prototype,
            &presentation.trees[&copy.id].prototype
        ));
        assert_ne!(
            presentation.trees[&entity.id].transform,
            presentation.trees[&copy.id].transform
        );
        let mut candidate = ImportedTreePresentation::prepare(
            EntityId::deterministic("hidden-imported-owner"),
            head,
            &bindings,
            &cache,
            &vegetation,
            &meshes,
        )
        .unwrap();
        let mut staging_world = World::new();
        let warm = candidate.staging_selection();
        candidate
            .stage_hidden(&mut staging_world, head, &warm)
            .unwrap();
        assert!(candidate.entities.values().all(|&entity| staging_world.get::<Visibility>(entity) == Some(&Visibility::Hidden)));
        assert!(candidate.is_intact(&staging_world));
        assert!(!candidate.gpu_ready(&staging_world));
        assert!(!candidate.simulation_resources_ready(&staging_world, head));
        let hidden_readiness = candidate.readiness(&staging_world, head);
        assert!(hidden_readiness.intact);
        assert!(!hidden_readiness.gpu_ready);
        assert!(!hidden_readiness.shadows_ready);
        assert!(!hidden_readiness.simulation_resources_ready);
        assert!(candidate.simulation_admission.lock().unwrap().is_none());
        let full = candidate
            .selection_for_camera(transform.translation)
            .unwrap();
        assert!(full.iter().any(|&(id, lod)| id == entity.id && lod == 0));
        assert!(full.iter().any(|&(id, _)| id == copy.id));
        assert!(
            candidate
                .selection_for_camera(Vec3::splat(f32::NAN))
                .is_err()
        );
        let transition = candidate
            .ranges
            .near
            .end_margin
            .start
            .midpoint(candidate.ranges.near.end_margin.end);
        let overlap = candidate
            .selection_for_camera(transform.translation + Vec3::X * transition)
            .unwrap();
        assert!(overlap.contains(&(entity.id, 0)) && overlap.contains(&(entity.id, 1)));
        candidate.select(&mut staging_world, head, &full).unwrap();
        assert!(candidate.entities.keys().any(|(id, _, _)| *id == copy.id));
        assert!(candidate.entities.values().all(|&entity| staging_world.get::<Visibility>(entity) == Some(&Visibility::Hidden)));
        assert!(!candidate.gpu_ready(&staging_world));
        // Renderer dependencies can finish after hidden model staging. A
        // resized buffer or adopted texture must revoke the old inventory,
        // without rebuilding the authored models or manufacturing GPU proof.
        staging_world.insert_resource(std::mem::take(&mut meshes));
        staging_world.init_resource::<Assets<bevy::render::storage::ShaderBuffer>>();
        staging_world.init_resource::<Assets<crate::VisualValleyVegetationMaterial>>();
        let buffer = staging_world
            .resource_mut::<Assets<bevy::render::storage::ShaderBuffer>>()
            .add(bevy::render::storage::ShaderBuffer::from(vec![Vec4::ZERO]));
        let material = crate::VisualValleyVegetationMaterial {
            extension: crate::vegetation::VisualValleyVegetationExtension {
                temporal_lod_buffer: buffer.clone(),
                ..default()
            },
            ..default()
        };
        let material_id = Handle::<crate::VisualValleyVegetationMaterial>::default().id();
        staging_world
            .resource_mut::<Assets<crate::VisualValleyVegetationMaterial>>()
            .insert(material_id, material)
            .unwrap();
        candidate.request_gpu(&mut staging_world).unwrap();
        let generation = candidate.generation;
        let entities = candidate.entities.clone();
        candidate.request_gpu(&mut staging_world).unwrap();
        assert_eq!(candidate.generation, generation);
        assert!(crate::render_inventory::scoped_sources_current(
            &staging_world,
            candidate.owner
        ));
        staging_world
            .resource_mut::<Assets<bevy::render::storage::ShaderBuffer>>()
            .get_mut(&buffer)
            .unwrap()
            .set_data(vec![Vec4::ONE; 2]);
        assert!(!crate::render_inventory::scoped_sources_current(
            &staging_world,
            candidate.owner
        ));
        candidate.request_gpu(&mut staging_world).unwrap();
        assert_eq!(candidate.generation, generation + 1);
        staging_world.init_resource::<Assets<Image>>();
        let image = staging_world
            .resource_mut::<Assets<Image>>()
            .add(Image::default());
        staging_world
            .resource_mut::<Assets<crate::VisualValleyVegetationMaterial>>()
            .get_mut(material_id)
            .unwrap()
            .base
            .base_color_texture = Some(image);
        assert!(!crate::render_inventory::scoped_sources_current(
            &staging_world,
            candidate.owner
        ));
        candidate.request_gpu(&mut staging_world).unwrap();
        assert_eq!(candidate.generation, generation + 2);
        assert!(crate::render_inventory::scoped_sources_current(
            &staging_world,
            candidate.owner
        ));
        candidate.request_gpu(&mut staging_world).unwrap();
        assert_eq!(candidate.generation, generation + 2);
        assert_eq!(candidate.entities, entities);
        assert!(!candidate.gpu_ready(&staging_world));
        meshes = staging_world.remove_resource::<Assets<Mesh>>().unwrap();
        let mut world = World::new();
        let foreign = world.spawn_empty().id();
        presentation
            .select(&mut world, head, &[(entity.id, 0)])
            .unwrap();
        let first = *presentation.entities.values().next().unwrap();
        assert_eq!(world.get::<Transform>(first), Some(&transform));
        assert!(!presentation.gpu_ready(&world));
        assert!(!presentation.simulation_resources_ready(&world, head));
        let cpu_readiness = presentation.readiness(&world, head);
        assert!(cpu_readiness.intact);
        assert!(!cpu_readiness.gpu_ready);
        assert!(!cpu_readiness.shadows_ready);
        assert!(!cpu_readiness.simulation_resources_ready);
        assert!(presentation.simulation_admission.lock().unwrap().is_none());
        // This CPU fixture deliberately has no material/GPU assets: it must not
        // manufacture a renderer receipt from its successfully spawned entities.
        assert!(presentation.request_gpu(&mut world).is_err());
        world.get_mut::<Transform>(first).unwrap().translation.x += 1.0;
        assert!(
            presentation
                .select(&mut world, head, &[(entity.id, 0)])
                .is_err()
        );
        assert!(!presentation.gpu_ready(&world));
        *world.get_mut::<Transform>(first).unwrap() = transform;
        presentation
            .select(&mut world, head, &[(entity.id, 0)])
            .unwrap();
        assert_eq!(presentation.entities.values().next(), Some(&first));
        assert!(
            presentation
                .select(&mut world, head, &[(entity.id, 0), (entity.id, 0)])
                .is_err()
        );
        assert_eq!(presentation.entity_count(), 1);
        let retained_transform_tick = world
            .entity(first)
            .get_ref::<Transform>()
            .unwrap()
            .last_changed();
        let retained_material_tick = world
            .entity(first)
            .get_ref::<MeshMaterial3d<crate::VisualValleyVegetationMaterial>>()
            .unwrap()
            .last_changed();
        world.increment_change_tick();
        presentation
            .select(&mut world, head, &[(entity.id, 0), (entity.id, 1)])
            .unwrap();
        assert_eq!(
            world
                .entity(first)
                .get_ref::<Transform>()
                .unwrap()
                .last_changed(),
            retained_transform_tick
        );
        assert_eq!(
            world
                .entity(first)
                .get_ref::<MeshMaterial3d<crate::VisualValleyVegetationMaterial>>()
                .unwrap()
                .last_changed(),
            retained_material_tick
        );
        assert_eq!(presentation.entity_count(), 2);
        assert_eq!(presentation.generation, 1);
        let medium = presentation.entities[&(entity.id, 1, 0)];
        let near_range = world
            .get::<bevy::camera::visibility::VisibilityRange>(first)
            .unwrap();
        let medium_range = world
            .get::<bevy::camera::visibility::VisibilityRange>(medium)
            .unwrap();
        assert_eq!(near_range.end_margin, medium_range.start_margin);
        let bounds = world.get::<Aabb>(first).unwrap();
        assert!(bounds.min().x < -1.0 && bounds.max().y > 3.0);
        assert_world_wind_enclosed(
            *bounds,
            transform,
            crate::vegetation::maximum_deformation_m(vegetation.presentation)
                * transform.scale.x.max(transform.scale.z),
        );
        world.increment_change_tick();
        assert!(presentation.is_intact(&world));
        let checks = presentation
            .audit
            .lock()
            .unwrap()
            .as_ref()
            .unwrap()
            .complete_audits;
        for _ in 0..4 {
            assert!(presentation.is_intact(&world));
        }
        assert_eq!(
            presentation
                .audit
                .lock()
                .unwrap()
                .as_ref()
                .unwrap()
                .complete_audits,
            checks
        );
        // Same-tick changes and component removals still revoke the cache.
        assert!(presentation.readiness(&world, head).intact);
        let observation_tick = world.read_change_tick();
        let original = *world.get::<Transform>(first).unwrap();
        world.get_mut::<Transform>(first).unwrap().translation.x += 1.0;
        assert_eq!(world.read_change_tick(), observation_tick);
        let changed_readiness = presentation.readiness(&world, head);
        assert!(!changed_readiness.intact);
        assert!(!changed_readiness.gpu_ready);
        assert!(!changed_readiness.shadows_ready);
        assert!(!changed_readiness.simulation_resources_ready);
        assert!(presentation.simulation_admission.lock().unwrap().is_none());
        assert!(!presentation.is_intact(&world));
        world.entity_mut(first).insert(original);
        assert!(presentation.readiness(&world, head).intact);
        assert!(presentation.is_intact(&world));
        world.entity_mut(first).remove::<NoAutoAabb>();
        assert!(!presentation.is_intact(&world));
        world.entity_mut(first).insert(NoAutoAabb);
        assert!(presentation.is_intact(&world));
        world.entity_mut(first).insert(bevy::light::NotShadowCaster);
        assert!(!presentation.is_intact(&world));
        world
            .entity_mut(first)
            .remove::<bevy::light::NotShadowCaster>();
        assert!(presentation.is_intact(&world));
        presentation
            .select(&mut world, head, &[(entity.id, 2)])
            .unwrap();
        assert!(world.get_entity(first).is_err());
        let far = *presentation.entities.values().next().unwrap();
        assert!(world.get::<bevy::light::NotShadowCaster>(far).is_some());
        presentation.retire(&mut world);
        assert!(world.get_entity(far).is_err());
        assert!(world.get_entity(foreign).is_ok());
        let mut empty = ImportedTreePresentation::prepare(
            EntityId::deterministic("empty-imported-model-owner"),
            head,
            &[],
            &cache,
            &vegetation,
            &meshes,
        )
        .unwrap();
        empty.stage_hidden(&mut world, head, &[]).unwrap();
        empty.request_gpu(&mut world).unwrap();
        assert!(empty.is_empty());
        assert!(empty.gpu_evidence(&world).is_none());
        assert!(!empty.gpu_ready(&world));
        world.insert_resource(bevy::diagnostic::FrameCount(
            empty.publication_frame.unwrap(),
        ));
        assert!(empty.gpu_ready(&world));
        let empty_readiness = empty.readiness(&world, head);
        assert!(empty_readiness.intact);
        assert!(empty_readiness.gpu_ready);
        assert!(!empty_readiness.simulation_resources_ready);
        assert!(empty.simulation_admission.lock().unwrap().is_none());
        assert_eq!(empty.entity_count(), 0);
        empty.retire(&mut world);
        cache.selection_catalog.as_mut().unwrap().bindings[0].source_pivot_m[0] = 1.0;
        assert!(bindings[0].render_lods(&cache).is_err());
        assert!(
            bind_imported_trees(
                &view,
                ContentHash::from_bytes([8; 32]),
                &vegetation,
                &catalog
            )
            .is_err()
        );
        let mut stale = catalog.clone();
        stale.vegetation_sha256 = ContentHash::from_bytes([8; 32]);
        assert!(bind_imported_trees(&view, head, &vegetation, &stale).is_err());
    }
}
