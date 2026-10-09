// Source GTAOCommon.hlsl packs occlusion as 1 - PositivePow(visibility,intensity).
// Lighting consumes its complement, so source visibility is pow(raw,intensity).
// This projects intensity only; the native/source horizon algorithms still differ.
fn source_scene_ambient_visibility(raw_visibility:f32,source_intensity:f32)->f32 {
    if source_intensity<=0.0 {return 1.0;}
    return pow(clamp(raw_visibility,0.0,1.0),source_intensity);
}
