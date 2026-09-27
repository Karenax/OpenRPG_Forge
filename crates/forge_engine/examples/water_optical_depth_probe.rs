//! Explicit headless GPU proof, not part of normal unit tests or the game renderer.
use std::{borrow::Cow, error::Error, time::Duration};
use wgpu::util::DeviceExt;

type ProbeResult<T> = Result<T, Box<dyn Error>>;
type CaseDefinition = (&'static str, &'static [(usize, f32)], f32);

struct Case {
    name: &'static str,
    data: [f32; 28],
    expected_path: f32,
}

fn cases() -> Vec<Case> {
    // Inverse infinite reverse-Z perspective: near=0.1, focal scales=1.
    let base = [
        50.0, 50.0, 0.05, 0.025, // pixel XY, surface depth, opaque depth
        0.0, 0.0, 100.0, 100.0, // viewport
        0.0, 0.0, -2.0, 3.2, // surface world XYZ, path cap
        1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, -1.0, 0.0,
    ];
    let definitions: [CaseDefinition; 11] = [
        ("perspective_two_metres", &[], 2.0),
        (
            "offset_viewport",
            &[(0, 90.0), (1, 70.0), (4, 40.0), (5, 20.0)],
            2.0,
        ),
        (
            "oblique_ray_not_vertical_depth",
            &[(0, 75.0), (8, 1.0)],
            5.0_f32.sqrt(),
        ),
        ("foreground_rejected", &[(3, 0.1)], 0.0),
        ("contact_zero", &[(3, 0.05)], 0.0),
        ("contact_half_transition", &[(3, 0.1 / 2.1)], 0.1),
        ("contact_full_transition", &[(3, 0.1 / 2.2)], 0.2),
        ("sky_bounded", &[(3, 0.0)], 3.2),
        ("deep_bounded", &[(3, 0.001)], 3.2),
        ("outside_viewport", &[(0, 100.0)], 3.2),
        ("empty_viewport", &[(6, 0.0)], 3.2),
    ];
    definitions
        .into_iter()
        .map(|(name, changes, expected_path)| {
            let mut data = base;
            for &(index, value) in changes {
                data[index] = value;
            }
            Case {
                name,
                data,
                expected_path,
            }
        })
        .collect()
}

fn main() -> ProbeResult<()> {
    bevy::tasks::block_on(run())
}

