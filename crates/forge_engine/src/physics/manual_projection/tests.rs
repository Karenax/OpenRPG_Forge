use super::*;
use bevy_rapier3d::prelude::{Collider, NoUserData, RapierPhysicsPlugin};
use forge_core::{AssetId, ContentHash, EntityId};

fn sync(app: &mut App) {
    app.update();
    app.world_mut().run_schedule(FixedUpdate);
}

#[test]
fn manual_projection_rejects_preview_stale_backend_and_wrong_source() {
    let mut app = App::new();
    app.add_plugins((
        MinimalPlugins,
        TransformPlugin,
        RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
    ));
    let placement = ManualPlacement {
        physics: None,
        id: EntityId::deterministic("placed"),
        name: "Fixture".into(),
        asset_id: AssetId::deterministic("fixture"),
        asset_hash: ContentHash::from_bytes([3; 32]),
        transform: forge_core::Transform::from_translation(forge_core::Vec3::new(2.0, 3.0, 4.0)),
        locked: false,
    };
    let accepted = vec![placement.clone()];
    assert!(!manual_placements_ready(app.world_mut(), &accepted));
    let root = app
        .world_mut()
        .spawn((
            ManualPlacementProjection(placement.clone()),
            placement_transform(&placement),
        ))
        .id();
    let child = app
        .world_mut()
        .spawn((
            ChildOf(root),
            Transform::from_xyz(0.0, 2.0, 0.0),
            Collider::cuboid(1.0, 2.0, 0.5),
            super::super::ManualPlacementCollision(placement.id),
        ))
        .id();
    app.world_mut()
        .entity_mut(root)
        .insert(ManualPlacementCollisionExpected {
            placement: placement.id,
            primitives: vec![child],
        });
    assert!(!manual_placements_ready(app.world_mut(), &accepted));
    sync(&mut app);
    assert!(manual_placements_ready(app.world_mut(), &accepted));
    let mut wrong = accepted.clone();
    wrong[0].asset_hash = ContentHash::from_bytes([9; 32]);
    assert!(!manual_placements_ready(app.world_mut(), &wrong));
    app.world_mut()
        .get_mut::<Transform>(root)
        .unwrap()
        .translation
        .x += 5.0;
    assert!(
        !manual_placements_ready(app.world_mut(), &accepted),
        "unapplied gizmo root"
    );
    app.world_mut()
        .entity_mut(root)
        .insert(placement_transform(&placement));
    sync(&mut app);
    assert!(manual_placements_ready(app.world_mut(), &accepted));
    app.world_mut()
        .get_mut::<Transform>(child)
        .unwrap()
        .translation
        .y += 3.0;
    assert!(
        !manual_placements_ready(app.world_mut(), &accepted),
        "stale propagated child"
    );
    // Propagate without Rapier: the backend must not be considered synchronized.
    app.world_mut().run_schedule(PostUpdate);
    assert!(
        !manual_placements_ready(app.world_mut(), &accepted),
        "stale backend pose"
    );
    sync(&mut app);
    assert!(manual_placements_ready(app.world_mut(), &accepted));
    assert!(
        !manual_placements_ready(app.world_mut(), &[]),
        "old objects in an empty target"
    );
    app.world_mut().despawn(child);
    assert!(!manual_placements_ready(app.world_mut(), &accepted));
}
