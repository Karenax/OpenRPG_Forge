// SPDX-License-Identifier: MIT
// Adapted from Tidewater ShoreSim.js, copyright 2026 DRG Software Solutions LLC.
// Revision 1438b1abfcaee3267092b75573014f4d9b4a983c; third_party/tidewater/LICENSE.
//! Resident source `ShoreSim` on Bevy's render device, with authored-terrain derivatives.
#![allow(clippy::too_many_lines)]

use crate::sampled_terrain_corrections::Corrections;
use crate::source_breaker_gpu::TerrainTextureLayout;
use crate::source_ocean;
use crate::source_shore_field;
use crate::source_water_schedule::ShoreComputeSet;
use bevy::{
    asset::RenderAssetUsages,
    core_pipeline::schedule::camera_driver,
    prelude::*,
    render::{
        RenderApp,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        render_asset::RenderAssets,
        renderer::{RenderContext, RenderDevice, RenderGraph, RenderQueue},
        storage::GpuShaderBuffer,
        texture::GpuImage,
    },
};
use forge_assets::source_shore_sim_package::ShoreSimPackage;
use forge_assets::source_water_surface_package::WaterSurfacePackage;
use forge_core::bytes_sha256;
use forge_project::TerrainDocument;
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::sync::{
    Arc, Mutex,
    atomic::{AtomicBool, AtomicU32, Ordering},
};
use wgpu::util::DeviceExt;

/// Shared source hash arithmetic used by runtime and qualification probes.
pub const SHORE_HASH_SOURCE: &str = include_str!("source_shore_hash.wgsl");

const STATE_SIDE: u32 = 768;
const SHORE_SIDE: u32 = 512;
const BOOTSTRAP_STEPS: u32 = 120;
const HASH_DEFINITION: &str =
    "fn shoreHash1( x: f32 ) -> f32 { return fract( sin( x * 127.1 + 311.7 ) * 43758.5453 ); }";

struct Input {
    generation: Arc<()>,
    kernel: String,
    heights: Vec<u8>,
    shore: Vec<u8>,
    domain: source_shore_field::Domain,
    layout: TerrainTextureLayout,
    water_level: f32,
    direction: [u8; 16],
    lace: Vec<Vec<u8>>,
    simulation_params: [u8; 48],
    terrain_params: [u8; 32],
    shore_params: [u8; 64],
    corrections: Corrections,
    document_hash: forge_core::ContentHash,
    shore_derived: bool,
    surface_package_sha256: forge_core::ContentHash,
}

/// Fully checked CPU input for a new authored terrain generation.
#[derive(Clone)]
pub struct PreparedShoreSim(Arc<Input>);

/// A shore field bound to the exact terrain plan used to prepare the simulation.
#[derive(Clone, Copy)]
pub struct ShoreField<'a> {
    pub(crate) bytes: &'a [u8],
    pub(crate) document_hash: forge_core::ContentHash,
    pub(crate) derived: bool,
    pub(crate) surface_package_sha256: forge_core::ContentHash,
    simulation_params: &'a [u8; 48],
    domain: source_shore_field::Domain,
}

impl<'a> ShoreField<'a> {
    #[must_use]
    pub fn bytes(&self) -> &[u8] {
        self.bytes
    }

    #[must_use]
    pub const fn document_hash(&self) -> forge_core::ContentHash {
        self.document_hash
    }

    #[must_use]
    pub const fn surface_package_sha256(&self) -> forge_core::ContentHash {
        self.surface_package_sha256
    }

    #[must_use]
    pub const fn is_derived(&self) -> bool {
        self.derived
    }

    /// The exact simulation window associated with this prepared shore field.
    #[must_use]
    pub const fn simulation_parameters(&self) -> &'a [u8; 48] {
        self.simulation_params
    }

    #[must_use]
    pub const fn domain(&self) -> source_shore_field::Domain {
        self.domain
    }
}

