#![deny(unsafe_code)]
#![cfg_attr(
    target_env = "msvc",
    allow(
        linker_messages,
        reason = "MSVC emits its normal cdylib import-library creation message"
    )
)]
//! Pure navigation generation; neither Bevy nor a physics world is constructed here.
//! Geometry is supplied by the engine's installed Rapier shapes. Every result is
//! validated in its original winding before it can become a live island.

mod provider;
use glam::Vec3;
pub use landmass;
use landmass::{CoordinateSystem, PointSampleDistance3d};
pub use provider::{BuiltTile, generate_tile, generate_tile_cancellable};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

pub const PROVIDER_SOURCE_SHA256: &str = env!("FORGE_NAV_PROVIDER_SOURCE_SHA256");
/// Horizontal contour simplification bound, in generation cells.
pub const CONTOUR_ERROR_CELLS: f32 = 1.1;

/// Exact NPC envelope shared by generation, navigation and the physical adapter.
#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AgentProfile {
    pub radius_m: f32,
    pub height_m: f32,
    pub speed_mps: f32,
    pub step_m: f32,
    pub snap_m: f32,
    pub max_slope_radians: f32,
}
impl Default for AgentProfile {
    fn default() -> Self {
        Self {
            radius_m: 0.35,
            height_m: 1.90,
            speed_mps: 2.0,
            step_m: 0.4,
            snap_m: 0.1,
            max_slope_radians: 45.0_f32.to_radians(),
        }
    }
}
impl AgentProfile {
    pub fn validate(self) -> Result<(), String> {
        if [
            self.radius_m,
            self.height_m,
            self.speed_mps,
            self.step_m,
            self.snap_m,
            self.max_slope_radians,
        ]
        .iter()
        .any(|v| !v.is_finite())
            || !(0.05..=2.0).contains(&self.radius_m)
            || !(self.radius_m * 2.0..=8.0).contains(&self.height_m)
            || !(0.0..=20.0).contains(&self.speed_mps)
            || !(0.0..=self.height_m).contains(&self.step_m)
            || !(0.0..=self.height_m).contains(&self.snap_m)
            || !(0.0..std::f32::consts::FRAC_PI_2).contains(&self.max_slope_radians)
        {
            return Err("invalid navigation agent profile".into());
        }
        Ok(())
    }
    /// Tiles contain 192 horizontal voxels. Their border is five voxels for
    /// this radius/2 generation profile; no cropped detail mesh is invented.
    #[must_use]
    pub fn tile_width_m(self) -> f32 {
        self.radius_m * 0.5 * 192.0
    }
    #[must_use]
    pub fn border_m(self) -> f32 {
        self.radius_m * 0.5 * 5.0
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub struct TileKey {
    pub x: i32,
    pub z: i32,
}
impl TileKey {
    #[must_use]
    #[allow(
        clippy::cast_possible_truncation,
        reason = "bounded finite world positions are checked before requests"
    )]
    pub fn at(point: [f32; 3], profile: AgentProfile) -> Self {
        let width = profile.tile_width_m();
        Self {
            x: (point[0] / width).floor() as i32,
            z: (point[2] / width).floor() as i32,
        }
    }
    #[must_use]
    #[allow(
        clippy::cast_precision_loss,
        reason = "tile coordinates are bounded to the supported world extent"
    )]
    pub fn bounds(self, profile: AgentProfile) -> ([f32; 2], [f32; 2]) {
        let width = profile.tile_width_m();
        let min = [self.x as f32 * width, self.z as f32 * width];
        (min, [min[0] + width, min[1] + width])
    }
}

/// A derived convex area from a canonical traversal document. Zero is blocked;
/// other indices select costs in Landmass. Height limits preserve bridges above water.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaVolume {
    pub vertices_xz: Vec<[f32; 2]>,
    pub min_y: f32,
    pub max_y: f32,
    pub area: u8,
}
impl AreaVolume {
    pub fn validate(&self) -> Result<(), String> {
        let points = &self.vertices_xz;
        if points.len() < 3
            || points.len() > 256
            || points
                .iter()
                .flatten()
                .any(|v| !v.is_finite() || v.abs() > 100_000.0)
            || !self.min_y.is_finite()
            || !self.max_y.is_finite()
            || self.min_y.abs() > 100_000.0
            || self.max_y.abs() > 100_000.0
            || self.min_y > self.max_y
        {
            return Err("invalid canonical navigation area bounds".into());
        }
        let cross = |a: [f32; 2], b: [f32; 2], c: [f32; 2]| {
            (f64::from(b[0]) - f64::from(a[0])) * (f64::from(c[1]) - f64::from(a[1]))
                - (f64::from(b[1]) - f64::from(a[1])) * (f64::from(c[0]) - f64::from(a[0]))
        };
        let orientation = cross(points[0], points[1], points[2]);
        if orientation == 0.0
            || (0..points.len()).any(|i| {
                cross(
                    points[i],
                    points[(i + 1) % points.len()],
                    points[(i + 2) % points.len()],
                ) * orientation
                    <= 0.0
            })
        {
            return Err("canonical navigation area must be strictly convex".into());
        }
        for a in 0..points.len() {
            for b in a + 1..points.len() {
                let a1 = (a + 1) % points.len();
                let b1 = (b + 1) % points.len();
                if a == b1 || a1 == b {
                    continue;
                }
                if cross(points[a], points[a1], points[b])
                    * cross(points[a], points[a1], points[b1])
                    <= 0.0
                    && cross(points[b], points[b1], points[a])
                        * cross(points[b], points[b1], points[a1])
                        <= 0.0
                {
                    return Err("canonical navigation area has intersecting edges".into());
                }
            }
        }
        Ok(())
    }
}

