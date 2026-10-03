//! Shared live wind state for source-compatible visible vegetation.
use bevy::{
    prelude::{Assets, Vec4, World},
    render::{render_resource::ShaderType, storage::ShaderBuffer},
};

pub const DEFAULT_BUFFER: bevy::asset::Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("a9b97320-2f48-4d8b-91f4-2e088f326b43");

#[derive(Clone, Copy, Debug, ShaderType)]
struct LiveMotion {
    wind_time: Vec4,
    gust_frame: Vec4,
    /// `x` selects live motion; zero preserves the exact source material fields.
    control: Vec4,
}

impl LiveMotion {
    const DISABLED: Self = Self {
        wind_time: Vec4::ZERO,
        gust_frame: Vec4::ZERO,
        control: Vec4::ZERO,
    };
}

/// Install the fixed-size shared storage buffer once. Existing live data is retained.
pub fn install(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Plant motion requires shader buffers")?;
    if buffers.get(DEFAULT_BUFFER.id()).is_none() {
        buffers
            .insert(
                DEFAULT_BUFFER.id(),
                ShaderBuffer::from(LiveMotion::DISABLED),
            )
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

/// Replace the single live vegetation pose without modifying any material asset.
pub fn update(world: &mut World, wind_time: Vec4, gust_frame: Vec4) -> Result<(), String> {
    if !wind_time.is_finite() || !gust_frame.is_finite() {
        return Err("Invalid live plant motion".into());
    }
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Plant motion requires shader buffers")?;
    let mut buffer = buffers
        .get_mut(DEFAULT_BUFFER.id())
        .ok_or("Plant motion buffer absent")?;
    buffer.set_data(LiveMotion {
        wind_time,
        gust_frame,
        control: Vec4::X,
    });
    Ok(())
}

/// Add binding 43 and select live values only after an explicit [`update`].
pub fn shader_source(source: &str) -> Result<String, String> {
    if source.is_empty()
        || source.len() > 1_000_000
        || source.contains("@binding(43)")
        || source.contains("struct LivePlantMotion")
    {
        return Err("Invalid plant motion source".into());
    }
    let mut result = source.replace("\r\n", "\n");
    let declaration = "@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> probe: ProbeParams;";
    if result.matches(declaration).count() != 4 {
        return Err("Plant motion declaration anchor drift".into());
    }
    result = result.replace(
        declaration,
        "struct LivePlantMotion { wind_time:vec4f, gust_frame:vec4f, control:vec4f };\n@group(#{MATERIAL_BIND_GROUP}) @binding(43) var<storage,read> motion:LivePlantMotion;\n@group(#{MATERIAL_BIND_GROUP}) @binding(0) var<uniform> probe: ProbeParams;",
    );
    for (anchor, replacement) in [
        (
            "    frame.time=probe.wind.w;",
            "    frame.time=select(probe.wind.w,motion.wind_time.w,motion.control.x>0.5);",
        ),
        (
            "    frame.windDir=probe.wind.xy;",
            "    frame.windDir=select(probe.wind.xy,motion.wind_time.xy,motion.control.x>0.5);",
        ),
        (
            "    frame.windSpeed=probe.wind.z;",
            "    frame.windSpeed=select(probe.wind.z,motion.wind_time.z,motion.control.x>0.5);",
        ),
        (
            "    frame.frameIndex=u32(probe.lodEndFrame.y);",
            "    frame.frameIndex=select(u32(probe.lodEndFrame.y),u32(motion.gust_frame.z),motion.control.x>0.5);",
        ),
        (
            "    vegParams.gustOffset=probe.gustLod.xy;",
            "    vegParams.gustOffset=select(probe.gustLod.xy,motion.gust_frame.xy,motion.control.x>0.5);",
        ),
    ] {
        if result.matches(anchor).count() != 4 {
            return Err(format!("Plant motion adapter drift: {anchor}"));
        }
        result = result.replace(anchor, replacement);
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn source(count: usize) -> String {
        let setup = "    frame.time=probe.wind.w;\r\n    frame.windDir=probe.wind.xy;\r\n    frame.windSpeed=probe.wind.z;\r\n    frame.frameIndex=u32(probe.lodEndFrame.y);\r\n    vegParams.gustOffset=probe.gustLod.xy;\r\n";
        format!("struct ProbeParams {{}};\r\n@group(#{{MATERIAL_BIND_GROUP}}) @binding(0) var<uniform> probe: ProbeParams;\r\n{setup}").repeat(count)
    }

    #[test]
    fn derives_all_source_setup_paths_without_changing_disabled_values() {
        let derived = shader_source(&source(4)).expect("valid source anchors");
        assert!(!derived.contains('\r'));
        assert_eq!(derived.matches("@binding(43)").count(), 4);
        assert_eq!(
            derived
                .matches("select(probe.wind.w,motion.wind_time.w")
                .count(),
            4
        );
        assert_eq!(
            derived
                .matches("select(probe.gustLod.xy,motion.gust_frame.xy")
                .count(),
            4
        );
    }

    #[test]
    fn rejects_incomplete_source_setup_paths() {
        assert!(shader_source(&source(3)).is_err());
    }

    #[test]
    fn rejects_nonfinite_motion_without_replacing_the_buffer_data() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        install(&mut world).expect("install fixed buffer");
        update(&mut world, Vec4::ONE, Vec4::new(2.0, 3.0, 4.0, 5.0)).expect("finite pose");
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(DEFAULT_BUFFER.id())
            .expect("installed buffer")
            .data
            .clone();
        assert!(update(&mut world, Vec4::NAN, Vec4::ZERO).is_err());
        let after = &world
            .resource::<Assets<ShaderBuffer>>()
            .get(DEFAULT_BUFFER.id())
            .expect("installed buffer")
            .data;
        assert_eq!(before.as_ref().map(Vec::len), Some(48));
        assert_eq!(after, &before);
    }
}
