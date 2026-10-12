// Diagnostic port of the pinned Unity SDK POM. Not a qualified Forge material.
@group(0) @binding(0) var height_texture: texture_2d<f32>;
@group(0) @binding(1) var height_sampler: sampler;

struct SourcePomResult {
    uv: vec2<f32>,
    pixel_depth_offset: f32,
    sampled_height: f32,
};

fn source_height(uv: vec2<f32>, lod: f32) -> f32 {
    // These source nodes omit m_Channel; the SDK initializer is Channel.Red.
    return textureSampleLevel(height_texture, height_sampler, uv, lod).r;
}

fn source_pom_search(uv: vec2<f32>, lod: f32, lod_threshold: f32,
                     num_steps: i32, view_dir_uv: vec3<f32>) -> vec3<f32> {
    let step_size = 1.0 / f32(num_steps);
    let max_offset = view_dir_uv.xy / -view_dir_uv.z;
    let offset_per_step = step_size * max_offset;
    var current_offset = vec2<f32>(0.0);
    var previous_height = source_height(uv + current_offset, lod);
    current_offset += offset_per_step;
    var current_height = source_height(uv + current_offset, lod);
    var ray_height = 1.0 - step_size;
    for (var step_index = 0; step_index < num_steps; step_index += 1) {
        if (current_height > ray_height) { break; }
        previous_height = current_height;
        ray_height -= step_size;
        current_offset += offset_per_step;
        current_height = source_height(uv + current_offset, lod);
    }
    var pt0 = ray_height + step_size;
    var pt1 = ray_height;
    var delta0 = pt0 - previous_height;
    var delta1 = pt1 - current_height;
    var offset = vec2<f32>(0.0);
    for (var i = 0; i < 3; i += 1) {
        let intersection_height = (pt0 * delta1 - pt1 * delta0) / (delta1 - delta0);
        offset = (1.0 - intersection_height) * offset_per_step * f32(num_steps);
        current_height = source_height(uv + offset, lod);
        let delta = intersection_height - current_height;
        if (delta >= -0.01 && delta <= 0.01) { break; }
        if (delta < 0.0) {
            delta1 = delta;
            pt1 = intersection_height;
        } else {
            delta0 = delta;
            pt0 = intersection_height;
        }
    }
    offset *= 1.0 - clamp(lod - lod_threshold, 0.0, 1.0);
    return vec3<f32>(offset, current_height);
}

fn evaluate_source_pom(uv: vec2<f32>, tangent_view: vec3<f32>,
                       world_to_object: mat4x4<f32>, amplitude_cm: f32,
                       steps: f32, tiling: vec2<f32>, offset: vec2<f32>,
                       primitive_size: vec2<f32>, texture_scale: vec2<f32>,
                       texture_offset: vec2<f32>, lod: f32,
                       lod_threshold: f32) -> SourcePomResult {
    // GetDisplacementObjectScale().xzy: inverse-matrix row lengths for X and Z,
    // followed by the SDK's unchanged Y scale of one.
    let scale_x = length(vec3<f32>(world_to_object[0].x, world_to_object[1].x, world_to_object[2].x));
    let scale_z = length(vec3<f32>(world_to_object[0].z, world_to_object[1].z, world_to_object[2].z));
    let view_dir = tangent_view * vec3<f32>(scale_x, scale_z, 1.0);
    let ndotv = view_dir.z;
    var max_height = amplitude_cm * 0.01;
    max_height *= 2.0 / (abs(tiling.x) + abs(tiling.y));
    let uv_space_scale = max_height * tiling / primitive_size;
    let view_dir_uv = normalize(vec3<f32>(view_dir.xy * uv_space_scale, view_dir.z));
    let source_uv = (uv * tiling + offset) * texture_scale + texture_offset;
    let num_steps = i32(max(min(steps, 256.0), 1.0));
    let result = source_pom_search(source_uv, lod, lod_threshold, num_steps, view_dir_uv);
    return SourcePomResult(source_uv + result.xy,
        (max_height - result.z * max_height) / max(ndotv, 0.0001), result.z);
}
