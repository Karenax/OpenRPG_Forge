use bevy::{
    app::AppExit,
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use forge_engine::{
    EnvironmentProjectionStats, EnvironmentQualityRequest, GlobalQualityRequest,
    GlobalQualityStats, VegetationProjectionStats, VegetationQualityRequest, WaterProjectionStats,
    WaterQualityRequest,
};
use forge_project::VisualQualityTier;
use std::{path::PathBuf, time::Instant};

mod weather;
pub use weather::weather_domain;

const TIERS: [VisualQualityTier; 5] = [
    VisualQualityTier::High,
    VisualQualityTier::Low,
    VisualQualityTier::Medium,
    VisualQualityTier::Ultra,
    VisualQualityTier::High,
];

#[derive(Resource)]
struct Cycle {
    domain: Domain,
    directory: PathBuf,
    started: Instant,
    stage: usize,
    requested_stage: Option<usize>,
    stable: u32,
    issued: bool,
    readback: bool,
    source_hash: Option<String>,
    terminal: bool,
    environment: Option<(Entity, EnvironmentMapLight)>,
}

fn request_stage_once(requested: &mut Option<usize>, stage: usize) -> bool {
    if *requested == Some(stage) {
        return false;
    }
    *requested = Some(stage);
    true
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub enum Domain {
    Water(VisualQualityTier),
    Environment,
    Vegetation,
    Global,
    Weather(forge_project::WeatherSunlightProfile),
}

impl Domain {
    const fn stages(self) -> usize {
        match self {
            Self::Weather(_) => 3,
            Self::Water(VisualQualityTier::Low) => 7,
            Self::Water(_) => 9,
            _ => TIERS.len(),
        }
    }

    fn tier(self, stage: usize) -> VisualQualityTier {
        match self {
            Self::Weather(_) => VisualQualityTier::High,
            Self::Water(tier) => tier,
            _ => TIERS[stage],
        }
    }
}

pub fn install(app: &mut App, directory: PathBuf) -> Result<(), String> {
    install_domain(app, directory, Domain::Environment)
}

pub fn install_domain(app: &mut App, directory: PathBuf, domain: Domain) -> Result<(), String> {
    // Fresh directory prevents an old PNG from satisfying this execution's evidence.
    std::fs::create_dir(&directory)
        .map_err(|error| format!("cycle proof requires a fresh output directory: {error}"))?;
    app.insert_resource(Cycle {
        domain,
        directory,
        started: Instant::now(),
        stage: 0,
        requested_stage: None,
        stable: 0,
        issued: false,
        readback: false,
        source_hash: None,
        terminal: false,
        environment: None,
    });
    app.add_systems(Last, advance);
    if matches!(domain, Domain::Weather(_)) {
        weather::install(app);
    }
    if matches!(domain, Domain::Water(_)) {
        app.add_systems(Startup, freeze_water_cycle_time);
    }
    Ok(())
}

#[allow(clippy::needless_pass_by_value)]
fn freeze_water_cycle_time(mut time: ResMut<Time<Virtual>>) {
    // Proof-only: keep wave phase, weather and animated lighting identical across images.
    // Real-time deadline/readback/frame counting remain active; normal runtime is unaffected.
    time.pause();
    info!(target: "forge::proof", event_kind = "water_cycle_clock_frozen",
        "virtual simulation time paused for water optical isolation; not a benchmark");
}

#[allow(
    clippy::needless_pass_by_value,
    clippy::too_many_arguments,
    clippy::too_many_lines,
    reason = "one bounded capture state machine shares optional domain system parameters"
)]
fn advance(
    mut commands: Commands,
    mut cycle: ResMut<Cycle>,
    request: Option<ResMut<EnvironmentQualityRequest>>,
    observation: Option<Res<EnvironmentProjectionStats>>,
    vegetation_request: Option<ResMut<VegetationQualityRequest>>,
    vegetation_observation: Option<Res<VegetationProjectionStats>>,
    global_request: Option<ResMut<GlobalQualityRequest>>,
    global_observation: Option<Res<GlobalQualityStats>>,
    warmup: Option<Res<forge_engine::ProfileWarmup>>,
    water_request: Option<ResMut<WaterQualityRequest>>,
    water_observation: Option<Res<WaterProjectionStats>>,
    weather_observation: Option<Res<weather::Observation>>,
    player: Option<Res<forge_engine::PlayerRuntimeSnapshot>>,
    volumetric_preview: Option<Res<forge_engine::VolumetricPreviewRequest>>,
    mut environment_lights: Query<
        (Entity, &Camera, Option<&mut EnvironmentMapLight>),
        With<Camera3d>,
    >,
    mut exit: MessageWriter<AppExit>,
) {
    if cycle.terminal {
        return;
    }
    if let Some(warmup) = warmup {
        if warmup.is_warming() {
            // Preparation has its own deadline; the hot cycle starts afterwards.
            cycle.started = Instant::now();
            return;
        }
        if matches!(
            warmup.phase,
            forge_engine::ProfileWarmupPhase::Failed | forge_engine::ProfileWarmupPhase::Cancelled
        ) {
            error!(target: "forge::proof", event_kind = "quality_cycle_failed",
                warmup = ?warmup.phase, "profile preparation did not complete");
            cycle.terminal = true;
            exit.write(AppExit::from_code(1));
            return;
        }
    }
    if cycle.started.elapsed().as_secs() >= 120 {
        if let Some(global) = global_observation.as_deref() {
            error!(target: "forge::proof", event_kind = "quality_cycle_final_observation",
                observation = ?serde_json::to_string(global), "last quality state before timeout");
        }
        error!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64, event_kind = "quality_cycle_failed", domain = ?cycle.domain, "120-second cycle deadline expired");
        cycle.terminal = true;
        exit.write(AppExit::from_code(1));
        return;
    }
    let tier = cycle.domain.tier(cycle.stage);
    let (source_hash, matching, resolution) = match cycle.domain {
        Domain::Weather(_) => {
            let Some(observation) = weather_observation else {
                return;
            };
            (observation.source_hash.clone(), observation.matching, 0)
        }
        Domain::Water(_) => {
            let (Some(mut request), Some(observation)) = (water_request, water_observation) else {
                return;
            };
            request.request(tier);
            request.preview_reflections_disabled(cycle.stage == 1);
            request.preview_screen_reflections_disabled(cycle.stage == 7);
            request.preview_surface_tint(if cycle.stage == 3 { Some(false) } else { None });
            let environment_matches = observation
                .quality
                .as_ref()
                .is_some_and(|q| q.environment_textures_available)
                && water_environment_matches(&mut cycle, &mut environment_lights);
            let matching = environment_matches
                && observation.quality.as_ref().is_some_and(|q| {
                    q.requested_tier == tier
                        && q.reflections_disabled_preview == (cycle.stage == 1)
                        && q.screen_reflections_disabled_preview == (cycle.stage == 7)
                        && q.surface_tint_preview
                            == if cycle.stage == 3 { Some(false) } else { None }
                        && q.ssr_budget_projected
                        && q.material_reflectance_projected
                        && q.surface_tint_projected
                        && q.environment_textures_available
                        && observation.shader_uniforms_match_profile
                });
            if matching && cycle.stable == 179 {
                info!(target: "forge::proof", event_kind = "water_cycle_observed", stage = cycle.stage,
                    environment_intensity = cycle.environment.as_ref().map(|(_, light)| if cycle.stage == 5 { 0.0 } else { light.intensity }),
                    quality = ?observation.quality, "CPU material matched; captured pixels require inspection");
            }
            (
                format!(
                    "{}:{}",
                    observation.terrain_source_hash, observation.source_hash
                ),
                matching,
                0,
            )
        }
        Domain::Global => {
            let (Some(mut request), Some(global), Some(environment)) =
                (global_request, global_observation, observation)
            else {
                return;
            };
            // A manual request deliberately creates a fresh application sequence.
            // Send it once per stage, not every frame while waiting for its GPU receipt.
            let stage = cycle.stage;
            if request_stage_once(&mut cycle.requested_stage, stage) {
                request.request(tier);
                return;
            }
            let Some(plan) = global.effective.as_ref().and_then(|e| e.plan.as_ref()) else {
                return;
            };
            let Some(bindings) = plan.source_bindings else {
                return;
            };
            let matching = global_application_matches(&global, tier)
                && (!volumetric_preview.as_ref().is_some_and(|r| r.enabled)
                    || volumetric_matches(&environment));
            if matching && cycle.stable == 179 {
                info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
                    event_kind = "global_quality_cycle_observed", tier = ?tier, domains = ?global.domains,
                    full_preset_applied = global.full_preset_applied,
                    application_status = global.application.as_ref().map(|a| a.status),
                    application_sequence = global.application.as_ref().and_then(|a| a.identity).map(|i| i.sequence),
                    application_plan_hash = global.effective.as_ref().and_then(|e| e.plan_hash).map(|hash| hash.to_string()),
                    gpu_ready = global.application.as_ref().is_some_and(|a| a.gpu_ready),
                    observation = ?serde_json::to_string(&*global),
                    volumetric_applied = environment.volumetric.applied,
                    volumetric_volumes = environment.fog_volume_entities,
                    volumetric_steps = environment.volumetric.requested_step_count,
                    volumetric_maximum_m = ?environment.volumetric.effective_maximum_distance_m,
                    "Effective plan prepared/submitted and CPU domains matched; full authored preset remains unqualified");
            }
            (
                format!(
                    "{}:{}:{}:{}:{}:{}",
                    bindings.terrain,
                    bindings.vegetation,
                    bindings.environment,
                    bindings.water,
                    bindings.weather,
                    plan.requested_policy_hash
                ),
                matching,
                0,
            )
        }
        Domain::Environment => {
            let (Some(mut request), Some(observation)) = (request, observation) else {
                return;
            };
            request.request(tier);
            let matching = observation.quality.as_ref().is_some_and(|q| {
                q.requested_tier == tier && q.sky_budget_projected && q.generated_texture_bound
            });
            (
                observation.source_hash.clone(),
                matching,
                observation
                    .sky
                    .atmosphere_ibl_resolution
                    .map_or(0, |size| size[0]),
            )
        }
        Domain::Vegetation => {
            let (Some(mut request), Some(observation)) =
                (vegetation_request, vegetation_observation)
            else {
                return;
            };
            request.request(tier);
            let matching = observation.quality.as_ref().is_some_and(|q| {
                q.requested_tier == tier
                    && q.projected_tier == tier
                    && !q.status.starts_with("rejected")
            });
            (
                format!(
                    "{}:{}",
                    observation.terrain_document_hash, observation.vegetation_document_hash
                ),
                matching,
                0,
            )
        }
    };
    if let Some(hash) = &cycle.source_hash {
        if *hash != source_hash {
            error!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64, event_kind = "quality_cycle_failed", domain = ?cycle.domain, "authored source changed");
            cycle.terminal = true;
            exit.write(AppExit::from_code(1));
            return;
        }
    } else {
        cycle.source_hash = Some(source_hash.clone());
    }
    if !matching {
        cycle.stable = 0;
        return;
    }
    cycle.stable += 1;
    let path = cycle.directory.join(format!("stage-{}.png", cycle.stage));
    if !cycle.issued && cycle.stable >= 180 {
        commands
            .spawn(Screenshot::primary_window())
            .observe(save_to_disk(path.clone()))
            .observe(record_readback);
        cycle.issued = true;
        info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64,
            event_kind = "quality_cycle_capture", domain = ?cycle.domain, stage = cycle.stage, tier = ?tier,
            resolution, source_hash = %source_hash,
            player_position = ?player.as_deref().map(|p| p.player_position),
            player_velocity = ?player.as_deref().map(|p| p.horizontal_velocity),
            "capture after 180 matching observations (including effective application readiness for Global); image requires inspection");
    }
    if cycle.readback && std::fs::metadata(&path).is_ok_and(|file| file.len() > 0) {
        info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64, event_kind = "quality_cycle_stage_verified", domain = ?cycle.domain, stage = cycle.stage, tier = ?tier, path = %path.display(), "fresh capture read back and file verified");
        cycle.stage += 1;
        cycle.stable = 0;
        cycle.issued = false;
        cycle.readback = false;
        if cycle.stage == cycle.domain.stages() {
            cycle.terminal = true;
            info!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64, event_kind = "quality_cycle_completed", domain = ?cycle.domain, stages = cycle.domain.stages(), "native quality cycle complete; artistic acceptance not implied");
            exit.write(AppExit::Success);
        }
    }
}

