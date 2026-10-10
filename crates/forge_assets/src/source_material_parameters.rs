//! Explicit scalar slots for imported source programs. Slot order is supplied by
//! the compiler and remains stable; no material or pack name chooses a layout.
//! GPU adapters must bind the returned 16 vec4 lanes to their declared uniform.

pub const SOURCE_SCALAR_CAPACITY: usize = 64;

#[derive(Clone, Debug, PartialEq)]
pub struct SourceMaterialScalarParameters {
    names: Vec<String>,
    values: [f32; SOURCE_SCALAR_CAPACITY],
}

impl SourceMaterialScalarParameters {
    pub fn from_compiler_slots(slots: Vec<(String, f32)>) -> Result<Self, &'static str> {
        if slots.len() > SOURCE_SCALAR_CAPACITY {
            return Err("source scalar uniform capacity exceeded");
        }
        let mut names = Vec::with_capacity(slots.len());
        let mut values = [0.0; SOURCE_SCALAR_CAPACITY];
        for (index, (name, value)) in slots.into_iter().enumerate() {
            if name.is_empty() || name.chars().any(char::is_control) || !value.is_finite() {
                return Err("invalid source scalar declaration");
            }
            if names.contains(&name) {
                return Err("duplicate source scalar declaration");
            }
            names.push(name);
            values[index] = value;
        }
        Ok(Self { names, values })
    }

    #[must_use]
    pub fn slot(&self, name: &str) -> Option<usize> {
        self.names.iter().position(|candidate| candidate == name)
    }

    #[must_use]
    pub fn value(&self, name: &str) -> Option<f32> {
        self.slot(name).map(|index| self.values[index])
    }

    /// Update this material instance without changing slot order or another clone.
    pub fn set_value(&mut self, name: &str, value: f32) -> Result<(), &'static str> {
        let index = self.slot(name).ok_or("undeclared source scalar")?;
        if !value.is_finite() {
            return Err("nonfinite source scalar override");
        }
        self.values[index] = value;
        Ok(())
    }

    #[must_use]
    pub fn vec4_lanes(&self) -> [[f32; 4]; 16] {
        std::array::from_fn(|lane| {
            std::array::from_fn(|component| self.values[lane * 4 + component])
        })
    }

    #[must_use]
    pub fn little_endian_uniform_bytes(&self) -> [u8; 256] {
        let mut bytes = [0_u8; 256];
        for (index, value) in self.values.iter().enumerate() {
            bytes[index * 4..index * 4 + 4].copy_from_slice(&value.to_le_bytes());
        }
        bytes
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn compiler_order_and_vec4_boundaries_survive_material_updates() {
        let slots = (0_u8..64)
            .map(|i| (format!("source_{i}"), f32::from(i)))
            .collect();
        let mut parameters = SourceMaterialScalarParameters::from_compiler_slots(slots).unwrap();
        parameters.set_value("source_4", 0.75).unwrap();
        assert_eq!(parameters.slot("source_4"), Some(4));
        assert_eq!(parameters.vec4_lanes()[1][0].to_bits(), 0.75_f32.to_bits());
        assert_eq!(parameters.vec4_lanes()[15][3].to_bits(), 63.0_f32.to_bits());
        let bytes = parameters.little_endian_uniform_bytes();
        assert_eq!(&bytes[16..20], &0.75_f32.to_le_bytes());
        assert_eq!(&bytes[252..256], &63.0_f32.to_le_bytes());
    }

    #[test]
    fn clone_changes_are_local_and_undeclared_names_are_refused() {
        let shared = SourceMaterialScalarParameters::from_compiler_slots(vec![
            ("_Dissolve".into(), 0.0),
            ("_Opacity".into(), 3.0),
        ])
        .unwrap();
        let mut instance = shared.clone();
        instance.set_value("_Dissolve", 0.5).unwrap();
        assert_eq!(shared.value("_Dissolve"), Some(0.0));
        assert_eq!(instance.value("_Dissolve"), Some(0.5));
        let before = instance.clone();
        assert!(instance.set_value("_Undeclared", 1.0).is_err());
        assert!(instance.set_value("_Dissolve", f32::NAN).is_err());
        assert_eq!(instance, before);
    }

    #[test]
    fn declarations_refuse_duplicates_and_overflow() {
        assert!(
            SourceMaterialScalarParameters::from_compiler_slots(vec![
                ("same".into(), 0.0),
                ("same".into(), 1.0),
            ])
            .is_err()
        );
        assert!(
            SourceMaterialScalarParameters::from_compiler_slots(
                (0..65).map(|i| (i.to_string(), 0.0)).collect(),
            )
            .is_err()
        );
        assert!(
            SourceMaterialScalarParameters::from_compiler_slots(vec![(
                "infinite".into(),
                f32::INFINITY
            ),])
            .is_err()
        );
    }
}
