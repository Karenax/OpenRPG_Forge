//! Select the resident terrain's actual resources for the volumetric post pass.
//! This is disposable render state; no asset/module-name policy or authoring copy.
#![allow(clippy::needless_pass_by_value)] // Bevy system resource wrappers.

use crate::{
    tidewater_clouds, tidewater_object_shadow,
    tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
    tidewater_underwater_lighting,
};
use bevy::{
    ecs::system::SystemParam,
    prelude::*,
    render::{extract_resource::ExtractResource, storage::ShaderBuffer},
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU64, Ordering},
};

/// A diagnosis of CPU selection only; `Selected` does not prove GPU readiness.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum Selection {
    #[default]
    NoTerrain,
    NoMaterial,
    NoObjectGraph,
    NoCloudGraph,
    NoOpticalMaps,
    NoOpticalSurface,
    NoLightingSnapshot,
    LightingMismatch,
    ObjectMismatch,
    CloudMismatch,
    OpticalStampMismatch,
    InvalidDomain,
    Selected,
}

/// Exact resources referenced by the displayed terrain and active scene graphs.
#[derive(Clone)]
pub struct Bindings {
    pub terrain: Handle<LodMaterial>,
    pub hill_shadow: Handle<Image>,
    pub object_depth: Handle<Image>,
    pub live_shadow: Handle<ShaderBuffer>,
    pub cloud_shadow: Handle<Image>,
    /// Map origins/stamp, 32 bytes. This is NOT the 720-byte source frame.
    pub optical_stamp: Handle<ShaderBuffer>,
    /// Resolve this material's prepared optical bind group for GPU lighting.
    pub optical_surface: bevy::asset::UntypedAssetId,
    pub source_lighting: Handle<ShaderBuffer>,
    pub terrain_domain: Vec4,
    pub cloud_rectangle: Vec4,
    object_generation: Arc<AtomicBool>,
    cloud_generation: Arc<AtomicBool>,
    optical_generation: Arc<AtomicU64>,
}

impl Bindings {
    /// Check against the current render-world generations immediately before
    /// preparing bindings. An old `true` ready flag never authorizes new maps.
    #[must_use]
    pub fn encoded_for(
        &self,
        object: &tidewater_object_shadow::graph::Target,
        clouds: &tidewater_clouds::Targets,
        optics: &tidewater_underwater_lighting::Targets,
        surface: &tidewater_underwater_lighting::Surface,
        lighting: &super::lighting::Targets,
    ) -> bool {
        Arc::ptr_eq(&self.object_generation, &object.ready)
            && Arc::ptr_eq(&self.cloud_generation, &clouds.ready)
            && Arc::ptr_eq(&self.optical_generation, &optics.encoded_frames)
            && self.object_depth == object.image
            && clouds.images.get(2) == Some(&self.cloud_shadow)
            && self.optical_stamp == optics.bindings.frame
            && self.optical_surface == surface.0
            && self.source_lighting == lighting.buffer
            && lighting.encoded_for(optics, surface)
            && self.object_generation.load(Ordering::Acquire)
            && self.cloud_generation.load(Ordering::Acquire)
            && self.optical_generation.load(Ordering::Acquire) > 0
    }
}

/// Extract `None` too, so unloading/replacing terrain clears stale GPU inputs.
#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Inputs {
    pub selection: Selection,
    pub current: Option<Bindings>,
}

#[derive(SystemParam)]
pub(super) struct OpticalResources<'w> {
    optics: Option<Res<'w, tidewater_underwater_lighting::Targets>>,
    surface: Option<Res<'w, tidewater_underwater_lighting::Surface>>,
    lighting: Option<Res<'w, super::lighting::Targets>>,
}

