use bevy::prelude::*;
#[cfg(not(target_arch = "wasm32"))]
use bevy::tasks::Task;
use forge_core::EntityId;
use forge_navigation::landmass;
use forge_navigation::{AgentProfile, BuiltTile, ForgeCoordinates, TileInput, TileKey, point};
use landmass::{
    Agent, AgentId, AgentState, Archipelago, ArchipelagoOptions, FromAgentRadius, Island, IslandId,
    PointSampleDistance3d, TargetReachedCondition,
};
use serde::Serialize;
use std::{
    collections::{BTreeMap, BTreeSet, VecDeque},
    sync::Arc,
};

pub(super) const MAX_TILES: usize = 64;
pub(super) const MAX_PENDING: usize = 8;
#[cfg(not(target_arch = "wasm32"))]
pub(super) const MAX_NATIVE_JOBS: usize = 2;
pub(super) const MAX_CACHE_BYTES: usize = 32 * 1024 * 1024;
pub(super) const MAX_PENDING_BYTES: usize = 32 * 1024 * 1024;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum NavigationStatus {
    #[default]
    WaitingForSource,
    Queued,
    Generating,
    Ready,
    Moving,
    Reached,
    NoPath,
    OutsideMesh,
    EmptySource,
    InvalidGeometry,
    Stale,
    BudgetExceeded,
    Disabled,
}
#[derive(Clone, Debug, Serialize)]
pub struct NavigationTileObservation {
    pub key: TileKey,
    pub revision: u64,
    pub token: u64,
    pub source_hash: String,
    pub status: NavigationStatus,
    pub source_triangles: usize,
    pub curved_obstacles: usize,
    pub curved_footprint_excess_bound_m: f32,
    pub polygons: usize,
    pub retained_bytes: usize,
    pub build_ms: f64,
    pub error: Option<String>,
}
#[derive(Clone, Debug, Serialize)]
pub struct ActorNavigationObservation {
    pub actor: EntityId,
    pub status: NavigationStatus,
    pub target: Option<[f32; 3]>,
    pub feet: [f32; 3],
    pub actual_velocity: [f32; 3],
    pub desired_velocity: [f32; 3],
    pub required_tiles: Vec<TileKey>,
    pub grounded: bool,
    pub collision_count: usize,
    pub feedback_tick: Option<u64>,
    pub requested_translation: Option<[f32; 3]>,
    /// Exact provider state, separate from the portable actuation status.
    pub provider_state: Option<String>,
    pub route_diagnostic: Option<RouteDiagnostic>,
    /// Bounded live-provider queries for alternative danger destinations.
    pub reachable_goal_queries: u64,
}
/// Query-only evidence from the same live islands after their provider update.
#[derive(Clone, Debug, Serialize)]
pub struct RouteDiagnostic {
    pub start: [f32; 3],
    pub goal: [f32; 3],
    pub source_generation: u64,
    pub completed_builds: u64,
    pub start_sample: Option<[f32; 3]>,
    pub goal_sample: Option<[f32; 3]>,
    pub start_island: Option<String>,
    pub goal_island: Option<String>,
    pub start_type_index: Option<usize>,
    pub goal_type_index: Option<usize>,
    pub start_error: Option<String>,
    pub goal_error: Option<String>,
    pub path_error: Option<String>,
    pub path_steps: Option<usize>,
    pub animation_links: usize,
}
pub(super) struct TileRecord {
    pub stamp: Option<u64>,
    pub observation: NavigationTileObservation,
    pub island: Option<IslandId>,
}
pub(super) struct Route {
    pub agent: AgentId,
    pub observation: ActorNavigationObservation,
    pub patrol_index: usize,
    pub escape_search: Option<super::reachable::EscapeSearch>,
}
#[cfg(not(target_arch = "wasm32"))]
pub(super) struct NativeJob {
    pub input: TileInput,
    pub started: std::time::Instant,
    pub task: Task<Result<BuiltTile, String>>,
    pub cancelled: Arc<std::sync::atomic::AtomicBool>,
}

