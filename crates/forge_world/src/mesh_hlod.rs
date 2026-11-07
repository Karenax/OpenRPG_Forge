//! Bounded derived mesh reduction. Representatives are original vertices, so a
//! render adapter can retain every source attribute without inventing new data.
use std::cell::Cell;
use std::collections::{BTreeMap, BTreeSet};

#[cfg(test)]
mod tests;

const MAX_VERTICES: usize = 250_000;
const MAX_TRIANGLES: usize = 500_000;
const MAX_COMPONENTS: usize = 16;
const MAX_TRIANGLE_CHECKS: usize = 8_000_000;
const MAX_COLLAPSE_INCIDENT_FACES: usize = 128;
const MIN_FACE_ALIGNMENT: f64 = 0.5;

/// Component-wise attribute error, in the original attribute's units. Discrete
/// attributes and seams belong in `domains`, which must match exactly.
pub struct ErrorChannel<'a> {
    pub values: &'a [f32],
    pub components: usize,
    pub maximum_delta: f32,
}

pub struct MeshSource<'a> {
    pub positions_m: &'a [[f32; 3]],
    pub indices: &'a [u32],
    /// Adapter-interned exact signatures; distinct signatures never collapse.
    pub domains: &'a [u32],
    pub channels: &'a [ErrorChannel<'a>],
}

#[derive(Debug, PartialEq)]
pub struct ReducedMesh {
    /// Original source vertices, in increasing source-index order.
    pub retained_vertices: Vec<u32>,
    /// Each referenced source vertex maps to a retained output vertex. `None`
    /// is allowed only for vertices unused by every source triangle.
    pub source_to_output_vertex: Vec<Option<u32>>,
    pub indices: Vec<u32>,
    pub maximum_position_error_m: f64,
    pub maximum_channel_deltas: Vec<f64>,
    /// Cosine of the largest surviving-face rotation from its original normal.
    pub minimum_face_alignment: f64,
    pub source_triangles: usize,
    pub removed_triangles: usize,
    pub triangle_checks: usize,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ReductionError {
    InvalidSource,
    InvalidErrorBound,
    BudgetExceeded,
    EmptySurface,
    LostSurfaceSupport,
}

impl std::fmt::Display for ReductionError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "mesh HLOD reduction: {self:?}")
    }
}
impl std::error::Error for ReductionError {}

struct Solver<'a> {
    source: MeshSource<'a>,
    parents: Vec<Cell<usize>>,
    buckets: Vec<[i64; 3]>,
    incident: Vec<Vec<usize>>,
    lower: Vec<f32>,
    upper: Vec<f32>,
    components: usize,
    limits: Vec<f64>,
    component_ids: Vec<usize>,
    component_faces: Vec<usize>,
    triangle_checks: usize,
}

/// Edge-connected clustering with a spatial error bound. Collapses preserve
/// exact domains, bounded floating attributes, winding, manifold edge links and
/// at least one triangle per original connected component. It does not replace
/// the collision mesh, bake materials, or promise a target reduction ratio.
pub fn reduce(source: MeshSource<'_>, maximum_error_m: f64) -> Result<ReducedMesh, ReductionError> {
    validate(&source, maximum_error_m)?;
    let n = source.positions_m.len();
    let components = source
        .channels
        .iter()
        .map(|channel| channel.components)
        .sum();
    let mut values = Vec::with_capacity(n * components);
    for vertex in 0..n {
        for channel in source.channels {
            values.extend_from_slice(
                &channel.values[vertex * channel.components..(vertex + 1) * channel.components],
            );
        }
    }
    let mut incident = vec![Vec::new(); n];
    let connectivity: Vec<_> = (0..n).map(Cell::new).collect();
    for (face, triangle) in source.indices.as_chunks::<3>().0.iter().enumerate() {
        for &vertex in triangle {
            incident[vertex as usize].push(face);
        }
        for edge in 0..3 {
            let a = root(&connectivity, triangle[edge] as usize);
            let b = root(&connectivity, triangle[(edge + 1) % 3] as usize);
            connectivity[a.max(b)].set(a.min(b));
        }
    }
    let component_ids: Vec<_> = (0..n).map(|vertex| root(&connectivity, vertex)).collect();
    let mut component_faces = vec![0; n];
    for triangle in source.indices.as_chunks::<3>().0 {
        if nondegenerate(source.positions_m, triangle) {
            component_faces[component_ids[triangle[0] as usize]] += 1;
        }
    }
    let minimum = (0..3)
        .map(|axis| {
            source
                .positions_m
                .iter()
                .map(|p| f64::from(p[axis]))
                .fold(f64::INFINITY, f64::min)
        })
        .collect::<Vec<_>>();
    let step = maximum_error_m / 3_f64.sqrt();
    let buckets = source
        .positions_m
        .iter()
        .map(|position| {
            // Validated finite f32 positions within +/-1e6 and error >=1e-4.
            #[allow(clippy::cast_possible_truncation)]
            std::array::from_fn(|axis| {
                ((f64::from(position[axis]) - minimum[axis]) / step).floor() as i64
            })
        })
        .collect();
    let limits = source
        .channels
        .iter()
        .flat_map(|channel| {
            std::iter::repeat_n(f64::from(channel.maximum_delta), channel.components)
        })
        .collect();
    let mut solver = Solver {
        source,
        parents: (0..n).map(Cell::new).collect(),
        buckets,
        incident,
        lower: values.clone(),
        upper: values,
        components,
        limits,
        component_ids,
        component_faces,
        triangle_checks: 0,
    };
    // Input order is part of the pinned geometry; no hash iteration or randomness.
    for face in 0..solver.source.indices.len() / 3 {
        let triangle = solver.source.indices.as_chunks::<3>().0[face];
        for edge in 0..3 {
            solver.try_collapse(triangle[edge] as usize, triangle[(edge + 1) % 3] as usize)?;
        }
    }
    solver.finish(maximum_error_m)
}

