//! Read the historic reference data already admitted by the Studio installation.
use crate::{Result, manifest};
use forge_assets::AssetCatalog;
use forge_assets::source_environment_package::{EnvironmentPackage, ResourcePin};
use forge_assets::source_surface_package::SurfacePackage;
use forge_assets::source_vegetation_package::{
    LegacyPlantIndex, PlantPackage, PlantResourcePin, profile::PlantPrototype,
};
use forge_core::{AreaId, AssetId, ContentHash, bytes_sha256};
use forge_project::{
    module_package::{
        ModuleEnvironmentBinding, ModuleEnvironmentFiles, ModuleEnvironmentSource, ModuleFile,
        ModuleFilePin, ModuleFileRole, ModuleObjectAsset, ModuleObjectBinding, ModuleObjectFiles,
        ModuleObjectSource, ModuleVegetationBinding, ModuleVegetationSource,
    },
    zone_authoring::session::ZoneEditSession,
};
use std::{
    collections::BTreeMap,
    fs::File,
    io::Read,
    path::{Path, PathBuf},
};

pub const OBJECTS_PREFIX: &str = "data/buildings/";
const CLOUDS_PREFIX: &str = "data/clouds/";
const CLOUDS_METADATA: &str = "data/clouds/environment.json";
const OBJECTS_INDEX: &str = "data/buildings/package.json";
const OBJECTS_CATALOG: &str = "data/buildings/catalog.json";
const ENVIRONMENT_PREFIX: &str = "legacy-environment/";
const ENVIRONMENT_INDEX: &str = "legacy-environment/package.json";

pub struct ReferenceScene {
    root: PathBuf,
    revision: ContentHash,
    inventory: BTreeMap<String, PlantResourcePin>,
    license_paths: Vec<String>,
}

pub struct ReferenceArea {
    index: LegacyPlantIndex,
    environment: Option<Vec<u8>>,
}

pub struct ReferenceHistory {
    root: PathBuf,
    files: BTreeMap<String, manifest::Entry>,
    notice: Vec<u8>,
}

impl ReferenceHistory {
    /// Admit a private supplement bound to the exact installed reference catalogue.
    pub fn open(root: &Path, revision: ContentHash, reference: ContentHash) -> Result<Self> {
        let descriptor_path = manifest::confined(root, "package-manifest.json")?;
        let raw = read_bounded(&descriptor_path, manifest::MAX_MANIFEST as usize)?;
        if bytes_sha256(&raw) != revision {
            return Err("Legacy model history inventory differs from its expected pin".into());
        }
        let descriptor: manifest::Descriptor =
            serde_json::from_slice(&raw).map_err(|error| error.to_string())?;
        descriptor.validate_host()?;
        if descriptor.id != "reference-model-history"
            || descriptor.kind != manifest::Kind::Assets
            || descriptor.rights != manifest::Rights::PrivateLocalOnly
            || descriptor.source_revision != format!("reference-assets/{reference}")
            || descriptor.license_paths.as_slice() != ["PROVENANCE.md"]
        {
            return Err("Invalid private reference model history descriptor".into());
        }
        let files: BTreeMap<_, _> = descriptor
            .files
            .into_iter()
            .map(|entry| (entry.path.clone(), entry))
            .collect();
        let mut history = Self {
            root: root.to_owned(),
            files,
            notice: Vec::new(),
        };
        history.notice = history
            .read("PROVENANCE.md", 1024 * 1024)?
            .ok_or("Missing pinned reference model history notice")?;
        Ok(history)
    }

    #[must_use]
    pub fn notice(&self) -> &[u8] {
        &self.notice
    }

    pub fn model(&self, id: AssetId, pin: ContentHash, limit: usize) -> Result<Option<Vec<u8>>> {
        let name = format!("models/{id}/{pin}.glb");
        let bytes = self.read(&name, limit)?;
        if bytes
            .as_ref()
            .is_some_and(|bytes| bytes_sha256(bytes) != pin)
        {
            return Err("Legacy model history content differs from the authored model pin".into());
        }
        Ok(bytes)
    }

