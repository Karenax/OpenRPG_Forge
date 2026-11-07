// SPDX-License-Identifier: MIT
// Adapted from dgreenheck/tidewater@1438b1ab, copyright 2026 DRG Software Solutions LLC.
//! Pinned Tidewater ShoreField.js propagation, preserving f64 arithmetic and f32 stores.
#![allow(
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss,
    clippy::many_single_char_names
)] // Source equation names and explicit Float32Array stores.
const SIDE: usize = 512;

/// Square world-space frame and resolution of a shore field.
///
/// A rectangular terrain can use an enclosing square without stretching either
/// axis. Consumers must sample the result with this same frame. Admitting this
/// field alone does not admit the ocean or shore shaders for another terrain.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Domain {
    origin: [f64; 2],
    extent_m: f64,
    water_level_m: f64,
    side: usize,
}

impl Domain {
    /// The frame used by the original source profile.
    pub const REFERENCE: Self = Self {
        origin: [-1024.0, -1024.0],
        extent_m: 2048.0,
        water_level_m: 0.0,
        side: SIDE,
    };

    /// Validate a square frame and the water level used to calculate depth.
    /// This compatibility constructor retains the original 512 × 512 grid.
    pub fn new(origin: [f64; 2], extent_m: f64, water_level_m: f64) -> Result<Self, &'static str> {
        Self::with_side(origin, extent_m, water_level_m, SIDE)
    }

    /// Enclose an admitted sampled terrain without reducing the source's
    /// four-metre shoreline detail. The existing sampled-terrain axis limit
    /// bounds the generated field instead of silently coarsening large maps.
    pub fn for_terrain(
        document: &forge_project::TerrainDocument,
        water_level_m: f64,
    ) -> Result<Self, String> {
        document.validate().map_err(|error| error.to_string())?;
        if document.sampled_source.is_none() {
            return Err("Shore field requires an admitted sampled terrain".into());
        }
        let extent_m = f64::from(document.extent_m.size_x_m.max(document.extent_m.size_z_m));
        let origin = [-extent_m * 0.5; 2];
        let maximum_side =
            usize::try_from(forge_project::sampled_terrain::MAX_SAMPLED_TERRAIN_AXIS)
                .map_err(|_| "Shore-field axis does not fit this platform")?;
        let maximum_cells = maximum_side
            .checked_mul(maximum_side)
            .ok_or("Shore-field budget overflow")?;
        Self::preserving_spacing(origin, extent_m, water_level_m, 4.0, maximum_cells)
            .map_err(str::to_owned)
    }

    /// Choose enough cells to retain the requested world-space detail.
    /// The caller must supply an explicit cell budget; exceeding it is an error
    /// rather than silently coarsening the field. GPU admission is separate.
    #[allow(
        clippy::cast_sign_loss,
        reason = "The finite positive side is checked before conversion"
    )]
    pub fn preserving_spacing(
        origin: [f64; 2],
        extent_m: f64,
        water_level_m: f64,
        maximum_cell_m: f64,
        maximum_cells: usize,
    ) -> Result<Self, &'static str> {
        if !extent_m.is_finite()
            || extent_m <= 0.0
            || !maximum_cell_m.is_finite()
            || maximum_cell_m <= 0.0
        {
            return Err("Invalid shore-field spacing");
        }
        let required = (extent_m / maximum_cell_m).ceil().max(2.0);
        if !required.is_finite() || required > f64::from(u32::MAX) {
            return Err("Shore-field resolution exceeds its budget");
        }
        let mut side = required as usize;
        if extent_m / side as f64 > maximum_cell_m {
            side = side
                .checked_add(1)
                .ok_or("Shore-field resolution overflow")?;
        }
        let cells = side
            .checked_mul(side)
            .ok_or("Shore-field resolution overflow")?;
        if cells > maximum_cells {
            return Err("Shore-field resolution exceeds its budget");
        }
        Self::with_side(origin, extent_m, water_level_m, side)
    }

    /// Grid side length; all four channels use this exact square frame.
    #[must_use]
    pub const fn side(self) -> usize {
        self.side
    }

    /// Apply the same terrain, hill-shadow and shoreline coordinates to each
    /// water consumer, using the domain that actually produced its field.
    pub fn prepare_water_shader(
        self,
        source: &str,
        document: &forge_project::TerrainDocument,
    ) -> Result<String, String> {
        if self != Self::for_terrain(document, self.water_level_m)? {
            return Err("Water shader domain differs from the admitted terrain".into());
        }
        let source = crate::source_terrain::sampling::prepare_shader(source, document)?;
        let source = crate::source_terrain::sampling::prepare_surface_shader(&source, document)?;
        let source =
            crate::source_terrain::sampling::prepare_water_hill_shadow_shader(&source, document)?;
        self.prepare_shader(&source)
    }

    /// Adapt the existing shoreline lookup to this field's admitted frame.
    /// Reference coordinates retain the source program byte for byte; this
    /// does not admit textures, a renderer, or the other water programs.
    #[expect(
        clippy::float_cmp,
        reason = "Reference identity requires exact source-frame values"
    )]
    pub fn prepare_shader(self, source: &str) -> Result<String, String> {
        const MARKER: &str = "fn terrainShoreSample(";
        const RESOLUTION: &str = "let res = terrainParams.shoreRes;";
        const COORDINATES: &str =
            "let f = ( xz - terrainParams.origin ) / terrainParams.size * res - 0.5;";
        if source.trim().is_empty()
            || source.len() > 2_000_000
            || source.matches(MARKER).count() != 1
        {
            return Err("Expected one bounded shore-field shader".into());
        }
        let start = source.find(MARKER).expect("one shoreline evaluator");
        let open = source[start..]
            .find('{')
            .map(|index| start + index)
            .ok_or("Shoreline evaluator has no body")?;
        let signature: String = source[start..open]
            .chars()
            .filter(|character| !character.is_whitespace())
            .collect();
        if signature != "fnterrainShoreSample(xz:vec2f)->vec4f" {
            return Err("Unsupported shoreline evaluator signature".into());
        }
        let end = crate::source_terrain::sampling::function_end(source.as_bytes(), open)
            .map_err(|_| "Shoreline evaluator has an unclosed body or comment")?;
        let function = &source[start..end];
        if function.matches(RESOLUTION).count() != 1 || function.matches(COORDINATES).count() != 1 {
            return Err("Unsupported shoreline sampling coordinates".into());
        }
        if self.origin == Self::REFERENCE.origin
            && self.extent_m == Self::REFERENCE.extent_m
            && self.side == Self::REFERENCE.side
        {
            return Ok(source.to_owned());
        }
        if f64::from(self.side as f32) != self.side as f64 {
            return Err("Shoreline resolution exceeds shader precision".into());
        }
        let origin = self.origin.map(|axis| axis as f32);
        let extent = self.extent_m as f32;
        let resolution = self.side as f32;
        let adapted = function
            .replacen(RESOLUTION, &format!("let res = {resolution:?};"), 1)
            .replacen(
                COORDINATES,
                &format!(
                    "let f = ( xz - vec2f({:?}, {:?}) ) / {extent:?} * res - 0.5;",
                    origin[0], origin[1]
                ),
                1,
            );
        Ok(format!("{}{adapted}{}", &source[..start], &source[end..]))
    }

    fn with_side(
        origin: [f64; 2],
        extent_m: f64,
        water_level_m: f64,
        side: usize,
    ) -> Result<Self, &'static str> {
        let cells = side
            .checked_mul(side)
            .ok_or("Shore-field resolution overflow")?;
        let output_bytes = cells
            .checked_mul(4 * std::mem::size_of::<f32>())
            .ok_or("Shore-field resolution overflow")?;
        if side < 2 || isize::try_from(output_bytes).is_err() {
            return Err("Invalid shore-field resolution");
        }
        let cell = extent_m / side as f64;
        let renderer_cell = cell as f32;
        if origin.iter().any(|value| !value.is_finite())
            || !extent_m.is_finite()
            || extent_m <= 0.0
            || !cell.is_normal()
            || !(1.0 / cell).is_normal()
            || !renderer_cell.is_normal()
            || !(1.0_f32 / renderer_cell).is_normal()
            || !(extent_m as f32).is_finite()
            || !water_level_m.is_finite()
            || !(water_level_m as f32).is_finite()
            || origin.iter().any(|value| {
                let end = value + extent_m;
                let renderer_origin = *value as f32;
                !end.is_finite()
                    || end <= *value
                    || value + cell <= *value
                    || !renderer_origin.is_finite()
                    || !(end as f32).is_finite()
                    || renderer_origin + renderer_cell <= renderer_origin
            })
        {
            return Err("Invalid shore-field domain");
        }
        Ok(Self {
            origin,
            extent_m,
            water_level_m,
            side,
        })
    }
}

