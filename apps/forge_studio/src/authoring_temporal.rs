//! Temporal presentation of the actual Studio player camera.
//! The authored scene is unchanged; `RenderWorld` remains the source sequence owner.
use crate::authoring_input::{self, TemporalMode};
use bevy::{
    anti_alias::taa::TemporalAntiAliasing,
    camera::Hdr,
    core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass},
    ecs as bevy_ecs,
    prelude::*,
    render::{
        camera::{MipBias, TemporalJitter},
        view::Msaa,
    },
};
use forge_engine::{tidewater_refraction::taa_mirror, tidewater_temporal as temporal};

#[derive(Resource)]
struct Session {
    source_registration: Result<(), String>,
    hull_adapter_installed: bool,
    optics: bool,
    active: Option<(Entity, TemporalMode)>,
    owned_taa: Option<OwnedTaa>,
    revision: u64,
}

struct OwnedTaa {
    camera: Entity,
    depth: bool,
    motion: bool,
    mip_bias: bool,
}

/// Motion prepass added by this TAA transaction. The graphics controller can
/// retain it for motion blur and take responsibility for its later cleanup.
#[derive(Component)]
pub(super) struct OwnedTemporalPrepasses {
    pub(super) motion: bool,
}

/// Call after installing every scene/render plugin. Render adapters are fixed
/// for this App lifetime; changing them requires recreating the playtest App.
pub(super) fn install(app: &mut App, optics: bool) {
    let source_registration = if optics {
        register_source(app)
    } else {
        Err("la chaîne d’eau et d’effets sous-marins est absente".into())
    };
    let hull_adapter_installed = app
        .get_sub_app(bevy::render::RenderApp)
        .is_some_and(|render| {
            render
                .world()
                .contains_resource::<temporal::render::HullMaskAdapter>()
        });
    app.insert_resource(Session {
        source_registration,
        hull_adapter_installed,
        optics,
        active: None,
        owned_taa: None,
        revision: 0,
    });
}

