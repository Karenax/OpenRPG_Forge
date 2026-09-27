// Forge-original screen-space optical geometry. Pure functions: the caller owns
// depth-texture access, render-phase ordering and the explicit missing-depth fallback.
// Screen coordinates are absolute render-target pixels, not viewport-local UVs.

fn water_optical_path_m(
    pixel: vec2<f32>,
    viewport: vec4<f32>,
    world_from_clip: mat4x4<f32>,
    water_position: vec3<f32>,
    water_reverse_depth: f32,
    scene_reverse_depth: f32,
    maximum_path_m: f32,
) -> f32 {
    let maximum = max(maximum_path_m, 0.0);
    // Invalid/empty viewport must not invent a shallow transparent shore.
    if any(viewport.zw <= vec2(0.0)) {
        return maximum;
    }
    let uv = (pixel - viewport.xy) / viewport.zw;
    if any(uv < vec2(0.0)) || any(uv >= vec2(1.0)) {
        return maximum;
    }
    // Reversed-Z: greater depth is nearer. Foreground is not underwater.
    if scene_reverse_depth >= water_reverse_depth {
        return 0.0;
    }
    // Clear depth/sky cannot be unprojected at infinity.
    if scene_reverse_depth <= 0.0 {
        return maximum;
    }
    let ndc = vec3(uv * vec2(2.0, -2.0) + vec2(-1.0, 1.0), scene_reverse_depth);
    let homogeneous = world_from_clip * vec4(ndc, 1.0);
    if abs(homogeneous.w) < 0.000001 {
        return maximum;
    }
    let scene_position = homogeneous.xyz / homogeneous.w;
    // This is the unrefracted view-ray path, not vertical bed depth. Ray bending
    // requires a separately validated refracted lookup; do not label this a ray march.
    return min(length(scene_position - water_position), maximum);
}

fn water_beer_transmittance(
    path_m: f32,
    transmittance_at_reference: vec3<f32>,
    reference_distance_m: f32,
) -> vec3<f32> {
    let reference = max(reference_distance_m, 0.000001);
    let transmission = clamp(transmittance_at_reference, vec3(0.000001), vec3(1.0));
    return exp(log(transmission) * (max(path_m, 0.0) / reference));
}

// Homogeneous RGB approximation. Inputs are validated nonnegative inverse metres,
// not display colours. The zero-extinction limit is exactly transparent.
fn water_medium_transmittance(path_m: f32, absorption: vec3<f32>, scattering: vec3<f32>) -> vec3<f32> {
    return exp(-(absorption + scattering) * max(path_m, 0.0));
}

// Integrated single-scattering weight for constant incident illumination.
// A zero-extinction channel contributes no scattering (and must never divide by zero).
fn water_medium_scattering_weight(transmission: vec3<f32>, absorption: vec3<f32>, scattering: vec3<f32>) -> vec3<f32> {
    let extinction = absorption + scattering;
    let denominator = select(vec3(1.0), extinction, extinction > vec3(0.0));
    return (scattering / denominator) * (vec3(1.0) - transmission);
}

// Screen-space contact coverage, not an authored bank width or a terrain edit.
// A disabled interval keeps the full water result, including at zero depth.
fn water_contact_coverage(path_m: f32, transition_path_m: f32) -> f32 {
    if transition_path_m <= 0.0 {
        return 1.0;
    }
    return smoothstep(0.0, transition_path_m, max(path_m, 0.0));
}