impl PreparedShoreSim {
    /// Borrow the checked inputs for a coupled render-device simulation.
    #[must_use]
    pub fn gpu_inputs(&self) -> GpuInputs<'_> {
        self.0.gpu_inputs()
    }

    /// Relocate an unpublished simulation while retaining its original size,
    /// resolution and physics. This prepares one window only: following a
    /// camera or transferring the existing GPU state is a separate operation.
    pub fn with_simulation_center(mut self, center: [f64; 2]) -> Result<Self, String> {
        let input =
            Arc::get_mut(&mut self.0).ok_or("Cannot relocate a shared shoreline simulation")?;
        let parameters = centered_simulation_parameters(&input.simulation_params, center)?;
        if parameters != input.simulation_params {
            input.simulation_params = parameters;
            // An earlier GPU consumer may still hold the old generation token.
            input.generation = Arc::new(());
        }
        Ok(self)
    }

    pub fn new(
        package: &ShoreSimPackage,
        surface: &WaterSurfacePackage,
        document: &TerrainDocument,
        plan: &SampledTerrainPlan,
        source_heights: &[u8],
    ) -> Result<Self, String> {
        plan.require_document(document).map_err(|e| e.to_string())?;
        let frame = surface
            .file("water-frame.bin")
            .ok_or("Water frame absent")?;
        let water_level = crate::source_water_host::water_level_from_frame(frame)?;
        let domain = source_shore_field::Domain::for_terrain(document, water_level)?;
        let file = |name: &str| {
            package
                .file(name)
                .ok_or_else(|| format!("Pinned ShoreSim file missing: {name}"))
        };
        if document
            .sampled_source
            .as_ref()
            .is_none_or(|source| source.samples_sha256 != bytes_sha256(source_heights))
        {
            return Err("ShoreSim terrain differs from the editable source".into());
        }
        for (shore_name, surface_name) in [
            ("shore-field.rgba32.bin", "shore-field.rgba32.bin"),
            ("shore-params.bin", "uniform-shoreP.bin"),
            ("simulation-params.bin", "uniform-shoreSimP.bin"),
        ] {
            if Some(file(shore_name)?) != surface.file(surface_name) {
                return Err(format!("ShoreSim and optical surface differ: {shore_name}"));
            }
        }
        let source = std::str::from_utf8(file("kernel.wgsl")?).map_err(|e| e.to_string())?;
        if source.matches(HASH_DEFINITION).count() != 1 {
            return Err("Original ShoreSim hash definition changed".into());
        }
        let mut kernel = source.replace(HASH_DEFINITION, SHORE_HASH_SOURCE);
        kernel = domain.prepare_water_shader(&kernel, document)?;
        let corrections = Corrections::from_document(document)?;
        let edited = !document.height_deltas.is_empty() || corrections.elevation != 0.0;
        let shore_derived = edited
            || file("heights.f32.bin")? != source_heights
            || domain != source_shore_field::Domain::REFERENCE;
        if edited {
            kernel = corrections.shader_existing(&kernel)?;
        }
        let shore = if shore_derived {
            source_shore_field::bake_in_domain(domain, |x, z| plan.height_at(x, z))
                .map_err(|error| error.to_string())?
                .into_iter()
                .flat_map(f32::to_le_bytes)
                .collect()
        } else {
            file("shore-field.rgba32.bin")?.to_vec()
        };
        let lace = (0..10)
            .map(|mip| file(&format!("lace-m{mip}.rgba8.bin")).map(<[u8]>::to_vec))
            .collect::<Result<Vec<_>, _>>()?;
        let raster = plan.raster().source();
        let layout = TerrainTextureLayout {
            heights: [raster.width, raster.height],
            shore_side: u32::try_from(domain.side()).map_err(|_| "Shore-field side overflow")?,
            deltas: [corrections.width, corrections.height],
        };
        layout.validate()?;
        #[allow(clippy::cast_possible_truncation)] // The frame stores this value as f32.
        let water_level_f32 = water_level as f32;
        Ok(Self(Arc::new(Input {
            generation: Arc::new(()),
            kernel,
            heights: source_heights.to_vec(),
            shore,
            domain,
            layout,
            water_level: water_level_f32,
            direction: file("shore-direction.rgba32.bin")?
                .try_into()
                .map_err(|_| "Shore direction size mismatch")?,
            lace,
            simulation_params: file("simulation-params.bin")?
                .try_into()
                .map_err(|_| "Shore simulation parameter size mismatch")?,
            terrain_params: file("terrain-params.bin")?
                .try_into()
                .map_err(|_| "Shore terrain parameter size mismatch")?,
            shore_params: file("shore-params.bin")?
                .try_into()
                .map_err(|_| "Shore parameter size mismatch")?,
            corrections,
            document_hash: plan.document_hash(),
            shore_derived,
            surface_package_sha256: surface.identity(),
        })))
    }

    #[must_use]
    pub fn shore_field(&self) -> ShoreField<'_> {
        ShoreField {
            bytes: &self.0.shore,
            document_hash: self.0.document_hash,
            derived: self.0.shore_derived,
            surface_package_sha256: self.0.surface_package_sha256,
            simulation_params: &self.0.simulation_params,
            domain: self.0.domain,
        }
    }

    pub(crate) fn corrections(&self) -> &Corrections {
        &self.0.corrections
    }

    pub(crate) fn texture_layout(&self) -> TerrainTextureLayout {
        self.0.layout
    }

    /// Borrow the same admitted terrain and shore inputs for coupled effects.
    pub(crate) fn breaker_file(&self, name: &str) -> Option<&[u8]> {
        match name {
            "heights.f32.bin" => Some(&self.0.heights),
            "shore-field.rgba32.bin" => Some(&self.0.shore),
            "terrain-params.bin" => Some(&self.0.terrain_params),
            "shore-params.bin" => Some(&self.0.shore_params),
            "shore-sim-params.bin" => Some(&self.0.simulation_params),
            "authored-height-deltas.bin" => Some(&self.0.corrections.bytes),
            _ => None,
        }
    }

    /// Publish a fresh output generation; old image handles remain valid until consumers replace them.
    pub fn stage(self, world: &mut World) -> Result<Targets, String> {
        if !world.contains_resource::<Registered>() {
            return Err("ShoreSim renderer not registered".into());
        }
        let mut image = Image::new_fill(
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
            wgpu::TextureDimension::D2,
            &[0; 8],
            wgpu::TextureFormat::Rgba16Float,
            RenderAssetUsages::RENDER_WORLD,
        );
        image.texture_descriptor.usage = wgpu::TextureUsages::TEXTURE_BINDING
            | wgpu::TextureUsages::COPY_DST
            | wgpu::TextureUsages::COPY_SRC;
        let state = world.resource_mut::<Assets<Image>>().add(image);
        let mut lace = Vec::new();
        for mip in &self.0.lace {
            lace.extend_from_slice(mip);
        }
        let lace =
            world
                .resource_mut::<Assets<Image>>()
                .add(crate::source_vegetation::textures::mipped(
                    crate::source_vegetation::textures::Layout {
                        width: SHORE_SIDE,
                        height: SHORE_SIDE,
                        mips: 10,
                    },
                    &lace,
                )?);
        let params = std::array::from_fn(|row| {
            UVec4::from_array(std::array::from_fn(|column| {
                let offset = row * 16 + column * 4;
                u32::from_le_bytes(
                    self.0.simulation_params[offset..offset + 4]
                        .try_into()
                        .expect("fixed ShoreSim parameter size"),
                )
            }))
        });
        let targets = Targets {
            state,
            lace,
            params,
            document_hash: self.0.document_hash,
            surface_package_sha256: self.0.surface_package_sha256,
            completed_steps: Arc::new(AtomicU32::new(0)),
            ready: Arc::new(AtomicBool::new(false)),
            error: Arc::new(Mutex::new(None)),
        };
        world.insert_resource(StagedInput(self.0));
        world.insert_resource(targets.clone());
        Ok(targets)
    }
}

