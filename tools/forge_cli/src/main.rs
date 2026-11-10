#![deny(unsafe_code)]

use clap::{Parser, Subcommand};
use forge_aurora_runtime::{
    project_module, render_zone_map_svg, run_scripted_visit, validate_forge_navigation,
};
use forge_cli::{
    CliError, compile_world_sketch_file, hash_file, validate_file, validate_world_sketch_file,
    write_default_proof,
};
use std::path::PathBuf;

#[derive(Debug, Parser)]
#[command(
    name = "forge_cli",
    version,
    about = "Validate deterministic OpenRPG Forge documents"
)]
struct Arguments {
    #[command(subcommand)]
    command: Command,
}

#[derive(Debug, Subcommand)]
enum Command {
    /// Validate a versioned scene-proof document and report its canonical hash.
    Validate {
        /// Path to a forge.scene-proof JSON document.
        document: PathBuf,
    },
    /// Print the canonical SHA-256 of a valid scene-proof document.
    Hash {
        /// Path to a forge.scene-proof JSON document.
        document: PathBuf,
    },
    /// Generate the canonical procedural Gate 0 scene without overwriting files.
    GenerateProof {
        /// New output path. Parent directories must already exist.
        output: PathBuf,
    },
    /// Validate a `WorldSketch` and verify every referenced image hash and PNG dimension.
    ValidateWorldSketch {
        /// Path to a forge.world-sketch JSON document.
        document: PathBuf,
        /// Root used to resolve the document's portable relative source-image paths.
        #[arg(long, default_value = ".")]
        source_root: PathBuf,
    },
    /// Compile a `WorldSketch` into bounded terrain, masks, carving caches and an explicit report.
    CompileWorldSketch {
        /// Path to a forge.world-sketch JSON document.
        document: PathBuf,
        /// New output directory. The command refuses an existing directory.
        output_directory: PathBuf,
        /// Root used to resolve the document's portable relative source-image paths.
        #[arg(long, default_value = ".")]
        source_root: PathBuf,
    },
    /// Audit an Aurora module migration bundle without writing anything.
    AuroraAudit {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
    },
    /// Project an Aurora bundle into a Forge module package directory.
    AuroraImport {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
        /// New package directory. The command refuses an existing directory.
        output: PathBuf,
    },
    /// Run the deterministic two-zone M2 visit and write optional evidence.
    AuroraVisit {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
        /// Optional JSON visit report.
        #[arg(long)]
        report: Option<PathBuf>,
        /// Optional top-down SVG map of the walkmesh and visit events.
        #[arg(long)]
        map: Option<PathBuf>,
    },
    /// Run the deterministic M3 scripted quest and write optional evidence.
    AuroraScenario {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
        /// Optional JSON M3 report.
        #[arg(long)]
        report: Option<PathBuf>,
    },
    /// Run the deterministic M4 gameplay and write optional evidence.
    AuroraGameplay {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
        /// Optional JSON M4 report.
        #[arg(long)]
        report: Option<PathBuf>,
        /// Deterministic RNG seed.
        #[arg(long, default_value_t = 2026)]
        seed: u64,
        /// Optional `forge.nwn-rules-profile@1` JSON document.
        #[arg(long)]
        profile: Option<PathBuf>,
    },
    /// Run the M5 qualification scenario with save migration and a reproducible trace.
    AuroraM5 {
        /// Path to a `module-migration-bundle@1.0.0` directory.
        bundle: PathBuf,
        /// Optional JSON M5 report.
        #[arg(long)]
        report: Option<PathBuf>,
        /// Optional standalone redacted trace JSON.
        #[arg(long)]
        trace: Option<PathBuf>,
        /// Deterministic RNG seed.
        #[arg(long, default_value_t = 2026)]
        seed: u64,
    },
    /// Compare two M5 reports and report divergences.
    AuroraDiffOracle {
        /// Expected M5 report JSON.
        expected: PathBuf,
        /// Actual M5 report JSON.
        actual: PathBuf,
        /// Optional JSON diff report.
        #[arg(long)]
        report: Option<PathBuf>,
    },
    /// Audit local NWN 2DA tables without copying them.
    #[command(name = "aurora-2da")]
    Aurora2da {
        /// Directory containing classes.2da, spells.2da, feat.2da and racialtypes.2da.
        tables_dir: PathBuf,
        /// Optional JSON audit report.
        #[arg(long)]
        report: Option<PathBuf>,
    },
    /// Compare an external NWN oracle combat log with the M4 combat trace.
    #[command(name = "aurora-oracle-log")]
    AuroraOracleLog {
        /// Line-oriented `forge.nwn-oracle-log@1` file.
        log: PathBuf,
        /// M4 report JSON containing the Forge combat trace.
        report: PathBuf,
        /// Optional JSON comparison report.
        #[arg(long)]
        output: Option<PathBuf>,
    },
}

