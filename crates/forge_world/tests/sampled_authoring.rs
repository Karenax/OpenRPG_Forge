use forge_core::{CommandId, bytes_sha256};
use forge_project::{
    HeightSamplingAlgorithm, TERRAIN_DOCUMENT_SCHEMA_VERSION_V4, TerrainDocument, TerrainExtent,
    TerrainHeightDelta,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SAMPLED_TERRAIN_SCHEMA, SampledTerrainSource},
    terrain_edit::{TerrainAction, TerrainEdit, TerrainEditRequest, TerrainEditSession},
};
use forge_world::sampled_terrain::SampledTerrainPlan;

#[test]
#[allow(clippy::float_cmp)] // Binary fractions deliberately give exact expected texels.
fn derived_cell_centres_preserve_source_edges_and_average_corner_edits() {
    let (mut doc, bytes) = fixture();
    let original = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let heights = original.cell_center_heights();
    assert_eq!(
        heights
            .iter()
            .flat_map(|v| v.to_le_bytes())
            .collect::<Vec<_>>(),
        bytes
    );
    doc.height_sampling.base_elevation_m = 2.0;
    doc.height_deltas = vec![
        TerrainHeightDelta::new(1, 1, 8.0),
        TerrainHeightDelta::new(4, 4, 12.0),
    ];
    let plan = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let derived = plan.cell_center_heights();
    for z in 0..4 {
        for x in 0..4 {
            let extra = if x < 2 && z < 2 {
                2.0
            } else if x == 3 && z == 3 {
                3.0
            } else {
                0.0
            };
            assert_eq!(derived[z * 4 + x], heights[z * 4 + x] + 2.0 + extra);
            if x < 3 && z < 3 {
                let wx = f64::from(u32::try_from(x).unwrap()) - 1.5;
                let wz = f64::from(u32::try_from(z).unwrap()) - 1.5;
                assert_eq!(
                    f64::from(derived[z * 4 + x]),
                    plan.height_at(wx, wz).unwrap()
                );
            }
        }
    }
    // The outside-query sentinel must never overwrite the final source texel.
    assert_eq!(derived[15], 20.0);
    assert_eq!(original.cell_center_heights(), heights);
}

fn fixture() -> (TerrainDocument, Vec<u8>) {
    let bytes: Vec<_> = (0_u16..16)
        .flat_map(|v| f32::from(v).to_le_bytes())
        .collect();
    let mut document = new_terrain("sampled", "authoring", "Raster").unwrap();
    document.schema_version = TERRAIN_DOCUMENT_SCHEMA_VERSION_V4;
    document.extent_m = TerrainExtent::new(4.0, 4.0);
    document.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    document.height_sampling.sample_spacing_m = 1.0;
    document.height_sampling.base_elevation_m = 0.0;
    document.height_sampling.amplitude_m = 0.0;
    document.height_sampling.base_frequency_cycles_per_m = 1.0;
    document.height_sampling.octaves = 1;
    document.height_sampling.lacunarity = 2.0;
    document.height_sampling.persistence = 0.5;
    document.sampled_source = Some(SampledTerrainSource {
        schema: SAMPLED_TERRAIN_SCHEMA.into(),
        schema_version: 1,
        width: 4,
        height: 4,
        origin_xz_m: [-2.0; 2],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(&bytes),
    });
    (document, bytes)
}

fn execute(session: &mut TerrainEditSession, id: &str, action: TerrainAction) {
    session
        .execute(&TerrainEditRequest {
            command_id: CommandId::deterministic(id),
            expected_revision: session.document().revision,
            expected_hash: session.document().content_hash().unwrap(),
            action,
        })
        .unwrap();
}

#[test]
#[allow(clippy::float_cmp)]
fn raster_retouch_survives_history_archive_and_keeps_source_identity() {
    let (document, bytes) = fixture();
    let source = document.sampled_source.clone();
    let original = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    assert_eq!(original.height_at(0.0, 0.0).unwrap(), 7.5);
    let mut session = TerrainEditSession::new(document).unwrap();
    execute(
        &mut session,
        "raise",
        TerrainAction::Edit(TerrainEdit::SetHeightDeltas {
            samples: vec![TerrainHeightDelta::new(2, 2, 8.0)],
        }),
    );
    let altered = SampledTerrainPlan::from_document(session.document(), &bytes).unwrap();
    assert_eq!(altered.height_at(0.0, 0.0).unwrap(), 15.5);
    assert_eq!(altered.height_at(0.5, 0.5).unwrap(), 12.0);
    assert_eq!(altered.height_at(-1.5, -1.5).unwrap(), 0.0);
    assert_ne!(altered.document_hash(), original.document_hash());
    execute(&mut session, "undo", TerrainAction::Undo);
    assert_eq!(
        SampledTerrainPlan::from_document(session.document(), &bytes)
            .unwrap()
            .height_at(0.0, 0.0)
            .unwrap(),
        7.5
    );
    execute(&mut session, "redo", TerrainAction::Redo);
    let archive = session.archive_bytes().unwrap();
    let restored = TerrainEditSession::from_archive_bytes(&archive).unwrap();
    assert_eq!(restored.document(), session.document());
    assert_eq!(restored.document().sampled_source, source);
    assert_eq!(
        SampledTerrainPlan::from_document(restored.document(), &bytes)
            .unwrap()
            .height_at(0.0, 0.0)
            .unwrap(),
        15.5
    );
}

