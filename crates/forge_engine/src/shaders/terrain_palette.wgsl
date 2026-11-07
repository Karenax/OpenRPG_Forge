// Forge-original authored palette; shared arithmetic, no map resources.
struct VisualValleyTerrainExtension {
    grass_color_roughness: vec4<f32>,
    earth_color_roughness: vec4<f32>,
    rock_color_roughness: vec4<f32>,
    grass_metallic_reflectance: vec4<f32>,
    earth_metallic_reflectance: vec4<f32>,
    rock_metallic_reflectance: vec4<f32>,
    wetness_parameters: vec4<f32>,
}

struct AuthoredTerrainSurface {
    base_color: vec4<f32>,
    perceptual_roughness: f32,
    metallic: f32,
    reflectance: vec3<f32>,
    normal: vec3<f32>,
}

fn normalized_material_weights(raw_weights: vec3<f32>) -> vec3<f32> {
    let non_negative = max(raw_weights, vec3(0.0));
    let total = dot(non_negative, vec3(1.0));
    if total <= 0.000001 {
        return vec3(1.0, 0.0, 0.0);
    }
    return non_negative / total;
}

// World coordinates preserve chunk continuity. The repeatable R8 tile is generated
// once by Forge, rather than evaluating procedural hashes for every rendered pixel.
fn filtered_terrain_detail(position: vec2<f32>, footprint_m: f32, cells_per_m: f32, terrain_detail: texture_2d<f32>, terrain_detail_sampler: sampler) -> f32 {
    // Fade subpixel grain to its mean instead of shimmering at long range.
    let resolved = 1.0 - smoothstep(0.35, 1.25, footprint_m * cells_per_m);
    // Ease between texel centres: plain bilinear interpolation has discontinuous
    // slopes at cell edges, which become a grid when used for lighting normals.
    let cell = position * cells_per_m - vec2(0.5);
    let fraction = fract(cell);
    let eased = fraction * fraction * (vec2(3.0) - 2.0 * fraction);
    let uv = (floor(cell) + eased + vec2(0.5)) / 128.0;
    let value = textureSample(terrain_detail, terrain_detail_sampler, uv).r;
    return (value - 0.5) * resolved;
}

fn authored_terrain_surface(weights: vec3<f32>, position: vec3<f32>, world_normal: vec3<f32>, visual_valley: VisualValleyTerrainExtension, terrain_detail: texture_2d<f32>, terrain_detail_sampler: sampler) -> AuthoredTerrainSurface {
    var surface: AuthoredTerrainSurface;
    let world_xz = position.xz;
    let footprint_m = max(length(dpdx(world_xz)), length(dpdy(world_xz)));
    let broad_detail = filtered_terrain_detail(world_xz, footprint_m, 0.18, terrain_detail, terrain_detail_sampler);
    let medium_detail = filtered_terrain_detail(world_xz, footprint_m, 3.0, terrain_detail, terrain_detail_sampler);
    let fine_detail = filtered_terrain_detail(world_xz, footprint_m, 32.0, terrain_detail, terrain_detail_sampler);
    // Shading-only microrelief in metres, from the already filtered detail samples.
    // A surface gradient works without UV tangents and remains continuous across chunks.
    // Keep geometric normals/positions intact for shadow bias and terrain collisions.
    let relief_height_m = medium_detail * dot(weights, vec3(0.045, 0.025, 0.065))
        + fine_detail * dot(weights, vec3(0.008, 0.006, 0.003));
    let surface_dx = dpdx(position);
    let surface_dy = dpdy(position);
    let normal = normalize(world_normal);
    let reciprocal_x = cross(surface_dy, normal);
    let reciprocal_y = cross(normal, surface_dx);
    let determinant = dot(surface_dx, reciprocal_x);
    let gradient_numerator = dpdx(relief_height_m) * reciprocal_x
        + dpdy(relief_height_m) * reciprocal_y;
    // Bound slopes to avoid grazing-angle amplification and division by zero.
    // With a degenerate projected surface, sign(0) gives the original normal.
    let gradient = sign(determinant) * gradient_numerator
        / max(max(abs(determinant), length(gradient_numerator) / 0.65), 0.00000001);
    surface.normal = normalize(normal - gradient);
    let layer_detail = vec3(
        1.0 + broad_detail * 0.12 + medium_detail * 0.36 + fine_detail * 0.24,
        1.0 + broad_detail * 0.12 + medium_detail * 0.30 + fine_detail * 0.52,
        1.0 + broad_detail * 0.12 + medium_detail * 0.50 + fine_detail * 0.18,
    );
    // Dry litter is presentation detail inside the grass layer, not a rewritten biome mask.
    // Keep chromatic litter at sub-metre scale: thresholding broad noise paints
    // metre-wide camouflage islands unrelated to the authored biome weights.
    // Linear mixing also converges to the same mean as fine detail becomes subpixel.
    // Reuse filtered samples; no extra texture fetch or noise hash.
    let dry_litter = clamp(0.08 + medium_detail * 0.16, 0.0, 0.16);
    let grass_surface = mix(
        visual_valley.grass_color_roughness.rgb,
        visual_valley.earth_color_roughness.rgb,
        dry_litter,
    );
    let base_color =
        grass_surface * weights.x * layer_detail.x
        + visual_valley.earth_color_roughness.rgb * weights.y * layer_detail.y
        + visual_valley.rock_color_roughness.rgb * weights.z * layer_detail.z;
    let perceptual_roughness = dot(
        vec3(
            visual_valley.grass_color_roughness.a,
            visual_valley.earth_color_roughness.a,
            visual_valley.rock_color_roughness.a,
        ),
        weights,
    );
    let metallic = dot(
        vec3(
            visual_valley.grass_metallic_reflectance.x,
            visual_valley.earth_metallic_reflectance.x,
            visual_valley.rock_metallic_reflectance.x,
        ),
        weights,
    );
    let reflectance = dot(
        vec3(
            visual_valley.grass_metallic_reflectance.y,
            visual_valley.earth_metallic_reflectance.y,
            visual_valley.rock_metallic_reflectance.y,
        ),
        weights,
    );

    let wetness = clamp(visual_valley.wetness_parameters.x, 0.0, 1.0);
    let wet_albedo_multiplier = mix(
        1.0,
        visual_valley.wetness_parameters.z,
        wetness,
    );
    // Bevy squares perceptual roughness into microfacet alpha. Apply the weather
    // multiplier in alpha space, without unintentionally squaring its reduction.
    let wet_roughness_multiplier = sqrt(mix(
        1.0,
        visual_valley.wetness_parameters.y,
        wetness,
    ));
    surface.base_color = vec4(base_color * wet_albedo_multiplier, 1.0);
    surface.perceptual_roughness = clamp(
        (perceptual_roughness + medium_detail * 0.08 + fine_detail * 0.04)
            * wet_roughness_multiplier,
        0.04,
        1.0,
    );
    surface.metallic = clamp(metallic, 0.0, 1.0);
    surface.reflectance = vec3(clamp(reflectance, 0.0, 1.0));
    return surface;
}
