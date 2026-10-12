//! One transient observation path, published in the actual authoring HUD.
use super::{BridgeRun, GameView, VIEW, generation};
use crate::authoring_imported_quality_observation::{CurrentProfileInventory, snapshot};
use bevy::{ecs as bevy_ecs, prelude::*, window::PrimaryWindow};
use forge_core::{ContentHash, bytes_sha256};
use forge_engine::{
    gameplay::GameplayRuntime,
    navigation::{NavigationPolicy, NavigationRuntime},
    playtest_timing::{PlaytestTiming, PlaytestTimingSnapshot, TimingState},
};
use leptos::prelude::Update;
use serde::Serialize;
use std::cell::RefCell;

#[derive(Clone, Copy)]
pub(super) enum TimingCommand {
    Start,
    Stop,
}
thread_local! { static REQUEST: RefCell<Option<(u64, ContentHash, TimingCommand)>> = const { RefCell::new(None) }; }

pub(super) fn clear_request() {
    REQUEST.with(|request| request.replace(None));
}
pub(super) fn enqueue(view: &GameView, command: TimingCommand) {
    if view.ready
        && view.run == generation()
        && let Some(source) = view.source
    {
        REQUEST.with(|request| *request.borrow_mut() = Some((view.run, source, command)));
    }
}

#[derive(Resource, Default)]
pub(crate) struct TimingPublication {
    last_publication_seconds: Option<f64>,
    publication: u64,
    graphics: Option<crate::authoring_input::PlaytestGraphics>,
    graphics_hash: Option<ContentHash>,
    policy_hash: String,
    policy_revision: u64,
    last_state: Option<TimingState>,
    start_profile: Option<CurrentProfileInventory>,
    end_profile: Option<CurrentProfileInventory>,
    start_fingerprint: Option<ContentHash>,
    end_fingerprint: Option<ContentHash>,
    profile_error: Option<String>,
}

#[derive(Serialize)]
struct PublishedTiming<'a> {
    #[serde(flatten)]
    timing: PlaytestTimingSnapshot,
    profile_start: &'a Option<CurrentProfileInventory>,
    profile_end: &'a Option<CurrentProfileInventory>,
    profile_start_fingerprint: Option<ContentHash>,
    profile_end_fingerprint: Option<ContentHash>,
    profile_error: &'a Option<String>,
    profile_observation_scope: &'static str,
    publication: u64,
    published_loop_seconds: f64,
    publication_clock: &'static str,
    published_frame: Option<u32>,
    published_tick: u64,
}

pub(crate) fn observe(world: &mut World) {
    let run = world.resource::<BridgeRun>().id();
    if run != generation() {
        world
            .resource_mut::<PlaytestTiming>()
            .invalidate_if_active("playtest incarnation changed");
        world
            .resource_mut::<NavigationRuntime>()
            .set_timing_capture(None);
        return;
    }
    let runtime = world.resource::<GameplayRuntime>();
    let (source, tick, ready) = (
        runtime.session().source_hash(),
        runtime.session().state().tick,
        runtime.is_ready(),
    );
    let request = REQUEST
        .with(|request| request.borrow_mut().take())
        .filter(|(expected_run, expected_source, _)| {
            *expected_run == run && *expected_source == source && ready
        })
        .map(|(_, _, command)| command);
    world.resource_scope(|world, mut publication: Mut<TimingPublication>| {
        update_context(world, &mut publication, source, tick, ready, request);
        collect_profile(world, &mut publication, request);
        let timing = world.resource::<PlaytestTiming>();
        let state = timing.state();
        // Time<Real> bounds publication independently of gameplay's millisecond tick.
        let now = world.resource::<Time<Real>>().elapsed_secs_f64();
        if request.is_none() && publication.last_state == Some(state)
            && publication.last_publication_seconds.is_some_and(|last| now - last < 1.0) { return; }
        let Some(sequence) = publication.publication.checked_add(1) else { return; };
        let json = serde_json::to_string(&PublishedTiming {
            timing: timing.snapshot(), profile_start: &publication.start_profile, profile_end: &publication.end_profile,
            profile_start_fingerprint: publication.start_fingerprint, profile_end_fingerprint: publication.end_fingerprint,
            profile_error: &publication.profile_error,
            profile_observation_scope: "effective settings observed at start/end only; graphics_hash pins preferences; endpoint equality does not prove intermediate invariance or rendered GPU completion",
            publication: sequence, published_loop_seconds: now, publication_clock: "bevy Time<Real> elapsed seconds since app start; distinct from page/worker performance origins",
            published_frame: world.get_resource::<bevy::diagnostic::FrameCount>().map(|frame| frame.0), published_tick: tick,
        });
        publication.last_state = Some(state); publication.last_publication_seconds = Some(now); publication.publication = sequence;
        VIEW.with(|slot| {
            if let Some(signal) = *slot.borrow() {
                let _ = signal.try_update(|view| {
                    if view.run == run && view.source == Some(source) {
                        view.timing_state = Some(state);
                        view.timing_json = std::sync::Arc::from(json.unwrap_or_default());
                    }
                });
            }
        });
    });
}

