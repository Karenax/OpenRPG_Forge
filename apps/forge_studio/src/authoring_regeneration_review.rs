//! Read-only review text derived from the accepted transaction, never another head.
use forge_project::zone_authoring::{ZoneCorrections, session::ZoneReceipt};

/// Derive vegetation only after verifying the complete receipt; never accept a new head.
pub(super) fn vegetation_proposal(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    receipt: &ZoneReceipt,
) -> Result<Option<forge_project::VegetationDocument>, String> {
    if !matches_source(Some(head), receipt) {
        return Err("Proposition de végétation périmée".into());
    }
    let change = receipt.base_change.as_ref().ok_or("Recette inchangée")?;
    if change.inverse != head.base
        || receipt.inverse != head.corrections
        || receipt.applied != head.corrections
        || change.applied.terrain != head.base.terrain
    {
        return Err("Aperçu végétal seul indisponible : terrain ou retouches modifiés".into());
    }
    let mut candidate = head.clone();
    candidate.base = change.applied.clone();
    candidate.revision = receipt.resulting_revision;
    if candidate.content_hash()? != receipt.resulting_hash {
        return Err("Proposition de végétation incohérente".into());
    }
    // Projection on the accepted terrain, not a publication of the resulting revision.
    candidate.revision = head.revision;
    candidate.effective().map(|(_, vegetation)| vegetation)
}

pub(super) fn matches_source(
    head: Option<&forge_project::zone_authoring::ZoneAuthoringHead>,
    receipt: &ZoneReceipt,
) -> bool {
    head.is_some_and(|head| {
        head.revision == receipt.request.expected_revision
            && head.content_hash().ok() == Some(receipt.request.expected_hash)
    })
}

pub(super) fn terrain_label(
    head: Option<&forge_project::zone_authoring::ZoneAuthoringHead>,
    receipt: &ZoneReceipt,
) -> String {
    let Some(head) =
        head.filter(|head| head.content_hash().ok() == Some(receipt.request.expected_hash))
    else {
        return "Diff terrain indisponible : la source du reçu n’est pas la zone courante. Réexaminer la retouche.".into();
    };
    let before = &receipt.inverse;
    let after = &receipt.applied;
    let mut changes = Vec::new();
    if before.name != after.name {
        let value = |name: &Option<String>| {
            name.as_ref().map_or_else(
                || format!("{} (hérité)", head.base.terrain.name),
                |name| format!("{name} (retouché)"),
            )
        };
        changes.push(format!(
            "Nom : {} → {}",
            value(&before.name),
            value(&after.name)
        ));
    }
    if before.base_elevation_m != after.base_elevation_m {
        let value = |height: Option<f32>| {
            height.map_or_else(
                || {
                    format!(
                        "{} m (héritée)",
                        head.base.terrain.height_sampling.base_elevation_m
                    )
                },
                |height| format!("{height} m (retouchée)"),
            )
        };
        changes.push(format!(
            "Altitude de base : {} → {}",
            value(before.base_elevation_m),
            value(after.base_elevation_m)
        ));
    }
    if before.height_deltas != after.height_deltas {
        changes.push(format!(
            "Relief : {} → {} ; contenu modifié",
            layer_state(before.height_deltas.as_deref()),
            layer_state(after.height_deltas.as_deref())
        ));
    }
    if before.material_overrides != after.material_overrides {
        changes.push(format!(
            "Matériaux : {} → {} ; contenu modifié",
            layer_state(before.material_overrides.as_deref()),
            layer_state(after.material_overrides.as_deref())
        ));
    }
    if changes.is_empty() {
        changes.push("Aucun changement des valeurs de terrain dans ce reçu".into());
    }
    format!(
        "Proposition terrain · non appliquée. {}. Source : {}. Appliquer puis sauvegarder.",
        changes.join(" ; "),
        receipt.request.expected_hash
    )
}

fn layer_state<T>(values: Option<&[T]>) -> String {
    values.map_or_else(
        || "héritée de la recette".into(),
        |values| {
            if values.is_empty() {
                "remplacement vide explicite".into()
            } else {
                format!("{} échantillon(s) authoré(s)", values.len())
            }
        },
    )
}