#[test]
fn incompatible_or_unresolved_sources_fail_closed() {
    let (document, bytes) = fixture();
    for version in [1, 2, 3] {
        let mut bad = document.clone();
        bad.schema_version = version;
        assert!(bad.validate().is_err());
    }
    let mut bad = document.clone();
    bad.sampled_source = None;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.height_sampling.algorithm = HeightSamplingAlgorithm::FractalValueNoiseV1;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.sampled_source.as_mut().unwrap().origin_xz_m[0] += 0.5;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.sampled_source.as_mut().unwrap().width = 3;
    assert!(bad.validate().is_err());
    let mut bad = document.clone();
    bad.height_sampling.amplitude_m = 1.0;
    assert!(bad.validate().is_err());
    assert!(SampledTerrainPlan::from_document(&document, &[]).is_err());
    let mut corrupt = bytes.clone();
    corrupt[0] ^= 1;
    assert!(SampledTerrainPlan::from_document(&document, &corrupt).is_err());
    let legacy = new_terrain("legacy", "legacy", "Legacy").unwrap();
    assert!(!legacy.to_pretty_json().unwrap().contains("sampled_source"));
    assert!(forge_world::TerrainGenerationPlan::from_document(&document).is_err());
}

#[test]
fn patches_share_edges_normals_and_author_corrections_without_seams() {
    let (mut document, bytes) = fixture();
    document.height_deltas = vec![TerrainHeightDelta::new(2, 2, 3.0)];
    let plan = SampledTerrainPlan::from_document(&document, &bytes).unwrap();
    let left = plan.build_patch([0, 0], [2, 4], 1).unwrap();
    let right = plan.build_patch([2, 0], [2, 4], 1).unwrap();
    for row in 0..5 {
        assert_eq!(
            left.positions_m[row * 3 + 2].map(f32::to_bits),
            right.positions_m[row * 3].map(f32::to_bits)
        );
        assert_eq!(
            left.normals[row * 3 + 2].map(f32::to_bits),
            right.normals[row * 3].map(f32::to_bits)
        );
    }
    assert_eq!(left.indices.len(), 48);
    assert!(
        left.indices
            .iter()
            .all(|i| (*i as usize) < left.positions_m.len())
    );
    for triangle in left.indices.as_chunks::<3>().0 {
        let [a, b, c] =
            [triangle[0], triangle[1], triangle[2]].map(|i| left.positions_m[i as usize]);
        assert!((b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]) > 0.0);
    }
    assert!(plan.build_patch([3, 0], [2, 1], 1).is_err());
    assert!(plan.build_patch([0, 0], [1, 1], 0).is_err());
    assert!(plan.build_patch([0, 0], [129, 1], 1).is_err());
    assert!(plan.build_patch([0, 0], [128, 128], u16::MAX).is_err());
}

#[test]
fn sampled_placement_alignment_uses_grid_then_authored_height_and_preserves_transform() {
    use forge_core::{Transform, UnitQuaternion, Vec3};
    use forge_world::{placement_snap::PlacementSnap, placement_transform::Axis};
    let (mut doc, bytes) = fixture();
    let original_hash = bytes_sha256(&bytes);
    doc.height_sampling.base_elevation_m = 2.0;
    doc.height_deltas = vec![TerrainHeightDelta::new(2, 2, 8.0)];
    let plan = SampledTerrainPlan::from_document(&doc, &bytes).unwrap();
    let source = Transform {
        translation_m: Vec3::new(0.2, 99.0, -0.2),
        rotation: UnitQuaternion::new(0.0, 1.0, 0.0, 0.0),
        scale: Vec3::new(2.0, 3.0, 4.0),
    };
    let snap = PlacementSnap {
        grid_step_m: Some(1.0),
        surface_offset_m: Some(0.25),
    };
    let aligned = snap.resolve_sampled(source, &plan).unwrap();
    // Raster centre 7.5 + base elevation 2 + authored corner 8 + offset .25.
    assert_eq!(aligned.translation_m, Vec3::new(0.0, 17.75, 0.0));
    assert_eq!(aligned.rotation, source.rotation);
    assert_eq!(aligned.scale, source.scale);
    assert_eq!(source.translation_m, Vec3::new(0.2, 99.0, -0.2));
    let axis = snap.resolve_axis_sampled(source, Axis::X, &plan).unwrap();
    assert_eq!(axis.translation_m.x.to_bits(), 0.0_f32.to_bits());
    assert_eq!(
        axis.translation_m.z.to_bits(),
        source.translation_m.z.to_bits()
    );
    assert!(axis.translation_m.y > 15.34 && axis.translation_m.y < 15.36);
    assert!(snap.resolve_axis_sampled(source, Axis::Y, &plan).is_err());
    assert_eq!(
        PlacementSnap::default()
            .resolve_sampled(source, &plan)
            .unwrap(),
        source
    );
    let outside = Transform::from_translation(Vec3::new(3.0, 9.0, 0.0));
    assert!(snap.resolve_sampled(outside, &plan).is_err());
    assert_eq!(
        PlacementSnap::default()
            .resolve_sampled(outside, &plan)
            .unwrap(),
        outside
    );
    assert_eq!(bytes_sha256(&bytes), original_hash);
    assert_eq!(plan.document(), &doc);
}
