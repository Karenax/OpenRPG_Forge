//! Prepare a verified fish profile for any scene using the shared renderer.
use super::{
    material,
    prepare::{self, FishPropTemplate},
    shadow,
    surface::SpecularMode,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_fish_payload::{SourceFishPayload, SourceFishProfile};
use forge_core::{AssetId, ContentHash};
use std::{collections::BTreeSet, sync::Arc};

#[cfg(feature = "physics-rapier")]
pub mod live;
#[cfg(feature = "physics-rapier")]
pub mod scene;

/// CPU-prepared geometry, shaders and skin data for one caller-admitted profile.
pub struct PreparedProfile {
    pub package_sha256: ContentHash,
    pub templates: Vec<FishPropTemplate>,
    shader: String,
    shadow_shader: Arc<String>,
    skin: ShaderBuffer,
}

/// Prepared resources installed once; the caller owns their scene lifetime.
pub struct StagedProfile {
    pub package_sha256: ContentHash,
    pub templates: Vec<FishPropTemplate>,
    pub shadow_shader: Arc<String>,
    pub skin: Handle<ShaderBuffer>,
}

impl PreparedProfile {
    /// Prepare only the supplied profile; no module, Vault path or asset is inferred.
    ///
    /// # Errors
    /// Rejects missing or invalid geometry, material data and shader closures.
    pub fn new(
        profile: &impl SourceFishProfile,
        package_sha256: ContentHash,
        lighting_source: &str,
        mode: SpecularMode,
    ) -> Result<Self, String> {
        Ok(Self {
            package_sha256,
            templates: prepare::profile(profile)?,
            shader: material::shader_source(profile, lighting_source, mode)?,
            shadow_shader: Arc::new(shadow::shader_source(profile)?),
            skin: material::skin_buffer(profile)?,
        })
    }

    /// Prepare only assets requested by the caller, keeping the same profile pins.
    /// Unselected instance records are not turned into meshes.
    ///
    /// # Errors
    /// Rejects an empty selection, missing identities or invalid selected resources.
    pub fn new_selected(
        profile: &impl SourceFishProfile,
        package_sha256: ContentHash,
        lighting_source: &str,
        mode: SpecularMode,
        assets: &BTreeSet<AssetId>,
    ) -> Result<Self, String> {
        if assets.is_empty() {
            return Err("No fish assets selected".into());
        }
        let selected = SelectedProfile { profile, assets };
        let prepared = Self::new(&selected, package_sha256, lighting_source, mode)?;
        let actual: BTreeSet<_> = prepared
            .templates
            .iter()
            .map(|template| template.asset_id)
            .collect();
        if &actual != assets {
            return Err("Selected fish assets are absent from the admitted profile".into());
        }
        Ok(prepared)
    }

    /// Install the prepared shader and skin buffer without changing source data.
    ///
    /// # Errors
    /// Rejects absent renderer assets, unavailable shared scene receivers or a
    /// different fish shader while existing fish materials are still resident.
    pub fn stage(self, world: &mut World) -> Result<StagedProfile, String> {
        if !world.contains_resource::<Assets<Shader>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
        {
            return Err("Fish shader or buffer assets unavailable".into());
        }
        material::stage_source(world, self.shader)?;
        let skin = world.resource_mut::<Assets<ShaderBuffer>>().add(self.skin);
        Ok(StagedProfile {
            package_sha256: self.package_sha256,
            templates: self.templates,
            shadow_shader: self.shadow_shader,
            skin,
        })
    }
}

struct SelectedProfile<'a, P: SourceFishProfile> {
    profile: &'a P,
    assets: &'a BTreeSet<AssetId>,
}

impl<P: SourceFishProfile> SourceFishPayload for SelectedProfile<'_, P> {
    fn file(&self, name: &str) -> Option<&[u8]> {
        self.profile.file(name)
    }
}

impl<P: SourceFishProfile> SourceFishProfile for SelectedProfile<'_, P> {
    fn description(&self) -> &[u8] {
        self.profile.description()
    }

    fn inventory(&self) -> (usize, usize) {
        self.profile.inventory()
    }

    fn identify(&self, ordinal: usize, source_id: &str) -> Result<Option<AssetId>, String> {
        Ok(self
            .profile
            .identify(ordinal, source_id)?
            .filter(|asset| self.assets.contains(asset)))
    }
}
