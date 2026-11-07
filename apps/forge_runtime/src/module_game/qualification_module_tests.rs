//! The actual entry preflight on installed, originally generated modules.
//! No test invokes `scene::run`, starts a process, or opens a window.
use super::{installed_fixture, preflight, qualification_fixture};
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_delivery::{install, module_builder::ExportedModule, module_runtime};
use forge_project::module_package::ModulePlacementCompanionKind;
use std::{
    ffi::OsString,
    path::{Path, PathBuf},
};

#[derive(Clone, Copy)]
struct Pins {
    head: ContentHash,
    archive: ContentHash,
    transport: ContentHash,
}

fn install_module(
    fixture: &installed_fixture::Fixture,
    exported: &ExportedModule,
) -> (PathBuf, Pins, Vec<ModulePlacementCompanionKind>) {
    let plan = install::prepare(
        &fixture.install,
        std::slice::from_ref(&exported.package_path),
    )
    .unwrap();
    install::apply(&plan).unwrap();
    let directory = fixture.module_root(exported.module_id);
    let loaded = module_runtime::load(&directory, exported.area_id, None).unwrap();
    let pins = Pins {
        head: exported.head_hash,
        archive: bytes_sha256(&loaded.archive),
        transport: exported.transport_manifest_sha256.parse().unwrap(),
    };
    module_runtime::verify_transport_revision(&loaded, pins.transport).unwrap();
    let mut kinds: Vec<_> = loaded
        .manifest
        .placement_companions
        .iter()
        .map(|item| item.kind)
        .collect();
    kinds.sort_unstable();
    (directory, pins, kinds)
}

fn arguments(
    root: &Path,
    package: &Path,
    area: AreaId,
    pins: [Option<ContentHash>; 3],
) -> Vec<OsString> {
    let mut arguments = vec![
        OsString::from("--module"),
        package.as_os_str().to_owned(),
        OsString::from("--zone"),
        OsString::from(area.to_string()),
        OsString::from("--cache"),
        root.join("preflight-cache").into_os_string(),
        OsString::from("--save-root"),
        root.join("preflight-save").into_os_string(),
        OsString::from("--capture"),
        root.join("preflight-capture.png").into_os_string(),
        OsString::from("--frames"),
        OsString::from("1"),
    ];
    for (flag, pin) in [
        ("--expected-head", pins[0]),
        ("--expected-archive-sha256", pins[1]),
        ("--expected-package-manifest-sha256", pins[2]),
    ] {
        if let Some(pin) = pin {
            arguments.extend([OsString::from(flag), OsString::from(pin.to_string())]);
        }
    }
    arguments
}

fn refused(arguments: Vec<OsString>, qualify_graph: bool) -> String {
    match preflight(arguments, false, qualify_graph) {
        Ok(_) => panic!("Unexpected module admission"),
        Err(error) => error.to_string(),
    }
}

#[test]
fn ordinary_installed_launch_routes_source_scenes_through_the_common_host() {
    for (mode, source_scene) in [("source-graph", true), ("valid", false)] {
        let fixture = installed_fixture::Fixture::new();
        let exported =
            qualification_fixture::create_fixture(&fixture.root.join("export"), mode).unwrap();
        let (directory, pins, kinds) = install_module(&fixture, &exported);
        assert!(kinds.is_empty());
        let start = preflight(
            arguments(
                &fixture.root,
                &directory,
                exported.area_id,
                [Some(pins.head), Some(pins.archive), Some(pins.transport)],
            ),
            false,
            false,
        )
        .unwrap();
        let normal = super::manual::prepare(&start.loaded, false).unwrap();
        assert_eq!(normal.has_source_graphs(), source_scene);
        assert_eq!(
            normal.accepted(),
            start
                .loaded
                .zone
                .head()
                .corrections
                .placements
                .clone()
                .unwrap()
        );
        if source_scene {
            let diagnostic = super::manual::prepare(&start.loaded, true).unwrap();
            assert_eq!(normal.accepted(), diagnostic.accepted());
        }
    }
}

