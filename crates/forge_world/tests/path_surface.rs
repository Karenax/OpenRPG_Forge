use forge_core::{EntityId, Revision};
use forge_project::{
    authoring::new_terrain,
    zone_authoring::{
        GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
        paths::{AuthoredPath, PathPoint, PathVerticalProfileV1},
    },
};
use forge_world::{TerrainGenerationPlan, path_surface::project};

fn head() -> ZoneAuthoringHead {
    let terrain = new_terrain("paths", "surface", "Surface").unwrap();
    let hash = terrain.content_hash().unwrap();
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 3,
        revision: Revision::INITIAL,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: hash,
            },
            terrain,
            vegetation: None,
        },
        corrections: ZoneCorrections {
            paths: Some(vec![AuthoredPath {
                id: EntityId::deterministic("path"),
                name: "Path".into(),
                points: vec![
                    PathPoint {
                        x_m: -5.0,
                        z_m: 0.0,
                    },
                    PathPoint { x_m: 5.0, z_m: 0.0 },
                ],
                width_m: 3.0,
                shoulder_m: 1.0,
                profile: None,
                locked: false,
            }]),
            ..Default::default()
        },
    }
}

#[test]
fn subdivided_path_uses_normal_preview_retry_history_and_archive() {
    use forge_core::CommandId;
    use forge_project::{
        terrain_edit::TerrainEditSession,
        zone_authoring::{
            migration::ZoneMigrationProposal,
            paths::PathEdit,
            session::{ZoneAction, ZoneEditSession, ZoneRequest},
        },
    };
    let fixture = head();
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
    let original = fixture.corrections.paths.unwrap()[0].clone();
    let insert = forge_world::path_surface::prepare_edit(
        session.head(),
        CommandId::deterministic("subdivision-initial"),
        PathEdit::Insert {
            path: original.clone(),
        },
    )
    .unwrap();
    session.execute(&insert).unwrap();
    let before = session.head().clone();
    let points: Vec<_> = original.points.iter().map(|p| [p.x_m, p.z_m]).collect();
    let mut changed = original.clone();
    changed.points = forge_world::path_stroke::insert_midpoint(&points, 0)
        .unwrap()
        .into_iter()
        .map(|[x_m, z_m]| PathPoint { x_m, z_m })
        .collect();
    let request = forge_world::path_surface::prepare_edit(
        session.head(),
        CommandId::deterministic("subdivision"),
        PathEdit::Replace {
            path: changed.clone(),
        },
    )
    .unwrap();
    let preview = session.preview(&request).unwrap();
    forge_world::path_surface::preview(session.head(), &preview).unwrap();
    assert_eq!(session.head(), &before);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.execute(&request).unwrap(), preview);
    assert_eq!(session.audit().len(), 2);
    assert_eq!(
        session.head().corrections.paths.as_ref().unwrap(),
        &[changed.clone()]
    );
    for (action, expected) in [(ZoneAction::Undo, original), (ZoneAction::Redo, changed)] {
        session
            .execute(&ZoneRequest {
                command_id: CommandId::deterministic(format!(
                    "subdivision-history-{}",
                    session.audit().len()
                )),
                expected_revision: session.head().revision,
                expected_hash: session.head().content_hash().unwrap(),
                action,
            })
            .unwrap();
        assert_eq!(
            session.head().corrections.paths.as_ref().unwrap(),
            &[expected]
        );
        for bytes in [
            session.archive_bytes().unwrap(),
            session.delta_archive_bytes().unwrap(),
            session.compact_archive_bytes().unwrap(),
        ] {
            let reopened = ZoneEditSession::from_archive_bytes(&bytes).unwrap();
            assert_eq!(reopened.head(), session.head());
            assert_eq!(reopened.audit(), session.audit());
        }
    }
}

#[test]
fn path_surface_is_deterministic_upward_and_draped_without_mutating_source() {
    let head = head();
    let before = head.to_bytes().unwrap();
    let surface = project(&head).unwrap();
    assert_eq!(surface, project(&head).unwrap());
    assert_eq!(head.to_bytes().unwrap(), before);
    assert_eq!(surface.source_hash, head.content_hash().unwrap());
    let terrain = head.effective().unwrap().0;
    let plan = TerrainGenerationPlan::from_document(&terrain).unwrap();
    let mesh = &surface.meshes[0];
    assert_eq!(mesh.positions.len(), mesh.normals.len());
    assert_eq!(mesh.positions.len(), mesh.colors.len());
    for position in &mesh.positions {
        assert!(
            (position[1] - plan.surface_height_at(position[0], position[2]).unwrap() - 0.02).abs()
                < 0.0001
        );
    }
    for triangle in mesh.indices.as_chunks::<3>().0 {
        let [a, b, c] = [
            mesh.positions[triangle[0] as usize],
            mesh.positions[triangle[1] as usize],
            mesh.positions[triangle[2] as usize],
        ];
        let normal_y = (b[2] - a[2]) * (c[0] - a[0]) - (b[0] - a[0]) * (c[2] - a[2]);
        assert!(normal_y > 0.0);
    }
    assert!(mesh.colors.iter().any(|c| c[3] == 0.0));
    assert!(
        mesh.colors
            .iter()
            .any(|c| c[3].to_bits() == 1.0_f32.to_bits())
    );
}

