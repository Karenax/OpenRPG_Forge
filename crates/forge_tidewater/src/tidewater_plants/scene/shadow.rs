//! Masked plant casters from the same validated population shown in Studio.
use super::Vegetation;
use crate::tidewater_object_shadow::{plant_shader, plants};
use bevy::{mesh::VertexAttributeValues, prelude::*};
use forge_tidewater_assets::tidewater_plants::PlantPackage;

pub struct Source {
    groups: Vec<Group>,
    shaders: [String; 2],
    leaf: Vec<u8>,
    detail: Vec<u8>,
}

const SHADOW_DISTANCE_SCALE: f32 = 1.5;

struct Group {
    name: String,
    kind: u8,
    vertices: Vec<u8>,
    indices: Vec<u8>,
    range: Vec4,
    records: Vec<crate::tidewater_plants::Record>,
    models: Vec<Mat4>,
    positions: Vec<Vec4>,
    radii: Vec<f32>,
    query_radius: f64,
    cell_size: f64,
    capacity: usize,
}

impl Source {
    #[allow(
        clippy::too_many_lines,
        reason = "Prepares the verified source shadow resources and vegetation groups together."
    )]
    pub fn from_vegetation(scene: &Vegetation, package: &PlantPackage) -> Result<Self, String> {
        let source = std::str::from_utf8(package.file("plant.wgsl").ok_or("Plant shader absent")?)
            .map_err(|error| error.to_string())?;
        let leaf = package
            .file("leaf.bin")
            .ok_or("Leaf atlas absent")?
            .to_vec();
        let detail = package
            .file("detail.bin")
            .ok_or("Plant detail absent")?
            .to_vec();
        if leaf.len() != 5_592_404 || detail.len() != 512 * 512 * 4 {
            return Err("Plant shadow textures have invalid dimensions".into());
        }
        let mut groups = Vec::new();
        for group in &scene.groups {
            if group.name == "understory" {
                continue;
            }
            let level = group
                .levels
                .iter()
                .find(|level| level.description.near)
                .ok_or("Near plant level absent")?;
            if level.description.kind > 1 {
                return Err("Unsupported near plant shadow material".into());
            }
            let mesh = &level.mesh;
            let count = match mesh.attribute(Mesh::ATTRIBUTE_POSITION) {
                Some(VertexAttributeValues::Float32x3(values)) => values.len(),
                _ => return Err("Plant shadow positions absent".into()),
            };
            let attributes = [
                (Mesh::ATTRIBUTE_POSITION, 3),
                (Mesh::ATTRIBUTE_NORMAL, 3),
                (Mesh::ATTRIBUTE_UV_0, 2),
                (crate::tidewater_plants::VEG, 4),
                (crate::tidewater_plants::MAT, 4),
                (crate::tidewater_plants::LOBE, 4),
            ];
            let mut columns = Vec::with_capacity(attributes.len());
            for (attribute, width) in attributes {
                let values = match (width, mesh.attribute(attribute)) {
                    (2, Some(VertexAttributeValues::Float32x2(values))) => values
                        .iter()
                        .flat_map(|value| value.iter().flat_map(|n| n.to_le_bytes()))
                        .collect::<Vec<_>>(),
                    (3, Some(VertexAttributeValues::Float32x3(values))) => values
                        .iter()
                        .flat_map(|value| value.iter().flat_map(|n| n.to_le_bytes()))
                        .collect::<Vec<_>>(),
                    (4, Some(VertexAttributeValues::Float32x4(values))) => values
                        .iter()
                        .flat_map(|value| value.iter().flat_map(|n| n.to_le_bytes()))
                        .collect::<Vec<_>>(),
                    _ => return Err("Plant shadow attribute layout changed".into()),
                };
                if values.len() != count * width * 4 {
                    return Err("Plant shadow attribute count changed".into());
                }
                columns.push((values, width * 4));
            }
            let mut vertices = Vec::with_capacity(count * 80);
            for index in 0..count {
                for (column, stride) in &columns {
                    vertices.extend_from_slice(&column[index * stride..(index + 1) * stride]);
                }
            }
            let indices = mesh
                .indices()
                .ok_or("Plant shadow indices absent")?
                .iter()
                .map(|index| {
                    u32::try_from(index)
                        .map(u32::to_le_bytes)
                        .map_err(|error| error.to_string())
                })
                .collect::<Result<Vec<_>, _>>()?
                .into_iter()
                .flatten()
                .collect();
            groups.push(Group {
                name: group.name.clone(),
                kind: level.description.kind,
                vertices,
                indices,
                range: Vec4::new(
                    0.0,
                    level.description.range[0] * SHADOW_DISTANCE_SCALE,
                    level.description.range[1] * SHADOW_DISTANCE_SCALE,
                    level.description.range[2] * SHADOW_DISTANCE_SCALE,
                ),
                records: group.records.clone(),
                models: group.models.clone(),
                positions: group.records.iter().map(|record| record.i_pos).collect(),
                radii: group
                    .query_radii
                    .iter()
                    .map(|r| r * SHADOW_DISTANCE_SCALE.powi(2))
                    .collect(),
                query_radius: group.query_radius * f64::from(SHADOW_DISTANCE_SCALE),
                cell_size: group.cell_size,
                capacity: level
                    .description
                    .capacity
                    .saturating_mul(9)
                    .div_ceil(4)
                    .min(65536),
            });
        }
        if groups.len() != 5 {
            return Err("Incomplete plant shadow groups".into());
        }
        Ok(Self {
            groups,
            shaders: [
                plant_shader::make(source, 0)?.clone(),
                plant_shader::make(source, 1)?.clone(),
            ],
            leaf,
            detail,
        })
    }

    pub fn select(&self, camera: Vec4) -> Result<Vec<plants::Stream>, String> {
        self.select_observed(camera).map(|(streams, _)| streams)
    }

    /// Observe the exact CPU selection used for upload, not GPU depth coverage.
    pub fn select_observed(
        &self,
        camera: Vec4,
    ) -> Result<(Vec<plants::Stream>, Vec<SelectionObservation>), String> {
        if !camera.is_finite() {
            return Err("Invalid plant shadow camera".into());
        }
        let mut observations = Vec::with_capacity(self.groups.len());
        let streams = self
            .groups
            .iter()
            .map(|group| {
                let mut ids = selected(group, camera.truncate());
                let candidates = ids.len();
                ids.truncate(group.capacity);
                let distances = ids.iter().map(|&index| {
                    group.positions[index]
                        .truncate()
                        .distance(camera.truncate())
                });
                observations.push(SelectionObservation {
                    group: group.name.clone(),
                    candidates,
                    submitted: ids.len(),
                    capacity_dropped: candidates - ids.len(),
                    lod_range: group.range.yzw().to_array(),
                    max_base_distance: distances.clone().reduce(f32::max),
                    submitted_beyond_lod_end: distances.filter(|d| *d >= group.range.w).count(),
                });
                plants::Stream {
                    records: ids
                        .iter()
                        .flat_map(|&index| {
                            [group.records[index].i_pos, group.records[index].i_dat]
                                .into_iter()
                                .flat_map(|vector| {
                                    vector.to_array().into_iter().flat_map(f32::to_le_bytes)
                                })
                        })
                        .collect(),
                    models: ids
                        .iter()
                        .flat_map(|&index| {
                            group.models[index]
                                .to_cols_array()
                                .into_iter()
                                .flat_map(f32::to_le_bytes)
                        })
                        .collect(),
                    count: u32::try_from(ids.len()).expect("validated plant capacity"),
                }
            })
            .collect();
        Ok((streams, observations))
    }

    pub fn at(&self, camera: Vec4) -> Result<plants::Input, String> {
        let streams = self.select(camera)?;
        let batches = self
            .groups
            .iter()
            .zip(streams)
            .map(|(group, stream)| plants::Batch {
                kind: group.kind,
                vertices: group.vertices.clone(),
                indices: group.indices.clone(),
                records: stream.records,
                models: stream.models,
                count: stream.count,
                range: group.range,
            })
            .collect();
        Ok(plants::Input {
            batches,
            shaders: self.shaders.clone(),
            leaf: self.leaf.clone(),
            detail: self.detail.clone(),
            size: 1024,
            mips: 11,
            camera,
            initial_motion: None,
        })
    }
}

