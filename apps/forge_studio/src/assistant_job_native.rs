//! Native atomic job storage, rooted by the application rather than tool arguments.
use forge_core::{CommandId, ContentHash, document_store::DocumentStore};
use forge_project::assistant::{
    jobs::{JobJournal, JobSnapshot},
    provider::{OutboundApproval, ProviderRequest, ProviderResult},
};
use std::{
    fs,
    path::{Path, PathBuf},
};

mod recovery;
pub use recovery::JobRecoveryView;

/// A bounded, non-recursive page of native job identities. No caller paths.
pub(crate) fn recovery_page_ids(
    root: &Path,
    offset: usize,
) -> Result<(Vec<CommandId>, Option<usize>), String> {
    const PAGE: usize = 32;
    if offset > 100_000 {
        return Err("Revenir au début de la liste des demandes.".into());
    }
    let root = root.canonicalize().map_err(|error| error.to_string())?;
    let group = root.join("assistant-jobs");
    match fs::symlink_metadata(&group) {
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => {
            return Ok((Vec::new(), None));
        }
        Ok(metadata) if metadata.is_dir() && !reparse(&metadata) => (),
        _ => return Err("Le dossier des demandes ne peut pas être parcouru.".into()),
    }
    let mut ids = Vec::new();
    let entries = fs::read_dir(group).map_err(|error| error.to_string())?;
    for entry in entries.skip(offset).take(PAGE) {
        let entry = entry.map_err(|error| error.to_string())?;
        let metadata = fs::symlink_metadata(entry.path()).map_err(|error| error.to_string())?;
        if !metadata.is_dir() || reparse(&metadata) {
            return Err(
                "Une entrée de demande est invalide ; les liens ne sont pas suivis.".into(),
            );
        }
        let id = entry
            .file_name()
            .to_str()
            .ok_or("Identité de demande illisible")?
            .parse::<CommandId>()
            .map_err(|_| "Identité de demande invalide")?;
        job_directory(&root, id, false)?;
        ids.push(id);
    }
    let next = (ids.len() == PAGE).then_some(offset + PAGE);
    Ok((ids, next))
}

pub struct StoredJob {
    store: DocumentStore,
    journal: JobJournal,
    saved_hash: Option<ContentHash>,
}

impl StoredJob {
    #[must_use]
    pub const fn limits(&self) -> forge_project::assistant::jobs::JobLimits {
        self.journal.limits()
    }

    pub fn verify_request(&self, request: &ProviderRequest) -> Result<(), String> {
        self.journal.verify_request(request)
    }

    pub fn create(authorized_root: &Path, journal: JobJournal) -> Result<Self, String> {
        let directory = job_directory(authorized_root, journal.snapshot()?.job_id, true)?;
        let store = DocumentStore::open(&directory).map_err(|error| error.to_string())?;
        if store.load().map_err(|error| error.to_string())?.is_some()
            || store
                .pending()
                .map_err(|error| error.to_string())?
                .is_some()
        {
            return Err("Job already exists or has a pending save; reopen it".into());
        }
        let bytes = journal.archive_bytes()?;
        let saved_hash = Some(
            store
                .save(None, &bytes)
                .map_err(|error| error.to_string())?,
        );
        Ok(Self {
            store,
            journal,
            saved_hash,
        })
    }

    pub fn open(authorized_root: &Path, id: CommandId) -> Result<Self, String> {
        let directory = job_directory(authorized_root, id, false)?;
        let store = DocumentStore::open(&directory).map_err(|error| error.to_string())?;
        if store
            .pending()
            .map_err(|error| error.to_string())?
            .is_some()
        {
            return Err("Interrupted job save requires explicit recovery".into());
        }
        let saved = store
            .load()
            .map_err(|error| error.to_string())?
            .ok_or("No saved job")?;
        let journal = JobJournal::from_bytes(&saved.bytes)?;
        if journal.snapshot()?.job_id != id {
            return Err("Saved job identity differs from its directory".into());
        }
        Ok(Self {
            store,
            journal,
            saved_hash: Some(saved.hash),
        })
    }

    pub fn snapshot(&self) -> Result<JobSnapshot, String> {
        self.journal.snapshot()
    }

    /// The durable Started event is committed before the caller dispatches HTTP.
    pub fn begin(
        &mut self,
        request: &ProviderRequest,
        approval: &OutboundApproval,
    ) -> Result<u8, String> {
        self.change(|journal| journal.begin(request, approval))
    }
    pub fn interrupt(&mut self, attempt: u8) -> Result<(), String> {
        self.change(|journal| journal.interrupt(attempt))
    }
    pub fn stage(&mut self, attempt: u8, result: ProviderResult) -> Result<(), String> {
        self.change(|journal| journal.stage(attempt, result))
    }
    pub fn cancel(&mut self) -> Result<(), String> {
        self.change(JobJournal::cancel)
    }
    pub fn restore_unpersisted_application(
        &mut self,
        request: &ProviderRequest,
    ) -> Result<(), String> {
        self.change(|journal| journal.restore_unpersisted_application(request))
    }
    pub fn acknowledge_applied(
        &mut self,
        command: CommandId,
        resulting_hash: ContentHash,
    ) -> Result<(), String> {
        self.change(|journal| journal.acknowledge_applied(command, resulting_hash))
    }

    fn change<T>(
        &mut self,
        change: impl FnOnce(&mut JobJournal) -> Result<T, String>,
    ) -> Result<T, String> {
        let mut candidate = JobJournal::from_bytes(&self.journal.archive_bytes()?)?;
        let result = change(&mut candidate)?;
        let hash = self
            .store
            .save(self.saved_hash, &candidate.archive_bytes()?)
            .map_err(|error| error.to_string())?;
        self.journal = candidate;
        self.saved_hash = Some(hash);
        Ok(result)
    }
}

fn job_directory(root: &Path, id: CommandId, create: bool) -> Result<PathBuf, String> {
    let root = root.canonicalize().map_err(|error| error.to_string())?;
    if !root.is_dir() {
        return Err("Job root must be an existing authorized directory".into());
    }
    let group = root.join("assistant-jobs");
    for path in [&group, &group.join(id.to_string())] {
        match fs::symlink_metadata(path) {
            Ok(metadata) if metadata.is_dir() && !reparse(&metadata) => (),
            Ok(_) => return Err("Job directory cannot be a link or non-directory".into()),
            Err(error) if error.kind() == std::io::ErrorKind::NotFound && create => {
                fs::create_dir(path).map_err(|error| error.to_string())?;
                let metadata = fs::symlink_metadata(path).map_err(|error| error.to_string())?;
                if !metadata.is_dir() || reparse(&metadata) {
                    return Err("Invalid job directory".into());
                }
            }
            Err(error) => return Err(error.to_string()),
        }
        if !path
            .canonicalize()
            .map_err(|error| error.to_string())?
            .starts_with(&root)
        {
            return Err("Job directory leaves authorized root".into());
        }
    }
    Ok(group.join(id.to_string()))
}

fn reparse(metadata: &fs::Metadata) -> bool {
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        metadata.file_attributes() & 0x400 != 0
    }
    #[cfg(not(windows))]
    {
        metadata.file_type().is_symlink()
    }
}
