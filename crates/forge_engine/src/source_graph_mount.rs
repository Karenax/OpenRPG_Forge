//! One source-scene mount transaction for editor and installed-module hosts.
//! Payloads retain their complete pins; mounting does not reveal the scene or
//! qualify GPU passes, authored ordering, physics or visual fidelity.
use crate::{
    source_graph_context::HostFrame,
    source_graph_host::{self, SourceSceneHost},
    source_graph_scene_attachment::OrdinaryPrimitive,
    source_graph_scene_geometry::SourcePrimitiveKey,
    source_physics_contacts::ContactSceneSettings,
};
use bevy::prelude::*;
use forge_core::{ContentHash, bytes_sha256};
use std::{collections::BTreeMap, sync::Arc};

#[derive(Clone)]
pub struct SourcePhysicsPayload {
    pub wire: Arc<[u8]>,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package_sha256: ContentHash,
}

impl SourcePhysicsPayload {
    /// Retain the entire saved identity until the carrier is actually mounted.
    pub fn decode_for_carrier(
        &self,
        carrier: ContentHash,
    ) -> Result<forge_assets::source_physics_package::SourcePhysicsPackage, String> {
        if self.carrier_sha256 != carrier {
            return Err("Paquet physique différent du modèle à monter".into());
        }
        forge_assets::source_physics_package::SourcePhysicsPackage::decode_pinned(
            &self.wire,
            self.carrier_sha256,
            self.manifest_sha256,
            self.package_sha256,
        )
    }
}

#[derive(Clone)]
pub struct SourceParticlePayload {
    pub wire: Arc<[u8]>,
    pub carrier_sha256: ContentHash,
    pub manifest_sha256: ContentHash,
    pub package_sha256: ContentHash,
}

impl SourceParticlePayload {
    pub fn install(
        &self,
        world: &mut World,
        host: &mut SourceSceneHost,
        carrier: &[u8],
    ) -> Result<(), String> {
        let package = forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
            &self.wire,
            self.carrier_sha256,
            self.manifest_sha256,
            self.package_sha256,
        )?;
        let resolved = package.resolve_for_carrier(carrier)?;
        let slots = crate::source_particle_host::HostedSourceParticle::available_slot_pairs(
            world,
            resolved.contract.owners.len(),
        )?;
        host.install_particle_package(world, carrier, &resolved, &slots)
    }
}

pub struct SourceSceneMount<'a> {
    pub root: Entity,
    pub carrier: &'a [u8],
    pub selected_scene: Option<usize>,
    pub target_world_from_root: Mat4,
    pub frame: &'a HostFrame,
    pub ordinary: BTreeMap<SourcePrimitiveKey, OrdinaryPrimitive>,
    pub physics: Option<&'a SourcePhysicsPayload>,
    pub particles: Option<&'a SourceParticlePayload>,
    pub contacts: Option<&'a ContactSceneSettings>,
    pub shadow_layer: usize,
}

