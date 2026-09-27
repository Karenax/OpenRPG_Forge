use super::*;
fn fixture() -> (VisualValleyQualityProfileDocument, QualityCapabilities) {
    use QualityCapability::*;
    let source = VisualValleyQualityProfileDocument::from_json(include_str!(
        "../../../../examples/visual_valley/quality-profile.v1.json"
    ))
    .unwrap();
    let capabilities = [
        Terrain,
        Vegetation,
        Shadows,
        Sky,
        Rain,
        PostProcess,
        EnvironmentReflection,
        ReflectionIntensity,
        ScreenReflection,
        ReflectionDistance,
        SurfaceTint,
        ScreenRefraction,
        VolumetricFog,
        DistanceFog,
    ]
    .into_iter()
    .map(|cap| (cap, CapabilityStatus::Supported))
    .collect();
    (
        source,
        QualityCapabilities {
            schema_version: 1,
            adapter_id: "synthetic-test".into(),
            generation: 1,
            capabilities,
            terrain_constraints: None,
            source_bindings: None,
        },
    )
}

#[test]
fn every_domain_source_is_bound_to_the_candidate_plan() {
    let (source, mut caps) = fixture();
    let hash = source.content_hash().unwrap();
    let bindings = QualitySourceBindings {
        terrain: hash,
        vegetation: hash,
        environment: hash,
        water: hash,
        weather: hash,
    };
    caps.source_bindings = Some(bindings);
    let initial = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    for field in 0..5 {
        let mut changed = bindings;
        *match field {
            0 => &mut changed.terrain,
            1 => &mut changed.vegetation,
            2 => &mut changed.environment,
            3 => &mut changed.water,
            _ => &mut changed.weather,
        } = "aa".repeat(32).parse().unwrap();
        caps.source_bindings = Some(changed);
        let plan = resolve_effective_quality(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011,
        )
        .unwrap();
        assert_ne!(
            initial.content_hash().unwrap(),
            plan.content_hash().unwrap()
        );
        assert!(
            initial
                .verify(
                    &source,
                    VisualQualityTier::High,
                    &caps,
                    QualityResolutionPolicy::Adr0011
                )
                .is_err()
        );
        let decoded: EffectiveQualityPlan =
            serde_json::from_str(&serde_json::to_string(&plan).unwrap()).unwrap();
        assert_eq!(decoded.source_bindings, Some(changed));
    }
    caps.terrain_constraints = Some(TerrainQualityConstraints {
        terrain_hash: "bb".repeat(32).parse().unwrap(),
        canonical_chunks: 64,
        full_resolution_chunks: vec![EntityId::deterministic("bank")],
    });
    assert!(matches!(
        resolve_effective_quality(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011
        ),
        Err(QualityResolutionError::InvalidTerrainConstraints)
    ));
}

#[test]
fn terrain_constraints_bind_hashes_and_declare_the_approved_exception() {
    let (source, mut caps) = fixture();
    let constraints = TerrainQualityConstraints {
        terrain_hash: source.content_hash().unwrap(),
        canonical_chunks: 64,
        full_resolution_chunks: vec![EntityId::deterministic("synthetic-bank")],
    };
    caps.terrain_constraints = Some(constraints.clone());
    let plan = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(plan.blockers.is_empty());
    assert_eq!(
        plan.substitutions,
        vec![QualitySubstitution::TerrainBanksFullResolution]
    );
    assert_eq!(plan.terrain_constraints, Some(constraints.clone()));
    let decoded: EffectiveQualityPlan =
        serde_json::from_str(&serde_json::to_string(&plan).unwrap()).unwrap();
    assert_eq!(
        plan.content_hash().unwrap(),
        decoded.content_hash().unwrap()
    );
    caps.terrain_constraints
        .as_mut()
        .unwrap()
        .full_resolution_chunks[0] = EntityId::deterministic("different-bank");
    let changed = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert_ne!(
        plan.content_hash().unwrap(),
        changed.content_hash().unwrap()
    );
    assert!(
        plan.verify(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011
        )
        .is_err()
    );
    for invalid in [
        TerrainQualityConstraints {
            canonical_chunks: 0,
            ..constraints.clone()
        },
        TerrainQualityConstraints {
            full_resolution_chunks: vec![],
            ..constraints.clone()
        },
        TerrainQualityConstraints {
            full_resolution_chunks: vec![constraints.full_resolution_chunks[0]; 2],
            ..constraints.clone()
        },
    ] {
        caps.terrain_constraints = Some(invalid);
        assert!(matches!(
            resolve_effective_quality(
                &source,
                VisualQualityTier::High,
                &caps,
                QualityResolutionPolicy::Adr0011
            ),
            Err(QualityResolutionError::InvalidTerrainConstraints)
        ));
    }
}

