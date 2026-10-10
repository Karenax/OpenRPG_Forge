// SPDX-License-Identifier: MIT
// Adapted from Tidewater LocalLights.js at 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Source-shaped local lighting on the caller's existing Bevy renderer.
//!
//! Scene adapters supply ordered, live light records and the actual camera.
//! This module retains no authoring document and creates no renderer. Uploading
//! its buffer does not supply the other mesh resources or execute a mesh draw.

use crate::tidewater_temporal::frame::{FrameTag, ViewPacket};
use bevy::{
    math::{DMat4, DVec2, DVec3, DVec4},
    render::{
        render_resource::{Buffer, BufferDescriptor, BufferUsages},
        renderer::{RenderDevice, RenderQueue},
    },
};
use std::{error::Error as StdError, fmt};

pub mod collection;
#[cfg(feature = "physics-rapier")]
pub mod projection;
pub mod scene;

pub const MAX_LIGHTS: usize = 8;
pub const PACKET_BYTES: usize = 448;

/// Derived runtime parameters. Persistent descriptors belong to Forge documents.
/// Colors and intensity use source scene units, rather than Bevy photometric units.
#[derive(Clone, Debug)]
pub struct Light {
    pub position: DVec3,
    pub color: DVec3,
    pub intensity: f64,
    pub range: f64,
    /// Normalized world axis, as produced by the source's transformDirection.
    pub direction: Option<DVec3>,
    pub cos_inner: Option<f64>,
    pub cos_outer: Option<f64>,
    pub enabled: bool,
    pub scale: f64,
    pub flicker: f64,
    /// Acquired or explicitly authored phase; never re-randomized by the renderer.
    pub phase: f64,
}

impl Light {
    fn validate(&self) -> Result<(), Error> {
        if !self.position.is_finite()
            || !self.color.is_finite()
            || self.color.min_element() < 0.0
            || ![
                self.intensity,
                self.range,
                self.scale,
                self.flicker,
                self.phase,
            ]
            .iter()
            .all(|v| v.is_finite())
            || self.range <= 0.0
            || self.intensity < 0.0
            || self.scale < 0.0
            || self.direction.is_some_and(|v| !v.is_finite())
            || self
                .direction
                .is_some_and(|v| (v.length_squared() - 1.0).abs() > 1e-6)
            || self.cos_inner.is_some_and(|v| !v.is_finite())
            || self.cos_outer.is_some_and(|v| !v.is_finite())
            || self.cos_inner.is_some_and(|v| !(-1.0..=1.0).contains(&v))
            || self.cos_outer.is_some_and(|v| !(-1.0..=1.0).contains(&v))
        {
            return Err(Error::InvalidLight);
        }
        Ok(())
    }
}

/// The source uses local camera.position for lamp selection, and matrixWorld for
/// the torch. Keep both explicitly, including for a parented camera.
#[derive(Clone, Copy, Debug)]
pub struct CameraPose {
    pub local_position: DVec3,
    pub world_from_view: DMat4,
}

#[derive(Clone, Copy, Debug)]
pub struct Flashlight {
    pub intensity: f64,
    pub range: f64,
    pub color: DVec3,
    pub cos_inner: f64,
    pub cos_outer: f64,
}

impl Default for Flashlight {
    fn default() -> Self {
        Self {
            intensity: 55.0,
            range: 30.0,
            color: DVec3::new(1.0, 0.71, 0.49),
            cos_inner: 9_f64.to_radians().cos(),
            cos_outer: 35_f64.to_radians().cos(),
        }
    }
}

#[derive(Clone, Copy, Debug)]
pub struct Settings {
    pub delta_seconds: f64,
    pub night: f64,
    pub enabled: bool,
    pub strength: f64,
    pub flashlight: Flashlight,
}

/// Exact values retained between updates, including inactive array entries and
/// the torch fields while flashOn=0. Padding is always zero when packing.
#[derive(Clone, Debug, PartialEq)]
pub struct Packet {
    pub positions: [DVec4; MAX_LIGHTS],
    pub colors: [DVec4; MAX_LIGHTS],
    pub directions: [DVec4; MAX_LIGHTS],
    pub count: i32,
    pub flash_on: f64,
    pub flash_cone: DVec2,
    pub flash_position: DVec3,
    pub flash_direction: DVec3,
    pub flash_color: DVec3,
}

