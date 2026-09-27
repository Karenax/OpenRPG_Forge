use bevy::{core_pipeline::tonemapping::Tonemapping, prelude::*, render::view::ColorGrading};
use forge_project::{
    PostProcessQualitySettings, TerrainDocument, VisualQualityTier,
    VisualValleyQualityProfileDocument, VisualValleyQualityProfileError,
};
use serde::Serialize;

/// Optional renderer request consumed by the weather adapter, which owns camera post-processing.
#[derive(Resource)]
pub struct PostProcessQualityRequest {
    document: VisualValleyQualityProfileDocument,
    source_hash: String,
    tier: VisualQualityTier,
}

impl PostProcessQualityRequest {
    pub fn new(
        terrain: &TerrainDocument,
        document: VisualValleyQualityProfileDocument,
        tier: VisualQualityTier,
    ) -> Result<Self, VisualValleyQualityProfileError> {
        document.validate_identity(terrain.project_id, terrain.area_id, terrain.revision)?;
        let source_hash = document.content_hash()?.to_string();
        Ok(Self {
            document,
            source_hash,
            tier,
        })
    }

    pub const fn request(&mut self, tier: VisualQualityTier) {
        self.tier = tier;
    }

    pub(crate) fn settings(&self) -> PostProcessQualitySettings {
        self.document.preset(self.tier).post_process
    }

    pub(crate) fn observe(&self, matches: bool) -> PostProcessQualityObservation {
        PostProcessQualityObservation {
            policy_hash: self.source_hash.clone(),
            requested_tier: self.tier,
            projected_tier: matches.then_some(self.tier),
            requested: self.settings(),
            status: if matches {
                "observed_cpu_components_not_gpu_execution"
            } else {
                "incomplete_cpu_projection"
            },
        }
    }
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct PostProcessQualityObservation {
    pub policy_hash: String,
    pub requested_tier: VisualQualityTier,
    pub projected_tier: Option<VisualQualityTier>,
    pub requested: PostProcessQualitySettings,
    pub status: &'static str,
}

pub(crate) const fn tonemapping(enabled: bool) -> Tonemapping {
    if enabled {
        Tonemapping::TonyMcMapface
    } else {
        Tonemapping::None
    }
}

pub(crate) fn grading_matches(actual: &ColorGrading, expected: &ColorGrading) -> bool {
    let globals = |grade: &ColorGrading| {
        [
            grade.global.exposure,
            grade.global.temperature,
            grade.global.tint,
            grade.global.hue,
            grade.global.post_saturation,
            grade.global.midtones_range.start,
            grade.global.midtones_range.end,
        ]
        .map(f32::to_bits)
    };
    globals(actual) == globals(expected)
        && actual.shadows == expected.shadows
        && actual.midtones == expected.midtones
        && actual.highlights == expected.highlights
}
