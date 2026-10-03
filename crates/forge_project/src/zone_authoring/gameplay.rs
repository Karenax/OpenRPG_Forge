//! Gameplay has the same aggregate authority, inverse and audit as all other zone edits.
use super::{
    ZoneAuthoringHead,
    session::{ZoneAction, ZoneRequest},
};
use forge_core::CommandId;
pub use forge_gameplay::GameplayDocument;

pub(super) fn validate(
    head: &ZoneAuthoringHead,
    document: &GameplayDocument,
) -> Result<(), String> {
    document.validate().map_err(|e| e.to_string())?;
    let terrain = &head.base.terrain;
    if document.area_id != terrain.area_id {
        return Err("Gameplay area identity differs from its canonical zone".into());
    }
    let inside = |point: forge_core::Vec3| {
        point.x.abs() <= terrain.extent_m.size_x_m * 0.5
            && point.z.abs() <= terrain.extent_m.size_z_m * 0.5
    };
    if document
        .actors
        .iter()
        .any(|a| !inside(a.position) || a.patrol.iter().any(|p| !inside(*p)))
        || document.objects.iter().any(|o| !inside(o.position))
        || document.places.iter().any(|p| !inside(p.position))
    {
        return Err("Gameplay placement lies outside its canonical terrain".into());
    }
    Ok(())
}

impl ZoneAuthoringHead {
    pub fn prepare_gameplay(
        &self,
        document: Option<GameplayDocument>,
        command_id: CommandId,
    ) -> Result<ZoneRequest, String> {
        self.validate()?;
        if let Some(document) = &document {
            validate(self, document)?;
        }
        let mut corrections = self.corrections.clone();
        corrections.gameplay = document.map(Box::new);
        Ok(ZoneRequest {
            command_id,
            expected_revision: self.revision,
            expected_hash: self.content_hash()?,
            action: ZoneAction::edit(corrections),
        })
    }
}
