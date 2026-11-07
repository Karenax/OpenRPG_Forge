use super::*;

fn inventory() -> SourceLocalLights {
    let original = descriptor(0.915_999_999_999_999_9);
    let mut document = serde_json::to_value(original.document()).unwrap();
    let mut other_frame = Mat4::IDENTITY.to_cols_array().map(f64::from);
    other_frame[12] = 20.0;
    document["frames"].as_array_mut().unwrap().extend([
        json!({"id":"carrier","source_object":"carrier","parent":"root", "world_from_local":Mat4::IDENTITY.to_cols_array().map(f64::from)}),
        json!({"id":"other","source_object":"other","parent":"root", "world_from_local":other_frame}),
    ]);
    document["frames"][1]["parent"] = json!("carrier");
    let base = document["lights"][0].clone();
    document["lights"] = json!(
        (0..5)
            .map(|index| {
                let mut light = base.clone();
                light["id"] = json!(id(index));
                light["registration_order"] = json!(index);
                let (frame, x) = match index {
                    0 => ("root", 1.0),
                    2 => ("other", 21.0),
                    _ => ("owner", 13.0),
                };
                light["owner_frame"] = json!(frame);
                light["acquired_position_world_m"] = json!([x, 2.0, 3.0]);
                light
            })
            .collect::<Vec<_>>()
    );
    document["sets"]["one"]["count"] = json!(5);
    let bytes = serde_json::to_vec(&document).unwrap();
    SourceLocalLights::load(&bytes, bytes_sha256(&bytes)).unwrap()
}

fn id(index: usize) -> ContentHash {
    bytes_sha256(format!("scope-light-{index}").as_bytes())
}

fn select(
    document: &SourceLocalLights,
    base: u64,
    ids: &[ContentHash],
) -> Result<PreparedSourceLights, String> {
    PreparedSourceLights::prepare_selected(
        document,
        AssetId::deterministic("scoped-asset"),
        bytes_sha256(b"package"),
        base,
        Some(ids),
    )
}

#[test]
fn source_local_lights_selection_keeps_source_order_bits_and_only_used_frames() {
    let document = inventory();
    let selected = select(&document, 100, &[id(3), id(1)]).unwrap();
    assert_eq!(selected.registration_orders(), &[101, 103]);
    assert_eq!(
        selected.owner_frame_ids().collect::<Vec<_>>(),
        vec!["owner"]
    );
    assert_eq!(selected.frame_parents["owner"], None);
    assert_eq!(selected.lights[0].id, id(1));
    assert_eq!(selected.lights[1].id, id(3));
    for light in &selected.lights {
        assert_eq!(
            light.parameters.phase.to_bits(),
            document.document().lights[1].phase.to_bits()
        );
        assert_eq!(light.parameters.position, DVec3::new(1.0, 2.0, 3.0));
        assert_eq!(light.parameters.scale.to_bits(), 0.0_f64.to_bits());
    }
    let all = PreparedSourceLights::prepare(
        &document,
        AssetId::deterministic("scoped-asset"),
        bytes_sha256(b"package"),
        0,
    )
    .unwrap();
    assert_eq!(
        all.owner_frame_ids().count(),
        4,
        "full-package API retains its original binding contract"
    );
    let rooted = select(&document, 0, &[id(0), id(1)]).unwrap();
    assert_eq!(
        rooted.frame_parents["owner"],
        Some("root".into()),
        "unused intermediate retains selected ancestor relation"
    );
}

#[test]
fn source_local_lights_selection_refuses_empty_unknown_duplicate_and_overflow() {
    let document = inventory();
    for ids in [vec![], vec![id(1), id(1)], vec![id(9)], vec![id(1); 6]] {
        assert!(select(&document, 0, &ids).is_err());
    }
    assert!(select(&document, u64::MAX, &[id(1)]).is_err());
    assert_eq!(
        select(&document, u64::MAX, &[id(0)])
            .unwrap()
            .registration_orders(),
        &[u64::MAX]
    );
}