#[derive(Default)]
struct Heap(Vec<(f64, usize)>);
impl Heap {
    fn push(&mut self, key: f64, value: usize) {
        let mut i = self.0.len();
        self.0.push((key, value));
        while i > 0 {
            let p = (i - 1) / 2;
            if self.0[p].0 <= key {
                break;
            }
            self.0[i] = self.0[p];
            i = p;
        }
        self.0[i] = (key, value);
    }
    fn pop(&mut self) -> Option<usize> {
        let top = self.0.first()?.1;
        let last = self.0.pop()?;
        if self.0.is_empty() {
            return Some(top);
        }
        let mut i = 0;
        loop {
            let mut c = 2 * i + 1;
            if c >= self.0.len() {
                break;
            }
            if c + 1 < self.0.len() && self.0[c + 1].0 < self.0[c].0 {
                c += 1;
            }
            if self.0[c].0 >= last.0 {
                break;
            }
            self.0[i] = self.0[c];
            i = c;
        }
        self.0[i] = last;
        Some(top)
    }
}
fn neighbors(k: usize, side: usize) -> [Option<usize>; 4] {
    let x = k % side;
    let z = k / side;
    [
        (x > 0).then(|| k - 1),
        (x + 1 < side).then(|| k + 1),
        (z > 0).then(|| k - side),
        (z + 1 < side).then(|| k + side),
    ]
}
fn solve(k: usize, times: &[f32], state: &[u8], speed: &[f32], cell: f64, side: usize) -> f64 {
    let mut n = [f64::INFINITY; 4];
    for (i, neighbor) in neighbors(k, side).into_iter().enumerate() {
        if let Some(v) = neighbor
            && state[v] == 2
        {
            n[i] = f64::from(times[v]);
        }
    }
    let tx = n[0].min(n[1]);
    let tz = n[2].min(n[3]);
    let a = tx.min(tz);
    let b = tx.max(tz);
    let f = cell / f64::from(speed[k]);
    if !b.is_finite() || b - a >= f {
        a + f
    } else {
        0.5 * (a + b + (2.0 * f * f - (a - b) * (a - b)).sqrt())
    }
}
fn extend(field: &mut [f32], passes: usize, increment: f64, side: usize) {
    for _ in 0..passes {
        let previous = field.to_vec();
        let mut changed = false;
        for (k, value) in field.iter_mut().enumerate() {
            if previous[k].is_finite() {
                continue;
            }
            let mut sum = 0.0;
            let mut count = 0;
            for n in neighbors(k, side).into_iter().flatten() {
                if previous[n].is_finite() {
                    sum += f64::from(previous[n]);
                    count += 1;
                }
            }
            if count > 0 {
                *value = (sum / f64::from(count) + increment) as f32;
                changed = true;
            }
        }
        if !changed {
            break;
        }
    }
}
/// Original source frame: 2048 m, origin -1024 m, sea level zero.
/// The callback evaluates the effective canonical height without resampling it.
pub fn bake<E>(height: impl Fn(f64, f64) -> Result<f64, E>) -> Result<Vec<f32>, E> {
    bake_in_domain(Domain::REFERENCE, height)
}