    fn read(&self, name: &str, limit: usize) -> Result<Option<Vec<u8>>> {
        let Some(entry) = self.files.get(name) else {
            return Ok(None);
        };
        let size = usize::try_from(entry.bytes).map_err(|error| error.to_string())?;
        if size > limit {
            return Err("Legacy model history entry exceeds its read budget".into());
        }
        let path = manifest::confined(&self.root, name)?;
        let bytes = read_bounded(&path, size)?;
        if bytes.len() != size || bytes_sha256(&bytes).to_string() != entry.sha256 {
            return Err("Legacy model history entry differs from its admitted inventory".into());
        }
        Ok(Some(bytes))
    }
}

pub struct ReferenceBindings {
    pub vegetation: ModuleVegetationBinding,
    pub objects: Option<ModuleObjectBinding>,
    pub environment: Option<ModuleEnvironmentBinding>,
    pub files: Vec<ModuleFile>,
}

impl ReferenceScene {
    pub fn open(scene: &Path, revision: ContentHash) -> Result<Self> {
        let root = scene
            .parent()
            .and_then(Path::parent)
            .ok_or("Invalid reference scene path")?;
        if !root.is_absolute() || manifest::confined(root, manifest::REFERENCE_SCENE)? != scene {
            return Err("Invalid reference scene path".into());
        }
        let descriptor_path = manifest::confined(root, "package-manifest.json")?;
        let bytes = read_bounded(&descriptor_path, manifest::MAX_MANIFEST as usize)?;
        if bytes_sha256(&bytes) != revision {
            return Err("Legacy reference inventory pin differs".into());
        }
        let descriptor: manifest::Descriptor =
            serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        descriptor.validate_host()?;
        manifest::reference_catalog(&descriptor)?;
        let license_paths = descriptor.license_paths;
        let prefix = format!("{}/", manifest::REFERENCE_SCENE);
        let inventory = descriptor
            .files
            .into_iter()
            .filter(|entry| {
                entry.path.starts_with(&prefix)
                    || entry.path.starts_with(OBJECTS_PREFIX)
                    || entry.path.starts_with(CLOUDS_PREFIX)
                    || license_paths.contains(&entry.path)
            })
            .map(|entry| {
                Ok((
                    entry.path,
                    PlantResourcePin {
                        sha256: ContentHash::parse(&entry.sha256).map_err(|e| e.to_string())?,
                        bytes: usize::try_from(entry.bytes).map_err(|e| e.to_string())?,
                    },
                ))
            })
            .collect::<Result<_>>()?;
        Ok(Self {
            root: root.to_owned(),
            revision,
            inventory,
            license_paths,
        })
    }

    #[must_use]
    pub fn revision(&self) -> ContentHash {
        self.revision
    }

    /// Preserve declared notices and pinned scene credits from the private installation.
    pub fn read_notices(&self) -> Result<Vec<(String, Vec<u8>)>> {
        // Older packs listed only their provenance summary as a licence path.
        // Scene credits were pinned in the inventory as ordinary media files.
        let scene_credits = self.inventory.keys().filter(|path| {
            path.rsplit_once('/').is_some_and(|(directory, name)| {
                directory == manifest::REFERENCE_SCENE
                    && (name == "CREDITS.md"
                        || (name.starts_with("CREDITS-") && name.ends_with(".md")))
            }) && !self.license_paths.contains(path)
        });
        let mut remaining = 1024 * 1024;
        self.license_paths
            .iter()
            .chain(scene_credits)
            .map(|path| {
                let bytes = self.read_pinned(path, remaining)?;
                remaining -= bytes.len();
                Ok((path.clone(), bytes))
            })
            .collect()
    }

