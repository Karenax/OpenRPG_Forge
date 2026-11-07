//! Repeatable isolated UI fixture, using the canonical workspace/authoring host.
use forge_core::{CommandId, EntityId, ModuleId, ProjectId};
use forge_project::{
    audit::AuditStamp,
    authoring::{
        AuthoringRequest as A,
        creation::{NewTerrainSettings, prepare_module_terrain},
        host::AuthoringHost,
        workspace::WorkspaceHostRequest as W,
    },
    workspace::session::{WorkspaceAction, WorkspaceRequest},
};

fn arguments() -> Result<(std::path::PathBuf, AuditStamp, bool), String> {
    let mut arguments = std::env::args().skip(1);
    let root = std::path::PathBuf::from(
        arguments
            .next()
            .ok_or("Supply a new, absolute qualification-data directory")?,
    );
    let utc = arguments
        .next()
        .ok_or("Supply the actual UTC audit timestamp")?;
    let audit = AuditStamp::ai("gate6-qualification-fixture", utc)?;
    let creator = match arguments.next().as_deref() {
        None => false,
        Some("--creator") if arguments.next().is_none() => true,
        _ => return Err("Only the optional --creator flag is accepted".into()),
    };
    if !root.is_absolute() || root.exists() || !root.parent().is_some_and(std::path::Path::is_dir) {
        return Err("Fixture root must be a new child of an existing authorized directory".into());
    }
    Ok((root, audit, creator))
}

fn main() -> Result<(), String> {
    let (root, audit, creator) = arguments()?;
    std::fs::create_dir(&root).map_err(|error| error.to_string())?;
    let mut host = AuthoringHost::new(&root)?;
    host.execute_workspace(W::Create)?;
    let project = ProjectId::deterministic("gate6-ui/project");
    let module = ModuleId::deterministic("gate6-ui/module");
    for (key, action) in [
        (
            "project",
            WorkspaceAction::CreateProject {
                id: project,
                name: "Recette IA".into(),
            },
        ),
        (
            "module",
            WorkspaceAction::CreateModule {
                project_id: project,
                id: module,
                name: "Assistant local".into(),
            },
        ),
    ] {
        let head = host
            .execute_workspace(W::Inspect)?
            .head
            .ok_or("Workspace absent")?;
        host.execute_workspace_with_audit(
            W::Apply {
                request: Box::new(WorkspaceRequest {
                    command_id: CommandId::deterministic(format!("gate6-ui/{key}")),
                    base_hash: head.content_hash().map_err(|error| error.to_string())?,
                    base_revision: head.revision,
                    action,
                }),
            },
            audit.clone(),
        )?;
    }
    let document = Box::new(prepare_module_terrain(
        project,
        module,
        "village",
        "Village de recette",
        NewTerrainSettings::default(),
    )?);
    let area = document.area_id;
    let review = host.execute_workspace(W::ReviewNewZone {
        module_id: module,
        document: document.clone(),
    })?;
    host.execute_workspace_with_audit(
        W::CreateNewZone {
            request: Box::new(review.receipt.ok_or("Zone review absent")?.request),
            document,
        },
        audit.clone(),
    )?;
    host.execute_workspace(W::Save)?;
    let head = host
        .execute(A::OpenManaged { area_id: area })?
        .zone_head
        .ok_or("Zone absent")?;
    let id = |key: &str| EntityId::deterministic(format!("gate6-ui/{key}"));
    let gameplay: forge_gameplay::GameplayDocument = serde_json::from_value(serde_json::json!({
        "schema":"forge.gameplay","version":1,"module_id":module,"area_id":area,"player":id("player"),
        "profile":{"id":"forge.gate3-minimal","version":1,"interaction_range_m":3.0,"attack_range_m":2.0,"attack_damage":20,"attack_stamina_cost":5,"max_stamina":100,"max_action_receipts":512,"max_events_per_tick":128},
        "actors":[{"id":id("player"),"name":"Joueur de recette","role":"player","position":{"x":0.0,"y":0.85,"z":0.0},"max_vitality":100,"protection":0,"inventory_capacity":12,"dialogue":null,"patrol":[],"flees_danger":false}],
        "items":[],"places":[],"quests":[],
        "objects":[{"id":id("door"),"name":"Porte de recette","position":{"x":2.0,"y":0.0,"z":0.0},"kind":{"kind":"door","initially_open":false},"physics":forge_gameplay::ObjectPhysics::default()}],
        "dialogues":[{"id":id("dialogue"),"start":id("line"),"nodes":[{"id":id("line"),"text":"Bonjour, visiteur de recette.","choices":[]}]}]
    })).map_err(|error| error.to_string())?;
    // Only enable authoring capabilities in the starting fixture. The adventure
    // itself must still be generated, reviewed and applied through Studio.
    let gameplay = if creator {
        gameplay
            .with_creator_configuration(gameplay.creator_configuration())
            .map_err(|error| error.to_string())?
    } else {
        gameplay
    };
    let request = head.prepare_gameplay(
        Some(gameplay),
        CommandId::deterministic("gate6-ui/gameplay"),
    )?;
    host.execute_with_audit(
        A::PreviewZone {
            request: request.clone(),
        },
        audit.clone(),
    )?;
    host.execute_with_audit(A::ApplyZone { request }, audit)?;
    host.execute_workspace(W::Save)?;
    let state = host.execute(A::Inspect)?;
    println!(
        "{}",
        serde_json::json!({"schema":"forge.gate6-ui-fixture.v1","root":root,"project_id":project,"module_id":module,"area_id":area,"head_hash":state.zone_head.ok_or("Zone absent")?.content_hash()?,"synthetic_data_only":true,"creator_preconfigured":creator})
    );
    Ok(())
}
