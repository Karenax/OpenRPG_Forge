use super::{AuthoringHost, session::ActiveSession};
use crate::zone_authoring::session::ZoneCompactionReview;

impl AuthoringHost {
    pub(super) fn review_zone_compaction(&mut self) -> Result<ZoneCompactionReview, String> {
        let Some(area) = self.managed_area() else {
            return self.zone()?.session().review_compaction();
        };
        let workspace = self
            .workspace
            .as_ref()
            .ok_or("Managed workspace unavailable")?;
        let (review, receipt) = workspace.preview_zone_compaction(area)?;
        self.managed_compaction = Some(receipt.request);
        Ok(review)
    }

    pub(super) fn compact_zone(&mut self, review: &ZoneCompactionReview) -> Result<(), String> {
        let Some(area) = self.managed_area() else {
            return self.zone()?.compact(review);
        };
        let request = self
            .managed_compaction
            .as_ref()
            .ok_or("Review managed compaction before applying")?;
        let workspace = self
            .workspace
            .as_mut()
            .ok_or("Managed workspace unavailable")?;
        workspace.apply_zone_compaction(request, review)?;
        self.active = Some(ActiveSession::Managed {
            session: Box::new(workspace.read_zone(area)?),
            dirty: workspace.zone_is_dirty(area),
        });
        self.managed_review = None;
        Ok(())
    }
}
