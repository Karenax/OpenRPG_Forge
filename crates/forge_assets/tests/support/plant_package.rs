//! Synthetic transport data, not an acquired source scene or render qualification.
use forge_core::bytes_sha256;
use serde_json::json;
use std::collections::BTreeMap;

pub struct Fixture {
    pub files: BTreeMap<String, Vec<u8>>,
}

impl Fixture {
    pub fn new() -> Self {
        let level = json!({"attributes":{"position":"position.bin","normal":"normal.bin"},"indices":"indices.bin"});
        let groups: Vec<_> = ["palms", "bananas", "understory", "monsteras", "broadleaf", "canopy"].into_iter().enumerate()
            .map(|(index,name)| json!({"name":name,"count":if index==0 {65_378} else {1},"levels":[level.clone()]})).collect();
        let commit = "1438b1abfcaee3267092b75573014f4d9b4a983c";
        let mut files = BTreeMap::from([
            ("vegetation-scene.json".into(), serde_json::to_vec(&json!({"schema":"forge.tidewater.vegetation-scene-probe","version":1,"groups":groups})).unwrap()),
            ("grass.json".into(), serde_json::to_vec(&json!({"schema":"forge.tidewater.grass-probe","version":1,"source_commit":commit,"height_sha256":bytes_sha256(b"synthetic heights"),"cells_per_side":256,"flags":"flags.bin","min_y":"min.bin","max_y":"max.bin","mask":{"width":1024,"height":1024,"file":"mask.bin"},"levels":[level.clone(),level.clone(),level]})).unwrap()),
            ("plant-textures.json".into(), serde_json::to_vec(&json!({"schema":"forge.tidewater.plant-textures","version":1,"leaf":{"width":1024,"height":1024,"mips":11},"impostor":{"width":3840,"height":768,"mips":12}})).unwrap()),
            ("plant.wgsl".into(), b"// Synthetic transport-only shader; never executed".to_vec()),
            ("LICENSE-Tidewater.txt".into(), b"Synthetic structural fixture only; no source distribution claim".to_vec()),
            ("position.bin".into(), [0.0_f32,0.0,0.0,1.0,0.0,0.0,0.0,1.0,0.0].into_iter().flat_map(f32::to_le_bytes).collect()),
            ("normal.bin".into(), [0.0_f32,0.0,1.0].repeat(3).into_iter().flat_map(f32::to_le_bytes).collect()),
            ("indices.bin".into(), [0_u32,1,2].into_iter().flat_map(u32::to_le_bytes).collect()),
        ]);
        for (name, size) in [
            ("detail.bin", 1_048_576),
            ("leaf.bin", 5_592_404),
            ("impostor-a.bin", 15_728_624),
            ("impostor-b.bin", 15_728_624),
            ("flags.bin", 65_536),
            ("min.bin", 262_144),
            ("max.bin", 262_144),
            ("mask.bin", 4_194_304),
        ] {
            files.insert(name.into(), vec![0; size]);
        }
        Self { files }
    }

    pub fn index(&self) -> Vec<u8> {
        let files: BTreeMap<_, _> = self
            .files
            .iter()
            .map(|(name, bytes)| {
                (
                    name,
                    json!({"sha256":bytes_sha256(bytes),"bytes":bytes.len()}),
                )
            })
            .collect();
        serde_json::to_vec(&json!({"schema":"forge.tidewater.plant-package","version":1,"source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c","source_fixture_sha256":bytes_sha256(b"Synthetic package; no renderer claim"),"files":files})).unwrap()
    }
}
