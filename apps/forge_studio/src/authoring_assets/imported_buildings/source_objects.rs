//! Import adapter: source object metadata comes from verified packages, never UI presets.
mod binding;
use super::{
    beach_props_templates, boardwalk_templates, debris_templates, fish_props_templates,
    fish_stand_package, pier_template, plant_package, plaza_templates, terrain_package,
    village_props_templates,
};
use binding::SourcePose;
use forge_core::{ContentHash, Transform, UnitQuaternion, Vec3};
use forge_project::{
    TerrainDocument,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    zone_authoring::{ZoneAuthoringHead, placements::ManualPlacement},
};

pub(crate) fn source_placements(head: &ZoneAuthoringHead) -> Result<Vec<ManualPlacement>, String> {
    let area = head.base.terrain.area_id;
    let height_pin = plant_package()
        .map(|package| {
            binding::height_pin(
                package
                    .file("grass.json")
                    .ok_or("Description source de l’herbe absente")?,
            )
        })
        .transpose()?;
    let geometry = verified_geometry(height_pin);
    let inherit = binding::associated_dataset(
        head,
        height_pin,
        geometry.as_ref().map(|(source, extent)| (source, *extent)),
    )?;
    let mut result = Vec::new();
    let mut add =
        |key: String, name: String, asset_id, asset_hash, transform| -> Result<(), String> {
            let template = binding::placement(area, &key, name, asset_id, asset_hash, transform);
            if let Some(source) =
                binding::select_source(head, &template, inherit, SourcePose::PackageTransform)?
            {
                result.push(source);
            }
            Ok(())
        };
    for template in boardwalk_templates() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_boardwalk::package_hash(),
            template.transform,
        )?;
    }
    if let Some(template) = pier_template() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_pier::package_hash(),
            template.transform,
        )?;
    }
    for template in plaza_templates() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_plaza::package_hash(),
            template.transform,
        )?;
    }
    for template in village_props_templates() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_village_props::package_hash(),
            template.transform,
        )?;
    }
    for template in beach_props_templates() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_beach_props::package_hash(),
            template.transform,
        )?;
    }
    for template in debris_templates() {
        add(
            template.source_id,
            template.kind,
            template.asset_id,
            forge_assets::tidewater_debris::package_hash(),
            template.transform,
        )?;
    }
    for template in fish_props_templates() {
        add(
            template.source_id.clone(),
            template.source_id,
            template.asset_id,
            forge_assets::tidewater_fish_props::package_hash(),
            template.transform,
        )?;
    }
    if let Some(package) = fish_stand_package() {
        let [x, z, yaw] = package.source_pose();
        let (sin, cos) = (yaw * 0.5).sin_cos();
        let reference = package.reference();
        let mut template = binding::placement(
            area,
            "source",
            "Échoppe".into(),
            reference.asset_id,
            reference.bundle_hash,
            Transform {
                translation_m: Vec3::new(x, 0.0, z),
                rotation: UnitQuaternion::new(0.0, sin, 0.0, cos),
                scale: Vec3::ONE,
            },
        );
        if let Some(source) =
            binding::select_source(head, &template, false, SourcePose::TerrainHeight)?
        {
            result.push(source);
        } else if inherit {
            template.transform = crate::authoring_raster::align(
                &crate::authoring_scene::terrain(head)?,
                forge_world::placement_snap::PlacementSnap {
                    grid_step_m: None,
                    surface_offset_m: Some(0.0),
                },
                template.transform,
            )?;
            result.push(template);
        }
    }
    result.sort_by_key(|p| p.id);
    Ok(result)
}

fn verified_geometry(height_pin: Option<ContentHash>) -> Option<(SampledTerrainSource, [f32; 2])> {
    terrain_package()
        .zip(height_pin)
        .map(|(package, pin)| package_geometry(&package, pin))
}

fn package_geometry(
    package: &forge_assets::tidewater_terrain::TerrainPackage,
    pin: ContentHash,
) -> (SampledTerrainSource, [f32; 2]) {
    let geometry = package.raster_geometry();
    (
        SampledTerrainSource {
            schema: SAMPLED_TERRAIN_SCHEMA.into(),
            schema_version: 1,
            width: geometry.width,
            height: geometry.height,
            origin_xz_m: geometry.origin_xz_m,
            sample_spacing_m: geometry.sample_spacing_m,
            outside_height_m: geometry.outside_height_m,
            encoding: match geometry.encoding {
                forge_assets::tidewater_terrain::RasterEncoding::Float32Le => {
                    HeightEncoding::Float32Le
                }
            },
            samples_sha256: pin,
        },
        geometry.extent_m,
    )
}

/// Check the packages verified by the loader before publishing any projection.
/// A global library cannot supply another map's terrain textures/environment.
pub(super) fn verify_projection_source(
    document: &TerrainDocument,
    terrain: Option<&forge_assets::tidewater_terrain::TerrainPackage>,
    plants: Option<&forge_assets::tidewater_plants::PlantPackage>,
) -> Result<(), String> {
    let (terrain, plants) = terrain.zip(plants).ok_or(
        "Les ressources terrain et plantes vérifiées sont nécessaires pour afficher cette carte.",
    )?;
    let height_pin = binding::height_pin(
        plants
            .file("grass.json")
            .ok_or("La description vérifiée du relief de la bibliothèque est absente.")?,
    )?;
    let (source, extent) = package_geometry(terrain, height_pin);
    if !binding::associated_terrain(document, Some(height_pin), Some((&source, extent)))? {
        return Err("La bibliothèque vérifiée ne correspond pas au relief de cette carte.".into());
    }
    Ok(())
}
