//! Source-declared local collision geometry. Preparing a shape does not admit
//! simulation materials, layer filtering, contact policy or scene activation.
use bevy::prelude::{Mat4, Quat, Vec3};
use bevy_rapier3d::geometry::RayIntersection;
use bevy_rapier3d::prelude::Collider;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

/// Transient adapter data; the complete original observation remains available.
pub struct PreparedBox {
    pub node: usize,
    pub guid: String,
    pub file_id: String,
    pub center: Vec3,
    pub half_extents: Vec3,
    pub enabled: bool,
    pub is_trigger: bool,
    pub collider: Collider,
    pub original: Value,
}

impl PreparedBox {
    /// Direct shape query in the parent's local target frame, without scene filtering.
    ///
    /// # Errors
    /// Refuses non-finite coordinates.
    pub fn closest_point(&self, point: Vec3) -> Result<Vec3, String> {
        if !point.is_finite() {
            return Err("Source box query is not finite".into());
        }
        Ok(if self.enabled {
            self.collider
                .project_point(self.center, Quat::IDENTITY, point, true)
                .point
        } else {
            point
        })
    }

    /// Direct source box ray query; direction is normalized and distance is in metres.
    ///
    /// # Errors
    /// Refuses a non-finite, zero-length ray or negative distance.
    pub fn raycast(
        &self,
        origin: Vec3,
        direction: Vec3,
        max_distance: f32,
    ) -> Result<Option<RayIntersection>, String> {
        if !origin.is_finite()
            || !direction.is_finite()
            || direction.as_dvec3().length_squared() <= 0.
            || !max_distance.is_finite()
            || max_distance < 0.
        {
            return Err("Invalid source box ray".into());
        }
        Ok(if self.enabled {
            self.collider.cast_ray_and_get_normal(
                self.center,
                Quat::IDENTITY,
                origin,
                // Float32 finite components can overflow or underflow when
                // squared in Float32. Float64 covers their complete range.
                direction.as_dvec3().normalize().as_vec3(),
                max_distance,
                true,
            )
        } else {
            None
        })
    }
}

pub(crate) fn identity(value: &Value) -> Result<(String, String), String> {
    let guid = value["guid"]
        .as_str()
        .ok_or("Collider source GUID absent")?;
    let file = value["file_id"]
        .as_str()
        .ok_or("Collider source file ID absent")?;
    if guid.len() != 32
        || !guid.bytes().all(|v| v.is_ascii_hexdigit())
        || file.parse::<i64>().is_err()
    {
        return Err("Invalid source collider identity".into());
    }
    Ok((guid.into(), file.into()))
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::float_cmp,
    reason = "Require exact finite source float32 observations."
)]
fn vector(value: &Value) -> Result<Vec3, String> {
    if value["property_type"] != "Vector3" {
        return Err("Source collider vector type differs".into());
    }
    let entries = value["vector"]
        .as_array()
        .ok_or("Source collider vector absent")?;
    if entries.len() != 3 {
        return Err("Source collider vector width differs".into());
    }
    let mut result = [0.; 3];
    for (out, entry) in result.iter_mut().zip(entries) {
        let source = entry
            .as_f64()
            .ok_or("Source collider coordinate is not numeric")?;
        *out = source as f32;
        if !out.is_finite() || f64::from(*out) != source {
            return Err("Collider coordinate is not exact finite float32".into());
        }
    }
    Ok(Vec3::from_array(result))
}

pub(crate) fn boolean(value: &Value) -> Result<bool, String> {
    if value["property_type"] != "Boolean" {
        return Err("Source collider bool type differs".into());
    }
    value["boolean"]
        .as_bool()
        .ok_or_else(|| "Source collider bool absent".into())
}

/// Prepare the complete batch before any allocation in a physics World.
/// Only signed unit axis mappings and measured `BoxCollider` observations are
/// admitted here; unsupported shapes or bases are explicit refusals.
///
/// # Errors
/// Refuses incomplete identities, observations, owner binding, geometry or basis.
pub fn prepare(document: &Value, target_from_source: Mat4) -> Result<Vec<PreparedBox>, String> {
    let axes = signed_axes(target_from_source)?;
    prepare_boxes(document, target_from_source, axes)
}

