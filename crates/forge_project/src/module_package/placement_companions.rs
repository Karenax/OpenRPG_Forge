//! Closed source packages referenced by placements and retained edit history.
use super::{
    AreaId, AssetId, BTreeMap, BTreeSet, ContentHash, Deserialize, MAX_MODULE_FILES,
    MODULE_PLACEMENT_COMPANIONS_API_VERSION, ModuleFile, ModuleFilePin, ModuleFileRole,
    ModulePackageError, ModulePackageManifest, Serialize,
};
use crate::zone_authoring::session::{
    SourceCompanionPin, SourceCompanionRequirements, ZoneEditSession,
};

#[derive(Clone, Copy, Debug, Eq, Ord, PartialEq, PartialOrd, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ModulePlacementCompanionKind {
    Physics,
    Particles,
}

/// A complete package and its exact logical carrier, shared only across declared areas.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ModulePlacementCompanion {
    pub areas: Vec<AreaId>,
    pub kind: ModulePlacementCompanionKind,
    pub asset_id: AssetId,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package: ModuleFilePin,
    /// Embedded private provenance for this precise package and carrier.
    pub proof: ModuleFilePin,
}

impl ModulePlacementCompanion {
    #[must_use]
    pub fn pin(&self) -> SourceCompanionPin {
        SourceCompanionPin {
            asset_id: self.asset_id,
            carrier_sha256: self.carrier_sha256,
            manifest_sha256: self.manifest_sha256,
            package_sha256: self.package.sha256,
        }
    }
}

impl ModulePackageManifest {
    pub(super) fn validate_placement_companions<'a>(
        &'a self,
        files: &BTreeMap<&str, &ModuleFile>,
        used: &mut BTreeSet<&'a str>,
    ) -> Result<(), ModulePackageError> {
        if !self.placement_companions.is_empty()
            && !(MODULE_PLACEMENT_COMPANIONS_API_VERSION
                ..=super::MODULE_HISTORICAL_MODELS_API_VERSION)
                .contains(&self.compatibility.module_api_version)
        {
            return Err(ModulePackageError::Compatibility);
        }
        if self.placement_companions.len() > MAX_MODULE_FILES {
            return Err(ModulePackageError::Limit("placement companions"));
        }
        let mut seen = BTreeSet::new();
        let areas: BTreeSet<_> = self.zones.iter().map(|zone| zone.area_id).collect();
        let assets: BTreeSet<_> = self
            .assets
            .iter()
            .map(|asset| (asset.asset_id, asset.sha256))
            .collect();
        let proofs: BTreeSet<_> = self
            .provenance
            .proof_paths
            .iter()
            .map(String::as_str)
            .collect();
        for companion in &self.placement_companions {
            if !seen.insert((companion.kind, companion.pin())) {
                return Err(ModulePackageError::Duplicate);
            }
            if companion.areas.is_empty()
                || companion.areas.len() > self.zones.len()
                || companion.areas.windows(2).any(|ids| ids[0] >= ids[1])
                || companion.areas.iter().any(|area| !areas.contains(area))
            {
                return Err(ModulePackageError::Reference("companion area closure"));
            }
            if !assets.contains(&(companion.asset_id, companion.carrier_sha256)) {
                return Err(ModulePackageError::Reference("companion carrier asset"));
            }
            let file = files
                .get(companion.package.path.as_str())
                .copied()
                .ok_or(ModulePackageError::Reference("missing companion package"))?;
            if file.role != ModuleFileRole::PlacementCompanion
                || file.sha256 != companion.package.sha256
            {
                return Err(ModulePackageError::Reference("companion file role or pin"));
            }
            let proof = files
                .get(companion.proof.path.as_str())
                .copied()
                .ok_or(ModulePackageError::Reference("missing companion proof"))?;
            if proof.role != ModuleFileRole::Provenance
                || proof.sha256 != companion.proof.sha256
                || !proofs.contains(companion.proof.path.as_str())
            {
                return Err(ModulePackageError::Reference("companion proof role or pin"));
            }
            used.insert(companion.package.path.as_str());
        }
        Ok(())
    }

    pub(super) fn validate_zone_placement_companions(
        &self,
        zone: &ZoneEditSession,
        area: AreaId,
    ) -> Result<(), ModulePackageError> {
        let required = zone
            .required_source_companions()
            .map_err(ModulePackageError::Archive)?;
        if self.compatibility.module_api_version < MODULE_PLACEMENT_COMPANIONS_API_VERSION
            && (!required.physics.is_empty() || !required.particles.is_empty())
        {
            return Err(ModulePackageError::Archive(
                "physics and particle companion packages are not supported by this module API"
                    .into(),
            ));
        }
        let mut declared = SourceCompanionRequirements::default();
        for companion in self
            .placement_companions
            .iter()
            .filter(|companion| companion.areas.binary_search(&area).is_ok())
        {
            match companion.kind {
                ModulePlacementCompanionKind::Physics => &mut declared.physics,
                ModulePlacementCompanionKind::Particles => &mut declared.particles,
            }
            .insert(companion.pin());
        }
        if required != declared {
            return Err(ModulePackageError::Reference(
                "zone companion package closure differs",
            ));
        }
        Ok(())
    }
}
