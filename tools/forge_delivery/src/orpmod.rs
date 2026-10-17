//! Semantic admission of `.orpmod` v2 using the existing streaming container.
//! Legacy `.forgepack` v1 remains a separate, unchanged delivery contract.
use crate::{
    Result, archive,
    manifest::{self, Descriptor, Entry, Kind, Rights},
};
use forge_assets::{
    governed_import::{
        DurableAssetSource, GovernedAssetFile, GovernedAssetKind, GovernedImportCatalog,
    },
    media_validation::{MediaFormat, MediaLimits, validate_media},
};
use forge_core::ContentHash;
use forge_project::module_package::{
    MAX_MODULE_MANIFEST_BYTES, MODULE_MANIFEST_PATH, ModuleFile, ModuleFileRole,
    ModulePackageManifest, ModuleRights,
};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs::{self, File},
    io::{Read, Seek, SeekFrom},
    path::{Path, PathBuf},
    sync::Arc,
};

/// Exact versions supported by this adapter; compatibility is never inferred.
pub const ENGINE_VERSION: &str = "0.1.0";
pub const MODULE_API_VERSION: u32 = 1;

/// Exact rights documents from an admitted, bounded in-memory origin package.
pub struct OriginDocuments {
    pub module: ModulePackageManifest,
    pub descriptor: Descriptor,
    pub module_manifest: Vec<u8>,
    pub transport_manifest: Vec<u8>,
    pub evidence: BTreeMap<String, Vec<u8>>,
}

/// Admit the exact immutable buffer without publishing files or touching a cache.
pub fn inspect_bytes(bytes: Arc<[u8]>, publication: bool) -> Result<ModulePackageManifest> {
    let (source, descriptor, _) = Source::memory(bytes)?;
    let module = validate(&source, publication)?;
    check_descriptor(&descriptor, &module)?;
    Ok(module)
}

pub fn read_origin_documents(bytes: Arc<[u8]>) -> Result<OriginDocuments> {
    let (source, descriptor, transport_manifest) = Source::memory(bytes)?;
    let module = validate(&source, false)?;
    check_descriptor(&descriptor, &module)?;
    let module_manifest = source.small(MODULE_MANIFEST_PATH, MAX_MODULE_MANIFEST_BYTES)?;
    let mut evidence = BTreeMap::new();
    for path in module
        .provenance
        .license_paths
        .iter()
        .chain(&module.provenance.proof_paths)
        .chain(std::iter::once(&module.provenance.credits_path))
    {
        let document = source.small(
            path,
            forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES as usize,
        )?;
        evidence.insert(path.clone(), document);
    }
    Ok(OriginDocuments {
        module,
        descriptor,
        module_manifest,
        transport_manifest,
        evidence,
    })
}

pub fn validate_directory(root: &Path, publication: bool) -> Result<ModulePackageManifest> {
    validate(&Source::directory(root, false)?, publication)
}

pub fn pack(
    root: &Path,
    descriptor: Descriptor,
    output: &Path,
    publication: bool,
) -> Result<(Descriptor, String)> {
    if !output.is_absolute() || output.extension().and_then(|s| s.to_str()) != Some("orpmod") {
        return Err("An absolute .orpmod output is required".into());
    }
    let module = validate_directory(root, publication)?;
    check_descriptor(&descriptor, &module)?;
    let staged = output
        .parent()
        .ok_or("Output parent missing")?
        .join(format!(".orpmod-{}.tmp", crate::attempt()?));
    let result = (|| {
        let packed = archive::pack(root, descriptor, &staged)?;
        if inspect(&staged, publication)? != module {
            return Err("Module changed during assembly".into());
        }
        // Reuse the transport's staging/rename protocol, including on volumes
        // without hard-link support. Never expose a partially written package.
        if output.try_exists().map_err(|e| e.to_string())? {
            return Err("Refusing to overwrite a module package".into());
        }
        fs::rename(&staged, output).map_err(|e| e.to_string())?;
        Ok(packed)
    })();
    let _ = fs::remove_file(&staged);
    result
}

pub fn inspect(path: &Path, publication: bool) -> Result<ModulePackageManifest> {
    let (source, descriptor) = Source::archive(path)?;
    let module = validate(&source, publication)?;
    check_descriptor(&descriptor, &module)?;
    Ok(module)
}