#[allow(
    clippy::float_cmp,
    reason = "Only exact signed unit-axis mappings are admitted; approximate frames are intentionally refused."
)]
fn signed_axes(target_from_source: Mat4) -> Result<[bevy::prelude::Vec4; 3], String> {
    let axes = [
        target_from_source.x_axis,
        target_from_source.y_axis,
        target_from_source.z_axis,
    ];
    if !target_from_source.is_finite()
        || target_from_source.w_axis != bevy::prelude::Vec4::W
        || axes.iter().any(|axis| {
            axis.w != 0.
                || axis.truncate().length_squared() != 1.
                || axis
                    .truncate()
                    .to_array()
                    .iter()
                    .any(|v| ![-1., 0., 1.].contains(v))
        })
        || axes[0].dot(axes[1]) != 0.
        || axes[0].dot(axes[2]) != 0.
        || axes[1].dot(axes[2]) != 0.
    {
        return Err("Source collider basis is not a signed unit axis mapping".into());
    }
    Ok(axes)
}

fn prepare_boxes(
    document: &Value,
    target_from_source: Mat4,
    axes: [bevy::prelude::Vec4; 3],
) -> Result<Vec<PreparedBox>, String> {
    let nodes = document["nodes"]
        .as_array()
        .ok_or("Source collider scene nodes absent")?;
    let mut result = Vec::new();
    let mut seen = BTreeSet::new();
    for (node, value) in nodes.iter().enumerate() {
        let source = &value["extras"]["unity_source_node"];
        let Some(observations) = source["native_collider_observations"].as_array() else {
            if source["colliders"]
                .as_array()
                .is_some_and(|items| !items.is_empty())
            {
                return Err("Authored collider lacks native observation".into());
            }
            continue;
        };
        for original in observations {
            if original["identity"]["type"] != "UnityEngine.BoxCollider" {
                return Err("Source collider shape requires its own adapter".into());
            }
            let (guid, file_id) = identity(&original["identity"])?;
            if source["gameobject"]["guid"].as_str() != Some(guid.as_str()) {
                return Err("Source collider GUID differs from its prefab owner".into());
            }
            if !seen.insert((guid.clone(), file_id.clone())) {
                return Err("Repeated source collider identity".into());
            }
            let mut properties = BTreeMap::new();
            for property in original["serialized"]
                .as_array()
                .ok_or("Collider properties absent")?
            {
                let key = property["property_path"]
                    .as_str()
                    .ok_or("Collider property path absent")?;
                if properties.insert(key, property).is_some() {
                    return Err("Repeated collider property".into());
                }
            }
            let property = |key| {
                properties
                    .get(key)
                    .copied()
                    .ok_or_else(|| format!("Source collider property absent: {key}"))
            };
            if identity(&property("m_GameObject")?["reference"])?
                != identity(&source["gameobject"])?
            {
                return Err("Source collider owner differs from its node".into());
            }
            let center = target_from_source.transform_point3(vector(property("m_Center")?)?);
            let size = vector(property("m_Size")?)?;
            if size.min_element() <= 0. {
                return Err("Source box dimensions must be positive".into());
            }
            let half = size * 0.5;
            let half_extents = axes[0].truncate().abs() * half.x
                + axes[1].truncate().abs() * half.y
                + axes[2].truncate().abs() * half.z;
            if !half_extents.is_finite() || half_extents.min_element() <= 0. {
                return Err("Source box half extents invalid".into());
            }
            let active = source["active_source_parent_chain"]
                .as_bool()
                .ok_or("Collider owner activation absent")?;
            result.push(PreparedBox {
                node,
                guid,
                file_id,
                center,
                half_extents,
                enabled: boolean(property("m_Enabled")?)? && active,
                is_trigger: boolean(property("m_IsTrigger")?)?,
                collider: Collider::cuboid(half_extents.x, half_extents.y, half_extents.z),
                original: original.clone(),
            });
        }
    }
    Ok(result)
}
