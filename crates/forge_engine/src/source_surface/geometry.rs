//! Scene-owned immutable geometry keyed by exact asset revision.
use bevy::prelude::*;
use forge_core::{AssetId, ContentHash};
use std::collections::BTreeMap;
pub struct MeshPart {
    pub mesh: Mesh,
    pub material: String,
}

/// Shared geometry for placement roots; cloning a part only clones its handle.
#[derive(Clone)]
pub struct ResidentPart {
    pub mesh: Handle<Mesh>,
    pub material: String,
}

/// A package-scoped cache with exact revision lookup. Roots retain their own
/// canonical placement identity and transform; geometry stays shared.
#[derive(Resource)]
pub struct ResidentMeshes {
    parts: BTreeMap<(AssetId, ContentHash), Vec<ResidentPart>>,
}

/// Validated CPU geometry, ready for adoption by an existing renderer.
/// Preparing a package never allocates handles or mutates the current scene.
pub struct PreparedMeshes {
    parts: BTreeMap<(AssetId, ContentHash), Vec<MeshPart>>,
}

impl PreparedMeshes {
    /// The acquisition adapter supplies admitted parts, keyed by exact asset revision.
    /// Placement preparation resolves every material and validates collision surfaces
    /// before touching the current scene. No module identity or package is selected here.
    #[must_use]
    pub fn from_parts(parts: BTreeMap<(AssetId, ContentHash), Vec<MeshPart>>) -> Self {
        Self { parts }
    }

    /// Adoption is infallible: every part and material reference was validated.
    pub fn stage(self, assets: &mut Assets<Mesh>) -> ResidentMeshes {
        let parts = self
            .parts
            .into_iter()
            .map(|(key, parts)| {
                let resident = parts
                    .into_iter()
                    .map(|part| ResidentPart {
                        mesh: assets.add(part.mesh),
                        material: part.material,
                    })
                    .collect();
                (key, resident)
            })
            .collect();
        ResidentMeshes { parts }
    }
}

impl ResidentMeshes {
    pub fn resolve(&self, id: AssetId, hash: ContentHash) -> Result<&[ResidentPart], String> {
        self.parts
            .get(&(id, hash))
            .map(Vec::as_slice)
            .ok_or_else(|| "Building geometry revision is not resident".into())
    }
}
