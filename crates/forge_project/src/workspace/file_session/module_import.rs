//! Import admitted module archives into the existing workspace authority.
use super::FileWorkspaceSession;
use crate::{
    audit::AuditStamp,
    module_package::{ModuleFileRole, ModulePackageManifest},
    workspace::{
        ModuleCreatorReference, ModuleEntry, ModuleOriginPin,
        export::MAX_MODULE_EXPORT_SOURCE_BYTES,
        objects::ReviewedZone,
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest},
        source_blobs::SourceBlobStore,
    },
    zone_authoring::MAX_ZONE_HEAD_BYTES,
};
use forge_core::{CommandId, ContentHash, bytes_sha256};
use forge_gameplay::ModuleCreatorProfile;
use std::{collections::BTreeMap, fs};

pub struct PreparedModuleImport {
    review: WorkspaceReceipt,
    zones: Vec<ReviewedZone>,
    sources: BTreeMap<ContentHash, (String, usize)>,
    creator: Option<ModuleCreatorProfile>,
}

impl PreparedModuleImport {
    #[must_use]
    pub fn review(&self) -> &WorkspaceReceipt {
        &self.review
    }
}

impl FileWorkspaceSession {
    /// Prepare a single undoable edit from a native package reader. The reader
    /// must hold the same admitted package as the immutable origin pin.
    pub fn prepare_module_import(
        &self,
        manifest: &ModulePackageManifest,
        origin: ModuleOriginPin,
        audit: AuditStamp,
        read_payload: impl Fn(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<PreparedModuleImport, String> {
        manifest.validate().map_err(|error| error.to_string())?;
        let head = self.session.head();
        let base_hash = head.content_hash().map_err(|error| error.to_string())?;
        let new_project_name = (!head.projects.iter().any(|p| p.id == manifest.project_id))
            .then(|| manifest.name.clone());
        let mut remaining = MAX_MODULE_EXPORT_SOURCE_BYTES;
        let mut zones = Vec::new();
        let mut sources = BTreeMap::new();
        for zone in &manifest.zones {
            if super::super::ownership::owns(&self.session, zone.area_id) {
                return Err(format!(
                    "Zone {} is already owned by this workspace",
                    zone.area_id
                ));
            }
            let directory = self.root.join(zone.area_id.to_string());
            match fs::symlink_metadata(&directory) {
                Ok(_) => return Err(format!("Zone {} is already reserved", zone.area_id)),
                Err(error) if error.kind() == std::io::ErrorKind::NotFound => (),
                Err(error) => return Err(error.to_string()),
            }
            let bytes = read_payload(&zone.archive_path, remaining.min(MAX_ZONE_HEAD_BYTES))?;
            remaining = remaining
                .checked_sub(bytes.len())
                .ok_or("Module source budget exceeded")?;
            manifest
                .validate_zone_archive(zone.area_id, &bytes)
                .map_err(|error| error.to_string())?;
            let reviewed = ReviewedZone::from_bytes(&bytes, zone.archive_sha256)?;
            for (&hash, &length) in reviewed.required_source_blobs() {
                if sources.contains_key(&hash) {
                    continue;
                }
                let source = manifest
                    .files
                    .iter()
                    .find(|file| {
                        file.role == ModuleFileRole::SourceBlob
                            && file.sha256 == hash
                            && file.bytes == length as u64
                    })
                    .ok_or("An archive source is absent from the admitted package")?;
                remaining = remaining
                    .checked_sub(length)
                    .ok_or("Module source budget exceeded")?;
                sources.insert(hash, (source.path.clone(), length));
            }
            zones.push(reviewed);
        }
        let creator = manifest
            .creator
            .as_ref()
            .map(|definition| {
                let bytes = definition.to_bytes().map_err(|error| error.to_string())?;
                Ok::<_, String>(ModuleCreatorReference {
                    sha256: bytes_sha256(&bytes),
                    bytes: bytes.len() as u64,
                })
            })
            .transpose()?;
        let request = WorkspaceRequest {
            command_id: CommandId::deterministic(format!(
                "module-import/{base_hash}/{}",
                origin.package_sha256
            )),
            base_revision: head.revision,
            base_hash,
            action: WorkspaceAction::ImportModulePackage {
                project_id: manifest.project_id,
                new_project_name,
                module: ModuleEntry {
                    id: manifest.module_id,
                    name: manifest.name.clone(),
                    origin: Some(origin),
                    creator,
                    zones: zones.iter().map(|zone| zone.reference().clone()).collect(),
                },
            },
        };
        let review = self
            .session
            .preview_with_audit(&request, audit)
            .map_err(|error| error.to_string())?;
        Ok(PreparedModuleImport {
            review,
            zones,
            sources,
            creator: manifest.creator.clone(),
        })
    }

    /// Publish immutable objects first. A failed import leaves the workspace
    /// head intact and does not replace any existing zone or source.
    pub fn apply_module_import(
        &mut self,
        prepared: PreparedModuleImport,
        read_payload: impl Fn(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<WorkspaceReceipt, String> {
        let PreparedModuleImport {
            review,
            zones,
            sources,
            creator,
        } = prepared;
        let receipt = self
            .session
            .preview_with_audit(&review.request, review.audit.clone())
            .map_err(|error| error.to_string())?;
        if self
            .session
            .audit()
            .iter()
            .any(|entry| entry.request.command_id == review.request.command_id)
        {
            return Ok(receipt);
        }
        if !sources.is_empty() {
            let store = SourceBlobStore::open(&self.root.join("workspace-v1"))?;
            for (hash, (path, length)) in sources {
                let bytes = read_payload(&path, length)?;
                if bytes.len() != length {
                    return Err("Imported source differs from the reviewed length".into());
                }
                store.publish(hash, &bytes)?;
            }
        }
        if let Some(creator) = creator {
            self.objects.publish_creator(&creator)?;
        }
        for zone in &zones {
            self.objects.publish(zone)?;
        }
        let mut directories = Vec::new();
        let result = (|| {
            // Legacy creation also uses create_dir, so it cannot claim any of
            // these identities between the workspace edit and its first save.
            for zone in &zones {
                let directory = self.root.join(zone.reference().area_id.to_string());
                fs::create_dir(&directory).map_err(|error| error.to_string())?;
                directories.push(directory);
            }
            self.session
                .execute_with_audit(&review.request, review.audit)
                .map_err(|error| error.to_string())
        })();
        if result.is_err() {
            for directory in directories {
                let _ = fs::remove_dir(directory);
            }
        }
        result
    }
}