#[allow(
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss,
    reason = "The source GPU uses f32 coordinates, checked for cell-level precision"
)]
fn centered_simulation_parameters(source: &[u8; 48], center: [f64; 2]) -> Result<[u8; 48], String> {
    let size = f32::from_le_bytes(source[8..12].try_into().expect("fixed parameter size"));
    let cell = size / STATE_SIDE as f32;
    if !size.is_finite()
        || size <= 0.0
        || !cell.is_normal()
        || center.iter().any(|value| !value.is_finite())
    {
        return Err("Invalid shoreline simulation window".into());
    }
    let mut result = *source;
    for (axis, coordinate) in center.into_iter().enumerate() {
        let min = (coordinate - f64::from(size) * 0.5) as f32;
        let max = min + size;
        if !min.is_finite() || !max.is_finite() || min + cell <= min || max - cell >= max {
            return Err("Shoreline simulation window exceeds GPU precision".into());
        }
        result[axis * 4..axis * 4 + 4].copy_from_slice(&min.to_le_bytes());
    }
    Ok(result)
}

#[cfg(test)]
mod window_tests {
    use super::centered_simulation_parameters;

    fn reference() -> [u8; 48] {
        let values = [
            -180.0_f32, -215.0, 380.0, 28.0, 4.5, 2.6, 5.0, 1.0, 0.02, -0.0, 0.0, 0.0,
        ];
        let mut bytes = [0; 48];
        for (chunk, value) in bytes.as_chunks_mut::<4>().0.iter_mut().zip(values) {
            chunk.copy_from_slice(&value.to_le_bytes());
        }
        bytes
    }

    #[test]
    fn relocated_window_preserves_reference_resolution_and_other_parameters() {
        let original = reference();
        assert_eq!(
            centered_simulation_parameters(&original, [10.0, -25.0]).unwrap(),
            original
        );
        let moved = centered_simulation_parameters(&original, [550.0, -300.0]).unwrap();
        assert_eq!(&moved[0..4], &360.0_f32.to_le_bytes());
        assert_eq!(&moved[4..8], &(-490.0_f32).to_le_bytes());
        assert_eq!(&moved[8..], &original[8..]);
    }

    #[test]
    fn relocated_window_rejects_invalid_or_imprecise_coordinates() {
        let original = reference();
        assert!(centered_simulation_parameters(&original, [f64::NAN, 0.0]).is_err());
        assert!(centered_simulation_parameters(&original, [1.0e30, 0.0]).is_err());
        let mut invalid = original;
        invalid[8..12].copy_from_slice(&0.0_f32.to_le_bytes());
        assert!(centered_simulation_parameters(&invalid, [0.0, 0.0]).is_err());
    }
}

#[derive(Resource, Clone, ExtractResource)]
struct StagedInput(Arc<Input>);

#[derive(Resource, Clone, ExtractResource)]
pub struct Targets {
    pub state: Handle<Image>,
    pub lace: Handle<Image>,
    pub params: [UVec4; 3],
    /// Authored terrain revision used to derive this generation.
    pub document_hash: forge_core::ContentHash,
    /// Full admitted surface package identity used for this generation.
    pub surface_package_sha256: forge_core::ContentHash,
    pub completed_steps: Arc<AtomicU32>,
    pub ready: Arc<AtomicBool>,
    pub(crate) error: Arc<Mutex<Option<String>>>,
}

impl Targets {
    #[must_use]
    pub fn error(&self) -> Option<String> {
        self.error.lock().expect("shore error mutex").clone()
    }
}

#[derive(Resource)]
struct Registered;

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Registered>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Image>>() {
        return Err("ShoreSim requires Bevy's active render device".into());
    }
    app.add_plugins((
        ExtractResourcePlugin::<StagedInput>::default(),
        ExtractResourcePlugin::<Targets>::default(),
    ));
    app.sub_app_mut(RenderApp).add_systems(
        RenderGraph,
        compute
            .in_set(ShoreComputeSet)
            .after(crate::source_water_schedule::OceanComputeSet)
            .before(camera_driver)
            .run_if(resource_exists::<StagedInput>)
            .run_if(resource_exists::<Targets>)
            .run_if(resource_exists::<source_ocean::Targets>)
            .run_if(resource_exists::<source_ocean::Clock>),
    );
    app.insert_resource(Registered);
    Ok(())
}

