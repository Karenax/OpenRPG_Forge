use super::*;
use bevy::asset::{AssetApp, AssetEvent, AssetPlugin};

#[test]
fn stationary_object_emits_no_material_changes_burning_and_color_updates_still_publish() {
    let mut app = App::new();
    app.add_plugins((MinimalPlugins, AssetPlugin::default()))
        .init_asset::<StandardMaterial>();
    let color = Color::srgb(0.4, 0.4, 0.4);
    let handle = app
        .world_mut()
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: color,
            perceptual_roughness: 0.23,
            ..default()
        });
    app.update();
    app.world_mut()
        .resource_mut::<Messages<AssetEvent<StandardMaterial>>>()
        .clear();
    for _ in 0..100 {
        refresh_object_surface(
            &mut app.world_mut().resource_mut::<Assets<StandardMaterial>>(),
            &handle,
            Some(color),
            false,
        );
        app.update();
    }
    assert_eq!(
        app.world()
            .resource::<Messages<AssetEvent<StandardMaterial>>>()
            .len(),
        0
    );
    let burned = Color::srgb(0.2, 0.1, 0.0);
    refresh_object_surface(
        &mut app.world_mut().resource_mut::<Assets<StandardMaterial>>(),
        &handle,
        Some(burned),
        true,
    );
    app.update();
    assert!(
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<StandardMaterial>>>()
            .drain()
            .any(|event| matches!(event, AssetEvent::Modified { id } if id == handle.id()))
    );
    let material = app
        .world()
        .resource::<Assets<StandardMaterial>>()
        .get(&handle)
        .unwrap();
    assert_eq!(material.base_color, burned);
    assert_eq!(material.emissive, LinearRgba::rgb(4.0, 0.5, 0.05));
    assert_eq!(material.perceptual_roughness.to_bits(), 0.23_f32.to_bits());
    refresh_object_surface(
        &mut app.world_mut().resource_mut::<Assets<StandardMaterial>>(),
        &handle,
        None,
        false,
    );
    app.update();
    let material = app
        .world()
        .resource::<Assets<StandardMaterial>>()
        .get(&handle)
        .unwrap();
    assert_eq!(material.base_color, burned);
    assert_eq!(material.emissive, LinearRgba::BLACK);
    assert!(
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<StandardMaterial>>>()
            .drain()
            .any(|event| matches!(event, AssetEvent::Modified { id } if id == handle.id()))
    );
    refresh_object_surface(
        &mut app.world_mut().resource_mut::<Assets<StandardMaterial>>(),
        &Handle::default(),
        Some(color),
        false,
    );
}
