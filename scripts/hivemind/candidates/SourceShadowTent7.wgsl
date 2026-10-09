// Port of Unity Core ShadowSamplingTent.hlsl, package a4c9a9793789.
// Keep the source 0.081632 coefficient; do not renormalize its weights.
struct Tent7Axis {
    weights: vec4<f32>,
    offsets: vec4<f32>,
}
struct Tent7Samples {
    weights: array<f32,16>,
    uv: array<vec2<f32>,16>,
}
fn source_tent7_axis(offset: f32) -> Tent7Axis {
    let exterior = (offset + 0.5) * (offset + 0.5) * 0.5;
    let uncut_y = 1.0 - offset;
    let uncut_z = 1.0 + offset;
    let left = min(offset,0.0);
    let right = max(offset,0.0);
    let cut_y = uncut_y - left * left;
    let cut_z = uncut_z - right * right;
    let a = 0.081632 * vec4<f32>(exterior-offset,uncut_y,uncut_y+1.0,cut_y+2.0);
    let b = 0.081632 * vec4<f32>(cut_z+2.0,uncut_z+1.0,uncut_z,exterior);
    let weights = vec4<f32>(a.x+a.y,a.z+a.w,b.x+b.y,b.z+b.w);
    let offsets = vec4<f32>(a.y,a.w,b.y,b.w)/weights+vec4<f32>(-3.5,-1.5,0.5,2.5);
    return Tent7Axis(weights,offsets);
}
fn source_tent7_samples(texel_size: vec4<f32>, coord: vec2<f32>) -> Tent7Samples {
    let center = coord * texel_size.zw;
    let fetch_center = floor(center + vec2<f32>(0.5));
    let offset = center-fetch_center;
    let u = source_tent7_axis(offset.x);
    let v = source_tent7_axis(offset.y);
    let origin = fetch_center * texel_size.xy;
    var samples: Tent7Samples;
    for(var y=0u;y<4u;y+=1u) {
        for(var x=0u;x<4u;x+=1u) {
            let index=y*4u+x;
            samples.weights[index]=u.weights[x]*v.weights[y];
            samples.uv[index]=origin+vec2<f32>(u.offsets[x]*texel_size.x,v.offsets[y]*texel_size.y);
        }
    }
    return samples;
}
