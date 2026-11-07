#![deny(unsafe_code)]
//! Optional map integration, outside the generic engine.

pub mod amphibious_locomotion;
pub mod breaker_geometry;
pub mod scene_wind;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_assets;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_beach_props;
pub mod tidewater_boardwalk;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_boat;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_debris;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_fish_props;
pub mod tidewater_object_shadow;
#[cfg(feature = "physics-rapier")]
pub mod tidewater_pier;
pub mod tidewater_pier_animation;
pub mod tidewater_plants;
pub mod tidewater_plaza;
pub mod tidewater_stall;
pub mod tidewater_village_props;

#[cfg(feature = "physics-rapier")]
pub mod amphibious_player;
#[cfg(feature = "physics-rapier")]
pub use amphibious_locomotion::ClimbableLadder;
#[cfg(feature = "physics-rapier")]
pub use amphibious_player::{AmphibiousPlayerInput, AmphibiousPlayerPlugin, AmphibiousPlayerState};
