//! Project-selected images, carried with the existing governed import catalogue.
use crate::{Result, manifest};
use base64::{Engine, engine::general_purpose::STANDARD};
use forge_assets::{
    governed_import::{DurableAssetSource, GovernedAssetKind, GovernedImportCatalog},
    media_validation::{MediaFormat, MediaLimits, validate_media},
};
use forge_core::bytes_sha256;
use forge_project::workspace::launcher::{GameConfiguration, PresentationAsset};
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    fs,
    io::Read,
    path::Path,
};

const ROOT: &str = "presentation";
const CATALOG: &str = "presentation/catalog.json";
const MAX_TOTAL: u64 = 512 * 1024 * 1024;
pub const MAX_IMAGE_BYTES: u64 = 8 * 1024 * 1024;

/// Owned logical bytes from a native, pinned asset reader; never frontend paths.
pub struct PresentationBundle {
    pub catalog: GovernedImportCatalog,
    pub files: BTreeMap<String, Vec<u8>>,
}

/// Only admitted raster data reaches the WebView, with no filesystem authority.
#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PresentationView {
    pub icon: Option<String>,
    pub logo: Option<String>,
    pub background: Option<String>,
}
impl PresentationView {
    pub const fn is_empty(&self) -> bool {
        self.icon.is_none() && self.logo.is_none() && self.background.is_none()
    }
}

pub fn selections(game: &GameConfiguration) -> Vec<PresentationAsset> {
    [game.icon, game.logo, game.background]
        .into_iter()
        .flatten()
        .collect()
}

impl PresentationBundle {
    pub fn validate(&self, game: &GameConfiguration) -> Result<()> {
        self.catalog
            .verify_payloads(|file| {
                self.files
                    .get(&file.path)
                    .cloned()
                    .ok_or_else(|| format!("Missing presentation file: {}", file.path))
            })
            .map_err(|error| error.to_string())?;
        let mut required = BTreeSet::new();
        for pin in selections(game) {
            let image = self
                .catalog
                .assets
                .iter()
                .find(|asset| asset.id == pin.asset_id && asset.payload.sha256 == pin.content_hash)
                .ok_or("Selected presentation revision is missing")?;
            if image.kind != GovernedAssetKind::Texture {
                return Err("Presentation requires an imported image".into());
            }
            image_mime(&image.payload.path, &self.files[&image.payload.path])?;
            let mut cursor = Some(image);
            while let Some(asset) = cursor {
                required.insert(asset.id);
                cursor = asset
                    .derived_from
                    .and_then(|id| self.catalog.assets.iter().find(|parent| parent.id == id));
            }
        }
        if required.len() != self.catalog.assets.len() {
            return Err("Presentation catalogue contains unrelated assets".into());
        }
        let mut paths = BTreeSet::new();
        for asset in &self.catalog.assets {
            paths.insert(asset.payload.path.as_str());
            if let Some(file) = &asset.thumbnail {
                paths.insert(&file.path);
            }
            if let Some(
                DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
            ) = &asset.durable_source
            {
                paths.insert(&file.path);
            }
        }
        let mut evidence = BTreeSet::new();
        for asset in &self.catalog.assets {
            evidence.insert(asset.provenance.evidence.as_str());
            if let Some(path) = &asset.provenance.rights.redistribution_evidence {
                evidence.insert(path.as_str());
            }
        }
        for path in evidence {
            if !paths.insert(path) {
                return Err("Presentation evidence aliases a payload".into());
            }
            let bytes = self
                .files
                .get(path)
                .ok_or("Presentation licence evidence is missing")?;
            if path != format!("licenses/{}.txt", bytes_sha256(bytes)) || bytes.len() > 65536 {
                return Err("Presentation licence evidence pin differs".into());
            }
            let text = std::str::from_utf8(bytes).map_err(|error| error.to_string())?;
            if text.trim().is_empty()
                || text.chars().any(|c| {
                    c == '\u{fffd}' || (c.is_control() && !matches!(c, '\n' | '\r' | '\t'))
                })
            {
                return Err("Presentation licence evidence text is invalid".into());
            }
        }
        if paths.len() != self.files.len()
            || paths.iter().any(|path| !self.files.contains_key(*path))
        {
            return Err("Presentation file closure differs".into());
        }
        let total = self.files.values().try_fold(0_u64, |sum, bytes| {
            sum.checked_add(bytes.len() as u64)
                .ok_or("Presentation size overflow")
        })?;
        if total > MAX_TOTAL {
            return Err("Presentation exceeds its allocation budget".into());
        }
        Ok(())
    }

