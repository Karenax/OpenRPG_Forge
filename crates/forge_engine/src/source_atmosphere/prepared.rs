//! One bounded set of atmosphere pipelines per render-system generation.
use super::{Input, KernelInput, NAMES, Targets};
use bevy::render::{render_resource as gpu, renderer::RenderDevice};
use std::sync::{Arc, atomic::AtomicBool};

pub(crate) struct Prepared {
    input: Arc<Input>,
    generation: Arc<AtomicBool>,
    pub pipelines: [gpu::ComputePipeline; 4],
    pub sampler: gpu::Sampler,
}

impl Prepared {
    fn current(&self, input: &KernelInput, targets: &Targets) -> bool {
        current(&self.input, &self.generation, input, targets)
    }
}

fn current(
    input: &Arc<Input>,
    generation: &Arc<AtomicBool>,
    next: &KernelInput,
    targets: &Targets,
) -> bool {
    Arc::ptr_eq(input, &next.0) && Arc::ptr_eq(generation, &targets.ready)
}

pub(super) fn get<'a>(
    cache: &'a mut Option<Prepared>,
    input: &KernelInput,
    targets: &Targets,
    device: &RenderDevice,
) -> &'a Prepared {
    if cache
        .as_ref()
        .is_none_or(|prepared| !prepared.current(input, targets))
    {
        let pipelines = std::array::from_fn(|index| {
            let shader = device.create_and_validate_shader_module(gpu::ShaderModuleDescriptor {
                label: Some(NAMES[index]),
                source: gpu::ShaderSource::Wgsl(input.0.shaders[index].as_str().into()),
            });
            device.create_compute_pipeline(&gpu::RawComputePipelineDescriptor {
                label: Some(NAMES[index]),
                layout: None,
                module: &shader,
                entry_point: Some("main"),
                compilation_options: gpu::PipelineCompilationOptions::default(),
                cache: None,
            })
        });
        let sampler = device.create_sampler(&gpu::SamplerDescriptor {
            mag_filter: gpu::FilterMode::Linear,
            min_filter: gpu::FilterMode::Linear,
            ..Default::default()
        });
        *cache = Some(Prepared {
            input: input.0.clone(),
            generation: targets.ready.clone(),
            pipelines,
            sampler,
        });
    }
    cache.as_ref().expect("Prepared atmosphere generation")
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::prelude::*;
    #[test]
    fn changing_sun_keeps_programs_but_replacing_inputs_or_generation_retires_them() {
        let input = KernelInput(Arc::new(
            Input::new(std::array::from_fn(|_| "test".into()), [0.0; 16]).unwrap(),
        ));
        let generation = Arc::new(AtomicBool::new(false));
        let mut targets = Targets {
            params: [Vec4::ZERO; 4],
            images: vec![],
            irradiance: Handle::default(),
            ready: generation.clone(),
        };
        assert!(current(&input.0, &generation, &input, &targets));
        targets.params[3] = Vec4::new(0.5, 0.75, 0.25, 0.0);
        assert!(current(&input.0, &generation, &input, &targets));
        let replacement = KernelInput(Arc::new(
            Input::new(std::array::from_fn(|_| "test".into()), [0.0; 16]).unwrap(),
        ));
        assert!(!current(&input.0, &generation, &replacement, &targets));
        targets.ready = Arc::new(AtomicBool::new(false));
        assert!(!current(&input.0, &generation, &input, &targets));
    }
}
