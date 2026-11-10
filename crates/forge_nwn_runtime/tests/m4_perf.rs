use forge_nwn_runtime::M4Scenario;
use std::path::PathBuf;
use std::time::Instant;

fn fixture_bundle() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
}

#[test]
fn m4_scenario_stays_within_the_local_budget() {
    let mut samples = Vec::new();
    for iteration in 0..20 {
        let start = Instant::now();
        let mut scenario = M4Scenario::load(&fixture_bundle(), 2026).expect("load");
        let report = scenario.run().expect("run");
        assert!(report.resume_idempotent);
        samples.push(start.elapsed().as_secs_f64() * 1_000.0);
        assert!(iteration < 20);
    }
    samples.sort_by(f64::total_cmp);
    let average = samples.iter().sum::<f64>() / 20.0;
    let p95 = samples[(samples.len() * 95 / 100).min(samples.len() - 1)];
    let maximum = samples.last().copied().unwrap_or(0.0);
    println!(
        "M4_PERF samples={} average_ms={average:.3} p95_ms={p95:.3} max_ms={maximum:.3}",
        samples.len()
    );
    assert!(
        average < 2_000.0,
        "M4 scenario average {average:.3} ms exceeds the local 2 s budget"
    );
}