/// Bounded, disposable derived cache. Source/profile receipts are checked before
/// publication and before every actuator tick. Stale islands are removed at once.
#[derive(Resource)]
pub struct NavigationRuntime {
    pub(super) profile: AgentProfile,
    pub(super) archipelago: Archipelago<ForgeCoordinates>,
    pub(super) characters: BTreeMap<Entity, landmass::CharacterId>,
    pub(super) applied_costs: BTreeSet<u8>,
    pub(super) routes: BTreeMap<EntityId, Route>,
    pub(super) tiles: BTreeMap<TileKey, TileRecord>,
    pub(super) pending: VecDeque<TileInput>,
    #[cfg(not(target_arch = "wasm32"))]
    pub(super) jobs: Vec<NativeJob>,
    #[cfg(target_arch = "wasm32")]
    pub(super) jobs: BTreeMap<u64, (TileInput, f64)>,
    pub(super) token: u64,
    pub(super) rejected_stale: u64,
    pub(super) builds: u64,
    pub(super) actuation_ready: bool,
}
impl Default for NavigationRuntime {
    fn default() -> Self {
        let profile = AgentProfile::default();
        let mut options =
            ArchipelagoOptions::<ForgeCoordinates>::from_agent_radius(profile.radius_m);
        options.point_sample_distance = PointSampleDistance3d {
            horizontal_distance: profile.radius_m,
            distance_above: profile.step_m + profile.radius_m * 0.25,
            distance_below: profile.step_m + profile.snap_m,
            vertical_preference_ratio: 2.0,
            animation_link_max_vertical_distance: profile.step_m,
        };
        Self {
            profile,
            archipelago: Archipelago::new(options),
            characters: BTreeMap::new(),
            applied_costs: BTreeSet::new(),
            routes: BTreeMap::new(),
            tiles: BTreeMap::new(),
            pending: VecDeque::new(),
            #[cfg(not(target_arch = "wasm32"))]
            jobs: Vec::new(),
            #[cfg(target_arch = "wasm32")]
            jobs: BTreeMap::new(),
            token: 0,
            rejected_stale: 0,
            builds: 0,
            actuation_ready: false,
        }
    }
}
impl Drop for NavigationRuntime {
    fn drop(&mut self) {
        #[cfg(not(target_arch = "wasm32"))]
        for job in &self.jobs {
            job.cancelled
                .store(true, std::sync::atomic::Ordering::Relaxed);
        }
        #[cfg(target_arch = "wasm32")]
        super::worker::reset();
    }
}
impl NavigationRuntime {
    #[must_use]
    pub const fn profile(&self) -> AgentProfile {
        self.profile
    }
    #[must_use]
    pub fn actors(&self) -> Vec<ActorNavigationObservation> {
        self.routes
            .values()
            .map(|route| route.observation.clone())
            .collect()
    }
    #[must_use]
    pub fn tiles(&self) -> Vec<NavigationTileObservation> {
        self.tiles
            .values()
            .map(|tile| tile.observation.clone())
            .collect()
    }
    #[must_use]
    pub const fn rejected_stale_results(&self) -> u64 {
        self.rejected_stale
    }
    #[must_use]
    pub const fn completed_builds(&self) -> u64 {
        self.builds
    }
    #[must_use]
    pub fn pending_jobs(&self) -> usize {
        self.pending.len()
    }
    #[must_use]
    pub fn active_jobs(&self) -> usize {
        self.jobs.len()
    }
    pub(crate) fn patrol_index(&self, actor: EntityId) -> usize {
        self.routes.get(&actor).map_or(0, |r| r.patrol_index)
    }
    pub(crate) fn advance_patrol(&mut self, actor: EntityId, count: usize) {
        if let Some(route) = self.routes.get_mut(&actor)
            && count > 0
        {
            route.patrol_index = (route.patrol_index + 1) % count;
        }
    }
    pub(crate) fn intent(
        &mut self,
        actor: EntityId,
        center: Vec3,
        actual: Vec3,
        target_center: Option<Vec3>,
        support: (bool, usize),
        enabled: bool,
    ) {
        let (grounded, collisions) = support;
        let feet = center - Vec3::Y * (self.profile.height_m * 0.5);
        let target = target_center.map(|target| target - Vec3::Y * (self.profile.height_m * 0.5));
        let route = self.routes.entry(actor).or_insert_with(|| {
            let mut agent = Agent::<ForgeCoordinates>::create(
                point(feet.to_array()),
                point([0.0; 3]),
                self.profile.radius_m,
                self.profile.speed_mps,
                self.profile.speed_mps,
            );
            agent.target_reached_condition = TargetReachedCondition::Distance(Some(0.25));
            Route {
                agent: self.archipelago.add_agent(agent),
                patrol_index: 0,
                escape_search: None,
                observation: ActorNavigationObservation {
                    actor,
                    status: NavigationStatus::WaitingForSource,
                    target: None,
                    feet: feet.to_array(),
                    actual_velocity: [0.0; 3],
                    desired_velocity: [0.0; 3],
                    required_tiles: Vec::new(),
                    grounded: false,
                    collision_count: 0,
                    feedback_tick: None,
                    requested_translation: None,
                    provider_state: None,
                    route_diagnostic: None,
                    reachable_goal_queries: 0,
                },
            }
        });
        route.observation.feet = feet.to_array();
        route.observation.actual_velocity = actual.to_array();
        route.observation.target = target.map(|target| target.to_array());
        route.observation.grounded = grounded;
        route.observation.provider_state = None;
        route.observation.collision_count = collisions;
        // Living idle actors warm their current neighbourhood before a danger
        // produces a destination. This cache preparation never actuates movement.
        let required = if enabled {
            corridor(feet, target.unwrap_or(feet), self.profile)
        } else {
            Ok(BTreeSet::new())
        };
        route.observation.required_tiles = required
            .as_ref()
            .map(|tiles| tiles.iter().copied().collect())
            .unwrap_or_default();
        if let Some(agent) = self.archipelago.get_agent_mut(route.agent) {
            agent.position = point(feet.to_array());
            agent.velocity = point(actual.to_array());
            agent.current_target = target.map(|target| point(target.to_array()));
            agent.paused = !enabled || !grounded || required.is_err();
        }
        route.observation.status = if !enabled {
            NavigationStatus::Disabled
        } else if required.is_err() {
            NavigationStatus::BudgetExceeded
        } else if target.is_none() {
            NavigationStatus::Ready
        } else {
            NavigationStatus::WaitingForSource
        };
    }
    pub(crate) fn disable_actor(&mut self, actor: EntityId, center: Vec3) {
        self.intent(actor, center, Vec3::ZERO, None, (false, 0), false);
    }
    pub(crate) fn tick(&mut self, dt: f32) {
        let budget_valid = self.required().len() <= MAX_TILES;
        for route in self.routes.values_mut() {
            let phase =
                route
                    .observation
                    .required_tiles
                    .iter()
                    .find_map(|key| match self.tiles.get(key) {
                        Some(tile)
                            if matches!(
                                tile.observation.status,
                                NavigationStatus::Ready | NavigationStatus::EmptySource
                            ) =>
                        {
                            None
                        }
                        Some(tile) => Some(tile.observation.status),
                        None => Some(NavigationStatus::WaitingForSource),
                    });
            let enabled = self.actuation_ready
                && budget_valid
                && route.observation.grounded
                && !matches!(
                    route.observation.status,
                    NavigationStatus::Disabled | NavigationStatus::BudgetExceeded
                )
                && phase.is_none();
            if let Some(agent) = self.archipelago.get_agent_mut(route.agent) {
                agent.paused = !enabled;
            }
            if let Some(phase) = phase {
                route.observation.status = phase;
            }
            if !budget_valid {
                route.observation.status = NavigationStatus::BudgetExceeded;
            }
        }
        if dt.is_finite() && dt > 0.0 {
            self.archipelago.update(dt);
        }
        for route in self.routes.values_mut() {
            route.observation.desired_velocity = [0.0; 3];
            let Some(agent) = self.archipelago.get_agent(route.agent) else {
                continue;
            };
            if agent.paused {
                route.escape_search = None;
                route.observation.provider_state = None;
                route.observation.route_diagnostic = None;
                continue;
            }
            route.observation.provider_state = Some(format!("{:?}", agent.state()));
            route.observation.status = match agent.state() {
                AgentState::Moving => NavigationStatus::Moving,
                AgentState::ReachedTarget => NavigationStatus::Reached,
                AgentState::AgentNotOnNavMesh | AgentState::TargetNotOnNavMesh => {
                    NavigationStatus::OutsideMesh
                }
                AgentState::Idle => NavigationStatus::Ready,
                // Physical animation links require an explicit validated actuator;
                // the initial product only uses Landmass's ordinary connected seams.
                _ => NavigationStatus::NoPath,
            };
            if route.observation.status == NavigationStatus::Moving {
                route.observation.desired_velocity = agent.get_desired_velocity().to_array();
            }
            if matches!(
                route.observation.status,
                NavigationStatus::NoPath | NavigationStatus::OutsideMesh
            ) {
                if let Some(goal) = route.observation.target {
                    let start = route.observation.feet;
                    let reusable =
                        route
                            .observation
                            .route_diagnostic
                            .as_ref()
                            .is_some_and(|previous| {
                                previous.source_generation == self.token
                                    && previous.completed_builds == self.builds
                                    && Vec3::from_array(previous.start)
                                        .distance(Vec3::from_array(start))
                                        < self.profile.radius_m * 0.5
                                    && Vec3::from_array(previous.goal)
                                        .distance(Vec3::from_array(goal))
                                        < self.profile.radius_m * 0.5
                            });
                    if !reusable {
                        route.observation.route_diagnostic = Some(diagnose_route(
                            &self.archipelago,
                            start,
                            goal,
                            self.token,
                            self.builds,
                        ));
                    }
                }
            } else {
                route.observation.route_diagnostic = None;
            }
        }
    }
    pub(crate) fn desired(&self, actor: EntityId) -> Vec3 {
        self.routes.get(&actor).map_or(Vec3::ZERO, |route| {
            Vec3::from_array(route.observation.desired_velocity).with_y(0.0)
        })
    }
    pub(super) fn required(&self) -> BTreeSet<TileKey> {
        self.routes
            .values()
            .flat_map(|route| route.observation.required_tiles.iter().copied())
            .collect()
    }
    pub(super) fn invalidate(&mut self, key: TileKey, error: Option<String>) {
        self.pending.retain(|input| input.key != key);
        #[cfg(not(target_arch = "wasm32"))]
        for job in &self.jobs {
            if job.input.key == key {
                job.cancelled
                    .store(true, std::sync::atomic::Ordering::Relaxed);
            }
        }
        #[cfg(target_arch = "wasm32")]
        {
            let cancelled: Vec<_> = self
                .jobs
                .iter()
                .filter_map(|(token, (input, _))| (input.key == key).then_some(*token))
                .collect();
            for token in cancelled {
                super::worker::cancel(token);
                self.jobs.remove(&token);
                self.rejected_stale += 1;
            }
        }
        if let Some(tile) = self.tiles.get_mut(&key) {
            if let Some(island) = tile.island.take() {
                self.archipelago.remove_island(island);
            }
            tile.stamp = None;
            tile.observation.status = NavigationStatus::Stale;
            tile.observation.retained_bytes = 0;
            tile.observation.error = error;
        }
    }
    pub(super) fn publish(
        &mut self,
        input: &TileInput,
        result: Result<BuiltTile, String>,
        build_ms: f64,
    ) {
        let retained = self
            .tiles
            .values()
            .map(|tile| tile.observation.retained_bytes)
            .sum::<usize>();
        let Some(tile) = self.tiles.get_mut(&input.key) else {
            self.rejected_stale += 1;
            return;
        };
        if tile.observation.token != input.token
            || tile.observation.revision != input.revision
            || tile.observation.source_hash != input.source_hash
            || tile.stamp.is_none()
        {
            self.rejected_stale += 1;
            return;
        }
        let prepared = result.and_then(|built| {
            if built.key != input.key
                || built.profile != input.profile
                || built.token != input.token
                || built.revision != input.revision
                || built.source_hash != input.source_hash
                || built.curved_obstacles != input.curved_obstacles.len()
                || built.curved_footprint_excess_bound_m.to_bits()
                    != input
                        .curved_obstacles
                        .iter()
                        .map(|obstacle| obstacle.footprint_excess_bound_m)
                        .fold(0.0, f32::max)
                        .to_bits()
            {
                return Err("worker result identity mismatch".into());
            }
            let bytes = serde_json::to_vec(&built)
                .map_err(|error| error.to_string())?
                .len()
                .saturating_mul(4);
            if retained.saturating_add(bytes) > MAX_CACHE_BYTES {
                return Err("navigation retained cache byte budget exceeded".into());
            }
            let valid = built.validate()?;
            Ok((built, valid, bytes))
        });
        tile.observation.build_ms = build_ms;
        match prepared {
            Ok((built, valid, bytes)) => {
                tile.observation.polygons = built.polygon.polygon_count();
                tile.observation.source_triangles = built.source_triangles;
                tile.observation.retained_bytes = bytes;
                tile.island = Some(
                    self.archipelago
                        .add_island(Island::new(landmass::Transform::default(), Arc::new(valid))),
                );
                tile.observation.status = NavigationStatus::Ready;
                tile.observation.error = None;
                self.builds += 1;
            }
            Err(error) => {
                tile.observation.status = NavigationStatus::InvalidGeometry;
                tile.observation.error = Some(error);
            }
        }
    }
}

