//! Disposable source-material previews in the same authoring world/renderer.
//! Source mounting never falls back to ordinary PBR.
use super::source_graph_draw_probe as probe;
use super::source_graph_runtime::{self, RuntimePolicy, SourceClock};
use bevy::{diagnostic::FrameCount, ecs as bevy_ecs, prelude::*};
use forge_engine::{source_graph_context, source_graph_mount, source_graph_route};
use std::{cell::RefCell, collections::BTreeMap};

enum Request {
    Show(
        String,
        Vec<u8>,
        Option<super::source_graph_physics::SourcePhysicsPayload>,
        Option<Box<super::source_graph_particles::SourceParticlePayload>>,
    ),
    Clear,
}
thread_local! {static REQUEST:RefCell<Option<Request>>=const{RefCell::new(None)};}
#[derive(Resource, Default)]
struct State {
    active: Option<Active>,
}
struct Active {
    root: Entity,
    parts: Option<Vec<Entity>>,
    started: u32,
    probe: Option<probe::Probe>,
    visible: bool,
}
pub(super) fn current_root(world: &World) -> Option<Entity> {
    world
        .get_resource::<State>()?
        .active
        .as_ref()
        .map(|a| a.root)
}

pub(super) fn submit(
    id: String,
    bytes: Vec<u8>,
    physics: Option<super::source_graph_physics::SourcePhysicsPayload>,
    particles: Option<super::source_graph_particles::SourceParticlePayload>,
) {
    REQUEST.with(|r| {
        r.replace(Some(Request::Show(
            id,
            bytes,
            physics,
            particles.map(Box::new),
        )))
    });
}
pub(super) fn cancel() {
    REQUEST.with(|r| r.replace(Some(Request::Clear)));
}
pub(super) fn register(app: &mut App) {
    app.init_resource::<State>();
    app.add_systems(
        PostUpdate,
        update
            .after(super::preview::PreviewSystems::Requests)
            .after(source_graph_runtime::update)
            .after(bevy::camera::visibility::VisibilitySystems::CalculateBounds),
    );
}

/// The carrier declares its source-to-author coordinate conversion. The
/// presentation translation never becomes authored world coordinates.
fn policy(basis: source_graph_context::ObjectBasis, offset: Vec3) -> Result<RuntimePolicy, String> {
    let mapping = source_graph_context::world_mapping(basis.source_from_target, offset)?;
    Ok(RuntimePolicy {
        source_world_from_target_world: mapping,
        source_absolute_world_from_target_world: mapping,
        global_mip_bias: 0.,
        source_float_min: f32::MIN_POSITIVE,
    })
}

fn mount(
    world: &mut World,
    id: &str,
    bytes: &[u8],
    physics: Option<&super::source_graph_physics::SourcePhysicsPayload>,
    particles: Option<&super::source_graph_particles::SourceParticlePayload>,
) -> Result<Active, String> {
    let offset = Vec3::new(0., 1024., 0.);
    let transform = Transform::from_translation(offset);
    // Admit the declaration before creating any disposable entity. Source
    // carriers can use different reflection axes; no Unity-wide default applies.
    let glb =
        source_graph_route::read(bytes)?.ok_or("Scène sans déclaration de matériau source")?;
    let basis = source_graph_context::ObjectBasis::from_document(glb.document())?
        .ok_or("Conversion de coordonnées source absente de l’aperçu")?;
    let policy = policy(basis, offset)?;
    let clock = world
        .get_resource::<SourceClock>()
        .copied()
        .ok_or("Horloge de l’aperçu indisponible")?;
    let frame = policy.frame(clock)?;
    let contacts = if physics.is_some() {
        Some(
            world
                .get_resource::<super::source_graph_physics::SceneContactSettings>()
                .ok_or("Paramètres physiques explicites de la scène absents")?
                .resolved()?,
        )
    } else {
        None
    };
    let root = world
        .spawn((
            Visibility::Hidden,
            transform,
            GlobalTransform::from(transform),
            Name::new(format!("Aperçu temporaire source · {id}")),
        ))
        .id();
    let result = source_graph_mount::mount(
        world,
        source_graph_mount::SourceSceneMount {
            root,
            carrier: bytes,
            selected_scene: None,
            target_world_from_root: transform.to_matrix(),
            frame: &frame,
            ordinary: BTreeMap::new(),
            physics,
            particles,
            contacts: contacts.as_ref(),
            shadow_layer: 31,
        },
    )
    .and_then(|host| host.ok_or_else(|| "Déclaration source absente au montage".to_owned()))
    .and_then(|host| {
        let static_preview = physics.is_none()
            && particles.is_none()
            && host
                .original_state
                .as_ref()
                .is_none_or(|state| state.lod_groups.is_empty());
        let parts = static_preview.then(|| host.attached.primitives.clone());
        source_graph_runtime::retain(world, host, policy).map_err(|failure| {
            failure.host.rollback_unpublished(world);
            failure.reason
        })?;
        Ok(parts)
    });
    let parts = result.inspect_err(|_| {
        world.despawn(root);
    })?;
    Ok(Active {
        root,
        parts,
        started: world.resource::<FrameCount>().0,
        probe: None,
        visible: false,
    })
}

