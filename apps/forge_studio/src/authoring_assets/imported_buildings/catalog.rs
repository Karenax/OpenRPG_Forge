//! Package adapter feeding the common catalog; it does not place or edit objects.
use super::*;
use crate::authoring_assets::catalog::Entry;

pub(in crate::authoring_assets) fn entries() -> Vec<Entry> {
    let mut entries = Vec::new();
    let mut add = |label: String, asset_id, hash| {
        entries.push(Entry {
            id: format!("verified-package/{asset_id}"), label, kind: "architecture".into(),
            reference: (asset_id, hash),
            provenance: "Ressources Tidewater vérifiées, source 1438b1ab ; licences conservées dans le paquet importé.".into(),
        });
    };
    if let Some(package) = building_package() {
        for asset in &package.catalog().assets {
            add(asset.logical_name.clone(), asset.id, asset.content_hash);
        }
    }
    if let Some(reference) = fish_stand_reference() {
        add(
            "Échoppe de Joe".into(),
            reference.asset_id,
            reference.bundle_hash,
        );
    }
    for template in boardwalk_templates() {
        if boardwalk_package().is_some() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_boardwalk::package_hash(),
            );
        }
    }
    if pier_package().is_some() {
        if let Some(template) = pier_template() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_pier::package_hash(),
            );
        }
    }
    for template in plaza_templates() {
        if plaza_package().is_some() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_plaza::package_hash(),
            );
        }
    }
    for template in village_props_templates() {
        if village_props_package().is_some() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_village_props::package_hash(),
            );
        }
    }
    for template in beach_props_templates() {
        if beach_props_package().is_some() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_beach_props::package_hash(),
            );
        }
    }
    for template in fish_props_templates() {
        if fish_props_package().is_some() {
            add(
                template.source_id,
                template.asset_id,
                forge_assets::tidewater_fish_props::package_hash(),
            );
        }
    }
    for template in debris_templates() {
        entries.push(Entry {
            id: format!("verified-package/{}", template.asset_id),
            label: format!("{} · {}", template.kind, template.source_id),
            kind: "decoration".into(),
            reference: (template.asset_id, forge_assets::tidewater_debris::package_hash()),
            provenance: "Décor Tidewater vérifié, source 1438b1ab ; licences conservées dans le paquet importé.".into(),
        });
    }
    entries
}
