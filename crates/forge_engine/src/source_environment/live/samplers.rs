// SPDX-License-Identifier: MIT
// Source sampler contract from Tidewater engine/gpu/GPU.js and Shader.js,
// revision 1438b1ab.
// Copyright 2026 DRG Software Solutions LLC; third_party/tidewater/LICENSE.
pub(super) fn create(device: &wgpu::Device) -> Vec<wgpu::Sampler> {
    (0..9)
        .map(|index| {
            let mut desc = wgpu::SamplerDescriptor::default();
            if index < 6 {
                desc.mag_filter = wgpu::FilterMode::Linear;
                desc.min_filter = wgpu::FilterMode::Linear;
                desc.mipmap_filter = wgpu::MipmapFilterMode::Linear;
            }
            if [0, 3, 5, 7].contains(&index) {
                desc.address_mode_u = wgpu::AddressMode::Repeat;
                desc.address_mode_v = wgpu::AddressMode::Repeat;
                desc.address_mode_w = wgpu::AddressMode::Repeat;
            }
            if index == 2 {
                desc.address_mode_u = wgpu::AddressMode::MirrorRepeat;
                desc.address_mode_v = wgpu::AddressMode::MirrorRepeat;
                desc.address_mode_w = wgpu::AddressMode::MirrorRepeat;
            }
            desc.anisotropy_clamp = match index {
                3 | 4 => 8,
                5 => 4,
                _ => 1,
            };
            if index == 8 {
                desc.mag_filter = wgpu::FilterMode::Linear;
                desc.min_filter = wgpu::FilterMode::Linear;
                desc.compare = Some(wgpu::CompareFunction::Less);
            }
            device.create_sampler(&desc)
        })
        .collect()
}
