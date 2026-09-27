//! Asset-free starting recipe for authored zones, independent of demonstration maps.
use forge_project::{
    TerrainDocument, TerrainMaterialRole, VegetationDensityPattern, VegetationDocument,
    VegetationExtent, VegetationKind, VegetationLayer, VegetationMaterialFilter,
    VegetationPresentationProfile,
};

/// Empty derived source for a zone without an accepted vegetation recipe.
pub fn empty(terrain: &TerrainDocument) -> Result<VegetationDocument, String> {
    let mut source = prepare(terrain, 0)?;
    for layer in &mut source.layers {
        layer.acceptance_probability = 0.0;
    }
    Ok(source)
}

/// Initialize once, or change only the seed of an existing base recipe.
/// The command retains all authored corrections and is not applied here.
pub fn prepare_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    seed: u64,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let source_hash = head.content_hash()?;
    let mut base = head.base.clone();
    if let Some(vegetation) = base.vegetation.as_mut() {
        if vegetation.seed == seed {
            return Err("La seed de végétation est déjà celle demandée".into());
        }
        vegetation.seed = seed;
    } else {
        base.vegetation = Some(prepare(&base.terrain, seed)?);
    }
    // This stamp describes this transformation, not the terrain's historical generator.
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-transform".into(),
        version: "1".into(),
        inputs_hash: forge_core::canonical_json_sha256(&(source_hash, seed))
            .map_err(|error| error.to_string())?,
    };
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-vegetation:{source_hash}:{seed}")),
    )
}

/// Change one existing layer by stable ID, retaining every authored correction.
pub fn prepare_layer_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    acceptance_probability: f32,
    minimum_spacing_m: f32,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let base = prepare_layer_base(head, layer_id, acceptance_probability, minimum_spacing_m)?;
    let inputs_hash = base.generator.inputs_hash;
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-vegetation-layer:{inputs_hash}")),
    )
}

/// Proposed base only; callers must validate origins or explicitly reconcile them.
pub fn prepare_layer_base(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    acceptance_probability: f32,
    minimum_spacing_m: f32,
) -> Result<forge_project::zone_authoring::ZoneBase, String> {
    let source_hash = head.content_hash()?;
    let mut base = head.base.clone();
    let vegetation = base
        .vegetation
        .as_mut()
        .ok_or("Initialiser la végétation d’abord")?;
    let layer = vegetation
        .layers
        .iter_mut()
        .find(|layer| &layer.id == layer_id)
        .ok_or("Couche de végétation absente")?;
    if layer.acceptance_probability.to_bits() == acceptance_probability.to_bits()
        && layer.minimum_spacing_m.to_bits() == minimum_spacing_m.to_bits()
    {
        return Err("Les réglages de cette couche sont déjà ceux demandés".into());
    }
    layer.acceptance_probability = acceptance_probability;
    layer.minimum_spacing_m = minimum_spacing_m;
    vegetation.validate().map_err(|error| error.to_string())?;
    let inputs_hash = forge_core::canonical_json_sha256(&(
        source_hash,
        layer_id,
        acceptance_probability,
        minimum_spacing_m,
    ))
    .map_err(|error| error.to_string())?;
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-layer-transform".into(),
        version: "1".into(),
        inputs_hash,
    };
    Ok(base)
}

/// Change habitat limits in the existing recipe without rewriting painted masks.
/// Slope is rise/run (1 means 100 percent), not an angle in degrees.
pub fn prepare_layer_habitat_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    minimum_altitude_m: f32,
    maximum_altitude_m: f32,
    maximum_slope_gradient: f32,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let base = prepare_layer_habitat_base(
        head,
        layer_id,
        minimum_altitude_m,
        maximum_altitude_m,
        maximum_slope_gradient,
    )?;
    let inputs_hash = base.generator.inputs_hash;
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-layer-habitat:{inputs_hash}")),
    )
}

