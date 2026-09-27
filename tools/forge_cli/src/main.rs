#![deny(unsafe_code)]

use clap::{Parser, Subcommand};
use forge_cli::{
    compile_world_sketch_file, hash_file, validate_file, validate_world_sketch_file,
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
}

fn main() {
    if let Err(error) = run(Arguments::parse()) {
        eprintln!("ERROR {error}");
        std::process::exit(2);
    }
}

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
    }
    Ok(())
}