#[test]
fn vertical_profile_projects_crossfall_and_refuses_excessive_cut_fill() {
    let mut head = head();
    head.corrections.paths.as_mut().unwrap()[0].profile = Some(PathVerticalProfileV1 {
        elevations_m: vec![0.0, 1.0],
        crossfall_ratio: 0.1,
        maximum_cut_fill_m: 32.0,
    });
    let profiled = project(&head).unwrap();
    assert!(
        profiled.meshes[0]
            .positions
            .iter()
            .any(|position| position[1] < 0.0)
    );
    assert!(
        profiled.meshes[0]
            .positions
            .iter()
            .any(|position| position[1] > 1.0)
    );
    head.corrections.paths.as_mut().unwrap()[0]
        .profile
        .as_mut()
        .unwrap()
        .maximum_cut_fill_m = 0.0;
    assert!(project(&head).unwrap_err().contains("cut/fill"));
}

#[test]
fn paths_outside_corridor_and_over_budget_fail_explicitly() {
    let mut head = head();
    head.corrections.paths.as_mut().unwrap()[0].points[0].z_m = 256.0;
    head.corrections.paths.as_mut().unwrap()[0].points[1].z_m = 256.0;
    assert!(project(&head).is_err());
    let path = &mut head.corrections.paths.as_mut().unwrap()[0];
    path.width_m = 64.0;
    path.shoulder_m = 32.0;
    path.points = vec![
        PathPoint {
            x_m: -190.0,
            z_m: 0.0,
        },
        PathPoint {
            x_m: 190.0,
            z_m: 0.0,
        },
    ];
    let template = path.clone();
    head.corrections.paths = Some(
        (0..64)
            .map(|i| AuthoredPath {
                id: EntityId::deterministic(format!("budget-{i}")),
                ..template.clone()
            })
            .collect(),
    );
    head.corrections
        .paths
        .as_mut()
        .unwrap()
        .sort_by_key(|p| p.id);
    assert!(project(&head).unwrap_err().contains("250000"));
}

#[test]
fn bends_share_vertices_preserve_width_and_have_no_overlapping_triangles() {
    for sign in [-1.0, 1.0] {
        let mut head = head();
        head.corrections.paths.as_mut().unwrap()[0].points = vec![
            PathPoint {
                x_m: -20.0,
                z_m: 0.0,
            },
            PathPoint { x_m: 0.0, z_m: 0.0 },
            PathPoint {
                x_m: 0.0,
                z_m: sign * 20.0,
            },
        ];
        let before = head.to_bytes().unwrap();
        let result = project(&head).unwrap();
        assert_eq!(result, project(&head).unwrap());
        assert_eq!(before, head.to_bytes().unwrap());
        let mesh = &result.meshes[0];
        let mut edges = std::collections::BTreeMap::new();
        let mut area = 0.0;
        for &[a, b, c] in mesh.indices.as_chunks::<3>().0 {
            let [pa, pb, pc] = [a, b, c].map(|i| mesh.positions[i as usize]);
            let twice_area = (pb[2] - pa[2]) * (pc[0] - pa[0]) - (pb[0] - pa[0]) * (pc[2] - pa[2]);
            assert!(twice_area > 0.0);
            area += twice_area * 0.5;
            for (a, b) in [(a, b), (b, c), (c, a)] {
                *edges.entry((a.min(b), a.max(b))).or_insert(0) += 1;
            }
        }
        assert!((area - 200.0_f32).abs() < 0.01);
        // Every edge across the miter (x = -sign*z) is welded, not two caps.
        let mut seam_edges = 0;
        for ((a, b), count) in edges {
            let on_seam = |i: u32| {
                let p = mesh.positions[i as usize];
                (p[0] + sign * p[2]).abs() < 0.0001
            };
            if on_seam(a) && on_seam(b) {
                assert_eq!(count, 2);
                seam_edges += 1;
            }
        }
        assert!(seam_edges > 0);
    }
}

#[test]
fn reversals_and_folded_short_bends_are_actionable_errors() {
    let mut head = head();
    for (points, message) in [
        (vec![[-5.0, 0.0], [5.0, 0.0], [-5.0, 0.0]], "too sharp"),
        (vec![[-0.1, 0.0], [0.0, 0.0], [0.0, 0.1]], "folds"),
    ] {
        head.corrections.paths.as_mut().unwrap()[0].points = points
            .into_iter()
            .map(|[x_m, z_m]| PathPoint { x_m, z_m })
            .collect();
        assert!(project(&head).unwrap_err().contains(message));
    }
}

