#![cfg(not(target_arch = "wasm32"))]
use forge_core::{CommandId, ContentHash, Vec3};
use forge_project::{
    assistant::{
        planning::{PlanProposal, PlannedOperation},
        selection::{Region, Selection},
    },
    authoring::{AuthoringRequest as A, host::AuthoringHost, new_terrain},
    terrain_edit::{TerrainAction, TerrainEdit},
};
use forge_studio_frontend::{
    assistant_mcp::ToolCall,
    assistant_mcp_controller::Controller,
    mcp_protocol::{McpRequest, ScopeRequest},
};
use std::path::{Path, PathBuf};
const UTC: &str = "2026-10-22T11:00:00Z";

fn fixture(label: &str) -> (PathBuf, AuthoringHost, Controller, ScopeRequest) {
    let root = std::env::temp_dir().join(format!(
        "forge-mcp-controller-{label}-{}",
        std::process::id()
    ));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    let state = host
        .execute(A::CreateZone {
            document: Box::new(new_terrain("mcp-controller", "zone", "Original").unwrap()),
        })
        .unwrap();
    let scope = ScopeRequest {
        assets: None,
        expected_head: state.zone_head.unwrap().content_hash().unwrap(),
        selection: Selection {
            zone_properties: true,
            ..Selection::default()
        },
        region: Region {
            minimum: Vec3::new(-5.0, -5.0, -5.0),
            maximum: Vec3::new(5.0, 5.0, 5.0),
        },
    };
    (root, host, Controller::default(), scope)
}
fn authorize(controller: &mut Controller, host: &mut AuthoringHost, scope: &ScopeRequest) {
    controller.set_transport_open(true);
    let preview = controller
        .ui(
            host,
            McpRequest::Preview {
                scope: scope.clone(),
            },
        )
        .unwrap();
    assert!(!preview.authorized);
    assert!(controller.context(host).unwrap().is_none());
    controller
        .ui(
            host,
            McpRequest::Authorize {
                scope: scope.clone(),
                expected_context_hash: preview.context_hash.unwrap(),
            },
        )
        .unwrap();
}
fn propose(controller: &mut Controller, host: &mut AuthoringHost) {
    let context = controller.context(host).unwrap().unwrap();
    controller
        .tool(
            host,
            UTC,
            ToolCall::ProposeEdits(Box::new(PlanProposal {
                context_hash: context.content_hash().unwrap(),
                summary: "MCP local variant".into(),
                missing_capabilities: vec![],
                operations: vec![PlannedOperation::RenameZone {
                    name: "Variant".into(),
                }],
            })),
        )
        .unwrap();
}
fn cleanup(root: &Path, host: AuthoringHost, controller: Controller) {
    drop(controller);
    drop(host);
    let absolute = root.canonicalize().unwrap();
    assert_eq!(
        absolute.parent(),
        Some(std::env::temp_dir().canonicalize().unwrap().as_path())
    );
    assert!(
        absolute
            .file_name()
            .unwrap()
            .to_string_lossy()
            .starts_with("forge-mcp-controller-")
    );
    std::fs::remove_dir_all(absolute).unwrap();
}
#[test]
fn preview_is_not_authorization_and_hash_transport_and_scope_must_all_match() {
    let (root, mut host, mut controller, scope) = fixture("approval");
    assert!(!controller.status().unwrap().stdio_open);
    let view = controller
        .ui(
            &mut host,
            McpRequest::Preview {
                scope: scope.clone(),
            },
        )
        .unwrap();
    let hash = view.context_hash.unwrap();
    assert!(!view.authorized);
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Authorize {
                    scope: scope.clone(),
                    expected_context_hash: hash
                }
            )
            .is_err()
    );
    controller.set_transport_open(true);
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Authorize {
                    scope: scope.clone(),
                    expected_context_hash: ContentHash::parse(&"0".repeat(64)).unwrap()
                }
            )
            .is_err()
    );
    let mut altered = scope.clone();
    altered.region.maximum.x = 4.0;
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Authorize {
                    scope: altered,
                    expected_context_hash: hash
                }
            )
            .is_err()
    );
    assert!(controller.context(&mut host).unwrap().is_none());
    controller
        .ui(
            &mut host,
            McpRequest::Authorize {
                scope,
                expected_context_hash: hash,
            },
        )
        .unwrap();
    assert!(controller.context(&mut host).unwrap().is_some());
    cleanup(&root, host, controller);
}
#[test]
fn client_stages_in_the_ui_inbox_and_only_exact_studio_approval_mutates_the_host() {
    let (root, mut host, mut controller, scope) = fixture("apply");
    authorize(&mut controller, &mut host, &scope);
    controller
        .tool(&mut host, UTC, ToolCall::InspectSelection)
        .unwrap();
    propose(&mut controller, &mut host);
    let view = controller.status().unwrap();
    assert_eq!(view.tool_calls, 2);
    assert_eq!(view.proposals.len(), 1);
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .content_hash()
            .unwrap(),
        scope.expected_head
    );
    let review = &view.proposals[0];
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Apply {
                    command_id: review.command_id,
                    review_hash: ContentHash::parse(&"0".repeat(64)).unwrap()
                }
            )
            .is_err()
    );
    let applied = controller
        .ui(
            &mut host,
            McpRequest::Apply {
                command_id: review.command_id,
                review_hash: review.review_hash,
            },
        )
        .unwrap();
    assert_eq!(
        applied
            .authoring
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Variant"
    );
    assert!(controller.context(&mut host).unwrap().is_none());
    let replay = controller
        .ui(
            &mut host,
            McpRequest::Apply {
                command_id: review.command_id,
                review_hash: review.review_hash,
            },
        )
        .unwrap();
    assert_eq!(
        replay
            .authoring
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Variant"
    );
    cleanup(&root, host, controller);
}
#[test]
fn revoke_disconnect_and_new_preview_remove_prior_authority_and_pending_approvals() {
    let (root, mut host, mut controller, scope) = fixture("revoke");
    authorize(&mut controller, &mut host, &scope);
    propose(&mut controller, &mut host);
    let review = controller.status().unwrap().proposals.remove(0);
    controller.revoke_view().unwrap();
    assert!(controller.context(&mut host).unwrap().is_none());
    assert!(controller.status().unwrap().proposals.is_empty());
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Apply {
                    command_id: review.command_id,
                    review_hash: review.review_hash
                }
            )
            .is_err()
    );
    authorize(&mut controller, &mut host, &scope);
    propose(&mut controller, &mut host);
    controller.set_transport_open(false);
    assert!(!controller.status().unwrap().stdio_open);
    assert!(controller.status().unwrap().proposals.is_empty());
    assert!(
        controller
            .tool(&mut host, UTC, ToolCall::InspectSelection)
            .is_err()
    );
    authorize(&mut controller, &mut host, &scope);
    propose(&mut controller, &mut host);
    controller
        .ui(&mut host, McpRequest::Preview { scope })
        .unwrap();
    assert!(!controller.authorized());
    assert!(controller.status().unwrap().proposals.is_empty());
    cleanup(&root, host, controller);
}
#[test]
fn manual_edit_hides_the_scope_and_blocks_approval_of_the_previous_diff() {
    let (root, mut host, mut controller, scope) = fixture("stale");
    authorize(&mut controller, &mut host, &scope);
    propose(&mut controller, &mut host);
    let review = controller.status().unwrap().proposals.remove(0);
    let head = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename {
                name: "Human".into(),
            }),
            CommandId::deterministic("mcp-controller/human"),
        )
        .unwrap();
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    host.execute(A::ApplyZone { request }).unwrap();
    assert!(controller.context(&mut host).unwrap().is_none());
    assert!(
        controller
            .tool(&mut host, UTC, ToolCall::InspectSelection)
            .is_err()
    );
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Apply {
                    command_id: review.command_id,
                    review_hash: review.review_hash
                }
            )
            .is_err()
    );
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Human"
    );
    cleanup(&root, host, controller);
}

