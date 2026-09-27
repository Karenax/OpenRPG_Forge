use forge_core::{CommandId, bytes_sha256};
use forge_project::{
    HeightSamplingAlgorithm,
    authoring::new_terrain,
    sampled_terrain::{HeightEncoding, SampledTerrainSource},
    zone_authoring::session::{ZoneAction, ZoneEditSession, ZoneRequest},
};

#[test]
fn sampled_source_survives_zone_creation_save_edit_undo_and_reopen() {
    let mut terrain = new_terrain("import", "island", "Island").unwrap();
    terrain.schema_version = 4;
    terrain.height_sampling.algorithm = HeightSamplingAlgorithm::SampledRasterV1;
    terrain.height_sampling.sample_spacing_m = 1.0;
    terrain.height_sampling.amplitude_m = 0.0;
    terrain.height_sampling.base_frequency_cycles_per_m = 1.0;
    terrain.height_sampling.octaves = 1;
    terrain.height_sampling.lacunarity = 2.0;
    terrain.height_sampling.persistence = 0.5;
    terrain.sampled_source = Some(SampledTerrainSource {
        schema: "forge.sampled-terrain".into(),
        schema_version: 1,
        width: 512,
        height: 512,
        origin_xz_m: [-256.0, -256.0],
        sample_spacing_m: 1.0,
        outside_height_m: -90.0,
        encoding: HeightEncoding::Float32Le,
        samples_sha256: bytes_sha256(b"immutable terrain fixture identity"),
    });
    terrain.validate().unwrap();
    let mut session = ZoneEditSession::from_initial_terrain(terrain.clone()).unwrap();
    let mut corrections = session.head().corrections.clone();
    corrections.name = Some("Renamed island".into());
    let request = ZoneRequest {
        command_id: CommandId::deterministic("rename-imported"),
        expected_revision: session.head().revision,
        expected_hash: session.head().content_hash().unwrap(),
        action: ZoneAction::edit(corrections),
    };
    session.execute(&request).unwrap();
    let mut restored =
        ZoneEditSession::from_archive_bytes(&session.archive_bytes().unwrap()).unwrap();
    assert_eq!(restored.head(), session.head());
    assert_eq!(
        restored.head().base.terrain.sampled_source,
        terrain.sampled_source
    );
    let undo = ZoneRequest {
        command_id: CommandId::deterministic("undo-imported"),
        expected_revision: restored.head().revision,
        expected_hash: restored.head().content_hash().unwrap(),
        action: ZoneAction::Undo,
    };
    restored.execute(&undo).unwrap();
    assert_eq!(restored.head().effective().unwrap().0.name, terrain.name);
    assert_eq!(restored.head().base.terrain.schema_version, 4);
    assert_eq!(
        restored.head().base.terrain.sampled_source,
        terrain.sampled_source
    );
}
