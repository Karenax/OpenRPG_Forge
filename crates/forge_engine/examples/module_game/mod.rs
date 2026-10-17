//! Generic installed-module entry point shared by every admitted adventure.
mod evidence;
mod evidence_contract;
pub mod input;
mod manual;
mod output;
mod scene;

use std::{error::Error, ffi::OsString};

pub fn run(arguments: impl IntoIterator<Item = OsString>) -> Result<(), Box<dyn Error>> {
    let options = input::ModuleGameOptions::parse(arguments)?;
    let loaded = forge_delivery::module_runtime::load(
        &options.package,
        options.area_id,
        Some(&options.cache_root),
    )?;
    input::verify_expected_zone(
        options.expected_zone,
        loaded.zone.head().content_hash()?,
        &loaded.archive,
    )?;
    if let Some(expected) = options.expected_package_manifest {
        forge_delivery::module_runtime::verify_transport_revision(&loaded, expected)?;
    }
    scene::run(options, loaded).map_err(Into::into)
}
