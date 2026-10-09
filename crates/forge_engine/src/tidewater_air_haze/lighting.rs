//! GPU lighting snapshot produced by the prepared optical material's dispatch.
//! CPU publication records command encoding, never GPU completion or pixel parity.
use crate::tidewater_underwater_lighting::{Surface, Targets as OpticalTargets};
use bevy::{
    prelude::*,
    render::{
        extract_resource::ExtractResource,
        gpu_readback::{Readback, ReadbackComplete},
        storage::ShaderBuffer,
    },
};
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicU64, Ordering},
};

pub(crate) fn storage() -> ShaderBuffer {
    let mut buffer = ShaderBuffer::from(vec![Vec4::ZERO; 3]);
    buffer.buffer_description.usage |= bevy::render::render_resource::BufferUsages::COPY_SRC;
    buffer
}

#[derive(Component)]
struct ObservedBuffer {
    handle: Handle<ShaderBuffer>,
    generation: Arc<AtomicU64>,
}

/// Opt-in diagnostic. Delivery-time counters are deliberately not frame tags.
pub(crate) fn observe_gpu(app: &mut App, targets: &Targets) {
    if std::env::var("FORGE_TIDEWATER_HAZE_LIGHTING_READBACK").as_deref() != Ok("1") {
        return;
    }
    app.world_mut()
        .spawn((
            Readback::buffer(targets.buffer.clone()),
            ObservedBuffer {
                handle: targets.buffer.clone(),
                generation: targets.generation.clone(),
            },
        ))
        .observe(receive_gpu);
}

fn decode_gpu(bytes: &[u8]) -> Option<[f32; 12]> {
    if bytes.len() != 48 {
        return None;
    }
    let words = bytes.as_chunks::<4>().0;
    let values = std::array::from_fn(|index| f32::from_le_bytes(words[index]));
    values
        .iter()
        .all(|value| value.is_finite())
        .then_some(values)
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive_gpu(
    event: On<ReadbackComplete>,
    sources: Query<&ObservedBuffer>,
    targets: Option<Res<Targets>>,
) {
    let (Ok(source), Some(targets)) = (sources.get(event.entity), targets) else {
        return;
    };
    if source.handle != targets.buffer || !Arc::ptr_eq(&source.generation, &targets.generation) {
        return;
    }
    let Some(values) = decode_gpu(&event.data) else {
        warn!("Tidewater haze lighting GPU readback rejected: expected 48 finite bytes");
        return;
    };
    info!(
        "Tidewater haze lighting GPU readback: buffer={:?} delivery_encoded_count={} values={:?}; frame_tag_unpaired=true",
        source.handle.id(),
        source.generation.load(Ordering::Acquire),
        values,
    );
}

#[derive(Clone, Copy)]
struct Publication {
    surface: bevy::asset::UntypedAssetId,
    sequence: u64,
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn snapshot_buffer_has_the_exact_shader_span_and_readback_usage() {
        let buffer = storage();
        assert_eq!(buffer.data.as_ref().unwrap().len(), 48);
        assert!(buffer.buffer_description.usage.contains(
            bevy::render::render_resource::BufferUsages::STORAGE
                | bevy::render::render_resource::BufferUsages::COPY_SRC,
        ));
    }

    #[test]
    fn gpu_readback_decodes_all_fields_and_rejects_invalid_payloads() {
        let expected: [f32; 12] = [
            0.25, 0.5, -0.75, -2.0, 1.0, 2.0, 3.0, 0.0, 4.0, 5.0, 6.0, 1.0,
        ];
        let bytes: Vec<u8> = expected
            .iter()
            .flat_map(|value| value.to_le_bytes())
            .collect();
        assert_eq!(decode_gpu(&bytes), Some(expected));
        assert!(decode_gpu(&bytes[..44]).is_none());
        let mut oversized = bytes.clone();
        oversized.extend_from_slice(&[0; 4]);
        assert!(decode_gpu(&oversized).is_none());
        for index in 0..12 {
            let mut invalid = bytes.clone();
            invalid[index * 4..index * 4 + 4].copy_from_slice(&f32::NAN.to_le_bytes());
            assert!(decode_gpu(&invalid).is_none());
        }
    }
}

/// Forty-eight GPU bytes: sun/sea, sun color, sky irradiance/immersion.
#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub buffer: Handle<ShaderBuffer>,
    map_stamp: Handle<ShaderBuffer>,
    generation: Arc<AtomicU64>,
    publication: Arc<Mutex<Option<Publication>>>,
}

impl Targets {
    pub(crate) fn new(
        buffer: Handle<ShaderBuffer>,
        map_stamp: Handle<ShaderBuffer>,
        generation: Arc<AtomicU64>,
    ) -> Self {
        Self {
            buffer,
            map_stamp,
            generation,
            publication: default(),
        }
    }

    /// Reject replacement even if an old counter is nonzero.
    #[must_use]
    pub fn same_generation(&self, optics: &OpticalTargets) -> bool {
        self.map_stamp == optics.bindings.frame
            && Arc::ptr_eq(&self.generation, &optics.encoded_frames)
    }

    /// The snapshot was encoded for this exact material and latest dispatch.
    /// Its consumers must additionally prove their own view/frame-tag contract.
    #[must_use]
    pub fn encoded_for(&self, optics: &OpticalTargets, surface: &Surface) -> bool {
        if !self.same_generation(optics) {
            return false;
        }
        let Ok(publication) = self.publication.lock() else {
            return false;
        };
        publication.as_ref().is_some_and(|p| {
            p.surface == surface.0
                && p.sequence > 0
                && p.sequence == self.generation.load(Ordering::Acquire)
        })
    }

    pub(crate) fn record(&self, surface: &Surface, sequence: u64) -> bool {
        if sequence == 0 || sequence != self.generation.load(Ordering::Acquire) {
            return false;
        }
        let Ok(mut publication) = self.publication.lock() else {
            return false;
        };
        *publication = Some(Publication {
            surface: surface.0,
            sequence,
        });
        true
    }
}