#[test]
fn terrain_exception_requires_approval_and_a_qualified_projection() {
    let (source, mut caps) = fixture();
    caps.terrain_constraints = Some(TerrainQualityConstraints {
        terrain_hash: source.content_hash().unwrap(),
        canonical_chunks: 64,
        full_resolution_chunks: vec![EntityId::deterministic("synthetic-bank")],
    });
    for status in [
        CapabilityStatus::Supported,
        CapabilityStatus::Unavailable,
        CapabilityStatus::Unqualified,
    ] {
        caps.capabilities.insert(QualityCapability::Terrain, status);
        for policy in [
            QualityResolutionPolicy::Strict,
            QualityResolutionPolicy::Adr0011,
        ] {
            let plan =
                resolve_effective_quality(&source, VisualQualityTier::High, &caps, policy).unwrap();
            let accepted =
                status == CapabilityStatus::Supported && policy == QualityResolutionPolicy::Adr0011;
            assert_eq!(
                plan.blockers.contains(&QualityCapability::Terrain),
                !accepted
            );
            assert_eq!(
                plan.substitutions
                    .contains(&QualitySubstitution::TerrainBanksFullResolution),
                accepted
            );
            assert_eq!(plan.effective.terrain, source.presets.high.terrain);
        }
    }
}

#[test]
fn reflection_gain_requires_explicit_approval_and_never_qualifies_reflection_paths() {
    let (source, mut caps) = fixture();
    let before = source.content_hash().unwrap();
    for status in [CapabilityStatus::Unavailable, CapabilityStatus::Unqualified] {
        caps.capabilities
            .insert(QualityCapability::ReflectionIntensity, status);
        for policy in [
            QualityResolutionPolicy::Strict,
            QualityResolutionPolicy::Adr0011,
        ] {
            let plan =
                resolve_effective_quality(&source, VisualQualityTier::High, &caps, policy).unwrap();
            let accepted = status == CapabilityStatus::Unavailable
                && policy == QualityResolutionPolicy::Adr0011;
            assert_eq!(
                plan.substitutions
                    .contains(&QualitySubstitution::ReflectionIntensityNotApplied),
                accepted
            );
            assert_eq!(
                plan.blockers
                    .contains(&QualityCapability::ReflectionIntensity),
                !accepted
            );
        }
    }
    caps.capabilities
        .remove(&QualityCapability::ReflectionIntensity);
    let missing = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(
        missing
            .blockers
            .contains(&QualityCapability::ReflectionIntensity)
    );
    caps.capabilities.insert(
        QualityCapability::ReflectionIntensity,
        CapabilityStatus::Unavailable,
    );
    caps.capabilities.insert(
        QualityCapability::EnvironmentReflection,
        CapabilityStatus::Unqualified,
    );
    caps.capabilities.insert(
        QualityCapability::ScreenReflection,
        CapabilityStatus::Unqualified,
    );
    let plan = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(
        plan.blockers
            .contains(&QualityCapability::EnvironmentReflection)
    );
    assert!(plan.blockers.contains(&QualityCapability::ScreenReflection));
    assert!(
        plan.substitutions
            .contains(&QualitySubstitution::ReflectionIntensityNotApplied)
    );
    let mut decoded: EffectiveQualityPlan =
        serde_json::from_str(&serde_json::to_string(&plan).unwrap()).unwrap();
    assert_eq!(
        decoded.content_hash().unwrap(),
        plan.content_hash().unwrap()
    );
    decoded.substitutions.clear();
    assert!(
        decoded
            .verify(
                &source,
                VisualQualityTier::High,
                &caps,
                QualityResolutionPolicy::Adr0011
            )
            .is_err()
    );
    assert_eq!(before, source.content_hash().unwrap());
}

#[test]
fn approved_fallbacks_are_explicit_and_do_not_edit_sources() {
    let (source, mut caps) = fixture();
    let before = source.content_hash().unwrap();
    for cap in [
        QualityCapability::ScreenRefraction,
        QualityCapability::VolumetricFog,
        QualityCapability::ReflectionDistance,
    ] {
        caps.capabilities.insert(cap, CapabilityStatus::Unavailable);
    }
    let strict = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Strict,
    )
    .unwrap();
    assert_eq!(strict.blockers.len(), 3);
    assert!(strict.substitutions.is_empty());
    let plan = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(plan.blockers.is_empty());
    assert_eq!(plan.substitutions.len(), 3);
    assert_eq!(plan.effective.water.reflection_max_distance_m, None);
    assert_eq!(
        plan.effective.water.refraction,
        QualityWaterRefraction::SurfaceTint
    );
    assert_eq!(plan.effective.water.refraction_steps, 0);
    assert!(!plan.effective.environment.volumetric_fog_enabled);
    assert_eq!(source.content_hash().unwrap(), before);
    assert_eq!(plan.requested_policy_hash, before);
}

