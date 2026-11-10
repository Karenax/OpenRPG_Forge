//! Ownership of a map camera's mesh decals and its admitted source receivers.
//! Scene hosts supply already SHA-verified rasters and camera-exclusive materials.
use crate::{
    source_decal_view::{SourceDecalRaster, SourceDecalView, SourceDecalViewStatus},
    source_graph::SourceGraphMaterial,
};
use bevy::{
    camera::MainPassResolutionOverride,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass, NormalPrepass},
    prelude::*,
};
use std::collections::BTreeSet;

type Receivers = BTreeSet<AssetId<SourceGraphMaterial>>;

#[derive(Default)]
pub struct SourceDecalMap {
    camera: Option<BoundCamera>,
    bound: Receivers,
}

struct BoundCamera {
    entity: Entity,
    size: UVec2,
    targets: [Handle<Image>; 2],
    had_depth: bool,
}

impl SourceDecalMap {
    /// An empty map releases its camera and restores all original materials.
    pub fn clear(&mut self, world: &mut World) {
        if !self.bound.is_empty() {
            let mut materials = world.resource_mut::<Assets<SourceGraphMaterial>>();
            for id in std::mem::take(&mut self.bound) {
                if let Some(mut material) = materials.get_mut(id) {
                    material.extension.unbind_decal_view();
                }
            }
        }
        if let Some(bound) = self.camera.take()
            && let Ok(mut entity) = world.get_entity_mut(bound.entity)
            && entity
                .get::<SourceDecalView>()
                .is_some_and(|view| view.targets() == bound.targets)
        {
            entity.remove::<SourceDecalView>();
            if !bound.had_depth
                && !entity.contains::<MotionVectorPrepass>()
                && !entity.contains::<NormalPrepass>()
            {
                entity.remove::<DepthPrepass>();
            }
        }
    }

    /// Only the provided map camera samples these two attachments. The host
    /// controls material visibility and must release this route before preview.
    pub fn reconcile(
        &mut self,
        world: &mut World,
        camera: Entity,
        receivers: &Receivers,
        draws: Vec<(SourceDecalRaster, Mat4)>,
    ) -> Result<SourceDecalViewStatus, String> {
        if draws.is_empty() {
            self.clear(world);
            return Ok(SourceDecalViewStatus::Encoded(0));
        }
        let size = world
            .get::<Camera>(camera)
            .filter(|c| c.is_active)
            .and_then(Camera::physical_target_size)
            .ok_or("Caméra de carte indisponible pour les toiles")?;
        if world
            .get::<Msaa>(camera)
            .is_some_and(|msaa| *msaa != Msaa::Off)
            || world.get::<MainPassResolutionOverride>(camera).is_some()
        {
            self.clear(world);
            return Err(
                "Les toiles exigent une vue à résolution entière sans anticrénelage MSAA".into(),
            );
        }
        if self.camera.as_ref().is_some_and(|old| {
            old.entity != camera
                || old.size != size
                || world
                    .get::<SourceDecalView>(camera)
                    .is_none_or(|v| v.targets() != old.targets)
        }) {
            self.clear(world);
        }
        if self.camera.is_none() {
            if world.get::<SourceDecalView>(camera).is_some() {
                return Ok(SourceDecalViewStatus::Pending);
            }
            let view = SourceDecalView::new(&mut world.resource_mut::<Assets<Image>>(), size)?;
            self.camera = Some(BoundCamera {
                entity: camera,
                size,
                targets: view.targets(),
                had_depth: world.get::<DepthPrepass>(camera).is_some(),
            });
            world.entity_mut(camera).insert(view);
        }
        let view = world.get::<SourceDecalView>(camera).unwrap().clone();
        {
            let mut materials = world.resource_mut::<Assets<SourceGraphMaterial>>();
            self.bound.retain(|id| {
                if receivers.contains(id) {
                    return materials.contains(*id);
                }
                if let Some(mut material) = materials.get_mut(*id) {
                    material.extension.unbind_decal_view();
                }
                false
            });
            for id in receivers
                .difference(&self.bound)
                .copied()
                .collect::<Vec<_>>()
            {
                if materials
                    .get_mut(id)
                    .is_some_and(|mut mat| view.bind_graph_receiver(&mut mat))
                {
                    self.bound.insert(id);
                }
            }
        }
        let mut binding = world.get_mut::<SourceDecalView>(camera).unwrap();
        if self.bound.is_empty() {
            binding.set_draws(Vec::new())?;
            return Ok(SourceDecalViewStatus::Pending);
        }
        binding.set_draws(draws)?;
        Ok(binding.status())
    }
}
