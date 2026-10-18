//! Additional isolated shadow triplet after the three unchanged immersion pairs.
//! Uses the same drained source camera/clock; never replaces a regression gate.
use super::{FreezeSignature, Stage, synchronized_signature};
use bevy::{
    prelude::*,
    render::view::screenshot::{Screenshot, ScreenshotCaptured, save_to_disk},
};
use std::{
    path::PathBuf,
    sync::{Arc, Mutex},
};
use {
    forge_engine::ForgePlayerCamera,
    forge_tidewater::{
        tidewater_underwater::{TidewaterUnderwaterControl, UnderwaterObservation},
        tidewater_underwater_lighting::{Control, Targets},
    },
};

pub(super) struct Proof {
    signature: FreezeSignature,
    path: PathBuf,
    captures: Arc<Mutex<Vec<serde_json::Value>>>,
    baseline_pixels: Arc<Mutex<Option<Vec<u8>>>>,
    next: usize,
    waiting: bool,
    baseline: u64,
    original_medium: TidewaterUnderwaterControl,
    original_lighting: Control,
}

impl Proof {
    pub(super) fn start(
        signature: FreezeSignature,
        path: &std::path::Path,
        medium: &mut TidewaterUnderwaterControl,
        lighting: &mut Control,
        baseline: u64,
    ) -> Self {
        let proof = Self {
            signature,
            path: path.to_owned(),
            captures: Arc::default(),
            baseline_pixels: Arc::default(),
            next: 0,
            waiting: false,
            baseline,
            original_medium: *medium,
            original_lighting: *lighting,
        };
        medium.enabled = true;
        medium.shafts_strength = 0.0;
        lighting.enabled = true;
        lighting.caustics_strength = 0.0;
        lighting.shadow_refraction = true;
        proof
    }

    pub(super) fn restore(&self, medium: &mut TidewaterUnderwaterControl, lighting: &mut Control) {
        *medium = self.original_medium;
        *lighting = self.original_lighting;
    }

    pub(super) fn fail(&self) {
        if let Ok(captures) = self.captures.lock() {
            let _ = self.save("failed", &captures);
        }
    }

    pub(super) fn update(
        &mut self,
        commands: &mut Commands,
        signature: Result<Option<FreezeSignature>, String>,
        encoded: u64,
        lighting: &mut Control,
    ) -> Result<bool, String> {
        if signature? != Some(self.signature) {
            return Err("isolated shadow triplet changed its frozen GPU/camera inputs".into());
        }
        let captures = self.captures.lock().map_err(|_| "shadow receipt lock")?;
        if self.waiting {
            let Some(capture) = captures.get(self.next) else {
                return Ok(false);
            };
            if capture["valid"] != true {
                self.save("failed", &captures)?;
                return Err(
                    "isolated shadow screenshot failed its signature/control checks".into(),
                );
            }
            self.next += 1;
            self.waiting = false;
            self.baseline = encoded;
            if self.next == 3 {
                let repeated = captures[0]["sha256"] == captures[2]["sha256"]
                    && captures[0]["pixel_sha256"] == captures[2]["pixel_sha256"];
                let differs = captures[1]["pixel_delta"]["mean_abs_rgb"]
                    .as_f64()
                    .is_some_and(|mean| mean > 0.01)
                    && captures[1]["pixel_delta"]["pixels_delta_gt_2"]
                        .as_u64()
                        .is_some_and(|pixels| pixels >= 1000);
                self.save(
                    if repeated && differs {
                        "passed"
                    } else {
                        "failed"
                    },
                    &captures,
                )?;
                if !repeated || !differs {
                    return Err(format!(
                        "isolated shadow decoded pixels: repeat={repeated}, measurable_effect={differs}"
                    ));
                }
                return Ok(true);
            }
            lighting.shadow_refraction = self.next != 1;
            self.save("running", &captures)?;
            return Ok(false);
        }
        if encoded <= self.baseline + 40 {
            return Ok(false);
        }
        drop(captures);
        self.capture(commands);
        Ok(false)
    }