fn diagnose_route(
    archipelago: &Archipelago<ForgeCoordinates>,
    start: [f32; 3],
    goal: [f32; 3],
    source_generation: u64,
    completed_builds: u64,
) -> RouteDiagnostic {
    let distance = &archipelago.archipelago_options.point_sample_distance;
    let start_sample = archipelago.sample_point(point(start), distance);
    let goal_sample = archipelago.sample_point(point(goal), distance);
    let mut diagnostic = RouteDiagnostic {
        start,
        goal,
        source_generation,
        completed_builds,
        start_sample: start_sample
            .as_ref()
            .ok()
            .map(|sample| sample.point().to_array()),
        goal_sample: goal_sample
            .as_ref()
            .ok()
            .map(|sample| sample.point().to_array()),
        start_island: start_sample
            .as_ref()
            .ok()
            .map(|sample| format!("{:?}", sample.island())),
        goal_island: goal_sample
            .as_ref()
            .ok()
            .map(|sample| format!("{:?}", sample.island())),
        start_type_index: start_sample
            .as_ref()
            .ok()
            .map(landmass::SampledPoint::type_index),
        goal_type_index: goal_sample
            .as_ref()
            .ok()
            .map(landmass::SampledPoint::type_index),
        start_error: start_sample.as_ref().err().map(ToString::to_string),
        goal_error: goal_sample.as_ref().err().map(ToString::to_string),
        path_error: None,
        path_steps: None,
        animation_links: 0,
    };
    if let (Ok(start), Ok(goal)) = (&start_sample, &goal_sample) {
        match archipelago.find_path(
            start,
            goal,
            &std::collections::HashMap::default(),
            landmass::PermittedAnimationLinks::default(),
        ) {
            Ok(path) => {
                diagnostic.path_steps = Some(path.len());
                diagnostic.animation_links = path
                    .iter()
                    .filter(|step| matches!(step, landmass::PathStep::AnimationLink { .. }))
                    .count();
            }
            Err(error) => diagnostic.path_error = Some(error.to_string()),
        }
    }
    diagnostic
}

fn corridor(start: Vec3, end: Vec3, profile: AgentProfile) -> Result<BTreeSet<TileKey>, String> {
    if !start.is_finite()
        || !end.is_finite()
        || start.abs().max_element() > 100_000.0
        || end.abs().max_element() > 100_000.0
    {
        return Err("invalid navigation destination".into());
    }
    let start = TileKey::at(start.to_array(), profile);
    let end = TileKey::at(end.to_array(), profile);
    let min_x = start.x.min(end.x) - 1;
    let max_x = start.x.max(end.x) + 1;
    let min_z = start.z.min(end.z) - 1;
    let max_z = start.z.max(end.z) + 1;
    if i64::from(max_x - min_x + 1) * i64::from(max_z - min_z + 1)
        > i64::try_from(MAX_TILES).unwrap_or(0)
    {
        return Err("navigation corridor tile budget exceeded".into());
    }
    Ok((min_x..=max_x)
        .flat_map(|x| (min_z..=max_z).map(move |z| TileKey { x, z }))
        .collect())
}
