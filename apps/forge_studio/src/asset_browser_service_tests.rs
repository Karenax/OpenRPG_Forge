use super::*;

fn request(request_serial: u64, refresh: bool) -> BrowseAssetsRequest {
    BrowseAssetsRequest {
        schema_version: BROWSE_ASSETS_REQUEST_SCHEMA_VERSION,
        request_serial,
        refresh,
    }
}

#[test]
fn first_scan_hit_and_refresh_keep_one_valid_atomic_snapshot() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();

    let BrowseAssetsWork::Scan(first) = service.begin(request(1, false)).unwrap() else {
        panic!("an empty cache must scan");
    };
    let first = service.finish(first, Ok(snapshot.clone())).unwrap();
    assert_eq!(first.cache_status, AssetCatalogCacheStatus::FirstScan);

    let BrowseAssetsWork::Ready(hit) = service.begin(request(2, false)).unwrap() else {
        panic!("a warm cache must be returned without scanning");
    };
    assert_eq!(hit.cache_status, AssetCatalogCacheStatus::Hit);
    assert_eq!(hit.snapshot.snapshot_hash, snapshot.snapshot_hash);

    let BrowseAssetsWork::Scan(refresh) = service.begin(request(3, true)).unwrap() else {
        panic!("an explicit refresh must scan");
    };
    let refreshed = service.finish(refresh, Ok(snapshot)).unwrap();
    assert_eq!(refreshed.cache_status, AssetCatalogCacheStatus::Refreshed);
}

#[test]
fn exactly_one_required_scan_can_be_in_flight_while_warm_hits_remain_available() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();
    let BrowseAssetsWork::Scan(initial) = service.begin(request(1, false)).unwrap() else {
        panic!("an empty cache must scan");
    };
    assert!(service.begin(request(2, false)).is_err());
    assert!(service.begin(request(3, true)).is_err());
    service
        .finish(initial, Ok(snapshot.clone()))
        .expect("the only in-flight scan must finish");

    let BrowseAssetsWork::Scan(refresh) = service.begin(request(4, true)).unwrap() else {
        panic!("refresh must scan");
    };
    let BrowseAssetsWork::Ready(hit) = service.begin(request(1, false)).unwrap() else {
        panic!("a warm cache hit needs no second scan");
    };
    assert_eq!(hit.request_serial, 1);
    assert_eq!(hit.cache_status, AssetCatalogCacheStatus::Hit);
    assert!(service.begin(request(5, true)).is_err());
    let refreshed = service.finish(refresh, Ok(snapshot)).unwrap();
    assert_eq!(refreshed.request_serial, 4);
    assert_eq!(refreshed.cache_status, AssetCatalogCacheStatus::Refreshed);
}

#[test]
fn client_serial_is_only_correlation_and_survives_reload_or_high_values() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();
    let BrowseAssetsWork::Scan(ticket) = service.begin(request(u64::MAX, false)).unwrap() else {
        panic!("empty cache must scan");
    };
    let high = service.finish(ticket, Ok(snapshot.clone())).unwrap();
    assert_eq!(high.request_serial, u64::MAX);

    let BrowseAssetsWork::Ready(reloaded) = service.begin(request(1, false)).unwrap() else {
        panic!("a reloaded client must be able to use the cache from serial one");
    };
    assert_eq!(reloaded.request_serial, 1);
    let BrowseAssetsWork::Scan(refresh) = service.begin(request(1, true)).unwrap() else {
        panic!("a duplicate client serial remains valid correlation for a new scan");
    };
    let refreshed = service.finish(refresh, Ok(snapshot)).unwrap();
    assert_eq!(refreshed.request_serial, 1);
}

#[test]
fn invalid_requests_do_not_replace_the_cache() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();
    let BrowseAssetsWork::Scan(ticket) = service.begin(request(1, false)).unwrap() else {
        panic!("empty cache must scan");
    };
    service.finish(ticket, Ok(snapshot.clone())).unwrap();
    assert!(service.begin(request(0, false)).is_err());
    let BrowseAssetsWork::Ready(hit) = service.begin(request(2, false)).unwrap() else {
        panic!("valid cache must survive rejected requests");
    };
    assert_eq!(hit.snapshot.snapshot_hash, snapshot.snapshot_hash);
}