/// Called by installation verification before a package can become active.
/// Old delivery modules without `module.json` keep their v1 contract.
pub(crate) fn verify_installed(root: &Path, descriptor: &Descriptor) -> Result<()> {
    if descriptor
        .files
        .iter()
        .any(|f| f.path == MODULE_MANIFEST_PATH)
    {
        let module = validate(&Source::directory(root, true)?, false)?;
        check_descriptor(descriptor, &module)?;
    }
    Ok(())
}

fn check_descriptor(descriptor: &Descriptor, module: &ModulePackageManifest) -> Result<()> {
    let rights = match module.provenance.rights {
        ModuleRights::PrivateLocalOnly => Rights::PrivateLocalOnly,
        ModuleRights::Redistributable => Rights::Redistributable,
    };
    let license_paths: BTreeSet<_> = descriptor.license_paths.iter().collect();
    let module_licenses: BTreeSet<_> = module.provenance.license_paths.iter().collect();
    if descriptor.kind != Kind::Module
        || descriptor.id != module.module_id.to_string()
        || descriptor.version != module.module_version
        || descriptor.platform != "any"
        || descriptor.source_revision != module.provenance.source_revision
        || descriptor.rights != rights
        || license_paths != module_licenses
        || descriptor.product.is_some()
    {
        return Err(
            "Delivery descriptor disagrees with the semantic module identity, version or rights"
                .into(),
        );
    }
    Ok(())
}

enum Location {
    Directory(PathBuf),
    Archive(File, BTreeMap<String, u64>),
    Memory(Arc<[u8]>, BTreeMap<String, u64>),
}
struct Source {
    location: Location,
    files: BTreeMap<String, Entry>,
}

impl Source {
    fn memory(bytes: Arc<[u8]>) -> Result<(Self, Descriptor, Vec<u8>)> {
        if bytes.len() as u64 > forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES {
            return Err(
                "Origin module package exceeds the bounded in-memory admission budget".into(),
            );
        }
        let mut reader = std::io::Cursor::new(bytes.clone());
        let (descriptor, _) = archive::header_reader(&mut reader, bytes.len() as u64)?;
        let mut position = reader.position();
        let transport_manifest = bytes
            .get(16..usize::try_from(position).map_err(|e| e.to_string())?)
            .ok_or("Origin transport descriptor is out of bounds")?
            .to_vec();
        let mut offsets = BTreeMap::new();
        for entry in &descriptor.files {
            offsets.insert(entry.path.clone(), position);
            position = position
                .checked_add(entry.bytes)
                .ok_or("Origin module offset overflow")?;
        }
        let files = descriptor
            .files
            .iter()
            .map(|f| (f.path.clone(), f.clone()))
            .collect();
        Ok((
            Self {
                location: Location::Memory(bytes, offsets),
                files,
            },
            descriptor,
            transport_manifest,
        ))
    }

    fn directory(root: &Path, installed: bool) -> Result<Self> {
        let root = if root.is_absolute() {
            root.to_path_buf()
        } else {
            std::env::current_dir()
                .map_err(|e| e.to_string())?
                .join(root)
        };
        manifest::safe_chain(&root)?;
        let root = fs::canonicalize(root).map_err(|e| e.to_string())?;
        let mut files = BTreeMap::new();
        let mut pending = vec![root.clone()];
        let mut visited = 0_usize;
        while let Some(folder) = pending.pop() {
            for child in fs::read_dir(folder).map_err(|e| e.to_string())? {
                let child = child.map_err(|e| e.to_string())?;
                visited += 1;
                if visited > manifest::MAX_FILES {
                    return Err("Module directory inventory exceeds entry limit".into());
                }
                let path = child.path();
                manifest::no_links(&path)?;
                let relative = path
                    .strip_prefix(&root)
                    .map_err(|e| e.to_string())?
                    .to_str()
                    .ok_or("Non-Unicode module path")?
                    .replace('\\', "/");
                manifest::relative(&relative)?;
                let metadata = child.metadata().map_err(|e| e.to_string())?;
                if metadata.is_dir() {
                    pending.push(path);
                } else if metadata.is_file() {
                    if installed && relative == "package-manifest.json" {
                        continue;
                    }
                    if files.len() >= manifest::MAX_FILES
                        || metadata.len() > manifest::MAX_FILE_BYTES
                    {
                        return Err("Module inventory exceeds transport limits".into());
                    }
                    files.insert(
                        relative.clone(),
                        Entry {
                            path: relative,
                            bytes: metadata.len(),
                            sha256: String::new(),
                        },
                    );
                } else {
                    return Err("Module payload must contain regular files only".into());
                }
            }
        }
        Ok(Self {
            location: Location::Directory(root),
            files,
        })
    }

