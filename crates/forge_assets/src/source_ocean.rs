//! Transport-neutral access to admitted ocean simulation inputs.

/// Supplies immutable inputs already admitted by a package reader.
/// The transport remains responsible for provenance, rights and content hashes;
/// the engine validates its own supported GPU ABI and bounds before staging.
pub trait OceanInputSource {
    fn file(&self, name: &str) -> Option<&[u8]>;
    fn bootstrap_frames(&self) -> usize;
    fn has_acquired_frames(&self) -> bool;
}
