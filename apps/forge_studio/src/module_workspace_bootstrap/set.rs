//! Qualification producer for several admitted modules in one fresh workspace.
use super::{BootstrapReport, BootstrapRequest, files};
use forge_core::{AreaId, ContentHash};
use serde::{Deserialize, Serialize};
use std::{
    collections::{BTreeMap, BTreeSet},
    path::PathBuf,
    sync::Arc,
};

pub const MAX_BOOTSTRAP_MODULES: usize = 16;

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSetInput {
    pub package_path: PathBuf,
    pub expected_package_sha256: ContentHash,
    pub area_id: AreaId,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleSetRequest {
    pub modules: Vec<ModuleSetInput>,
    pub destination: PathBuf,
}

#[derive(Debug, Serialize)]
pub struct ModuleSetReport {
    pub workspace_root: PathBuf,
    pub modules: Vec<BootstrapReport>,
}

pub fn prepare_module_set_workspace(request: &ModuleSetRequest) -> Result<ModuleSetReport, String> {
    if request.modules.is_empty() || request.modules.len() > MAX_BOOTSTRAP_MODULES {
        return Err("Module set producer accepts 1 to 16 modules".into());
    }
    let parent = files::fresh_destination(&request.destination)?;
    let mut admitted = Vec::new();
    let mut module_ids = BTreeSet::new();
    let mut area_ids = BTreeSet::new();
    let mut assets = BTreeMap::new();
    let mut remaining = forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES;
    for input in &request.modules {
        let bytes: Arc<[u8]> = files::read_bounded(&input.package_path, remaining)?.into();
        remaining -= bytes.len() as u64;
        if forge_core::bytes_sha256(&bytes) != input.expected_package_sha256 {
            return Err("Module set package differs from its reviewed pin".into());
        }
        let documents = forge_delivery::orpmod::read_origin_documents(Arc::clone(&bytes))?;
        super::validate_producer_module(&documents.module, input.area_id)?;
        if !module_ids.insert(documents.module.module_id) {
            return Err("Module set repeats a module identity".into());
        }
        for zone in &documents.module.zones {
            if !area_ids.insert(zone.area_id) {
                return Err("Module set repeats an area identity".into());
            }
        }
        for asset in &documents.module.assets {
            if assets
                .insert(asset.asset_id, asset.sha256)
                .is_some_and(|old| old != asset.sha256)
            {
                return Err(
                    "Module set needs conflicting revisions of one shared asset identity".into(),
                );
            }
        }
        if assets.len() > super::MAX_BOOTSTRAP_ASSETS {
            return Err("Module set exceeds the producer's combined asset budget".into());
        }
        admitted.push((input, bytes, documents));
    }
    let stage = files::new_stage(&parent)?;
    let result: Result<ModuleSetReport, String> = (|| {
        let mut reports = Vec::new();
        for (input, bytes, documents) in admitted {
            reports.push(super::populate_staged(
                &BootstrapRequest {
                    package_path: input.package_path.clone(),
                    expected_package_sha256: input.expected_package_sha256,
                    area_id: input.area_id,
                    destination: request.destination.clone(),
                },
                &stage,
                &bytes,
                &documents,
            )?);
        }
        files::activate(&stage, &request.destination)?;
        Ok(ModuleSetReport {
            workspace_root: request.destination.clone(),
            modules: reports,
        })
    })();
    result.map_err(|error| {
        format!(
            "{error}; inactive module set stage retained at {}",
            stage.display()
        )
    })
}
