//! Resolve installed species metadata from the closed module inventory, without Bevy or a Vault.
use crate::Result;
use forge_assets::{
    GovernedAssetKitBuildReport, VegetationPrototypeCatalog, VegetationPrototypeSelection,
    asset_browser::AssetBrowser,
    governed_import::{GovernedAssetKind, GovernedImportCatalog},
};
use forge_core::canonical_json_sha256;
use forge_project::{
    VegetationKind,
    module_package::{
        ModuleFilePin, ModulePackageManifest, ModuleRights, ModuleVegetationBinding,
        ModuleVegetationSource,
    },
    zone_authoring::ZoneAuthoringHead,
};
use std::collections::{BTreeMap, BTreeSet};

const MAX_DOCUMENT_BYTES: usize = 1024 * 1024;
mod imported;
pub use imported::{ResolvedImportedPrototype, ResolvedModuleImported, resolve_imported};

pub struct ResolvedModuleSpecies {
    pub catalog: VegetationPrototypeCatalog,
    pub asset_kit: GovernedAssetKitBuildReport,
    /// Logical kit asset IDs map to exact installed module files, never original kit paths.
    pub assets: BTreeMap<String, ModuleFilePin>,
}

/// Metadata admission only. The renderer must still load GLBs, project all LODs/collisions
/// and observe GPU resources. Imported resources are handled by `resolve_imported`.
pub fn resolve_species(
    module: &ModulePackageManifest,
    head: &ZoneAuthoringHead,
    governed: &GovernedImportCatalog,
    read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedModuleSpecies>> {
    let binding = authored_binding(module, head)?;
    resolve_species_binding(module, head, governed, binding, read)
}

fn resolve_species_binding(
    module: &ModulePackageManifest,
    head: &ZoneAuthoringHead,
    governed: &GovernedImportCatalog,
    binding: Option<&ModuleVegetationBinding>,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Option<ResolvedModuleSpecies>> {
    let (_, vegetation) = head.effective()?;
    let selections = head.corrections.species.as_deref().unwrap_or_default();
    if selections.is_empty() {
        return Ok(None);
    }
    let vegetation = vegetation.ok_or("Authored species require vegetation")?;
    let target = vegetation
        .content_hash()
        .map_err(|error| error.to_string())?;
    let binding = binding.ok_or("Authored species catalog is absent from the module")?;
    if binding.vegetation_sha256 != target {
        return Err("Species companions differ from the authored vegetation".into());
    }
    let (catalogs, asset_kit) = binding
        .sources
        .iter()
        .find_map(|source| match source {
            ModuleVegetationSource::GovernedSpecies {
                catalogs,
                asset_kit,
            } => Some((catalogs, asset_kit)),
            ModuleVegetationSource::ImportedPrototypes { .. } => None,
        })
        .ok_or("Authored species catalog is absent from the module")?;
    let kit: GovernedAssetKitBuildReport =
        serde_json::from_slice(&read_document(module, asset_kit, &mut read)?)
            .map_err(|error| error.to_string())?;
    kit.validate().map_err(|error| error.to_string())?;
    let mut sources = Vec::with_capacity(catalogs.len());
    let mut observed = BTreeSet::new();
    for pin in catalogs {
        let source: VegetationPrototypeCatalog =
            serde_json::from_slice(&read_document(module, pin, &mut read)?)
                .map_err(|error| error.to_string())?;
        if canonical_json_sha256(&source).map_err(|error| error.to_string())? != pin.sha256
            || !observed.insert(pin.sha256)
        {
            return Err("Species source catalog differs from its authored canonical pin".into());
        }
        sources.push(source);
    }
    let selected: Vec<_> = selections
        .iter()
        .flat_map(|layer| {
            layer.prototypes.iter().map(|reference| {
                (
                    reference.catalog_sha256,
                    VegetationPrototypeSelection {
                        source_layer_id: reference.source_layer_id.as_str().into(),
                        target_layer_id: layer.layer_id.as_str().into(),
                        asset_id: reference.asset_id.clone(),
                    },
                )
            })
        })
        .collect();
    let expected: BTreeSet<_> = selected.iter().map(|(hash, _)| *hash).collect();
    if observed != expected {
        return Err("Species catalog closure differs from canonical author selections".into());
    }
    let tree_layers: Vec<_> = vegetation
        .layers
        .iter()
        .filter(|layer| layer.kind == VegetationKind::Tree)
        .map(|layer| layer.id.as_str())
        .collect();
    let catalog = forge_assets::resolve_pinned_vegetation_catalogs(
        &sources,
        target,
        &tree_layers,
        &selected,
        &kit,
    )
    .map_err(|error| error.to_string())?;
    let assets = resolve_assets(module, governed, &catalog, &kit)?;
    Ok(Some(ResolvedModuleSpecies {
        catalog,
        asset_kit: kit,
        assets,
    }))
}

fn read_document(
    module: &ModulePackageManifest,
    pin: &ModuleFilePin,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    read_pinned(module, pin, MAX_DOCUMENT_BYTES, read)
}

fn read_pinned(
    module: &ModulePackageManifest,
    pin: &ModuleFilePin,
    maximum: usize,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<Vec<u8>> {
    let file = module
        .files
        .iter()
        .find(|file| file.path == pin.path)
        .ok_or("Species document is absent from inventory")?;
    if file.bytes > maximum as u64 || file.sha256 != pin.sha256 {
        return Err("Species document exceeds its budget or pin".into());
    }
    let bytes = read(&pin.path, maximum)?;
    if bytes.len() as u64 != file.bytes || forge_core::bytes_sha256(&bytes) != pin.sha256 {
        return Err("Species document bytes differ from the declared inventory".into());
    }
    Ok(bytes)
}

fn authored_binding<'a>(
    module: &'a ModulePackageManifest,
    head: &ZoneAuthoringHead,
) -> Result<Option<&'a forge_project::module_package::ModuleVegetationBinding>> {
    module.validate().map_err(|error| error.to_string())?;
    head.validate()?;
    let area = head.base.terrain.area_id;
    let head_hash = head.content_hash()?;
    if head.base.terrain.project_id != module.project_id
        || !module
            .zones
            .iter()
            .any(|zone| zone.area_id == area && zone.head_hash == head_hash)
    {
        return Err("Vegetation resolution requires the module's exact authored head".into());
    }
    let binding = module
        .vegetation
        .iter()
        .find(|binding| binding.area_id == area);
    check_binding(head, binding)?;
    Ok(binding)
}

fn check_binding(
    head: &ZoneAuthoringHead,
    binding: Option<&ModuleVegetationBinding>,
) -> Result<()> {
    let (_, vegetation) = head.effective()?;
    let imported = vegetation
        .as_ref()
        .is_some_and(|value| value.imported_source.is_some());
    let species = head
        .corrections
        .species
        .as_ref()
        .is_some_and(|value| !value.is_empty());
    let supplied_imported = binding.is_some_and(|binding| {
        binding
            .sources
            .iter()
            .any(|source| matches!(source, ModuleVegetationSource::ImportedPrototypes { .. }))
    });
    let supplied_species = binding.is_some_and(|binding| {
        binding
            .sources
            .iter()
            .any(|source| matches!(source, ModuleVegetationSource::GovernedSpecies { .. }))
    });
    if imported != supplied_imported || species != supplied_species {
        return Err("Module vegetation families differ from the exact authored head".into());
    }
    if let Some(binding) = binding {
        let vegetation = vegetation.ok_or("Unreferenced module vegetation binding")?;
        if vegetation
            .content_hash()
            .map_err(|error| error.to_string())?
            != binding.vegetation_sha256
        {
            return Err("Module vegetation companions differ from the authored document".into());
        }
    }
    Ok(())
}

/// Current authoring projection over unchanged admitted origin resources.
/// Installed admission still requires its exact archive head; no historical
/// manifest, archive pin or rights document is rewritten for an authoring edit.
pub struct ResolvedAuthoringVegetation {
    pub binding: Option<ModuleVegetationBinding>,
    pub imported: Option<ResolvedModuleImported>,
    pub species: Option<ResolvedModuleSpecies>,
}

pub fn restore_authoring(
    module: &ModulePackageManifest,
    origin_head: &ZoneAuthoringHead,
    head: &ZoneAuthoringHead,
    governed: &GovernedImportCatalog,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<ResolvedAuthoringVegetation> {
    let original = authored_binding(module, origin_head)?;
    head.validate()?;
    if head.base.terrain.area_id != origin_head.base.terrain.area_id
        || head.base.terrain.project_id != module.project_id
        || head
            .corrections
            .gameplay
            .as_deref()
            .is_some_and(|gameplay| gameplay.module_id != module.module_id)
    {
        return Err("Authoring vegetation differs from its original module authority".into());
    }
    let (_, vegetation) = head.effective()?;
    let imported = vegetation
        .as_ref()
        .is_some_and(|value| value.imported_source.is_some());
    let species = head
        .corrections
        .species
        .as_ref()
        .is_some_and(|value| !value.is_empty());
    let mut binding = if imported || species {
        original.cloned()
    } else {
        None
    };
    if let Some(binding) = &mut binding {
        binding.vegetation_sha256 = vegetation
            .as_ref()
            .ok_or("Authoring vegetation absent")?
            .content_hash()
            .map_err(|error| error.to_string())?;
        binding.sources.retain(|source| match source {
            ModuleVegetationSource::ImportedPrototypes { .. } => imported,
            ModuleVegetationSource::GovernedSpecies { .. } => species,
        });
    }
    check_binding(head, binding.as_ref())?;
    let imported = imported::resolve_binding(module, head, binding.as_ref(), &mut read)?;
    let species = resolve_species_binding(module, head, governed, binding.as_ref(), read)?;
    Ok(ResolvedAuthoringVegetation {
        binding,
        imported,
        species,
    })
}

/// Complete companion admission, separate from native renderer availability.
pub fn validate_companions(
    module: &ModulePackageManifest,
    head: &ZoneAuthoringHead,
    governed: &GovernedImportCatalog,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>>,
) -> Result<()> {
    resolve_species(module, head, governed, &mut read)?;
    resolve_imported(module, head, read)?;
    Ok(())
}

fn resolve_assets(
    module: &ModulePackageManifest,
    governed: &GovernedImportCatalog,
    catalog: &VegetationPrototypeCatalog,
    kit: &GovernedAssetKitBuildReport,
) -> Result<BTreeMap<String, ModuleFilePin>> {
    if module.provenance.rights == ModuleRights::Redistributable {
        governed.validate_for_publication()
    } else {
        governed.validate_for_local_use()
    }
    .map_err(|error| error.to_string())?;
    let browser = AssetBrowser::new(kit.clone()).map_err(|error| error.to_string())?;
    let mut result = BTreeMap::new();
    for binding in &catalog.bindings {
        let (id, hash) = browser
            .reference(&binding.asset_id)
            .map_err(|error| error.to_string())?;
        let declared = module
            .assets
            .iter()
            .find(|asset| asset.asset_id == id && asset.sha256 == hash)
            .ok_or("Species mesh has no exact module asset identity/pin")?;
        if !governed.assets.iter().any(|asset| {
            asset.id == id
                && asset.kind == GovernedAssetKind::Mesh
                && asset.payload.sha256 == hash
                && asset.payload.path == declared.file_path
        }) {
            return Err("Species mesh has no exact governed import provenance".into());
        }
        result.insert(
            binding.asset_id.clone(),
            ModuleFilePin {
                path: declared.file_path.clone(),
                sha256: hash,
            },
        );
    }
    Ok(result)
}
