//! Accepted paths or an explicitly reviewed proposal, never a canonical scene.
use bevy::{
    asset::RenderAssetUsages, ecs as bevy_ecs, mesh::Indices, prelude::*,
    render::render_resource::PrimitiveTopology,
};
use forge_core::ContentHash;
use forge_project::{authoring::AuthoringState, zone_authoring::ZoneAuthoringHead};
use leptos::prelude::*;
use std::cell::RefCell;

thread_local! {
    static PENDING: RefCell<Option<(ContentHash, ContentHash, ZoneAuthoringHead)>> = const { RefCell::new(None) };
    static PREVIEW: RefCell<Option<forge_world::path_surface::PathSurfaceProjection>> = const { RefCell::new(None) };
    static STATUS: RefCell<String> = const { RefCell::new(String::new()) };
    static SIGNAL: RefCell<Option<WriteSignal<String>>> = const { RefCell::new(None) };
}

pub(super) fn restore() {
    PREVIEW.with(|p| p.replace(None));
}

pub(super) fn preview(
    state: &AuthoringState,
    receipt: &forge_project::zone_authoring::session::ZoneReceipt,
) -> Result<(), String> {
    restore();
    let head = state
        .zone_head
        .as_ref()
        .ok_or("Aucune zone pour l’aperçu")?;
    let derived = forge_world::path_surface::preview(head, receipt)?;
    PREVIEW.with(|p| p.replace(Some(derived)));
    report(
        "Aperçu chemins demandé, non appliqué : surface cyan ; végétation acceptée inchangée."
            .into(),
    );
    Ok(())
}

fn report(text: String) {
    STATUS.with(|s| s.replace(text.clone()));
    SIGNAL.with(|s| {
        if let Some(signal) = *s.borrow() {
            signal.set(text);
        }
    });
}

#[component]
pub(super) fn ProjectionStatus() -> impl IntoView {
    let (status, set_status) = signal(STATUS.with(|s| s.borrow().clone()));
    SIGNAL.with(|s| s.replace(Some(set_status)));
    view! { <output aria-live="polite">{move || status.get()}</output> }
}

pub(super) fn observe(state: &AuthoringState) {
    restore();
    let pending = state
        .zone_head
        .as_ref()
        .map(|head| {
            let hash = head.content_hash()?;
            let terrain_hash = super::authoring_scene::terrain(head)?
                .content_hash()
                .map_err(|e| e.to_string())?;
            Ok::<_, String>((hash, terrain_hash, head.clone()))
        })
        .transpose();
    match pending {
        Ok(pending) => {
            PENDING.with(|p| p.replace(pending));
        }
        Err(error) => {
            PENDING.with(|p| p.replace(None));
            report(format!("Chemins : source refusée : {error}"));
        }
    }
}

#[derive(Resource, Default)]
struct Projection {
    attempted: Option<ContentHash>,
    attempted_preview: bool,
    successful: Option<ContentHash>,
    entities: Vec<Entity>,
    profiled_colliders: Vec<(Entity, forge_core::EntityId)>,
    meshes: Vec<Handle<Mesh>>,
    material: Option<Handle<StandardMaterial>>,
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<Projection>()
        .add_systems(Update, update);
}

fn clear(world: &mut World, projection: &mut Projection) {
    projection.successful = None;
    projection.profiled_colliders.clear();
    for entity in projection.entities.drain(..) {
        world.despawn(entity);
    }
    for mesh in projection.meshes.drain(..) {
        world.resource_mut::<Assets<Mesh>>().remove(mesh.id());
    }
    if let Some(material) = projection.material.take() {
        world
            .resource_mut::<Assets<StandardMaterial>>()
            .remove(material.id());
    }
}

const fn accepted_profiled_collision(is_preview: bool, profiled: bool) -> bool {
    !is_preview && profiled
}

