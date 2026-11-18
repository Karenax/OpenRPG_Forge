//! Resource declarations for a source stall, independent of the renderer.
use forge_core::ContentHash;
use serde::Deserialize;
use std::collections::BTreeMap;

pub const ATTRIBUTES: [(&str, usize); 6] = [
    ("position", 3),
    ("normal", 3),
    ("uv", 2),
    ("color", 4),
    ("aLayer", 1),
    ("aUV2", 2),
];
pub const TEXTURES: [&str; 7] = [
    "surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs",
];

#[derive(Clone, Debug, Deserialize)]
pub struct Layout {
    pub geometry: Geometry,
    pub textures: BTreeMap<String, TextureArray>,
}

#[derive(Clone, Debug, Deserialize)]
pub struct Geometry {
    pub attributes: BTreeMap<String, Payload>,
    pub indices: Payload,
    pub triangles: usize,
}

#[derive(Clone, Debug, Deserialize)]
pub struct Payload {
    pub file: String,
    pub bytes: usize,
    pub sha256: ContentHash,
    pub item_size: usize,
    pub count: usize,
}

#[derive(Clone, Debug, Deserialize)]
pub struct TextureArray {
    pub file: String,
    pub bytes: usize,
    pub sha256: ContentHash,
    pub width: u32,
    pub height: u32,
    pub layers: u32,
    pub srgb: bool,
    pub address_mode: String,
    pub mip_status: String,
}

impl Geometry {
    pub fn attribute(&self, name: &str, width: usize) -> Result<&Payload, String> {
        let count = self
            .attributes
            .get("position")
            .ok_or("Missing stall positions")?
            .count;
        let pin = self
            .attributes
            .get(name)
            .ok_or("Missing stall mesh attribute")?;
        if pin.item_size != width
            || pin.count != count
            || count == 0
            || count
                .checked_mul(width)
                .and_then(|size| size.checked_mul(4))
                != Some(pin.bytes)
        {
            return Err("Invalid stall attribute shape".into());
        }
        Ok(pin)
    }

    pub fn indices(&self) -> Result<&Payload, String> {
        let pin = &self.indices;
        if pin.item_size != 4
            || pin.count == 0
            || self.triangles.checked_mul(3) != Some(pin.count)
            || pin.count.checked_mul(4) != Some(pin.bytes)
        {
            return Err("Invalid stall index shape".into());
        }
        Ok(pin)
    }
}

impl Layout {
    pub fn texture(&self, name: &str) -> Result<&TextureArray, String> {
        if self.textures.len() != TEXTURES.len() {
            return Err("Incomplete stall texture arrays".into());
        }
        let pin = self
            .textures
            .get(name)
            .ok_or("Missing stall texture array")?;
        if !matches!(pin.address_mode.as_str(), "repeat" | "clamp") {
            return Err("Invalid stall texture address mode".into());
        }
        if pin.mip_status != "base_level_only_source_gpu_mips_unported" {
            return Err("Stall texture expects base levels".into());
        }
        validate_texture_extent(pin.width, pin.height, pin.layers, pin.bytes)?;
        Ok(pin)
    }

    /// Enumerate exactly the files required to reproduce the declared stall.
    /// Paths are relative to the descriptor; size and digest come from that descriptor.
    pub fn resource_pins(&self) -> Result<BTreeMap<&str, (usize, ContentHash)>, String> {
        if self.geometry.attributes.len() != ATTRIBUTES.len() {
            return Err("Unexpected stall mesh attribute".into());
        }
        let mut inventory = BTreeMap::new();
        for (name, width) in ATTRIBUTES {
            let pin = self.geometry.attribute(name, width)?;
            insert_pin(&mut inventory, &pin.file, (pin.bytes, pin.sha256))?;
        }
        let pin = self.geometry.indices()?;
        insert_pin(&mut inventory, &pin.file, (pin.bytes, pin.sha256))?;
        for name in TEXTURES {
            let pin = self.texture(name)?;
            insert_pin(&mut inventory, &pin.file, (pin.bytes, pin.sha256))?;
        }
        Ok(inventory)
    }
}

fn insert_pin<'a>(
    inventory: &mut BTreeMap<&'a str, (usize, ContentHash)>,
    name: &'a str,
    pin: (usize, ContentHash),
) -> Result<(), String> {
    if name.is_empty()
        || !name
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'.' | b'-' | b'_'))
        || matches!(name, "." | "..")
    {
        return Err(format!("Invalid stall resource name: {name}"));
    }
    if inventory.insert(name, pin).is_some() {
        return Err(format!("Duplicate stall resource: {name}"));
    }
    Ok(())
}

pub fn validate_texture_extent(
    width: u32,
    height: u32,
    layers: u32,
    bytes: usize,
) -> Result<(), String> {
    if !width.is_power_of_two()
        || !height.is_power_of_two()
        || width > 2048
        || height > 2048
        || !(1..=32).contains(&layers)
        || bytes != (width * height * layers * 4) as usize
    {
        return Err("Invalid stall texture array extent or byte length".into());
    }
    Ok(())
}
