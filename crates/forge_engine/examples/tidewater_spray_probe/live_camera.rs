//! Main-world camera position consumed at each live simulation step.
use bevy::prelude::*;
use std::sync::{Arc, Mutex};

#[derive(Default)]
struct State {
    position: Option<Vec3>,
    first: Option<(u32, Vec3)>,
    latest: Option<(u32, Vec3)>,
}

#[derive(Resource, Clone, Default)]
pub struct LiveCamera(Arc<Mutex<State>>);

impl LiveCamera {
    #[allow(dead_code)] // The standalone compute probe has no player camera.
    pub fn set_position(&self, position: Vec3) {
        if position.is_finite() {
            self.0.lock().expect("live camera mutex").position = Some(position);
        }
    }

    pub(super) fn apply(&self, step: u32, breakers: &mut [u8]) {
        // Preserve the original 120-step acquisition, including its camera.
        if step <= 120 {
            return;
        }
        let mut state = self.0.lock().expect("live camera mutex");
        let Some(position) = state.position else {
            return;
        };
        for (axis, component) in position.to_array().into_iter().enumerate() {
            let offset = axis * 4;
            let bytes = component.to_le_bytes();
            // Source BreakersParams starts with cameraPos. Preserve its
            // emission budget at byte 28 and the unrelated Frame matrices.
            breakers[offset..offset + 4].copy_from_slice(&bytes);
        }
        state.first.get_or_insert((step, position));
        state.latest = Some((step, position));
    }

    #[allow(dead_code)] // Native walk receipt; these are encoded inputs, not GPU readbacks.
    pub fn samples(&self) -> Option<[(u32, [f32; 3]); 2]> {
        let state = self.0.lock().expect("live camera mutex");
        let (first, latest) = (state.first?, state.latest?);
        Some([
            (first.0, first.1.to_array()),
            (latest.0, latest.1.to_array()),
        ])
    }
}