fn global_application_matches(global: &GlobalQualityStats, tier: VisualQualityTier) -> bool {
    let (Some(application), Some(effective)) = (&global.application, &global.effective) else {
        return false;
    };
    let Some(plan) = &effective.plan else {
        return false;
    };
    global.requested_tier == tier
        && global.requests_dispatched
        && global.available_budgets_projected
        && application.status == "plan_application_ready"
        && application.gpu_ready
        && application.error.is_none()
        && application.projected_domains.iter().all(|yes| *yes)
        && effective.status == "plan_application_ready"
        && effective.gpu_ready
        && effective.error.is_none()
        && plan.requested_tier == tier
        && plan.blockers.is_empty()
        && plan.source_bindings.is_some()
        && application.identity.is_some_and(|identity| {
            Some(identity.plan_hash) == effective.plan_hash
                && identity.adapter_generation == plan.adapter_generation
        })
}

fn water_environment_matches(
    cycle: &mut Cycle,
    cameras: &mut Query<(Entity, &Camera, Option<&mut EnvironmentMapLight>), With<Camera3d>>,
) -> bool {
    let mut active = cameras.iter_mut().filter(|(_, camera, _)| camera.is_active);
    let Some((entity, _, light)) = active.next() else {
        return false;
    };
    if active.next().is_some() {
        return false;
    }
    let Some(mut light) = light else {
        return false;
    };
    if cycle.environment.is_none() {
        if !light.intensity.is_finite() || light.intensity <= 0.0 {
            return false;
        }
        cycle.environment = Some((entity, light.clone()));
    }
    let Some((camera, original)) = &cycle.environment else {
        return false;
    };
    if *camera != entity
        || light.diffuse_map.id() != original.diffuse_map.id()
        || light.specular_map.id() != original.specular_map.id()
    {
        return false;
    }
    // Only IBL intensity changes, not water reflectance, direct lights, maps or source documents.
    let desired = if cycle.stage == 5 {
        0.0
    } else {
        original.intensity
    };
    let matched = light.intensity.to_bits() == desired.to_bits();
    light.intensity = desired;
    matched
}

