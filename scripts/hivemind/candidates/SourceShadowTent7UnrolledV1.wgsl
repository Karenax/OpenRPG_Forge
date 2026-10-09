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
    samples.weights[0]=u.weights[0]*v.weights[0];
    samples.uv[0]=origin+vec2<f32>(u.offsets[0]*texel_size.x,v.offsets[0]*texel_size.y);
    samples.weights[1]=u.weights[1]*v.weights[0];
    samples.uv[1]=origin+vec2<f32>(u.offsets[1]*texel_size.x,v.offsets[0]*texel_size.y);
    samples.weights[2]=u.weights[2]*v.weights[0];
    samples.uv[2]=origin+vec2<f32>(u.offsets[2]*texel_size.x,v.offsets[0]*texel_size.y);
    samples.weights[3]=u.weights[3]*v.weights[0];
    samples.uv[3]=origin+vec2<f32>(u.offsets[3]*texel_size.x,v.offsets[0]*texel_size.y);
    samples.weights[4]=u.weights[0]*v.weights[1];
    samples.uv[4]=origin+vec2<f32>(u.offsets[0]*texel_size.x,v.offsets[1]*texel_size.y);
    samples.weights[5]=u.weights[1]*v.weights[1];
    samples.uv[5]=origin+vec2<f32>(u.offsets[1]*texel_size.x,v.offsets[1]*texel_size.y);
    samples.weights[6]=u.weights[2]*v.weights[1];
    samples.uv[6]=origin+vec2<f32>(u.offsets[2]*texel_size.x,v.offsets[1]*texel_size.y);
    samples.weights[7]=u.weights[3]*v.weights[1];
    samples.uv[7]=origin+vec2<f32>(u.offsets[3]*texel_size.x,v.offsets[1]*texel_size.y);
    samples.weights[8]=u.weights[0]*v.weights[2];
    samples.uv[8]=origin+vec2<f32>(u.offsets[0]*texel_size.x,v.offsets[2]*texel_size.y);
    samples.weights[9]=u.weights[1]*v.weights[2];
    samples.uv[9]=origin+vec2<f32>(u.offsets[1]*texel_size.x,v.offsets[2]*texel_size.y);
    samples.weights[10]=u.weights[2]*v.weights[2];
    samples.uv[10]=origin+vec2<f32>(u.offsets[2]*texel_size.x,v.offsets[2]*texel_size.y);
    samples.weights[11]=u.weights[3]*v.weights[2];
    samples.uv[11]=origin+vec2<f32>(u.offsets[3]*texel_size.x,v.offsets[2]*texel_size.y);
    samples.weights[12]=u.weights[0]*v.weights[3];
    samples.uv[12]=origin+vec2<f32>(u.offsets[0]*texel_size.x,v.offsets[3]*texel_size.y);
    samples.weights[13]=u.weights[1]*v.weights[3];
    samples.uv[13]=origin+vec2<f32>(u.offsets[1]*texel_size.x,v.offsets[3]*texel_size.y);
    samples.weights[14]=u.weights[2]*v.weights[3];
    samples.uv[14]=origin+vec2<f32>(u.offsets[2]*texel_size.x,v.offsets[3]*texel_size.y);
    samples.weights[15]=u.weights[3]*v.weights[3];
    samples.uv[15]=origin+vec2<f32>(u.offsets[3]*texel_size.x,v.offsets[3]*texel_size.y);
    return samples;
}
