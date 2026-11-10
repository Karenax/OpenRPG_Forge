use forge_nwn_runtime::M3Scenario;
use std::path::PathBuf;

fn fixture_bundle() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
}

#[test]
#[allow(clippy::too_many_lines)]
fn m3_scenario_completes_the_quest_and_resumes_without_duplicates() {
    let mut scenario = M3Scenario::load(&fixture_bundle()).expect("load scenario");
    let report = scenario.run().expect("run scenario");

    assert_eq!(report.schema, "forge.aurora-m3-report@1");
    assert_eq!(report.entry_area, "ab_camp");
    assert_eq!(report.final_area, "ab_camp");
    assert_eq!(report.quest_status, "completed");
    assert_eq!(report.journal_entries, vec![10, 20, 30]);
    assert_eq!(report.gold, 50);
    assert!(report.inventory.contains(&"AB_RELIC".to_owned()));
    assert_eq!(report.transitions, 2);
    assert_eq!(report.visited_areas, vec!["ab_camp", "ab_crypt"]);
    assert_eq!(report.doors_opened, 2);
    assert!(report.distance_m > 0.0);
    assert!(report.ticks > 0);
    assert!(report.resume_idempotent);
    assert!(report.effects >= 30);
    assert_eq!(report.natives.len(), 11);
    assert_eq!(report.native_registry, "nwscript-ee-ordinals-v1");

    assert!(
        report
            .messages
            .iter()
            .any(|message| message.contains("pont rompu"))
    );
    assert!(
        report
            .messages
            .iter()
            .any(|message| message.contains("relique du pont"))
    );
    assert!(
        report
            .messages
            .iter()
            .any(|message| message.contains("gardien"))
    );
    assert!(
        report
            .events
            .iter()
            .any(|event| { event.step == "timer" && event.detail.contains("ab_relic_trigger") })
    );
    let steps = report
        .events
        .iter()
        .map(|event| event.step.as_str())
        .collect::<Vec<_>>();
    for step in [
        "module_load",
        "camp_enter",
        "crypt_enter",
        "relic_trigger_scheduled",
        "timer",
        "chest",
        "combat",
        "camp_return",
        "quest_complete",
        "save_resume",
    ] {
        assert!(steps.contains(&step), "missing event {step}");
    }
    assert_eq!(
        steps.iter().filter(|step| **step == "transition").count(),
        2
    );

    let chosen = report
        .dialogue_transcript
        .iter()
        .filter_map(|turn| turn.chosen_reply.clone())
        .collect::<Vec<_>>();
    assert!(chosen.contains(&"Parle-moi du pont.".to_owned()));
    assert!(chosen.contains(&"Où se trouve la relique ?".to_owned()));
    assert!(chosen.contains(&"J'ai la relique.".to_owned()));
    assert!(chosen.contains(&"Le pont est-il réparé ?".to_owned()));

    for script in [
        "ab_mod_load",
        "ab_camp_enter",
        "ab_guide_talk",
        "ab_act_start",
        "ab_reward",
        "ab_cond_start",
        "ab_cond_active",
        "ab_cond_relic",
        "ab_cond_done",
        "ab_crypt_enter",
        "ab_relic_trigger",
        "ab_chest_dist",
        "ab_guard_death",
        "ab_gate_used",
    ] {
        assert!(
            report.executed_scripts.iter().any(|name| name == script),
            "script {script} should have executed"
        );
    }

    assert!(report.unsupported.is_empty(), "{:?}", report.unsupported);
    assert!(
        report
            .natives
            .iter()
            .any(|entry| entry.starts_with("GetModule x"))
    );
}

#[test]
fn m3_scenario_is_deterministic_across_runs() {
    let first = M3Scenario::load(&fixture_bundle())
        .expect("load")
        .run()
        .expect("run");
    let second = M3Scenario::load(&fixture_bundle())
        .expect("load")
        .run()
        .expect("run");
    assert_eq!(first.save_sha256, second.save_sha256);
    assert_eq!(first.events, second.events);
    assert_eq!(first.dialogue_transcript, second.dialogue_transcript);
}
