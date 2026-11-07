//! Admitted grass fields retain the immutable source raster and authored corner deltas separately.
use super::{GrassMaterial, Grid};
use crate::{
    sampled_terrain_corrections::Corrections, source_vegetation::world::WorldPlantMaterial,
};
use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::{
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        storage::ShaderBuffer,
    },
};
use forge_assets::{
    source_vegetation_geometry::SourceVegetationGeometry, source_vegetation_package::PlantPackage,
};
use forge_core::{ContentHash, canonical_json_sha256};
use forge_world::{sampled_terrain::SampledTerrainPlan, terrain_lod_bounds::RasterLodBounds};

pub struct PreparedFields {
    pub identity: ContentHash,
    pub grid: Grid,
    pub meshes: [Mesh; 3],
    pub heights: Image,
    pub deltas: Image,
    pub uniform: Vec4,
}

impl PreparedFields {
    #[allow(clippy::cast_possible_truncation)] // Admitted finite source-profile positions become explicit renderer f32 values.
    pub fn new(
        package: &PlantPackage,
        meshes: Vec<SourceVegetationGeometry>,
        plan: &SampledTerrainPlan,
    ) -> Result<Self, String> {
        let decoded = package.grass_from_geometry(meshes)?;
        let source = plan.raster().source();
        if decoded.source_height != Some(source.samples_sha256)
            || source.width != 2048
            || source.height != 2048
            || source.sample_spacing_m.to_bits() != 1.0_f64.to_bits()
        {
            return Err(
                "Grass source-profile fields do not match the admitted terrain raster".into(),
            );
        }
        let bounds = RasterLodBounds::from_plan(plan).map_err(|e| e.to_string())?;
        let mut min_y = Vec::with_capacity(256 * 256);
        let mut max_y = Vec::with_capacity(256 * 256);
        for z in 0_u16..256 {
            for x in 0_u16..256 {
                let [lo, hi] = bounds
                    .bounds_for(
                        [
                            source.origin_xz_m[0] + f64::from(x) * 8.0,
                            source.origin_xz_m[1] + f64::from(z) * 8.0,
                        ],
                        8.0,
                    )
                    .map_err(|e| e.to_string())?;
                min_y.push(lo as f32);
                max_y.push(hi as f32);
            }
        }
        let grid = Grid::new(decoded.flags, min_y, max_y)?.with_origin(source.origin_xz_m)?;
        let (heights, deltas, uniform) = raster_fields(plan)?;
        let identity = canonical_json_sha256(&(
            "forge.source-grass-fields.v1",
            package.manifest_sha256(),
            plan.document_hash(),
        ))
        .map_err(|e| e.to_string())?;
        Ok(Self {
            identity,
            grid,
            meshes: decoded
                .meshes
                .map(crate::source_vegetation::scene::mesh_from_geometry),
            heights,
            deltas,
            uniform,
        })
    }
}

#[allow(clippy::cast_possible_truncation)] // The admitted source profile is explicitly checked before conversion to shader values.
fn raster_fields(plan: &SampledTerrainPlan) -> Result<(Image, Image, Vec4), String> {
    let source = plan.raster().source();
    let corrections = Corrections::from_document(plan.document())?;
    let uniform = Vec4::new(
        corrections.elevation,
        source.outside_height_m,
        source.origin_xz_m[0] as f32,
        source.origin_xz_m[1] as f32,
    );
    if !uniform.is_finite() {
        return Err("Grass field uniform exceeds renderer precision".into());
    }
    Ok((
        float_image(
            source.width,
            source.height,
            plan.raster()
                .heights()
                .iter()
                .copied()
                .flat_map(f32::to_le_bytes)
                .collect(),
        ),
        float_image(corrections.width, corrections.height, corrections.bytes),
        uniform,
    ))
}

fn float_image(width: u32, height: u32, bytes: Vec<u8>) -> Image {
    Image::new(
        Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        bytes,
        TextureFormat::R32Float,
        RenderAssetUsages::RENDER_WORLD,
    )
}

