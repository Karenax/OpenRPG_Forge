//! Explicit fixture export for native UI pagination checks, never production data.
#![cfg(not(target_arch = "wasm32"))]
use forge_core::{AreaId, CommandId, EntityId, Vec3};
use forge_gameplay::{ActorRole, DialogueDefinition, DialogueNode, ObjectKind};
use forge_project::{
    audit::AuditStamp,
    authoring::{AuthoringRequest as A, host::AuthoringHost, workspace::WorkspaceHostRequest as W},
};
use std::path::PathBuf;

#[test]
#[ignore = "manual UI fixture export; requires the isolated UX copy and a closed Studio"]
fn prepare_complete_target_index_for_native_ui() {
    let root =
        PathBuf::from(std::env::var_os("FORGE_UX_EDITOR_ROOT").expect("explicit UX fixture root"));
    let workspace = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .ancestors()
        .nth(3)
        .unwrap()
        .to_path_buf();
    let expected = workspace.join(".build/qualification-data/ux-editor/workspace");
    assert_eq!(
        root.canonicalize().unwrap(),
        expected.canonicalize().unwrap()
    );
    assert!(
        root.parent()
            .unwrap()
            .join("ux-fixture-marker.json")
            .is_file()
    );
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute_workspace(W::Browse).unwrap();
    let area: AreaId = "30e53b52-88ff-50bf-8ded-18283e0a48d6".parse().unwrap();
    let head = host
        .execute(A::OpenManaged { area_id: area })
        .unwrap()
        .zone_head
        .unwrap();
    let mut gameplay = *head.corrections.gameplay.clone().unwrap();
    let before = gameplay.clone();
    add_objects(&mut gameplay);
    add_dialogues(&mut gameplay);
    gameplay.validate().unwrap();
    if gameplay != before {
        let request = head
            .prepare_gameplay(
                Some(gameplay),
                CommandId::deterministic(format!(
                    "ux-editor-targets/{}",
                    head.content_hash().unwrap()
                )),
            )
            .unwrap();
        let audit = AuditStamp::ai("codex/isolated-ux-fixture", "2026-10-22T12:35:00Z").unwrap();
        host.execute_with_audit(
            A::PreviewZone {
                request: request.clone(),
            },
            audit.clone(),
        )
        .unwrap();
        host.execute_with_audit(A::ApplyZone { request }, audit)
            .unwrap();
        host.execute(A::Save).unwrap();
    }
    host.execute(A::Close).unwrap();
    let state = host.execute(A::OpenManaged { area_id: area }).unwrap();
    let gameplay = state.zone_head.unwrap().corrections.gameplay.unwrap();
    assert_eq!(
        gameplay
            .objects
            .iter()
            .filter(|object| object.name.starts_with("UX cible "))
            .count(),
        129
    );
    assert!(
        gameplay
            .dialogues
            .iter()
            .map(|dialogue| dialogue.nodes.len())
            .sum::<usize>()
            > 128
    );
    println!(
        "UX_TARGET_FIXTURE objects={} dialogue_lines={} reopened=true",
        gameplay.objects.len(),
        gameplay
            .dialogues
            .iter()
            .map(|dialogue| dialogue.nodes.len())
            .sum::<usize>()
    );
}

fn add_objects(gameplay: &mut forge_gameplay::GameplayDocument) {
    let object = gameplay
        .objects
        .iter()
        .find(|object| matches!(object.kind, ObjectKind::Prop))
        .unwrap()
        .clone();
    for index in 1_u16..=129 {
        let name = format!("UX cible {index:03}");
        if gameplay.objects.iter().any(|object| object.name == name) {
            continue;
        }
        let mut object = object.clone();
        object.id = EntityId::deterministic(format!("ux-editor-target/{index}"));
        object.name = name;
        object.position = Vec3::new(
            -60.0 + f32::from(index % 16) * 5.0,
            0.5,
            -60.0 + f32::from(index / 16) * 5.0,
        );
        gameplay.objects.push(object);
    }
}

fn add_dialogues(gameplay: &mut forge_gameplay::GameplayDocument) {
    let actor = gameplay
        .actors
        .iter()
        .find(|actor| actor.role == ActorRole::Friendly)
        .unwrap()
        .clone();
    for group in 0_u16..2 {
        let dialogue_id = EntityId::deterministic(format!("ux-editor-dialogue/{group}"));
        if gameplay.dialogue(dialogue_id).is_some() {
            continue;
        }
        let nodes = (1_u16..=65)
            .map(|index| DialogueNode {
                id: EntityId::deterministic(format!("ux-editor-dialogue/{group}/{index}")),
                text: format!("UX réplique {:03}", group * 65 + index),
                choices: vec![],
            })
            .collect::<Vec<_>>();
        let mut speaker = actor.clone();
        speaker.id = EntityId::deterministic(format!("ux-editor-speaker/{group}"));
        speaker.name = format!("UX interlocuteur {}", group + 1);
        speaker.dialogue = Some(dialogue_id);
        speaker.patrol.clear();
        speaker.position = Vec3::new(110.0 + f32::from(group) * 5.0, 1.0, 20.0);
        gameplay.actors.push(speaker);
        gameplay.dialogues.push(DialogueDefinition {
            id: dialogue_id,
            start: nodes[0].id,
            nodes,
        });
    }
}
