//! Transient area navigation, identified by source IDs rather than labels.
use forge_core::{AreaId, ModuleId, ProjectId};
use forge_project::WorkspaceDocument;
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, Eq, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AreaKey {
    pub project_id: ProjectId,
    pub module_id: ModuleId,
    pub area_id: AreaId,
}

impl AreaKey {
    #[must_use]
    pub fn exists(self, document: &WorkspaceDocument) -> bool {
        document.project.id == self.project_id
            && document.area(self.module_id, self.area_id).is_some()
    }
}

#[derive(Clone, Debug, Default, Eq, PartialEq)]
pub struct AreaTabs {
    pub open: Vec<AreaKey>,
    pub active: Option<AreaKey>,
}

impl AreaTabs {
    pub fn open(&mut self, key: AreaKey, document: &WorkspaceDocument) {
        self.reconcile(Some(document));
        if !key.exists(document) {
            return;
        }
        if !self.open.contains(&key) {
            self.open.push(key);
        }
        self.active = Some(key);
    }

    pub fn close(&mut self, key: AreaKey) {
        let Some(index) = self.open.iter().position(|item| *item == key) else {
            return;
        };
        self.open.remove(index);
        if self.active == Some(key) {
            self.active = self.open.get(index).or_else(|| self.open.last()).copied();
        }
    }

    pub fn reconcile(&mut self, document: Option<&WorkspaceDocument>) {
        self.open
            .retain(|key| document.is_some_and(|doc| key.exists(doc)));
        if self.active.is_some_and(|key| !self.open.contains(&key)) {
            self.active = self.open.last().copied();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{CommandId, Vec3};
    use forge_project::NewProjectCommand;

    fn document() -> WorkspaceDocument {
        WorkspaceDocument::create(NewProjectCommand {
            command_id: CommandId::deterministic("tabs"),
            project_id: ProjectId::deterministic("tabs"),
            module_id: ModuleId::deterministic("tabs"),
            area_id: AreaId::deterministic("tabs"),
            project_name: "Project".into(),
            module_name: "Module".into(),
            area_name: "Area".into(),
            seed: 42,
            spawn: Vec3::new(0.0, 2.0, 0.0),
        })
        .unwrap()
        .document
    }

    #[test]
    fn duplicate_open_is_idempotent_and_close_is_only_presentation() {
        let doc = document();
        let before = doc.content_hash().unwrap();
        let key = AreaKey {
            project_id: doc.project.id,
            module_id: doc.project.modules[0].id,
            area_id: doc.project.modules[0].areas[0].id,
        };
        let mut tabs = AreaTabs::default();
        tabs.open(key, &doc);
        tabs.open(key, &doc);
        assert_eq!(tabs.open, [key]);
        tabs.close(key);
        assert_eq!(tabs, AreaTabs::default());
        assert_eq!(doc.content_hash().unwrap(), before);
    }

    #[test]
    fn stale_area_or_another_project_cannot_survive_reconciliation() {
        let mut doc = document();
        let key = AreaKey {
            project_id: doc.project.id,
            module_id: doc.project.modules[0].id,
            area_id: doc.project.modules[0].areas[0].id,
        };
        let mut tabs = AreaTabs::default();
        tabs.open(key, &doc);
        doc.project.modules[0].areas.clear();
        tabs.reconcile(Some(&doc));
        assert_eq!(tabs, AreaTabs::default());
        tabs.open(key, &doc);
        assert!(tabs.open.is_empty());
        tabs.open(key, &document());
        tabs.reconcile(None);
        assert_eq!(tabs, AreaTabs::default());
    }

    #[test]
    fn closing_active_tab_selects_its_neighbour_and_renaming_keeps_identity() {
        let mut doc = document();
        let module = &mut doc.project.modules[0];
        let mut second = module.areas[0].clone();
        second.id = AreaId::deterministic("second");
        second.terrain.area_id = second.id;
        module.areas.push(second);
        let first = AreaKey {
            project_id: doc.project.id,
            module_id: module.id,
            area_id: module.areas[0].id,
        };
        let second = AreaKey {
            area_id: module.areas[1].id,
            ..first
        };
        let mut tabs = AreaTabs::default();
        tabs.open(first, &doc);
        tabs.open(second, &doc);
        doc.project.modules[0].areas[1].name = "Renamed".into();
        tabs.reconcile(Some(&doc));
        assert_eq!(tabs.active, Some(second));
        tabs.close(second);
        assert_eq!(tabs.active, Some(first));
        let mut another = doc.clone();
        another.project.id = ProjectId::deterministic("different project");
        tabs.reconcile(Some(&another));
        assert_eq!(tabs, AreaTabs::default());
    }
}
