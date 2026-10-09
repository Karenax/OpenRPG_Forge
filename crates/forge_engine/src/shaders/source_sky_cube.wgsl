// Explicit prefiltered native cube; scene admission supplies its shared image.
@group(#{MATERIAL_BIND_GROUP}) @binding(123) var source_sky_cube_image:texture_cube_array<f32>;
@group(#{MATERIAL_BIND_GROUP}) @binding(124) var source_sky_cube_sampler:sampler;
fn source_prefiltered_sky_sample(source_direction:vec3<f32>,slice:i32,lod:f32)->vec4<f32> {
    return textureSampleLevel(source_sky_cube_image,source_sky_cube_sampler,normalize(source_direction),slice,lod);
}