pub(super) fn refresh(
    resident: Option<Res<ResidentTerrain>>,
    materials: Option<Res<Assets<LodMaterial>>>,
    object: Option<Res<tidewater_object_shadow::graph::Target>>,
    clouds: Option<Res<tidewater_clouds::Targets>>,
    optical: OpticalResources,
    mut inputs: ResMut<Inputs>,
) {
    let OpticalResources {
        optics,
        surface,
        lighting,
    } = optical;
    let selected = (|| {
        let resident = resident.as_deref().ok_or(Selection::NoTerrain)?;
        let handle = resident.material.as_ref().ok_or(Selection::NoMaterial)?;
        let material = materials
            .as_deref()
            .and_then(|m| m.get(handle))
            .ok_or(Selection::NoMaterial)?;
        let base = &material.base;
        let object = object.as_deref().ok_or(Selection::NoObjectGraph)?;
        let clouds = clouds.as_deref().ok_or(Selection::NoCloudGraph)?;
        let optics = optics.as_deref().ok_or(Selection::NoOpticalMaps)?;
        let surface = surface.as_deref().ok_or(Selection::NoOpticalSurface)?;
        let lighting = lighting.as_deref().ok_or(Selection::NoLightingSnapshot)?;
        if !lighting.same_generation(optics) {
            return Err(Selection::LightingMismatch);
        }
        if base.object_shadow != object.image
            || base.live_shadows != tidewater_object_shadow::receiver::DEFAULT_BUFFER
        {
            return Err(Selection::ObjectMismatch);
        }
        let cloud = base.cloud_shadow.as_ref().ok_or(Selection::CloudMismatch)?;
        if clouds.images.get(2) != Some(cloud) {
            return Err(Selection::CloudMismatch);
        }
        if base.underwater_map_frame != optics.bindings.frame {
            return Err(Selection::OpticalStampMismatch);
        }
        let domain = base.settings.domain;
        let rectangle = base.clouds[4];
        if !domain.is_finite()
            || domain.y <= 0.0
            || !rectangle.is_finite()
            || rectangle.z <= 0.0
            || !(0.0..=1.0).contains(&rectangle.w)
        {
            return Err(Selection::InvalidDomain);
        }
        Ok(Bindings {
            terrain: handle.clone(),
            hill_shadow: base.hill_shadow.clone(),
            object_depth: base.object_shadow.clone(),
            live_shadow: base.live_shadows.clone(),
            cloud_shadow: cloud.clone(),
            optical_stamp: base.underwater_map_frame.clone(),
            optical_surface: surface.0,
            source_lighting: lighting.buffer.clone(),
            // The source square terrain uses one origin on both axes, and
            // its current hill-shadow shader blends sunBaked at exactly 1.
            terrain_domain: Vec4::new(domain.x, domain.x, domain.y, 1.0),
            cloud_rectangle: rectangle,
            object_generation: object.ready.clone(),
            cloud_generation: clouds.ready.clone(),
            optical_generation: optics.encoded_frames.clone(),
        })
    })();
    match selected {
        Ok(current) => {
            inputs.selection = Selection::Selected;
            inputs.current = Some(current);
        }
        Err(reason) => {
            inputs.selection = reason;
            inputs.current = None;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::{Mutex, atomic::AtomicU32};

    fn fixture() -> (
        Bindings,
        tidewater_object_shadow::graph::Target,
        tidewater_clouds::Targets,
        tidewater_underwater_lighting::Targets,
        super::super::lighting::Targets,
    ) {
        let mut images = Assets::<Image>::default();
        let object = tidewater_object_shadow::graph::Target {
            settings: Default::default(),
            image: images.add(Image::default()),
            trace: Default::default(),
            ready: Arc::new(AtomicBool::new(false)),
        };
        let clouds = tidewater_clouds::Targets {
            images: (0..3).map(|_| images.add(Image::default())).collect(),
            ready: Arc::new(AtomicBool::new(false)),
            completed_steps: Arc::new(AtomicU32::new(0)),
            submissions: Arc::new(AtomicU32::new(0)),
        };
        let optics = tidewater_underwater_lighting::Targets {
            bindings: tidewater_underwater_lighting::Bindings {
                waves: images.add(Image::default()),
                level: images.add(Image::default()),
                frame: tidewater_underwater_lighting::NEUTRAL_FRAME,
            },
            encoded_frames: Arc::new(AtomicU64::new(0)),
            observation: Arc::new(Mutex::new(Default::default())),
        };
        let lighting = super::super::lighting::Targets::new(
            Default::default(),
            optics.bindings.frame.clone(),
            optics.encoded_frames.clone(),
        );
        let inputs = Bindings {
            terrain: Default::default(),
            hill_shadow: images.add(Image::default()),
            object_depth: object.image.clone(),
            live_shadow: tidewater_object_shadow::receiver::DEFAULT_BUFFER,
            cloud_shadow: clouds.images[2].clone(),
            optical_stamp: optics.bindings.frame.clone(),
            optical_surface: Handle::<StandardMaterial>::default().id().untyped(),
            source_lighting: lighting.buffer.clone(),
            terrain_domain: Vec4::new(-1024.0, -1024.0, 2048.0, 1.0),
            cloud_rectangle: Vec4::new(0.0, 0.0, 8192.0, 1.0),
            object_generation: object.ready.clone(),
            cloud_generation: clouds.ready.clone(),
            optical_generation: optics.encoded_frames.clone(),
        };
        (inputs, object, clouds, optics, lighting)
    }

    #[test]
    fn requires_all_current_generations_and_matching_handles() {
        let (inputs, mut object, mut clouds, mut optics, lighting) = fixture();
        let mut surface = tidewater_underwater_lighting::Surface(inputs.optical_surface);
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        object.ready.store(true, Ordering::Release);
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        clouds.ready.store(true, Ordering::Release);
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        optics.encoded_frames.store(1, Ordering::Release);
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        assert!(lighting.record(&surface, 1));
        assert!(inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        optics.encoded_frames.store(2, Ordering::Release);
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        assert!(!lighting.record(&surface, 1));
        assert!(lighting.record(&surface, 2));
        assert!(inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));

        let current = object.ready.clone();
        object.ready = Arc::new(AtomicBool::new(true));
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        object.ready = current;
        let current = clouds.ready.clone();
        clouds.ready = Arc::new(AtomicBool::new(true));
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        clouds.ready = current;
        let current = optics.encoded_frames.clone();
        optics.encoded_frames = Arc::new(AtomicU64::new(1));
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        optics.encoded_frames = current;

        object.image = Default::default();
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        object.image = inputs.object_depth.clone();
        clouds.images[2] = Default::default();
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        clouds.images[2] = inputs.cloud_shadow.clone();
        optics.bindings.frame = Default::default();
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
        optics.bindings.frame = inputs.optical_stamp.clone();
        let mut material = Assets::<StandardMaterial>::default();
        surface.0 = material.add(StandardMaterial::default()).id().untyped();
        assert!(!inputs.encoded_for(&object, &clouds, &optics, &surface, &lighting));
    }

    #[test]
    fn unloading_terrain_clears_previous_selected_inputs() {
        let (current, _, _, _, _) = fixture();
        let mut app = App::new();
        app.insert_resource(Inputs {
            selection: Selection::Selected,
            current: Some(current),
        })
        .add_systems(Update, refresh);
        app.update();
        let inputs = app.world().resource::<Inputs>();
        assert_eq!(inputs.selection, Selection::NoTerrain);
        assert!(inputs.current.is_none());
    }
}
