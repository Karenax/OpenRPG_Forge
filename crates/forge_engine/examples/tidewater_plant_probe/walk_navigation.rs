//! Bounded input replay from the beach to the inland vegetation, without teleporting.
use bevy::prelude::*;
use forge_engine::{PlayerControlCommand, PlayerRuntimeSnapshot};

const POINTS: [[f32; 2]; 5] = [
    [20.0, -90.0],
    [0.0, -90.0],
    [-20.0, -110.0],
    [-40.0, -150.0],
    [-60.0, -190.0],
];

#[derive(Default, serde::Serialize)]
pub(super) struct Route {
    pub enabled: bool,
    pub reached: Vec<[f32; 3]>,
    pub distance: f32,
    pub grounded_ticks: u32,
    pub ticks: u32,
    #[serde(skip)]
    previous: Option<Vec3>,
    #[serde(skip)]
    sensitivity: f32,
}

impl Route {
    pub fn new() -> Self {
        let profile = forge_world::embedded_visual_valley_player_controller_profile()
            .expect("validated player profile");
        Self {
            enabled: std::env::var("FORGE_TIDEWATER_NAVIGATION_SMOKE").as_deref() == Ok("1"),
            sensitivity: profile
                .camera
                .look_sensitivity_degrees_per_unit
                .to_radians(),
            ..default()
        }
    }

    pub fn complete(&self) -> bool {
        !self.enabled || self.reached.len() == POINTS.len()
    }

    pub fn valid(&self) -> bool {
        !self.enabled
            || self.complete() && self.distance > 150.0 && self.grounded_ticks > self.ticks / 2
    }

    pub fn command(&mut self, state: &PlayerRuntimeSnapshot) -> PlayerControlCommand {
        if let Some(previous) = self.previous.replace(state.player_position) {
            self.distance += (state.player_position - previous).xz().length();
        }
        self.ticks += 1;
        self.grounded_ticks += u32::from(state.grounded);
        let Some(target) = POINTS.get(self.reached.len()) else {
            return default();
        };
        let delta = Vec2::from_array(*target) - state.player_position.xz();
        if delta.length() < 0.7 {
            self.reached.push(state.player_position.to_array());
            info!(waypoint = self.reached.len(), position = ?state.player_position, "inland waypoint reached");
            return default();
        }
        let yaw = (-delta.x).atan2(-delta.y);
        let difference = (yaw - state.camera_yaw_radians + std::f32::consts::PI)
            .rem_euclid(std::f32::consts::TAU)
            - std::f32::consts::PI;
        PlayerControlCommand {
            move_axis: Vec2::Y,
            sprint: true,
            look_delta: Vec2::new(-difference / self.sensitivity, 0.0),
            ..default()
        }
    }
}
