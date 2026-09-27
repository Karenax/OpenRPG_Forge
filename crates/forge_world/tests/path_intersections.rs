use forge_core::{CommandId, EntityId};
use forge_project::{
    authoring::new_terrain,
    terrain_edit::TerrainEditSession,
    zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
        migration::ZoneMigrationProposal,
        paths::{AuthoredPath, PathPoint, PathVerticalProfileV1},
        session::{ZoneAction, ZoneEditSession, ZoneRequest},
    },
};
use forge_world::path_intersections::prepare;

fn path(name: &str, points: &[[f32; 2]]) -> AuthoredPath {
    AuthoredPath {
        id: EntityId::deterministic(name),
        name: name.into(),
        points: points
            .iter()
            .map(|p| PathPoint {
                x_m: p[0],
                z_m: p[1],
            })
            .collect(),
        width_m: 2.0,
        shoulder_m: 1.0,
        profile: None,
        locked: false,
    }
}

fn profiled_path(name: &str, points: &[[f32; 2]], elevations_m: &[f32]) -> AuthoredPath {
    let mut path = path(name, points);
    path.profile = Some(PathVerticalProfileV1 {
        elevations_m: elevations_m.to_vec(),
        crossfall_ratio: 0.08,
        maximum_cut_fill_m: 32.0,
    });
    path
}

fn head(mut paths: Vec<AuthoredPath>) -> ZoneAuthoringHead {
    paths.sort_by_key(|p| p.id);
    let terrain = new_terrain("junction", "test", "Junction").unwrap();
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 3,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: None,
        },
        corrections: ZoneCorrections {
            paths: Some(paths),
            ..Default::default()
        },
    }
}

fn crossing_head() -> ZoneAuthoringHead {
    head(vec![
        path("east", &[[-20.0, 0.0], [20.0, 0.0]]),
        path("north", &[[0.0, -20.0], [0.0, 20.0]]),
    ])
}

fn revised(source: &ZoneAuthoringHead) -> ZoneAuthoringHead {
    let request = prepare(source, CommandId::deterministic("junction")).unwrap();
    let ZoneAction::Edit(corrections) = request.action else {
        panic!("edit expected")
    };
    let mut result = source.clone();
    result.corrections = *corrections;
    result
}

#[test]
fn crossing_preserves_controls_profiles_and_source_and_is_idempotent() {
    let source = crossing_head();
    let original = source.clone();
    let result = revised(&source);
    for (old, new) in source
        .corrections
        .paths
        .as_ref()
        .unwrap()
        .iter()
        .zip(result.corrections.paths.as_ref().unwrap())
    {
        let mut expected = old.clone();
        expected.points.insert(1, PathPoint { x_m: 0.0, z_m: 0.0 });
        assert_eq!(*new, expected);
    }
    assert_eq!(source, original);
    assert!(
        prepare(&result, CommandId::deterministic("again"))
            .unwrap_err()
            .contains("Aucun nouveau")
    );
}

#[test]
fn t_junction_and_multiple_crossings_preserve_endpoints_and_sort_insertions() {
    let source = head(vec![
        path("main", &[[-20.0, 0.0], [20.0, 0.0]]),
        path("branch-right", &[[10.0, 0.0], [10.0, 20.0]]),
        path("branch-left", &[[-10.0, 0.0], [-10.0, -20.0]]),
    ]);
    let result = revised(&source);
    for p in result.corrections.paths.as_ref().unwrap() {
        if p.name == "main" {
            assert_eq!(
                p.points.iter().map(|p| p.x_m).collect::<Vec<_>>(),
                [-20.0, -10.0, 10.0, 20.0]
            );
        } else {
            assert_eq!(
                p,
                source
                    .corrections
                    .paths
                    .as_ref()
                    .unwrap()
                    .iter()
                    .find(|old| old.id == p.id)
                    .unwrap()
            );
        }
    }
    // Three paths sharing one crossing insert a single anchor in each path.
    let mut source = crossing_head();
    source
        .corrections
        .paths
        .as_mut()
        .unwrap()
        .push(path("diagonal", &[[-15.0, -15.0], [15.0, 15.0]]));
    source
        .corrections
        .paths
        .as_mut()
        .unwrap()
        .sort_by_key(|p| p.id);
    assert!(
        revised(&source)
            .corrections
            .paths
            .unwrap()
            .iter()
            .all(|p| p.points.len() == 3)
    );
}

