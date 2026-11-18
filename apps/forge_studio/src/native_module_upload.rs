use forge_project::workspace::MAX_MODULE_ORIGIN_PACKAGE_BYTES;
use forge_studio_frontend::module_import_protocol::{CHUNK_BYTES, size_error};
use std::sync::Mutex;

pub static UPLOAD: Mutex<Uploads> = Mutex::new(Uploads {
    next_id: 0,
    pending: None,
});

pub struct Uploads {
    next_id: u32,
    pending: Option<Upload>,
}

struct Upload {
    id: u32,
    window: String,
    expected: usize,
    bytes: Vec<u8>,
}

impl Uploads {
    pub fn start(&mut self, window: &str, expected: u32) -> Result<u32, String> {
        if expected == 0 || u64::from(expected) > MAX_MODULE_ORIGIN_PACKAGE_BYTES {
            return Err(size_error());
        }
        let id = self
            .next_id
            .checked_add(1)
            .ok_or("Compteur d’import épuisé.")?;
        self.pending = None;
        let mut bytes = Vec::new();
        bytes
            .try_reserve_exact(expected as usize)
            .map_err(|_| "Mémoire insuffisante pour recevoir le fichier .orpmod.")?;
        self.next_id = id;
        self.pending = Some(Upload {
            id,
            window: window.to_owned(),
            expected: expected as usize,
            bytes,
        });
        Ok(id)
    }

    pub fn append(
        &mut self,
        window: &str,
        id: u32,
        offset: usize,
        chunk: &[u8],
    ) -> Result<(), String> {
        let upload = self.current(window, id)?;
        if offset != upload.bytes.len()
            || chunk.is_empty()
            || chunk.len() > CHUNK_BYTES
            || chunk.len() > upload.expected - offset
        {
            return Err("Partie du fichier manquante, répétée ou trop volumineuse.".into());
        }
        upload.bytes.extend_from_slice(chunk);
        Ok(())
    }

    pub fn finish(&mut self, window: &str, id: u32) -> Result<Vec<u8>, String> {
        let upload = self.current(window, id)?;
        if upload.bytes.len() != upload.expected {
            return Err("Le fichier .orpmod est incomplet.".into());
        }
        self.pending
            .take()
            .map(|upload| upload.bytes)
            .ok_or_else(|| "Import interrompu.".into())
    }

    pub fn cancel(&mut self, window: &str, id: u32) {
        if self.current(window, id).is_ok() {
            self.pending = None;
        }
    }

    pub fn discard_window(&mut self, window: &str) {
        if self
            .pending
            .as_ref()
            .is_some_and(|upload| upload.window == window)
        {
            self.pending = None;
        }
    }

    fn current(&mut self, window: &str, id: u32) -> Result<&mut Upload, String> {
        self.pending
            .as_mut()
            .filter(|upload| upload.id == id && upload.window == window)
            .ok_or_else(|| {
                "Import remplacé ou interrompu. Sélectionnez le fichier de nouveau.".into()
            })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn module_upload_rejects_partial_replayed_and_superseded_parts() {
        let mut uploads = Uploads {
            next_id: 0,
            pending: None,
        };
        assert!(uploads.start("main", 0).is_err());
        let over_limit = u32::try_from(MAX_MODULE_ORIGIN_PACKAGE_BYTES + 1).unwrap();
        assert!(uploads.start("main", over_limit).is_err());
        let first = uploads.start("main", 4).unwrap();
        uploads.append("main", first, 0, &[1, 2]).unwrap();
        assert!(uploads.append("main", first, 0, &[1, 2]).is_err());
        assert!(uploads.append("atelier", first, 2, &[3, 4]).is_err());
        assert!(uploads.append("main", first, 2, &[3, 4, 5]).is_err());
        assert!(uploads.finish("main", first).is_err());
        let second = uploads.start("main", 4).unwrap();
        uploads.cancel("main", first);
        assert!(uploads.append("main", first, 0, &[1, 2]).is_err());
        uploads.append("main", second, 0, &[1, 2]).unwrap();
        uploads.append("main", second, 2, &[3, 4]).unwrap();
        assert_eq!(uploads.finish("main", second).unwrap(), [1, 2, 3, 4]);
        assert!(uploads.finish("main", second).is_err());
        let third = uploads.start("main", 4).unwrap();
        uploads.discard_window("atelier");
        uploads.append("main", third, 0, &[1, 2]).unwrap();
        uploads.discard_window("main");
        assert!(uploads.append("main", third, 2, &[3, 4]).is_err());
    }
}
