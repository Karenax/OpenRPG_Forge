//! Generic installed-module entry point shared by every admitted adventure.
mod companions;
mod environment;
mod evidence;
mod evidence_contract;
mod graphics;
mod imported;
pub mod input;
mod lights;
mod manual;
mod output;
mod scene;
mod source_terrain;
mod species;
mod streamed;
mod temporal;
mod water;

use std::{error::Error, ffi::OsString};

#[cfg(test)]
#[path = "../../../../tools/forge_delivery/tests/support/installed_fixture.rs"]
mod installed_fixture;
#[cfg(test)]
#[allow(
    dead_code,
    reason = "Reuse the original qualification generator in runtime tests"
)]
#[path = "../../examples/source_fragment_module_fixture.rs"]
mod qualification_fixture;
#[cfg(test)]
#[path = "qualification_module_tests.rs"]
mod qualification_module_tests;

pub fn run(arguments: impl IntoIterator<Item = OsString>) -> Result<(), Box<dyn Error>> {
    run_inner(arguments, false, false)
}

#[cfg(feature = "qualification")]
pub fn run_water_qualification(
    arguments: impl IntoIterator<Item = OsString>,
) -> Result<(), Box<dyn Error>> {
    run_inner(arguments, true, false)
}

#[cfg(feature = "qualification")]
pub fn run_source_graph_qualification(
    arguments: impl IntoIterator<Item = OsString>,
) -> Result<(), Box<dyn Error>> {
    run_inner(arguments, false, true)
}

fn run_inner(
    arguments: impl IntoIterator<Item = OsString>,
    water_qualification: bool,
    source_graph_qualification: bool,
) -> Result<(), Box<dyn Error>> {
    let admitted = preflight(arguments, water_qualification, source_graph_qualification)?;
    scene::run(
        admitted.options,
        admitted.loaded,
        water_qualification,
        source_graph_qualification,
        admitted.default_display_mode,
    )
    .map_err(Into::into)
}

struct PreparedModuleStart {
    options: input::ModuleGameOptions,
    loaded: forge_delivery::module_runtime::LoadedModuleZone,
    default_display_mode: Option<forge_project::workspace::launcher::DisplayMode>,
}

fn preflight(
    arguments: impl IntoIterator<Item = OsString>,
    water_qualification: bool,
    source_graph_qualification: bool,
) -> Result<PreparedModuleStart, Box<dyn Error>> {
    let options = input::ModuleGameOptions::parse(arguments)?;
    let display_environment =
        std::env::var_os(forge_delivery::installed_modules::GAME_DEFAULT_DISPLAY_MODE_ENV);
    let default_display_mode = input::parse_default_display_mode(display_environment.as_deref())?;
    let loaded = forge_delivery::module_runtime::load(
        &options.package,
        options.area_id,
        Some(&options.cache_root),
    )?;
    if !source_companion_admission(
        options.expected_zone.is_some(),
        options.expected_package_manifest.is_some(),
        loaded
            .manifest
            .placement_companions
            .iter()
            .map(|item| item.kind),
    ) {
        loaded.reject_unhandled_placement_companions()?;
    }
    // The normal game prepares declared water through the same engine host as
    // qualification; incomplete inputs are rejected by scene::run.
    if water_qualification {
        if options.expected_zone.is_none() || options.expected_package_manifest.is_none() {
            return Err(
                "Water qualification requires exact reviewed module and transport pins".into(),
            );
        }
        if loaded.ocean()?.is_none() {
            return Err("Water qualification requires an explicit ocean binding".into());
        }
    }
    if source_graph_qualification
        && (options.expected_zone.is_none() || options.expected_package_manifest.is_none())
    {
        return Err(
            "Source-graph qualification requires exact reviewed module and transport pins".into(),
        );
    }
    input::verify_expected_zone(
        options.expected_zone,
        loaded.zone.head().content_hash()?,
        &loaded.archive,
    )?;
    if let Some(expected) = options.expected_package_manifest {
        forge_delivery::module_runtime::verify_transport_revision(&loaded, expected)?;
    }
    Ok(PreparedModuleStart {
        options,
        loaded,
        default_display_mode,
    })
}

/// Both entry paths require exact reviewed zone and transport pins before
/// source companions reach the common physics and effect admission.
fn source_companion_admission(
    has_zone_pins: bool,
    has_manifest_pin: bool,
    kinds: impl IntoIterator<Item = forge_project::module_package::ModulePlacementCompanionKind>,
) -> bool {
    use forge_project::module_package::ModulePlacementCompanionKind;
    has_zone_pins
        && has_manifest_pin
        && kinds.into_iter().all(|kind| {
            matches!(
                kind,
                ModulePlacementCompanionKind::Physics | ModulePlacementCompanionKind::Particles
            )
        })
}

#[cfg(test)]
mod qualification_scope_tests {
    use super::source_companion_admission;
    use forge_project::module_package::ModulePlacementCompanionKind::{Particles, Physics};

    #[test]
    fn only_fully_pinned_source_companions_can_reach_the_common_host() {
        assert!(source_companion_admission(true, true, [Physics]));
        for (zone_pin, manifest_pin) in [(false, true), (true, false), (false, false)] {
            assert!(!source_companion_admission(
                zone_pin,
                manifest_pin,
                [Physics],
            ));
            assert!(!source_companion_admission(
                zone_pin,
                manifest_pin,
                [Particles],
            ));
        }
        assert!(source_companion_admission(true, true, [Particles]));
        assert!(source_companion_admission(true, true, [Physics, Particles],));
    }
}
