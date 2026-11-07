use super::{TransformBounds, WIND_DIRECTION_LENGTH, wind_strength};
use crate::source_vegetation::LOBE;
use crate::source_vegetation::MAT;
use crate::source_vegetation::Record;
use crate::source_vegetation::VEG;
use bevy::{math::DVec3, mesh::VertexAttributeValues, prelude::Mesh};

#[derive(Default)]
pub(super) struct Summary {
    absolute: DVec3,
    radius: f64,
    stem: DVec3,
    crown: DVec3,
    crown_radius: f64,
    max_u: f64,
    max_curve: f64,
    normal: f64,
    lobe_xz: f64,
    lobe_y: f64,
    hf_squared: f64,
    flex: f64,
    flutter: f64,
    quad_x: f64,
    quad_y: f64,
    has_stem: bool,
    has_crown: bool,
}

fn four(
    mesh: &Mesh,
    id: bevy::mesh::MeshVertexAttribute,
) -> Result<Option<&Vec<[f32; 4]>>, String> {
    match mesh.attribute(id) {
        Some(VertexAttributeValues::Float32x4(values)) => Ok(Some(values)),
        None => Ok(None),
        _ => Err("Invalid plant envelope attribute layout".into()),
    }
}

impl Summary {
    pub(super) fn from_mesh(
        mesh: &Mesh,
        kind: u8,
        plant_kind: f32,
        shrub: bool,
    ) -> Result<Self, String> {
        let Some(VertexAttributeValues::Float32x3(positions)) =
            mesh.attribute(Mesh::ATTRIBUTE_POSITION)
        else {
            return Err("Plant envelope positions unavailable".into());
        };
        let Some(VertexAttributeValues::Float32x3(normals)) =
            mesh.attribute(Mesh::ATTRIBUTE_NORMAL)
        else {
            return Err("Plant envelope normals unavailable".into());
        };
        let veg = four(mesh, VEG)?;
        let mat = four(mesh, MAT)?;
        let lobes = four(mesh, LOBE)?;
        if normals.len() != positions.len()
            || [veg, mat, lobes]
                .iter()
                .flatten()
                .any(|v| v.len() != positions.len())
        {
            return Err("Plant envelope attribute counts differ".into());
        }
        let mut out = Self::default();
        for (index, position) in positions.iter().enumerate() {
            let material = mat.map_or([0.0; 4], |v| v[index]);
            let lobe = lobes.map_or([0.0; 4], |v| v[index]);
            let weights = veg.map_or([0.0; 4], |v| v[index]);
            let vertex_kind = -lobe[3] - 1.0;
            if (kind == 0 && !(vertex_kind < 0.5 || (vertex_kind - plant_kind).abs() < 0.5))
                || (kind == 1 && (material[0] > 3.5) != shrub)
            {
                continue;
            }
            let p = DVec3::from_array(position.map(f64::from));
            let n = DVec3::from_array(normals[index].map(f64::from));
            if !p.is_finite()
                || !n.is_finite()
                || weights
                    .iter()
                    .chain(&lobe)
                    .chain(&material)
                    .any(|v| !v.is_finite())
            {
                return Err("Nonfinite plant envelope geometry".into());
            }
            out.absolute = out.absolute.max(p.abs());
            out.radius = out.radius.max(p.length());
            out.normal = out.normal.max(n.length());
            if material[0] < 0.5 {
                out.has_stem = true;
                out.stem = out.stem.max(p.abs());
                let u = f64::from(weights[0]);
                out.max_u = out.max_u.max(u.abs());
                out.max_curve = out.max_curve.max(u.abs().max((u * (2.0 - u)).abs()));
            } else {
                out.has_crown = true;
                out.crown = out.crown.max(p.abs());
                out.crown_radius = out.crown_radius.max(p.length());
            }
            if lobe[3] >= 0.0 {
                if kind == 1 && lobe[3] >= 8.0 {
                    return Err("Unsupported source canopy lobe index".into());
                }
                out.lobe_xz = out
                    .lobe_xz
                    .max(f64::from(lobe[0]).hypot(f64::from(lobe[2])));
                out.lobe_y = out.lobe_y.max(f64::from(lobe[1]).abs());
            }
            out.hf_squared = out.hf_squared.max(f64::from(weights[0]).powi(2));
            out.flex = out.flex.max(f64::from(weights[1]).abs());
            out.flutter = out.flutter.max(f64::from(weights[2]).abs());
            out.quad_x = out.quad_x.max(p.x.abs());
            out.quad_y = out.quad_y.max(p.y.abs());
        }
        Ok(out)
    }

    pub(super) fn enclose(
        &self,
        kind: u8,
        record: &Record,
        transform: &TransformBounds,
    ) -> Result<(f64, f64), String> {
        let scale = f64::from(record.i_pos.w).abs();
        let height = f64::from(record.i_dat.z).abs();
        let stretch = f64::from(record.i_dat.y).abs();
        let wind = wind_strength();
        let sway =
            (wind * wind * 0.009 * 1.1 + wind * 0.0045 * 1.4) * height * WIND_DIRECTION_LENGTH;
        match kind {
            0 => {
                // All frond bends/bounces/flutter finish with a length-preserving
                // rotation. Stem radial rotation is also length preserving.
                let center = height * stretch.hypot(1.0);
                let stem = (height * self.max_u).hypot(height * stretch * self.max_curve)
                    + transform.horizontal(self.stem);
                let crown = center + transform.radius(self.crown, self.crown_radius);
                let radius = if self.has_stem { stem } else { 0.0 }.max(if self.has_crown {
                    crown
                } else {
                    0.0
                });
                let sway = (wind * wind * 0.014 * 1.2 + wind * 0.0065 * 1.45 + wind * 0.0028)
                    * height
                    * WIND_DIRECTION_LENGTH;
                let movement = sway
                    * (if self.has_stem {
                        self.max_u.powi(2)
                    } else {
                        0.0
                    })
                    .max(f64::from(u8::from(self.has_crown)));
                Ok((radius, movement))
            }
            1 => {
                // Source table lies in [0,1.14838], so abs(1-lobeScale)<=1.
                let lobe = self.lobe_xz.hypot(self.lobe_y * stretch) * scale;
                let radius = transform.radius(self.absolute, self.radius) + lobe;
                let stem = (sway + wind * 0.002 * height * WIND_DIRECTION_LENGTH) * self.hf_squared;
                let branch = self.flex * scale * (wind * 0.105 + wind * wind * 0.052);
                let flutter = transform.normal_operator
                    * self.normal
                    * self.flutter
                    * scale
                    * (wind * 0.035 + 0.005);
                Ok((radius, stem + branch + flutter))
            }
            2 => {
                let (horizontal, vertical, center) = if record.i_dat.y < 0.0 {
                    (
                        1.706_517_677_293_758_3,
                        1.088_007_862_269_878_3,
                        0.721_987_217_664_718_6,
                    )
                } else {
                    (
                        6.556_488_643_724_781,
                        6.740_293_570_160_866,
                        6.025_867_998_600_006,
                    )
                };
                // Billboard right is unit; up is perpendicular and at most unit.
                // This covers every camera orientation, not a sampled view angle.
                let radius = scale
                    * (center * stretch
                        + (horizontal * self.quad_x)
                            .hypot((vertical * stretch + horizontal) * self.quad_y));
                Ok((radius, sway * 0.45))
            }
            _ => Err("Unsupported world plant geometry kind".into()),
        }
    }
}
