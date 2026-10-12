//! Resolve measured material values with an explicit target scene policy.
//! Source contact offset is retained as evidence, never treated as contact skin.
//! This adapter does not qualify native simulation or original-project settings.
use crate::{source_collider_projection::ContactPolicy, source_colliders};
use bevy_rapier3d::prelude::{
    CoefficientCombineRule, CollisionGroups, ContactSkin, Friction, Group, Restitution,
    SolverGroups,
};
use forge_assets::source_physics_transport::PhysicsTransportComponent;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::collections::BTreeMap;

/// Caller-owned settings for the target scene; no default matrix or skin.
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ContactSceneSettings {
    pub layer_matrix: [u32; 32],
    pub contact_skin_m: f32,
    pub evidence: String,
}

fn scalar(value: &Value, key: &str) -> Result<f32, String> {
    let number = value[key]
        .as_f64()
        .ok_or_else(|| format!("Contact {key} absent"))?;
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Exact Float32 round trip checked below"
    )]
    let result = number as f32;
    if !result.is_finite() || result < 0. || f64::from(result).to_bits() != number.to_bits() {
        return Err(format!("Contact {key} invalid or not exact Float32"));
    }
    Ok(result)
}

fn properties(original: &Value) -> Result<BTreeMap<&str, &Value>, String> {
    let mut result = BTreeMap::new();
    for property in original["serialized"]
        .as_array()
        .ok_or("Contact properties absent")?
    {
        let path = property["property_path"]
            .as_str()
            .ok_or("Contact property path absent")?;
        if result.insert(path, property).is_some() {
            return Err("Duplicate contact property".into());
        }
    }
    Ok(result)
}

/// Resolve the whole batch before any collider is allocated. Unsupported
/// material rules and per-collider layer overrides are retained as refusals.
pub fn contacts_from_transport(
    components: &[PhysicsTransportComponent],
    settings: &ContactSceneSettings,
) -> Result<BTreeMap<(String, String), ContactPolicy>, String> {
    if settings.evidence.trim().is_empty()
        || !settings.contact_skin_m.is_finite()
        || settings.contact_skin_m < 0.
    {
        return Err("Explicit target contact settings invalid".into());
    }
    for (layer, mask) in settings.layer_matrix.iter().enumerate() {
        for (other, other_mask) in settings.layer_matrix.iter().enumerate() {
            if ((mask >> other) & 1) != ((other_mask >> layer) & 1) {
                return Err("Target layer matrix is asymmetric".into());
            }
        }
    }
    let mut result = BTreeMap::new();
    for component in components {
        let identity = source_colliders::identity(&component.original["identity"])?;
        let Some(metrics) = &component.native_metrics else {
            if component.evidence["source_binding"]["kind"] != "unassigned_mesh" {
                return Err("Contact metrics absent for assigned shape".into());
            }
            continue;
        };
        let properties = properties(&component.original)?;
        for key in [
            "m_IncludeLayers.m_Bits",
            "m_ExcludeLayers.m_Bits",
            "m_LayerOverridePriority",
        ] {
            if properties.get(key).and_then(|p| p["number"].as_f64()) != Some(0.) {
                return Err(
                    "Source layer overrides require a dedicated pair-policy adapter".into(),
                );
            }
        }
        let material = properties
            .get("m_Material")
            .ok_or("Source material reference absent")?;
        if ["guid", "file_id", "path", "type"]
            .iter()
            .any(|key| material["reference"][key].as_str() != Some(""))
        {
            return Err("Explicit source physics material requires its measured transport".into());
        }
        let measured = &metrics["implicit_material"];
        if measured["original_reference_null"] != true
            || measured["friction_combine"] != "Average"
            || measured["bounce_combine"] != "Average"
        {
            return Err("Source material combine rule unsupported".into());
        }
        let friction = scalar(measured, "dynamic_friction")?;
        if friction.to_bits() != scalar(measured, "static_friction")?.to_bits() {
            return Err("Distinct static/dynamic friction requires a dedicated adapter".into());
        }
        let restitution = scalar(measured, "bounciness")?;
        let source_offset = scalar(metrics, "contact_offset")?;
        let layer = metrics["game_object_layer"]
            .as_u64()
            .ok_or("Source layer absent")?;
        let layer = usize::try_from(layer).map_err(|_| "Source layer invalid")?;
        let mask = *settings
            .layer_matrix
            .get(layer)
            .ok_or("Source layer outside 0..31")?;
        let membership = Group::from_bits_retain(1u32 << layer);
        let filter = Group::from_bits_retain(mask);
        let policy = ContactPolicy {
            friction: Friction {
                coefficient: friction,
                combine_rule: CoefficientCombineRule::Average,
            },
            restitution: Restitution {
                coefficient: restitution,
                combine_rule: CoefficientCombineRule::Average,
            },
            contact_skin: ContactSkin(settings.contact_skin_m),
            collision_groups: CollisionGroups::new(membership, filter),
            solver_groups: SolverGroups::new(membership, filter),
            evidence: format!(
                "{}; measured material, source contact offset {source_offset}; target skin {}; native simulation unqualified",
                settings.evidence, settings.contact_skin_m
            ),
        };
        policy.validate()?;
        if result.insert(identity, policy).is_some() {
            return Err("Duplicate source contact identity".into());
        }
    }
    Ok(result)
}
