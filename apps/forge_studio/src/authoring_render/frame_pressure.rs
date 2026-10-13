//! Wait for submitted GPU work before requesting another editing frame.
//! DOM authoring controls keep their own event loop while Bevy waits.
use bevy::{
    ecs as bevy_ecs,
    pbr::PreparedMaterial,
    prelude::*,
    render as bevy_render,
    render::{
        Render, RenderApp, RenderSystems,
        erased_render_asset::ErasedRenderAssets,
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        renderer::RenderQueue,
    },
    winit::{EventLoopProxyWrapper, UpdateMode, WinitSettings, WinitUserEvent},
};
use std::{
    sync::{
        Arc, Mutex,
        atomic::{AtomicUsize, Ordering},
    },
    time::Duration,
};

type Wake = Arc<dyn Fn() + Send + Sync>;

#[derive(Resource, Clone, Default, ExtractResource)]
pub(crate) struct FramePressure {
    pending: Arc<AtomicUsize>,
    peak: Arc<AtomicUsize>,
    material_counts: Arc<[AtomicUsize; 5]>,
    wake: Arc<Mutex<Option<Wake>>>,
}

impl FramePressure {
    pub(crate) fn observation(&self) -> (usize, usize) {
        (
            self.pending.load(Ordering::Acquire),
            self.peak.load(Ordering::Acquire),
        )
    }
    pub(crate) fn material_counts(&self) -> [usize; 5] {
        std::array::from_fn(|index| self.material_counts[index].load(Ordering::Acquire))
    }
}

pub(super) fn install(app: &mut App) {
    app.init_resource::<FramePressure>()
        .add_plugins(ExtractResourcePlugin::<FramePressure>::default())
        .add_systems(Last, regulate);
    // RenderSystems::Render submits the graph's command buffers. The completion
    // callback covers that submission, including all earlier queue work.
    app.sub_app_mut(RenderApp)
        .add_systems(Render, submitted.after(RenderSystems::Render));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn regulate(
    pressure: Res<FramePressure>,
    proxy: Res<EventLoopProxyWrapper>,
    mut settings: ResMut<WinitSettings>,
) {
    if let Ok(mut wake) = pressure.wake.lock() {
        if wake.is_none() {
            let proxy = (**proxy).clone();
            *wake = Some(Arc::new(move || {
                let _ = proxy.send_event(WinitUserEvent::WakeUp);
            }));
        }
        if pressure.pending.load(Ordering::Acquire) > 0 {
            // WakeUp is emitted by GPU completion. Input remains buffered in
            // winit and is consumed in the next complete authoring frame.
            let waiting = UpdateMode::Reactive {
                wait: Duration::MAX,
                react_to_device_events: false,
                react_to_user_events: true,
                react_to_window_events: false,
            };
            settings.focused_mode = waiting;
            settings.unfocused_mode = waiting;
            return;
        }
    }
    settings.focused_mode = UpdateMode::reactive(Duration::from_millis(16));
    settings.unfocused_mode = UpdateMode::reactive_low_power(Duration::from_millis(250));
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
fn submitted(
    queue: Res<RenderQueue>,
    pressure: Res<FramePressure>,
    materials: Res<ErasedRenderAssets<PreparedMaterial>>,
) {
    let kinds = [
        std::any::TypeId::of::<StandardMaterial>(),
        std::any::TypeId::of::<forge_engine::tidewater_material::VillageMaterial>(),
        std::any::TypeId::of::<forge_engine::tidewater_plants::PlantMaterial>(),
        std::any::TypeId::of::<forge_engine::tidewater_terrain::lod::LodMaterial>(),
        std::any::TypeId::of::<forge_engine::tidewater_fish_props::material::FishMaterial>(),
    ];
    for (index, kind) in kinds.iter().enumerate() {
        self::store_count(
            &pressure,
            index,
            materials
                .iter()
                .filter(|(id, _)| id.type_id() == *kind)
                .count(),
        );
    }
    let pending = pressure.pending.fetch_add(1, Ordering::AcqRel) + 1;
    pressure.peak.fetch_max(pending, Ordering::AcqRel);
    let pressure = pressure.clone();
    queue.on_submitted_work_done(move || {
        if pressure.pending.fetch_sub(1, Ordering::AcqRel) == 1
            && let Ok(wake) = pressure.wake.lock()
            && let Some(wake) = wake.as_ref()
        {
            wake();
        }
    });
}

fn store_count(pressure: &FramePressure, index: usize, count: usize) {
    pressure.material_counts[index].store(count, Ordering::Release);
}
