//! Verified source environment kernels, prepared on the scene's Bevy device.
//! Resource allocation, scheduling and publication remain explicit caller work.
pub mod cycle;
pub mod encode;
mod layout;
pub mod package;
use bevy::render::{render_resource::ComputePipeline, renderer::RenderDevice};
use serde_json::Value;
use std::collections::BTreeSet;

pub struct Input {
    kernels: Vec<Kernel>,
    pub uniforms: [Vec<u8>; 4],
}
pub struct Kernel {
    pub name: String,
    pub shader: String,
    pub dispatch: [u32; 3],
    pub groups: [Vec<Value>; 2],
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
    /// Admit the complete source algorithm input before exposing it to the renderer.
    /// Transport provenance is checked by the caller; no source schema or map is inferred.
    pub fn new(kernels: Vec<Kernel>, uniforms: [Vec<u8>; 4]) -> Result<Self, String> {
        let mut names: BTreeSet<String> = (0..6).map(|face| format!("face-{face}")).collect();
        names.extend(["mips-a".into(), "mips-b".into()]);
        for target in 0..2 {
            names.insert(format!("sh-{target}"));
            names.extend((0..6).map(|level| format!("filter-{target}-{level}")));
        }
        if kernels.len() != names.len()
            || uniforms.iter().map(Vec::len).collect::<Vec<_>>() != [720, 64, 80, 32]
        {
            return Err("Environment kernel inventory or uniform ABI differs".into());
        }
        for kernel in &kernels {
            if !names.remove(&kernel.name)
                || kernel.shader.trim().is_empty()
                || kernel.shader.len() > 1_000_000
            {
                return Err("Unknown, duplicate or invalid environment kernel".into());
            }
            let expected = if kernel.name.starts_with("face-") {
                [16, 16, 1]
            } else if kernel.name == "mips-a" {
                [4, 4, 6]
            } else if kernel.name == "mips-b" {
                [1, 1, 6]
            } else if kernel.name.starts_with("sh-") {
                [1, 1, 1]
            } else {
                let level: u32 = kernel
                    .name
                    .rsplit('-')
                    .next()
                    .ok_or("Filter level absent")?
                    .parse()
                    .map_err(|e| format!("Invalid filter level: {e}"))?;
                let groups = (128_u32 >> level).div_ceil(8);
                [groups, groups, 6]
            };
            if kernel.dispatch != expected {
                return Err("Environment dispatch differs from kernel".into());
            }
            for group in &kernel.groups {
                if group.is_empty() || group.len() > 10 {
                    return Err("Invalid environment binding group".into());
                }
                let mut bindings = BTreeSet::new();
                let mut labels = BTreeSet::new();
                for binding in group {
                    let entry = layout::entry(binding).map_err(|e| e.to_string())?;
                    let label = binding["name"].as_str().ok_or("Binding identity absent")?;
                    if label.is_empty() || !bindings.insert(entry.binding) || !labels.insert(label)
                    {
                        return Err("Duplicate environment binding".into());
                    }
                }
            }
        }
        Ok(Self { kernels, uniforms })
    }
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

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn inputs() -> (Vec<Kernel>, [Vec<u8>; 4]) {
        let mut inventory: Vec<(String, [u32; 3])> =
            (0..6).map(|i| (format!("face-{i}"), [16, 16, 1])).collect();
        inventory.extend([("mips-a".into(), [4, 4, 6]), ("mips-b".into(), [1, 1, 6])]);
        for target in 0..2 {
            inventory.push((format!("sh-{target}"), [1, 1, 1]));
            for (level, groups) in [16, 8, 4, 2, 1, 1].into_iter().enumerate() {
                inventory.push((format!("filter-{target}-{level}"), [groups, groups, 6]));
            }
        }
        let binding = json!({"binding":0,"name":"frame","layout":{"visibility":4,"buffer":{"type":"uniform"}}});
        (
            inventory
                .into_iter()
                .map(|(name, dispatch)| Kernel {
                    name,
                    dispatch,
                    shader: "@compute @workgroup_size(1) fn main() {}".into(),
                    groups: [vec![binding.clone()], vec![binding.clone()]],
                })
                .collect(),
            [vec![0; 720], vec![0; 64], vec![0; 80], vec![0; 32]],
        )
    }

    #[test]
    fn common_admission_keeps_all_kernels_and_rejects_malformed_execution_inputs() {
        let (kernels, uniforms) = inputs();
        assert_eq!(Input::new(kernels, uniforms).unwrap().kernels.len(), 22);
        for mutation in 0..8 {
            let (mut kernels, mut uniforms) = inputs();
            match mutation {
                0 => {
                    kernels.pop();
                }
                1 => kernels[1].name = kernels[0].name.clone(),
                2 => kernels[0].dispatch[0] = 0,
                3 => kernels[0].shader.clear(),
                4 => uniforms[0].pop().map(|_| ()).unwrap(),
                5 => kernels[0].groups[0].clear(),
                6 => {
                    let duplicate = kernels[1].groups[0][0].clone();
                    kernels[0].groups[0].push(duplicate);
                }
                _ => kernels[0].groups[0][0]["layout"]["visibility"] = json!(1),
            }
            assert!(
                Input::new(kernels, uniforms).is_err(),
                "mutation {mutation}"
            );
        }
    }
}
