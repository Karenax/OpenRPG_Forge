//! Disposable source-material previews in the same authoring world/renderer.
//! Source mounting never falls back to ordinary PBR. Reveal awaits pass/LOD routing.
use super::source_graph_runtime::{self, RuntimePolicy, SourceClock};
use bevy::{ecs as bevy_ecs, prelude::*};
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
    root: Option<Entity>,
}
pub(super) fn current_root(world: &World) -> Option<Entity> {
    world.get_resource::<State>().and_then(|state| state.root)
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
            .after(source_graph_runtime::update),
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
) -> Result<Entity, String> {
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
        source_graph_runtime::retain(world, host, policy).map_err(|failure| {
            failure.host.rollback_unpublished(world);
            failure.reason
        })
    });
    if let Err(error) = result {
        world.despawn(root);
        return Err(error);
    }
    Ok(root)
}

pub(super) fn update(world: &mut World) {
    let Some(request) = REQUEST.with(|r| r.borrow_mut().take()) else {
        return;
    };
    if let Some(root) = world.resource_mut::<State>().root.take() {
        source_graph_runtime::release(world, root);
        world.despawn(root);
    }
    match request {
        Request::Clear => {}
        Request::Show(id, bytes, physics, particles) => {
            let particles = particles.map(|payload| *payload);
            match mount(world, &id, &bytes, physics.as_ref(), particles.as_ref()) {
                Ok(root) => {
                    world.resource_mut::<State>().root = Some(root);
                    super::preview_status(
                        "Scène source chargée ; contrôle du rendu et des niveaux de détail encore requis avant affichage.",
                    );
                }
                Err(reason) => super::preview_status(&format!("Aperçu refusé : {reason}")),
            }
        }
    }
}
