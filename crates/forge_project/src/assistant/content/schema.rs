//! Strict provider/MCP schema for additions using canonical gameplay definitions.
use serde_json::{Value, json};

fn object(properties: Value) -> Value {
    let required: Vec<_> = properties
        .as_object()
        .expect("static schema")
        .keys()
        .cloned()
        .collect();
    let mut schema = json!({"type":"object","additionalProperties":false,"required":required});
    schema["properties"] = properties;
    schema
}
fn array(items: Value) -> Value {
    let mut schema = json!({"type":"array","maxItems":128});
    schema["items"] = items;
    schema
}
fn optional(schema: Value) -> Value {
    let mut result = json!({});
    result["anyOf"] = Value::Array(vec![schema, json!({"type":"null"})]);
    result
}
fn tagged(kind: &str, mut properties: Value) -> Value {
    properties["kind"] = json!({"type":"string","enum":[kind]});
    object(properties)
}

pub(crate) fn identifier() -> Value {
    json!({"type":"string","pattern":"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"})
}

fn id_reference() -> Value {
    json!({"$ref":"#/$defs/forge_content_id"})
}

pub(crate) fn condition() -> Value {
    let id = id_reference();
    let integer = json!({"type":"integer","minimum":0,"maximum":1_000_000});
    let recursive = json!({"$ref":"#/$defs/forge_content_condition"});
    json!({"anyOf":[
        tagged("always",json!({})),
        tagged("quest",json!({"quest":id,"status":{"type":"string","enum":["available","active","completed","failed"]}})),
        tagged("has_item",json!({"item":id,"quantity":integer})),
        tagged("actor_defeated",json!({"actor":id})),
        tagged("object_destroyed",json!({"object":id})),
        tagged("all",json!({"conditions":array(recursive.clone())})),
        tagged("any",json!({"conditions":array(recursive)})),
        tagged("variable",json!({"variable":id,"comparison":{"type":"string","enum":["equal","not_equal","at_least","at_most"]},"value":narrative_value()}))
    ]})
}

fn narrative_value() -> Value {
    json!({"anyOf":[
        object(json!({"type":{"type":"string","enum":["boolean"]},"value":{"type":"boolean"}})),
        object(json!({"type":{"type":"string","enum":["integer"]},"value":{"type":"integer","minimum":-2_147_483_648_i64,"maximum":2_147_483_647}})),
        object(json!({"type":{"type":"string","enum":["text"]},"value":{"type":"string","maxLength":256}}))
    ]})
}

fn effect() -> Value {
    let id = id_reference();
    json!({"anyOf":[
        tagged("accept_quest",json!({"quest":id})),
        tagged("complete_quest",json!({"quest":id})),
        tagged("complete_quest_solution",json!({"quest":id,"solution":id})),
        tagged("fail_quest",json!({"quest":id})),
        tagged("set_door",json!({"object":id,"open":{"type":"boolean"}})),
        tagged("set_variable",json!({"variable":id,"value":narrative_value()}))
    ]})
}

