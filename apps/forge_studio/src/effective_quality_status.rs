use forge_project::effective_quality::{
    EffectiveQualityPlan, QualityCapability, QualitySubstitution,
};

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ReflectionPreviewMode {
    Profile,
    EnvironmentOnly,
    Disabled,
}

impl ReflectionPreviewMode {
    pub const fn flags(self) -> (bool, bool) {
        match self {
            Self::Profile => (false, false),
            Self::EnvironmentOnly => (false, true),
            Self::Disabled => (true, false),
        }
    }

    pub const fn from_observed(all_disabled: bool, screen_disabled: bool) -> Self {
        if all_disabled {
            Self::Disabled
        } else if screen_disabled {
            Self::EnvironmentOnly
        } else {
            Self::Profile
        }
    }

    pub const fn label(self) -> &'static str {
        match self {
            Self::Profile => "Reflets du profil",
            Self::EnvironmentOnly => "Reflets d’environnement seuls · reflets écran désactivés",
            Self::Disabled => "Aperçu sans reflets",
        }
    }
}

pub fn adaptation_summary(
    status: &str,
    acknowledged: bool,
    proposals: u64,
    error: Option<&str>,
) -> String {
    let phase = match status {
        "disabled" => "Mode fixe · choix manuels prioritaires",
        "waiting_application" => "Adaptation en attente du plan effectif et du GPU",
        "waiting_focus" => {
            "Adaptation en pause · réaffichez le Studio et cliquez dans le viewport ; aucun échantillon admissible"
        }
        "tracking" if acknowledged && error.is_none() => {
            "Adaptation active · profil effectif acquitté"
        }
        "applying" => "Changement adaptatif en cours · nouveau profil non encore acquitté",
        "stopped" => "Adaptation arrêtée · aucun retour arrière confirmé",
        _ => "Adaptation non confirmée",
    };
    format!(
        "{phase} · {proposals} proposition(s). {}",
        error.unwrap_or("")
    )
}

pub fn application_summary(
    status: &str,
    projected: usize,
    error: Option<&str>,
    gpu_ready: bool,
) -> String {
    if status == "plan_application_ready" && projected == 7 && error.is_none() && gpu_ready {
        return "Plan effectif préparé et soumis au GPU ; 7/7 domaines observés. Preset demandé complet et qualité visuelle non validés.".into();
    }
    let phase = match status {
        "plan_application_pending_gpu" => "application en attente de preuve GPU",
        "plan_application_waiting_renderer" => {
            "preuve GPU périmée ; attente des ressources avant une nouvelle application"
        }
        "plan_application_failed" => "échec d'acquittement, aucun retour arrière confirmé",
        "plan_application_invalidated" => "plan périmé, réglages dérivés retirés",
        "plan_application_rejected" => "installation refusée",
        _ => "application non qualifiée",
    };
    format!(
        "{phase} ; {projected}/7 domaines observés côté CPU ; GPU non confirmé. {}",
        error.unwrap_or("")
    )
}

/// A blocked plan is not a background operation that will eventually complete.
pub fn projection_summary(
    tier: forge_project::VisualQualityTier,
    budgets_projected: bool,
    plan: Option<&EffectiveQualityPlan>,
) -> String {
    if let Some(plan) = plan
        && !plan.blockers.is_empty()
    {
        return format!(
            "{tier:?} demandé · profil incomplet, capacités à qualifier : {}. Aucun passage automatique à un profil validé.",
            plan.blockers
                .iter()
                .map(|value| capability_label(*value))
                .collect::<Vec<_>>()
                .join(", ")
        );
    }
    format!(
        "{tier:?} demandé · {}",
        if budgets_projected {
            "budgets disponibles projetés côté CPU · reçu GPU dans le détail du profil effectif · preset complet non validé"
        } else {
            "projection incomplète ou non observée · consulter le profil effectif"
        }
    )
}

