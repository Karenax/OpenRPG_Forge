use super::mesh::TerrainMaterialWeights;
use super::{
    TERRAIN_GLOBAL_INTERVALS_PER_AXIS, TERRAIN_HALF_EXTENT_M, TERRAIN_SAMPLE_SPACING_M,
    TerrainGenerationError, TerrainGenerationPlan,
};
use forge_project::TerrainDocument;

const NOISE_X_MIX: u64 = 0x9e37_79b9_7f4a_7c15;
const NOISE_Z_MIX: u64 = 0xbf58_476d_1ce4_e5b9;
const NOISE_OCTAVE_MIX: u64 = 0x94d0_49bb_1331_11eb;
/// Every integer through 2^53 - 1 has an exact f64 representation. Keeping lattice coordinates
/// inside this range also guarantees that the `floor + 1` neighbour cannot overflow `i64`.
const MAX_EXACT_F64_INTEGER: f64 = 9_007_199_254_740_991.0;
const F64_UNIT_FROM_53_BITS: f64 = 1.0 / MAX_EXACT_F64_INTEGER;

pub(super) fn validate_arithmetic_contract(
    document: &TerrainDocument,
) -> Result<(), TerrainGenerationError> {
    let parameters = document.height_sampling;

    // Value noise and its positive normalized octave weights keep the result in [-1, 1].
    // This bound therefore guarantees that the one specified f64 -> f32 conversion stays finite.
    let maximum_absolute_delta = document
        .height_deltas
        .iter()
        .map(|delta| f64::from(delta.delta_m).abs())
        .fold(0.0_f64, f64::max);
    let maximum_absolute_height = f64::from(parameters.base_elevation_m).abs()
        + f64::from(parameters.amplitude_m)
        + maximum_absolute_delta;
    if !maximum_absolute_height.is_finite() || maximum_absolute_height > f64::from(f32::MAX) {
        return Err(TerrainGenerationError::HeightOutputOutOfRange);
    }

    let maximum_absolute_sample_coordinate = f64::from(super::TERRAIN_PADDED_MAXIMUM_M);
    let mut frequency = f64::from(parameters.base_frequency_cycles_per_m);
    for octave in 0..parameters.octaves {
        let maximum_absolute_lattice_coordinate = maximum_absolute_sample_coordinate * frequency;
        if !maximum_absolute_lattice_coordinate.is_finite()
            || maximum_absolute_lattice_coordinate > MAX_EXACT_F64_INTEGER
        {
            return Err(TerrainGenerationError::OctaveLatticeCoordinateOutOfRange { octave });
        }
        if octave + 1 < parameters.octaves {
            frequency *= f64::from(parameters.lacunarity);
        }
    }

    Ok(())
}

/// Validates the document and samples its exact versioned height function.
pub fn sample_height(
    document: &TerrainDocument,
    x_m: f32,
    z_m: f32,
) -> Result<f32, TerrainGenerationError> {
    TerrainGenerationPlan::from_document(document)?.height_at(x_m, z_m)
}

/// `FractalValueNoiseV1` semantics:
/// - signed `SplitMix64` lattice values keyed by seed, octave and integer X/Z;
/// - quintic interpolation on both axes;
/// - normalized octave sum using document lacunarity and persistence;
/// - f64 intermediates followed by one specified f32 conversion.
#[must_use]
#[allow(clippy::cast_possible_truncation)]
pub(super) fn sample_height_unchecked(document: &TerrainDocument, x_m: f32, z_m: f32) -> f32 {
    let parameters = document.height_sampling;
    let mut frequency = f64::from(parameters.base_frequency_cycles_per_m);
    let mut octave_weight = 1.0_f64;
    let mut weighted_value = 0.0_f64;
    let mut weight_sum = 0.0_f64;

    for octave in 0..parameters.octaves {
        let octave_seed = document
            .seed
            .wrapping_add(u64::from(octave).wrapping_mul(NOISE_OCTAVE_MIX));
        let value = value_noise_2d(
            octave_seed,
            f64::from(x_m) * frequency,
            f64::from(z_m) * frequency,
        );
        weighted_value += value * octave_weight;
        weight_sum += octave_weight;
        if octave + 1 < parameters.octaves {
            frequency *= f64::from(parameters.lacunarity);
            octave_weight *= f64::from(parameters.persistence);
        }
    }

    let normalized = (weighted_value / weight_sum).clamp(-1.0, 1.0);
    let maximum_f32 = f64::from(f32::MAX);
    let procedural_height =
        f64::from(parameters.base_elevation_m) + f64::from(parameters.amplitude_m) * normalized;
    if document.height_deltas.is_empty() {
        return procedural_height.clamp(-maximum_f32, maximum_f32) as f32;
    }
    (procedural_height + interpolated_height_delta(document, x_m, z_m))
        .clamp(-maximum_f32, maximum_f32) as f32
}

