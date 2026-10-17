use forge_core::bytes_sha256;
use forge_delivery::selected_legacy_dialogue::*;
use serde_json::{Value, json};

fn fixture() -> Value {
    json!({"formatVersion":"1.30.0","project":{
        "module":{"metadata":{"license":"LicenseRef-Proprietary","author":"Non renseigné"}},
        "dialogues":[{"slug":"maelis-beacon","nodes":[
            {"id":FIRST_NODE,"text":{"fallback":"Question sélectionnée.","key":"first"},"onEnterActions":[],"choices":[
                {"id":SELECTED_CHOICE,"text":{"fallback":"Pourquoi ?","key":"choice"},"targetNodeId":SECOND_NODE,"conditions":[],"actions":[]}
            ]},
            {"id":SECOND_NODE,"text":{"fallback":"Réponse sélectionnée.","key":"second"},"onEnterActions":[],"choices":[
                {"id":"4037f164-d055-cb47-be23-5d2e8eb50316","text":{"fallback":"EXCLUDED TEXT","key":"excluded"},"targetNodeId":"38dfbe34-10f9-0c40-808e-133c9187db93","conditions":[],"actions":[{"kind":"startQuest"}]}
            ]}
        ]}],"audio":[{"path":"EXCLUDED AUDIO"}],"scripts":["EXCLUDED SCRIPT"]
    }})
}

#[test]
fn exact_selected_text_and_edge_keep_the_original_rights_and_exclude_other_actions() {
    let bytes = serde_json::to_vec(&fixture()).unwrap();
    let selected = extract(&bytes, bytes_sha256(&bytes)).unwrap();
    assert_eq!(selected.nodes.len(), 2);
    assert_eq!(selected.nodes[0].text, "Question sélectionnée.");
    assert_eq!(selected.nodes[1].text, "Réponse sélectionnée.");
    assert_eq!(selected.choice.text, "Pourquoi ?");
    assert_eq!(selected.choice.to.to_string(), SECOND_NODE);
    assert_eq!(selected.license, "LicenseRef-Proprietary");
    assert_eq!(selected.author, "Non renseigné");
    assert_eq!(selected.source_commit, SOURCE_COMMIT);
    assert_eq!(selected.source_path, SOURCE_PATH);
    assert!(!selected.exclusions.is_empty());
    let serialized = serde_json::to_string(&selected).unwrap();
    assert!(!serialized.contains("EXCLUDED") && !serialized.contains("startQuest"));
}

#[test]
fn selected_edge_with_conditions_actions_or_another_target_is_not_silently_simplified() {
    for property in ["conditions", "actions", "targetNodeId"] {
        let mut value = fixture();
        value["project"]["dialogues"][0]["nodes"][0]["choices"][0][property] =
            if property == "targetNodeId" {
                json!("38dfbe34-10f9-0c40-808e-133c9187db93")
            } else {
                json!([{"kind":"unsupported"}])
            };
        let bytes = serde_json::to_vec(&value).unwrap();
        assert!(extract(&bytes, bytes_sha256(&bytes)).is_err());
    }
}

#[test]
fn missing_duplicate_nodes_and_modified_source_pins_are_refused() {
    let original = serde_json::to_vec(&fixture()).unwrap();
    assert!(extract(b"{}", bytes_sha256(&original)).is_err());
    let mut duplicate = fixture();
    let repeated = duplicate["project"]["dialogues"][0]["nodes"][0].clone();
    duplicate["project"]["dialogues"][0]["nodes"]
        .as_array_mut()
        .unwrap()
        .push(repeated);
    let bytes = serde_json::to_vec(&duplicate).unwrap();
    assert!(extract(&bytes, bytes_sha256(&bytes)).is_err());
    let mut missing = fixture();
    missing["project"]["dialogues"][0]["nodes"]
        .as_array_mut()
        .unwrap()
        .remove(1);
    let bytes = serde_json::to_vec(&missing).unwrap();
    assert!(extract(&bytes, bytes_sha256(&bytes)).is_err());
}

#[test]
#[ignore = "Read-only real historical source replay via FORGE_SOLAREN_PROJECT_JSON"]
fn selected_real_solaren_dialogue_has_its_exact_pinned_source_and_original_text() {
    let bytes = std::fs::read(std::env::var_os("FORGE_SOLAREN_PROJECT_JSON").unwrap()).unwrap();
    let expected = forge_core::ContentHash::parse(
        "4f3b74205b596aa28028f8794c49296053a93b739fbfb330a63efd63752c7dfd",
    )
    .unwrap();
    let selected = extract(&bytes, expected).unwrap();
    assert_eq!(selected.source_sha256, expected);
    assert_eq!(
        selected.nodes[0].text,
        "Maélis : La pluie a éteint le repère. Peux-tu le rallumer ?"
    );
    assert_eq!(
        selected.nodes[1].text,
        "Maélis : Il guide les voyageurs lorsque la brume couvre Aubeciel."
    );
    assert_eq!(
        selected.choice.text,
        "Pourquoi ce repère est-il important ?"
    );
}
