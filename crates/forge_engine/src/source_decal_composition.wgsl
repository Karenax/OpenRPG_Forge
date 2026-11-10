#define_import_path forge_source_decal::composition

#import bevy_pbr::pbr_types::PbrInput

fn apply(pbr: ptr<function, PbrInput>, color: vec4<f32>, normal: vec4<f32>) {
    (*pbr).material.base_color = vec4(
        (*pbr).material.base_color.rgb * color.a + color.rgb,
        (*pbr).material.base_color.a);
    if normal.a < 1.0 {
        let composed = (*pbr).N * normal.a + 2.0 * normal.rgb - vec3(254.0 / 255.0);
        if dot(composed, composed) > 1e-12 {
            (*pbr).N = normalize(composed);
        }
    }
}
