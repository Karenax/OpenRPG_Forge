//! Shared implementation; target name and runtime input contract are preserved.
#[path = "support/source_glb_lighting_components_v52.rs"]
mod harness;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    harness::main()
}