    fn archive(path: &Path) -> Result<(Self, Descriptor)> {
        let path = if path.is_absolute() {
            path.to_path_buf()
        } else {
            std::env::current_dir()
                .map_err(|e| e.to_string())?
                .join(path)
        };
        manifest::safe_chain(&path)?;
        let mut options = fs::OpenOptions::new();
        options.read(true);
        #[cfg(windows)]
        {
            use std::os::windows::fs::OpenOptionsExt;
            // Keep the same archive identity and refuse concurrent write/delete handles.
            options.share_mode(1);
        }
        let mut file = options.open(&path).map_err(|e| e.to_string())?;
        let (descriptor, _) = archive::header(&mut file)?;
        let mut position = file.stream_position().map_err(|e| e.to_string())?;
        let mut offsets = BTreeMap::new();
        for entry in &descriptor.files {
            offsets.insert(entry.path.clone(), position);
            position = position
                .checked_add(entry.bytes)
                .ok_or("Module offset overflow")?;
        }
        let files = descriptor
            .files
            .iter()
            .map(|f| (f.path.clone(), f.clone()))
            .collect();
        Ok((
            Self {
                location: Location::Archive(file, offsets),
                files,
            },
            descriptor,
        ))
    }

    fn reader(&self, path: &str) -> Result<Box<dyn Read>> {
        let entry = self
            .files
            .get(path)
            .ok_or_else(|| format!("Missing module payload: {path}"))?;
        match &self.location {
            Location::Directory(root) => {
                let file =
                    File::open(manifest::confined(root, path)?).map_err(|e| e.to_string())?;
                Ok(Box::new(file.take(entry.bytes.saturating_add(1))))
            }
            Location::Archive(archive, offsets) => {
                let mut file = archive.try_clone().map_err(|e| e.to_string())?;
                file.seek(SeekFrom::Start(
                    *offsets.get(path).ok_or("Missing module offset")?,
                ))
                .map_err(|e| e.to_string())?;
                Ok(Box::new(file.take(entry.bytes)))
            }
            Location::Memory(bytes, offsets) => {
                let mut reader = std::io::Cursor::new(bytes.clone());
                reader.set_position(*offsets.get(path).ok_or("Missing origin module offset")?);
                Ok(Box::new(reader.take(entry.bytes)))
            }
        }
    }

    fn small(&self, path: &str, limit: usize) -> Result<Vec<u8>> {
        if self.files.get(path).ok_or("Missing module document")?.bytes > limit as u64 {
            return Err(format!("Module document exceeds limit: {path}"));
        }
        let mut bytes = Vec::new();
        self.reader(path)?
            .take(limit as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|e| e.to_string())?;
        let entry = self.files.get(path).ok_or("Missing module document")?;
        if bytes.len() > limit || bytes.len() as u64 != entry.bytes {
            return Err(format!(
                "Module document changed size or exceeds limit: {path}"
            ));
        }
        if !entry.sha256.is_empty() && forge_core::bytes_sha256(&bytes).to_string() != entry.sha256
        {
            return Err(format!("Transport document integrity mismatch: {path}"));
        }
        Ok(bytes)
    }

    fn document(&self, file: &ModuleFile, limit: usize) -> Result<Vec<u8>> {
        let bytes = self.small(&file.path, limit)?;
        if bytes.len() as u64 != file.bytes || forge_core::bytes_sha256(&bytes) != file.sha256 {
            return Err(format!("Module document integrity mismatch: {}", file.path));
        }
        Ok(bytes)
    }

