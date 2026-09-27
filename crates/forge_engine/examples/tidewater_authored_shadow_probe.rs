//! CPU contract checks for authored terrain shadows; no GPU or source mutation.
use bevy::math::{DMat4, DQuat, DVec3};
use forge_engine::{
    sun_cascades::CascadeView, tidewater_object_shadow::terrain::authored,
    tidewater_terrain_edits::Corrections,
};
use forge_project::{TerrainDocument, TerrainHeightDelta};
use std::{error::Error, fs};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 3 {
        return Err("TERRAIN_JSON HEIGHTS_BIN SHADER_WGSL required".into());
    }
    let doc = TerrainDocument::from_json(&fs::read_to_string(&args[0])?)?;
    let heights = fs::read(&args[1])?;
    let shader = fs::read_to_string(&args[2])?;
    let view = CascadeView {
        world_from_view: DMat4::from_rotation_translation(
            DQuat::from_array([
                0.0,
                f64::from(0.995_004_2_f32),
                f64::from(0.099_833_414_f32),
                0.0,
            ]),
            DVec3::new(42.0, 14.0, -90.0),
        ),
        near: 0.06,
        fov_degrees: 62.0,
        aspect: 1920.0 / 1080.0,
    };
    cache_roundtrip(&doc, &heights, &shader, view)?;
    let before = authored::prepare(&doc, heights.clone(), &shader, view)?;
    assert!(before.count > 0);
    assert_eq!(before.edit_side, 1);
    let mut edited = doc.clone();
    edited.height_deltas = vec![TerrainHeightDelta::new(2048, 2048, 12.0)];
    let after = authored::prepare(&edited, heights.clone(), &shader, view)?;
    assert_eq!(after.edit_side, 2049);
    assert_eq!(after.heights, before.heights);
    assert_eq!(after.edits.len(), 2049 * 2049 * 4);
    assert_eq!(
        &after.edits[after.edits.len() - 4..],
        &12.0_f32.to_le_bytes()
    );
    assert!(after.edits[..after.edits.len() - 4].iter().all(|b| *b == 0));
    edited.height_deltas = vec![TerrainHeightDelta::new(2049, 2048, 12.0)];
    assert!(Corrections::from_document(&edited).is_err());
    edited.height_deltas = vec![TerrainHeightDelta::new(0, 0, f32::NAN)];
    assert!(Corrections::from_document(&edited).is_err());
    let invalid = CascadeView {
        aspect: 0.0,
        ..view
    };
    assert!(authored::prepare(&doc, heights.clone(), &shader, invalid).is_err());
    assert!(authored::prepare(&doc, heights.clone(), "", view).is_err());
    let replay = authored::prepare(&doc, heights, &shader, view)?;
    assert_eq!(before.nodes, replay.nodes);
    assert_eq!(before.edits, replay.edits);
    assert_eq!(before.shader, replay.shader);
    println!(
        "AUTHORED_TERRAIN_SHADOW_PASS nodes={} nodes_sha256={} shader_sha256={}",
        before.count,
        forge_core::bytes_sha256(&before.nodes),
        forge_core::bytes_sha256(before.shader.as_bytes())
    );
    Ok(())
}

fn same(
    left: &forge_engine::tidewater_object_shadow::terrain::Input,
    right: &forge_engine::tidewater_object_shadow::terrain::Input,
) {
    assert_eq!(left.heights, right.heights);
    assert_eq!(left.edits, right.edits);
    assert_eq!(left.edit_side, right.edit_side);
    assert_eq!(left.nodes, right.nodes);
    assert_eq!(left.count, right.count);
    assert_eq!(left.shader, right.shader);
    assert_eq!(left.view_position, right.view_position);
    assert_eq!(left.morph, right.morph);
}
fn cache_roundtrip(
    doc: &TerrainDocument,
    heights: &[u8],
    shader: &str,
    view: CascadeView,
) -> Result<(), Box<dyn Error>> {
    use std::time::{Duration, Instant};
    let start = Instant::now();
    let cached = authored::PreparedSource::new(doc, heights.to_vec(), shader)?;
    let build = start.elapsed();
    let initial = cached.at(view)?;
    let mut cold = Duration::ZERO;
    let mut hot = Duration::ZERO;
    for i in 0..8 {
        let mut moved = view;
        moved.world_from_view.w_axis.x += f64::from(i) * 8.0;
        moved.world_from_view.w_axis.z += f64::from(i) * 4.0;
        let start = Instant::now();
        let reference = authored::prepare(doc, heights.to_vec(), shader, moved)?;
        cold += start.elapsed();
        let start = Instant::now();
        let selected = cached.at(moved)?;
        hot += start.elapsed();
        same(&reference, &selected);
    }
    let mut far = view;
    far.world_from_view.w_axis.x = 100_000.0;
    let outside = cached.at(far)?;
    assert_eq!(outside.count, 0);
    assert!(outside.nodes.is_empty());
    same(&initial, &cached.at(view)?);
    let mut edited = doc.clone();
    edited.height_deltas = vec![TerrainHeightDelta::new(1024, 1024, 12.0)];
    let replacement = authored::PreparedSource::new(&edited, heights.to_vec(), shader)?;
    assert_ne!(replacement.document_hash, cached.document_hash);
    assert_ne!(replacement.at(view)?.edits, initial.edits);
    same(&initial, &cached.at(view)?);
    let invalid = CascadeView {
        aspect: 0.0,
        ..view
    };
    assert!(cached.at(invalid).is_err());
    let mut invalid_bytes = heights.to_vec();
    invalid_bytes[0] ^= 1;
    assert!(authored::PreparedSource::new(doc, invalid_bytes, shader).is_err());
    assert!(authored::PreparedSource::new(doc, heights.to_vec(), "bad shader").is_err());
    same(&initial, &cached.at(view)?);
    println!(
        "SHADOW_CACHE_PASS eight views identical; leave/return; edited generation isolated; rejected invalid view/raster/shader; build_ms={:.3} cold_8_ms={:.3} cached_8_ms={:.3}",
        build.as_secs_f64() * 1000.0,
        cold.as_secs_f64() * 1000.0,
        hot.as_secs_f64() * 1000.0
    );
    Ok(())
}