#[expect(
    clippy::too_many_lines,
    reason = "bounded headless proof keeps GPU allocation, submission, readback and destruction in one ownership scope"
)]
async fn run() -> ProbeResult<()> {
    let cases = cases();
    let instance = wgpu::Instance::new(wgpu::InstanceDescriptor::new_without_display_handle());
    let adapter = instance
        .request_adapter(&wgpu::RequestAdapterOptions {
            power_preference: wgpu::PowerPreference::HighPerformance,
            force_fallback_adapter: false,
            compatible_surface: None,
        })
        .await?;
    let info = adapter.get_info();
    if info.device_type == wgpu::DeviceType::Cpu {
        return Err("Software adapter is not a hardware GPU proof".into());
    }
    let (device, queue) = adapter
        .request_device(&wgpu::DeviceDescriptor::default())
        .await?;
    let input: Vec<u8> = cases
        .iter()
        .flat_map(|case| case.data)
        .flat_map(f32::to_le_bytes)
        .collect();
    let inputs = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
        label: Some("optical cases"),
        contents: &input,
        usage: wgpu::BufferUsages::STORAGE,
    });
    let size = u64::try_from(cases.len())? * 96;
    let output = device.create_buffer(&wgpu::BufferDescriptor {
        label: Some("optical GPU results"),
        size,
        usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
        mapped_at_creation: false,
    });
    let readback = device.create_buffer(&wgpu::BufferDescriptor {
        label: Some("optical readback"),
        size,
        usage: wgpu::BufferUsages::MAP_READ | wgpu::BufferUsages::COPY_DST,
        mapped_at_creation: false,
    });
    // Inputs arrive through storage, preventing a constant-folded numeric proof.
    let source = format!(
        "{}\n{}\n{}",
        include_str!("../src/shaders/water_optical_depth.wgsl"),
        include_str!("../src/shaders/water_flow_detail.wgsl"),
        r"
@group(0) @binding(0) var<storage, read> inputs: array<vec4<f32>>;
@group(0) @binding(1) var<storage, read_write> results: array<vec4<f32>>;
@compute @workgroup_size(1)
fn main(@builtin(global_invocation_id) id: vec3<u32>) {
    let i = id.x * 7u;
    let p = inputs[i];
    let v = inputs[i + 1u];
    let w = inputs[i + 2u];
    let m = mat4x4<f32>(inputs[i+3u], inputs[i+4u], inputs[i+5u], inputs[i+6u]);
    let path = water_optical_path_m(p.xy, v, m, w.xyz, p.z, p.w, w.w);
    let absorption = vec3(0.8, 0.0, 0.0);
    let scattering = vec3(0.1, 0.2, 0.0);
    let transmission = water_medium_transmittance(path, absorption, scattering);
    results[id.x * 6u] = vec4(water_beer_transmittance(path, vec3(0.5, 0.25, 0.125), 2.0), path);
    results[id.x * 6u + 1u] = vec4(transmission, path);
    results[id.x * 6u + 2u] = vec4(water_medium_scattering_weight(transmission, absorption, scattering), path);
    results[id.x * 6u + 3u] = vec4(water_contact_coverage(path, 0.2), water_contact_coverage(path, 0.4), water_contact_coverage(path, 0.0), path);
    // Known affine field: u=2*x+z, v=-x+3*z, h=0.2*u-0.1*v.
    // World gradient is (0.5,-0.1) for arbitrary nondegenerate screen bases.
    // The screen basis comes from storage rather than a constant-folded fixture.
    let dx = p.xy;
    let dy = vec2(-p.y, p.x);
    let ux = vec2(2.0 * dx.x + dx.y, -dx.x + 3.0 * dx.y);
    let uy = vec2(2.0 * dy.x + dy.y, -dy.x + 3.0 * dy.y);
    let g = vec2(0.2, -0.1);
    results[id.x * 6u + 4u] = vec4(
        river_world_gradient(g, ux, uy, dx, dy),
        river_world_gradient(g, ux, -uy, dx, -dy));
    results[id.x * 6u + 5u] = vec4(
        river_world_gradient(g, ux, ux, dx, dx),
        river_world_gradient(g, ux, uy, vec2(0.0), dy));
}"
    );
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("Forge optical depth proof"),
        source: wgpu::ShaderSource::Wgsl(Cow::Owned(source)),
    });
    let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
        label: Some("optical proof"),
        layout: None,
        module: &shader,
        entry_point: Some("main"),
        compilation_options: wgpu::PipelineCompilationOptions::default(),
        cache: None,
    });
    let group = device.create_bind_group(&wgpu::BindGroupDescriptor {
        label: Some("optical proof"),
        layout: &pipeline.get_bind_group_layout(0),
        entries: &[
            wgpu::BindGroupEntry {
                binding: 0,
                resource: inputs.as_entire_binding(),
            },
            wgpu::BindGroupEntry {
                binding: 1,
                resource: output.as_entire_binding(),
            },
        ],
    });
    let mut encoder = device.create_command_encoder(&wgpu::CommandEncoderDescriptor::default());
    {
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor::default());
        pass.set_pipeline(&pipeline);
        pass.set_bind_group(0, &group, &[]);
        pass.dispatch_workgroups(u32::try_from(cases.len())?, 1, 1);
    }
    encoder.copy_buffer_to_buffer(&output, 0, &readback, 0, size);
    let submission = queue.submit([encoder.finish()]);
    let (sender, receiver) = std::sync::mpsc::channel();
    readback
        .slice(..)
        .map_async(wgpu::MapMode::Read, move |result| {
            let _ = sender.send(result);
        });
    device.poll(wgpu::PollType::Wait {
        submission_index: Some(submission),
        timeout: Some(Duration::from_secs(30)),
    })?;
    receiver.recv_timeout(Duration::from_secs(5))??;
    let mapped = readback.slice(..).get_mapped_range();
    let mut observations = Vec::new();
    let mut passed = true;
    for (case, bytes) in cases.iter().zip(mapped.as_chunks::<96>().0) {
        let mut actual = [0.0_f32; 24];
        for (value, chunk) in actual.iter_mut().zip(bytes.as_chunks::<4>().0) {
            *value = f32::from_le_bytes(*chunk);
        }
        let p = case.expected_path;
        let coverage = |width: f32| {
            let t = (p / width).clamp(0.0, 1.0);
            t * t * (3.0 - 2.0 * t)
        };
        let expected = [
            0.5_f32.powf(p / 2.0),
            0.25_f32.powf(p / 2.0),
            0.125_f32.powf(p / 2.0),
            p,
            (-0.9 * p).exp(),
            (-0.2 * p).exp(),
            1.0,
            p,
            (1.0 - (-0.9 * p).exp()) / 9.0,
            1.0 - (-0.2 * p).exp(),
            0.0,
            p,
            coverage(0.2),
            coverage(0.4),
            1.0,
            p,
            0.5,
            -0.1,
            0.5,
            -0.1,
            0.0,
            0.0,
            0.0,
            0.0,
        ];
        let ok = actual
            .iter()
            .zip(expected)
            .all(|(a, e)| a.is_finite() && (a - e).abs() <= 0.0001);
        passed &= ok;
        observations.push(
            serde_json::json!({"case":case.name,"actual":actual,"expected":expected,"passed":ok}),
        );
    }
    drop(mapped);
    readback.unmap();
    device.destroy();
    println!(
        "{}",
        serde_json::json!({"adapter":info.name,"backend":format!("{:?}",info.backend),
        "device_type":format!("{:?}",info.device_type),"passed":passed,"cases":observations,
        "scope":"compute readback only; no scene depth texture, reflection or visual qualification"})
    );
    if !passed {
        return Err("Optical GPU result mismatch".into());
    }
    Ok(())
}
