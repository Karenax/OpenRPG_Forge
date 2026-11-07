//! Private saved-light qualification data, using the ordinary archive and package admission.
//! This does not qualify Studio authoring, a rendered frame or source-asset visual parity.
use forge_assets::source_local_lights::SourceLocalLightsPackage;
use forge_core::{CommandId, ContentHash, EntityId, bytes_sha256};
use forge_delivery::{
    manifest::{Descriptor, Kind, Rights},
    module_local_lights, module_runtime, orpmod,
};
use forge_project::{
    module_package::{ModuleFile, ModuleFilePin, ModuleFileRole, ModuleLocalLightPackage},
    zone_authoring::{
        placements::LocalLightPackageReference,
        session::{ZoneAction, ZoneRequest},
    },
};
use serde::Deserialize;
use std::{
    collections::BTreeMap,
    fs,
    io::Read,
    path::{Path, PathBuf},
};

type Result<T> = std::result::Result<T, Box<dyn std::error::Error>>;

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Request {
    schema: String,
    placement: EntityId,
    #[serde(deserialize_with = "nullable_reference")]
    reference: Option<Box<LocalLightPackageReference>>,
    #[serde(default)]
    solar_clock: Option<forge_project::module_package::ModuleSolarClock>,
}

fn nullable_reference<'de, D: serde::Deserializer<'de>>(
    deserializer: D,
) -> std::result::Result<Option<Box<LocalLightPackageReference>>, D::Error> {
    Option::deserialize(deserializer)
}

