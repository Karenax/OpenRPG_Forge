//! Shared scene resources and generation checks for atmospheric scattering.
//! Publication proves command encoding only, not GPU completion or source parity.
use bevy::{
    asset::UntypedAssetId,
    prelude::*,
    render::{extract_resource::ExtractResource, storage::ShaderBuffer},
};
use std::sync::{
    Arc,
    atomic::{AtomicBool, AtomicU64, Ordering},
};

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

/// Assets are renderer inputs supplied by a scene provider, not map documents.
#[derive(Clone)]
pub struct Resources {
    pub owner: UntypedAssetId,
    pub hill_shadow: Handle<Image>,
    pub object_depth: Handle<Image>,
    pub live_shadow: Handle<ShaderBuffer>,
    pub cloud_shadow: Handle<Image>,
    pub optical_stamp: Handle<ShaderBuffer>,
    pub optical_surface: UntypedAssetId,
    pub lighting: Handle<ShaderBuffer>,
    pub terrain_domain: Vec4,
    pub cloud_rectangle: Vec4,
}

#[derive(Clone)]
pub struct Readiness {
    pub objects: Arc<AtomicBool>,
    pub clouds: Arc<AtomicBool>,
    pub optics: Arc<AtomicU64>,
}

/// The provider must obtain these identities from its current producing graphs,
/// independently of the extracted binding set, immediately before encoding.
pub struct Current<'a> {
    pub readiness: Readiness,
    pub object_depth: &'a Handle<Image>,
    pub cloud_shadow: &'a Handle<Image>,
    pub optical_stamp: &'a Handle<ShaderBuffer>,
    pub optical_surface: UntypedAssetId,
    pub lighting: &'a Handle<ShaderBuffer>,
    pub lighting_encoded: bool,
}

#[derive(Clone)]
pub struct Bindings {
    pub resources: Resources,
    readiness: Readiness,
}

impl Bindings {
    /// Invalid domains never become a published scattering binding set.
    #[must_use]
    pub fn new(resources: Resources, readiness: Readiness) -> Option<Self> {
        if !resources.terrain_domain.is_finite()
            || resources.terrain_domain.z <= 0.0
            || !resources.cloud_rectangle.is_finite()
            || resources.cloud_rectangle.z <= 0.0
            || !(0.0..=1.0).contains(&resources.cloud_rectangle.w)
        {
            return None;
        }
        Some(Self {
            resources,
            readiness,
        })
    }

    #[must_use]
    pub fn encoded_for(&self, current: &Current) -> bool {
        Arc::ptr_eq(&self.readiness.objects, &current.readiness.objects)
            && Arc::ptr_eq(&self.readiness.clouds, &current.readiness.clouds)
            && Arc::ptr_eq(&self.readiness.optics, &current.readiness.optics)
            && &self.resources.object_depth == current.object_depth
            && &self.resources.cloud_shadow == current.cloud_shadow
            && &self.resources.optical_stamp == current.optical_stamp
            && self.resources.optical_surface == current.optical_surface
            && &self.resources.lighting == current.lighting
            && current.lighting_encoded
            && self.readiness.objects.load(Ordering::Acquire)
            && self.readiness.clouds.load(Ordering::Acquire)
            && self.readiness.optics.load(Ordering::Acquire) > 0
    }
}

#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Inputs {
    pub selection: Selection,
    pub current: Option<Bindings>,
    provider: Option<std::any::TypeId>,
}

