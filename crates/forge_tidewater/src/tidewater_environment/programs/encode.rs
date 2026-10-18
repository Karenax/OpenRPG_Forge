//! Encode source kernels using borrowed resident resources on Bevy's device.
//! No texture replacement, queue submission, readback or publication is implicit.
use super::Prepared;

#[derive(Clone, Copy)]
pub struct FrameBindings<'a> {
    pub frame: &'a wgpu::Buffer,
    pub samplers: &'a [wgpu::Sampler],
}
#[derive(Clone, Copy)]
pub enum SceneBindings<'a> {
    Sky {
        atmosphere: &'a wgpu::Buffer,
        /// Transmittance, sky LUT, shadow, panorama, current cloud view.
        textures: [&'a wgpu::TextureView; 5],
        clouds: &'a wgpu::Buffer,
        sky: &'a wgpu::Buffer,
        output: &'a wgpu::Texture,
    },
    Mips {
        source: &'a wgpu::Texture,
    },
    Filter {
        source: &'a wgpu::Texture,
        output: &'a wgpu::Texture,
        level: u32,
    },
    Sh {
        source: &'a wgpu::Texture,
        output: &'a wgpu::Buffer,
    },
}

fn cube(texture: &wgpu::Texture, levels: u32, usage: wgpu::TextureUsages) -> Result<(), String> {
    if texture.size()
        != (wgpu::Extent3d {
            width: 128,
            height: 128,
            depth_or_array_layers: 6,
        })
        || texture.format() != wgpu::TextureFormat::Rgba16Float
        || texture.mip_level_count() != levels
        || texture.sample_count() != 1
        || !texture.usage().contains(usage)
    {
        return Err("Environment cube dimensions/format/usage differ".into());
    }
    Ok(())
}
fn view(
    texture: &wgpu::Texture,
    dimension: wgpu::TextureViewDimension,
    level: Option<u32>,
) -> wgpu::TextureView {
    texture.create_view(&wgpu::TextureViewDescriptor {
        dimension: Some(dimension),
        base_mip_level: level.unwrap_or(0),
        mip_level_count: level.map(|_| 1),
        ..Default::default()
    })
}
fn uniform(buffer: &wgpu::Buffer, size: u64) -> Result<(), String> {
    if buffer.size() != size || !buffer.usage().contains(wgpu::BufferUsages::UNIFORM) {
        return Err("Environment uniform buffer ABI differs".into());
    }
    Ok(())
}

