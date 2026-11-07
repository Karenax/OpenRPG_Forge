//! Stable glTF node identity and its original local frame, shared by scene hosts.
//!
//! This is transient ECS state derived while loading a verified model. The
//! model bytes and the authoring document remain unchanged.
use bevy::{
    asset::{AssetPath, LoadContext},
    ecs::world::EntityWorldMut,
    gltf::{
        GltfLoaderSettings,
        extensions::{ErasedGltfExtensionHandler, GltfExtensionHandler, GltfExtensionHandlers},
    },
    prelude::*,
};
use std::{
    collections::{BTreeSet, HashMap},
    sync::{Arc, RwLock},
};

/// Original node index within one pinned model and its immutable local frame.
#[derive(Component, Clone, Copy, Debug, Reflect)]
#[reflect(Component, Clone)]
pub struct ModelNodeIdentity {
    index: u64,
    rest: Transform,
}

impl ModelNodeIdentity {
    pub(crate) const fn new(index: u64, rest: Transform) -> Self {
        Self { index, rest }
    }

    #[must_use]
    pub const fn index(self) -> u64 {
        self.index
    }

    #[must_use]
    pub const fn rest(self) -> Transform {
        self.rest
    }
}

/// Install before models are requested; Bevy's ordinary and source-fragment
/// loaders share the same handler registry.
pub struct ModelNodeIdentityPlugin;

/// Only explicitly selected nodes receive an identity. Ordinary models and
/// unselected nodes remain untouched.
#[derive(Resource, Clone, Default)]
pub struct ModelNodeIdentityTracking(Arc<RwLock<HashMap<AssetPath<'static>, BTreeSet<u64>>>>);

impl ModelNodeIdentityTracking {
    /// Request the union of needed identities before loading the model. An
    /// already loaded model must be reloaded and its scene reinstantiated if
    /// another node is requested. Asset sources remain distinct; labels do not.
    pub fn track_before_load(
        &self,
        mut path: AssetPath<'_>,
        node_indices: &[u64],
    ) -> Result<bool, String> {
        let requested: BTreeSet<_> = node_indices.iter().copied().collect();
        if requested.is_empty() || requested.len() != node_indices.len() {
            return Err("Model node tracking requires unique selected nodes".into());
        }
        path.remove_label();
        let path = path.into_owned();
        let mut models = self
            .0
            .write()
            .map_err(|_| "Model node tracking lock is poisoned".to_owned())?;
        let tracked = models.entry(path).or_default();
        let before = tracked.len();
        tracked.extend(requested);
        Ok(tracked.len() != before)
    }
}

impl Plugin for ModelNodeIdentityPlugin {
    fn build(&self, app: &mut App) {
        app.register_type::<ModelNodeIdentity>()
            .init_resource::<GltfExtensionHandlers>()
            .init_resource::<ModelNodeIdentityTracking>();
        let handlers = app.world().resource::<GltfExtensionHandlers>().0.clone();
        let tracking = app.world().resource::<ModelNodeIdentityTracking>().clone();
        let handler = NodeIdentityHandler {
            tracking,
            selected: BTreeSet::new(),
        };
        #[cfg(not(target_family = "wasm"))]
        handlers.write_blocking().push(Box::new(handler));
        #[cfg(target_family = "wasm")]
        bevy::tasks::block_on(async {
            handlers.write().await.push(Box::new(handler));
        });
    }
}

#[derive(Clone)]
struct NodeIdentityHandler {
    tracking: ModelNodeIdentityTracking,
    selected: BTreeSet<u64>,
}

impl GltfExtensionHandler for NodeIdentityHandler {
    fn dyn_clone(&self) -> Box<dyn ErasedGltfExtensionHandler> {
        Box::new(self.clone())
    }

    fn on_root(
        &mut self,
        context: &mut LoadContext<'_>,
        _gltf: &gltf::Gltf,
        _settings: &GltfLoaderSettings,
    ) {
        self.selected = self
            .tracking
            .0
            .read()
            .ok()
            .and_then(|tracked| tracked.get(&context.path().without_label()).cloned())
            .unwrap_or_default();
    }

    fn on_gltf_node(
        &mut self,
        _context: &mut LoadContext<'_>,
        node: &gltf::Node<'_>,
        entity: &mut EntityWorldMut<'_>,
    ) {
        if self.selected.is_empty() {
            return;
        }
        let index = u64::try_from(node.index()).expect("glTF node index fits u64");
        if !self.selected.contains(&index) {
            return;
        }
        let rest = *entity
            .get::<Transform>()
            .expect("Bevy glTF nodes have a local transform");
        entity.insert(ModelNodeIdentity::new(index, rest));
    }
}

#[cfg(all(test, feature = "physics-rapier", not(target_arch = "wasm32")))]
mod tests;
