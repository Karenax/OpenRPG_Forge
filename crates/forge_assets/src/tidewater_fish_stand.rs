//! Hash-pinned `FishStand` input resources, independent of the Bevy renderer.
//!
//! The Tidewater stand is a composite source asset, not a GLB substitute and
//! not an entry in the imported-building catalogue.  This reader preserves one
//! stable placement identity plus the preview-bundle pin while exposing the
//! exact descriptor, mesh attributes and seven texture arrays to a renderer
//! adapter.  It does not create entities, collisions, or a Studio UI route.
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

const SOURCE_COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const ASSET_ID_SEED: &str = "tidewater/fish-stand/v1";
#[cfg(test)]
const BUNDLE_011_SHA256: &str = "c739c737914261991fd9aba21c4372e7e9cafa31527f7a4139001a4db8e9b335";
const DESCRIPTOR: &str = "fish-stand/fish-stand.json";
const CONTENT_DESCRIPTOR: &str = "fish-stand-content/content.json";
const FISH_DESCRIPTOR: &str = "fish-stand-fish/fish.json";
const JOE_RENDER: &str = "scene/joe.glb";
const JOE_SOURCE: &str = "source/public/models/characters/joe.glb";
const ROCKETBOX_LICENSE: &str = "source/public/models/characters/LICENSE-Rocketbox.md";
#[cfg(test)]
const JOE_RENDER_SHA256: &str = "6a859d795aab742c39a92163a17687f93036973184455562d917dff3af7432e4";
#[cfg(test)]
const JOE_SOURCE_SHA256: &str = "da3efce5a452d3c17f40b8d33bf449f565c0a6bef93602dc3e74364fb64081ca";
#[cfg(test)]
const ROCKETBOX_LICENSE_SHA256: &str =
    "a388bf32e3c2f6b02c30228c0896893c82aa678a00b96614a848e358c6da3a12";
const SPECIES: [&str; 5] = ["jack", "redSnapper", "yellowtail", "grunt", "mullet"];
const PROVENANCE: [&str; 4] = [
    "fish-stand/source/LICENSE-Tidewater.txt",
    "fish-stand/source/FishStand.js",
    "fish-stand/source/StallKit.js",
    "fish-stand/source/props/CREDITS.md",
];
const ATTRIBUTES: [&str; 6] = ["position", "normal", "uv", "color", "aLayer", "aUV2"];
const TEXTURES: [&str; 7] = [
    "surf_a", "surf_n", "surf_r", "prop_a", "prop_n", "prop_r", "signs",
];

/// Stable identity persisted by Studio placements for the composite asset.
#[must_use]
pub fn asset_id() -> AssetId {
    AssetId::deterministic(ASSET_ID_SEED)
}

