//! Source-identity joins onto the live lantern pivots of an authored pier.
//! The common placement remains the only mutable document; captures verify
//! anchors and never substitute for the current hierarchy or transform.
use {crate::{source_local_lights::projection::{PreparedSourceLights, ResidentSourceLights}, tidewater_pier::{PierLanternPivot, ResidentPier}}, forge_engine::{ManualPlacementProjection, domain_transform_to_bevy}};
use bevy::{
    math::{DMat4, DVec3},
    prelude::*,
};
use {forge_tidewater_assets::{tidewater_pier::{self as pier, PierPackage}}, forge_assets::{source_local_lights::{SourceLocalLightsPackage, ownership::SourceLightOwnership}}};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Deserialize)]
struct Description {
    pier: SourcePier,
}
#[derive(Deserialize)]
struct SourcePier {
    source_id: String,
    source_frame: Frame,
    lanterns: Vec<Lantern>,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
}
#[derive(Deserialize)]
struct Lantern {
    pivot: [f64; 3],
    rest: [f64; 3],
}

/// Immutable preparation for one canonical placement and its eleven subframes.
pub struct PreparedPierLights {
    placement: ManualPlacement,
    lights: PreparedSourceLights,
    pivots: BTreeMap<usize, (String, Vec3)>,
}

impl PreparedPierLights {
    /// Validate independently acquired geometry and emitter identities before
    /// resolving any live entities. Copies require an explicit disjoint order base.
    ///
    /// # Errors
    /// Rejects unknown revisions, absent identities, changed source anchors,
    /// nonfinite coordinates, duplicate ordinals and registration overflow.
    pub fn from_packages(
        geometry: &PierPackage,
        package: &SourceLocalLightsPackage,
        placement: &ManualPlacement,
        registration_base: u64,
    ) -> Result<Self, String> {
        placement.transform.validate().map_err(|e| e.to_string())?;
        if placement.asset_id != pier::asset_id() || placement.asset_hash != pier::package_hash() {
            return Err("Pier light asset revision differs".into());
        }
        let description: Description =
            serde_json::from_slice(geometry.description()).map_err(|e| e.to_string())?;
        let source = description.pier;
        let document = package.descriptor().document();
        verify_source_frame(&source, &document.source_commit)?;
        let ownership = SourceLightOwnership::from_package(package)?;
        let rows: Vec<_> = ownership
            .records()
            .iter()
            .filter(|r| {
                r.source_binding.producer_path == "src/world/Village.js"
                    && r.source_binding.producer_function.as_deref() == Some("_buildLanterns")
            })
            .collect();
        if rows.len() != 11 {
            return Err("Pier lantern ownership is incomplete".into());
        }
        let mut pivots = BTreeMap::new();
        let mut ids = Vec::new();
        let origin = DVec3::from_array(source.source_frame.translation_m);
        for (ordinal, lantern) in source.lanterns.iter().enumerate() {
            let matches: Vec<_> = rows
                .iter()
                .filter(|row| row.source_binding.ordinal == Some(ordinal))
                .collect();
            if matches.len() != 1 {
                return Err("Pier lantern ordinal is absent or ambiguous".into());
            }
            let row = matches[0];
            let binding = &row.source_binding;
            let frame_id = format!("village-lantern-{ordinal}");
            if binding.collection.as_deref() != Some("lanterns")
                || binding.anchor != "obj"
                || binding.evidence != "live-vector-object-identity"
                || row.owner_frame != frame_id
            {
                return Err("Pier lantern source identity differs".into());
            }
            let frame = document
                .frames
                .iter()
                .find(|f| f.id == frame_id)
                .ok_or("Pier lantern frame absent")?;
            if frame.parent.as_deref() != Some("world")
                || frame.source_object != format!("Village.lanterns[{ordinal}].obj")
            {
                return Err("Pier lantern source hierarchy differs".into());
            }
            let pivot = DVec3::from_array(lantern.pivot);
            let rest = DVec3::from_array(lantern.rest);
            let expected = DMat4::from_translation(origin + pivot).to_cols_array();
            let captured = frame
                .world_from_local
                .ok_or("Pier lantern capture matrix absent")?;
            if expected
                .iter()
                .zip(captured)
                .any(|(a, b)| !a.is_finite() || !b.is_finite() || (a - b).abs() > 1e-12)
            {
                return Err("Pier lantern geometry and emitter anchors differ".into());
            }
            let light = document
                .lights
                .iter()
                .find(|l| l.id == row.light_id)
                .ok_or("Pier lantern emitter absent")?;
            if light.owner_frame != frame_id
                || light.direction_local.is_some()
                || !near(DVec3::from_array(light.position_local_m), rest - pivot)
                || !near(
                    DVec3::from_array(light.acquired_position_world_m),
                    origin + rest,
                )
            {
                return Err("Pier lantern rest centre differs".into());
            }
            pivots.insert(ordinal, (frame_id, pivot.as_vec3()));
            ids.push(row.light_id);
        }
        let lights = PreparedSourceLights::from_package_selection(
            package,
            placement.asset_id,
            registration_base,
            &ids,
        )?;
        if lights.owner_frame_ids().collect::<BTreeSet<_>>()
            != pivots.values().map(|(id, _)| id.as_str()).collect()
        {
            return Err("Pier light preparation requires unrelated frames".into());
        }
        Ok(Self {
            placement: placement.clone(),
            lights,
            pivots,
        })
    }

