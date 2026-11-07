// SPDX-License-Identifier: MIT
// Source lighting publication adapted from Tidewater App at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Shared solar publication, preserving the existing clock, atmosphere cadence and ABI.
use super::{SkyMaterial, frame_lighting::FrameLighting, solar, solar_lighting};
use crate::{source_atmosphere as atmosphere, source_environment::scene::ResidentEnvironment};
use bevy::prelude::*;

/// One clock. A requested starting pose waits for the acquired atmosphere's first
/// completed readback; a new pose then primes its own readback before advancing.
pub struct Clock {
    source: solar::Clock,
    target: solar::Settings,
    primed: bool,
    applied: bool,
}

pub struct Step {
    pub frame: solar::Frame,
    pub changed: bool,
}

impl Clock {
    pub fn from_acquired(
        initial: solar::Settings,
        target: solar::Settings,
        acquired: Vec3,
    ) -> Result<Self, String> {
        let clock = solar::Clock::new(initial)?;
        solar::Clock::new(target)?;
        if clock.frame().sun.as_vec3() != acquired {
            return Err("Acquired sun differs from the declared initial solar clock".into());
        }
        Ok(Self {
            source: clock,
            target,
            primed: false,
            applied: initial == target,
        })
    }

    pub fn advance(&mut self, delta: f64) -> Result<Step, String> {
        if !delta.is_finite() || delta < 0.0 {
            return Err("Invalid solar publication delta".into());
        }
        let mut changed = false;
        if self.primed && !self.applied {
            self.source.set(self.target)?;
            self.applied = true;
            self.primed = false;
            changed = true;
        } else if self.primed {
            self.source.advance(delta)?;
        }
        Ok(Step {
            frame: *self.source.frame(),
            changed,
        })
    }

    pub fn acknowledge_readback(&mut self) {
        self.primed = true;
    }
    #[must_use]
    pub const fn ready(&self) -> bool {
        self.primed && self.applied
    }
}

pub struct Publication {
    pub frame: solar::Frame,
    pub lighting: solar_lighting::Lighting,
    pub packed: FrameLighting,
    pub params: [Vec4; 4],
}

impl Publication {
    /// The diffuse SH remains owned by the HDR graph, never by this CPU update.
    #[allow(
        clippy::cast_possible_truncation,
        reason = "FrameLighting validated f32 representability"
    )]
    pub fn publish_direct(&self, environment: &mut ResidentEnvironment) {
        environment.scene_lighting.sun_direction = self.frame.key.as_vec3().extend(0.0);
        environment.scene_lighting.sun_color =
            Vec3::from_array(self.lighting.sun_color.map(|v| v as f32)).extend(0.0);
    }
}

/// Advance the existing atmosphere controller before cloud submission. This is
/// the Studio's original path; map-specific optical/material adapters stay callers.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated solar GPU values"
)]
pub fn advance(
    world: &mut World,
    environment: &mut ResidentEnvironment,
    delta: f64,
    camera_y: f64,
    frame: solar::Frame,
) -> Result<Option<Publication>, String> {
    validate_camera_height(camera_y)?;
    let mut params = world
        .get_resource::<atmosphere::Targets>()
        .ok_or("Solar atmosphere targets absent")?
        .params;
    params[1].w = atmosphere::observer::radius(camera_y)?;
    params[3] = frame.sun.as_vec3().extend(params[3].w);
    let transmittance = environment
        .atmosphere
        .images
        .first()
        .ok_or("Solar transmittance absent")?
        .clone();
    let publication = advance_uniforms(world, delta, frame, params, Some(&transmittance), None)?;
    environment.atmosphere.params = params;
    Ok(publication)
}

/// Shared by the module/Studio environment and the legacy presentation probe.
/// None selects every resident sky, as the original presentation menu did.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated solar GPU values"
)]
pub fn advance_uniforms(
    world: &mut World,
    delta: f64,
    frame: solar::Frame,
    params: [Vec4; 4],
    transmittance: Option<&Handle<Image>>,
    moon_intensity: Option<f32>,
) -> Result<Option<Publication>, String> {
    if moon_intensity.is_some_and(|v| !v.is_finite() || v < 0.0) {
        return Err("Invalid source moon intensity".into());
    }
    let values = std::array::from_fn(|i| params[i / 4][i % 4]);
    atmosphere::controller::advance(world, delta, frame, values, false)?;
    update_skies(world, frame, params, transmittance, moon_intensity)?;
    let Some(completed) = atmosphere::readback::latest(world, frame) else {
        return Ok(None);
    };
    let completed = completed?;
    let packed = FrameLighting::new(frame, &completed.lighting)?;
    Ok(Some(Publication {
        frame,
        lighting: completed.lighting,
        packed,
        params,
    }))
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated solar GPU values"
)]
fn update_skies(
    world: &mut World,
    frame: solar::Frame,
    params: [Vec4; 4],
    transmittance: Option<&Handle<Image>>,
    moon_intensity: Option<f32>,
) -> Result<(), String> {
    let Some(mut skies) = world.get_resource_mut::<Assets<SkyMaterial>>() else {
        return if transmittance.is_some() {
            Err("Solar sky assets absent".into())
        } else {
            Ok(())
        };
    };
    let changed: Vec<_> = skies
        .iter()
        .filter_map(|(id, sky)| {
            let moon = frame
                .moon
                .as_vec3()
                .extend(moon_intensity.unwrap_or(sky.settings.moon_disk.w));
            (transmittance.is_none_or(|handle| sky.transmittance == *handle)
                && (sky.atmosphere != params
                    || sky.settings.moon_disk != moon
                    || sky.settings.stars_time_night.z.to_bits() != (frame.night as f32).to_bits()))
            .then_some((id, moon))
        })
        .collect();
    for (id, moon) in changed {
        let mut sky = skies.get_mut(id).ok_or("Resident solar sky disappeared")?;
        sky.atmosphere = params;
        sky.settings.moon_disk = moon;
        sky.settings.stars_time_night.z = frame.night as f32;
    }
    Ok(())
}