/// Bilinearly interpolates the sparse authored offsets. A query in the one-sample normal padding
/// outside the source grid receives no authored offset.
#[must_use]
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn interpolated_height_delta(document: &TerrainDocument, x_m: f32, z_m: f32) -> f64 {
    let sample_x = f64::from((x_m + TERRAIN_HALF_EXTENT_M) / TERRAIN_SAMPLE_SPACING_M);
    let sample_z = f64::from((z_m + TERRAIN_HALF_EXTENT_M) / TERRAIN_SAMPLE_SPACING_M);
    let maximum = f64::from(TERRAIN_GLOBAL_INTERVALS_PER_AXIS);
    if !(0.0..=maximum).contains(&sample_x) || !(0.0..=maximum).contains(&sample_z) {
        return 0.0;
    }

    let minimum_x = sample_x.floor() as u16;
    let minimum_z = sample_z.floor() as u16;
    let maximum_x = minimum_x
        .saturating_add(1)
        .min(TERRAIN_GLOBAL_INTERVALS_PER_AXIS);
    let maximum_z = minimum_z
        .saturating_add(1)
        .min(TERRAIN_GLOBAL_INTERVALS_PER_AXIS);
    let amount_x = sample_x - f64::from(minimum_x);
    let amount_z = sample_z - f64::from(minimum_z);
    let back = linear_interpolate(
        height_delta_at(document, minimum_x, minimum_z),
        height_delta_at(document, maximum_x, minimum_z),
        amount_x,
    );
    let front = linear_interpolate(
        height_delta_at(document, minimum_x, maximum_z),
        height_delta_at(document, maximum_x, maximum_z),
        amount_x,
    );
    linear_interpolate(back, front, amount_z)
}

#[must_use]
fn height_delta_at(document: &TerrainDocument, sample_x: u16, sample_z: u16) -> f64 {
    let key = (sample_z, sample_x);
    document
        .height_deltas
        .binary_search_by_key(&key, |delta| delta.sort_key())
        .map_or(0.0, |index| {
            f64::from(document.height_deltas[index].delta_m)
        })
}

#[must_use]
pub(super) fn sample_normal(document: &TerrainDocument, x_m: f32, z_m: f32) -> [f32; 3] {
    let spacing_m = document.height_sampling.sample_spacing_m;
    let spacing = f64::from(spacing_m);
    let left = sample_height_unchecked(document, x_m - spacing_m, z_m);
    let right = sample_height_unchecked(document, x_m + spacing_m, z_m);
    let back = sample_height_unchecked(document, x_m, z_m - spacing_m);
    let front = sample_height_unchecked(document, x_m, z_m + spacing_m);
    normalize([
        (f64::from(left) - f64::from(right)) / (2.0 * spacing),
        1.0,
        (f64::from(back) - f64::from(front)) / (2.0 * spacing),
    ])
}