/// Proposed habitat recipe; does not discard unresolved promoted origins.
pub fn prepare_layer_habitat_base(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    minimum_altitude_m: f32,
    maximum_altitude_m: f32,
    maximum_slope_gradient: f32,
) -> Result<forge_project::zone_authoring::ZoneBase, String> {
    let mut base = head.base.clone();
    let vegetation = base
        .vegetation
        .as_mut()
        .ok_or("Initialiser la végétation d’abord")?;
    let layer = vegetation
        .layers
        .iter_mut()
        .find(|layer| &layer.id == layer_id)
        .ok_or("Couche de végétation absente")?;
    if layer.minimum_altitude_m.to_bits() == minimum_altitude_m.to_bits()
        && layer.maximum_altitude_m.to_bits() == maximum_altitude_m.to_bits()
        && layer.maximum_slope_gradient.to_bits() == maximum_slope_gradient.to_bits()
    {
        return Err("Les limites de cette couche sont déjà celles demandées".into());
    }
    layer.minimum_altitude_m = minimum_altitude_m;
    layer.maximum_altitude_m = maximum_altitude_m;
    layer.maximum_slope_gradient = maximum_slope_gradient;
    vegetation.validate().map_err(|error| error.to_string())?;
    let inputs_hash = forge_core::canonical_json_sha256(&(
        head.content_hash()?,
        layer_id,
        minimum_altitude_m,
        maximum_altitude_m,
        maximum_slope_gradient,
    ))
    .map_err(|error| error.to_string())?;
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-habitat".into(),
        version: "1".into(),
        inputs_hash,
    };
    Ok(base)
}

/// Rename a layer without changing its stable ID, generation settings or corrections.
pub fn prepare_layer_name_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    name: &str,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let name = name.trim();
    let mut base = head.base.clone();
    let vegetation = base
        .vegetation
        .as_mut()
        .ok_or("Initialiser la végétation d’abord")?;
    let layer = vegetation
        .layers
        .iter_mut()
        .find(|layer| &layer.id == layer_id)
        .ok_or("Couche de végétation absente")?;
    if layer.display_name == name {
        return Err("Le nom de cette couche est déjà celui demandé".into());
    }
    layer.display_name = name.into();
    vegetation.validate().map_err(|error| error.to_string())?;
    let inputs_hash = forge_core::canonical_json_sha256(&(head.content_hash()?, layer_id, name))
        .map_err(|error| error.to_string())?;
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-layer-name".into(),
        version: "1".into(),
        inputs_hash,
    };
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-layer-name:{inputs_hash}")),
    )
}

/// Membership changes available through reviewed zone regeneration.
#[derive(Clone, Copy)]
pub enum LayerMembershipEdit {
    Copy,
    Remove,
}

/// Copy recipe settings without painted corrections, or reject removal of referenced layers.
pub fn prepare_layer_membership_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    edit: LayerMembershipEdit,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let source_hash = head.content_hash()?;
    let inputs_hash = forge_core::canonical_json_sha256(&(
        source_hash,
        layer_id,
        matches!(edit, LayerMembershipEdit::Copy),
    ))
    .map_err(|error| error.to_string())?;
    let mut base = head.base.clone();
    let vegetation = base
        .vegetation
        .as_mut()
        .ok_or("Initialiser la végétation d’abord")?;
    let index = vegetation
        .layers
        .iter()
        .position(|layer| &layer.id == layer_id)
        .ok_or("Couche absente")?;
    match edit {
        LayerMembershipEdit::Copy => {
            let mut copy = vegetation.layers[index].clone();
            copy.id = forge_project::VegetationLayerId::new(format!(
                "layer_{}",
                forge_core::CommandId::deterministic(inputs_hash.to_string())
                    .to_string()
                    .replace('-', "_")
            ));
            copy.display_name = "Nouvelle couche (copie)".into();
            copy.density_mask = None;
            vegetation.layers.insert(index + 1, copy);
        }
        LayerMembershipEdit::Remove => {
            vegetation.layers.remove(index);
        }
    }
    vegetation.validate().map_err(|error| error.to_string())?;
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-membership".into(),
        version: "1".into(),
        inputs_hash,
    };
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-layer-membership:{inputs_hash}")),
    )
}

