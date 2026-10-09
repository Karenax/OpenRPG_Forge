//! Camera-following grass uses the same cells and meshes as the static oracle.
use bevy::prelude::*;
use forge_engine::{
    ForgePlayerCamera, PlayerSimulationReady,
    tidewater_plants::grass::{Grid, Resident},
};

#[derive(Clone, serde::Serialize)]
pub(super) struct Snapshot {
    pub camera: [f32; 3],
    pub counts: [usize; 3],
    pub hashes: [forge_core::ContentHash; 3],
}

#[derive(Resource)]
pub(super) struct Live {
    grid: Grid,
    resident: Resident,
    previous: Option<(Transform, [u32; 2])>,
    pub updates: u64,
    pub samples: Vec<Snapshot>,
}

impl Live {
    pub fn buffers(&self) -> Vec<Handle<bevy::render::storage::ShaderBuffer>> {
        self.resident.buffers()
    }

    pub fn navigation_valid(&self, extended: bool) -> bool {
        self.updates > 10
            && self.samples.len() >= if extended { 10 } else { 2 }
            && self
                .samples
                .iter()
                .any(|s| s.counts.iter().sum::<usize>() > 0)
            && self
                .samples
                .windows(2)
                .any(|pair| pair[0].hashes != pair[1].hashes)
    }

    pub fn new(grid: Grid, resident: Resident) -> Self {
        Self {
            grid,
            resident,
            previous: None,
            updates: 0,
            samples: Vec::new(),
        }
    }

    fn update(
        &mut self,
        world: &mut World,
        transform: Transform,
        projection: &PerspectiveProjection,
    ) -> Result<(), String> {
        let lens = [projection.fov.to_bits(), projection.aspect_ratio.to_bits()];
        if self.previous.is_some_and(|(old, old_lens)| {
            (transform.translation - old.translation)
                .abs()
                .cmplt(Vec3::splat(0.05))
                .all()
                && (transform.rotation * Vec3::Z - old.rotation * Vec3::Z)
                    .abs()
                    .cmplt(Vec3::splat(1.0e-3))
                    .all()
                && lens == old_lens
        }) {
            return Ok(());
        }
        let cells = self
            .grid
            .select(crate::terrain_lod::source_view(&transform, projection))?;
        self.resident.update(world, &cells, Visibility::Visible)?;
        self.previous = Some((transform, lens));
        self.updates += 1;
        if self.samples.len() < 128
            && self
                .samples
                .last()
                .is_none_or(|s| Vec3::from_array(s.camera).distance(transform.translation) >= 8.0)
        {
            self.samples.push(Snapshot {
                camera: transform.translation.to_array(),
                counts: std::array::from_fn(|i| cells[i].len()),
                hashes: std::array::from_fn(|i| {
                    let bytes: Vec<_> = cells[i]
                        .iter()
                        .flatten()
                        .flat_map(|v| v.to_le_bytes())
                        .collect();
                    forge_core::bytes_sha256(&bytes)
                }),
            });
        }
        Ok(())
    }
}

pub(super) fn install(app: &mut App) {
    app.add_systems(
        PostUpdate,
        update.after(bevy::transform::TransformSystems::Propagate),
    );
}

fn update(world: &mut World) {
    if !world.resource::<PlayerSimulationReady>().0 || !world.contains_resource::<Live>() {
        return;
    }
    let mut cameras =
        world.query_filtered::<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>();
    let Ok((camera, Projection::Perspective(projection))) = cameras.single(world) else {
        return;
    };
    let transform = camera.compute_transform();
    let projection = projection.clone();
    let started = super::walk::perf::start(world);
    world.resource_scope(|world, mut live: Mut<Live>| {
        if let Err(error) = live.update(world, transform, &projection) {
            error!(%error, "grass navigation failed");
            world.write_message(bevy::app::AppExit::error());
        }
    });
    super::walk::perf::end(world, started, "grass_selection");
}
