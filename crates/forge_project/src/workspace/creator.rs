//! Serializable proposal shared by the native authority and the Studio.
use super::session::WorkspaceRequest;
use forge_core::AreaId;
pub use forge_gameplay::{CreatorSurface, ModuleCreatorProfile};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModuleCreatorReview {
    pub request: WorkspaceRequest,
    pub definition: ModuleCreatorProfile,
    pub changed_areas: Vec<AreaId>,
}
