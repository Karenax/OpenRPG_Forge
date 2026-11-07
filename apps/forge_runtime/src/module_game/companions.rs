//! Retain the exact companion packets declared by an admitted module zone.
//! This prepares source inputs only; it does not admit physics or rendering.
use forge_core::AreaId;
use forge_delivery::module_runtime::LoadedModuleZone;
use forge_engine::source_graph_mount::{SourceParticlePayload, SourcePhysicsPayload};
use forge_project::{
    module_package::{
        ModuleFile, ModuleFileRole, ModulePlacementCompanion, ModulePlacementCompanionKind,
    },
    zone_authoring::{placements::ManualPlacement, session::SourceCompanionPin},
};
use std::{collections::BTreeMap, sync::Arc};

#[derive(Clone, Default)]
pub(super) struct Prepared {
    pub physics: Option<SourcePhysicsPayload>,
    pub particles: Option<SourceParticlePayload>,
}

/// Shared buffers are keyed by their complete saved identity, including kind.
#[derive(Default)]
pub(super) struct Cache {
    wires: BTreeMap<(ModulePlacementCompanionKind, SourceCompanionPin), Arc<[u8]>>,
    total: usize,
}

pub(super) fn prepare(
    loaded: &LoadedModuleZone,
    placement: &ManualPlacement,
    cache: &mut Cache,
) -> Result<Prepared, String> {
    prepare_with(
        loaded.zone.head().base.terrain.area_id,
        &loaded.manifest.placement_companions,
        &loaded.manifest.files,
        placement,
        cache,
        |path, maximum| loaded.read_payload(path, maximum),
    )
}

fn prepare_with(
    area: AreaId,
    bindings: &[ModulePlacementCompanion],
    files: &[ModuleFile],
    placement: &ManualPlacement,
    cache: &mut Cache,
    mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<Prepared, String> {
    let physics = if let Some(saved) = &placement.physics {
        if saved.schema_version != 1 || saved.carrier_sha256 != placement.asset_hash {
            return Err("Module physics reference differs from its model".into());
        }
        let pin = SourceCompanionPin {
            asset_id: placement.asset_id,
            carrier_sha256: saved.carrier_sha256,
            manifest_sha256: saved.manifest_sha256,
            package_sha256: saved.package_sha256,
        };
        let wire = read_exact(
            area,
            bindings,
            files,
            ModulePlacementCompanionKind::Physics,
            pin,
            cache,
            &mut read,
        )?;
        Some(SourcePhysicsPayload {
            wire,
            carrier_sha256: pin.carrier_sha256,
            manifest_sha256: pin.manifest_sha256,
            package_sha256: pin.package_sha256,
        })
    } else {
        None
    };
    let particles = if let Some(saved) = &placement.particles {
        if saved.schema_version != 1 || saved.carrier_sha256 != placement.asset_hash {
            return Err("Module particle reference differs from its model".into());
        }
        let pin = SourceCompanionPin {
            asset_id: placement.asset_id,
            carrier_sha256: saved.carrier_sha256,
            manifest_sha256: saved.manifest_sha256,
            package_sha256: saved.package_sha256,
        };
        let wire = read_exact(
            area,
            bindings,
            files,
            ModulePlacementCompanionKind::Particles,
            pin,
            cache,
            &mut read,
        )?;
        Some(SourceParticlePayload {
            wire,
            carrier_sha256: pin.carrier_sha256,
            manifest_sha256: pin.manifest_sha256,
            package_sha256: pin.package_sha256,
        })
    } else {
        None
    };
    Ok(Prepared { physics, particles })
}

#[allow(
    clippy::too_many_arguments,
    reason = "Bind each read to its admitted area, inventory, full pin and shared budget"
)]
fn read_exact(
    area: AreaId,
    bindings: &[ModulePlacementCompanion],
    files: &[ModuleFile],
    kind: ModulePlacementCompanionKind,
    pin: SourceCompanionPin,
    cache: &mut Cache,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
) -> Result<Arc<[u8]>, String> {
    let mut matches = bindings
        .iter()
        .filter(|binding| binding.kind == kind && binding.pin() == pin);
    let binding = matches
        .next()
        .ok_or("Saved companion is absent from the admitted module")?;
    if matches.next().is_some() || !binding.areas.contains(&area) {
        return Err("Saved companion is ambiguous or outside the selected area".into());
    }
    let mut inventory = files
        .iter()
        .filter(|file| file.path == binding.package.path);
    let file = inventory
        .next()
        .ok_or("Saved companion is absent from the module inventory")?;
    if inventory.next().is_some()
        || file.role != ModuleFileRole::PlacementCompanion
        || file.sha256 != pin.package_sha256
    {
        return Err("Saved companion file identity differs from the admitted inventory".into());
    }
    if let Some(wire) = cache.wires.get(&(kind, pin)) {
        if u64::try_from(wire.len()).map_err(|_| "Saved companion size is not representable")?
            != file.bytes
        {
            return Err("Cached companion byte length differs from the admitted inventory".into());
        }
        return Ok(Arc::clone(wire));
    }
    let maximum = match kind {
        ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::MAX_PACKAGE_BYTES
        }
        ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::MAX_PACKAGE_BYTES
        }
    };
    let size =
        usize::try_from(file.bytes).map_err(|_| "Saved companion size is not representable")?;
    let total = cache
        .total
        .checked_add(size)
        .ok_or("Saved companion budget exceeded")?;
    if size == 0
        || size > maximum
        || total > forge_project::workspace::export::MAX_MODULE_EXPORT_SOURCE_BYTES
    {
        return Err("Saved companion budget exceeded".into());
    }
    let wire = read(&binding.package.path, maximum)?;
    if wire.len() != size {
        return Err("Saved companion byte length differs".into());
    }
    match kind {
        ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
                &wire,
                pin.carrier_sha256,
                pin.manifest_sha256,
                pin.package_sha256,
            )?;
        }
        ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
                &wire,
                pin.carrier_sha256,
                pin.manifest_sha256,
                pin.package_sha256,
            )?;
        }
    }
    let wire: Arc<[u8]> = wire.into();
    cache.wires.insert((kind, pin), Arc::clone(&wire));
    cache.total = total;
    Ok(wire)
}

#[cfg(test)]
#[path = "companions/tests.rs"]
mod tests;
