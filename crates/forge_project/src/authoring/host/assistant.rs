//! Trusted application entry points. MCP callers cannot supply policy/approval.
use super::AuthoringHost;
use crate::{
    assistant::{self, Approval, PlanReview, ScopePolicy},
    audit::AuditStamp,
    authoring::{AuthoringRequest, AuthoringState},
    zone_authoring::session::ZoneRequest,
};

impl AuthoringHost {
    /// Read canonical receipts when an accepted source mutation outlived its job acknowledgement.
    pub fn assistant_receipt(
        &self,
        command: forge_core::CommandId,
    ) -> Result<Option<crate::zone_authoring::session::ZoneReceipt>, String> {
        Ok(self
            .active
            .as_ref()
            .ok_or("Open a zone before reading its receipt")?
            .zone_session()?
            .audit()
            .iter()
            .find(|receipt| receipt.request.command_id == command)
            .cloned())
    }

    /// Previews the real active session, including native world/workspace checks.
    pub fn prepare_assistant(
        &mut self,
        request: ZoneRequest,
        policy: &ScopePolicy,
        audit: AuditStamp,
    ) -> Result<PlanReview, String> {
        let session = self
            .active
            .as_ref()
            .ok_or("Open a zone before planning")?
            .zone_session()?;
        let review = assistant::prepare(session, &request, policy, audit.clone())?;
        let state = self.execute_with_audit(AuthoringRequest::PreviewZone { request }, audit)?;
        if state.zone_receipt.as_ref() != Some(review.receipt()) {
            return Err("Native assistant preview differs from reviewed transaction".into());
        }
        Ok(review)
    }

    /// Applies through the common host; managed zones keep their workspace
    /// transaction and native candidate validation. No separate writable session.
    pub fn apply_assistant(
        &mut self,
        policy: &ScopePolicy,
        review: &PlanReview,
        approval: &Approval,
    ) -> Result<AuthoringState, String> {
        let session = self
            .active
            .as_ref()
            .ok_or("Open a zone before applying")?
            .zone_session()?;
        assistant::validate_application(session, policy, review, approval)?;
        if session
            .audit()
            .iter()
            .any(|entry| entry == review.receipt())
        {
            let mut state = self.execute(AuthoringRequest::Inspect)?;
            state.zone_receipt = Some(review.receipt().clone());
            return Ok(state);
        }
        let request = review.receipt().request.clone();
        let audit = review.receipt().audit.clone();
        // Rebuilds the native workspace review even when another preview replaced it.
        let state = self.execute_with_audit(
            AuthoringRequest::PreviewZone {
                request: request.clone(),
            },
            audit.clone(),
        )?;
        if state.zone_receipt.as_ref() != Some(review.receipt()) {
            return Err("Native assistant preview changed; review again".into());
        }
        self.execute_with_audit(AuthoringRequest::ApplyZone { request }, audit)
    }
}
