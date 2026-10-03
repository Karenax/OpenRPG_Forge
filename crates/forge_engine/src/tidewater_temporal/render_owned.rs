// SPDX-License-Identifier: MIT
//! Explicit source-resolve ownership in Bevy's existing `RenderWorld`.
//!
//! Main submits intent and near/far. The actual extracted view supplies pose,
//! projection and size. Queue, view uniforms and resolve use one Render packet.
//! Main acknowledgement arrival is telemetry and never advances this sequence.
//! Registration remains inactive; adapters still register their real materials,
//! HDR passes and hull contract before explicitly selecting a supported camera.

use super::{
    frame,
    render_owner::{Owner, Snapshot, Status},
    resolve, targets,
};
use crate::tidewater_temporal_frame::Input;
use bevy::{
    anti_alias::taa::TemporalAntiAliasing,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        camera::{ExtractedCamera, TemporalJitter},
        extract_component::{ExtractComponent, ExtractComponentPlugin},
        extract_resource::{ExtractResource, ExtractResourcePlugin},
        sync_world::MainEntity,
        view::{ExtractedView, Msaa},
    },
};
use std::sync::{Arc, Mutex};

#[derive(Resource)]
struct Installed;

/// Extracted intent only. This is not a camera sequence or a history store.
#[derive(Resource, Clone, Default, ExtractResource)]
pub struct Control {
    selected: Option<Entity>,
    revision: u64,
    serial_floor: u64,
}
impl Control {
    #[must_use]
    pub const fn selected_view(&self) -> Option<Entity> {
        self.selected
    }
    #[must_use]
    pub const fn revision(&self) -> u64 {
        self.revision
    }
}

#[derive(Component, Clone, Copy, ExtractComponent)]
struct CameraInput {
    near: f32,
    far: f32,
}

/// Fresh Main provenance, independent of Render markers from an older frame.
#[derive(Resource, Clone, Default, ExtractResource)]
struct JitterIntent(Vec<Entity>);
impl JitterIntent {
    fn foreign(&self, main: Entity) -> bool {
        self.0.contains(&main)
    }
}

/// Render-only provenance. Never extracted from Main or inserted on a capture.
#[derive(Component)]
#[doc(hidden)]
pub struct OwnedPacket {
    _private: (),
}
#[derive(Component)]
struct OwnsRenderJitter;

/// Latest render status, observational only; the Owner itself is never shared.
#[derive(Clone, Default)]
pub struct Report {
    pub owner: Option<Snapshot>,
    pub error: Option<String>,
    pub recovery_resets: u64,
    pub intent_revision: u64,
}
#[derive(Resource, Clone, Default)]
pub struct Observation(Arc<Mutex<Report>>);
impl Observation {
    #[must_use]
    pub fn snapshot(&self) -> Option<Report> {
        self.0.lock().ok().map(|value| value.clone())
    }
    pub(super) fn publish(&self, owner: &Owner, runtime: &Runtime, error: Option<String>) {
        if let Ok(mut report) = self.0.lock() {
            *report = Report {
                owner: owner.snapshot().ok(),
                error,
                recovery_resets: runtime.recovery_resets,
                intent_revision: runtime.intent.map_or(0, |(_, revision)| revision),
            };
        }
    }
}

