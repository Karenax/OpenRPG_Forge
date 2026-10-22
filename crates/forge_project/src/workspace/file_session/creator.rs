//! One workspace edit publishes the definition and all area materializations together.
use super::FileWorkspaceSession;
use crate::{
    audit::AuditStamp,
    workspace::{
        ModuleCreatorReference, ModuleCreatorReview,
        objects::{ReviewedZone, ZoneObjectStore},
        session::{WorkspaceAction, WorkspaceReceipt, WorkspaceRequest, WorkspaceSession},
    },
    zone_authoring::session::ZoneEditSession,
};
use forge_core::{CommandId, ModuleId, bytes_sha256};
use forge_gameplay::ModuleCreatorProfile;
use std::collections::BTreeSet;

struct PreparedCreator {
    review: ModuleCreatorReview,
    zones: Vec<ReviewedZone>,
}

impl FileWorkspaceSession {
    pub fn module_creator(&self, module: ModuleId) -> Result<Option<ModuleCreatorProfile>, String> {
        let entry = self
            .session
            .head()
            .projects
            .iter()
            .flat_map(|project| &project.modules)
            .find(|entry| entry.id == module)
            .ok_or("Module is absent from workspace")?;
        entry
            .creator
            .map(|pin| self.objects.read_creator(module, pin))
            .transpose()
    }

    pub fn preview_module_creator(
        &self,
        definition: ModuleCreatorProfile,
        command: CommandId,
    ) -> Result<ModuleCreatorReview, String> {
        self.preview_module_creator_with_audit(definition, command, AuditStamp::legacy())
    }

    pub fn preview_module_creator_with_audit(
        &self,
        definition: ModuleCreatorProfile,
        command: CommandId,
        audit: AuditStamp,
    ) -> Result<ModuleCreatorReview, String> {
        Ok(self.prepare_creator(definition, command, audit)?.review)
    }

    pub fn apply_module_creator(
        &mut self,
        review: &ModuleCreatorReview,
    ) -> Result<WorkspaceReceipt, String> {
        self.apply_module_creator_with_audit(review, AuditStamp::legacy())
    }

    pub fn apply_module_creator_with_audit(
        &mut self,
        review: &ModuleCreatorReview,
        audit: AuditStamp,
    ) -> Result<WorkspaceReceipt, String> {
        let prepared = self.prepare_creator(
            review.definition.clone(),
            review.request.command_id,
            audit.clone(),
        )?;
        if prepared.review != *review {
            return Err("Module creator changed since review".into());
        }
        let preview = self
            .session
            .preview_with_audit(&review.request, audit.clone())
            .map_err(|error| error.to_string())?;
        if self
            .session
            .audit()
            .iter()
            .any(|entry| entry.request.command_id == review.request.command_id)
        {
            return Ok(preview);
        }
        let pin = self.objects.publish_creator(&review.definition)?;
        let WorkspaceAction::ConfigureCreator { after, .. } = &review.request.action else {
            return Err("Creator review has another action".into());
        };
        if pin != *after {
            return Err("Published creator differs from review".into());
        }
        for zone in &prepared.zones {
            self.objects.publish(zone)?;
        }
        self.session
            .execute_with_audit(&review.request, audit)
            .map_err(|error| error.to_string())
    }