#[test]
fn retrying_the_same_scope_confirmation_keeps_staged_proposals_and_review_hashes() {
    let (root, mut host, mut controller, scope) = fixture("reauthorize");
    authorize(&mut controller, &mut host, &scope);
    propose(&mut controller, &mut host);
    let before = controller.status().unwrap();
    let proposal = &before.proposals[0];
    let after = controller
        .ui(
            &mut host,
            McpRequest::Authorize {
                scope,
                expected_context_hash: before.context_hash.unwrap(),
            },
        )
        .unwrap();
    assert_eq!(after.proposals.len(), 1);
    assert_eq!(after.proposals[0].command_id, proposal.command_id);
    assert_eq!(after.proposals[0].review_hash, proposal.review_hash);
    let applied = controller
        .ui(
            &mut host,
            McpRequest::Apply {
                command_id: proposal.command_id,
                review_hash: proposal.review_hash,
            },
        )
        .unwrap();
    assert_eq!(
        applied
            .authoring
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Variant"
    );
    cleanup(&root, host, controller);
}

fn persistent_controller(root: &Path) -> Controller {
    let mut controller = Controller::default();
    controller.set_storage_root(root).unwrap();
    controller.set_transport_open(true);
    controller
}
fn authorize_saved(
    controller: &mut Controller,
    host: &mut AuthoringHost,
    scope: &ScopeRequest,
) -> Result<forge_studio_frontend::mcp_protocol::McpView, String> {
    let preview = controller.ui(
        host,
        McpRequest::Preview {
            scope: scope.clone(),
        },
    )?;
    controller.ui(
        host,
        McpRequest::Authorize {
            scope: scope.clone(),
            expected_context_hash: preview.context_hash.unwrap(),
        },
    )
}
fn proposal_file(root: &Path, controller: &Controller) -> PathBuf {
    let context = controller.status().unwrap().context_hash.unwrap();
    let directory = root
        .join("assistant-mcp-proposals")
        .join(context.to_string());
    let proposal = std::fs::read_dir(directory)
        .unwrap()
        .next()
        .unwrap()
        .unwrap();
    proposal.path().join("authoring.json")
}

