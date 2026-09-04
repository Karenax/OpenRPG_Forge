//! Read-only structural migration. Neither revisions, IDs nor terrain content are rewritten.
use crate::{
    WORKSPACE_SCHEMA_VERSION, WorkspaceArea, WorkspaceDocument, WorkspaceError, WorkspaceModule,
    WorkspaceProject,
};
use forge_core::{ModuleId, ProjectId, Revision};
use serde::Deserialize;

#[derive(Deserialize)]
struct Version {
    schema_version: u32,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct V1 {
    schema: String,
    schema_version: u32,
    revision: Revision,
    project: ProjectV1,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ProjectV1 {
    id: ProjectId,
    name: String,
    module: ModuleV1,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct ModuleV1 {
    id: ModuleId,
    name: String,
    area: WorkspaceArea,
}

pub(super) fn read(json: &str) -> Result<WorkspaceDocument, WorkspaceError> {
    // Inspect only the version, then deserialize the original bytes strictly. Parsing through
    // Value would discard duplicate keys before the actual schema had a chance to reject them.
    match serde_json::from_str::<Version>(json)?.schema_version {
        1 => {
            let old: V1 = serde_json::from_str(json)?;
            debug_assert_eq!(old.schema_version, 1);
            Ok(WorkspaceDocument {
                schema: old.schema,
                schema_version: WORKSPACE_SCHEMA_VERSION,
                revision: old.revision,
                project: WorkspaceProject {
                    id: old.project.id,
                    name: old.project.name,
                    modules: vec![WorkspaceModule {
                        id: old.project.module.id,
                        name: old.project.module.name,
                        areas: vec![old.project.module.area],
                    }],
                },
            })
        }
        WORKSPACE_SCHEMA_VERSION => Ok(serde_json::from_str(json)?),
        version => Err(WorkspaceError::UnsupportedVersion(version)),
    }
}
