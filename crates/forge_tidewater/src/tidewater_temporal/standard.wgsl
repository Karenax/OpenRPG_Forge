// SPDX-License-Identifier: MIT OR Apache-2.0
// Rigid opaque vertex geometry adapted from Bevy 0.19.1 render/mesh.wgsl.
// The same public mesh functions and model uniforms drive the main draw.
// Registration refuses skin, morph, alpha, transmission and dither extensions.
#import bevy_pbr::{
    forward_io::Vertex,
    mesh_functions,
    view_transformations::position_world_to_clip,
}

struct StandardAuxiliaryVertex {
    @builtin(position) position: vec4<f32>,
    @location(0) world_position: vec3<f32>,
    @location(1) previous_world_position: vec3<f32>,
};

@vertex
fn vertex(input: Vertex) -> StandardAuxiliaryVertex {
    let world_from_local = mesh_functions::get_world_from_local(input.instance_index);
    let previous_world_from_local = mesh_functions::get_previous_world_from_local(input.instance_index);
    let local_position = vec4<f32>(input.position, 1.0);
    let current = mesh_functions::mesh_position_local_to_world(world_from_local, local_position);
    let previous = mesh_functions::mesh_position_local_to_world(previous_world_from_local, local_position);
    var output: StandardAuxiliaryVertex;
    output.position = position_world_to_clip(current.xyz);
    output.world_position = current.xyz;
    output.previous_world_position = previous.xyz;
    return output;
}

@fragment
fn fragment(input: StandardAuxiliaryVertex) -> TidewaterAuxiliaryOutput {
    return tidewater_auxiliary_world(input.world_position, input.previous_world_position,
        1.0, 1.0, vec4<f32>(0.0));
}
