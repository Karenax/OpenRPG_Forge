//! Standard glTF light transport on retained source nodes. Native Bevy shadow
//! parameters require explicit host admission; they do not qualify HDRP parity.
use bevy::{ecs as bevy_ecs, prelude::*};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Component, Clone, Copy)]
pub(super) struct NativeSourceLightTransport;

#[derive(Component, Clone)]
pub(super) struct OriginalSourceLight {
    pub definition: Value,
    pub source: Option<Value>,
    pub renderer_parity: bool,
}

enum PreparedLight {
    Point(PointLight),
    Spot(SpotLight),
    Directional(DirectionalLight),
}
pub(super) struct LightPlan(Vec<(usize, PreparedLight, OriginalSourceLight)>);

fn number(value: &Value, default: Option<f32>) -> Result<f32, String> {
    let number = if value.is_null() {
        default.ok_or("Required light number absent")?
    } else {
        super::source_graph_context::number(value, "Light number")?
    };
    if !number.is_finite() || number < 0.0 {
        return Err("Light number must be finite and nonnegative".into());
    }
    Ok(number)
}

/// Decode before allocating renderer assets or attaching any entities. Unknown
/// light kinds and incomplete source shadow declarations are errors, never drops.
pub(super) fn prepare(
    document: &Value,
    selected_nodes: &BTreeSet<usize>,
    native_transport: bool,
) -> Result<LightPlan, String> {
    let definitions = document.pointer("/extensions/KHR_lights_punctual/lights");
    let definitions = definitions
        .map(|v| v.as_array().ok_or("Invalid KHR light array"))
        .transpose()?
        .map_or(&[][..], Vec::as_slice);
    if definitions.len() > 65_536 {
        return Err("KHR light allocation limit".into());
    }
    let nodes = document["nodes"]
        .as_array()
        .ok_or("Light scene nodes absent")?;
    let mut result = Vec::new();
    for index in selected_nodes {
        let node = nodes.get(*index).ok_or("Selected light node absent")?;
        let Some(reference) = node.pointer("/extensions/KHR_lights_punctual") else {
            continue;
        };
        let light_index = reference["light"]
            .as_u64()
            .and_then(|v| usize::try_from(v).ok())
            .ok_or("Invalid KHR light reference")?;
        let definition = definitions
            .get(light_index)
            .ok_or("KHR light reference out of range")?;
        let prepared = prepare_light(definition, native_transport)?;
        let source = definition.pointer("/extras/OPENRPG_source_light");
        result.push((
            *index,
            prepared,
            OriginalSourceLight {
                definition: definition.clone(),
                source: source.cloned(),
                renderer_parity: false,
            },
        ));
    }
    Ok(LightPlan(result))
}

impl LightPlan {
    pub fn apply(self, world: &mut World, nodes: &BTreeMap<usize, Entity>) -> Result<(), String> {
        // All ownership checks happen before the first light component changes.
        for (index, _, _) in &self.0 {
            let entity = *nodes.get(index).ok_or("Attached light node absent")?;
            if world.get_entity(entity).is_err()
                || world.get::<PointLight>(entity).is_some()
                || world.get::<SpotLight>(entity).is_some()
                || world.get::<DirectionalLight>(entity).is_some()
                || world.get::<OriginalSourceLight>(entity).is_some()
            {
                return Err("Attached light node has conflicting light ownership".into());
            }
        }
        for (index, prepared, original) in self.0 {
            let mut node = world.entity_mut(nodes[&index]);
            node.insert(original);
            match prepared {
                PreparedLight::Point(light) => {
                    node.insert(light);
                }
                PreparedLight::Spot(light) => {
                    node.insert(light);
                }
                PreparedLight::Directional(light) => {
                    node.insert(light);
                }
            }
        }
        Ok(())
    }
}

