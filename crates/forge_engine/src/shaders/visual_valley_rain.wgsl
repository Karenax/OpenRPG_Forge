#import bevy_pbr::{
    mesh_functions,
    forward_io::{Vertex, VertexOutput},
    view_transformations::position_world_to_clip,
}

// Forge-original procedural rain animation. UV0 stores the normalized X/Z centre of a streak;
// UV1.x stores its normalized Y centre. Every vertex of a streak carries the same centre, so the
// wrapped motion cannot tear the quad when its batch crosses a volume boundary.
struct VisualValleyRainExtension {
    time_fall: vec4<f32>,
    volume_opacity: vec4<f32>,
    wind_velocity: vec4<f32>,
}

@group(#{MATERIAL_BIND_GROUP}) @binding(100)
var<uniform> rain: VisualValleyRainExtension;

fn wrapped_center(normalized_center: vec3<f32>, time_seconds: f32) -> vec3<f32> {
    let volume = max(rain.volume_opacity.xyz, vec3(0.000001));
    let velocity = vec3(
        rain.wind_velocity.x,
        rain.wind_velocity.y - max(rain.time_fall.z, 0.0),
        rain.wind_velocity.z,
    );
    let translated = normalized_center + velocity * time_seconds / volume;
    return fract(translated) * volume - volume * 0.5;
}

fn animated_local_position(vertex: Vertex, time_seconds: f32) -> vec3<f32> {
#ifdef VERTEX_UVS_A
    let center_xz = vertex.uv;
#else
    let center_xz = vec2(0.5);
#endif
#ifdef VERTEX_UVS_B
    let center_y = vertex.uv_b.x;
#else
    let center_y = 0.5;
#endif
    let normalized_center = vec3(center_xz.x, center_y, center_xz.y);
    let volume = max(rain.volume_opacity.xyz, vec3(0.000001));
    let original_center = normalized_center * volume - volume * 0.5;
    let local_offset = vertex.position - original_center;
    return wrapped_center(normalized_center, time_seconds) + local_offset;
}

@vertex
fn vertex(vertex: Vertex) -> VertexOutput {
    var out: VertexOutput;
    let world_from_local = mesh_functions::get_world_from_local(vertex.instance_index);
    let local_position = animated_local_position(vertex, max(rain.time_fall.x, 0.0));
    out.world_position = mesh_functions::mesh_position_local_to_world(
        world_from_local,
        vec4(local_position, 1.0),
    );
    out.position = position_world_to_clip(out.world_position.xyz);

#ifdef UNCLIPPED_DEPTH_ORTHO_EMULATION
    out.unclipped_depth = out.position.z;
    out.position.z = min(out.position.z, 1.0);
#endif

#ifdef VERTEX_NORMALS
    out.world_normal = mesh_functions::mesh_normal_local_to_world(
        vertex.normal,
        vertex.instance_index,
    );
#endif
#ifdef VERTEX_UVS_A
    out.uv = vertex.uv;
#endif
#ifdef VERTEX_UVS_B
    out.uv_b = vertex.uv_b;
#endif
#ifdef VERTEX_TANGENTS
    out.world_tangent = mesh_functions::mesh_tangent_local_to_world(
        world_from_local,
        vertex.tangent,
        vertex.instance_index,
    );
#endif
#ifdef VERTEX_COLORS
    out.color = vertex.color;
#endif

#ifdef MOTION_VECTOR_PREPASS
    let previous_world_from_local = mesh_functions::get_previous_world_from_local(vertex.instance_index);
    let previous_local_position = animated_local_position(vertex, max(rain.time_fall.y, 0.0));
    out.previous_world_position = mesh_functions::mesh_position_local_to_world(
        previous_world_from_local,
        vec4(previous_local_position, 1.0),
    );
#endif

#ifdef VERTEX_OUTPUT_INSTANCE_INDEX
    out.instance_index = vertex.instance_index;
#endif
    return out;
}
