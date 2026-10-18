//! Bounded shared GPU transport; no per-object material and no animated range keys.
use super::{TemporalLod, TemporalLodPolicy, TemporalLodSample};
use bevy::{
    asset::uuid_handle,
    camera::visibility::{VisibilityRange, VisibilitySystems},
    diagnostic::FrameCount,
    prelude::*,
    render::{render_resource::ShaderType, storage::ShaderBuffer},
    transform::TransformSystems,
};
use std::collections::{HashMap, HashSet};

pub(crate) const BUFFER: Handle<ShaderBuffer> =
    uuid_handle!("9d99b2de-e1b1-455a-9ce0-21e34a0d3221");
#[cfg(any(feature = "physics-rapier", test))]
const MAX_GROUPS: usize = 32_768;

#[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
pub(crate) enum GroupKey {
    #[cfg(any(feature = "physics-rapier", test))]
    Tree(forge_world::VegetationInstanceId),
    #[cfg(any(feature = "physics-rapier", test))]
    Kit(Entity),
}

#[derive(Clone, Component)]
pub(crate) struct Member {
    pub slot: u32,
    pub level: u32,
    #[cfg(any(feature = "physics-rapier", test))]
    pub original_tag: u32,
    pub original_range: VisibilityRange,
}

#[derive(Clone, Component)]
pub(crate) struct BatchMembers {
    pub slots: Vec<u32>,
    pub original_range: VisibilityRange,
}

#[derive(Clone, Copy, Default, PartialEq, ShaderType)]
struct Record {
    codes: IVec4,
    metadata: UVec4,
}

#[cfg(feature = "qualification")]
pub(crate) struct Diagnostic {
    pub slot: u32,
    pub codes: [i32; 4],
    pub active: bool,
}

struct Group {
    key: GroupKey,
    root: Vec3,
    state: Option<TemporalLod>,
    policy: TemporalLodPolicy,
    last_registration_frame: u32,
    acknowledged_epoch: u64,
}

#[derive(Resource, Default)]
#[allow(
    clippy::struct_field_names,
    reason = "The resource owns group states and their separate GPU records"
)]
pub(crate) struct Groups {
    keys: HashMap<GroupKey, usize>,
    groups: Vec<Option<Group>>,
    records: Vec<Record>,
    frame: u32,
    dirty: bool,
    free: Vec<usize>,
    live: HashSet<u32>,
    levels: HashMap<u32, u8>,
    uploaded_records: usize,
}

