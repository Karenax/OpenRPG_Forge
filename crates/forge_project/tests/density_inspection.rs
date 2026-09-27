use forge_project::{
    VegetationDensityMask, VegetationDocument, VegetationLayerId,
    authoring::new_terrain,
    zone_authoring::{
        DensityOverride, GeneratorStamp, ZoneAuthoringHead, ZoneBase, ZoneCorrections,
    },
};

fn head() -> ZoneAuthoringHead {
    let terrain = new_terrain("inspection", "zone", "Zone").unwrap();
    let mut vegetation: VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
        layer.density_mask = None;
    }
    ZoneAuthoringHead {
        schema: "forge.zone-authoring".into(),
        version: 1,
        revision: terrain.revision,
        base: ZoneBase {
            generator: GeneratorStamp {
                name: "fixture".into(),
                version: "1".into(),
                inputs_hash: terrain.content_hash().unwrap(),
            },
            terrain,
            vegetation: Some(vegetation),
        },
        corrections: ZoneCorrections::default(),
    }
}

#[test]
fn inspection_distinguishes_inheritance_manual_mask_and_lock_without_mutating() {
    let mut head = head();
    let id = head.base.vegetation.as_ref().unwrap().layers[0].id.clone();
    let other = head.base.vegetation.as_ref().unwrap().layers[1].id.clone();
    let inherited = head.inspect_density(&id).unwrap();
    assert!(!inherited.has_override && !inherited.locked);
    assert!(inherited.recipe_mask.is_none() && inherited.effective_mask.is_none());
    let recipe = VegetationDensityMask {
        vertices_per_axis: 2,
        samples_u8: vec![64, 128, 255, 0],
    };
    head.base.vegetation.as_mut().unwrap().layers[0].density_mask = Some(recipe);
    let inherited = head.inspect_density(&id).unwrap();
    assert_eq!(inherited.recipe_mask, inherited.effective_mask);
    assert_eq!(inherited.effective_mask.as_ref().unwrap().zero_samples, 1);
    head.corrections.density.push(DensityOverride {
        layer_id: id.clone(),
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 0, 100, 200],
        },
        locked: true,
    });
    let before = head.clone();
    let read = head.inspect_density(&id).unwrap();
    assert_eq!(head, before);
    assert!(read.has_override && read.locked);
    assert_eq!(read.recipe_mask, inherited.recipe_mask);
    assert_ne!(read.recipe_mask, read.effective_mask);
    let mask = read.effective_mask.as_ref().unwrap();
    assert_eq!(
        (mask.samples, mask.zero_samples, mask.minimum, mask.maximum),
        (4, 2, 0, 200)
    );
    assert!(!head.inspect_density(&other).unwrap().has_override);
    head.corrections.density[0].locked = false;
    let unlocked = head.inspect_density(&id).unwrap();
    assert!(unlocked.has_override && !unlocked.locked);
    assert_eq!(read.effective_mask, unlocked.effective_mask);
    assert_ne!(read.head_hash, unlocked.head_hash);
    assert!(
        !serde_json::to_string(&unlocked)
            .unwrap()
            .contains("samples_u8")
    );
    head.corrections.density.clear();
    assert_eq!(head.inspect_density(&id).unwrap(), inherited);
    assert!(
        head.inspect_density(&VegetationLayerId::new("absent"))
            .is_err()
    );
    head.base.vegetation = None;
    assert!(head.inspect_density(&id).is_err());
}

#[test]
fn playtest_snapshot_captures_effective_zone_and_rejects_partial_projection() {
    use forge_project::authoring::playtest::{PlaytestSnapshot, ProjectionReadiness};
    let mut head = head();
    let layer_id = head.base.vegetation.as_ref().unwrap().layers[0].id.clone();
    head.corrections.base_elevation_m = Some(12.0);
    head.corrections.density.push(DensityOverride {
        layer_id,
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 0, 255, 255],
        },
        locked: true,
    });
    let hash = head.content_hash().unwrap();
    let snapshot = PlaytestSnapshot::from_zone(&head, hash).unwrap();
    let effective = head.effective().unwrap();
    assert_eq!(snapshot.terrain(), &effective.0);
    assert_eq!(snapshot.vegetation(), effective.1.as_ref());
    let identity = snapshot.identity();
    assert_eq!(identity.source_hash, hash);
    assert_ne!(identity.source_hash, identity.terrain_hash);
    assert!(PlaytestSnapshot::from_zone(&head, identity.terrain_hash).is_err());
    assert_eq!(
        identity.readiness(hash, None, None),
        ProjectionReadiness::WaitingTerrain
    );
    assert_eq!(
        identity.readiness(hash, Some(hash), identity.vegetation_hash),
        ProjectionReadiness::WaitingTerrain
    );
    assert_eq!(
        identity.readiness(hash, Some(identity.terrain_hash), None),
        ProjectionReadiness::WaitingVegetation
    );
    assert_eq!(
        identity.readiness(hash, Some(identity.terrain_hash), Some(hash)),
        ProjectionReadiness::WaitingVegetation
    );
    assert_eq!(
        identity.readiness(hash, Some(identity.terrain_hash), identity.vegetation_hash),
        ProjectionReadiness::Ready
    );
    head.corrections.base_elevation_m = Some(42.0);
    assert_eq!(
        snapshot.terrain(),
        &effective.0,
        "snapshot independent from later edits"
    );
    assert_eq!(
        identity.readiness(
            head.content_hash().unwrap(),
            Some(identity.terrain_hash),
            identity.vegetation_hash
        ),
        ProjectionReadiness::StaleSource
    );
    assert!(PlaytestSnapshot::from_zone(&head, hash).is_err());
    let legacy = PlaytestSnapshot::from_terrain(&effective.0, identity.terrain_hash).unwrap();
    assert!(legacy.vegetation().is_none());
    assert_eq!(
        legacy
            .identity()
            .readiness(identity.terrain_hash, Some(identity.terrain_hash), None),
        ProjectionReadiness::Ready
    );
    assert_eq!(
        legacy.identity().readiness(
            identity.terrain_hash,
            Some(identity.terrain_hash),
            identity.vegetation_hash
        ),
        ProjectionReadiness::WaitingVegetation
    );
}
