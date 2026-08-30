#![deny(unsafe_code)]

use clap::{Parser, Subcommand};
use forge_cli::{hash_file, validate_file, write_default_proof};
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
    }
    Ok(())
}