impl Default for Packet {
    fn default() -> Self {
        Self {
            // THREE.Vector4() initializes w=1, including slots never written.
            positions: [DVec4::W; MAX_LIGHTS],
            colors: [DVec4::W; MAX_LIGHTS],
            directions: [DVec4::W; MAX_LIGHTS],
            count: 0,
            flash_on: 0.0,
            flash_cone: DVec2::new(0.99, 0.82),
            flash_position: DVec3::ZERO,
            flash_direction: DVec3::NEG_Z,
            flash_color: DVec3::ZERO,
        }
    }
}

impl Packet {
    pub fn pack(&self) -> Result<[u8; PACKET_BYTES], Error> {
        if !(0..=8).contains(&self.count) {
            return Err(Error::InvalidPacket);
        }
        let mut bytes = [0; PACKET_BYTES];
        for (base, values) in [
            (0, &self.positions),
            (128, &self.colors),
            (256, &self.directions),
        ] {
            for (i, value) in values.iter().enumerate() {
                write_values(&mut bytes, base + i * 16, &value.to_array())?;
            }
        }
        bytes[384..388].copy_from_slice(&self.count.to_le_bytes());
        write_values(&mut bytes, 388, &[self.flash_on])?;
        write_values(&mut bytes, 392, &self.flash_cone.to_array())?;
        write_values(&mut bytes, 400, &self.flash_position.to_array())?;
        write_values(&mut bytes, 416, &self.flash_direction.to_array())?;
        write_values(&mut bytes, 432, &self.flash_color.to_array())?;
        Ok(bytes)
    }
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Source UniformBlock converts finite JS doubles to f32 at this boundary"
)]
fn write_values(
    bytes: &mut [u8; PACKET_BYTES],
    offset: usize,
    values: &[f64],
) -> Result<(), Error> {
    for (i, &value) in values.iter().enumerate() {
        if !value.is_finite() || value.abs() > f64::from(f32::MAX) {
            return Err(Error::InvalidPacket);
        }
        let start = offset + i * 4;
        bytes[start..start + 4].copy_from_slice(&(value as f32).to_le_bytes());
    }
    Ok(())
}

#[derive(Clone, Debug, Default)]
pub struct Collector {
    time: f64,
    flashlight_on: bool,
    primed: bool,
    direction: DVec3,
    packet: Packet,
    last_pose: Option<CameraPose>,
    revision: u64,
}

impl Collector {
    /// Mirrors toggleFlashlight, including re-priming when called with the same state.
    pub fn toggle_flashlight(&mut self, on: bool) {
        self.flashlight_on = on;
        self.primed = false;
    }

    #[must_use]
    pub const fn time(&self) -> f64 {
        self.time
    }

    #[must_use]
    pub const fn packet(&self) -> &Packet {
        &self.packet
    }

    /// Lights must be supplied in registration order; the stable distance sort
    /// preserves that order on ties. A rejected update leaves all history intact.
    pub fn update(
        &mut self,
        camera: CameraPose,
        settings: Settings,
        lights: &[Light],
    ) -> Result<&Packet, Error> {
        let mut next = self.clone();
        let result: Result<(), Error> = (|| {
            next.advance(camera, settings, lights)?;
            next.packet.pack()?;
            next.last_pose = Some(camera);
            next.revision = next.revision.checked_add(1).ok_or(Error::StaleFrame)?;
            Ok(())
        })();
        if let Err(error) = result {
            // Retain numeric history for recovery, but never rebind it as a
            // successful new scene update after a rejected preparation.
            self.last_pose = None;
            return Err(error);
        }
        *self = next;
        Ok(&self.packet)
    }

