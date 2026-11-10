//! Bounded parser for the ASCII NWmax walkmesh format preserved by the Aurora bundle.
//!
//! NWN:EE stores tile WOK/PWK/DWK walkmeshes as ASCII `NWmax WALKMESH` documents. The
//! parser deliberately reads only vertices, triangles and surface identifiers; node
//! transform/lighting/AABB metadata is ignored. Geometry is expressed in the NVN source
//! basis (Z-up) and is converted once to the Forge right-handed Y-up basis by
//! [`Walkmesh::transformed`].

use forge_core::Vec3;
use serde::{Deserialize, Serialize};
use std::fmt;

pub const MAX_WALKMESH_BYTES: usize = 8 * 1024 * 1024;
pub const MAX_WALKMESH_VERTICES: usize = 1_000_000;
pub const MAX_WALKMESH_FACES: usize = 2_000_000;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum WalkmeshError {
    TooLarge(usize),
    NotText,
    UnsupportedFormat,
    MissingVertices,
    MissingFaces,
    InvalidVertex(usize),
    InvalidFace(usize),
    TooManyVertices(usize),
    TooManyFaces(usize),
    IndexOutOfRange { face: usize, vertex: u32 },
    DegenerateFace(usize),
    NonFiniteVertex(usize),
}

impl fmt::Display for WalkmeshError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::TooLarge(size) => write!(formatter, "walkmesh exceeds the byte limit ({size})"),
            Self::NotText => formatter.write_str("walkmesh is not UTF-8 text"),
            Self::UnsupportedFormat => formatter.write_str(
                "walkmesh is not an ASCII NWmax WALKMESH; binary/unknown formats are refused",
            ),
            Self::MissingVertices => formatter.write_str("walkmesh has no verts block"),
            Self::MissingFaces => formatter.write_str("walkmesh has no faces block"),
            Self::InvalidVertex(index) => write!(formatter, "invalid vertex line {index}"),
            Self::InvalidFace(index) => write!(formatter, "invalid face line {index}"),
            Self::TooManyVertices(count) => write!(formatter, "too many vertices ({count})"),
            Self::TooManyFaces(count) => write!(formatter, "too many faces ({count})"),
            Self::IndexOutOfRange { face, vertex } => {
                write!(formatter, "face {face} references missing vertex {vertex}")
            }
            Self::DegenerateFace(index) => write!(formatter, "face {index} is degenerate"),
            Self::NonFiniteVertex(index) => write!(formatter, "vertex {index} is not finite"),
        }
    }
}

