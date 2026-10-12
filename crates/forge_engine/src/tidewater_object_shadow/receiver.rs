//! One resident buffer publishes the settings of the depth layers actually encoded.
//! Updating it in the depth graph keeps all receivers in the same submission.
use crate::tidewater_material::ObjectShadowSettings;
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

pub(super) fn reset(world: &mut World) -> Result<(), String> {
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

pub(super) fn bytes(mut settings: ObjectShadowSettings, temporal_aa: bool) -> Vec<u8> {
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
    if !source.contains("ObjectShadowSettings") {
        return Ok(source.to_owned());
    }
    if source.matches(ANCHOR).count() != 1 {
        return Err("Shadow receiver declaration anchor drift".into());
    }
    let result = source.replace("shadowParams.", "currentShadowParams().");
    Ok(result.replace(ANCHOR, &format!("{ANCHOR}\nstruct LiveShadowSettings {{ settings:ObjectShadowSettings, control:vec4f }};\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(44) var<storage,read> liveShadow:LiveShadowSettings;\nfn currentShadowParams()->ObjectShadowSettings {{\n    if (liveShadow.control.x>0.5) {{ return liveShadow.settings; }}\n    return shadowParams;\n}}")))
}

#[cfg(test)]
mod tests {
    use super::*;
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