    pub(crate) fn write(&self, root: &Path, game: &GameConfiguration) -> Result<Vec<String>> {
        self.validate(game)?;
        let catalog = crate::json(&self.catalog)?;
        let files = self
            .files
            .iter()
            .map(|(path, bytes)| (path.as_str(), bytes.as_slice()))
            .chain(std::iter::once(("catalog.json", catalog.as_slice())));
        for (relative, bytes) in files {
            let path = manifest::confined(root, &format!("{ROOT}/{relative}"))?;
            fs::create_dir_all(path.parent().ok_or("Presentation parent missing")?)
                .map_err(|error| error.to_string())?;
            use std::io::Write;
            fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(path)
                .and_then(|mut file| file.write_all(bytes))
                .map_err(|error| error.to_string())?;
        }
        Ok(self
            .files
            .keys()
            .filter(|path| path.starts_with("licenses/"))
            .map(|path| format!("{ROOT}/{path}"))
            .collect())
    }

    fn view(&self, game: &GameConfiguration) -> Result<PresentationView> {
        let image = |pin: Option<PresentationAsset>| -> Result<Option<String>> {
            let Some(pin) = pin else { return Ok(None) };
            let asset = self
                .catalog
                .assets
                .iter()
                .find(|asset| asset.id == pin.asset_id && asset.payload.sha256 == pin.content_hash)
                .ok_or("Selected presentation revision is missing")?;
            let bytes = &self.files[&asset.payload.path];
            let mime = image_mime(&asset.payload.path, bytes)?;
            Ok(Some(format!(
                "data:{mime};base64,{}",
                STANDARD.encode(bytes)
            )))
        };
        Ok(PresentationView {
            icon: image(game.icon)?,
            logo: image(game.logo)?,
            background: image(game.background)?,
        })
    }
}

fn image_mime(path: &str, bytes: &[u8]) -> Result<&'static str> {
    let (format, mime) = match path.rsplit('.').next() {
        Some("png") => (MediaFormat::Png, "image/png"),
        Some("jpg" | "jpeg") => (MediaFormat::Jpeg, "image/jpeg"),
        _ => return Err("Presentation requires a PNG or JPEG image".into()),
    };
    validate_media(
        bytes,
        format,
        MediaLimits {
            max_input_bytes: MAX_IMAGE_BYTES,
            max_image_pixels: 16 * 1024 * 1024,
            max_decoded_bytes: 64 * 1024 * 1024,
            ..MediaLimits::default()
        },
    )
    .map_err(|error| error.to_string())?;
    Ok(mime)
}

fn read_bounded(root: &Path, relative: &str, limit: u64) -> Result<Vec<u8>> {
    let path = manifest::confined(root, relative)?;
    manifest::no_links(&path)?;
    let metadata = fs::metadata(&path).map_err(|error| error.to_string())?;
    if !metadata.is_file() || metadata.len() > limit {
        return Err("Presentation read limit exceeded".into());
    }
    let mut bytes = Vec::new();
    fs::File::open(path)
        .map_err(|error| error.to_string())?
        .take(limit + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() as u64 > limit {
        return Err("Presentation read limit exceeded".into());
    }
    Ok(bytes)
}

/// Read under the existing package verification/installation lock.
pub(crate) fn read(root: &Path, game: &GameConfiguration) -> Result<PresentationView> {
    if selections(game).is_empty() {
        return Ok(PresentationView::default());
    }
    let catalog: GovernedImportCatalog =
        serde_json::from_slice(&read_bounded(root, CATALOG, 8 * 1024 * 1024)?)
            .map_err(|error| error.to_string())?;
    catalog
        .validate_for_local_use()
        .map_err(|error| error.to_string())?;
    let mut declared = BTreeMap::new();
    for asset in &catalog.assets {
        declared.insert(asset.payload.path.clone(), asset.payload.bytes);
        if let Some(file) = &asset.thumbnail {
            declared.insert(file.path.clone(), file.bytes);
        }
        if let Some(
            DurableAssetSource::AuthoredFile { file } | DurableAssetSource::Recipe { file },
        ) = &asset.durable_source
        {
            declared.insert(file.path.clone(), file.bytes);
        }
        declared
            .entry(asset.provenance.evidence.clone())
            .or_insert(65536);
        if let Some(path) = &asset.provenance.rights.redistribution_evidence {
            declared.entry(path.clone()).or_insert(65536);
        }
    }
    let mut total = 0_u64;
    let mut files = BTreeMap::new();
    for (path, limit) in declared {
        if limit > MAX_TOTAL {
            return Err("Presentation exceeds its allocation budget".into());
        }
        let bytes = read_bounded(root, &format!("{ROOT}/{path}"), limit)?;
        total = total
            .checked_add(bytes.len() as u64)
            .ok_or("Presentation size overflow")?;
        if total > MAX_TOTAL {
            return Err("Presentation exceeds its allocation budget".into());
        }
        files.insert(path, bytes);
    }
    let bundle = PresentationBundle { catalog, files };
    bundle.validate(game)?;
    bundle.view(game)
}