/// Previously validated source inputs for the common shore GPU simulation.
pub struct GpuInputs<'a> {
    /// Identity of the admitted preparation; absent for standalone reference probes.
    pub generation: Option<Arc<()>>,
    pub kernel: &'a str,
    pub heights: &'a [u8],
    pub shore: &'a [u8],
    pub direction: &'a [u8],
    pub lace: [&'a [u8]; 10],
    pub simulation_params: &'a [u8],
    pub terrain_params: &'a [u8],
    pub shore_params: &'a [u8],
    pub deltas: &'a [u8],
    pub layout: TerrainTextureLayout,
    pub water_level: f32,
}

impl GpuInputs<'_> {
    pub fn validate_for_device(&self, limit: u32) -> Result<(), String> {
        self.layout.validate_device(limit)?;
        if limit < STATE_SIDE || limit < SHORE_SIDE || !self.water_level.is_finite() {
            return Err("Shore simulation exceeds the render-device limits".into());
        }
        for (bytes, expected) in [
            (
                self.heights,
                TerrainTextureLayout::byte_count(self.layout.heights, 4)?,
            ),
            (
                self.shore,
                TerrainTextureLayout::byte_count([self.layout.shore_side; 2], 16)?,
            ),
            (
                self.deltas,
                TerrainTextureLayout::byte_count(self.layout.deltas, 4)?,
            ),
            (self.direction, 16),
            (self.simulation_params, 48),
            (self.terrain_params, 32),
            (self.shore_params, 64),
        ] {
            if bytes.len() != expected {
                return Err("Shore simulation input size differs from its admitted layout".into());
            }
        }
        for (mip, bytes) in self.lace.iter().enumerate() {
            let side = SHORE_SIDE >> mip;
            if bytes.len() != TerrainTextureLayout::byte_count([side; 2], 4)? {
                return Err("Shore simulation lace mip size differs".into());
            }
        }
        if self.kernel.trim().is_empty() {
            return Err("Shore simulation shader is empty".into());
        }
        Ok(())
    }
}

impl Input {
    fn gpu_inputs(&self) -> GpuInputs<'_> {
        GpuInputs {
            generation: Some(self.generation.clone()),
            kernel: &self.kernel,
            heights: &self.heights,
            shore: &self.shore,
            direction: &self.direction,
            lace: std::array::from_fn(|index| self.lace[index].as_slice()),
            simulation_params: &self.simulation_params,
            terrain_params: &self.terrain_params,
            shore_params: &self.shore_params,
            deltas: &self.corrections.bytes,
            layout: self.layout,
            water_level: self.water_level,
        }
    }
}

/// GPU state shared by the resident host and coupled water simulations.
#[derive(Clone)]
pub struct GpuSimulation {
    generation: Option<Arc<()>>,
    state_a: wgpu::Texture,
    state_b: wgpu::Texture,
    deposit: wgpu::Buffer,
    pipeline: wgpu::ComputePipeline,
    group1: wgpu::BindGroup,
    layout0: wgpu::BindGroupLayout,
    samplers: Vec<wgpu::Sampler>,
    water_level: f32,
}

/// Ordered ownership of a resident shore simulation for coupled water steps.
pub struct OrderedSimulation {
    gpu: GpuSimulation,
    completed_step: u32,
}

impl OrderedSimulation {
    /// Adopt an existing state with its actual last completed simulation step.
    #[must_use]
    pub const fn new(gpu: GpuSimulation, completed_step: u32) -> Self {
        Self {
            gpu,
            completed_step,
        }
    }

    #[must_use]
    pub const fn gpu(&self) -> &GpuSimulation {
        &self.gpu
    }

    /// Encode the common source water order once for the next consecutive step.
    pub fn encode_water_step(
        &mut self,
        resources: &crate::source_breaker_gpu::Resources,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        crest: &crate::source_breaker_gpu::CrestFrame<'_>,
        step: u32,
    ) -> Result<(wgpu::BindGroup, wgpu::BindGroup), String> {
        if self.completed_step.checked_add(1) != Some(step) {
            return Err("Coupled shore step is not the next consecutive step".into());
        }
        let (frame, spray) = resources
            .encode_water_step(device, encoder, crest, Some(&self.gpu), None)
            .map_err(str::to_owned)?;
        let spray = spray.ok_or("Coupled spray pass was not encoded")?;
        self.completed_step = step;
        Ok((frame, spray))
    }

    /// Publish the current state to the existing shore texture.
    pub fn publish(&self, encoder: &mut wgpu::CommandEncoder, texture: &wgpu::Texture) {
        self.gpu.publish(encoder, texture);
    }
}