#[derive(Clone, Debug, serde::Serialize)]
pub struct SelectionObservation {
    pub group: String,
    pub candidates: usize,
    pub submitted: usize,
    pub capacity_dropped: usize,
    pub lod_range: [f32; 3],
    pub max_base_distance: Option<f32>,
    /// Geometric observation only: source dither/deformation may use a transition band.
    pub submitted_beyond_lod_end: usize,
}

// Shadow selection extends the visible near-source range, including edited trees.
// Per-record radii are squared distances; they scale by the square of the distance factor.
fn selected(group: &Group, camera: Vec3) -> Vec<usize> {
    let x = f64::from(camera.x);
    let z = f64::from(camera.z);
    let radius = group.query_radius;
    let cell = group.cell_size;
    let distance = |index: usize| {
        let p = group.positions[index];
        (f64::from(p.x) - x).powi(2) + (f64::from(p.z) - z).powi(2)
    };
    let mut ids: Vec<_> = (0..group.records.len())
        .filter(|&index| {
            let p = group.positions[index];
            let px = f64::from(p.x);
            let pz = f64::from(p.z);
            let cell_x = (px / cell).floor();
            let cell_z = (pz / cell).floor();
            let r = if group.radii[index] > 0.0 {
                f64::from(group.radii[index])
            } else {
                radius * radius
            };
            cell_x >= ((x - radius) / cell).floor()
                && cell_x <= ((x + radius) / cell).floor()
                && cell_z >= ((z - radius) / cell).floor()
                && cell_z <= ((z + radius) / cell).floor()
                && distance(index) < r
        })
        .collect();
    ids.sort_by(|&a, &b| {
        let pa = group.positions[a];
        let pb = group.positions[b];
        (f64::from(pa.z) / cell)
            .floor()
            .total_cmp(&(f64::from(pb.z) / cell).floor())
            .then_with(|| {
                (f64::from(pa.x) / cell)
                    .floor()
                    .total_cmp(&(f64::from(pb.x) / cell).floor())
            })
    });
    #[allow(
        clippy::cast_possible_truncation,
        reason = "Preserves source float32 distance ordering for the bounded nearby caster selection."
    )]
    ids.sort_by(|&a, &b| (distance(a) as f32).total_cmp(&(distance(b) as f32)));
    ids
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn edited_near_tree_is_selected_as_a_shadow_caster() {
        let positions = vec![
            Vec4::new(4.0, 0.0, 2.0, 1.0),
            Vec4::new(400.0, 0.0, 2.0, 1.0),
        ];
        let group = Group {
            name: "edited-tree".into(),
            kind: 0,
            vertices: Vec::new(),
            indices: Vec::new(),
            range: Vec4::ZERO,
            records: positions
                .iter()
                .map(|&position| crate::tidewater_plants::Record {
                    i_pos: position,
                    i_dat: Vec4::ZERO,
                })
                .collect(),
            models: vec![Mat4::IDENTITY; 2],
            positions,
            radii: vec![0.0; 2],
            query_radius: 50.0,
            cell_size: 32.0,
            capacity: 2,
        };
        assert_eq!(selected(&group, Vec3::ZERO), vec![0]);
        assert_eq!(
            selected(&group, Vec3::new(54.0, 0.0, 2.0)),
            Vec::<usize>::new()
        );
        assert_eq!(selected(&group, Vec3::new(400.0, 0.0, 0.0)), vec![1]);
    }

    #[test]
    fn stream_selection_respects_the_radius_threshold_and_matches_render_input() {
        let positions = vec![
            Vec4::new(3.0, 0.0, 0.0, 1.0),
            Vec4::new(5.0, 0.0, 0.0, 1.0),
            Vec4::new(1.0, 0.0, 0.0, 1.0),
        ];
        let records = positions
            .iter()
            .zip(0_u16..)
            .map(|(&i_pos, index)| crate::tidewater_plants::Record {
                i_pos,
                i_dat: Vec4::splat(f32::from(index)),
            })
            .collect();
        let models = vec![
            Mat4::from_translation(Vec3::X),
            Mat4::from_translation(Vec3::Y),
            Mat4::from_translation(Vec3::Z),
        ];
        let mut source = Source {
            groups: vec![Group {
                name: "test-tree".into(),
                kind: 0,
                vertices: vec![1],
                indices: vec![2],
                range: Vec4::ZERO,
                records,
                models,
                positions,
                radii: vec![25.0; 3],
                query_radius: 50.0,
                cell_size: 32.0,
                capacity: 2,
            }],
            shaders: ["first".into(), "second".into()],
            leaf: vec![3],
            detail: vec![4],
        };

        let stream = source.select(Vec4::ZERO).unwrap().remove(0);
        assert_eq!(stream.count, 2);
        let expected_records = [2, 0]
            .into_iter()
            .flat_map(|index| {
                [
                    source.groups[0].records[index].i_pos,
                    source.groups[0].records[index].i_dat,
                ]
            })
            .flat_map(|value| value.to_array().into_iter().flat_map(f32::to_le_bytes))
            .collect::<Vec<_>>();
        let expected_models = [2, 0]
            .into_iter()
            .flat_map(|index| {
                source.groups[0].models[index]
                    .to_cols_array()
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
            })
            .collect::<Vec<_>>();
        assert_eq!(stream.records, expected_records);
        assert_eq!(stream.models, expected_models);

        let input = source.at(Vec4::ZERO).unwrap();
        assert_eq!(input.batches.len(), 1);
        assert_eq!(input.batches[0].count, stream.count);
        assert_eq!(input.batches[0].records, stream.records);
        assert_eq!(input.batches[0].models, stream.models);

        let (observed_streams, observations) = source.select_observed(Vec4::ZERO).unwrap();
        assert_eq!(observed_streams[0].records, stream.records);
        assert_eq!(observations[0].candidates, 2); // Radius boundary excluded.
        assert_eq!(observations[0].submitted, 2);
        assert_eq!(observations[0].capacity_dropped, 0);
        assert_eq!(observations[0].max_base_distance, Some(3.0));
        source.groups[0].capacity = 1;
        let (bounded_streams, observations) = source.select_observed(Vec4::ZERO).unwrap();
        assert_eq!(bounded_streams[0].count, 1);
        assert_eq!(observations[0].submitted, 1);
        assert_eq!(observations[0].capacity_dropped, 1);
        assert_eq!(observations[0].max_base_distance, Some(1.0));
        assert!(source.select_observed(Vec4::splat(f32::NAN)).is_err());
    }
}