fn bounded(path: &Path, maximum: usize) -> Result<Vec<u8>> {
    forge_delivery::manifest::safe_chain(path)?;
    let file = fs::File::open(path)?;
    if !file.metadata()?.is_file() {
        return Err("Fixture input must be a regular file".into());
    }
    let mut bytes = Vec::new();
    file.take(u64::try_from(maximum)? + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() > maximum {
        return Err("Fixture input exceeds its byte limit".into());
    }
    Ok(bytes)
}

fn main() -> Result<()> {
    let args: Vec<_> = std::env::args_os().skip(1).collect();
    let [input, output, source_manifest, source_pin, request] = args.as_slice() else {
        return Err("INPUT_INSTALLED_MODULE OUTPUT_NEW_DIRECTORY LIGHT_MANIFEST LIGHT_MANIFEST_SHA256 REQUEST_JSON".into());
    };
    let (input, output, source_manifest, request) = (
        PathBuf::from(input),
        PathBuf::from(output),
        PathBuf::from(source_manifest),
        PathBuf::from(request),
    );
    if [&input, &output, &source_manifest, &request]
        .iter()
        .any(|p| !p.is_absolute())
        || output.exists()
    {
        return Err("Inputs must be absolute and output must be new".into());
    }
    forge_delivery::manifest::safe_chain(&output)?;
    let admitted = orpmod::validate_directory(&input, false)?;
    if admitted.zones.len() != 1
        || admitted.environments.len() != 1
        || admitted.local_light_packages.len() > 1
    {
        return Err(
            "This fixture requires one source-environment area and at most one light package"
                .into(),
        );
    }
    let mut loaded = module_runtime::load(&input, admitted.zones[0].area_id, None)?;
    let imported_vegetation = loaded.vegetation_imported()?.is_some();
    if admitted.provenance.rights != forge_project::module_package::ModuleRights::PrivateLocalOnly {
        return Err("This fixture requires an explicitly private qualification input".into());
    }
    let request_bytes = bounded(&request, 1024 * 1024)?;
    let request: Request = serde_json::from_slice(&request_bytes)?;
    if request.schema != "forge.gate7.saved-light-fixture.v1" {
        return Err("Unknown fixture request schema".into());
    }
    let pin: ContentHash = source_pin
        .to_str()
        .ok_or("Non-Unicode source pin")?
        .parse()?;
    let manifest = bounded(&source_manifest, 2 * 1024 * 1024)?;
    let parent = source_manifest
        .parent()
        .ok_or("Source manifest has no parent")?;
    let lights = SourceLocalLightsPackage::load(&manifest, pin, |name, limit| {
        bounded(&parent.join(name), limit).map_err(|e| e.to_string())
    })?;
    let original_head = loaded.zone.head().content_hash()?;
    let mut corrections = loaded.zone.head().corrections.clone();
    let placement = corrections
        .placements
        .as_mut()
        .and_then(|p| p.iter_mut().find(|p| p.id == request.placement))
        .ok_or("Fixture carrier is not a current explicit manual placement")?;
    if let Some(reference) = &request.reference {
        reference.validate(placement.asset_hash)?;
    }
    if placement.locked {
        return Err("Qualification carrier is locked".into());
    }
    if request.reference.as_ref().is_some_and(|reference| {
        reference.manifest_sha256 != pin
            || reference.descriptor_sha256 != lights.descriptor().content_hash()
    }) {
        return Err("Fixture reference differs from its verified source package".into());
    }
    let reference_changed = placement.local_lights != request.reference;
    placement.local_lights = request.reference;
    if reference_changed {
        loaded.zone.execute(&ZoneRequest {
            command_id: CommandId::deterministic(format!(
                "gate7/saved-lights/{}",
                bytes_sha256(&request_bytes)
            )),
            expected_revision: loaded.zone.head().revision,
            expected_hash: original_head,
            action: ZoneAction::edit(corrections),
        })?;
    }
    let archive = loaded.zone.archive_bytes()?;
    if loaded.zone.required_local_light_packages()?
        != BTreeMap::from([(pin, lights.descriptor().content_hash())])
    {
        return Err("This qualification fixture requires exactly the supplied light package, including history".into());
    }
    let payloads = module_local_lights::transport_payloads(&manifest, &lights)?;
    let mut module = loaded.manifest.clone();
    // Effective documents carry the new head revision after the ordinary edit.
    // Repin that document; retain every immutable prototype/resource pin.
    let (_, vegetation) = loaded.zone.head().effective()?;
    for binding in &mut module.vegetation {
        binding.vegetation_sha256 = vegetation
            .as_ref()
            .ok_or("Vegetation binding lost its authored document")?
            .content_hash()?;
    }
    let old_paths: std::collections::BTreeSet<_> = module
        .local_light_packages
        .iter()
        .flat_map(|p| std::iter::once(&p.manifest).chain(&p.resources))
        .map(|p| p.path.clone())
        .collect();
    if module
        .local_light_packages
        .iter()
        .any(|p| p.manifest.sha256 != pin)
    {
        return Err("Fixture cannot replace an unrelated historical light package".into());
    }
    module.files.retain(|f| !old_paths.contains(&f.path));
    module
        .provenance
        .license_paths
        .retain(|p| !old_paths.contains(p));
    module.local_light_packages.clear();
    if let Some(clock) = request.solar_clock {
        clock.validate()?;
        module.environments[0].solar_clock = Some(clock);
    }
    module.module_version = "1.0.8".into();
    module.name = "Qualification des lumières enregistrées".into();
    module.compatibility.module_api_version =
        forge_project::module_package::MODULE_LOCAL_LIGHTS_API_VERSION;
    if module.environments[0].solar_clock.is_some() {
        module.compatibility.module_api_version =
            forge_project::module_package::MODULE_SOLAR_CLOCK_API_VERSION;
        module.module_version = "1.0.9".into();
    }
    module.zones[0].archive_sha256 = bytes_sha256(&archive);
    module.zones[0].head_hash = loaded.zone.head().content_hash()?;
    module.provenance.source_revision = format!("gate7/saved-lights/{}", module.zones[0].head_hash);
    module.provenance.source = format!(
        "Private saved-light fixture of head {original_head}; carrier attachment is explicitly authored qualification data, not source-asset parity. Request {}",
        bytes_sha256(&request_bytes)
    );
    let path = module_local_lights::manifest_path(pin);
    let mut resources = Vec::new();
    for (name, bytes) in &payloads {
        let role = if bytes.as_slice() == lights.license() {
            module.provenance.license_paths.push(name.clone());
            ModuleFileRole::License
        } else {
            ModuleFileRole::LocalLights
        };
        module.files.push(ModuleFile {
            path: name.clone(),
            bytes: u64::try_from(bytes.len())?,
            sha256: bytes_sha256(bytes),
            role,
        });
        if name != &path {
            resources.push(ModuleFilePin {
                path: name.clone(),
                sha256: bytes_sha256(bytes),
            });
        }
    }
    module.local_light_packages.push(ModuleLocalLightPackage {
        areas: vec![module.zones[0].area_id],
        manifest: ModuleFilePin { path, sha256: pin },
        descriptor_sha256: lights.descriptor().content_hash(),
        resources,
    });
    let zone_file = module
        .files
        .iter_mut()
        .find(|f| f.path == module.zones[0].archive_path)
        .ok_or("Zone payload absent")?;
    zone_file.sha256 = bytes_sha256(&archive);
    zone_file.bytes = u64::try_from(archive.len())?;
    module.validate_zone_archive(module.zones[0].area_id, &archive)?;
    let descriptor = module.to_bytes()?;
    publish(&output, &loaded, &module, &archive, &payloads, &descriptor)?;
    let installed = module_runtime::load(&output.join("source"), module.zones[0].area_id, None)?;
    if installed.vegetation_imported()?.is_some() != imported_vegetation {
        return Err("Saved-light fixture changed its admitted vegetation binding".into());
    }
    println!(
        "{}",
        serde_json::json!({"source":output.join("source"),"package":output.join("lights.orpmod"),"original_head":original_head,"head":module.zones[0].head_hash,"archive_sha256":module.zones[0].archive_sha256,"area":module.zones[0].area_id,"package_sha256":forge_delivery::file_hash(&output.join("lights.orpmod"))?,"payloads":module.files.len()+1,"request_sha256":bytes_sha256(&request_bytes),"light_manifest_sha256":pin,"native_qualified":false,"gate_closed":false})
    );
    Ok(())
}

fn publish(
    output: &Path,
    loaded: &module_runtime::LoadedModuleZone,
    module: &forge_project::module_package::ModulePackageManifest,
    archive: &[u8],
    payloads: &BTreeMap<String, Vec<u8>>,
    descriptor: &[u8],
) -> Result<()> {
    fs::create_dir(output)?;
    let source = output.join("source");
    fs::create_dir(&source)?;
    for file in &module.files {
        let target = source.join(&file.path);
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent)?;
        }
        if file.path == module.zones[0].archive_path {
            fs::write(&target, archive)?;
        } else if let Some(bytes) = payloads.get(&file.path) {
            if loaded
                .manifest
                .files
                .iter()
                .any(|f| f.path == file.path && f.sha256 == file.sha256 && f.bytes == file.bytes)
            {
                loaded.read_payload(&file.path, 64 * 1024 * 1024)?;
                fs::hard_link(loaded.package_root.join(&file.path), &target)?;
            } else {
                fs::write(&target, bytes)?;
            }
        } else {
            loaded.read_payload(&file.path, 64 * 1024 * 1024)?;
            fs::hard_link(loaded.package_root.join(&file.path), &target)?;
        }
    }
    fs::write(source.join("module.json"), descriptor)?;
    let package = output.join("lights.orpmod");
    let (_, transport) = orpmod::pack(
        &source,
        Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: module.module_id.to_string(),
            label: module.name.clone(),
            version: module.module_version.clone(),
            kind: Kind::Module,
            platform: "any".into(),
            source_revision: module.provenance.source_revision.clone(),
            rights: Rights::PrivateLocalOnly,
            license_paths: module.provenance.license_paths.clone(),
            files: vec![],
            product: None,
        },
        &package,
        false,
    )?;
    let installed = module_runtime::load(
        &package,
        module.zones[0].area_id,
        Some(&output.join("cache")),
    )?;
    if installed.local_lights()?.len() != module.local_light_packages.len()
        || installed.environment()?.is_none()
    {
        return Err("Saved-light fixture failed installed admission".into());
    }
    fs::write(
        output.join("transport-sha256.txt"),
        format!("{transport}\n"),
    )?;
    Ok(())
}
