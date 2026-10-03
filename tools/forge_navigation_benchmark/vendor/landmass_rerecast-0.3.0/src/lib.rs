#![deny(unsafe_code)]

//! Lab-only direct conversion between Rerecast and Landmass raw navigation documents.
//!
//! The upstream crate also owns a Bevy asset/plugin bridge. The isolated benchmark does not use
//! that bridge, so this copy keeps only the public raw conversion and its direct dependencies.

mod raw_conversion;
pub use raw_conversion::convert_rerecast_navmesh_to_landmass_navmesh;