#[derive(Resource, Default)]
#[doc(hidden)]
pub struct Runtime {
    intent: Option<(Entity, u64)>,
    owner_revision: u64,
    missing_frame: bool,
    pub(super) recovery_resets: u64,
    retire_histories: bool,
}
impl Runtime {
    fn next_revision(&self) -> Result<u64, String> {
        self.owner_revision
            .checked_add(1)
            .ok_or_else(|| "Render temporal revision exhausted".into())
    }
    fn prepare(
        &mut self,
        control: &Control,
        owner: &Owner,
        input: Result<Input, String>,
    ) -> Result<Option<frame::ViewPacket>, String> {
        self.retire_histories = false;
        let Some(view) = control.selected else {
            if self.intent.is_some() {
                owner
                    .apply_selection(None, self.owner_revision)
                    .map_err(|e| e.to_string())?;
                self.retire_histories = true;
            }
            self.intent = None;
            self.missing_frame = false;
            return Ok(None);
        };
        if self
            .intent
            .is_some_and(|(old_view, revision)| old_view == view && control.revision < revision)
        {
            return Err("Stale Main temporal intent".into());
        }
        let changed = self.intent != Some((view, control.revision));
        let snapshot = owner.snapshot().map_err(|e| e.to_string())?;
        // Mesh models can advance on a rendered frame even if source resolve
        // cannot encode. Retaining old HDR with those newer models is invalid.
        // Before first successful resolve there is no history to discard.
        let gap = !changed
            && snapshot.last_frame.is_some()
            && (self.missing_frame || matches!(snapshot.status, Status::Pending { .. }));
        if changed || gap {
            let revision = self.next_revision()?;
            owner
                .raise_serial_floor(control.serial_floor)
                .map_err(|e| e.to_string())?;
            owner
                .apply_selection(Some(view), revision)
                .map_err(|e| e.to_string())?;
            self.owner_revision = revision;
            self.intent = Some((view, control.revision));
            self.retire_histories = true;
            if gap {
                self.recovery_resets = self.recovery_resets.saturating_add(1);
            }
        }
        let input = match input {
            Ok(input) => input,
            Err(error) => {
                self.missing_frame = true;
                return Err(error);
            }
        };
        let mut packet = match owner.prepare(view, input) {
            Ok(packet) => packet,
            Err(error) => {
                self.missing_frame = true;
                return Err(error.to_string());
            }
        };
        self.missing_frame = false;
        // Bit 1 forces previous rigid models to current for a new/reset/gap
        // seed. A successful resize preserves previous models and camera.
        if owner
            .snapshot()
            .map_err(|e| e.to_string())?
            .last_frame
            .is_none()
        {
            packet.packet.phase_epoch_seed.w |= 2;
        }
        Ok(Some(packet))
    }
}

pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Installed>() {
        return Ok(());
    }
    resolve::register(app)?;
    let observation = Observation::default();
    app.insert_resource(Installed)
        .init_resource::<Control>()
        .insert_resource(observation.clone())
        .add_plugins((
            ExtractResourcePlugin::<Control>::default(),
            ExtractResourcePlugin::<JitterIntent>::default(),
            ExtractComponentPlugin::<CameraInput>::default(),
        ))
        .add_systems(
            PostUpdate,
            stage_camera_input
                .after(super::FrameSystems::Prepare)
                .before(super::FrameSystems::Jitter),
        );
    app.init_resource::<JitterIntent>().add_systems(
        PostUpdate,
        track_jitter
            .after(super::FrameSystems::Jitter)
            .before(crate::tidewater_refraction::Synchronize),
    );
    app.sub_app_mut(RenderApp)
        .init_resource::<Owner>()
        .init_resource::<Runtime>()
        .insert_resource(observation)
        // Bevy chooses TEMPORAL_JITTER in PrepareAssets, before Queue. Apply
        // this frame's packet/marker after extraction and before that choice;
        // view uniforms and both target preparations also run later.
        .add_systems(
            Render,
            prepare
                .after(RenderSystems::ExtractCommands)
                .before(RenderSystems::PrepareAssets),
        );
    Ok(())
}

