#[path = "installed_fixture.rs"]
mod fixture;
use super::*;
use fixture::Fixture;
use std::{cell::Cell, fs};

#[test]
fn admitted_selection_reaches_the_spawner_with_only_server_prepared_arguments() {
    let fixture = Fixture::new();
    let calls = Cell::new(0);
    let result = start_with(
        &fixture.install,
        ModuleSelection {
            module_id: fixture.first.id,
            area_id: fixture.first.area,
        },
        |command| {
            calls.set(calls.get() + 1);
            assert_eq!(command.get_args().next().unwrap(), "--module");
            Ok(37)
        },
    )
    .unwrap();
    assert_eq!(result, 37);
    assert_eq!(calls.get(), 1);
}

#[test]
fn absent_identity_zone_core_or_dependency_never_reaches_the_spawner() {
    let fixture = Fixture::new();
    let calls = Cell::new(0);
    let attempt = |selection| {
        start_with(&fixture.install, selection, |_| {
            calls.set(calls.get() + 1);
            Ok(())
        })
    };
    assert!(
        attempt(ModuleSelection {
            module_id: ModuleId::deterministic("absent"),
            area_id: fixture.first.area
        })
        .is_err()
    );
    assert!(
        attempt(ModuleSelection {
            module_id: fixture.first.id,
            area_id: AreaId::deterministic("absent")
        })
        .is_err()
    );
    fs::write(
        fixture
            .module_root(fixture.second.id)
            .join("assets/model.glb"),
        b"corrupt dependency",
    )
    .unwrap();
    assert!(
        attempt(ModuleSelection {
            module_id: fixture.second.id,
            area_id: fixture.second.area
        })
        .is_err()
    );
    fs::write(fixture.core_root().join("game.bin"), b"corrupt executable").unwrap();
    assert!(
        attempt(ModuleSelection {
            module_id: fixture.first.id,
            area_id: fixture.first.area
        })
        .is_err()
    );
    assert_eq!(calls.get(), 0);
}

#[test]
fn installation_mutation_is_locked_through_the_final_spawn_boundary() {
    let fixture = Fixture::new();
    let rollback = install::prepare_rollback(&fixture.install).unwrap();
    let original = fs::read(fixture.install.join("installation.json")).unwrap();
    start_with(&fixture.install,ModuleSelection{module_id:fixture.first.id,area_id:fixture.first.area},|_| {
        assert!(install::apply(&rollback).is_err(),"Another installation transaction must not change the admitted selection at the spawn boundary");
        Ok(())
    }).unwrap();
    assert_eq!(
        fs::read(fixture.install.join("installation.json")).unwrap(),
        original
    );
}
