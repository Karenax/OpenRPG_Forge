//! Original source files are inert transport data, decoded only for the existing
//! immutable package reader. No source script is written as an executable payload.
use crate::Result;
use base64::{Engine, engine::general_purpose::STANDARD};
use forge_assets::source_local_lights::SourceLocalLightsPackage;
use forge_core::{AreaId, ContentHash, bytes_sha256};
use forge_project::module_package::{
    ModuleFilePin, ModuleFileRole, ModuleLocalLightPackage, ModulePackageManifest,
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};

pub type ResolvedModuleLocalLights = BTreeMap<ContentHash, Arc<SourceLocalLightsPackage>>;
pub const PREFIX: &str = "_forge-export/local-lights/";
use forge_assets::source_local_lights::package::transport::{decode, encoded_bound, member_path};
const MARKER: &[u8] = b"b64:";

#[must_use]
pub fn manifest_path(hash: ContentHash) -> String {
    format!("{PREFIX}{hash}/manifest.json")
}

pub(crate) fn license_path(hash: ContentHash) -> String {
    format!("{PREFIX}{hash}/license.txt")
}

pub(crate) fn prepare_export(
    manifest_hash: ContentHash,
    descriptor: ContentHash,
    area: AreaId,
    payloads: &BTreeMap<String, Vec<u8>>,
) -> Result<ModuleLocalLightPackage> {
    let path = manifest_path(manifest_hash);
    let manifest = payloads
        .get(&path)
        .ok_or("Source-light export manifest is missing")?;
    let expected = SourceLocalLightsPackage::resource_pins(manifest, manifest_hash)
        .map_err(|e| e.to_string())?;
    let license = SourceLocalLightsPackage::license_pin(manifest, manifest_hash)
        .map_err(|e| e.to_string())?;
    let prefix = path
        .strip_suffix("manifest.json")
        .ok_or("Invalid source-light transport prefix")?;
    let mut resources = BTreeMap::new();
    let loaded = SourceLocalLightsPackage::load(manifest, manifest_hash, |name, maximum| {
        let original = &expected[name];
        let member = member_path(prefix, original.sha256, license.sha256);
        let bytes = payloads
            .get(&member)
            .ok_or("Source-light export member is missing")?;
        resources.insert(
            member.clone(),
            ModuleFilePin {
                path: member,
                sha256: bytes_sha256(bytes),
            },
        );
        decode(bytes.clone(), original.sha256 == license.sha256, maximum)
    })
    .map_err(|e| e.to_string())?;
    if loaded.descriptor().content_hash() != descriptor {
        return Err("Source-light export descriptor pin differs".into());
    }
    Ok(ModuleLocalLightPackage {
        areas: vec![area],
        manifest: ModuleFilePin {
            path,
            sha256: manifest_hash,
        },
        descriptor_sha256: descriptor,
        resources: resources.into_values().collect(),
    })
}

/// Repackage verified original bytes without renaming or changing their source
/// manifest. Runtime decoding must recover exactly the original SHA-256 values.
pub fn transport_payloads(
    manifest: &[u8],
    package: &SourceLocalLightsPackage,
) -> Result<BTreeMap<String, Vec<u8>>> {
    if bytes_sha256(manifest) != package.receipt_hash() {
        return Err("Source-light manifest pin differs".into());
    }
    let path = manifest_path(package.receipt_hash());
    let prefix = path
        .strip_suffix("manifest.json")
        .ok_or("Invalid source-light transport prefix")?;
    let license = bytes_sha256(package.license());
    let mut payloads = BTreeMap::from([(path.clone(), manifest.to_vec())]);
    for (_, raw) in package.files() {
        let hash = bytes_sha256(raw);
        let bytes = if hash == license {
            raw.to_vec()
        } else {
            let mut bytes = MARKER.to_vec();
            bytes.extend_from_slice(STANDARD.encode(raw).as_bytes());
            bytes
        };
        payloads.insert(member_path(prefix, hash, license), bytes);
    }
    Ok(payloads)
}