/// Retained approximation for a consumer that still owns only an acquired SH.
/// Live HDR consumers keep their GPU-owned SH and must not use this path.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Retained source clamped irradiance ratio"
)]
#[must_use]
pub fn scaled_reference(
    reference: &crate::source_lighting::SceneLighting,
    captured_irradiance: [f64; 3],
    publication: &Publication,
) -> crate::source_lighting::SceneLighting {
    let ratio = Vec3::from_array(std::array::from_fn(|i| {
        (publication.lighting.sky_irradiance[i] / captured_irradiance[i].max(1e-8)).clamp(0.0, 8.0)
            as f32
    }));
    crate::source_lighting::SceneLighting {
        sun_direction: publication.frame.key.as_vec3().extend(0.0),
        sun_color: Vec3::from_array(publication.lighting.sun_color.map(|v| v as f32)).extend(0.0),
        environment_sh: reference
            .environment_sh
            .map(|v| (v.truncate() * ratio).extend(0.0)),
    }
}

/// Preserve the presentation path's calibration of the existing native sun.
pub fn sync_directional_lights(
    world: &mut World,
    scene: &crate::source_lighting::SceneLighting,
    frame: solar::Frame,
) {
    let color = scene.sun_color.truncate();
    let strength = color.max_element().max(0.00001);
    let direction = -frame.key.as_vec3();
    let up = if direction.dot(Vec3::Y).abs() > 0.999 {
        Vec3::Z
    } else {
        Vec3::Y
    };
    for (mut light, mut pose) in world
        .query::<(&mut DirectionalLight, &mut Transform)>()
        .iter_mut(world)
    {
        light.color = Color::linear_rgb(color.x / strength, color.y / strength, color.z / strength);
        light.illuminance = 10000.0 * strength / 11.0;
        *pose = Transform::default().looking_to(direction, up);
    }
}

pub fn validate_camera_height(camera_y: f64) -> Result<(), String> {
    if !camera_y.is_finite() || !(-1000.0..=60000.0).contains(&camera_y) {
        return Err("Invalid camera height for solar atmosphere".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn solar_publication_clock_primes_captured_then_requested_pose_before_running() {
        let initial = solar::Settings::default();
        let acquired = solar::Clock::new(initial).unwrap().frame().sun.as_vec3();
        let target = solar::Settings {
            hours: 0.0,
            speed: 0.01,
            ..initial
        };
        let mut clock = Clock::from_acquired(initial, target, acquired).unwrap();
        assert_eq!(
            clock.advance(20.0).unwrap().frame.hours.to_bits(),
            initial.hours.to_bits()
        );
        assert!(!clock.ready());
        clock.acknowledge_readback();
        let step = clock.advance(1.0).unwrap();
        assert!(step.changed);
        assert_eq!(step.frame.hours.to_bits(), 0.0_f64.to_bits());
        assert_eq!(step.frame.night.to_bits(), 1.0_f64.to_bits());
        assert!(!clock.ready());
        assert_eq!(
            clock.advance(100.0).unwrap().frame.hours.to_bits(),
            0.0_f64.to_bits()
        );
        clock.acknowledge_readback();
        assert!(clock.ready());
        assert!((clock.advance(1.0).unwrap().frame.hours - 0.01).abs() < 1e-12);
    }
    #[test]
    fn solar_publication_clock_refuses_mismatched_capture_and_invalid_delta_without_switching() {
        let initial = solar::Settings::default();
        assert!(Clock::from_acquired(initial, initial, Vec3::Y).is_err());
        let acquired = solar::Clock::new(initial).unwrap().frame().sun.as_vec3();
        let mut clock = Clock::from_acquired(
            initial,
            solar::Settings {
                hours: 0.0,
                ..initial
            },
            acquired,
        )
        .unwrap();
        clock.acknowledge_readback();
        assert!(clock.advance(f64::NAN).is_err());
        assert!(clock.advance(-1.0).is_err());
        assert!(clock.advance(0.0).unwrap().changed);
    }
}
