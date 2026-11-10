//! Native Studio entry points for the Aurora import assistant.
//!
//! The frontend only selects a local bundle directory. All parsing, validation, package
//! projection and hashing stay in Rust; the browser receives a bounded summary and the
//! generated package report.

use forge_aurora_bridge::load_bundle;
use forge_aurora_runtime::{PackageReport, project_module, validate_forge_navigation};
use serde::Serialize;
use std::path::{Path, PathBuf};

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AuroraAuditView {
    pub module: String,
    pub module_resref: String,
    pub entry_area: String,
    pub zones: Vec<AuroraZoneView>,
    pub assets: usize,
    pub package_files: usize,
    pub manifest_sha256: String,
    pub playable: bool,
    pub note: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AuroraZoneView {
    pub resref: String,
    pub name: String,
    pub tiles: usize,
    pub instances: usize,
    pub walkmesh_triangles: usize,
    pub doors: usize,
    pub wok_files: usize,
    pub dwk_files: usize,
    pub pwk_files: usize,
    pub navmesh_polygons: usize,
}

fn canonical_bundle(path: &str) -> Result<PathBuf, String> {
    let path = Path::new(path)
        .canonicalize()
        .map_err(|error| format!("Bundle Aurora introuvable : {error}"))?;
    if !path.is_dir() {
        return Err("Choisissez un dossier de bundle Aurora.".to_owned());
    }
    Ok(path)
}

fn audit(path: &Path) -> Result<AuroraAuditView, String> {
    let imported = load_bundle(path).map_err(|error| error.to_string())?;
    let projected = project_module(&imported).map_err(|error| error.to_string())?;
    let mut zones = Vec::new();
    for zone in projected.zones.values() {
        let navmesh = validate_forge_navigation(zone)?;
        zones.push(AuroraZoneView {
            resref: zone.resref.clone(),
            name: zone.name.clone(),
            tiles: zone.tile_count,
            instances: zone.instances.len(),
            walkmesh_triangles: zone.walkmesh.triangles.len(),
            doors: zone.doors.len(),
            wok_files: zone.navigation.wok_files,
            dwk_files: zone.navigation.dwk_files,
            pwk_files: zone.navigation.pwk_files,
            navmesh_polygons: navmesh.polygons,
        });
    }
    Ok(AuroraAuditView {
        module: projected.name.clone(),
        module_resref: imported.manifest.source.module_resref.clone(),
        entry_area: projected.entry_area.clone(),
        zones,
        assets: projected.assets.len(),
        package_files: projected.manifest.files.len(),
        manifest_sha256: projected
            .manifest
            .content_hash()
            .map_err(|error| error.to_string())?
            .to_string(),
        playable: false,
        note: "Prévisualisation structurelle : aucun script NCS ni règle D&D n'est exécuté."
            .to_owned(),
    })
}

#[tauri::command]
pub async fn preview_aurora_bundle(bundle: String) -> Result<AuroraAuditView, String> {
    let bundle = canonical_bundle(&bundle)?;
    tauri::async_runtime::spawn_blocking(move || audit(&bundle))
        .await
        .map_err(|error| format!("Audit Aurora interrompu : {error}"))?
}

#[tauri::command]
pub async fn import_aurora_bundle(bundle: String, output: String) -> Result<PackageReport, String> {
    let bundle = canonical_bundle(&bundle)?;
    let output = PathBuf::from(output);
    if !output.is_absolute() {
        return Err("La destination de l'import doit être un chemin absolu.".to_owned());
    }
    tauri::async_runtime::spawn_blocking(move || {
        let imported = load_bundle(&bundle).map_err(|error| error.to_string())?;
        let projected = project_module(&imported).map_err(|error| error.to_string())?;
        projected
            .write_package(&output)
            .map_err(|error| error.to_string())
    })
    .await
    .map_err(|error| format!("Import Aurora interrompu : {error}"))?
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AuroraGameplayView {
    pub schema: String,
    pub module: String,
    pub seed: u64,
    pub winner: Option<String>,
    pub rounds: u32,
    pub party_hp: i32,
    pub guardian_hp: i32,
    pub quest_status: String,
    pub gold: i64,
    pub journal_entries: Vec<u32>,
    pub trace_entries: usize,
    pub trace_sha256: String,
    pub save_sha256: String,
    pub migration_accepted: bool,
    pub migration_restored: bool,
    pub resume_idempotent: bool,
    pub unsupported: Vec<String>,
    pub oracle_status: String,
    pub note: String,
}

fn gameplay(bundle: &Path, seed: u64) -> Result<AuroraGameplayView, String> {
    let report = forge_nwn_runtime::run_m5(bundle, seed).map_err(|error| error.to_string())?;
    Ok(AuroraGameplayView {
        schema: report.schema.clone(),
        module: report.m4.module.clone(),
        seed: report.m4.seed,
        winner: report.m4.winner.clone(),
        rounds: report.m4.rounds,
        party_hp: report.m4.party_hp,
        guardian_hp: report.m4.guardian_hp,
        quest_status: report.m4.quest_status.clone(),
        gold: report.m4.gold,
        journal_entries: report.m4.journal_entries.clone(),
        trace_entries: report.trace.entries.len(),
        trace_sha256: report.trace.entries_sha256.to_string(),
        save_sha256: report.save_sha256.to_string(),
        migration_accepted: report.migration.accepted,
        migration_restored: report.migration.restored,
        resume_idempotent: report.resume_idempotent,
        unsupported: report.m4.unsupported.clone(),
        oracle_status: report.m4.oracle_status.clone(),
        note: "Scénario de compatibilité synthétique ; aucune compatibilité NWN générale."
            .to_owned(),
    })
}

#[tauri::command]
pub async fn run_aurora_gameplay(bundle: String, seed: u64) -> Result<AuroraGameplayView, String> {
    let bundle = canonical_bundle(&bundle)?;
    tauri::async_runtime::spawn_blocking(move || gameplay(&bundle, seed))
        .await
        .map_err(|error| format!("Scénario Aurora interrompu : {error}"))?
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture_bundle() -> PathBuf {
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../../crates/forge_aurora_bridge/tests/fixtures/ab_proving.module-migration-v1")
    }

    #[test]
    fn audits_the_committed_bundle_without_writing() {
        let audit = audit(&fixture_bundle()).expect("audit");
        assert_eq!(audit.module_resref, "ab_proving");
        assert_eq!(audit.entry_area, "ab_camp");
        assert_eq!(audit.zones.len(), 2);
        assert!(!audit.playable);
        let camp = audit
            .zones
            .iter()
            .find(|zone| zone.resref == "ab_camp")
            .expect("camp");
        assert!(camp.walkmesh_triangles > 0);
        assert_eq!(camp.doors, 1);
    }

    #[test]
    fn refuses_a_missing_bundle_directory() {
        let error = canonical_bundle("Z:/missing/aurora-bundle").expect_err("missing");
        assert!(error.contains("introuvable"), "{error}");
    }

    #[test]
    fn gameplay_view_exposes_compatibility_diagnostics() {
        let view = gameplay(&fixture_bundle(), 2026).expect("gameplay");
        assert_eq!(view.quest_status, "completed");
        assert_eq!(view.guardian_hp, 0);
        assert!(view.trace_entries > 0);
        assert!(view.migration_accepted);
        assert!(view.migration_restored);
        assert!(view.resume_idempotent);
        assert_eq!(view.oracle_status, "not-run");
        assert!(!view.unsupported.is_empty());
    }
}
