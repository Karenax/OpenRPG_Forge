use forge_core::CommandId;
use forge_project::{
    VegetationExclusion, VegetationExclusionId,
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp, exclusions::VegetationExclusionEdit, migration::ZoneMigrationProposal,
        session::ZoneEditSession,
    },
};
use forge_world::{VegetationGenerationPlan, editor_vegetation};

#[test]
fn author_exclusion_reaches_the_effective_scatter_projection() {
    let terrain = new_terrain("author-exclusion", "zone", "Zone").unwrap();
    let hash = terrain.content_hash().unwrap();
    let vegetation = editor_vegetation::prepare(&terrain, 42).unwrap();
    let legacy = TerrainEditSession::new(terrain).unwrap();
    let mut session = ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            hash,
            GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            Some(vegetation),
        )
        .unwrap(),
    )
    .unwrap();
    let (base_terrain, base_vegetation) = session.head().effective().unwrap();
    let baseline =
        VegetationGenerationPlan::from_documents(&base_terrain, &base_vegetation.unwrap())
            .unwrap()
            .generate()
            .unwrap();
    let request = session
        .head()
        .prepare_vegetation_exclusions(
            VegetationExclusionEdit::Replace {
                exclusions: vec![VegetationExclusion::Circle {
                    id: VegetationExclusionId::new("author_clearance"),
                    center_x_m: 0.0,
                    center_z_m: 0.0,
                    radius_m: 250.0,
                }],
            },
            CommandId::deterministic("author_clearance"),
        )
        .unwrap();
    session.execute(&request).unwrap();
    let (terrain, vegetation) = session.head().effective().unwrap();
    let projection = VegetationGenerationPlan::from_documents(&terrain, &vegetation.unwrap())
        .unwrap()
        .generate()
        .unwrap();
    let inside = |x: f32, z: f32| x * x + z * z < 250.0 * 250.0;
    assert!(
        baseline
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .any(|instance| inside(instance.position_m.x, instance.position_m.z))
    );
    assert!(
        !projection
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .any(|instance| inside(instance.position_m.x, instance.position_m.z))
    );
}