    fn digest(&self, path: &str, kind: Option<GovernedAssetKind>) -> Result<(u64, ContentHash)> {
        use sha2::{Digest, Sha256};
        let mut reader = HashingReader {
            reader: self.reader(path)?,
            hasher: Sha256::new(),
            bytes: 0,
        };
        let mut header = [0_u8; 20];
        let mut count = 0;
        while count < header.len() {
            let size = reader
                .read(&mut header[count..])
                .map_err(|e| e.to_string())?;
            if size == 0 {
                break;
            }
            count += size;
        }
        reject_program(path, &header[..count])?;
        if let Some(kind) = kind {
            media(
                path,
                kind,
                &header,
                count,
                self.files.get(path).ok_or("Missing media file")?.bytes,
                &mut reader,
            )?;
        }
        let mut buffer = [0_u8; 65536];
        loop {
            let count = reader.read(&mut buffer).map_err(|e| e.to_string())?;
            if count == 0 {
                break;
            }
        }
        Ok((
            reader.bytes,
            ContentHash::from_bytes(reader.hasher.finalize().into()),
        ))
    }
}

struct HashingReader<R> {
    reader: R,
    hasher: sha2::Sha256,
    bytes: u64,
}
impl<R: Read> Read for HashingReader<R> {
    fn read(&mut self, bytes: &mut [u8]) -> std::io::Result<usize> {
        use sha2::Digest;
        let count = self.reader.read(bytes)?;
        self.hasher.update(&bytes[..count]);
        self.bytes += count as u64;
        Ok(count)
    }
}

fn validate(source: &Source, publication: bool) -> Result<ModulePackageManifest> {
    let module_bytes = source.small(MODULE_MANIFEST_PATH, MAX_MODULE_MANIFEST_BYTES)?;
    let module = ModulePackageManifest::from_bytes(&module_bytes).map_err(|e| e.to_string())?;
    module
        .check_compatibility(ENGINE_VERSION, MODULE_API_VERSION)
        .map_err(|e| e.to_string())?;
    let publishing = publication || module.provenance.rights == ModuleRights::Redistributable;
    if publication && module.provenance.rights != ModuleRights::Redistributable {
        return Err("Module is private, publication refused".into());
    }
    if source.files.len() != module.files.len() + 1 {
        return Err("Module payload inventory is not closed".into());
    }
    let catalog_file = module
        .files
        .iter()
        .find(|f| f.path == module.asset_catalog_path)
        .ok_or("Missing asset catalog declaration")?;
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&source.document(catalog_file, MAX_MODULE_MANIFEST_BYTES)?)
            .map_err(|e| e.to_string())?;
    if publishing {
        catalog.validate_for_publication()
    } else {
        catalog.validate_for_local_use()
    }
    .map_err(|e| e.to_string())?;
    for binding in &module.gameplay_visuals {
        if !catalog.assets.iter().any(|asset| {
            asset.id == binding.asset.asset_id
                && asset.payload.sha256 == binding.asset.sha256
                && asset.kind == GovernedAssetKind::Mesh
        }) {
            return Err("Gameplay visual must reference an exact governed mesh pin".into());
        }
    }
    let mut kinds = BTreeMap::new();
    let mut auxiliaries = BTreeSet::new();
    for asset in &catalog.assets {
        for (path, kind) in std::iter::once((&asset.payload.path, asset.kind)).chain(
            asset
                .thumbnail
                .iter()
                .map(|f| (&f.path, GovernedAssetKind::Texture)),
        ) {
            if kinds
                .insert(path.clone(), kind)
                .is_some_and(|old| old != kind)
            {
                return Err("Shared media file has conflicting declared kinds".into());
            }
        }
        if let Some(file) = &asset.thumbnail {
            auxiliaries.insert(file.path.as_str());
        }
        if let Some(
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
        ) = &asset.durable_source
        {
            auxiliaries.insert(file.path.as_str());
        }
    }
    if module
        .files
        .iter()
        .any(|f| f.role == ModuleFileRole::AssetAuxiliary && !auxiliaries.contains(f.path.as_str()))
    {
        return Err("Unreferenced asset auxiliary payload".into());
    }
    let mut actual = Vec::with_capacity(module.files.len());
    for declared in &module.files {
        let (bytes, sha256) = source.digest(&declared.path, kinds.get(&declared.path).copied())?;
        actual.push(ModuleFile {
            path: declared.path.clone(),
            bytes,
            sha256,
            role: declared.role,
        });
    }
    module
        .validate_inventory(&actual)
        .map_err(|e| e.to_string())?;
    // The transport's own pins include module.json, which cannot hash itself.
    let actual_index: BTreeMap<_, _> = actual.iter().map(|f| (f.path.as_str(), f)).collect();
    for entry in source.files.values() {
        if !entry.sha256.is_empty() {
            let (bytes, pin) = if entry.path == MODULE_MANIFEST_PATH {
                (
                    module_bytes.len() as u64,
                    forge_core::bytes_sha256(&module_bytes),
                )
            } else {
                let actual = actual_index
                    .get(entry.path.as_str())
                    .ok_or("Unexpected transport payload")?;
                (actual.bytes, actual.sha256)
            };
            if bytes != entry.bytes || pin.to_string() != entry.sha256 {
                return Err(format!("Transport integrity mismatch: {}", entry.path));
            }
        }
    }
    for zone in &module.zones {
        let file = module
            .files
            .iter()
            .find(|f| f.path == zone.archive_path)
            .ok_or("Missing zone file")?;
        module
            .validate_zone_archive(
                zone.area_id,
                &source.document(file, forge_project::zone_authoring::MAX_ZONE_HEAD_BYTES)?,
            )
            .map_err(|e| e.to_string())?;
    }
    if catalog.assets.len() != module.assets.len() {
        return Err("Module and catalog asset inventories disagree".into());
    }
    for asset in &catalog.assets {
        let declared = module
            .assets
            .iter()
            .find(|a| a.asset_id == asset.id)
            .ok_or("Catalog asset identity absent from module")?;
        if declared.file_path != asset.payload.path || declared.sha256 != asset.payload.sha256 {
            return Err(format!("Catalog asset pin disagrees: {}", asset.id));
        }
        if asset
            .derived_from
            .is_some_and(|parent| !declared.dependencies.contains(&parent))
        {
            return Err("Asset lineage parent absent from module dependency list".into());
        }
        check_file(&module, &asset.payload, false)?;
        if let Some(thumbnail) = &asset.thumbnail {
            check_file(&module, thumbnail, true)?;
        }
        if let Some(
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
        ) = &asset.durable_source
        {
            check_file(&module, file, true)?;
        }
        // Rights evidence must travel with the module, not point at the Vault or a website.
        evidence(&module, &asset.provenance.evidence)?;
        if publishing {
            evidence(
                &module,
                asset
                    .provenance
                    .rights
                    .redistribution_evidence
                    .as_deref()
                    .ok_or("Missing redistribution evidence")?,
            )?;
        }
    }
    Ok(module)
}

