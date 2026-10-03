// SPDX-License-Identifier: MIT
// Tidewater UnderwaterLighting.js, pinned 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
struct UnderwaterMapFrame { origins: vec4f, stamp: vec4u };
@group(#{MATERIAL_BIND_GROUP}) @binding(53) var<storage, read> uwMapFrame: UnderwaterMapFrame;
struct UwMapSample { height: f32, slope: vec2f, foam: f32, mean: f32, detailK: f32 };
fn uwMapLookup(xz: vec2f, waves: bool) -> UwMapSample {
    var a = vec4f(0.0);
    var b = vec4f(0.0, 0.9, 0.0, 0.0);
    let st0 = (xz - uwMapFrame.origins.xy) / 0.25;
    let st1 = (xz - uwMapFrame.origins.zw) / 2.0;
    var layer = -1;
    var st = vec2f(0.0);
    if (all(st0 > vec2f(0.5)) && all(st0 < vec2f(511.5))) {
        layer = 0; st = st0;
    } else if (all(st1 > vec2f(0.5)) && all(st1 < vec2f(511.5))) {
        layer = 1; st = st1;
    }
    if (layer >= 0 && uwMapFrame.stamp.z == 512u) {
        let uv = st / 512.0;
        if (waves) { a = textureSampleLevel(uwWaves, uwLinear, uv, layer, 0.0); }
        b = textureSampleLevel(uwLevel, uwLinear, uv, layer, 0.0);
    }
    return UwMapSample(UW_SEA_LEVEL + a.x, a.yz, a.w, UW_SEA_LEVEL + b.x, b.y);
}