    /// Bind only to an already prepared camera packet. The caller still owns
    /// scene inventory and publication order; this is not a scene census proof.
    pub fn for_view(&self, view: &ViewPacket) -> Result<FramePacket, Error> {
        if view.tag.serial == 0 {
            return Err(Error::StaleFrame);
        }
        self.for_camera_pose(view.frame.view_from_world.inverse())?;
        Ok(FramePacket {
            tag: view.tag,
            packet: self.packet.clone(),
            revision: self.revision,
        })
    }

    /// Validate an extracted camera without inventing a temporal frame tag.
    fn for_camera_pose(&self, world_from_view: bevy::math::Mat4) -> Result<(u64, &Packet), Error> {
        let pose = self.last_pose.ok_or(Error::StaleFrame)?;
        let expected = world_from_view.to_cols_array();
        let actual = pose.world_from_view.to_cols_array();
        // Bevy matrices are f32; inverting the camera's inverse introduces
        // roundoff. Source scalar math stays f64 until UniformBlock packing.
        if expected
            .iter()
            .zip(actual)
            .any(|(&a, b)| !a.is_finite() || (f64::from(a) - b).abs() > 1e-4)
        {
            return Err(Error::StaleFrame);
        }
        Ok((self.revision, &self.packet))
    }

    #[allow(
        clippy::too_many_lines,
        reason = "Keep the pinned source's stateful slot selection in its original operation order"
    )]
    fn advance(
        &mut self,
        camera: CameraPose,
        settings: Settings,
        lights: &[Light],
    ) -> Result<(), Error> {
        if !camera.local_position.is_finite()
            || !camera.world_from_view.is_finite()
            || camera.world_from_view.row(3) != DVec4::W
        {
            return Err(Error::InvalidCamera);
        }
        let fl = settings.flashlight;
        if ![
            settings.delta_seconds,
            settings.night,
            settings.strength,
            fl.intensity,
            fl.range,
            fl.cos_inner,
            fl.cos_outer,
        ]
        .iter()
        .all(|v| v.is_finite())
            || settings.delta_seconds < 0.0
            || settings.strength < 0.0
            || fl.intensity < 0.0
            || fl.range <= 0.0
            || !fl.color.is_finite()
            || fl.color.min_element() < 0.0
        {
            return Err(Error::InvalidSettings);
        }
        self.time += settings.delta_seconds;
        if !self.time.is_finite() {
            return Err(Error::InvalidSettings);
        }
        let p = &mut self.packet;
        p.flash_on = if self.flashlight_on && settings.enabled {
            1.0
        } else {
            0.0
        };
        let mut n = 0_usize;
        if self.flashlight_on && settings.enabled {
            let right = source_normalize(camera.world_from_view.x_axis.truncate())?;
            let up = source_normalize(camera.world_from_view.y_axis.truncate())?;
            let forward = source_normalize(-camera.world_from_view.z_axis.truncate())?;
            if !self.primed {
                self.direction = forward;
                self.primed = true;
            }
            let alpha = 1.0 - (-settings.delta_seconds / 0.06).exp();
            // THREE.Vector3.lerp is current + (target-current)*alpha, not mix().
            self.direction += (forward - self.direction) * alpha;
            self.direction = source_normalize(self.direction)?;
            let mut position = camera.world_from_view.w_axis.truncate();
            position += right * 0.2;
            position += up * -0.22;
            position += forward * 0.15;
            let k = fl.intensity * (0.08 + 0.92 * smooth(settings.night, 0.0, 0.6));
            let color = fl.color * k;
            p.positions[0] = position.extend(fl.range * fl.range);
            p.colors[0] = color.extend(fl.cos_inner);
            p.directions[0] = self.direction.extend(fl.cos_outer);
            p.flash_position = position;
            p.flash_direction = self.direction;
            p.flash_color = color;
            p.flash_cone = DVec2::new(fl.cos_inner, fl.cos_outer);
            n = 1;
        }
        let on = smooth(settings.night, 0.15, 0.75) * settings.strength;
        if on > 0.002 && settings.enabled {
            // Source update never reads disabled lamps or any lamps in daylight.
            for light in lights.iter().filter(|light| light.enabled) {
                light.validate()?;
            }
            let mut ranked: Vec<_> = lights
                .iter()
                .filter(|l| l.enabled)
                .map(|light| {
                    let d = light.position - camera.local_position;
                    (light, d.x * d.x + d.y * d.y + d.z * d.z)
                })
                .collect();
            if ranked.iter().any(|(_, distance)| !distance.is_finite()) {
                return Err(Error::InvalidLight);
            }
            ranked.sort_by(|a, b| a.1.total_cmp(&b.1));
            let slots = MAX_LIGHTS - n;
            let cutoff = ranked.get(slots).map(|(_, distance)| distance.sqrt());
            // The source computes 0/0 if more than eight sources coincide with
            // the camera. Do not publish its NaNs or silently invent a fallback.
            if cutoff == Some(0.0) {
                return Err(Error::DegenerateCutoff);
            }
            for &(light, distance) in ranked.iter().take(slots) {
                let fade = cutoff.map_or(1.0, |cut| smooth(distance.sqrt(), cut, cut * 0.8));
                let flicker = if light.flicker == 0.0 {
                    1.0
                } else {
                    1.0 + light.flicker
                        * (self.time * 9.0 + light.phase).sin()
                        * (self.time * 5.3 + light.phase * 0.37).sin()
                };
                let k = light.intensity * on * fade * flicker * light.scale;
                if k <= 1e-4 {
                    continue;
                }
                p.positions[n] = light.position.extend(light.range * light.range);
                p.colors[n] = (light.color * k).extend(light.cos_inner.unwrap_or(-1.5));
                p.directions[n] = light
                    .direction
                    .map_or(DVec4::new(0.0, -1.0, 0.0, -2.0), |direction| {
                        direction.extend(light.cos_outer.unwrap_or(-2.0))
                    });
                n += 1;
            }
        }
        p.count = i32::try_from(n).map_err(|_| Error::InvalidPacket)?;
        Ok(())
    }
}