fn evidence(module: &ModulePackageManifest, path: &str) -> Result<()> {
    if !module.files.iter().any(|f| {
        f.path == path && matches!(f.role, ModuleFileRole::License | ModuleFileRole::Provenance)
    }) {
        return Err(format!(
            "Rights evidence is not an embedded license/provenance file: {path}"
        ));
    }
    Ok(())
}

fn check_file(
    module: &ModulePackageManifest,
    file: &GovernedAssetFile,
    auxiliary: bool,
) -> Result<()> {
    if !module.files.iter().any(|f| {
        f.path == file.path
            && f.sha256 == file.sha256
            && f.bytes == file.bytes
            && (f.role == ModuleFileRole::Asset
                || (auxiliary && f.role == ModuleFileRole::AssetAuxiliary))
    }) {
        return Err(format!(
            "Catalog payload is not pinned in module: {}",
            file.path
        ));
    }
    Ok(())
}

fn reject_program(path: &str, prefix: &[u8]) -> Result<()> {
    let extension = Path::new(path)
        .extension()
        .and_then(|s| s.to_str())
        .unwrap_or_default()
        .to_ascii_lowercase();
    if matches!(
        extension.as_str(),
        "exe" | "dll" | "msi" | "com" | "scr" | "bat" | "cmd" | "ps1" | "sh" | "py" | "js" | "wasm"
    ) || [
        b"MZ".as_slice(),
        b"\x7fELF",
        b"#!",
        b"\0asm",
        b"\xfe\xed\xfa\xce",
        b"\xce\xfa\xed\xfe",
        b"\xfe\xed\xfa\xcf",
        b"\xcf\xfa\xed\xfe",
        b"\xca\xfe\xba\xbe",
    ]
    .iter()
    .any(|magic| prefix.starts_with(magic))
    {
        return Err(format!(
            "Program content or extension forbidden in module: {path}"
        ));
    }
    Ok(())
}

