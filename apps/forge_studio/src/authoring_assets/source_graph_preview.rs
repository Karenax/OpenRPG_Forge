//! Disposable source-material previews in the same authoring world/renderer.
//! Source mounting never falls back to ordinary PBR. Reveal awaits pass/LOD routing.
use super::{
    source_graph_context, source_graph_host,
    source_graph_runtime::{self, RuntimePolicy, SourceClock},
};
use bevy::{ecs as bevy_ecs, prelude::*};
use std::{cell::RefCell, collections::BTreeMap};

enum Request {
    Show(String, Vec<u8>),
    Clear,
}
thread_local! {static REQUEST:RefCell<Option<Request>>=const{RefCell::new(None)};}
#[derive(Resource, Default)]
struct State {
    root: Option<Entity>,
}

pub(super) fn submit(id: String, bytes: Vec<u8>) {
    REQUEST.with(|r| r.replace(Some(Request::Show(id, bytes))));
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

/// Coordinate conversion shared by Unity-derived source scenes: X reflection,
/// metres and Y up. The presentation translation never becomes authored world.
fn policy(offset: Vec3) -> Result<RuntimePolicy, String> {
    let author_basis = Mat4::from_scale(Vec3::new(-1., 1., 1.));
    let mapping = source_graph_context::world_mapping(author_basis, offset)?;
    Ok(RuntimePolicy {
        source_world_from_target_world: mapping,
        source_absolute_world_from_target_world: mapping,
        global_mip_bias: 0.,
        source_float_min: f32::MIN_POSITIVE,
    })
}

fn mount(world: &mut World, id: &str, bytes: &[u8]) -> Result<Entity, String> {
    let offset = Vec3::new(0., 1024., 0.);
    let transform = Transform::from_translation(offset);
    let policy = policy(offset)?;
    let clock = world
        .get_resource::<SourceClock>()
        .copied()
        .ok_or("Horloge de l’aperçu indisponible")?;
    let frame = policy.frame(clock)?;
    // Check the carrier before creating any disposable entity.
    if super::source_graph_route::read(bytes)?.is_none() {
        return Err("Scène sans déclaration de matériau source".into());
    }
    let root = world
        .spawn((
            Visibility::Hidden,
            transform,
            GlobalTransform::from(transform),
            Name::new(format!("Aperçu temporaire source · {id}")),
        ))
        .id();
    let result = source_graph_host::mount_initial(
        world,
        root,
        bytes,
        None,
        transform.to_matrix(),
        &frame,
        BTreeMap::new(),
    )
    .and_then(|host| host.ok_or_else(|| "Déclaration source absente au montage".to_owned()))
    // Reserved explicitly by this preview host. The engine rejects occupation
    // or camera exposure; no source identity selects or overrides a layer.
    .and_then(|host| {
        host.install_shadow_routes(world, 31)?;
        Ok(host)
    })
    .and_then(|host| source_graph_runtime::retain(world, host, policy));
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
        world.despawn(root);
    }
    match request {
        Request::Clear => {}
        Request::Show(id, bytes) => match mount(world, &id, &bytes) {
            Ok(root) => {
                world.resource_mut::<State>().root = Some(root);
                super::preview_status(
                    "Scène source chargée ; contrôle du rendu et des niveaux de détail encore requis avant affichage.",
                );
            }
            Err(reason) => super::preview_status(&format!("Aperçu refusé : {reason}")),
        },
    }
}
