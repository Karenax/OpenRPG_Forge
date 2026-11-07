//! Non-shadowing test material for the shared generic residency contract.
use crate::source_vegetation::{Params, world::SourceMaterial};
use bevy::{
    prelude::*,
    render::{render_resource::AsBindGroup, storage::ShaderBuffer},
};
#[derive(Asset, TypePath, AsBindGroup, Clone, Debug)]
pub struct PlantMaterial {
    #[uniform(0)]
    pub params: Params,
    #[texture(1)]
    #[sampler(2)]
    #[dependency]
    pub detail: Handle<Image>,
    #[texture(3)]
    #[sampler(4)]
    #[dependency]
    pub leaf: Handle<Image>,
    #[texture(5)]
    #[sampler(6)]
    #[dependency]
    pub impostor_a: Handle<Image>,
    #[texture(7)]
    #[dependency]
    pub impostor_b: Handle<Image>,
    #[storage(8, read_only)]
    #[dependency]
    pub instances: Handle<ShaderBuffer>,
    #[storage(9, read_only)]
    #[dependency]
    pub models: Handle<ShaderBuffer>,
    pub kind: u8,
}
impl Material for PlantMaterial {
    fn enable_prepass() -> bool {
        false
    }
    fn enable_shadows() -> bool {
        false
    }
}
impl SourceMaterial for PlantMaterial {
    fn params(&self) -> &Params {
        &self.params
    }
    fn params_mut(&mut self) -> &mut Params {
        &mut self.params
    }
    fn kind(&self) -> u8 {
        self.kind
    }
    fn set_source(
        &mut self,
        kind: u8,
        instances: Handle<ShaderBuffer>,
        models: Handle<ShaderBuffer>,
    ) {
        self.kind = kind;
        self.instances = instances;
        self.models = models;
    }
}
