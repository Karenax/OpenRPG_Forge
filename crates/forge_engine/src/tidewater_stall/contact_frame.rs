//! Contact-shadow camera ABI and validation against an encoded opaque-depth copy.
//! A copy receipt describes command encoding, not GPU completion or visual parity.
use crate::tidewater_material::SceneLighting;
pub use crate::tidewater_ocean_depth::DepthCopy;
use crate::tidewater_temporal::frame::ViewPacket;
use bevy::{prelude::*, render::render_resource::ShaderType};

/// Actual render-camera metadata needed by contacts, independently of a TAA
/// phase or resolve owner. The adapter must join this tag to an encoded copy.
#[derive(Clone, Debug)]
pub struct CameraFrame {
    pub tag: crate::tidewater_temporal::frame::FrameTag,
    pub input_size: UVec2,
    pub history_epoch: u64,
    pub needs_history_seed: bool,
    pub view_from_world: Mat4,
    pub clip_from_world: Mat4,
    pub unjittered_clip_from_world: Mat4,
    pub previous_unjittered_clip_from_world: Mat4,
    pub camera_position: Vec3,
    pub near: f32,
}

impl From<&ViewPacket> for CameraFrame {
    fn from(packet: &ViewPacket) -> Self {
        let frame = &packet.frame;
        Self {
            tag: packet.tag,
            input_size: frame.input_size,
            history_epoch: frame.history_epoch,
            needs_history_seed: frame.needs_history_seed,
            view_from_world: frame.view_from_world,
            clip_from_world: frame.clip_from_world,
            unjittered_clip_from_world: frame.unjittered_clip_from_world,
            previous_unjittered_clip_from_world: frame.previous_unjittered_clip_from_world,
            camera_position: frame.camera_position,
            near: frame.near_far.x,
        }
    }
}

/// Binding 57: exact 256-byte WGSL `ContactShadowFrame` layout.
#[derive(Clone, Debug, ShaderType)]
pub struct Uniform {
    pub view: Mat4,
    pub view_proj: Mat4,
    pub previous_view_proj_no_jitter: Mat4,
    pub camera_position: Vec3,
    pub near: f32,
    pub sun_direction: Vec3,
    pub sea_level: f32,
    pub sun_color: Vec3,
    pub strength: f32,
    pub resolution: Vec2,
    pub frame_index: u32,
    pub padding: u32,
}

/// Build from the matching camera packet and the actual preceding opaque copy.
/// `frame_index` is the scene's monotone source frame counter, not Halton phase.
///
/// # Errors
/// Refuses missing, foreign, stale, resized/reset or matrix-mismatched history,
/// and invalid light/effect inputs. No empty or final-after-water depth fallback.
pub fn prepare(
    current: &ViewPacket,
    depth: Option<&DepthCopy>,
    light: &SceneLighting,
    sea_level: f32,
    strength: f32,
    frame_index: u32,
) -> Result<Uniform, String> {
    prepare_camera(
        &CameraFrame::from(current),
        depth,
        light,
        sea_level,
        strength,
        frame_index,
    )
}

/// Prepare from real camera metadata without requiring a temporal resolve.
/// The same strict depth/view/serial/epoch/projection join remains mandatory.
pub fn prepare_camera(
    current: &CameraFrame,
    depth: Option<&DepthCopy>,
    light: &SceneLighting,
    sea_level: f32,
    strength: f32,
    frame_index: u32,
) -> Result<Uniform, String> {
    let depth = depth.ok_or("Contact shadows require an encoded previous opaque-depth copy")?;
    if current.needs_history_seed
        || depth.tag.view != current.tag.view
        || depth.tag.serial.checked_add(1) != Some(current.tag.serial)
        || depth.size != current.input_size
        || depth.history_epoch != current.history_epoch
        || depth.unjittered_clip_from_world != current.previous_unjittered_clip_from_world
    {
        return Err(
            "Contact-shadow opaque depth does not match this view's previous camera".into(),
        );
    }
    checked_uniform(
        current,
        depth.unjittered_clip_from_world,
        light,
        sea_level,
        strength,
        frame_index,
    )
}

/// Explicitly suspend contact sampling on a first/reset/unavailable history frame.
/// Surface shading continues; this is never evidence of active contact shadows.
pub fn suspended(
    current: &ViewPacket,
    light: &SceneLighting,
    sea_level: f32,
    frame_index: u32,
) -> Result<Uniform, String> {
    suspend_camera(&CameraFrame::from(current), light, sea_level, frame_index)
}

