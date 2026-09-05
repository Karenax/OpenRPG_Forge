//! Native, bounded asset-catalog scanning with an atomic in-memory cache.

use crate::asset_browser::{
    AssetCatalog, AssetCatalogCacheStatus, BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
    BrowseAssetsRequest,
};
use forge_assets::browser::{
    ASSET_BROWSER_SNAPSHOT_SCHEMA, ASSET_BROWSER_SNAPSHOT_VERSION, AssetBrowserEntry,
    AssetBrowserSnapshot, scan_asset_browser,
};
use forge_core::{AssetId, ContentHash};
use forge_project::PlacementDocument;
use std::collections::BTreeSet;
use std::path::{Path, PathBuf};

#[derive(Debug, Default)]
pub struct AssetBrowserService {
    cache: Option<AssetBrowserSnapshot>,
    cache_resolvable: bool,
    scan_in_flight: Option<u64>,
    next_scan_ticket: u64,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct AssetScanTicket {
    server_ticket: u64,
    request_serial: u64,
}

#[derive(Clone, Debug, PartialEq)]
pub enum BrowseAssetsWork {
    Ready(AssetCatalog),
    Scan(AssetScanTicket),
}

/// Native-only result of resolving an IPC asset identity against the exact verified cache.
/// Paths, provenance and byte budgets never come from the browser payload.
#[derive(Clone, Debug, PartialEq)]
pub struct ResolvedAsset {
    pub entry: AssetBrowserEntry,
    pub license_evidence_hash: ContentHash,
    /// Conservative upper bound for the scene and its local dependencies.
    pub tracked_bytes: u64,
}

impl ResolvedAsset {
    pub fn asset_pin(&self) -> Result<forge_project::AssetPin, String> {
        let provenance = &self.entry.provenance;
        let pin = forge_project::AssetPin {
            asset_id: self.entry.id,
            stable_key: self.entry.stable_key.clone(),
            subset_id: provenance.subset_id.clone(),
            pack_id: provenance.pack_id.clone(),
            source_version: provenance.source_version.clone(),
            author: provenance.author.clone(),
            source_path: provenance.source_path.clone(),
            scene_path: self.entry.scene_path.clone(),
            scene_content_hash: provenance.scene_content_hash,
            manifest_hash: provenance.manifest_hash,
            license_spdx: provenance.license_spdx.clone(),
            license_evidence_hash: self.license_evidence_hash,
            base_scale: self.entry.base_scale,
        };
        pin.validate().map_err(|error| {
            format!("Asset résolu incompatible avec le document de placement : {error}")
        })?;
        Ok(pin)
    }
}

impl AssetBrowserService {
    pub fn begin(&mut self, request: BrowseAssetsRequest) -> Result<BrowseAssetsWork, String> {
        if request.schema_version != BROWSE_ASSETS_REQUEST_SCHEMA_VERSION {
            return Err(format!(
                "Version de requête Assets incompatible : {} (attendu {}).",
                request.schema_version, BROWSE_ASSETS_REQUEST_SCHEMA_VERSION
            ));
        }
        if request.request_serial == 0 {
            return Err("Le numéro de requête Assets doit être supérieur à zéro.".into());
        }
        if !request.refresh
            && let Some(snapshot) = self.cache.clone()
        {
            return Ok(BrowseAssetsWork::Ready(AssetCatalog {
                request_serial: request.request_serial,
                cache_status: AssetCatalogCacheStatus::Hit,
                snapshot,
            }));
        }
        if self.scan_in_flight.is_some() {
            return Err("Un scan Assets est déjà en cours ; réessayez après sa fin.".into());
        }
        let server_ticket = self
            .next_scan_ticket
            .checked_add(1)
            .ok_or_else(|| "Compteur interne des scans Assets épuisé.".to_owned())?;
        self.next_scan_ticket = server_ticket;
        self.scan_in_flight = Some(server_ticket);
        if request.refresh {
            // A failed explicit refresh is evidence that the previously scanned filesystem state
            // can no longer authorize a runtime placement, even though it remains displayable.
            self.cache_resolvable = false;
        }
        Ok(BrowseAssetsWork::Scan(AssetScanTicket {
            server_ticket,
            request_serial: request.request_serial,
        }))
    }

