//! Bounded presentation path. Sources change only when the finished stroke is accepted natively.
#[derive(Clone, Debug, Default, PartialEq)]
pub(crate) struct BrushPath {
    pub points: Vec<[f32; 2]>,
}

impl BrushPath {
    pub fn append(&mut self, point: [f32; 2], radius: f32) -> Result<(), String> {
        if !radius.is_finite()
            || !(2.0..=64.0).contains(&radius)
            || point
                .iter()
                .any(|p| !p.is_finite() || !(-256.0..=256.0).contains(p))
        {
            return Err("Position de brosse invalide.".into());
        }
        let Some(previous) = self.points.last().copied() else {
            self.points.push(point);
            return Ok(());
        };
        let dx = point[0] - previous[0];
        let dz = point[1] - previous[1];
        let distance = dx.hypot(dz);
        let spacing = (radius * 0.25).max(1.0);
        if distance < spacing * 0.5 {
            return Ok(());
        }
        #[allow(
            clippy::cast_possible_truncation,
            clippy::cast_sign_loss,
            reason = "Finite distance within the bounded 512 m area"
        )]
        let count = (distance / spacing).ceil() as u16;
        if self.points.len() + usize::from(count) > 64 {
            return Err("Trait trop long (64 empreintes maximum). Rien n’a été appliqué ; dessinez un trait plus court.".into());
        }
        for index in 1..=count {
            let t = f32::from(index) / f32::from(count);
            self.points
                .push([previous[0] + dx * t, previous[1] + dz * t]);
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn path_fills_gaps_without_duplicates_and_refuses_overflow_atomically() {
        let mut path = BrushPath::default();
        path.append([0.0, 0.0], 8.0).unwrap();
        path.append([0.0, 0.0], 8.0).unwrap();
        path.append([8.0, 0.0], 8.0).unwrap();
        assert_eq!(
            path.points,
            vec![[0.0, 0.0], [2.0, 0.0], [4.0, 0.0], [6.0, 0.0], [8.0, 0.0]]
        );
        let before = path.clone();
        assert!(path.append([256.0, 0.0], 2.0).is_err());
        assert_eq!(path, before);
        assert!(path.append([f32::NAN, 0.0], 8.0).is_err());
        assert!(path.append([0.0, 0.0], 0.0).is_err());
    }
}
