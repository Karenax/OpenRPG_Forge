//! Native orchestration for a private package candidate; no `WebView` paths.
use crate::game_export_protocol::GameExportRequest;
use forge_core::{ContentHash, bytes_sha256};
use forge_delivery::{
    game_builder::{GameCandidate, VerifiedGameKit, build_candidate_with_presentation},
    game_presentation::PresentationBundle,
    manifest,
    module_builder::{ExportMetadata, ExportedModule},
    orpmod,
};
use forge_project::{
    authoring::host::AuthoringHost,
    module_package::ModuleRights,
    workspace::export::{GameExportModule, ModuleExportSnapshot},
    zone_authoring::session::ZoneEditSession,
};
use std::{fs, path::Path};

/// Native adapters resolve the kit and build each selected module. Neither the
/// kit nor any path is accepted from the frontend. Candidate publication waits
/// until the saved workspace, module pins and kit have all been rechecked.
pub fn export_game<L, B>(
    host: &AuthoringHost,
    output_root: &Path,
    request: &GameExportRequest,
    load_kit: L,
    build_module: B,
) -> Result<GameCandidate, String>
where
    L: FnMut() -> Result<(VerifiedGameKit, ContentHash), String>,
    B: FnMut(
        &Path,
        &GameExportModule,
        &ModuleExportSnapshot,
        ExportMetadata,
    ) -> Result<ExportedModule, String>,
{
    export_game_with_presentation(host, output_root, request, load_kit, build_module, |_| {
        Ok(None)
    })
}

pub fn export_game_with_presentation<L, B, P>(
    host: &AuthoringHost,
    output_root: &Path,
    request: &GameExportRequest,
    mut load_kit: L,
    mut build_module: B,
    mut load_presentation: P,
) -> Result<GameCandidate, String>
where
    L: FnMut() -> Result<(VerifiedGameKit, ContentHash), String>,
    B: FnMut(
        &Path,
        &GameExportModule,
        &ModuleExportSnapshot,
        ExportMetadata,
    ) -> Result<ExportedModule, String>,
    P: FnMut(
        &forge_project::workspace::launcher::GameConfiguration,
    ) -> Result<Option<PresentationBundle>, String>,
{
    request.validate()?;
    let plan = host.game_export_plan(request.project_id, request.expected_workspace_hash)?;
    let presentation = load_presentation(plan.game.configuration())?;
    let (kit, kit_hash) = load_kit()?;
    manifest::safe_chain(output_root)?;
    fs::create_dir_all(output_root).map_err(|error| error.to_string())?;
    let output_root = fs::canonicalize(output_root).map_err(|error| error.to_string())?;
    let attempt = forge_delivery::attempt()?;
    let scratch = output_root.join(format!(".game-inputs-{attempt}.staging"));
    let destination = output_root.join(format!("game-{attempt}"));
    fs::create_dir(&scratch).map_err(|error| error.to_string())?;
    let result = (|| {
        let mut modules = Vec::with_capacity(plan.modules.len());
        let mut snapshot_hashes = Vec::with_capacity(plan.modules.len());
        for selection in &plan.modules {
            let mut snapshot = host.game_export_module(
                request.project_id,
                selection.module_id,
                request.expected_workspace_hash,
            )?;
            choose_primary(&mut snapshot)?;
            let metadata = ExportMetadata {
                name: selection.name.clone(),
                version: request.version.clone(),
                publisher: request.publisher.clone(),
                license_text: request.license_text.clone(),
                rights: ModuleRights::PrivateLocalOnly,
            };
            let exported = build_module(&scratch, selection, &snapshot, metadata)?;
            verify_module(&snapshot, &exported)?;
            snapshot_hashes.push(snapshot.content_hash()?);
            modules.push(exported);
        }
        let mut candidate = build_candidate_with_presentation(
            &scratch.join("candidate"),
            &kit,
            &plan.game,
            request.expected_workspace_hash,
            &modules,
            presentation.as_ref(),
        )?;
        if candidate.kit_manifest_sha256 != kit_hash {
            return Err("Le manifeste du kit ne correspond pas à la ressource du Studio.".into());
        }
        let (_, current_kit_hash) = load_kit()?;
        if current_kit_hash != kit_hash {
            return Err("Le kit de jeu a changé pendant l’assemblage.".into());
        }
        for (selection, expected) in plan.modules.iter().zip(&snapshot_hashes) {
            let mut snapshot = host.game_export_module(
                request.project_id,
                selection.module_id,
                request.expected_workspace_hash,
            )?;
            revalidate_snapshot(&mut snapshot, *expected)?;
        }
        host.game_export_plan(request.project_id, request.expected_workspace_hash)?;
        if destination
            .try_exists()
            .map_err(|error| error.to_string())?
        {
            return Err("Le dossier de destination existe déjà.".into());
        }
        fs::rename(&candidate.directory, &destination).map_err(|error| error.to_string())?;
        candidate.directory.clone_from(&destination);
        Ok(candidate)
    })();
    match (result, remove_owned_tree(&scratch)) {
        (Ok(candidate), Ok(())) => Ok(candidate),
        (Ok(candidate), Err(cleanup)) => Err(format!(
            "Les paquets sont créés dans {}, mais les fichiers temporaires restent dans {} : {cleanup}",
            candidate.directory.display(),
            scratch.display()
        )),
        (Err(error), Ok(())) => Err(error),
        (Err(error), Err(cleanup)) => Err(format!(
            "{error} ; les fichiers temporaires restent dans {} : {cleanup}",
            scratch.display()
        )),
    }
}

