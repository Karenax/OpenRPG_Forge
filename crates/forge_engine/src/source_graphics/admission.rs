//! Completed profile resources, independent of subsequent visibility publications.
use crate::render_inventory::{GraphicsContextReceipt, RenderInventoryToken};
use bevy::prelude::World;
use forge_core::ContentHash;

/// The caller binds its effective settings and source policy into `identity`.
/// This admits simulation resources; it never certifies a new scene or capture.
pub struct AppliedProfileAdmission {
    identity: ContentHash,
    context: GraphicsContextReceipt,
}

impl AppliedProfileAdmission {
    /// Call only after the complete effective profile has passed its strict
    /// CPU/GPU admission. A real current inventory must cover its application.
    #[must_use]
    pub fn observe(
        world: &World,
        identity: ContentHash,
        applied_frame: u32,
        token: &RenderInventoryToken,
    ) -> Option<Self> {
        token.covers_frame(world, applied_frame).then_some(())?;
        Some(Self {
            identity,
            context: token.graphics_context(world)?,
        })
    }

    /// The host must also check its current effective CPU settings and physical
    /// source readiness. An identity or renderer-context change requires a new
    /// complete profile admission.
    #[must_use]
    pub fn is_current(&self, world: &World, identity: ContentHash) -> bool {
        self.identity == identity && self.context.is_current(world)
    }
}
