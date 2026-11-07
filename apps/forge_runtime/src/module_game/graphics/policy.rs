//! Labels for the shared engine display policy.
pub use forge_engine::source_graphics::quality_policy::{Policy, TIERS};
use forge_project::VisualQualityTier;

pub fn label(tier: VisualQualityTier) -> &'static str {
    match tier {
        VisualQualityTier::Low => "Faible",
        VisualQualityTier::Medium => "Moyen",
        VisualQualityTier::High => "Élevé",
        VisualQualityTier::Ultra => "Ultra",
    }
}
