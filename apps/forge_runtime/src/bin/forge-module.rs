//! Native entry point for any admitted Forge module.

fn main() -> Result<(), Box<dyn std::error::Error>> {
    #[cfg(feature = "qualification")]
    if std::env::args_os()
        .nth(1)
        .is_some_and(|argument| argument == "--qualify-module-water")
    {
        return forge_runtime::module_game::run_water_qualification(
            std::iter::once(std::ffi::OsString::from("--module"))
                .chain(std::env::args_os().skip(2)),
        );
    }
    #[cfg(feature = "qualification")]
    if std::env::args_os()
        .nth(1)
        .is_some_and(|argument| argument == "--qualify-module-source-graph")
    {
        return forge_runtime::module_game::run_source_graph_qualification(
            std::iter::once(std::ffi::OsString::from("--module"))
                .chain(std::env::args_os().skip(2)),
        );
    }
    forge_runtime::module_game::run(std::env::args_os().skip(1))
}