#[test]
fn locks_overlap_and_limits_refuse_without_mutation_and_near_misses_do_not_snap() {
    let mut locked = crossing_head();
    locked.corrections.paths.as_mut().unwrap()[0].locked = true;
    let bytes = locked.to_bytes().unwrap();
    assert!(
        prepare(&locked, CommandId::deterministic("locked"))
            .unwrap_err()
            .contains("Unlock")
    );
    assert_eq!(locked.to_bytes().unwrap(), bytes);
    let overlap = head(vec![
        path("a", &[[0.0, 0.0], [20.0, 0.0]]),
        path("b", &[[10.0, 0.0], [30.0, 0.0]]),
    ]);
    assert!(
        prepare(&overlap, CommandId::deterministic("overlap"))
            .unwrap_err()
            .contains("colinéaires")
    );
    let miss = head(vec![
        path("a", &[[-20.0, 0.0], [20.0, 0.0]]),
        path("b", &[[0.0, 0.01], [0.0, 20.0]]),
    ]);
    assert!(
        prepare(&miss, CommandId::deterministic("miss"))
            .unwrap_err()
            .contains("Aucun nouveau")
    );
    let points: Vec<_> = (0..128_u16).map(|i| [f32::from(i) - 127.0, 0.0]).collect();
    let full = head(vec![
        path("full", &points),
        path("branch", &[[-126.5, -10.0], [-126.5, 10.0]]),
    ]);
    assert!(
        prepare(&full, CommandId::deterministic("full"))
            .unwrap_err()
            .contains("128 points")
    );
    let large = head(
        (0..12_u16)
            .map(|i| {
                let points: Vec<_> = (0..128_u16)
                    .map(|j| [f32::from(j) - 127.0, f32::from(i) * 5.0])
                    .collect();
                path(&format!("line-{i}"), &points)
            })
            .collect(),
    );
    assert!(
        prepare(&large, CommandId::deterministic("large"))
            .unwrap_err()
            .contains("budget")
    );
}

#[test]
fn self_crossings_and_unchanged_locked_t_endpoints_are_supported() {
    let source = head(vec![path(
        "loop",
        &[[-20.0, -20.0], [20.0, 20.0], [-20.0, 20.0], [20.0, -20.0]],
    )]);
    let result = revised(&source);
    let points = &result.corrections.paths.as_ref().unwrap()[0].points;
    assert_eq!(points.len(), 6);
    assert_eq!(points[1], PathPoint { x_m: 0.0, z_m: 0.0 });
    assert_eq!(points[4], points[1]);
    let mut branch = path("locked-branch", &[[0.0, 0.0], [0.0, 20.0]]);
    branch.locked = true;
    let source = head(vec![
        path("main", &[[-20.0, 0.0], [20.0, 0.0]]),
        branch.clone(),
    ]);
    let result = revised(&source);
    assert_eq!(
        result
            .corrections
            .paths
            .unwrap()
            .into_iter()
            .find(|p| p.id == branch.id)
            .unwrap(),
        branch
    );
}

#[test]
fn profiled_x_and_t_intersections_preserve_exact_controls_and_interpolate_anchors() {
    let source = head(vec![
        profiled_path("east", &[[-5.0, 0.0], [5.0, 0.0]], &[0.0, 1.0]),
        profiled_path("north", &[[0.0, -5.0], [0.0, 5.0]], &[2.0, 4.0]),
    ]);
    let source_bytes = source.to_bytes().unwrap();
    let first = prepare(&source, CommandId::deterministic("profiled-x-first")).unwrap();
    let second = prepare(&source, CommandId::deterministic("profiled-x-second")).unwrap();
    assert_eq!(first.action, second.action);
    assert_eq!(source.to_bytes().unwrap(), source_bytes);
    let ZoneAction::Edit(corrections) = first.action else {
        panic!("edit expected")
    };
    for path in corrections.paths.unwrap() {
        let profile = path.profile.unwrap();
        assert_eq!(path.points.len(), profile.elevations_m.len());
        assert!(
            profile
                .elevations_m
                .iter()
                .all(|elevation| elevation.is_finite())
        );
        match path.name.as_str() {
            "east" => assert_eq!(profile.elevations_m, [0.0, 0.5, 1.0]),
            "north" => assert_eq!(profile.elevations_m, [2.0, 3.0, 4.0]),
            _ => panic!("unexpected path"),
        }
    }

    let source = head(vec![
        profiled_path("main", &[[-20.0, 0.0], [20.0, 0.0]], &[0.0, 1.0]),
        profiled_path("left", &[[-10.0, 0.0], [-10.0, 20.0]], &[3.0, 4.0]),
        profiled_path("right", &[[10.0, 0.0], [10.0, 20.0]], &[5.0, 6.0]),
    ]);
    let result = revised(&source);
    let main = result
        .corrections
        .paths
        .unwrap()
        .into_iter()
        .find(|path| path.name == "main")
        .unwrap();
    assert_eq!(
        main.points
            .iter()
            .map(|point| point.x_m)
            .collect::<Vec<_>>(),
        [-20.0, -10.0, 10.0, 20.0]
    );
    assert_eq!(main.profile.unwrap().elevations_m, [0.0, 0.25, 0.75, 1.0]);
}

