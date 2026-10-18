//! Relay imported material lighting into the common solar contract.
use super::VillageMaterial;
use bevy::prelude::*;
use forge_engine::lens_flare_sun::SceneSun;
pub struct ImportedSunRelayPlugin;
impl Plugin for ImportedSunRelayPlugin {
    fn build(&self, app: &mut App) {
        app.add_systems(PostUpdate, publish.before(crate::lens_flare::update));
    }
}
fn publish(world: &mut World) {
    let source = world
        .get_resource::<Assets<VillageMaterial>>()
        .and_then(|materials| {
            materials.iter().next().map(|(_, value)| {
                (
                    value.scene_lighting.sun_direction.truncate(),
                    value.scene_lighting.sun_color.truncate(),
                )
            })
        });
    match source {
        Some((direction, color)) => match SceneSun::new(direction, color) {
            Ok(sun) => {
                world.insert_resource(sun);
            }
            Err(reason) => {
                world.remove_resource::<SceneSun>();
                error!("Imported solar lighting refused: {reason}");
            }
        },
        None => {
            world.remove_resource::<SceneSun>();
        }
    }
}
