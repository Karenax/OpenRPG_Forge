// Requires SourceShadowTent5.wgsl. Receives already resolved atlas coordinates.
// Caller owns face selection, projection, normal bias and atlas bounds.
// Pass the resolved comparison bias explicitly (HDRP fragment default: 1/65536).
fn source_tent5_compare(
    atlas: texture_depth_2d,
    comparison_sampler: sampler_comparison,
    texel_size: vec4<f32>,
    position_tc: vec3<f32>,
    comparison_bias: f32,
) -> f32 {
    let samples = source_tent5_samples(texel_size, position_tc.xy);
    let reference_depth = position_tc.z + comparison_bias;
    var shadow = 0.0;
    for(var i=0u;i<9u;i+=1u) {
        shadow += samples.weights[i] * textureSampleCompareLevel(
            atlas, comparison_sampler, samples.uv[i], reference_depth,
        );
    }
    return shadow;
}
