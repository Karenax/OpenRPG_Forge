use super::{
    BTreeMap, Cell, Chunk, ModuleFile, ModuleFilePin, ModuleFileRole, Reply, Request, Scope,
    Snapshot, bytes_sha256, complete, ready, scope, snapshot,
};
use forge_core::AssetId;
use forge_project::{
    module_package::{ModulePlacementCompanion, ModulePlacementCompanionKind},
    zone_authoring::session::SourceCompanionPin,
};
use forge_studio_frontend::module_resources_acquisition::read_companion;

fn fixture(kind: ModulePlacementCompanionKind) -> (Scope, Snapshot, SourceCompanionPin, Vec<u8>) {
    let scope = scope();
    let carrier_sha256 = bytes_sha256(b"fixture model");
    let manifest = b"{}".to_vec();
    let manifest_sha256 = bytes_sha256(&manifest);
    let wire = match kind {
        ModulePlacementCompanionKind::Physics => {
            forge_assets::source_physics_package::SourcePhysicsPackage {
                carrier_sha256,
                manifest_sha256,
                manifest,
                candidates: BTreeMap::new(),
            }
            .encode()
            .unwrap()
        }
        ModulePlacementCompanionKind::Particles => {
            forge_assets::source_particle_package::SourceParticlePackage {
                carrier_sha256,
                manifest_sha256,
                manifest,
                resources: BTreeMap::new(),
            }
            .encode()
            .unwrap()
        }
    };
    let pin = SourceCompanionPin {
        asset_id: AssetId::deterministic("resources/companion"),
        carrier_sha256,
        manifest_sha256,
        package_sha256: bytes_sha256(&wire),
    };
    let package = ModuleFilePin {
        path: "companions/package.bin".into(),
        sha256: pin.package_sha256,
    };
    let proof = ModuleFilePin {
        path: "companions/proof.json".into(),
        sha256: bytes_sha256(b"proof"),
    };
    let mut description = snapshot(scope);
    description
        .placement_companions
        .push(ModulePlacementCompanion {
            areas: vec![scope.area],
            kind,
            asset_id: pin.asset_id,
            carrier_sha256,
            manifest_sha256,
            package: package.clone(),
            proof: proof.clone(),
        });
    description.files = vec![
        ModuleFile {
            path: package.path,
            sha256: package.sha256,
            bytes: u64::try_from(wire.len()).unwrap(),
            role: ModuleFileRole::PlacementCompanion,
        },
        ModuleFile {
            path: proof.path,
            sha256: proof.sha256,
            bytes: 5,
            role: ModuleFileRole::Provenance,
        },
    ];
    (scope, description, pin, wire)
}

fn response(
    scope: Scope,
    wire: &[u8],
    pin: SourceCompanionPin,
    request: Request,
    corrupt: bool,
) -> Reply {
    let Request::Read {
        expected_head,
        expected_package,
        resource_index,
        offset,
    } = request
    else {
        panic!("Unexpected description request")
    };
    assert_eq!(expected_head, scope.head);
    assert_eq!(Some(expected_package), scope.package());
    assert_eq!(resource_index, 0);
    let end =
        (offset + forge_studio_frontend::module_resources_protocol::CHUNK_BYTES).min(wire.len());
    let mut bytes = wire[offset..end].to_vec();
    if corrupt {
        bytes[0] ^= 1;
    }
    Reply::Resource {
        chunk: Chunk {
            head_hash: scope.head,
            package_sha256: expected_package,
            resource_index,
            resource_sha256: pin.package_sha256,
            total_bytes: wire.len(),
            offset,
            bytes,
        },
    }
}

#[test]
fn exact_origin_packages_are_read_and_absence_alone_permits_fallback() {
    for kind in [
        ModulePlacementCompanionKind::Physics,
        ModulePlacementCompanionKind::Particles,
    ] {
        let (scope, description, pin, wire) = fixture(kind);
        let calls = Cell::new(0);
        let found = complete(read_companion(
            scope,
            &description,
            kind,
            pin,
            |request| {
                calls.set(calls.get() + 1);
                ready(Ok(response(scope, &wire, pin, request, false)))
            },
            || true,
        ))
        .unwrap();
        assert_eq!(found.as_deref(), Some(wire.as_slice()));
        assert!(calls.get() > 0);

        let missing = SourceCompanionPin {
            package_sha256: bytes_sha256(b"different revision"),
            ..pin
        };
        calls.set(0);
        assert!(
            complete(read_companion(
                scope,
                &description,
                kind,
                missing,
                |_| {
                    calls.set(calls.get() + 1);
                    ready(Err("unexpected read".into()))
                },
                || true
            ))
            .unwrap()
            .is_none()
        );
        assert_eq!(calls.get(), 0);

        let corrupted = complete(read_companion(
            scope,
            &description,
            kind,
            pin,
            |request| ready(Ok(response(scope, &wire, pin, request, true))),
            || true,
        ));
        assert!(
            corrupted.is_err(),
            "corruption must not be treated as absence"
        );
    }
}

