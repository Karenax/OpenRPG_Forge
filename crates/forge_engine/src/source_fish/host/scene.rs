//! Shared fish placement staging for an admitted scene.
use super::PreparedProfile;
use crate::{
    distance_lod::Policy,
    source_fish::{material, prepare, resident},
    source_surface::SourceSurfaceMaterial,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_assets::source_fish_payload::{OwnedFishProfile, SourceFishPayload, SourceFishProfile};
use forge_core::AssetId;
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};
#[cfg(test)]
pub(in crate::source_fish::host) mod tests;

/// Profiles and authored placements validated before creating scene entities.
pub struct PreparedScene {
    profiles: Vec<PreparedProfile>,
    placements: Vec<ManualPlacement>,
    policies: BTreeMap<AssetId, Policy>,
}

/// Resources owned by one fish scene; shared lighting remains caller-owned.
pub struct Scene {
    resident: resident::ResidentFishProps,
    materials: BTreeMap<AssetId, Handle<material::FishMaterial>>,
    skins: Vec<Handle<ShaderBuffer>>,
    shadow_shader: Arc<String>,
}

/// Build the standard fish LOD policy from the ranges supplied by a profile.
/// Callers can instead pass their own validated policy to `PreparedScene::new`.
///
/// # Errors
/// Rejects absent, non-finite or invalid authored draw and shadow ranges.
pub fn profile_policy(profile: &impl SourceFishProfile) -> Result<Policy, String> {
    #[derive(serde::Deserialize)]
    struct Ranges {
        draw_range_m: f64,
        shadow_range_m: f64,
    }
    let ranges: Ranges =
        serde_json::from_slice(profile.description()).map_err(|error| error.to_string())?;
    Policy::new(
        ranges.draw_range_m,
        ranges.draw_range_m * 0.9,
        ranges.shadow_range_m,
        28.0,
        0.88,
    )
    .map_err(str::to_owned)
}

/// Select this scene's fish placements without inventing a fallback resource.
///
/// # Errors
/// Rejects duplicate catalog identities and changed package pins.
pub fn select_placements<'a, P: SourceFishPayload + 'a>(
    profiles: impl IntoIterator<Item = &'a OwnedFishProfile<P>>,
    placements: &[ManualPlacement],
) -> Result<Vec<ManualPlacement>, String> {
    let mut pins = BTreeMap::new();
    for profile in profiles {
        for asset in &profile.catalog.assets {
            if pins.insert(asset.id, profile.package_sha256).is_some() {
                return Err("Fish identity belongs to more than one profile".into());
            }
        }
    }
    let mut selected = Vec::new();
    for placement in placements {
        if let Some(pin) = pins.get(&placement.asset_id) {
            if *pin != placement.asset_hash {
                return Err("Fish placement profile pin differs".into());
            }
            resident::validate_companions(placement)?;
            selected.push(placement.clone());
        }
    }
    Ok(selected)
}

/// Prepare the selected assets from explicitly admitted catalogs and resources.
/// An empty placement set does not allocate fish geometry or shaders.
///
/// # Errors
/// Refuses ambiguous identities, foreign pins, invalid profiles or shaders.
pub fn from_catalogs<'a, P: SourceFishPayload + 'a>(
    profiles: impl IntoIterator<Item = &'a OwnedFishProfile<P>>,
    placements: &[ManualPlacement],
    lighting_source: &str,
    mode: crate::source_fish::surface::SpecularMode,
) -> Result<Option<PreparedScene>, String> {
    if placements.is_empty() {
        return Ok(None);
    }
    let profiles: Vec<_> = profiles.into_iter().collect();
    let mut owners = BTreeMap::new();
    for (index, profile) in profiles.iter().enumerate() {
        for asset in &profile.catalog.assets {
            if owners.insert(asset.id, index).is_some() {
                return Err("Fish identity belongs to more than one profile".into());
            }
        }
    }
    let mut selected: BTreeMap<usize, BTreeSet<AssetId>> = BTreeMap::new();
    for placement in placements {
        let index = *owners
            .get(&placement.asset_id)
            .ok_or("Fish placement is absent from the admitted profiles")?;
        if profiles[index].package_sha256 != placement.asset_hash {
            return Err("Fish placement profile pin differs".into());
        }
        selected
            .entry(index)
            .or_default()
            .insert(placement.asset_id);
    }
    let mut prepared = Vec::with_capacity(selected.len());
    for (index, assets) in selected {
        let profile = profiles[index];
        let view = profile.profile()?;
        let current_policy = profile_policy(&view)?;
        prepared.push((
            PreparedProfile::new_selected(
                &view,
                profile.package_sha256,
                lighting_source,
                mode,
                &assets,
            )?,
            current_policy,
        ));
    }
    PreparedScene::with_profile_policies(prepared, placements.to_vec()).map(Some)
}

impl PreparedScene {
    #[must_use]
    pub fn placements(&self) -> &[ManualPlacement] {
        &self.placements
    }

    /// Match every placement to its admitted asset and exact profile revision.
    ///
    /// # Errors
    /// Rejects empty scenes, duplicate identities, missing or changed pins, and
    /// simultaneous profiles with incompatible material or shadow shaders.
    pub fn new(
        profiles: Vec<PreparedProfile>,
        placements: Vec<ManualPlacement>,
        policy: Policy,
    ) -> Result<Self, String> {
        Self::with_profile_policies(
            profiles
                .into_iter()
                .map(|profile| (profile, policy))
                .collect(),
            placements,
        )
    }

