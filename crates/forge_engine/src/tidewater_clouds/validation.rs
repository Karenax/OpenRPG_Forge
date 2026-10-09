//! Validate transported cloud data before allocating GPU resources.
use super::Input;
fn shader(s: &str) -> bool {
    !s.trim().is_empty() && s.len() <= 2_000_000
}
fn floats(bytes: &[u8], size: usize) -> bool {
    bytes.len() == size
        && bytes
            .as_chunks::<4>()
            .0
            .iter()
            .all(|v| f32::from_le_bytes(*v).is_finite())
}
pub(crate) fn validate(
    input: &Input,
    count: usize,
    atmosphere: Option<&[bevy::math::Vec4; 4]>,
) -> Result<(), String> {
    if !floats(&input.settings, 208) || !input.shaders.iter().all(|s| shader(s)) {
        return Err("invalid cloud settings or kernels".into());
    }
    if let Some(a) = atmosphere
        && a.iter().any(|v| !v.is_finite())
    {
        return Err("invalid shared cloud atmosphere".into());
    }
    let Some(shadow) = &input.shadow else {
        return if count == 2 {
            Ok(())
        } else {
            Err("weather-only cloud output count".into())
        };
    };
    if !shader(&shadow.shader)
        || shadow.noise.len() != 7
        || !(1..=256).contains(&shadow.frames.len())
        || shadow
            .noise
            .iter()
            .enumerate()
            .any(|(level, b)| b.len() != 4 * (64_usize >> level).pow(3))
        || shadow
            .frames
            .iter()
            .any(|(local, global)| !floats(local, 544) || global.len() != 720)
    {
        return Err("invalid cloud shadow inputs".into());
    }
    if shadow
        .schedule
        .as_ref()
        .map_or(shadow.frames.len() != 120, |schedule| {
            schedule.len() != shadow.frames.len()
                || schedule.iter().any(|frame| frame.view_index > 1)
        })
    {
        return Err("invalid or missing acquired cloud schedule".into());
    }
    if let Some(frames) = &shadow.atmosphere_frames {
        let Some(base) = atmosphere else {
            return Err("acquired observer sequence requires a shared atmosphere".into());
        };
        let baseline: Vec<_> = base
            .iter()
            .flat_map(bevy::math::Vec4::to_array)
            .flat_map(f32::to_le_bytes)
            .collect();
        if !input.stepped
            || frames.len() != shadow.frames.len()
            || frames
                .iter()
                .any(|params| !crate::tidewater_atmosphere::observer::validate(params, &baseline))
        {
            return Err("invalid acquired atmosphere observer sequence".into());
        }
    }
    let Some(trace) = &shadow.trace else {
        return if count == 3 {
            Ok(())
        } else {
            Err("shadow-only cloud output count".into())
        };
    };
    if !shader(&trace.shader)
        || !floats(&trace.atmosphere, 64)
        || trace.textures.len() != 3
        || trace.temporal.is_some() != trace.panorama.is_some()
        || trace.temporal.as_ref().is_some_and(|s| !shader(s))
        || trace.panorama.as_ref().is_some_and(|s| !shader(s))
        || count != if trace.temporal.is_some() { 10 } else { 5 }
    {
        return Err("invalid cloud trace inputs or output count".into());
    }
    for (i, size) in [256 * 64 * 8, 192 * 108 * 8, 4096].into_iter().enumerate() {
        let bytes = &trace.textures[i];
        if i < 2 && atmosphere.is_some() && bytes.is_empty() {
            continue;
        }
        if bytes.len() != size
            || (i < 2
                && bytes
                    .as_chunks::<2>()
                    .0
                    .iter()
                    .any(|v| u16::from_le_bytes(*v) & 0x7c00 == 0x7c00))
        {
            return Err("invalid cloud atmosphere texture or blue noise".into());
        }
    }
    if let Some(a) = atmosphere {
        let bytes: Vec<_> = a
            .iter()
            .flat_map(bevy::math::Vec4::to_array)
            .flat_map(f32::to_le_bytes)
            .collect();
        if bytes != trace.atmosphere {
            return Err("cloud atmosphere parameters differ".into());
        }
    }
    Ok(())
}