impl Active {
    fn retire(self, world: &mut World) {
        world.entity_mut(self.root).insert(Visibility::Hidden);
        if let Some(probe) = self.probe {
            probe.finish(world);
        }
        source_graph_runtime::release(world, self.root);
        world.despawn(self.root);
        if self.visible {
            crate::authoring_camera::leave_preview();
        }
    }

    fn update(&mut self, world: &mut World) -> Result<(), String> {
        match world.get::<source_graph_runtime::SourceFrameStatus>(self.root) {
            Some(source_graph_runtime::SourceFrameStatus::Refused(reason)) => {
                return Err(reason.clone());
            }
            Some(source_graph_runtime::SourceFrameStatus::Updated) => {}
            None => return Ok(()),
        }
        if self.visible {
            return Ok(());
        }
        let Some(parts) = &self.parts else {
            return Ok(());
        };
        if self.probe.is_none() {
            self.probe = probe::Probe::start(world, self.root, parts, probe::PREVIEW_LAYER)?;
            if self.probe.is_none()
                && world.resource::<FrameCount>().0.wrapping_sub(self.started) > 180
            {
                return Err("Dimensions du modèle indisponibles".into());
            }
            return Ok(());
        }
        let probe = self
            .probe
            .as_mut()
            .ok_or("Caméra de contrôle indisponible")?;
        match probe.poll(world) {
            probe::Outcome::Pending => {}
            probe::Outcome::Empty => {
                return Err(format!(
                    "Aucun dessin stable sous les deux angles de contrôle : {}",
                    probe.diagnostics(world)
                ));
            }
            probe::Outcome::Drawn => {
                let probe = self.probe.take().ok_or("Caméra de contrôle indisponible")?;
                let (pivot, eye) = probe.framing();
                probe.finish(world);
                world.entity_mut(self.root).insert(Visibility::Inherited);
                crate::authoring_camera::frame_preview_from(pivot, eye);
                self.visible = true;
                super::preview_status(
                    "Aperçu source dessiné. Inspectez le modèle avec la rotation et le zoom.",
                );
            }
        }
        Ok(())
    }
}

pub(super) fn update(world: &mut World) {
    let mut active = world.resource_mut::<State>().active.take();
    if let Some(request) = REQUEST.with(|r| r.borrow_mut().take()) {
        if let Some(previous) = active.take() {
            previous.retire(world);
        }
        if let Request::Show(id, bytes, physics, particles) = request {
            let particles = particles.map(|payload| *payload);
            match mount(world, &id, &bytes, physics.as_ref(), particles.as_ref()) {
                Ok(preview) => {
                    super::preview_status(if preview.parts.is_some() {
                        "Vérification du rendu de l’aperçu…"
                    } else {
                        "Scène source chargée ; l’aperçu de cette scène requiert un contrôle des niveaux de détail ou des effets."
                    });
                    active = Some(preview);
                }
                Err(reason) => super::preview_status(&format!("Aperçu refusé : {reason}")),
            }
        }
    }
    if let Some(mut preview) = active {
        match preview.update(world) {
            Ok(()) => world.resource_mut::<State>().active = Some(preview),
            Err(reason) => {
                preview.retire(world);
                super::preview_status(&format!("Aperçu refusé : {reason}"));
            }
        }
    }
}
