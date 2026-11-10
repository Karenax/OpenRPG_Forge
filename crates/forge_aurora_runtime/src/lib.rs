#![deny(unsafe_code)]
#![allow(
    clippy::assigning_clones,
    clippy::borrow_deref_ref,
    clippy::cast_possible_truncation,
    clippy::cast_precision_loss,
    clippy::cast_sign_loss,
    clippy::doc_markdown,
    clippy::format_push_string,
    clippy::map_unwrap_or,
    clippy::must_use_candidate,
    clippy::needless_borrows_for_generic_args,
    clippy::needless_pass_by_value,
    clippy::too_many_lines,
    clippy::unnecessary_wraps,
    reason = "Bridge projection favours direct numeric conversions and bounded long functions; all casts are range-checked by the walkmesh, navigation and package budgets."
)]
//! Aurora bundle projection, WOK navigation and deterministic multi-zone visit.

pub mod layout;
pub mod navmesh;
pub mod projection;
pub mod session;
pub mod visit;
pub mod walkmesh;

pub use layout::AreaDocument;
pub use navmesh::{NavigationValidation, validate_forge_navigation};
pub use projection::{
    AURORA_PROJECTION_SCHEMA, NavigationStats, PackageReport, ProjectedAsset, ProjectedDoor,
    ProjectedInstance, ProjectedModule, ProjectedZone, ProjectedZoneSummary, ProjectionError,
    project_module,
};
pub use session::{
    DOOR_INTERACTION_RANGE_M, ModuleSession, PlayerState, SessionEvent, SessionEventKind,
    SessionSnapshot, TRANSITION_RANGE_M, TransitionResult, VisitReport,
};
pub use visit::{render_zone_map_svg, run_scripted_visit};
pub use walkmesh::{WalkSurface, Walkmesh, WalkmeshError, WalkmeshFace};
