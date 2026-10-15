//! Read-only metadata-governed particle resource transport.
use super::{Library, LibraryRequest, LibraryResponse, read_limited, required};
use crate::asset_library_protocol::{LibraryParticleRequest, LibrarySavedParticleRequest};
use forge_assets::source_particle_package::{MAX_PACKAGE_BYTES, SourceParticlePackage};
use forge_core::ContentHash;

impl Library {
    pub fn read_saved_particle_package(
        &mut self,
        request: &LibrarySavedParticleRequest,
    ) -> Result<Vec<u8>, String> {
        if request.schema_version != 1 {
            return Err("Version de référence d’effet invalide".into());
        }
        self.execute(LibraryRequest::Search {
            query: super::LibraryQuery {
                limit: 1,
                ..Default::default()
            },
        })?;
        let result = (|| {
            let snapshot = self.snapshot.as_ref().ok_or("Snapshot d’effet absent")?;
            let mut identities = snapshot
                .assets
                .iter()
                .filter(|asset| super::vault_asset_id(&asset.card.id) == request.asset_id);
            let current = identities.next().ok_or("Référence d’effet inconnue")?;
            if identities.next().is_some() || current.card.availability != "converted_model" {
                return Err("Référence d’effet ambiguë ou non plaçable".into());
            }
            let matches = |asset: &super::StoredAsset| {
                let declaration = &asset.profile["source_particle_package"];
                [
                    ("carrier_sha256", request.carrier_sha256),
                    ("manifest_sha256", request.manifest_sha256),
                    ("sha256", request.package_sha256),
                ]
                .into_iter()
                .all(|(key, expected)| {
                    declaration[key]
                        .as_str()
                        .and_then(|s| ContentHash::parse(s).ok())
                        == Some(expected)
                }) && asset.details["model_sha256"]
                    .as_str()
                    .and_then(|s| ContentHash::parse(s).ok())
                    == Some(request.carrier_sha256)
            };
            let asset = if matches(current) {
                current.clone()
            } else {
                self.read_historical_asset_matching(
                    snapshot,
                    current,
                    request.carrier_sha256,
                    matches,
                )?
            };
            let declaration = &asset.profile["source_particle_package"];
            let path = self.safe_from(&self.root, required(declaration, "path")?)?;
            let wire = read_limited(
                &path,
                u64::try_from(MAX_PACKAGE_BYTES).map_err(|_| "Budget d’effet invalide")?,
            )?;
            let package = SourceParticlePackage::decode_pinned(
                &wire,
                request.carrier_sha256,
                request.manifest_sha256,
                request.package_sha256,
            )?;
            let model = self.read_model(&asset)?;
            package.resolve_for_carrier(&model.bytes)?;
            Ok(wire)
        })();
        let snapshot = self.snapshot.as_ref().ok_or("Snapshot d’effet absent")?;
        if let Err(error) = self.verify_registry_pins(
            &snapshot.registry_raw,
            snapshot.source_support_registry_raw.as_deref(),
        ) {
            self.snapshot = None;
            return Err(error);
        }
        result
    }
    pub fn read_particle_package(
        &mut self,
        request: LibraryParticleRequest,
    ) -> Result<Vec<u8>, String> {
        if request.schema_version != 1 {
            return Err("Version de requête d’effet invalide".into());
        }
        let LibraryResponse::Details { detail } = self.execute(LibraryRequest::Details {
            id: request.id.clone(),
            revision: request.revision.clone(),
        })?
        else {
            return Err("Fiche d’effet inattendue".into());
        };
        if detail.card.availability != "converted_model" {
            return Err("Source conservée non plaçable".into());
        }
        let declaration = detail
            .profile
            .get("source_particle_package")
            .ok_or("Paquet d’effet non enregistré")?;
        for (name, expected) in [
            ("carrier_sha256", request.carrier_sha256),
            ("manifest_sha256", request.manifest_sha256),
            ("sha256", request.package_sha256),
        ] {
            let declared =
                ContentHash::parse(required(declaration, name)?).map_err(|e| e.to_string())?;
            if declared != expected {
                return Err("Empreintes d’effet différentes de la fiche".into());
            }
        }
        if detail.details["model_sha256"].as_str() != declaration["carrier_sha256"].as_str() {
            return Err("Porteur d’effet différent du modèle".into());
        }
        let path = self.safe_from(&self.root, required(declaration, "path")?)?;
        let wire = read_limited(
            &path,
            u64::try_from(MAX_PACKAGE_BYTES).map_err(|_| "Budget d’effet invalide")?,
        )?;
        let package = SourceParticlePackage::decode_pinned(
            &wire,
            request.carrier_sha256,
            request.manifest_sha256,
            request.package_sha256,
        )?;
        let LibraryResponse::Model { model } = self.execute(LibraryRequest::Model {
            id: request.id,
            revision: request.revision,
        })?
        else {
            return Err("Modèle d’effet inattendu".into());
        };
        if model.sha256 != request.carrier_sha256 {
            return Err("Modèle d’effet remplacé".into());
        }
        package.resolve_for_carrier(&model.bytes)?;
        Ok(wire)
    }
}
