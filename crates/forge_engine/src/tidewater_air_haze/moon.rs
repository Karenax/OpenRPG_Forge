//! Select the displayed sky's actual moon/night inputs, never the first asset.
use crate::{tidewater_atmosphere, tidewater_sky::SkyMaterial};
use bevy::{prelude::*, render::extract_resource::ExtractResource};
use std::sync::{
    Arc,
    atomic::{AtomicBool, Ordering},
};

#[derive(Clone)]
pub struct Selected {
    pub material: Handle<SkyMaterial>,
    pub sky: Handle<Image>,
    pub transmittance: Handle<Image>,
    pub direction_night: Vec4,
    generation: Arc<AtomicBool>,
}

impl Selected {
    #[must_use]
    pub fn encoded_for(&self, atmosphere: &tidewater_atmosphere::Targets) -> bool {
        Arc::ptr_eq(&self.generation, &atmosphere.ready)
            && atmosphere.ready.load(Ordering::Acquire)
            && atmosphere.images.get(2) == Some(&self.sky)
            && atmosphere.images.first() == Some(&self.transmittance)
    }
}

#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Inputs {
    pub current: Option<Selected>,
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system wrappers")]
pub(super) fn refresh(
    sky_entities: Query<&MeshMaterial3d<SkyMaterial>>,
    materials: Option<Res<Assets<SkyMaterial>>>,
    atmosphere: Option<Res<tidewater_atmosphere::Targets>>,
    mut inputs: ResMut<Inputs>,
) {
    inputs.current = None;
    let (Some(materials), Some(atmosphere)) = (materials, atmosphere) else {
        return;
    };
    let mut selected: Option<Selected> = None;
    for handle in &sky_entities {
        let Some(material) = materials.get(&handle.0) else {
            continue;
        };
        if atmosphere.images.get(2) != Some(&material.sky)
            || atmosphere.images.first() != Some(&material.transmittance)
        {
            continue;
        }
        if selected
            .as_ref()
            .is_some_and(|current| current.material != handle.0)
        {
            // Several displayed sky materials cannot be silently assigned to
            // every camera; a view-specific ownership contract is required.
            return;
        }
        let direction = material.settings.moon_disk.truncate();
        let night = material.settings.stars_time_night.z;
        if !direction.is_finite()
            || direction.length_squared() < 1e-12
            || !night.is_finite()
            || !(0.0..=1.0).contains(&night)
        {
            return;
        }
        selected = Some(Selected {
            material: handle.0.clone(),
            sky: material.sky.clone(),
            transmittance: material.transmittance.clone(),
            direction_night: direction.extend(night),
            generation: atmosphere.ready.clone(),
        });
    }
    inputs.current = selected;
}
