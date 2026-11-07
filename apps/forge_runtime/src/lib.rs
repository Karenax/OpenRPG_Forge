#![deny(unsafe_code)]

//! Shared native runtime entry points for admitted Forge modules.

#[cfg(all(feature = "module-game", not(target_arch = "wasm32")))]
#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub mod module_game;
