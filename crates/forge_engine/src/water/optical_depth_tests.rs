//! Validate the actual optical WGSL, not a Rust reimplementation of its formulas.
//! Numeric GPU readback and render integration are separate, still-required proofs.

#[test]
fn refracted_lookup_wgsl_validates_with_explicit_bevy_interface_fixture() {
    // Syntax/type validation of Forge's actual function. These stubs deliberately
    // do not simulate Bevy traversal or count as a rendered/GPU intersection proof.
    let shader = include_str!("../shaders/water_reflection_preview.wgsl");
    let (_, function) = shader.split_once("struct WaterRefractionHit").unwrap();
    let source = format!(
        r"
struct View {{ viewport: vec4<f32>, world_from_clip: mat4x4<f32> }}
@group(0) @binding(0) var<uniform> view: View;
@group(0) @binding(1) var depth_prepass_texture: texture_depth_2d;
@group(0) @binding(2) var view_transmission_texture: texture_2d<f32>;
struct Trace {{ linear_steps: u32, bisection_steps: u32, use_secant: bool, depth_thickness_linear_z: f32 }}
struct Hit {{ hit: bool, hit_uv: vec2<f32> }}
fn depth_ray_march_new_from_depth(dims: vec2<f32>) -> Trace {{ return Trace(2u, 0u, false, 0.2); }}
fn position_world_to_ndc(p: vec3<f32>) -> vec3<f32> {{ return p; }}
fn depth_ray_march_from_cs(t: ptr<function, Trace>, p: vec3<f32>) {{}}
fn depth_ray_march_to_ws(t: ptr<function, Trace>, p: vec3<f32>) {{}}
fn depth_ray_march_march(t: ptr<function, Trace>) -> Hit {{ return Hit(false, vec2(0.0)); }}
struct WaterRefractionHit{function}
@fragment
fn main() -> @location(0) vec4<f32> {{
    let hit = water_scene_refraction(vec3(0.0), vec3(0.0, 1.0, 0.0),
        vec3(0.0, 1.0, 0.0), 1.333, vec4(24.0, 3.2, 0.2, 1.0));
    return vec4(hit.radiance, hit.path_m);
}}
"
    );
    let module = naga::front::wgsl::parse_str(&source)
        .unwrap_or_else(|error| panic!("{}", error.emit_to_string(&source)));
    naga::valid::Validator::new(
        naga::valid::ValidationFlags::all(),
        naga::valid::Capabilities::empty(),
    )
    .validate(&module)
    .unwrap_or_else(|error| panic!("{error:?}"));
}

#[test]
fn optical_depth_wgsl_validates_for_compute_and_fragment_consumers() {
    let functions = format!(
        "{}\n{}",
        include_str!("../shaders/water_optical_depth.wgsl"),
        include_str!("../shaders/water_flow_detail.wgsl")
    );
    let consumers = [
        r"
@group(0) @binding(0) var<storage, read_write> result: array<vec4<f32>>;
@compute @workgroup_size(1)
fn main() {
    let path = water_optical_path_m(vec2(10.0), vec4(0.0, 0.0, 100.0, 100.0),
        mat4x4<f32>(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 1.0, 0.0, 0.0),
        vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0)),
        vec3(0.0), 0.5, 0.25, 3.2);
    let flow = flowing_ripple_gradient_uv(vec2(10.0), 0.25, vec2(1.0, 0.0), 5.5, 0.12, 0.01);
    let transmission = water_medium_transmittance(path, vec3(0.2), vec3(0.1));
    let scattering = water_medium_scattering_weight(transmission, vec3(0.2), vec3(0.1));
    result[0] = vec4(water_beer_transmittance(path, vec3(0.5), 1.0) + scattering, water_contact_coverage(path, 0.2)) + vec4(flow, 0.0, 0.0);
}",
        r"
@fragment
fn main(@builtin(position) position: vec4<f32>) -> @location(0) vec4<f32> {
    let path = water_optical_path_m(position.xy, vec4(40.0, 20.0, 100.0, 100.0),
        mat4x4<f32>(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 1.0, 0.0, 0.0),
        vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0)),
        vec3(0.0), position.z, 0.25, 3.2);
    let transmission = water_medium_transmittance(path, vec3(0.0), vec3(0.0));
    return vec4(water_beer_transmittance(path, vec3(0.5), 1.0)
        + water_medium_scattering_weight(transmission, vec3(0.0), vec3(0.0)), 1.0);
}",
    ];
    for consumer in consumers {
        let source = format!("{functions}\n{consumer}");
        let module = naga::front::wgsl::parse_str(&source)
            .unwrap_or_else(|error| panic!("{}", error.emit_to_string(&source)));
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::empty(),
        )
        .validate(&module)
        .unwrap_or_else(|error| panic!("{error:?}"));
    }
}
