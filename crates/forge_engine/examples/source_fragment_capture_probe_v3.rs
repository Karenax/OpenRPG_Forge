//! Build the shared native capture proof separately, preserving a running probe executable.
#[path = "source_fragment_render_probe.rs"]
mod source_capture;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    source_capture::main()
}
