// SPDX-License-Identifier: MIT
// Adapted from Tidewater core/CDLOD.js, copyright 2026 DRG Software Solutions LLC.
// Upstream 1438b1abfcaee3267092b75573014f4d9b4a983c; see third_party/tidewater/LICENSE.
//! Render-only continuous distance LOD selection. Canonical terrain is unchanged.
use thiserror::Error;

#[derive(Clone, Copy, Debug)]
pub struct CdlodConfig {
    pub grid_size: u16,
    pub leaf_size_m: f64,
    pub levels: u8,
    pub range_factor: f64,
    pub morph_start_ratio: f64,
    pub max_nodes: usize,
}

/// Unit patch shared by every selected node. The shader snaps and morphs its
/// coordinates onto each node's world lattice before applying displacement.
#[derive(Clone, Debug)]
pub struct CdlodGrid {
    pub positions: Vec<[f32; 3]>,
    pub indices: Vec<u32>,
}

impl CdlodConfig {
    /// Build the source's alternating-diagonal patch without engine types.
    ///
    /// # Errors
    /// Returns the same configuration errors as [`Self::validate`].
    pub fn grid(self) -> Result<CdlodGrid, CdlodError> {
        self.validate()?;
        let side = u32::from(self.grid_size);
        let mut positions = Vec::with_capacity(usize::from(self.grid_size + 1).pow(2));
        for z in 0..=self.grid_size {
            for x in 0..=self.grid_size {
                positions.push([
                    f32::from(x) / f32::from(self.grid_size),
                    0.0,
                    f32::from(z) / f32::from(self.grid_size),
                ]);
            }
        }
        let mut indices = Vec::with_capacity(usize::from(self.grid_size).pow(2) * 6);
        for z in 0..side {
            for x in 0..side {
                let a = z * (side + 1) + x;
                let b = a + 1;
                let c = a + side + 1;
                let d = c + 1;
                indices.extend(if (x + z).is_multiple_of(2) {
                    [a, c, b, b, c, d]
                } else {
                    [a, c, d, a, d, b]
                });
            }
        }
        Ok(CdlodGrid { positions, indices })
    }

    pub fn validate(self) -> Result<(), CdlodError> {
        if !(2..=128).contains(&self.grid_size)
            || !self.grid_size.is_multiple_of(2)
            || !(1..=12).contains(&self.levels)
            || !(1..=16384).contains(&self.max_nodes)
            || !(0.001..=1e6).contains(&self.leaf_size_m)
            || !(0.1..=100.0).contains(&self.range_factor)
            || !self.morph_start_ratio.is_finite()
            || !(0.0..1.0).contains(&self.morph_start_ratio)
            || self.leaf_size_m * 2048.0 * self.range_factor > 1e9
        {
            return Err(CdlodError::Config);
        }
        Ok(())
    }

    /// Source uniform rows: morph start, inverse span, lattice spacing, unused.
    pub fn morph_rows(self) -> Result<Vec<[f64; 4]>, CdlodError> {
        self.validate()?;
        let mut previous = 0.0;
        Ok((0..self.levels)
            .map(|level| {
                let size = self.leaf_size_m * 2_f64.powi(i32::from(level));
                let range = size * self.range_factor;
                let start = previous + (range - previous) * self.morph_start_ratio;
                previous = range;
                [
                    start,
                    1.0 / (range - start).max(0.001),
                    size / f64::from(self.grid_size),
                    0.0,
                ]
            })
            .collect())
    }
}

#[derive(Clone, Copy, Debug)]
pub enum CdlodRoot {
    Fixed {
        origin_xz: [f64; 2],
        size_m: f64,
    },
    /// Nine roots centred on the camera's current root cell (e.g. ocean).
    CameraFollowing,
}

#[derive(Clone, Copy, Debug)]
pub struct CdlodView {
    pub position: [f64; 3],
    /// Inward planes (nx,ny,nz,d). An infinite far plane may have a zero normal.
    pub planes: [[f64; 4]; 6],
}