#[must_use]
#[allow(clippy::cast_possible_truncation)]
pub(super) fn material_weights_for(
    document: &TerrainDocument,
    height_m: f32,
    normal_y: f32,
) -> TerrainMaterialWeights {
    let parameters = document.height_sampling;
    let amplitude = f64::from(parameters.amplitude_m);
    let elevation_range = (2.0 * amplitude).max(f64::from(f32::EPSILON));
    let elevation = ((f64::from(height_m) - (f64::from(parameters.base_elevation_m) - amplitude))
        / elevation_range)
        .clamp(0.0, 1.0) as f32;
    let steepness = (1.0 - normal_y).clamp(0.0, 1.0);
    let slope_rock = smoothstep(0.08, 0.42, steepness);
    let high_rock = smoothstep(0.72, 0.95, elevation) * 0.55;
    let rock_factor = (slope_rock + high_rock).clamp(0.0, 1.0);
    let earth_factor = smoothstep(0.015, 0.18, steepness) * (1.0 - rock_factor);

    let grass_raw = (1.0 - rock_factor) * (1.0 - 0.7 * earth_factor) + 0.01;
    let earth_raw = earth_factor + 0.01;
    let rock_raw = rock_factor + 0.01;
    let total = grass_raw + earth_raw + rock_raw;
    TerrainMaterialWeights {
        grass: grass_raw / total,
        earth: earth_raw / total,
        rock: rock_raw / total,
    }
}

#[must_use]
#[allow(clippy::cast_precision_loss)]
fn value_noise_2d(seed: u64, x: f64, z: f64) -> f64 {
    let lattice_x = floor_to_i64(x);
    let lattice_z = floor_to_i64(z);
    let fraction_x = x - lattice_x as f64;
    let fraction_z = z - lattice_z as f64;
    let smooth_x = quintic_smooth(fraction_x);
    let smooth_z = quintic_smooth(fraction_z);

    let bottom_left = lattice_value(seed, lattice_x, lattice_z);
    let bottom_right = lattice_value(seed, lattice_x + 1, lattice_z);
    let top_left = lattice_value(seed, lattice_x, lattice_z + 1);
    let top_right = lattice_value(seed, lattice_x + 1, lattice_z + 1);
    let bottom = linear_interpolate(bottom_left, bottom_right, smooth_x);
    let top = linear_interpolate(top_left, top_right, smooth_x);
    linear_interpolate(bottom, top, smooth_z)
}

#[must_use]
#[allow(clippy::cast_possible_truncation)]
fn floor_to_i64(value: f64) -> i64 {
    value.floor() as i64
}

#[must_use]
#[allow(clippy::cast_precision_loss)]
fn lattice_value(seed: u64, x: i64, z: i64) -> f64 {
    let mut value = seed
        .wrapping_add(i64_bits(x).wrapping_mul(NOISE_X_MIX))
        .wrapping_add(i64_bits(z).wrapping_mul(NOISE_Z_MIX));
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^= value >> 31;
    let unit = ((value >> 11) as f64) * F64_UNIT_FROM_53_BITS;
    unit * 2.0 - 1.0
}

#[must_use]
fn i64_bits(value: i64) -> u64 {
    u64::from_ne_bytes(value.to_ne_bytes())
}

#[must_use]
fn quintic_smooth(value: f64) -> f64 {
    value * value * value * (value * (value * 6.0 - 15.0) + 10.0)
}

#[must_use]
fn linear_interpolate(left: f64, right: f64, amount: f64) -> f64 {
    left + (right - left) * amount
}

#[must_use]
#[allow(clippy::cast_possible_truncation)]
fn normalize(value: [f64; 3]) -> [f32; 3] {
    let length_squared = value[0] * value[0] + value[1] * value[1] + value[2] * value[2];
    let inverse_length = length_squared.sqrt().recip();
    [
        (value[0] * inverse_length) as f32,
        (value[1] * inverse_length) as f32,
        (value[2] * inverse_length) as f32,
    ]
}

#[must_use]
fn smoothstep(edge_minimum: f32, edge_maximum: f32, value: f32) -> f32 {
    let amount = ((value - edge_minimum) / (edge_maximum - edge_minimum)).clamp(0.0, 1.0);
    amount * amount * (3.0 - 2.0 * amount)
}