impl std::error::Error for WalkmeshError {}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Walkmesh {
    pub vertices: Vec<[f32; 3]>,
    pub faces: Vec<WalkmeshFace>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WalkmeshFace {
    pub vertices: [u32; 3],
    pub surface: u32,
}

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WorldTriangle {
    pub a: Vec3,
    pub b: Vec3,
    pub c: Vec3,
    pub surface: u32,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WalkSurface {
    pub triangles: Vec<WorldTriangle>,
    pub min: Vec3,
    pub max: Vec3,
}

impl Walkmesh {
    pub fn parse(bytes: &[u8]) -> Result<Self, WalkmeshError> {
        if bytes.len() > MAX_WALKMESH_BYTES {
            return Err(WalkmeshError::TooLarge(bytes.len()));
        }
        let text = std::str::from_utf8(bytes).map_err(|_| WalkmeshError::NotText)?;
        if !text.contains("NWmax WALKMESH")
            && !text.contains("NWmax DWKMESH")
            && !text.contains("MAXDOOR ASCII")
            && !text.contains("beginwalkmeshgeom")
            && !text.contains("node trimesh")
        {
            return Err(WalkmeshError::UnsupportedFormat);
        }
        let mut lines = text.lines().map(str::trim);
        let mut vertices = Vec::new();
        let mut faces = Vec::new();
        let mut saw_vertices = false;
        let mut saw_faces = false;
        let mut base = 0usize;
        while let Some(line) = lines.next() {
            let line = line.split(';').next().unwrap_or("").trim();
            if line.is_empty() || line.starts_with('#') {
                continue;
            }
            if let Some(count) = token_count(line, "verts") {
                saw_vertices = true;
                if vertices.len().saturating_add(count) > MAX_WALKMESH_VERTICES {
                    return Err(WalkmeshError::TooManyVertices(vertices.len() + count));
                }
                base = vertices.len();
                for index in 0..count {
                    let vertex = lines
                        .next()
                        .ok_or(WalkmeshError::InvalidVertex(index))?
                        .split(';')
                        .next()
                        .unwrap_or("")
                        .split_whitespace()
                        .map(str::parse::<f32>)
                        .collect::<Result<Vec<_>, _>>()
                        .map_err(|_| WalkmeshError::InvalidVertex(index))?;
                    if vertex.len() < 3 {
                        return Err(WalkmeshError::InvalidVertex(index));
                    }
                    if !vertex[..3].iter().all(|value| value.is_finite()) {
                        return Err(WalkmeshError::NonFiniteVertex(vertices.len()));
                    }
                    vertices.push([vertex[0], vertex[1], vertex[2]]);
                }
                continue;
            }
            if let Some(count) = token_count(line, "faces") {
                saw_faces = true;
                if faces.len().saturating_add(count) > MAX_WALKMESH_FACES {
                    return Err(WalkmeshError::TooManyFaces(faces.len() + count));
                }
                for index in 0..count {
                    let values = lines
                        .next()
                        .ok_or(WalkmeshError::InvalidFace(index))?
                        .split(';')
                        .next()
                        .unwrap_or("")
                        .split_whitespace()
                        .map(str::parse::<i64>)
                        .collect::<Result<Vec<_>, _>>()
                        .map_err(|_| WalkmeshError::InvalidFace(index))?;
                    if values.len() < 4 {
                        return Err(WalkmeshError::InvalidFace(index));
                    }
                    let vertices = [
                        u32::try_from(values[0])
                            .ok()
                            .and_then(|value| value.checked_add(base as u32))
                            .ok_or(WalkmeshError::InvalidFace(index))?,
                        u32::try_from(values[1])
                            .ok()
                            .and_then(|value| value.checked_add(base as u32))
                            .ok_or(WalkmeshError::InvalidFace(index))?,
                        u32::try_from(values[2])
                            .ok()
                            .and_then(|value| value.checked_add(base as u32))
                            .ok_or(WalkmeshError::InvalidFace(index))?,
                    ];
                    let surface =
                        u32::try_from(values[3]).map_err(|_| WalkmeshError::InvalidFace(index))?;
                    faces.push(WalkmeshFace { vertices, surface });
                }
            }
        }
        if !saw_vertices {
            return Err(WalkmeshError::MissingVertices);
        }
        if !saw_faces {
            return Err(WalkmeshError::MissingFaces);
        }
        let mesh = Self { vertices, faces };
        mesh.validate()?;
        Ok(mesh)
    }

    pub fn validate(&self) -> Result<(), WalkmeshError> {
        if self.vertices.len() > MAX_WALKMESH_VERTICES {
            return Err(WalkmeshError::TooManyVertices(self.vertices.len()));
        }
        if self.faces.len() > MAX_WALKMESH_FACES {
            return Err(WalkmeshError::TooManyFaces(self.faces.len()));
        }
        for (index, vertex) in self.vertices.iter().enumerate() {
            if !vertex.iter().all(|value| value.is_finite()) {
                return Err(WalkmeshError::NonFiniteVertex(index));
            }
        }
        for (index, face) in self.faces.iter().enumerate() {
            if face
                .vertices
                .iter()
                .any(|vertex| *vertex as usize >= self.vertices.len())
            {
                let vertex = face
                    .vertices
                    .iter()
                    .copied()
                    .find(|vertex| *vertex as usize >= self.vertices.len())
                    .unwrap_or(0);
                return Err(WalkmeshError::IndexOutOfRange {
                    face: index,
                    vertex,
                });
            }
            if face.vertices[0] == face.vertices[1]
                || face.vertices[1] == face.vertices[2]
                || face.vertices[0] == face.vertices[2]
            {
                return Err(WalkmeshError::DegenerateFace(index));
            }
            let a = self.vertices[face.vertices[0] as usize];
            let b = self.vertices[face.vertices[1] as usize];
            let c = self.vertices[face.vertices[2] as usize];
            let ab = [b[0] - a[0], b[1] - a[1], b[2] - a[2]];
            let ac = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
            let cross = [
                ab[1] * ac[2] - ab[2] * ac[1],
                ab[2] * ac[0] - ab[0] * ac[2],
                ab[0] * ac[1] - ab[1] * ac[0],
            ];
            if cross[0] * cross[0] + cross[1] * cross[1] + cross[2] * cross[2] <= 1.0e-10 {
                return Err(WalkmeshError::DegenerateFace(index));
            }
        }
        Ok(())
    }

    /// Converts one tile-local walkmesh into the Forge Y-up basis and applies the tile
    /// transform. The NWN source vector `[x, y, z]` becomes `[x, z, -y]`.
    pub fn transformed(
        &self,
        position: [f32; 3],
        rotation: [f32; 4],
    ) -> Result<WalkSurface, WalkmeshError> {
        self.validate()?;
        let rotation = normalize_quaternion(rotation);
        let mut triangles = Vec::with_capacity(self.faces.len());
        let mut min = Vec3::new(f32::INFINITY, f32::INFINITY, f32::INFINITY);
        let mut max = Vec3::new(f32::NEG_INFINITY, f32::NEG_INFINITY, f32::NEG_INFINITY);
        for (index, face) in self.faces.iter().enumerate() {
            let a = self.world_vertex(face.vertices[0], position, rotation);
            let b = self.world_vertex(face.vertices[1], position, rotation);
            let c = self.world_vertex(face.vertices[2], position, rotation);
            if triangle_area_squared(a, b, c) <= 1.0e-10 {
                return Err(WalkmeshError::DegenerateFace(index));
            }
            for point in [a, b, c] {
                min.x = min.x.min(point.x);
                min.y = min.y.min(point.y);
                min.z = min.z.min(point.z);
                max.x = max.x.max(point.x);
                max.y = max.y.max(point.y);
                max.z = max.z.max(point.z);
            }
            triangles.push(WorldTriangle {
                a,
                b,
                c,
                surface: face.surface,
            });
        }
        Ok(WalkSurface {
            triangles,
            min,
            max,
        })
    }

    fn world_vertex(&self, index: u32, position: [f32; 3], rotation: [f32; 4]) -> Vec3 {
        let source = self.vertices[index as usize];
        let canonical = [source[0], source[2], -source[1]];
        let rotated = rotate(rotation, canonical);
        Vec3::new(
            rotated[0] + position[0],
            rotated[1] + position[1],
            rotated[2] + position[2],
        )
    }
}

impl WalkSurface {
    pub fn is_empty(&self) -> bool {
        self.triangles.is_empty()
    }

    pub fn height_at(&self, x: f32, z: f32) -> Option<f32> {
        if !x.is_finite() || !z.is_finite() {
            return None;
        }
        if x < self.min.x || x > self.max.x || z < self.min.z || z > self.max.z {
            return None;
        }
        let point = [x, z];
        for triangle in &self.triangles {
            if !surface_is_walkable(triangle.surface) {
                continue;
            }
            if let Some(weights) = barycentric_xz(
                point,
                [triangle.a.x, triangle.a.z],
                [triangle.b.x, triangle.b.z],
                [triangle.c.x, triangle.c.z],
            ) {
                let y = weights[0] * triangle.a.y
                    + weights[1] * triangle.b.y
                    + weights[2] * triangle.c.y;
                if y.is_finite() {
                    return Some(y);
                }
            }
        }
        None
    }

    pub fn contains_walkable(&self, x: f32, z: f32) -> bool {
        self.height_at(x, z).is_some()
    }

    pub fn merge(&mut self, other: WalkSurface) {
        if self.triangles.is_empty() {
            *self = other;
            return;
        }
        self.min.x = self.min.x.min(other.min.x);
        self.min.y = self.min.y.min(other.min.y);
        self.min.z = self.min.z.min(other.min.z);
        self.max.x = self.max.x.max(other.max.x);
        self.max.y = self.max.y.max(other.max.y);
        self.max.z = self.max.z.max(other.max.z);
        self.triangles.extend(other.triangles);
    }
}

/// NWN surface identifiers that cannot carry a walking creature.
pub fn surface_is_walkable(surface: u32) -> bool {
    !matches!(surface, 0 | 6 | 7 | 8 | 15 | 16 | 17)
}

fn token_count(line: &str, token: &str) -> Option<usize> {
    let mut parts = line.split_whitespace();
    if parts.next()? != token {
        return None;
    }
    parts.next()?.parse().ok()
}

fn normalize_quaternion(rotation: [f32; 4]) -> [f32; 4] {
    let length = (rotation[0] * rotation[0]
        + rotation[1] * rotation[1]
        + rotation[2] * rotation[2]
        + rotation[3] * rotation[3])
        .sqrt();
    if length.is_finite() && length > 1.0e-6 {
        [
            rotation[0] / length,
            rotation[1] / length,
            rotation[2] / length,
            rotation[3] / length,
        ]
    } else {
        [0.0, 0.0, 0.0, 1.0]
    }
}

fn rotate(quaternion: [f32; 4], vector: [f32; 3]) -> [f32; 3] {
    let [x, y, z, w] = quaternion;
    let [vx, vy, vz] = vector;
    let tx = 2.0 * (y * vz - z * vy);
    let ty = 2.0 * (z * vx - x * vz);
    let tz = 2.0 * (x * vy - y * vx);
    [
        vx + w * tx + (y * tz - z * ty),
        vy + w * ty + (z * tx - x * tz),
        vz + w * tz + (x * ty - y * tx),
    ]
}

fn barycentric_xz(point: [f32; 2], a: [f32; 2], b: [f32; 2], c: [f32; 2]) -> Option<[f32; 3]> {
    let denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1]);
    if denominator.abs() <= 1.0e-9 {
        return None;
    }
    let wa = ((b[1] - c[1]) * (point[0] - c[0]) + (c[0] - b[0]) * (point[1] - c[1])) / denominator;
    let wb = ((c[1] - a[1]) * (point[0] - c[0]) + (a[0] - c[0]) * (point[1] - c[1])) / denominator;
    let wc = 1.0 - wa - wb;
    let epsilon = -1.0e-4;
    if wa < epsilon || wb < epsilon || wc < epsilon {
        return None;
    }
    Some([wa, wb, wc])
}

