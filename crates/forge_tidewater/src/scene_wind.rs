//! Compatibility re-export; common source clocks live in the engine.
pub use forge_engine::source_wind::AcquiredFrame;
#[cfg(all(test, feature = "physics-rapier"))]
mod acquisition;