fn choose_primary(snapshot: &mut ModuleExportSnapshot) -> Result<(), String> {
    let mut choice = None;
    for zone in &snapshot.zones {
        let session = ZoneEditSession::from_archive_bytes(&zone.bytes)?;
        if session.head().corrections.gameplay.is_some() {
            if choice.is_none() || zone.area_id == snapshot.primary_area {
                choice = Some((zone.area_id, zone.head_hash));
            }
            if zone.area_id == snapshot.primary_area {
                break;
            }
        }
    }
    let (area, head) = choice
        .ok_or("Le module doit contenir une carte avec des définitions RPG avant l’export.")?;
    snapshot.primary_area = area;
    snapshot.primary_head = head;
    Ok(())
}

fn revalidate_snapshot(
    snapshot: &mut ModuleExportSnapshot,
    expected: ContentHash,
) -> Result<(), String> {
    choose_primary(snapshot)?;
    if snapshot.content_hash()? != expected {
        return Err("Un module a changé pendant l’assemblage du jeu.".into());
    }
    Ok(())
}

fn verify_module(snapshot: &ModuleExportSnapshot, exported: &ExportedModule) -> Result<(), String> {
    if exported.module_id != snapshot.module_id
        || exported.module_hash != Some(snapshot.content_hash()?)
    {
        return Err("Le module exporté ne correspond pas au module examiné.".into());
    }
    let module = orpmod::inspect(&exported.package_path, false)?;
    if module.project_id != snapshot.project_id
        || module.module_id != snapshot.module_id
        || module.creator != snapshot.creator
        || module.zones.len() != snapshot.zones.len()
    {
        return Err("Le contenu du module exporté ne correspond pas au projet.".into());
    }
    for zone in &snapshot.zones {
        let actual = module
            .zones
            .iter()
            .find(|actual| actual.area_id == zone.area_id)
            .ok_or("Une carte du module exporté est absente.")?;
        if actual.head_hash != zone.head_hash || actual.archive_sha256 != bytes_sha256(&zone.bytes)
        {
            return Err("Une carte du module exporté diffère de la version sauvegardée.".into());
        }
    }
    Ok(())
}