#[test]
fn unknown_and_missing_replacement_capabilities_never_resolve() {
    let (source, mut caps) = fixture();
    caps.capabilities
        .remove(&QualityCapability::EnvironmentReflection);
    caps.capabilities.insert(
        QualityCapability::ScreenRefraction,
        CapabilityStatus::Unavailable,
    );
    caps.capabilities.insert(
        QualityCapability::SurfaceTint,
        CapabilityStatus::Unqualified,
    );
    let plan = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(
        plan.blockers
            .contains(&QualityCapability::EnvironmentReflection)
    );
    assert!(plan.blockers.contains(&QualityCapability::SurfaceTint));
    assert!(
        !plan
            .substitutions
            .contains(&QualitySubstitution::ScreenRefractionToSurfaceTint)
    );
}

#[test]
fn roundtrip_hash_and_revalidation_bind_identity_and_effective_parameters() {
    let (source, mut caps) = fixture();
    let plan = resolve_effective_quality(
        &source,
        VisualQualityTier::Low,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    let mut decoded: EffectiveQualityPlan =
        serde_json::from_str(&serde_json::to_string(&plan).unwrap()).unwrap();
    assert_eq!(
        decoded.content_hash().unwrap(),
        plan.content_hash().unwrap()
    );
    decoded
        .verify(
            &source,
            VisualQualityTier::Low,
            &caps,
            QualityResolutionPolicy::Adr0011,
        )
        .unwrap();
    decoded.effective.rain.drop_budget += 1;
    assert!(
        decoded
            .verify(
                &source,
                VisualQualityTier::Low,
                &caps,
                QualityResolutionPolicy::Adr0011
            )
            .is_err()
    );
    caps.generation += 1;
    assert!(
        plan.verify(
            &source,
            VisualQualityTier::Low,
            &caps,
            QualityResolutionPolicy::Adr0011
        )
        .is_err()
    );
    assert_ne!(
        plan.content_hash().unwrap(),
        resolve_effective_quality(
            &source,
            VisualQualityTier::Low,
            &caps,
            QualityResolutionPolicy::Adr0011
        )
        .unwrap()
        .content_hash()
        .unwrap()
    );
}

#[test]
fn all_tiers_are_deterministic_and_unused_optical_capabilities_do_not_block_low() {
    let (source, mut caps) = fixture();
    for cap in [
        QualityCapability::ScreenRefraction,
        QualityCapability::VolumetricFog,
        QualityCapability::ReflectionDistance,
    ] {
        caps.capabilities.insert(cap, CapabilityStatus::Unavailable);
    }
    for (tier, substitutions) in [
        (VisualQualityTier::Low, 0),
        (VisualQualityTier::Medium, 2),
        (VisualQualityTier::High, 3),
        (VisualQualityTier::Ultra, 3),
    ] {
        let plan =
            resolve_effective_quality(&source, tier, &caps, QualityResolutionPolicy::Adr0011)
                .unwrap();
        assert!(plan.blockers.is_empty());
        assert_eq!(plan.substitutions.len(), substitutions);
        assert_eq!(
            plan,
            resolve_effective_quality(&source, tier, &caps, QualityResolutionPolicy::Adr0011)
                .unwrap()
        );
        assert_eq!(plan.effective.terrain, source.preset(tier).terrain);
        assert_eq!(plan.effective.vegetation, source.preset(tier).vegetation);
        assert_eq!(plan.effective.shadows, source.preset(tier).shadows);
        assert_eq!(plan.effective.rain, source.preset(tier).rain);
        assert_eq!(
            plan.effective.post_process,
            source.preset(tier).post_process
        );
    }
    caps.capabilities
        .remove(&QualityCapability::ScreenRefraction);
    let low = resolve_effective_quality(
        &source,
        VisualQualityTier::Low,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(low.blockers.is_empty());
    assert!(
        low.verify(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011
        )
        .is_err()
    );
    assert!(
        low.verify(
            &source,
            VisualQualityTier::Low,
            &caps,
            QualityResolutionPolicy::Strict
        )
        .is_err()
    );
    let high = resolve_effective_quality(
        &source,
        VisualQualityTier::High,
        &caps,
        QualityResolutionPolicy::Adr0011,
    )
    .unwrap();
    assert!(high.blockers.contains(&QualityCapability::ScreenRefraction));
}

#[test]
fn invalid_capability_identity_and_unknown_serialized_values_are_rejected() {
    let (source, caps) = fixture();
    for (version, generation, id) in [
        (2, 1, "adapter"),
        (1, 0, "adapter"),
        (1, 1, ""),
        (1, 1, "../adapter"),
    ] {
        let mut invalid = caps.clone();
        invalid.schema_version = version;
        invalid.generation = generation;
        invalid.adapter_id = id.into();
        assert!(
            resolve_effective_quality(
                &source,
                VisualQualityTier::High,
                &invalid,
                QualityResolutionPolicy::Adr0011
            )
            .is_err()
        );
    }
    let mut json = serde_json::to_value(&caps).unwrap();
    json["capabilities"]["environment_reflection"] = "unknown".into();
    assert!(serde_json::from_value::<QualityCapabilities>(json).is_err());
    let mut json = serde_json::to_value(&caps).unwrap();
    json["extra"] = true.into();
    assert!(serde_json::from_value::<QualityCapabilities>(json).is_err());
}
