//! Shared creator definitions use the existing immutable object namespace.
use super::{ModuleCreatorReference, objects::ZoneObjectStore};
use forge_core::{ModuleId, bytes_sha256};
use forge_gameplay::{MAX_MODULE_CREATOR_BYTES, ModuleCreatorProfile};
use std::{
    fs,
    io::{Read, Write},
    sync::atomic::{AtomicU64, Ordering},
};

static NEXT_STAGE: AtomicU64 = AtomicU64::new(0);

impl ZoneObjectStore {
    pub(crate) fn read_creator(
        &self,
        module: ModuleId,
        pin: ModuleCreatorReference,
    ) -> Result<ModuleCreatorProfile, String> {
        pin.validate().map_err(|error| error.to_string())?;
        let path = self.root.join(format!("{}.json", pin.sha256));
        let metadata = fs::symlink_metadata(&path).map_err(|error| error.to_string())?;
        if !metadata.is_file() || metadata.file_type().is_symlink() || metadata.len() != pin.bytes {
            return Err("Creator object must be a regular file with the reviewed length".into());
        }
        let mut bytes = Vec::new();
        fs::File::open(&path)
            .map_err(|error| error.to_string())?
            .take(MAX_MODULE_CREATOR_BYTES as u64 + 1)
            .read_to_end(&mut bytes)
            .map_err(|error| error.to_string())?;
        if bytes.len() as u64 != pin.bytes || bytes_sha256(&bytes) != pin.sha256 {
            return Err("Creator object differs from its reviewed hash or length".into());
        }
        let definition =
            ModuleCreatorProfile::from_bytes(&bytes).map_err(|error| error.to_string())?;
        if definition.module_id != module
            || definition.to_bytes().map_err(|error| error.to_string())? != bytes
        {
            return Err("Creator object module identity or canonical encoding differs".into());
        }
        Ok(definition)
    }

    pub(crate) fn publish_creator(
        &self,
        definition: &ModuleCreatorProfile,
    ) -> Result<ModuleCreatorReference, String> {
        let bytes = definition.to_bytes().map_err(|error| error.to_string())?;
        let pin = ModuleCreatorReference {
            sha256: bytes_sha256(&bytes),
            bytes: bytes.len() as u64,
        };
        let destination = self.root.join(format!("{}.json", pin.sha256));
        if destination
            .try_exists()
            .map_err(|error| error.to_string())?
        {
            self.read_creator(definition.module_id, pin)?;
            return Ok(pin);
        }
        let sequence = NEXT_STAGE.fetch_add(1, Ordering::Relaxed);
        let stage = self.root.join(format!(
            ".creator-pending-{}-{sequence}",
            std::process::id()
        ));
        let mut file = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&stage)
            .map_err(|error| error.to_string())?;
        let mut consumed = false;
        let result = (|| {
            file.write_all(&bytes).map_err(|error| error.to_string())?;
            file.sync_all().map_err(|error| error.to_string())?;
            drop(file);
            match super::atomic_publish::publish(&stage, &destination) {
                Ok(value) => consumed = value,
                Err(error) if error.kind() == std::io::ErrorKind::AlreadyExists => (),
                Err(error) => return Err(error.to_string()),
            }
            self.read_creator(definition.module_id, pin)?;
            Ok(pin)
        })();
        let cleanup = if consumed {
            Ok(())
        } else {
            fs::remove_file(&stage).map_err(|error| error.to_string())
        };
        match (result, cleanup) {
            (Ok(pin), Ok(())) => Ok(pin),
            (Err(error), _) | (_, Err(error)) => Err(error),
        }
    }
}
