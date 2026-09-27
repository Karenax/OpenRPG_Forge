//! CPU normal/AO rebake preserves source channels and rejects malformed targets.
use bevy::prelude::*;
use forge_engine::tidewater_terrain::normals::{self, Shared};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use forge_world::sampled_terrain::SampledTerrainPlan;
use std::{
    error::Error,
    fs,
    sync::{Arc, atomic::Ordering},
};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("TERRAIN_JSON HEIGHTS_BIN NORMAL_BIN required".into());
    }
    let mut document = TerrainDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let source = fs::read(&args[1])?;
    let normal = fs::read(&args[2])?;
    let original = SampledTerrainPlan::from_document(&document, &source)?;
    let mut baseline = normal.clone();
    normals::update_base(&original, &mut baseline)?;
    document.height_deltas = vec![TerrainHeightDelta::new(1024, 1024, 12.0)];
    let edited = SampledTerrainPlan::from_document(&document, &source)?;
    let mut raised = normal.clone();
    normals::update_base(&edited, &mut raised)?;
    assert_ne!(&baseline[..2048 * 2048 * 4], &raised[..2048 * 2048 * 4]);
    assert_eq!(&normal[2048 * 2048 * 4..], &raised[2048 * 2048 * 4..]);
    for (before, after) in normal
        .as_chunks::<4>()
        .0
        .iter()
        .zip(raised.as_chunks::<4>().0)
    {
        assert_eq!(before[2], after[2], "rock mask changed");
    }
    let mut restored = normal.clone();
    normals::update_base(&original, &mut restored)?;
    assert_eq!(restored, baseline);
    let mut invalid = normal[..normal.len() - 1].to_vec();
    let before = invalid.clone();
    assert!(normals::update_base(&edited, &mut invalid).is_err());
    assert_eq!(invalid, before);
    let mut images = Assets::<Image>::default();
    assert!(Shared::stage(&invalid, true, &mut images).is_err());
    assert!(images.is_empty());
    let a = Shared::stage(&raised, true, &mut images)?;
    let b = Shared::stage(&restored, true, &mut images)?;
    assert_ne!(a.image, b.image);
    assert!(!Arc::ptr_eq(&a.ready, &b.ready));
    a.ready.store(true, Ordering::Release);
    assert!(!b.ready.load(Ordering::Acquire));
    assert!(normals::register(&mut App::new()).is_err());
    println!(
        "NORMAL_CPU_PASS baseline={} edited={} restored={}",
        forge_core::bytes_sha256(&baseline),
        forge_core::bytes_sha256(&raised),
        forge_core::bytes_sha256(&restored)
    );
    Ok(())
}