    /// An authored raster with unchanged imported prototypes can retain the
    /// installed reference closure. Other areas use their own sources.
    pub fn terrain_pin(zone: &ZoneEditSession) -> Result<Option<ContentHash>> {
        let head = zone.head();
        let (terrain, vegetation) = head.effective()?;
        if !vegetation.is_some_and(|value| value.imported_source.is_some())
            || head
                .corrections
                .species
                .as_ref()
                .is_some_and(|species| !species.is_empty())
        {
            return Ok(None);
        }
        Ok(Some(
            terrain
                .sampled_source
                .ok_or("Legacy vegetation requires its sampled terrain")?
                .samples_sha256,
        ))
    }

    pub fn plant_index(&self, terrain: ContentHash) -> Result<LegacyPlantIndex> {
        let fixture = "fixture.json";
        let pin = self.pin(fixture)?;
        let fixture = self.read(fixture, 4 * 1024 * 1024)?;
        PlantPackage::index_legacy_fixture(
            &fixture,
            pin.sha256,
            terrain,
            |name| self.pin(name).cloned(),
            |name, limit| self.read(name, limit),
        )
    }

    pub fn index_area(&self, terrain: ContentHash) -> Result<ReferenceArea> {
        Ok(ReferenceArea {
            index: self.plant_index(terrain)?,
            environment: self.environment_index()?,
        })
    }

    pub fn read(&self, name: &str, limit: usize) -> Result<Vec<u8>> {
        self.read_pinned(&format!("{}/{name}", manifest::REFERENCE_SCENE), limit)
    }

    fn pin(&self, name: &str) -> Result<&PlantResourcePin> {
        self.pinned_file(&format!("{}/{name}", manifest::REFERENCE_SCENE))
    }

    pub fn environment_index(&self) -> Result<Option<Vec<u8>>> {
        if !self.inventory.contains_key(CLOUDS_METADATA) {
            return Ok(None);
        }
        let metadata = self.read_pinned(CLOUDS_METADATA, 256 * 1024)?;
        EnvironmentPackage::index_legacy_fixture(&metadata, |name| {
            let pin = self.pinned_file(&environment_path(name))?;
            Ok(ResourcePin {
                sha256: pin.sha256,
                bytes: pin.bytes,
            })
        })
        .map(Some)
    }

    pub fn read_environment(&self, name: &str, limit: usize) -> Result<Vec<u8>> {
        self.read_pinned(&environment_path(name), limit)
    }

    /// Bind the installed surface package only when the map references an exact catalog pin.
    pub fn objects(
        &self,
        zone: &ZoneEditSession,
    ) -> Result<Option<(ModuleObjectBinding, Vec<ModuleFile>)>> {
        let Some(index) = self.inventory.get(OBJECTS_INDEX) else {
            return Ok(None);
        };
        let catalog: AssetCatalog =
            serde_json::from_slice(&self.read_object(OBJECTS_CATALOG, 2 * 1024 * 1024)?)
                .map_err(|error| error.to_string())?;
        catalog.validate_import_metadata()?;
        let mut assets: Vec<_> = catalog
            .assets
            .iter()
            .map(|asset| ModuleObjectAsset {
                asset_id: asset.id,
                sha256: asset.content_hash,
            })
            .collect();
        assets.sort_unstable_by_key(|asset| asset.asset_id);
        let mut referenced = false;
        zone.visit_referenced_model_placements(|placement| {
            referenced |= assets
                .binary_search_by_key(&placement.asset_id, |asset| asset.asset_id)
                .is_ok_and(|i| assets[i].sha256 == placement.asset_hash);
            Ok(())
        })?;
        if !referenced {
            return Ok(None);
        }
        let raw = self.read_object(OBJECTS_INDEX, 2 * 1024 * 1024)?;
        let declared = SurfacePackage::resource_pins(&raw, index.sha256)?;
        let license = SurfacePackage::license_name(&raw, index.sha256)?;
        let mut files = vec![ModuleFile {
            path: OBJECTS_INDEX.into(),
            sha256: index.sha256,
            bytes: index.bytes as u64,
            role: ModuleFileRole::ObjectSource,
        }];
        let mut resources = Vec::with_capacity(declared.len());
        for (name, pin) in declared {
            let path = format!("{OBJECTS_PREFIX}{name}");
            let installed = self.pinned_file(&path)?;
            if (installed.sha256, installed.bytes) != (pin.sha256, pin.bytes) {
                return Err(format!(
                    "Legacy object resource differs from its installed pin: {path}"
                ));
            }
            resources.push(ModuleFilePin {
                path: path.clone(),
                sha256: pin.sha256,
            });
            files.push(ModuleFile {
                path,
                sha256: pin.sha256,
                bytes: pin.bytes as u64,
                role: if name == license {
                    ModuleFileRole::License
                } else {
                    ModuleFileRole::ObjectSource
                },
            });
        }
        Ok(Some((
            ModuleObjectBinding {
                area_id: zone.head().base.terrain.area_id,
                source: ModuleObjectSource::SourceSurfaces {
                    package: ModuleObjectFiles {
                        manifest: ModuleFilePin {
                            path: OBJECTS_INDEX.into(),
                            sha256: index.sha256,
                        },
                        resources,
                    },
                    assets,
                },
            },
            files,
        )))
    }