    pub fn finish(
        &mut self,
        ticket: AssetScanTicket,
        outcome: Result<AssetBrowserSnapshot, String>,
    ) -> Result<AssetCatalog, String> {
        if self.scan_in_flight != Some(ticket.server_ticket) {
            return Err("Résultat Assets associé à un ticket serveur périmé.".into());
        }
        self.scan_in_flight = None;
        let snapshot = outcome?;
        validate_snapshot(&snapshot)?;
        let cache_status = if self.cache.is_some() {
            AssetCatalogCacheStatus::Refreshed
        } else {
            AssetCatalogCacheStatus::FirstScan
        };
        self.cache = Some(snapshot.clone());
        self.cache_resolvable = true;
        Ok(AssetCatalog {
            request_serial: ticket.request_serial,
            cache_status,
            snapshot,
        })
    }

    pub fn resolve_asset(
        &self,
        snapshot_hash: ContentHash,
        asset_id: AssetId,
    ) -> Result<ResolvedAsset, String> {
        if self.scan_in_flight.is_some() {
            return Err(
                "Le catalogue Assets est en cours de vérification ; recommencez le dépôt ensuite."
                    .into(),
            );
        }
        if !self.cache_resolvable {
            return Err(
                "Le dernier contrôle Assets n’autorise plus de placement ; actualisez le catalogue."
                    .into(),
            );
        }
        let snapshot = self
            .cache
            .as_ref()
            .ok_or("Le catalogue Assets doit être chargé avant un placement.")?;
        resolve_asset_in_snapshot(snapshot, snapshot_hash, asset_id)
    }

    /// Resolves every asset actually instantiated by a persistent placement document. An unopened
    /// Assets window is not a prerequisite for reopening a project: the same pinned offline scan is
    /// performed here once and then reused by both workflows.
    pub fn resolve_placement_document(
        &mut self,
        document: &PlacementDocument,
    ) -> Result<Vec<ResolvedAsset>, String> {
        document
            .validate()
            .map_err(|error| format!("Document de placement invalide : {error}"))?;
        let used = document
            .placements
            .iter()
            .map(|placement| placement.asset_id)
            .collect::<BTreeSet<_>>();
        if used.is_empty() {
            return Ok(Vec::new());
        }
        let snapshot = if self.cache_resolvable {
            self.cache
                .as_ref()
                .ok_or("Catalogue Assets résolvable absent.")?
                .clone()
        } else {
            // Reopen must not depend on the Assets window. If its refresh is concurrently scanning,
            // perform this independent exact scan; the single browser ticket remains untouched.
            let snapshot = scan_compiled_assets()?;
            validate_snapshot(&snapshot)?;
            if self.scan_in_flight.is_none() {
                self.cache = Some(snapshot.clone());
                self.cache_resolvable = true;
            }
            snapshot
        };
        let snapshot_hash = snapshot.snapshot_hash;
        used.into_iter()
            .map(|asset_id| {
                let resolved = resolve_asset_in_snapshot(&snapshot, snapshot_hash, asset_id)?;
                let current_pin = resolved.asset_pin()?;
                let persisted_pin = document
                    .asset_pins
                    .binary_search_by_key(&asset_id, |pin| pin.asset_id)
                    .ok()
                    .and_then(|index| document.asset_pins.get(index))
                    .ok_or("Pin Asset persistant introuvable.")?;
                if &current_pin != persisted_pin {
                    return Err(format!(
                        "L’Asset persistant {asset_id} ne correspond plus au catalogue gouverné."
                    ));
                }
                Ok(resolved)
            })
            .collect()
    }

