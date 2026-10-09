//! UV requirements derive from the pinned source-stage input ABI.
use bevy::prelude::Reflect;

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Hash, Reflect)]
#[reflect(opaque)]
pub struct SourceGraphUvRequirements(u8);

impl SourceGraphUvRequirements {
    pub fn channels(self) -> u8 {
        self.0
    }

    /// Require exact canonical source inputs. No ordinary UV attribute, unknown
    /// location or differently typed field can become an admitted source UV.
    pub fn from_shader(source: &str, locations: [u32; 7]) -> Result<Self, &'static str> {
        let header = "struct SourceGraphVertex {";
        let headers = source.matches(header).count();
        if headers == 0 {
            if (1..=7).any(|channel| source.contains(&format!("source_uv{channel}"))) {
                return Err("Additional source UV has no canonical vertex input");
            }
            return Ok(Self::default());
        }
        if headers != 1 {
            return Err("Source vertex input declaration is duplicated");
        }
        let body = source
            .split_once(header)
            .unwrap()
            .1
            .split_once('}')
            .ok_or("Source vertex input declaration is incomplete")?
            .0;
        let mut mask = 0;
        for line in body.lines().map(str::trim) {
            if !line.contains("source_uv")
                || (line.contains("source_uv0:")
                    && !line.replace("source_uv0:", "").contains("source_uv"))
            {
                continue;
            }
            let channel = (1..=7)
                .find(|channel| {
                    line == format!(
                        "@location({}) source_uv{channel}:vec4<f32>,",
                        locations[channel - 1]
                    )
                })
                .ok_or("Source UV input is not the pinned canonical layout")?;
            let bit = 1 << channel;
            if mask & bit != 0 {
                return Err("Source UV channel declaration is duplicated");
            }
            mask |= bit;
        }
        Ok(Self(mask))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    const LOCATIONS: [u32; 7] = [12, 13, 14, 15, 3, 4, 5];

    #[test]
    fn old_programs_keep_the_empty_requirement() {
        assert_eq!(
            SourceGraphUvRequirements::from_shader("fn vertex() {}", LOCATIONS)
                .unwrap()
                .channels(),
            0
        );
        assert_eq!(
            SourceGraphUvRequirements::from_shader(
                "struct SourceGraphVertex {\n @location(10) source_uv0:vec4<f32>,\n}",
                LOCATIONS
            )
            .unwrap()
            .channels(),
            0
        );
    }

    #[test]
    fn canonical_channels_are_required_by_the_actual_input_declaration() {
        let uv1 = "struct SourceGraphVertex {\n @location(12) source_uv1:vec4<f32>,\n}";
        assert_eq!(
            SourceGraphUvRequirements::from_shader(uv1, LOCATIONS)
                .unwrap()
                .channels(),
            2
        );
        let mut all = String::from("struct SourceGraphVertex {\n");
        for channel in 1..=7 {
            all += &format!(
                " @location({}) source_uv{channel}:vec4<f32>,\n",
                LOCATIONS[channel - 1]
            );
        }
        all += "}";
        assert_eq!(
            SourceGraphUvRequirements::from_shader(&all, LOCATIONS)
                .unwrap()
                .channels(),
            0xfe
        );
    }

    #[test]
    fn unknown_type_location_channel_and_duplicate_declarations_are_refused() {
        for declaration in [
            "@location(12) source_uv1:vec2<f32>,",
            "@location(3) source_uv1:vec4<f32>,",
            "@location(12) source_uv8:vec4<f32>,",
            "@location(12) source_uv1:vec4<f32>,\n@location(12) source_uv1:vec4<f32>,",
            "@location(10) source_uv0:vec4<f32>, @location(12) source_uv1:vec4<f32>,",
        ] {
            let source = format!("struct SourceGraphVertex {{\n{declaration}\n}}");
            assert!(SourceGraphUvRequirements::from_shader(&source, LOCATIONS).is_err());
        }
    }

    #[test]
    fn missing_or_duplicate_canonical_vertex_input_is_refused() {
        assert!(
            SourceGraphUvRequirements::from_shader(
                "@location(12) source_uv1:vec4<f32>,",
                LOCATIONS
            )
            .is_err()
        );
        assert!(
            SourceGraphUvRequirements::from_shader("struct SourceGraphVertex {", LOCATIONS)
                .is_err()
        );
        assert!(
            SourceGraphUvRequirements::from_shader(
                "struct SourceGraphVertex {} struct SourceGraphVertex {}",
                LOCATIONS
            )
            .is_err()
        );
    }
}