fn prepare_light(definition: &Value, native_transport: bool) -> Result<PreparedLight, String> {
    let mut rgb = [1.; 3];
    if let Some(color) = definition.get("color") {
        let values = color.as_array().ok_or("Invalid KHR light color")?;
        if values.len() != 3 {
            return Err("KHR light color requires three channels".into());
        }
        for (channel, value) in rgb.iter_mut().zip(values) {
            *channel = number(value, None)?;
            if *channel > 1. {
                return Err("KHR light color outside linear unit range".into());
            }
        }
    }
    let mut intensity = number(&definition["intensity"], Some(1.))?;
    let range = number(&definition["range"], Some(20.))?;
    if range == 0. {
        return Err("KHR light range must be positive".into());
    }
    let color = Color::linear_rgb(rgb[0], rgb[1], rgb[2]);
    let source = definition.pointer("/extras/OPENRPG_source_light");
    let (shadows, contact, radius) = if let Some(source) = source {
        if source["schema_version"].as_u64() != Some(1) || !native_transport {
            return Err("Source light requires explicit native transport admission".into());
        }
        let enabled = source["enabled"]
            .as_bool()
            .ok_or("Source light enabled state absent")?;
        if !enabled {
            intensity = 0.;
        }
        (
            source["shadow_maps_enabled"]
                .as_bool()
                .ok_or("Source light shadow state absent")?,
            source["contact_shadows_enabled"]
                .as_bool()
                .ok_or("Source contact shadow state absent")?,
            number(&source["radius_m"], None)?,
        )
    } else {
        (false, false, 0.)
    };
    // Match the pinned Bevy glTF loader: punctual intensity is candela,
    // while Bevy PointLight/SpotLight intensity is luminous power (lumens).
    let lumens = intensity * (4. * std::f32::consts::PI);
    if !lumens.is_finite() {
        return Err("KHR punctual light photometry overflow".into());
    }
    let prepared = match definition["type"].as_str() {
        Some("point") => PreparedLight::Point(PointLight {
            color,
            intensity: lumens,
            range,
            radius,
            shadow_maps_enabled: shadows,
            contact_shadows_enabled: contact,
            ..default()
        }),
        Some("spot") => {
            let inner = number(&definition["spot"]["innerConeAngle"], Some(0.))?;
            let outer = number(
                &definition["spot"]["outerConeAngle"],
                Some(std::f32::consts::FRAC_PI_4),
            )?;
            if outer <= 0. || outer > std::f32::consts::FRAC_PI_2 || inner >= outer {
                return Err("Invalid KHR spot cone".into());
            }
            PreparedLight::Spot(SpotLight {
                color,
                intensity: lumens,
                range,
                radius,
                inner_angle: inner,
                outer_angle: outer,
                shadow_maps_enabled: shadows,
                contact_shadows_enabled: contact,
                ..default()
            })
        }
        Some("directional") => PreparedLight::Directional(DirectionalLight {
            color,
            illuminance: intensity,
            shadow_maps_enabled: shadows,
            contact_shadows_enabled: contact,
            ..default()
        }),
        _ => return Err("Unknown KHR light kind".into()),
    };
    Ok(prepared)
}

#[cfg(test)]
mod tests {
    use super::*;
    fn fixture() -> Value {
        serde_json::json!({"nodes":[{"extensions":{"KHR_lights_punctual":{"light":0}}}],
        "extensions":{"KHR_lights_punctual":{"lights":[{"type":"point","color":[1.,0.32,0.],"intensity":19.894367,"range":1.,
        "extras":{"OPENRPG_source_light":{"schema_version":1,"enabled":true,"shadow_maps_enabled":true,"contact_shadows_enabled":false,"radius_m":0.01}}}]}}})
    }
    #[test]
    fn light_transport_preserves_photometry_and_attaches_to_existing_node() {
        let doc = fixture();
        let selected = BTreeSet::from([0]);
        assert!(prepare(&doc, &selected, false).is_err());
        let plan = prepare(&doc, &selected, true).unwrap();
        let mut world = World::new();
        let node = world
            .spawn((Transform::from_xyz(2., 3., 4.), GlobalTransform::default()))
            .id();
        let before = world.entities().len();
        plan.apply(&mut world, &BTreeMap::from([(0, node)]))
            .unwrap();
        assert_eq!(world.entities().len(), before);
        let light = world.get::<PointLight>(node).unwrap();
        assert!((light.intensity - 250.).abs() < 0.001);
        assert_eq!(light.range, 1.);
        assert_eq!(light.radius, 0.01);
        assert!(light.shadow_maps_enabled);
        assert_eq!(
            world.get::<Transform>(node).unwrap().translation,
            Vec3::new(2., 3., 4.)
        );
        assert!(
            !world
                .get::<OriginalSourceLight>(node)
                .unwrap()
                .renderer_parity
        );
    }
    #[test]
    fn invalid_lights_and_conflicts_refuse_without_partial_attachment() {
        let selected = BTreeSet::from([0]);
        let mut doc = fixture();
        doc["extensions"]["KHR_lights_punctual"]["lights"][0]["range"] = serde_json::json!(0.);
        assert!(prepare(&doc, &selected, true).is_err());
        let plan = prepare(&fixture(), &selected, true).unwrap();
        let mut world = World::new();
        let node = world.spawn(PointLight::default()).id();
        assert!(
            plan.apply(&mut world, &BTreeMap::from([(0, node)]))
                .is_err()
        );
        assert!(world.get::<OriginalSourceLight>(node).is_none());
    }
}
