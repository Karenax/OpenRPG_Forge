use forge_project::audit::{AuditActor, AuditStamp};

pub(crate) fn audit_label(audit: &AuditStamp) -> String {
    match &audit.actor {
        AuditActor::Legacy => "Archive historique : acteur et horodatage non disponibles.".into(),
        AuditActor::Human { subject } => format!(
            "Auteur humain : {subject} · {}",
            audit
                .recorded_at_utc
                .as_deref()
                .unwrap_or("horodatage invalide")
        ),
        AuditActor::Ai { subject } => format!(
            "Assistant IA : {subject} · {}",
            audit
                .recorded_at_utc
                .as_deref()
                .unwrap_or("horodatage invalide")
        ),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn formats_human_audit_stamp() {
        let audit = AuditStamp::human("Camille", "2026-09-22T12:34:56Z").unwrap();
        assert_eq!(
            audit_label(&audit),
            "Auteur humain : Camille · 2026-09-22T12:34:56Z"
        );
        assert_eq!(
            audit_label(&AuditStamp {
                actor: AuditActor::Human {
                    subject: "Camille".into(),
                },
                recorded_at_utc: None,
            }),
            "Auteur humain : Camille · horodatage invalide"
        );
    }

    #[test]
    fn formats_ai_audit_stamp() {
        let audit = AuditStamp::ai("Forge Assistant", "2026-09-22T12:34:56Z").unwrap();
        assert_eq!(
            audit_label(&audit),
            "Assistant IA : Forge Assistant · 2026-09-22T12:34:56Z"
        );
    }

    #[test]
    fn formats_legacy_audit_stamp() {
        assert_eq!(
            audit_label(&AuditStamp::legacy()),
            "Archive historique : acteur et horodatage non disponibles."
        );
    }
}
