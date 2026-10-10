//! Device-epoch calibration and fail-closed bindings for declared source materials.
#[cfg(target_arch = "wasm32")]
use super::source_srgb_async::Calibration;
use super::source_srgb_curve::NativeSrgbDeviceKey;
use super::source_srgb_measure::device_key;
#[cfg(not(target_arch = "wasm32"))]
use super::source_srgb_measure::measure;
use super::{SourceGraphExtension, SourceGraphMaterial};
use bevy::{
    asset::RenderAssetUsages,
    prelude::*,
    render::{
        render_resource::{Extent3d, TextureDimension, TextureFormat},
        renderer::{RenderAdapterInfo, RenderDevice, RenderQueue},
    },
};

#[derive(Clone, Debug)]
pub(super) struct NativeSrgbBinding {
    pub(super) handle: Handle<Image>,
    bytes: Vec<u8>,
}
impl NativeSrgbBinding {
    pub(super) fn new(bytes: Vec<u8>, images: &mut Assets<Image>) -> Self {
        let handle = images.add(Image::new(
            Extent3d {
                width: 256,
                height: 1,
                depth_or_array_layers: 1,
            },
            TextureDimension::D2,
            bytes.clone(),
            TextureFormat::R32Float,
            RenderAssetUsages::MAIN_WORLD | RenderAssetUsages::RENDER_WORLD,
        ));
        Self { handle, bytes }
    }
    fn validate(&self, images: &Assets<Image>) -> Result<(), &'static str> {
        let image = images
            .get(&self.handle)
            .ok_or("Native sRGB thresholds are not resident")?;
        let d = &image.texture_descriptor;
        if d.format != TextureFormat::R32Float
            || d.dimension != TextureDimension::D2
            || d.size.width != 256
            || d.size.height != 1
            || d.size.depth_or_array_layers != 1
            || d.mip_level_count != 1
            || d.sample_count != 1
            || self.bytes.len() != 1024
            || image.data.as_deref() != Some(self.bytes.as_slice())
        {
            return Err("Native sRGB threshold storage changed");
        }
        Ok(())
    }
}
#[derive(Resource, Default)]
pub(super) struct NativeSrgbRuntime {
    device: Option<RenderDevice>,
    key: Option<NativeSrgbDeviceKey>,
    binding: Option<NativeSrgbBinding>,
    failed: bool,
    #[cfg(target_arch = "wasm32")]
    calibration: Option<Calibration>,
}
pub(super) fn validate_extension(
    extension: &SourceGraphExtension,
    images: &Assets<Image>,
) -> Result<(), &'static str> {
    if !extension.program_key.requires_native_srgb_encoder
        && extension.native_srgb_binding.is_none()
        && extension.native_srgb_thresholds.is_none()
    {
        return Ok(());
    }
    if !extension.native_srgb_validated {
        return Err("Native sRGB thresholds have not been validated for the current device");
    }
    let binding = extension
        .native_srgb_binding
        .as_ref()
        .ok_or("Required native sRGB thresholds are absent")?;
    if extension.native_srgb_thresholds.as_ref() != Some(&binding.handle) {
        return Err("Native sRGB threshold handle changed");
    }
    binding.validate(images)
}
fn clear_bindings(materials: &mut Assets<SourceGraphMaterial>) {
    let ids: Vec<_> = materials
        .iter()
        .filter(|(_, m)| {
            m.extension.native_srgb_binding.is_some()
                || m.extension.native_srgb_thresholds.is_some()
                || m.extension.native_srgb_validated
        })
        .map(|(id, _)| id)
        .collect();
    for id in ids {
        let mut m = materials.get_mut(id).unwrap();
        m.extension.native_srgb_binding = None;
        m.extension.native_srgb_thresholds = None;
        m.extension.native_srgb_validated = false;
    }
}
/// Runs after authoring updates, before extraction. Unchanged materials are not
/// marked modified every frame. Failure is retained until the GPU epoch changes
/// (or this internal runtime resource is explicitly reset).
pub(super) fn ensure_native_srgb_bindings(
    device: Option<Res<RenderDevice>>,
    queue: Option<Res<RenderQueue>>,
    info: Option<Res<RenderAdapterInfo>>,
    mut runtime: ResMut<NativeSrgbRuntime>,
    mut images: ResMut<Assets<Image>>,
    mut materials: ResMut<Assets<SourceGraphMaterial>>,
) {
    let (Some(device), Some(queue), Some(info)) = (device, queue, info) else {
        clear_bindings(&mut materials);
        *runtime = NativeSrgbRuntime::default();
        return;
    };
    let key = device_key(&info);
    let same = runtime
        .device
        .as_ref()
        .is_some_and(|old| old.wgpu_device() == device.wgpu_device())
        && runtime.key.as_ref() == Some(&key);
    if !same {
        clear_bindings(&mut materials);
        *runtime = NativeSrgbRuntime {
            device: Some(device.clone()),
            key: Some(key.clone()),
            ..Default::default()
        };
    }
    if !materials
        .iter()
        .any(|(_, m)| m.extension.program_key.requires_native_srgb_encoder)
    {
        return;
    }
    if runtime.failed {
        return;
    }
    if let Some(binding) = &runtime.binding {
        if let Err(error) = binding.validate(&images) {
            warn!("Native sRGB binding refused: {error}");
            runtime.failed = true;
            clear_bindings(&mut materials);
            return;
        }
    } else {
        #[cfg(not(target_arch = "wasm32"))]
        let measured = measure(&device, &queue, &info)
            .and_then(|curve| curve.r32f_bytes_for_device(&key).map_err(Into::into))
            .map_err(|error| error.to_string());
        #[cfg(target_arch = "wasm32")]
        let measured = {
            if runtime.calibration.is_none() {
                runtime.calibration = Some(Calibration::new(&device, key.clone()));
            }
            match runtime.calibration.as_mut().unwrap().poll(&device, &queue) {
                Ok(None) => return,
                Ok(Some(curve)) => {
                    runtime.calibration = None;
                    curve.r32f_bytes_for_device(&key).map_err(str::to_string)
                }
                Err(error) => {
                    runtime.calibration = None;
                    Err(error)
                }
            }
        };
        match measured {
            Ok(bytes) => runtime.binding = Some(NativeSrgbBinding::new(bytes, &mut images)),
            Err(error) => {
                warn!("Native sRGB calibration refused: {error}");
                runtime.failed = true;
                clear_bindings(&mut materials);
                return;
            }
        }
    }
    let binding = runtime.binding.as_ref().unwrap();
    let ids: Vec<_> = materials
        .iter()
        .filter(|(_, m)| {
            m.extension.program_key.requires_native_srgb_encoder
                && (m.extension.native_srgb_thresholds.as_ref() != Some(&binding.handle)
                    || !m.extension.native_srgb_validated)
        })
        .map(|(id, _)| id)
        .collect();
    for id in ids {
        let mut m = materials.get_mut(id).unwrap();
        m.extension.native_srgb_binding = Some(binding.clone());
        m.extension.native_srgb_thresholds = Some(binding.handle.clone());
        m.extension.native_srgb_validated = true;
    }
}