/// Explicit opt-in. Validation finishes before changing intent or markers.
/// Calling again for the same view is a no-op; reset is a separate operation.
pub fn select(world: &mut World, view: Option<Entity>) -> Result<(), String> {
    let control = world
        .get_resource::<Control>()
        .ok_or("Render temporal owner is not registered")?;
    if control.selected == view {
        return Ok(());
    }
    let revision = control
        .revision
        .checked_add(1)
        .ok_or("Main temporal revision exhausted")?;
    if let Some(view) = view {
        super::validate_camera_selection(world, Some(view))?;
        resolve::validate_camera(world, view).map_err(|e| e.to_string())?;
    }
    let floor = world
        .resource_mut::<frame::Frames>()
        .begin_render_owned(view);
    // Preflight above validated the exact constraints of activation. Its only
    // remaining checks are the selection and owner just set by begin_render_owned.
    resolve::activate(world, view).map_err(|e| e.to_string())?;
    let mut control = world.resource_mut::<Control>();
    control.selected = view;
    control.revision = revision;
    control.serial_floor = floor;
    Ok(())
}

/// Explicit lifecycle cut; resizing a viewport must not call this operation.
pub fn reset(world: &mut World, view: Entity) -> Result<(), String> {
    let mut control = world
        .get_resource_mut::<Control>()
        .ok_or("Render temporal owner is not registered")?;
    if control.selected != Some(view) {
        return Err("Render temporal view is not selected".into());
    }
    control.revision = control
        .revision
        .checked_add(1)
        .ok_or("Main temporal revision exhausted")?;
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy camera staging receives commands and queries by value"
)]
fn stage_camera_input(
    mut commands: Commands,
    control: Res<Control>,
    cameras: Query<
        (Entity, &Camera, &Projection),
        Without<crate::tidewater_refraction::CaptureCamera>,
    >,
    previous: Query<Entity, With<CameraInput>>,
) {
    for entity in &previous {
        if Some(entity) != control.selected {
            commands.entity(entity).remove::<CameraInput>();
        }
    }
    let Some(view) = control.selected else {
        return;
    };
    match cameras.get(view) {
        Ok((_, camera, Projection::Perspective(projection))) if camera.is_active => {
            commands.entity(view).insert(CameraInput {
                near: projection.near,
                far: projection.far,
            });
        }
        _ => {
            commands.entity(view).remove::<CameraInput>();
        }
    }
}

fn extracted_input(
    view: &ExtractedView,
    camera: &ExtractedCamera,
    input: CameraInput,
    msaa: Msaa,
    foreign_taa: bool,
    foreign_jitter: bool,
) -> Result<Input, String> {
    let size = camera
        .physical_viewport_size
        .ok_or("Missing extracted viewport")?;
    if !camera.hdr
        || msaa.samples() != 1
        || foreign_taa
        || foreign_jitter
        || view.viewport != UVec4::new(0, 0, size.x, size.y)
        || camera.physical_target_size != Some(size)
    {
        return Err(
            "Render temporal camera requires full HDR viewport, MSAA off and exclusive jitter"
                .into(),
        );
    }
    Ok(Input {
        world_from_view: view.world_from_view.to_matrix(),
        clip_from_view: view.clip_from_view,
        input_size: size,
        output_size: size,
        near: input.near,
        far: input.far,
    })
}

#[allow(
    clippy::type_complexity,
    reason = "The query tuple names the independent jitter ownership components"
)]
fn track_jitter(
    mut intent: ResMut<JitterIntent>,
    cameras: Query<
        (
            Entity,
            Option<&TemporalJitter>,
            Option<&TemporalAntiAliasing>,
            Option<&super::OwnsJitter>,
        ),
        (
            With<Camera>,
            Without<crate::tidewater_refraction::CaptureCamera>,
        ),
    >,
) {
    intent.0.clear();
    for (entity, jitter, taa, legacy) in &cameras {
        if taa.is_some() || (jitter.is_some() && legacy.is_none()) {
            intent.0.push(entity);
        }
    }
}

fn preserve_extracted_jitter(
    intent: &JitterIntent,
    main: Option<&MainEntity>,
    taa: bool,
    capture: Option<&crate::tidewater_refraction::CaptureSource>,
) -> bool {
    taa || main.is_some_and(|main| intent.foreign(main.id()))
        || capture.is_some_and(|capture| intent.foreign(capture.view))
}