    pub fn read_object(&self, path: &str, limit: usize) -> Result<Vec<u8>> {
        if !path.starts_with(OBJECTS_PREFIX) {
            return Err("Legacy object path is outside the reference surface package".into());
        }
        self.read_pinned(path, limit)
    }

    fn read_pinned(&self, name: &str, limit: usize) -> Result<Vec<u8>> {
        let pin = self.pinned_file(name)?;
        if pin.bytes > limit {
            return Err(format!(
                "Legacy reference resource exceeds its budget: {name}"
            ));
        }
        let path = manifest::confined(&self.root, name)?;
        let bytes = read_bounded(&path, pin.bytes)?;
        if bytes.len() != pin.bytes || bytes_sha256(&bytes) != pin.sha256 {
            return Err(format!("Legacy reference resource changed: {name}"));
        }
        Ok(bytes)
    }

    fn pinned_file(&self, name: &str) -> Result<&PlantResourcePin> {
        self.inventory
            .get(name)
            .ok_or_else(|| format!("Legacy reference resource absent: {name}"))
    }
}

impl ReferenceArea {
    pub fn describe(
        &self,
        scene: &ReferenceScene,
        zone: &ZoneEditSession,
    ) -> Result<ReferenceBindings> {
        let zone_head = zone.head();
        let (_, vegetation) = zone_head.effective()?;
        let vegetation = vegetation.ok_or("Legacy vegetation document absent")?;
        let source = vegetation
            .imported_source
            .as_ref()
            .ok_or("Legacy vegetation source absent")?;
        for block in &source.blocks {
            PlantPrototype::validate_parameters(&block.parameter_names)?;
            PlantPrototype::from_key(&block.prototype_key)?;
        }
        let index_pin = bytes_sha256(&self.index.manifest);
        if source
            .grass
            .as_ref()
            .is_some_and(|grass| grass.profile_manifest_sha256 != index_pin)
        {
            return Err("Authored grass profile differs from the installed reference index".into());
        }
        let area_id = zone_head.base.terrain.area_id;
        let resources = self
            .index
            .files
            .iter()
            .map(|(path, pin)| ModuleFilePin {
                path: path.clone(),
                sha256: pin.sha256,
            })
            .collect();
        let mut files: Vec<_> = self
            .index
            .files
            .iter()
            .map(|(path, pin)| ModuleFile {
                path: path.clone(),
                sha256: pin.sha256,
                bytes: pin.bytes as u64,
                role: if path == "LICENSE-Tidewater.txt" {
                    ModuleFileRole::License
                } else {
                    ModuleFileRole::Vegetation
                },
            })
            .collect();
        files.push(ModuleFile {
            path: "package.json".into(),
            sha256: index_pin,
            bytes: self.index.manifest.len() as u64,
            role: ModuleFileRole::Vegetation,
        });
        let objects = scene.objects(zone)?.map(|(binding, object_files)| {
            files.extend(object_files);
            binding
        });
        let environment = self
            .environment
            .as_ref()
            .map(|index| environment_binding(area_id, index, &mut files))
            .transpose()?;
        Ok(ReferenceBindings {
            vegetation: ModuleVegetationBinding {
                area_id,
                vegetation_sha256: vegetation.content_hash().map_err(|e| e.to_string())?,
                sources: vec![ModuleVegetationSource::ImportedPrototypes {
                    index: ModuleFilePin {
                        path: "package.json".into(),
                        sha256: index_pin,
                    },
                    resources,
                }],
            },
            objects,
            environment,
            files,
        })
    }