#[derive(Clone, Copy, Debug, PartialEq)]
pub struct CdlodNode {
    pub origin_xz: [f64; 2],
    pub size_m: f64,
    pub lod: u8,
}

/// Whether an opaque patch mesh can retain its existing packing order.
/// Selection still returns the source's front-to-back order; this comparison
/// only avoids rebuilding identical patch geometry after a distance-sort change.
/// Multiplicity and every node field are preserved, including signed zero.
#[must_use]
pub fn same_node_geometry(a: &[CdlodNode], b: &[CdlodNode]) -> bool {
    if a.len() != b.len()
        || a.iter().chain(b).any(|node| {
            !node.origin_xz.iter().all(|value| value.is_finite()) || !node.size_m.is_finite()
        })
    {
        return false;
    }
    let key = |node: &CdlodNode| {
        (
            node.origin_xz[0].to_bits(),
            node.origin_xz[1].to_bits(),
            node.size_m.to_bits(),
            node.lod,
        )
    };
    if a.iter().zip(b).all(|(a, b)| key(a) == key(b)) {
        return true;
    }
    let mut a = a.iter().map(key).collect::<Vec<_>>();
    let mut b = b.iter().map(key).collect::<Vec<_>>();
    a.sort_unstable();
    b.sort_unstable();
    a == b
}

/// Pure CPU selection with source traversal and stable front-to-back ordering.
/// Capacity exhaustion is an error, never an apparently successful missing tile.
pub fn select_nodes(
    config: CdlodConfig,
    root: CdlodRoot,
    view: CdlodView,
    mut height_bounds: impl FnMut([f64; 2], f64) -> Result<[f64; 2], CdlodError>,
) -> Result<Vec<CdlodNode>, CdlodError> {
    config.validate()?;
    if view
        .position
        .iter()
        .any(|v| !v.is_finite() || v.abs() > 1e9)
        || view
            .planes
            .iter()
            .flatten()
            .any(|v| !v.is_finite() || v.abs() > 1e12)
    {
        return Err(CdlodError::View);
    }
    let top = config.levels - 1;
    let root_size = config.leaf_size_m * 2_f64.powi(i32::from(top));
    let (origin, n) = match root {
        CdlodRoot::Fixed { origin_xz, size_m } => {
            if origin_xz.iter().any(|v| !v.is_finite() || v.abs() > 1e9)
                || !size_m.is_finite()
                || size_m <= 0.0
                || (size_m / root_size).ceil() > 16.0
            {
                return Err(CdlodError::Root);
            }
            #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
            (origin_xz, (size_m / root_size).ceil() as u8)
        }
        CdlodRoot::CameraFollowing => (
            [
                (view.position[0] / root_size).floor() * root_size - root_size,
                (view.position[2] / root_size).floor() * root_size - root_size,
            ],
            3,
        ),
    };
    let mut nodes = Vec::new();
    let mut selector = Selector {
        config,
        view,
        nodes: &mut nodes,
        height_bounds: &mut height_bounds,
        visited: 0,
    };
    for z in 0..n {
        for x in 0..n {
            selector.visit(CdlodNode {
                origin_xz: [
                    origin[0] + f64::from(x) * root_size,
                    origin[1] + f64::from(z) * root_size,
                ],
                size_m: root_size,
                lod: top,
            })?;
        }
    }
    let distance = |node: &CdlodNode| {
        let dx = (node.origin_xz[0] - view.position[0])
            .max(0.0)
            .max(view.position[0] - node.origin_xz[0] - node.size_m);
        let dz = (node.origin_xz[1] - view.position[2])
            .max(0.0)
            .max(view.position[2] - node.origin_xz[1] - node.size_m);
        dx * dx + dz * dz
    };
    nodes.sort_by(|a, b| distance(a).total_cmp(&distance(b)));
    Ok(nodes)
}