impl Groups {
    /// New geometry must receive fresh GPU evidence even when its distance policy is unchanged.
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn invalidate(&mut self, slot: u32) {
        let Some(index) = slot.checked_sub(1).map(|index| index as usize) else {
            return;
        };
        if let Some(Some(group)) = self.groups.get_mut(index) {
            group.last_registration_frame = self.frame.saturating_add(1);
            group.state = None;
            self.records[index + 1].metadata.x = 0;
            self.dirty = true;
        }
    }
    #[cfg(any(feature = "physics-rapier", test))]
    pub(crate) fn register(
        &mut self,
        key: GroupKey,
        root: Vec3,
        tag: u32,
        policy: TemporalLodPolicy,
    ) -> Result<u32, &'static str> {
        if !root.is_finite() {
            return Err("non-finite temporal LOD root");
        }
        TemporalLod::new(policy, 0).map_err(|_| "invalid LOD policy")?;
        if self.records.is_empty() {
            self.records.push(Record::default());
        }
        if let Some(&index) = self.keys.get(&key) {
            let group = self.groups[index]
                .as_mut()
                .ok_or("retired temporal LOD key")?;
            group.root = root;
            if group.policy != policy {
                group.policy = policy;
                group.state = None;
                group.last_registration_frame = self.frame.saturating_add(1);
                self.records[index + 1].metadata.x = 0;
                self.dirty = true;
            }
            // Re-registration may receive an encoded MeshTag: preserve the authored tag.
            return u32::try_from(index + 1).map_err(|_| "LOD index overflow");
        }
        let index = self.free.pop().unwrap_or(self.groups.len());
        if index >= MAX_GROUPS {
            return Err("temporal LOD group budget exhausted");
        }
        let group = Some(Group {
            key,
            root,
            policy,
            state: None,
            last_registration_frame: self.frame.saturating_add(1),
            acknowledged_epoch: 0,
        });
        let record = Record {
            codes: IVec4::ZERO,
            metadata: UVec4::new(0, tag, 0, 0),
        };
        if index == self.groups.len() {
            self.groups.push(group);
            self.records.push(record);
        } else {
            self.groups[index] = group;
            self.records[index + 1] = record;
        }
        self.keys.insert(key, index);
        self.dirty = true;
        u32::try_from(index + 1).map_err(|_| "LOD index overflow")
    }

    fn collect_retired(&mut self, live: &HashSet<u32>) {
        for (index, entry) in self.groups.iter_mut().enumerate() {
            let slot = u32::try_from(index + 1).expect("bounded group index");
            if !live.contains(&slot)
                && let Some(group) = entry.take()
            {
                self.keys.remove(&group.key);
                self.records[index + 1] = Record::default();
                self.free.push(index);
                self.dirty = true;
            }
        }
    }

    fn record(&self, slot: u32) -> Option<&Record> {
        self.records.get(slot as usize)
    }
    pub(crate) fn active(&self, slot: u32) -> bool {
        self.record(slot)
            .is_some_and(|record| record.metadata.x == 1)
    }

    /// Shared transport observations for explicit capture; not GPU timing evidence.
    #[cfg(feature = "qualification")]
    pub(crate) fn diagnostic_slot(&self, slot: u32) -> Option<(bool, [i32; 4], bool)> {
        let record = self.record(slot)?;
        let group = self.groups.get(slot.checked_sub(1)? as usize)?.as_ref()?;
        Some((
            record.metadata.x == 1,
            record.codes.to_array(),
            group.state.is_some_and(|state| state.fade.is_some()),
        ))
    }

    /// Allocates only on an explicit diagnostic request, not during renderer updates.
    #[cfg(feature = "qualification")]
    pub(crate) fn diagnostic(&self) -> Vec<Diagnostic> {
        self.groups
            .iter()
            .enumerate()
            .filter_map(|(index, group)| {
                group.as_ref()?;
                let record = self.records[index + 1];
                Some(Diagnostic {
                    slot: u32::try_from(index + 1).expect("bounded group index"),
                    codes: record.codes.to_array(),
                    active: record.metadata.x == 1,
                })
            })
            .collect()
    }
}

/// Exactly two immutable keys; animating `VisibilityRange` grows Bevy's retained GPU table.
pub(crate) fn broad_range(visible: bool) -> VisibilityRange {
    VisibilityRange {
        start_margin: if visible {
            0.0..0.0
        } else {
            1_000_000.0..1_000_001.0
        },
        end_margin: 1_000_002.0..1_000_003.0,
        use_aabb: false,
    }
}

pub(crate) fn install(app: &mut App) {
    if app.world().contains_resource::<Groups>() {
        return;
    }
    app.init_resource::<Groups>();
    if !app.world().contains_resource::<Assets<ShaderBuffer>>() {
        app.init_asset::<ShaderBuffer>();
    }
    app.world_mut()
        .resource_mut::<Assets<ShaderBuffer>>()
        .insert(BUFFER.id(), ShaderBuffer::from(vec![Record::default()]))
        .expect("constant LOD buffer UUID is valid");
    crate::vegetation::install_temporal_material(app);
    app.add_systems(
        PostUpdate,
        update
            .before(VisibilitySystems::CheckVisibility)
            .after(TransformSystems::Propagate),
    );
}

#[cfg(feature = "physics-rapier")]
fn gpu_ready(quality: Option<&crate::GlobalQualityStats>, frame: u32, registered: u32) -> bool {
    let Some(quality) = quality else {
        return false;
    };
    let submission = &quality.gpu_submission;
    quality.application.as_ref().is_some_and(|application| {
        application.gpu_ready
            && application.error.is_none()
            && application.identity.is_some()
            && application.identity == submission.identity
    }) && submission.renderer_epoch > 0
        && submission.assets.inventory_ready
        && submission.pipelines_total > 0
        && submission.pipelines_pending == 0
        && submission.pipelines_failed == 0
        && submission.queue_work_completed
        && submission.matching_view_ready
        && frame
            .checked_sub(submission.observed_frame)
            .is_some_and(|age| age <= 2)
        && submission
            .completed_frame
            .is_some_and(|completed| completed >= registered)
}

