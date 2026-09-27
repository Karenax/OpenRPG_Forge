use super::{RenderVegetationPrototype, VegetationPrototypeCache, VegetationPrototypeLoadStatus};
use crate::vegetation::{
    VegetationInteractionField, VegetationPresentation, VisualValleyVegetationExtension,
    VisualValleyVegetationMaterial,
};
use bevy::{material::OpaqueRendererMethod, prelude::*};
use forge_project::VegetationPresentationProfile;
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq)]
pub struct WindVegetationPrimitive {
    pub mesh: Handle<Mesh>,
    pub material: Handle<VisualValleyVegetationMaterial>,
}

#[derive(Clone, PartialEq, Resource, Default)]
pub struct VegetationPrototypeWindCache {
    pub selection_catalog: Option<forge_assets::VegetationPrototypeCatalog>,
    pub layers: BTreeMap<String, [Vec<WindVegetationPrimitive>; 3]>,
    pub failure: Option<String>,
}

impl VegetationPrototypeWindCache {
    pub(crate) fn for_placement(
        &self,
        layer: &str,
        placement: forge_core::EntityId,
    ) -> Result<&[Vec<WindVegetationPrimitive>; 3], String> {
        let key = if let Some(catalog) = self
            .selection_catalog
            .as_ref()
            .filter(|catalog| catalog.schema_version == 2)
        {
            let binding = catalog
                .select_binding(layer, placement)
                .map_err(|error| error.to_string())?
                .ok_or("tree layer missing from prototype catalog")?;
            super::prototype_key(catalog.schema_version, binding)
        } else {
            layer.to_owned()
        };
        self.layers
            .get(&key)
            .ok_or_else(|| format!("selected tree prototype missing from wind cache: {key}"))
    }
}

#[allow(clippy::needless_pass_by_value, reason = "Bevy system parameters")]
pub(super) fn prepare_wind_cache(
    status: Res<VegetationPrototypeLoadStatus>,
    source: Res<VegetationPrototypeCache>,
    profile: Res<VegetationPresentation>,
    interaction: Res<VegetationInteractionField>,
    pbr: Res<Assets<StandardMaterial>>,
    mut materials: ResMut<Assets<VisualValleyVegetationMaterial>>,
    mut cache: ResMut<VegetationPrototypeWindCache>,
) {
    if !matches!(*status, VegetationPrototypeLoadStatus::Ready { .. })
        || source.selection_catalog.is_none()
        || (cache.selection_catalog == source.selection_catalog
            && (!cache.layers.is_empty() || cache.failure.is_some()))
    {
        return;
    }
    match build_wind_cache(
        &source.render_layers,
        &pbr,
        &mut materials,
        profile.0,
        *interaction,
    ) {
        Ok((layers, count)) => {
            cache.layers = layers;
            cache.failure = None;
            cache
                .selection_catalog
                .clone_from(&source.selection_catalog);
            info!(
                event_kind = "vegetation_prototype_wind_materials_prepared",
                materials = count,
                layers = cache.layers.len(),
                "wind material cache ready; scatter not replaced"
            );
        }
        Err(error) => {
            error!(event_kind = "vegetation_prototype_wind_materials_failed", reason = %error);
            cache.failure = Some(error);
            // Never expose old species geometry under the new catalog's identities.
            cache.layers.clear();
            cache
                .selection_catalog
                .clone_from(&source.selection_catalog);
        }
    }
}

type WindLayers = BTreeMap<String, [Vec<WindVegetationPrimitive>; 3]>;

