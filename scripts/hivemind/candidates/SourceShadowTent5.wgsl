// PCF5 sampling coordinates from Unity Core a4c9a9793789.
// Numeric candidate only: atlas comparison and scene integration remain separate.
struct Tent5Axis {
    weights: vec3<f32>,
    offsets: vec3<f32>,
}
struct Tent5Samples {
    weights: array<f32,9>,
    uv: array<vec2<f32>,9>,
}
fn source_tent5_axis(offset: f32) -> Tent5Axis {
    let exterior = (offset + 0.5) * (offset + 0.5) * 0.5;
    let uncut_y = (1.5 - offset) - 0.5;
    let uncut_z = (1.5 + offset) - 0.5;
    let left = min(offset,0.0);
    let right = max(offset,0.0);
    let cut_y = uncut_y - left * left;
    let cut_z = uncut_z - right * right;
    let a = 0.16 * vec3<f32>(exterior-offset,uncut_y,cut_y+1.0);
    let b = 0.16 * vec3<f32>(cut_z+1.0,uncut_z,exterior);
    let weights = vec3<f32>(a.x+a.y,a.z+b.x,b.y+b.z);
    let offsets = vec3<f32>(a.y,b.x,b.z)/weights+vec3<f32>(-2.5,-0.5,1.5);
    return Tent5Axis(weights,offsets);
}
fn source_tent5_samples(texel_size: vec4<f32>, coord: vec2<f32>) -> Tent5Samples {
    let center = coord * texel_size.zw;
    let fetch_center = floor(center + vec2<f32>(0.5));
    let offset = center-fetch_center;
    let u = source_tent5_axis(offset.x);
    let v = source_tent5_axis(offset.y);
    let origin = fetch_center * texel_size.xy;
    var samples: Tent5Samples;
    for(var y=0u;y<3u;y+=1u) {
        for(var x=0u;x<3u;x+=1u) {
            let index=y*3u+x;
            samples.weights[index]=u.weights[x]*v.weights[y];
            samples.uv[index]=origin+vec2<f32>(u.offsets[x]*texel_size.x,v.offsets[y]*texel_size.y);
        }
    }
    return samples;
}
