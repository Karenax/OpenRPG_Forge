//! Native adapter for shared terrain normal/AO preparation.
use bevy::prelude::*;
pub use forge_tidewater::tidewater_terrain::normals::{Shared, update_base};
pub fn setup(mut commands: Commands, probe: Res<super::Probe>, mut images: ResMut<Assets<Image>>) {
    let Some(data) = &probe.terrain_textures else {
        commands.insert_resource(Shared::disabled(&mut images));
        return;
    };
    let edited = probe.terrain.as_ref().is_some_and(|p| {
        !p.document().height_deltas.is_empty()
            || p.document().height_sampling.base_elevation_m != 0.0
    });
    commands.insert_resource(
        Shared::stage(&data[0], edited, &mut images)
            .expect("terrain mip sizes validated before application construction"),
    );
}

pub struct NormalPlugin;
impl Plugin for NormalPlugin {
    fn build(&self, app: &mut App) {
        forge_tidewater::tidewater_terrain::normals::register(app)
            .expect("native probe installs renderer before terrain normal graph");
    }
}
