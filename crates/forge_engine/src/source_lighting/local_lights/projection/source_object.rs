//! Exact source-identity joins for lights on verified source-object placements.
//! Captured matrices verify the shared anchor; only the authored live root
//! supplies the transform at staging. Geometry and source light bytes stay immutable.
use super::{PreparedSourceLights, ResidentSourceLights};
use crate::ManualPlacementProjection;
use bevy::{
    math::{DMat4, DQuat, DVec3},
    prelude::*,
};
use forge_assets::source_local_lights::{
    SourceLocalLightsPackage, ownership::SourceLightOwnership,
};
use forge_core::{AssetId, ContentHash};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

mod attestations;

/// One hash-verified acquisition identity supplied by the caller's codec.
/// No catalogue size, module name or renderer scene type selects this binding.
pub struct SourceObject {
    pub asset_id: AssetId,
    pub asset_hash: ContentHash,
    pub source_id: String,
    pub source_frame: ConstructionFrame,
}

/// Exact double-precision construction pose, before live authoring transforms.
#[derive(Clone, Debug, Deserialize)]
pub struct ConstructionFrame {
    pub translation_m: [f64; 3],
    pub rotation_xyzw: [f64; 4],
    pub scale: [f64; 3],
}

struct Binding {
    frame: Option<String>,
    lights: Vec<ContentHash>,
    empty_attested: bool,
}

/// Immutable catalogue join. Missing identity never attests emitter absence.
pub struct SourceObjectLightBindings {
    package_revision: ContentHash,
    descriptor_revision: ContentHash,
    assets: BTreeMap<(AssetId, ContentHash), Binding>,
}

/// One placement's source lights, awaiting its propagated live root.
pub struct PreparedObjectLights {
    placement: ManualPlacement,
    frame: String,
    lights: PreparedSourceLights,
}

impl SourceObjectLightBindings {
    pub fn from_sources(
        source_commit: &str,
        sources: impl IntoIterator<Item = SourceObject>,
        lights: &SourceLocalLightsPackage,
    ) -> Result<Self, String> {
        let ownership = SourceLightOwnership::from_package(lights)?;
        let attestations = attestations::read(lights)?;
        let document = lights.descriptor().document();
        if source_commit != document.source_commit {
            return Err("Source object geometry and lights use different source commits".into());
        }
        let mut assets = BTreeMap::new();
        let mut names = BTreeSet::new();
        for asset in sources {
            if asset.source_id.is_empty() || asset.source_id.chars().any(char::is_control) {
                return Err("Source object identity is invalid".into());
            }
            if !names.insert(asset.source_id.clone()) {
                return Err("Source object source identity is ambiguous".into());
            }
            let ids = ownership
                .selection_for_source_object(&asset.source_id)
                .unwrap_or_default()
                .to_vec();
            let empty_attested = if let Some(invocations) = &attestations {
                let invocation = invocations
                    .get(&asset.source_id)
                    .ok_or("Source object invocation identity is absent from the source receipt")?;
                verify_anchor(&asset.source_frame, invocation.world_from_local)?;
                if invocation.emitted_lights != ids.len() {
                    return Err(
                        "Source object invocation emitter count differs from ownership".into(),
                    );
                }
                invocation.emitted_lights == 0
            } else {
                false
            };
            let rows: Vec<_> = ownership
                .records()
                .iter()
                .filter(|row| {
                    row.source_binding.source_object_id.as_deref() == Some(asset.source_id.as_str())
                })
                .collect();
            if let Some(invocation) = attestations
                .as_ref()
                .and_then(|rows| rows.get(&asset.source_id))
                && rows.iter().any(|row| {
                    row.source_binding.producer_function.as_deref()
                        != Some(invocation.producer.as_str())
                })
            {
                return Err(
                    "Source object invocation producer differs from light ownership".into(),
                );
            }
            let mut frames = BTreeSet::new();
            for row in &rows {
                if row.source_binding.anchor != "construction-root" {
                    return Err(
                        "Source object light does not declare its construction anchor".into(),
                    );
                }
                frames.insert(row.owner_frame.as_str());
            }
            if frames.len() > 1 {
                return Err(
                    "Source object lights require more than one construction anchor".into(),
                );
            }
            let frame = frames.first().map(|frame| (*frame).to_owned());
            if let Some(frame_id) = &frame {
                let source = document
                    .frames
                    .iter()
                    .find(|frame| &frame.id == frame_id)
                    .ok_or("Source object light frame absent")?;
                let captured = source
                    .world_from_local
                    .ok_or("Source object light construction pose absent")?;
                verify_anchor(&asset.source_frame, captured)?;
            }
            if assets
                .insert(
                    (asset.asset_id, asset.asset_hash),
                    Binding {
                        frame,
                        lights: ids,
                        empty_attested,
                    },
                )
                .is_some()
            {
                return Err("Source object asset revision is duplicated".into());
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
    ) -> Result<Option<PreparedObjectLights>, String> {
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        if package.receipt_hash() != self.package_revision
            || package.descriptor().content_hash() != self.descriptor_revision
        {
            return Err("Source object light package revision differs".into());
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
    ) -> Result<PreparedObjectLights, String> {
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        if package.receipt_hash() != self.package_revision
            || package.descriptor().content_hash() != self.descriptor_revision
        {
            return Err("Source object light package revision differs".into());
        }
        let binding = self
            .assets
            .get(&(placement.asset_id, placement.asset_hash))
            .ok_or("Source object light asset revision is unknown")?;
        let frame = binding.frame.as_ref().ok_or(
            "Source object has no explicit light binding; emitter absence is not attested",
        )?;
        let prepared = PreparedSourceLights::from_package_selection(
            package,
            placement.asset_id,
            registration_base,
            &binding.lights,
        )?;
        if prepared.owner_frame_ids().collect::<Vec<_>>() != vec![frame.as_str()] {
            return Err("Source object light selection requires another anchor".into());
        }
        Ok(PreparedObjectLights {
            placement: placement.clone(),
            frame: frame.clone(),
            lights: prepared,
        })
    }
}

impl PreparedObjectLights {
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
                "Source object light replacement changes its canonical instance identity".into(),
            );
        }
        let owners = self.live_owners(world)?;
        resident.replace(world, &self.lights, &owners)
    }

