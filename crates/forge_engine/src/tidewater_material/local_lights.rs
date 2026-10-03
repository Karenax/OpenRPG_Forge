// SPDX-License-Identifier: MIT
// Adapted from Tidewater LocalLights.js at 1438b1abfcaee3267092b75573014f4d9b4a983c.
// Copyright 2026 DRG Software Solutions LLC; see third_party/tidewater/LICENSE.
//! Building receiver for the source local-light packet. Scene adapters must
//! publish a current camera packet; the neutral default does not invent lamps.
use crate::source_local_lights::{Error, FramePacket, Packet};
use crate::tidewater_temporal::frame::FrameTag;
use bevy::{prelude::*, render::render_resource::ShaderType};
pub mod gpu;

#[derive(Clone, Debug, ShaderType)]
pub struct Uniform {
    pub positions: [Vec4; 8],
    pub colors: [Vec4; 8],
    pub directions: [Vec4; 8],
    pub count: i32,
    pub flash_on: f32,
    pub flash_cone: Vec2,
    pub flash_position: Vec3,
    pub flash_direction: Vec3,
    pub flash_color: Vec3,
}

impl Default for Uniform {
    fn default() -> Self {
        Self::from_packet(&Packet::default()).expect("The source neutral packet is valid")
    }
}

impl Uniform {
    /// Match the actual prepared view, including its serial, before replacing a
    /// material uniform. No implicit current camera or old frame fallback.
    pub fn from_frame(frame: &FramePacket, expected: FrameTag) -> Result<Self, Error> {
        if frame.tag() != expected {
            return Err(Error::StaleFrame);
        }
        Self::from_packet(frame.packet())
    }
    /// Use precisely the existing checked f64-to-f32 packet conversion. Failure
    /// occurs before a caller can replace any material's resident uniform.
    pub fn from_packet(packet: &Packet) -> Result<Self, Error> {
        let bytes = packet.pack()?;
        let scalar = |offset| f32::from_le_bytes(bytes[offset..offset + 4].try_into().unwrap());
        Ok(Self {
            positions: packet.positions.map(bevy::math::DVec4::as_vec4),
            colors: packet.colors.map(bevy::math::DVec4::as_vec4),
            directions: packet.directions.map(bevy::math::DVec4::as_vec4),
            count: packet.count,
            flash_on: scalar(388),
            flash_cone: packet.flash_cone.as_vec2(),
            flash_position: packet.flash_position.as_vec3(),
            flash_direction: packet.flash_direction.as_vec3(),
            flash_color: packet.flash_color.as_vec3(),
        })
    }
}

pub(super) fn shader_source(source: &str) -> Result<String, String> {
    const MARKER: &str = "+s.emissive;";
    if !source.contains("fn sourceSceneLight(")
        || source.matches(MARKER).count() != 1
        || source.contains("fn villageLocalLights(")
    {
        return Err("Building shader lacks the unique local-light receiver seam".into());
    }
    Ok(format!(
        "{}\n{}",
        source.replace(MARKER, "+villageLocalLights(s,P,N,V)+s.emissive;"),
        include_str!("local_lights.wgsl")
    ))
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::math::{DVec2, DVec3, DVec4};
    use bevy::render::render_resource::encase::{StorageBuffer, UniformBuffer};

    #[test]
    fn local_lights_material_uniform_matches_all_source_packet_bytes() {
        let mut packet = Packet::default();
        packet.count = 8;
        packet.flash_on = 1.0;
        packet.flash_cone = DVec2::new(0.9, 0.7);
        packet.flash_position = DVec3::new(-1.25, 3.5, 200.0);
        packet.flash_direction = DVec3::new(0.1, 0.2, -0.9);
        packet.flash_color = DVec3::new(12.0, 14.5, 7.25);
        for (index, value) in packet.positions.iter_mut().enumerate() {
            *value = DVec4::new(f64::from(u32::try_from(index).unwrap()), -2.5, 300.0, 121.0);
        }
        packet.colors.fill(DVec4::new(0.71, 0.33, 0.22, -1.5));
        packet.directions.fill(DVec4::new(0.0, -1.0, 0.0, -2.0));
        let mut buffer = UniformBuffer::new(Vec::<u8>::new());
        buffer
            .write(&Uniform::from_packet(&packet).unwrap())
            .unwrap();
        assert_eq!(buffer.into_inner(), packet.pack().unwrap());
        let mut storage = StorageBuffer::new(Vec::<u8>::new());
        storage
            .write(&Uniform::from_packet(&packet).unwrap())
            .unwrap();
        assert_eq!(storage.into_inner(), packet.pack().unwrap());
        packet.colors[3].x = f64::INFINITY;
        assert!(Uniform::from_packet(&packet).is_err());
        packet.colors[3].x = f64::from(f32::MAX) * 2.0;
        assert!(Uniform::from_packet(&packet).is_err());
        packet.colors[3].x = 1.0;
        packet.count = 9;
        assert!(Uniform::from_packet(&packet).is_err());
    }

    #[test]
    fn local_lights_building_receiver_parses_and_refuses_shader_drift() {
        let fixture = r"
const INV_PI: f32 = 0.318309886;
struct LightingSurface { albedo: vec3f, metalness: f32, specularIntensity: f32, roughness: f32, emissive: vec3f }
struct Water { tiles_m: vec4f, water_absorption: vec4f, water_scattering: vec4f }
@group(1) @binding(48) var<uniform> villageUnderwaterParams: Water;
fn BRDF_GGX(L:vec3f,V:vec3f,N:vec3f,f0:vec3f,f90:f32,r:f32)->vec3f { return f0; }
fn sourceSceneLight(s:LightingSurface,V:vec3f,P:vec3f,pixel:vec2f)->vec3f {
let N=vec3f(0.0,1.0,0.0); return vec3f(0.0)+s.emissive;
}
@fragment fn fragment()->@location(0) vec4f {
let s=LightingSurface(vec3f(1.0),0.0,1.0,0.5,vec3f(0.0));
return vec4f(sourceSceneLight(s,vec3f(0.0,0.0,1.0),vec3f(0.0),vec2f(0.0)),1.0);
}";
        let source = shader_source(fixture).unwrap();
        let module =
            naga::front::wgsl::parse_str(&source.replace("#{MATERIAL_BIND_GROUP}", "1")).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert!(source.contains("+villageLocalLights(s,P,N,V)+s.emissive;"));
        assert!(shader_source(&source).is_err());
        assert!(shader_source(&fixture.replace("+s.emissive;", "+s.emissive*0.5;")).is_err());
        assert!(shader_source(&format!("{fixture}\n+s.emissive;")).is_err());
    }
}