/// Mount using the same carrier, physical/particle packages and shadow routes.
/// Errors after attachment roll back the unpublished host without despawning
/// the caller's root. `None` retains the existing ordinary glTF route.
pub fn mount(
    world: &mut World,
    request: SourceSceneMount<'_>,
) -> Result<Option<SourceSceneHost>, String> {
    let physics = request
        .physics
        .map(|payload| payload.decode_for_carrier(bytes_sha256(request.carrier)))
        .transpose()?;
    if physics.is_some() && request.contacts.is_none() {
        return Err("Paramètres physiques explicites de la scène absents".into());
    }
    let Some(mut host) = source_graph_host::mount_initial(
        world,
        request.root,
        request.carrier,
        request.selected_scene,
        request.target_world_from_root,
        request.frame,
        request.ordinary,
    )?
    else {
        if physics.is_some() || request.particles.is_some() {
            return Err("Source companion packages require a source scene".into());
        }
        return Ok(None);
    };
    let result = (|| {
        if let Some(particles) = request.particles {
            particles.install(world, &mut host, request.carrier)?;
        }
        if let (Some(physics), Some(contacts)) = (&physics, request.contacts) {
            host.install_decoded_physics_package_with_settings(
                world,
                request.carrier,
                physics,
                contacts,
            )?;
        }
        host.install_shadow_routes(world, request.shadow_layer)?;
        Ok::<(), String>(())
    })();
    if let Err(error) = result {
        host.rollback_unpublished(world);
        return Err(error);
    }
    Ok(Some(host))
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_assets::source_physics_package::SourcePhysicsPackage;

    fn frame() -> HostFrame {
        HostFrame {
            source_world_from_target_world: Mat4::IDENTITY,
            source_absolute_world_from_target_world: Mat4::IDENTITY,
            time_parameters: Vec4::ZERO,
            previous_time_parameters: Vec4::ZERO,
            global_mip_bias: 0.,
            source_float_min: f32::MIN_POSITIVE,
        }
    }

    fn physics(carrier: &[u8]) -> SourcePhysicsPayload {
        let manifest = b"{\"rows\":[]}".to_vec();
        let package = SourcePhysicsPackage {
            carrier_sha256: bytes_sha256(carrier),
            manifest_sha256: bytes_sha256(&manifest),
            manifest,
            candidates: BTreeMap::from([("candidate.bin".into(), b"collider".to_vec())]),
        };
        let wire = package.encode().unwrap();
        SourcePhysicsPayload {
            package_sha256: bytes_sha256(&wire),
            carrier_sha256: package.carrier_sha256,
            manifest_sha256: package.manifest_sha256,
            wire: wire.into(),
        }
    }

    fn request<'a>(
        root: Entity,
        frame: &'a HostFrame,
        carrier: &'a [u8],
        physics: &'a SourcePhysicsPayload,
    ) -> SourceSceneMount<'a> {
        SourceSceneMount {
            root,
            carrier,
            selected_scene: None,
            target_world_from_root: Mat4::IDENTITY,
            frame,
            ordinary: BTreeMap::new(),
            physics: Some(physics),
            particles: None,
            contacts: None,
            shadow_layer: 31,
        }
    }

    fn ordinary_glb() -> Vec<u8> {
        let mut json = br#"{"asset":{"version":"2.0"},"buffers":[{"byteLength":0}],"scenes":[{"nodes":[]}],"scene":0}"#.to_vec();
        while !json.len().is_multiple_of(4) {
            json.push(b' ');
        }
        let mut bytes = b"glTF".to_vec();
        bytes.extend(2_u32.to_le_bytes());
        bytes.extend(u32::try_from(28 + json.len()).unwrap().to_le_bytes());
        bytes.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        bytes.extend(b"JSON");
        bytes.extend(json);
        bytes.extend(0_u32.to_le_bytes());
        bytes.extend(b"BIN\0");
        bytes
    }

    #[test]
    fn ordinary_gltf_never_silently_discards_source_companions() {
        let mut world = World::new();
        let root = world
            .spawn((Name::new("ordinary root"), Visibility::Hidden))
            .id();
        let before = world.entities().len();
        let frame = frame();
        let carrier = ordinary_glb();
        let payload = SourceParticlePayload {
            wire: Arc::from([]),
            carrier_sha256: bytes_sha256(&carrier),
            manifest_sha256: bytes_sha256(b"manifest"),
            package_sha256: bytes_sha256(b"package"),
        };
        let request = |include: bool| SourceSceneMount {
            root,
            carrier: &carrier,
            selected_scene: None,
            target_world_from_root: Mat4::IDENTITY,
            frame: &frame,
            ordinary: BTreeMap::new(),
            physics: None,
            particles: include.then_some(&payload),
            contacts: None,
            shadow_layer: 31,
        };
        assert!(mount(&mut world, request(false)).unwrap().is_none());
        let error = mount(&mut world, request(true)).err().unwrap();
        assert!(error.contains("require a source scene"));
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "ordinary root");
        assert_eq!(world.entities().len(), before);
    }

    #[test]
    fn physical_package_pins_and_explicit_contacts_precede_any_scene_mutation() {
        let carrier = b"original carrier";
        let payload = physics(carrier);
        assert_eq!(
            payload
                .decode_for_carrier(bytes_sha256(carrier))
                .unwrap()
                .candidates["candidate.bin"],
            b"collider"
        );
        let mut world = World::new();
        let root = world
            .spawn((Name::new("module root"), Visibility::Hidden))
            .id();
        let before = world.entities().len();
        let frame = frame();
        let error = mount(&mut world, request(root, &frame, carrier, &payload))
            .err()
            .unwrap();
        assert!(error.contains("Paramètres physiques"));
        let error = mount(
            &mut world,
            request(root, &frame, b"other carrier", &payload),
        )
        .err()
        .unwrap();
        assert!(error.contains("différent du modèle"));
        let mut corrupt = payload.clone();
        let mut bytes = corrupt.wire.to_vec();
        *bytes.last_mut().unwrap() ^= 1;
        corrupt.wire = bytes.into();
        assert!(mount(&mut world, request(root, &frame, carrier, &corrupt)).is_err());
        assert_eq!(world.entities().len(), before);
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "module root");
        assert_eq!(*world.get::<Visibility>(root).unwrap(), Visibility::Hidden);
        assert!(
            world
                .get::<crate::source_graph_scene_attachment::SourceSceneAttached>(root)
                .is_none()
        );
    }
}