    fn live_owners(&self, world: &mut World) -> Result<BTreeMap<String, Entity>, String> {
        let mut query = world.query::<(Entity, &ManualPlacementProjection)>();
        let mut roots = query
            .iter(world)
            .filter(|(_, source)| source.0.id == self.placement.id);
        let (root, source) = roots.next().ok_or("Source object placement root absent")?;
        if roots.next().is_some()
            || source.0 != self.placement
            || world.get::<ChildOf>(root).is_some()
            || world.get::<Transform>(root)
                != Some(&crate::domain_transform_to_bevy(self.placement.transform))
        {
            return Err(
                "Source object light root differs from its unique canonical placement".into(),
            );
        }
        Ok(BTreeMap::from([(self.frame.clone(), root)]))
    }

    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        self.lights.registration_orders()
    }
}

fn verify_anchor(frame: &ConstructionFrame, captured: [f64; 16]) -> Result<(), String> {
    let translation = DVec3::from_array(frame.translation_m);
    let rotation = DQuat::from_array(frame.rotation_xyzw);
    let scale = DVec3::from_array(frame.scale);
    if !translation.is_finite()
        || !rotation.is_finite()
        || !scale.is_finite()
        || (rotation.length_squared() - 1.0).abs() > 1e-12
    {
        return Err("Source object construction pose is nonfinite or non-unit".into());
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
        return Err("Source object geometry and light construction anchors differ".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests;

/// Decode admitted surface-object identities for the common light ownership join.
/// The package reader has already verified descriptor members and their hashes.
pub fn sources_from_surfaces(
    package: &forge_assets::source_surface_package::SurfacePackage,
) -> Result<Vec<SourceObject>, String> {
    #[derive(Deserialize)]
    struct Descriptor {
        building: Building,
    }
    #[derive(Deserialize)]
    struct Building {
        source_id: String,
        source_frame: ConstructionFrame,
    }

    package
        .catalog()
        .assets
        .iter()
        .map(|asset| {
            let bytes = package
                .file(&format!("{}.json", asset.content_hash))
                .ok_or("Building descriptor absent")?;
            let descriptor: Descriptor =
                serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
            if descriptor.building.source_id != asset.logical_name {
                return Err("Building source identity differs".into());
            }
            Ok(SourceObject {
                asset_id: asset.id,
                asset_hash: asset.content_hash,
                source_id: descriptor.building.source_id,
                source_frame: descriptor.building.source_frame,
            })
        })
        .collect()
}
