//! One resident buffer publishes the settings of the depth layers actually encoded.
//! Updating it in the depth graph keeps all receivers in the same submission.
use crate::source_shadows::ObjectShadowSettings;
use bevy::{
    prelude::*,
    render::{render_resource::ShaderType, storage::ShaderBuffer},
};

pub const DEFAULT_BUFFER: Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("6df95209-e769-4226-a88b-35c1de084744");

#[path = "receiver_observation.rs"]
mod observation;
pub use observation::{GpuObservation, observe_while_held};

#[derive(Clone, Debug, ShaderType)]
struct LiveSettings {
    settings: ObjectShadowSettings,
    control: Vec4,
}

pub fn install(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Shadow receivers require shader buffers")?;
    if buffers.get(DEFAULT_BUFFER.id()).is_none() {
        buffers
            .insert(DEFAULT_BUFFER.id(), disabled())
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

/// Reset the shared receiver when its scene graph retires the live generation.
pub fn reset(world: &mut World) -> Result<(), String> {
    install(world)?;
    world
        .resource_mut::<Assets<ShaderBuffer>>()
        .insert(DEFAULT_BUFFER.id(), disabled())
        .map_err(|e| e.to_string())?;
    Ok(())
}

fn disabled() -> ShaderBuffer {
    let mut buffer = ShaderBuffer::from(LiveSettings {
        settings: ObjectShadowSettings::default(),
        control: Vec4::ZERO,
    });
    buffer.buffer_description.usage |= bevy::render::render_resource::BufferUsages::COPY_SRC;
    buffer
}

/// The depth encoder publishes these bytes in the submission that owns the layers.
#[must_use]
pub fn bytes(mut settings: ObjectShadowSettings, temporal_aa: bool) -> Vec<u8> {
    // PCSS rotates its sample disk using this receiver-only index. Without
    // temporal accumulation, changing it creates visible noise on static roofs.
    // Keep the caster animation and cascade refresh cadence independent.
    if !temporal_aa {
        settings.frame_index = 0;
    }
    ShaderBuffer::from(LiveSettings {
        settings,
        control: Vec4::X,
    })
    .data
    .expect("initialized receiver buffer")
}

/// Preserve shaders without object shadows and the exact disabled source path.
pub fn shader_source(source: &str) -> Result<String, String> {
    const ANCHOR: &str = "@group(#{MATERIAL_BIND_GROUP}) @binding(17) var<uniform> shadowParams:ObjectShadowSettings;";
    if source.contains("@binding(44)") || source.contains("struct LiveShadowSettings") {
        return Err("Shadow receiver binding already occupied".into());
    }
    let source = disabled_cloud_transmission(source)?;
    if !source.contains("ObjectShadowSettings") {
        return Ok(source);
    }
    if source.matches(ANCHOR).count() != 1 {
        return Err("Shadow receiver declaration anchor drift".into());
    }
    let result = source.replace("shadowParams.", "currentShadowParams().");
    Ok(result.replace(ANCHOR, &format!("{ANCHOR}\nstruct LiveShadowSettings {{ settings:ObjectShadowSettings, control:vec4f }};\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(44) var<storage,read> liveShadow:LiveShadowSettings;\nfn currentShadowParams()->ObjectShadowSettings {{\n    if (liveShadow.control.x>0.5) {{ return liveShadow.settings; }}\n    return shadowParams;\n}}")))
}

fn disabled_cloud_transmission(source: &str) -> Result<String, String> {
    const ENTRY: &str = "fn cloudsShadow( worldXZ: vec2f ) -> f32 {";
    const GUARD: &str = "if ( cloudsParams.shadowStrength == 0.0 ) { return 1.0; }";
    if !source.contains("fn cloudsShadow(") {
        return Ok(source.to_owned());
    }
    if source.matches(ENTRY).count() != 1 {
        return Err("Cloud shadow receiver declaration anchor drift".into());
    }
    if source.contains(GUARD) {
        return Ok(source.to_owned());
    }
    // A disabled source has zero strength and may have no allocated shadow map
    // or extent. Mixing an invalid sample with zero strength can still yield NaN.
    // Positive strengths retain the original source coordinate and sampling path.
    Ok(source.replacen(ENTRY, &format!("{ENTRY}\n\t{GUARD}"), 1))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn disabled_cloud_receiver_guards_sampling_and_preserves_the_active_body() {
        let source = "fn cloudsShadow( worldXZ: vec2f ) -> f32 {\n\tlet uv = worldXZ / cloudsParams.shadowSize;\n\treturn sampleOriginal(uv);\n}";
        let derived = shader_source(source).unwrap();
        assert!(
            derived.find("shadowStrength == 0.0").unwrap()
                < derived.find("worldXZ / cloudsParams.shadowSize").unwrap()
        );
        assert!(derived.ends_with(
            "let uv = worldXZ / cloudsParams.shadowSize;\n\treturn sampleOriginal(uv);\n}"
        ));
        assert_eq!(shader_source(&derived).unwrap(), derived);
        assert!(shader_source(&source.replace("worldXZ: vec2f", "other: vec2f")).is_err());
        assert!(shader_source(&(source.to_owned() + source)).is_err());
    }
    #[test]
    fn storage_layout_matches_source_and_keeps_disabled_path() {
        let initial = disabled().data.unwrap();
        assert_eq!(initial.len(), 464);
        assert_eq!(&initial[448..], &[0; 16]);
        let live = bytes(
            ObjectShadowSettings {
                frame_index: 37,
                ..default()
            },
            true,
        );
        assert_eq!(live.len(), 464);
        assert_eq!(&live[412..416], &37_u32.to_le_bytes());
        assert_eq!(&live[448..452], &1_f32.to_le_bytes());
    }
    #[test]
    fn unaccumulated_shadow_filter_is_stable_across_frames() {
        let settings = |frame_index| ObjectShadowSettings {
            frame_index,
            ..default()
        };
        assert_eq!(bytes(settings(1), false), bytes(settings(37), false));
        assert_eq!(&bytes(settings(37), false)[412..416], &0_u32.to_le_bytes());
        assert_ne!(bytes(settings(1), true), bytes(settings(37), true));
        let stable = bytes(settings(37), false);
        let temporal = bytes(settings(37), true);
        assert_eq!(&stable[..412], &temporal[..412]);
        assert_eq!(&stable[416..], &temporal[416..]);
    }
    #[test]
    fn derives_receiver_reads_and_refuses_drift_or_double_adaptation() {
        let source = "struct ObjectShadowSettings {};\n@group(#{MATERIAL_BIND_GROUP}) @binding(17) var<uniform> shadowParams:ObjectShadowSettings;\nfn value()->u32 {return shadowParams.count;}";
        let derived = shader_source(source).unwrap();
        assert!(derived.contains("return currentShadowParams().count;"));
        assert!(derived.contains("return shadowParams;"));
        assert!(shader_source(&derived).is_err());
        assert!(shader_source("struct ObjectShadowSettings {};").is_err());
        assert_eq!(
            shader_source("// no shadow filter").unwrap(),
            "// no shadow filter"
        );
    }
}