fn update(world: &mut World) {
    if !super::authoring_viewport::can_project_sources() {
        return;
    }
    let preview_target =
        PREVIEW.with(|p| p.borrow().as_ref().map(|p| (p.source_hash, p.terrain_hash)));
    let target_hash = preview_target
        .map(|p| p.0)
        .or_else(|| PENDING.with(|p| p.borrow().as_ref().map(|(hash, _, _)| *hash)));
    let is_preview = preview_target.is_some();
    if world.resource::<Projection>().attempted == target_hash
        && world.resource::<Projection>().attempted_preview == is_preview
    {
        return;
    }
    world.resource_scope(|world, mut projection: Mut<Projection>| {
        clear(world, &mut projection);
        let Some((hash, terrain_hash)) = preview_target.or_else(|| PENDING.with(|p| p.borrow().as_ref().map(|(hash, terrain_hash, _)| (*hash, *terrain_hash)))) else {
            projection.attempted = None; projection.attempted_preview = false; report("Chemins : aucune zone".into()); return;
        };
        if world.get_resource::<forge_engine::TerrainRuntimeState>().is_none_or(|r| r.cache().source_document_hash != terrain_hash) {
            return;
        }
        projection.attempted = Some(hash);
        projection.attempted_preview = is_preview;
        let result = if is_preview {
            PREVIEW.with(|p| p.borrow().clone().ok_or_else(|| "Aperçu chemins absent".to_owned()))
        } else {
            PENDING.with(|p| p.borrow().as_ref().ok_or_else(|| "Source chemins absente".to_owned()).and_then(|(_, _, head)| forge_world::path_surface::project(head)))
        };
        let derived = match result {
            Ok(derived) => derived,
            Err(error) => { report(format!("Chemins non projetés : {error}")); return; }
        };
        let count = derived.meshes.len();
        if count > 0 {
            let material = world.resource_mut::<Assets<StandardMaterial>>().add(StandardMaterial {
                base_color: if is_preview { Color::srgb(0.05, 0.65, 0.85) } else { Color::srgb(0.31, 0.23, 0.13) }, perceptual_roughness: 0.95,
                alpha_mode: AlphaMode::Blend, ..default()
            });
            for source in derived.meshes {
                let id = source.id;
                let profiled = source.profiled;
                let collider = if accepted_profiled_collision(is_preview, profiled) {
                    match forge_engine::profiled_path_collider(&source) {
                        Ok(collider) => Some(collider),
                        Err(error) => {
                            report(format!("Chemins non projetés : {error}"));
                            return;
                        }
                    }
                } else {
                    None
                };
                let mut mesh = Mesh::new(PrimitiveTopology::TriangleList, RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD);
                mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, source.positions);
                mesh.insert_attribute(Mesh::ATTRIBUTE_NORMAL, source.normals);
                mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, source.colors);
                mesh.insert_indices(Indices::U32(source.indices));
                let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
                let mut entity = world.spawn((Name::new(format!("Chemin {id}")), Mesh3d(mesh.clone()), MeshMaterial3d(material.clone()), Transform::IDENTITY));
                if let Some(collider) = collider {
                    entity.insert((
                        forge_engine::PathSurfaceColliderIdentity(id),
                        collider,
                    ));
                }
                let entity = entity.id();
                if accepted_profiled_collision(is_preview, profiled) {
                    projection.profiled_colliders.push((entity, id));
                }
                projection.entities.push(entity);
                projection.meshes.push(mesh);
            }
            projection.material = Some(material);
        }
        projection.successful = Some(hash);
        report(format!("{} : {count} surfaces préparées côté moteur · rendu GPU à vérifier · intersections non ajustées", if is_preview { "Aperçu chemins non appliqué, végétation inchangée" } else { "Chemins" }));
    });
}

pub(super) fn ready(world: &World, expected_hash: ContentHash) -> bool {
    let Some(projection) = world.get_resource::<Projection>() else {
        return false;
    };
    let expected_profiled = PENDING.with(|p| {
        p.borrow()
            .as_ref()
            .filter(|(hash, _, _)| *hash == expected_hash)
            .map(|(_, _, head)| {
                head.corrections.paths.as_deref().map_or(0, |paths| {
                    paths.iter().filter(|path| path.profile.is_some()).count()
                })
            })
    });
    if projection.attempted_preview
        || PREVIEW.with(|p| p.borrow().is_some())
        || projection.successful != Some(expected_hash)
        || projection.entities.len() != projection.meshes.len()
        || expected_profiled != Some(projection.profiled_colliders.len())
        || !PENDING.with(|p| {
            p.borrow().as_ref().is_some_and(|(hash, _, head)| {
                *hash == expected_hash
                    && head.corrections.paths.as_ref().map_or(0, Vec::len)
                        == projection.entities.len()
            })
        })
    {
        return false;
    }
    if !projection
        .profiled_colliders
        .iter()
        .all(|&(entity, id)| forge_engine::profiled_path_collider_present(world, entity, id))
    {
        return false;
    }
    let Some(material) = &projection.material else {
        return projection.entities.is_empty();
    };
    if world
        .resource::<Assets<StandardMaterial>>()
        .get(material)
        .is_none()
    {
        return false;
    }
    projection
        .entities
        .iter()
        .zip(&projection.meshes)
        .all(|(entity, mesh)| {
            world.get::<Mesh3d>(*entity).is_some_and(|m| m.0 == *mesh)
                && world.resource::<Assets<Mesh>>().get(mesh).is_some()
                && world
                    .get::<MeshMaterial3d<StandardMaterial>>(*entity)
                    .is_some_and(|m| m.0 == *material)
                && world.get::<Transform>(*entity) == Some(&Transform::IDENTITY)
                && world.get::<ChildOf>(*entity).is_none()
        })
}

#[cfg(test)]
mod tests {
    use super::accepted_profiled_collision;

    #[test]
    fn collision_is_reserved_for_accepted_profiled_paths() {
        assert!(accepted_profiled_collision(false, true));
        assert!(!accepted_profiled_collision(true, true));
        assert!(!accepted_profiled_collision(false, false));
    }
}