#[derive(Clone, Copy, Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    sha256: ContentHash,
    bytes: usize,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct PreviewBundle {
    schema: String,
    version: u32,
    source_commit: String,
    area_id: String,
    building_package_sha256: ContentHash,
    scope: String,
    files: BTreeMap<String, Entry>,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Descriptor {
    schema: String,
    version: u32,
    source_commit: String,
    source_export_manifest_sha256: ContentHash,
    placement: Placement,
    geometry: Geometry,
    textures: BTreeMap<String, Texture>,
    material: Material,
    licenses: Licenses,
}

#[derive(Clone, Deserialize)]
#[serde(deny_unknown_fields)]
struct Placement {
    x: f32,
    z: f32,
    yaw_radians: f32,
    y: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Licenses {
    tidewater: String,
    props: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Geometry {
    attributes: BTreeMap<String, Payload>,
    indices: Payload,
    triangles: usize,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Payload {
    file: String,
    bytes: usize,
    sha256: ContentHash,
    item_size: usize,
    count: usize,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Texture {
    file: String,
    bytes: usize,
    sha256: ContentHash,
    width: u32,
    height: u32,
    layers: u32,
    srgb: bool,
    address_mode: String,
    mip_status: String,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Material {
    source_wgsl: String,
    status: String,
    limitations: Vec<String>,
}

#[derive(Deserialize)]
struct Source {
    commit: String,
}

#[derive(Deserialize)]
struct ContentDescriptor {
    schema: String,
    version: u32,
    source: Source,
    vendor: Vendor,
    display: Display,
    licenses: ContentLicenses,
    source_files: BTreeMap<String, Entry>,
}

#[derive(Deserialize)]
struct Vendor {
    id: String,
    kind: String,
    placement: VendorPlacement,
    character: Character,
}

#[derive(Deserialize)]
struct VendorPlacement {
    #[serde(rename = "local_x")]
    x: f32,
    #[serde(rename = "local_y")]
    y: f32,
    #[serde(rename = "local_z")]
    z: f32,
}

#[derive(Deserialize)]
struct Character {
    file: String,
    idle: String,
    license: String,
    license_file: String,
}

#[derive(Deserialize)]
struct ContentLicenses {
    tidewater_code: License,
    joe: License,
}

#[derive(Deserialize)]
struct License {
    spdx: String,
    file: String,
}

#[derive(Deserialize)]
struct Display {
    fish_on_ice: Vec<DisplayFish>,
}

#[derive(Deserialize)]
struct DisplayFish {
    species: String,
    length_meters: f32,
    local_x: f32,
    local_z: f32,
    local_yaw_radians: f32,
    pose: String,
}

#[derive(Deserialize)]
struct FishDescriptor {
    schema: String,
    version: u32,
    source: Source,
    source_files: BTreeMap<String, FishSourceFile>,
    fish: Vec<FishInput>,
}

#[derive(Deserialize)]
struct FishSourceFile {
    file: String,
    bytes: usize,
    sha256: ContentHash,
}

#[derive(Deserialize)]
struct FishInput {
    species: String,
    length_meters: f32,
    local_x: f32,
    local_z: f32,
    local_yaw_radians: f32,
    pose: String,
    index: usize,
    rest_height_meters: f32,
    geometry: FishGeometry,
}

#[derive(Deserialize)]
struct FishGeometry {
    position: Payload,
    normal: Payload,
    a_data: Payload,
    indices: Payload,
    triangles: usize,
}

/// Validated source placement and geometry keys for one displayed fish.
#[derive(Clone, Debug)]
pub struct FishStandFish {
    pub species: String,
    pub length_meters: f32,
    pub local_x: f32,
    pub local_z: f32,
    pub local_yaw_radians: f32,
    pub pose: String,
    pub rest_height_meters: f32,
    pub position_key: String,
    pub normal_key: String,
    pub indices_key: String,
    pub vertices: usize,
    pub triangles: usize,
}

/// A persisted reference uses this stable identity and the returned bundle pin.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct FishStandReference {
    pub asset_id: AssetId,
    pub bundle_hash: ContentHash,
}

/// Verified renderer inputs. Files use the descriptor's local names so the
/// engine adapter can consume them without an alternate scene representation.
pub struct FishStandPackage {
    placement: Placement,
    reference: FishStandReference,
    files: BTreeMap<String, Vec<u8>>,
    provenance: BTreeMap<String, ContentHash>,
    content_descriptor: Vec<u8>,
    fish_content_descriptor: Vec<u8>,
    joe_glb: Vec<u8>,
    fish: Vec<FishStandFish>,
    fish_payloads: BTreeMap<String, Vec<u8>>,
    source_joe_hash: ContentHash,
    rocketbox_license_hash: ContentHash,
    source_export_manifest_hash: ContentHash,
    building_package_hash: ContentHash,
}

impl FishStandPackage {
    /// Validates the full preview-bundle pin before any payload request, then
    /// verifies descriptor-declared mesh and texture bytes against both pins.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let bundle = parse_manifest(manifest, expected)?;
        let descriptor_entry = entry(&bundle, DESCRIPTOR)?;
        let descriptor_bytes = read(DESCRIPTOR, descriptor_entry.bytes)?;
        verify(DESCRIPTOR, descriptor_entry, &descriptor_bytes)?;
        let descriptor: Descriptor =
            serde_json::from_slice(&descriptor_bytes).map_err(|error| error.to_string())?;
        validate_descriptor(&descriptor)?;

        let mut files = BTreeMap::new();
        files.insert("fish-stand.json".into(), descriptor_bytes);
        for payload in descriptor
            .geometry
            .attributes
            .values()
            .chain(std::iter::once(&descriptor.geometry.indices))
        {
            load_payload(&bundle, &mut read, &mut files, payload)?;
        }
        for texture in descriptor.textures.values() {
            load_texture(&bundle, &mut read, &mut files, texture)?;
        }

        let mut provenance = BTreeMap::new();
        for name in PROVENANCE {
            let source = entry(&bundle, name)?;
            let bytes = read(name, source.bytes)?;
            verify(name, source, &bytes)?;
            if std::str::from_utf8(&bytes)
                .map_err(|error| error.to_string())?
                .trim()
                .is_empty()
            {
                return Err(format!("Empty FishStand provenance file: {name}"));
            }
            provenance.insert(name.to_owned(), source.sha256);
        }
        let content = load_content(&bundle, &mut read, &mut provenance)?;
        Ok(Self {
            placement: descriptor.placement,
            reference: FishStandReference {
                asset_id: asset_id(),
                bundle_hash: expected,
            },
            files,
            provenance,
            content_descriptor: content.content_descriptor,
            fish_content_descriptor: content.fish_descriptor,
            joe_glb: content.joe_glb,
            fish: content.fish,
            fish_payloads: content.fish_payloads,
            source_joe_hash: content.source_joe_hash,
            rocketbox_license_hash: content.rocketbox_license_hash,
            source_export_manifest_hash: descriptor.source_export_manifest_sha256,
            building_package_hash: bundle.building_package_sha256,
        })
    }

    /// Sequential browser transport. The manifest pin is checked before the
    /// first payload request, and every requested payload is checked before
    /// continuing. Callers may retain the returned package for renderer
    /// preparation; this method does not reread it during placement edits.
    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let bundle = parse_manifest(manifest, expected)?;
        let descriptor_entry = entry(&bundle, DESCRIPTOR)?;
        let descriptor_bytes = read(DESCRIPTOR.to_owned(), descriptor_entry.bytes).await?;
        verify(DESCRIPTOR, descriptor_entry, &descriptor_bytes)?;
        let descriptor: Descriptor =
            serde_json::from_slice(&descriptor_bytes).map_err(|error| error.to_string())?;
        validate_descriptor(&descriptor)?;

        let mut files = BTreeMap::new();
        files.insert("fish-stand.json".into(), descriptor_bytes);
        for payload in descriptor
            .geometry
            .attributes
            .values()
            .chain(std::iter::once(&descriptor.geometry.indices))
        {
            load_payload_async(&bundle, &mut read, &mut files, payload).await?;
        }
        for texture in descriptor.textures.values() {
            load_texture_async(&bundle, &mut read, &mut files, texture).await?;
        }

        let mut provenance = BTreeMap::new();
        for name in PROVENANCE {
            let source = entry(&bundle, name)?;
            let bytes = read(name.to_owned(), source.bytes).await?;
            verify(name, source, &bytes)?;
            if std::str::from_utf8(&bytes)
                .map_err(|error| error.to_string())?
                .trim()
                .is_empty()
            {
                return Err(format!("Empty FishStand provenance file: {name}"));
            }
            provenance.insert(name.to_owned(), source.sha256);
        }
        let content = load_content_async(&bundle, &mut read, &mut provenance).await?;
        Ok(Self {
            placement: descriptor.placement,
            reference: FishStandReference {
                asset_id: asset_id(),
                bundle_hash: expected,
            },
            files,
            provenance,
            content_descriptor: content.content_descriptor,
            fish_content_descriptor: content.fish_descriptor,
            joe_glb: content.joe_glb,
            fish: content.fish,
            fish_payloads: content.fish_payloads,
            source_joe_hash: content.source_joe_hash,
            rocketbox_license_hash: content.rocketbox_license_hash,
            source_export_manifest_hash: descriptor.source_export_manifest_sha256,
            building_package_hash: bundle.building_package_sha256,
        })
    }

    /// Verified source pose; the terrain adapter resolves its height policy.
    #[must_use]
    pub const fn source_pose(&self) -> [f32; 3] {
        [
            self.placement.x,
            self.placement.z,
            self.placement.yaw_radians,
        ]
    }

    #[must_use]
    pub const fn reference(&self) -> FishStandReference {
        self.reference
    }

    #[must_use]
    pub const fn files(&self) -> &BTreeMap<String, Vec<u8>> {
        &self.files
    }

    #[must_use]
    pub const fn provenance(&self) -> &BTreeMap<String, ContentHash> {
        &self.provenance
    }

    /// The source-authored vendor and display specification from bundle 011.
    #[must_use]
    pub fn content_descriptor(&self) -> &[u8] {
        &self.content_descriptor
    }

    /// The source-derived geometry and placement specification for five fish.
    #[must_use]
    pub fn fish_content_descriptor(&self) -> &[u8] {
        &self.fish_content_descriptor
    }

    /// The PNG-embedded derivative loaded by Bevy. The original is verified
    /// separately against the MIT source record and retained as provenance.
    #[must_use]
    pub fn joe_glb(&self) -> &[u8] {
        &self.joe_glb
    }

    #[must_use]
    pub fn fish(&self) -> &[FishStandFish] {
        &self.fish
    }

    /// Verified binary payloads, keyed as `species/file`.
    #[must_use]
    pub const fn fish_payloads(&self) -> &BTreeMap<String, Vec<u8>> {
        &self.fish_payloads
    }

    #[must_use]
    pub const fn source_joe_hash(&self) -> ContentHash {
        self.source_joe_hash
    }

    #[must_use]
    pub const fn rocketbox_license_hash(&self) -> ContentHash {
        self.rocketbox_license_hash
    }

    /// Pins that relate the composite input to its source export and its
    /// sibling imported-building package; neither changes this asset's identity.
    #[must_use]
    pub const fn source_export_manifest_hash(&self) -> ContentHash {
        self.source_export_manifest_hash
    }

    #[must_use]
    pub const fn building_package_hash(&self) -> ContentHash {
        self.building_package_hash
    }
}

struct ContentPlan {
    content_descriptor: Vec<u8>,
    fish_descriptor: Vec<u8>,
    fish: Vec<FishStandFish>,
    requests: BTreeMap<String, Entry>,
    source_joe_hash: ContentHash,
    rocketbox_license_hash: ContentHash,
}

struct ContentLoaded {
    content_descriptor: Vec<u8>,
    fish_descriptor: Vec<u8>,
    joe_glb: Vec<u8>,
    fish: Vec<FishStandFish>,
    fish_payloads: BTreeMap<String, Vec<u8>>,
    source_joe_hash: ContentHash,
    rocketbox_license_hash: ContentHash,
}

fn load_content(
    bundle: &PreviewBundle,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    provenance: &mut BTreeMap<String, ContentHash>,
) -> Result<ContentLoaded, String> {
    let content = read_named(bundle, read, CONTENT_DESCRIPTOR)?;
    let fish = read_named(bundle, read, FISH_DESCRIPTOR)?;
    let plan = prepare_content(bundle, content, fish)?;
    let mut payloads = BTreeMap::new();
    for (name, expected) in &plan.requests {
        let bytes = read(name, expected.bytes)?;
        verify(name, expected, &bytes)?;
        payloads.insert(name.clone(), bytes);
    }
    finish_content(plan, payloads, provenance)
}

async fn load_content_async<F, Fut>(
    bundle: &PreviewBundle,
    read: &mut F,
    provenance: &mut BTreeMap<String, ContentHash>,
) -> Result<ContentLoaded, String>
where
    F: FnMut(String, usize) -> Fut,
    Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
{
    let content = read_named_async(bundle, read, CONTENT_DESCRIPTOR).await?;
    let fish = read_named_async(bundle, read, FISH_DESCRIPTOR).await?;
    let plan = prepare_content(bundle, content, fish)?;
    let mut payloads = BTreeMap::new();
    for (name, expected) in &plan.requests {
        let bytes = read(name.clone(), expected.bytes).await?;
        verify(name, expected, &bytes)?;
        payloads.insert(name.clone(), bytes);
    }
    finish_content(plan, payloads, provenance)
}

fn read_named(
    bundle: &PreviewBundle,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    name: &str,
) -> Result<Vec<u8>, String> {
    let expected = entry(bundle, name)?;
    let bytes = read(name, expected.bytes)?;
    verify(name, expected, &bytes)?;
    Ok(bytes)
}

async fn read_named_async<F, Fut>(
    bundle: &PreviewBundle,
    read: &mut F,
    name: &str,
) -> Result<Vec<u8>, String>
where
    F: FnMut(String, usize) -> Fut,
    Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
{
    let expected = entry(bundle, name)?;
    let bytes = read(name.to_owned(), expected.bytes).await?;
    verify(name, expected, &bytes)?;
    Ok(bytes)
}

fn prepare_content(
    bundle: &PreviewBundle,
    content_descriptor: Vec<u8>,
    fish_descriptor: Vec<u8>,
) -> Result<ContentPlan, String> {
    let content: ContentDescriptor =
        serde_json::from_slice(&content_descriptor).map_err(|error| error.to_string())?;
    let fish_source: FishDescriptor =
        serde_json::from_slice(&fish_descriptor).map_err(|error| error.to_string())?;
    validate_content_descriptors(&content, &fish_source)?;
    let source_joe = content
        .source_files
        .get(JOE_SOURCE)
        .ok_or("Missing original Joe provenance")?;
    let license = content
        .source_files
        .get(ROCKETBOX_LICENSE)
        .ok_or("Missing Rocketbox license")?;
    let mut requests = BTreeMap::new();
    for (relative, expected) in &content.source_files {
        let name = format!("fish-stand-content/{relative}");
        request(bundle, &mut requests, &name, *expected)?;
    }
    let joe = entry(bundle, JOE_RENDER)?;
    requests.insert(JOE_RENDER.to_owned(), *joe);
    let fish = prepare_fish_entries(bundle, &fish_source, &content, &mut requests)?;
    if requests.len() != 32 {
        return Err("Incomplete FishStand content request set".into());
    }
    Ok(ContentPlan {
        content_descriptor,
        fish_descriptor,
        fish,
        requests,
        source_joe_hash: source_joe.sha256,
        rocketbox_license_hash: license.sha256,
    })
}

fn validate_content_descriptors(
    content: &ContentDescriptor,
    fish_source: &FishDescriptor,
) -> Result<(), String> {
    if content.schema != "forge.tidewater.fish-stand-content-source-export"
        || content.version != 1
        || content.source.commit != SOURCE_COMMIT
        || content.vendor.id != "tidewater:fish-stand:joe"
        || content.vendor.kind != "buyer"
        || content.vendor.character.file != JOE_SOURCE
        || content.vendor.character.idle != "idle_neutral_01"
        || content.vendor.character.license != "MIT"
        || content.vendor.character.license_file != ROCKETBOX_LICENSE
        || content.licenses.tidewater_code.spdx != "MIT"
        || content.licenses.tidewater_code.file != "source/LICENSE"
        || content.licenses.joe.spdx != "MIT"
        || content.licenses.joe.file != ROCKETBOX_LICENSE
        || [
            content.vendor.placement.x,
            content.vendor.placement.y,
            content.vendor.placement.z,
        ]
        .iter()
        .any(|value| !value.is_finite())
        || content.vendor.placement.x.to_bits() != 0.2_f32.to_bits()
        || content.vendor.placement.y.to_bits() != 0.06_f32.to_bits()
        || content.vendor.placement.z.to_bits() != 0.12_f32.to_bits()
        || content.source_files.len() != 11
        || fish_source.schema != "forge.tidewater.fish-stand-fish-source-export"
        || fish_source.version != 1
        || fish_source.source.commit != SOURCE_COMMIT
        || fish_source.fish.len() != 5
        || content.display.fish_on_ice.len() != 5
    {
        return Err("Unsupported FishStand content descriptor".into());
    }
    let source_pairs = [
        ("FishGeometry.js", "source/src/world/fish/FishGeometry.js"),
        ("FishSpecies.js", "source/src/world/fish/FishSpecies.js"),
        ("FishStand.js", "source/src/game/FishStand.js"),
        ("CatchDisplay.js", "source/src/game/CatchDisplay.js"),
        ("LICENSE", "source/LICENSE"),
    ];
    if fish_source.source_files.len() != source_pairs.len() {
        return Err("Incomplete fish source provenance".into());
    }
    for (key, relative) in source_pairs {
        let recorded = fish_source
            .source_files
            .get(key)
            .ok_or("Missing fish source provenance")?;
        let inherited = content
            .source_files
            .get(relative)
            .ok_or("Missing shared fish source provenance")?;
        if recorded.file != key
            || recorded.bytes != inherited.bytes
            || recorded.sha256 != inherited.sha256
        {
            return Err(format!("Fish source provenance differs: {key}"));
        }
    }
    Ok(())
}

fn prepare_fish_entries(
    bundle: &PreviewBundle,
    fish_source: &FishDescriptor,
    content: &ContentDescriptor,
    requests: &mut BTreeMap<String, Entry>,
) -> Result<Vec<FishStandFish>, String> {
    fish_source
        .fish
        .iter()
        .zip(&content.display.fish_on_ice)
        .enumerate()
        .map(|(index, (item, display))| prepare_fish_entry(bundle, requests, index, item, display))
        .collect()
}

fn prepare_fish_entry(
    bundle: &PreviewBundle,
    requests: &mut BTreeMap<String, Entry>,
    index: usize,
    item: &FishInput,
    display: &DisplayFish,
) -> Result<FishStandFish, String> {
    if item.species != SPECIES[index]
        || item.index != index
        || display.species != item.species
        || display.length_meters.to_bits() != item.length_meters.to_bits()
        || display.local_x.to_bits() != item.local_x.to_bits()
        || display.local_z.to_bits() != item.local_z.to_bits()
        || display.local_yaw_radians.to_bits() != item.local_yaw_radians.to_bits()
        || display.pose != item.pose
        || !matches!(item.pose.as_str(), "side" | "sideFlip")
        || ![
            item.length_meters,
            item.local_x,
            item.local_z,
            item.local_yaw_radians,
            item.rest_height_meters,
        ]
        .iter()
        .all(|value| value.is_finite())
        || !(0.0..=2.0).contains(&item.length_meters)
        || !(0.0..=1.0).contains(&item.rest_height_meters)
    {
        return Err(format!("Invalid FishStand display fish: {index}"));
    }
    let geometry = &item.geometry;
    let vertices = geometry.position.count;
    if vertices < 3
        || geometry.normal.count != vertices
        || geometry.a_data.count != vertices
        || !geometry.indices.count.is_multiple_of(3)
        || geometry.triangles == 0
        || geometry.triangles != geometry.indices.count / 3
    {
        return Err(format!("Invalid FishStand fish geometry: {}", item.species));
    }
    let geometry_items = [
        (&geometry.position, "position.f32le.bin", 3, 12),
        (&geometry.normal, "normal.f32le.bin", 3, 12),
        (&geometry.a_data, "a-data.f32le.bin", 4, 16),
        (&geometry.indices, "indices.u32le.bin", 4, 4),
    ];
    for (payload, file, width, bytes_per_item) in geometry_items {
        if payload.file != file
            || payload.item_size != width
            || payload.count == 0
            || Some(payload.bytes) != payload.count.checked_mul(bytes_per_item)
        {
            return Err(format!("Invalid fish payload shape: {file}"));
        }
        let name = format!("fish-stand-fish/{}/{file}", item.species);
        request(
            bundle,
            requests,
            &name,
            Entry {
                sha256: payload.sha256,
                bytes: payload.bytes,
            },
        )?;
    }
    let key = |file: &str| format!("{}/{file}", item.species);
    Ok(FishStandFish {
        species: item.species.clone(),
        length_meters: item.length_meters,
        local_x: item.local_x,
        local_z: item.local_z,
        local_yaw_radians: item.local_yaw_radians,
        pose: item.pose.clone(),
        rest_height_meters: item.rest_height_meters,
        position_key: key("position.f32le.bin"),
        normal_key: key("normal.f32le.bin"),
        indices_key: key("indices.u32le.bin"),
        vertices,
        triangles: geometry.triangles,
    })
}

#[allow(
    clippy::case_sensitive_file_extension_comparisons,
    reason = "These provenance suffixes are exact case-sensitive keys in the pinned bundle."
)]
fn finish_content(
    plan: ContentPlan,
    mut payloads: BTreeMap<String, Vec<u8>>,
    provenance: &mut BTreeMap<String, ContentHash>,
) -> Result<ContentLoaded, String> {
    let joe_glb = payloads
        .remove(JOE_RENDER)
        .ok_or("Derived Joe GLB missing")?;
    if !joe_glb.starts_with(b"glTF") {
        return Err("Derived Joe GLB header differs".into());
    }
    let mut fish_payloads = BTreeMap::new();
    for (name, bytes) in payloads {
        if let Some(key) = name.strip_prefix("fish-stand-fish/") {
            fish_payloads.insert(key.to_owned(), bytes);
        } else if name.starts_with("fish-stand-content/source/") {
            provenance.insert(name.clone(), plan.requests[&name].sha256);
            if (name.ends_with(".md") || name.ends_with("/LICENSE"))
                && std::str::from_utf8(&bytes)
                    .map_err(|error| error.to_string())?
                    .trim()
                    .is_empty()
            {
                return Err(format!("Empty FishStand content provenance: {name}"));
            }
        } else {
            return Err(format!("Unexpected FishStand content payload: {name}"));
        }
    }
    if fish_payloads.len() != 20 {
        return Err("FishStand requires twenty fish geometry payloads".into());
    }
    Ok(ContentLoaded {
        content_descriptor: plan.content_descriptor,
        fish_descriptor: plan.fish_descriptor,
        joe_glb,
        fish: plan.fish,
        fish_payloads,
        source_joe_hash: plan.source_joe_hash,
        rocketbox_license_hash: plan.rocketbox_license_hash,
    })
}

fn request(
    bundle: &PreviewBundle,
    requests: &mut BTreeMap<String, Entry>,
    name: &str,
    declared: Entry,
) -> Result<(), String> {
    let expected = entry(bundle, name)?;
    if expected.sha256 != declared.sha256 || expected.bytes != declared.bytes {
        return Err(format!(
            "FishStand descriptor differs from bundle pin: {name}"
        ));
    }
    if requests.insert(name.to_owned(), *expected).is_some() {
        return Err(format!("Duplicate FishStand payload: {name}"));
    }
    Ok(())
}

fn parse_manifest(bytes: &[u8], expected: ContentHash) -> Result<PreviewBundle, String> {
    if bytes.len() > 2 * 1024 * 1024 || bytes_sha256(bytes) != expected {
        return Err("FishStand preview bundle pin mismatch or size limit".into());
    }
    let bundle: PreviewBundle = serde_json::from_slice(bytes).map_err(|error| error.to_string())?;
    if bundle.schema != "forge.tidewater.preview-scene"
        || bundle.version != 1
        || bundle.source_commit != SOURCE_COMMIT
        || bundle.area_id.trim().is_empty()
        || bundle.scope.trim().is_empty()
        || bundle.scope.len() > 512
        || bundle.files.len() > 4096
    {
        return Err("Unsupported FishStand preview bundle".into());
    }
    Ok(bundle)
}

fn validate_descriptor(descriptor: &Descriptor) -> Result<(), String> {
    if descriptor.schema != "forge.tidewater.fish-stand-bevy-input"
        || descriptor.version != 1
        || descriptor.source_commit != SOURCE_COMMIT
        || ![
            descriptor.placement.x,
            descriptor.placement.z,
            descriptor.placement.yaw_radians,
        ]
        .into_iter()
        .all(f32::is_finite)
        || descriptor.placement.y != "terrain.heightAt(x,z)"
        || descriptor.licenses.tidewater != "source/LICENSE-Tidewater.txt"
        || descriptor.licenses.props != "source/props/CREDITS.md"
        || descriptor.geometry.triangles == 0
        || descriptor.material.source_wgsl.trim().is_empty()
        || descriptor.material.status != "bevy_base_array_shader_not_source_parity"
        || descriptor.material.limitations.is_empty()
        || descriptor
            .geometry
            .attributes
            .keys()
            .map(String::as_str)
            .collect::<BTreeSet<_>>()
            != ATTRIBUTES.into_iter().collect()
        || descriptor
            .textures
            .keys()
            .map(String::as_str)
            .collect::<BTreeSet<_>>()
            != TEXTURES.into_iter().collect()
    {
        return Err("Unsupported or incomplete FishStand descriptor".into());
    }
    for payload in descriptor
        .geometry
        .attributes
        .values()
        .chain(std::iter::once(&descriptor.geometry.indices))
    {
        if !safe_local_name(&payload.file)
            || payload.bytes == 0
            || payload.bytes > 32 * 1024 * 1024
            || payload.item_size == 0
            || payload.count == 0
        {
            return Err("Invalid FishStand geometry payload".into());
        }
    }
    for texture in descriptor.textures.values() {
        let _ = texture.srgb;
        if !safe_local_name(&texture.file)
            || texture.bytes == 0
            || texture.bytes > 32 * 1024 * 1024
            || texture.width == 0
            || texture.height == 0
            || texture.layers == 0
            || !matches!(texture.address_mode.as_str(), "repeat" | "clamp")
            || texture.mip_status != "base_level_only_source_gpu_mips_unported"
        {
            return Err("Invalid FishStand texture payload".into());
        }
    }
    Ok(())
}

fn load_payload(
    bundle: &PreviewBundle,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    files: &mut BTreeMap<String, Vec<u8>>,
    payload: &Payload,
) -> Result<(), String> {
    load_declared(
        bundle,
        read,
        files,
        payload.file.as_str(),
        payload.bytes,
        payload.sha256,
    )
}

fn load_texture(
    bundle: &PreviewBundle,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    files: &mut BTreeMap<String, Vec<u8>>,
    texture: &Texture,
) -> Result<(), String> {
    load_declared(
        bundle,
        read,
        files,
        texture.file.as_str(),
        texture.bytes,
        texture.sha256,
    )
}

async fn load_payload_async<F, Fut>(
    bundle: &PreviewBundle,
    read: &mut F,
    files: &mut BTreeMap<String, Vec<u8>>,
    payload: &Payload,
) -> Result<(), String>
where
    F: FnMut(String, usize) -> Fut,
    Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
{
    load_declared_async(
        bundle,
        read,
        files,
        payload.file.as_str(),
        payload.bytes,
        payload.sha256,
    )
    .await
}

async fn load_texture_async<F, Fut>(
    bundle: &PreviewBundle,
    read: &mut F,
    files: &mut BTreeMap<String, Vec<u8>>,
    texture: &Texture,
) -> Result<(), String>
where
    F: FnMut(String, usize) -> Fut,
    Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
{
    load_declared_async(
        bundle,
        read,
        files,
        texture.file.as_str(),
        texture.bytes,
        texture.sha256,
    )
    .await
}

async fn load_declared_async<F, Fut>(
    bundle: &PreviewBundle,
    read: &mut F,
    files: &mut BTreeMap<String, Vec<u8>>,
    local_name: &str,
    declared_bytes: usize,
    declared_hash: ContentHash,
) -> Result<(), String>
where
    F: FnMut(String, usize) -> Fut,
    Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
{
    let bundle_name = format!("fish-stand/{local_name}");
    let item = entry(bundle, &bundle_name)?;
    if item.bytes != declared_bytes || item.sha256 != declared_hash {
        return Err(format!(
            "FishStand descriptor differs from bundle pin: {local_name}"
        ));
    }
    let bytes = read(bundle_name.clone(), item.bytes).await?;
    verify(&bundle_name, item, &bytes)?;
    files.insert(local_name.to_owned(), bytes);
    Ok(())
}

fn load_declared(
    bundle: &PreviewBundle,
    read: &mut impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    files: &mut BTreeMap<String, Vec<u8>>,
    local_name: &str,
    declared_bytes: usize,
    declared_hash: ContentHash,
) -> Result<(), String> {
    let bundle_name = format!("fish-stand/{local_name}");
    let item = entry(bundle, &bundle_name)?;
    if item.bytes != declared_bytes || item.sha256 != declared_hash {
        return Err(format!(
            "FishStand descriptor differs from bundle pin: {local_name}"
        ));
    }
    let bytes = read(&bundle_name, item.bytes)?;
    verify(&bundle_name, item, &bytes)?;
    files.insert(local_name.to_owned(), bytes);
    Ok(())
}

fn entry<'a>(bundle: &'a PreviewBundle, name: &str) -> Result<&'a Entry, String> {
    let entry = bundle
        .files
        .get(name)
        .ok_or_else(|| format!("Missing FishStand bundle entry: {name}"))?;
    if !safe_bundle_path(name) || entry.bytes == 0 || entry.bytes > 32 * 1024 * 1024 {
        return Err(format!(
            "Unsafe or oversized FishStand bundle entry: {name}"
        ));
    }
    Ok(entry)
}