    /// Attach emitters only to the actual, propagated pivots of this placement.
    ///
    /// # Errors
    /// Rejects missing/ambiguous roots, changed placements, foreign pivots and
    /// conflicts; the generic projector checks everything before spawning.
    pub fn stage(&self, world: &mut World) -> Result<ResidentSourceLights, String> {
        let owners = self.live_owners(world)?;
        self.lights.stage(world, self.placement.id, &owners)
    }

    /// Replace a projection after a common placement edit; refusal retains it.
    ///
    /// # Errors
    /// Rejects a different instance identity or invalid current owners.
    pub fn replace(
        &self,
        world: &mut World,
        resident: &mut ResidentSourceLights,
    ) -> Result<(), String> {
        if resident.instance_id() != self.placement.id {
            return Err("Pier light instance identity differs".into());
        }
        let owners = self.live_owners(world)?;
        resident.replace(world, &self.lights, &owners)
    }

    #[must_use]
    pub fn registration_orders(&self) -> &[u64] {
        self.lights.registration_orders()
    }

    fn live_owners(&self, world: &mut World) -> Result<BTreeMap<String, Entity>, String> {
        let roots: Vec<_> = world
            .query::<(Entity, &ManualPlacementProjection)>()
            .iter(world)
            .filter(|(_, p)| p.0.id == self.placement.id)
            .map(|(e, p)| (e, p.0.clone()))
            .collect();
        if roots.len() != 1 || roots[0].1 != self.placement {
            return Err("Pier light root is absent or ambiguous".into());
        }
        let root = roots[0].0;
        if world.get::<ChildOf>(root).is_some()
            || world.get::<Transform>(root)
                != Some(&domain_transform_to_bevy(self.placement.transform))
            || !world.get_resource::<ResidentPier>().is_some_and(|pier| {
                pier.authored_roots().iter().any(|(e, id, t)| {
                    *e == root && *id == self.placement.id && *t == self.placement.transform
                })
            })
        {
            return Err("Pier light root differs from the resident geometry".into());
        }
        let mut owners = BTreeMap::new();
        for (entity, marker, parent, transform) in world
            .query::<(Entity, &PierLanternPivot, &ChildOf, &Transform)>()
            .iter(world)
        {
            if parent.parent() != root {
                continue;
            }
            let (frame, pivot) = self
                .pivots
                .get(&marker.0)
                .ok_or("Unknown pier lantern ordinal")?;
            if transform.translation != *pivot
                || transform.scale != Vec3::ONE
                || !transform.rotation.is_finite()
                || (transform.rotation.length_squared() - 1.0).abs() > 1e-5
                || owners.insert(frame.clone(), entity).is_some()
            {
                return Err("Pier lantern pivot differs or is duplicated".into());
            }
        }
        if owners.len() != self.pivots.len() {
            return Err("Pier lantern live inventory is incomplete".into());
        }
        Ok(owners)
    }
}

fn near(a: DVec3, b: DVec3) -> bool {
    a.is_finite() && b.is_finite() && (a - b).abs().max_element() < 1e-12
}

#[allow(
    clippy::float_cmp,
    reason = "The hash-pinned import contract requires the exact serialized identity rotation and scale."
)]
fn verify_source_frame(source: &SourcePier, commit: &str) -> Result<(), String> {
    if commit != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        || source.source_id != format!("tidewater:{commit}:pier:0")
        || source.lanterns.len() != 11
        || source.source_frame.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
        || source.source_frame.scale != [1.0, 1.0, 1.0]
    {
        return Err("Pier light source inventory differs".into());
    }
    Ok(())
}
