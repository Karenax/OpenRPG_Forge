//! A managed map without an ORPMOD origin can use its installed reference data.
use super::{Reply, Request, Snapshot, read_chunk};
use forge_core::{ContentHash, ModuleId};
use forge_delivery::legacy_reference::{ReferenceArea, ReferenceScene};
use forge_project::{authoring::host::AuthoringHost, zone_authoring::session::ZoneEditSession};
use std::path::{Path, PathBuf};

pub(super) struct Cached {
    workspace: PathBuf,
    module: ModuleId,
    terrain: ContentHash,
    revision: ContentHash,
    scene: ReferenceScene,
    area: ReferenceArea,
    snapshot: Option<Snapshot>,
    payload: Option<(usize, Vec<u8>)>,
}

pub(super) fn execute(
    cache: &mut Option<Cached>,
    workspace: &Path,
    host: &AuthoringHost,
    request: Request,
) -> Result<Reply, String> {
    let head = request.expected_head();
    let current = host.managed_module_identity();
    if cache
        .as_ref()
        .is_some_and(|entry| entry.workspace != workspace || Some(entry.module) != current)
    {
        *cache = None;
    }
    match request {
        Request::Describe { .. } => {
            let snapshot = describe(cache, workspace, host, head, current)?;
            Ok(Reply::Description {
                snapshot: snapshot.map(Box::new),
            })
        }
        Request::Read {
            expected_package,
            resource_index,
            offset,
            ..
        } => {
            let entry = cache
                .as_mut()
                .filter(|entry| entry.revision == expected_package)
                .ok_or("Current module has no matching installed reference resources")?;
            read_chunk(
                entry.snapshot.as_ref(),
                &mut entry.payload,
                head,
                resource_index,
                offset,
                |name, size| entry.area.read_resource(&entry.scene, name, size),
            )
            .map(|chunk| Reply::Resource { chunk })
        }
    }
}

fn describe(
    cache: &mut Option<Cached>,
    workspace: &Path,
    host: &AuthoringHost,
    head: ContentHash,
    module: Option<ModuleId>,
) -> Result<Option<Snapshot>, String> {
    let Some((module, revision)) = module.zip(std::env::var_os(
        "FORGE_MODULE_ASSET_REFERENCE_MANIFEST_SHA256",
    )) else {
        *cache = None;
        return Ok(None);
    };
    if let Some(snapshot) = cache
        .as_ref()
        .and_then(|entry| entry.snapshot.as_ref())
        .filter(|snapshot| snapshot.head_hash == head)
    {
        return Ok(Some(snapshot.clone()));
    }
    if let Some(entry) = cache.as_mut() {
        entry.snapshot = None;
        entry.payload = None;
    }
    let archive = host.game_archive(head)?;
    let zone = ZoneEditSession::from_archive_bytes(&archive)?;
    let zone_head = zone.head();
    let Some(terrain) = ReferenceScene::terrain_pin(&zone)? else {
        return Ok(None);
    };
    let revision = ContentHash::parse(revision.to_str().ok_or("Invalid reference manifest pin")?)
        .map_err(|e| e.to_string())?;
    if cache
        .as_ref()
        .is_none_or(|entry| entry.terrain != terrain || entry.revision != revision)
    {
        let root =
            std::env::var_os("FORGE_MODULE_ASSET_ROOT").ok_or("Legacy reference scene absent")?;
        let scene = ReferenceScene::open(Path::new(&root), revision)?;
        let area = scene.index_area(terrain)?;
        *cache = Some(Cached {
            workspace: workspace.to_owned(),
            module,
            terrain,
            revision,
            scene,
            area,
            snapshot: None,
            payload: None,
        });
    }
    let entry = cache.as_mut().ok_or("Legacy reference cache absent")?;
    let bindings = entry.area.describe(&entry.scene, &zone)?;
    let snapshot = Snapshot {
        head_hash: head,
        package_sha256: revision,
        legacy_reference: true,
        module_id: module,
        area_id: zone_head.base.terrain.area_id,
        vegetation: Some(bindings.vegetation),
        objects: bindings.objects,
        fish_profiles: Vec::new(),
        environment: bindings.environment,
        terrain: None,
        ocean: None,
        local_lights: Vec::new(),
        placement_companions: Vec::new(),
        species: None,
        files: bindings.files,
    };
    entry.snapshot = Some(snapshot.clone());
    Ok(Some(snapshot))
}
