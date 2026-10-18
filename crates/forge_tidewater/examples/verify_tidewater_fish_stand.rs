//! Verify the additive `FishStand` Bevy input without a renderer or GPU.
use forge_tidewater::tidewater_material::SceneLighting;
use forge_tidewater::tidewater_stall::{FishStandMaterial, FishStandPrepared, LAYER, UV2};
use serde::Deserialize;
use std::{collections::BTreeMap, error::Error, fs, path::PathBuf};

#[derive(Deserialize)]
struct SourceLighting {
    sun_direction: [f32; 3],
    sun_color: [f32; 3],
    environment_sh: [[f32; 3]; 9],
}

fn main() -> Result<(), Box<dyn Error>> {
    let root = PathBuf::from(
        std::env::args()
            .nth(1)
            .ok_or("fish-stand directory required")?,
    )
    .canonicalize()?;
    let descriptor = fs::read(root.join("fish-stand.json"))?;
    let value: serde_json::Value = serde_json::from_slice(&descriptor)?;
    let mut files = BTreeMap::new();
    files.insert("fish-stand.json".into(), descriptor);
    for payload in value["geometry"]["attributes"]
        .as_object()
        .ok_or("attributes")?
        .values()
        .chain(std::iter::once(&value["geometry"]["indices"]))
    {
        let name = payload["file"].as_str().ok_or("payload file")?;
        files.insert(name.into(), fs::read(root.join(name))?);
    }
    for texture in value["textures"].as_object().ok_or("textures")?.values() {
        let name = texture["file"].as_str().ok_or("texture file")?;
        files.insert(name.into(), fs::read(root.join(name))?);
    }
    let prepared = FishStandPrepared::load(&files)?;
    assert_eq!(prepared.mesh.count_vertices(), 32_333);
    assert!(prepared.mesh.attribute(LAYER).is_some());
    assert!(prepared.mesh.attribute(UV2).is_some());
    assert_eq!(prepared.textures.len(), 7);
    let mut meshes = bevy::asset::Assets::default();
    let mut images = bevy::asset::Assets::default();
    let resident = prepared.stage(&mut meshes, &mut images);
    let lighting_path = root
        .parent()
        .ok_or("FishStand bundle parent required")?
        .join("scene/village-lighting.json");
    let source: SourceLighting = serde_json::from_slice(&fs::read(lighting_path)?)?;
    let lighting = SceneLighting::new(
        source.sun_direction,
        source.sun_color,
        source.environment_sh,
    )?;
    let _material = FishStandMaterial::from_resident(&resident, lighting)?;
    assert_eq!(resident.placement.x.to_bits(), 49.9_f32.to_bits());
    assert_eq!(resident.placement.z.to_bits(), (-74.6_f32).to_bits());
    assert_eq!(resident.placement.yaw_radians.to_bits(), 1.45_f32.to_bits());
    println!(
        "FISH_STAND_BEVY_INPUT_OK vertices=32333 triangles=40259 arrays=7 source_wgsl_unported={}",
        resident.limitations.len()
    );
    Ok(())
}
