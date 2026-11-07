//! Read an admitted module zone without a workspace, Vault or authoring session.
use crate::{Result, archive, manifest, orpmod};
use forge_core::{AreaId, ContentHash};
use forge_project::{
    module_package::{
        MAX_MODULE_MANIFEST_BYTES, MODULE_MANIFEST_PATH, ModuleFileRole, ModulePackageManifest,
    },
    zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    fs::{self, File},
    io::Read,
    path::{Path, PathBuf},
};

/// Canonical input and verified source blobs; renderer state is never persisted here.
pub struct LoadedModuleZone {
    pub package_root: PathBuf,
    pub manifest: ModulePackageManifest,
    pub archive: Vec<u8>,
    pub zone: ZoneEditSession,
    pub source_blobs: BTreeMap<ContentHash, Vec<u8>>,
}

impl LoadedModuleZone {
    /// A host cannot execute modules whose placement companions are not connected.
    /// Inspection and authoring can still load their verified transport.
    pub fn reject_unhandled_placement_companions(&self) -> Result<()> {
        if !self.manifest.placement_companions.is_empty() {
            return Err("Le module contient des effets ou composants physiques qui ne sont pas encore pris en charge par le jeu".into());
        }
        Ok(())
    }

    /// Temporary fail-closed guard for scene hosts which do not render declared ocean inputs.
    pub fn reject_unrendered_ocean(&self) -> Result<()> {
        if self
            .manifest
            .ocean_inputs
            .iter()
            .any(|binding| binding.area_id == self.zone.head().base.terrain.area_id)
        {
            return Err("The selected area declares ocean inputs but the module water renderer is not yet connected".into());
        }
        Ok(())
    }

    /// Resolve only FFT inputs explicitly associated with the selected area.
    pub fn ocean(&self) -> Result<Option<crate::module_ocean::ResolvedOcean>> {
        let files: BTreeMap<_, _> = self
            .manifest
            .files
            .iter()
            .map(|file| (file.path.as_str(), file))
            .collect();
        crate::module_ocean::resolve(
            &self.manifest,
            self.zone.head().base.terrain.area_id,
            |path, maximum| {
                let file = files
                    .get(path)
                    .ok_or("Runtime payload is absent from the module inventory")?;
                read_pinned(&self.package_root, path, file.bytes, file.sha256, maximum)
            },
        )
    }

