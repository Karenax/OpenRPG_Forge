// Linear preconvolved SH9 polynomial. Coefficients are supplied explicitly.
// This evaluates irradiance only; BRDF, occlusion and exposure remain separate.
struct SourceDiffuseProbe {
    direction_source_from_target: mat4x4<f32>,
    coefficients: array<vec4<f32>,7>,
    control: vec4<f32>,
}
@group(#{MATERIAL_BIND_GROUP}) @binding(121) var<uniform> source_diffuse_probe: SourceDiffuseProbe;
fn source_preconvolved_diffuse_irradiance(target_normal:vec3<f32>)->vec3<f32> {
    if source_diffuse_probe.control.x!=1.0 {return vec3<f32>(0.0);}
    let n=normalize((source_diffuse_probe.direction_source_from_target*vec4<f32>(target_normal,0.0)).xyz);
    let linear=vec4<f32>(n,1.0);
    let quadratic=vec4<f32>(n.x*n.y,n.y*n.z,n.z*n.z,n.z*n.x);
    let c=source_diffuse_probe.coefficients;
    return vec3<f32>(dot(c[0],linear)+dot(c[3],quadratic),dot(c[1],linear)+dot(c[4],quadratic),dot(c[2],linear)+dot(c[5],quadratic)) + c[6].xyz*(n.x*n.x-n.y*n.y);
}
