//! Presentation only: preserve unknown diagnostics and native refusal semantics.

pub(super) fn rejection(reason: String) -> String {
    match reason.as_str() {
        "Zone edit has no effect" => "Aucun changement à prévisualiser : ces valeurs sont déjà celles de la carte. Modifiez une valeur pour créer une retouche.".into(),
        "Stale zone command" => "La carte a changé depuis cette proposition. Reprenez ses valeurs, puis prévisualisez à nouveau.".into(),
        _ => reason,
    }
}

/// The detached window must not equate a synchronized session with application.
pub(super) fn atelier_status(status: &str, busy: bool, preview_ready: bool) -> String {
    if busy {
        "Traitement en cours dans la fenêtre principale…".into()
    } else if preview_ready {
        // A local refusal can coexist with an earlier valid preview. Keep that
        // diagnostic rather than replacing it with a success-looking message.
        let status: String = status.chars().take(896).collect();
        format!(
            "{status} · Aperçu prêt · non appliqué. Vérifiez la carte, puis cliquez sur Appliquer dans la fenêtre principale."
        )
    } else {
        status.chars().take(1024).collect()
    }
}

#[cfg(test)]
mod tests {
    use super::{atelier_status, rejection};

    #[test]
    fn detached_preview_requires_explicit_application_and_busy_is_not_success() {
        let ready = atelier_status("Session synchronisée", false, true);
        assert!(ready.contains("non appliqué"));
        assert!(ready.contains("Appliquer dans la fenêtre principale"));
        assert!(atelier_status("Saisie refusée", false, true).starts_with("Saisie refusée"));
        assert_eq!(
            atelier_status("ancien résultat", true, true),
            "Traitement en cours dans la fenêtre principale…"
        );
    }

    #[test]
    fn detached_refusal_and_cancellation_survive_without_preview() {
        for status in [
            "Nombre incomplet ou invalide",
            "Aperçu abandonné ; carte appliquée inchangée.",
        ] {
            assert_eq!(atelier_status(status, false, false), status);
        }
        assert_eq!(
            atelier_status(&"é".repeat(1100), false, false)
                .chars()
                .count(),
            1024
        );
    }

    #[test]
    fn unchanged_values_explain_the_next_action_without_claiming_success() {
        let message = rejection("Zone edit has no effect".into());
        assert!(message.starts_with("Aucun changement à prévisualiser"));
        assert!(message.contains("Modifiez une valeur"));
    }

    #[test]
    fn stale_proposal_requires_another_preview() {
        assert!(rejection("Stale zone command".into()).contains("prévisualisez à nouveau"));
    }

    #[test]
    fn unknown_or_similar_diagnostics_are_preserved_exactly() {
        for reason in [
            "Zone edit has no effect: extra context",
            "Nombre incomplet ou invalide",
            "IO error: archive inaccessible",
        ] {
            assert_eq!(rejection(reason.into()), reason);
        }
    }
}
