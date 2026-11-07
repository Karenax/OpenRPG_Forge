//! Original dense textured geometry for runtime HLOD qualification, not a user map.
use super::write;
use forge_assets::governed_import::{
    AssetProvenance, AssetRights, AssetTransformation, DurableAssetSource, GovernedAssetFile,
    GovernedAssetKind, GovernedImportAsset, GovernedImportCatalog, RedistributionRights,
};
use forge_core::{AssetId, CommandId, Transform, Vec3, bytes_sha256};
use forge_project::{
    module_package::{ModuleFile, ModuleFileRole},
    zone_authoring::{placements::PlacementEdit, session::ZoneEditSession, start::ZoneStart},
};
use serde_json::json;
use std::path::Path;

const LONGITUDE: u32 = 256;
const LATITUDE: u32 = 128;

pub(super) fn add_asset(
    root: &Path,
    files: &mut Vec<ModuleFile>,
    catalog: &mut GovernedImportCatalog,
) -> Result<(), Box<dyn std::error::Error>> {
    let recipe = serde_json::to_vec_pretty(&json!({
        "schema":"forge.dense-sphere-recipe", "version":1,
        "longitude":LONGITUDE, "latitude":LATITUDE, "radius_m":1.0,
        "uv_seam":"exact repeated positions; U=0/1 preserved",
        "texture":{"dimensions":[8,8], "colors":[[235,64,192,255],[32,48,96,255]], "filter":"nearest", "wrap":"repeat"},
        "generator":"build_regression_module/dense", "license":"CC0-1.0"
    }))?;
    let bytes = sphere()?;
    let recipe_path = "recipes/dense-sphere.json";
    let mesh_path = "assets/dense-sphere.glb";
    write(
        root,
        files,
        recipe_path,
        &recipe,
        ModuleFileRole::AssetAuxiliary,
    )?;
    write(root, files, mesh_path, &bytes, ModuleFileRole::Asset)?;
    catalog.assets.push(GovernedImportAsset {
        id: AssetId::deterministic("forge/gate7/dense-textured-sphere/v1"),
        name: "Original dense textured sphere".into(),
        kind: GovernedAssetKind::Mesh,
        payload: GovernedAssetFile {
            path: mesh_path.into(),
            bytes: bytes.len() as u64,
            sha256: bytes_sha256(&bytes),
        },
        provenance: AssetProvenance {
            source: "Original analytic sphere and generated checker from preserved recipe".into(),
            author: "OpenRPG Forge".into(),
            license: "CC0-1.0".into(),
            evidence: "licenses/generated.txt".into(),
            acquired_on: "2026-10-24".into(),
            rights: AssetRights {
                local_use: true,
                redistribution: RedistributionRights::Allowed,
                redistribution_evidence: Some("licenses/generated.txt".into()),
            },
        },
        derived_from: None,
        transformations: vec![AssetTransformation {
            tool: "build_regression_module/dense".into(),
            version: "1".into(),
            parameters_sha256: bytes_sha256(&recipe),
        }],
        authored_changes: true,
        thumbnail: None,
        durable_source: Some(DurableAssetSource::Recipe {
            file: GovernedAssetFile {
                path: recipe_path.into(),
                bytes: recipe.len() as u64,
                sha256: bytes_sha256(&recipe),
            },
        }),
    });
    Ok(())
}

pub(super) fn place_objects(
    zone: &mut ZoneEditSession,
    catalog: &GovernedImportCatalog,
) -> Result<(), String> {
    let asset = catalog
        .assets
        .iter()
        .find(|asset| asset.id == AssetId::deterministic("forge/gate7/dense-textured-sphere/v1"))
        .ok_or("Dense sphere absent")?;
    let placements = zone
        .head()
        .corrections
        .placements
        .clone()
        .unwrap_or_default();
    for (index, placement) in placements.iter().enumerate() {
        let replace = zone.head().prepare_placement(
            CommandId::deterministic(format!("dense-hlod/pin/{}", placement.id)),
            PlacementEdit::ReplaceAsset {
                ids: vec![placement.id],
                expected_asset_id: placement.asset_id,
                expected_asset_hash: placement.asset_hash,
                asset_id: asset.id,
                asset_hash: asset.payload.sha256,
            },
        )?;
        zone.execute(&replace)?;
        let pose = zone.head().prepare_placement(
            CommandId::deterministic(format!("dense-hlod/pose/{}", placement.id)),
            PlacementEdit::Transform {
                id: placement.id,
                transform: Transform {
                    translation_m: Vec3::new(if index == 0 { 250.0 } else { 262.0 }, 3.0, -1.0),
                    scale: Vec3::new(3.0, 3.0, 3.0),
                    ..Transform::IDENTITY
                },
            },
        )?;
        zone.execute(&pose)?;
    }
    let start = zone.head().prepare_start(
        Some(ZoneStart {
            x_m: 256.0,
            z_m: 12.0,
            facing_degrees: Some(0.0),
        }),
        CommandId::deterministic("dense-hlod/start"),
    )?;
    zone.execute(&start)?;
    Ok(())
}

