//! Verified source environment kernels, prepared on the scene's Bevy device.
//! Resource allocation, scheduling and publication remain explicit caller work.
pub mod cycle;
pub mod encode;
mod layout;
use bevy::render::{render_resource::ComputePipeline, renderer::RenderDevice};
use forge_core::{ContentHash, bytes_sha256};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub struct Input {
    kernels: Vec<Kernel>,
    pub uniforms: [Vec<u8>; 4],
}
struct Kernel {
    name: String,
    shader: String,
    dispatch: [u32; 3],
    groups: [Vec<Value>; 2],
}
pub struct Program {
    pub name: String,
    pub dispatch: [u32; 3],
    pub pipeline: ComputePipeline,
    pub layouts: Vec<wgpu::BindGroupLayout>,
    /// Source binding identities, retained for binding scene-owned resources.
    pub bindings: [Vec<String>; 2],
}
pub struct Prepared {
    pub programs: Vec<Program>,
}

impl Input {
    /// Decode already acquired bytes without a second network transport.
    pub fn from_package(
        package: &forge_assets::tidewater_environment::programs::ProgramsPackage,
    ) -> Result<Self, String> {
        Self::from_acquisition(package.manifest(), package.pin(), |name| {
            package
                .file(name)
                .map(<[u8]>::to_vec)
                .ok_or_else(|| format!("Environment program absent: {name}"))
        })
    }
    /// Only read verified package bytes through the caller's transport. No paths,
    /// `AssetServer` fallback, source execution or replacement shaders are used.
    #[allow(
        clippy::too_many_lines,
        reason = "Validate the complete acquisition before exposing any GPU input"
    )]
    pub fn from_acquisition(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        if manifest.len() > 2_000_000 || bytes_sha256(manifest) != expected {
            return Err("Environment program manifest hash/size differs".into());
        }
        let meta: Value = serde_json::from_slice(manifest).map_err(|e| e.to_string())?;
        if meta["schema"] != "forge.tidewater.environment-programs"
            || meta["version"] != 1
            || meta["source_commit"] != "1438b1abfcaee3267092b75573014f4d9b4a983c"
        {
            return Err("Unsupported environment program acquisition".into());
        }
        let pins = meta["files"]
            .as_object()
            .ok_or("Environment file pins absent")?;
        if pins.len() != 27 {
            return Err("Environment requires 27 pinned payloads".into());
        }
        let mut files = BTreeMap::new();
        for (name, pin) in pins {
            if name.is_empty()
                || name == "."
                || name == ".."
                || !name
                    .bytes()
                    .all(|b| b.is_ascii_alphanumeric() || b"._-".contains(&b))
            {
                return Err("Environment payload path must be flat".into());
            }
            let expected: ContentHash = pin
                .as_str()
                .ok_or("Environment payload pin missing")?
                .parse()
                .map_err(|e| format!("Invalid environment hash: {e}"))?;
            let bytes = read(name)?;
            if bytes.len() > 1_000_000 || bytes_sha256(&bytes) != expected {
                return Err(format!("Environment payload differs: {name}"));
            }
            files.insert(name.as_str(), bytes);
        }
        let records = meta["kernels"]
            .as_array()
            .ok_or("Environment kernels absent")?;
        if records.len() != 22 {
            return Err("Environment requires 22 kernels".into());
        }
        let mut expected_names: BTreeSet<String> =
            (0..6).map(|face| format!("face-{face}")).collect();
        expected_names.extend(["mips-a".into(), "mips-b".into()]);
        for target in 0..2 {
            expected_names.insert(format!("sh-{target}"));
            expected_names.extend((0..6).map(|level| format!("filter-{target}-{level}")));
        }
        let mut kernels = Vec::new();
        for record in records {
            let name = record["name"].as_str().ok_or("Kernel name absent")?;
            if !expected_names.remove(name) {
                return Err(format!("Unknown or duplicate kernel: {name}"));
            }
            let dispatch: [u32; 3] =
                serde_json::from_value(record["dispatch"].clone()).map_err(|e| e.to_string())?;
            let expected_dispatch = if name.starts_with("face-") {
                [16, 16, 1]
            } else if name == "mips-a" {
                [4, 4, 6]
            } else if name == "mips-b" || name.starts_with("sh-") {
                [1, 1, if name == "mips-b" { 6 } else { 1 }]
            } else {
                let level: u32 = name
                    .rsplit('-')
                    .next()
                    .ok_or("Filter level absent")?
                    .parse()
                    .map_err(|e| format!("Invalid filter level: {e}"))?;
                let groups = (128_u32 >> level).div_ceil(8);
                [groups, groups, 6]
            };
            if dispatch != expected_dispatch {
                return Err("Environment dispatch differs from acquired kernel".into());
            }
            let shader_name = record["shader"].as_str().ok_or("Kernel shader absent")?;
            let shader = String::from_utf8(
                files
                    .get(shader_name)
                    .ok_or("Kernel shader not pinned")?
                    .clone(),
            )
            .map_err(|e| e.to_string())?;
            if shader.trim().is_empty() {
                return Err("Empty environment shader".into());
            }
            let groups: [Vec<Value>; 2] =
                serde_json::from_value(record["groups"].clone()).map_err(|e| e.to_string())?;
            for group in &groups {
                if group.is_empty() || group.len() > 10 {
                    return Err("Invalid environment binding group".into());
                }
                let mut bindings = BTreeSet::new();
                let mut names = BTreeSet::new();
                for binding in group {
                    let entry = layout::entry(binding).map_err(|e| e.to_string())?;
                    let label = binding["name"].as_str().ok_or("Binding identity absent")?;
                    if label.is_empty() || !bindings.insert(entry.binding) || !names.insert(label) {
                        return Err("Duplicate environment binding".into());
                    }
                }
            }
            kernels.push(Kernel {
                name: name.into(),
                shader,
                dispatch,
                groups,
            });
        }
        let uniforms = std::array::from_fn(|id| {
            files
                .get(format!("uniform-{id}.bin").as_str())
                .cloned()
                .unwrap_or_default()
        });
        if uniforms.iter().map(Vec::len).collect::<Vec<_>>() != [720, 64, 80, 32] {
            return Err("Environment uniform ABI differs".into());
        }
        Ok(Self { kernels, uniforms })
    }

    /// Compile exact shaders/layouts with asynchronous validation. Use the device
    /// owned by the scene; no auxiliary wgpu instance or queue is created.
    pub async fn prepare(&self, device: &RenderDevice) -> Result<Prepared, String> {
        let gpu = device.wgpu_device();
        let mut programs = Vec::new();
        for kernel in &self.kernels {
            let entries = kernel
                .groups
                .iter()
                .map(|group| {
                    group
                        .iter()
                        .map(layout::entry)
                        .collect::<Result<Vec<_>, _>>()
                })
                .collect::<Result<Vec<_>, _>>()
                .map_err(|e| e.to_string())?;
            let scope = gpu.push_error_scope(wgpu::ErrorFilter::Validation);
            let shader = device.create_and_validate_shader_module(wgpu::ShaderModuleDescriptor {
                label: Some(&kernel.name),
                source: wgpu::ShaderSource::Wgsl(kernel.shader.clone().into()),
            });
            let layouts: Vec<_> = entries
                .iter()
                .map(|entries| {
                    gpu.create_bind_group_layout(&wgpu::BindGroupLayoutDescriptor {
                        label: Some(&kernel.name),
                        entries,
                    })
                })
                .collect();
            let references: Vec<_> = layouts.iter().map(Some).collect();
            let layout = gpu.create_pipeline_layout(&wgpu::PipelineLayoutDescriptor {
                label: Some(&kernel.name),
                bind_group_layouts: &references,
                immediate_size: 0,
            });
            let pipeline = device.create_compute_pipeline(&wgpu::ComputePipelineDescriptor {
                label: Some(&kernel.name),
                layout: Some(&layout),
                module: &shader,
                entry_point: Some("main"),
                compilation_options: wgpu::PipelineCompilationOptions::default(),
                cache: None,
            });
            if let Some(error) = scope.pop().await {
                return Err(format!("Environment kernel {}: {error}", kernel.name));
            }
            let bindings = kernel.groups.clone().map(|group| {
                group
                    .into_iter()
                    .map(|binding| {
                        binding["name"]
                            .as_str()
                            .expect("validated binding identity")
                            .to_owned()
                    })
                    .collect()
            });
            programs.push(Program {
                name: kernel.name.clone(),
                dispatch: kernel.dispatch,
                pipeline,
                layouts,
                bindings,
            });
        }
        Ok(Prepared { programs })
    }
}