fn main() {
    if let Err(error) = run(Arguments::parse()) {
        eprintln!("ERROR {error}");
        std::process::exit(2);
    }
}

#[allow(clippy::too_many_lines)]
fn run(arguments: Arguments) -> Result<(), forge_cli::CliError> {
    match arguments.command {
        Command::Validate { document } => {
            let report = validate_file(document)?;
            println!(
                "VALID schema_version={} revision={} entities={} sha256={}",
                report.schema_version, report.revision, report.entity_count, report.content_hash
            );
        }
        Command::Hash { document } => println!("{}", hash_file(document)?),
        Command::GenerateProof { output } => {
            let report = write_default_proof(&output)?;
            println!(
                "GENERATED path={} entities={} sha256={}",
                output.display(),
                report.entity_count,
                report.content_hash
            );
        }
        Command::ValidateWorldSketch {
            document,
            source_root,
        } => {
            let report = validate_world_sketch_file(document, source_root)?;
            println!(
                "VALID_WORLD_SKETCH schema_version={} revision={} sources={} regions={} splines={} landmarks={} cameras={} sha256={}",
                report.schema_version,
                report.revision,
                report.source_image_count,
                report.semantic_region_count,
                report.spline_count,
                report.landmark_count,
                report.validation_camera_count,
                report.content_hash
            );
        }
        Command::CompileWorldSketch {
            document,
            output_directory,
            source_root,
        } => {
            let report = compile_world_sketch_file(document, source_root, &output_directory)?;
            println!(
                "COMPILED_WORLD_SKETCH output={} world_sketch_sha256={} terrain_sha256={} carved_terrain_sha256={} vegetation_sha256={} semantic_masks_sha256={} spline_carving_sha256={} traversal_audit_sha256={} bridge_placements_sha256={} blender_input_sha256={} validated_pending_outputs={}",
                report.output_directory.display(),
                report.world_sketch_hash,
                report.terrain_hash,
                report.carved_terrain_hash,
                report.vegetation_hash,
                report.semantic_masks_hash,
                report.spline_carving_hash,
                report.traversal_audit_hash,
                report.bridge_placements_hash,
                report.blender_input_hash,
                report.validated_pending_output_count
            );
        }
        Command::AuroraAudit { bundle } => {
            let imported = forge_aurora_bridge::load_bundle(&bundle)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let projected =
                project_module(&imported).map_err(|error| CliError::Aurora(error.to_string()))?;
            println!(
                "AURORA_AUDIT module={} zones={} assets={} package_files={} manifest_sha256={}",
                projected.name,
                projected.zones.len(),
                projected.assets.len(),
                projected.manifest.files.len(),
                projected
                    .manifest
                    .content_hash()
                    .map_err(|error| CliError::Aurora(error.to_string()))?
            );
            for zone in projected.zones.values() {
                let navmesh = validate_forge_navigation(zone).map_err(CliError::Aurora)?;
                println!(
                    "AURORA_ZONE resref={} tiles={} instances={} walkmesh_triangles={} doors={} wok={} dwk={} pwk={} navmesh_polygons={} navmesh_tile=({}, {})",
                    zone.resref,
                    zone.tile_count,
                    zone.instances.len(),
                    zone.walkmesh.triangles.len(),
                    zone.doors.len(),
                    zone.navigation.wok_files,
                    zone.navigation.dwk_files,
                    zone.navigation.pwk_files,
                    navmesh.polygons,
                    navmesh.tile_x,
                    navmesh.tile_z
                );
            }
        }
        Command::AuroraImport { bundle, output } => {
            let imported = forge_aurora_bridge::load_bundle(&bundle)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let projected =
                project_module(&imported).map_err(|error| CliError::Aurora(error.to_string()))?;
            let report = projected
                .write_package(&output)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            println!(
                "AURORA_IMPORT package={} zones={} assets={} files={} bytes={} manifest_sha256={}",
                report.package_path,
                report.zone_count,
                report.asset_count,
                report.file_count,
                report.payload_bytes,
                report.manifest_sha256
            );
        }
        Command::AuroraVisit {
            bundle,
            report,
            map,
        } => {
            let imported = forge_aurora_bridge::load_bundle(&bundle)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let projected =
                project_module(&imported).map_err(|error| CliError::Aurora(error.to_string()))?;
            let visit = run_scripted_visit(projected.clone()).map_err(CliError::Aurora)?;
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&visit)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            if let Some(path) = map {
                std::fs::write(&path, render_zone_map_svg(&projected, &visit)).map_err(
                    |error| CliError::Write {
                        path,
                        source: error,
                    },
                )?;
            }
            println!(
                "AURORA_VISIT module={} entry={} final={} zones={} transitions={} doors_opened={} distance_m={:.2} ticks={} walkmesh_triangles={}",
                visit.module,
                visit.entry_area,
                visit.final_area,
                visit.zones_visited.len(),
                visit.transitions,
                visit.doors_opened,
                visit.distance_m,
                visit.ticks,
                visit.walkmesh_triangles
            );
        }
        Command::AuroraScenario { bundle, report } => {
            let mut scenario = forge_nwn_runtime::M3Scenario::load(&bundle)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let result = scenario
                .run()
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&result)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            println!(
                "AURORA_SCENARIO module={} entry={} final={} quest={} journal={:?} gold={} transitions={} doors_opened={} distance_m={:.2} ticks={} natives={} unsupported={} save_sha256={} resume_idempotent={}",
                result.module,
                result.entry_area,
                result.final_area,
                result.quest_status,
                result.journal_entries,
                result.gold,
                result.transitions,
                result.doors_opened,
                result.distance_m,
                result.ticks,
                result.natives.len(),
                result.unsupported.len(),
                result.save_sha256,
                result.resume_idempotent
            );
        }
        Command::AuroraGameplay {
            bundle,
            report,
            seed,
            profile,
        } => {
            let rules_profile = match profile {
                Some(path) => {
                    let text = std::fs::read_to_string(&path).map_err(|error| CliError::Write {
                        path: path.clone(),
                        source: error,
                    })?;
                    forge_nwn_rules::NwnRulesProfile::from_json(&text)
                        .map_err(|error| CliError::Aurora(error.to_string()))?
                }
                None => forge_nwn_rules::NwnRulesProfile::vanilla_subset(),
            };
            let mut scenario =
                forge_nwn_runtime::M4Scenario::load_with_profile(&bundle, seed, rules_profile)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
            let result = scenario
                .run()
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&result)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            println!(
                "AURORA_GAMEPLAY module={} profile={} seed={} winner={:?} rounds={} actions={} ai_moves={} party_hp={} guardian_hp={} quest={} journal={:?} gold={} spells={} rests={} rng_trace={} oracle={} save_sha256={} resume_idempotent={} unsupported={}",
                result.module,
                result.profile_id,
                result.seed,
                result.winner,
                result.rounds,
                result.combat_actions,
                result.ai_moves,
                result.party_hp,
                result.guardian_hp,
                result.quest_status,
                result.journal_entries,
                result.gold,
                result.spells.len(),
                result.rests,
                result.rng_trace_len,
                result.oracle_status,
                result.save_sha256,
                result.resume_idempotent,
                result.unsupported.len()
            );
        }
        Command::AuroraM5 {
            bundle,
            report,
            trace,
            seed,
        } => {
            let result = forge_nwn_runtime::run_m5(&bundle, seed)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&result)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            if let Some(path) = trace {
                let bytes = serde_json::to_vec_pretty(&result.trace)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            println!(
                "AURORA_M5 module={} trace_entries={} trace_sha256={} save_sha256={} migration={} resume_idempotent={} transitions={}",
                result.m4.module,
                result.trace.entries.len(),
                result.trace.entries_sha256,
                result.save_sha256,
                result.migration,
                result.resume_idempotent,
                result.repeated_transitions
            );
        }
        Command::AuroraDiffOracle {
            expected,
            actual,
            report,
        } => {
            let expected: forge_nwn_runtime::M5Report =
                serde_json::from_slice(&std::fs::read(&expected).map_err(|error| {
                    CliError::Read {
                        path: expected.clone(),
                        source: error,
                    }
                })?)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let actual: forge_nwn_runtime::M5Report =
                serde_json::from_slice(&std::fs::read(&actual).map_err(|error| {
                    CliError::Read {
                        path: actual.clone(),
                        source: error,
                    }
                })?)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let diff = forge_nwn_runtime::compare_m5(&expected, &actual);
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&diff)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            println!(
                "AURORA_DIFF matches={} divergences={} expected_trace={} actual_trace={}",
                diff.matches,
                diff.divergences.len(),
                diff.expected_trace_sha256,
                diff.actual_trace_sha256
            );
            if !diff.matches {
                return Err(CliError::Aurora(format!(
                    "oracle diff found {} divergence(s)",
                    diff.divergences.len()
                )));
            }
        }
        Command::Aurora2da { tables_dir, report } => {
            let audit = forge_nwn_rules::TwoDaAudit::load(&tables_dir)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let summary = audit.summary();
            if let Some(path) = report {
                let bytes = serde_json::to_vec_pretty(&summary)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            let table_summary = summary
                .tables
                .iter()
                .map(|(name, table)| format!("{name}:{}x{}", table.rows, table.columns))
                .collect::<Vec<_>>()
                .join(",");
            println!(
                "AURORA_2DA tables={} missing={} summary={}",
                summary.tables.len(),
                summary.missing.len(),
                table_summary
            );
        }
        Command::AuroraOracleLog {
            log,
            report,
            output,
        } => {
            let text = std::fs::read_to_string(&log).map_err(|error| CliError::Read {
                path: log.clone(),
                source: error,
            })?;
            let log = forge_nwn_rules::OracleLog::parse(&text)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let bytes = std::fs::read(&report).map_err(|error| CliError::Read {
                path: report.clone(),
                source: error,
            })?;
            let m4: forge_nwn_runtime::M4Report = serde_json::from_slice(&bytes)
                .map_err(|error| CliError::Aurora(error.to_string()))?;
            let comparison = log.compare(&m4.combat);
            if let Some(path) = output {
                let bytes = serde_json::to_vec_pretty(&comparison)
                    .map_err(|error| CliError::Aurora(error.to_string()))?;
                std::fs::write(&path, bytes).map_err(|error| CliError::Write {
                    path,
                    source: error,
                })?;
            }
            println!(
                "AURORA_ORACLE_LOG matches={} divergences={} attacks={}",
                comparison.matches,
                comparison.divergences.len(),
                log.attacks.len()
            );
            if !comparison.matches {
                return Err(CliError::Aurora(format!(
                    "oracle log found {} divergence(s)",
                    comparison.divergences.len()
                )));
            }
        }
    }
    Ok(())
}
