//! Tidewater-owned caster inventory; collection and depth draws belong to the engine.
use bevy::prelude::*;
use forge_engine::source_shadows::casters::{OpaqueDraw, scene};

pub fn opaque_parts(world: &World) -> Result<Vec<(Entity, Mat4)>, String> {
    scene::source_surface_parts(world, &additional_parts(world))
}

pub fn buildings(world: &World) -> Result<Vec<OpaqueDraw>, String> {
    scene::source_surface_draws(world, &additional_parts(world))
}

fn additional_parts(world: &World) -> Vec<Entity> {
    let mut parts = Vec::new();
    if let Some(boardwalk) = world.get_resource::<crate::tidewater_boardwalk::ResidentBoardwalk>() {
        parts.extend_from_slice(boardwalk.render_parts());
    }
    if let Some(pier) = world.get_resource::<crate::tidewater_pier::ResidentPier>() {
        parts.extend_from_slice(pier.render_parts());
    }
    if let Some(plaza) = world.get_resource::<crate::tidewater_plaza::ResidentPlaza>() {
        parts.extend_from_slice(plaza.render_parts());
    }
    if let Some(props) =
        world.get_resource::<crate::tidewater_village_props::ResidentVillageProps>()
    {
        parts.extend_from_slice(props.render_parts());
    }
    if let Some(props) = world.get_resource::<crate::tidewater_beach_props::ResidentBeachProps>() {
        parts.extend_from_slice(props.render_parts());
    }
    parts
}