#[test]
fn preflight_preserves_command_and_source_and_rejects_unrenderable_edits() {
    use forge_core::CommandId;
    use forge_project::zone_authoring::paths::PathEdit;
    use forge_world::path_surface::prepare_edit;
    let mut head = head();
    let mut path = head.corrections.paths.take().unwrap().remove(0);
    head.version = 1;
    let before = head.to_bytes().unwrap();
    let id = CommandId::deterministic("preflight");
    let edit = PathEdit::Insert { path: path.clone() };
    assert_eq!(
        prepare_edit(&head, id, edit.clone()).unwrap(),
        head.prepare_path(id, edit).unwrap()
    );
    path.points.push(path.points[0]);
    let invalid = PathEdit::Insert { path: path.clone() };
    // Domain syntax is valid, but the current ribbon cannot represent a reversal.
    assert!(head.prepare_path(id, invalid.clone()).is_ok());
    assert!(
        prepare_edit(&head, id, invalid)
            .unwrap_err()
            .contains("too sharp")
    );
    assert_eq!(head.to_bytes().unwrap(), before);

    head.version = 3;
    path.locked = true;
    head.corrections.paths = Some(vec![path.clone()]);
    let unlock = PathEdit::SetLocked {
        id: path.id,
        locked: false,
    };
    assert!(prepare_edit(&head, id, unlock).is_ok());
    head.corrections.paths.as_mut().unwrap()[0].locked = false;
    assert!(prepare_edit(&head, id, PathEdit::Remove { id: path.id }).is_ok());
}

#[test]
fn preflight_preserves_the_full_manual_exclusion_budget() {
    use forge_core::CommandId;
    use forge_project::{
        VegetationExclusion, VegetationExclusionId, zone_authoring::paths::PathEdit,
    };
    let mut head = head();
    let mut vegetation = forge_world::editor_vegetation::empty(&head.base.terrain).unwrap();
    vegetation.exclusions = (0..32)
        .map(|i| VegetationExclusion::Circle {
            id: VegetationExclusionId::new(format!("existing_{i}")),
            center_x_m: 100.0,
            center_z_m: 100.0,
            radius_m: 1.0,
        })
        .collect();
    head.base.vegetation = Some(vegetation);
    let before = head.to_bytes().unwrap();
    let mut path = head.corrections.paths.as_ref().unwrap()[0].clone();
    path.width_m = 4.0;
    let result = forge_world::path_surface::prepare_edit(
        &head,
        CommandId::deterministic("quota-preflight"),
        PathEdit::Replace { path },
    );
    assert!(result.is_ok());
    assert_eq!(head.to_bytes().unwrap(), before);
}

fn preview_receipt(
    head: &ZoneAuthoringHead,
) -> forge_project::zone_authoring::session::ZoneReceipt {
    use forge_project::zone_authoring::{
        paths::PathEdit,
        session::{ZoneAction, ZoneReceipt},
    };
    let mut path = head.corrections.paths.as_ref().unwrap()[0].clone();
    path.width_m = 5.0;
    let request = head
        .prepare_path(
            forge_core::CommandId::deterministic("preview"),
            PathEdit::Replace { path },
        )
        .unwrap();
    let ZoneAction::Edit(applied) = &request.action else {
        panic!("expected edit")
    };
    let mut candidate = head.clone();
    candidate.corrections = (**applied).clone();
    candidate.revision = head.revision.next().unwrap();
    ZoneReceipt {
        audit: forge_project::audit::AuditStamp::legacy(),
        applied: (**applied).clone(),
        inverse: head.corrections.clone(),
        request,
        base_change: None,
        resulting_revision: candidate.revision,
        resulting_hash: candidate.content_hash().unwrap(),
    }
}

#[test]
fn reviewed_path_preview_uses_accepted_terrain_and_preserves_sources() {
    let head = head();
    let before = head.to_bytes().unwrap();
    let receipt = preview_receipt(&head);
    let projection = forge_world::path_surface::preview(&head, &receipt).unwrap();
    assert_eq!(projection.source_hash, receipt.resulting_hash);
    assert_eq!(
        projection.terrain_hash,
        head.effective().unwrap().0.content_hash().unwrap()
    );
    assert_eq!(head.to_bytes().unwrap(), before);
    assert_ne!(
        projection.meshes[0].positions,
        project(&head).unwrap().meshes[0].positions
    );
}

#[test]
fn path_preview_rejects_stale_forged_and_mixed_receipts() {
    use forge_project::zone_authoring::session::ZoneAction;
    let head = head();
    let original = preview_receipt(&head);
    let mut wrong = original.clone();
    wrong.resulting_hash = head.content_hash().unwrap();
    assert!(forge_world::path_surface::preview(&head, &wrong).is_err());
    wrong = original.clone();
    wrong.request.expected_revision = head.revision.next().unwrap();
    assert!(forge_world::path_surface::preview(&head, &wrong).is_err());
    wrong = original;
    wrong.applied.name = Some("unrelated rename".into());
    wrong.request.action = ZoneAction::edit(wrong.applied.clone());
    let mut candidate = head.clone();
    candidate.corrections = wrong.applied.clone();
    candidate.revision = wrong.resulting_revision;
    wrong.resulting_hash = candidate.content_hash().unwrap();
    assert!(
        forge_world::path_surface::preview(&head, &wrong)
            .unwrap_err()
            .contains("paths-only")
    );
}
