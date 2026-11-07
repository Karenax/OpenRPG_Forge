//! Reusable source-fish projection. Callers supply their own verified content.

pub mod clock;
pub mod geometry;
pub mod group;
pub mod host;
pub mod material;
pub mod pose;
pub mod prepare;
#[cfg(feature = "physics-rapier")]
pub mod resident;
pub mod shadow;
pub mod surface;
