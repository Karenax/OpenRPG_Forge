//! Pure proposal preparation; never a second authoring session or a filesystem writer.
//! Acceptance must go through the zone's regeneration review and command journal.
use crate::{WorldSketchDocument, WorldSketchSpline};
use forge_core::ContentHash;
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "snake_case", deny_unknown_fields)]
pub enum SplineEdit {
    Insert { spline: WorldSketchSpline },
    Replace { spline: WorldSketchSpline },
    Remove { id: String },
}

/// Immutable preview with the exact source fingerprint and old/new feature values.
#[derive(Clone, Debug)]
pub struct SplineEditPreview {
    source_hash: ContentHash,
    before: Option<WorldSketchSpline>,
    after: Option<WorldSketchSpline>,
    proposal: WorldSketchDocument,
}

impl SplineEditPreview {
    pub fn prepare(
        source: &WorldSketchDocument,
        expected_hash: ContentHash,
        edit: SplineEdit,
    ) -> Result<Self, String> {
        let source_hash = source.content_hash().map_err(|e| e.to_string())?;
        if source_hash != expected_hash {
            return Err("Stale spline edit source".into());
        }
        let id = match &edit {
            SplineEdit::Insert { spline } | SplineEdit::Replace { spline } => &spline.id,
            SplineEdit::Remove { id } => id,
        };
        if source.locked_feature_ids.contains(id) {
            return Err(format!("Spline feature is locked: {id}"));
        }
        let index = source.splines.iter().position(|s| &s.id == id);
        let before = index.map(|i| source.splines[i].clone());
        let mut proposal = source.clone();
        let after = match edit {
            SplineEdit::Insert { spline } => {
                if index.is_some() {
                    return Err("Spline already exists; use Replace explicitly".into());
                }
                proposal.splines.push(spline.clone());
                Some(spline)
            }
            SplineEdit::Replace { spline } => {
                let index = index.ok_or("Spline to replace does not exist")?;
                if proposal.splines[index] == spline {
                    return Err("Spline edit has no change".into());
                }
                proposal.splines[index] = spline.clone();
                Some(spline)
            }
            SplineEdit::Remove { .. } => {
                let index = index.ok_or("Spline to remove does not exist")?;
                proposal.splines.remove(index);
                None
            }
        };
        // Validate all references, bounds and profiles, not just the edited coordinates.
        proposal.validate().map_err(|e| e.to_string())?;
        Ok(Self {
            source_hash,
            before,
            after,
            proposal,
        })
    }

    #[must_use]
    pub const fn source_hash(&self) -> ContentHash {
        self.source_hash
    }

    #[must_use]
    pub fn before(&self) -> Option<&WorldSketchSpline> {
        self.before.as_ref()
    }

    #[must_use]
    pub fn after(&self) -> Option<&WorldSketchSpline> {
        self.after.as_ref()
    }

    /// No revision is published here. The accepted zone command owns revision advancement.
    #[must_use]
    pub const fn proposal(&self) -> &WorldSketchDocument {
        &self.proposal
    }
}
