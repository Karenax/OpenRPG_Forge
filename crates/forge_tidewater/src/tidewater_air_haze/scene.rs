//! Select the resident terrain's actual resources for the volumetric post pass.
//! This is disposable render state; no asset/module-name policy or authoring copy.
#![allow(clippy::needless_pass_by_value)] // Bevy system resource wrappers.

use crate::{
    tidewater_clouds, tidewater_object_shadow,
    tidewater_terrain::{lod::LodMaterial, scene::ResidentTerrain},
    tidewater_underwater_lighting,
};
use bevy::{ecs::system::SystemParam, prelude::*};
use forge_engine::atmospheric_haze::scene::{
    Bindings, Current, Inputs, Readiness, Resources, Selection,
};
#[cfg(test)]
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU64, Ordering},
};

/// Adapt current producer identities to the shared renderer contract.
pub(super) fn current<'a>(
    object: &'a tidewater_object_shadow::graph::Target,
    clouds: &'a tidewater_clouds::Targets,
    optics: &'a tidewater_underwater_lighting::Targets,
    surface: &'a tidewater_underwater_lighting::Surface,
    lighting: &'a super::lighting::Targets,
) -> Option<Current<'a>> {
    Some(Current {
        readiness: Readiness {
            objects: object.ready.clone(),
            clouds: clouds.ready.clone(),
            optics: optics.encoded_frames.clone(),
        },
        object_depth: &object.image,
        cloud_shadow: clouds.images.get(2)?,
        optical_stamp: &optics.bindings.frame,
        optical_surface: surface.0,
        lighting: &lighting.buffer,
        lighting_encoded: lighting.encoded_for(optics, surface),
    })
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
    if resident.is_none() {
        let _ = inputs.release::<ResidentTerrain>();
        return;
    }
    if !inputs.claim::<ResidentTerrain>() {
        return;
    }
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
        Bindings::new(
            Resources {
                owner: handle.id().untyped(),
                hill_shadow: base.hill_shadow.clone(),
                object_depth: base.object_shadow.clone(),
                live_shadow: base.live_shadows.clone(),
                cloud_shadow: cloud.clone(),
                optical_stamp: base.underwater_map_frame.clone(),
                optical_surface: surface.0,
                lighting: lighting.buffer.clone(),
                // The source square terrain uses one origin on both axes, and
                // its current hill-shadow shader blends sunBaked at exactly 1.
                terrain_domain: Vec4::new(domain.x, domain.x, domain.y, 1.0),
                cloud_rectangle: rectangle,
            },
            Readiness {
                objects: object.ready.clone(),
                clouds: clouds.ready.clone(),
                optics: optics.encoded_frames.clone(),
            },
        )
        .ok_or(Selection::InvalidDomain)
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
            settings: crate::tidewater_material::ObjectShadowSettings::default(),
            image: images.add(Image::default()),
            trace: Handle::default(),
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
            observation: Arc::new(Mutex::new(
                tidewater_underwater_lighting::Observation::default(),
            )),
        };
        let lighting = super::super::lighting::Targets::new(
            Handle::default(),
            optics.bindings.frame.clone(),
            optics.encoded_frames.clone(),
        );
        let inputs = Bindings::new(
            Resources {
                owner: Handle::<LodMaterial>::default().id().untyped(),
                hill_shadow: images.add(Image::default()),
                object_depth: object.image.clone(),
                live_shadow: tidewater_object_shadow::receiver::DEFAULT_BUFFER,
                cloud_shadow: clouds.images[2].clone(),
                optical_stamp: optics.bindings.frame.clone(),
                optical_surface: Handle::<StandardMaterial>::default().id().untyped(),
                lighting: lighting.buffer.clone(),
                terrain_domain: Vec4::new(-1024.0, -1024.0, 2048.0, 1.0),
                cloud_rectangle: Vec4::new(0.0, 0.0, 8192.0, 1.0),
            },
            Readiness {
                objects: object.ready.clone(),
                clouds: clouds.ready.clone(),
                optics: optics.encoded_frames.clone(),
            },
        )
        .unwrap();
        (inputs, object, clouds, optics, lighting)
    }

    fn encoded_for(
        inputs: &Bindings,
        object: &tidewater_object_shadow::graph::Target,
        clouds: &tidewater_clouds::Targets,
        optics: &tidewater_underwater_lighting::Targets,
        surface: &tidewater_underwater_lighting::Surface,
        lighting: &super::super::lighting::Targets,
    ) -> bool {
        current(object, clouds, optics, surface, lighting)
            .is_some_and(|state| inputs.encoded_for(&state))
    }

    #[test]
    fn requires_all_current_generations_and_matching_handles() {
        let (inputs, mut object, mut clouds, mut optics, lighting) = fixture();
        let mut surface = tidewater_underwater_lighting::Surface(inputs.resources.optical_surface);
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        object.ready.store(true, Ordering::Release);
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        clouds.ready.store(true, Ordering::Release);
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        optics.encoded_frames.store(1, Ordering::Release);
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        assert!(lighting.record(&surface, 1));
        assert!(encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        optics.encoded_frames.store(2, Ordering::Release);
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        assert!(!lighting.record(&surface, 1));
        assert!(lighting.record(&surface, 2));
        assert!(encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));

        let current = object.ready.clone();
        object.ready = Arc::new(AtomicBool::new(true));
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        object.ready = current;
        let current = clouds.ready.clone();
        clouds.ready = Arc::new(AtomicBool::new(true));
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        clouds.ready = current;
        let current = optics.encoded_frames.clone();
        optics.encoded_frames = Arc::new(AtomicU64::new(1));
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        optics.encoded_frames = current;

        object.image = Handle::default();
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        object.image = inputs.resources.object_depth.clone();
        clouds.images[2] = Handle::default();
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        clouds.images[2] = inputs.resources.cloud_shadow.clone();
        optics.bindings.frame = Handle::default();
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
        optics.bindings.frame = inputs.resources.optical_stamp.clone();
        let mut material = Assets::<StandardMaterial>::default();
        surface.0 = material.add(StandardMaterial::default()).id().untyped();
        assert!(!encoded_for(
            &inputs, &object, &clouds, &optics, &surface, &lighting
        ));
    }

    #[test]
    fn unloading_terrain_clears_previous_selected_inputs() {
        let (current, _, _, _, _) = fixture();
        let mut app = App::new();
        let mut inputs = Inputs::default();
        assert!(inputs.publish::<ResidentTerrain>(current));
        app.insert_resource(inputs).add_systems(Update, refresh);
        app.update();
        let inputs = app.world().resource::<Inputs>();
        assert_eq!(inputs.selection, Selection::NoTerrain);
        assert!(inputs.current.is_none());
    }
}