#[test]
fn incompatible_request_versions_are_rejected_without_starting_a_scan() {
    let mut service = AssetBrowserService::default();
    for schema_version in [0, BROWSE_ASSETS_REQUEST_SCHEMA_VERSION + 1] {
        assert!(
            service
                .begin(BrowseAssetsRequest {
                    schema_version,
                    request_serial: 1,
                    refresh: false,
                })
                .is_err()
        );
    }
    assert!(matches!(
        service.begin(request(1, false)).unwrap(),
        BrowseAssetsWork::Scan(_)
    ));
}

#[test]
fn invalid_refresh_snapshot_cannot_replace_the_valid_cached_snapshot() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();
    let BrowseAssetsWork::Scan(initial) = service.begin(request(1, false)).unwrap() else {
        panic!("empty cache must scan");
    };
    service.finish(initial, Ok(snapshot.clone())).unwrap();

    let BrowseAssetsWork::Scan(refresh) = service.begin(request(2, true)).unwrap() else {
        panic!("refresh must scan");
    };
    let mut corrupted = snapshot.clone();
    corrupted.snapshot_hash = forge_core::ContentHash::from_bytes([0; 32]);
    assert!(service.finish(refresh, Ok(corrupted)).is_err());
    assert_eq!(
        service.cache.as_ref().map(|cached| cached.snapshot_hash),
        Some(snapshot.snapshot_hash)
    );

    let BrowseAssetsWork::Scan(retry) = service.begin(request(3, true)).unwrap() else {
        panic!("a rejected snapshot must release its exact scan ticket");
    };
    service.finish(retry, Ok(snapshot.clone())).unwrap();

    let BrowseAssetsWork::Ready(hit) = service.begin(request(4, false)).unwrap() else {
        panic!("the prior valid cache must survive a rejected refresh");
    };
    assert_eq!(hit.cache_status, AssetCatalogCacheStatus::Hit);
    assert_eq!(hit.snapshot.snapshot_hash, snapshot.snapshot_hash);
}

#[test]
fn scan_failure_releases_only_its_exact_ticket_and_allows_retry() {
    let snapshot = scan_compiled_assets().unwrap();
    let mut service = AssetBrowserService::default();
    let BrowseAssetsWork::Scan(failed) = service.begin(request(1, false)).unwrap() else {
        panic!("empty cache must scan");
    };
    assert!(
        service
            .finish(failed, Err("synthetic scan failure".into()))
            .is_err()
    );

    let BrowseAssetsWork::Scan(retry) = service.begin(request(2, false)).unwrap() else {
        panic!("a failed scan must release its ticket for retry");
    };
    assert!(service.finish(failed, Ok(snapshot.clone())).is_err());
    assert!(service.cache.is_none());
    assert!(service.begin(request(3, true)).is_err());
    let catalog = service.finish(retry, Ok(snapshot)).unwrap();
    assert_eq!(catalog.request_serial, 2);
    assert_eq!(catalog.cache_status, AssetCatalogCacheStatus::FirstScan);

    let BrowseAssetsWork::Scan(refresh) = service.begin(request(3, true)).unwrap() else {
        panic!("refresh must scan");
    };
    assert!(
        service
            .finish(refresh, Err("synthetic join failure".into()))
            .is_err()
    );
    let BrowseAssetsWork::Ready(hit) = service.begin(request(1, false)).unwrap() else {
        panic!("a failed refresh must preserve the prior valid cache");
    };
    assert_eq!(hit.snapshot.snapshot_hash, catalog.snapshot.snapshot_hash);
    assert!(matches!(
        service.begin(request(4, true)).unwrap(),
        BrowseAssetsWork::Scan(_)
    ));
}