fn sphere() -> Result<Vec<u8>, serde_json::Error> {
    let count = (LONGITUDE + 1) * (LATITUDE + 1);
    let mut positions = Vec::with_capacity(count as usize);
    let mut uv = Vec::with_capacity(count as usize);
    for y in 0..=LATITUDE {
        let phi = f64::from(y) * std::f64::consts::PI / f64::from(LATITUDE);
        for x in 0..=LONGITUDE {
            let theta = f64::from(x % LONGITUDE) * std::f64::consts::TAU / f64::from(LONGITUDE);
            let point = match y {
                0 => [0.0, 1.0, 0.0],
                LATITUDE => [0.0, -1.0, 0.0],
                _ => [
                    (phi.sin() * theta.cos()) as f32,
                    phi.cos() as f32,
                    (phi.sin() * theta.sin()) as f32,
                ],
            };
            positions.push(point);
            uv.push([x as f32 / LONGITUDE as f32, y as f32 / LATITUDE as f32]);
        }
    }
    let mut indices = Vec::new();
    for y in 0..LATITUDE {
        for x in 0..LONGITUDE {
            let a = y * (LONGITUDE + 1) + x;
            let b = a + 1;
            let c = a + LONGITUDE + 1;
            let d = c + 1;
            if y > 0 {
                indices.extend([a, b, c]);
            }
            if y + 1 < LATITUDE {
                indices.extend([b, d, c]);
            }
        }
    }
    // Independently verify outward winding and non-degenerate source faces.
    for triangle in indices.as_chunks::<3>().0 {
        let p: [[f32; 3]; 3] = std::array::from_fn(|i| positions[triangle[i] as usize]);
        let a = subtract(p[1], p[0]);
        let b = subtract(p[2], p[0]);
        let cross = [
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0],
        ];
        assert!(
            cross
                .iter()
                .zip(p[0])
                .map(|(a, b)| a * f64::from(b))
                .sum::<f64>()
                > 0.0
        );
    }
    let mut binary = Vec::new();
    for _ in 0..2 {
        for position in &positions {
            for value in position {
                binary.extend(value.to_le_bytes());
            }
        }
    }
    let uv_offset = binary.len();
    for values in &uv {
        for value in values {
            binary.extend(value.to_le_bytes());
        }
    }
    let index_offset = binary.len();
    for index in &indices {
        binary.extend(index.to_le_bytes());
    }
    let image_offset = binary.len();
    let image = checker();
    binary.extend(&image);
    while !binary.len().is_multiple_of(4) {
        binary.push(0);
    }
    let vertex_bytes = count as usize * 12;
    let mut document = serde_json::to_vec(&json!({
        "asset":{"version":"2.0","generator":"OpenRPG Forge dense sphere recipe v1"},"buffers":[{"byteLength":binary.len()}],
        "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":vertex_bytes,"target":34962},{"buffer":0,"byteOffset":vertex_bytes,"byteLength":vertex_bytes,"target":34962},{"buffer":0,"byteOffset":uv_offset,"byteLength":count*8,"target":34962},{"buffer":0,"byteOffset":index_offset,"byteLength":indices.len()*4,"target":34963},{"buffer":0,"byteOffset":image_offset,"byteLength":image.len()}],
        "accessors":[{"bufferView":0,"componentType":5126,"count":count,"type":"VEC3","min":[-1,-1,-1],"max":[1,1,1]},{"bufferView":1,"componentType":5126,"count":count,"type":"VEC3"},{"bufferView":2,"componentType":5126,"count":count,"type":"VEC2"},{"bufferView":3,"componentType":5125,"count":indices.len(),"type":"SCALAR"}],
        "images":[{"bufferView":4,"mimeType":"image/png"}],"samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":10497,"wrapT":10497}],"textures":[{"source":0,"sampler":0}],
        "materials":[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0},"metallicFactor":0.0,"roughnessFactor":0.75}}],
        "meshes":[{"primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3,"material":0}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0
    }))?;
    while !document.len().is_multiple_of(4) {
        document.push(b' ');
    }
    let mut result = b"glTF".to_vec();
    result.extend(2_u32.to_le_bytes());
    result.extend(((28 + document.len() + binary.len()) as u32).to_le_bytes());
    result.extend((document.len() as u32).to_le_bytes());
    result.extend(b"JSON");
    result.extend(document);
    result.extend((binary.len() as u32).to_le_bytes());
    result.extend(b"BIN\0");
    result.extend(binary);
    Ok(result)
}

fn subtract(a: [f32; 3], b: [f32; 3]) -> [f64; 3] {
    std::array::from_fn(|i| f64::from(a[i]) - f64::from(b[i]))
}

fn checker() -> Vec<u8> {
    let mut raw = Vec::new();
    for y in 0..8 {
        raw.push(0);
        for x in 0..8 {
            raw.extend(if (x + y) % 2 == 0 {
                [235, 64, 192, 255]
            } else {
                [32, 48, 96, 255]
            });
        }
    }
    let mut zlib = vec![0x78, 0x01, 0x01];
    let length = raw.len() as u16;
    zlib.extend(length.to_le_bytes());
    zlib.extend((!length).to_le_bytes());
    zlib.extend(&raw);
    let (mut a, mut b) = (1_u32, 0_u32);
    for byte in raw {
        a = (a + u32::from(byte)) % 65521;
        b = (b + a) % 65521;
    }
    zlib.extend(((b << 16) | a).to_be_bytes());
    let mut png = b"\x89PNG\r\n\x1a\n".to_vec();
    chunk(&mut png, b"IHDR", &[0, 0, 0, 8, 0, 0, 0, 8, 8, 6, 0, 0, 0]);
    chunk(&mut png, b"IDAT", &zlib);
    chunk(&mut png, b"IEND", &[]);
    png
}
fn chunk(output: &mut Vec<u8>, kind: &[u8; 4], data: &[u8]) {
    output.extend((data.len() as u32).to_be_bytes());
    output.extend(kind);
    output.extend(data);
    let mut crc = !0_u32;
    for byte in kind.iter().chain(data) {
        crc ^= u32::from(*byte);
        for _ in 0..8 {
            crc = (crc >> 1) ^ (0xedb88320_u32 & 0_u32.wrapping_sub(crc & 1));
        }
    }
    output.extend((!crc).to_be_bytes());
}