    fn capture(&mut self, commands: &mut Commands) {
        let phase = ["on", "off", "repeat"][self.next];
        let path = self.path.with_extension(format!("shadow.{phase}.png"));
        let expected = self.signature;
        let records = Arc::clone(&self.captures);
        let baseline_pixels = Arc::clone(&self.baseline_pixels);
        let baseline = self.baseline;
        commands.spawn(Screenshot::primary_window()).observe(
            move |event: On<ScreenshotCaptured>,
                  camera: Query<(&GlobalTransform, &Projection), With<ForgePlayerCamera>>,
                  live: Res<super::super::super::ocean::LiveClock>,
                  ocean: Res<super::super::super::ocean::Targets>,
                  spray: Res<super::super::super::spray::Targets>,
                  queries: Res<super::super::super::spray::water_queries::Targets>,
                  water: Res<super::super::super::water_time::WaterTime>,
                  underwater: Res<UnderwaterObservation>,
                  maps: Res<Targets>,
                  lighting: Res<Control>,
                  terrain: Res<Assets<forge_tidewater::tidewater_terrain::lod::LodMaterial>>,
                  village: Res<Assets<forge_tidewater::tidewater_material::VillageMaterial>>| {
                let actual = synchronized_signature(Stage::Neutral, &camera, &live, &ocean,
                    &spray, &queries, &water, &underwater, &maps).ok().flatten();
                let dimensions = [event.image.texture_descriptor.size.width,
                    event.image.texture_descriptor.size.height];
                let refracted = phase != "off";
                let terrain_params: Vec<_> = terrain.iter()
                    .filter(|(_, m)| m.base.underwater_map_frame == maps.bindings.frame)
                    .map(|(_, m)| [m.base.caustic_params.water_scattering.w,
                        m.base.caustic_params.sun_and_strength.w, m.base.caustic_params.water_absorption.w]).collect();
                let village_params: Vec<_> = village.iter()
                    .filter(|(_, m)| m.underwater_map_frame == maps.bindings.frame)
                    .map(|(_, m)| [m.underwater_params.water_scattering.w,
                        m.underwater_params.sun_and_strength.w, m.underwater_params.water_absorption.w]).collect();
                let expected_params = [1.0, 0.0, f32::from(refracted)];
                let controls_valid = underwater.effect_enabled && underwater.shafts_strength == 0.0
                    && lighting.enabled && lighting.caustics_strength == 0.0
                    && lighting.shadow_refraction == refracted
                    && !terrain_params.is_empty() && !village_params.is_empty()
                    && terrain_params.iter().chain(&village_params)
                        .all(|params| params.map(f32::to_bits) == expected_params.map(f32::to_bits));
                // Match save_to_disk's RGB8 conversion; encoded PNG inequality
                // can result from one quantized pixel and is not a visual proof.
                let pixels = event.image.clone().try_into_dynamic().ok()
                    .map(|image| image.to_rgb8().into_raw());
                let pixel_sha = pixels.as_ref()
                    .map(|rgb| forge_core::bytes_sha256(rgb).to_string());
                let mut pixel_delta = None;
                if let (Some(rgb), Ok(mut baseline)) = (pixels.as_ref(), baseline_pixels.lock()) {
                    if phase == "on" {
                        *baseline = Some(rgb.clone());
                    } else if let Some(first) = baseline.as_ref() {
                        pixel_delta = compare_rgb(first, rgb);
                    }
                }
                save_to_disk(&path)(event);
                let data = std::fs::read(&path).ok();
                let sha = data.as_ref().map(|bytes| forge_core::bytes_sha256(bytes).to_string());
                let valid = actual == Some(expected) && controls_valid && dimensions == [1920,1080]
                    && data.as_ref().is_some_and(|bytes| !bytes.is_empty())
                    && pixels.as_ref().is_some_and(|rgb| rgb.len() == 1920*1080*3)
                    && (phase == "on" || pixel_delta.is_some());
                if let Ok(mut captures) = records.lock() {
                    captures.push(serde_json::json!({ "phase": phase, "path": path,
                        "valid": valid, "sha256": sha, "signature": actual,
                        "pixel_sha256":pixel_sha, "pixel_delta":pixel_delta,
                        "dimensions": dimensions, "encoding_baseline": baseline,
                        "encoded_after": underwater.encoded_frames,
                        "medium_enabled":underwater.effect_enabled,"shafts_strength":underwater.shafts_strength,
                        "lighting_enabled":lighting.enabled,"caustics_strength":lighting.caustics_strength,
                        "shadow_refraction":lighting.shadow_refraction,
                        "terrain_receivers":terrain_params,"lite_receivers":village_params }));
                }
            },
        );
        self.waiting = true;
    }

    fn save(&self, status: &str, captures: &[serde_json::Value]) -> Result<(), String> {
        let receipt = serde_json::json!({ "schema":"forge.tidewater.native-refracted-shadow-triplet",
            "status":status,"scope":"Additional Neutral shadow-only triplet; all three original immersion pairs remain required; no source parity or temporal resolve verdict",
            "signature":self.signature,"captures":captures });
        std::fs::write(
            self.path.with_extension("shadow.json"),
            serde_json::to_vec_pretty(&receipt).map_err(|e| e.to_string())?,
        )
        .map_err(|e| e.to_string())
    }
}

fn compare_rgb(first: &[u8], other: &[u8]) -> Option<serde_json::Value> {
    if first.len() != other.len() || first.is_empty() || !first.len().is_multiple_of(3) {
        return None;
    }
    let channels = u32::try_from(first.len()).ok()?;
    let mut sum = 0_u32;
    let mut changed = 0_u32;
    let mut gt_two = 0_u32;
    let mut max_delta = 0_u8;
    for (a, b) in first
        .as_chunks::<3>()
        .0
        .iter()
        .zip(other.as_chunks::<3>().0)
    {
        let deltas = [
            a[0].abs_diff(b[0]),
            a[1].abs_diff(b[1]),
            a[2].abs_diff(b[2]),
        ];
        for delta in deltas {
            sum = sum.checked_add(u32::from(delta))?;
            max_delta = max_delta.max(delta);
        }
        changed += u32::from(deltas.iter().any(|delta| *delta > 0));
        gt_two += u32::from(deltas.iter().any(|delta| *delta > 2));
    }
    Some(
        serde_json::json!({ "mean_abs_rgb":f64::from(sum)/f64::from(channels),
        "changed_pixels":changed,"pixels_delta_gt_2":gt_two,"max_channel_delta":max_delta }),
    )
}
