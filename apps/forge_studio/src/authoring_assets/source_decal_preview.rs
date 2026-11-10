//! The source mesh decal is drawn on a disposable receiver in Studio's actual view.
use bevy::{
    camera::visibility::RenderLayers,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass},
    ecs as bevy_ecs,
    material::OpaqueRendererMethod,
    prelude::*,
    render::renderer::{RenderDevice, RenderQueue},
};
use forge_engine::{
    source_decal_receiver::{SourceDecalPbrMaterial, SourceDecalReceiver},
    source_decal_view::{SourceDecalRaster, SourceDecalView, SourceDecalViewStatus},
};
use std::cell::RefCell;

enum Request {
    Show(u64, Vec<u8>),
    Cancel,
}

type ResultSlot = Option<(u64, Result<SourceDecalRaster, String>)>;
thread_local! {
    static REQUEST: RefCell<Option<Request>> = const { RefCell::new(None) };
    static RESULT: RefCell<ResultSlot> = const { RefCell::new(None) };
}

#[derive(Resource, Default)]
struct State {
    active: Option<Active>,
}

struct Active {
    camera: Entity,
    board: Entity,
    light: Entity,
    original_msaa: Option<Msaa>,
    had_depth: bool,
    raster: SourceDecalRaster,
    world_from_root: Mat4,
    size: UVec2,
    reported: bool,
}

pub(super) fn register(app: &mut App) {
    app.init_resource::<State>().add_systems(
        PostUpdate,
        update.after(super::preview::PreviewSystems::Requests),
    );
}

pub(super) fn cancel() {
    REQUEST.with(|slot| slot.replace(Some(Request::Cancel)));
}

pub(super) fn submit(ticket: u64, bytes: Vec<u8>) {
    REQUEST.with(|slot| slot.replace(Some(Request::Show(ticket, bytes))));
}

fn launch(world: &World, ticket: u64, bytes: Vec<u8>) -> Result<(), String> {
    let device = world
        .get_resource::<RenderDevice>()
        .cloned()
        .ok_or("GPU indisponible")?;
    let queue = world
        .get_resource::<RenderQueue>()
        .cloned()
        .ok_or("GPU indisponible")?;
    wasm_bindgen_futures::spawn_local(async move {
        let result = SourceDecalRaster::prepare(&device, &queue, &bytes).await;
        if super::preview::is_current(ticket) {
            RESULT.with(|slot| slot.replace(Some((ticket, result))));
        }
    });
    Ok(())
}

fn view(
    world: &mut World,
    size: UVec2,
    raster: SourceDecalRaster,
    transform: Mat4,
) -> Result<(SourceDecalView, Handle<SourceDecalPbrMaterial>), String> {
    let mut binding = SourceDecalView::new(&mut world.resource_mut::<Assets<Image>>(), size)?;
    binding.set_draws(vec![(raster, transform)])?;
    let [color, normal] = binding.targets();
    let material = SourceDecalReceiver::material(
        StandardMaterial {
            base_color: Color::linear_rgb(0.65, 0.56, 0.43),
            perceptual_roughness: 1.0,
            opaque_render_method: OpaqueRendererMethod::Forward,
            ..default()
        },
        color,
        normal,
    )?;
    let material = world
        .resource_mut::<Assets<SourceDecalPbrMaterial>>()
        .add(material);
    Ok((binding, material))
}

