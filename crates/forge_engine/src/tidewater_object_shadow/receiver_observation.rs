//! Opt-in readback of the actual live receiver buffer during a held comparison.
//! A depth-pass counter alone cannot establish the settings seen by the GPU.
use super::DEFAULT_BUFFER;
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};

#[derive(Clone, Debug)]
pub struct Sample {
    pub enabled: f32,
    pub control: f32,
    pub count: u32,
    pub map_size: f32,
    pub frame_index: u32,
    pub camera: [f32; 4],
    pub forward: [f32; 4],
    pub matrices: [[f32; 16]; 3],
    pub cascades: [[f32; 4]; 3],
}
/// Asynchronous observation; it can lag the live render and is not a fence.
#[derive(Resource, Default)]
pub struct GpuObservation(pub Option<Sample>);
#[derive(Component)]
struct Probe;

/// Use only while the view and source clocks are held. No permanent readback cost.
pub fn observe_while_held(world: &mut World, held: bool) {
    let probes: Vec<_> = world
        .query_filtered::<Entity, With<Probe>>()
        .iter(world)
        .collect();
    if !held {
        for entity in probes {
            world.despawn(entity);
        }
        if let Some(mut observation) = world.get_resource_mut::<GpuObservation>() {
            observation.0 = None;
        }
        return;
    }
    if !probes.is_empty() {
        return;
    }
    world.init_resource::<GpuObservation>();
    world.resource_mut::<GpuObservation>().0 = None;
    world
        .spawn((Readback::buffer(DEFAULT_BUFFER.clone()), Probe))
        .observe(receive);
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy readback observer parameters"
)]
fn receive(event: On<ReadbackComplete>, mut observation: ResMut<GpuObservation>) {
    observation.0 = decode(&event.data);
}

fn decode(bytes: &[u8]) -> Option<Sample> {
    let words: Vec<_> = bytes
        .get(..464)?
        .as_chunks::<4>()
        .0
        .iter()
        .map(|word| u32::from_le_bytes(*word))
        .collect();
    let scalar = |index| f32::from_bits(words[index]);
    let vector = |start| std::array::from_fn(|i| scalar(start + i));
    let sample = Sample {
        enabled: scalar(100),
        control: scalar(112),
        count: words[96],
        map_size: scalar(97),
        frame_index: words[103],
        camera: vector(104),
        forward: vector(108),
        matrices: std::array::from_fn(|m| std::array::from_fn(|i| scalar(m * 16 + i))),
        cascades: std::array::from_fn(|m| vector(64 + m * 4)),
    };
    if !matches!(sample.enabled, 0.0 | 1.0)
        || !matches!(sample.control, 0.0 | 1.0)
        || sample.count != 3
        || sample.map_size.to_bits() != 2048.0_f32.to_bits()
        || sample
            .camera
            .iter()
            .chain(&sample.forward)
            .chain(sample.matrices.iter().flatten())
            .chain(sample.cascades.iter().flatten())
            .any(|v| !v.is_finite())
    {
        return None;
    }
    Some(sample)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_material::ObjectShadowSettings;
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks the exact packed GPU receipt fields without changing their comparison contract."
    )]
    fn decodes_the_same_packed_live_receiver_that_the_graph_writes() {
        let settings = ObjectShadowSettings {
            enabled: 1.0,
            frame_index: 37,
            camera: Vec4::new(40.0, -1.0, 29.0, 0.0),
            forward: Vec4::Z,
            ..default()
        };
        let bytes = super::super::bytes(settings.clone());
        let sample = decode(&bytes).unwrap();
        assert_eq!(sample.control, 1.0);
        assert_eq!(sample.enabled, settings.enabled);
        assert_eq!(sample.frame_index, settings.frame_index);
        assert_eq!(sample.camera, settings.camera.to_array());
        assert_eq!(sample.forward, settings.forward.to_array());
        assert_eq!(sample.matrices[0], settings.matrices[0].to_cols_array());
        assert!(decode(&bytes[..463]).is_none());
        let mut invalid = bytes;
        invalid[416..420].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(decode(&invalid).is_none());
    }
}