#[test]
fn malformed_source_profile_is_refused_atomically_before_intersection_edits() {
    let mut source = crossing_head();
    source.corrections.paths.as_mut().unwrap()[0].profile = Some(PathVerticalProfileV1 {
        elevations_m: vec![0.0],
        crossfall_ratio: 0.08,
        maximum_cut_fill_m: 32.0,
    });
    let original = source.clone();
    assert!(
        prepare(&source, CommandId::deterministic("invalid-profile"))
            .unwrap_err()
            .contains("finite elevations")
    );
    assert_eq!(source, original);
}

#[test]
fn unsafe_profiled_bend_crossings_are_refused_atomically() {
    let mut bend = profiled_path(
        "profiled bend",
        &[[-5.0, 0.0], [5.0, 0.0], [0.0, 10.0]],
        &[0.0, 0.5, 1.0],
    );
    bend.width_m = 4.0;
    let source = head(vec![
        bend,
        path("crossing route", &[[0.0, -3.0], [4.0, 2.0]]),
    ]);
    let original = source.clone();
    assert!(forge_world::path_surface::project(&source).is_ok());
    let request = prepare(&source, CommandId::deterministic("bend-crossing"));
    assert!(
        request
            .unwrap_err()
            .contains("Aucun point de croisement sûr à insérer")
    );
    assert_eq!(source, original);
}

fn request(session: &ZoneEditSession, action: ZoneAction) -> ZoneRequest {
    ZoneRequest {
        command_id: CommandId::deterministic(format!("request-{}", session.audit().len())),
        expected_hash: session.head().content_hash().unwrap(),
        expected_revision: session.head().revision,
        action,
    }
}

#[test]
fn intersections_are_one_reviewed_undoable_regenerable_and_persistent_command() {
    let fixture = crossing_head();
    let legacy = TerrainEditSession::new(fixture.base.terrain.clone()).unwrap();
    let mut session = ZoneEditSession::from_migration(
        ZoneMigrationProposal::prepare(
            &legacy.archive_bytes().unwrap(),
            fixture.base.terrain.content_hash().unwrap(),
            fixture.base.generator.clone(),
            None,
        )
        .unwrap(),
    )
    .unwrap();
    session
        .execute(&request(&session, ZoneAction::edit(fixture.corrections)))
        .unwrap();
    let original = session.head().clone();
    let command = prepare(session.head(), CommandId::deterministic("crossings")).unwrap();
    let preview = session.preview(&command).unwrap();
    assert_eq!(session.head(), &original);
    let projection = forge_world::path_surface::preview(session.head(), &preview).unwrap();
    assert_eq!(projection.meshes.len(), 2);
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.execute(&command).unwrap(), preview);
    assert_eq!(session.audit().len(), 2);
    let applied = session.head().corrections.clone();
    session
        .execute(&request(&session, ZoneAction::Undo))
        .unwrap();
    assert_eq!(session.head().corrections, original.corrections);
    session
        .execute(&request(&session, ZoneAction::Redo))
        .unwrap();
    assert_eq!(session.head().corrections, applied);
    // A new terrain recipe must not erase the manually accepted intersection anchors.
    let mut base = session.head().base.clone();
    base.terrain.seed += 1;
    session
        .execute(&request(&session, ZoneAction::Regenerate(Box::new(base))))
        .unwrap();
    assert_eq!(session.head().corrections, applied);
    for bytes in [
        session.archive_bytes().unwrap(),
        session.compact_archive_bytes().unwrap(),
        session.delta_archive_bytes().unwrap(),
    ] {
        let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
        assert_eq!(reopened.head(), session.head());
        assert_eq!(reopened.audit(), session.audit());
    }
    let current = session.head().clone();
    assert_eq!(session.execute(&command).unwrap(), preview); // Retry never reapplies an old edit.
    assert_eq!(session.head(), &current);
    let mut stale = command;
    stale.command_id = CommandId::deterministic("stale-new-command");
    assert!(session.execute(&stale).is_err());
    assert_eq!(session.head(), &current);
}