pub(super) fn label(receipt: &ZoneReceipt) -> Option<String> {
    let change = receipt.base_change.as_ref()?;
    let order = |base: &forge_project::zone_authoring::ZoneBase| {
        base.vegetation
            .as_ref()
            .map(|v| {
                v.layers
                    .iter()
                    .map(|l| l.id.to_string())
                    .collect::<Vec<_>>()
            })
            .unwrap_or_default()
    };
    let before_order = order(&change.inverse);
    let after_order = order(&change.applied);
    let order_change = if before_order == after_order {
        String::new()
    } else {
        format!(
            "Ordre des couches : {} → {}. ",
            before_order.join(" / "),
            after_order.join(" / ")
        )
    };
    let seed = |base: &forge_project::zone_authoring::ZoneBase| {
        base.vegetation
            .as_ref()
            .map_or_else(|| "absente".into(), |v| v.seed.to_string())
    };
    let identical = match (
        serde_json::to_vec(&receipt.applied),
        serde_json::to_vec(&receipt.inverse),
    ) {
        (Ok(after), Ok(before)) => after == before,
        _ => false,
    };
    let retention = if identical {
        format!(
            "Retouches conservées sans changement : {}",
            retained(&receipt.applied)
        )
    } else {
        "Attention : ce reçu change aussi les retouches ; conservation exacte non confirmée.".into()
    };
    let layer_changes = change
        .applied
        .vegetation
        .as_ref()
        .map(|vegetation| {
            vegetation
                .layers
                .iter()
                .filter_map(|after| {
                    let before = change
                        .inverse
                        .vegetation
                        .as_ref()?
                        .layers
                        .iter()
                        .find(|v| v.id == after.id)?;
                    layer_change(before, after)
                })
                .collect::<Vec<_>>()
                .join(" ")
        })
        .unwrap_or_default();
    Some(format!(
        "Proposition de recette · seed végétation {} → {} · non appliquée. {order_change}{layer_changes} {retention} Source : {}. Appliquer puis sauvegarder pour conserver la nouvelle recette.",
        seed(&change.inverse),
        seed(&change.applied),
        receipt.request.expected_hash,
    ))
}

fn layer_change(
    before: &forge_project::VegetationLayer,
    after: &forge_project::VegetationLayer,
) -> Option<String> {
    let settings = |layer: &forge_project::VegetationLayer| {
        (
            layer.display_name.clone(),
            layer.acceptance_probability.to_bits(),
            layer.minimum_spacing_m.to_bits(),
            layer.minimum_altitude_m.to_bits(),
            layer.maximum_altitude_m.to_bits(),
            layer.maximum_slope_gradient.to_bits(),
        )
    };
    (settings(before) != settings(after)).then(|| format!(
        "Nom {} → {} : densité {} → {} %, espacement {} → {} m. Altitude [{} ; {}] → [{} ; {}] m ; pente maximum {} → {} %.",
        before.display_name, after.display_name,
        before.acceptance_probability * 100.0, after.acceptance_probability * 100.0,
        before.minimum_spacing_m, after.minimum_spacing_m,
        before.minimum_altitude_m, before.maximum_altitude_m,
        after.minimum_altitude_m, after.maximum_altitude_m,
        before.maximum_slope_gradient * 100.0, after.maximum_slope_gradient * 100.0,
    ))
}

