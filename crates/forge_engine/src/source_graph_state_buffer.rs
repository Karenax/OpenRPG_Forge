//! Host-owned resident state admitted for an existing source material.
use super::SourceGraphExtension;
use bevy::{
    asset::{AssetId, RenderAssetUsages, uuid::Uuid},
    prelude::*,
    render::{render_resource::BufferUsages, storage::ShaderBuffer},
};

// Layout-only resource for shaders that never declare source_state. Required
// state still fails closed, and this identity cannot be admitted as source data.
const UNUSED_STATE_UUID: u128 = 0x464f_5247_455f_554e_5553_4544_5354_4154;

pub(super) fn unused_source_state_handle() -> Handle<ShaderBuffer> {
    Handle::Uuid(Uuid::from_u128(UNUSED_STATE_UUID), std::marker::PhantomData)
}

pub(super) fn install_unused_state_asset(app: &mut App) {
    let mut buffers = app.world_mut().resource_mut::<Assets<ShaderBuffer>>();
    let id = AssetId::Uuid {
        uuid: Uuid::from_u128(UNUSED_STATE_UUID),
    };
    if let Some(existing) = buffers.get(id) {
        assert_eq!(
            existing.data.as_deref(),
            Some([0_u8; 16].as_slice()),
            "Reserved unused source-state identity is occupied"
        );
    } else {
        buffers
            .insert(
                id,
                ShaderBuffer::new(&[0; 16], RenderAssetUsages::default()),
            )
            .expect("Reserved source-state asset identity must be admitted");
    }
}

/// Transient asset identity. Source record layout and provenance remain with
/// the compiler/host, not with this renderer resource.
#[derive(Clone, Debug)]
pub struct SourceGraphStateBufferBinding {
    handle: Handle<ShaderBuffer>,
}

fn admit(
    handle: &Handle<ShaderBuffer>,
    buffers: &Assets<ShaderBuffer>,
) -> Result<(), &'static str> {
    if handle.id() == unused_source_state_handle().id() {
        return Err("Unused material layout resource is not source state");
    }
    let buffer = buffers
        .get(handle)
        .ok_or("Source state buffer asset is absent")?;
    let size = buffer
        .data
        .as_ref()
        .map_or(buffer.buffer_description.size, |bytes| bytes.len() as u64);
    if size == 0
        || size % 16 != 0
        || !buffer
            .buffer_description
            .usage
            .contains(BufferUsages::STORAGE)
        || !buffer.asset_usage.contains(RenderAssetUsages::RENDER_WORLD)
    {
        return Err("Source state buffer exceeds the admitted resident word ABI");
    }
    Ok(())
}

impl SourceGraphStateBufferBinding {
    pub fn new(
        handle: Handle<ShaderBuffer>,
        buffers: &Assets<ShaderBuffer>,
    ) -> Result<Self, &'static str> {
        admit(&handle, buffers)?;
        Ok(Self { handle })
    }

    pub fn validate_residency(&self, buffers: &Assets<ShaderBuffer>) -> Result<(), &'static str> {
        admit(&self.handle, buffers)
    }
}

impl SourceGraphExtension {
    #[must_use]
    pub fn is_source_state_buffer(&self, handle: &Handle<ShaderBuffer>) -> bool {
        self.source_state_admitted && self.source_state == *handle
    }
    /// A later vertex/fragment pass may read the producer's last encoded state.
    /// This does not attest CPU readback or qualify the source effect's rendering.
    pub fn set_source_state_buffer(&mut self, binding: &SourceGraphStateBufferBinding) {
        self.source_state = binding.handle.clone();
        self.source_state_admitted = true;
    }

    pub fn clear_source_state_buffer(&mut self) {
        self.source_state = unused_source_state_handle();
        self.source_state_admitted = false;
    }

    pub fn validate_source_state_buffer(
        &self,
        buffers: &Assets<ShaderBuffer>,
    ) -> Result<(), &'static str> {
        match (
            self.source_state_admitted,
            self.program_key.requires_source_state,
        ) {
            (true, _) => admit(&self.source_state, buffers),
            (false, true) => Err("Required source state buffer is not bound"),
            (false, false) => Ok(()),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn state_admission_requires_a_live_storage_buffer_with_complete_words() {
        let mut buffers = Assets::<ShaderBuffer>::default();
        let valid = buffers.add(ShaderBuffer::new(&[0; 32], RenderAssetUsages::default()));
        let binding = SourceGraphStateBufferBinding::new(valid.clone(), &buffers).unwrap();
        binding.validate_residency(&buffers).unwrap();
        buffers.remove(&valid);
        assert!(binding.validate_residency(&buffers).is_err());
        for bytes in [0, 15, 17] {
            let handle = buffers.add(ShaderBuffer::new(
                &vec![0; bytes],
                RenderAssetUsages::default(),
            ));
            assert!(SourceGraphStateBufferBinding::new(handle, &buffers).is_err());
        }
    }

    #[test]
    fn non_storage_or_cpu_only_assets_cannot_supply_a_render_state() {
        let mut buffers = Assets::<ShaderBuffer>::default();
        let mut buffer = ShaderBuffer::new(&[0; 16], RenderAssetUsages::default());
        buffer.buffer_description.usage = BufferUsages::COPY_SRC;
        let handle = buffers.add(buffer);
        assert!(SourceGraphStateBufferBinding::new(handle, &buffers).is_err());
        let handle = buffers.add(ShaderBuffer::new(&[0; 16], RenderAssetUsages::MAIN_WORLD));
        assert!(SourceGraphStateBufferBinding::new(handle, &buffers).is_err());
    }

    #[test]
    fn unused_layout_resource_is_never_admitted_as_required_source_state() {
        let mut buffers = Assets::<ShaderBuffer>::default();
        buffers
            .insert(
                unused_source_state_handle().id(),
                ShaderBuffer::new(&[0; 16], RenderAssetUsages::default()),
            )
            .unwrap();
        assert!(
            SourceGraphStateBufferBinding::new(unused_source_state_handle(), &buffers).is_err()
        );
    }
}
