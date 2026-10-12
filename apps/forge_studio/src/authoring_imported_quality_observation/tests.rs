use super::*;

#[test]
fn unavailable_is_distinct_from_observed_zero_and_false() {
    let missing = Observation::unavailable("private", true);
    let zero = Observation::value(
        ParameterValue::Number(0.0),
        Origin::CpuMaterialUniform,
        true,
    );
    let disabled = Observation::value(
        ParameterValue::Boolean(false),
        Origin::MainWorldComponent,
        true,
    );
    assert!(missing.value.is_none());
    assert!(zero.value.is_some() && zero.unavailable.is_none());
    assert!(disabled.value.is_some() && disabled.unavailable.is_none());
}

#[test]
fn non_finite_scalars_vectors_and_rows_are_explicitly_unavailable() {
    for value in [
        ParameterValue::Number(f64::NAN),
        ParameterValue::Numbers(vec![1.0, f64::INFINITY]),
        ParameterValue::NumberRows(vec![vec![f64::NEG_INFINITY]]),
    ] {
        let observed = Observation::value(value, Origin::CpuMaterialUniform, true);
        assert!(observed.value.is_none());
        assert_eq!(observed.unavailable, Some("non_finite_parameter"));
    }
}

#[test]
fn fingerprint_excludes_pose_frames_and_producer_counters() {
    let mut profile = CurrentProfileInventory::new(None, Some(1));
    let before = profile.fingerprint().expect("pure profile hash");
    profile.frame = Some(300);
    for (key, value) in [
        ("camera/world_pose", ParameterValue::Numbers(vec![3.0, 5.0])),
        ("water/step", ParameterValue::Unsigned(300)),
        ("environment/ready", ParameterValue::Boolean(true)),
    ] {
        profile.observations.insert(
            key.into(),
            Observation::value(value, Origin::MainWorldResource, false),
        );
    }
    assert_eq!(before, profile.fingerprint().expect("pure profile hash"));
}

#[test]
fn settings_resolution_and_replaced_generation_change_fingerprint() {
    let profile = CurrentProfileInventory::new(None, None);
    let baseline = profile.fingerprint().expect("pure profile hash");
    for (key, value) in [
        ("post/bloom", ParameterValue::Number(0.05)),
        (
            "camera/physical_viewport_px",
            ParameterValue::Dimensions(vec![1920, 1080]),
        ),
        (
            "terrain/material_identity",
            ParameterValue::Text("replacement".into()),
        ),
    ] {
        let mut changed = profile.clone();
        changed.observations.insert(
            key.into(),
            Observation::value(value, Origin::MainWorldComponent, true),
        );
        assert_ne!(baseline, changed.fingerprint().expect("pure profile hash"));
    }
}

#[test]
fn empty_inventory_has_no_gpu_verdict_and_keeps_required_unknowns() {
    let profile = CurrentProfileInventory::new(None, None);
    let value = serde_json::to_value(&profile).expect("pure inventory json");
    assert!(value["gpu_completion"].is_null() && value["presentation"].is_null());
    assert!(value["observations"]["rain/drop_budget"]["value"].is_null());
    assert_eq!(
        value["observations"]["terrain/cdlod_config"]["unavailable"],
        "private_adapter_configuration"
    );
    assert!(value.get("tier").is_none());
}

#[test]
fn every_observation_origin_and_integer_shape_serializes_without_a_renderer() {
    let observations: Vec<_> = [
        Origin::MainWorldComponent,
        Origin::MainWorldResource,
        Origin::CpuMaterialUniform,
        Origin::CpuImageDescriptor,
        Origin::AsyncGpuReadback,
        Origin::Unavailable,
    ]
    .into_iter()
    .map(|origin| Observation::value(ParameterValue::Count(0), origin, false))
    .collect();
    assert_eq!(
        serde_json::to_value(observations)
            .expect("pure origins")
            .as_array()
            .map(Vec::len),
        Some(6)
    );
    let raw = Observation::value(
        ParameterValue::RawWordRows(vec![vec![1.0_f32.to_bits()]]),
        Origin::MainWorldResource,
        true,
    );
    assert_eq!(
        serde_json::to_value(raw).expect("pure raw words")["value"]["value"][0][0],
        1.0_f32.to_bits()
    );
}
