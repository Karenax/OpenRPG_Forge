//! Generation-local trace density; acquired packets keep their original contract.
use bevy::prelude::*;

#[derive(Resource, Default, Clone, Copy, Debug, PartialEq, Eq)]
pub enum CloudTraceQuality {
    #[default]
    Acquired,
    Dense,
}

impl CloudTraceQuality {
    pub(super) fn source_size(self) -> [u32; 2] {
        match self {
            Self::Acquired => [240, 135],
            Self::Dense => [960, 540],
        }
    }

    pub(super) fn panorama_size(self) -> [u32; 2] {
        match self {
            Self::Acquired => [512, 160],
            Self::Dense => [1024, 320],
        }
    }

    // Trace every history texel each frame; no interleaved grid at cloud edges.
    fn sampling(valid: f32) -> [f32; 4] {
        [0.0, 0.0, 1.0, valid]
    }

    pub(super) fn output(
        self,
        output: &crate::cloud_controller::Output,
    ) -> crate::cloud_controller::Output {
        let mut output = output.clone();
        if self == Self::Dense {
            output.frame[9] = Self::sampling(output.frame[9][3]);
            output.source_size = self.source_size();
        }
        output
    }

    pub(super) fn prepare(self, input: &mut super::Input) -> Result<(), String> {
        if self != Self::Dense {
            return Ok(());
        }
        let Some(shadow) = &mut input.shadow else {
            return Ok(());
        };
        for (local, _) in &mut shadow.frames {
            // Source packets have already passed byte-size and finite-value validation.
            let word = |index: usize| {
                f32::from_le_bytes(
                    local[index * 4..index * 4 + 4]
                        .try_into()
                        .expect("validated cloud word"),
                )
            };
            let sampling = Self::sampling(word(39));
            local[144..160].copy_from_slice(
                &sampling
                    .into_iter()
                    .flat_map(f32::to_le_bytes)
                    .collect::<Vec<_>>(),
            );
        }
        if let Some(panorama) = shadow
            .trace
            .as_mut()
            .and_then(|trace| trace.panorama.as_mut())
        {
            const CONE: &str = "let cone = 0.01227184630308513;";
            if panorama.matches(CONE).count() != 1 {
                return Err("Dense cloud panorama requires the validated angular footprint".into());
            }
            *panorama = panorama.replace(CONE, "let cone = 0.006135923151542565;");
        }
        Ok(())
    }
}

pub(super) fn dense_packet(local: &[u8]) -> bool {
    local.get(152..156) == Some(1.0_f32.to_le_bytes().as_slice())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn dense_trace_refreshes_every_history_texel_and_preserves_validity() {
        assert_eq!(CloudTraceQuality::sampling(1.0), [0.0, 0.0, 1.0, 1.0]);
        assert_eq!(CloudTraceQuality::sampling(0.0), [0.0, 0.0, 1.0, 0.0]);
        assert_eq!(CloudTraceQuality::Dense.source_size(), [960, 540]);
        assert_eq!(CloudTraceQuality::Dense.panorama_size(), [1024, 320]);
    }
}
