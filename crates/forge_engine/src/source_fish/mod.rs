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
/// Original source prop draw, shadow and transition distances.
pub const SOURCE_PROP_POLICY: crate::distance_lod::Policy =
    match crate::distance_lod::Policy::new(90.0, 81.0, 22.0, 28.0, 0.88) {
        Ok(policy) => policy,
        Err(_) => panic!("Invalid source fish LOD policy"),
    };
