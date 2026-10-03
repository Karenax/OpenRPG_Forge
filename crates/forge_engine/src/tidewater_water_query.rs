//! Shared GPU water-height query state and compute pass.
//!
//! This keeps the bounded CPU-facing slot registry separate from the render graph.
//! The graph owns packet allocation and asynchronous readback.
// SPDX-License-Identifier: MIT
// Adapted from Tidewater `src/ocean/WaterQuery.js`, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1ab; see `third_party/tidewater/LICENSE`.

use bevy::{
    prelude::{Resource, default},
    render::{render_resource as gpu, renderer::RenderDevice},
};
use serde::Serialize;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex, MutexGuard},
};

/// Number of fixed GPU query slots, matching Tidewater's `MAX_QUERIES`.
pub const MAX_QUERIES: usize = 64;
/// Header (step, count, time) plus 64 input and 64 result `vec4f` values.
pub const PACKET_BYTES: usize = 16 + MAX_QUERIES * 16 * 2;

#[derive(Clone, Debug, PartialEq)]
pub struct RequestBatch {
    pub count: u32,
    pub points: [[f32; 4]; MAX_QUERIES],
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct Sample {
    pub height: f32,
    pub nx: f32,
    pub nz: f32,
    pub floor: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct SampleBatch {
    pub step: u32,
    pub time: f64,
    pub count: u32,
    pub points: Vec<[f32; 2]>,
    pub values: Vec<Sample>,
}

struct State {
    slots: BTreeMap<String, (u32, u32)>,
    count: u32,
    points: [[f32; 4]; MAX_QUERIES],
    latest: Option<SampleBatch>,
    latest_step: Option<u32>,
}

impl Default for State {
    fn default() -> Self {
        Self {
            slots: BTreeMap::new(),
            count: 1,
            points: [[0.0; 4]; MAX_QUERIES],
            latest: None,
            latest_step: None,
        }
    }
}

/// Thread-safe bounded water-query registry.
///
/// Slot zero is permanently reserved for the camera.  Named allocations begin
/// at slot one and are stable for the lifetime of this resource.
#[derive(Clone, Default, Resource)]
pub struct Queries {
    state: Arc<Mutex<State>>,
}

impl Queries {
    /// Allocates a stable, named range of gameplay slots and returns its first slot.
    pub fn allocate(&self, name: &str, count: u32) -> Result<u32, String> {
        if name.is_empty() {
            return Err("water query allocation name is empty".into());
        }
        if count == 0 {
            return Err("water query allocation count must be positive".into());
        }
        let mut state = self.lock();
        if let Some(&(start, _)) = state.slots.get(name) {
            return Ok(start);
        }
        let start = state.count;
        let end = start
            .checked_add(count)
            .ok_or_else(|| "water query allocation overflows".to_owned())?;
        if end as usize > MAX_QUERIES {
            return Err("water query slots exhausted".into());
        }
        state.slots.insert(name.to_owned(), (start, count));
        state.count = end;
        Ok(start)
    }

    /// Changes a query's world X/Z position.
    pub fn set_point(&self, slot: u32, x: f32, z: f32) -> Result<(), String> {
        if !x.is_finite() || !z.is_finite() {
            return Err("water query point must be finite".into());
        }
        let mut state = self.lock();
        let count = state.count;
        if slot >= count || slot as usize >= MAX_QUERIES {
            return Err("water query slot is not allocated".into());
        }
        state.points[slot as usize] = [x, z, 0.0, 0.0];
        Ok(())
    }

    /// Captures an immutable request packet for one GPU submission.
    #[must_use]
    pub fn snapshot(&self) -> RequestBatch {
        let state = self.lock();
        RequestBatch {
            count: state.count,
            points: state.points,
        }
    }

    /// Returns the newest accepted asynchronous result batch.
    #[must_use]
    pub fn latest(&self) -> Option<SampleBatch> {
        self.lock().latest.clone()
    }

    /// Accepts a completed packet readback.
    ///
    /// Initial zeroed buffers and older packets are ignored. Every new packet is
    /// decoded and validated before the shared latest result is replaced.
    pub fn accept(&self, bytes: &[u8]) -> Result<bool, String> {
        if bytes.len() != PACKET_BYTES {
            return Err(format!(
                "water query packet length {} is not {PACKET_BYTES}",
                bytes.len()
            ));
        }
        let step = u32_at(bytes, 0)?;
        if step == 0 {
            return Ok(false);
        }
        let count = u32_at(bytes, 4)?;
        let time = f64_at(bytes, 8)?;
        if count == 0 || count as usize > MAX_QUERIES {
            return Err("water query packet count is invalid".into());
        }
        if !time.is_finite() || time < 0.0 {
            return Err("water query packet time is invalid".into());
        }

        let mut state = self.lock();
        if state.latest_step.is_some_and(|latest| step <= latest) {
            return Ok(false);
        }
        if count > state.count {
            return Err("water query packet count exceeds allocated slots".into());
        }
        if state
            .latest
            .as_ref()
            .is_some_and(|latest| time < latest.time)
        {
            return Err("water query packet time moved backwards".into());
        }

        let mut points = Vec::with_capacity(count as usize);
        let mut values = Vec::with_capacity(count as usize);
        for index in 0..MAX_QUERIES {
            let point_offset = 16 + index * 16;
            let point = vec4_at(bytes, point_offset)?;
            if !point.iter().all(|value| value.is_finite()) {
                return Err("water query point is not finite".into());
            }
            let result_offset = 16 + MAX_QUERIES * 16 + index * 16;
            let value = vec4_at(bytes, result_offset)?;
            if !value.iter().all(|component| component.is_finite()) {
                return Err("water query result is not finite".into());
            }
            let normal_squared = value[1].mul_add(value[1], value[2] * value[2]);
            if !normal_squared.is_finite() || normal_squared > 1.000_1 {
                return Err("water query normal is invalid".into());
            }
            if index < count as usize {
                points.push([point[0], point[1]]);
                values.push(Sample {
                    height: value[0],
                    nx: value[1],
                    nz: value[2],
                    floor: value[3],
                });
            }
        }
        state.latest = Some(SampleBatch {
            step,
            time,
            count,
            points,
            values,
        });
        state.latest_step = Some(step);
        Ok(true)
    }

    fn lock(&self) -> MutexGuard<'_, State> {
        self.state
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
    }
}

fn u32_at(bytes: &[u8], offset: usize) -> Result<u32, String> {
    let raw: [u8; 4] = bytes
        .get(offset..offset + 4)
        .ok_or_else(|| "water query packet is truncated".to_owned())?
        .try_into()
        .map_err(|_| "water query packet integer is malformed".to_owned())?;
    Ok(u32::from_le_bytes(raw))
}

fn f32_at(bytes: &[u8], offset: usize) -> Result<f32, String> {
    Ok(f32::from_bits(u32_at(bytes, offset)?))
}

fn f64_at(bytes: &[u8], offset: usize) -> Result<f64, String> {
    let raw: [u8; 8] = bytes
        .get(offset..offset + 8)
        .ok_or_else(|| "water query packet is truncated".to_owned())?
        .try_into()
        .map_err(|_| "water query packet float is malformed".to_owned())?;
    Ok(f64::from_le_bytes(raw))
}

fn vec4_at(bytes: &[u8], offset: usize) -> Result<[f32; 4], String> {
    let mut value = [0.0; 4];
    for (component, destination) in value.iter_mut().enumerate() {
        *destination = f32_at(bytes, offset + component * 4)?;
    }
    Ok(value)
}

/// GPU compute pipeline for one full 64-slot water-query pass.
pub struct Prepared {
    pipeline: gpu::ComputePipeline,
}

impl Prepared {
    #[must_use]
    pub fn new(
        device: &RenderDevice,
        surface_shader: &str,
        layout0: &gpu::BindGroupLayout,
        layout1: &gpu::BindGroupLayout,
    ) -> Self {
        let shader = format!("{surface_shader}\n{QUERY_SHADER}");
        let module = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
            label: Some("forge water queries"),
            source: gpu::ShaderSource::Wgsl(shader.into()),
        });
        let query_layout = device.create_bind_group_layout(
            "forge water query buffers",
            &[gpu::BindGroupLayoutEntry {
                binding: 0,
                visibility: gpu::ShaderStages::COMPUTE,
                ty: gpu::BindingType::Buffer {
                    ty: gpu::BufferBindingType::Storage { read_only: false },
                    has_dynamic_offset: false,
                    min_binding_size: gpu::BufferSize::new(PACKET_BYTES as u64),
                },
                count: None,
            }],
        );
        let layout = device.create_pipeline_layout(&gpu::PipelineLayoutDescriptor {
            label: Some("forge water query layout"),
            bind_group_layouts: &[Some(layout0), Some(layout1), Some(&query_layout)],
            immediate_size: 0,
        });
        let pipeline = device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
            label: Some("forge water queries"),
            layout: Some(&layout),
            module: &module,
            entry_point: Some("forge_water_queries"),
            compilation_options: default(),
            cache: None,
        });
        Self { pipeline }
    }

    #[allow(clippy::too_many_arguments)] // GPU command recording requires all three existing bind groups.
    pub fn encode(
        &self,
        device: &RenderDevice,
        encoder: &mut gpu::CommandEncoder,
        group0: &gpu::BindGroup,
        group1: &gpu::BindGroup,
        packet: &gpu::Buffer,
        requests: &RequestBatch,
        step: u32,
        time: f64,
    ) {
        let input = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
            label: Some("forge water query inputs"),
            contents: &points_bytes(&requests.points),
            usage: gpu::BufferUsages::COPY_SRC,
        });
        let header = device.create_buffer_with_data(&gpu::BufferInitDescriptor {
            label: Some("forge water query stamp"),
            contents: &header_bytes(step, requests.count, time),
            usage: gpu::BufferUsages::COPY_SRC,
        });
        encoder.copy_buffer_to_buffer(&header, 0, packet, 0, 16);
        // The same submission copies immutable inputs before this pass reads them from `packet`.
        encoder.copy_buffer_to_buffer(&input, 0, packet, 16, (MAX_QUERIES * 16) as u64);
        let group2 = device.create_bind_group(
            "forge water query buffers",
            &self.pipeline.get_bind_group_layout(2).into(),
            &[gpu::BindGroupEntry {
                binding: 0,
                resource: packet.as_entire_binding(),
            }],
        );
        let mut pass = encoder.begin_compute_pass(&gpu::ComputePassDescriptor {
            label: Some("forge water queries"),
            ..default()
        });
        pass.set_pipeline(&self.pipeline);
        pass.set_bind_group(0, group0, &[]);
        pass.set_bind_group(1, group1, &[]);
        pass.set_bind_group(2, &group2, &[]);
        pass.dispatch_workgroups(1, 1, 1);
    }
}

