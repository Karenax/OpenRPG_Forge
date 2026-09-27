use forge_world::{
    embedded_visual_valley_world_sketch,
    spline_edit::{SplineEdit, SplineEditPreview},
};

#[test]
fn spline_edit_is_targeted_roundtrippable_and_does_not_mutate_source() {
    let source = embedded_visual_valley_world_sketch().unwrap();
    let hash = source.content_hash().unwrap();
    let mut spline = source.splines[0].clone();
    // Work with a deliberately unlocked source; production never unlocks implicitly.
    let mut source = source;
    source.locked_feature_ids.retain(|id| id != &spline.id);
    let unlocked_hash = source.content_hash().unwrap();
    spline.width_m += 1.0;
    let edit = SplineEdit::Replace {
        spline: spline.clone(),
    };
    let wire = serde_json::to_string(&edit).unwrap();
    let preview =
        SplineEditPreview::prepare(&source, unlocked_hash, serde_json::from_str(&wire).unwrap())
            .unwrap();
    assert_eq!(preview.before(), Some(&source.splines[0]));
    assert_eq!(preview.after(), Some(&spline));
    assert_eq!(preview.source_hash(), unlocked_hash);
    let mut expected = source.clone();
    expected.splines[0] = spline;
    assert_eq!(preview.proposal(), &expected);
    assert_eq!(source.content_hash().unwrap(), unlocked_hash);
    assert_eq!(
        embedded_visual_valley_world_sketch()
            .unwrap()
            .content_hash()
            .unwrap(),
        hash
    );
    let encoded = preview.proposal().to_pretty_json().unwrap();
    assert_eq!(
        forge_world::WorldSketchDocument::from_json(&encoded).unwrap(),
        expected
    );
}

#[test]
fn spline_edit_insert_remove_and_rejections() {
    let mut source = embedded_visual_valley_world_sketch().unwrap();
    let mut spline = source.splines[0].clone();
    spline.id = "manual_road_test".into();
    let hash = source.content_hash().unwrap();
    let insert = SplineEdit::Insert {
        spline: spline.clone(),
    };
    let preview = SplineEditPreview::prepare(&source, hash, insert.clone()).unwrap();
    assert!(preview.before().is_none());
    let proposed = preview.proposal();
    let new_hash = proposed.content_hash().unwrap();
    assert!(SplineEditPreview::prepare(proposed, hash, insert.clone()).is_err());
    assert!(SplineEditPreview::prepare(proposed, new_hash, insert).is_err());
    let removed = SplineEditPreview::prepare(
        proposed,
        new_hash,
        SplineEdit::Remove {
            id: spline.id.clone(),
        },
    )
    .unwrap();
    assert_eq!(removed.proposal(), &source);
    assert!(removed.after().is_none());
    assert!(
        SplineEditPreview::prepare(
            &source,
            hash,
            SplineEdit::Replace {
                spline: spline.clone()
            }
        )
        .is_err()
    );
    source = proposed.clone();
    source.locked_feature_ids.push(spline.id.clone());
    source.locked_feature_ids.sort();
    assert!(
        SplineEditPreview::prepare(
            &source,
            source.content_hash().unwrap(),
            SplineEdit::Remove {
                id: spline.id.clone()
            }
        )
        .is_err()
    );
    spline.width_m = f32::NAN;
    assert!(
        SplineEditPreview::prepare(proposed, new_hash, SplineEdit::Replace { spline }).is_err()
    );
    assert_eq!(proposed.content_hash().unwrap(), new_hash);
}