pub(crate) fn operation() -> Value {
    let id = id_reference();
    let text = json!({"type":"string"});
    let integer = json!({"type":"integer","minimum":0,"maximum":1_000_000});
    let boolean = json!({"type":"boolean"});
    let position =
        object(json!({"x":{"type":"number"},"y":{"type":"number"},"z":{"type":"number"}}));
    let condition = json!({"$ref":"#/$defs/forge_content_condition"});
    let actor = object(
        json!({"id":id,"name":text,"role":{"type":"string","enum":["friendly","enemy"]},"position":position,"max_vitality":integer,"protection":integer,"inventory_capacity":integer,"dialogue":optional(id.clone()),"patrol":array(position.clone()),"flees_danger":boolean}),
    );
    let item = object(json!({"id":id,"name":text,"max_stack":integer}));
    let physics = object(
        json!({"body_half_extents_m":position,"mass_kg":{"type":"number"},"pushable":boolean,"throwable":boolean,"break_energy_j":optional(json!({"type":"number"})),"supports":array(id.clone()),"flammable":boolean,"heat_damage_per_tick":integer}),
    );
    let kind = json!({"anyOf":[tagged("pickup",json!({"item":id,"quantity":integer})),tagged("door",json!({"initially_open":boolean})),tagged("prop",json!({}))]});
    let object_definition =
        object(json!({"id":id,"name":text,"position":position,"kind":kind,"physics":physics}));
    let place = object(json!({"id":id,"name":text,"position":position,"links":array(id.clone())}));
    let choice = object(
        json!({"id":id,"text":text,"condition":condition,"effects":array(effect()),"next":optional(id.clone())}),
    );
    let node = object(json!({"id":id,"text":text,"choices":array(choice)}));
    let dialogue = object(json!({"id":id,"start":id,"nodes":array(node)}));
    let quest = object(
        json!({"id":id,"name":text,"giver":id,"required_item":id,"required_quantity":integer,"reward_item":optional(id.clone()),"reward_quantity":integer}),
    );
    let consume = object(json!({"item":id,"quantity":integer}));
    let solution =
        object(json!({"id":id,"name":text,"condition":condition,"consume":array(consume)}));
    let flow = object(
        json!({"quest":id,"prerequisites":array(id.clone()),"solutions":array(solution),"failure_condition":optional(condition),"journal_available":text,"journal_active":text,"journal_completed":text,"journal_failed":text}),
    );
    let variable = object(json!({"id":id,"name":text,"initial":narrative_value()}));
    let narrative = object(
        json!({"version":{"type":"integer","enum":[1]},"variables":array(variable),"quests":array(flow)}),
    );
    object(
        json!({"operation":{"type":"string","enum":["add_content"]},"content":object(json!({
            "actors":array(actor),"items":array(item),"objects":array(object_definition),"places":array(place),"dialogues":array(dialogue),"quests":array(quest),"narrative":optional(narrative)
        }))}),
    )
}

pub(crate) fn places_operation() -> Value {
    let key = json!({"type":"string","minLength":1,"maxLength":64,"pattern":"^[A-Za-z0-9_-]+$"});
    let position =
        object(json!({"x":{"type":"number"},"y":{"type":"number"},"z":{"type":"number"}}));
    let place = object(
        json!({"key":key,"name":{"type":"string"},"position":position,"links":{"type":"array","maxItems":32,"items":key}}),
    );
    object(
        json!({"operation":{"type":"string","enum":["create_places"]},"places":{"type":"array","minItems":1,"maxItems":32,"items":place}}),
    )
}

pub(crate) fn fetch_quest_operation() -> Value {
    let text = json!({"type":"string"});
    let positive = json!({"type":"integer","minimum":1,"maximum":10_000});
    let position =
        object(json!({"x":{"type":"number"},"y":{"type":"number"},"z":{"type":"number"}}));
    let actor = object(json!({"name":text,"position":position,"greeting":text}));
    let collection = object(
        json!({"name":text,"choice_text":text,"item_name":text,"pickup_name":text,"position":position,"quantity":positive}),
    );
    let testimony = object(
        json!({"approach":{"type":"string","enum":["testimony"]},"key":text,"name":text,"choice_text":text,"witness":actor,"learn_text":text}),
    );
    let defeat = object(
        json!({"approach":{"type":"string","enum":["defeat"]},"key":text,"name":text,"choice_text":text,"enemy_name":text,"position":position,"vitality":positive,"protection":{"type":"integer","minimum":0,"maximum":1_000_000}}),
    );
    let door = object(
        json!({"name":text,"position":position,"initially_open":{"type":"boolean"},"open_after_completion":{"type":"boolean"}}),
    );
    let journal = object(json!({"available":text,"active":text,"completed":text,"failed":text}));
    let quest = object(
        json!({"key":text,"name":text,"giver":actor,"accept_text":text,"collection":collection,"alternatives":{"type":"array","maxItems":15,"items":{"anyOf":[testimony,defeat]}},"reward_name":text,"reward_quantity":positive,"journal":journal,"door":optional(door)}),
    );
    object(json!({"operation":{"type":"string","enum":["create_fetch_quest"]},"quest":quest}))
}
