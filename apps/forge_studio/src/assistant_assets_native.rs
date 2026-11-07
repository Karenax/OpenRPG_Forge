//! Search the existing library and resolve actual pins before sharing with AI.
use crate::{
    asset_library_native::Library,
    asset_library_protocol::{
        LibraryDetail, LibraryQuery, LibraryRequest, LibraryResponse, vault_asset_id,
    },
    assistant_asset_protocol::{AssetQuery, AssetSearchEntry, AssetSearchPage, AssetSelection},
};
use forge_core::ContentHash;
use forge_project::assistant::assets::{AssetCatalog, AssetReference, MAX_SHARED_ASSETS};
use std::collections::BTreeSet;

const MAX_VERIFICATION_BYTES: u64 = 512 * 1024 * 1024;
const VERIFICATION_LIMIT: &str =
    "La sélection dépasse 512 Mio de modèles et de paquets ; choisir moins de références.";

struct VerificationBudget {
    remaining: u64,
}
impl VerificationBudget {
    fn charge(&mut self, bytes: usize) -> Result<(), String> {
        let bytes = u64::try_from(bytes).map_err(|_| VERIFICATION_LIMIT)?;
        self.remaining = self
            .remaining
            .checked_sub(bytes)
            .ok_or(VERIFICATION_LIMIT)?;
        Ok(())
    }
}

pub struct BoundCatalog {
    selection: AssetSelection,
    catalog: AssetCatalog,
}
impl BoundCatalog {
    pub fn catalog_for(&self, selection: &AssetSelection) -> Result<&AssetCatalog, String> {
        if &self.selection != selection {
            return Err("Asset selection differs from the native verification".into());
        }
        Ok(&self.catalog)
    }
}

pub fn search(library: &mut Library, query: &AssetQuery) -> Result<AssetSearchPage, String> {
    if query.text.len() > 256 || query.pack.len() > 256 || query.offset > 100_000 {
        return Err("Recherche trop longue ; préciser les critères.".into());
    }
    let LibraryResponse::Page { page } = library.execute(LibraryRequest::Search {
        query: LibraryQuery {
            text: query.text.clone(),
            pack: query.pack.clone(),
            offset: query.offset,
            limit: 12,
            ..LibraryQuery::default()
        },
    })?
    else {
        return Err("Réponse de bibliothèque inattendue.".into());
    };
    let revision = ContentHash::parse(&page.revision).map_err(|error| error.to_string())?;
    let mut assets = Vec::new();
    for card in page.assets {
        let detail = details(library, &card.id, revision)?;
        assets.push(AssetSearchEntry {
            library_id: card.id,
            asset: describe(&detail)?,
        });
    }
    if !assets.is_empty() {
        AssetCatalog::new(
            revision,
            assets.iter().map(|entry| entry.asset.clone()).collect(),
        )?;
    }
    Ok(AssetSearchPage {
        revision,
        matched: page.matched_assets,
        offset: page.offset,
        assets,
    })
}

pub fn resolve(library: &mut Library, selection: &AssetSelection) -> Result<BoundCatalog, String> {
    if selection.ids.is_empty()
        || selection.ids.len() > MAX_SHARED_ASSETS
        || selection.ids.iter().collect::<BTreeSet<_>>().len() != selection.ids.len()
    {
        return Err("Choisir entre un et seize assets distincts.".into());
    }
    let mut assets = Vec::new();
    let mut budget = VerificationBudget {
        remaining: MAX_VERIFICATION_BYTES,
    };
    for id in &selection.ids {
        let detail = details(library, id, selection.revision)?;
        let mut asset = describe(&detail)?;
        if asset.model_available() && asset.metadata_ready && !asset.license.trim().is_empty() {
            let metadata = std::fs::metadata(
                detail
                    .model_path
                    .as_ref()
                    .ok_or("Référence de modèle absente")?,
            )
            .map_err(|_| "Modèle inaccessible ; relancer la recherche.")?;
            if !metadata.is_file() || metadata.len() > budget.remaining {
                return Err(VERIFICATION_LIMIT.into());
            }
            let LibraryResponse::Model { model } = library.execute(LibraryRequest::Model {
                id: id.clone(),
                revision: selection.revision.to_string(),
            })?
            else {
                return Err("Réponse de modèle inattendue.".into());
            };
            budget.charge(model.bytes.len())?;
            if model.asset_id != asset.asset_id || Some(model.sha256) != asset.model_hash {
                return Err("Le modèle a changé ; relancer la recherche.".into());
            }
            asset.model_verified = true;
            if selection.allow_placement {
                resolve_packages(
                    library,
                    id,
                    selection.revision,
                    &detail.profile,
                    &mut asset,
                    &mut budget,
                )?;
            }
        }
        assets.push(asset);
    }
    let mut catalog = AssetCatalog::new(selection.revision, assets)?;
    if selection.allow_placement {
        catalog = catalog.authorize_placement()?;
    }
    Ok(BoundCatalog {
        selection: selection.clone(),
        catalog,
    })
}

