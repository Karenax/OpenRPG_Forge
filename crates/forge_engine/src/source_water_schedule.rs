//! Shared ordering contracts for water simulation and its optical consumers.
use bevy::prelude::SystemSet;

/// Downstream water and shore passes consume images after the FFT submission.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct OceanComputeSet;

/// Optical passes consume the completed shoreline simulation before camera work.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct ShoreComputeSet;
