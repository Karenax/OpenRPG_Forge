//! Bounded viewport observations; asset estimates are not OS GPU residency.
use bevy::{prelude::*, render::storage::ShaderBuffer};
use leptos::prelude::*;
use std::cell::Cell;

thread_local! {
    static OUTPUT: Cell<Option<RwSignal<String>>> = const { Cell::new(None) };
    static MATERIAL_OUTPUT: Cell<Option<RwSignal<String>>> = const { Cell::new(None) };
}

#[component]
pub(super) fn ViewportMetrics() -> impl IntoView {
    let text = RwSignal::new(String::new());
    let material_text = RwSignal::new(String::new());
    OUTPUT.with(|slot| slot.set(Some(text)));
    MATERIAL_OUTPUT.with(|slot| slot.set(Some(material_text)));
    on_cleanup(|| {
        OUTPUT.with(|slot| slot.set(None));
        MATERIAL_OUTPUT.with(|slot| slot.set(None));
    });
    view! {
        <output class="forge-viewport-metrics" aria-live="off">{move || text.get()}</output>
        <output class="forge-viewport-metrics" aria-live="off">{move || material_text.get()}</output>
    }
}

#[derive(Default)]
pub(super) struct MaterialActivity {
    standard: bevy::ecs::message::MessageCursor<AssetEvent<StandardMaterial>>,
    village: bevy::ecs::message::MessageCursor<
        AssetEvent<forge_tidewater::tidewater_material::VillageMaterial>,
    >,
    plants: bevy::ecs::message::MessageCursor<
        AssetEvent<forge_tidewater::tidewater_plants::PlantMaterial>,
    >,
    terrain: bevy::ecs::message::MessageCursor<
        AssetEvent<forge_tidewater::tidewater_terrain::lod::LodMaterial>,
    >,
    fish: bevy::ecs::message::MessageCursor<
        AssetEvent<forge_tidewater::tidewater_fish_props::material::FishMaterial>,
    >,
    buffers: bevy::ecs::message::MessageCursor<AssetEvent<ShaderBuffer>>,
    changed: [usize; 6],
    previous: f64,
}

fn changes<A: Asset>(
    world: &World,
    cursor: &mut bevy::ecs::message::MessageCursor<AssetEvent<A>>,
) -> usize {
    world
        .get_resource::<Messages<AssetEvent<A>>>()
        .map_or(0, |events| {
            cursor
                .read(events)
                .filter(|event| {
                    matches!(
                        event,
                        AssetEvent::Added { .. } | AssetEvent::Modified { .. }
                    )
                })
                .count()
        })
}

pub(super) fn observe_materials(world: &mut World, mut activity: Local<MaterialActivity>) {
    let changed = [
        changes(world, &mut activity.standard),
        changes(world, &mut activity.village),
        changes(world, &mut activity.plants),
        changes(world, &mut activity.terrain),
        changes(world, &mut activity.fish),
        changes(world, &mut activity.buffers),
    ];
    for (total, count) in activity.changed.iter_mut().zip(changed) {
        *total += count;
    }
    let now = world.resource::<Time<Real>>().elapsed_secs_f64();
    if now - activity.previous < 2.0 {
        return;
    }
    let totals = [
        world
            .get_resource::<Assets<StandardMaterial>>()
            .map_or(0, Assets::len),
        world
            .get_resource::<Assets<forge_tidewater::tidewater_material::VillageMaterial>>()
            .map_or(0, Assets::len),
        world
            .get_resource::<Assets<forge_tidewater::tidewater_plants::PlantMaterial>>()
            .map_or(0, Assets::len),
        world
            .get_resource::<Assets<forge_tidewater::tidewater_terrain::lod::LodMaterial>>()
            .map_or(0, Assets::len),
        world
            .get_resource::<Assets<forge_tidewater::tidewater_fish_props::material::FishMaterial>>()
            .map_or(0, Assets::len),
    ];
    let prepared = world
        .resource::<crate::authoring_render::frame_pressure::FramePressure>()
        .material_counts();
    let text = format!(
        "Matériaux GPU/CPU Standard {}/{} · Village {}/{} · Plantes {}/{} · Terrain {}/{} · Poissons {}/{} · ajouts/modifications sur 2 s {:?} (buffers en dernier)",
        prepared[0],
        totals[0],
        prepared[1],
        totals[1],
        prepared[2],
        totals[2],
        prepared[3],
        totals[3],
        prepared[4],
        totals[4],
        activity.changed
    );
    MATERIAL_OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(text);
        }
    });
    activity.changed = [0; 6];
    activity.previous = now;
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
#[allow(
    clippy::cast_precision_loss,
    reason = "Approximate diagnostic byte counts, never allocation decisions"
)]
pub(super) fn observe(
    time: Res<Time<Real>>,
    images: Res<Assets<Image>>,
    buffers: Res<Assets<ShaderBuffer>>,
    meshes: Res<Assets<Mesh>>,
    pressure: Res<crate::authoring_render::frame_pressure::FramePressure>,
    mut elapsed: Local<f64>,
    mut frames: Local<u32>,
) {
    *elapsed += time.delta_secs_f64();
    *frames += 1;
    if *elapsed < 2.0 {
        return;
    }
    let image_bytes: usize = images
        .iter()
        .filter_map(|(_, image)| image.data.as_ref())
        .map(Vec::len)
        .sum();
    let buffer_bytes: u64 = buffers
        .iter()
        .map(|(_, buffer)| {
            buffer
                .data
                .as_ref()
                .map_or(buffer.buffer_description.size, |data| data.len() as u64)
        })
        .sum();
    let mesh_bytes: usize = meshes
        .iter()
        .map(|(_, mesh)| {
            mesh.try_attributes().map_or(0, |attributes| {
                attributes.map(|(_, values)| values.get_bytes().len()).sum()
            })
        })
        .sum();
    let (pending, peak) = pressure.observation();
    let text = format!(
        "Vue 3D · {:.1} mises à jour/s · {} textures ({:.0} Mio de données) · {} buffers ({:.0} Mio) · {} maillages ({:.0} Mio) · file GPU {pending} (maximum {peak}) · éclairage fixe",
        f64::from(*frames) / *elapsed,
        images.len(),
        image_bytes as f64 / 1_048_576.0,
        buffers.len(),
        buffer_bytes as f64 / 1_048_576.0,
        meshes.len(),
        mesh_bytes as f64 / 1_048_576.0
    );
    OUTPUT.with(|slot| {
        if let Some(output) = slot.get() {
            output.set(text.clone());
        }
    });
    if let Some(canvas) = web_sys::window()
        .and_then(|w| w.document())
        .and_then(|d| d.get_element_by_id("forge-authoring-canvas"))
    {
        let _ = canvas.set_attribute("data-forge-resource-observation", &text);
    }
    *elapsed = 0.0;
    *frames = 0;
}
