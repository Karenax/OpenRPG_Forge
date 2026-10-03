// SPDX-License-Identifier: MIT
// Derived from Tidewater SceneRenderer.js and TemporalUpscale.js at
// 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Per-view auxiliary textures on the existing Bevy `RenderDevice`.
//!
//! Allocation is not readiness. Only the renderer may record an auxiliary
//! encoding after its actual draws and depth copies. This state is neither a
//! GPU completion fence nor a source TAAU resolve acknowledgement.

use super::frame::{FrameTag, ViewPacket};
use bevy::{
    prelude::*,
    render::{
        render_resource::{
            Extent3d, TextureDescriptor, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::RenderDevice,
        texture::{CachedTexture, TextureCache},
        view::Msaa,
    },
};
use std::{
    collections::{HashMap, HashSet},
    error::Error as StdError,
    fmt,
    sync::{Arc, Mutex},
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Spec {
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub history_epoch: u64,
}

impl Spec {
    pub fn from_packet(packet: &ViewPacket) -> Result<Self, TargetError> {
        let frame = &packet.frame;
        let spec = Self {
            input_size: frame.input_size,
            output_size: frame.output_size,
            history_epoch: frame.history_epoch,
        };
        spec.validate()?;
        Ok(spec)
    }

    fn validate(self) -> Result<(), TargetError> {
        if self.input_size.min_element() == 0
            || self.output_size.min_element() == 0
            || self.input_size.cmpgt(self.output_size).any()
            || self.output_size.max_element() > 1 << 24
        {
            return Err(TargetError::InvalidSize);
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TargetError {
    InvalidSize,
    WrongViewOrEpoch,
    StaleEncoding,
    CounterExhausted,
    HistoryPoisoned,
    MissingMsaa,
    UnsupportedMsaa,
}

impl fmt::Display for TargetError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.write_str(match self {
            Self::InvalidSize => "Auxiliary targets require positive internal/output dimensions with internal no larger than output",
            Self::WrongViewOrEpoch => "Auxiliary targets belong to another view, size or history epoch",
            Self::StaleEncoding => "Auxiliary encoding is duplicate or older than the recorded encoding",
            Self::CounterExhausted => "Auxiliary allocation generation exhausted",
            Self::HistoryPoisoned => "Auxiliary encoded-history mutex is poisoned",
            Self::MissingMsaa => "Auxiliary depth copies require an explicit MSAA Off view",
            Self::UnsupportedMsaa => "Auxiliary depth copies require sample count one",
        })
    }
}

impl StdError for TargetError {}

#[derive(Default)]
struct EncodedHistory {
    last_tag: Option<FrameTag>,
}

/// Five distinct textures. Final depth is not the existing opaque-depth capture.
#[derive(Component, Clone)]
pub struct AuxTargets {
    pub view: Entity,
    pub input_size: UVec2,
    pub output_size: UVec2,
    pub history_epoch: u64,
    pub generation: u64,
    pub velocity: CachedTexture,
    pub mask: CachedTexture,
    pub depth_aux: CachedTexture,
    pub current_final_depth: CachedTexture,
    pub previous_depth: CachedTexture,
    history: Arc<Mutex<EncodedHistory>>,
}

impl AuxTargets {
    fn validate_packet(&self, packet: &ViewPacket) -> Result<(), TargetError> {
        if packet.tag.view != self.view
            || packet.frame.input_size != self.input_size
            || packet.frame.output_size != self.output_size
            || packet.frame.history_epoch != self.history_epoch
        {
            return Err(TargetError::WrongViewOrEpoch);
        }
        Ok(())
    }

    /// Seed previous-depth from the new current image on a fresh allocation.
    /// Otherwise copy old current-final-depth to previous-depth before overwriting
    /// current-final-depth. The renderer owns those real encoding operations.
    pub fn needs_seed(&self, packet: &ViewPacket) -> Result<bool, TargetError> {
        self.validate_packet(packet)?;
        let history = self
            .history
            .lock()
            .map_err(|_| TargetError::HistoryPoisoned)?;
        Ok(packet.frame.needs_history_seed || history.last_tag.is_none())
    }

    pub fn was_aux_encoded(&self, tag: FrameTag) -> Result<bool, TargetError> {
        let history = self
            .history
            .lock()
            .map_err(|_| TargetError::HistoryPoisoned)?;
        Ok(history.last_tag == Some(tag))
    }

    /// Call only after the real auxiliary draws and current/previous-depth copies.
    /// Records command encoding, not GPU completion or texture-readback validity.
    pub fn mark_aux_encoded(&self, packet: &ViewPacket) -> Result<(), TargetError> {
        self.validate_packet(packet)?;
        let mut history = self
            .history
            .lock()
            .map_err(|_| TargetError::HistoryPoisoned)?;
        if history
            .last_tag
            .is_some_and(|tag| tag.serial >= packet.tag.serial)
        {
            return Err(TargetError::StaleEncoding);
        }
        history.last_tag = Some(packet.tag);
        Ok(())
    }
}

struct OwnedAllocation {
    spec: Spec,
    targets: AuxTargets,
    // The global TextureCache resets `taken` every frame. Keeping an independent
    // cache for this persistent view prevents another view from borrowing its
    // history allocation between prepares. This cache is not globally updated.
    _cache: TextureCache,
}

/// RenderWorld-only ownership; uses the existing device, never another renderer.
#[derive(Resource, Default)]
pub struct Allocator {
    allocations: HashMap<Entity, OwnedAllocation>,
    generation: u64,
    pub errors: HashMap<Entity, TargetError>,
}

impl Allocator {
    pub(super) fn retire_all(&mut self) {
        self.allocations.clear();
        self.errors.clear();
        // Allocation generations remain monotone through source instances.
    }

    pub fn allocate(
        &mut self,
        packet: &ViewPacket,
        device: &RenderDevice,
    ) -> Result<AuxTargets, TargetError> {
        let spec = Spec::from_packet(packet)?;
        if let Some(allocation) = self.allocations.get(&packet.tag.view)
            && allocation.spec == spec
        {
            return Ok(allocation.targets.clone());
        }
        let generation = self
            .generation
            .checked_add(1)
            .ok_or(TargetError::CounterExhausted)?;
        let mut cache = TextureCache::default();
        let targets = AuxTargets {
            view: packet.tag.view,
            input_size: spec.input_size,
            output_size: spec.output_size,
            history_epoch: spec.history_epoch,
            generation,
            velocity: cache.get(device, descriptor(spec, Role::Velocity)),
            mask: cache.get(device, descriptor(spec, Role::Mask)),
            depth_aux: cache.get(device, descriptor(spec, Role::AuxDepth)),
            current_final_depth: cache.get(device, descriptor(spec, Role::CurrentFinalDepth)),
            previous_depth: cache.get(device, descriptor(spec, Role::PreviousDepth)),
            history: Arc::default(),
        };
        self.allocations.insert(
            packet.tag.view,
            OwnedAllocation {
                spec,
                targets: targets.clone(),
                _cache: cache,
            },
        );
        self.generation = generation;
        Ok(targets)
    }
}

#[derive(Clone, Copy)]
enum Role {
    Velocity,
    Mask,
    AuxDepth,
    CurrentFinalDepth,
    PreviousDepth,
}

fn descriptor(spec: Spec, role: Role) -> TextureDescriptor<'static> {
    let (label, format, copy_destination) = match role {
        Role::Velocity => (
            "Tidewater temporal velocity",
            TextureFormat::Rgba16Float,
            false,
        ),
        Role::Mask => ("Tidewater temporal mask", TextureFormat::Rgba8Unorm, false),
        Role::AuxDepth => (
            "Tidewater temporal auxiliary depth",
            TextureFormat::Depth32Float,
            false,
        ),
        Role::CurrentFinalDepth => (
            "Tidewater temporal current final depth",
            TextureFormat::Depth32Float,
            true,
        ),
        Role::PreviousDepth => (
            "Tidewater temporal previous depth",
            TextureFormat::Depth32Float,
            true,
        ),
    };
    let mut usage =
        TextureUsages::TEXTURE_BINDING | TextureUsages::RENDER_ATTACHMENT | TextureUsages::COPY_SRC;
    if copy_destination {
        usage |= TextureUsages::COPY_DST;
    }
    TextureDescriptor {
        label: Some(label),
        size: Extent3d {
            width: spec.input_size.x,
            height: spec.input_size.y,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: TextureDimension::D2,
        format,
        usage,
        view_formats: &[],
    }
}

/// Install in `RenderSystems::PrepareResources` before packet offsets/phase queue.
/// Allocation does not expose a ready flag; unsupported depth copies remove it.
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy preparation systems receive commands, resources, and queries by value"
)]
pub fn prepare_targets(
    mut commands: Commands,
    device: Res<RenderDevice>,
    mut allocator: ResMut<Allocator>,
    views: Query<(Entity, &ViewPacket, Option<&Msaa>)>,
    stale: Query<Entity, (With<AuxTargets>, Without<ViewPacket>)>,
) {
    let active: HashSet<_> = views.iter().map(|(_, packet, _)| packet.tag.view).collect();
    allocator
        .allocations
        .retain(|view, _| active.contains(view));
    allocator.errors.retain(|view, _| active.contains(view));
    for entity in &stale {
        commands.entity(entity).remove::<AuxTargets>();
    }
    for (entity, packet, msaa) in &views {
        let result = match msaa {
            None => Err(TargetError::MissingMsaa),
            Some(msaa) if msaa.samples() != 1 => Err(TargetError::UnsupportedMsaa),
            Some(_) => allocator.allocate(packet, &device),
        };
        match result {
            Ok(targets) => {
                allocator.errors.remove(&packet.tag.view);
                commands.entity(entity).insert(targets);
            }
            Err(error) => {
                allocator.errors.insert(packet.tag.view, error);
                commands.entity(entity).remove::<AuxTargets>();
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn odd_resize_uses_exact_internal_pixels_and_separate_role_descriptors() {
        let spec = Spec {
            input_size: UVec2::new(1919, 1079),
            output_size: UVec2::new(1919, 1079),
            history_epoch: 3,
        };
        spec.validate().expect("odd dimensions");
        for role in [
            Role::Velocity,
            Role::Mask,
            Role::AuxDepth,
            Role::CurrentFinalDepth,
            Role::PreviousDepth,
        ] {
            let target = descriptor(spec, role);
            assert_eq!(target.size.width, 1919);
            assert_eq!(target.size.height, 1079);
            assert_eq!(target.sample_count, 1);
            assert!(target.usage.contains(
                TextureUsages::COPY_SRC
                    | TextureUsages::TEXTURE_BINDING
                    | TextureUsages::RENDER_ATTACHMENT
            ));
        }
        let current = descriptor(spec, Role::CurrentFinalDepth);
        let previous = descriptor(spec, Role::PreviousDepth);
        assert_ne!(current.label, previous.label);
        assert!(current.usage.contains(TextureUsages::COPY_DST));
        assert!(previous.usage.contains(TextureUsages::COPY_DST));
        assert_eq!(
            descriptor(spec, Role::Velocity).format,
            TextureFormat::Rgba16Float
        );
        assert_eq!(
            descriptor(spec, Role::Mask).format,
            TextureFormat::Rgba8Unorm
        );
        assert_eq!(
            descriptor(spec, Role::AuxDepth).format,
            TextureFormat::Depth32Float
        );
        assert_ne!(
            spec,
            Spec {
                history_epoch: 4,
                ..spec
            }
        );
        assert_ne!(
            spec,
            Spec {
                input_size: UVec2::new(1918, 1079),
                ..spec
            }
        );
    }

    #[test]
    fn invalid_or_oversized_internal_rectangles_are_rejected() {
        for (input_size, output_size) in [
            (UVec2::ZERO, UVec2::ONE),
            (UVec2::ONE, UVec2::ZERO),
            (UVec2::new(1920, 1080), UVec2::new(1919, 1079)),
            (UVec2::ONE, UVec2::splat((1 << 24) + 1)),
        ] {
            assert_eq!(
                Spec {
                    input_size,
                    output_size,
                    history_epoch: 0
                }
                .validate(),
                Err(TargetError::InvalidSize)
            );
        }
    }
}