#[allow(clippy::too_many_lines)]
pub fn initialize_gpu(
    input: &GpuInputs<'_>,
    device: &wgpu::Device,
    queue: &RenderQueue,
    shared_deposit: Option<wgpu::Buffer>,
) -> Result<GpuSimulation, String> {
    input.validate_for_device(device.limits().max_texture_dimension_2d)?;
    if shared_deposit.as_ref().is_some_and(|buffer| {
        buffer.size() != u64::from(STATE_SIDE * STATE_SIDE * 4)
            || !buffer.usage().contains(wgpu::BufferUsages::STORAGE)
    }) {
        return Err("Invalid shared shore deposit buffer".into());
    }
    let texture = |size: [u32; 2], format, mips, usage| {
        device.create_texture(&wgpu::TextureDescriptor {
            label: Some("Source ShoreSim"),
            size: wgpu::Extent3d {
                width: size[0],
                height: size[1],
                depth_or_array_layers: 1,
            },
            mip_level_count: mips,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format,
            usage,
            view_formats: &[],
        })
    };
    let sampled = wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::COPY_DST;
    let state_a = texture(
        [STATE_SIDE; 2],
        wgpu::TextureFormat::Rgba16Float,
        1,
        sampled | wgpu::TextureUsages::COPY_SRC,
    );
    let state_b = texture(
        [STATE_SIDE; 2],
        wgpu::TextureFormat::Rgba16Float,
        1,
        wgpu::TextureUsages::STORAGE_BINDING | wgpu::TextureUsages::COPY_SRC,
    );
    let height = texture(
        input.layout.heights,
        wgpu::TextureFormat::R32Float,
        1,
        sampled,
    );
    let shore = texture(
        [input.layout.shore_side; 2],
        wgpu::TextureFormat::Rgba32Float,
        1,
        sampled,
    );
    let direction = texture([1, 1], wgpu::TextureFormat::Rgba32Float, 1, sampled);
    let lace = texture(
        [SHORE_SIDE; 2],
        wgpu::TextureFormat::Rgba8Unorm,
        10,
        sampled,
    );
    let dummy = texture([1, 1], wgpu::TextureFormat::Rgba8Unorm, 1, sampled);
    let deltas = texture(
        input.layout.deltas,
        wgpu::TextureFormat::R32Float,
        1,
        sampled,
    );
    let upload = |tex: &wgpu::Texture, mip, size: [u32; 2], bpp, data: &[u8]| {
        queue.write_texture(
            wgpu::TexelCopyTextureInfo {
                texture: tex,
                mip_level: mip,
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            data,
            wgpu::TexelCopyBufferLayout {
                offset: 0,
                bytes_per_row: Some(size[0] * bpp),
                rows_per_image: Some(size[1]),
            },
            wgpu::Extent3d {
                width: size[0],
                height: size[1],
                depth_or_array_layers: 1,
            },
        );
    };
    upload(
        &state_a,
        0,
        [STATE_SIDE; 2],
        8,
        &vec![0; (STATE_SIDE * STATE_SIDE * 8) as usize],
    );
    upload(&height, 0, input.layout.heights, 4, input.heights);
    upload(&shore, 0, [input.layout.shore_side; 2], 16, input.shore);
    upload(&direction, 0, [1, 1], 16, input.direction);
    upload(&deltas, 0, input.layout.deltas, 4, input.deltas);
    for (mip, data) in (0_u32..).zip(&input.lace) {
        upload(&lace, mip, [SHORE_SIDE >> mip; 2], 4, data);
    }
    let uniform = |bytes: &[u8]| {
        device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("ShoreSim source parameters"),
            contents: bytes,
            usage: wgpu::BufferUsages::UNIFORM,
        })
    };
    let simulation_params = uniform(input.simulation_params);
    let terrain_params = uniform(input.terrain_params);
    let shore_params = uniform(input.shore_params);
    let deposit = shared_deposit.unwrap_or_else(|| {
        device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("ShoreSim zero deposition"),
            contents: &vec![0; (STATE_SIDE * STATE_SIDE * 4) as usize],
            usage: wgpu::BufferUsages::STORAGE | wgpu::BufferUsages::COPY_SRC,
        })
    });
    let ub = wgpu::BindingType::Buffer {
        ty: wgpu::BufferBindingType::Uniform,
        has_dynamic_offset: false,
        min_binding_size: None,
    };
    let tex = |filterable| wgpu::BindingType::Texture {
        sample_type: wgpu::TextureSampleType::Float { filterable },
        view_dimension: wgpu::TextureViewDimension::D2,
        multisampled: false,
    };
    let entry = |binding, ty| wgpu::BindGroupLayoutEntry {
        binding,
        visibility: wgpu::ShaderStages::COMPUTE,
        ty,
        count: None,
    };
    let mut e0 = vec![entry(0, ub)];
    for i in 1..10 {
        e0.push(entry(
            i,
            wgpu::BindingType::Sampler(if i == 9 {
                wgpu::SamplerBindingType::Comparison
            } else if i >= 7 {
                wgpu::SamplerBindingType::NonFiltering
            } else {
                wgpu::SamplerBindingType::Filtering
            }),
        ));
    }
    let layout0 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("ShoreSim frame"),
        entries: &e0,
    });
    let e1: Vec<_> = (0..15)
        .map(|i| {
            entry(
                i,
                match i {
                    2 | 9 | 11 => ub,
                    12 => wgpu::BindingType::StorageTexture {
                        access: wgpu::StorageTextureAccess::WriteOnly,
                        format: wgpu::TextureFormat::Rgba16Float,
                        view_dimension: wgpu::TextureViewDimension::D2,
                    },
                    13 => wgpu::BindingType::Buffer {
                        ty: wgpu::BufferBindingType::Storage { read_only: false },
                        has_dynamic_offset: false,
                        min_binding_size: None,
                    },
                    3 | 6 | 7 | 10 => tex(false),
                    _ => tex(true),
                },
            )
        })
        .collect();
    let layout1 = device.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
        label: Some("ShoreSim resources"),
        entries: &e1,
    });
    let layout = device.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
        label: Some("ShoreSim"),
        bind_group_layouts: &[Some(&layout0), Some(&layout1)],
        immediate_size: 0,
    });
    let shader = device.create_shader_module(wgpu::ShaderModuleDescriptor {
        label: Some("Source ShoreSim source"),
        source: wgpu::ShaderSource::Wgsl(input.kernel.into()),
    });
    let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
        label: Some("ShoreSim"),
        layout: Some(&layout),
        module: &shader,
        entry_point: Some("main"),
        compilation_options: default(),
        cache: None,
    });
    let samplers: Vec<_> = (1..10)
        .map(|i| {
            let address = match i {
                1 | 4 | 6 | 8 => wgpu::AddressMode::Repeat,
                3 => wgpu::AddressMode::MirrorRepeat,
                _ => wgpu::AddressMode::ClampToEdge,
            };
            let filter = if i >= 7 {
                wgpu::FilterMode::Nearest
            } else {
                wgpu::FilterMode::Linear
            };
            device.create_sampler(&wgpu::SamplerDescriptor {
                address_mode_u: address,
                address_mode_v: address,
                address_mode_w: address,
                min_filter: filter,
                mag_filter: filter,
                mipmap_filter: if i >= 7 {
                    wgpu::MipmapFilterMode::Nearest
                } else {
                    wgpu::MipmapFilterMode::Linear
                },
                anisotropy_clamp: match i {
                    4 | 5 => 8,
                    6 => 4,
                    _ => 1,
                },
                compare: if i == 9 {
                    Some(wgpu::CompareFunction::LessEqual)
                } else {
                    None
                },
                ..default()
            })
        })
        .collect();
    let views: Vec<_> = [
        &state_a, &lace, &height, &dummy, &shore, &direction, &state_b, &deltas,
    ]
    .into_iter()
    .map(|texture| texture.create_view(&default()))
    .collect();
    let view = |i| wgpu::BindingResource::TextureView(&views[i]);
    let resources = [
        view(0),
        view(1),
        simulation_params.as_entire_binding(),
        view(2),
        view(3),
        view(3),
        view(7),
        view(4),
        view(3),
        terrain_params.as_entire_binding(),
        view(5),
        shore_params.as_entire_binding(),
        view(6),
        deposit.as_entire_binding(),
        view(1),
    ];
    let entries: Vec<_> = resources
        .into_iter()
        .zip(0_u32..)
        .map(|(resource, binding)| wgpu::BindGroupEntry { binding, resource })
        .collect();
    let group1 = device.create_bind_group(&wgpu::BindGroupDescriptor {
        label: Some("ShoreSim resources"),
        layout: &layout1,
        entries: &entries,
    });
    Ok(GpuSimulation {
        generation: input.generation.clone(),
        state_a,
        state_b,
        deposit,
        pipeline,
        group1,
        layout0,
        samplers,
        water_level: input.water_level,
    })
}