fn validate(source: &MeshSource<'_>, error: f64) -> Result<(), ReductionError> {
    let n = source.positions_m.len();
    if !error.is_finite() || !(0.0001..=16.0).contains(&error) {
        return Err(ReductionError::InvalidErrorBound);
    }
    if n > MAX_VERTICES
        || source.indices.len() / 3 > MAX_TRIANGLES
        || source.channels.len() > MAX_COMPONENTS
    {
        return Err(ReductionError::BudgetExceeded);
    }
    if source
        .channels
        .iter()
        .any(|channel| channel.components == 0 || channel.components > MAX_COMPONENTS)
    {
        return Err(ReductionError::InvalidSource);
    }
    if source
        .channels
        .iter()
        .map(|channel| channel.components)
        .sum::<usize>()
        > MAX_COMPONENTS
    {
        return Err(ReductionError::BudgetExceeded);
    }
    if n == 0
        || source.indices.is_empty()
        || !source.indices.len().is_multiple_of(3)
        || source.domains.len() != n
        || source.indices.iter().any(|&index| index as usize >= n)
        || source
            .positions_m
            .iter()
            .flatten()
            .any(|value| !value.is_finite() || value.abs() > 1_000_000.0)
        || source.channels.iter().any(|channel| {
            channel.components == 0
                || channel.components > MAX_COMPONENTS
                || channel.values.len() != n * channel.components
                || !channel.maximum_delta.is_finite()
                || channel.maximum_delta < 0.0
                || channel.values.iter().any(|value| !value.is_finite())
        })
    {
        return Err(ReductionError::InvalidSource);
    }
    if source
        .indices
        .as_chunks::<3>()
        .0
        .iter()
        .any(|triangle| !nondegenerate(source.positions_m, triangle))
    {
        return Err(ReductionError::InvalidSource);
    }
    Ok(())
}

fn root(parents: &[Cell<usize>], mut vertex: usize) -> usize {
    while parents[vertex].get() != vertex {
        let parent = parents[vertex].get();
        parents[vertex].set(parents[parent].get());
        vertex = parent;
    }
    vertex
}

fn difference(a: [f32; 3], b: [f32; 3]) -> [f64; 3] {
    std::array::from_fn(|axis| f64::from(a[axis]) - f64::from(b[axis]))
}
fn cross(a: [f64; 3], b: [f64; 3]) -> [f64; 3] {
    [
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    ]
}
fn normal(positions: &[[f32; 3]], triangle: &[u32; 3]) -> [f64; 3] {
    let [a, b, c] = triangle.map(|vertex| positions[vertex as usize]);
    cross(difference(b, a), difference(c, a))
}
fn nondegenerate(positions: &[[f32; 3]], triangle: &[u32; 3]) -> bool {
    normal(positions, triangle)
        .iter()
        .any(|value| *value != 0.0)
}

fn face_alignment(old: [f64; 3], next: [f64; 3]) -> f64 {
    let length = |value: [f64; 3]| value[0].hypot(value[1]).hypot(value[2]);
    old.iter().zip(next).map(|(a, b)| a * b).sum::<f64>() / (length(old) * length(next))
}