/// Run the same propagation on an explicitly admitted square and water level.
/// The returned field uses the supplied frame and its explicitly admitted resolution.
#[allow(clippy::too_many_lines)]
pub fn bake_in_domain<E>(
    domain: Domain,
    height: impl Fn(f64, f64) -> Result<f64, E>,
) -> Result<Vec<f32>, E> {
    let side = domain.side;
    let n = side * side;
    let cell = domain.extent_m / side as f64;
    let mut speed = vec![0.0_f32; n];
    let mut times = vec![f32::INFINITY; n];
    let mut state = vec![0_u8; n];
    let mut heap = Heap::default();
    let length = (0.12_f64 * 0.12 + 1.0).sqrt();
    let (sdx, sdz) = (-0.12 / length, -1.0 / length);
    let c0 = (9.81_f64 * 25.0).sqrt();
    for k in 0..n {
        let (i, j) = (k % side, k / side);
        let x = domain.origin[0] + (i as f64 + 0.5) * cell;
        let z = domain.origin[1] + (j as f64 + 0.5) * cell;
        let depth = domain.water_level_m - height(x, z)?;
        speed[k] = if depth > 0.0 {
            (9.81 * depth.clamp(0.25, 25.0)).sqrt() as f32
        } else {
            0.0
        };
        if (i == 0 || j == 0 || i == side - 1 || j == side - 1) && speed[k] > 0.0 {
            times[k] = ((x * sdx + z * sdz) / c0 + 2048.0) as f32;
            state[k] = 1;
            heap.push(f64::from(times[k]), k);
        }
    }
    while let Some(k) = heap.pop() {
        if state[k] == 2 {
            continue;
        }
        state[k] = 2;
        for nk in neighbors(k, side).into_iter().flatten() {
            if state[nk] == 2 || speed[nk] <= 0.0 {
                continue;
            }
            let t = solve(nk, &times, &state, &speed, cell, side);
            if t < f64::from(times[nk]) {
                times[nk] = t as f32;
                state[nk] = 1;
                heap.push(t, nk);
            }
        }
    }
    let mut shore = times.clone();
    extend(&mut shore, 40, 0.0, side);
    extend(&mut times, 24, cell / 1.5, side);
    for _ in 0..3 {
        let mut out = times.clone();
        for k in 0..n {
            if !times[k].is_finite() {
                continue;
            }
            let mut sum = f64::from(times[k]) * 4.0;
            let mut weight = 4;
            for nk in neighbors(k, side).into_iter().flatten() {
                if times[nk].is_finite() {
                    sum += f64::from(times[nk]);
                    weight += 1;
                }
            }
            out[k] = (sum / f64::from(weight)) as f32;
        }
        times = out;
    }
    let sl = sdx.hypot(sdz);
    let mut data = vec![0.0; n * 4];
    let gradient = |a: f64, b: f64| {
        if a.is_finite() && b.is_finite() {
            a - b
        } else {
            0.0
        }
    };
    for k in 0..n {
        let t = f64::from(times[k]);
        let nb = neighbors(k, side).map(|n| n.map_or(t, |v| f64::from(times[v])));
        let mut gx = gradient(nb[1], nb[0]);
        let mut gz = gradient(nb[3], nb[2]);
        if gx == 0.0 && nb[1].is_finite() && t.is_finite() {
            gx = nb[1] - t;
        }
        if gz == 0.0 && nb[3].is_finite() && t.is_finite() {
            gz = nb[3] - t;
        }
        let len = gx.hypot(gz);
        let len = if len == 0.0 { 1.0 } else { len };
        let (dx, dz) = (gx / len, gz / len);
        let exposure = (((dx * sdx + dz * sdz) / sl) * 1.4 + 0.1).clamp(0.02, 1.0);
        data[k * 4] = if t.is_finite() { t as f32 } else { 1e5 };
        data[k * 4 + 1] = (dx * exposure) as f32;
        data[k * 4 + 2] = (dz * exposure) as f32;
        data[k * 4 + 3] = if shore[k].is_finite() {
            shore[k]
        } else {
            data[k * 4]
        };
    }
    Ok(data)
}

