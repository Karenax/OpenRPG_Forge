//! Exact source-identity joins for lights on verified building placements.
//! Captured matrices verify the shared anchor; only the authored live root
//! supplies the transform at staging. Geometry and source light bytes stay immutable.
use super::scene::ResidentScene;
use bevy::{
    math::{DMat4, DQuat, DVec3},
    prelude::*,
};
use forge_core::{AssetId, ContentHash};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};
use {
    crate::source_local_lights::projection::{PreparedSourceLights, ResidentSourceLights},
    forge_engine::ManualPlacementProjection,
};
use {
    forge_assets::source_local_lights::{
        SourceLocalLightsPackage, ownership::SourceLightOwnership,
    },
    forge_tidewater_assets::tidewater_package::BuildingPackage,
};

mod attestations;

#[derive(Deserialize)]
struct Descriptor {
    building: Building,
}
#[derive(Deserialize)]
struct Building {
    source_id: String,
    source_frame: Frame,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
}

struct Binding {
    frame: Option<String>,
    lights: Vec<ContentHash>,
    empty_attested: bool,
}

/// Immutable catalogue join. Missing identity never attests emitter absence.
pub struct BuildingLightBindings {
    package_revision: ContentHash,
    descriptor_revision: ContentHash,
    assets: BTreeMap<(AssetId, ContentHash), Binding>,
}

/// One placement's source lights, awaiting its propagated live root.
pub struct PreparedPlacementLights {
    placement: ManualPlacement,
    frame: String,
    lights: PreparedSourceLights,
}

impl BuildingLightBindings {
    pub fn from_packages(
        buildings: &BuildingPackage,
        lights: &SourceLocalLightsPackage,
    ) -> Result<Self, String> {
        let ownership = SourceLightOwnership::from_package(lights)?;
        let attestations = attestations::read(lights)?;
        let document = lights.descriptor().document();
        if buildings.source_commit() != document.source_commit {
            return Err("Building geometry and lights use different source commits".into());
        }
        let mut assets = BTreeMap::new();
        let mut names = BTreeSet::new();
        for asset in &buildings.catalog().assets {
            if !names.insert(asset.logical_name.as_str()) {
                return Err("Building source identity is ambiguous".into());
            }
            let bytes = buildings
                .file(&format!("{}.json", asset.content_hash))
                .ok_or("Building descriptor absent")?;
            let descriptor: Descriptor =
                serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
            if descriptor.building.source_id != asset.logical_name {
                return Err("Building source identity differs".into());
            }
            let ids = ownership
                .selection_for_source_object(&asset.logical_name)
                .unwrap_or_default()
                .to_vec();
            let empty_attested = if let Some(invocations) = &attestations {
                let invocation = invocations
                    .get(&asset.logical_name)
                    .ok_or("Building invocation identity is absent from the source receipt")?;
                verify_anchor(
                    &descriptor.building.source_frame,
                    invocation.world_from_local,
                )?;
                if invocation.emitted_lights != ids.len() {
                    return Err("Building invocation emitter count differs from ownership".into());
                }
                invocation.emitted_lights == 0
            } else {
                false
            };
            let rows: Vec<_> = ownership
                .records()
                .iter()
                .filter(|row| {
                    row.source_binding.source_object_id.as_deref()
                        == Some(asset.logical_name.as_str())
                })
                .collect();
            if let Some(invocation) = attestations
                .as_ref()
                .and_then(|rows| rows.get(&asset.logical_name))
                && rows.iter().any(|row| {
                    row.source_binding.producer_function.as_deref()
                        != Some(invocation.producer.as_str())
                })
            {
                return Err("Building invocation producer differs from light ownership".into());
            }
            let mut frames = BTreeSet::new();
            for row in &rows {
                if row.source_binding.anchor != "construction-root" {
                    return Err("Building light does not declare its construction anchor".into());
                }
                frames.insert(row.owner_frame.as_str());
            }
            if frames.len() > 1 {
                return Err("Building lights require more than one construction anchor".into());
            }
            let frame = frames.first().map(|frame| (*frame).to_owned());
            if let Some(frame_id) = &frame {
                let source = document
                    .frames
                    .iter()
                    .find(|frame| &frame.id == frame_id)
                    .ok_or("Building light frame absent")?;
                let captured = source
                    .world_from_local
                    .ok_or("Building light construction pose absent")?;
                verify_anchor(&descriptor.building.source_frame, captured)?;
            }
            if assets
                .insert(
                    (asset.id, asset.content_hash),
                    Binding {
                        frame,
                        lights: ids,
                        empty_attested,
                    },
                )
                .is_some()
            {
                return Err("Building asset revision is duplicated".into());
            }
        }
        Ok(Self {
            package_revision: lights.receipt_hash(),
            descriptor_revision: lights.descriptor().content_hash(),
            assets,
        })
    }

