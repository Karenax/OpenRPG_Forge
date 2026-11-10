use forge_aurora_bridge::load_bundle;
use forge_aurora_runtime::{ModuleSession, project_module};
use std::path::PathBuf;

fn projected() -> forge_aurora_runtime::ProjectedModule {
    let bundle = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1");
    let imported = load_bundle(&bundle).expect("bundle");
    project_module(&imported).expect("projection")
}

fn travel(session: &mut ModuleSession, destination: &str) -> usize {
    let door = session
        .zone()
        .doors
        .iter()
        .find(|door| door.destination.as_deref() == Some(destination))
        .cloned()
        .expect("destination door");
    let from = session.player().position;
    let approach = {
        let dx = from.x - door.position.x;
        let dz = from.z - door.position.z;
        let length = (dx * dx + dz * dz).sqrt();
        if length <= 1.0e-4 {
            forge_core::Vec3::new(door.position.x, door.position.y, door.position.z + 1.2)
        } else {
            forge_core::Vec3::new(
                door.position.x + dx / length * 1.2,
                door.position.y,
                door.position.z + dz / length * 1.2,
            )
        }
    };
    session.walk_to(approach).expect("walk");
    if !session.is_door_open(&door) {
        session.interact().expect("interact");
    }
    let transition = session
        .try_transition()
        .expect("transition")
        .expect("transition triggered");
    assert_eq!(transition.to, destination);
    session.snapshot().transitions
}

#[test]
fn repeated_transitions_restore_without_state_leaks_or_duplicate_events() {
    let module = projected();
    let mut session = ModuleSession::new(module.clone(), "ab_camp").expect("session");
    assert_eq!(session.current_area(), "ab_camp");

    for cycle in 0..3 {
        let before_events = session.events().len();
        let transitions = travel(&mut session, "ab_crypt");
        assert_eq!(transitions, cycle * 2 + 1);
        assert_eq!(session.current_area(), "ab_crypt");
        assert_eq!(session.visited_areas().len(), 2);
        assert_eq!(session.loaded_areas().len(), 1);
        assert!(session.events().len() > before_events);

        let snapshot = session.snapshot();
        let restored = ModuleSession::restore(module.clone(), snapshot).expect("restore");
        assert_eq!(restored.current_area(), "ab_crypt");
        assert_eq!(restored.visited_areas().len(), 2);

        let transitions = travel(&mut session, "ab_camp");
        assert_eq!(transitions, cycle * 2 + 2);
        assert_eq!(session.current_area(), "ab_camp");
    }

    let report = session.report();
    assert_eq!(report.transitions, 6);
    assert_eq!(report.zones_visited, vec!["ab_camp", "ab_crypt"]);
    assert_eq!(
        report.doors_opened, 2,
        "doors stay open after the first use"
    );
}