#[test]
fn persistent_proposal_survives_restart_without_restoring_scope_or_approval() {
    let (root, mut host, controller, scope) = fixture("persistent-restart");
    drop(controller);
    host.execute(A::Save).unwrap();
    let area = host
        .execute(A::Inspect)
        .unwrap()
        .zone_head
        .unwrap()
        .base
        .terrain
        .area_id;
    let mut controller = persistent_controller(&root);
    authorize_saved(&mut controller, &mut host, &scope).unwrap();
    propose(&mut controller, &mut host);
    let old = controller.status().unwrap().proposals[0].clone();
    let file = proposal_file(&root, &controller);
    let saved = std::fs::read(&file).unwrap();
    drop(controller);
    drop(host);
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::Open { area_id: area }).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    let mut controller = persistent_controller(&root);
    assert!(!controller.status().unwrap().authorized);
    assert!(controller.status().unwrap().proposals.is_empty());
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Apply {
                    command_id: old.command_id,
                    review_hash: old.review_hash
                }
            )
            .is_err()
    );
    let restored = authorize_saved(&mut controller, &mut host, &scope)
        .unwrap()
        .proposals
        .remove(0);
    assert_eq!(restored.command_id, old.command_id);
    assert_ne!(restored.review_hash, old.review_hash);
    assert!(
        controller
            .ui(
                &mut host,
                McpRequest::Apply {
                    command_id: old.command_id,
                    review_hash: old.review_hash
                }
            )
            .is_err()
    );
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert_eq!(std::fs::read(file).unwrap(), saved);
    propose(&mut controller, &mut host);
    assert_eq!(controller.status().unwrap().proposals.len(), 1);
    assert_eq!(
        controller.status().unwrap().proposals[0].review_hash,
        restored.review_hash
    );
    controller
        .ui(
            &mut host,
            McpRequest::Apply {
                command_id: restored.command_id,
                review_hash: restored.review_hash,
            },
        )
        .unwrap();
    host.execute(A::Save).unwrap();
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Variant"
    );
    cleanup(&root, host, controller);
}