#[allow(
    clippy::too_many_arguments,
    clippy::type_complexity,
    clippy::needless_pass_by_value,
    reason = "This ordered Bevy render preparation system owns the selected view"
)]
fn prepare(
    mut commands: Commands,
    control: Res<Control>,
    owner: Res<Owner>,
    mut runtime: ResMut<Runtime>,
    observation: Res<Observation>,
    jitter_intent: Res<JitterIntent>,
    mut auxiliary: ResMut<targets::Allocator>,
    mut resolve: ResMut<resolve::Allocator>,
    views: Query<
        (
            Entity,
            &MainEntity,
            &ExtractedView,
            &ExtractedCamera,
            &CameraInput,
            &Msaa,
            Option<&TemporalJitter>,
            Option<&OwnsRenderJitter>,
            Option<&TemporalAntiAliasing>,
        ),
        (
            With<resolve::ResolveEnabled>,
            Without<crate::tidewater_refraction::CaptureCamera>,
        ),
    >,
    captures: Query<
        (
            Entity,
            &crate::tidewater_refraction::CaptureSource,
            &ExtractedView,
            &ExtractedCamera,
        ),
        With<crate::tidewater_refraction::CaptureCamera>,
    >,
    old_packets: Query<Entity, With<OwnedPacket>>,
    old_jitter: Query<
        (
            Entity,
            Option<&MainEntity>,
            Option<&TemporalAntiAliasing>,
            Option<&crate::tidewater_refraction::CaptureSource>,
        ),
        With<OwnsRenderJitter>,
    >,
) {
    for entity in &old_packets {
        commands
            .entity(entity)
            .remove::<(OwnedPacket, frame::ViewPacket)>();
    }
    for (entity, main, taa, capture) in &old_jitter {
        let foreign = preserve_extracted_jitter(&jitter_intent, main, taa.is_some(), capture);
        commands.entity(entity).remove::<OwnsRenderJitter>();
        if !foreign {
            commands.entity(entity).remove::<TemporalJitter>();
        }
    }
    let mut selected = views
        .iter()
        .filter(|(_, main, ..)| Some(main.id()) == control.selected);
    let current = selected.next();
    let input = if selected.next().is_some() {
        Err("Multiple render views for selected Main camera".into())
    } else if let Some((_, main, view, camera, input, msaa, jitter, ours, taa)) = current {
        extracted_input(
            view,
            camera,
            *input,
            *msaa,
            taa.is_some(),
            jitter_intent.foreign(main.id()) || (jitter.is_some() && ours.is_none()),
        )
    } else {
        Err("Selected temporal camera is not extracted".into())
    };
    let result = runtime.prepare(&control, &owner, input);
    if runtime.retire_histories {
        auxiliary.retire_all();
        resolve.retire_all();
    }
    let result = result.and_then(|packet| {
        let Some(packet) = packet else {
            return Ok(());
        };
        let (entity, _, main_view, ..) =
            current.ok_or("Selected temporal Render Entity missing")?;
        for (capture, source, view, camera) in &captures {
            if source.view != packet.tag.view {
                continue;
            }
            let size = camera
                .physical_viewport_size
                .ok_or("Missing extracted capture viewport")?;
            if view.clip_from_view != main_view.clip_from_view
                || view.world_from_view != main_view.world_from_view
            {
                return Err(
                    "Refraction capture does not mirror the extracted source camera".into(),
                );
            }
            commands.entity(capture).insert((
                OwnsRenderJitter,
                TemporalJitter {
                    offset: packet.frame.jitter_pixels * size.as_vec2()
                        / packet.frame.input_size.as_vec2(),
                },
            ));
        }
        commands.entity(entity).insert((
            OwnedPacket { _private: () },
            TemporalJitter {
                offset: packet.frame.jitter_pixels,
            },
            OwnsRenderJitter,
            packet,
        ));
        Ok(())
    });
    if result.is_err() {
        runtime.missing_frame = true;
    }
    observation.publish(&owner, &runtime, result.err());
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn fresh_main_jitter_attestation_excludes_legacy_and_derived_capture() {
        let mut app = App::new();
        app.init_resource::<JitterIntent>()
            .add_systems(Update, track_jitter);
        let foreign = app
            .world_mut()
            .spawn((Camera::default(), TemporalJitter::default()))
            .id();
        let taa = app
            .world_mut()
            .spawn((Camera::default(), TemporalAntiAliasing::default()))
            .id();
        let legacy = app
            .world_mut()
            .spawn((
                Camera::default(),
                TemporalJitter::default(),
                super::super::OwnsJitter,
            ))
            .id();
        let capture = app
            .world_mut()
            .spawn((
                Camera::default(),
                TemporalJitter::default(),
                crate::tidewater_refraction::CaptureCamera,
            ))
            .id();
        app.update();
        let intent = app.world().resource::<JitterIntent>();
        assert!(intent.foreign(foreign));
        assert!(intent.foreign(taa));
        assert!(!intent.foreign(legacy));
        assert!(!intent.foreign(capture));
        assert_eq!(intent.0.len(), 2);
    }
    #[test]
    fn cleanup_preserves_new_main_or_capture_owner_using_main_identity() {
        let main = MainEntity::from(view());
        let intent = JitterIntent(vec![main.id()]);
        let source = crate::tidewater_refraction::CaptureSource { view: main.id() };
        assert!(preserve_extracted_jitter(&intent, Some(&main), false, None));
        let unrelated = MainEntity::from(Entity::from_raw_u32(900).unwrap());
        assert!(preserve_extracted_jitter(
            &intent,
            Some(&unrelated),
            false,
            Some(&source)
        ));
        assert!(!preserve_extracted_jitter(
            &intent,
            Some(&unrelated),
            false,
            None
        ));
        assert!(preserve_extracted_jitter(
            &JitterIntent::default(),
            Some(&unrelated),
            true,
            None
        ));
    }
    fn view() -> Entity {
        Entity::from_raw_u32(42).unwrap()
    }
    fn input(x: f32, size: UVec2) -> Input {
        Input {
            world_from_view: Mat4::from_translation(Vec3::new(x, 2.0, 3.0)),
            clip_from_view: Mat4::perspective_infinite_reverse_rh(
                1.0,
                size.as_vec2().x / size.as_vec2().y,
                0.06,
            ),
            input_size: size,
            output_size: size,
            near: 0.06,
            far: 60_000.0,
        }
    }
    fn control() -> Control {
        Control {
            selected: Some(view()),
            revision: 1,
            serial_floor: 17,
        }
    }
    fn prepare(runtime: &mut Runtime, owner: &Owner, x: f32) -> frame::ViewPacket {
        runtime
            .prepare(&control(), owner, Ok(input(x, UVec2::new(1920, 1080))))
            .unwrap()
            .unwrap()
    }
    fn commit(owner: &Owner, p: &frame::ViewPacket) {
        owner
            .commit_resolve(p.tag, p.frame.history_epoch, 1, 2, 1, true, true)
            .unwrap();
    }
    #[test]
    fn inactive_intent_never_prepares_an_owner() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        assert!(
            runtime
                .prepare(&Control::default(), &owner, Err("No camera".into()))
                .unwrap()
                .is_none()
        );
        assert_eq!(owner.snapshot().unwrap().prepared, 0);
    }
    #[test]
    fn adoption_serial_floor_seed_and_last_rendered_camera() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        let first = prepare(&mut runtime, &owner, 1.0);
        assert_eq!(first.tag.serial, 18);
        assert_eq!(first.packet.phase_epoch_seed.w, 3);
        commit(&owner, &first);
        let next = prepare(&mut runtime, &owner, 4.0);
        assert_eq!(
            next.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert_eq!(next.packet.phase_epoch_seed.w, 0);
        assert_eq!(next.frame.phase, 1);
        assert!(!runtime.retire_histories);
    }
    #[test]
    fn warmup_skip_keeps_allocation_but_gap_after_commit_reseeds_objects_and_camera() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        let warmup = prepare(&mut runtime, &owner, 1.0);
        let first = prepare(&mut runtime, &owner, 2.0);
        assert_eq!(warmup.frame.history_epoch, first.frame.history_epoch);
        assert!(!runtime.retire_histories);
        commit(&owner, &first);
        let skipped = prepare(&mut runtime, &owner, 3.0);
        let recovered = prepare(&mut runtime, &owner, 4.0);
        assert_eq!(recovered.packet.phase_epoch_seed.w, 3);
        assert_eq!(recovered.frame.phase, 0);
        assert!(runtime.retire_histories);
        assert_eq!(
            recovered.frame.previous_camera_position,
            recovered.frame.camera_position
        );
        assert!(recovered.frame.history_epoch > skipped.frame.history_epoch);
        assert!(
            owner
                .validate_pending(skipped.tag, skipped.frame.history_epoch)
                .is_err()
        );
        assert_eq!(runtime.recovery_resets, 1);
    }
    #[test]
    fn successful_resize_keeps_previous_models_phase_and_camera() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        let first = prepare(&mut runtime, &owner, 1.0);
        commit(&owner, &first);
        let resized = runtime
            .prepare(&control(), &owner, Ok(input(2.0, UVec2::new(1919, 1079))))
            .unwrap()
            .unwrap();
        assert_eq!(resized.packet.phase_epoch_seed.w, 1);
        assert_eq!(resized.frame.phase, 1);
        assert_eq!(
            resized.frame.previous_camera_position,
            first.frame.camera_position
        );
        assert!(!runtime.retire_histories);
    }
    #[test]
    fn unavailable_view_after_commit_cuts_before_recovery() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        let first = prepare(&mut runtime, &owner, 1.0);
        commit(&owner, &first);
        assert!(
            runtime
                .prepare(&control(), &owner, Err("Camera unavailable".into()))
                .is_err()
        );
        let recovered = prepare(&mut runtime, &owner, 8.0);
        assert_eq!(recovered.packet.phase_epoch_seed.w, 3);
        assert_eq!(
            recovered.frame.previous_camera_position,
            recovered.frame.camera_position
        );
    }
    #[test]
    fn switch_deselect_return_is_fresh_and_intent_revision_is_idempotent() {
        let owner = Owner::default();
        let mut runtime = Runtime::default();
        let first = prepare(&mut runtime, &owner, 1.0);
        commit(&owner, &first);
        let mut intent = control();
        intent.revision += 1;
        let cut = runtime
            .prepare(&intent, &owner, Ok(input(2.0, UVec2::new(1920, 1080))))
            .unwrap()
            .unwrap();
        commit(&owner, &cut);
        let next = runtime
            .prepare(&intent, &owner, Ok(input(3.0, UVec2::new(1920, 1080))))
            .unwrap()
            .unwrap();
        assert_eq!(next.frame.phase, 1);
        commit(&owner, &next);
        assert!(
            runtime
                .prepare(&control(), &owner, Ok(input(0.0, UVec2::ONE)))
                .is_err()
        );
        assert!(
            runtime
                .prepare(&Control::default(), &owner, Err("No camera".into()))
                .unwrap()
                .is_none()
        );
        let returned = prepare(&mut runtime, &owner, 7.0);
        assert_eq!(returned.frame.phase, 0);
        assert!(returned.tag.serial > next.tag.serial);
        assert_eq!(returned.packet.phase_epoch_seed.w, 3);
    }
}
