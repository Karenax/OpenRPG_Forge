//! Exercise source carrier generation against a real Bevy prepass, then
//! sample the two outputs on a lit receiver before its PBR lighting.
//! This private scene does not qualify Studio placement or original lighting.
use bevy::{
    camera::{RenderTarget, ScalingMode},
    material::OpaqueRendererMethod,
    prelude::*,
    render::{
        RenderPlugin,
        gpu_readback::{Readback, ReadbackComplete},
        pipelined_rendering::PipelinedRenderingPlugin,
        render_resource::{TextureFormat, TextureUsages},
        renderer::{RenderDevice, RenderQueue},
        settings::{Backends, RenderCreation, WgpuFeatures, WgpuSettings},
        view::NoIndirectDrawing,
    },
    tasks::block_on,
    window::ExitCondition,
    winit::WinitPlugin,
};
use forge_engine::{
    source_decal_receiver::{SourceDecalPbrMaterial, SourceDecalReceiver},
    source_decal_view::{SourceDecalRaster, SourceDecalView, SourceDecalViewPlugin},
};
use std::{
    path::Path,
    time::{Duration, Instant},
};

const SIZE: u32 = 256;
#[derive(Component, Default)]
struct Pixels {
    data: Vec<u8>,
    received: u64,
}

fn reader(world: &mut World, handle: Handle<Image>) -> Entity {
    world
        .spawn((Readback::texture(handle), Pixels::default()))
        .observe(
            |event: On<ReadbackComplete>, mut query: Query<&mut Pixels>| {
                if let Ok(mut pixels) = query.get_mut(event.entity) {
                    pixels.data.clone_from(&event.data);
                    pixels.received += 1;
                }
            },
        )
        .id()
}

fn image(app: &mut App, format: TextureFormat, clear: [u8; 4]) -> Handle<Image> {
    let mut image = Image::new_target_texture(SIZE, SIZE, format, None);
    image.texture_descriptor.usage |= TextureUsages::COPY_SRC | TextureUsages::COPY_DST;
    image.data = Some(clear.repeat((SIZE * SIZE) as usize));
    app.world_mut().resource_mut::<Assets<Image>>().add(image)
}

fn setup(app: &mut App) -> (Entity, [Entity; 3]) {
    let binding = SourceDecalView::new(
        &mut app.world_mut().resource_mut::<Assets<Image>>(),
        UVec2::splat(SIZE),
    )
    .unwrap();
    let [color, normal] = binding.targets();
    let lit = image(app, TextureFormat::Rgba8UnormSrgb, [0; 4]);
    let world = app.world_mut();
    let receiver = world.resource_mut::<Assets<SourceDecalPbrMaterial>>().add(
        SourceDecalReceiver::material(
            StandardMaterial {
                base_color: Color::linear_rgb(0.65, 0.56, 0.43),
                perceptual_roughness: 1.0,
                opaque_render_method: OpaqueRendererMethod::Forward,
                ..default()
            },
            color.clone(),
            normal.clone(),
        )
        .expect("valid opaque receiver"),
    );
    let blocker = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: Color::linear_rgb(0.1, 0.1, 0.1),
            ..default()
        });
    let plane = world
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(3.0, 3.0, 0.01));
    let block = world
        .resource_mut::<Assets<Mesh>>()
        .add(Cuboid::new(1.5, 3.0, 0.01));
    world.spawn((
        Mesh3d(plane),
        MeshMaterial3d(receiver),
        Transform::from_xyz(0., 0., 0.02),
    ));
    world.spawn((
        Mesh3d(block),
        MeshMaterial3d(blocker),
        Transform::from_xyz(-0.75, 0., -0.02),
    ));
    let camera = world
        .spawn((
            Camera3d::default(),
            binding,
            NoIndirectDrawing,
            RenderTarget::Image(lit.clone().into()),
            Msaa::Off,
            Projection::Orthographic(OrthographicProjection {
                scaling_mode: ScalingMode::Fixed {
                    width: 3.,
                    height: 3.,
                },
                ..OrthographicProjection::default_3d()
            }),
            Transform::from_xyz(0., 0., -4.).looking_at(Vec3::ZERO, Vec3::Y),
        ))
        .id();
    world.spawn((
        DirectionalLight {
            illuminance: 8500.,
            ..default()
        },
        Transform::IDENTITY.looking_to(Vec3::new(0.15, -0.2, 1.), Vec3::Y),
    ));
    let readers = [
        reader(world, color.clone()),
        reader(world, normal.clone()),
        reader(world, lit),
    ];
    (camera, readers)
}

