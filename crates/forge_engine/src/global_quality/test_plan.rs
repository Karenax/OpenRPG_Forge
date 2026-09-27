//! Synthetic capabilities for adapter tests; not runtime qualification.
use forge_project::{VisualQualityTier, VisualValleyQualityProfileDocument, effective_quality::*};
use forge_world::quality_application::QualityApplicationGuard;

pub(crate) fn guard(
    source: &VisualValleyQualityProfileDocument,
    tier: VisualQualityTier,
) -> QualityApplicationGuard {
    guard_with_sequence(source, tier, 1)
}

pub(crate) fn guard_with_sequence(
    source: &VisualValleyQualityProfileDocument,
    tier: VisualQualityTier,
    sequence: u64,
) -> QualityApplicationGuard {
    use QualityCapability::*;
    let hash = source.content_hash().unwrap();
    let capabilities = QualityCapabilities {
        schema_version: 1,
        adapter_id: "synthetic-optical-installation".into(),
        generation: 1,
        terrain_constraints: None,
        source_bindings: Some(QualitySourceBindings {
            terrain: hash,
            vegetation: hash,
            environment: hash,
            water: hash,
            weather: hash,
        }),
        capabilities: [
            Terrain,
            Vegetation,
            Shadows,
            Sky,
            Rain,
            PostProcess,
            EnvironmentReflection,
            ScreenReflection,
            SurfaceTint,
            DistanceFog,
        ]
        .into_iter()
        .map(|c| (c, CapabilityStatus::Supported))
        .chain(
            [
                ReflectionIntensity,
                ReflectionDistance,
                ScreenRefraction,
                VolumetricFog,
            ]
            .into_iter()
            .map(|c| (c, CapabilityStatus::Unavailable)),
        )
        .collect(),
    };
    QualityApplicationGuard::new(
        source,
        tier,
        &capabilities,
        QualityResolutionPolicy::Adr0011,
        sequence,
        std::time::Duration::from_secs(10),
    )
    .unwrap()
}