fn details(
    library: &mut Library,
    id: &str,
    revision: ContentHash,
) -> Result<Box<LibraryDetail>, String> {
    let LibraryResponse::Details { detail } = library
        .execute(LibraryRequest::Details {
            id: id.into(),
            revision: revision.to_string(),
        })
        .map_err(|error| {
            if error == "révision périmée ou identifiant invalide" {
                "La bibliothèque a changé ou une référence n’est plus disponible. Relancer la recherche et choisir à nouveau les références avant de préparer l’envoi.".to_owned()
            } else {
                error
            }
        })?
    else {
        return Err("Réponse de fiche inattendue.".into());
    };
    Ok(detail)
}

fn describe(detail: &LibraryDetail) -> Result<AssetReference, String> {
    let card = &detail.card;
    let available = card.availability == "converted_model" && detail.model_path.is_some();
    let hash = if available {
        Some(
            ContentHash::parse(
                detail.details["model_sha256"]
                    .as_str()
                    .ok_or("Empreinte de modèle absente")?,
            )
            .map_err(|error| error.to_string())?,
        )
    } else {
        None
    };
    Ok(AssetReference {
        asset_id: vault_asset_id(&card.id),
        model_hash: hash,
        physics: None,
        particles: None,
        name: card.name.clone(),
        pack: card.pack.clone(),
        family: card.family.clone(),
        asset_type: card.asset_type.clone(),
        license: detail.license.clone(),
        dimensions_m: card.dimensions,
        triangles: card.triangles,
        metadata_ready: card.metadata_ready,
        runtime_qualified: card.runtime_qualified,
        model_verified: false,
    })
}

fn resolve_packages(
    library: &mut Library,
    id: &str,
    revision: ContentHash,
    profile: &serde_json::Value,
    asset: &mut AssetReference,
    budget: &mut VerificationBudget,
) -> Result<(), String> {
    use crate::asset_library_protocol::{LibraryParticleRequest, LibraryPhysicsRequest};
    use forge_project::zone_authoring::placements::{
        ParticlePackageReference, PhysicsPackageReference,
    };
    let pins = |declaration: &serde_json::Value| -> Result<_, String> {
        let pin = |key: &str| {
            ContentHash::parse(
                declaration[key]
                    .as_str()
                    .ok_or("Empreinte de paquet absente")?,
            )
            .map_err(|error| error.to_string())
        };
        let carrier = pin("carrier_sha256")?;
        if Some(carrier) != asset.model_hash {
            return Err("Le paquet ne correspond pas au modèle sélectionné.".into());
        }
        Ok((carrier, pin("manifest_sha256")?, pin("sha256")?))
    };
    if let Some(declaration) = profile.get("source_physics_package") {
        let (carrier_sha256, manifest_sha256, package_sha256) = pins(declaration)?;
        let wire = library.read_physics_package(LibraryPhysicsRequest {
            schema_version: 1,
            id: id.into(),
            revision: revision.to_string(),
            carrier_sha256,
            manifest_sha256,
        })?;
        budget.charge(wire.len())?;
        if forge_core::bytes_sha256(&wire) != package_sha256 {
            return Err("Le paquet physique a changé.".into());
        }
        asset.physics = Some(Box::new(PhysicsPackageReference {
            schema_version: 1,
            carrier_sha256,
            manifest_sha256,
            package_sha256,
        }));
    }
    if let Some(declaration) = profile.get("source_particle_package") {
        let (carrier_sha256, manifest_sha256, package_sha256) = pins(declaration)?;
        let wire = library.read_particle_package(LibraryParticleRequest {
            schema_version: 1,
            id: id.into(),
            revision: revision.to_string(),
            carrier_sha256,
            manifest_sha256,
            package_sha256,
        })?;
        budget.charge(wire.len())?;
        asset.particles = Some(Box::new(ParticlePackageReference {
            schema_version: 1,
            carrier_sha256,
            manifest_sha256,
            package_sha256,
        }));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::{MAX_VERIFICATION_BYTES, VerificationBudget};

    #[test]
    fn asset_verification_budget_includes_models_and_both_package_kinds() {
        let mut budget = VerificationBudget {
            remaining: MAX_VERIFICATION_BYTES,
        };
        for bytes in [64 * 1024 * 1024, 32 * 1024 * 1024, 16 * 1024 * 1024] {
            budget.charge(bytes).unwrap();
        }
        assert_eq!(budget.remaining, 400 * 1024 * 1024);
        assert!(budget.charge(400 * 1024 * 1024 + 1).is_err());
        assert_eq!(budget.remaining, 400 * 1024 * 1024);
        budget.charge(400 * 1024 * 1024).unwrap();
        assert!(budget.charge(1).is_err());
        assert_eq!(budget.remaining, 0);
    }
}