impl Solver<'_> {
    fn face(&self, face: usize) -> [u32; 3] {
        self.source.indices.as_chunks::<3>().0[face].map(|vertex| {
            u32::try_from(root(&self.parents, vertex as usize))
                .expect("source vertices bounded to 250000")
        })
    }

    fn try_collapse(&mut self, first: usize, second: usize) -> Result<(), ReductionError> {
        let first = root(&self.parents, first);
        let second = root(&self.parents, second);
        let (keep, remove) = (first.min(second), first.max(second));
        if keep == remove
            || self.buckets[keep] != self.buckets[remove]
            || self.source.domains[keep] != self.source.domains[remove]
        {
            return Ok(());
        }
        if !self.attributes_fit(keep, remove) {
            return Ok(());
        }
        // A high-valence fan keeps its current detail instead of allowing one
        // collapse to monopolize the bounded worker. No target ratio is promised.
        if self.incident[keep].len() + self.incident[remove].len() > MAX_COLLAPSE_INCIDENT_FACES {
            return Ok(());
        }
        let mut faces = self.incident[keep].clone();
        faces.extend_from_slice(&self.incident[remove]);
        faces.sort_unstable();
        faces.dedup();
        self.triangle_checks = self
            .triangle_checks
            .checked_add(faces.len())
            .ok_or(ReductionError::BudgetExceeded)?;
        if self.triangle_checks > MAX_TRIANGLE_CHECKS {
            return Err(ReductionError::BudgetExceeded);
        }
        let Some(removed_faces) = self.topology_allows(keep, remove, &faces) else {
            return Ok(());
        };
        let component = self.component_ids[keep];
        if removed_faces >= self.component_faces[component] {
            return Ok(());
        }
        self.component_faces[component] -= removed_faces;
        self.parents[remove].set(keep);
        self.incident[keep] = faces
            .into_iter()
            .filter(|&face| nondegenerate(self.source.positions_m, &self.face(face)))
            .collect();
        self.incident[remove] = Vec::new();
        for component in 0..self.components {
            let a = keep * self.components + component;
            let b = remove * self.components + component;
            self.lower[a] = self.lower[a].min(self.lower[b]);
            self.upper[a] = self.upper[a].max(self.upper[b]);
        }
        Ok(())
    }

    fn attributes_fit(&self, keep: usize, remove: usize) -> bool {
        (0..self.components).all(|component| {
            let a = keep * self.components + component;
            let b = remove * self.components + component;
            let value = self.attribute(keep, component);
            (value - f64::from(self.lower[a].min(self.lower[b]))).abs() <= self.limits[component]
                && (value - f64::from(self.upper[a].max(self.upper[b]))).abs()
                    <= self.limits[component]
        })
    }

    fn attribute(&self, vertex: usize, mut component: usize) -> f64 {
        for channel in self.source.channels {
            if component < channel.components {
                return f64::from(channel.values[vertex * channel.components + component]);
            }
            component -= channel.components;
        }
        unreachable!("component count validated from channels")
    }

    fn topology_allows(&self, keep: usize, remove: usize, faces: &[usize]) -> Option<usize> {
        let mut neighbours = [BTreeSet::new(), BTreeSet::new()];
        let mut opposite = BTreeSet::new();
        let mut opposite_faces = BTreeMap::<usize, usize>::new();
        let mut surviving = BTreeMap::new();
        let mut shared_faces = 0;
        let mut removed_faces = 0;
        let mut edge_counts = [BTreeMap::<usize, usize>::new(), BTreeMap::new()];
        for &face in faces {
            let before = self.face(face);
            if !nondegenerate(self.source.positions_m, &before) {
                continue;
            }
            let ids = before.map(|value| value as usize);
            for (index, vertex) in [keep, remove].into_iter().enumerate() {
                if ids.contains(&vertex) {
                    neighbours[index]
                        .extend(ids.into_iter().filter(|&id| id != keep && id != remove));
                    for neighbour in ids.into_iter().filter(|&id| id != vertex) {
                        *edge_counts[index].entry(neighbour).or_default() += 1;
                    }
                }
            }
            if ids.contains(&keep) && ids.contains(&remove) {
                shared_faces += 1;
                opposite.extend(ids.into_iter().filter(|&id| id != keep && id != remove));
                for vertex in ids.into_iter().filter(|&id| id != keep && id != remove) {
                    *opposite_faces.entry(vertex).or_default() += 1;
                }
            }
            let after = before.map(|value| {
                if value as usize == remove {
                    u32::try_from(keep).expect("bounded index")
                } else {
                    value
                }
            });
            if after[0] == after[1] || after[1] == after[2] || after[2] == after[0] {
                removed_faces += 1;
                continue;
            }
            let mut triangle_key = after;
            triangle_key.sort_unstable();
            let mut original = before;
            original.sort_unstable();
            // Do not flatten a closed minimal component into coincident sheets.
            if surviving
                .insert(triangle_key, original)
                .is_some_and(|prior| prior != original)
            {
                return None;
            }
            let old = normal(
                self.source.positions_m,
                &self.source.indices.as_chunks::<3>().0[face],
            );
            let next = normal(self.source.positions_m, &after);
            // A merely positive dot product permits near-tangent foldovers on
            // curved surfaces. Keep every surviving normal within sixty degrees
            // of its original direction, in addition to the metric error bound.
            let alignment = face_alignment(old, next);
            if !alignment.is_finite() || alignment < MIN_FACE_ALIGNMENT {
                return None;
            }
        }
        if !(1..=2).contains(&shared_faces)
            || neighbours[0]
                .intersection(&neighbours[1])
                .copied()
                .collect::<BTreeSet<_>>()
                != opposite
        {
            return None;
        }
        if shared_faces == 2
            && edge_counts
                .iter()
                .all(|edges| edges.values().any(|&count| count == 1))
        {
            return None;
        }
        // Every collapsed triangle maps onto a surviving support edge. Without
        // this, a short base edge could erase an arbitrarily long thin appendage
        // while reporting a small displacement of the two contracted vertices.
        if opposite_faces.iter().any(|(vertex, &faces)| {
            edge_counts[0].get(vertex).copied().unwrap_or(0)
                + edge_counts[1].get(vertex).copied().unwrap_or(0)
                <= 2 * faces
        }) {
            return None;
        }
        Some(removed_faces)
    }

    fn finish(self, maximum_error_m: f64) -> Result<ReducedMesh, ReductionError> {
        let mut triangles = BTreeSet::new();
        let mut minimum_face_alignment = 1_f64;
        for face in 0..self.source.indices.len() / 3 {
            let triangle = self.face(face);
            if !nondegenerate(self.source.positions_m, &triangle) {
                continue;
            }
            let alignment = face_alignment(
                normal(
                    self.source.positions_m,
                    &self.source.indices.as_chunks::<3>().0[face],
                ),
                normal(self.source.positions_m, &triangle),
            );
            if !alignment.is_finite() || alignment < MIN_FACE_ALIGNMENT {
                return Err(ReductionError::LostSurfaceSupport);
            }
            minimum_face_alignment = minimum_face_alignment.min(alignment);
            let minimum = (0..3).min_by_key(|&i| triangle[i]).expect("three indices");
            triangles.insert([
                triangle[minimum],
                triangle[(minimum + 1) % 3],
                triangle[(minimum + 2) % 3],
            ]);
        }
        if triangles.is_empty() {
            return Err(ReductionError::EmptySurface);
        }
        let used: BTreeSet<_> = triangles.iter().flatten().copied().collect();
        let retained_vertices: Vec<_> = used.into_iter().collect();
        let remap: BTreeMap<_, _> = retained_vertices.iter().copied().zip(0_u32..).collect();
        let source_to_output_vertex: Vec<_> = (0..self.parents.len())
            .map(|vertex| {
                let representative =
                    u32::try_from(root(&self.parents, vertex)).expect("bounded source vertex");
                remap.get(&representative).copied()
            })
            .collect();
        if self
            .source
            .indices
            .iter()
            .any(|&vertex| source_to_output_vertex[vertex as usize].is_none())
        {
            return Err(ReductionError::LostSurfaceSupport);
        }
        let indices: Vec<_> = triangles
            .into_iter()
            .flatten()
            .map(|index| remap[&index])
            .collect();
        let mut position_error = 0_f64;
        let mut deltas = vec![0_f64; self.source.channels.len()];
        for vertex in 0..self.parents.len() {
            let representative = root(&self.parents, vertex);
            let delta = difference(
                self.source.positions_m[vertex],
                self.source.positions_m[representative],
            );
            position_error =
                position_error.max(delta.into_iter().map(|v| v * v).sum::<f64>().sqrt());
            for (i, channel) in self.source.channels.iter().enumerate() {
                for component in 0..channel.components {
                    let difference =
                        f64::from(channel.values[vertex * channel.components + component])
                            - f64::from(
                                channel.values[representative * channel.components + component],
                            );
                    deltas[i] = deltas[i].max(difference.abs());
                }
            }
        }
        if position_error > maximum_error_m {
            return Err(ReductionError::InvalidErrorBound);
        }
        Ok(ReducedMesh {
            removed_triangles: self.source.indices.len() / 3 - indices.len() / 3,
            source_triangles: self.source.indices.len() / 3,
            retained_vertices,
            source_to_output_vertex,
            indices,
            maximum_position_error_m: position_error,
            maximum_channel_deltas: deltas,
            minimum_face_alignment,
            triangle_checks: self.triangle_checks,
        })
    }
}