impl GpuSimulation {
    pub(crate) fn belongs_to(&self, prepared: &PreparedShoreSim) -> bool {
        self.generation
            .as_ref()
            .is_some_and(|generation| Arc::ptr_eq(generation, &prepared.0.generation))
    }

    #[must_use]
    pub const fn state_texture(&self) -> &wgpu::Texture {
        &self.state_a
    }

    #[must_use]
    pub const fn deposit_buffer(&self) -> &wgpu::Buffer {
        &self.deposit
    }

    fn encode_step(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        step: u32,
        time: f64,
        dt: f32,
    ) {
        let mut frame = [0_u8; 720];
        frame[576..580].copy_from_slice(&step.to_le_bytes());
        #[allow(clippy::cast_possible_truncation)]
        frame[580..584].copy_from_slice(&(time as f32).to_le_bytes());
        frame[584..588].copy_from_slice(&dt.to_le_bytes());
        frame[588..592].copy_from_slice(&self.water_level.to_le_bytes());
        self.encode_frame(device, encoder, &frame);
    }

    /// Encode one already packed source frame with the caller's ordered timeline.
    pub fn encode_frame(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        frame: &[u8],
    ) {
        let frame_buffer = device.create_buffer_init(&wgpu::util::BufferInitDescriptor {
            label: Some("ShoreSim source frame"),
            contents: frame,
            usage: wgpu::BufferUsages::UNIFORM,
        });
        self.encode_frame_buffer(device, encoder, &frame_buffer);
    }

