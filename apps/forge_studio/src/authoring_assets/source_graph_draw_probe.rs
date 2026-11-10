//! Draw an admitted source graph on an isolated camera before revealing it.
use bevy::{
    camera::{
        RenderTarget,
        primitives::Aabb,
        visibility::{NoFrustumCulling, RenderLayers},
    },
    diagnostic::FrameCount,
    ecs as bevy_ecs,
    light::NotShadowCaster,
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{TextureFormat, TextureUsages},
    },
};
use forge_engine::source_graph::SourceGraphMaterial;

// Map shelf uses 30; the source shadow proxies use 31.
pub(super) const PLACEMENT_LAYER: usize = 28;
pub(super) const PREVIEW_LAYER: usize = 29;
const SIZE: u32 = 192;
const ANGLE_WAIT_SECONDS: f64 = 12.0;

#[derive(Component, Default)]
struct Pixels {
    previous: Vec<u8>,
    stable: u8,
    samples: u32,
    bytes: usize,
    covered: usize,
    different: usize,
}

pub(super) struct DrawCopy {
    mesh: Mesh3d,
    graph: Option<MeshMaterial3d<SourceGraphMaterial>>,
    ordinary: Option<MeshMaterial3d<StandardMaterial>>,
    local: Transform,
}

impl DrawCopy {
    pub(super) fn prepare(world: &World, entity: Entity) -> Result<Self, &'static str> {
        let transform = world
            .get::<GlobalTransform>(entity)
            .ok_or("Transformation source absente")?;
        let local = transform.compute_transform();
        if !local.to_matrix().abs_diff_eq(transform.to_matrix(), 1e-4) {
            return Err("Le contrôle du rendu ne peut pas reproduire la transformation source");
        }
        let mesh = world
            .get::<Mesh3d>(entity)
            .ok_or("Maillage source absent")?
            .clone();
        let graph = world
            .get::<MeshMaterial3d<SourceGraphMaterial>>(entity)
            .cloned();
        let ordinary = world
            .get::<MeshMaterial3d<StandardMaterial>>(entity)
            .cloned();
        if graph.is_some() == ordinary.is_some() {
            return Err("Le contrôle du rendu exige un matériau par primitive");
        }
        Ok(Self {
            mesh,
            graph,
            ordinary,
            local,
        })
    }

    pub(super) fn spawn(self, world: &mut World, layer: usize) -> Entity {
        let mut copy = world.spawn((
            self.mesh,
            self.local,
            RenderLayers::layer(layer),
            NotShadowCaster,
            NoFrustumCulling,
        ));
        if let Some(material) = self.graph {
            copy.insert(material);
        }
        if let Some(material) = self.ordinary {
            copy.insert(material);
        }
        copy.id()
    }
}

pub(super) struct Probe {
    camera: Entity,
    reader: Entity,
    light: Entity,
    image: Handle<Image>,
    copies: Vec<Entity>,
    started: u32,
    started_at: f64,
    pub(super) pivot: Vec3,
    distance: f32,
    opposite: bool,
}

pub(super) enum Outcome {
    Pending,
    Drawn,
    Empty,
}

fn view(pivot: Vec3, distance: f32, opposite: bool) -> Transform {
    let side = if opposite { -1.0 } else { 1.0 };
    Transform::from_translation(pivot + Vec3::new(side, 0.45, side).normalize() * distance)
        .looking_at(pivot, Vec3::Y)
}

fn enabled_under_root(world: &World, root: Entity, mut entity: Entity) -> bool {
    while entity != root {
        if world.get::<Visibility>(entity) == Some(&Visibility::Hidden) {
            return false;
        }
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return false;
        };
        entity = parent.parent();
    }
    true
}