fn source_normalize(vector: DVec3) -> Result<DVec3, Error> {
    let length = (vector.x * vector.x + vector.y * vector.y + vector.z * vector.z).sqrt();
    if !length.is_finite() {
        return Err(Error::InvalidCamera);
    }
    // THREE divides through a reciprocal; preserve that arithmetic before f32 packing.
    // Its normalize() uses length || 1, retaining a zero interpolated torch axis.
    Ok(vector * (1.0 / if length == 0.0 { 1.0 } else { length }))
}

fn smooth(x: f64, a: f64, b: f64) -> f64 {
    let t = ((x - a) / (b - a)).clamp(0.0, 1.0);
    t * t * (3.0 - 2.0 * t)
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Error {
    InvalidLight,
    InvalidCamera,
    InvalidSettings,
    InvalidPacket,
    DegenerateCutoff,
    DuplicateOrder,
    MissingCamera,
    MissingLightTransform,
    InventoryMismatch,
    StaleFrame,
}

impl fmt::Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Self::InvalidLight => "Source light has invalid or nonfinite parameters",
            Self::InvalidCamera => "Source local lights require a finite, affine camera pose",
            Self::InvalidSettings => "Source local-light settings are invalid",
            Self::InvalidPacket => "Source local-light packet cannot be represented in its 448-byte ABI",
            Self::DegenerateCutoff => "Source light selection produces a zero-distance cutoff and nonfinite fade",
            Self::DuplicateOrder => "Source light registration orders must be unique",
            Self::MissingCamera => "Source local lights require the selected active main camera and propagated transforms",
            Self::MissingLightTransform => "A source light has no propagated world transform",
            Self::InventoryMismatch => "Live source lights do not match the adapter's explicit registration inventory",
            Self::StaleFrame => "Source local-light buffer does not belong to the requested camera frame",
        })
    }
}
impl StdError for Error {}

/// A scene packet paired with the selected main camera's actual preparation tag.
#[derive(Clone, Debug)]
pub struct FramePacket {
    tag: FrameTag,
    packet: Packet,
    revision: u64,
}