fn build_wind_cache(
    layers: &BTreeMap<String, RenderVegetationPrototype>,
    pbr: &Assets<StandardMaterial>,
    materials: &mut Assets<VisualValleyVegetationMaterial>,
    profile: VegetationPresentationProfile,
    interaction: VegetationInteractionField,
) -> Result<(WindLayers, usize), String> {
    let mut prepared = BTreeMap::new();
    for prototype in layers.values() {
        let extension =
            VisualValleyVegetationExtension::from_profile_and_interaction(profile, interaction)
                .with_governed_prototype_height(prototype.target_height_m)?;
        for primitive in prototype.lods.iter().flatten() {
            let key = (primitive.material.id(), prototype.target_height_m.to_bits());
            if let std::collections::btree_map::Entry::Vacant(entry) = prepared.entry(key) {
                let mut base = pbr
                    .get(&primitive.material)
                    .ok_or("prototype PBR material disappeared")?
                    .clone();
                if base.unlit {
                    return Err(
                        "unlit prototype material is unsupported by the vegetation PBR shader"
                            .into(),
                    );
                }
                base.opaque_render_method = OpaqueRendererMethod::Forward;
                entry.insert(VisualValleyVegetationMaterial {
                    base,
                    extension: extension.clone(),
                });
            }
        }
    }
    // No asset allocation before every source material and height has been validated.
    let handles: BTreeMap<_, _> = prepared
        .into_iter()
        .map(|(key, material)| (key, materials.add(material)))
        .collect();
    let result = layers
        .iter()
        .map(|(layer, prototype)| {
            let lods = prototype.lods.each_ref().map(|primitives| {
                primitives
                    .iter()
                    .map(|primitive| WindVegetationPrimitive {
                        mesh: primitive.mesh.clone(),
                        material: handles
                            [&(primitive.material.id(), prototype.target_height_m.to_bits())]
                            .clone(),
                    })
                    .collect()
            });
            (layer.clone(), lods)
        })
        .collect();
    Ok((result, handles.len()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::vegetation_prototypes::RenderVegetationPrimitive;

    #[test]
    fn wind_cache_shares_lods_preserves_pbr_sources_and_rejects_atomically() {
        let mut images = Assets::<Image>::default();
        let texture = images.add(Image::default());
        let mut pbr = Assets::<StandardMaterial>::default();
        let source = pbr.add(StandardMaterial {
            base_color_texture: Some(texture.clone()),
            normal_map_texture: Some(texture.clone()),
            metallic_roughness_texture: Some(texture.clone()),
            alpha_mode: AlphaMode::Mask(0.4),
            perceptual_roughness: 0.73,
            double_sided: true,
            ..default()
        });
        let primitive = RenderVegetationPrimitive {
            mesh: Handle::default(),
            material: source.clone(),
        };
        let mut layers = BTreeMap::from([(
            "trees".into(),
            RenderVegetationPrototype {
                target_height_m: 5.65,
                lods: std::array::from_fn(|_| vec![primitive.clone()]),
            },
        )]);
        let profile = forge_world::VisualValleyVegetationRecipe::default()
            .build()
            .unwrap()
            .presentation;
        let mut materials = Assets::<VisualValleyVegetationMaterial>::default();
        let (result, count) = build_wind_cache(
            &layers,
            &pbr,
            &mut materials,
            profile,
            VegetationInteractionField::default(),
        )
        .unwrap();
        assert_eq!(count, 1);
        assert_eq!(
            result["trees"][0][0].material,
            result["trees"][2][0].material
        );
        let material = materials.get(&result["trees"][0][0].material).unwrap();
        assert_eq!(material.base.base_color_texture, Some(texture.clone()));
        assert_eq!(material.base.normal_map_texture, Some(texture.clone()));
        assert_eq!(material.base.metallic_roughness_texture, Some(texture));
        assert_eq!(material.base.alpha_mode, AlphaMode::Mask(0.4));
        assert_eq!(
            material.base.perceptual_roughness.to_bits(),
            0.73_f32.to_bits()
        );
        assert_eq!(
            material.extension.prototype_parameters.x.to_bits(),
            1_f32.to_bits()
        );
        assert!(material.base.double_sided);
        assert_eq!(pbr.len(), 1);
        assert_eq!(
            pbr.get(&source).unwrap().perceptual_roughness.to_bits(),
            0.73_f32.to_bits()
        );
        layers.get_mut("trees").unwrap().target_height_m = f32::NAN;
        assert!(
            build_wind_cache(
                &layers,
                &pbr,
                &mut materials,
                profile,
                VegetationInteractionField::default()
            )
            .is_err()
        );
        assert_eq!(materials.len(), 1);
    }
}