/// Move an existing layer one position without changing its identity or authored corrections.
pub fn prepare_layer_order_request(
    head: &forge_project::zone_authoring::ZoneAuthoringHead,
    layer_id: &forge_project::VegetationLayerId,
    move_up: bool,
) -> Result<forge_project::zone_authoring::session::ZoneRequest, String> {
    let source_hash = head.content_hash()?;
    let mut base = head.base.clone();
    let layers = &mut base
        .vegetation
        .as_mut()
        .ok_or("Initialiser la végétation d’abord")?
        .layers;
    let from = layers
        .iter()
        .position(|layer| &layer.id == layer_id)
        .ok_or("Couche absente")?;
    let to = if move_up {
        from.checked_sub(1)
    } else {
        from.checked_add(1).filter(|&i| i < layers.len())
    }
    .ok_or("La couche est déjà à cette extrémité")?;
    layers.swap(from, to);
    let inputs_hash = forge_core::canonical_json_sha256(&(source_hash, layer_id, move_up))
        .map_err(|error| error.to_string())?;
    base.generator = forge_project::zone_authoring::GeneratorStamp {
        name: "forge-editor-vegetation-layer-order".into(),
        version: "1".into(),
        inputs_hash,
    };
    crate::promotion_preflight::prepare_regeneration(
        head,
        base,
        forge_core::CommandId::deterministic(format!("editor-layer-order:{inputs_hash}")),
    )
}

/// Prepare a new source; callers must review and accept it through zone commands.
/// Stable layer IDs survive seed changes. This does not replace an existing recipe.
pub fn prepare(terrain: &TerrainDocument, seed: u64) -> Result<VegetationDocument, String> {
    terrain.validate().map_err(|error| error.to_string())?;
    let grass = terrain
        .material_layers
        .get(TerrainMaterialRole::Grass.index())
        .ok_or("Terrain has no grass material layer")?;
    let filter = || {
        Some(VegetationMaterialFilter::new(
            TerrainMaterialRole::Grass,
            grass.id.clone(),
            0.25,
        ))
    };
    let mut trees = VegetationLayer::new(
        "editor_trees",
        "Arbres",
        VegetationKind::Tree,
        0.35,
        2,
        5.0,
        0.9,
        1.4,
        0.65,
        -4096.0,
        4096.0,
        filter(),
        None,
        0.35,
    );
    trees.density_pattern = Some(VegetationDensityPattern {
        patch_size_m: 64.0,
        minimum_multiplier: 0.1,
        maximum_multiplier: 1.5,
    });
    let mut ground = VegetationLayer::new(
        "editor_grass",
        "Herbe",
        VegetationKind::Grass,
        0.65,
        4,
        0.0,
        0.7,
        1.2,
        0.8,
        -4096.0,
        4096.0,
        filter(),
        None,
        0.8,
    );
    ground.density_pattern = Some(VegetationDensityPattern {
        patch_size_m: 24.0,
        minimum_multiplier: 0.0,
        maximum_multiplier: 1.5,
    });
    let document = VegetationDocument::new(
        terrain.project_id,
        terrain.area_id,
        terrain.revision,
        "Végétation de zone",
        seed,
        VegetationExtent::new(terrain.extent_m.size_x_m, terrain.extent_m.size_z_m),
        8,
        vec![trees, ground],
        Vec::new(),
        VegetationPresentationProfile {
            near_lod_distance_m: 24.0,
            medium_lod_distance_m: 72.0,
            hlod_distance_m: 160.0,
            cull_distance_m: 320.0,
            shadow_distance_m: 96.0,
            wind_strength: 0.5,
            wind_speed_hz: 0.18,
            interaction_radius_m: 2.0,
            interaction_strength: 0.6,
            grass_blades_per_patch: 24,
        },
    );
    document.validate().map_err(|error| error.to_string())?;
    Ok(document)
}