impl FramePacket {
    #[must_use]
    pub const fn tag(&self) -> FrameTag {
        self.tag
    }
    #[must_use]
    pub const fn packet(&self) -> &Packet {
        &self.packet
    }
}

/// The exact UBO used by source-composed mesh bindings. It contains no textures,
/// cluster data or second device. A consumer must check the same frame tag.
pub struct GpuPacket {
    buffer: Buffer,
    tag: FrameTag,
    ready: bool,
    revision: u64,
}

impl GpuPacket {
    /// Adapters call this when collection/view preparation fails before update.
    /// Consumers must always request their current prepared `ViewPacket` tag.
    pub fn invalidate(&mut self) {
        self.ready = false;
    }
    pub fn upload(
        device: &RenderDevice,
        queue: &RenderQueue,
        frame: &FramePacket,
    ) -> Result<Self, Error> {
        let bytes = frame.packet.pack()?;
        let buffer = device.create_buffer(&BufferDescriptor {
            label: Some("source LocalLightParams"),
            size: 448,
            usage: BufferUsages::UNIFORM | BufferUsages::COPY_DST | BufferUsages::COPY_SRC,
            mapped_at_creation: false,
        });
        queue.write_buffer(&buffer, 0, &bytes);
        Ok(Self {
            buffer,
            tag: frame.tag,
            ready: true,
            revision: frame.revision,
        })
    }

    pub fn update(&mut self, queue: &RenderQueue, frame: &FramePacket) -> Result<(), Error> {
        self.ready = false;
        if frame.tag.view != self.tag.view
            || frame.tag.serial <= self.tag.serial
            || frame.revision <= self.revision
        {
            return Err(Error::StaleFrame);
        }
        let bytes = frame.packet.pack()?;
        queue.write_buffer(&self.buffer, 0, &bytes);
        self.tag = frame.tag;
        self.ready = true;
        self.revision = frame.revision;
        Ok(())
    }