fn triangle_area_squared(a: Vec3, b: Vec3, c: Vec3) -> f32 {
    let ab = [b.x - a.x, b.y - a.y, b.z - a.z];
    let ac = [c.x - a.x, c.y - a.y, c.z - a.z];
    let cross = [
        ab[1] * ac[2] - ab[2] * ac[1],
        ab[2] * ac[0] - ab[0] * ac[2],
        ab[0] * ac[1] - ab[1] * ac[0],
    ];
    cross[0] * cross[0] + cross[1] * cross[1] + cross[2] * cross[2]
}

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE: &[u8] = b"# Exported from NWmax\n#NWmax WALKMESH  ASCII\nbeginwalkmeshgeom tile\nnode aabb walk\nparent tile\nverts 3\n0 0 0\n10 0 0\n0 10 0\nfaces 1\n0 1 2 3 0 0 0 3\nendnode\nendwalkmeshgeom tile\n";

    #[test]
    fn parses_ascii_walkmesh_and_ignores_node_metadata() {
        let mesh = Walkmesh::parse(SAMPLE).expect("walkmesh");
        assert_eq!(mesh.vertices.len(), 3);
        assert_eq!(mesh.faces.len(), 1);
        assert_eq!(mesh.faces[0].surface, 3);
    }

    #[test]
    fn transforms_nwn_z_up_to_forge_y_up() {
        let mesh = Walkmesh::parse(SAMPLE).expect("walkmesh");
        let surface = mesh
            .transformed([5.0, 0.0, -5.0], [0.0, 0.0, 0.0, 1.0])
            .unwrap();
        assert_eq!(surface.min, Vec3::new(5.0, 0.0, -15.0));
        assert_eq!(surface.max, Vec3::new(15.0, 0.0, -5.0));
        assert_eq!(surface.height_at(5.0, -5.0), Some(0.0));
        assert_eq!(surface.height_at(15.0, -5.0), Some(0.0));
        assert_eq!(surface.height_at(0.0, 0.0), None);
    }

    #[test]
    fn refuses_unknown_or_degenerate_meshes() {
        assert_eq!(
            Walkmesh::parse(b"binary data"),
            Err(WalkmeshError::UnsupportedFormat)
        );
        let degenerate =
            b"beginwalkmeshgeom t\nverts 3\n0 0 0\n0 0 0\n0 0 0\nfaces 1\n0 1 2 3 0 0 0 3\nend\n";
        assert!(matches!(
            Walkmesh::parse(degenerate),
            Err(WalkmeshError::DegenerateFace(0))
        ));
    }

    #[test]
    fn surface_classification_blocks_water_and_pits() {
        assert!(surface_is_walkable(1));
        assert!(surface_is_walkable(3));
        assert!(!surface_is_walkable(6));
        assert!(!surface_is_walkable(7));
        assert!(!surface_is_walkable(16));
    }
}
