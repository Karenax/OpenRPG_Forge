//! Read an explicitly registered physics package; no client path or implicit
//! transport discovery. Registration does not qualify simulation or rendering.
use super::{Library, LibraryRequest, LibraryResponse, read_limited, required, same_hash};
use crate::asset_library_protocol::{LibraryPhysicsRequest, LibrarySavedPhysicsRequest};
use forge_assets::source_physics_package::{MAX_PACKAGE_BYTES, SourcePhysicsPackage};
use std::collections::BTreeSet;

pub(super) fn matches_physics(
    asset: &super::StoredAsset,
    carrier: forge_core::ContentHash,
    manifest: forge_core::ContentHash,
    wire: forge_core::ContentHash,
) -> bool {
    let declared = &asset.profile["source_physics_package"];
    [
        ("carrier_sha256", carrier),
        ("manifest_sha256", manifest),
        ("sha256", wire),
    ]
    .into_iter()
    .all(|(key, expected)| {
        declared[key]
            .as_str()
            .and_then(|s| forge_core::ContentHash::parse(s).ok())
            == Some(expected)
    }) && asset.details["model_sha256"]
        .as_str()
        .and_then(|s| forge_core::ContentHash::parse(s).ok())
        == Some(carrier)
}

impl Library {
    pub fn read_saved_physics_package(
        &mut self,
        request: &LibrarySavedPhysicsRequest,
    ) -> Result<Vec<u8>, String> {
        if request.schema_version != 1 {
            return Err("Version de référence physique invalide".into());
        }
        // Refresh and verify the same metadata snapshot as ordinary library reads.
        self.execute(LibraryRequest::Search {
            query: super::LibraryQuery {
                limit: 1,
                ..Default::default()
            },
        })?;
        let result = (|| {
            let snapshot = self.snapshot.as_ref().ok_or("Snapshot physique absent")?;
            let mut matches = snapshot
                .assets
                .iter()
                .filter(|asset| super::vault_asset_id(&asset.card.id) == request.asset_id);
            let current = matches.next().ok_or("Référence physique inconnue")?;
            if matches.next().is_some() || current.card.availability != "converted_model" {
                return Err("Référence physique ambiguë ou non plaçable".into());
            }
            let asset = if matches_physics(
                current,
                request.carrier_sha256,
                request.manifest_sha256,
                request.package_sha256,
            ) {
                current.clone()
            } else {
                self.read_historical_asset(
                    snapshot,
                    current,
                    request.carrier_sha256,
                    Some((request.manifest_sha256, request.package_sha256)),
                )?
            };
            let declared = &asset.profile["source_physics_package"];
            let path = self.safe_from(&self.root, required(declared, "path")?)?;
            let wire = read_limited(
                &path,
                u64::try_from(MAX_PACKAGE_BYTES).map_err(|_| "Budget physique invalide")?,
            )?;
            if forge_core::bytes_sha256(&wire) != request.package_sha256 {
                return Err("Empreinte du paquet sauvegardé différente".into());
            }
            let model = self.read_model(&asset)?;
            validate_transport(
                &wire,
                request.carrier_sha256,
                request.manifest_sha256,
                &model,
            )?;
            Ok(wire)
        })();
        let snapshot = self.snapshot.as_ref().ok_or("Snapshot physique absent")?;
        if let Err(error) = self.verify_registry_pins(
            &snapshot.registry_raw,
            snapshot.source_support_registry_raw.as_deref(),
        ) {
            self.snapshot = None;
            return Err(error);
        }
        result
    }

    pub fn read_physics_package(
        &mut self,
        request: LibraryPhysicsRequest,
    ) -> Result<Vec<u8>, String> {
        if request.schema_version != 1 {
            return Err("Version de requête physique invalide".into());
        }
        let LibraryResponse::Details { detail } = self.execute(LibraryRequest::Details {
            id: request.id.clone(),
            revision: request.revision.clone(),
        })?
        else {
            return Err("Fiche physique inattendue".into());
        };
        if detail.card.availability != "converted_model" {
            return Err("Source conservée non plaçable".into());
        }
        let declared = detail
            .profile
            .get("source_physics_package")
            .ok_or("Paquet physique non enregistré")?;
        let carrier = forge_core::ContentHash::parse(required(declared, "carrier_sha256")?)
            .map_err(|e| e.to_string())?;
        let manifest = forge_core::ContentHash::parse(required(declared, "manifest_sha256")?)
            .map_err(|e| e.to_string())?;
        if carrier != request.carrier_sha256
            || manifest != request.manifest_sha256
            || detail.details["model_sha256"].as_str() != declared["carrier_sha256"].as_str()
        {
            return Err("Pins physiques différents de la fiche".into());
        }
        let path = self.safe_from(&self.root, required(declared, "path")?)?;
        let wire = read_limited(
            &path,
            u64::try_from(MAX_PACKAGE_BYTES).map_err(|_| "Budget physique invalide")?,
        )?;
        if !same_hash(&wire, required(declared, "sha256")?) {
            return Err("Empreinte du paquet physique différente".into());
        }
        let LibraryResponse::Model { model } = self.execute(LibraryRequest::Model {
            id: request.id,
            revision: request.revision,
        })?
        else {
            return Err("Modèle physique inattendu".into());
        };
        validate_transport(&wire, carrier, manifest, &model)?;
        Ok(wire)
    }
}

fn validate_transport(
    wire: &[u8],
    carrier: forge_core::ContentHash,
    manifest: forge_core::ContentHash,
    model: &super::LibraryModel,
) -> Result<(), String> {
    let package = SourcePhysicsPackage::decode(wire, carrier, manifest)?;
    if model.sha256 != carrier {
        return Err("Carrier physique différent du modèle".into());
    }
    let glb = forge_assets::source_graph_glb::read_source_graph_glb_with_budget(
        &model.bytes,
        forge_assets::source_graph_glb::SourceGraphGlbReadBudget::LargeDesktop,
    )?
    .ok_or("Carrier physique sans déclaration source")?;
    let mut used = BTreeSet::new();
    forge_assets::source_physics_transport::read_source_physics_transport(
        &package.manifest,
        manifest,
        carrier,
        &glb,
        |path| {
            used.insert(path.to_owned());
            package
                .candidates
                .get(path)
                .cloned()
                .ok_or("Buffer physique absent du paquet".into())
        },
    )?;
    if used.len() != package.candidates.len() {
        return Err("Buffer physique sans propriétaire".into());
    }
    Ok(())
}