    /// Encode using a frame uniform shared with other ordered water passes.
    pub(crate) fn encode_frame_buffer(
        &self,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        frame_buffer: &wgpu::Buffer,
    ) {
        let mut entries = vec![wgpu::BindGroupEntry {
            binding: 0,
            resource: frame_buffer.as_entire_binding(),
        }];
        for (index, sampler) in self.samplers.iter().enumerate() {
            entries.push(wgpu::BindGroupEntry {
                binding: u32::try_from(index + 1).expect("fixed ShoreSim sampler count"),
                resource: wgpu::BindingResource::Sampler(sampler),
            });
        }
        let group0 = device.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some("ShoreSim source frame"),
            layout: &self.layout0,
            entries: &entries,
        });
        {
            let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
                label: Some("ShoreSim source step"),
                ..default()
            });
            pass.set_pipeline(&self.pipeline);
            pass.set_bind_group(0, &group0, &[]);
            pass.set_bind_group(1, &self.group1, &[]);
            pass.dispatch_workgroups(96, 96, 1);
        }
        encoder.copy_texture_to_texture(
            self.state_b.as_image_copy(),
            self.state_a.as_image_copy(),
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
        );
    }

    pub fn publish(&self, encoder: &mut wgpu::CommandEncoder, texture: &wgpu::Texture) {
        encoder.copy_texture_to_texture(
            self.state_a.as_image_copy(),
            texture.as_image_copy(),
            wgpu::Extent3d {
                width: STATE_SIDE,
                height: STATE_SIDE,
                depth_or_array_layers: 1,
            },
        );
    }
}

#[derive(Default)]
struct Execution {
    generation: Option<Arc<AtomicBool>>,
    simulation: Option<GpuSimulation>,
    coupled: Option<crate::source_water_host::CoupledBreakerGpu>,
    next_step: u32,
    time: f64,
}

/// Hand off the resident state exactly once, then execute the common GPU
/// sequence with the same camera, ocean frame and adaptive feedback.
#[allow(clippy::too_many_arguments, clippy::too_many_lines)]
fn encode_coupled(
    context: &mut RenderContext,
    device: &RenderDevice,
    queue: &RenderQueue,
    buffers: &RenderAssets<GpuShaderBuffer>,
    images: &RenderAssets<GpuImage>,
    ocean: &source_ocean::Targets,
    frame: source_ocean::live_clock::LiveStep,
    shore: Option<&GpuSimulation>,
    last_step: u32,
    coupled: &mut Option<crate::source_water_host::CoupledBreakerGpu>,
    effects: &crate::source_breaker_resident::Targets,
    feedback: &crate::source_spray_feedback::LiveFeedback,
) -> Result<bool, String> {
    if !effects.camera_ready.load(Ordering::Acquire)
        || effects.generation != effects.current_generation.load(Ordering::Acquire)
    {
        return Ok(false);
    }
    let gpu: Vec<_> = effects
        .buffers
        .iter()
        .filter_map(|handle| buffers.get(handle))
        .collect();
    let Some(displacement) = images.get(&ocean.images[0]) else {
        return Ok(false);
    };
    let Some(packet) = buffers.get(feedback.packet()) else {
        return Ok(false);
    };
    if gpu.len() != effects.buffers.len() {
        return Ok(false);
    }
    let Some(cutoff) = buffers.get(&effects.shadow_cutoff) else {
        return Ok(false);
    };
    if coupled.is_none() {
        let resident = shore.ok_or("Resident shore state absent at breaker handoff")?;
        *coupled = Some(crate::source_water_host::CoupledBreakerGpu::adopt(
            device,
            queue,
            &effects.input,
            resident,
            last_step,
        )?);
    }
    let coupled = coupled.as_mut().expect("coupled handoff");
    if !coupled.uses_input(&effects.input) {
        coupled.replace_stations(
            device,
            context.command_encoder(),
            &gpu[4].buffer,
            &cutoff.buffer,
            &effects.input,
        )?;
    }
    let mut source_frame = *effects.frame.lock().expect("breaker frame mutex");
    crate::source_water_frame::write_time(&mut source_frame, frame.step, frame.time, frame.dt);
    let frame_bytes: Vec<u8> = source_frame
        .iter()
        .flat_map(UVec4::to_array)
        .flat_map(u32::to_le_bytes)
        .collect();
    let mut ocean_bytes: Vec<u8> = ocean
        .parameters
        .iter()
        .flat_map(UVec4::to_array)
        .flat_map(u32::to_le_bytes)
        .collect();
    #[allow(clippy::cast_possible_truncation)]
    let time = frame.time as f32;
    ocean_bytes[212..216].copy_from_slice(&time.to_le_bytes());
    let camera = source_frame[32].to_array().map(f32::from_bits);
    let mut budget = effects.budget.lock().expect("budget mutex");
    #[allow(clippy::cast_possible_truncation)]
    let emitter = budget.value() as f32;
    let params = crate::source_breaker_params::BreakerParams {
        camera: [camera[0], camera[1], camera[2]],
        budget: emitter,
        ..default()
    }
    .encode()
    .map_err(str::to_owned)?;
    let spray = crate::source_breaker_params::spray_without_body(frame.step);
    let uniform = |label, data: &[u8]| {
        device
            .wgpu_device()
            .create_buffer_init(&wgpu::util::BufferInitDescriptor {
                label: Some(label),
                contents: data,
                usage: wgpu::BufferUsages::UNIFORM,
            })
    };
    let ocean_uniform = uniform("resident breaker ocean", &ocean_bytes);
    let params_uniform = uniform("resident breaker parameters", &params);
    let spray_uniform = uniform("resident spray parameters", &spray);
    let input = crate::source_water_host::CoupledBreakerFrame {
        frame: &frame_bytes,
        ocean: &ocean_uniform,
        parameters: &params_uniform,
        spray_parameters: &spray_uniform,
        displacement: &displacement.texture_view,
        particles: [
            &gpu[1].buffer,
            &gpu[2].buffer,
            &gpu[3].buffer,
            &gpu[4].buffer,
        ],
        output: &gpu[0].buffer,
        station_count: u32::try_from(effects.input.gpu_inputs().stations.len() / 16)
            .map_err(|e| e.to_string())?,
        emit: true,
    };
    coupled.encode(device.wgpu_device(), context.command_encoder(), &input)?;
    let latest = budget.latest();
    budget
        .after_emission(latest, false)
        .map_err(str::to_owned)?;
    budget.record_encoded(frame.step, frame.time);
    feedback.encode(
        device.wgpu_device(),
        context.command_encoder(),
        packet,
        &gpu[4].buffer,
        frame,
        latest.map(|v| v.0),
    );
    effects.completed_steps.store(frame.step, Ordering::Release);
    effects.gpu_ready.store(true, Ordering::Release);
    Ok(true)
}

