//! Engine-owned terrain programs and compatibility with historical pinned packages.
//!
//! The embedded programs are the byte-exact Forge adapters derived from Tidewater
//! upstream 1438b1abfcaee3267092b75573014f4d9b4a983c. The upstream licence and
//! attribution are retained in `third_party/tidewater/LICENSE` and
//! `THIRD_PARTY_NOTICES.md`. Module-provided surface data does not supply code.
use forge_assets::source_terrain_package::{TerrainPackage, TerrainPackageFormat};

const SURFACE: &str = include_str!("engine_surface.wgsl");
const LOD: &str = include_str!("engine_lod.wgsl");

pub(super) fn for_package(package: &TerrainPackage) -> Result<(&str, &str), String> {
    let (surface, lod) = select(package)?;
    Ok((surface.text(), lod.text()))
}

enum Program<'a> {
    Engine(&'static str),
    Legacy(&'a str),
}

impl<'a> Program<'a> {
    #[allow(
        clippy::match_same_arms,
        reason = "The engine program has static lifetime; the legacy program borrows its package"
    )]
    fn text(self) -> &'a str {
        match self {
            Self::Engine(source) => source,
            Self::Legacy(source) => source,
        }
    }
}

fn select(package: &TerrainPackage) -> Result<(Program<'_>, Program<'_>), String> {
    match package.format() {
        TerrainPackageFormat::SurfaceData => Ok((Program::Engine(SURFACE), Program::Engine(LOD))),
        TerrainPackageFormat::LegacySource => {
            let text = |name| {
                std::str::from_utf8(package.file(name).ok_or("Missing terrain shader")?)
                    .map_err(|error| error.to_string())
            };
            let surface = text("terrain.wgsl")?;
            let lod = text("terrain-lod.wgsl")?;
            // Reuse the engine program only when the historical pin resolves to
            // the exact same bytes. Other admitted variants keep their behavior.
            Ok((
                if surface == SURFACE {
                    Program::Engine(SURFACE)
                } else {
                    Program::Legacy(surface)
                },
                if lod == LOD {
                    Program::Engine(LOD)
                } else {
                    Program::Legacy(lod)
                },
            ))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::bytes_sha256;
    use std::collections::BTreeMap;

    #[test]
    fn data_only_package_uses_engine_owned_reference_programs() {
        let files = BTreeMap::from([
            ("terrain-normal.bin", vec![7; 20]),
            ("terrain-splat.bin", vec![8; 20]),
            ("terrain-detail.bin", vec![9; 4]),
            ("LICENSE.txt", b"fixture licence".to_vec()),
        ]);
        let pins: serde_json::Map<_, _> = files
            .iter()
            .map(|(name, bytes)| {
                (
                    (*name).to_owned(),
                    serde_json::json!({
                        "bytes": bytes.len(), "sha256": bytes_sha256(bytes)
                    }),
                )
            })
            .collect();
        let index = serde_json::to_vec(&serde_json::json!({
            "schema": "forge.terrain-surface-package", "version": 1,
            "raster": {"width": 2, "height": 2, "origin_xz_m": [-1.0, -1.0],
                "sample_spacing_m": 1.0, "outside_height_m": -10.0,
                "encoding": "float32_le", "extent_m": [2.0, 2.0]},
            "textures": {"normal": [2, 2], "splat": [2, 2], "detail": [1, 1]},
            "files": pins
        }))
        .unwrap();
        let package = TerrainPackage::load(&index, bytes_sha256(&index), |name, _| {
            files
                .get(name)
                .cloned()
                .ok_or_else(|| "unexpected file".into())
        })
        .unwrap();
        assert!(package.file("terrain.wgsl").is_none());
        let (surface, lod) = for_package(&package).unwrap();
        assert_eq!(
            bytes_sha256(surface.as_bytes()).to_string(),
            "8651d33064d6ca6b0ecd56989d65dfe658abd3ce339d2bac21c39889e24baaad"
        );
        assert_eq!(
            bytes_sha256(lod.as_bytes()).to_string(),
            "a8e3e7558e0980702838bcad508ab61f46e399dcf1176e067b5cc5a9405b95a5"
        );
        assert!(surface.contains("fn terrainUvOf("));
        assert!(lod.contains("fn terrainHeightAt("));
    }

    fn legacy_package(surface: &str, lod: &str) -> TerrainPackage {
        let files = BTreeMap::from([
            ("terrain.wgsl", surface.as_bytes().to_vec()),
            ("terrain-lod.wgsl", lod.as_bytes().to_vec()),
            ("terrain-normal.bin", vec![0; 22_369_620]),
            ("terrain-splat.bin", vec![0; 22_369_620]),
            ("terrain-detail.bin", vec![0; 1_398_100]),
            ("LICENSE-Tidewater.txt", b"fixture licence".to_vec()),
        ]);
        let pins: serde_json::Map<_, _> = files
            .iter()
            .map(|(name, bytes)| {
                (
                    (*name).to_owned(),
                    serde_json::json!({"bytes": bytes.len(), "sha256": bytes_sha256(bytes)}),
                )
            })
            .collect();
        let index = serde_json::to_vec(&serde_json::json!({
            "schema": "forge.tidewater.terrain-package", "version": 1,
            "source_commit": "1438b1abfcaee3267092b75573014f4d9b4a983c", "files": pins
        }))
        .unwrap();
        TerrainPackage::load(&index, bytes_sha256(&index), |name, _| {
            files
                .get(name)
                .cloned()
                .ok_or_else(|| "unexpected file".into())
        })
        .unwrap()
    }

    #[test]
    fn legacy_reference_reuses_engine_programs_without_replacing_other_variants() {
        let package = legacy_package(SURFACE, LOD);
        let (surface, lod) = for_package(&package).unwrap();
        assert_eq!((surface, lod), (SURFACE, LOD));
        assert!(matches!(
            select(&package).unwrap(),
            (Program::Engine(_), Program::Engine(_))
        ));
        assert_eq!(super::super::lod_program(&package).unwrap(), LOD);

        let different_lod = format!("{LOD}\n// admitted historical variation");
        let alternate = legacy_package(SURFACE, &different_lod);
        let (surface, lod) = for_package(&alternate).unwrap();
        assert_eq!(surface, SURFACE);
        assert_eq!(lod, different_lod);
        assert!(matches!(
            select(&alternate).unwrap(),
            (Program::Engine(_), Program::Legacy(_))
        ));
    }
}
