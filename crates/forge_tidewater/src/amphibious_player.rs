//! Compatibility export and map-owned regression harness for the shared controller.
pub use forge_engine::amphibious_player::*;

// Compile the exact engine source as a test-only module so historical Tidewater
// regression fixtures stay map-owned and private controller invariants stay testable.
#[cfg(test)]
#[allow(
    dead_code,
    clippy::needless_pass_by_value,
    reason = "Test harness compiles the public engine implementation with Bevy system parameters"
)]
mod reference_tests {
    include!("../../forge_engine/src/amphibious_player.rs");
    mod tests {
        include!("amphibious_player_tests.rs");
    }
}
