//! Cached pictures of resident objects, rendered once on the existing GPU.
//! No additional renderer, asset loading, source mutation or animated preview.
use bevy::prelude::With;
use bevy::{
    camera::{RenderTarget, primitives::Aabb, visibility::RenderLayers},
    diagnostic::FrameCount,
    ecs as bevy_ecs,
    prelude::*,
    render::{
        gpu_readback::{Readback, ReadbackComplete},
        render_resource::{TextureFormat, TextureUsages},
    },
};
use forge_core::{AssetId, ContentHash, EntityId};
use leptos::prelude::Update;
use leptos::prelude::*;
use std::{
    cell::RefCell,
    collections::{BTreeMap, BTreeSet},
};
use wasm_bindgen::JsCast;

type Key = (AssetId, ContentHash);
const SIZE: u32 = 192;
const LAYER: usize = 30;
#[derive(Clone, PartialEq)]
struct Row {
    id: EntityId,
    key: Key,
    name: String,
}
#[derive(Default)]
struct Cache {
    head: Option<ContentHash>,
    rows: Vec<Row>,
    images: BTreeMap<Key, String>,
    requested: BTreeSet<Key>,
    failed: BTreeSet<Key>,
    revision: Option<RwSignal<u64>>,
}
thread_local! { static CACHE: RefCell<Cache> = RefCell::new(Cache::default()); }
fn changed(cache: &Cache) {
    if let Some(revision) = cache.revision {
        revision.update(|n| *n = n.wrapping_add(1));
    }
}
fn image(key: Key) -> String {
    CACHE.with(|cache| {
        let cache = cache.borrow();
        if let Some(revision) = cache.revision {
            let _ = revision.get();
        }
        cache.images.get(&key).cloned().unwrap_or_default()
    })
}
fn pending_label(key: Key) -> &'static str {
    CACHE.with(|cache| {
        let cache = cache.borrow();
        if let Some(revision) = cache.revision {
            let _ = revision.get();
        }
        if cache.failed.contains(&key) {
            "Aperçu indisponible"
        } else {
            "Aperçu en préparation…"
        }
    })
}
#[component]
pub(super) fn MapShelf(
    inspected: RwSignal<Option<EntityId>>,
    query: RwSignal<String>,
) -> impl IntoView {
    let revision = RwSignal::new(0);
    CACHE.with(|c| c.borrow_mut().revision = Some(revision));
    on_cleanup(move || {
        CACHE.with(|c| {
            if c.borrow().revision == Some(revision) {
                c.borrow_mut().revision = None;
            }
        })
    });
    let rows = move || {
        let _ = revision.get();
        let query = super::catalog::search_terms(&query.get());
        CACHE.with(|c| {
            c.borrow()
                .rows
                .iter()
                .filter(|row| {
                    let name = super::catalog::search_terms(&row.name);
                    query.split_whitespace().all(|word| name.contains(word))
                })
                .cloned()
                .collect::<Vec<_>>()
        })
    };
    view! {
        <div id="forge-map-object-shelf" class="gold-asset-cards map-object-cards">
            <For each=rows key=|row|row.id children=move |row| {
                let id = row.id; let key = row.key;
                view! {
                    <button data-map-object-id=id.to_string() title=row.name.clone()
                        aria-pressed=move ||(inspected.get()==Some(id)).to_string()
                        on:click=move |_| {
                            super::placements::selection::choose(Some(id));
                            super::placements::focus(id);
                        }>
                        <Show when=move ||!image(key).is_empty() fallback=move ||view!{<small class="map-thumbnail-loading">{move ||pending_label(key)}</small>}>
                            <img src=move ||image(key) alt="Aperçu du modèle présent dans la carte"/>
                        </Show>
                        <span>{row.name.clone()}</span>
                    </button>
                }
            }/>
        </div>
        <Show when=move ||rows().is_empty()><p>"Les objets apparaissent ici lorsque la carte est chargée."</p></Show>
    }
}

#[derive(Resource)]
struct Capture {
    key: Key,
    head: ContentHash,
    camera: Entity,
    reader: Entity,
    target: Handle<Image>,
    layers: Vec<(Entity, Option<RenderLayers>)>,
    started: u32,
    done: bool,
    previous: Vec<u8>,
    stable: u8,
}
pub(super) fn install(app: &mut App) {
    app.add_systems(
        PostUpdate,
        update
            .after(bevy::transform::TransformSystems::Propagate)
            .after(bevy::camera::visibility::VisibilitySystems::CalculateBounds),
    );
}

