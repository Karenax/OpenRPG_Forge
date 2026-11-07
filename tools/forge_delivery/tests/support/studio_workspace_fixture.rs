#![allow(dead_code, reason = "shared by workspace and launch integration tests")]

use super::delivery;
use delivery::{
    archive, hash, install,
    manifest::{Descriptor, Kind, Launch, Product, Rights},
};
use std::{
    collections::BTreeMap,
    fs,
    path::{Path, PathBuf},
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT: AtomicU64 = AtomicU64::new(0);

pub struct Fixture {
    pub root: PathBuf,
    parent: PathBuf,
}

impl Fixture {
    pub fn new() -> Self {
        let parent = std::env::temp_dir().canonicalize().unwrap();
        let root = parent.join(format!(
            "forge-studio-v3-{}-{}",
            std::process::id(),
            NEXT.fetch_add(1, Ordering::Relaxed)
        ));
        fs::create_dir(&root).unwrap();
        Self { root, parent }
    }

    pub fn install(&self) -> PathBuf {
        self.root.join("installation")
    }

    pub fn user(&self) -> PathBuf {
        self.root.join("user-data")
    }

    pub fn workspace(&self) -> PathBuf {
        self.user().join("workspaces/openrpg-forge")
    }

    pub fn pack(&self, version: &str) -> PathBuf {
        self.pack_with_options(version, Vec::new())
    }

    pub fn pack_with_options(&self, version: &str, optional_packs: Vec<String>) -> PathBuf {
        let source = self.root.join(format!("source-{version}"));
        fs::create_dir_all(source.join("bin/game-export-kit/bin")).unwrap();
        fs::write(source.join("LICENSE.txt"), b"fixture license").unwrap();
        fs::write(
            source.join("bin/forge-studio.exe"),
            format!("inert studio {version}"),
        )
        .unwrap();
        fs::write(
            source.join("bin/game-export-kit/bin/forge-module.exe"),
            b"inert runtime",
        )
        .unwrap();
        fs::write(
            source.join("bin/game-export-kit/bin/forge-game-launcher.exe"),
            b"inert launcher",
        )
        .unwrap();
        let descriptor = Descriptor {
            schema: "forge.delivery.package.v1".into(),
            id: "forge-studio".into(),
            label: "Studio fixture".into(),
            version: version.into(),
            kind: Kind::Applications,
            platform: "windows-x64".into(),
            source_revision: hash(b"fixture source"),
            rights: Rights::PrivateLocalOnly,
            license_paths: vec!["LICENSE.txt".into()],
            files: Vec::new(),
            product: Some(Product {
                schema: "forge.delivery.product.v3".into(),
                id: "openrpg-forge".into(),
                title: "OpenRPG Forge Studio".into(),
                apps: BTreeMap::from([(
                    "editor".into(),
                    Launch {
                        executable: "bin/forge-studio.exe".into(),
                        args: Vec::new(),
                        env: BTreeMap::from([
                            ("FORGE_AUTHORING_ROOT".into(), "{workspace}".into()),
                            (
                                "FORGE_NATIVE_GAME_EXECUTABLE".into(),
                                "{app}/bin/game-export-kit/bin/forge-module.exe".into(),
                            ),
                            (
                                "FORGE_NATIVE_GAME_CONTENT_ROOT".into(),
                                "{app}/bin/game-export-kit".into(),
                            ),
                            (
                                "FORGE_NATIVE_GAME_SESSION_ROOT".into(),
                                "{user}/game-sessions".into(),
                            ),
                        ]),
                        clear_env_prefixes: [
                            "FORGE_AUTHORING_",
                            "FORGE_TIDEWATER_",
                            "FORGE_NATIVE_GAME_",
                            "FORGE_MODULE_ASSET_",
                            "OPENRPG_ASSET_VAULT",
                            "WEBVIEW2_",
                        ]
                        .into_iter()
                        .map(str::to_owned)
                        .collect(),
                    },
                )]),
                required_packs: Vec::new(),
                optional_packs,
                authoring_seed: None,
            }),
        };
        let output = self.root.join(format!("studio-{version}.forgepack"));
        archive::pack(&source, descriptor, &output).unwrap();
        output
    }

    pub fn first_plan(&self, package: &Path) -> install::Plan {
        let mut plan = install::prepare(&self.install(), &[package.to_owned()]).unwrap();
        plan.user_root = self.user().to_string_lossy().into_owned();
        plan.command_id.clear();
        plan.command_id = hash(&delivery::json(&plan).unwrap());
        plan
    }
}

impl Drop for Fixture {
    fn drop(&mut self) {
        assert_eq!(self.root.parent(), Some(self.parent.as_path()));
        fs::remove_dir_all(&self.root).unwrap();
    }
}