/// Explicit first/reset-frame suspension for either camera ownership mode.
pub fn suspend_camera(
    current: &CameraFrame,
    light: &SceneLighting,
    sea_level: f32,
    frame_index: u32,
) -> Result<Uniform, String> {
    checked_uniform(
        current,
        current.unjittered_clip_from_world,
        light,
        sea_level,
        0.0,
        frame_index,
    )
}

fn checked_uniform(
    current: &CameraFrame,
    previous_clip: Mat4,
    light: &SceneLighting,
    sea_level: f32,
    strength: f32,
    frame_index: u32,
) -> Result<Uniform, String> {
    let direction = light.sun_direction.truncate();
    let color = light.sun_color.truncate();
    let frame = current;
    if !sea_level.is_finite()
        || !strength.is_finite()
        || !(0.0..=1.0).contains(&strength)
        || !direction.is_finite()
        || (direction.length_squared() - 1.0).abs() > 0.001
        || !color.is_finite()
        || color.min_element() < 0.0
        || !frame.view_from_world.is_finite()
        || !frame.clip_from_world.is_finite()
        || !frame.previous_unjittered_clip_from_world.is_finite()
        || !previous_clip.is_finite()
        || !frame.camera_position.is_finite()
        || !frame.near.is_finite()
        || frame.near <= 0.0
        || frame.input_size.min_element() == 0
    {
        return Err("Invalid contact-shadow frame or lighting input".into());
    }
    Ok(Uniform {
        view: frame.view_from_world,
        view_proj: frame.clip_from_world,
        previous_view_proj_no_jitter: previous_clip,
        camera_position: frame.camera_position,
        near: frame.near,
        sun_direction: direction,
        sea_level,
        sun_color: color,
        strength,
        resolution: frame.input_size.as_vec2(),
        frame_index,
        padding: 0,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::tidewater_temporal::frame::{FrameTag, Packet};
    use crate::tidewater_temporal_frame::{Input, Sequence, ViewKey};
    use bevy::render::render_resource::encase::UniformBuffer;

    fn fixture() -> (ViewPacket, DepthCopy, SceneLighting) {
        let entity = Entity::from_bits(7);
        let input = Input {
            world_from_view: Mat4::IDENTITY,
            clip_from_view: Mat4::perspective_infinite_reverse_rh(1.0, 1.0, 0.1),
            input_size: UVec2::new(64, 64),
            output_size: UVec2::new(64, 64),
            near: 0.1,
            far: 1000.0,
        };
        let mut sequence = Sequence::new(ViewKey(7));
        let previous = sequence.commit(sequence.prepare(input).unwrap()).unwrap();
        let frame = *sequence
            .prepare(Input {
                world_from_view: Mat4::from_translation(Vec3::new(1.0, 2.0, 3.0)),
                ..input
            })
            .unwrap()
            .frame();
        let packet = ViewPacket {
            packet: Packet::from_frame(&frame),
            tag: FrameTag {
                view: entity,
                serial: 1,
            },
            frame,
        };
        let copy = DepthCopy {
            image: Handle::default(),
            tag: FrameTag {
                view: entity,
                serial: 0,
            },
            size: input.input_size,
            history_epoch: previous.history_epoch,
            unjittered_clip_from_world: previous.unjittered_clip_from_world,
        };
        let light = SceneLighting::new([0.0, 1.0, 0.0], [8.0, 7.0, 6.0], [[0.0; 3]; 9]).unwrap();
        (packet, copy, light)
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn camera_contacts_preserve_actual_raster_projection_without_temporal_phase() {
        let (_, copy, light) = fixture();
        let world_from_view = Mat4::from_translation(Vec3::new(4.0, 5.0, 6.0));
        let view_from_world = world_from_view.inverse();
        let projection = Mat4::perspective_infinite_reverse_rh(1.0, 1.0, 0.06);
        let mut raster_projection = projection;
        raster_projection.z_axis.x += 0.003;
        let current = CameraFrame {
            tag: FrameTag {
                view: copy.tag.view,
                serial: copy.tag.serial + 1,
            },
            input_size: copy.size,
            history_epoch: copy.history_epoch,
            needs_history_seed: false,
            view_from_world,
            clip_from_world: raster_projection * view_from_world,
            unjittered_clip_from_world: projection * view_from_world,
            previous_unjittered_clip_from_world: copy.unjittered_clip_from_world,
            camera_position: world_from_view.w_axis.truncate(),
            near: 0.06,
        };
        let uniform = prepare_camera(&current, Some(&copy), &light, 0.0, 1.0, 97).unwrap();
        assert_eq!(uniform.view, view_from_world);
        assert_eq!(uniform.view_proj, current.clip_from_world);
        assert_ne!(uniform.view_proj, current.unjittered_clip_from_world);
        assert_eq!(
            uniform.previous_view_proj_no_jitter,
            copy.unjittered_clip_from_world
        );
        assert_eq!(uniform.camera_position, Vec3::new(4.0, 5.0, 6.0));
        assert_eq!(uniform.near, 0.06);
        assert_eq!(uniform.frame_index, 97);
        let reset = CameraFrame {
            needs_history_seed: true,
            ..current
        };
        assert!(prepare_camera(&reset, Some(&copy), &light, 0.0, 1.0, 98).is_err());
        let suspended = suspend_camera(&reset, &light, 0.0, 98).unwrap();
        assert_eq!(suspended.strength, 0.0);
        assert_eq!(suspended.view_proj, reset.clip_from_world);
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn contact_uniform_matches_gpu_layout_and_keeps_frame_counter() {
        let (packet, copy, light) = fixture();
        let uniform = prepare(&packet, Some(&copy), &light, 0.25, 0.8, 543).unwrap();
        let mut buffer = UniformBuffer::new(Vec::<u8>::new());
        buffer.write(&uniform).unwrap();
        let bytes = buffer.into_inner();
        assert_eq!(bytes.len(), 256);
        for (offset, expected) in [
            (0, packet.frame.view_from_world),
            (64, packet.frame.clip_from_world),
            (128, copy.unjittered_clip_from_world),
        ] {
            let expected = expected.to_cols_array().map(f32::to_le_bytes).concat();
            assert_eq!(&bytes[offset..offset + 64], expected);
        }
        let scalar = |offset| f32::from_le_bytes(bytes[offset..offset + 4].try_into().unwrap());
        assert_eq!(scalar(204), 0.1);
        assert_eq!(scalar(220), 0.25);
        assert_eq!(scalar(236), 0.8);
        assert_eq!(scalar(240), 64.0);
        assert_eq!(u32::from_le_bytes(bytes[248..252].try_into().unwrap()), 543);
        assert_ne!(543, u32::from(packet.frame.phase));
        assert_eq!(&bytes[252..256], &[0; 4]);
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn suspended_frame_never_samples_unmatched_depth_history() {
        let (mut packet, copy, light) = fixture();
        packet.frame.needs_history_seed = true;
        assert!(prepare(&packet, Some(&copy), &light, 0.0, 1.0, 90).is_err());
        let frame = suspended(&packet, &light, 0.0, 90).unwrap();
        assert_eq!(frame.strength, 0.0);
        assert_eq!(frame.frame_index, 90);
        assert_eq!(
            frame.previous_view_proj_no_jitter,
            packet.frame.unjittered_clip_from_world
        );
        assert_eq!(frame.camera_position, packet.frame.camera_position);
        let mut invalid = light;
        invalid.sun_direction.x = f32::NAN;
        assert!(suspended(&packet, &invalid, 0.0, 90).is_err());
    }

    #[test]
    fn contact_rejects_foreign_stale_resized_reset_and_mismatched_depth() {
        let (packet, copy, light) = fixture();
        assert!(prepare(&packet, None, &light, 0.0, 1.0, 5).is_err());
        for invalid in [
            DepthCopy {
                tag: FrameTag {
                    view: Entity::from_bits(8),
                    ..copy.tag
                },
                ..copy.clone()
            },
            DepthCopy {
                tag: FrameTag {
                    serial: 2,
                    ..copy.tag
                },
                ..copy.clone()
            },
            DepthCopy {
                size: UVec2::new(32, 32),
                ..copy.clone()
            },
            DepthCopy {
                history_epoch: 9,
                ..copy.clone()
            },
            DepthCopy {
                unjittered_clip_from_world: Mat4::IDENTITY,
                ..copy.clone()
            },
        ] {
            assert!(prepare(&packet, Some(&invalid), &light, 0.0, 1.0, 5).is_err());
        }
        let mut reset = packet.clone();
        reset.frame.needs_history_seed = true;
        assert!(prepare(&reset, Some(&copy), &light, 0.0, 1.0, 5).is_err());
        assert!(prepare(&packet, Some(&copy), &light, f32::NAN, 1.0, 5).is_err());
        assert!(prepare(&packet, Some(&copy), &light, 0.0, f32::NAN, 5).is_err());
    }
}