    fn prepare_creator(
        &self,
        definition: ModuleCreatorProfile,
        command: CommandId,
        audit: AuditStamp,
    ) -> Result<PreparedCreator, String> {
        let bytes = definition.to_bytes().map_err(|error| error.to_string())?;
        let pin = ModuleCreatorReference {
            sha256: bytes_sha256(&bytes),
            bytes: bytes.len() as u64,
        };
        if let Some(receipt) = self
            .session
            .audit()
            .iter()
            .find(|entry| entry.request.command_id == command)
        {
            return replay_review(receipt, definition, pin);
        }
        let (project, module) = self
            .session
            .head()
            .projects
            .iter()
            .find_map(|project| {
                project
                    .modules
                    .iter()
                    .find(|module| module.id == definition.module_id)
                    .map(|module| (project, module))
            })
            .ok_or("Module is absent from workspace")?;
        let mut after_zones = module.zones.clone();
        let mut zones = Vec::new();
        let mut changed_areas = Vec::new();
        let mut remaining = 256 * 1024 * 1024;
        for (reference, after) in module.zones.iter().zip(&mut after_zones) {
            let bytes = self
                .objects
                .read_with_budget(project.id, reference, remaining)?;
            remaining -= bytes.len();
            let mut session = ZoneEditSession::from_archive_bytes(&bytes)?;
            let Some(gameplay) = session.head().corrections.gameplay.as_deref() else {
                continue;
            };
            if definition.validate_document(gameplay).is_ok() {
                continue;
            }
            let materialized = definition
                .apply_to(gameplay)
                .map_err(|error| error.to_string())?;
            let request = session.head().prepare_gameplay(
                Some(materialized),
                CommandId::deterministic(format!("module-creator/{command}/{}", reference.area_id)),
            )?;
            session.execute_with_audit(&request, audit.clone())?;
            let bytes = session.archive_bytes()?;
            let reviewed = ReviewedZone::from_bytes(&bytes, bytes_sha256(&bytes))?;
            *after = reviewed.reference().clone();
            changed_areas.push(reference.area_id);
            zones.push(reviewed);
        }
        let request = WorkspaceRequest {
            command_id: command,
            base_revision: self.session.head().revision,
            base_hash: self
                .session
                .head()
                .content_hash()
                .map_err(|error| error.to_string())?,
            action: WorkspaceAction::ConfigureCreator {
                project_id: project.id,
                module_id: module.id,
                before: module.creator,
                after: pin,
                before_zones: module.zones.clone(),
                after_zones,
            },
        };
        self.session
            .preview_with_audit(&request, audit)
            .map_err(|error| error.to_string())?;
        Ok(PreparedCreator {
            review: ModuleCreatorReview {
                request,
                definition,
                changed_areas,
            },
            zones,
        })
    }
}

fn replay_review(
    receipt: &WorkspaceReceipt,
    definition: ModuleCreatorProfile,
    pin: ModuleCreatorReference,
) -> Result<PreparedCreator, String> {
    let WorkspaceAction::ConfigureCreator {
        module_id,
        after,
        before_zones,
        after_zones,
        ..
    } = &receipt.request.action
    else {
        return Err("Command id was used for another action".into());
    };
    if *module_id != definition.module_id || *after != pin {
        return Err("Command id was used with another creator definition".into());
    }
    Ok(PreparedCreator {
        review: ModuleCreatorReview {
            request: receipt.request.clone(),
            definition,
            changed_areas: before_zones
                .iter()
                .zip(after_zones)
                .filter(|(before, after)| before != after)
                .map(|(_, after)| after.area_id)
                .collect(),
        },
        zones: Vec::new(),
    })
}

pub(super) fn verify_creator_references(
    session: &WorkspaceSession,
    objects: &ZoneObjectStore,
) -> Result<(), String> {
    let mut profiles = std::collections::BTreeMap::new();
    let mut checked = BTreeSet::new();
    let mut remaining_profiles = 16 * 1024 * 1024;
    let mut remaining_zones = 256 * 1024 * 1024;
    for head in std::iter::once(session.head()).chain(
        session
            .audit()
            .iter()
            .flat_map(|entry| [&entry.before, &entry.after]),
    ) {
        for project in &head.projects {
            for module in &project.modules {
                let Some(pin) = module.creator else {
                    continue;
                };
                let key = (module.id, pin.sha256, pin.bytes);
                if let std::collections::btree_map::Entry::Vacant(entry) = profiles.entry(key) {
                    if pin.bytes > remaining_profiles {
                        return Err("Creator history exceeds verification byte budget".into());
                    }
                    remaining_profiles -= pin.bytes;
                    entry.insert(objects.read_creator(module.id, pin)?);
                }
                let definition = &profiles[&key];
                for zone in &module.zones {
                    if !checked.insert((
                        project.id,
                        module.id,
                        pin.sha256,
                        zone.archive_hash,
                        zone.head_hash,
                    )) {
                        continue;
                    }
                    let bytes = objects.read_with_budget(project.id, zone, remaining_zones)?;
                    remaining_zones -= bytes.len();
                    let session = ZoneEditSession::from_archive_bytes(&bytes)?;
                    if let Some(gameplay) = session.head().corrections.gameplay.as_deref() {
                        definition
                            .validate_document(gameplay)
                            .map_err(|error| error.to_string())?;
                    }
                }
            }
        }
    }
    Ok(())
}