#[test]
fn failed_staging_preserves_the_source_and_never_inserts_an_applicable_proposal() {
    let (root, mut host, controller, scope) = fixture("persistent-write-refused");
    drop(controller);
    let mut controller = persistent_controller(&root);
    authorize_saved(&mut controller, &mut host, &scope).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    let group = root.join("assistant-mcp-proposals");
    std::fs::write(&group, b"protected invalid directory").unwrap();
    let context = controller.context(&mut host).unwrap().unwrap();
    assert!(
        controller
            .tool(
                &mut host,
                UTC,
                ToolCall::ProposeEdits(Box::new(PlanProposal {
                    context_hash: context.content_hash().unwrap(),
                    summary: "Refused staging".into(),
                    missing_capabilities: vec![],
                    operations: vec![PlannedOperation::RenameZone {
                        name: "Never applied".into()
                    }]
                }))
            )
            .is_err()
    );
    assert!(controller.status().unwrap().proposals.is_empty());
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    assert_eq!(
        std::fs::read(group).unwrap(),
        b"protected invalid directory"
    );
    cleanup(&root, host, controller);
}

#[test]
fn altered_saved_proposal_cannot_restore_authority_or_rewrite_its_evidence() {
    let (root, mut host, controller, scope) = fixture("persistent-tampered");
    drop(controller);
    let mut controller = persistent_controller(&root);
    authorize_saved(&mut controller, &mut host, &scope).unwrap();
    propose(&mut controller, &mut host);
    let path = proposal_file(&root, &controller);
    let mut value: serde_json::Value =
        serde_json::from_slice(&std::fs::read(&path).unwrap()).unwrap();
    value["proposal"]["operations"][0]["name"] = serde_json::json!("Forged");
    let tampered = serde_json::to_vec(&value).unwrap();
    std::fs::write(&path, &tampered).unwrap();
    drop(controller);
    let before = host.execute(A::Inspect).unwrap();
    let mut controller = persistent_controller(&root);
    assert!(authorize_saved(&mut controller, &mut host, &scope).is_err());
    assert!(!controller.status().unwrap().authorized);
    assert!(controller.status().unwrap().proposals.is_empty());
    assert_eq!(std::fs::read(path).unwrap(), tampered);
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    cleanup(&root, host, controller);
}

#[test]
fn complete_interrupted_immutable_staging_recovers_only_after_new_scope_authorization() {
    let (root, mut host, controller, scope) = fixture("persistent-pending");
    drop(controller);
    let mut controller = persistent_controller(&root);
    authorize_saved(&mut controller, &mut host, &scope).unwrap();
    propose(&mut controller, &mut host);
    let original = controller.status().unwrap().proposals[0].clone();
    let path = proposal_file(&root, &controller);
    let bytes = std::fs::read(&path).unwrap();
    drop(controller);
    let pending = path.with_file_name("authoring.pending");
    std::fs::rename(&path, &pending).unwrap();
    let before = host.execute(A::Inspect).unwrap();
    let mut controller = persistent_controller(&root);
    assert!(controller.status().unwrap().proposals.is_empty());
    assert!(pending.exists());
    let restored = authorize_saved(&mut controller, &mut host, &scope).unwrap();
    assert_eq!(restored.proposals.len(), 1);
    assert_eq!(restored.proposals[0].command_id, original.command_id);
    assert_ne!(restored.proposals[0].review_hash, original.review_hash);
    assert!(!pending.exists());
    assert_eq!(std::fs::read(path).unwrap(), bytes);
    assert_eq!(host.execute(A::Inspect).unwrap(), before);
    cleanup(&root, host, controller);
}