    pub fn buffer(&self, expected: FrameTag) -> Result<&Buffer, Error> {
        if !self.ready || expected != self.tag {
            return Err(Error::StaleFrame);
        }
        Ok(&self.buffer)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::*;

    fn lamp() -> Light {
        Light {
            position: DVec3::new(0.0, 0.0, -2.0),
            color: DVec3::ONE,
            intensity: 1.0,
            range: 10.0,
            direction: None,
            cos_inner: None,
            cos_outer: None,
            enabled: true,
            scale: 1.0,
            flicker: 0.08,
            phase: 3.0,
        }
    }

    fn settings() -> Settings {
        Settings {
            delta_seconds: 0.1,
            night: 1.0,
            enabled: true,
            strength: 1.0,
            flashlight: Flashlight::default(),
        }
    }

    fn pose() -> CameraPose {
        CameraPose {
            local_position: DVec3::ZERO,
            world_from_view: DMat4::IDENTITY,
        }
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn invalid_input_does_not_advance_light_history_or_publish_nan() {
        let mut collector = Collector::default();
        collector.toggle_flashlight(true);
        collector
            .update(pose(), settings(), &[lamp()])
            .expect("valid frame");
        let before = collector.packet().pack().expect("finite bytes");
        let before_time = collector.time();
        let mut invalid = lamp();
        invalid.range = f64::MAX;
        assert_eq!(
            collector.update(pose(), settings(), &[invalid]),
            Err(Error::InvalidPacket)
        );
        assert_eq!(collector.time(), before_time);
        assert_eq!(collector.packet().pack().expect("old bytes"), before);
        let mut bad_pose = pose();
        bad_pose.world_from_view.x_axis.w = 1.0;
        assert_eq!(
            collector.update(bad_pose, settings(), &[]),
            Err(Error::InvalidCamera)
        );
        assert_eq!(collector.time(), before_time);
        collector.toggle_flashlight(false);
        let mut coincident = lamp();
        coincident.position = DVec3::ZERO;
        assert_eq!(
            collector.update(pose(), settings(), &vec![coincident; 9]),
            Err(Error::DegenerateCutoff)
        );
        assert_eq!(collector.time(), before_time);
    }

    #[test]
    #[expect(
        clippy::float_cmp,
        reason = "Exact source flags and preserved payload values must not admit a tolerance"
    )]
    fn live_child_pose_and_registration_inventory_follow_ecs_lifecycle() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Transform::default()))
            .id();
        let root = app
            .world_mut()
            .spawn(Transform::from_xyz(10.0, 0.0, 0.0))
            .id();
        let child = app
            .world_mut()
            .spawn((
                ChildOf(root),
                Transform::from_xyz(1.0, 0.0, 0.0),
                scene::SourceLight {
                    registration_order: 4,
                    parameters: lamp(),
                },
            ))
            .id();
        app.update();
        let (camera_pose, lights) =
            scene::harvest(app.world_mut(), camera, &[4]).expect("current transforms");
        assert_eq!(camera_pose.local_position, DVec3::ZERO);
        assert_eq!(lights[0].position, DVec3::new(11.0, 0.0, -2.0));
        app.world_mut()
            .get_mut::<Transform>(root)
            .expect("parent")
            .translation
            .x = 20.0;
        app.update();
        assert_eq!(
            scene::harvest(app.world_mut(), camera, &[4])
                .expect("moved light")
                .1[0]
                .position
                .x,
            21.0
        );
        assert!(matches!(
            scene::harvest(app.world_mut(), camera, &[4, 5]),
            Err(Error::InventoryMismatch)
        ));
        app.world_mut().despawn(child);
        assert!(matches!(
            scene::harvest(app.world_mut(), camera, &[4]),
            Err(Error::InventoryMismatch)
        ));
        app.world_mut()
            .get_mut::<Camera>(camera)
            .expect("camera")
            .is_active = false;
        assert!(matches!(
            scene::harvest(app.world_mut(), camera, &[]),
            Err(Error::MissingCamera)
        ));
    }

    #[test]
    fn missing_transform_and_duplicate_order_refuse_incomplete_scene() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        let camera = app
            .world_mut()
            .spawn((Camera3d::default(), Transform::default()))
            .id();
        let missing = app
            .world_mut()
            .spawn(scene::SourceLight {
                registration_order: 1,
                parameters: lamp(),
            })
            .id();
        app.update();
        assert!(matches!(
            scene::harvest(app.world_mut(), camera, &[1]),
            Err(Error::MissingLightTransform)
        ));
        app.world_mut()
            .entity_mut(missing)
            .insert(Transform::default());
        app.world_mut().spawn((
            Transform::default(),
            scene::SourceLight {
                registration_order: 1,
                parameters: lamp(),
            },
        ));
        app.update();
        assert!(matches!(
            scene::harvest(app.world_mut(), camera, &[1]),
            Err(Error::DuplicateOrder)
        ));
    }

    #[test]
    fn unused_lamps_do_not_poison_source_update_and_preview_camera_is_refused() {
        let mut collector = Collector::default();
        let mut bad = lamp();
        bad.position.x = f64::NAN;
        let mut day = settings();
        day.night = 0.0;
        assert_eq!(
            collector
                .update(pose(), day, &[bad.clone()])
                .expect("lamps not read by source in daylight")
                .count,
            0
        );
        bad.enabled = false;
        assert_eq!(
            collector
                .update(pose(), settings(), &[bad])
                .expect("disabled lamp not read")
                .count,
            0
        );
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, TransformPlugin));
        #[cfg(feature = "physics-rapier")]
        let camera = app
            .world_mut()
            .spawn((
                Camera3d::default(),
                Transform::default(),
                crate::tidewater_sky::preview::PreviewCamera,
            ))
            .id();
        let two_d = app.world_mut().spawn((Camera2d, Transform::default())).id();
        app.update();
        let mut excluded_views = vec![two_d];
        #[cfg(feature = "physics-rapier")]
        excluded_views.push(camera);
        for excluded in excluded_views {
            assert!(matches!(
                scene::harvest(app.world_mut(), excluded, &[]),
                Err(Error::MissingCamera)
            ));
        }
    }
}
