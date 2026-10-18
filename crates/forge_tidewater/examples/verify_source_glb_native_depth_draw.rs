//! Shared implementation; target name and runtime input contract are preserved.
#[path = "support/source_glb_native_depth_draw.rs"]
mod harness;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    harness::main()
}