    pub fn placement_projection_binding(
        &mut self,
        document: &PlacementDocument,
    ) -> Result<crate::workspace_api::PlacementProjectionBinding, String> {
        let resolved = self.resolve_placement_document(document)?;
        Ok(crate::workspace_api::PlacementProjectionBinding {
            source_hash: document.content_hash().map_err(|error| error.to_string())?,
            asset_budgets: resolved
                .into_iter()
                .map(|asset| crate::workspace_api::PlacementAssetBudget {
                    asset_id: asset.entry.id,
                    tracked_bytes: asset.tracked_bytes,
                })
                .collect(),
        })
    }
}

fn resolve_asset_in_snapshot(
    snapshot: &AssetBrowserSnapshot,
    snapshot_hash: ContentHash,
    asset_id: AssetId,
) -> Result<ResolvedAsset, String> {
    validate_snapshot(snapshot)?;
    if snapshot.snapshot_hash != snapshot_hash {
        return Err(
            "Le catalogue Assets a changé depuis le début du déplacement ; recommencez.".into(),
        );
    }
    let mut assets = snapshot.assets.iter().filter(|asset| asset.id == asset_id);
    let asset = assets
        .next()
        .ok_or("Asset absent du catalogue vérifié courant.")?;
    if assets.next().is_some() {
        return Err("Identité Asset ambiguë dans le catalogue vérifié.".into());
    }
    let mut subsets = snapshot.manifests.iter().filter(|subset| {
        subset.subset_id == asset.provenance.subset_id
            && subset.manifest_hash == asset.provenance.manifest_hash
    });
    let subset = subsets
        .next()
        .ok_or("Sous-ensemble gouverné introuvable pour cet Asset.")?;
    if subsets.next().is_some()
        || subset.source.pack_id != asset.provenance.pack_id
        || subset.source.version != asset.provenance.source_version
        || subset.source.author != asset.provenance.author
        || subset.license.spdx != asset.provenance.license_spdx
        || !asset.provenance.license_verified
        || subset.license.attribution_required != asset.provenance.attribution_required
    {
        return Err("Provenance Asset incohérente avec son sous-ensemble gouverné.".into());
    }
    let scene = subset
        .files
        .iter()
        .find(|file| file.relative_path == asset.scene_path)
        .ok_or("Scène Asset absente du manifeste vérifié.")?;
    if scene.content_hash != asset.provenance.scene_content_hash {
        return Err("Scène Asset incohérente avec son empreinte vérifiée.".into());
    }
    let tracked_bytes = subset.files.iter().try_fold(0_u64, |total, file| {
        total
            .checked_add(file.bytes)
            .ok_or("Budget d’octets Asset épuisé.")
    })?;
    if tracked_bytes == 0 {
        return Err("Sous-ensemble Asset vide.".into());
    }
    Ok(ResolvedAsset {
        entry: asset.clone(),
        license_evidence_hash: subset.license.evidence_hash,
        tracked_bytes,
    })
}

#[must_use]
pub fn compiled_asset_root() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("..")
        .join("..")
        .join("assets")
}

pub fn scan_compiled_assets() -> Result<AssetBrowserSnapshot, String> {
    scan_asset_browser(compiled_asset_root())
        .map_err(|error| format!("Catalogue Assets indisponible : {error}"))
}

fn validate_snapshot(snapshot: &AssetBrowserSnapshot) -> Result<(), String> {
    if snapshot.schema != ASSET_BROWSER_SNAPSHOT_SCHEMA
        || snapshot.schema_version != ASSET_BROWSER_SNAPSHOT_VERSION
    {
        return Err("Snapshot Assets de schéma incompatible.".into());
    }
    let hash = snapshot
        .recompute_hash()
        .map_err(|error| format!("Snapshot Assets non canonique : {error}"))?;
    if hash != snapshot.snapshot_hash {
        return Err("Snapshot Assets incohérent avec son empreinte.".into());
    }
    Ok(())
}

#[cfg(test)]
#[path = "asset_browser_service_tests.rs"]
mod tests;