pub fn resolve(
    module: &ModulePackageManifest,
    area: AreaId,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<ResolvedModuleLocalLights> {
    module.validate().map_err(|error| error.to_string())?;
    if !module.zones.iter().any(|z| z.area_id == area) {
        return Err("Source-light area is absent from the module".into());
    }
    let mut resolved = BTreeMap::new();
    for package in module
        .local_light_packages
        .iter()
        .filter(|p| p.areas.contains(&area))
    {
        let loaded = load(module, package, &mut read)?;
        resolved.insert(package.manifest.sha256, Arc::new(loaded));
    }
    Ok(resolved)
}

pub fn resolve_zone(
    module: &ModulePackageManifest,
    zone: &forge_project::zone_authoring::session::ZoneEditSession,
    read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<ResolvedModuleLocalLights> {
    let area = zone.head().base.terrain.area_id;
    if !module.zones.iter().any(|z| {
        z.area_id == area
            && zone
                .head()
                .content_hash()
                .is_ok_and(|hash| hash == z.head_hash)
    }) {
        return Err("Source-light zone revision differs from the admitted module".into());
    }
    let packages = resolve(module, area, read)?;
    zone.visit_local_light_placements(|placement| {
        let reference = placement
            .local_lights
            .as_ref()
            .ok_or("Source-light reference absent")?;
        reference.validate(placement.asset_hash)?;
        let package = packages
            .get(&reference.manifest_sha256)
            .ok_or("Referenced source-light package is absent")?;
        if package.descriptor().content_hash() != reference.descriptor_sha256 {
            return Err("Source-light reference descriptor differs".into());
        }
        let frames = package
            .descriptor()
            .selected_frame_parents(&reference.light_ids)
            .map_err(|e| e.to_string())?;
        if frames
            .keys()
            .map(String::as_str)
            .ne(reference.anchors.iter().map(|a| a.frame_id.as_str()))
        {
            return Err("Source-light reference frame closure differs".into());
        }
        Ok(())
    })?;
    Ok(packages)
}

fn load(
    module: &ModulePackageManifest,
    package: &ModuleLocalLightPackage,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<SourceLocalLightsPackage> {
    let manifest = read_pin(module, &package.manifest, 2 * 1024 * 1024, read)?;
    let expected = SourceLocalLightsPackage::resource_pins(&manifest, package.manifest.sha256)
        .map_err(|e| e.to_string())?;
    let license = SourceLocalLightsPackage::license_pin(&manifest, package.manifest.sha256)
        .map_err(|e| e.to_string())?;
    let prefix = package
        .manifest
        .path
        .rsplit_once('/')
        .map_or(String::new(), |(parent, _)| format!("{parent}/"));
    let supplied: BTreeMap<_, _> = package
        .resources
        .iter()
        .map(|p| (p.path.as_str(), p))
        .collect();
    let mut used = BTreeSet::new();
    for raw in expected.values() {
        let path = member_path(&prefix, raw.sha256, license.sha256);
        if !supplied.contains_key(path.as_str()) {
            return Err("Source-light member is not declared".into());
        }
        used.insert(path);
    }
    if used.len() != supplied.len() {
        return Err("Source-light transport has extra members".into());
    }
    let license_path = member_path(&prefix, license.sha256, license.sha256);
    if !module
        .files
        .iter()
        .any(|f| f.path == license_path && f.role == ModuleFileRole::License)
        || !module.provenance.license_paths.contains(&license_path)
    {
        return Err("Source-light licence is not declared".into());
    }
    let loaded =
        SourceLocalLightsPackage::load(&manifest, package.manifest.sha256, |name, maximum| {
            let raw = &expected[name];
            let path = member_path(&prefix, raw.sha256, license.sha256);
            let bound = encoded_bound(maximum, raw.sha256 == license.sha256)?;
            let bytes = read_pin(module, supplied[path.as_str()], bound, read)?;
            decode(bytes, raw.sha256 == license.sha256, maximum)
        })
        .map_err(|e| e.to_string())?;
    if loaded.descriptor().content_hash() != package.descriptor_sha256 {
        return Err("Source-light descriptor pin differs".into());
    }
    Ok(loaded)
}

fn read_pin(
    module: &ModulePackageManifest,
    pin: &ModuleFilePin,
    maximum: usize,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    let file = module
        .files
        .iter()
        .find(|f| f.path == pin.path)
        .ok_or("Source-light payload absent")?;
    if file.bytes == 0 || file.bytes > maximum as u64 || file.sha256 != pin.sha256 {
        return Err("Source-light payload size or pin differs".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || bytes_sha256(&bytes) != pin.sha256 {
        return Err("Source-light payload bytes differ".into());
    }
    Ok(bytes)
}