/// The directory is allocated by this operation; never follow links in it.
fn remove_owned_tree(root: &Path) -> Result<(), String> {
    manifest::no_links(root)?;
    for child in fs::read_dir(root).map_err(|error| error.to_string())? {
        let path = child.map_err(|error| error.to_string())?.path();
        manifest::no_links(&path)?;
        if fs::metadata(&path)
            .map_err(|error| error.to_string())?
            .is_dir()
        {
            remove_owned_tree(&path)?;
        } else {
            fs::remove_file(path).map_err(|error| error.to_string())?;
        }
    }
    fs::remove_dir(root).map_err(|error| error.to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{CommandId, EntityId, ModuleId};
    use forge_project::{authoring::new_terrain, workspace::export::ModuleAreaArchive};
    use std::collections::BTreeMap;

    #[test]
    fn primary_can_move_to_an_rpg_area_without_changing_the_game_entry() {
        let module = ModuleId::deterministic("native-game-export-module");
        let first = ZoneEditSession::from_initial_terrain(
            new_terrain("native-game-export", "first", "First").unwrap(),
        )
        .unwrap();
        let mut second = ZoneEditSession::from_initial_terrain(
            new_terrain("native-game-export", "second", "Second").unwrap(),
        )
        .unwrap();
        let area = second.head().base.terrain.area_id;
        let player = EntityId::deterministic("native-game-export-player");
        let gameplay = serde_json::from_value(serde_json::json!({
            "schema":"forge.gameplay", "version":1, "module_id":module, "area_id":area, "player":player,
            "profile":{"id":"forge.gate3-minimal", "version":1, "interaction_range_m":3.0,
                "attack_range_m":2.0, "attack_damage":20, "attack_stamina_cost":5, "max_stamina":100,
                "max_action_receipts":512, "max_events_per_tick":128},
            "actors":[{"id":player,"name":"Player","role":"player","position":{"x":0.0,"y":0.0,"z":0.0},
                "max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
            "items":[],"objects":[],"places":[],"dialogues":[],"quests":[]
        })).unwrap();
        let edit = second
            .head()
            .prepare_gameplay(
                Some(gameplay),
                CommandId::deterministic("native-game-export-edit"),
            )
            .unwrap();
        second.execute(&edit).unwrap();
        let archive = |zone: &ZoneEditSession| ModuleAreaArchive {
            area_id: zone.head().base.terrain.area_id,
            head_hash: zone.head().content_hash().unwrap(),
            bytes: zone.archive_bytes().unwrap(),
        };
        let first_area = first.head().base.terrain.area_id;
        let mut snapshot = ModuleExportSnapshot {
            project_id: first.head().base.terrain.project_id,
            module_id: module,
            primary_area: first_area,
            primary_head: first.head().content_hash().unwrap(),
            workspace_hash: Some(bytes_sha256(b"workspace")),
            creator: None,
            zones: vec![archive(&first), archive(&second)],
            source_blobs: BTreeMap::new(),
        };
        let original_hash = snapshot.content_hash().unwrap();
        choose_primary(&mut snapshot).unwrap();
        assert_eq!(snapshot.primary_area, area);
        assert_eq!(snapshot.primary_head, second.head().content_hash().unwrap());
        assert_ne!(snapshot.primary_area, first_area);
        let normalized_hash = snapshot.content_hash().unwrap();
        assert_eq!(normalized_hash, original_hash);

        // A fresh workspace snapshot still names the original game entry.
        snapshot.primary_area = first_area;
        snapshot.primary_head = first.head().content_hash().unwrap();
        revalidate_snapshot(&mut snapshot, normalized_hash).unwrap();
        assert_eq!(snapshot.primary_area, area);
        assert_eq!(snapshot.primary_head, second.head().content_hash().unwrap());

        snapshot.zones[0].head_hash = bytes_sha256(b"changed zone head");
        assert!(revalidate_snapshot(&mut snapshot, normalized_hash).is_err());
    }
}
