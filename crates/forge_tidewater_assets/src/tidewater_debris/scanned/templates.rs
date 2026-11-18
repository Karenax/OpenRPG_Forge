//! Tidewater labels and revision for the common scanned assets.
use super::{geometry, revision};

pub use forge_assets::source_scanned_package::templates::Template;

/// Prepare the original Tidewater variations and pose defaults.
///
/// # Errors
/// Propagates validation errors from the common scanned asset reader.
pub fn prepare(geometry: &geometry::Geometry) -> Result<Vec<Template>, String> {
    let names = [
        "Tronc échoué",
        "Branche échouée",
        "Branche échouée",
        "Coquillage",
    ];
    forge_assets::source_scanned_package::templates::prepare(geometry, revision(), &names)
}
