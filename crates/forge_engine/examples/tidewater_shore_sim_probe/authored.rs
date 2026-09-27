//! Validated derived inputs, applied only after acquired fixture integrity checks.
use crate::terrain_edits;
use std::{collections::BTreeMap, error::Error};
pub struct AuthoredTerrain {
    corrections: terrain_edits::Corrections,
    field: Vec<u8>,
    height_hash: forge_core::ContentHash,
    document: forge_project::TerrainDocument,
}
impl AuthoredTerrain {
    #[allow(clippy::float_cmp)] // Exact pinned-domain contract, not an approximate spatial comparison.
    pub fn new(
        document: &forge_project::TerrainDocument,
        field: Vec<u8>,
    ) -> Result<Option<Self>, Box<dyn Error>> {
        document.validate()?;
        if document.height_deltas.is_empty() && document.height_sampling.base_elevation_m == 0.0 {
            return Ok(None);
        }
        let source = document
            .sampled_source
            .as_ref()
            .ok_or("sampled terrain required")?;
        if source.width != 2048
            || source.height != 2048
            || document.extent_m.size_x_m != 2048.0
            || document.extent_m.size_z_m != 2048.0
            || source.origin_xz_m != [-1024.0, -1024.0]
            || source.sample_spacing_m != 1.0
        {
            return Err("authored coast requires pinned Tidewater domain".into());
        }
        if field.len() != 512 * 512 * 16
            || field
                .as_chunks::<4>()
                .0
                .iter()
                .any(|v| !f32::from_le_bytes(*v).is_finite())
        {
            return Err("invalid authored shore field".into());
        }
        Ok(Some(Self {
            corrections: terrain_edits::Corrections::from_document(document)?,
            field,
            height_hash: source.samples_sha256,
            document: document.clone(),
        }))
    }
    pub fn stations(&self, heights: &[u8]) -> Result<Vec<[f32; 4]>, Box<dyn Error>> {
        let plan = forge_world::sampled_terrain::SampledTerrainPlan::from_document(
            &self.document,
            heights,
        )?;
        let result = forge_engine::breaker_geometry::beach_stations(|x, z| {
            plan.height_at(x, z)
                .expect("validated plan, bounded finite station coordinates")
        });
        if result.len() > 4096 {
            return Err("authored station limit exceeded".into());
        }
        Ok(result)
    }
    pub fn apply(
        &self,
        files: &mut BTreeMap<String, Vec<u8>>,
        shaders: &[&str],
    ) -> Result<(), Box<dyn Error>> {
        if forge_core::bytes_sha256(
            files
                .get("heights.f32.bin")
                .ok_or("simulation heights missing")?,
        ) != self.height_hash
        {
            return Err("simulation terrain differs from canonical source".into());
        }
        for name in shaders {
            let source = std::str::from_utf8(files.get(*name).ok_or("simulation shader missing")?)?;
            let adapted = self.corrections.shader_existing(source)?;
            files.insert((*name).into(), adapted.into_bytes());
        }
        files.insert("shore-field.rgba32.bin".into(), self.field.clone());
        files.insert(
            "authored-height-deltas.bin".into(),
            self.corrections.bytes.clone(),
        );
        eprintln!(
            "authored coupled terrain: field={}, deltas={}, elevation={}",
            forge_core::bytes_sha256(&self.field),
            forge_core::bytes_sha256(&self.corrections.bytes),
            self.corrections.elevation
        );
        Ok(())
    }
}
