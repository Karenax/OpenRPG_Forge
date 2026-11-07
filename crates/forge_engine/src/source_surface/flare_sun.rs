//! Relay imported material lighting into the common solar contract.
use super::SourceSurfaceMaterial;
use crate::lens_flare_sun::SceneSun;
use bevy::{ecs::change_detection::Tick, prelude::*};
pub struct ImportedSunRelayPlugin;
#[derive(Resource, Default)]
struct RelayOwnership(Option<Tick>);
/// Scene consumers order after this publication without a map-specific system dependency.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct SurfaceSunPublication;
impl Plugin for ImportedSunRelayPlugin {
    fn build(&self, app: &mut App) {
        app.init_resource::<RelayOwnership>()
            .add_systems(PostUpdate, publish.in_set(SurfaceSunPublication));
    }
}
fn publish(world: &mut World) {
    let existing = world
        .get_resource_ref::<SceneSun>()
        .map(|sun| sun.last_changed());
    let owned = existing.is_some() && existing == world.resource::<RelayOwnership>().0;
    if existing.is_some() && !owned {
        // Another scene supplied this light. Do not claim or retire its resource.
        world.resource_mut::<RelayOwnership>().0 = None;
        return;
    }
    let source = world
        .get_resource::<Assets<SourceSurfaceMaterial>>()
        .and_then(|materials| {
            materials.iter().next().map(|(_, value)| {
                (
                    value.scene_lighting.sun_direction.truncate(),
                    value.scene_lighting.sun_color.truncate(),
                )
            })
        });
    let Some((direction, color)) = source else {
        if owned {
            world.remove_resource::<SceneSun>();
        }
        world.resource_mut::<RelayOwnership>().0 = None;
        return;
    };
    match SceneSun::new(direction, color) {
        Ok(sun) => {
            world.insert_resource(sun);
            let tick = world
                .get_resource_ref::<SceneSun>()
                .expect("inserted sun")
                .last_changed();
            world.resource_mut::<RelayOwnership>().0 = Some(tick);
        }
        Err(reason) => {
            if owned {
                world.remove_resource::<SceneSun>();
            }
            world.resource_mut::<RelayOwnership>().0 = None;
            error!("Imported solar lighting refused: {reason}");
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_surface::tests::lighting;

    #[derive(Resource, Default)]
    struct Observed(Option<(Vec3, Vec3)>);

    fn consume(mut observed: ResMut<Observed>, sun: Option<Res<SceneSun>>) {
        observed.0 = sun.map(|sun| (sun.direction(), sun.radiance()));
    }

    #[test]
    fn scene_consumer_observes_current_sun_then_refusal_and_retirement() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, ImportedSunRelayPlugin))
            .init_resource::<Assets<SourceSurfaceMaterial>>()
            .init_resource::<Observed>()
            .add_systems(PostUpdate, consume.after(SurfaceSunPublication));
        let handle = app
            .world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .add(lighting());
        app.update();
        assert_eq!(
            app.world().resource::<Observed>().0,
            Some((Vec3::Y, Vec3::new(4.0, 3.0, 2.0)))
        );
        app.world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .get_mut(&handle)
            .unwrap()
            .scene_lighting
            .sun_direction
            .x = f32::NAN;
        app.update();
        assert!(app.world().resource::<Observed>().0.is_none());
        app.world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .get_mut(&handle)
            .unwrap()
            .scene_lighting
            .sun_direction
            .x = 0.0;
        app.update();
        assert!(app.world().resource::<Observed>().0.is_some());
        app.world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .remove(handle.id());
        app.update();
        assert!(app.world().resource::<Observed>().0.is_none());
    }

    #[test]
    fn relay_preserves_an_explicit_sun_from_another_scene() {
        let external = SceneSun::new(Vec3::X, Vec3::new(2.0, 3.0, 4.0)).unwrap();
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, ImportedSunRelayPlugin))
            .init_resource::<Assets<SourceSurfaceMaterial>>()
            .insert_resource(external);
        app.update();
        assert_eq!(app.world().resource::<SceneSun>().direction(), Vec3::X);
        let handle = app
            .world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .add(lighting());
        app.update();
        assert_eq!(app.world().resource::<SceneSun>().direction(), Vec3::X);
        app.world_mut().remove_resource::<SceneSun>();
        app.update();
        assert_eq!(app.world().resource::<SceneSun>().direction(), Vec3::Y);
        let replacement = *app.world().resource::<SceneSun>();
        app.world_mut().insert_resource(replacement);
        app.world_mut()
            .resource_mut::<Assets<SourceSurfaceMaterial>>()
            .remove(handle.id());
        app.update();
        assert_eq!(app.world().resource::<SceneSun>().direction(), Vec3::Y);
    }
}