impl Prepared {
    /// Encode one source phase in the caller's command encoder. Returning Ok
    /// acknowledges encoding only; the caller owns ordering and GPU completion.
    #[allow(
        clippy::too_many_lines,
        reason = "Keep the exact binding order and validation of all four source phases together"
    )]
    pub fn encode(
        &self,
        name: &str,
        device: &wgpu::Device,
        encoder: &mut wgpu::CommandEncoder,
        frame: FrameBindings<'_>,
        scene: SceneBindings<'_>,
    ) -> Result<(), String> {
        uniform(frame.frame, 720)?;
        if frame.samplers.len() != 9 {
            return Err("Nine source samplers required".into());
        }
        let program = self
            .programs
            .iter()
            .find(|program| program.name == name)
            .ok_or("Environment phase absent")?;
        let globals = [
            "frame",
            "smpLinearRepeat",
            "smpLinearClamp",
            "smpLinearMirror",
            "smpAnisoRepeat",
            "smpAnisoClamp",
            "smpAniso4Repeat",
            "smpNearestClamp",
            "smpNearestRepeat",
            "smpShadow",
        ];
        if program.layouts.len() != 2 || program.bindings[0] != globals {
            return Err("Environment global binding order differs".into());
        }
        let views;
        let expected: Vec<String>;
        let resources: Vec<wgpu::BindingResource<'_>>;
        match scene {
            SceneBindings::Sky {
                atmosphere,
                textures,
                clouds,
                sky,
                output,
            } => {
                if !name.starts_with("face-") {
                    return Err("Sky resources require a face kernel".into());
                }
                uniform(atmosphere, 64)?;
                uniform(clouds, 80)?;
                uniform(sky, 32)?;
                cube(output, 8, wgpu::TextureUsages::STORAGE_BINDING)?;
                views = vec![view(output, wgpu::TextureViewDimension::D2Array, Some(0))];
                expected = [
                    "atmosphereParams",
                    "atmosphereTransmittanceLUT",
                    "atmosphereSkyViewLUT",
                    "cloudsShadowMap",
                    "cloudsParams",
                    "cloudsPanorama",
                    "cloudsView",
                    "skyParams",
                    "outCube",
                ]
                .map(str::to_owned)
                .to_vec();
                resources = vec![
                    atmosphere.as_entire_binding(),
                    wgpu::BindingResource::TextureView(textures[0]),
                    wgpu::BindingResource::TextureView(textures[1]),
                    wgpu::BindingResource::TextureView(textures[2]),
                    clouds.as_entire_binding(),
                    wgpu::BindingResource::TextureView(textures[3]),
                    wgpu::BindingResource::TextureView(textures[4]),
                    sky.as_entire_binding(),
                    wgpu::BindingResource::TextureView(&views[0]),
                ];
            }
            SceneBindings::Mips { source } => {
                let (first, last) = match name {
                    "mips-a" => (0, 5),
                    "mips-b" => (5, 7),
                    _ => return Err("Mipmap resources require a mip kernel".into()),
                };
                cube(
                    source,
                    8,
                    wgpu::TextureUsages::TEXTURE_BINDING | wgpu::TextureUsages::STORAGE_BINDING,
                )?;
                views = (first..=last)
                    .map(|level| view(source, wgpu::TextureViewDimension::D2Array, Some(level)))
                    .collect::<Vec<_>>();
                expected = std::iter::once(format!("src{first}"))
                    .chain((first + 1..=last).map(|level| format!("out{level}")))
                    .collect();
                resources = views
                    .iter()
                    .map(wgpu::BindingResource::TextureView)
                    .collect();
            }
            SceneBindings::Filter {
                source,
                output,
                level,
            } => {
                if level >= 6
                    || !name.starts_with("filter-")
                    || !name.ends_with(&format!("-{level}"))
                    || source == output
                {
                    return Err("Invalid filter phase or aliased output".into());
                }
                cube(source, 8, wgpu::TextureUsages::TEXTURE_BINDING)?;
                cube(output, 6, wgpu::TextureUsages::STORAGE_BINDING)?;
                views = vec![
                    view(source, wgpu::TextureViewDimension::Cube, None),
                    view(output, wgpu::TextureViewDimension::D2Array, Some(level)),
                ];
                expected = vec!["srcCube".into(), "outCube".into()];
                resources = views
                    .iter()
                    .map(wgpu::BindingResource::TextureView)
                    .collect();
            }
            SceneBindings::Sh { source, output } => {
                if !name.starts_with("sh-")
                    || output.size() != 144
                    || !output.usage().contains(wgpu::BufferUsages::STORAGE)
                {
                    return Err("Invalid SH phase or output ABI".into());
                }
                cube(source, 8, wgpu::TextureUsages::TEXTURE_BINDING)?;
                views = vec![view(source, wgpu::TextureViewDimension::D2Array, None)];
                expected = vec!["srcCube".into(), "shOut".into()];
                resources = vec![
                    wgpu::BindingResource::TextureView(&views[0]),
                    output.as_entire_binding(),
                ];
            }
        }
        if program.bindings[1] != expected {
            return Err("Environment scene binding order differs".into());
        }
        let global_entries: Vec<_> = std::iter::once(wgpu::BindGroupEntry {
            binding: 0,
            resource: frame.frame.as_entire_binding(),
        })
        .chain(
            frame
                .samplers
                .iter()
                .zip(1..)
                .map(|(sampler, binding)| wgpu::BindGroupEntry {
                    binding,
                    resource: wgpu::BindingResource::Sampler(sampler),
                }),
        )
        .collect();
        let scene_entries: Vec<_> = resources
            .into_iter()
            .zip(0..)
            .map(|(resource, binding)| wgpu::BindGroupEntry { binding, resource })
            .collect();
        let gpu = device;
        let global = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some(name),
            layout: &program.layouts[0],
            entries: &global_entries,
        });
        let scene = gpu.create_bind_group(&wgpu::BindGroupDescriptor {
            label: Some(name),
            layout: &program.layouts[1],
            entries: &scene_entries,
        });
        let mut pass = encoder.begin_compute_pass(&wgpu::ComputePassDescriptor {
            label: Some(name),
            ..Default::default()
        });
        pass.set_pipeline(&program.pipeline);
        pass.set_bind_group(0, &global, &[]);
        pass.set_bind_group(1, &scene, &[]);
        let [x, y, z] = program.dispatch;
        pass.dispatch_workgroups(x, y, z);
        Ok(())
    }
}
