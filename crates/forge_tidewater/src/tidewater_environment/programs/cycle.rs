//! Ordered environment refresh on the renderer's resident resources.
use super::{
    Prepared,
    encode::{FrameBindings, SceneBindings},
};
use crate::tidewater_environment::controller::{Controller, Planned, Step};

#[derive(Clone, Copy)]
pub struct Resources<'a> {
    pub atmosphere: &'a wgpu::Buffer,
    /// Transmittance, sky LUT, cloud shadow, panorama, current cloud view.
    pub textures: [&'a wgpu::TextureView; 5],
    pub clouds: &'a wgpu::Buffer,
    pub sky: &'a wgpu::Buffer,
    pub source: &'a wgpu::Texture,
    pub filtered: [&'a wgpu::Texture; 2],
    pub sh: [&'a wgpu::Buffer; 2],
}

impl Prepared {
    /// Encode the complete plan in source order, preserving the displayed target.
    /// Returns a target to publish only if the plan reaches its final swap step.
    /// The caller must discard the encoder on error, and commit the controller
    /// only after accepting these commands. This is not a GPU completion fence.
    pub fn encode_cycle(
        &self,
        controller: &Controller,
        plan: &Planned,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        frame: FrameBindings<'_>,
        resources: Resources<'_>,
    ) -> Result<Option<u8>, String> {
        controller.validate(plan)?;
        if resources.filtered[0] == resources.filtered[1]
            || resources.sh[0] == resources.sh[1]
            || resources.filtered.contains(&resources.source)
        {
            return Err("Environment front/back resources alias".into());
        }
        let mut publication = None;
        for step in plan.steps() {
            let (name, bindings) = match *step {
                Step::Face(face) => (
                    format!("face-{face}"),
                    SceneBindings::Sky {
                        atmosphere: resources.atmosphere,
                        textures: resources.textures,
                        clouds: resources.clouds,
                        sky: resources.sky,
                        output: resources.source,
                    },
                ),
                Step::Mips => {
                    for name in ["mips-a", "mips-b"] {
                        self.encode(
                            name,
                            device,
                            encoder,
                            frame,
                            SceneBindings::Mips {
                                source: resources.source,
                            },
                        )?;
                    }
                    continue;
                }
                Step::Filter { target, level } => (
                    format!("filter-{target}-{level}"),
                    SceneBindings::Filter {
                        source: resources.source,
                        output: resources.filtered[usize::from(target)],
                        level: u32::from(level),
                    },
                ),
                Step::Sh { target } => (
                    format!("sh-{target}"),
                    SceneBindings::Sh {
                        source: resources.source,
                        output: resources.sh[usize::from(target)],
                    },
                ),
                Step::Publish { target } => {
                    publication = Some(target);
                    continue;
                }
            };
            self.encode(&name, device, encoder, frame, bindings)?;
        }
        Ok(publication)
    }
}