fn retained(c: &ZoneCorrections) -> String {
    let buildings = c.buildings.as_deref().unwrap_or_default();
    let yes_no = |value| if value { "oui" } else { "non" };
    format!(
        "{} masque(s) de densité dont {} verrouillé(s), {} bâtiment(s) dont {} verrouillé(s) en entier et {} porte(s) retouchée(s), {} chemin(s), {} rivière(s), {} placement(s). Nom retouché : {} ; altitude retouchée : {} ; couches relief/matériaux remplacées : {}/{}. Un verrou de bâtiment protège toute la pièce, pas seulement sa porte.",
        c.density.len(),
        c.density.iter().filter(|v| v.locked).count(),
        buildings.len(),
        buildings.iter().filter(|b| b.locked).count(),
        buildings
            .iter()
            .filter(|b| b.door_offset_override_m.is_some())
            .count(),
        c.paths.as_ref().map_or(0, Vec::len),
        c.rivers.as_ref().map_or(0, Vec::len),
        c.placements.as_ref().map_or(0, Vec::len),
        yes_no(c.name.is_some()),
        yes_no(c.base_elevation_m.is_some()),
        yes_no(c.height_deltas.is_some()),
        yes_no(c.material_overrides.is_some()),
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::CommandId;
    use forge_project::{authoring::new_terrain, zone_authoring::session::ZoneEditSession};

    #[test]
    fn habitat_review_shows_units_and_effective_preview_without_application() {
        let mut session =
            ZoneEditSession::from_initial_terrain(new_terrain("habitat", "zone", "Zone").unwrap())
                .unwrap();
        let init = forge_world::editor_vegetation::prepare_request(session.head(), 42).unwrap();
        session.execute(&init).unwrap();
        let archive = session.archive_bytes().unwrap();
        let id = &session.head().base.vegetation.as_ref().unwrap().layers[0].id;
        let request = forge_world::editor_vegetation::prepare_layer_habitat_request(
            session.head(),
            id,
            5.0,
            50.0,
            0.25,
        )
        .unwrap();
        let receipt = session.preview(&request).unwrap();
        let text = label(&receipt).unwrap();
        assert!(text.contains("Altitude [-4096 ; 4096] → [5 ; 50] m"));
        assert!(text.contains("pente maximum 65 → 25 %"));
        let proposed = vegetation_proposal(session.head(), &receipt)
            .unwrap()
            .unwrap();
        assert_eq!(
            proposed.layers[0].minimum_altitude_m.to_bits(),
            5.0_f32.to_bits()
        );
        assert_eq!(
            proposed.layers[0].maximum_altitude_m.to_bits(),
            50.0_f32.to_bits()
        );
        assert_eq!(
            proposed.layers[0].maximum_slope_gradient.to_bits(),
            0.25_f32.to_bits()
        );
        assert_eq!(session.archive_bytes().unwrap(), archive);
    }

    #[test]
    fn vegetation_preview_uses_the_reviewed_recipe_without_mutating_the_session() {
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("vegetation-preview", "zone", "Zone").unwrap(),
        )
        .unwrap();
        let init = forge_world::editor_vegetation::prepare_request(session.head(), 42).unwrap();
        session.execute(&init).unwrap();
        let archive = session.archive_bytes().unwrap();
        let reseed = forge_world::editor_vegetation::prepare_request(session.head(), 43).unwrap();
        let receipt = session.preview(&reseed).unwrap();
        let projected = vegetation_proposal(session.head(), &receipt)
            .unwrap()
            .unwrap();
        assert_eq!(projected.seed, 43);
        assert_eq!(
            projected.layers,
            session.head().effective().unwrap().1.unwrap().layers
        );
        assert_eq!(session.archive_bytes().unwrap(), archive);
        let mut stale = receipt.clone();
        stale.request.expected_revision = receipt.resulting_revision;
        assert!(vegetation_proposal(session.head(), &stale).is_err());
        let mut invalid = receipt.clone();
        invalid.resulting_hash = receipt.request.expected_hash;
        assert!(vegetation_proposal(session.head(), &invalid).is_err());
        invalid = receipt.clone();
        invalid.applied.name = Some("Other".into());
        assert!(vegetation_proposal(session.head(), &invalid).is_err());
        let mut base = session.head().base.clone();
        base.terrain.height_sampling.base_elevation_m = 2.0;
        let request = session
            .head()
            .prepare_regeneration(base, CommandId::deterministic("terrain-change"))
            .unwrap();
        let terrain_receipt = session.preview(&request).unwrap();
        assert!(vegetation_proposal(session.head(), &terrain_receipt).is_err());
        assert_eq!(session.archive_bytes().unwrap(), archive);
    }

    #[test]
    fn terrain_diff_shows_effective_values_and_explicit_empty_without_writing() {
        use forge_project::zone_authoring::session::{ZoneAction, ZoneRequest};
        let session = ZoneEditSession::from_initial_terrain(
            new_terrain("diff", "terrain", "Recette").unwrap(),
        )
        .unwrap();
        let initial = session.archive_bytes().unwrap();
        let head = session.head();
        let mut corrections = head.corrections.clone();
        corrections.base_elevation_m = Some(6.0);
        corrections.height_deltas = Some(Vec::new());
        let request = ZoneRequest {
            command_id: CommandId::deterministic("terrain-diff"),
            expected_hash: head.content_hash().unwrap(),
            expected_revision: head.revision,
            action: ZoneAction::edit(corrections),
        };
        let receipt = session.preview(&request).unwrap();
        assert!(matches_source(Some(head), &receipt));
        assert!(!matches_source(None, &receipt));
        let mut wrong_revision = receipt.clone();
        wrong_revision.request.expected_revision = head.revision.next().unwrap();
        assert!(!matches_source(Some(head), &wrong_revision));
        let text = terrain_label(Some(head), &receipt);
        assert!(text.contains("0 m (héritée) → 6 m (retouchée)"));
        assert!(text.contains("héritée de la recette → remplacement vide explicite"));
        assert!(text.contains("non appliquée"));
        assert!(!text.contains("Some("));
        assert!(!text.contains("Nom :"));
        assert_eq!(session.archive_bytes().unwrap(), initial);
        let mut stale = head.clone();
        stale.corrections.name = Some("Autre".into());
        assert!(!matches_source(Some(&stale), &receipt));
        assert!(terrain_label(Some(&stale), &receipt).contains("Réexaminer"));
        assert!(terrain_label(None, &receipt).contains("indisponible"));
    }

    #[test]
    fn review_is_read_only_and_does_not_claim_retention_for_changed_corrections() {
        let session =
            ZoneEditSession::from_initial_terrain(new_terrain("review", "zone", "Review").unwrap())
                .unwrap();
        let before = session.archive_bytes().unwrap();
        let mut base = session.head().base.clone();
        base.generator.version = "review-v2".into();
        let request = session
            .head()
            .prepare_regeneration(base, CommandId::deterministic("review"))
            .unwrap();
        let mut receipt = session.preview(&request).unwrap();
        let text = label(&receipt).unwrap();
        assert!(text.contains("Retouches conservées sans changement"));
        assert!(text.contains("0 bâtiment(s)"));
        assert!(text.contains("non appliquée"));
        assert_eq!(before, session.archive_bytes().unwrap());
        receipt.applied.name = Some("Different".into());
        assert!(
            label(&receipt)
                .unwrap()
                .contains("conservation exacte non confirmée")
        );
        receipt.base_change = None;
        assert!(label(&receipt).is_none());
    }

    #[test]
    fn explicit_empty_replacements_are_not_described_as_inherited() {
        let mut corrections = ZoneCorrections::default();
        assert!(retained(&corrections).contains("non/non"));
        corrections.height_deltas = Some(Vec::new());
        corrections.material_overrides = Some(Vec::new());
        assert!(retained(&corrections).contains("oui/oui"));
    }

    #[test]
    fn layer_change_shows_old_and_new_settings_even_with_unchanged_seed() {
        let mut session = ZoneEditSession::from_initial_terrain(
            new_terrain("layers", "review", "Review").unwrap(),
        )
        .unwrap();
        let init = forge_world::editor_vegetation::prepare_request(session.head(), 42).unwrap();
        session.execute(&init).unwrap();
        let id = session.head().base.vegetation.as_ref().unwrap().layers[0]
            .id
            .clone();
        let request =
            forge_world::editor_vegetation::prepare_layer_request(session.head(), &id, 0.7, 3.0)
                .unwrap();
        let text = label(&session.preview(&request).unwrap()).unwrap();
        assert!(text.contains("42 → 42"));
        assert!(text.contains("densité 35 → 70 %"));
        assert!(text.contains("espacement 5 → 3 m"));
        let rename = forge_world::editor_vegetation::prepare_layer_name_request(
            session.head(),
            &id,
            "Pins de la berge",
        )
        .unwrap();
        let text = label(&session.preview(&rename).unwrap()).unwrap();
        assert!(text.contains("Nom Arbres → Pins de la berge"));
        assert!(text.contains("non appliquée"));
    }
}