/// A started fade has already acquired both representations. Unrelated pipeline
/// discovery may renew the global plan receipt without evicting these resources.
#[cfg(feature = "physics-rapier")]
fn prepared_epoch(quality: Option<&crate::GlobalQualityStats>, frame: u32) -> Option<u64> {
    let submission = &quality?.gpu_submission;
    (submission.renderer_epoch > 0
        && submission.assets.inventory_ready
        && submission.pipelines_failed == 0
        && frame
            .checked_sub(submission.observed_frame)
            .is_some_and(|age| age <= 2))
    .then_some(submission.renderer_epoch)
}

fn codes(sample: TemporalLodSample, outgoing: usize) -> IVec4 {
    let mut codes = [16; 4];
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Coverage is bounded to 17 integer levels"
    )]
    let progress = ((1.0 - sample.weights[outgoing]) * 16.0).round() as i32;
    codes[outgoing] = progress;
    for (level, weight) in sample.weights.iter().enumerate() {
        if level != outgoing && *weight > 0.0 {
            codes[level] = progress - 16;
        }
    }
    IVec4::from_array(codes)
}

#[allow(
    clippy::too_many_lines,
    reason = "One coherent ECS snapshot covers receipt, lifetime, selection and upload"
)]
pub(crate) fn update(world: &mut World) {
    let frame = world
        .get_resource::<FrameCount>()
        .map_or(0, |frame| frame.0);
    let delta = world.get_resource::<Time>().map_or(0.0, Time::delta_secs);
    let mut cameras = world.query_filtered::<(&Camera, &GlobalTransform), With<Camera3d>>();
    let mut active = cameras.iter(world).filter(|(camera, _)| camera.is_active);
    let camera = active.next().map(|(_, transform)| transform.translation());
    let camera = camera.filter(|_| active.next().is_none());
    #[cfg(feature = "physics-rapier")]
    let (completed, prepared) = {
        let quality = world.get_resource::<crate::GlobalQualityStats>();
        let completed = gpu_ready(quality, frame, 0)
            .then(|| quality.and_then(|q| q.gpu_submission.completed_frame))
            .flatten();
        (completed, prepared_epoch(quality, frame))
    };
    // Without the quality backend, no renderer-completion receipt exists.
    // Retain the selected representation instead of inventing a ready fade.
    #[cfg(not(feature = "physics-rapier"))]
    let (completed, prepared) = (None::<u32>, None::<u64>);
    world.resource_scope(|world, mut groups: Mut<Groups>| {
        groups.frame = frame;
        let mut live = std::mem::take(&mut groups.live);
        live.clear();
        groups.levels.clear();
        for member in world.query::<&Member>().iter(world) {
            live.insert(member.slot);
            if member.level < 3 {
                *groups.levels.entry(member.slot).or_default() |= 1 << member.level;
            }
        }
        live.extend(
            world
                .query::<&BatchMembers>()
                .iter(world)
                .flat_map(|batch| batch.slots.iter().copied()),
        );
        groups.collect_retired(&live);
        groups.live = live;
        let groups = &mut *groups;
        for (index, entry) in groups.groups.iter_mut().enumerate() {
            let Some(group) = entry else {
                continue;
            };
            let record = &mut groups.records[index + 1];
            let previous = *record;
            if let Some(camera) = camera {
                let slot = u32::try_from(index + 1).expect("bounded group index");
                let newly_ready =
                    completed.is_some_and(|completed| completed >= group.last_registration_frame);
                let acquired_fade = group.state.is_some_and(|state| state.fade.is_some())
                    && prepared == Some(group.acknowledged_epoch);
                if groups.levels.get(&slot) == Some(&0b111) && (newly_ready || acquired_fade) {
                    if newly_ready {
                        group.acknowledged_epoch = prepared.unwrap_or(0);
                    }
                    let distance = camera.distance(group.root);
                    if group.state.is_none() {
                        let initial = group.policy.thresholds_m[..group.policy.levels - 1]
                            .iter()
                            .filter(|threshold| distance >= **threshold)
                            .count();
                        group.state = TemporalLod::new(group.policy, initial).ok();
                    }
                    if let Some(state) = &mut group.state {
                        let outgoing = state.current;
                        if let Ok(sample) = state.advance(distance, delta, [true; 4]) {
                            record.codes = codes(sample, outgoing);
                            record.metadata.x = 1;
                        }
                    }
                }
                // Unprepared resources never start a new transition. A previously
                // acquired fade finishes while its GPU assets and device remain valid.
            } else {
                record.metadata.x = 0;
                group.state = None;
            }
            groups.dirty |= *record != previous;
        }
        for (member, mut current) in world
            .query::<(&Member, &mut VisibilityRange)>()
            .iter_mut(world)
        {
            let range = if groups.active(member.slot) {
                let code = groups.records[member.slot as usize].codes[member.level.min(3) as usize];
                broad_range(code.abs() < 16)
            } else {
                member.original_range.clone()
            };
            if *current != range {
                *current = range;
            }
        }
        for (batch, mut current) in world
            .query::<(&BatchMembers, &mut VisibilityRange)>()
            .iter_mut(world)
        {
            let any_active = batch.slots.iter().any(|slot| groups.active(*slot));
            let range = if any_active {
                broad_range(batch.slots.iter().any(|slot| {
                    !groups.active(*slot)
                        || groups
                            .record(*slot)
                            .is_some_and(|record| record.codes.z.abs() < 16)
                }))
            } else {
                batch.original_range.clone()
            };
            if *current != range {
                *current = range;
            }
        }
        if groups.dirty {
            let resized = groups.uploaded_records != groups.records.len();
            let mut uploaded = false;
            if let Some(mut buffers) = world.get_resource_mut::<Assets<ShaderBuffer>>()
                && let Some(mut buffer) = buffers.get_mut(&BUFFER)
            {
                buffer.set_data(groups.records.clone());
                groups.dirty = false;
                groups.uploaded_records = groups.records.len();
                uploaded = true;
            }
            // A resized ShaderBuffer owns a new GPU allocation. Explicitly reprepare
            // dependent bind groups, including static kit materials not clock-updated.
            if uploaded
                && resized
                && let Some(mut materials) = world
                    .get_resource_mut::<Assets<crate::vegetation::VisualValleyVegetationMaterial>>()
            {
                for (_, material) in materials.iter_mut() {
                    if material.extension.temporal_lod_buffer == BUFFER {
                        material.extension.temporal_lod_buffer = BUFFER;
                    }
                }
            }
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    fn policy() -> TemporalLodPolicy {
        TemporalLodPolicy::new(&[24.0, 80.0], 2.0, 0.2).unwrap()
    }

    fn retained(code: i32, rank: i32) -> bool {
        code == 0
            || (code.abs() < 16
                && if code > 0 {
                    rank < 16 - code
                } else {
                    rank >= -code
                })
    }

    #[test]
    fn quantization_is_complementary_in_both_directions_including_half_steps() {
        for (outgoing, incoming) in [(0, 2), (2, 0)] {
            for step in 0..=64_u16 {
                let fraction = f32::from(step) / 64.0;
                let mut weights = [0.0; 4];
                weights[outgoing] = 1.0 - fraction;
                weights[incoming] = fraction;
                let result = codes(
                    TemporalLodSample {
                        weights,
                        transition_active: step < 64,
                        waiting_for_ready: false,
                    },
                    outgoing,
                );
                for rank in 0..16 {
                    assert_ne!(
                        retained(result[outgoing], rank),
                        retained(result[incoming], rank)
                    );
                    assert!(!retained(result[1], rank));
                    assert!(!retained(result[3], rank));
                }
            }
        }
    }

    #[test]
    fn slots_recycle_only_after_last_live_member_disappears() {
        let mut world = World::new();
        let key_a = GroupKey::Kit(world.spawn_empty().id());
        let key_b = GroupKey::Kit(world.spawn_empty().id());
        let mut groups = Groups::default();
        let first = groups.register(key_a, Vec3::ZERO, 42, policy()).unwrap();
        groups.collect_retired(&HashSet::from([first]));
        let second = groups.register(key_b, Vec3::ZERO, 99, policy()).unwrap();
        assert_ne!(first, second);
        groups.collect_retired(&HashSet::from([second]));
        let recycled = groups.register(key_a, Vec3::X, 73, policy()).unwrap();
        assert_eq!(first, recycled);
        assert_eq!(groups.records.len(), 3);
        assert_eq!(
            groups.record(recycled).unwrap().metadata,
            UVec4::new(0, 73, 0, 0)
        );
        assert!(groups.dirty);
        groups
            .register(key_a, Vec3::Y, recycled * 4, policy())
            .unwrap();
        assert_eq!(groups.record(recycled).unwrap().metadata.y, 73);
    }

    fn world_with_member() -> (World, Entity, u32, VisibilityRange) {
        let mut world = World::new();
        let key = GroupKey::Kit(world.spawn_empty().id());
        let mut groups = Groups::default();
        let slot = groups.register(key, Vec3::ZERO, 42, policy()).unwrap();
        world.insert_resource(groups);
        let range = VisibilityRange {
            start_margin: 0.0..0.0,
            end_margin: 20.0..28.0,
            use_aabb: false,
        };
        let entity = world
            .spawn((
                Member {
                    slot,
                    level: 0,
                    original_tag: 42,
                    original_range: range.clone(),
                },
                range.clone(),
            ))
            .id();
        (world, entity, slot, range)
    }

    #[test]
    fn missing_gpu_receipt_never_enables_new_group_and_uploads_safe_metadata() {
        let (mut world, entity, slot, range) = world_with_member();
        world.spawn((
            Camera3d::default(),
            Camera::default(),
            GlobalTransform::default(),
        ));
        let mut buffers = Assets::<ShaderBuffer>::default();
        buffers
            .insert(BUFFER.id(), ShaderBuffer::from(vec![Record::default()]))
            .unwrap();
        world.insert_resource(buffers);
        update(&mut world);
        assert!(!world.resource::<Groups>().active(slot));
        assert!(!world.resource::<Groups>().dirty);
        assert!(world.get::<VisibilityRange>(entity) == Some(&range));
        assert_eq!(
            world.resource::<Groups>().record(slot).unwrap().metadata.y,
            42
        );
        assert!(!gpu_ready(None, 100, 1));
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "One fixture verifies acquisition, renewal during a fade, and subsequent invalidation"
    )]
    fn missing_lod_blocks_temporal_activation_even_with_fresh_gpu_receipt() {
        let (mut world, entity, slot, original) = world_with_member();
        world.spawn((
            Camera3d::default(),
            Camera::default(),
            GlobalTransform::default(),
        ));
        world.insert_resource(FrameCount(10));
        let identity = forge_world::quality_application::QualityApplicationIdentity {
            sequence: 1,
            plan_hash: "ab".repeat(32).parse().unwrap(),
            adapter_generation: 1,
        };
        let mut submission = crate::global_quality::GpuSubmissionEvidence::default();
        submission.identity = Some(identity);
        submission.renderer_epoch = 1;
        submission.observed_frame = 10;
        submission.completed_frame = Some(10);
        submission.pipelines_total = 1;
        submission.queue_work_completed = true;
        submission.matching_view_ready = true;
        submission.assets.inventory_ready = true;
        world.insert_resource(crate::GlobalQualityStats {
            requested_tier: forge_project::VisualQualityTier::High,
            domains: [crate::QualityDomainProjection::CpuProjected; 7],
            requests_dispatched: true,
            available_budgets_projected: true,
            full_preset_applied: true,
            effective: None,
            application: Some(crate::QualityApplicationEvidence {
                status: "ready",
                identity: Some(identity),
                projected_domains: [true; 7],
                gpu_ready: true,
                error: None,
            }),
            gpu_submission: submission,
            adaptation: crate::QualityAdaptationStats::default(),
        });
        assert!(gpu_ready(
            world.get_resource::<crate::GlobalQualityStats>(),
            10,
            1
        ));
        update(&mut world);
        assert!(!world.resource::<Groups>().active(slot));
        assert!(world.get::<VisibilityRange>(entity) == Some(&original));
        for level in 1..3 {
            // Far original is intentionally retained without Mesh3d after batching.
            world.spawn((
                Member {
                    slot,
                    level,
                    original_tag: 42,
                    original_range: original.clone(),
                },
                original.clone(),
            ));
        }
        update(&mut world);
        assert!(world.resource::<Groups>().active(slot));
        let mut clock = Time::<()>::default();
        clock.advance_by(std::time::Duration::from_millis(50));
        world.insert_resource(clock);
        let camera = world
            .query_filtered::<Entity, With<Camera3d>>()
            .single(&world)
            .unwrap();
        world
            .entity_mut(camera)
            .insert(GlobalTransform::from_translation(Vec3::X * 50.0));
        update(&mut world);
        assert!(
            world.resource::<Groups>().groups[slot as usize - 1]
                .as_ref()
                .unwrap()
                .state
                .unwrap()
                .fade
                .is_some()
        );
        {
            let mut quality = world.resource_mut::<crate::GlobalQualityStats>();
            quality.application.as_mut().unwrap().gpu_ready = false;
            quality.gpu_submission.pipelines_pending = 1;
        }
        // A global receipt renewal cannot strand an already acquired fade at rest.
        for _ in 0..4 {
            update(&mut world);
        }
        let state = world.resource::<Groups>().groups[slot as usize - 1]
            .as_ref()
            .unwrap()
            .state
            .unwrap();
        assert_eq!(state.current, 1);
        assert!(state.fade.is_none());
        world
            .entity_mut(camera)
            .insert(GlobalTransform::from_translation(Vec3::X * 200.0));
        update(&mut world);
        let state = world.resource::<Groups>().groups[slot as usize - 1]
            .as_ref()
            .unwrap()
            .state
            .unwrap();
        assert_eq!(
            state.current, 1,
            "No new transition may borrow a pending receipt"
        );
        assert!(state.fade.is_none());
        {
            let mut quality = world.resource_mut::<crate::GlobalQualityStats>();
            quality.gpu_submission.assets.inventory_ready = false;
        }
        assert!(prepared_epoch(world.get_resource::<crate::GlobalQualityStats>(), 10).is_none());
        {
            let mut quality = world.resource_mut::<crate::GlobalQualityStats>();
            quality.application.as_mut().unwrap().gpu_ready = true;
            quality.gpu_submission.pipelines_pending = 0;
            quality.gpu_submission.assets.inventory_ready = true;
        }
        world.resource_mut::<Groups>().invalidate(slot);
        assert!(!world.resource::<Groups>().active(slot));
        update(&mut world);
        assert!(!world.resource::<Groups>().active(slot));
        assert!(world.get::<VisibilityRange>(entity) == Some(&original));
    }

    #[test]
    fn multiple_views_restore_spatial_fallback_without_retiring_live_groups() {
        let (mut world, entity, slot, original) = world_with_member();
        {
            let mut groups = world.resource_mut::<Groups>();
            groups.records[slot as usize].metadata.x = 1;
            groups.groups[slot as usize - 1].as_mut().unwrap().state =
                Some(TemporalLod::new(policy(), 0).unwrap());
        }
        world.entity_mut(entity).insert(broad_range(true));
        for _ in 0..2 {
            world.spawn((
                Camera3d::default(),
                Camera::default(),
                GlobalTransform::default(),
            ));
        }
        update(&mut world);
        assert!(!world.resource::<Groups>().active(slot));
        assert!(world.get::<VisibilityRange>(entity) == Some(&original));
        assert!(
            world.resource::<Groups>().groups[slot as usize - 1]
                .as_ref()
                .unwrap()
                .state
                .is_none()
        );
    }
}