#[test]
fn installed_module_preflight_requires_real_pins_for_physics_and_particle_inputs() {
    use ModulePlacementCompanionKind::{Particles, Physics};
    let fixture = installed_fixture::Fixture::new();
    let root = fixture.root.clone();
    let physical = qualification_fixture::create_fixture(
        &root.join("physical-export"),
        "source-graph-physics",
    )
    .unwrap();
    let (directory, pins, kinds) = install_module(&fixture, &physical);
    assert_eq!(kinds, [Physics]);
    let valid = [Some(pins.head), Some(pins.archive), Some(pins.transport)];
    let args = || arguments(&root, &directory, physical.area_id, valid);
    assert!(preflight(args(), false, true).is_ok());
    let normal = preflight(args(), false, false).unwrap();
    let manual = super::manual::prepare(&normal.loaded, false).unwrap();
    assert!(manual.has_source_graphs());
    assert!(
        manual
            .accepted()
            .iter()
            .all(|placement| placement.physics.is_some())
    );

    for (index, expected) in [(0, "head"), (1, "archive"), (2, "manifest")] {
        let mut wrong = valid;
        wrong[index] = Some(bytes_sha256(
            format!("wrong qualification pin {index}").as_bytes(),
        ));
        let reason = refused(arguments(&root, &directory, physical.area_id, wrong), true);
        assert!(reason.to_lowercase().contains(expected), "{reason}");
        let reason = refused(arguments(&root, &directory, physical.area_id, wrong), false);
        assert!(reason.to_lowercase().contains(expected), "{reason}");
    }
    for index in 0..3 {
        let mut missing = valid;
        missing[index] = None;
        let reason = refused(
            arguments(&root, &directory, physical.area_id, missing),
            true,
        );
        assert!(!reason.is_empty());
        assert!(
            !refused(
                arguments(&root, &directory, physical.area_id, missing),
                false
            )
            .is_empty()
        );
    }
    let mut no_zone_pins = valid;
    no_zone_pins[0] = None;
    no_zone_pins[1] = None;
    assert!(
        !refused(
            arguments(&root, &directory, physical.area_id, no_zone_pins),
            true
        )
        .is_empty()
    );

    let combined = qualification_fixture::create_fixture(
        &root.join("combined-export"),
        "source-graph-companions",
    )
    .unwrap();
    assert_particle_entry(&fixture, &combined, &[Physics, Particles]);

    let particles =
        qualification_fixture::create_particle_only_fixture(&root.join("particles-export"))
            .unwrap();
    assert_particle_entry(&fixture, &particles, &[Particles]);
    assert!(!root.join("preflight-cache").exists());
    assert!(!root.join("preflight-save").exists());
    assert!(!root.join("preflight-capture.png").exists());
    drop(fixture);
    assert!(!root.exists());
}

fn assert_particle_entry(
    fixture: &installed_fixture::Fixture,
    exported: &ExportedModule,
    expected_kinds: &[ModulePlacementCompanionKind],
) {
    let (directory, pins, kinds) = install_module(fixture, exported);
    assert_eq!(kinds, expected_kinds);
    let valid = [Some(pins.head), Some(pins.archive), Some(pins.transport)];
    for diagnostic in [false, true] {
        let start = preflight(
            arguments(&fixture.root, &directory, exported.area_id, valid),
            false,
            diagnostic,
        )
        .unwrap();
        let manual = super::manual::prepare(&start.loaded, diagnostic).unwrap();
        assert!(
            manual
                .accepted()
                .iter()
                .any(|item| item.particles.is_some())
        );
        if expected_kinds == [ModulePlacementCompanionKind::Particles] {
            assert!(
                manual
                    .accepted()
                    .iter()
                    .all(|item| item.particles.is_some() && item.physics.is_none())
            );
        }
        for index in 0..3 {
            let mut missing = valid;
            missing[index] = None;
            assert!(
                !refused(
                    arguments(&fixture.root, &directory, exported.area_id, missing),
                    diagnostic
                )
                .is_empty()
            );
        }
    }
}
