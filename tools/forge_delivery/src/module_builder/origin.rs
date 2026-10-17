//! Preserve source module conditions independently from licenses declared for new edits.
use super::ExportOrigin;
use crate::{Result, orpmod};
use forge_core::{ContentHash, ModuleId, bytes_sha256};
use forge_project::module_package::{ModuleFileRole, ModuleRights};
use std::collections::BTreeMap;

const PREFIX: &str = "_forge-export/origin";

pub(super) struct PreparedOrigin {
    documents: orpmod::OriginDocuments,
    package_sha256: ContentHash,
    package_bytes: u64,
}

pub(super) fn prepare(
    origin: Option<ExportOrigin>,
    module: ModuleId,
    rights: ModuleRights,
) -> Result<Option<PreparedOrigin>> {
    let Some(origin) = origin else {
        return Ok(None);
    };
    let length = origin.package_bytes.len() as u64;
    if length > forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES {
        return Err("Origin package exceeds the bounded origin admission budget".into());
    }
    if bytes_sha256(&origin.package_bytes) != origin.expected_package_sha256 {
        return Err("Origin package bytes differ from the canonical import pin".into());
    }
    let documents = orpmod::read_origin_documents(origin.package_bytes)?;
    if documents.module.module_id != module {
        return Err("Origin package belongs to another module".into());
    }
    if documents.module.provenance.rights == ModuleRights::PrivateLocalOnly
        && rights == ModuleRights::Redistributable
    {
        return Err(
            "A private/local origin module cannot be published by changing the new export license"
                .into(),
        );
    }
    Ok(Some(PreparedOrigin {
        documents,
        package_sha256: origin.expected_package_sha256,
        package_bytes: length,
    }))
}

impl PreparedOrigin {
    pub(super) fn proof(&self, head: ContentHash) -> serde_json::Value {
        serde_json::json!({"schema":"forge.module-export-origin","version":1,
            "module_id":self.documents.module.module_id,"project_id":self.documents.module.project_id,
            "package_sha256":self.package_sha256,"package_bytes":self.package_bytes,
            "module_manifest_sha256":bytes_sha256(&self.documents.module_manifest),
            "package_manifest_sha256":bytes_sha256(&self.documents.transport_manifest),
            "origin_rights":self.documents.module.provenance.rights,"origin_publisher":self.documents.module.provenance.publisher,
            "exported_head_sha256":head,
            "scope":"Exact admitted module origin and rights documents; no claim of full historical content migration"})
    }

    pub(super) fn credits_reference(&self) -> String {
        format!(
            "\nSource module conditions remain applicable. Original credits: {PREFIX}/files/{} ; source manifest and package proof: {PREFIX}/.",
            self.documents.module.provenance.credits_path
        )
    }

    pub(super) fn append(
        self,
        head: ContentHash,
        payloads: &mut BTreeMap<String, Vec<u8>>,
        roles: &mut BTreeMap<String, ModuleFileRole>,
    ) -> Result<()> {
        let proof = crate::json(&self.proof(head))?;
        for (path, bytes) in [
            (
                format!("{PREFIX}/module.json"),
                self.documents.module_manifest,
            ),
            (
                format!("{PREFIX}/package-manifest.json"),
                self.documents.transport_manifest,
            ),
            (format!("{PREFIX}/source-package.json"), proof),
        ] {
            insert(path, bytes, ModuleFileRole::Provenance, payloads, roles)?;
        }
        for (path, bytes) in self.documents.evidence {
            let role = if self
                .documents
                .module
                .provenance
                .license_paths
                .contains(&path)
            {
                ModuleFileRole::License
            } else {
                ModuleFileRole::Provenance
            };
            insert(
                format!("{PREFIX}/files/{path}"),
                bytes,
                role,
                payloads,
                roles,
            )?;
        }
        Ok(())
    }
}

fn insert(
    path: String,
    bytes: Vec<u8>,
    role: ModuleFileRole,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    roles: &mut BTreeMap<String, ModuleFileRole>,
) -> Result<()> {
    if payloads.contains_key(&path) || roles.contains_key(&path) {
        return Err(format!(
            "Asset input conflicts with a preserved origin document: {path}"
        ));
    }
    payloads.insert(path.clone(), bytes);
    roles.insert(path, role);
    Ok(())
}
