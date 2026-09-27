use forge_project::{
    VegetationDensityMask, VegetationDocument, authoring::new_terrain, zone_authoring::*,
};

fn head() -> ZoneAuthoringHead {
    let mut terrain = new_terrain("zone-head", "test", "Zone").unwrap();
    terrain.schema_version = 3;
    let mut vegetation: VegetationDocument = serde_json::from_str(include_str!(
        "../../../examples/visual_valley/vegetation.v1.json"
    ))
    .unwrap();
    vegetation.project_id = terrain.project_id;
    vegetation.area_id = terrain.area_id;
    for layer in &mut vegetation.layers {
        layer.material_filter = None;
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
fn aggregate_roundtrip_preserves_correction_outside_history() {
    let mut head = head();
    let id = head.base.vegetation.as_ref().unwrap().layers[0].id.clone();
    let original_base = head.base.clone();
    head.corrections.density.push(DensityOverride {
        layer_id: id,
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 255, 255, 0],
        },
        locked: true,
    });
    let expected = head.effective().unwrap();
    let restored = ZoneAuthoringHead::from_bytes(&head.to_bytes().unwrap()).unwrap();
    assert_eq!(
        restored.content_hash().unwrap(),
        head.content_hash().unwrap()
    );
    assert_eq!(restored.effective().unwrap(), expected);
    assert_eq!(restored.base, original_base);
    let mask = expected.1.unwrap().layers[0].density_mask.clone();
    head.corrections.density[0].locked = false;
    assert_eq!(
        head.effective().unwrap().1.unwrap().layers[0].density_mask,
        mask
    );
}

#[test]
fn terrain_tools_preserve_other_domains_and_bind_the_aggregate_hash() {
    use forge_project::terrain_edit::{TerrainAction, TerrainEdit};
    use forge_project::zone_authoring::session::ZoneAction;
    let mut head = head();
    head.corrections.density.push(DensityOverride {
        layer_id: head.base.vegetation.as_ref().unwrap().layers[0].id.clone(),
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0; 4],
        },
        locked: true,
    });
    let before = head.to_bytes().unwrap();
    let id = forge_core::CommandId::deterministic("aggregate-terrain-tool");
    let request = head
        .prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::Rename {
                name: "Clairière protégée".into(),
            }),
            id,
        )
        .unwrap();
    assert_eq!(request.expected_hash, head.content_hash().unwrap());
    assert_ne!(
        request.expected_hash,
        head.effective().unwrap().0.content_hash().unwrap()
    );
    let ZoneAction::Edit(corrections) = request.action else {
        panic!("expected corrections");
    };
    assert_eq!(corrections.density, head.corrections.density);
    assert_eq!(
        corrections.material_overrides,
        head.corrections.material_overrides
    );
    assert_eq!(head.to_bytes().unwrap(), before);
    assert_eq!(
        head.prepare_terrain_action(TerrainAction::Undo, id)
            .unwrap()
            .action,
        ZoneAction::Undo
    );
    assert!(
        head.prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetSchemaVersion { version: 2 }),
            id
        )
        .is_err()
    );
    assert!(
        head.prepare_terrain_action(
            TerrainAction::Edit(TerrainEdit::SetBaseElevation { metres: f32::NAN }),
            id
        )
        .is_err()
    );
}

#[test]
fn orphan_duplicate_identity_and_unknown_schema_are_rejected() {
    let mut head = head();
    let id = head.base.vegetation.as_ref().unwrap().layers[0].id.clone();
    head.corrections.density.push(DensityOverride {
        layer_id: id,
        mask: VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0; 4],
        },
        locked: false,
    });
    head.base.vegetation.as_mut().unwrap().layers.remove(0);
    assert!(head.effective().unwrap_err().contains("Orphan"));
    let mut duplicate = self::head();
    let correction = head.corrections.density[0].clone();
    duplicate.corrections.density = vec![correction.clone(), correction];
    assert!(duplicate.validate().unwrap_err().contains("unique"));
    let mut clean = self::head();
    clean.base.vegetation.as_mut().unwrap().area_id = forge_core::AreaId::deterministic("wrong");
    assert!(clean.validate().is_err());
    clean = self::head();
    clean.version = 999;
    assert!(clean.to_bytes().is_err());
    assert!(ZoneAuthoringHead::from_bytes(&vec![b' '; MAX_ZONE_HEAD_BYTES + 1]).is_err());
}
