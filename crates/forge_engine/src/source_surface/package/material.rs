//! Source material parameters over caller-owned live scene bindings.
use super::MaterialParameters;
use crate::source_surface::{Settings, SourceSurfaceMaterial};
use bevy::prelude::*;
use std::collections::BTreeMap;

/// No asset allocation or scene lookup. All lighting bindings are retained.
pub fn prepare(
    parameters: &MaterialParameters,
    textures: &BTreeMap<String, Handle<Image>>,
    lighting: &SourceSurfaceMaterial,
) -> Result<SourceSurfaceMaterial, String> {
    if parameters.kind > 6
        || parameters.textures.is_empty()
        || parameters.textures.len() > 4
        || parameters
            .color
            .iter()
            .chain(&parameters.emissive)
            .chain(&parameters.surface)
            .any(|value| !value.is_finite())
        || !lighting.settings.wind.is_finite()
        || !lighting.settings.flags.is_finite()
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
    let mut material = lighting.clone();
    material.kind = parameters.kind;
    material.first = resolved[0].clone();
    material.second = resolved[1].clone();
    material.third = resolved[2].clone();
    material.fourth = resolved[3].clone();
    material.settings = Settings {
        wind: lighting.settings.wind,
        color: Vec4::from_array(parameters.color),
        emissive: Vec4::from_array(parameters.emissive),
        surface: Vec4::from_array(parameters.surface),
        flags: lighting.settings.flags,
    };
    Ok(material)
}

/// Build without modifying any asset store. Missing textures fail explicitly.
pub fn prepare_with_lighting(
    parameters: &MaterialParameters,
    textures: &BTreeMap<String, Handle<Image>>,
    lighting: &crate::source_surface::LightingBindings,
) -> Result<SourceSurfaceMaterial, String> {
    // This acquired-scene adapter supplies lighting only. Parameter admission
    // and source texture selection are the same engine path used by modules.
    let template = SourceSurfaceMaterial {
        scene_buffer: crate::source_lighting::scene_buffer::HANDLE.clone(),
        shared_scene_lighting: false,
        local_lights: crate::source_lighting::local_lights::gpu::BUFFER.clone(),
        scene_lighting: lighting.scene_lighting.clone(),
        environment: lighting.environment.clone(),
        hill_shadow: lighting.hill_shadow.clone(),
        object_shadow: lighting.object_shadow.clone(),
        object_settings: lighting.object_settings.clone(),
        live_shadows: crate::source_shadows::receiver::DEFAULT_BUFFER.clone(),
        underwater_params: lighting.caustics.params.clone(),
        underwater_level: lighting.caustics.light_maps.level.clone(),
        underwater_map_frame: lighting.caustics.light_maps.frame.clone(),
        ground_bounce: lighting.ground_bounce.clone(),
        bounce_settings: lighting.bounce_settings,
        clouds: lighting.clouds,
        cloud_shadow: lighting.cloud_shadow.clone(),
        kind: parameters.kind,
        first: Handle::default(),
        second: Handle::default(),
        third: Handle::default(),
        fourth: Handle::default(),
        settings: Settings {
            wind: lighting.wind,
            color: Vec4::from_array(parameters.color),
            emissive: Vec4::from_array(parameters.emissive),
            surface: Vec4::from_array(parameters.surface),
            flags: lighting.flags,
        },
    };
    prepare(parameters, textures, &template)
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::asset::VisitAssetDependencies;

    #[test]
    fn exact_textures_preserve_live_bindings_and_refuse_missing_or_invalid_inputs() {
        let mut images = Assets::<Image>::default();
        let first = images.add(Image::default());
        let third = images.add(Image::default());
        let textures = BTreeMap::from([
            ("paint".into(), first.clone()),
            ("normal".into(), third.clone()),
        ]);
        let mut lighting = crate::source_surface::tests::lighting();
        lighting.settings.wind = Vec4::new(1.0, 0.0, 6.0, 2.0);
        let mut parameters = MaterialParameters {
            kind: 3,
            textures: vec!["paint".into(), "normal".into()],
            color: [0.3; 4],
            emissive: [0.2; 4],
            surface: [0.7; 4],
        };
        let material = prepare(&parameters, &textures, &lighting).unwrap();
        assert_eq!(material.scene_buffer, lighting.scene_buffer);
        assert_eq!(material.object_shadow, lighting.object_shadow);
        assert_eq!(material.underwater_map_frame, lighting.underwater_map_frame);
        assert_eq!(material.settings.wind, lighting.settings.wind);
        assert_eq!(
            [
                material.first.clone(),
                material.second.clone(),
                material.third.clone(),
                material.fourth.clone()
            ],
            [first.clone(), third.clone(), first.clone(), first.clone()]
        );
        let mut dependencies = Vec::new();
        material.visit_dependencies(&mut |id| dependencies.push(id));
        assert!(dependencies.contains(&first.id().untyped()));
        assert!(dependencies.contains(&third.id().untyped()));
        assert!(dependencies.contains(&lighting.scene_buffer.id().untyped()));
        parameters.textures.push("missing".into());
        assert!(prepare(&parameters, &textures, &lighting).is_err());
        parameters.textures.pop();
        parameters.color[0] = f32::NAN;
        assert!(prepare(&parameters, &textures, &lighting).is_err());
    }
}