/// Decode the same bounded bytes consumed by the streaming hash reader.
/// Successful decoding is not artistic or runtime acceptance.
pub(crate) fn validate_payload_bytes(
    path: &str,
    bytes: &[u8],
    kind: Option<GovernedAssetKind>,
) -> Result<()> {
    let count = bytes.len().min(20);
    reject_program(path, &bytes[..count])?;
    if let Some(kind) = kind {
        let mut header = [0_u8; 20];
        header[..count].copy_from_slice(&bytes[..count]);
        media(
            path,
            kind,
            &header,
            count,
            bytes.len() as u64,
            &mut std::io::Cursor::new(&bytes[count..]),
        )?;
    }
    Ok(())
}

fn media(
    path: &str,
    kind: GovernedAssetKind,
    header: &[u8; 20],
    count: usize,
    length: u64,
    reader: &mut impl Read,
) -> Result<()> {
    let extension = Path::new(path)
        .extension()
        .and_then(|s| s.to_str())
        .unwrap_or_default()
        .to_ascii_lowercase();
    let format = match (kind, extension.as_str()) {
        (GovernedAssetKind::Mesh, "glb") => MediaFormat::Glb,
        (GovernedAssetKind::Texture, "png") => MediaFormat::Png,
        (GovernedAssetKind::Texture, "jpg" | "jpeg") => MediaFormat::Jpeg,
        (GovernedAssetKind::Audio, "wav") => MediaFormat::Wav,
        (GovernedAssetKind::Audio, "ogg") => MediaFormat::OggVorbis,
        (GovernedAssetKind::Audio, "flac") => MediaFormat::Flac,
        _ => return Err(format!("Unsupported or mismatched media format: {path}")),
    };
    let limits = MediaLimits::default();
    if length > limits.max_input_bytes || length < count as u64 {
        return Err(format!("Media input budget or length exceeded: {path}"));
    }
    let mut bytes = header[..count].to_vec();
    bytes
        .try_reserve_exact(
            usize::try_from(length - count as u64)
                .map_err(|_| "Media allocation budget exceeded")?,
        )
        .map_err(|_| "Media allocation budget unavailable")?;
    reader
        .take(length - count as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|e| e.to_string())?;
    if bytes.len() as u64 != length {
        return Err(format!("Media length changed while decoding: {path}"));
    }
    validate_media(&bytes, format, limits).map_err(|e| format!("{path}: {e}"))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn corrupt_png_with_a_plausible_header_is_not_admitted() {
        let mut bytes = [0_u8; 45];
        bytes[..8].copy_from_slice(b"\x89PNG\r\n\x1a\n");
        bytes[12..16].copy_from_slice(b"IHDR");
        bytes[16..20].copy_from_slice(&1_u32.to_be_bytes());
        bytes[20..24].copy_from_slice(&1_u32.to_be_bytes());
        assert!(
            media(
                "broken.png",
                GovernedAssetKind::Texture,
                bytes[..20].try_into().unwrap(),
                20,
                45,
                &mut std::io::Cursor::new(&bytes[20..])
            )
            .is_err()
        );
    }

    #[test]
    fn wav_container_without_a_decodable_stream_is_not_admitted() {
        let mut bytes = [0_u8; 44];
        bytes[..4].copy_from_slice(b"RIFF");
        bytes[4..8].copy_from_slice(&36_u32.to_le_bytes());
        bytes[8..12].copy_from_slice(b"WAVE");
        assert!(
            media(
                "broken.wav",
                GovernedAssetKind::Audio,
                bytes[..20].try_into().unwrap(),
                20,
                44,
                &mut std::io::Cursor::new(&bytes[20..])
            )
            .is_err()
        );
    }

    #[test]
    fn document_changed_after_inventory_is_not_interpreted_with_an_old_pin() {
        let root = std::env::temp_dir().join(format!(
            "forge-orpmod-document-{}",
            crate::attempt().unwrap()
        ));
        fs::create_dir(&root).unwrap();
        let original = br#"{"redistribution":"forbidden"}"#;
        fs::write(root.join("catalog.json"), original).unwrap();
        let source = Source::directory(&root, false).unwrap();
        let pin = ModuleFile {
            path: "catalog.json".into(),
            bytes: original.len() as u64,
            sha256: forge_core::bytes_sha256(original),
            role: ModuleFileRole::AssetCatalog,
        };
        fs::write(
            root.join("catalog.json"),
            br#"{"redistribution":"allowed"  }"#,
        )
        .unwrap();
        assert!(source.document(&pin, MAX_MODULE_MANIFEST_BYTES).is_err());
        fs::remove_dir_all(&root).unwrap();
    }
}