fn register_source(app: &mut App) -> Result<(), String> {
    use temporal::{phase::MaterialRole, shaders::Family};
    temporal::render_owned::register(app)?;
    temporal::register_material::<forge_engine::tidewater_terrain::lod::LodMaterial>(
        app,
        Family::TerrainLod,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_plants::PlantMaterial>(
        app,
        Family::Plant,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_material::VillageMaterial>(
        app,
        Family::Village,
        MaterialRole::Opaque,
    )?;
    temporal::register_material::<forge_engine::tidewater_ocean_optics::OpticalMaterial>(
        app,
        Family::Ocean,
        MaterialRole::Water,
    )?;
    temporal::register_material::<forge_engine::tidewater_stall::FishStandMaterial>(
        app,
        Family::FishStand,
        MaterialRole::Opaque,
    )?;
    temporal::phase::register_sky_material::<forge_engine::tidewater_sky::SkyMaterial>(app)?;
    temporal::phase::register_standard_opaque(app)?;
    Ok(())
}

/// An exclusive transaction runs before source packet preparation and capture
/// mirroring. Component changes are visible together, without deferred commands.
pub(super) fn update(world: &mut World) {
    world.resource_scope(|world, mut session: Mut<Session>| {
        let profile = authoring_input::graphics();
        let mut cameras = world.query_filtered::<Entity, With<forge_engine::ForgePlayerCamera>>();
        let camera = cameras.single(world).ok();
        let desired = if authoring_input::is_playing() {
            camera.map(|camera| (camera, profile.temporal_mode))
        } else {
            None
        };

        if session.active != desired {
            if let Err(error) = deselect_source(world) {
                authoring_input::set_temporal_status(format!(
                    "Arrêt du mode Tidewater refusé : {error}"
                ));
                return;
            }
            clear_owned_taa(world, &mut session);
            session.active = None;
            if let Some((camera, mode)) = desired {
                let result = match mode {
                    TemporalMode::Off => Ok(()),
                    TemporalMode::BevyTaa => enable_taa(world, &mut session, camera),
                    TemporalMode::SourceResolve => enable_source(world, &session, camera),
                };
                if let Err(error) = result {
                    authoring_input::set_temporal_status(format!(
                        "Mode temporel demandé indisponible : {error}"
                    ));
                    return;
                }
                session.active = Some((camera, mode));
            }
        }

        let message = match session.active {
            None if !authoring_input::is_playing() => {
                "Mode édition · rendu temporel du playtest arrêté".into()
            }
            None => "Rendu temporel · en attente de la caméra joueur".into(),
            Some((_, TemporalMode::Off)) => "Anti-crénelage temporel désactivé".into(),
            Some((camera, TemporalMode::BevyTaa)) => taa_status(world, &session, camera),
            Some((camera, TemporalMode::SourceResolve)) => source_status(world, camera),
        };
        authoring_input::set_temporal_status(message);
    });
}

fn deselect_source(world: &mut World) -> Result<(), String> {
    if world
        .get_resource::<temporal::render_owned::Control>()
        .is_some_and(|control| control.selected_view().is_some())
    {
        temporal::render_owned::select(world, None)?;
    }
    Ok(())
}

fn enable_taa(world: &mut World, session: &mut Session, camera: Entity) -> Result<(), String> {
    if world
        .get::<Camera>(camera)
        .is_none_or(|camera| !camera.is_active)
        || !matches!(world.get::<Projection>(camera), Some(Projection::Perspective(projection))
            if projection.near.is_finite() && projection.near > 0.0
                && projection.far.is_finite() && projection.far > projection.near)
    {
        return Err("la caméra joueur perspective n’est pas prête".into());
    }
    if world.get::<Hdr>(camera).is_none() || world.get::<Msaa>(camera) != Some(&Msaa::Off) {
        return Err("le TAA exige une caméra HDR sans MSAA".into());
    }
    if world.get::<TemporalAntiAliasing>(camera).is_some()
        || world.get::<TemporalJitter>(camera).is_some()
    {
        return Err("la caméra possède déjà un autre propriétaire temporel".into());
    }
    let revision = session
        .revision
        .checked_add(1)
        .ok_or("identité du réglage temporel épuisée")?;
    let owned = OwnedTaa {
        camera,
        depth: world.get::<DepthPrepass>(camera).is_none(),
        motion: world.get::<MotionVectorPrepass>(camera).is_none(),
        mip_bias: world.get::<MipBias>(camera).is_none(),
    };
    world.entity_mut(camera).insert((
        TemporalAntiAliasing::default(),
        taa_mirror::Request(revision),
        OwnedTemporalPrepasses {
            motion: owned.motion,
        },
    ));
    session.revision = revision;
    session.owned_taa = Some(owned);
    Ok(())
}

fn clear_owned_taa(world: &mut World, session: &mut Session) {
    let Some(owned) = session.owned_taa.take() else {
        return;
    };
    let Ok(mut camera) = world.get_entity_mut(owned.camera) else {
        return;
    };
    // Required components outlive removal of their requester in Bevy. Remove
    // only those added here, while preserving the motion-blur consumer.
    let motion_blur = camera.contains::<bevy::post_process::motion_blur::MotionBlur>();
    camera.remove::<(
        TemporalAntiAliasing,
        TemporalJitter,
        taa_mirror::Request,
        OwnedTemporalPrepasses,
    )>();
    if owned.mip_bias {
        camera.remove::<MipBias>();
    }
    if owned.depth {
        camera.remove::<DepthPrepass>();
    }
    if owned.motion && !motion_blur {
        camera.remove::<MotionVectorPrepass>();
    }
}

fn enable_source(world: &mut World, session: &Session, camera: Entity) -> Result<(), String> {
    session.source_registration.clone()?;
    // A no-hulls assertion must come from an audit of this actual scene. Neither
    // its module name nor the material registry establishes that assertion.
    let no_hulls = matches!(
        world.get::<temporal::render::HullRequirement>(camera),
        Some(temporal::render::HullRequirement::NoHullsInScene)
    );
    if !no_hulls && !session.hull_adapter_installed {
        return Err(
            "Tidewater expérimental : le contrat des coques de cette scène n’est pas encore validé"
                .into(),
        );
    }
    if world
        .get::<forge_engine::tidewater_underwater::TidewaterUnderwaterCamera>(camera)
        .is_none()
    {
        return Err("la passe sous-marine de cette caméra est absente".into());
    }
    temporal::render_owned::select(world, Some(camera))
}

fn taa_status(world: &World, session: &Session, camera: Entity) -> String {
    if world.get::<TemporalAntiAliasing>(camera).is_none() {
        return "TAA standard interrompu · composant caméra absent".into();
    }
    if !session.optics {
        return "TAA standard configuré sur la caméra joueur".into();
    }
    let report = world
        .get_resource::<taa_mirror::Observation>()
        .and_then(|bus| bus.snapshot(camera));
    match report {
        Some(report) if report.request == Some(session.revision) => match report.error {
            Some(error) => format!("TAA standard configuré · réfraction en attente : {error}"),
            None => "TAA standard configuré · caméra et réfraction synchronisées".into(),
        },
        _ => "TAA standard configuré · en attente du rendu de cette demande".into(),
    }
}

fn source_status(world: &World, camera: Entity) -> String {
    let Some(control) = world.get_resource::<temporal::render_owned::Control>() else {
        return "Tidewater expérimental interrompu · propriétaire absent".into();
    };
    let report = world
        .get_resource::<temporal::render_owned::Observation>()
        .and_then(temporal::render_owned::Observation::snapshot);
    let Some(report) = report.filter(|report| report.intent_revision == control.revision()) else {
        return "Tidewater expérimental · en attente du rendu de cette demande".into();
    };
    if let Some(error) = report.error {
        return format!("Tidewater expérimental · rendu en attente : {error}");
    }
    let resolve = world
        .get_resource::<temporal::resolve::ResolveObservations>()
        .and_then(|bus| bus.snapshot(camera));
    if let (Some(owner), Some(resolve)) = (report.owner, resolve)
        && let Some(commit) = owner.last_commit
        && commit.tag.view == camera
        && commit.tag == resolve.tag
        && commit.history_epoch == resolve.history_epoch
        && commit.resolve_generation == resolve.generation
        && commit.auxiliary_generation == resolve.auxiliary_generation
        && resolve.complete()
    {
        let auxiliary = world
            .get_resource::<temporal::render::AuxObservations>()
            .and_then(|bus| bus.snapshot(camera));
        let hdr = world
            .get_resource::<temporal::hdr::HdrObservations>()
            .and_then(|bus| bus.snapshot(camera));
        if auxiliary.is_some_and(|aux| {
            aux.tag == commit.tag && aux.generation == commit.auxiliary_generation && aux.complete()
        }) && hdr.is_some_and(|hdr| hdr.tag == commit.tag && hdr.complete())
        {
            return "Tidewater expérimental · commandes de rendu encodées".into();
        }
    }
    "Tidewater expérimental · passes et historique en préparation".into()
}

#[cfg(all(test, not(target_arch = "wasm32")))]
mod tests {
    use super::*;
    use bevy::{
        anti_alias::taa::TemporalAntiAliasing,
        camera::{Camera3d, Hdr},
        core_pipeline::prepass::{DepthPrepass, MotionVectorPrepass},
        post_process::motion_blur::MotionBlur,
        render::camera::{MipBias, TemporalJitter},
    };
    use forge_engine::{
        ForgePlayerCamera, tidewater_temporal as temporal,
        tidewater_underwater::TidewaterUnderwaterCamera,
    };

    fn session(optics: bool, hull_adapter_installed: bool) -> Session {
        Session {
            source_registration: Ok(()),
            hull_adapter_installed,
            optics,
            active: None,
            owned_taa: None,
            revision: 0,
        }
    }

    fn taa_camera(world: &mut World) -> Entity {
        world
            .spawn((ForgePlayerCamera, Camera3d::default(), Hdr, Msaa::Off))
            .id()
    }

    fn source_camera(world: &mut World, hdr: bool, underwater: bool, no_hulls: bool) -> Entity {
        let mut entity = world.spawn((ForgePlayerCamera, Camera3d::default(), Msaa::Off));
        if hdr {
            entity.insert(Hdr);
        }
        if underwater {
            entity.insert(TidewaterUnderwaterCamera);
        }
        entity.insert(if no_hulls {
            temporal::render::HullRequirement::NoHullsInScene
        } else {
            temporal::render::HullRequirement::Required
        });
        entity.id()
    }

    fn reset_input() {
        crate::authoring_input::reset_for_test();
    }

    #[test]
    fn enable_taa_refuses_foreign_taa_without_overwriting() {
        let mut world = World::new();
        let camera = taa_camera(&mut world);
        world
            .entity_mut(camera)
            .insert(TemporalAntiAliasing::default());
        let mut session = session(false, false);

        let result = enable_taa(&mut world, &mut session, camera);

        assert!(result.is_err());
        assert!(world.get::<TemporalAntiAliasing>(camera).is_some());
        assert!(
            world
                .get::<forge_engine::tidewater_refraction::taa_mirror::Request>(camera)
                .is_none()
        );
        assert!(session.owned_taa.is_none());
        assert_eq!(session.revision, 0);
    }

    #[test]
    fn enable_taa_refuses_foreign_jitter_without_overwriting() {
        let mut world = World::new();
        let camera = taa_camera(&mut world);
        let offset = Vec2::new(0.25, -0.5);
        world.entity_mut(camera).insert(TemporalJitter { offset });
        let mut session = session(false, false);

        let result = enable_taa(&mut world, &mut session, camera);

        assert!(result.is_err());
        assert_eq!(world.get::<TemporalJitter>(camera).unwrap().offset, offset);
        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());
        assert!(session.owned_taa.is_none());
        assert_eq!(session.revision, 0);
    }

    #[test]
    fn clear_owned_taa_preserves_preexisting_prepasses_mip_bias_and_motion_blur() {
        let mut world = World::new();
        let camera = world
            .spawn((
                ForgePlayerCamera,
                Camera3d::default(),
                Hdr,
                Msaa::Off,
                DepthPrepass,
                MipBias(0.25),
                MotionBlur::default(),
            ))
            .id();
        let mut session = session(false, false);

        enable_taa(&mut world, &mut session, camera).expect("valid camera owns TAA");
        assert!(
            session
                .owned_taa
                .as_ref()
                .is_some_and(|owned| { !owned.depth && !owned.motion && !owned.mip_bias })
        );

        clear_owned_taa(&mut world, &mut session);

        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());
        assert!(world.get::<TemporalJitter>(camera).is_none());
        assert!(
            world
                .get::<forge_engine::tidewater_refraction::taa_mirror::Request>(camera)
                .is_none()
        );
        assert!(world.get::<DepthPrepass>(camera).is_some());
        assert!(world.get::<MotionVectorPrepass>(camera).is_some());
        assert_eq!(world.get::<MipBias>(camera).unwrap().0, 0.25);
        assert!(
            world
                .get::<bevy::post_process::motion_blur::MotionBlur>(camera)
                .is_some()
        );
    }

    #[test]
    fn clear_owned_taa_removes_only_owned_prepasses_without_blur() {
        let mut world = World::new();
        let camera = taa_camera(&mut world);
        let mut session = session(false, false);

        enable_taa(&mut world, &mut session, camera).expect("valid camera owns TAA");
        assert!(world.get::<DepthPrepass>(camera).is_some());
        assert!(world.get::<MotionVectorPrepass>(camera).is_some());
        assert!(world.get::<MipBias>(camera).is_some());

        clear_owned_taa(&mut world, &mut session);

        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());
        assert!(world.get::<TemporalJitter>(camera).is_none());
        assert!(
            world
                .get::<forge_engine::tidewater_refraction::taa_mirror::Request>(camera)
                .is_none()
        );
        assert!(world.get::<DepthPrepass>(camera).is_none());
        assert!(world.get::<MotionVectorPrepass>(camera).is_none());
        assert!(world.get::<MipBias>(camera).is_none());
    }

    #[test]
    fn update_drops_owned_taa_when_camera_disappears_and_reactivates_replacement() {
        reset_input();
        crate::authoring_input::set_playing_for_test(true);
        crate::authoring_input::set_mode_for_test(TemporalMode::BevyTaa);

        let mut world = World::new();
        world.insert_resource(session(false, false));
        let old = taa_camera(&mut world);
        update(&mut world);
        assert!(world.get::<TemporalAntiAliasing>(old).is_some());
        assert_eq!(
            world.resource::<Session>().active,
            Some((old, TemporalMode::BevyTaa))
        );

        assert!(world.despawn(old));
        update(&mut world);
        assert_eq!(world.resource::<Session>().active, None);

        let replacement = taa_camera(&mut world);
        update(&mut world);
        assert!(world.get::<TemporalAntiAliasing>(replacement).is_some());
        assert_eq!(
            world.resource::<Session>().active,
            Some((replacement, TemporalMode::BevyTaa))
        );
        reset_input();
    }

    #[test]
    fn update_covers_off_taa_source_refusal_and_recovery() {
        reset_input();
        crate::authoring_input::set_playing_for_test(true);

        let mut world = World::new();
        world.insert_resource(session(true, false));
        let camera = taa_camera(&mut world);

        crate::authoring_input::set_mode_for_test(TemporalMode::BevyTaa);
        update(&mut world);
        assert_eq!(
            world.resource::<Session>().active,
            Some((camera, TemporalMode::BevyTaa))
        );
        assert!(world.get::<TemporalAntiAliasing>(camera).is_some());

        crate::authoring_input::set_mode_for_test(TemporalMode::Off);
        update(&mut world);
        assert_eq!(
            world.resource::<Session>().active,
            Some((camera, TemporalMode::Off))
        );
        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());

        crate::authoring_input::set_mode_for_test(TemporalMode::SourceResolve);
        update(&mut world);
        assert_eq!(world.resource::<Session>().active, None);
        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());

        crate::authoring_input::set_mode_for_test(TemporalMode::BevyTaa);
        update(&mut world);
        assert_eq!(
            world.resource::<Session>().active,
            Some((camera, TemporalMode::BevyTaa))
        );
        assert!(world.get::<TemporalAntiAliasing>(camera).is_some());

        crate::authoring_input::set_playing_for_test(false);
        update(&mut world);
        assert_eq!(world.resource::<Session>().active, None);
        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());
        reset_input();
    }

    #[test]
    fn source_refuses_required_hulls_without_adapter_before_temporal_intent() {
        let mut world = World::new();
        world.insert_resource(temporal::render_owned::Control::default());
        let camera = source_camera(&mut world, true, true, false);
        let session = session(true, false);

        let error = enable_source(&mut world, &session, camera).expect_err("hulls are unowned");

        assert!(error.contains("coques"));
        let control = world.resource::<temporal::render_owned::Control>();
        assert_eq!(control.selected_view(), None);
        assert_eq!(control.revision(), 0);
        assert!(
            world
                .get::<temporal::resolve::ResolveEnabled>(camera)
                .is_none()
        );
    }

    #[test]
    fn source_refuses_missing_underwater_before_temporal_intent() {
        let mut world = World::new();
        world.insert_resource(temporal::render_owned::Control::default());
        let camera = source_camera(&mut world, true, false, true);
        let session = session(true, false);

        let error =
            enable_source(&mut world, &session, camera).expect_err("underwater pass missing");

        assert!(error.contains("sous-marine"));
        let control = world.resource::<temporal::render_owned::Control>();
        assert_eq!(control.selected_view(), None);
        assert_eq!(control.revision(), 0);
        assert!(
            world
                .get::<temporal::resolve::ResolveEnabled>(camera)
                .is_none()
        );
    }

    #[test]
    fn source_refuses_missing_hdr_before_temporal_intent() {
        let mut world = World::new();
        world.insert_resource(temporal::render_owned::Control::default());
        let camera = source_camera(&mut world, false, true, true);
        let session = session(true, false);

        let error = enable_source(&mut world, &session, camera).expect_err("HDR pass missing");

        assert!(error.contains("MissingHdr"));
        let control = world.resource::<temporal::render_owned::Control>();
        assert_eq!(control.selected_view(), None);
        assert_eq!(control.revision(), 0);
        assert!(
            world
                .get::<temporal::resolve::ResolveEnabled>(camera)
                .is_none()
        );
    }

    #[test]
    fn clear_owned_taa_removes_owned_depth_but_keeps_motion_blur_prepass() {
        let mut world = World::new();
        let camera = taa_camera(&mut world);
        let mut session = session(false, false);

        enable_taa(&mut world, &mut session, camera).expect("valid camera owns TAA");
        assert!(
            session
                .owned_taa
                .as_ref()
                .is_some_and(|owned| owned.depth && owned.motion && owned.mip_bias)
        );
        assert!(world.get::<DepthPrepass>(camera).is_some());
        assert!(world.get::<MotionVectorPrepass>(camera).is_some());

        world.entity_mut(camera).insert(MotionBlur::default());
        assert!(world.get::<MotionBlur>(camera).is_some());

        clear_owned_taa(&mut world, &mut session);

        assert!(world.get::<TemporalAntiAliasing>(camera).is_none());
        assert!(world.get::<TemporalJitter>(camera).is_none());
        assert!(
            world
                .get::<forge_engine::tidewater_refraction::taa_mirror::Request>(camera)
                .is_none()
        );
        assert!(world.get::<OwnedTemporalPrepasses>(camera).is_none());
        assert!(world.get::<DepthPrepass>(camera).is_none());
        assert!(world.get::<MotionVectorPrepass>(camera).is_some());
        assert!(world.get::<MotionBlur>(camera).is_some());
        assert!(world.get::<MipBias>(camera).is_none());
    }
}