#[cfg(test)]
mod tests {
    use super::{Domain, SIDE, bake_in_domain};
    use std::cell::Cell;

    #[test]
    fn domain_rejects_invalid_or_unrepresentable_frames() {
        assert!(Domain::new([-2048.0, -2048.0], 4096.0, 3.0).is_ok());
        assert!(Domain::new([f64::NAN, 0.0], 2048.0, 0.0).is_err());
        assert!(Domain::new([0.0, 0.0], 0.0, 0.0).is_err());
        assert!(Domain::new([0.0, 0.0], -1.0, 0.0).is_err());
        assert!(Domain::new([0.0, 0.0], f64::INFINITY, 0.0).is_err());
        assert!(Domain::new([0.0, 0.0], 2048.0, f64::NAN).is_err());
        assert!(Domain::new([0.0, 0.0], 1e100, 0.0).is_err());
        assert!(Domain::new([0.0, 0.0], 2048.0, 1e100).is_err());
        assert!(Domain::new([1e20, 0.0], 2048.0, 0.0).is_err());
        assert!(Domain::new([f64::MAX, 0.0], 2048.0, 0.0).is_err());
    }

    #[test]
    fn preserving_detail_expands_resolution_or_refuses_the_budget() {
        let reference =
            Domain::preserving_spacing([-1024.0; 2], 2048.0, 0.0, 4.0, SIDE * SIDE).unwrap();
        assert_eq!(reference.side(), SIDE);
        let large =
            Domain::preserving_spacing([-2048.0; 2], 4096.0, 3.0, 4.0, 1024 * 1024).unwrap();
        assert_eq!(large.side(), 1024);
        assert!(
            Domain::preserving_spacing([-2048.0; 2], 4096.0, 3.0, 4.0, 1024 * 1024 - 1).is_err()
        );
        assert!(Domain::preserving_spacing([0.0; 2], 1.0, 0.0, 0.0, 100).is_err());
        assert!(Domain::preserving_spacing([0.0; 2], 1.0, 0.0, f64::NAN, 100).is_err());
        assert!(Domain::preserving_spacing([0.0; 2], 1.0, 0.0, f64::MIN_POSITIVE, 100).is_err());
    }

