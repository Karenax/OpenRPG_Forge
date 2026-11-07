//! Transient draft guides and screen-space handles. Accepted geometry is unchanged.
use bevy::prelude::*;
use forge_core::ContentHash;
use leptos::prelude::{Callable, Callback};
use std::cell::Cell;
use std::cell::RefCell;

#[derive(Clone, PartialEq)]
struct Draft {
    terrain: ContentHash,
    points: Vec<Option<[f32; 2]>>,
    active: Option<usize>,
    drawing: bool,
}

thread_local! {
    static DRAFT: RefCell<Option<Draft>> = const { RefCell::new(None) };
    static HANDLES: RefCell<Vec<(usize, Vec2)>> = const { RefCell::new(Vec::new()) };
    static DRAG: Cell<Option<usize>> = const { Cell::new(None) };
    static BEGIN: Cell<Option<Callback<usize, bool>>> = const { Cell::new(None) };
}

pub(super) fn bind_drag(callback: Callback<usize, bool>) {
    BEGIN.with(|slot| slot.set(Some(callback)));
    leptos::prelude::on_cleanup(|| {
        BEGIN.with(|slot| slot.set(None));
        clear();
    });
}

pub(super) fn cancel_drag() {
    DRAG.with(|slot| slot.set(None));
}

pub(super) fn is_dragging() -> bool {
    DRAG.with(Cell::get).is_some()
}

#[allow(
    clippy::cast_precision_loss,
    reason = "bounded browser pointer coordinates"
)]
pub(super) fn press(event: &web_sys::MouseEvent) {
    if event.button() != 0 || !super::authoring_viewport::can_preview_asset() {
        return;
    }
    let point = Vec2::new(event.offset_x() as f32, event.offset_y() as f32);
    let hit = HANDLES.with(|handles| {
        handles
            .borrow()
            .iter()
            .filter_map(|(index, handle)| {
                let distance = handle.distance_squared(point);
                (distance <= 144.0).then_some((*index, distance))
            })
            .min_by(|a, b| a.1.total_cmp(&b.1))
            .map(|(index, _)| index)
    });
    if let Some(index) = hit
        && BEGIN
            .with(Cell::get)
            .is_some_and(|callback| callback.run(index))
    {
        DRAG.with(|slot| slot.set(Some(index)));
        event.prevent_default();
    }
}

pub(super) fn clear() {
    DRAFT.with(|slot| slot.replace(None));
    HANDLES.with(|slot| slot.borrow_mut().clear());
    cancel_drag();
}

pub(super) fn show(
    terrain: ContentHash,
    points: Vec<Option<[f32; 2]>>,
    active: Option<usize>,
    drawing: bool,
) {
    if points.len() > 128 {
        clear();
        return;
    }
    DRAFT.with(|slot| {
        slot.replace(Some(Draft {
            terrain,
            points,
            active,
            drawing,
        }))
    });
}

#[derive(Default)]
struct Cache {
    draft: Option<Draft>,
    plan: Option<(ContentHash, forge_world::TerrainGenerationPlan)>,
    positions: Vec<Option<Vec3>>,
}

pub(super) fn install(app: &mut App) {
    app.add_systems(Update, draw);
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    reason = "Bevy independently schedules optional terrain resources and view parameters"
)]
fn draw(
    terrain: Option<Res<forge_engine::TerrainRuntimeState>>,
    streamed: Option<Res<forge_engine::cell_streaming::ResidentSampledTerrain>>,
    sampled: Option<Res<forge_engine::sampled_terrain_physics::SampledTerrainPhysicsState>>,
    cameras: Query<
        (&Camera, &GlobalTransform),
        forge_engine::source_sky::preview::SceneCameraFilter,
    >,
    cursor: Res<forge_engine::TerrainBrushCursor>,
    mut cache: Local<Cache>,
    mut gizmos: Gizmos,
) {
    HANDLES.with(|slot| slot.borrow_mut().clear());
    if !super::authoring_viewport::can_preview_asset() {
        return;
    }
    let raster = streamed
        .as_ref()
        .map(|source| source.plan().as_ref())
        .or_else(|| sampled.as_ref().map(|source| source.plan.as_ref()));
    let hash = raster
        .map(forge_world::sampled_terrain::SampledTerrainPlan::document_hash)
        .or_else(|| {
            terrain
                .as_ref()
                .map(|source| source.cache().source_document_hash)
        });
    let Ok((projection, camera)) = cameras.single() else {
        return;
    };
    DRAFT.with(|slot| {
        let slot = slot.borrow();
        let Some(draft) = slot.as_ref() else {
            return;
        };
        if Some(draft.terrain) != hash {
            return;
        }
        if cache.draft.as_ref() != Some(draft) {
            cache_positions(&mut cache, draft, terrain.as_deref(), raster);
        }
        let mut positions = cache.positions.clone();
        if let Some(index) = DRAG.with(Cell::get)
            && let Some(hit) = cursor.hit
            && let Some(point) = positions.get_mut(index)
        {
            *point = Some(hit + Vec3::Y * 0.06);
        }
        if draft.drawing
            && let Some(Some(last)) = positions.last()
            && let Some(hit) = cursor.hit
        {
            gizmos.line(*last, hit + Vec3::Y * 0.06, Color::srgb(0.5, 0.9, 1.0));
        }
        for segment in positions.windows(2) {
            if let [Some(start), Some(end)] = segment {
                gizmos.line(*start, *end, Color::srgb(1.0, 0.82, 0.35));
            }
        }
        for (index, point) in positions.iter().enumerate() {
            let Some(point) = point else {
                continue;
            };
            let active = draft.active == Some(index);
            let radius = (camera.translation().distance(*point) * 0.004).clamp(0.35, 3.0);
            let color = if active {
                Color::srgb(1.0, 0.35, 0.05)
            } else {
                Color::srgb(1.0, 0.9, 0.1)
            };
            let top = *point + Vec3::Y * radius * 3.0;
            gizmos.line(*point, top, color);
            gizmos.sphere(top, radius, color);
            if !draft.drawing
                && let Ok(screen) = projection.world_to_viewport(camera, top)
            {
                HANDLES.with(|slot| slot.borrow_mut().push((index, screen)));
            }
        }
    });
}

#[allow(
    clippy::cast_possible_truncation,
    reason = "validated finite terrain heights are rendered in float32 coordinates"
)]
fn cache_positions(
    cache: &mut Cache,
    draft: &Draft,
    terrain: Option<&forge_engine::TerrainRuntimeState>,
    raster: Option<&forge_world::sampled_terrain::SampledTerrainPlan>,
) {
    if raster.is_some() {
        cache.plan = None;
    } else if cache
        .plan
        .as_ref()
        .is_none_or(|(hash, _)| *hash != draft.terrain)
    {
        cache.plan = terrain
            .and_then(|source| {
                forge_world::TerrainGenerationPlan::from_document(source.document()).ok()
            })
            .map(|plan| (draft.terrain, plan));
    }
    cache.positions = draft
        .points
        .iter()
        .map(|point| {
            let [x, z] = (*point)?;
            let height = if let Some(raster) = raster {
                raster.height_at(f64::from(x), f64::from(z)).ok()? as f32
            } else {
                cache.plan.as_ref()?.1.surface_height_at(x, z).ok()?
            };
            Some(Vec3::new(x, height + 0.06, z))
        })
        .collect();
    cache.draft = Some(draft.clone());
}