fn capability_label(value: QualityCapability) -> &'static str {
    match value {
        QualityCapability::Terrain => "terrain",
        QualityCapability::Vegetation => "végétation",
        QualityCapability::Shadows => "ombres",
        QualityCapability::Sky => "ciel",
        QualityCapability::Rain => "pluie",
        QualityCapability::PostProcess => "post-traitement",
        QualityCapability::EnvironmentReflection => "reflets d'environnement",
        QualityCapability::ReflectionIntensity => "intensité des reflets",
        QualityCapability::ScreenReflection => "reflets écran",
        QualityCapability::ReflectionDistance => "distance des reflets écran",
        QualityCapability::SurfaceTint => "teinte de l'eau",
        QualityCapability::ScreenRefraction => "réfraction écran",
        QualityCapability::VolumetricFog => "brouillard volumétrique",
        QualityCapability::DistanceFog => "brouillard de distance",
    }
}

pub fn summary(plan: &EffectiveQualityPlan) -> String {
    let substitutions = plan
        .substitutions
        .iter()
        .map(|value| match value {
            QualitySubstitution::ScreenRefractionToSurfaceTint => {
                "teinte à la place de la réfraction écran"
            }
            QualitySubstitution::VolumetricToAuthoredDistanceFog => {
                "brouillard de distance à la place de la volumétrie"
            }
            QualitySubstitution::ReflectionDistanceNotGuaranteed => {
                "distance maximale des reflets écran non garantie"
            }
            QualitySubstitution::ReflectionIntensityNotApplied => {
                "intensité authorée des reflets conservée mais non appliquée ; réponse physique actuelle"
            }
            QualitySubstitution::TerrainBanksFullResolution => {
                "berges conservées à pleine résolution, écart au budget LOD accepté"
            }
        })
        .collect::<Vec<_>>()
        .join(" ; ");
    let blockers = plan
        .blockers
        .iter()
        .map(|value| capability_label(*value))
        .collect::<Vec<_>>()
        .join(", ");
    let constraints = plan.terrain_constraints.as_ref().map_or_else(String::new, |terrain| {
        format!(" Terrain : {} / {} chunks conservés à pleine résolution aux trois LOD pour préserver les berges ; budget terrain non validé.", terrain.full_resolution_chunks.len(), terrain.canonical_chunks)
    });
    format!(
        "Plan {:?}. Substitutions proposées : {}. Capacités à qualifier : {}.{constraints}",
        plan.requested_tier,
        if substitutions.is_empty() {
            "aucune"
        } else {
            &substitutions
        },
        if blockers.is_empty() {
            "aucune dans la déclaration, rendu encore non confirmé"
        } else {
            &blockers
        }
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reflection_modes_restore_both_flags_and_keep_disabled_priority() {
        for mode in [
            ReflectionPreviewMode::Profile,
            ReflectionPreviewMode::EnvironmentOnly,
            ReflectionPreviewMode::Disabled,
        ] {
            let (all, screen) = mode.flags();
            assert_eq!(ReflectionPreviewMode::from_observed(all, screen), mode);
        }
        assert_eq!(
            ReflectionPreviewMode::from_observed(true, true),
            ReflectionPreviewMode::Disabled
        );
        assert_eq!(ReflectionPreviewMode::Profile.flags(), (false, false));
        assert!(
            ReflectionPreviewMode::EnvironmentOnly
                .label()
                .contains("écran désactivés")
        );
    }

    #[test]
    fn adaptation_text_requires_acknowledgement_and_explains_stops() {
        assert!(adaptation_summary("tracking", true, 3, None).contains("profil effectif acquitté"));
        for (ack, error) in [(false, None), (true, Some("Invalidated"))] {
            assert!(adaptation_summary("tracking", ack, 3, error).contains("non confirmée"));
        }
        assert!(adaptation_summary("disabled", false, 0, None).contains("Mode fixe"));
        assert!(
            adaptation_summary("waiting_focus", true, 0, None).contains("cliquez dans le viewport")
        );
        assert!(adaptation_summary("applying", false, 1, None).contains("non encore acquitté"));
        assert!(
            adaptation_summary("stopped", false, 1, Some("TimedOut"))
                .contains("aucun retour arrière confirmé")
        );
    }

    #[test]
    fn application_text_never_equates_cpu_projection_with_gpu_success() {
        let pending = application_summary("plan_application_pending_gpu", 7, None, false);
        assert!(pending.contains("7/7"));
        assert!(pending.contains("en attente de preuve GPU"));
        let failed = application_summary("plan_application_failed", 7, Some("TimedOut"), false);
        assert!(failed.contains("aucun retour arrière confirmé"));
        assert!(failed.contains("TimedOut"));
        assert!(application_summary("unknown", 0, None, false).contains("non qualifiée"));
        assert!(
            application_summary(
                "plan_application_waiting_renderer",
                7,
                Some("Invalidated"),
                false
            )
            .contains("avant une nouvelle application")
        );
    }

    #[test]
    fn prepared_plan_text_requires_consistent_evidence_and_never_approves_the_preset() {
        let ready = application_summary("plan_application_ready", 7, None, true);
        assert!(ready.contains("préparé et soumis au GPU"));
        assert!(ready.contains("Preset demandé complet et qualité visuelle non validés"));
        for (projected, error, gpu) in [
            (6, None, true),
            (7, Some("Invalidated"), true),
            (7, None, false),
        ] {
            assert!(
                application_summary("plan_application_ready", projected, error, gpu)
                    .contains("GPU non confirmé")
            );
        }
    }
    use forge_project::{
        VisualQualityTier, VisualValleyQualityProfileDocument,
        effective_quality::{
            QualityCapabilities, QualityResolutionPolicy, resolve_effective_quality,
        },
    };

    #[test]
    fn unresolved_and_resolvable_plans_never_claim_application() {
        let source = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let caps = QualityCapabilities {
            terrain_constraints: None,
            source_bindings: None,
            schema_version: 1,
            adapter_id: "synthetic".into(),
            generation: 1,
            capabilities: std::collections::BTreeMap::default(),
        };
        let mut plan = resolve_effective_quality(
            &source,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Adr0011,
        )
        .unwrap();
        let text = summary(&plan);
        assert!(text.contains("Capacités à qualifier : terrain"));
        let original_plan = plan.clone();
        for projected in [false, true] {
            let status = projection_summary(VisualQualityTier::High, projected, Some(&plan));
            assert!(status.contains("profil incomplet"));
            assert!(status.contains("terrain"));
            assert!(!status.contains("en cours"));
        }
        assert_eq!(plan, original_plan);
        plan.blockers.clear(); // Presentation-only synthetic fixture, not renderer qualification.
        assert!(
            projection_summary(VisualQualityTier::High, true, Some(&plan))
                .contains("reçu GPU dans le détail du profil effectif · preset complet non validé")
        );
        assert!(projection_summary(VisualQualityTier::High, false, None).contains("non observée"));
        plan.substitutions = vec![
            QualitySubstitution::ReflectionDistanceNotGuaranteed,
            QualitySubstitution::ReflectionIntensityNotApplied,
            QualitySubstitution::TerrainBanksFullResolution,
            QualitySubstitution::ScreenRefractionToSurfaceTint,
            QualitySubstitution::VolumetricToAuthoredDistanceFog,
        ];
        let text = summary(&plan);
        assert!(text.contains("non garantie"));
        assert!(text.contains("écart au budget LOD accepté"));
        assert!(text.contains("intensité authorée des reflets conservée mais non appliquée"));
        assert!(text.contains("teinte à la place"));
        assert!(text.contains("brouillard de distance à la place"));
        assert!(text.contains("rendu encore non confirmé"));
        plan.terrain_constraints = Some(
            forge_project::effective_quality::TerrainQualityConstraints {
                terrain_hash: source.content_hash().unwrap(),
                canonical_chunks: 64,
                full_resolution_chunks: vec![forge_core::EntityId::deterministic("synthetic-bank")],
            },
        );
        assert!(summary(&plan).contains("1 / 64 chunks conservés à pleine résolution"));
        assert!(summary(&plan).contains("budget terrain non validé"));
    }
}
