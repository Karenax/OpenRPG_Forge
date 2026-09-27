//! Read-only index of authored objects and vegetation rules, never generated instances.
use forge_core::EntityId;
use forge_project::{VegetationLayerId, zone_authoring::ZoneAuthoringHead};

#[derive(Clone, Debug, PartialEq, Eq)]
pub(super) enum Target {
    Path(EntityId),
    River(EntityId),
    Building(EntityId),
    Placement(EntityId),
    Vegetation(VegetationLayerId),
}

impl Target {
    pub(super) fn id(&self) -> String {
        match self {
            Self::Path(id) | Self::River(id) | Self::Building(id) | Self::Placement(id) => {
                id.to_string()
            }
            Self::Vegetation(id) => id.to_string(),
        }
    }
}

pub(super) fn rows(head: &ZoneAuthoringHead, filter: &str) -> Vec<(Target, String)> {
    let mut rows = Vec::new();
    let lock = |locked| if locked { " · verrouillé" } else { "" };
    for path in head.corrections.paths.as_deref().unwrap_or_default() {
        rows.push((
            Target::Path(path.id),
            format!("Chemin · {}{}", path.name, lock(path.locked)),
        ));
    }
    for river in head.corrections.rivers.as_deref().unwrap_or_default() {
        rows.push((
            Target::River(river.id),
            format!("Rivière · {}{}", river.water.name, lock(river.locked)),
        ));
    }
    for building in head.corrections.buildings.as_deref().unwrap_or_default() {
        rows.push((
            Target::Building(building.id),
            format!("Bâtiment · {}{}", building.name, lock(building.locked)),
        ));
    }
    for placement in head.corrections.placements.as_deref().unwrap_or_default() {
        rows.push((
            Target::Placement(placement.id),
            format!("Asset placé · {}{}", placement.name, lock(placement.locked)),
        ));
    }
    if let Some(vegetation) = &head.base.vegetation {
        for layer in &vegetation.layers {
            let locked = head
                .corrections
                .density
                .iter()
                .any(|c| c.layer_id == layer.id && c.locked);
            rows.push((
                Target::Vegetation(layer.id.clone()),
                format!(
                    "Couche végétale · {}{}",
                    layer.display_name,
                    if locked { " · masque verrouillé" } else { "" }
                ),
            ));
        }
    }
    let filter = filter.trim().to_lowercase();
    rows.retain(|(target, label)| {
        label.to_lowercase().contains(&filter) || target.id().contains(&filter)
    });
    rows
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_project::{authoring::new_terrain, zone_authoring::session::ZoneEditSession};

    #[test]
    fn vegetation_index_is_read_only_and_uses_stable_layer_ids_not_instances() {
        let mut session =
            ZoneEditSession::from_initial_terrain(new_terrain("index", "zone", "Zone").unwrap())
                .unwrap();
        assert!(rows(session.head(), "").is_empty());
        let init = forge_world::editor_vegetation::prepare_request(session.head(), 42).unwrap();
        session.execute(&init).unwrap();
        let archive = session.archive_bytes().unwrap();
        let indexed = rows(session.head(), "");
        assert_eq!(indexed.len(), 2);
        assert_eq!(
            indexed[0].0,
            Target::Vegetation(VegetationLayerId::new("editor_trees"))
        );
        assert_eq!(rows(session.head(), " ARBRES "), vec![indexed[0].clone()]);
        assert_eq!(
            rows(session.head(), "editor_grass"),
            vec![indexed[1].clone()]
        );
        assert!(rows(session.head(), "absent").is_empty());
        assert_eq!(session.archive_bytes().unwrap(), archive);
    }
}
