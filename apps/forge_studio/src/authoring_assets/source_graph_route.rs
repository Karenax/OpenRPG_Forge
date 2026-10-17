//! One carrier admission shared by source mounting and ordinary scene consumers.
//! Source programs may never reach a loader that would replace their materials.
use forge_assets::source_graph_glb::{
    SourceGraphGlb, SourceGraphGlbReadBudget, read_source_graph_glb_with_budget,
};

pub(super) fn read(bytes: &[u8]) -> Result<Option<SourceGraphGlb<'_>>, String> {
    let budget = if cfg!(target_arch = "wasm32") {
        SourceGraphGlbReadBudget::Standard
    } else {
        SourceGraphGlbReadBudget::LargeDesktop
    };
    read_source_graph_glb_with_budget(bytes, budget)
}

/// Used only by the existing ordinary glTF path. Source mounting has its own
/// explicit frame/material admission; errors cannot authorize a fallback.
#[cfg(test)]
pub(super) fn require_ordinary_loader(bytes: &[u8]) -> Result<(), String> {
    match read(bytes)? {
        None => Ok(()),
        Some(_) => Err(
            "Ce modèle nécessite son rendu matériau d’origine, encore en cours d’intégration."
                .into(),
        ),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn glb(json: &str) -> Vec<u8> {
        let mut json = json.as_bytes().to_vec();
        while json.len() % 4 != 0 {
            json.push(b' ');
        }
        let mut bytes = b"glTF".to_vec();
        bytes.extend_from_slice(&2u32.to_le_bytes());
        bytes.extend_from_slice(&(20u32 + json.len() as u32).to_le_bytes());
        bytes.extend_from_slice(&(json.len() as u32).to_le_bytes());
        bytes.extend_from_slice(&0x4e4f534au32.to_le_bytes());
        bytes.extend_from_slice(&json);
        bytes
    }
    #[test]
    fn ordinary_scene_keeps_its_loader() {
        assert!(require_ordinary_loader(&glb(r#"{"asset":{"version":"2.0"}}"#)).is_ok());
    }
    #[test]
    fn unrelated_extension_does_not_change_the_route() {
        assert!(
            require_ordinary_loader(&glb(
                r#"{"asset":{"version":"2.0"},"materials":[{"extensions":{"OTHER_vendor":{}}}]}"#
            ))
            .is_ok()
        );
    }
    #[test]
    fn malformed_source_program_cannot_fall_back_to_ordinary_materials() {
        assert!(require_ordinary_loader(&glb(r#"{"asset":{"version":"2.0"},"materials":[{"extensions":{"OPENRPG_source_graph":{}}}]}"#)).is_err());
    }
    #[test]
    fn broken_carrier_cannot_reach_the_ordinary_loader() {
        let mut bytes = glb(r#"{"asset":{"version":"2.0"}}"#);
        bytes[0] = 0;
        assert!(require_ordinary_loader(&bytes).is_err());
    }
}
