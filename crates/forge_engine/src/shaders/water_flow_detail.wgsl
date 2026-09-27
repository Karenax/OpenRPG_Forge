// Forge-original two-scale normal detail advected by the local river direction.
fn river_noise_corner(cell: vec2<i32>) -> vec2<f32> {
    let bits = bitcast<vec2<u32>>(cell);
    var hash = bits.x * 1597334677u ^ bits.y * 3812015801u;
    hash = (hash ^ (hash >> 16u)) * 2246822519u;
    hash = hash ^ (hash >> 13u);
    // Eight unit directions, without per-corner trigonometry or a texture lookup.
    let x = select(-1.0, 1.0, (hash & 1u) != 0u);
    let y = select(-1.0, 1.0, (hash & 2u) != 0u);
    if (hash & 4u) != 0u {
        return vec2(x, y) * 0.7071067811865476;
    }
    return select(vec2(x, 0.0), vec2(0.0, x), (hash & 2u) != 0u);
}

fn river_noise_gradient(position: vec2<f32>) -> vec2<f32> {
    let cell = vec2<i32>(floor(position));
    let t = fract(position);
    // Quintic interpolation preserves derivatives across lattice boundaries.
    // Unlike differentiated value noise, the slope is not forced to zero at corners.
    let fade = t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
    let derivative = 30.0 * t * t * (t - 1.0) * (t - 1.0);
    let ga = river_noise_corner(cell);
    let gb = river_noise_corner(cell + vec2(1, 0));
    let gc = river_noise_corner(cell + vec2(0, 1));
    let gd = river_noise_corner(cell + vec2(1, 1));
    let a = dot(ga, t);
    let b = dot(gb, t - vec2(1.0, 0.0));
    let c = dot(gc, t - vec2(0.0, 1.0));
    let d = dot(gd, t - vec2(1.0, 1.0));
    let interpolated_gradient = mix(mix(ga, gb, fade.x), mix(gc, gd, fade.x), fade.y);
    return interpolated_gradient
        + derivative * vec2(mix(b - a, d - c, fade.y), mix(c - a, d - b, fade.x));
}

fn river_detail_gradient(position: vec2<f32>, wavelength: f32, footprint: f32) -> vec2<f32> {
    let coarse_frequency = 1.0 / max(wavelength * 0.22, 0.000001);
    let fine_frequency = coarse_frequency * 2.37;
    let coarse_filter = 1.0 - smoothstep(0.25, 1.0, footprint * coarse_frequency);
    let fine_filter = 1.0 - smoothstep(0.25, 1.0, footprint * fine_frequency);
    // Rotate the fine octave, then pull its gradient back into world XZ.
    // Merely rotating coordinates without this transpose gives incorrect normals.
    let rotation = mat2x2<f32>(vec2(0.8, 0.6), vec2(-0.6, 0.8));
    let fine_gradient = transpose(rotation) * river_noise_gradient(
        rotation * position * fine_frequency + vec2(19.7, 83.1));
    return river_noise_gradient(position * coarse_frequency) * coarse_frequency * 0.65 * coarse_filter
        + fine_gradient * fine_frequency * 0.35 * fine_filter;
}

fn flowing_ripple_gradient_uv(
    river_position_m: vec2<f32>, phase: f32, local_flow: vec2<f32>,
    wavelength: f32, amplitude: f32, footprint: f32,
) -> vec2<f32> {
    let flow_length = length(local_flow);
    // Keep metric detail in both axes. Flow is encoded by downstream advection,
    // not by stretching every ripple into a coherent cross-stream crest.
    // This local-frame approximation does not simulate hydraulic interactions.
    let flow_position = river_position_m;
    let cycle = fract(phase / 6.283185307179586);
    let travel = select(0.0, wavelength, flow_length > 0.000001);
    let offset_a = vec2(travel * cycle, 0.0);
    let offset_b = vec2(travel * fract(cycle + 0.5), 0.0);
    // Each advection reset has zero weight and zero weight derivative.
    let weight_a = 0.5 - 0.5 * cos(cycle * 6.283185307179586);
    let a = river_detail_gradient(flow_position - offset_a, wavelength, footprint);
    let b = river_detail_gradient(flow_position - offset_b, wavelength, footprint);
    let local_gradient = mix(b, a, weight_a);
    return local_gradient * max(amplitude, 0.0) * 0.3;
}

// Chain rule through the actual interpolated river coordinates, not an assumed
// orthonormal flow frame. Solves the two screen directional derivatives in XZ.
// Degenerate/grazing footprints contribute no detail rather than an unbounded slope.
fn river_world_gradient(
    gradient_uv: vec2<f32>, uv_dx: vec2<f32>, uv_dy: vec2<f32>,
    world_dx: vec2<f32>, world_dy: vec2<f32>,
) -> vec2<f32> {
    let determinant = world_dx.x * world_dy.y - world_dx.y * world_dy.x;
    let scale = length(world_dx) * length(world_dy);
    if scale <= 0.0 || abs(determinant) <= scale * 0.000001 {
        return vec2(0.0);
    }
    let screen_gradient = vec2(dot(gradient_uv, uv_dx), dot(gradient_uv, uv_dy));
    return vec2(
        screen_gradient.x * world_dy.y - screen_gradient.y * world_dx.y,
        screen_gradient.y * world_dx.x - screen_gradient.x * world_dy.x,
    ) / determinant;
}
