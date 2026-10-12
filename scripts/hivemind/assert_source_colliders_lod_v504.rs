fn assert_live_colliders(
    app: &mut App,
    entities: &[Entity],
    handles: &[bevy_rapier3d::rapier::prelude::ColliderHandle],
    native: &serde_json::Value,
    basis: Mat4,
) -> usize {
    app.world_mut().run_schedule(FixedUpdate);
    let mut samples = Vec::new();
    for (entity, handle) in entities.iter().zip(handles) {
        assert_eq!(
            app.world().get::<RapierColliderHandle>(*entity).unwrap().0,
            *handle
        );
        assert!(app.world().get::<ColliderDisabled>(*entity).is_none());
        let identity = app
            .world()
            .get::<forge_engine::source_collider_projection::SourceColliderIdentity>(*entity)
            .unwrap();
        let parent = app.world().get::<ChildOf>(*entity).unwrap().parent();
        let matrix = app
            .world()
            .get::<GlobalTransform>(parent)
            .unwrap()
            .to_matrix();
        let reference = native["boxes"]
            .as_array()
            .unwrap()
            .iter()
            .find(|r| r["source_guid"] == identity.guid && r["source_file_id"] == identity.file_id)
            .unwrap();
        for sample in reference["rays"].as_array().unwrap() {
            let origin = matrix.transform_point3(basis.transform_point3(vec3(&sample["origin"])));
            let axis =
                matrix.transform_vector3(basis.transform_vector3(vec3(&sample["direction"])));
            let scale = axis.length();
            let point = matrix.transform_point3(basis.transform_point3(vec3(&sample["point"])));
            let normal = matrix
                .inverse()
                .transpose()
                .transform_vector3(basis.transform_vector3(vec3(&sample["normal"])))
                .normalize();
            samples.push((
                *entity,
                origin,
                axis / scale,
                sample["max_distance"].as_f64().unwrap() as f32 * scale,
                sample["distance"].as_f64().unwrap() as f32 * scale,
                point,
                normal,
            ));
        }
    }
    let mut state = bevy::ecs::system::SystemState::<ReadRapierContext>::new(app.world_mut());
    let read = state.get(app.world()).unwrap();
    let context = read.single().unwrap();
    assert_eq!(context.colliders.colliders.len(), entities.len());
    for (entity, origin, direction, maximum, distance, point, normal) in &samples {
        let (hit, result) = context
            .cast_ray_and_get_normal(*origin, *direction, *maximum, true, QueryFilter::default())
            .unwrap();
        assert_eq!(hit, *entity);
        assert!(near(result.time_of_impact, *distance));
        assert!(vector_near(result.point, *point));
        assert!(vector_near(result.normal, *normal));
    }
    samples.len()
}

fn assert_removed_colliders(app: &mut App, entities: &[Entity]) {
    app.world_mut().run_schedule(FixedUpdate);
    for entity in entities {
        assert!(!app.world().entities().contains(*entity));
    }
    let mut state = bevy::ecs::system::SystemState::<ReadRapierContext>::new(app.world_mut());
    let read = state.get(app.world()).unwrap();
    assert!(read.single().unwrap().colliders.colliders.is_empty());
}