    /// Resolve the selected area's explicit terrain package against its effective source.
    pub fn terrain(&self) -> Result<Option<crate::module_terrain::ResolvedTerrain>> {
        let document = self.zone.head().effective()?.0;
        crate::module_terrain::resolve(
            &self.manifest,
            document.area_id,
            &document,
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    /// Resolve only the source object palette explicitly bound to this area.
    pub fn objects(&self) -> Result<Option<crate::module_objects::ResolvedObjects>> {
        crate::module_objects::resolve(
            &self.manifest,
            self.zone.head().base.terrain.area_id,
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    /// Resolve fish profiles explicitly bound to this area's admitted resources.
    /// Rendering and module API admission remain separate from this accessor.
    pub fn fish_profiles(&self) -> Result<Vec<crate::module_fish::ResolvedFish>> {
        crate::module_fish::resolve(
            &self.manifest,
            self.zone.head().base.terrain.area_id,
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    pub fn local_lights(&self) -> Result<crate::module_local_lights::ResolvedModuleLocalLights> {
        crate::module_local_lights::resolve_zone(&self.manifest, &self.zone, |path, maximum| {
            self.read_payload(path, maximum)
        })
    }
    /// Decode only the environment explicitly bound to the selected installed area.
    pub fn environment(
        &self,
    ) -> Result<Option<crate::module_environment::ResolvedModuleEnvironment>> {
        crate::module_environment::resolve(
            &self.manifest,
            self.zone.head().base.terrain.area_id,
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    /// Decode exact installed source-profile resources; native render admission is separate.
    pub fn vegetation_imported(
        &self,
    ) -> Result<Option<crate::module_vegetation::ResolvedModuleImported>> {
        crate::module_vegetation::resolve_imported(
            &self.manifest,
            self.zone.head(),
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    /// Read companion metadata only from the verified installed package. No external kit lookup.
    pub fn vegetation_species(
        &self,
    ) -> Result<Option<crate::module_vegetation::ResolvedModuleSpecies>> {
        let bytes =
            self.read_payload(&self.manifest.asset_catalog_path, MAX_MODULE_MANIFEST_BYTES)?;
        let catalog = serde_json::from_slice(&bytes).map_err(|error| error.to_string())?;
        crate::module_vegetation::resolve_species(
            &self.manifest,
            self.zone.head(),
            &catalog,
            |path, maximum| self.read_payload(path, maximum),
        )
    }

    /// Read a declared payload from the same bounded buffer whose pin is checked.
    pub fn read_payload(&self, path: &str, maximum: usize) -> Result<Vec<u8>> {
        let file = self
            .manifest
            .files
            .iter()
            .find(|file| file.path == path)
            .ok_or("Runtime payload is absent from the module inventory")?;
        read_pinned(&self.package_root, path, file.bytes, file.sha256, maximum)
    }
}

/// Verify the exact installed transport descriptor and every payload, after loading
/// and before scene construction. The zone archive pin remains a separate contract.
pub fn verify_transport_revision(loaded: &LoadedModuleZone, expected: ContentHash) -> Result<()> {
    let actual = inspect_installed(&loaded.package_root, &expected.to_string())?;
    if actual != loaded.manifest {
        return Err("Installed transport changed after the module was loaded".into());
    }
    Ok(())
}

/// `.orpmod` inputs require an absolute cache root. Admitted directories are read in place.
/// Cache revisions are immutable; existing or interrupted directories are never overwritten.
pub fn load(
    package_or_directory: &Path,
    area_id: AreaId,
    cache_root: Option<&Path>,
) -> Result<LoadedModuleZone> {
    let input = step(
        package_or_directory,
        "resolve absolute input",
        absolute(package_or_directory),
    )?;
    step(
        &input,
        "check input ancestor chain",
        manifest::safe_chain(&input),
    )?;
    let input = step(&input, "canonicalize input", fs::canonicalize(&input))?;
    let (root, module) = if input.is_dir() {
        let module = step(&input, "admit module directory", admitted_directory(&input))?;
        require_zone(&module, area_id)?;
        (input, module)
    } else {
        cached_archive(
            &input,
            area_id,
            cache_root.ok_or("An absolute module cache root is required")?,
        )
        .map_err(|error| {
            format!(
                "Module runtime: prepare archive cache for {}: {error}",
                input.display()
            )
        })?
    };
    let (archive, zone) = read_zone(&root, &module, area_id)?;
    let selected = module
        .zones
        .iter()
        .find(|zone| zone.area_id == area_id)
        .ok_or("Requested area is absent from this module")?;
    let source_blobs = read_sources(&root, &module.files, &selected.source_blobs)?;
    Ok(LoadedModuleZone {
        package_root: root,
        manifest: module,
        archive,
        zone,
        source_blobs,
    })
}

/// Read only a pinned zone archive from an already admitted module. Callers
/// must first validate the containing package and its revision.
fn read_zone(
    root: &Path,
    module: &ModulePackageManifest,
    area_id: AreaId,
) -> Result<(Vec<u8>, ZoneEditSession)> {
    let selected = module
        .zones
        .iter()
        .find(|zone| zone.area_id == area_id)
        .ok_or("Requested area is absent from this module")?;
    let archive_file = module
        .files
        .iter()
        .find(|file| file.path == selected.archive_path)
        .ok_or("Selected zone archive is absent from module inventory")?;
    let archive = read_pinned(
        root,
        &archive_file.path,
        archive_file.bytes,
        archive_file.sha256,
        forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES,
    )?;
    step(
        &root.join(&archive_file.path),
        "validate selected zone archive",
        module.validate_zone_archive(area_id, &archive),
    )?;
    let zone = step(
        &root.join(&archive_file.path),
        "decode selected zone archive",
        ZoneEditSession::from_archive_bytes(&archive),
    )?;
    Ok((archive, zone))
}

/// Project the authored name without loading a zone's source blob inventory.
pub(crate) fn admitted_zone(
    root: &Path,
    module: &ModulePackageManifest,
    area_id: AreaId,
) -> Result<(String, ZoneEditSession)> {
    let (_, zone) = read_zone(root, module, area_id)?;
    let name = zone.head().effective()?.0.name;
    let name = name.trim();
    if name.is_empty() || name.len() > 128 || name.chars().any(char::is_control) {
        return Err("Invalid player-facing zone name".into());
    }
    Ok((name.to_owned(), zone))
}

fn source_budget(
    files: &[forge_project::module_package::ModuleFile],
    hashes: &[ContentHash],
) -> Result<()> {
    let mut remaining = forge_project::workspace::export::MAX_MODULE_EXPORT_SOURCE_BYTES;
    for hash in hashes {
        let file = files
            .iter()
            .find(|file| file.role == ModuleFileRole::SourceBlob && file.sha256 == *hash)
            .ok_or("Selected source blob is absent")?;
        let length = usize::try_from(file.bytes).map_err(|e| e.to_string())?;
        if length == 0 || length > forge_project::workspace::source_blobs::MAX_SOURCE_BLOB_BYTES {
            return Err("Selected source exceeds the runtime's 64 MiB per-blob budget".into());
        }
        remaining = remaining
            .checked_sub(length)
            .ok_or("Selected module sources exceed the runtime's 256 MiB admission budget")?;
    }
    Ok(())
}

fn read_sources(
    root: &Path,
    files: &[forge_project::module_package::ModuleFile],
    hashes: &[ContentHash],
) -> Result<BTreeMap<ContentHash, Vec<u8>>> {
    // Validate every size before opening or allocating even the first source.
    source_budget(files, hashes)?;
    let mut source_blobs = BTreeMap::new();
    for hash in hashes {
        let file = files
            .iter()
            .find(|file| file.role == ModuleFileRole::SourceBlob && file.sha256 == *hash)
            .ok_or("Selected source blob is absent")?;
        // Raster dimensions cap one blob at 64 MiB; imported-instance blocks
        // have a smaller bound in their canonical source document.
        let bytes = read_pinned(root, &file.path, file.bytes, *hash, 64 * 1024 * 1024)?;
        source_blobs.insert(*hash, bytes);
    }
    Ok(source_blobs)
}

fn absolute(path: &Path) -> Result<PathBuf> {
    if path.is_absolute() {
        Ok(path.to_path_buf())
    } else {
        std::env::current_dir()
            .map(|root| root.join(path))
            .map_err(|error| error.to_string())
    }
}

fn require_zone(module: &ModulePackageManifest, area: AreaId) -> Result<()> {
    if module.zones.iter().any(|zone| zone.area_id == area) {
        Ok(())
    } else {
        Err("Requested area is absent from this module".into())
    }
}

fn admitted_directory(root: &Path) -> Result<ModulePackageManifest> {
    let descriptor_path = step(
        root,
        "resolve installed descriptor path",
        manifest::confined(root, "package-manifest.json"),
    )?;
    if !descriptor_path.try_exists().map_err(|error| {
        format!(
            "Module runtime: inspect descriptor {}: {error}",
            descriptor_path.display()
        )
    })? {
        return step(
            root,
            "admit source directory",
            orpmod::validate_directory(root, false),
        );
    }
    let descriptor: manifest::Descriptor = step(
        &descriptor_path,
        "read installed descriptor",
        crate::read_json(&descriptor_path),
    )?;
    let revision = crate::hash(&crate::json(&descriptor)?);
    inspect_installed(root, &revision)
}

pub(crate) fn inspect_installed(root: &Path, revision: &str) -> Result<ModulePackageManifest> {
    let descriptor = step(
        root,
        "verify installed descriptor and payload inventory",
        archive::verify_directory(root, revision),
    )?;
    let pin = descriptor
        .files
        .iter()
        .find(|file| file.path == MODULE_MANIFEST_PATH)
        .ok_or("Installed directory does not contain an .orpmod v2 manifest")?;
    ModulePackageManifest::from_bytes(&read_pinned(
        root,
        MODULE_MANIFEST_PATH,
        pin.bytes,
        ContentHash::parse(&pin.sha256).map_err(|error| error.to_string())?,
        MAX_MODULE_MANIFEST_BYTES,
    )?)
    .map_err(|error| error.to_string())
}

fn cached_archive(
    input: &Path,
    area: AreaId,
    cache: &Path,
) -> Result<(PathBuf, ModulePackageManifest)> {
    if !input
        .extension()
        .and_then(|extension| extension.to_str())
        .is_some_and(|extension| extension.eq_ignore_ascii_case("orpmod"))
    {
        return Err("Native module loading requires an .orpmod v2 archive".into());
    }
    let expected = step(input, "admit archive", orpmod::inspect(input, false))?;
    require_zone(&expected, area)?;
    let (_, revision) = step(
        input,
        "read archive transport descriptor",
        archive::inspect(input),
    )?;
    if !cache.is_absolute() {
        return Err("An absolute module cache root is required".into());
    }
    step(
        cache,
        "check cache ancestor chain",
        manifest::safe_chain(cache),
    )?;
    let parent = cache.join(expected.module_id.to_string());
    let destination = parent.join(&revision);
    step(
        &destination,
        "check cache revision ancestor chain",
        manifest::safe_chain(&destination),
    )?;
    if destination.try_exists().map_err(|error| {
        format!(
            "Module runtime: inspect cache revision {}: {error}",
            destination.display()
        )
    })? {
        step(
            &destination,
            "verify existing cache revision",
            archive::verify_directory(&destination, &revision),
        )?;
        let actual = admitted_directory(&destination)?;
        if actual != expected {
            return Err("Cached module manifest differs from archive".into());
        }
        return Ok((destination, actual));
    }
    step(
        &parent,
        "create module cache parent",
        fs::create_dir_all(&parent),
    )?;
    let stage = parent.join(format!(".module-{}", crate::attempt()?));
    step(
        &stage,
        &format!("extract archive {}", input.display()),
        archive::extract(input, &stage, &revision),
    )?;
    step(
        &stage,
        "verify staged cache revision",
        archive::verify_directory(&stage, &revision),
    )?;
    let actual = admitted_directory(&stage)?;
    if actual != expected {
        return Err("Module changed while preparing its runtime cache".into());
    }
    if let Err(error) = fs::rename(&stage, &destination) {
        // A concurrent successful loader may have published the same revision.
        // Validate that winner; retain our private staging for diagnosis.
        if !destination.try_exists().map_err(|probe| {
            format!(
                "Module runtime: inspect concurrent cache revision {}: {probe}",
                destination.display()
            )
        })? {
            return Err(format!(
                "Module runtime: promote cache {} to {}: {error}",
                stage.display(),
                destination.display()
            ));
        }
        step(
            &destination,
            "verify concurrent cache revision",
            archive::verify_directory(&destination, &revision),
        )?;
        if admitted_directory(&destination)? != expected {
            return Err("Concurrent module cache revision differs".into());
        }
    }
    Ok((destination, expected))
}

fn read_pinned(
    root: &Path,
    path: &str,
    bytes: u64,
    hash: ContentHash,
    maximum: usize,
) -> Result<Vec<u8>> {
    if bytes > maximum as u64 {
        return Err("Module source exceeds runtime read bound".into());
    }
    let path = step(
        &root.join(path),
        "resolve pinned payload",
        manifest::confined(root, path),
    )?;
    let file = step(&path, "open pinned payload", File::open(&path))?;
    let metadata = step(&path, "inspect pinned payload", file.metadata())?;
    if !metadata.is_file() || metadata.len() != bytes {
        return Err(format!(
            "Module runtime: pinned payload length changed after admission: {}",
            path.display()
        ));
    }
    let mut buffer = Vec::with_capacity(usize::try_from(bytes).map_err(|error| error.to_string())?);
    file.take(bytes + 1)
        .read_to_end(&mut buffer)
        .map_err(|error| {
            format!(
                "Module runtime: read pinned payload {}: {error}",
                path.display()
            )
        })?;
    if buffer.len() as u64 != bytes || forge_core::bytes_sha256(&buffer) != hash {
        return Err(format!(
            "Module runtime: pinned payload integrity changed after admission: {}",
            path.display()
        ));
    }
    Ok(buffer)
}

fn step<T, E: std::fmt::Display>(
    path: &Path,
    operation: &str,
    result: std::result::Result<T, E>,
) -> Result<T> {
    result.map_err(|error| format!("Module runtime: {operation} [{}]: {error}", path.display()))
}

#[cfg(test)]
mod source_tests {
    use super::*;
    use forge_project::module_package::ModuleFile;

    #[test]
    fn source_metadata_is_bounded_before_opening_or_allocating_any_payload() {
        let hashes: Vec<_> = (0_u8..5)
            .map(|byte| forge_core::bytes_sha256(&[byte]))
            .collect();
        let mut files: Vec<_> = hashes
            .iter()
            .map(|hash| ModuleFile {
                path: format!("sources/{hash}.bin"),
                bytes: 64 * 1024 * 1024,
                sha256: *hash,
                role: ModuleFileRole::SourceBlob,
            })
            .collect();
        source_budget(&files, &hashes[..4]).unwrap();
        assert!(
            read_sources(Path::new("nonexistent-root"), &files, &hashes)
                .unwrap_err()
                .contains("256 MiB")
        );
        files[0].bytes += 1;
        assert!(
            source_budget(&files, &hashes[..1])
                .unwrap_err()
                .contains("64 MiB")
        );
        files[0].bytes = 0;
        assert!(source_budget(&files, &hashes[..1]).is_err());
        assert!(source_budget(&files, &[forge_core::bytes_sha256(b"absent")]).is_err());
    }
}
