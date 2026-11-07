//! Assemble the exact placement packages and private proofs required by each area.
use super::*;
use forge_project::zone_authoring::session::SourceCompanionPin;

const PREFIX: &str = "_forge-export/placement-companions/";

fn requirements(
    zone: &ZoneEditSession,
) -> Result<Vec<(ModulePlacementCompanionKind, SourceCompanionPin)>> {
    let required = zone.required_source_companions()?;
    Ok(required
        .physics
        .into_iter()
        .map(|pin| (ModulePlacementCompanionKind::Physics, pin))
        .chain(
            required
                .particles
                .into_iter()
                .map(|pin| (ModulePlacementCompanionKind::Particles, pin)),
        )
        .collect())
}

pub(super) fn prepare(
    zone: &ZoneEditSession,
    payloads: &BTreeMap<String, Vec<u8>>,
) -> Result<Vec<ModulePlacementCompanion>> {
    let area = zone.head().base.terrain.area_id;
    requirements(zone)?
        .into_iter()
        .map(|(kind, pin)| {
            let (package_path, proof_path) =
                crate::module_placement_companions::export_paths(kind, pin);
            let package = payloads
                .get(&package_path)
                .ok_or("The physics and particle companion package payload is missing")?;
            let proof = payloads
                .get(&proof_path)
                .ok_or("The physics and particle companion provenance is missing")?;
            if bytes_sha256(package) != pin.package_sha256
                || proof.len() > crate::module_placement_companions::MAX_COMPANION_PROOF_BYTES
            {
                return Err("Companion package pin or proof budget differs".into());
            }
            Ok(ModulePlacementCompanion {
                areas: vec![area],
                kind,
                asset_id: pin.asset_id,
                carrier_sha256: pin.carrier_sha256,
                manifest_sha256: pin.manifest_sha256,
                package: ModuleFilePin {
                    path: package_path,
                    sha256: pin.package_sha256,
                },
                proof: ModuleFilePin {
                    path: proof_path,
                    sha256: bytes_sha256(proof),
                },
            })
        })
        .collect()
}

pub(super) fn inventory(
    packages: &[ModulePlacementCompanion],
    inventory: &mut Inventory,
) -> Result<()> {
    for package in packages {
        inventory.role(&package.package.path, ModuleFileRole::PlacementCompanion)?;
        inventory.role(&package.proof.path, ModuleFileRole::Provenance)?;
    }
    Ok(())
}

pub(super) fn take_payloads(payloads: &mut BTreeMap<String, Vec<u8>>) -> BTreeMap<String, Vec<u8>> {
    let paths = payloads
        .keys()
        .filter(|path| path.starts_with(PREFIX))
        .cloned()
        .collect::<Vec<_>>();
    paths
        .into_iter()
        .filter_map(|path| payloads.remove(&path).map(|bytes| (path, bytes)))
        .collect()
}

pub(super) fn insert_candidates(
    zone: &ZoneEditSession,
    payloads: &mut BTreeMap<String, Vec<u8>>,
    supplied: &BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    for (kind, pin) in requirements(zone)? {
        let (package, proof) = crate::module_placement_companions::export_paths(kind, pin);
        for path in [package, proof] {
            if let Some(bytes) = supplied.get(&path)
                && payloads.insert(path, bytes.clone()).is_some()
            {
                return Err("Companion transport conflicts with an export payload".into());
            }
        }
    }
    Ok(())
}

pub(super) fn append(
    prepared: &mut Prepared,
    zone: &ZoneEditSession,
    supplied: &BTreeMap<String, Vec<u8>>,
) -> Result<()> {
    let area = zone.head().base.terrain.area_id;
    let packages = prepare(zone, supplied)?;
    if !packages.is_empty() && prepared.module.provenance.rights == ModuleRights::Redistributable {
        return Err("Placement companion redistribution rights are not yet qualified".into());
    }
    for companion in packages {
        if let Some(existing) = prepared
            .module
            .placement_companions
            .iter_mut()
            .find(|existing| existing.kind == companion.kind && existing.pin() == companion.pin())
        {
            if existing.package != companion.package || existing.proof != companion.proof {
                return Err("Areas disagree on immutable companion data".into());
            }
            existing.areas.push(area);
            existing.areas.sort_unstable();
            existing.areas.dedup();
            continue;
        }
        for (pin, role) in [
            (&companion.package, ModuleFileRole::PlacementCompanion),
            (&companion.proof, ModuleFileRole::Provenance),
        ] {
            let bytes = supplied
                .get(&pin.path)
                .ok_or("Companion payload disappeared")?;
            if prepared.payloads.contains_key(&pin.path) {
                return Err("Companion payload conflicts with another area".into());
            }
            prepared.module.files.push(ModuleFile {
                path: pin.path.clone(),
                bytes: bytes.len() as u64,
                sha256: pin.sha256,
                role,
            });
            prepared.payloads.insert(pin.path.clone(), bytes.clone());
        }
        prepared
            .module
            .provenance
            .proof_paths
            .push(companion.proof.path.clone());
        prepared.module.placement_companions.push(companion);
    }
    if !prepared.module.placement_companions.is_empty() {
        prepared.module.compatibility.module_api_version = prepared
            .module
            .compatibility
            .module_api_version
            .max(MODULE_PLACEMENT_COMPANIONS_API_VERSION);
    }
    Ok(())
}