    #[test]
    fn variable_resolution_samples_the_declared_frame_without_stretching() {
        let domain = Domain::preserving_spacing([-32.0; 2], 64.0, 3.0, 4.0, 16 * 16).unwrap();
        let first = Cell::new(None);
        let last = Cell::new(None);
        let calls = Cell::new(0);
        let field = bake_in_domain(domain, |x, z| {
            if first.get().is_none() {
                first.set(Some([x, z]));
            }
            last.set(Some([x, z]));
            calls.set(calls.get() + 1);
            Ok::<_, ()>(1.0)
        })
        .unwrap();
        assert_eq!(domain.side(), 16);
        assert_eq!(first.get(), Some([-30.0, -30.0]));
        assert_eq!(last.get(), Some([30.0, 30.0]));
        assert_eq!(calls.get(), 16 * 16);
        assert_eq!(field.len(), 16 * 16 * 4);
        assert!(field.iter().all(|value| value.is_finite()));
    }

    #[test]
    fn expanded_field_propagates_at_reference_cell_spacing() {
        let domain =
            Domain::preserving_spacing([-2048.0; 2], 4096.0, 3.0, 4.0, 1024 * 1024).unwrap();
        let calls = Cell::new(0);
        let field = bake_in_domain(domain, |_, _| {
            calls.set(calls.get() + 1);
            Ok::<_, ()>(1.0)
        })
        .unwrap();
        assert_eq!(calls.get(), 1024 * 1024);
        assert_eq!(field.len(), 1024 * 1024 * 4);
        assert!(field.iter().all(|value| value.is_finite()));
    }

