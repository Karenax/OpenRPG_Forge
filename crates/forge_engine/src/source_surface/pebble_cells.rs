//! Disposable camera-following cell selection; no placement documents or GPU side effects.

const CELL_METRES: f64 = 4.0;
const RADIUS_METRES: f64 = 25.0;
const MAX_CELLS: usize = 240;

/// Immutable acquired grid; callers replace it when the terrain revision changes.
pub struct CellMap {
    side: usize,
    origin: f64,
    flags: Vec<u8>,
    min_y: Vec<f32>,
    max_y: Vec<f32>,
}

impl CellMap {
    /// Validate the acquired grid before it can drive a render projection.
    ///
    /// # Errors
    /// Rejects unbounded dimensions, invalid flags, heights or world origins.
    pub fn new(
        side: usize,
        origin: f64,
        flags: Vec<u8>,
        min_y: Vec<f32>,
        max_y: Vec<f32>,
    ) -> Result<Self, String> {
        let count = side.checked_mul(side).ok_or("Pebble grid size overflow")?;
        if side == 0
            || side > 4096
            || !origin.is_finite()
            || origin.abs() > 1_000_000.0
            || flags.len() != count
            || min_y.len() != count
            || max_y.len() != count
            || flags.iter().any(|flag| *flag > 1)
            || min_y.iter().chain(&max_y).any(|y| !y.is_finite())
            || flags
                .iter()
                .enumerate()
                .any(|(i, flag)| *flag != 0 && min_y[i] > max_y[i])
        {
            return Err("Invalid acquired pebble grid".into());
        }
        Ok(Self {
            side,
            origin,
            flags,
            min_y,
            max_y,
        })
    }
}

/// Source camera signature; the terrain height is sampled by the consuming adapter.
#[derive(Clone, Copy)]
pub struct Camera {
    pub position: [f64; 3],
    pub world_back: [f64; 3],
    pub projection_scale: [f64; 2],
    pub terrain_height: f64,
}

/// Source iCell values, ready for the instance buffer; no shadow-caster geometry.
#[derive(Default)]
pub struct Selector {
    last: Option<Camera>,
    slots: Vec<[f32; 4]>,
}

impl Selector {
    /// Reset after replacing the terrain/grid, even if the camera is unchanged.
    pub fn invalidate(&mut self) {
        self.last = None;
    }

    #[must_use]
    pub fn slots(&self) -> &[[f32; 4]] {
        &self.slots
    }

    /// Select original occupied cells in row order using a read-only frustum predicate.
    /// Returns false when the original camera cache permits skipping the update.
    ///
    /// # Errors
    /// Refuses nonfinite or unbounded camera inputs before changing cached state.
    #[allow(
        clippy::cast_possible_truncation,
        clippy::cast_possible_wrap,
        clippy::cast_precision_loss,
        clippy::float_cmp,
        reason = "Bounded source grid arithmetic narrows to indices and the original float32 iCell ABI; projection cache equality is exact by source contract."
    )]
    pub fn update(
        &mut self,
        grid: &CellMap,
        camera: Camera,
        mut intersects: impl FnMut([f64; 3], [f64; 3]) -> bool,
    ) -> Result<bool, String> {
        if camera
            .position
            .iter()
            .chain(&camera.world_back)
            .chain(&camera.projection_scale)
            .any(|v| !v.is_finite() || v.abs() > 1_000_000.0)
            || !camera.terrain_height.is_finite()
        {
            return Err("Invalid pebble camera".into());
        }
        if let Some(last) = self.last
            && camera
                .position
                .iter()
                .zip(last.position)
                .all(|(a, b)| (a - b).abs() < 0.05)
            && camera
                .world_back
                .iter()
                .zip(last.world_back)
                .all(|(a, b)| (a - b).abs() < 1e-3)
            && camera.projection_scale == last.projection_scale
        {
            return Ok(false);
        }
        self.last = Some(camera);
        self.slots.clear();
        let [cx, cy, cz] = camera.position;
        if cy - camera.terrain_height >= RADIUS_METRES {
            return Ok(true);
        }
        let lower = |v: f64| {
            ((v - RADIUS_METRES - grid.origin) / CELL_METRES)
                .floor()
                .max(0.0) as isize
        };
        let upper = |v: f64| {
            ((v + RADIUS_METRES - grid.origin) / CELL_METRES)
                .floor()
                .min((grid.side - 1) as f64) as isize
        };
        for j in lower(cz)..=upper(cz) {
            for i in lower(cx)..=upper(cx) {
                let index = usize::try_from(j).map_err(|e| e.to_string())? * grid.side
                    + usize::try_from(i).map_err(|e| e.to_string())?;
                if grid.flags[index] == 0 {
                    continue;
                }
                let x0 = grid.origin + i as f64 * CELL_METRES;
                let z0 = grid.origin + j as f64 * CELL_METRES;
                let dx = (x0 - cx).max(0.0).max(cx - x0 - CELL_METRES);
                let dz = (z0 - cz).max(0.0).max(cz - z0 - CELL_METRES);
                if dx.hypot(dz) > RADIUS_METRES
                    || !intersects(
                        [x0, f64::from(grid.min_y[index]) - 0.2, z0],
                        [
                            x0 + CELL_METRES,
                            f64::from(grid.max_y[index]) + 0.3,
                            z0 + CELL_METRES,
                        ],
                    )
                {
                    continue;
                }
                if self.slots.len() >= MAX_CELLS {
                    break;
                }
                self.slots.push([x0 as f32, z0 as f32, 0.0, 0.0]);
            }
        }
        Ok(true)
    }
}