    /// Match each profile and its own policy to the exact authored placements.
    ///
    /// # Errors
    /// Rejects empty scenes, duplicate identities, missing or changed pins, and
    /// simultaneous profiles with incompatible material or shadow shaders.
    pub fn with_profile_policies(
        mut profiles: Vec<(PreparedProfile, Policy)>,
        placements: Vec<ManualPlacement>,
    ) -> Result<Self, String> {
        if placements.is_empty() {
            return Err("No fish placements supplied".into());
        }
        PlacementChanges::between(&[], &placements)?;
        let mut assets = BTreeMap::new();
        for (profile, policy) in &profiles {
            for template in &profile.templates {
                if assets
                    .insert(template.asset_id, (profile.package_sha256, *policy))
                    .is_some()
                {
                    return Err("Repeated fish asset across profiles".into());
                }
            }
        }
        let wanted: BTreeSet<_> = placements.iter().map(|item| item.asset_id).collect();
        for placement in &placements {
            resident::validate_companions(placement)?;
            if assets.get(&placement.asset_id).map(|(hash, _)| hash) != Some(&placement.asset_hash)
            {
                return Err("Fish placement identity or profile pin differs".into());
            }
        }
        profiles.retain(|(profile, _)| {
            profile
                .templates
                .iter()
                .any(|template| wanted.contains(&template.asset_id))
        });
        let first = &profiles.first().ok_or("Fish profiles absent")?.0;
        if profiles.iter().any(|(profile, _)| {
            profile.shader != first.shader || profile.shadow_shader != first.shadow_shader
        }) {
            return Err("Fish profiles require incompatible material or shadow shaders".into());
        }
        let policies = wanted
            .into_iter()
            .map(|asset| (asset, assets[&asset].1))
            .collect();
        Ok(Self {
            profiles: profiles.into_iter().map(|(profile, _)| profile).collect(),
            placements,
            policies,
        })
    }

    /// Install only the selected templates and materials into the existing world.
    ///
    /// # Errors
    /// Refuses missing stores, invalid lighting, incompatible resident shaders
    /// or invalid placement resources before publishing scene entities.
    pub fn stage(
        self,
        world: &mut World,
        lighting: &SourceSurfaceMaterial,
        time: f32,
    ) -> Result<Scene, String> {
        if !world.contains_resource::<Assets<Shader>>()
            || !world.contains_resource::<Assets<ShaderBuffer>>()
            || !world.contains_resource::<Assets<Mesh>>()
            || !world.contains_resource::<Assets<material::FishMaterial>>()
        {
            return Err("Fish scene asset stores are unavailable".into());
        }
        let wanted: BTreeSet<_> = self.placements.iter().map(|item| item.asset_id).collect();
        for profile in &self.profiles {
            for template in &profile.templates {
                if wanted.contains(&template.asset_id) {
                    material::prepare_scene(template.surface, Handle::default(), lighting, time)?;
                }
            }
        }
        let shadow_shader = self.profiles[0].shadow_shader.clone();
        let mut skins = Vec::new();
        let mut materials = BTreeMap::new();
        let result = (|| {
            let mut templates = Vec::new();
            for profile in self.profiles {
                let staged = profile.stage(world)?;
                let hash = staged.package_sha256;
                skins.push(staged.skin.clone());
                for template in staged.templates {
                    if !wanted.contains(&template.asset_id) {
                        continue;
                    }
                    let value = material::prepare_scene(
                        template.surface,
                        staged.skin.clone(),
                        lighting,
                        time,
                    )?;
                    let handle = world
                        .resource_mut::<Assets<material::FishMaterial>>()
                        .add(value);
                    materials.insert(template.asset_id, handle);
                    templates.extend(prepare::resident_templates(vec![template], hash));
                }
            }
            resident::stage_authored_with_policies(
                templates,
                world,
                &materials,
                &self.placements,
                &self.policies,
            )
        })();
        match result {
            Ok(resident) => Ok(Scene {
                resident,
                materials,
                skins,
                shadow_shader,
            }),
            Err(error) => {
                release_assets(world, materials, skins);
                Err(error)
            }
        }
    }
}

impl Scene {
    #[must_use]
    pub fn resident(&self) -> &resident::ResidentFishProps {
        &self.resident
    }

    pub fn resident_mut(&mut self) -> &mut resident::ResidentFishProps {
        &mut self.resident
    }

    #[must_use]
    pub fn shadow_shader(&self) -> Arc<String> {
        self.shadow_shader.clone()
    }

    /// Release this scene's entities, materials and skin buffers only.
    pub fn clear(self, world: &mut World) {
        self.resident.clear(world);
        release_assets(world, self.materials, self.skins);
    }
}

fn release_assets(
    world: &mut World,
    materials: BTreeMap<AssetId, Handle<material::FishMaterial>>,
    skins: Vec<Handle<ShaderBuffer>>,
) {
    if let Some(mut store) = world.get_resource_mut::<Assets<material::FishMaterial>>() {
        for handle in materials.into_values() {
            store.remove(handle.id());
        }
    }
    if let Some(mut store) = world.get_resource_mut::<Assets<ShaderBuffer>>() {
        for handle in skins {
            store.remove(handle.id());
        }
    }
}
