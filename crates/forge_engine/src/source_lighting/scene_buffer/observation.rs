//! Opt-in buffer readback during a held diagnostic. Asynchronous data is not a fence.
use super::{HANDLE, ownership};
use bevy::{
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::BufferUsages,
        storage::ShaderBuffer,
    },
};

#[derive(Clone, Debug, serde::Serialize)]
pub struct Sample {
    pub readback_bytes: usize,
    pub payload_sha256: forge_core::ContentHash,
    pub direction_color_sh_vec4: [[f32; 4]; 11],
}

#[derive(Resource, Default)]
pub struct GpuObservation(pub Option<Sample>);

#[derive(Component)]
struct Probe;

/// Enable before rendering starts, or with a buffer already allowing `COPY_SRC`.
/// A GPU owner's allocation cannot be recreated from its possibly stale CPU seed.
pub fn observe_while_held(world: &mut World, held: bool) -> Result<(), String> {
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
        return Ok(());
    }
    if !probes.is_empty() {
        return Ok(());
    }
    let requires_usage_change = !world
        .get_resource::<Assets<ShaderBuffer>>()
        .and_then(|assets| assets.get(&HANDLE))
        .ok_or("Lighting observation requires the existing shared buffer")?
        .buffer_description
        .usage
        .contains(BufferUsages::COPY_SRC);
    if requires_usage_change {
        if ownership::gpu_owned(world, &HANDLE) {
            return Err("Cannot reallocate GPU-owned lighting for diagnostic readback".into());
        }
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&HANDLE)
            .ok_or("Lighting buffer disappeared before observation")?
            .buffer_description
            .usage |= BufferUsages::COPY_SRC;
    }
    world.init_resource::<GpuObservation>();
    world.resource_mut::<GpuObservation>().0 = None;
    world
        .spawn((Readback::buffer(HANDLE.clone()), Probe))
        .observe(receive);
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy readback observer parameters"
)]
fn receive(event: On<ReadbackComplete>, mut observation: ResMut<GpuObservation>) {
    observation.0 = decode(&event.data);
}

fn decode(bytes: &[u8]) -> Option<Sample> {
    let payload = bytes.get(..176)?;
    let vectors = payload.as_chunks::<16>().0;
    let values = std::array::from_fn::<_, 11, _>(|index| {
        std::array::from_fn::<_, 4, _>(|lane| {
            f32::from_le_bytes(vectors[index].as_chunks::<4>().0[lane])
        })
    });
    values
        .iter()
        .flatten()
        .all(|value| value.is_finite())
        .then(|| Sample {
            readback_bytes: bytes.len(),
            payload_sha256: forge_core::bytes_sha256(payload),
            direction_color_sh_vec4: values,
        })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn world() -> World {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        let light = crate::source_lighting::SceneLighting::new(
            [0.0, 1.0, 0.0],
            [2.0, 3.0, 4.0],
            [[0.1; 3]; 9],
        )
        .unwrap();
        super::super::publish(&mut world, &light).unwrap();
        world
    }

    #[test]
    fn held_probe_preserves_payload_is_unique_and_retires() {
        let mut world = world();
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&HANDLE)
            .unwrap()
            .data
            .clone();
        observe_while_held(&mut world, true).unwrap();
        observe_while_held(&mut world, true).unwrap();
        assert_eq!(
            world
                .query_filtered::<Entity, With<Probe>>()
                .iter(&world)
                .count(),
            1
        );
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&HANDLE)
                .unwrap()
                .data,
            before
        );
        observe_while_held(&mut world, false).unwrap();
        assert_eq!(
            world
                .query_filtered::<Entity, With<Probe>>()
                .iter(&world)
                .count(),
            0
        );
        assert!(world.resource::<GpuObservation>().0.is_none());
    }

    #[test]
    fn gpu_owned_buffer_cannot_be_reallocated_for_observation() {
        let mut world = world();
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&HANDLE)
            .unwrap()
            .buffer_description
            .usage
            .remove(BufferUsages::COPY_SRC);
        let _lease = ownership::claim(&mut world, &HANDLE).unwrap();
        let before = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&HANDLE)
            .unwrap()
            .data
            .clone();
        assert!(observe_while_held(&mut world, true).is_err());
        assert!(ownership::gpu_owned(&world, &HANDLE));
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&HANDLE)
                .unwrap()
                .data,
            before
        );
        assert_eq!(
            world
                .query_filtered::<Entity, With<Probe>>()
                .iter(&world)
                .count(),
            0
        );
    }

    #[test]
    fn readback_checks_the_exact_payload_and_rejects_nonfinite_or_short_data() {
        let world = world();
        let mut bytes = world
            .resource::<Assets<ShaderBuffer>>()
            .get(&HANDLE)
            .unwrap()
            .data
            .clone()
            .unwrap();
        let sample = decode(&bytes).unwrap();
        assert_eq!(sample.payload_sha256, forge_core::bytes_sha256(&bytes));
        assert_eq!(
            sample.direction_color_sh_vec4[1].map(f32::to_bits),
            [2.0_f32, 3.0, 4.0, 0.0].map(f32::to_bits)
        );
        assert!(decode(&bytes[..175]).is_none());
        bytes[4..8].copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(decode(&bytes).is_none());
    }
}