fn verify(name: &str, entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err(format!("FishStand payload differs: {name}"));
    }
    Ok(())
}

fn safe_bundle_path(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 256
        && !name.starts_with('/')
        && name.split('/').all(safe_local_name)
}

fn safe_local_name(name: &str) -> bool {
    !name.is_empty()
        && name.len() <= 128
        && !name.starts_with('.')
        && name
            .bytes()
            .all(|byte| byte.is_ascii_alphanumeric() || matches!(byte, b'-' | b'_' | b'.'))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn hash(byte: u8) -> ContentHash {
        ContentHash::from_bytes([byte; 32])
    }

    fn fixture_byte(value: usize) -> u8 {
        u8::try_from(value).expect("fixture values fit in a byte")
    }

    #[allow(
        clippy::too_many_lines,
        reason = "The fixture intentionally builds the complete pinned bundle in one deterministic sequence."
    )]
    fn fixture() -> (Vec<u8>, BTreeMap<String, Vec<u8>>) {
        let mut files = BTreeMap::new();
        let mut add = |name: &str, bytes: Vec<u8>| {
            files.insert(name.to_owned(), bytes);
        };
        let mut attributes = serde_json::Map::new();
        for name in ATTRIBUTES {
            let file = format!("{name}.bin");
            let bytes = vec![fixture_byte(name.len())];
            attributes.insert(
                name.to_owned(),
                json!({"file":file,"bytes":bytes.len(),"sha256":bytes_sha256(&bytes),"item_size":1,"count":1}),
            );
            add(&format!("fish-stand/{file}"), bytes);
        }
        let indices = vec![77];
        add("fish-stand/indices.bin", indices.clone());
        let mut textures = serde_json::Map::new();
        for name in TEXTURES {
            let file = format!("{name}.bin");
            let bytes = vec![fixture_byte(name.len()), 1];
            textures.insert(
                name.to_owned(),
                json!({"file":file,"bytes":bytes.len(),"sha256":bytes_sha256(&bytes),"width":1,"height":1,"layers":1,"srgb":true,"address_mode":"clamp","mip_status":"base_level_only_source_gpu_mips_unported"}),
            );
            add(&format!("fish-stand/{file}"), bytes);
        }
        let descriptor = json!({
            "schema":"forge.tidewater.fish-stand-bevy-input", "version":1,
            "source_commit":SOURCE_COMMIT,
            "source_export_manifest_sha256":hash(6),
            "placement":{"x":49.9,"z":-74.6,"yaw_radians":1.45,"y":"terrain.heightAt(x,z)"},
            "geometry":{"attributes":attributes,"indices":{"file":"indices.bin","bytes":indices.len(),"sha256":bytes_sha256(&indices),"item_size":1,"count":1},"triangles":1},
            "textures":textures,
            "material":{"source_wgsl":"source/FishStand.wgsl","status":"bevy_base_array_shader_not_source_parity","limitations":["fixture"]},
            "licenses":{"tidewater":"source/LICENSE-Tidewater.txt","props":"source/props/CREDITS.md"}
        });
        add(DESCRIPTOR, serde_json::to_vec(&descriptor).unwrap());
        for name in PROVENANCE {
            add(name, b"source record".to_vec());
        }
        let mut source_files = serde_json::Map::new();
        for relative in [
            "source/LICENSE",
            ROCKETBOX_LICENSE,
            JOE_SOURCE,
            "source/src/game/CatchDisplay.js",
            "source/src/game/FishStand.js",
            "source/src/game/FishTable.js",
            "source/src/game/Vendor.js",
            "source/src/world/fish/FishGeometry.js",
            "source/src/world/fish/FishMaterial.js",
            "source/src/world/fish/FishProps.js",
            "source/src/world/fish/FishSpecies.js",
        ] {
            let bytes = if relative == JOE_SOURCE {
                b"glTF original".to_vec()
            } else {
                b"MIT source".to_vec()
            };
            source_files.insert(
                relative.to_owned(),
                json!({"bytes":bytes.len(),"sha256":bytes_sha256(&bytes)}),
            );
            add(&format!("fish-stand-content/{relative}"), bytes);
        }
        let mut displayed = Vec::new();
        let mut fish = Vec::new();
        for (index, species) in SPECIES.into_iter().enumerate() {
            let mut geometry = serde_json::Map::new();
            for (key, filename, width, bytes_per_item) in [
                ("position", "position.f32le.bin", 3, 12),
                ("normal", "normal.f32le.bin", 3, 12),
                ("a_data", "a-data.f32le.bin", 4, 16),
                ("indices", "indices.u32le.bin", 4, 4),
            ] {
                let bytes = vec![fixture_byte(index); 3 * bytes_per_item];
                geometry.insert(
                    key.to_owned(),
                    json!({"file":filename,"bytes":bytes.len(),"sha256":bytes_sha256(&bytes),"item_size":width,"count":3}),
                );
                add(&format!("fish-stand-fish/{species}/{filename}"), bytes);
            }
            geometry.insert("triangles".into(), json!(1));
            let item = json!({
                "species":species,"length_meters":0.3,"local_x":-0.5,
                "local_z":0.7,"local_yaw_radians":0.1,"pose":"side",
            });
            displayed.push(item.clone());
            let mut input = item.as_object().unwrap().clone();
            input.insert("index".into(), json!(index));
            input.insert("rest_height_meters".into(), json!(0.01));
            input.insert("geometry".into(), serde_json::Value::Object(geometry));
            fish.push(serde_json::Value::Object(input));
        }
        let content = json!({
            "schema":"forge.tidewater.fish-stand-content-source-export","version":1,
            "source":{"commit":SOURCE_COMMIT},
            "vendor":{"id":"tidewater:fish-stand:joe","kind":"buyer",
                "placement":{"local_x":0.2,"local_y":0.06,"local_z":0.12},
                "character":{"file":JOE_SOURCE,"idle":"idle_neutral_01","license":"MIT","license_file":ROCKETBOX_LICENSE}},
            "display":{"fish_on_ice":displayed},
            "licenses":{"tidewater_code":{"spdx":"MIT","file":"source/LICENSE"},
                "joe":{"spdx":"MIT","file":ROCKETBOX_LICENSE}},
            "source_files":source_files.clone(),
        });
        add(CONTENT_DESCRIPTOR, serde_json::to_vec(&content).unwrap());
        let mut fish_source_files = serde_json::Map::new();
        for (key, relative) in [
            ("FishGeometry.js", "source/src/world/fish/FishGeometry.js"),
            ("FishSpecies.js", "source/src/world/fish/FishSpecies.js"),
            ("FishStand.js", "source/src/game/FishStand.js"),
            ("CatchDisplay.js", "source/src/game/CatchDisplay.js"),
            ("LICENSE", "source/LICENSE"),
        ] {
            let recorded = &source_files[relative];
            fish_source_files.insert(
                key.into(),
                json!({"file":key,"bytes":recorded["bytes"],"sha256":recorded["sha256"]}),
            );
        }
        let fish_descriptor = json!({
            "schema":"forge.tidewater.fish-stand-fish-source-export","version":1,
            "source":{"commit":SOURCE_COMMIT},"source_files":fish_source_files,
            "fish":fish,
        });
        add(
            FISH_DESCRIPTOR,
            serde_json::to_vec(&fish_descriptor).unwrap(),
        );
        add(JOE_RENDER, b"glTF derivative".to_vec());
        let entries = files
            .iter()
            .map(|(name, bytes)| {
                (
                    name.clone(),
                    json!({"bytes":bytes.len(),"sha256":bytes_sha256(bytes)}),
                )
            })
            .collect::<serde_json::Map<_, _>>();
        let mut entries = entries;
        // The real 011 preview manifest includes empty authoring locks outside
        // the composite. They are never read by this adapter.
        entries.insert(
            "workspace/workspace-v1/authoring.lock".into(),
            json!({"bytes":0,"sha256":bytes_sha256(&[])}),
        );
        let manifest = serde_json::to_vec(&json!({
            "schema":"forge.tidewater.preview-scene", "version":1,
            "source_commit":SOURCE_COMMIT, "area_id":"e620dd19-ead9-5913-a6d6-0f93109148c0",
            "building_package_sha256":hash(5),
            "scope":"Native fixed-view prototype only; not a distributable module or complete Studio map",
            "files":entries
        }))
        .unwrap();
        (manifest, files)
    }

    #[test]
    fn composite_reference_replays_only_with_the_exact_bundle_pin() {
        let (manifest, files) = fixture();
        let pin = bytes_sha256(&manifest);
        let package = FishStandPackage::load(&manifest, pin, |name, _| {
            files
                .get(name)
                .cloned()
                .ok_or_else(|| "fixture missing".into())
        })
        .unwrap();
        assert_eq!(
            package.reference().asset_id,
            AssetId::deterministic(ASSET_ID_SEED)
        );
        assert_eq!(package.reference().bundle_hash, pin);
        assert_eq!(package.files().len(), 15);
        assert_eq!(package.provenance().len(), 15);
        assert_eq!(package.fish().len(), 5);
        assert_eq!(package.fish_payloads().len(), 20);
        assert_eq!(package.joe_glb(), b"glTF derivative");
        assert_eq!(package.source_export_manifest_hash(), hash(6));
        assert_eq!(package.building_package_hash(), hash(5));
        assert!(FishStandPackage::load(&manifest, hash(7), |_, _| unreachable!()).is_err());
    }

    #[test]
    fn changed_payload_is_rejected_even_when_the_descriptor_is_unchanged() {
        let (manifest, mut files) = fixture();
        let pin = bytes_sha256(&manifest);
        files.insert("fish-stand/surf_a.bin".into(), vec![0, 0]);
        assert!(
            FishStandPackage::load(&manifest, pin, |name, _| {
                files
                    .get(name)
                    .cloned()
                    .ok_or_else(|| "fixture missing".into())
            })
            .is_err()
        );
    }

    #[test]
    fn joe_fish_and_license_must_match_the_same_manifest() {
        for name in [
            JOE_RENDER,
            "fish-stand-fish/jack/position.f32le.bin",
            "fish-stand-content/source/public/models/characters/LICENSE-Rocketbox.md",
            "fish-stand-content/source/public/models/characters/joe.glb",
        ] {
            let (manifest, mut files) = fixture();
            let pin = bytes_sha256(&manifest);
            files.insert(name.to_owned(), b"changed".to_vec());
            assert!(
                FishStandPackage::load(&manifest, pin, |requested, _| {
                    files
                        .get(requested)
                        .cloned()
                        .ok_or_else(|| "fixture missing".into())
                })
                .is_err(),
                "changed payload must fail: {name}"
            );
        }
    }

    /// Opt-in contract against an acquired bundle, never a required checkout
    /// fixture. Set `OPENRPG_TIDEWATER_FISH_STAND_BUNDLE` to its directory.
    #[test]
    fn acquired_bundle_011_loads_when_supplied() {
        let Ok(root) = std::env::var("OPENRPG_TIDEWATER_FISH_STAND_BUNDLE") else {
            return;
        };
        let root = std::path::PathBuf::from(root);
        let manifest = std::fs::read(root.join("preview-scene.json")).unwrap();
        let pin = bytes_sha256(&manifest);
        assert_eq!(
            pin,
            ContentHash::parse(BUNDLE_011_SHA256).expect("embedded 011 pin is valid")
        );
        let package = FishStandPackage::load(&manifest, pin, |name, expected| {
            let bytes = std::fs::read(root.join(name)).map_err(|error| error.to_string())?;
            if bytes.len() > expected {
                return Err(format!("Reader exceeded manifest bound: {name}"));
            }
            Ok(bytes)
        })
        .unwrap();
        assert_eq!(package.reference().bundle_hash, pin);
        assert_eq!(package.files().len(), 15);
        assert_eq!(package.provenance().len(), 15);
        assert_eq!(package.fish().len(), 5);
        assert_eq!(package.fish_payloads().len(), 20);
        assert_eq!(
            bytes_sha256(package.joe_glb()),
            ContentHash::parse(JOE_RENDER_SHA256).unwrap()
        );
        assert_eq!(
            package.source_joe_hash(),
            ContentHash::parse(JOE_SOURCE_SHA256).unwrap()
        );
        assert_eq!(
            package.rocketbox_license_hash(),
            ContentHash::parse(ROCKETBOX_LICENSE_SHA256).unwrap()
        );
        let descriptor: Descriptor = serde_json::from_slice(
            package
                .files()
                .get("fish-stand.json")
                .expect("verified package retains its descriptor"),
        )
        .unwrap();
        for name in descriptor
            .geometry
            .attributes
            .values()
            .map(|payload| payload.file.as_str())
            .chain(std::iter::once(descriptor.geometry.indices.file.as_str()))
            .chain(
                descriptor
                    .textures
                    .values()
                    .map(|texture| texture.file.as_str()),
            )
        {
            assert!(
                package.files().contains_key(name),
                "missing declared {name}"
            );
        }
    }
}
