use bevy::{asset::UntypedHandle, pbr::Material, prelude::*};
use std::any::TypeId;

pub(super) struct MaterialInspection {
    pub(super) handle: UntypedHandle,
    pub(super) images: Vec<bevy::asset::AssetId<Image>>,
    pub(super) can_bake: bool,
}

type Inspection = Option<Result<MaterialInspection, String>>;
type Inspect = fn(&World, Entity) -> Inspection;

/// A caller-reviewed shader route whose vertices can be baked into a shared
/// metric frame. Optional flags preserve a host's extra rendering components.
#[derive(Clone)]
pub struct StaticHlodMaterial {
    pub(super) family: TypeId,
    pub(super) inspect: Inspect,
    pub(super) insert: fn(&mut World, Entity, UntypedHandle),
    pub(super) flags: fn(&World, Entity) -> u64,
    pub(super) decorate: fn(&mut World, Entity, u64),
}

impl StaticHlodMaterial {
    #[must_use]
    pub fn pose_baked<M: Material>() -> Self {
        Self {
            family: TypeId::of::<M>(),
            inspect: inspect::<M, false>,
            insert: |world, entity, handle| {
                if let Ok(handle) = Handle::<M>::try_from(handle) {
                    world.entity_mut(entity).insert(MeshMaterial3d(handle));
                }
            },
            flags: |_, _| 0,
            decorate: |_, _, _| {},
        }
    }

    /// Admit complete source coverage while retaining original draw geometry.
    /// Use for shaders whose deformation, local coordinates or sorting have no
    /// reviewed pose-baking route. No proxy or GPU replacement is claimed.
    #[must_use]
    pub fn retained_detail<M: Material>() -> Self {
        Self {
            inspect: inspect::<M, true>,
            ..Self::pose_baked::<M>()
        }
    }

    #[must_use]
    pub fn with_render_flags(
        mut self,
        flags: fn(&World, Entity) -> u64,
        decorate: fn(&mut World, Entity, u64),
    ) -> Self {
        self.flags = flags;
        self.decorate = decorate;
        self
    }
}

fn inspect<M: Material, const RETAIN_DETAIL: bool>(world: &World, entity: Entity) -> Inspection {
    let handle = world.get::<MeshMaterial3d<M>>(entity)?.0.clone();
    Some((|| {
        let material = world
            .get_resource::<Assets<M>>()
            .and_then(|assets| assets.get(&handle))
            .ok_or("Static HLOD source material is not loaded")?;
        // Keep transparent originals in the renderer's per-object sort order.
        let can_bake = !RETAIN_DETAIL
            && matches!(
                material.alpha_mode(),
                AlphaMode::Opaque | AlphaMode::Mask(_)
            );
        let mut images = Vec::new();
        crate::render_inventory::visit_material_dependencies(material, &mut |id| {
            if let Ok(image) = id.try_typed::<Image>() {
                images.push(image);
            }
        });
        images.sort_unstable();
        images.dedup();
        Ok(MaterialInspection {
            handle: handle.untyped(),
            images,
            can_bake,
        })
    })())
}
