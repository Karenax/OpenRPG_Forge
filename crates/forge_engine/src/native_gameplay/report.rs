//! Small optional exit report for the Studio host. Never writes authored data or saves.
use super::{CommandResult, NativeGameplayUi, storage::GameStorage};
use crate::gameplay::GameplayRuntime;
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use forge_gameplay::QuestStatus;
use serde::Serialize;
use std::{fs::File, io::Write, path::Path};

const MAX_REPORT_BYTES: usize = 64 * 1024;
const MAX_TEXT_BYTES: usize = 1024;

#[derive(Serialize)]
struct QuestSummary {
    id: EntityId,
    status: QuestStatus,
}
#[derive(Serialize)]
struct NativeReport {
    schema: &'static str,
    version: u32,
    pid: u32,
    source_hash: ContentHash,
    archive_hash: ContentHash,
    functional_hash: ContentHash,
    exit_success: bool,
    ready: bool,
    tick: u64,
    sequence: u64,
    quests: Vec<QuestSummary>,
    inventory_total: u64,
    health: u32,
    stamina: u32,
    feedback: String,
    saved: Option<serde_json::Value>,
    last_command_error: Option<String>,
    simulation_error: Option<String>,
    resume_refused: Option<String>,
}
#[derive(Resource)]
pub(super) struct ReportSink {
    file: File,
    source: ContentHash,
    archive: ContentHash,
    functional: ContentHash,
    written: bool,
}
impl ReportSink {
    pub(super) fn new(
        path: &Path,
        source: ContentHash,
        archive: ContentHash,
        functional: ContentHash,
    ) -> Result<Self, String> {
        if !path.is_absolute() {
            return Err("The native game report path must be absolute".into());
        }
        let name = path.file_name().ok_or("Report file name missing")?;
        let parent = path
            .parent()
            .ok_or("Report parent missing")?
            .canonicalize()
            .map_err(|e| e.to_string())?;
        if !parent.is_dir() {
            return Err("Report parent is not a directory".into());
        }
        let file = std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(parent.join(name))
            .map_err(|e| e.to_string())?;
        Ok(Self {
            file,
            source,
            archive,
            functional,
            written: false,
        })
    }
    fn write(&mut self, report: &NativeReport) -> Result<(), String> {
        if self.written
            || report.pid != std::process::id()
            || report.source_hash != self.source
            || report.archive_hash != self.archive
            || report.functional_hash != self.functional
        {
            return Err("Native report identity mismatch or repeated exit".into());
        }
        let bytes = serde_json::to_vec(report).map_err(|e| e.to_string())?;
        if bytes.len() > MAX_REPORT_BYTES {
            return Err("Native game report exceeds its bounded size".into());
        }
        self.file
            .write_all(&bytes)
            .and_then(|()| self.file.sync_all())
            .map_err(|e| e.to_string())?;
        self.written = true;
        Ok(())
    }
}
fn bounded_text(text: &str) -> String {
    if text.len() <= MAX_TEXT_BYTES {
        return text.to_owned();
    }
    let end = text
        .char_indices()
        .map(|(index, _)| index)
        .take_while(|index| *index <= MAX_TEXT_BYTES)
        .last()
        .unwrap_or(0);
    text[..end].to_owned()
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy owns system parameters")]
pub(super) fn exit(
    mut exits: MessageReader<bevy::app::AppExit>,
    mut sink: ResMut<ReportSink>,
    runtime: Res<GameplayRuntime>,
    storage: Res<GameStorage>,
    ui: Res<NativeGameplayUi>,
    commands: Res<CommandResult>,
) {
    for exit in exits.read() {
        let state = runtime.session().state();
        let player = &state.actors[&runtime.session().document().player];
        let report = NativeReport {
            schema: "forge.native-gameplay.report",
            version: 1,
            pid: std::process::id(),
            source_hash: runtime.session().source_hash(),
            archive_hash: sink.archive,
            functional_hash: sink.functional,
            exit_success: exit.is_success(),
            ready: runtime.is_ready(),
            tick: state.tick,
            sequence: state.sequence,
            quests: state
                .quests
                .iter()
                .map(|(id, status)| QuestSummary {
                    id: *id,
                    status: *status,
                })
                .collect(),
            inventory_total: player
                .inventory
                .values()
                .map(|count| u64::from(*count))
                .sum(),
            health: player.vitality,
            stamina: player.stamina,
            feedback: bounded_text(&format!("{}\n{}", ui.feedback, runtime.feedback())),
            saved: match storage.metadata() {
                Ok(saved) => saved,
                Err(error) => {
                    tracing::error!(%error,"native game report save metadata unavailable");
                    None
                }
            },
            last_command_error: commands.last_error.as_deref().map(bounded_text),
            simulation_error: runtime.error().map(bounded_text),
            resume_refused: ui.recovery.as_deref().map(bounded_text),
        };
        if let Err(error) = sink.write(&report) {
            tracing::error!(%error,"native game exit report refused");
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn native_exit_report_is_optional_new_bounded_and_identity_bound() {
        let directory = super::super::tests::Directory::new();
        let source = ContentHash::from_bytes([1; 32]);
        let archive = ContentHash::from_bytes([2; 32]);
        let functional = ContentHash::from_bytes([3; 32]);
        assert!(
            ReportSink::new(
                Path::new("relative-report.json"),
                source,
                archive,
                functional
            )
            .is_err()
        );
        let path = directory.root.join("game-report.json");
        let mut sink = ReportSink::new(&path, source, archive, functional).unwrap();
        assert!(ReportSink::new(&path, source, archive, functional).is_err());
        let mut report = NativeReport {
            schema: "forge.native-gameplay.report",
            version: 1,
            pid: std::process::id(),
            source_hash: source,
            archive_hash: archive,
            functional_hash: functional,
            exit_success: true,
            ready: true,
            tick: 10,
            sequence: 2,
            quests: vec![],
            inventory_total: 4,
            health: 80,
            stamina: 90,
            feedback: "Sauvegardé".into(),
            saved: None,
            last_command_error: None,
            simulation_error: None,
            resume_refused: None,
        };
        report.source_hash = ContentHash::from_bytes([9; 32]);
        assert!(sink.write(&report).is_err());
        assert_eq!(std::fs::metadata(&path).unwrap().len(), 0);
        report.source_hash = source;
        report.feedback = "x".repeat(MAX_REPORT_BYTES);
        assert!(sink.write(&report).is_err());
        assert_eq!(std::fs::metadata(&path).unwrap().len(), 0);
        report.feedback = "Sauvegardé".into();
        sink.write(&report).unwrap();
        assert!(sink.write(&report).is_err());
        let bytes = std::fs::read(path).unwrap();
        assert!(bytes.len() < MAX_REPORT_BYTES);
        let value: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(value["source_hash"], serde_json::to_value(source).unwrap());
        assert_eq!(value["inventory_total"], 4);
        assert_eq!(value["health"], 80);
    }
    #[test]
    fn native_exit_report_limits_unicode_diagnostics_without_breaking_utf8() {
        let text = "é".repeat(800);
        let bounded = bounded_text(&text);
        assert!(bounded.len() <= MAX_TEXT_BYTES);
        assert!(text.starts_with(&bounded));
    }
}
