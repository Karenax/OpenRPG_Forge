//! Optional live receiver with a uniform path for independently staged probes.
use super::{HANDLE, SceneLighting};
use bevy::{prelude::*, render::storage::ShaderBuffer};

pub const DEFINE: &str = "FORGE_SHARED_SCENE_LIGHTING";

/// The unused storage binding still needs a resident buffer in uniform-only scenes.
/// Never overwrite a scene's acquired seed or its GPU-owned lighting.
pub fn install(world: &mut World) -> Result<(), String> {
    if world
        .get_resource::<Assets<ShaderBuffer>>()
        .is_some_and(|assets| assets.get(&HANDLE).is_some())
    {
        return Ok(());
    }
    super::publish(
        world,
        &SceneLighting::new([0.0, 1.0, 0.0], [0.0; 3], [[0.0; 3]; 9])?,
    )
}

/// Replace exactly one qualified declaration, preserving the existing aligned ABI.
pub fn shader_source(source: &str, lighting_type: &str) -> Result<String, String> {
    let declaration = format!(
        "@group(#{{MATERIAL_BIND_GROUP}}) @binding(18) var<uniform> sceneLighting: {lighting_type};"
    );
    if source.matches(&declaration).count() != 1
        || source.contains("@binding(55)")
        || source.contains(DEFINE)
    {
        return Err("Shared scene lighting requires one unmodified receiver declaration".into());
    }
    let replacement = format!(
        "#ifdef {DEFINE}\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(55) var<storage, read> sceneLighting: {lighting_type};\n#else\n{declaration}\n#endif"
    );
    Ok(source.replace(&declaration, &replacement))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn default_binding_never_resets_published_light() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        install(&mut world).unwrap();
        let light = SceneLighting::new([1.0, 0.0, 0.0], [3.0; 3], [[0.4; 3]; 9]).unwrap();
        super::super::publish(&mut world, &light).unwrap();
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&HANDLE)
            .unwrap()
            .data
            .clone();
        install(&mut world).unwrap();
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&HANDLE)
                .unwrap()
                .data,
            before
        );
    }

    #[test]
    fn duplicate_or_colliding_shader_seams_are_refused() {
        let source =
            "@group(#{MATERIAL_BIND_GROUP}) @binding(18) var<uniform> sceneLighting: Light;";
        let prepared = shader_source(source, "Light").unwrap();
        assert!(shader_source(&prepared, "Light").is_err());
        assert!(shader_source(&source.repeat(2), "Light").is_err());
        assert!(shader_source(&(source.to_owned() + "\n@binding(55)"), "Light").is_err());
        assert!(shader_source(source, "OtherLight").is_err());
    }
}
