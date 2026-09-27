//! Material construction shared by the scene renderer and package consumers.
use super::MaterialParameters;
use crate::tidewater_material::{ObjectShadowSettings, SceneLighting, Settings, VillageMaterial};
use bevy::prelude::*;
use std::collections::BTreeMap;

/// Runtime-owned lighting resources, never serialized into an asset descriptor.
pub struct LightingBindings {
    pub scene_lighting: SceneLighting,
    pub environment: Handle<Image>,
    pub hill_shadow: Handle<Image>,
    pub object_shadow: Handle<Image>,
    pub object_settings: ObjectShadowSettings,
    pub clouds: [Vec4; 5],
    pub cloud_shadow: Option<Handle<Image>>,
    pub wind: Vec4,
    pub flags: Vec4,
}

/// Build without modifying any asset store. Missing textures fail explicitly.
pub fn prepare(
    parameters: &MaterialParameters,
    textures: &BTreeMap<String, Handle<Image>>,
    lighting: &LightingBindings,
) -> Result<VillageMaterial, String> {
    if parameters.kind > 6
        || parameters.textures.is_empty()
        || parameters.textures.len() > 4
        || parameters
            .color
            .iter()
            .chain(&parameters.emissive)
            .chain(&parameters.surface)
            .any(|value| !value.is_finite())
        || !lighting.wind.is_finite()
        || !lighting.flags.is_finite()
    {
        return Err("Invalid building material parameters".into());
    }
    let mut resolved = parameters
        .textures
        .iter()
        .map(|name| {
            textures
                .get(name)
                .cloned()
                .ok_or_else(|| format!("Missing building texture: {name}"))
        })
        .collect::<Result<Vec<_>, _>>()?;
    resolved.resize(4, resolved[0].clone());
    Ok(VillageMaterial {
        scene_lighting: lighting.scene_lighting.clone(),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        kind: parameters.kind,
        first: resolved[0].clone(),
        second: resolved[1].clone(),
        third: resolved[2].clone(),
        fourth: resolved[3].clone(),
        settings: Settings {
            wind: lighting.wind,
            color: Vec4::from_array(parameters.color),
            emissive: Vec4::from_array(parameters.emissive),
            surface: Vec4::from_array(parameters.surface),
            flags: lighting.flags,
        },
    })
}