    pub fn read_resource(
        &self,
        scene: &ReferenceScene,
        name: &str,
        size: usize,
    ) -> Result<Vec<u8>> {
        match name {
            "package.json" => Ok(self.index.manifest.clone()),
            "plant-textures.json" => Ok(self.index.texture_metadata.clone()),
            ENVIRONMENT_INDEX => self
                .environment
                .clone()
                .ok_or("Legacy environment index absent".into()),
            path if path.starts_with(ENVIRONMENT_PREFIX) => {
                scene.read_environment(&path[ENVIRONMENT_PREFIX.len()..], size)
            }
            path if path.starts_with(OBJECTS_PREFIX) => scene.read_object(path, size),
            _ => scene.read(name, size),
        }
    }
}

fn environment_binding(
    area_id: AreaId,
    index: &[u8],
    files: &mut Vec<ModuleFile>,
) -> Result<ModuleEnvironmentBinding> {
    let sha256 = bytes_sha256(index);
    files.push(ModuleFile {
        path: ENVIRONMENT_INDEX.into(),
        sha256,
        bytes: index.len() as u64,
        role: ModuleFileRole::Environment,
    });
    let resources = EnvironmentPackage::resource_pins(index, sha256)?
        .into_iter()
        .map(|(name, pin)| {
            let path = format!("{ENVIRONMENT_PREFIX}{name}");
            files.push(ModuleFile {
                path: path.clone(),
                sha256: pin.sha256,
                bytes: pin.bytes as u64,
                role: if name == "LICENSE-Tidewater.txt" {
                    ModuleFileRole::License
                } else {
                    ModuleFileRole::Environment
                },
            });
            ModuleFilePin {
                path,
                sha256: pin.sha256,
            }
        })
        .collect();
    Ok(ModuleEnvironmentBinding {
        area_id,
        source: ModuleEnvironmentSource::SourceProfile {
            environment: ModuleEnvironmentFiles {
                manifest: ModuleFilePin {
                    path: ENVIRONMENT_INDEX.into(),
                    sha256,
                },
                resources,
            },
            programs: None,
        },
        solar_clock: None,
    })
}

fn environment_path(name: &str) -> String {
    match name {
        "cloud-environment.json" => CLOUDS_METADATA.into(),
        name if name.starts_with("cloud-") && name != "cloud-view-params.bin" => {
            format!("{CLOUDS_PREFIX}{name}")
        }
        name => format!("{}/{name}", manifest::REFERENCE_SCENE),
    }
}

fn read_bounded(path: &Path, limit: usize) -> Result<Vec<u8>> {
    let file = File::open(path).map_err(|error| error.to_string())?;
    let mut bytes = Vec::new();
    file.take(limit as u64 + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| error.to_string())?;
    if bytes.len() > limit {
        return Err("Legacy reference file exceeds its size limit".into());
    }
    Ok(bytes)
}