#[allow(clippy::too_many_arguments)]
#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy render systems require owned system parameter wrappers."
)]
fn compute(
    mut context: RenderContext,
    staged: Res<StagedInput>,
    targets: Res<Targets>,
    ocean: Res<source_ocean::Targets>,
    clock: Res<source_ocean::Clock>,
    device: Res<RenderDevice>,
    queue: Res<RenderQueue>,
    images: Res<RenderAssets<GpuImage>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    effects: Option<Res<crate::source_breaker_resident::Targets>>,
    feedback: Option<Res<crate::source_spray_feedback::LiveFeedback>>,
    mut execution: Local<Execution>,
) {
    if !execution
        .generation
        .as_ref()
        .is_some_and(|generation| Arc::ptr_eq(generation, &targets.ready))
    {
        *execution = Execution {
            generation: Some(targets.ready.clone()),
            ..default()
        };
    }
    let Some(output) = images.get(&targets.state) else {
        return;
    };
    let Execution {
        simulation,
        coupled,
        next_step,
        time,
        ..
    } = &mut *execution;
    if simulation.is_none() && coupled.is_none() {
        match initialize_gpu(&staged.0.gpu_inputs(), device.wgpu_device(), &queue, None) {
            Ok(value) => *simulation = Some(value),
            Err(reason) => {
                error!(%reason, "Resident shore input refused");
                *targets.error.lock().expect("shore error mutex") = Some(reason);
                return;
            }
        }
    }
    let mut published = None;
    let mut dependent_step = None;
    if *next_step <= BOOTSTRAP_STEPS {
        let end = (*next_step + 4).min(BOOTSTRAP_STEPS + 1);
        for step in *next_step..end {
            if step > 0 {
                *time += 1.0 / 60.0;
                simulation.as_ref().expect("bootstrap shore").encode_step(
                    device.wgpu_device(),
                    context.command_encoder(),
                    step,
                    *time,
                    1.0 / 60.0,
                );
                published = Some(step);
            }
        }
        *next_step = end;
    } else if ocean.ready.load(Ordering::Acquire) {
        let completed = ocean.completed_steps.load(Ordering::Acquire);
        // The ocean advances while ShoreSim finishes its 120-step bootstrap.
        // Join the latest completed source frame rather than waiting forever
        // for an ocean step that has already passed.
        let frame = clock
            .encoded_for_dependent(completed)
            .map(|frame| (frame, true))
            .or_else(|| clock.completed(completed).map(|frame| (frame, false)));
        if completed >= *next_step
            && let Some((frame, waiting_for_shore)) = frame
        {
            let mut encoded = false;
            if let (Some(effects), Some(feedback)) = (&effects, &feedback)
                && (coupled.is_some() || completed == *next_step)
            {
                match encode_coupled(
                    &mut context,
                    &device,
                    &queue,
                    &buffers,
                    &images,
                    &ocean,
                    frame,
                    simulation.as_ref(),
                    next_step.saturating_sub(1),
                    coupled,
                    effects,
                    feedback,
                ) {
                    Ok(value) => encoded = value,
                    Err(error) => {
                        error!(%error, "Resident breaker handoff failed");
                        *effects.error.lock().expect("breaker error mutex") = Some(error);
                        return;
                    }
                }
            }
            if encoded {
                // The coupled owner now has the only active handle to this state.
                *simulation = None;
            } else if coupled.is_some()
                || ((effects.is_some() || feedback.is_some())
                    && (waiting_for_shore || completed == *next_step))
            {
                // Wait for the admitted effects and their camera/buffers. Never
                // resume the independent scheduler after ownership changed.
                return;
            } else {
                simulation.as_ref().expect("resident shore").encode_step(
                    device.wgpu_device(),
                    context.command_encoder(),
                    frame.step,
                    frame.time,
                    frame.dt,
                );
            }
            *time = frame.time;
            *next_step = completed.saturating_add(1);
            published = Some(frame.step);
            if waiting_for_shore {
                dependent_step = Some(frame.step);
            }
        }
    }
    if let Some(step) = published {
        if let Some(coupled) = coupled {
            coupled
                .shore()
                .publish(context.command_encoder(), &output.texture);
        } else {
            simulation
                .as_ref()
                .expect("resident shore")
                .publish(context.command_encoder(), &output.texture);
        }
        targets.completed_steps.store(step, Ordering::Release);
        if step >= BOOTSTRAP_STEPS {
            targets.ready.store(true, Ordering::Release);
        }
        if let Some(step) = dependent_step {
            clock
                .dependent_encoded(step)
                .expect("shore encoded the pending ocean frame");
        }
    }
}
