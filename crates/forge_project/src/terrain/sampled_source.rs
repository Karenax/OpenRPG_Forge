use super::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument,
    TerrainDocumentError,
};

impl TerrainDocument {
    pub(super) fn validate_sampled_source(&self) -> Result<(), TerrainDocumentError> {
        let fail = |reason: &str| TerrainDocumentError::SampledSource(reason.into());
        match (&self.sampled_source, self.height_sampling.algorithm) {
            (None, HeightSamplingAlgorithm::FractalValueNoiseV1) => Ok(()),
            (Some(source), HeightSamplingAlgorithm::SampledRasterV1) => {
                if self.schema_version != TERRAIN_DOCUMENT_SCHEMA_VERSION_V4 {
                    return Err(fail("raster sources require version 4"));
                }
                source
                    .validate()
                    .map_err(|error| fail(&error.to_string()))?;
                let parameters = self.height_sampling;
                if parameters.amplitude_m.to_bits() != 0.0_f32.to_bits()
                    || parameters.base_frequency_cycles_per_m.to_bits() != 1.0_f32.to_bits()
                    || parameters.octaves != 1
                    || parameters.lacunarity.to_bits() != 2.0_f32.to_bits()
                    || parameters.persistence.to_bits() != 0.5_f32.to_bits()
                {
                    return Err(fail(
                        "unused noise parameters must use raster defaults (0, 1, 1, 2, 0.5)",
                    ));
                }
                // The authored correction lattice stays at the area's minimum
                // corner. The immutable raster itself uses cell-centred samples.
                // Reject silent rescaling, translation or partial coverage.
                let spacing = f64::from(self.height_sampling.sample_spacing_m);
                let extent = [
                    f64::from(self.extent_m.size_x_m),
                    f64::from(self.extent_m.size_z_m),
                ];
                if source.sample_spacing_m.to_bits() != spacing.to_bits()
                    || (f64::from(source.width) * spacing).to_bits() != extent[0].to_bits()
                    || (f64::from(source.height) * spacing).to_bits() != extent[1].to_bits()
                    || source.origin_xz_m[0].to_bits() != (-extent[0] * 0.5).to_bits()
                    || source.origin_xz_m[1].to_bits() != (-extent[1] * 0.5).to_bits()
                {
                    return Err(fail(
                        "raster domain must match the centred area and authored spacing",
                    ));
                }
                Ok(())
            }
            _ => Err(fail(
                "raster algorithm and source must be selected together",
            )),
        }
    }
}