fn volumetric_matches(environment: &EnvironmentProjectionStats) -> bool {
    let Some(quality) = &environment.quality else {
        return false;
    };
    if !quality.requested.volumetric_fog_enabled {
        return !environment.volumetric.applied && environment.fog_volume_entities == 0;
    }
    environment.volumetric.applied
        && environment.fog_volume_entities == 1
        && environment.volumetric.requested_step_count
            == quality.requested.volumetric_fog_step_count
        && environment
            .volumetric
            .effective_maximum_distance_m
            .is_some_and(|m| {
                m.is_finite() && m > 0.0 && m <= quality.requested.volumetric_fog_max_distance_m
            })
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy observer parameter ownership"
)]
fn record_readback(_: On<ScreenshotCaptured>, mut cycle: ResMut<Cycle>) {
    cycle.readback = true;
}

pub fn resolve_exit(app: &App, exit: AppExit) -> AppExit {
    if app
        .world()
        .get_resource::<Cycle>()
        .is_some_and(|cycle| cycle.stage != cycle.domain.stages())
    {
        error!(target: "forge::proof", schema = "forge.runtime-event", schema_version = 1_u64, event_kind = "quality_cycle_failed", "runtime stopped before all cycle captures completed");
        AppExit::from_code(1)
    } else {
        exit
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn synthetic_ready_global() -> GlobalQualityStats {
        use QualityCapability::*;
        use forge_project::effective_quality::*;
        let policy = forge_project::VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .unwrap();
        let hash = policy.content_hash().unwrap();
        let caps = QualityCapabilities {
            schema_version: 1,
            adapter_id: "synthetic-cycle-test".into(),
            generation: 1,
            terrain_constraints: None,
            source_bindings: Some(QualitySourceBindings {
                terrain: hash,
                vegetation: hash,
                environment: hash,
                water: hash,
                weather: hash,
            }),
            capabilities: [
                Terrain,
                Vegetation,
                Shadows,
                Sky,
                Rain,
                PostProcess,
                EnvironmentReflection,
                ReflectionIntensity,
                ScreenReflection,
                ReflectionDistance,
                SurfaceTint,
                ScreenRefraction,
                VolumetricFog,
                DistanceFog,
            ]
            .into_iter()
            .map(|c| (c, CapabilityStatus::Supported))
            .collect(),
        };
        let plan = resolve_effective_quality(
            &policy,
            VisualQualityTier::High,
            &caps,
            QualityResolutionPolicy::Strict,
        )
        .unwrap();
        let plan_hash = plan.content_hash().unwrap();
        let mut app = App::new();
        app.add_plugins(forge_engine::GlobalQualityPlugin(VisualQualityTier::High));
        let mut global = app.world().resource::<GlobalQualityStats>().clone();
        global.requests_dispatched = true;
        global.available_budgets_projected = true;
        global.effective = Some(forge_engine::EffectiveQualityEvidence {
            status: "plan_application_ready",
            plan: Some(plan),
            plan_hash: Some(plan_hash),
            error: None,
            gpu_ready: true,
        });
        global.application = Some(forge_engine::QualityApplicationEvidence {
            status: "plan_application_ready",
            identity: Some(
                forge_world::quality_application::QualityApplicationIdentity {
                    sequence: 1,
                    plan_hash,
                    adapter_generation: 1,
                },
            ),
            projected_domains: [true; 7],
            gpu_ready: true,
            error: None,
        });
        global
    }

    #[test]
    fn global_cycle_rejects_cpu_only_stale_and_partial_application_evidence() {
        let global = synthetic_ready_global();
        assert!(global_application_matches(&global, VisualQualityTier::High));
        assert!(!global_application_matches(&global, VisualQualityTier::Low));
        assert!(!global.full_preset_applied);
        for change in [
            |g: &mut GlobalQualityStats| g.application = None,
            |g: &mut GlobalQualityStats| g.application.as_mut().unwrap().gpu_ready = false,
            |g: &mut GlobalQualityStats| {
                g.application.as_mut().unwrap().status = "plan_application_waiting_renderer";
            },
            |g: &mut GlobalQualityStats| {
                g.application.as_mut().unwrap().error = Some("Invalidated".into());
            },
            |g: &mut GlobalQualityStats| {
                g.application.as_mut().unwrap().projected_domains[4] = false;
            },
            |g: &mut GlobalQualityStats| {
                g.application
                    .as_mut()
                    .unwrap()
                    .identity
                    .as_mut()
                    .unwrap()
                    .adapter_generation += 1;
            },
            |g: &mut GlobalQualityStats| g.effective.as_mut().unwrap().gpu_ready = false,
            |g: &mut GlobalQualityStats| g.effective.as_mut().unwrap().plan_hash = None,
            |g: &mut GlobalQualityStats| {
                g.effective
                    .as_mut()
                    .unwrap()
                    .plan
                    .as_mut()
                    .unwrap()
                    .source_bindings = None;
            },
            |g: &mut GlobalQualityStats| g.requests_dispatched = false,
        ] {
            let mut invalid = global.clone();
            change(&mut invalid);
            assert!(!global_application_matches(
                &invalid,
                VisualQualityTier::High
            ));
        }
    }
    #[test]
    fn frozen_water_proof_keeps_real_clock_running() {
        let mut app = App::new();
        app.add_plugins(MinimalPlugins);
        app.insert_resource(bevy::time::TimeUpdateStrategy::ManualDuration(
            std::time::Duration::from_millis(16),
        ));
        app.add_systems(Startup, freeze_water_cycle_time);
        for _ in 0..4 {
            app.update();
        }
        assert!(app.world().resource::<Time<Virtual>>().is_paused());
        assert!(app.world().resource::<Time>().elapsed().is_zero());
        assert!(!app.world().resource::<Time<Real>>().elapsed().is_zero());
    }

    #[test]
    fn water_cycle_preserves_requested_tier_at_every_stage() {
        for tier in TIERS {
            for stage in 0..Domain::Water(tier).stages() {
                assert_eq!(Domain::Water(tier).tier(stage), tier);
            }
        }
        for domain in [Domain::Environment, Domain::Vegetation, Domain::Global] {
            for (stage, tier) in TIERS.into_iter().enumerate() {
                assert_eq!(domain.tier(stage), tier);
            }
        }
    }

    #[test]
    fn incomplete_cycle_cannot_exit_successfully() {
        let mut app = App::new();
        app.insert_resource(Cycle {
            domain: Domain::Environment,
            directory: PathBuf::new(),
            started: Instant::now(),
            stage: 0,
            requested_stage: None,
            stable: 0,
            issued: false,
            readback: false,
            source_hash: None,
            terminal: false,
            environment: None,
        });
        assert!(!resolve_exit(&app, AppExit::Success).is_success());
        app.world_mut().resource_mut::<Cycle>().stage = TIERS.len();
        assert!(resolve_exit(&app, AppExit::Success).is_success());
        assert!(!resolve_exit(&app, AppExit::from_code(1)).is_success());
        app.world_mut().resource_mut::<Cycle>().domain = Domain::Water(VisualQualityTier::Low);
        assert!(!resolve_exit(&app, AppExit::Success).is_success());
        app.world_mut().resource_mut::<Cycle>().stage = 7;
        assert!(resolve_exit(&app, AppExit::Success).is_success());
        app.world_mut().resource_mut::<Cycle>().domain = Domain::Water(VisualQualityTier::High);
        assert!(!resolve_exit(&app, AppExit::Success).is_success());
        app.world_mut().resource_mut::<Cycle>().stage = 9;
        assert!(resolve_exit(&app, AppExit::Success).is_success());
    }

    #[test]
    fn global_cycle_does_not_reset_application_while_waiting_for_gpu() {
        let mut requested = None;
        for stage in 0..TIERS.len() {
            assert!(request_stage_once(&mut requested, stage));
            for _ in 0..180 {
                assert!(!request_stage_once(&mut requested, stage));
            }
        }
    }

    #[test]
    fn environment_probe_restores_intensity_and_rejects_ambiguous_or_replaced_maps() {
        fn step(world: &mut World, cycle: &mut Cycle) -> bool {
            let mut state = bevy::ecs::system::SystemState::<
                Query<(Entity, &Camera, Option<&mut EnvironmentMapLight>), With<Camera3d>>,
            >::new(world);
            water_environment_matches(cycle, &mut state.get_mut(world).unwrap())
        }
        let mut world = World::new();
        let entity = world
            .spawn((
                Camera3d::default(),
                Camera::default(),
                EnvironmentMapLight {
                    intensity: 2.0,
                    ..Default::default()
                },
            ))
            .id();
        let mut cycle = Cycle {
            domain: Domain::Water(VisualQualityTier::Low),
            directory: PathBuf::new(),
            started: Instant::now(),
            stage: 0,
            requested_stage: None,
            stable: 0,
            issued: false,
            readback: false,
            source_hash: None,
            terminal: false,
            environment: None,
        };
        assert!(step(&mut world, &mut cycle));
        cycle.stage = 5;
        assert!(!step(&mut world, &mut cycle));
        assert_eq!(
            world
                .get::<EnvironmentMapLight>(entity)
                .unwrap()
                .intensity
                .to_bits(),
            0.0_f32.to_bits()
        );
        assert!(step(&mut world, &mut cycle));
        cycle.stage = 6;
        assert!(!step(&mut world, &mut cycle));
        assert!(step(&mut world, &mut cycle));
        assert_eq!(
            world
                .get::<EnvironmentMapLight>(entity)
                .unwrap()
                .intensity
                .to_bits(),
            2.0_f32.to_bits()
        );
        let auxiliary = world.spawn((Camera3d::default(), Camera::default())).id();
        cycle.stage = 5;
        assert!(!step(&mut world, &mut cycle));
        assert_eq!(
            world
                .get::<EnvironmentMapLight>(entity)
                .unwrap()
                .intensity
                .to_bits(),
            2.0_f32.to_bits()
        );
        world.despawn(auxiliary);
        let mut images = Assets::<Image>::default();
        world
            .get_mut::<EnvironmentMapLight>(entity)
            .unwrap()
            .specular_map = images.add(Image::default());
        assert!(!step(&mut world, &mut cycle));
    }
}