    /// An explicit zero-emitter observation is distinct from missing ownership.
    /// Unknown revisions and unattested empty selections still fail in prepare.
    pub fn prepare_optional(
        &self,
        package: &SourceLocalLightsPackage,
        placement: &ManualPlacement,
        registration_base: u64,
    ) -> Result<Option<PreparedPlacementLights>, String> {
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        if package.receipt_hash() != self.package_revision
            || package.descriptor().content_hash() != self.descriptor_revision
        {
            return Err("Building light package revision differs".into());
        }
        if self
            .assets
            .get(&(placement.asset_id, placement.asset_hash))
            .is_some_and(|binding| binding.empty_attested)
        {
            return Ok(None);
        }
        self.prepare(package, placement, registration_base)
            .map(Some)
    }

    /// Copies supply their own canonical instance ID and explicit disjoint order
    /// offset. Missing ownership evidence and unknown revisions both fail.
    pub fn prepare(
        &self,
        package: &SourceLocalLightsPackage,
        placement: &ManualPlacement,
        registration_base: u64,
    ) -> Result<PreparedPlacementLights, String> {
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        if package.receipt_hash() != self.package_revision
            || package.descriptor().content_hash() != self.descriptor_revision
        {
            return Err("Building light package revision differs".into());
        }
        let binding = self
            .assets
            .get(&(placement.asset_id, placement.asset_hash))
            .ok_or("Building light asset revision is unknown")?;
        let frame = binding
            .frame
            .as_ref()
            .ok_or("Building has no explicit light binding; emitter absence is not attested")?;
        let prepared = PreparedSourceLights::from_package_selection(
            package,
            placement.asset_id,
            registration_base,
            &binding.lights,
        )?;
        if prepared.owner_frame_ids().collect::<Vec<_>>() != vec![frame.as_str()] {
            return Err("Building light selection requires another anchor".into());
        }
        Ok(PreparedPlacementLights {
            placement: placement.clone(),
            frame: frame.clone(),
            lights: prepared,
        })
    }
}

impl PreparedPlacementLights {
    pub fn stage(&self, world: &mut World) -> Result<ResidentSourceLights, String> {
        let owners = self.live_owners(world)?;
        self.lights.stage(world, self.placement.id, &owners)
    }

    /// Reconcile an edited canonical building against its current live root.
    /// The generic projection validates the existing emitters and all new
    /// bindings before replacing anything; a refusal retains the old projection.
    pub fn replace(
        &self,
        world: &mut World,
        resident: &mut ResidentSourceLights,
    ) -> Result<(), String> {
        if resident.instance_id() != self.placement.id {
            return Err(
                "Building light replacement changes its canonical instance identity".into(),
            );
        }
        let owners = self.live_owners(world)?;
        resident.replace(world, &self.lights, &owners)
    }

    fn live_owners(&self, world: &mut World) -> Result<BTreeMap<String, Entity>, String> {
        let root = {
            let scene = world
                .get_resource::<ResidentScene>()
                .ok_or("Building scene absent")?;
            let mut instances = scene
                .instances
                .iter()
                .filter(|instance| instance.source.id == self.placement.id);
            let instance = instances
                .next()
                .ok_or("Building placement is not in the current scene")?;
            if instance.source != self.placement || instances.next().is_some() {
                return Err("Building placement is ambiguous or changed".into());
            }
            instance.root
        };
        let roots = world
            .query::<(Entity, &ManualPlacementProjection)>()
            .iter(world)
            .filter(|(_, source)| source.0.id == self.placement.id)
            .take(2)
            .count();
        if roots != 1
            || world.get::<ChildOf>(root).is_some()
            || world
                .get::<ManualPlacementProjection>(root)
                .is_none_or(|source| source.0 != self.placement)
            || world.get::<Transform>(root)
                != Some(&forge_engine::domain_transform_to_bevy(
                    self.placement.transform,
                ))
        {
            return Err("Building light root differs from its canonical placement".into());
        }
        Ok(BTreeMap::from([(self.frame.clone(), root)]))
    }

    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        self.lights.registration_orders()
    }
}

fn verify_anchor(frame: &Frame, captured: [f64; 16]) -> Result<(), String> {
    let translation = DVec3::from_array(frame.translation_m);
    let rotation = DQuat::from_array(frame.rotation_xyzw);
    let scale = DVec3::from_array(frame.scale);
    if !translation.is_finite()
        || !rotation.is_finite()
        || !scale.is_finite()
        || (rotation.length_squared() - 1.0).abs() > 1e-12
    {
        return Err("Building construction pose is nonfinite or non-unit".into());
    }
    let matrix = DMat4::from_scale_rotation_translation(scale, rotation, translation);
    if !matrix.is_finite()
        || (rotation.length_squared() - 1.0).abs() > 1e-12
        || matrix.determinant().abs() < 1e-12
        || matrix
            .to_cols_array()
            .iter()
            .zip(captured)
            .any(|(expected, actual)| !actual.is_finite() || (*expected - actual).abs() > 1e-12)
    {
        return Err("Building geometry and light construction anchors differ".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests;