fn header_bytes(step: u32, count: u32, time: f64) -> [u8; 16] {
    let mut bytes = [0; 16];
    bytes[..4].copy_from_slice(&step.to_le_bytes());
    bytes[4..8].copy_from_slice(&count.to_le_bytes());
    bytes[8..].copy_from_slice(&time.to_le_bytes());
    bytes
}

fn points_bytes(points: &[[f32; 4]; MAX_QUERIES]) -> [u8; MAX_QUERIES * 16] {
    let mut bytes = [0; MAX_QUERIES * 16];
    for (index, point) in points.iter().enumerate() {
        for (component, value) in point.iter().enumerate() {
            let offset = index * 16 + component * 4;
            bytes[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
        }
    }
    bytes
}

pub(crate) const QUERY_SHADER: &str = r"
struct ForgeWaterQueryPacket {
    stamp: vec4u,
    points: array<vec4f, 64>,
    results: array<vec4f, 64>,
};

@group(2) @binding(0) var<storage, read_write> forgeWaterQueryPacket: ForgeWaterQueryPacket;

@compute @workgroup_size(64, 1, 1)
fn forge_water_queries(@builtin(global_invocation_id) gid: vec3u) {
    let i = gid.x;
    if (i >= 64u) { return; }
    let xz = forgeWaterQueryPacket.points[i].xy;
    let h = waterQueryHeightAtXZ(xz);
    let e = 0.35;
    let hx = waterQueryHeightAtXZ(xz + vec2f(e, 0.0));
    let hz = waterQueryHeightAtXZ(xz + vec2f(0.0, e));
    let n = normalize(vec3f((h - hx) / e, 1.0, (h - hz) / e));
    forgeWaterQueryPacket.results[i] = vec4f(h, n.x, n.z, terrainHeightAt(xz));
}
";

#[cfg(test)]
mod tests {
    use super::*;

    fn packet(step: u32, count: u32, time: f64, point: [f32; 2], value: [f32; 4]) -> Vec<u8> {
        let mut bytes = vec![0; PACKET_BYTES];
        bytes[..4].copy_from_slice(&step.to_le_bytes());
        bytes[4..8].copy_from_slice(&count.to_le_bytes());
        bytes[8..16].copy_from_slice(&time.to_le_bytes());
        for index in 0..MAX_QUERIES {
            let offset = 16 + index * 16;
            bytes[offset..offset + 4].copy_from_slice(&point[0].to_le_bytes());
            bytes[offset + 4..offset + 8].copy_from_slice(&point[1].to_le_bytes());
            let result = 16 + MAX_QUERIES * 16 + index * 16;
            for (component, number) in value.iter().enumerate() {
                bytes[result + component * 4..result + (component + 1) * 4]
                    .copy_from_slice(&number.to_le_bytes());
            }
        }
        bytes
    }

    #[test]
    fn allocation_reserves_camera_and_refuses_overflow() {
        let queries = Queries::default();
        assert_eq!(queries.snapshot().count, 1);
        assert_eq!(queries.allocate("swimmer", 3), Ok(1));
        assert_eq!(queries.allocate("swimmer", 99), Ok(1));
        assert_eq!(
            queries.allocate("", 1),
            Err("water query allocation name is empty".into())
        );
        assert!(queries.allocate("too-many", 61).is_err());
        assert!(queries.set_point(4, 1.0, 2.0).is_err());
    }

    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Checks exact float32 packet heights against the written fixture bytes."
    )]
    fn accepts_positions_from_the_completed_packet() {
        let queries = Queries::default();
        queries.set_point(0, 4.0, 5.0).unwrap();
        let bytes = packet(3, 1, 2.5, [17.0, -9.0], [4.0, 0.2, 0.3, -8.0]);
        assert_eq!(queries.accept(&bytes), Ok(true));
        let latest = queries.latest().unwrap();
        assert_eq!(latest.points, vec![[17.0, -9.0]]);
        assert_eq!(latest.values[0].height, 4.0);
        assert_eq!(latest.values[0].floor, -8.0);
    }

    #[test]
    fn ignores_initial_and_stale_packets() {
        let queries = Queries::default();
        assert_eq!(queries.accept(&vec![0; PACKET_BYTES]), Ok(false));
        let current = packet(6, 1, 1.0, [0.0, 0.0], [1.0, 0.0, 0.0, -1.0]);
        assert_eq!(queries.accept(&current), Ok(true));
        let stale = packet(5, 1, 2.0, [4.0, 5.0], [9.0, 0.0, 0.0, -2.0]);
        assert_eq!(queries.accept(&stale), Ok(false));
        assert_eq!(queries.latest().unwrap().step, 6);
    }

    #[test]
    fn rejects_nonfinite_readback_without_replacing_latest() {
        let queries = Queries::default();
        let valid = packet(1, 1, 1.0, [0.0, 0.0], [1.0, 0.0, 0.0, -1.0]);
        assert_eq!(queries.accept(&valid), Ok(true));
        let mut invalid = packet(2, 1, 2.0, [0.0, 0.0], [f32::NAN, 0.0, 0.0, -1.0]);
        invalid[16 + MAX_QUERIES * 16..20 + MAX_QUERIES * 16]
            .copy_from_slice(&f32::NAN.to_le_bytes());
        assert!(queries.accept(&invalid).is_err());
        assert_eq!(queries.latest().unwrap().step, 1);
    }

    #[test]
    fn rejects_incompatible_metadata_without_replacing_latest() {
        let queries = Queries::default();
        let first = packet(3, 1, 2.0, [0.0, 0.0], [1.0, 0.0, 0.0, -1.0]);
        assert_eq!(queries.accept(&first), Ok(true));
        let excess = packet(4, 2, 3.0, [0.0, 0.0], [1.0, 0.0, 0.0, -1.0]);
        assert!(queries.accept(&excess).is_err());
        let backward = packet(4, 1, 1.0, [0.0, 0.0], [1.0, 0.0, 0.0, -1.0]);
        assert!(queries.accept(&backward).is_err());
        assert_eq!(queries.latest().unwrap().step, 3);
    }
}
