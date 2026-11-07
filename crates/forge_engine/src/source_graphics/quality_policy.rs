//! Engine display policy for domains present in the installed-module host.
//! Authored population, physics, weather and other absent domains are not fabricated.
use forge_core::{ContentHash, canonical_json_sha256};
use forge_project::{
    PostProcessQualitySettings, ShadowQualitySettings, TerrainDocument, VisualQualityTier,
    VisualValleyQualityProfileDocument,
};
use serde::Serialize;

pub const TIERS: [VisualQualityTier; 4] = [
    VisualQualityTier::Low,
    VisualQualityTier::Medium,
    VisualQualityTier::High,
    VisualQualityTier::Ultra,
];

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct Preset {
    pub raster_strides: Option<[u8; 2]>,
    pub shadows: ShadowQualitySettings,
    pub post_process: PostProcessQualitySettings,
    pub msaa_samples: u8,
    pub contact_shadows: Option<ContactSettings>,
    pub temporal_aa: bool,
}

#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct ContactSettings {
    pub linear_steps: u32,
    pub thickness_m: f32,
    pub length_m: f32,
}

/// A single effective renderer request derived from a tier and optional edits.
/// The preset contains the budgets actually requested for this selection.
#[derive(Clone, Copy, Debug, PartialEq, Serialize)]
pub struct EffectiveProfile {
    pub preset: Preset,
    pub settings: super::Settings,
    pub fxaa: bool,
}

pub struct Policy {
    pub shadow_source: VisualValleyQualityProfileDocument,
    pub identity: ContentHash,
    pub shadow_hash: String,
    presets: [Preset; 4],
}

impl Policy {
    pub fn new(terrain: &TerrainDocument) -> Result<Self, String> {
        Self::with_procedural_streaming(terrain, false)
    }

    pub fn with_procedural_streaming(
        terrain: &TerrainDocument,
        procedural_streaming: bool,
    ) -> Result<Self, String> {
        let mut source = VisualValleyQualityProfileDocument::from_json(include_str!(
            "../../../../examples/visual_valley/quality-profile.v1.json"
        ))
        .map_err(|e| e.to_string())?;
        source.project_id = terrain.project_id;
        source.area_id = terrain.area_id;
        source.revision = terrain.revision;
        source.name = "Engine module shadow policy".into();
        source.validate().map_err(|e| e.to_string())?;
        let presets = TIERS.map(|tier| {
            let preset = source.preset(tier);
            Preset {
                raster_strides: (terrain.sampled_source.is_some() || procedural_streaming)
                    .then_some([
                        preset.terrain.lod1_sample_stride,
                        preset.terrain.lod2_sample_stride,
                    ]),
                shadows: preset.shadows,
                post_process: preset.post_process,
                // The shared final pass consumes single-sample scene depth.
                // Use the reference's FXAA lane when temporal AA is disabled.
                msaa_samples: 1,
                // Stochastic contact rays require temporal reconstruction.
                // The plant prepass supplies actual previous deformation vectors.
                contact_shadows: preset.shadows.contact_shadows_enabled.then_some(
                    ContactSettings {
                        linear_steps: 16,
                        thickness_m: 0.1,
                        length_m: 0.3,
                    },
                ),
                temporal_aa: preset.shadows.contact_shadows_enabled,
            }
        });
        let reference_settings = presets
            .iter()
            .map(|preset| super::Settings::for_quality(preset.post_process, preset.temporal_aa))
            .collect::<Result<Vec<_>, _>>()?;
        let identity = canonical_json_sha256(&(
            "forge.module-display-policy",
            3_u32,
            terrain.content_hash().map_err(|e| e.to_string())?,
            presets,
            "reference",
            reference_settings,
        ))
        .map_err(|e| e.to_string())?;
        let shadow_hash = source
            .content_hash()
            .map_err(|e| e.to_string())?
            .to_string();
        Ok(Self {
            shadow_source: source,
            identity,
            shadow_hash,
            presets,
        })
    }
    #[must_use]
    pub fn preset(&self, tier: VisualQualityTier) -> Preset {
        self.presets[TIERS
            .iter()
            .position(|value| *value == tier)
            .expect("closed graphics tier")]
    }

    /// Resolve the current tier and any explicitly customized source settings.
    /// Uncustomized tiers retain their existing budgets and values exactly.
    ///
    /// # Errors
    /// Refuses an invalid tier configuration or an out-of-range custom value.
    pub fn resolve(
        &self,
        tier: VisualQualityTier,
        custom: Option<super::Settings>,
    ) -> Result<EffectiveProfile, String> {
        let mut preset = self.preset(tier);
        let settings = if let Some(mut settings) = custom {
            if !settings.valid() {
                return Err("Invalid customized graphics settings".into());
            }
            preset.temporal_aa = settings.temporal_aa > 0.5;
            if preset.temporal_aa {
                // The shared renderer already gives TAA priority over FXAA.
                settings.fxaa = 0.0;
            }
            if !preset.temporal_aa {
                preset.shadows.contact_shadows_enabled = false;
                preset.contact_shadows = None;
            }
            // An edited value must not be silently disabled by its base tier.
            preset.post_process.tonemapping_enabled = true;
            preset.post_process.color_grading_enabled = true;
            preset.post_process.bloom_enabled = settings.bloom > 0.0;
            settings
        } else {
            super::Settings::for_quality(preset.post_process, preset.temporal_aa)?
        };
        Ok(EffectiveProfile {
            preset,
            fxaa: settings.fxaa > 0.5 && !preset.temporal_aa,
            settings,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn effective_profile_preserves_tiers_and_resolves_custom_effects() {
        let terrain = forge_world::embedded_visual_valley_terrain_document().unwrap();
        let policy = Policy::new(&terrain).unwrap();
        for tier in TIERS {
            let base = policy.preset(tier);
            let effective = policy.resolve(tier, None).unwrap();
            assert_eq!(effective.preset, base);
            assert_eq!(
                effective.settings,
                super::super::Settings::for_quality(base.post_process, base.temporal_aa).unwrap()
            );
            assert_eq!(effective.fxaa, !base.temporal_aa);
        }
        let mut edited = policy
            .resolve(VisualQualityTier::Low, None)
            .unwrap()
            .settings;
        edited.bloom = 0.1;
        edited.temporal_aa = 1.0;
        let low = policy
            .resolve(VisualQualityTier::Low, Some(edited))
            .unwrap();
        assert!(low.preset.post_process.bloom_enabled);
        assert!(low.preset.temporal_aa);
        assert!(!low.fxaa);
        assert_eq!(low.settings.fxaa.to_bits(), 0.0_f32.to_bits());
        let mut edited = policy
            .resolve(VisualQualityTier::High, None)
            .unwrap()
            .settings;
        edited.temporal_aa = 0.0;
        let high = policy
            .resolve(VisualQualityTier::High, Some(edited))
            .unwrap();
        assert!(!high.preset.shadows.contact_shadows_enabled);
        assert!(high.preset.contact_shadows.is_none());
        edited.bloom = f32::NAN;
        assert!(
            policy
                .resolve(VisualQualityTier::High, Some(edited))
                .is_err()
        );
    }
}
