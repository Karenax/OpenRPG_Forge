"""Explicit RG-or-AG candidate; retain the existing RGB modes unchanged."""
ORIGINAL = '''fn source_graph_normal_sample(encoded:vec4<f32>,mode:f32)->vec4<f32> {
    return vec4<f32>(source_decode_normal(encoded.rgb,mode),encoded.a);
}'''
CANDIDATE = '''fn source_graph_normal_sample(encoded:vec4<f32>,mode:f32)->vec4<f32> {
    // Mode 2 requires exact imported-image GPU reference and host admission.
    if mode==2.0 {
        let xy=vec2<f32>(encoded.r*encoded.a,encoded.g)*2.0-1.0;
        let z=max(1e-16,sqrt(1.0-clamp(dot(xy,xy),0.0,1.0)));
        return vec4<f32>(xy,z,encoded.a);
    }
    return vec4<f32>(source_decode_normal(encoded.rgb,mode),encoded.a);
}'''


def candidate(source):
    assert source.count(ORIGINAL) == 1, 'Unexpected source normal decoder'
    return source.replace(ORIGINAL, CANDIDATE)