#[test]
fn stale_or_invalid_inventory_never_reads_or_falls_back() {
    let (scope, description, pin, wire) = fixture(ModulePlacementCompanionKind::Physics);
    for alteration in 0..4 {
        let mut invalid = description.clone();
        match alteration {
            0 => {
                invalid.files.pop();
            }
            1 => invalid.files[0].role = ModuleFileRole::Asset,
            2 => invalid
                .placement_companions
                .push(invalid.placement_companions[0].clone()),
            _ => invalid.head_hash = bytes_sha256(b"another head"),
        }
        let calls = Cell::new(0);
        let result = complete(read_companion(
            scope,
            &invalid,
            ModulePlacementCompanionKind::Physics,
            pin,
            |_| {
                calls.set(calls.get() + 1);
                ready(Err("unexpected read".into()))
            },
            || true,
        ));
        assert!(result.is_err());
        assert_eq!(calls.get(), 0);
    }
    let current = Cell::new(true);
    let result = complete(read_companion(
        scope,
        &description,
        ModulePlacementCompanionKind::Physics,
        pin,
        |request| {
            current.set(false);
            ready(Ok(response(scope, &wire, pin, request, false)))
        },
        || current.get(),
    ));
    assert!(
        result.is_err(),
        "a replaced scope cannot admit returned bytes"
    );
}

#[test]
fn empty_legacy_descriptions_keep_their_serialized_shape() {
    let value = serde_json::to_value(snapshot(scope())).unwrap();
    assert!(value.get("placement_companions").is_none());
    let decoded: Snapshot = serde_json::from_value(value).unwrap();
    assert!(decoded.placement_companions.is_empty());
}

#[test]
fn a_replaced_module_is_not_treated_as_a_local_module_without_an_origin() {
    let local = Scope {
        origin: None,
        ..scope()
    };
    let retained = Scope::require_current_source(true, Some(local)).unwrap();
    assert_eq!(retained, local);
    assert!(
        retained.package().is_none(),
        "a current local module can use its library"
    );
    assert!(Scope::require_current_source(false, Some(local)).is_err());
    assert!(Scope::require_current_source(false, None).is_err());
    assert!(Scope::require_current_source(true, None).is_err());
}

#[test]
fn the_original_module_scope_and_epoch_survive_the_whole_acquisition() {
    use forge_studio_frontend::module_resources_acquisition::SourceToken;
    let original = scope();
    let token = SourceToken::capture(true, Some(original), 8).unwrap();
    assert!(token.ensure_current(true, Some(original), 8).is_ok());
    let other_origin = Scope {
        origin: Some(forge_project::workspace::ModuleOriginPin {
            package_sha256: bytes_sha256(b"another module package"),
            ..original.origin.unwrap()
        }),
        ..original
    };
    assert_eq!(other_origin.head, original.head);
    assert_eq!(other_origin.area, original.area);
    assert!(token.ensure_current(true, Some(other_origin), 8).is_err());
    assert!(
        token.ensure_current(true, Some(original), 9).is_err(),
        "same scope after replacement is an ABA"
    );
    assert!(token.ensure_current(false, Some(original), 8).is_err());
    assert!(token.ensure_current(true, None, 8).is_err());
}

#[test]
fn projection_restarts_for_replacement_without_reloading_a_ready_unchanged_scene() {
    use forge_studio_frontend::module_resources_acquisition::SourceToken;
    let original = scope();
    let first = SourceToken::capture(true, Some(original), 4).unwrap();
    let edited = Scope {
        head: bytes_sha256(b"edited head"),
        ..original
    };
    let next = SourceToken::capture(true, Some(edited), 5).unwrap();
    assert!(SourceToken::projection_needs_restart(
        Some(first),
        Some(next),
        false
    ));
    assert!(!SourceToken::projection_needs_restart(
        Some(first),
        Some(next),
        true
    ));
    let replacement = Scope {
        origin: Some(forge_project::workspace::ModuleOriginPin {
            package_sha256: bytes_sha256(b"other archive"),
            ..original.origin.unwrap()
        }),
        ..original
    };
    let replaced = SourceToken::capture(true, Some(replacement), 5).unwrap();
    assert!(SourceToken::projection_needs_restart(
        Some(first),
        Some(replaced),
        true
    ));
    assert!(SourceToken::projection_needs_restart(
        Some(first),
        None,
        true
    ));
    assert!(!SourceToken::projection_needs_restart(
        Some(first),
        Some(first),
        false
    ));
    assert!(!SourceToken::projection_needs_restart(None, None, false));
}
