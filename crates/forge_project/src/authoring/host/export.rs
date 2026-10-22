use super::AuthoringHost;
use crate::workspace::export::{ModuleAreaArchive, ModuleExportSnapshot};
use forge_core::ContentHash;

impl AuthoringHost {
    pub fn module_content_revision(
        &self,
        expected_head: ContentHash,
    ) -> Result<Option<ContentHash>, String> {
        self.active
            .as_ref()
            .ok_or("Open an area before reading its module revision")?
            .playtest_snapshot(expected_head)?;
        if self.managed_area().is_none() {
            return Ok(None);
        }
        let module = self.gameplay_module_id().ok_or("Owning module is absent")?;
        self.workspace
            .as_ref()
            .ok_or("Managed workspace is unavailable")?
            .module_content_hash(module)
            .map(Some)
    }
    pub fn module_export_snapshot(
        &self,
        expected_head: ContentHash,
        expected_workspace: Option<ContentHash>,
    ) -> Result<ModuleExportSnapshot, String> {
        let archive = self.game_archive(expected_head)?;
        let zone = crate::zone_authoring::session::ZoneEditSession::from_archive_bytes(&archive)?;
        let head = zone.head();
        let module = head
            .corrections
            .gameplay
            .as_deref()
            .ok_or("Module export requires RPG definitions in the active area")?
            .module_id;
        if self.managed_area().is_some() {
            return self
                .workspace
                .as_ref()
                .ok_or("Managed workspace is unavailable")?
                .module_export_snapshot(
                    module,
                    head.base.terrain.area_id,
                    expected_head,
                    expected_workspace.ok_or("Review the managed module before exporting")?,
                );
        }
        if expected_workspace.is_some() {
            return Err("Standalone area has no workspace export revision".into());
        }
        Ok(ModuleExportSnapshot {
            module_id: module,
            project_id: head.base.terrain.project_id,
            primary_area: head.base.terrain.area_id,
            primary_head: expected_head,
            workspace_hash: None,
            creator: None,
            zones: vec![ModuleAreaArchive {
                area_id: head.base.terrain.area_id,
                head_hash: expected_head,
                bytes: archive,
            }],
        })
    }

    pub fn module_export_revision(
        &self,
        expected_head: ContentHash,
    ) -> Result<Option<ContentHash>, String> {
        let active = self
            .active
            .as_ref()
            .ok_or("Open an area before exporting its module")?;
        active.playtest_snapshot(expected_head)?;
        self.managed_area()
            .map(|_| {
                self.workspace
                    .as_ref()
                    .ok_or("Managed workspace is unavailable")?
                    .session()
                    .head()
                    .content_hash()
                    .map_err(|error| error.to_string())
            })
            .transpose()
    }
}