impl Active {
    fn start(world: &mut World, raster: SourceDecalRaster) -> Result<Self, String> {
        super::source_decal_placement::release_camera(world);
        let mut cameras = world.query_filtered::<(Entity, &Camera), (
            forge_engine::source_sky::preview::SceneCameraFilter,
            Without<forge_engine::ForgePlayerCamera>,
        )>();
        let [(camera, size)] = cameras
            .iter(world)
            .filter(|(_, camera)| camera.is_active)
            .filter_map(|(entity, camera)| camera.physical_target_size().map(|size| (entity, size)))
            .collect::<Vec<_>>()
            .try_into()
            .map_err(|_: Vec<_>| "Caméra d’édition indisponible")?;
        if world.get::<SourceDecalView>(camera).is_some() {
            return Err("La caméra possède déjà un rendu de toiles actif".into());
        }
        let original_msaa = world.get::<Msaa>(camera).copied();
        let had_depth = world.get::<DepthPrepass>(camera).is_some();
        let layers = world
            .get::<RenderLayers>(camera)
            .cloned()
            .unwrap_or_default();
        let (center, normal, radius) = raster.preview_frame()?;
        let pivot = Vec3::new(0.0, 1024.0, 0.0);
        let world_from_root = Mat4::from_translation(pivot - center);
        let (binding, material) = view(world, size, raster.clone(), world_from_root)?;
        let thickness = (radius * 0.02).max(0.002);
        let mesh = world.resource_mut::<Assets<Mesh>>().add(Cuboid::new(
            radius * 2.5,
            radius * 2.5,
            thickness,
        ));
        let board = world
            .spawn((
                Name::new("Support temporaire d’aperçu de toile"),
                Mesh3d(mesh),
                MeshMaterial3d(material),
                Transform::from_translation(pivot - normal * thickness * 1.5)
                    .with_rotation(Quat::from_rotation_arc(Vec3::Z, normal)),
                layers.clone(),
            ))
            .id();
        let light = world
            .spawn((
                DirectionalLight {
                    illuminance: 8500.0,
                    ..default()
                },
                Transform::IDENTITY.looking_to(-normal, Vec3::Y),
                layers,
            ))
            .id();
        world.entity_mut(camera).insert((binding, Msaa::Off));
        let eye = pivot + normal * (3.2 * radius) + Vec3::new(0.27, 0.15, 0.0) * radius;
        crate::authoring_camera::frame_preview_from(pivot, eye);
        Ok(Self {
            camera,
            board,
            light,
            original_msaa,
            had_depth,
            raster,
            world_from_root,
            size,
            reported: false,
        })
    }

    fn update(&mut self, world: &mut World) -> Result<(), String> {
        let size = world
            .get::<Camera>(self.camera)
            .filter(|camera| camera.is_active)
            .and_then(Camera::physical_target_size)
            .ok_or("Caméra d’aperçu indisponible")?;
        if size != self.size {
            let (binding, material) = view(world, size, self.raster.clone(), self.world_from_root)?;
            world.entity_mut(self.camera).insert(binding);
            world
                .entity_mut(self.board)
                .insert(MeshMaterial3d(material));
            self.size = size;
            self.reported = false;
        }
        let binding = world
            .get::<SourceDecalView>(self.camera)
            .ok_or("Rendu d’aperçu indisponible")?;
        match binding.status() {
            SourceDecalViewStatus::Refused(reason) => Err(reason),
            SourceDecalViewStatus::Encoded(1) if !self.reported => {
                self.reported = true;
                super::preview_status(
                    "Toile chargée sur son support d’aperçu. La carte n’a pas été modifiée.",
                );
                Ok(())
            }
            _ => Ok(()),
        }
    }

    fn retire(self, world: &mut World) {
        world.despawn(self.board);
        world.despawn(self.light);
        if let Ok(mut camera) = world.get_entity_mut(self.camera) {
            camera.remove::<SourceDecalView>();
            if !self.had_depth && !camera.contains::<MotionVectorPrepass>() {
                camera.remove::<DepthPrepass>();
            }
            if camera.get::<Msaa>() == Some(&Msaa::Off) {
                if let Some(msaa) = self.original_msaa {
                    camera.insert(msaa);
                } else {
                    camera.remove::<Msaa>();
                }
            }
        }
        crate::authoring_camera::leave_preview();
    }
}

fn update(world: &mut World) {
    let mut active = world.resource_mut::<State>().active.take();
    if let Some(request) = REQUEST.with(|slot| slot.borrow_mut().take()) {
        if let Some(previous) = active.take() {
            previous.retire(world);
        }
        if let Request::Show(ticket, bytes) = request {
            match launch(world, ticket, bytes) {
                Ok(()) => super::preview_status("Préparation du rendu de la toile…"),
                Err(reason) => super::preview_status(&format!("Aperçu refusé : {reason}")),
            }
        }
    }
    if let Some((ticket, result)) = RESULT.with(|slot| slot.borrow_mut().take())
        && super::preview::is_current(ticket)
    {
        match result.and_then(|raster| Active::start(world, raster)) {
            Ok(next) => active = Some(next),
            Err(reason) => super::preview_status(&format!("Aperçu refusé : {reason}")),
        }
    }
    if let Some(current) = active.as_mut()
        && let Err(reason) = current.update(world)
    {
        if let Some(current) = active.take() {
            current.retire(world);
        }
        super::preview_status(&format!("Aperçu refusé : {reason}"));
    }
    world.resource_mut::<State>().active = active;
}