fn update_context(
    world: &mut World,
    publication: &mut TimingPublication,
    source: ContentHash,
    tick: u64,
    ready: bool,
    request: Option<TimingCommand>,
) {
    let graphics = crate::authoring_input::graphics();
    if publication.graphics != Some(graphics) {
        publication.graphics = Some(graphics);
        publication.graphics_hash = serde_json::to_vec(&graphics)
            .ok()
            .map(|bytes| bytes_sha256(&bytes));
    }
    let policy = world.resource::<NavigationPolicy>();
    if publication.policy_hash != policy.source_hash() {
        publication.policy_hash.clear();
        publication.policy_hash.push_str(policy.source_hash());
    }
    publication.policy_revision = policy.revision();
    let viewport = world
        .query_filtered::<&Window, With<PrimaryWindow>>()
        .single(world)
        .map_or([0; 2], |window| {
            [window.physical_width(), window.physical_height()]
        });
    world.resource_scope(|world, mut timing: Mut<PlaytestTiming>| {
        match publication.graphics_hash {
            Some(graphics) => timing.context(
                source,
                graphics,
                viewport,
                &publication.policy_hash,
                publication.policy_revision,
            ),
            None => timing.invalidate_if_active("graphics observation unavailable"),
        }
        if !ready {
            timing.invalidate_if_active("simulation readiness lost");
        }
        let mut navigation = world.resource_mut::<NavigationRuntime>();
        timing.absorb_navigation(
            navigation.take_timing_samples(),
            navigation.take_timing_jobs(),
        );
        timing.note_tick(tick);
        match request {
            Some(TimingCommand::Start) => timing.start(),
            Some(TimingCommand::Stop) => timing.stop(),
            None => (),
        }
        navigation.set_timing_capture(timing.capture());
    });
}

fn collect_profile(
    world: &mut World,
    publication: &mut TimingPublication,
    request: Option<TimingCommand>,
) {
    let timing = world.resource::<PlaytestTiming>();
    let state = timing.state();
    let identity = timing.identity();
    let starting = matches!(request, Some(TimingCommand::Start));
    let ending = matches!(
        state,
        TimingState::Stopped | TimingState::LimitReached | TimingState::Invalidated
    ) && publication.end_profile.is_none()
        && publication.start_profile.is_some();
    if !starting && !ending {
        return;
    }
    let profile = snapshot(world, Some(identity));
    let fingerprint = profile.fingerprint();
    if starting {
        publication.end_profile = None;
        publication.end_fingerprint = None;
        publication.profile_error = fingerprint.as_ref().err().map(ToString::to_string);
        publication.start_fingerprint = fingerprint.ok();
        publication.start_profile = Some(profile);
    } else {
        publication.end_fingerprint = fingerprint.ok();
        publication.end_profile = Some(profile);
        if publication.start_fingerprint.is_none()
            || publication.start_fingerprint != publication.end_fingerprint
        {
            publication.profile_error =
                Some("effective profile endpoint fingerprint unavailable or changed".into());
            world.resource_mut::<PlaytestTiming>().invalidate_if_active(
                "effective profile endpoint fingerprint unavailable or changed",
            );
        }
    }
}

pub(super) fn export(view: &GameView) -> Result<(), String> {
    use wasm_bindgen::JsCast;
    if view.run != generation()
        || !matches!(
            view.timing_state,
            Some(TimingState::Stopped | TimingState::LimitReached | TimingState::Invalidated)
        )
    {
        return Err("Arrêter la collecte dans la partie active avant l'export.".into());
    }
    let document = web_sys::window()
        .and_then(|window| window.document())
        .ok_or("Document navigateur absent")?;
    let bytes = view.timing_json.as_bytes();
    let parts = js_sys::Array::of1(&js_sys::Uint8Array::from(bytes).into());
    let blob =
        web_sys::Blob::new_with_u8_array_sequence(&parts).map_err(|error| format!("{error:?}"))?;
    let url =
        web_sys::Url::create_object_url_with_blob(&blob).map_err(|error| format!("{error:?}"))?;
    let result = (|| {
        let link = document
            .create_element("a")
            .map_err(|error| format!("{error:?}"))?;
        link.set_attribute("href", &url)
            .map_err(|error| format!("{error:?}"))?;
        link.set_attribute(
            "download",
            &format!("forge-playtest-timing-{}.json", view.run),
        )
        .map_err(|error| format!("{error:?}"))?;
        link.dyn_into::<web_sys::HtmlElement>()
            .map_err(|_| "Lien de téléchargement invalide")?
            .click();
        Ok(())
    })();
    web_sys::Url::revoke_object_url(&url).map_err(|error| format!("{error:?}"))?;
    result
}
