// SPDX-License-Identifier: MIT
// Derived from Tidewater Clouds.js at 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
#define_import_path forge::tidewater_air_haze_cloud_view

// Basis of the camera which produced the cloud history, not necessarily the
// current scene camera. Resize/startup and skipped underwater traces invalidate
// the history independently of the panorama. Render-only compact 80-byte ABI.
struct HazeCloudView {
    right: vec4f,
    up: vec4f,
    forward: vec4f,
    // Frustum tangents XY, traced history dimensions ZW.
    tangent_size: vec4f,
    // X: source viewValid; other components reserved.
    validity: vec4f,
}

fn haze_cloud_catmull(uv: vec2f, size: vec2f,
                     history: texture_2d<f32>, linear_clamp: sampler) -> vec4f {
    let sp = uv * size;
    let tp1 = floor(sp - 0.5) + 0.5;
    let f = sp - tp1;
    let w0 = f * (f * (f * -0.5 + 1.0) - 0.5);
    let w1 = f * f * (f * 1.5 - 2.5) + 1.0;
    let w2 = f * (f * (f * -1.5 + 2.0) + 0.5);
    let w3 = f * f * (f * 0.5 - 0.5);
    let w12 = w1 + w2;
    let tc0 = (tp1 - 1.0) / size;
    let tc3 = (tp1 + 2.0) / size;
    let tc12 = (tp1 + w2 / w12) / size;
    let a = w12.x * w0.y;
    let b = w0.x * w12.y;
    let c = w12.x * w12.y;
    let d = w3.x * w12.y;
    let e = w12.x * w3.y;
    let sum = textureSampleLevel(history, linear_clamp, vec2f(tc12.x, tc0.y), 0.0) * a
        + textureSampleLevel(history, linear_clamp, vec2f(tc0.x, tc12.y), 0.0) * b
        + textureSampleLevel(history, linear_clamp, tc12, 0.0) * c
        + textureSampleLevel(history, linear_clamp, vec2f(tc3.x, tc12.y), 0.0) * d
        + textureSampleLevel(history, linear_clamp, vec2f(tc12.x, tc3.y), 0.0) * e;
    return sum / (a + b + c + d + e);
}

fn haze_cloud_panorama(dir: vec3f, panorama: texture_2d<f32>, linear_repeat: sampler) -> vec4f {
    let u = fract(atan2(dir.z, dir.x) / 6.283185307179586);
    let elevation = -acos(clamp(dir.y, -1.0, 1.0)) + 1.5707963267948966;
    let t = clamp((elevation + 0.06981317007977318) / 1.6406094968746698, 0.0, 1.0);
    let value = textureSampleLevel(panorama, linear_repeat, vec2f(u, sqrt(t)), 0.0);
    let below = smoothstep(-0.07, -0.03, dir.y);
    return vec4f(value.rgb * below, mix(1.0, value.a, below));
}

fn haze_cloud_sample_view(dir: vec3f, view: HazeCloudView,
                          history: texture_2d<f32>, panorama: texture_2d<f32>,
                          linear_clamp: sampler, linear_repeat: sampler) -> vec4f {
    let z = max(dot(dir, view.forward.xyz), 1e-4);
    let ndc = vec2f(dot(dir, view.right.xyz), dot(dir, view.up.xyz)) / (view.tangent_size.xy * z);
    let uv = vec2f(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5);
    let inside = view.validity.x > 0.5 && dot(dir, view.forward.xyz) > 0.01
        && uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
    if (inside) {
        let value = max(haze_cloud_catmull(uv, view.tangent_size.zw, history, linear_clamp), vec4f(0.0));
        let above = smoothstep(-0.05, -0.03, dir.y);
        return vec4f(value.rgb * above, mix(1.0, min(value.a, 1.0), above));
    }
    return haze_cloud_panorama(dir, panorama, linear_repeat);
}
