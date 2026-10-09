// Source Deferred diffuse transport. Inputs are linear, preconvolved irradiance
// and the admitted source diffuse FGD. No extra PI, gamma or exposure is applied.
// Screen visibility is 1 - source SSAO red, not the raw occlusion texture value.
// Matches pinned HDRP GBuffer material AO followed by GTAO multibounce.
fn source_gtao_multibounce(visibility:f32, diffuse_color:vec3<f32>)->vec3<f32> {
    let a=2.0404*diffuse_color-vec3<f32>(0.3324);
    let b=-4.7951*diffuse_color+vec3<f32>(0.6417);
    let c=2.7552*diffuse_color+vec3<f32>(0.6903);
    return max(vec3<f32>(visibility),((visibility*a+b)*visibility+c)*visibility);
}

fn source_deferred_diffuse_lighting(
    preconvolved_irradiance:vec3<f32>,
    diffuse_fgd:f32,
    diffuse_color:vec3<f32>,
    material_ao:f32,
    screen_visibility:f32,
)->vec3<f32> {
    // Deferred applies scalar material AO before the screen-space multibounce.
    // Do not substitute min(material AO, screen visibility), used by Forward.
    return preconvolved_irradiance*diffuse_fgd*diffuse_color*material_ao
        *source_gtao_multibounce(screen_visibility,diffuse_color);
}