    #[test]
    fn shoreline_shaders_share_the_admitted_field_frame() {
        let domain =
            Domain::preserving_spacing([-2048.0, -1024.0], 4096.0, 3.0, 4.0, 1024 * 1024).unwrap();
        for source in [
            crate::source_breaker_gpu::COMMON_CREST_PROGRAM,
            crate::source_breaker_gpu::COMMON_SPRAY_PROGRAM,
        ] {
            assert_eq!(Domain::REFERENCE.prepare_shader(source).unwrap(), source);
            let shader = domain.prepare_shader(source).unwrap();
            let start = shader.find("fn terrainShoreSample(").unwrap();
            let open = start + shader[start..].find('{').unwrap();
            let end =
                crate::source_terrain::sampling::function_end(shader.as_bytes(), open).unwrap();
            let function = &shader[start..end];
            assert!(function.contains("let res = 1024.0;"));
            assert!(function.contains("vec2f(-2048.0, -1024.0)"));
            assert!(function.contains("/ 4096.0 * res"));
            let module = format!(
                "struct TerrainParams {{ origin:f32, size:f32, res:f32, shoreRes:f32, }};\n\
                 @group(0) @binding(0) var terrainShoreTex:texture_2d<f32>;\n\
                 @group(0) @binding(1) var<uniform> terrainParams:TerrainParams;\n\
                 {function}\n\
                 @compute @workgroup_size(1) fn main() {{ let result = terrainShoreSample(vec2f(0.0)); }}"
            );
            let parsed = naga::front::wgsl::parse_str(&module).unwrap();
            naga::valid::Validator::new(
                naga::valid::ValidationFlags::all(),
                naga::valid::Capabilities::all(),
            )
            .validate(&parsed)
            .unwrap();
            let restored = shader
                .replacen("let res = 1024.0;", "let res = terrainParams.shoreRes;", 1)
                .replacen(
                    "let f = ( xz - vec2f(-2048.0, -1024.0) ) / 4096.0 * res - 0.5;",
                    "let f = ( xz - terrainParams.origin ) / terrainParams.size * res - 0.5;",
                    1,
                );
            assert_eq!(restored, source);
        }
    }

    #[test]
    fn shoreline_shader_adapter_rejects_unknown_or_ambiguous_inputs() {
        let source = crate::source_breaker_gpu::COMMON_CREST_PROGRAM;
        let domain =
            Domain::preserving_spacing([-2048.0; 2], 4096.0, 0.0, 4.0, 1024 * 1024).unwrap();
        assert!(domain.prepare_shader("").is_err());
        assert!(
            domain
                .prepare_shader(&format!("{source}\n{source}"))
                .is_err()
        );
        assert!(
            domain
                .prepare_shader(
                    &source.replace("let res = terrainParams.shoreRes;", "let res = 512.0;")
                )
                .is_err()
        );
        assert!(
            domain
                .prepare_shader(&domain.prepare_shader(source).unwrap())
                .is_err()
        );
    }

    #[test]
    fn propagation_uses_the_admitted_frame_and_water_level() {
        let first = Cell::new(None);
        let last = Cell::new(None);
        let calls = Cell::new(0);
        let submerged = bake_in_domain(
            Domain::new([-2048.0, -2048.0], 4096.0, 3.0).unwrap(),
            |x, z| {
                if first.get().is_none() {
                    first.set(Some([x, z]));
                }
                last.set(Some([x, z]));
                calls.set(calls.get() + 1);
                Ok::<_, ()>(1.0)
            },
        )
        .unwrap();
        assert_eq!(first.get(), Some([-2044.0, -2044.0]));
        assert_eq!(last.get(), Some([2044.0, 2044.0]));
        assert_eq!(calls.get(), SIDE * SIDE);
        assert_eq!(submerged.len(), SIDE * SIDE * 4);
        assert!(submerged[0].is_finite() && submerged[0] < 1e5);

        let dry = bake_in_domain(
            Domain::new([-2048.0, -2048.0], 4096.0, 0.0).unwrap(),
            |_, _| Ok::<_, ()>(1.0),
        )
        .unwrap();
        assert_eq!(dry[0].to_bits(), 1e5_f32.to_bits());
        assert_ne!(submerged[0].to_bits(), dry[0].to_bits());
    }
}
