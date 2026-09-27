use forge_project::authoring::history::zone::{DensitySummary, SampleSummary, ZoneHistoryChange};

pub(super) fn label(change: &ZoneHistoryChange) -> String {
    match change {
        ZoneHistoryChange::Objects {
            domain,
            before_explicit,
            after_explicit,
            changes,
        } => {
            use forge_project::authoring::history::zone::objects::ObjectDomain;
            let kind = match domain {
                ObjectDomain::Buildings => "Bâtiments",
                ObjectDomain::Paths => "Chemins",
                ObjectDomain::Rivers => "Rivières",
            };
            let details = changes
                .iter()
                .take(8)
                .map(|change| match (&change.before, &change.after) {
                    (None, Some(value)) => format!("{} ajouté ({})", value.name, change.id),
                    (Some(value), None) => format!("{} retiré ({})", value.name, change.id),
                    (Some(before), Some(after)) => format!(
                        "{} → {} modifié ({}) · verrou {} → {}",
                        before.name, after.name, change.id, before.locked, after.locked
                    ),
                    (None, None) => "entrée sans objet".into(),
                })
                .collect::<Vec<_>>()
                .join(" ; ");
            format!(
                "{kind} · {} objet(s) changé(s) · couche explicite {} → {}. {details}{}",
                changes.len(),
                before_explicit,
                after_explicit,
                if changes.len() > 8 {
                    " ; suite disponible dans les détails du reçu"
                } else {
                    ""
                }
            )
        }
        ZoneHistoryChange::Start { before, after } => {
            format!("Départ de zone : {before:?} → {after:?}")
        }
        ZoneHistoryChange::Name { before, after } => format!(
            "override nom : {} → {}",
            before.as_deref().unwrap_or("hérité"),
            after.as_deref().unwrap_or("hérité")
        ),
        ZoneHistoryChange::Elevation { before, after } => format!(
            "override altitude : {} → {}",
            elevation(*before),
            elevation(*after)
        ),
        ZoneHistoryChange::Heights { before, after } => {
            format!(
                "override relief mis à jour : {} → {}",
                samples(before.as_ref()),
                samples(after.as_ref())
            )
        }
        ZoneHistoryChange::Materials { before, after } => format!(
            "override matériaux mis à jour : {} → {}",
            samples(before.as_ref()),
            samples(after.as_ref())
        ),
        ZoneHistoryChange::Placements { before, after } => format!(
            "placements manuels mis à jour : {} → {}",
            samples(before.as_ref()),
            samples(after.as_ref())
        ),
        ZoneHistoryChange::Density {
            layer_id,
            before,
            after,
        } => format!(
            "densité {} mise à jour : {} → {}",
            layer_id.as_str(),
            density(before.as_ref()),
            density(after.as_ref())
        ),
        ZoneHistoryChange::Recipe { before, after } => format!(
            "recette {} v{} → {} v{} ; seed terrain {} → {} ; seed végétation {} → {}",
            before.generator.name,
            before.generator.version,
            after.generator.name,
            after.generator.version,
            before.terrain_seed,
            after.terrain_seed,
            before
                .vegetation_seed
                .map_or_else(|| "absente".into(), |v| v.to_string()),
            after
                .vegetation_seed
                .map_or_else(|| "absente".into(), |v| v.to_string())
        ),
    }
}

fn elevation(value: Option<f32>) -> String {
    value.map_or_else(|| "héritée".into(), |v| format!("{v} m"))
}

fn samples(value: Option<&SampleSummary>) -> String {
    value.map_or_else(|| "hérité".into(), |v| format!("{} cases", v.count))
}

fn density(value: Option<&DensitySummary>) -> String {
    value.map_or_else(
        || "héritée".into(),
        |v| {
            format!(
                "{} cases · {}",
                v.mask.count,
                if v.locked {
                    "verrouillé"
                } else {
                    "modifiable"
                }
            )
        },
    )
}