impl Inputs {
    /// Scene providers cannot overwrite one another's published bindings.
    pub fn claim<P: 'static>(&mut self) -> bool {
        let provider = std::any::TypeId::of::<P>();
        if self.provider.is_some_and(|owner| owner != provider) {
            return false;
        }
        self.provider = Some(provider);
        true
    }

    #[must_use]
    pub fn owned_by<P: 'static>(&self) -> bool {
        self.provider == Some(std::any::TypeId::of::<P>())
    }

    pub fn publish<P: 'static>(&mut self, bindings: Bindings) -> bool {
        if !self.claim::<P>() {
            return false;
        }
        self.current = Some(bindings);
        self.selection = Selection::Selected;
        true
    }

    pub fn release<P: 'static>(&mut self) -> bool {
        if !self.owned_by::<P>() {
            return false;
        }
        self.provider = None;
        self.current = None;
        self.selection = Selection::default();
        true
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn another_provider_cannot_replace_or_unload_the_current_scene() {
        struct Forest;
        struct Desert;
        let (bindings, _, _) = fixture();
        let mut inputs = Inputs::default();
        assert!(inputs.publish::<Forest>(bindings.clone()));
        assert!(!inputs.publish::<Desert>(bindings.clone()));
        assert!(!inputs.release::<Desert>());
        assert!(inputs.current.is_some() && inputs.owned_by::<Forest>());
        assert!(inputs.release::<Forest>());
        assert!(inputs.current.is_none());
        assert!(inputs.publish::<Desert>(bindings));
    }

    fn fixture() -> (Bindings, Resources, Readiness) {
        let mut images = Assets::<Image>::default();
        let mut buffers = Assets::<ShaderBuffer>::default();
        let image = images.add(Image::default());
        let resources = Resources {
            owner: image.id().untyped(),
            hill_shadow: images.add(Image::default()),
            object_depth: image,
            live_shadow: buffers.add(ShaderBuffer::from(vec![Vec4::ZERO])),
            cloud_shadow: images.add(Image::default()),
            optical_stamp: buffers.add(ShaderBuffer::from(vec![Vec4::ZERO])),
            optical_surface: images.add(Image::default()).id().untyped(),
            lighting: buffers.add(ShaderBuffer::from(vec![Vec4::ZERO; 3])),
            terrain_domain: Vec4::new(-500.0, -500.0, 1000.0, 1.0),
            cloud_rectangle: Vec4::new(0.0, 0.0, 2000.0, 1.0),
        };
        let readiness = Readiness {
            objects: Arc::new(AtomicBool::new(true)),
            clouds: Arc::new(AtomicBool::new(true)),
            optics: Arc::new(AtomicU64::new(1)),
        };
        (
            Bindings::new(resources.clone(), readiness.clone()).unwrap(),
            resources,
            readiness,
        )
    }

    #[test]
    fn independent_provider_rejects_replaced_generations_and_unencoded_light() {
        let (bindings, resources, readiness) = fixture();
        let mut current = Current {
            readiness,
            object_depth: &resources.object_depth,
            cloud_shadow: &resources.cloud_shadow,
            optical_stamp: &resources.optical_stamp,
            optical_surface: resources.optical_surface,
            lighting: &resources.lighting,
            lighting_encoded: true,
        };
        assert!(bindings.encoded_for(&current));
        current.lighting_encoded = false;
        assert!(!bindings.encoded_for(&current));
        current.lighting_encoded = true;
        current.readiness.objects = Arc::new(AtomicBool::new(true));
        assert!(!bindings.encoded_for(&current));
        current.readiness = bindings.readiness.clone();
        current.readiness.clouds = Arc::new(AtomicBool::new(true));
        assert!(!bindings.encoded_for(&current));
        current.readiness = bindings.readiness.clone();
        current.readiness.optics = Arc::new(AtomicU64::new(1));
        assert!(!bindings.encoded_for(&current));
    }

    #[test]
    fn invalid_domains_and_changed_resource_handles_are_rejected() {
        let (bindings, mut resources, readiness) = fixture();
        resources.terrain_domain.z = 0.0;
        assert!(Bindings::new(resources.clone(), readiness.clone()).is_none());
        resources.terrain_domain = bindings.resources.terrain_domain;
        resources.cloud_rectangle.w = f32::NAN;
        assert!(Bindings::new(resources.clone(), readiness.clone()).is_none());
        let current = Current {
            readiness,
            object_depth: &Handle::default(),
            cloud_shadow: &resources.cloud_shadow,
            optical_stamp: &resources.optical_stamp,
            optical_surface: resources.optical_surface,
            lighting: &resources.lighting,
            lighting_encoded: true,
        };
        assert!(!bindings.encoded_for(&current));
    }
}