struct Selector<'a, F> {
    config: CdlodConfig,
    view: CdlodView,
    nodes: &'a mut Vec<CdlodNode>,
    height_bounds: &'a mut F,
    visited: usize,
}
impl<F: FnMut([f64; 2], f64) -> Result<[f64; 2], CdlodError>> Selector<'_, F> {
    fn bounds(&mut self, node: CdlodNode) -> Result<([f64; 3], [f64; 3]), CdlodError> {
        let [low, high] = (self.height_bounds)(node.origin_xz, node.size_m)?;
        if !low.is_finite()
            || !high.is_finite()
            || low > high
            || low.abs() > 1e9
            || high.abs() > 1e9
        {
            return Err(CdlodError::Bounds);
        }
        Ok((
            [node.origin_xz[0], low, node.origin_xz[1]],
            [
                node.origin_xz[0] + node.size_m,
                high,
                node.origin_xz[1] + node.size_m,
            ],
        ))
    }
    fn visible(&self, low: [f64; 3], high: [f64; 3]) -> bool {
        self.view.planes.iter().all(|plane| {
            let point = std::array::from_fn::<_, 3, _>(|axis| {
                if plane[axis] > 0.0 {
                    high[axis]
                } else {
                    low[axis]
                }
            });
            plane[0] * point[0] + plane[1] * point[1] + plane[2] * point[2] + plane[3] >= 0.0
        })
    }
    fn in_range(&self, low: [f64; 3], high: [f64; 3], lod: u8) -> bool {
        let delta = std::array::from_fn::<_, 3, _>(|axis| {
            (low[axis] - self.view.position[axis])
                .max(0.0)
                .max(self.view.position[axis] - high[axis])
        });
        let range = self.config.leaf_size_m * 2_f64.powi(i32::from(lod)) * self.config.range_factor;
        delta[0] * delta[0] + delta[1] * delta[1] + delta[2] * delta[2] <= range * range
    }
    fn add(&mut self, node: CdlodNode) -> Result<(), CdlodError> {
        if self.nodes.len() >= self.config.max_nodes {
            return Err(CdlodError::Capacity);
        }
        self.nodes.push(node);
        Ok(())
    }
    fn visit(&mut self, node: CdlodNode) -> Result<bool, CdlodError> {
        self.visited += 1;
        if self.visited > 1_000_000 {
            return Err(CdlodError::Traversal);
        }
        let (low, high) = self.bounds(node)?;
        if !self.in_range(low, high, node.lod) {
            return Ok(false);
        }
        if !self.visible(low, high) {
            return Ok(true);
        }
        if node.lod == 0 || !self.in_range(low, high, node.lod - 1) {
            self.add(node)?;
            return Ok(true);
        }
        let half = node.size_m * 0.5;
        for [dx, dz] in [[0.0, 0.0], [half, 0.0], [0.0, half], [half, half]] {
            let child = CdlodNode {
                origin_xz: [node.origin_xz[0] + dx, node.origin_xz[1] + dz],
                size_m: half,
                lod: node.lod - 1,
            };
            if !self.visit(child)? {
                let (low, high) = self.bounds(child)?;
                if self.visible(low, high) {
                    self.add(CdlodNode {
                        lod: node.lod,
                        ..child
                    })?;
                }
            }
        }
        Ok(true)
    }
}

#[derive(Clone, Copy, Debug, Error, PartialEq, Eq)]
pub enum CdlodError {
    #[error("invalid CDLOD configuration")]
    Config,
    #[error("invalid CDLOD view")]
    View,
    #[error("invalid CDLOD root domain")]
    Root,
    #[error("invalid CDLOD height bounds")]
    Bounds,
    #[error("CDLOD node capacity exhausted")]
    Capacity,
    #[error("CDLOD traversal budget exhausted")]
    Traversal,
}
