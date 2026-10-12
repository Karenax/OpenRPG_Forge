use bevy_rapier3d::prelude::*;
use std::collections::BTreeMap;

fn exercise_projection(
    glb: &forge_assets::source_graph_glb::SourceGraphGlb<'_>,
    native: &serde_json::Value,
    basis: Mat4,
) {
    let doc = glb.document();
    let boxes = source_colliders::prepare(doc, basis).unwrap();
    let plan = source_graph_scene::read_source_graph_scene(glb, None).unwrap();
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
    ));
    let root = app
        .world_mut()
        .spawn((
            Transform::IDENTITY,
            GlobalTransform::IDENTITY,
            Visibility::Hidden,
        ))
        .id();
    let nodes =
        source_graph_scene::spawn_source_graph_hierarchy(&plan, app.world_mut(), root).unwrap();
    let mut policies = BTreeMap::new();
    for shape in &boxes {
        let reference = native["boxes"]
            .as_array()
            .unwrap()
            .iter()
            .find(|r| r["source_guid"] == shape.guid && r["source_file_id"] == shape.file_id)
            .unwrap();
        policies.insert((shape.guid.clone(),shape.file_id.clone()), source_collider_projection::ContactPolicy {
            friction: Friction::coefficient(reference["implicit_material"]["dynamic_friction"].as_f64().unwrap() as f32),
            restitution: Restitution::coefficient(0.), contact_skin: ContactSkin(0.),
            collision_groups: CollisionGroups::new(Group::ALL, Group::ALL),
            solver_groups: SolverGroups::new(Group::ALL, Group::ALL),
            evidence: "v471-native-material-observation; query-bench-zero-skin; no-contact-equivalence-claim".into(),
        });
    }
    if !boxes.is_empty() {
        let count = app.world().entities().len();
        assert!(
            source_collider_projection::attach(
                app.world_mut(),
                root,
                doc,
                basis,
                &nodes,
                &BTreeMap::new()
            )
            .is_err()
        );
        assert_eq!(app.world().entities().len(), count);
        assert!(
            source_collider_projection::attach(
                app.world_mut(),
                root,
                doc,
                basis,
                &BTreeMap::new(),
                &policies
            )
            .is_err()
        );
        assert_eq!(app.world().entities().len(), count);
    }
    let projection =
        source_collider_projection::attach(app.world_mut(), root, doc, basis, &nodes, &policies)
            .unwrap();
    assert_eq!(projection.entities.len(), boxes.len());
    let count = app.world().entities().len();
    assert!(
        source_collider_projection::attach(app.world_mut(), root, doc, basis, &nodes, &policies)
            .is_err()
    );
    assert_eq!(app.world().entities().len(), count);
    for pose in [
        Transform::IDENTITY,
        Transform::from_xyz(9., 4., -3.).with_rotation(Quat::from_rotation_y(0.7)),
        Transform::from_xyz(-3., 2., 7.)
            .with_rotation(Quat::from_rotation_y(-0.4))
            .with_scale(Vec3::new(2., 1.5, 0.5)),
    ] {
        *app.world_mut().get_mut::<Transform>(root).unwrap() = pose;
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        for (shape, entity) in boxes.iter().zip(&projection.entities) {
            assert!(app.world().get::<RapierColliderHandle>(*entity).is_some());
            assert_eq!(
                app.world().get::<ChildOf>(*entity).unwrap().parent(),
                nodes[&shape.node]
            );
            let original = app
                .world()
                .get::<source_collider_projection::SourceColliderIdentity>(*entity)
                .unwrap();
            assert_eq!(original.original, shape.original);
            assert_eq!(original.guid, shape.guid);
            assert_eq!(original.file_id, shape.file_id);
            assert!(!original.policy_evidence.is_empty());
            let matrix = app
                .world()
                .get::<GlobalTransform>(nodes[&shape.node])
                .unwrap()
                .to_matrix();
            let reference = native["boxes"]
                .as_array()
                .unwrap()
                .iter()
                .find(|r| r["source_guid"] == shape.guid && r["source_file_id"] == shape.file_id)
                .unwrap();
            let mut state =
                bevy::ecs::system::SystemState::<ReadRapierContext>::new(app.world_mut());
            let read = state.get(app.world());
            let context = read.single().unwrap();
            for sample in reference["points"].as_array().unwrap() {
                let query =
                    matrix.transform_point3(basis.transform_point3(vec3(&sample["world_query"])));
                let expected =
                    matrix.transform_point3(basis.transform_point3(vec3(&sample["closest_world"])));
                let (hit, point) = context
                    .project_point(query, true, QueryFilter::default())
                    .unwrap();
                assert_eq!(hit, *entity);
                assert!(
                    vector_near(point.point, expected),
                    "context closest {:?} != {:?}",
                    point.point,
                    expected
                );
            }
            for sample in reference["rays"].as_array().unwrap() {
                let origin =
                    matrix.transform_point3(basis.transform_point3(vec3(&sample["origin"])));
                let axis =
                    matrix.transform_vector3(basis.transform_vector3(vec3(&sample["direction"])));
                let length = axis.length();
                let direction = axis / length;
                let (hit, ray) = context
                    .cast_ray_and_get_normal(
                        origin,
                        direction,
                        sample["max_distance"].as_f64().unwrap() as f32 * length,
                        true,
                        QueryFilter::default(),
                    )
                    .unwrap();
                assert_eq!(hit, *entity);
                let point = matrix.transform_point3(basis.transform_point3(vec3(&sample["point"])));
                let normal = matrix
                    .inverse()
                    .transpose()
                    .transform_vector3(basis.transform_vector3(vec3(&sample["normal"])))
                    .normalize();
                assert!(
                    vector_near(ray.point, point),
                    "context ray point {:?} != {:?}",
                    ray.point,
                    point
                );
                assert!(vector_near(ray.normal, normal));
                assert!(near(
                    ray.time_of_impact,
                    sample["distance"].as_f64().unwrap() as f32 * length
                ));
            }
        }
    }
    let removed = projection.entities.clone();
    projection.clear(app.world_mut());
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
    assert!(app.world().entities().contains(root));
    for entity in removed {
        assert!(!app.world().entities().contains(entity));
    }
    let mut state = bevy::ecs::system::SystemState::<ReadRapierContext>::new(app.world_mut());
    let read = state.get(app.world());
    let context = read.single().unwrap();
    assert!(context.colliders.colliders.is_empty());
}
