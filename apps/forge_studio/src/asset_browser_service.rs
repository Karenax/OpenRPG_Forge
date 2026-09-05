//! Native, bounded asset-catalog scanning with an atomic in-memory cache.

use crate::asset_browser::{
    AssetCatalog, AssetCatalogCacheStatus, BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
    BrowseAssetsRequest,
};
use forge_assets::browser::{
    ASSET_BROWSER_SNAPSHOT_SCHEMA, ASSET_BROWSER_SNAPSHOT_VERSION, AssetBrowserSnapshot,
    scan_asset_browser,
};
use std::path::{Path, PathBuf};

#[derive(Debug, Default)]
pub struct AssetBrowserService {
    cache: Option<AssetBrowserSnapshot>,
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
        Ok(AssetCatalog {
            request_serial: ticket.request_serial,
            cache_status,
            snapshot,
        })
    }
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