/// Prepared curved primitive identity and a conservative footprint certificate.
/// Its actual blocked polygon is carried in `TileInput::areas` and hashed together
/// with this receipt. No curved primitive supplies a walkable triangle surface.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct CurvedObstacleProjection {
    pub algorithm: String,
    pub kind: String,
    pub parameters: Vec<f32>,
    pub position: [f32; 3],
    pub rotation: [f32; 4],
    pub planes: u16,
    pub clearance_m: f32,
    pub footprint_excess_bound_m: f32,
}
impl CurvedObstacleProjection {
    pub fn validate(&self, profile: AgentProfile) -> Result<(), String> {
        let parameters_valid = match self.kind.as_str() {
            "cylinder" => self.parameters.len() == 2 && self.parameters.iter().all(|v| *v > 0.0),
            "capsule" => self.parameters.len() == 7 && self.parameters[6] > 0.0,
            "ball" => self.parameters.len() == 1 && self.parameters[0] > 0.0,
            _ => false,
        };
        let rotation_norm = self.rotation.iter().map(|v| v * v).sum::<f32>();
        let expected_clearance = profile.radius_m
            + profile.radius_m * 0.5 * (std::f32::consts::FRAC_1_SQRT_2 + CONTOUR_ERROR_CELLS)
            + 0.01
            + 0.02;
        if self.algorithm != "rapier-support-planes-v1"
            || self.planes != 32
            || !parameters_valid
            || self
                .parameters
                .iter()
                .chain(self.position.iter())
                .chain(self.rotation.iter())
                .any(|v| !v.is_finite() || v.abs() > 100_000.0)
            || (rotation_norm - 1.0).abs() > 1.0e-4
            || !self.clearance_m.is_finite()
            || (self.clearance_m - expected_clearance).abs() > 1.0e-5
            || !self.footprint_excess_bound_m.is_finite()
            || !(0.02..=100_000.0).contains(&self.footprint_excess_bound_m)
        {
            return Err("invalid curved navigation obstacle receipt".into());
        }
        Ok(())
    }
}

/// Job identity travels unchanged through native tasks and transferable worker bytes.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct TileInput {
    pub key: TileKey,
    pub profile: AgentProfile,
    pub revision: u64,
    pub source_hash: String,
    pub token: u64,
    pub vertices: Vec<[f32; 3]>,
    pub triangles: Vec<[u32; 3]>,
    pub areas: Vec<AreaVolume>,
    /// None explicitly denotes absence of an additional canonical traversal policy.
    pub policy_source_hash: Option<String>,
    pub curved_obstacles: Vec<CurvedObstacleProjection>,
}

/// Hash geometry, exact transforms already applied by Rapier, profile and policy.
/// Runtime identities/tokens are deliberately excluded; restoring geometry restores its hash.
pub fn source_hash(input: &TileInput) -> Result<String, String> {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let bytes = serde_json::to_vec(&(
        PROVIDER_SOURCE_SHA256,
        input.key,
        input.profile,
        &input.vertices,
        &input.triangles,
        &input.areas,
        &input.policy_source_hash,
        &input.curved_obstacles,
    ))
    .map_err(|error| error.to_string())?;
    Ok(Sha256::digest(bytes)
        .iter()
        .flat_map(|byte| {
            [
                char::from(HEX[usize::from(*byte >> 4)]),
                char::from(HEX[usize::from(*byte & 15)]),
            ]
        })
        .collect())
}

/// Same Y-up/-Z-forward conversion as the qualified upstream Bevy adapter.
/// Its ordinary polygon and detail-triangle reversals are coordinate conversion,
/// never a conditional repair of an invalid supplier output.
pub struct ForgeCoordinates;
impl CoordinateSystem for ForgeCoordinates {
    type Coordinate = landmass::Vec3;
    type SampleDistance = PointSampleDistance3d;
    const FLIP_POLYGONS: bool = true;
    fn to_landmass(v: &Self::Coordinate) -> landmass::Vec3 {
        landmass::Vec3::new(v.x, -v.z, v.y)
    }
    fn from_landmass(v: &landmass::Vec3) -> Self::Coordinate {
        landmass::Vec3::new(v.x, v.z, -v.y)
    }
}

#[must_use]
pub fn point(value: [f32; 3]) -> landmass::Vec3 {
    landmass::Vec3::from_array(value)
}
#[must_use]
pub fn vector(value: [f32; 3]) -> Vec3 {
    Vec3::from_array(value)
}

/// The dedicated worker has no DOM/GPU start function. It invokes this same
/// validated Rust pipeline on its own WebAssembly instance.
#[cfg(all(target_arch = "wasm32", feature = "web"))]
#[wasm_bindgen::prelude::wasm_bindgen]
pub fn generate_navigation_tile(bytes: &[u8]) -> Result<Vec<u8>, wasm_bindgen::JsValue> {
    if bytes.len() > 32 * 1024 * 1024 {
        return Err(wasm_bindgen::JsValue::from_str(
            "navigation worker input byte budget exceeded",
        ));
    }
    let input: TileInput = serde_json::from_slice(bytes)
        .map_err(|error| wasm_bindgen::JsValue::from_str(&error.to_string()))?;
    let result = generate_tile(input).map_err(|error| wasm_bindgen::JsValue::from_str(&error))?;
    serde_json::to_vec(&result).map_err(|error| wasm_bindgen::JsValue::from_str(&error.to_string()))
}