fn retire(world: &mut World) {
    if let Some(capture) = world.remove_resource::<Capture>() {
        let timed_out = world
            .resource::<FrameCount>()
            .0
            .saturating_sub(capture.started)
            > 300;
        CACHE.with(|cache| {
            let mut cache = cache.borrow_mut();
            if cache.head == Some(capture.head) && !cache.images.contains_key(&capture.key) {
                if capture.done || timed_out {
                    cache.failed.insert(capture.key);
                } else {
                    cache.requested.remove(&capture.key);
                }
                changed(&cache);
            }
        });
        for (entity, original) in capture.layers {
            if let Ok(mut entity) = world.get_entity_mut(entity) {
                if let Some(layers) = original {
                    entity.insert(layers);
                } else {
                    entity.remove::<RenderLayers>();
                }
            }
        }
        world.despawn(capture.camera);
        world.despawn(capture.reader);
        world
            .resource_mut::<Assets<Image>>()
            .remove(capture.target.id());
    }
}

fn update(world: &mut World) {
    let frame = world.resource::<FrameCount>().0;
    let head = super::placements::accepted_head();
    let hash = head.as_ref().and_then(|head| head.content_hash().ok());
    let retire_capture = world
        .get_resource::<Capture>()
        .is_some_and(|c| c.done || hash != Some(c.head) || frame.saturating_sub(c.started) > 300);
    if retire_capture {
        retire(world);
    }
    let shelf_visible = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("forge-map-object-shelf"))
        .is_some_and(|e| e.get_bounding_client_rect().height() > 0.0);
    if !shelf_visible
        || !crate::authoring_viewport::can_preview_asset()
        || super::placements::preview_active()
        || super::placements::gizmo::active()
    {
        retire(world);
        return;
    }
    let (Some(head), Some(hash)) = (head, hash) else {
        retire(world);
        return;
    };
    // Reconcile only at a bounded frequency, and never while a picture is active.
    if world.contains_resource::<Capture>() || !frame.is_multiple_of(15) {
        return;
    }
    let Ok(objects) = super::objects::list(&head) else {
        return;
    };
    let mut parts = world.query::<(
        Entity,
        &forge_engine::ManualPlacementPart,
        &Mesh3d,
        &GlobalTransform,
        &Aabb,
        Option<&InheritedVisibility>,
    )>();
    let mut grouped: BTreeMap<EntityId, Vec<(Entity, Vec3, Vec3)>> = BTreeMap::new();
    for (entity, part, _, transform, aabb, visible) in parts.iter(world) {
        if visible.is_some_and(|visible| !visible.get()) {
            continue;
        }
        let affine = transform.affine();
        let center = affine.transform_point3a(aabb.center);
        let extent = affine.matrix3.abs() * aabb.half_extents;
        grouped.entry(part.0).or_default().push((
            entity,
            (center - extent).into(),
            (center + extent).into(),
        ));
    }
    let rows: Vec<_> = objects
        .iter()
        .filter(|object| grouped.contains_key(&object.id))
        .map(|object| Row {
            id: object.id,
            key: (object.asset_id, object.asset_hash),
            name: object.name.clone(),
        })
        .collect();
    let request = CACHE.with(|cache| {
        let mut cache = cache.borrow_mut();
        if cache.head != Some(hash) {
            cache.head = Some(hash);
            cache.requested.clear();
            cache.failed.clear();
            let keys: BTreeSet<_> = rows.iter().map(|row| row.key).collect();
            cache.images.retain(|key, _| keys.contains(key));
        }
        if cache.rows != rows {
            cache.rows.clone_from(&rows);
            changed(&cache);
        }
        let next = rows
            .iter()
            .find(|row| !cache.images.contains_key(&row.key) && !cache.requested.contains(&row.key))
            .cloned();
        if let Some(row) = &next {
            cache.requested.insert(row.key);
        }
        next
    });
    let Some(row) = request else {
        return;
    };
    let Some(parts) = grouped.get(&row.id) else {
        return;
    };
    let mut min = Vec3::splat(f32::INFINITY);
    let mut max = Vec3::splat(f32::NEG_INFINITY);
    let mut layers = Vec::new();
    for (entity, lo, hi) in parts {
        min = min.min(*lo);
        max = max.max(*hi);
        let original = world.get::<RenderLayers>(*entity).cloned();
        let extended = original.clone().unwrap_or_default().with(LAYER);
        world.entity_mut(*entity).insert(extended);
        layers.push((*entity, original));
    }
    let lights: Vec<_> = world
        .query_filtered::<Entity, With<DirectionalLight>>()
        .iter(world)
        .collect();
    for entity in lights {
        let original = world.get::<RenderLayers>(entity).cloned();
        world
            .entity_mut(entity)
            .insert(original.clone().unwrap_or_default().with(LAYER));
        layers.push((entity, original));
    }
    let center = (min + max) * 0.5;
    let radius = ((max - min).length() * 0.5).max(0.1);
    let mut texture = Image::new_target_texture(SIZE, SIZE, TextureFormat::Rgba8UnormSrgb, None);
    texture.texture_descriptor.usage |= TextureUsages::COPY_SRC;
    let target = world.resource_mut::<Assets<Image>>().add(texture);
    let camera = world
        .spawn((
            forge_engine::tidewater_sky::preview::PreviewCamera,
            Camera3d::default(),
            Camera {
                order: -30,
                clear_color: ClearColorConfig::Custom(Color::srgb(0.91, 0.89, 0.84)),
                ..default()
            },
            RenderTarget::Image(target.clone().into()),
            Msaa::Off,
            RenderLayers::layer(LAYER),
            Transform::from_translation(
                center + Vec3::new(1.0, 0.65, 1.0).normalize() * radius * 3.2,
            )
            .looking_at(center, Vec3::Y),
            Projection::Perspective(PerspectiveProjection {
                fov: 45_f32.to_radians(),
                near: 0.01,
                far: radius * 10.0,
                ..default()
            }),
        ))
        .id();
    let reader = world
        .spawn(Readback::texture(target.clone()))
        .observe(receive)
        .id();
    world.insert_resource(Capture {
        key: row.key,
        head: hash,
        camera,
        reader,
        target,
        layers,
        started: frame,
        done: false,
        previous: Vec::new(),
        stable: 0,
    });
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy observer parameters")]
fn receive(event: On<ReadbackComplete>, capture: Option<ResMut<Capture>>, frame: Res<FrameCount>) {
    let Some(mut capture) = capture else {
        return;
    };
    if event.entity != capture.reader
        || capture.done
        || frame.0.saturating_sub(capture.started) < 12
    {
        return;
    }
    // 192 RGBA pixels occupy 768 bytes, an exact multiple of GPU row alignment.
    if event.data.len() != (SIZE * SIZE * 4) as usize {
        capture.done = true;
        return;
    }
    let nonuniform = event
        .data
        .chunks_exact(4)
        .any(|pixel| pixel != &event.data[..4]);
    capture.stable = if nonuniform && capture.previous == event.data {
        capture.stable + 1
    } else {
        0
    };
    capture.previous.clone_from(&event.data);
    if capture.stable < 2 && frame.0.saturating_sub(capture.started) < 90 {
        return;
    }
    if nonuniform && let Ok(url) = encode(&event.data) {
        CACHE.with(|cache| {
            let mut cache = cache.borrow_mut();
            if cache.head == Some(capture.head) {
                cache.images.insert(capture.key, url);
                changed(&cache);
            }
        });
    }
    capture.done = true;
}
fn encode(bytes: &[u8]) -> Result<String, String> {
    let canvas = web_sys::window()
        .and_then(|w| w.document())
        .ok_or("Document absent")?
        .create_element("canvas")
        .map_err(|_| "Canvas absent")?
        .dyn_into::<web_sys::HtmlCanvasElement>()
        .map_err(|_| "Canvas invalide")?;
    canvas.set_width(SIZE);
    canvas.set_height(SIZE);
    let context = canvas
        .get_context("2d")
        .map_err(|_| "Contexte absent")?
        .ok_or("Contexte absent")?
        .dyn_into::<web_sys::CanvasRenderingContext2d>()
        .map_err(|_| "Contexte invalide")?;
    let pixels = web_sys::ImageData::new_with_u8_clamped_array_and_sh(
        wasm_bindgen::Clamped(bytes),
        SIZE,
        SIZE,
    )
    .map_err(|_| "Pixels invalides")?;
    context
        .put_image_data(&pixels, 0.0, 0.0)
        .map_err(|_| "Image invalide")?;
    canvas
        .to_data_url()
        .map_err(|_| "Encodage impossible".into())
}