fn snapshot(app: &App, readers: &[Entity; 3]) -> Option<([Vec<u8>; 3], [u64; 3])> {
    const BYTES: usize = (SIZE * SIZE * 4) as usize;
    let [Some(a), Some(b), Some(c)] = readers.map(|reader| app.world().get::<Pixels>(reader))
    else {
        return None;
    };
    if [a, b, c].into_iter().any(|p| p.data.len() != BYTES) {
        return None;
    }
    Some((
        [a.data.clone(), b.data.clone(), c.data.clone()],
        [a.received, b.received, c.received],
    ))
}

fn wait(
    app: &mut App,
    readers: &[Entity; 3],
    expect_draw: bool,
    expected: Option<&[Vec<u8>; 3]>,
) -> Result<[Vec<u8>; 3], Box<dyn std::error::Error>> {
    let deadline = Instant::now() + Duration::from_secs(22);
    let mut previous = None;
    let mut last_reads = [0; 3];
    let mut stable = 0;
    loop {
        app.update();
        let current = snapshot(app, readers);
        if let Some((current, reads)) = current
            && reads
                .iter()
                .zip(last_reads)
                .all(|(latest, last)| *latest > last)
        {
            last_reads = reads;
            // The gray blocker may appear while the extended material is
            // still compiling. Check for the warm receiver at known pixels
            // outside the decal silhouette; two distinct colors alone can
            // mean we only saw blocker plus clear color.
            let rendered_receiver = [SIZE / 4, SIZE / 2, SIZE * 3 / 4].into_iter().all(|y| {
                let left = ((y * SIZE + 16) * 4) as usize;
                let right = ((y * SIZE + SIZE * 3 / 4) * 4) as usize;
                let warm = &current[2][left..left + 3];
                let gray = &current[2][right..right + 3];
                warm[0] > warm[1]
                    && warm[1] > warm[2]
                    && gray[0] >= 100
                    && gray[0].abs_diff(gray[1]) <= 2
                    && gray[1].abs_diff(gray[2]) <= 2
            });
            let coverage = if expect_draw {
                current[0].chunks_exact(4).any(|p| p[3] != 255)
                    && current[1].chunks_exact(4).any(|p| p[3] != 255)
            } else {
                current[0].chunks_exact(4).all(|p| p == [0, 0, 0, 255])
                    && current[1]
                        .chunks_exact(4)
                        .all(|p| p == [128, 128, 128, 255])
            };
            if rendered_receiver
                && coverage
                && expected.is_none_or(|reference| {
                    if expect_draw {
                        current[2] != reference[2]
                    } else {
                        reference == &current
                    }
                })
                && previous.as_ref() == Some(&current)
            {
                stable += 1;
                if stable >= 8 {
                    return Ok(current);
                }
            } else {
                stable = 0;
            }
            previous = Some(current);
        }
        if Instant::now() > deadline {
            if let Some((current, _)) = snapshot(app, readers) {
                for y in [SIZE / 4, SIZE / 2, SIZE * 3 / 4] {
                    let samples: Vec<_> = [16, SIZE / 4, SIZE * 3 / 4]
                        .into_iter()
                        .map(|x| {
                            let offset = ((y * SIZE + x) * 4) as usize;
                            &current[2][offset..offset + 4]
                        })
                        .collect();
                    eprintln!("Lit row {y}: {samples:?}");
                }
            }
            if let (Some(expected), Some((current, reads))) = (expected, snapshot(app, readers)) {
                let differences: Vec<_> = current
                    .iter()
                    .zip(expected)
                    .map(|(image, control)| {
                        let mut changed = 0;
                        let mut max_delta = 0;
                        for (a, b) in image.iter().zip(control) {
                            changed += usize::from(a != b);
                            max_delta = max_delta.max(a.abs_diff(*b));
                        }
                        (changed, max_delta)
                    })
                    .collect();
                eprintln!("Last readbacks {reads:?}; byte differences/max delta: {differences:?}");
                let sample = |bytes: &[u8], x| {
                    let offset = (((SIZE / 2) * SIZE + x) * 4) as usize;
                    format!("{:?}", &bytes[offset..offset + 4])
                };
                eprintln!(
                    "Lit left/right: {} {}; control: {} {}",
                    sample(&current[2], SIZE / 4),
                    sample(&current[2], SIZE * 3 / 4),
                    sample(&expected[2], SIZE / 4),
                    sample(&expected[2], SIZE * 3 / 4)
                );
            }
            return Err(
                format!("Timed out waiting for stable real view draw={expect_draw}").into(),
            );
        }
    }
}