impl GrassMaterial for WorldPlantMaterial {
    fn validate_grass(&self, world: &World) -> Result<(), String> {
        let images = world
            .get_resource::<Assets<Image>>()
            .ok_or("Grass images unavailable")?;
        let image = |handle: &Option<Handle<Image>>, side: u32, format| {
            handle
                .as_ref()
                .and_then(|h| images.get(h))
                .is_some_and(|image| {
                    let d = &image.texture_descriptor;
                    d.dimension == TextureDimension::D2
                        && d.size
                            == Extent3d {
                                width: side,
                                height: side,
                                depth_or_array_layers: 1,
                            }
                        && d.format == format
                        && d.mip_level_count == 1
                })
        };
        if !self.grass_field.is_finite()
            || !image(&self.grass_heights, 2048, TextureFormat::R32Float)
            || !image(&self.grass_mask, 1024, TextureFormat::Rgba8Unorm)
            || !(image(&self.grass_deltas, 1, TextureFormat::R32Float)
                || image(&self.grass_deltas, 2049, TextureFormat::R32Float))
        {
            return Err(
                "World grass requires its admitted height, mask and corner delta fields".into(),
            );
        }
        Ok(())
    }
    fn grass_buffers(&self) -> (&Handle<ShaderBuffer>, &Handle<ShaderBuffer>) {
        (&self.instances, &self.models)
    }
    fn same_grass_inputs(&self, other: &Self) -> bool {
        self.detail == other.detail
            && self.frame == other.frame
            && self.grass_heights == other.grass_heights
            && self.grass_mask == other.grass_mask
            && self.grass_deltas == other.grass_deltas
            && self.grass_field.to_array().map(f32::to_bits)
                == other.grass_field.to_array().map(f32::to_bits)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_vegetation::Params;
    use forge_project::{
        HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainExtent,
        TerrainHeightDelta,
        sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    };

    fn material() -> WorldPlantMaterial {
        WorldPlantMaterial {
            params: Params {
                i_pos: Vec4::ZERO,
                i_dat: Vec4::ZERO,
                wind: Vec4::ZERO,
                gust_lod: Vec4::ZERO,
                sun_night: Vec4::ZERO,
                lod_end_frame: Vec4::ZERO,
            },
            detail: default(),
            leaf: default(),
            impostor_a: default(),
            impostor_b: default(),
            instances: default(),
            models: default(),
            frame: default(),
            kind: 0,
            grass_heights: None,
            grass_mask: None,
            grass_deltas: None,
            grass_field: Vec4::ZERO,
            scene_lighting: Handle::default(),
            live_shadows: Handle::default(),
            local_lights: crate::source_lighting::local_lights::gpu::BUFFER,
            environment: None,
            hill_shadow: None,
            object_shadow: None,
            object_settings: crate::source_shadows::ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            source_lighting: false,
            hill_domain: bevy::prelude::Vec4::new(-1024.0, -1024.0, 2048.0, 0.0),
        }
    }

    #[test]
    fn world_grass_refuses_missing_and_neutral_height_fields_before_staging() {
        let mut world = World::new();
        world.init_resource::<Assets<Image>>();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<WorldPlantMaterial>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        let mut material = material();
        assert!(material.validate_grass(&world).is_err());
        material.grass_heights = Some(world.resource_mut::<Assets<Image>>().add(float_image(
            1,
            1,
            vec![0; 4],
        )));
        material.grass_mask = Some(
            world
                .resource_mut::<Assets<Image>>()
                .add(crate::source_vegetation::textures::mask(&vec![0; 1024 * 1024 * 4]).unwrap()),
        );
        material.grass_deltas = Some(world.resource_mut::<Assets<Image>>().add(float_image(
            1,
            1,
            vec![0; 4],
        )));
        let meshes = std::array::from_fn(|_| {
            Mesh::new(
                bevy::mesh::PrimitiveTopology::TriangleList,
                RenderAssetUsages::default(),
            )
        });
        assert!(super::super::Resident::stage(&mut world, &material, meshes).is_err());
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<WorldPlantMaterial>>().len(), 0);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), 0);
        material.grass_heights = Some(world.resource_mut::<Assets<Image>>().add(float_image(
            2048,
            2048,
            vec![0; 2048 * 2048 * 4],
        )));
        assert!(material.validate_grass(&world).is_ok());
        let meshes = std::array::from_fn(|_| {
            Mesh::new(
                bevy::mesh::PrimitiveTopology::TriangleList,
                RenderAssetUsages::default(),
            )
        });
        let resident = super::super::Resident::stage(&mut world, &material, meshes).unwrap();
        assert!(resident.intact(&world));
        let handles = resident.materials();
        world
            .resource_mut::<Assets<WorldPlantMaterial>>()
            .get_mut(&handles[0])
            .unwrap()
            .grass_mask = None;
        assert!(!resident.intact(&world));
    }

    #[test]
    fn fields_keep_original_centres_separate_from_exact_authored_corner_deltas() {
        let bytes = vec![0; 2048 * 2048 * 4];
        let mut doc = forge_world::embedded_visual_valley_terrain_document().unwrap();
        doc.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
        doc.extent_m = TerrainExtent::new(2048.0, 2048.0);
        doc.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
        doc.height_sampling.sample_spacing_m = 1.0;
        doc.height_sampling.base_elevation_m = 3.0;
        doc.height_sampling.amplitude_m = 0.0;
        doc.height_sampling.octaves = 1;
        doc.height_sampling.base_frequency_cycles_per_m = 1.0;
        doc.height_sampling.lacunarity = 2.0;
        doc.height_sampling.persistence = 0.5;
        doc.sampled_source = Some(SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: 2048,
            height: 2048,
            origin_xz_m: [-1024.0; 2],
            sample_spacing_m: 1.0,
            outside_height_m: -90.0,
            encoding: HeightEncoding::Float32Le,
            samples_sha256: forge_core::bytes_sha256(&bytes),
        });
        doc.height_deltas = vec![TerrainHeightDelta::new(128, 128, 4.0)];
        let plan = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
        let (heights, deltas, uniform) = raster_fields(&plan).unwrap();
        assert_eq!(heights.data.as_ref().unwrap(), &bytes);
        let offset = (128 * 2049 + 128) * 4;
        assert_eq!(
            &deltas.data.as_ref().unwrap()[offset..offset + 4],
            &4.0_f32.to_le_bytes()
        );
        assert_eq!(deltas.texture_descriptor.size.width, 2049);
        assert_eq!(uniform.x.to_bits(), 3.0_f32.to_bits());
        assert_eq!(
            plan.height_at(-896.0, -896.0).unwrap().to_bits(),
            7.0_f64.to_bits()
        );
        assert_ne!(
            plan.cell_center_heights()[128 * 2048 + 128].to_bits(),
            7.0_f32.to_bits()
        );
    }
}
