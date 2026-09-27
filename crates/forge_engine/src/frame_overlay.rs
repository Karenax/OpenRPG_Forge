use forge_core::FrameDistribution;
use std::collections::VecDeque;

pub(crate) fn render_timing_text(diagnostics: &bevy::diagnostic::DiagnosticsStore) -> String {
    let format_time = |path| {
        crate::diagnostic_value(diagnostics, path).map_or_else(
            || "indisponible".to_owned(),
            |value| format!("{value:.3} ms"),
        )
    };
    format!(
        "Passe opaque, dernier releve : CPU {} | GPU {} (pas le temps total de frame)",
        format_time("render/main_opaque_pass_3d/elapsed_cpu"),
        format_time("render/main_opaque_pass_3d/elapsed_gpu"),
    )
}

/// The bundled diagnostic font is a subset. Keep this fallback at the presentation boundary;
/// source documents, serialized evidence and the Studio's browser typography stay untouched.
pub(crate) fn diagnostic_font_text(text: &str) -> String {
    text.chars()
        .map(|character| match character {
            'é' | 'è' | 'ê' | 'ë' => 'e',
            'É' | 'È' | 'Ê' | 'Ë' => 'E',
            'à' | 'â' | 'ä' => 'a',
            'À' | 'Â' | 'Ä' => 'A',
            'î' | 'ï' => 'i',
            'ô' | 'ö' => 'o',
            'ù' | 'û' | 'ü' => 'u',
            'ç' => 'c',
            '·' => '|',
            '–' | '—' => '-',
            '’' => '\'',
            value if value.is_ascii() => value,
            _ => '?',
        })
        .collect()
}

/// Bounded live diagnostic, independent from the qualification sample collector.
#[derive(Default)]
pub(crate) struct FrameOverlay {
    samples: VecDeque<f64>,
    since_refresh: usize,
    text: String,
}

impl FrameOverlay {
    pub(crate) fn record(&mut self, milliseconds: f64) -> &str {
        if milliseconds.is_finite() && milliseconds > 0.0 {
            if self.samples.len() == 240 {
                self.samples.pop_front();
            }
            self.samples.push_back(milliseconds);
            self.since_refresh += 1;
            if self.since_refresh == 30 {
                self.since_refresh = 0;
                let samples: Vec<_> = self.samples.iter().copied().collect();
                if let Ok(stats) = FrameDistribution::from_samples_ms(&samples) {
                    let h = stats.histogram;
                    self.text = format!(
                        "Cadence glissante ({} images, non benchmark) : moyenne {:.2} ms · p95 {:.2} ms · 1 % low {:.1} FPS\nHistogramme : <=16,7 ms {} | 16,7–33,3 ms {} | >33,3 ms {}",
                        stats.samples,
                        stats.mean_ms,
                        stats.p95_ms,
                        stats.one_percent_low_fps,
                        h.frames_at_or_below_16_7_ms,
                        h.frames_over_16_7_ms_at_or_below_33_3_ms,
                        h.frames_over_33_3_ms,
                    );
                }
            }
        }
        if self.text.is_empty() {
            "Cadence glissante : collecte des 30 premières images"
        } else {
            &self.text
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn unavailable_gpu_is_not_presented_as_zero_or_as_frame_time() {
        use bevy::diagnostic::{
            Diagnostic, DiagnosticMeasurement, DiagnosticPath, DiagnosticsStore,
        };
        let mut diagnostics = DiagnosticsStore::default();
        let mut cpu = Diagnostic::new(DiagnosticPath::new(
            "render/main_opaque_pass_3d/elapsed_cpu",
        ));
        cpu.add_measurement(DiagnosticMeasurement {
            time: bevy::platform::time::Instant::now(),
            value: 0.125,
        });
        diagnostics.add(cpu);
        let text = render_timing_text(&diagnostics);
        assert!(text.contains("CPU 0.125 ms | GPU indisponible"));
        assert!(text.contains("pas le temps total de frame"));
        let mut gpu = Diagnostic::new(DiagnosticPath::new(
            "render/main_opaque_pass_3d/elapsed_gpu",
        ));
        gpu.add_measurement(DiagnosticMeasurement {
            time: bevy::platform::time::Instant::now(),
            value: 1.25,
        });
        diagnostics.add(gpu);
        assert!(render_timing_text(&diagnostics).contains("CPU 0.125 ms | GPU 1.250 ms"));
    }

    #[test]
    fn fallback_preserves_numbers_and_lines_without_missing_glyphs() {
        let text = "Météo · caméra : 27 entités\n16,7–33,3 ms | p95 9.87 | 1 % low 49.9";
        assert_eq!(
            diagnostic_font_text(text),
            "Meteo | camera : 27 entites\n16,7-33,3 ms | p95 9.87 | 1 % low 49.9"
        );
        assert!(diagnostic_font_text("Éclairage à l’ombre Ω").is_ascii());
        assert_eq!(diagnostic_font_text("18bd933d37f06a6a"), "18bd933d37f06a6a");
    }

    #[test]
    fn distribution_is_bounded_and_old_hitches_expire() {
        let mut overlay = FrameOverlay::default();
        for _ in 0..30 {
            overlay.record(40.0);
        }
        assert!(overlay.text.contains(">33,3 ms 30"));
        for _ in 0..240 {
            overlay.record(10.0);
        }
        assert_eq!(overlay.samples.len(), 240);
        assert!(
            overlay
                .text
                .contains("moyenne 10.00 ms · p95 10.00 ms · 1 % low 100.0 FPS")
        );
        assert!(
            overlay
                .text
                .contains("<=16,7 ms 240 | 16,7–33,3 ms 0 | >33,3 ms 0")
        );
    }

    #[test]
    fn invalid_samples_do_not_poison_or_refresh_the_display() {
        let mut overlay = FrameOverlay::default();
        for _ in 0..30 {
            overlay.record(20.0);
        }
        let before = overlay.text.clone();
        for value in [0.0, -1.0, f64::NAN, f64::INFINITY] {
            overlay.record(value);
        }
        assert_eq!(overlay.text, before);
        assert_eq!(overlay.samples.len(), 30);
        assert_eq!(overlay.since_refresh, 0);
    }
}