fn test(
    app: &mut App,
    path: &Path,
    camera: Entity,
    readers: &[Entity; 3],
    baseline: &[Vec<u8>; 3],
) -> Result<(), Box<dyn std::error::Error>> {
    let bytes = std::fs::read(path)?;
    let raster = block_on(SourceDecalRaster::prepare(
        app.world().resource::<RenderDevice>(),
        app.world().resource::<RenderQueue>(),
        &bytes,
    ))?;
    app.world_mut()
        .get_mut::<SourceDecalView>(camera)
        .unwrap()
        .set_draws(vec![(raster, Mat4::IDENTITY)])?;
    let observed = wait(app, readers, true, Some(baseline))?;
    let mut colors = 0;
    let mut normals = 0;
    let mut lit_pixels = 0;
    let mut blocker_leaks = 0;
    let mut changed_outside = 0;
    for (index, (((color, normal), lit), base)) in observed[0]
        .chunks_exact(4)
        .zip(observed[1].chunks_exact(4))
        .zip(observed[2].chunks_exact(4))
        .zip(baseline[2].chunks_exact(4))
        .enumerate()
    {
        let in_blocker = index as u32 % SIZE > SIZE / 2 + 2;
        let affected = color[3] != 255 || normal[3] != 255;
        colors += usize::from(color[3] != 255);
        normals += usize::from(normal[3] != 255);
        lit_pixels += usize::from(lit != base);
        blocker_leaks += usize::from(in_blocker && affected);
        changed_outside += usize::from(!affected && lit != base);
    }
    println!(
        "{}",
        serde_json::json!({
            "file":path,"color_pixels":colors,"normal_pixels":normals,
            "lit_pixels":lit_pixels,"blocker_leaks":blocker_leaks,
            "changed_outside":changed_outside,
        })
    );
    if colors == 0 || normals == 0 || lit_pixels == 0 || blocker_leaks != 0 || changed_outside != 0
    {
        return Err("Source decal depth or pre-light composition check failed".into());
    }
    Ok(())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            })
            .set(RenderPlugin {
                render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                    backends: Backends::from_env().or(Some(Backends::DX12)),
                    features: WgpuFeatures::TEXTURE_COMPRESSION_BC,
                    ..default()
                })),
                ..default()
            })
            .disable::<WinitPlugin>()
            .disable::<PipelinedRenderingPlugin>(),
    );
    app.add_plugins(SourceDecalViewPlugin);
    let deadline = Instant::now() + Duration::from_secs(30);
    while app.plugins_state() != bevy::app::PluginsState::Ready {
        if Instant::now() > deadline {
            return Err("Render device initialization timeout".into());
        }
        std::thread::sleep(Duration::from_millis(10));
    }
    app.finish();
    app.cleanup();
    let (camera, readers) = setup(&mut app);
    let baseline = wait(&mut app, &readers, false, None)?;
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    if args.is_empty() {
        return Err("Expected at least one private GLB".into());
    }
    let independent = args.len() == 1;
    for path in args {
        test(&mut app, Path::new(&path), camera, &readers, &baseline)?;
        if independent {
            // A single-carrier invocation validates one fresh scene; passing
            // several paths also validates removal and return to the control.
            break;
        }
        app.world_mut()
            .get_mut::<SourceDecalView>(camera)
            .unwrap()
            .set_draws(Vec::new())?;
        wait(&mut app, &readers, false, Some(&baseline))?;
    }
    Ok(())
}