#[test]
#[expect(
    clippy::float_cmp,
    reason = "Exact source flags and preserved payload values must not admit a tolerance"
)]
fn source_local_lights_selection_instances_move_replace_and_retire_independently() {
    let document = inventory();
    let (mut app, camera, owner, _) = scene();
    let copy = app
        .world_mut()
        .spawn(Transform::from_xyz(40.0, 0.0, 0.0))
        .id();
    app.update();
    let first = select(&document, 0, &[id(1), id(3)]).unwrap();
    let second = select(&document, 100, &[id(1), id(3)]).unwrap();
    let bindings = BTreeMap::from([("owner".into(), owner)]);
    let copy_bindings = BTreeMap::from([("owner".into(), copy)]);
    let mut resident = first
        .stage(app.world_mut(), EntityId::deterministic("first"), &bindings)
        .unwrap();
    let mut duplicate = second
        .stage(
            app.world_mut(),
            EntityId::deterministic("copy"),
            &copy_bindings,
        )
        .unwrap();
    app.update();
    assert!(resident.is_synchronized(app.world()));
    assert!(duplicate.is_synchronized(app.world()));
    let (_, gathered) = scene::harvest(app.world_mut(), camera, &[1, 3, 101, 103]).unwrap();
    assert_eq!(gathered[0].position.x, 13.0);
    assert_eq!(gathered[2].position.x, 41.0);
    app.world_mut()
        .get_mut::<Transform>(owner)
        .unwrap()
        .translation
        .x = 9.0;
    assert!(!resident.is_synchronized(app.world()));
    assert!(duplicate.is_synchronized(app.world()));
    app.update();
    let (_, gathered) = scene::harvest(app.world_mut(), camera, &[1, 3, 101, 103]).unwrap();
    assert_eq!(gathered[0].position.x, 20.0);
    assert_eq!(gathered[2].position.x, 41.0);
    let replacement = select(&document, 0, &[id(4)]).unwrap();
    resident
        .replace(app.world_mut(), &replacement, &bindings)
        .unwrap();
    app.update();
    assert!(scene::harvest(app.world_mut(), camera, &[4, 101, 103]).is_ok());
    resident.retire(app.world_mut());
    assert!(duplicate.is_synchronized(app.world()));
    assert!(scene::harvest(app.world_mut(), camera, &[101, 103]).is_ok());
    duplicate.retire(app.world_mut());
    assert!(app.world().get_entity(owner).is_ok());
    assert!(app.world().get_entity(copy).is_ok());
}

#[test]
fn source_local_lights_selection_still_refuses_wrong_mapped_ancestor_and_extra_binding() {
    let document = inventory();
    let (mut app, _, owner, bindings) = scene();
    let rooted = select(&document, 0, &[id(0), id(1)]).unwrap();
    let foreign = app.world_mut().spawn(Transform::IDENTITY).id();
    app.update();
    let count = app.world().entities().len();
    assert!(
        rooted
            .stage(
                app.world_mut(),
                EntityId::deterministic("first"),
                &BTreeMap::from([("root".into(), foreign), ("owner".into(), owner)])
            )
            .is_err()
    );
    let sparse = select(&document, 0, &[id(1)]).unwrap();
    assert!(
        sparse
            .stage(app.world_mut(), EntityId::deterministic("first"), &bindings)
            .is_err()
    );
    assert_eq!(app.world().entities().len(), count);
}

#[test]
fn source_local_lights_selection_invalidates_reparenting_and_refuses_retired_reactivation() {
    let document = inventory();
    let prepared = select(&document, 0, &[id(0), id(1)]).unwrap();
    let (mut app, _, owner, bindings) = scene();
    let mut resident = prepared
        .stage(app.world_mut(), EntityId::deterministic("first"), &bindings)
        .unwrap();
    app.update();
    assert!(resident.is_synchronized(app.world()));
    let foreign = app.world_mut().spawn(Transform::IDENTITY).id();
    app.world_mut().entity_mut(owner).insert(ChildOf(foreign));
    app.update();
    assert!(!resident.is_synchronized(app.world()));
    app.world_mut()
        .entity_mut(owner)
        .insert(ChildOf(bindings["root"]));
    app.update();
    assert!(resident.is_synchronized(app.world()));
    resident.retire(app.world_mut());
    let count = app.world().entities().len();
    assert!(
        resident
            .replace(app.world_mut(), &prepared, &bindings)
            .is_err()
    );
    assert_eq!(app.world().entities().len(), count);
    assert!(!resident.is_synchronized(app.world()));
}
