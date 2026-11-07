use forge_core::Vec3;
use forge_project::{
    assistant::{
        provider::{ApiKind, ProviderRequest},
        selection::{Region, Selection, SelectionScope},
    },
    authoring::new_terrain,
    zone_authoring::session::ZoneEditSession,
};
use serde_json::{Value, json};

fn request(kind: ApiKind, brief: &str, destination: &str) -> ProviderRequest {
    let session = ZoneEditSession::from_initial_terrain(
        new_terrain("provider-wire", "zone", "Original").unwrap(),
    )
    .unwrap();
    let scope = SelectionScope::new(
        session.head(),
        &Selection {
            zone_properties: true,
            ..Selection::default()
        },
        Region {
            minimum: Vec3::new(-10.0, -10.0, -10.0),
            maximum: Vec3::new(10.0, 10.0, 10.0),
        },
    )
    .unwrap();
    ProviderRequest::new(kind, destination, "test-model", brief, scope.context(), 512).unwrap()
}

fn proposal(request: &ProviderRequest) -> Value {
    let hash =
        match request.kind() {
            ApiKind::ChatCompletions => &request.body()["response_format"]["json_schema"]["schema"]
                ["properties"]["context_hash"]["enum"][0],
            ApiKind::Responses => {
                &request.body()["text"]["format"]["schema"]["properties"]["context_hash"]["enum"][0]
            }
        };
    json!({"context_hash":hash,"summary":"Proposed rename","missing_capabilities":[],"operations":[{"operation":"rename_zone","name":"Variant"}]})
}

fn chat_response(request: &ProviderRequest) -> Value {
    json!({"id":"response-1","model":"test-model","choices":[{"finish_reason":"stop","message":{"role":"assistant","content":proposal(request).to_string()}}],"usage":{"prompt_tokens":200,"completion_tokens":100}})
}

#[test]
fn both_wire_formats_are_strict_and_parse_real_plan_shapes_without_reasoning_capture() {
    let chat = request(
        ApiKind::ChatCompletions,
        "Rename the zone",
        "http://127.0.0.1:8080/v1/chat/completions",
    );
    assert_eq!(chat.body()["max_completion_tokens"], 512);
    assert_eq!(
        chat.body()["response_format"]["json_schema"]["strict"],
        true
    );
    let mut response = chat_response(&chat);
    response["choices"][0]["message"]["reasoning_content"] = json!("UNSTORED_REASONING");
    let result = chat
        .parse_response(&serde_json::to_vec(&response).unwrap())
        .unwrap();
    assert_eq!(result.usage.input_tokens, 200);
    assert!(
        !serde_json::to_string(&result)
            .unwrap()
            .contains("UNSTORED_REASONING")
    );
    let responses = request(
        ApiKind::Responses,
        "Rename the zone",
        "https://api.openai.com/v1/responses",
    );
    assert_eq!(responses.body()["store"], false);
    let raw = json!({"id":"response-2","model":"test-model","status":"completed","output":[{"type":"reasoning","summary":[]},{"type":"message","role":"assistant","content":[{"type":"output_text","text":proposal(&responses).to_string()}]}],"usage":{"input_tokens":200,"output_tokens":100}});
    assert!(
        responses
            .parse_response(&serde_json::to_vec(&raw).unwrap())
            .is_ok()
    );
}

#[test]
fn outbound_approval_binds_destination_brief_model_and_full_context() {
    let first = request(
        ApiKind::ChatCompletions,
        "Rename the zone",
        "http://127.0.0.1:8080/v1/chat/completions",
    );
    let approval = first
        .approve_outbound(first.content_hash().unwrap())
        .unwrap();
    assert!(first.verify_outbound(&approval).is_ok());
    let thinking = first.clone().with_local_reasoning().unwrap();
    assert_eq!(
        thinking.body()["chat_template_kwargs"]["enable_thinking"],
        true
    );
    assert!(thinking.verify_outbound(&approval).is_err());
    assert!(
        request(
            ApiKind::Responses,
            "Rename",
            "https://api.openai.com/v1/responses"
        )
        .with_local_reasoning()
        .is_err()
    );
    for other in [
        request(
            ApiKind::ChatCompletions,
            "Another request",
            first.destination(),
        ),
        request(
            ApiKind::ChatCompletions,
            "Rename the zone",
            "https://example.test/v1/chat/completions",
        ),
    ] {
        assert!(other.verify_outbound(&approval).is_err());
    }
}

#[test]
fn missing_usage_truncation_refusal_tools_and_wrong_context_fail_closed() {
    let request = request(
        ApiKind::ChatCompletions,
        "Rename",
        "http://127.0.0.1:8080/v1/chat/completions",
    );
    let mut cases = Vec::new();
    let mut value = chat_response(&request);
    value["usage"] = Value::Null;
    cases.push(value);
    let mut value = chat_response(&request);
    value["choices"][0]["finish_reason"] = json!("length");
    cases.push(value);
    let mut value = chat_response(&request);
    value["choices"][0]["message"]["refusal"] = json!("refused");
    cases.push(value);
    let mut value = chat_response(&request);
    value["choices"][0]["message"]["tool_calls"] = json!([{"name":"shell"}]);
    cases.push(value);
    let mut value = chat_response(&request);
    let mut plan = proposal(&request);
    plan["context_hash"] = json!("0".repeat(64));
    value["choices"][0]["message"]["content"] = json!(plan.to_string());
    cases.push(value);
    for value in cases {
        assert!(
            request
                .parse_response(&serde_json::to_vec(&value).unwrap())
                .is_err()
        );
    }
    assert!(
        request
            .parse_response(&vec![b' '; 1024 * 1024 + 1])
            .is_err()
    );
}