impl Probe {
    pub(super) fn stable_pixels<'w>(&self, world: &'w World) -> Option<(&'w [u8], u32)> {
        let pixels = world.get::<Pixels>(self.reader)?;
        (pixels.stable >= 2).then_some((pixels.previous.as_slice(), SIZE))
    }

    pub(super) fn framing(&self) -> (Vec3, Vec3) {
        (
            self.pivot,
            view(self.pivot, self.distance, self.opposite).translation,
        )
    }

    pub(super) fn start(
        world: &mut World,
        root: Entity,
        parts: &[Entity],
        layer: usize,
    ) -> Result<Option<Self>, &'static str> {
        if parts.is_empty() {
            return Err("Aucune primitive de rendu source");
        }
        let mut min = Vec3::splat(f32::INFINITY);
        let mut max = Vec3::splat(f32::NEG_INFINITY);
        let mut inputs = Vec::with_capacity(parts.len());
        for &entity in parts {
            if !enabled_under_root(world, root, entity) {
                continue;
            }
            let (Some(bounds), Some(transform)) = (
                world.get::<Aabb>(entity),
                world.get::<GlobalTransform>(entity),
            ) else {
                return Ok(None);
            };
            let affine = transform.affine();
            let center = affine.transform_point3a(bounds.center);
            let extent = affine.matrix3.abs() * bounds.half_extents;
            min = min.min((center - extent).into());
            max = max.max((center + extent).into());
            inputs.push(DrawCopy::prepare(world, entity)?);
        }
        if inputs.is_empty() {
            return Err("Aucune primitive visible dans le modèle source");
        }
        let radius = (max - min).length() * 0.5;
        let pivot = (min + max) * 0.5;
        if !radius.is_finite() || radius > 300.0 || !pivot.is_finite() {
            return Err("Dimensions incompatibles avec l’aperçu individuel");
        }
        let distance = (radius * 3.4).max(0.2);
        let mut texture =
            Image::new_target_texture(SIZE, SIZE, TextureFormat::Rgba8UnormSrgb, None);
        texture.texture_descriptor.usage |= TextureUsages::COPY_SRC;
        let image = world.resource_mut::<Assets<Image>>().add(texture);
        let camera = world
            .spawn((
                forge_engine::scene_camera::OffscreenSceneCamera,
                Camera3d::default(),
                Camera {
                    order: -29,
                    clear_color: ClearColorConfig::Custom(Color::NONE),
                    ..default()
                },
                RenderTarget::Image(image.clone().into()),
                Msaa::Off,
                RenderLayers::layer(layer),
                view(pivot, distance, false),
                Projection::Perspective(PerspectiveProjection {
                    fov: 45_f32.to_radians(),
                    near: 0.01,
                    far: (radius * 12.0).max(2.0),
                    ..default()
                }),
            ))
            .id();
        let light = world
            .spawn((
                DirectionalLight {
                    illuminance: 12_000.0,
                    ..default()
                },
                Transform::IDENTITY.looking_to(Vec3::new(-0.5, -0.7, -0.5), Vec3::Y),
                RenderLayers::layer(layer),
            ))
            .id();
        let reader = reader(world, image.clone());
        // The source shadow owner pins the original mesh's layer. Drawing
        // borrowed mesh/material handles on separate entities preserves that
        // contract and keeps the pending placement hidden from the map camera.
        let copies = inputs
            .into_iter()
            .map(|copy| copy.spawn(world, layer))
            .collect();
        Ok(Some(Self {
            camera,
            reader,
            light,
            image,
            copies,
            pivot,
            distance,
            started: world.resource::<FrameCount>().0,
            started_at: world.resource::<Time<Real>>().elapsed_secs_f64(),
            opposite: false,
        }))
    }

    pub(super) fn poll(&mut self, world: &mut World) -> Outcome {
        let frame = world.resource::<FrameCount>().0;
        let now = world.resource::<Time<Real>>().elapsed_secs_f64();
        let age = frame.wrapping_sub(self.started);
        // Ignore the initial target clear and allow shader preparation/readback.
        if age >= 12
            && world
                .get::<Pixels>(self.reader)
                .is_some_and(|p| p.stable >= 2)
        {
            return Outcome::Drawn;
        }
        // Fast offscreen frames can be queued while the WebGPU pipeline is
        // still compiling. Allow real time as well as readback frames.
        if age < 90 || now - self.started_at < ANGLE_WAIT_SECONDS {
            return Outcome::Pending;
        }
        if self.opposite {
            return Outcome::Empty;
        }
        self.opposite = true;
        world
            .entity_mut(self.camera)
            .insert(view(self.pivot, self.distance, true));
        world.despawn(self.reader);
        self.reader = reader(world, self.image.clone());
        self.started = frame;
        self.started_at = now;
        Outcome::Pending
    }

    pub(super) fn finish(self, world: &mut World) {
        for entity in self.copies {
            world.despawn(entity);
        }
        world.despawn(self.reader);
        world.despawn(self.camera);
        world.despawn(self.light);
        world
            .resource_mut::<Assets<Image>>()
            .remove(self.image.id());
    }

    pub(super) fn diagnostics(&self, world: &World) -> String {
        world.get::<Pixels>(self.reader).map_or_else(
            || "Aucun tampon de lecture GPU".into(),
            |p| {
                format!(
                    "{} lectures · {} octets · {} pixels alpha · {} pixels différents du fond",
                    p.samples, p.bytes, p.covered, p.different
                )
            },
        )
    }
}

fn reader(world: &mut World, image: Handle<Image>) -> Entity {
    world
        .spawn((Readback::texture(image), Pixels::default()))
        .observe(receive)
        .id()
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, mut pixels: Query<&mut Pixels>) {
    let Ok(mut pixels) = pixels.get_mut(event.entity) else {
        return;
    };
    pixels.samples = pixels.samples.saturating_add(1);
    pixels.bytes = event.data.len();
    if event.data.len() != (SIZE * SIZE * 4) as usize {
        return;
    }
    // A transparent clear has alpha zero. Require visible coverage rather than
    // assuming a completed copy or a material handle means a draw happened.
    let coverage = event
        .data
        .as_chunks::<4>()
        .0
        .iter()
        .filter(|p| p[3] != 0)
        .count();
    let different = event
        .data
        .as_chunks::<4>()
        .0
        .iter()
        .filter(|p| *p != &event.data[..4])
        .count();
    pixels.covered = coverage;
    pixels.different = different;
    pixels.stable = if different >= 4 && coverage >= 4 && pixels.previous == event.data {
        pixels.stable.saturating_add(1)
    } else {
        0
    };
    pixels.previous.clone_from(&event.data);
}
