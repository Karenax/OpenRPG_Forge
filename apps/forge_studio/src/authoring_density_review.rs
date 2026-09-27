//! Human-readable comparison of accepted/proposed density overrides, without mutation.
use forge_project::zone_authoring::DensityOverride;

pub(super) fn describe(before: &[DensityOverride], after: &[DensityOverride]) -> String {
    let mut changes = Vec::new();
    for old in before {
        if !after.iter().any(|new| new.layer_id == old.layer_id) {
            changes.push(format!(
                "{} : retouche retirée, retour au masque de la recette",
                old.layer_id
            ));
        }
    }
    for new in after {
        let old = before.iter().find(|old| old.layer_id == new.layer_id);
        if old == Some(new) {
            continue;
        }
        let mask = old.map_or_else(
            || "nouvelle retouche du masque".into(),
            |old| {
                if old.mask == new.mask {
                    "masque inchangé".into()
                } else if old.mask.vertices_per_axis != new.mask.vertices_per_axis
                    || old.mask.samples_u8.len() != new.mask.samples_u8.len()
                {
                    format!(
                        "grille remplacée : {}×{} → {}×{}",
                        old.mask.vertices_per_axis,
                        old.mask.vertices_per_axis,
                        new.mask.vertices_per_axis,
                        new.mask.vertices_per_axis
                    )
                } else {
                    let modified_samples = old
                        .mask
                        .samples_u8
                        .iter()
                        .zip(&new.mask.samples_u8)
                        .filter(|(a, b)| a != b)
                        .count();
                    format!(
                        "{modified_samples} échantillon(s) modifié(s) sur {}",
                        new.mask.samples_u8.len()
                    )
                }
            },
        );
        let zeros = new
            .mask
            .samples_u8
            .iter()
            .fold(0_usize, |count, &value| count + usize::from(value == 0));
        let lock = |locked| if locked { "actif" } else { "inactif" };
        changes.push(format!(
            "{} : {mask} ; {zeros} échantillon(s) à densité nulle ; verrou {} → {}",
            new.layer_id,
            lock(old.is_some_and(|old| old.locked)),
            lock(new.locked)
        ));
    }
    if changes.is_empty() {
        "Aucun changement de masque ou de verrou".into()
    } else {
        changes.join(". ")
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{VegetationDensityMask, VegetationLayerId};

    #[test]
    fn density_review_distinguishes_paint_lock_and_removal() {
        let old = DensityOverride {
            layer_id: VegetationLayerId::new("trees"),
            mask: VegetationDensityMask {
                vertices_per_axis: 2,
                samples_u8: vec![255; 4],
            },
            locked: false,
        };
        let mut new = old.clone();
        new.mask.samples_u8[1] = 0;
        let before = [old.clone()];
        let text = describe(&before, &[new]);
        assert!(text.contains("trees"));
        assert!(text.contains("1 échantillon(s) modifié(s) sur 4"));
        assert!(text.contains("1 échantillon(s) à densité nulle"));
        let mut locked = old.clone();
        locked.locked = true;
        let text = describe(&before, &[locked]);
        assert!(text.contains("masque inchangé"));
        assert!(text.contains("inactif → actif"));
        assert!(describe(&before, &[]).contains("retour au masque de la recette"));
        assert!(describe(&[], &before).contains("nouvelle retouche"));
        assert!(describe(&before, &before).contains("Aucun changement"));
        assert_eq!(before[0], old);
    }
}
