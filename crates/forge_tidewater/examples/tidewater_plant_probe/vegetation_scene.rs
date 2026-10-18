//! Native fixture adapter around the shared plant renderer.
use super::{PlantMaterial, Probe, ProbeMode};
use bevy::{prelude::*, render::storage::ShaderBuffer};
pub(super) use forge_tidewater::tidewater_plants::scene::{
    LevelDescription, Vegetation, load, mesh,
};
pub(super) fn spawn(
    commands: &mut Commands,
    probe: &Probe,
    base: &PlantMaterial,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<PlantMaterial>,
    buffers: &mut Assets<ShaderBuffer>,
) {
    let Some(scene) = &probe.vegetation_scene else {
        return;
    };
    let camera = Vec3::from_array(
        probe
            .camera_pose
            .as_ref()
            .expect("validated scene camera")
            .position,
    );
    let visibility = if matches!(
        probe.mode,
        ProbeMode::Surface
            | ProbeMode::Silhouette
            | ProbeMode::VegetationDirect
            | ProbeMode::VegetationEnvironment
            | ProbeMode::SceneLighting
    ) {
        Visibility::Visible
    } else {
        Visibility::Hidden
    };
    if matches!(
        probe.plant_projection,
        super::PlantProjection::ResidentNavigation
            | super::PlantProjection::Walk
            | super::PlantProjection::WalkSmoke
            | super::PlantProjection::WalkLongSmoke
    ) {
        resident_navigation(commands, base.clone(), camera, visibility);
        return;
    }
    forge_tidewater::tidewater_plants::scene::spawn(
        commands,
        scene,
        base,
        forge_tidewater::tidewater_plants::scene::View { camera, visibility },
        meshes,
        materials,
        buffers,
    )
    .expect("validated plant view");
}

#[derive(Resource)]
pub(super) struct ResidentProof {
    pub resident: forge_tidewater::tidewater_plants::scene::resident::Resident,
}

fn resident_navigation(
    commands: &mut Commands,
    base: PlantMaterial,
    camera: Vec3,
    visibility: Visibility,
) {
    commands.queue(move |world: &mut World| {
        let scene = world
            .resource_mut::<Probe>()
            .vegetation_scene
            .take()
            .expect("validated scene");
        let mut resident = forge_tidewater::tidewater_plants::scene::resident::Resident::stage(
            world, scene, &base,
        )
        .expect("resident assets");
        let sizes = |world: &World| {
            (
                world.resource::<Assets<Mesh>>().len(),
                world.resource::<Assets<PlantMaterial>>().len(),
                world.resource::<Assets<ShaderBuffer>>().len(),
            )
        };
        let original = sizes(world);
        let mut retained = 0;
        for position in [camera, Vec3::new(400., 30., 400.), camera] {
            let report = resident
                .select(
                    world,
                    forge_tidewater::tidewater_plants::scene::View {
                        camera: position,
                        visibility,
                    },
                )
                .expect("resident selection");
            retained += report.retained;
            assert_eq!(sizes(world), original);
        }
        assert!(retained > 0);
        info!(
            retained,
            "Resident plant A/B/A selection completed before GPU capture"
        );
        world.insert_resource(ResidentProof { resident });
    });
}
