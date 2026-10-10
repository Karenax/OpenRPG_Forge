//! Device-scoped sRGB thresholds from native GPU boundary witnesses.
//! This structural contract does not measure the GPU or qualify a material.
#[derive(Clone, Debug, PartialEq, Eq)]
pub(crate) struct NativeSrgbDeviceKey {
    pub backend: String,
    pub adapter: String,
    pub vendor: u32,
    pub device: u32,
    pub driver: String,
}
#[derive(Clone, Copy, Debug)]
pub(crate) struct NativeSrgbBoundaryWitness {
    pub code: u16,
    pub lower_bits: u32,
    pub threshold_bits: u32,
    pub lower_observed_code: u8,
    pub threshold_observed_code: u8,
}
#[derive(Clone, Debug)]
pub(crate) struct NativeSrgbCurve {
    device: NativeSrgbDeviceKey,
    threshold_bits: [u32; 256],
}
impl NativeSrgbCurve {
    /// Call only with witnesses returned by the native renderer's probe pass.
    /// Purchased shader metadata is not a source of device calibration.
    pub(crate) fn from_native_witnesses(
        device: NativeSrgbDeviceKey,
        witnesses: &[NativeSrgbBoundaryWitness],
    ) -> Result<Self, &'static str> {
        // WebGPU deliberately omits the driver and can hide adapter metadata.
        // Runtime bindings additionally require exact wgpu::Device equality;
        // metadata never admits a cached binding from another browser device.
        if device.backend.is_empty()
            || (device.backend != "BrowserWebGpu"
                && (device.adapter.is_empty() || device.driver.is_empty()))
        {
            return Err("Native sRGB device identity is incomplete");
        }
        if witnesses.len() != 255 {
            return Err("Native sRGB requires all 255 boundaries");
        }
        let mut bits = [0u32; 256];
        for (index, witness) in witnesses.iter().enumerate() {
            let code = index + 1;
            if witness.code as usize != code
                || witness.lower_observed_code as usize != code - 1
                || witness.threshold_observed_code as usize != code
                || witness.threshold_bits.checked_sub(witness.lower_bits) != Some(1)
            {
                return Err("Native sRGB boundary witness is inconsistent");
            }
            let lower = f32::from_bits(witness.lower_bits);
            let threshold = f32::from_bits(witness.threshold_bits);
            if !lower.is_finite()
                || !threshold.is_finite()
                || lower < 0.0
                || threshold <= 0.0
                || threshold > 1.0
                || witness.threshold_bits <= bits[index]
            {
                return Err("Native sRGB thresholds are invalid or non-monotonic");
            }
            bits[code] = witness.threshold_bits;
        }
        Ok(Self {
            device,
            threshold_bits: bits,
        })
    }
    /// A cache for another backend, adapter or driver must never be installed.
    pub(crate) fn r32f_bytes_for_device(
        &self,
        current: &NativeSrgbDeviceKey,
    ) -> Result<Vec<u8>, &'static str> {
        if current != &self.device {
            return Err("Native sRGB curve belongs to another device epoch");
        }
        Ok(self
            .threshold_bits
            .iter()
            .flat_map(|bits| bits.to_le_bytes())
            .collect())
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    fn key() -> NativeSrgbDeviceKey {
        NativeSrgbDeviceKey {
            backend: "test-native".into(),
            adapter: "synthetic".into(),
            vendor: 1,
            device: 2,
            driver: "fixture".into(),
        }
    }
    fn witnesses() -> Vec<NativeSrgbBoundaryWitness> {
        (1..=255u16)
            .map(|code| {
                let bits = (f32::from(code) / 256.0).to_bits();
                NativeSrgbBoundaryWitness {
                    code,
                    lower_bits: bits - 1,
                    threshold_bits: bits,
                    lower_observed_code: u8::try_from(code - 1)
                        .expect("Witness code is at most 255"),
                    threshold_observed_code: u8::try_from(code)
                        .expect("Witness code is at most 255"),
                }
            })
            .collect()
    }
    #[test]
    fn complete_curve_preserves_exact_float32_bits() {
        let rows = witnesses();
        let curve = NativeSrgbCurve::from_native_witnesses(key(), &rows).unwrap();
        let bytes = curve.r32f_bytes_for_device(&key()).unwrap();
        assert_eq!(bytes.len(), 1024);
        assert_eq!(&bytes[0..4], &0u32.to_le_bytes());
        for (i, row) in rows.iter().enumerate() {
            assert_eq!(
                &bytes[(i + 1) * 4..(i + 2) * 4],
                &row.threshold_bits.to_le_bytes()
            );
        }
    }
    #[test]
    fn other_device_backend_or_driver_is_refused() {
        let curve = NativeSrgbCurve::from_native_witnesses(key(), &witnesses()).unwrap();
        for changed in [
            NativeSrgbDeviceKey {
                backend: "other".into(),
                ..key()
            },
            NativeSrgbDeviceKey { device: 3, ..key() },
            NativeSrgbDeviceKey {
                driver: "updated".into(),
                ..key()
            },
            NativeSrgbDeviceKey {
                adapter: "other".into(),
                ..key()
            },
        ] {
            assert!(curve.r32f_bytes_for_device(&changed).is_err());
        }
    }
    #[test]
    fn incomplete_identity_and_partial_or_duplicate_boundaries_are_refused() {
        assert!(
            NativeSrgbCurve::from_native_witnesses(
                NativeSrgbDeviceKey {
                    driver: String::new(),
                    ..key()
                },
                &witnesses()
            )
            .is_err()
        );
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &witnesses()[..254]).is_err());
        let mut rows = witnesses();
        rows[100] = rows[99];
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &rows).is_err());
    }
    #[test]
    fn browser_privacy_metadata_keeps_witness_and_device_key_guards() {
        let browser = NativeSrgbDeviceKey {
            backend: "BrowserWebGpu".into(),
            adapter: String::new(),
            driver: String::new(),
            ..key()
        };
        let curve = NativeSrgbCurve::from_native_witnesses(browser.clone(), &witnesses()).unwrap();
        assert_eq!(curve.r32f_bytes_for_device(&browser).unwrap().len(), 1024);
        assert!(curve.r32f_bytes_for_device(&key()).is_err());
        let mut rows = witnesses();
        rows[0].threshold_observed_code = 0;
        assert!(NativeSrgbCurve::from_native_witnesses(browser, &rows).is_err());
    }
    #[test]
    fn contradictory_or_nonadjacent_witness_is_refused() {
        let mut rows = witnesses();
        rows[5].lower_observed_code = 6;
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &rows).is_err());
        let mut rows = witnesses();
        rows[5].lower_bits -= 1;
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &rows).is_err());
    }
    #[test]
    fn nonfinite_and_nonmonotonic_curve_is_refused() {
        let mut rows = witnesses();
        rows[5].threshold_bits = f32::NAN.to_bits();
        rows[5].lower_bits = rows[5].threshold_bits - 1;
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &rows).is_err());
        let mut rows = witnesses();
        rows[5].threshold_bits = rows[4].threshold_bits;
        rows[5].lower_bits = rows[5].threshold_bits - 1;
        assert!(NativeSrgbCurve::from_native_witnesses(key(), &rows).is_err());
    }
}
