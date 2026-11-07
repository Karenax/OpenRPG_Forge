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
    assistant_mcp::{self, Server},
    assistant_mcp_native::Inbox,
};
use serde_json::{Value, json};
use std::{io::Cursor, path::PathBuf};
const UTC: &str = "2026-10-22T10:00:00Z";

fn fixture(label: &str) -> (PathBuf, AuthoringHost, Inbox) {
    let root = std::env::temp_dir().join(format!("forge-mcp-{label}-{}", std::process::id()));
    std::fs::create_dir(&root).unwrap();
    let mut host = AuthoringHost::new(&root).unwrap();
    host.execute(A::CreateZone {
        document: Box::new(new_terrain("mcp", "zone", "Original").unwrap()),
    })
    .unwrap();
    let inbox = Inbox::authorize(
        &mut host,
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-5.0, -5.0, -5.0),
            maximum: Vec3::new(5.0, 5.0, 5.0),
        },
    )
    .unwrap();
    (root, host, inbox)
}
fn request(id: u32, method: &str, params: Value) -> Vec<u8> {
    let mut message = json!({"jsonrpc":"2.0","id":id,"method":method});
    message["params"] = params;
    serde_json::to_vec(&message).unwrap()
}
fn initialize(server: &mut Server, host: &mut AuthoringHost, inbox: &mut Inbox) {
    let reply=server.dispatch(&request(1,"initialize",json!({"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"qualification-client","version":"1"}})),&mut inbox.backend(host,UTC)).unwrap();
    assert_eq!(reply["result"]["protocolVersion"], "2025-11-25");
    assert!(
        server
            .dispatch(
                br#"{"jsonrpc":"2.0","method":"notifications/initialized"}"#,
                &mut inbox.backend(host, UTC)
            )
            .is_none()
    );
}
fn inspect(server: &mut Server, host: &mut AuthoringHost, inbox: &mut Inbox) -> Value {
    server
        .dispatch(
            &request(
                2,
                "tools/call",
                json!({"name":"forge_inspect_selection","arguments":{}}),
            ),
            &mut inbox.backend(host, UTC),
        )
        .unwrap()["result"]["structuredContent"]
        .clone()
}
fn proposal(context: &Value, name: &str) -> PlanProposal {
    // Use the canonical context hash, rather than an externally asserted scope.
    let hash = forge_core::canonical_json_sha256(context).unwrap();
    PlanProposal {
        context_hash: hash,
        summary: "MCP test proposal".into(),
        missing_capabilities: vec![],
        operations: vec![PlannedOperation::RenameZone { name: name.into() }],
    }
}
fn stage(
    server: &mut Server,
    host: &mut AuthoringHost,
    inbox: &mut Inbox,
    proposal: &PlanProposal,
) -> Value {
    server
        .dispatch(
            &request(
                3,
                "tools/call",
                json!({"name":"forge_propose_edits","arguments":proposal}),
            ),
            &mut inbox.backend(host, UTC),
        )
        .unwrap()
}
fn cleanup(root: &std::path::Path, host: AuthoringHost, inbox: Inbox) {
    drop(inbox);
    drop(host);
    let absolute = root.canonicalize().unwrap();
    let temporary = std::env::temp_dir().canonicalize().unwrap();
    assert_eq!(absolute.parent(), Some(temporary.as_path()));
    assert!(
        absolute
            .file_name()
            .unwrap()
            .to_string_lossy()
            .starts_with("forge-mcp-")
    );
    std::fs::remove_dir_all(absolute).unwrap();
}

#[test]
fn lifecycle_catalogue_and_protocol_refuse_approval_shell_paths_and_invalid_frames() {
    let (root, mut host, mut inbox) = fixture("wire");
    let mut server = Server::default();
    let reply = server
        .dispatch(
            &request(0, "tools/list", json!({})),
            &mut inbox.backend(&mut host, UTC),
        )
        .unwrap();
    assert_eq!(reply["error"]["code"], -32002);
    initialize(&mut server, &mut host, &mut inbox);
    let list = server
        .dispatch(
            &request(2, "tools/list", json!({})),
            &mut inbox.backend(&mut host, UTC),
        )
        .unwrap();
    let names: Vec<_> = list["result"]["tools"]
        .as_array()
        .unwrap()
        .iter()
        .map(|tool| tool["name"].as_str().unwrap())
        .collect();
    assert_eq!(
        names,
        vec!["forge_inspect_selection", "forge_propose_edits"]
    );
    for name in ["apply", "approve", "shell", "read_file"] {
        let reply = server
            .dispatch(
                &request(3, "tools/call", json!({"name":name,"arguments":{}})),
                &mut inbox.backend(&mut host, UTC),
            )
            .unwrap();
        assert_eq!(reply["error"]["code"], -32602);
    }
    let reply = server
        .dispatch(
            &request(
                4,
                "tools/call",
                json!({"name":"forge_inspect_selection","arguments":{"path":"C:/private"}}),
            ),
            &mut inbox.backend(&mut host, UTC),
        )
        .unwrap();
    assert_eq!(reply["error"]["code"], -32602);
    for frame in [
        b"[]".as_slice(),
        br#"{"jsonrpc":"2.0","id":null,"method":"ping"}"#,
    ] {
        assert_eq!(
            server
                .dispatch(frame, &mut inbox.backend(&mut host, UTC))
                .unwrap()["error"]["code"],
            -32600
        );
    }
    assert_eq!(
        server
            .dispatch(b"broken", &mut inbox.backend(&mut host, UTC))
            .unwrap()["error"]["code"],
        -32700
    );
    cleanup(&root, host, inbox);
}

#[test]
fn real_host_stages_without_mutation_deduplicates_then_requires_trusted_exact_approval() {
    let (root, mut host, mut inbox) = fixture("apply");
    let mut server = Server::default();
    initialize(&mut server, &mut host, &mut inbox);
    let context = inspect(&mut server, &mut host, &mut inbox);
    let before = host
        .execute(A::Inspect)
        .unwrap()
        .zone_head
        .unwrap()
        .content_hash()
        .unwrap();
    let plan = proposal(&context, "MCP variant");
    let first = stage(&mut server, &mut host, &mut inbox, &plan);
    assert_eq!(first["result"]["isError"], false);
    let repeated = stage(&mut server, &mut host, &mut inbox, &plan);
    assert_eq!(
        first["result"]["structuredContent"]["command_id"],
        repeated["result"]["structuredContent"]["command_id"]
    );
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .content_hash()
            .unwrap(),
        before
    );
    let reviews = inbox.proposals().unwrap();
    assert_eq!(reviews.len(), 1);
    assert_eq!(reviews[0].changes.len(), 1);
    let review = &reviews[0];
    assert!(
        inbox
            .apply_confirmed(
                &mut host,
                review.command_id,
                ContentHash::parse(&"0".repeat(64)).unwrap()
            )
            .is_err()
    );
    let applied = inbox
        .apply_confirmed(&mut host, review.command_id, review.review_hash)
        .unwrap();
    assert_eq!(
        applied.zone_head.unwrap().effective().unwrap().0.name,
        "MCP variant"
    );
    let replay = inbox
        .apply_confirmed(&mut host, review.command_id, review.review_hash)
        .unwrap();
    assert_eq!(
        replay.zone_head.unwrap().effective().unwrap().0.name,
        "MCP variant"
    );
    assert!(inspect(&mut server, &mut host, &mut inbox).is_null());
    cleanup(&root, host, inbox);
}

#[test]
fn stale_review_and_out_of_scope_proposal_preserve_human_sources() {
    let (root, mut host, mut inbox) = fixture("stale");
    let mut server = Server::default();
    initialize(&mut server, &mut host, &mut inbox);
    let context = inspect(&mut server, &mut host, &mut inbox);
    let mut plan = proposal(&context, "Rejected variant");
    plan.operations = vec![PlannedOperation::MoveObject {
        id: forge_core::EntityId::deterministic("not-selected"),
        position: Vec3::new(0.0, 0.0, 0.0),
    }];
    assert_eq!(
        stage(&mut server, &mut host, &mut inbox, &plan)["result"]["isError"],
        true
    );
    assert!(inbox.proposals().unwrap().is_empty());
    plan = proposal(&context, "Stale variant");
    assert_eq!(
        stage(&mut server, &mut host, &mut inbox, &plan)["result"]["isError"],
        false
    );
    let head = host.execute(A::Inspect).unwrap().zone_head.unwrap();
    let request = head
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename {
                name: "Human correction".into(),
            }),
            CommandId::deterministic("mcp-manual"),
        )
        .unwrap();
    host.execute(A::PreviewZone {
        request: request.clone(),
    })
    .unwrap();
    host.execute(A::ApplyZone { request }).unwrap();
    let review = inbox.proposals().unwrap().remove(0);
    assert!(
        inbox
            .apply_confirmed(&mut host, review.command_id, review.review_hash)
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
        "Human correction"
    );
    cleanup(&root, host, inbox);
}

#[test]
fn stdio_frames_are_json_only_and_oversized_input_terminates_without_backend_work() {
    let (root, mut host, mut inbox) = fixture("stdio");
    let mut input = request(
        1,
        "initialize",
        json!({"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"stdio-test","version":"1"}}),
    );
    input.push(b'\n');
    input.extend_from_slice(b"{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n");
    input.extend(request(
        2,
        "tools/call",
        json!({"name":"forge_inspect_selection","arguments":{}}),
    ));
    input.push(b'\n');
    let mut output = vec![];
    assistant_mcp::serve(
        Cursor::new(input),
        &mut output,
        &mut inbox.backend(&mut host, UTC),
    )
    .unwrap();
    let replies: Vec<Value> = std::str::from_utf8(&output)
        .unwrap()
        .lines()
        .map(|line| serde_json::from_str(line).unwrap())
        .collect();
    assert_eq!(replies.len(), 2);
    assert_eq!(
        replies[1]["result"]["structuredContent"]["zone_name"],
        "Original"
    );
    let oversized = vec![b'x'; assistant_mcp::MAX_FRAME_BYTES + 3];
    assert_eq!(
        assistant_mcp::serve(
            Cursor::new(oversized),
            Vec::new(),
            &mut inbox.backend(&mut host, UTC)
        )
        .unwrap_err()
        .kind(),
        std::io::ErrorKind::InvalidData
    );
    assert!(inbox.proposals().unwrap().is_empty());
    cleanup(&root, host, inbox);
}

#[test]
fn absent_authorization_and_session_quota_cannot_invoke_a_backend() {
    struct Unavailable;
    impl assistant_mcp::Backend for Unavailable {
        fn context(
            &self,
        ) -> Result<Option<forge_project::assistant::selection::PlanningContext>, String> {
            Ok(None)
        }
        fn execute(&mut self, _: assistant_mcp::ToolCall) -> Result<Value, String> {
            panic!("Unauthorized backend must never execute")
        }
    }
    let mut backend = Unavailable;
    let mut server = Server::default();
    server.dispatch(&request(1,"initialize",json!({"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"bounded","version":"1"}})),&mut backend).unwrap();
    server.dispatch(
        br#"{"jsonrpc":"2.0","method":"notifications/initialized"}"#,
        &mut backend,
    );
    assert_eq!(
        server
            .dispatch(&request(2, "tools/list", json!({})), &mut backend)
            .unwrap()["result"]["tools"],
        json!([])
    );
    assert_eq!(
        server
            .dispatch(
                &request(
                    3,
                    "tools/call",
                    json!({"name":"forge_inspect_selection","arguments":{}})
                ),
                &mut backend
            )
            .unwrap()["result"]["isError"],
        true
    );
    for id in 4..=512 {
        assert!(
            server
                .dispatch(&request(id, "ping", json!({})), &mut backend)
                .unwrap()
                .get("result")
                .is_some()
        );
    }
    assert_eq!(
        server
            .dispatch(&request(513, "ping", json!({})), &mut backend)
            .unwrap()["error"]["code"],
        -32000
    );
}

#[test]
fn native_validator_and_bounded_inbox_refuse_without_changing_sources() {
    let (root, host, mut inbox) = fixture("quota");
    let mut host = host.with_zone_validator(|head| {
        if head.corrections.name.as_deref() == Some("Forbidden") {
            Err("Native candidate rejected".into())
        } else {
            Ok(())
        }
    });
    let mut server = Server::default();
    initialize(&mut server, &mut host, &mut inbox);
    let context = inspect(&mut server, &mut host, &mut inbox);
    assert_eq!(
        stage(
            &mut server,
            &mut host,
            &mut inbox,
            &proposal(&context, "Forbidden")
        )["result"]["isError"],
        true
    );
    assert!(inbox.proposals().unwrap().is_empty());
    for index in 0..16 {
        assert_eq!(
            stage(
                &mut server,
                &mut host,
                &mut inbox,
                &proposal(&context, &format!("Proposal {index}"))
            )["result"]["isError"],
            false
        );
    }
    assert_eq!(
        stage(
            &mut server,
            &mut host,
            &mut inbox,
            &proposal(&context, "Overflow")
        )["result"]["isError"],
        true
    );
    assert_eq!(inbox.proposals().unwrap().len(), 16);
    assert_eq!(
        host.execute(A::Inspect)
            .unwrap()
            .zone_head
            .unwrap()
            .effective()
            .unwrap()
            .0
            .name,
        "Original"
    );
    cleanup(&root, host, inbox);
}
