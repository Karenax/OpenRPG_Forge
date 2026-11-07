//! Schedule shared masked-shadow merging after both Studio producers.
use bevy::prelude::*;
use forge_engine::source_shadows::casters::masked::merge;

pub(super) struct Village;
pub(super) struct Stand;

pub(super) fn install(app: &mut App) {
    app.init_resource::<merge::Contributions>().add_systems(
        PostUpdate,
        emit.after(super::fish_props_scene::update)
            .after(super::fish_stand_scene::update),
    );
}

fn emit(world: &mut World) {
    if let Err(error) = merge::emit(world) {
        super::placements::report(format!("Ombres des poissons non projetées : {error}"));
    }
}
