//! Source graph shader registration: full SHA identity, UUID collision guards,
//! atomic admission and private import ownership. This is not GPU qualification.
use bevy::{
    asset::AssetId,
    prelude::*,
    shader::{ShaderImport, Source, ValidateShader},
};
use forge_core::{ContentHash, bytes_sha256};
use std::collections::BTreeMap;
#[derive(Clone)]
pub struct SourceGraphShaderArtifact {
    pub source: String,
    pub expected_sha256: ContentHash,
}
#[derive(Resource, Default)]
pub struct SourceGraphShaderRegistry {
    owned: BTreeMap<u128, ContentHash>,
}
fn id(hash: ContentHash) -> u128 {
    u128::from_be_bytes(hash.as_bytes()[..16].try_into().unwrap())
}
fn aid(value: u128) -> AssetId<Shader> {
    AssetId::Uuid {
        uuid: bevy::asset::uuid::Uuid::from_u128(value),
    }
}
/// Validate the entire batch before inserting any shader. UUID zero and
/// unowned asset IDs cannot be adopted or replaced. Private modules may not
/// take a native Bevy import name, even when their bytes match a proof.
pub fn register_source_graph_shaders(
    assets: &mut Assets<Shader>,
    registry: &mut SourceGraphShaderRegistry,
    artifacts: &[SourceGraphShaderArtifact],
) -> Result<Vec<u128>, &'static str> {
    let mut pending: BTreeMap<u128, (ContentHash, Shader)> = BTreeMap::new();
    let mut result = Vec::new();
    for artifact in artifacts {
        let hash = bytes_sha256(artifact.source.as_bytes());
        if hash != artifact.expected_sha256 {
            return Err("Source shader full SHA mismatch");
        }
        let uuid = id(hash);
        if uuid == 0 {
            return Err("Source shader UUID zero is forbidden");
        }
        let mut shader = Shader::from_wgsl(
            artifact.source.clone(),
            format!("source-graph://{hash}.wgsl"),
        );
        shader.validate_shader = ValidateShader::Enabled;
        if let ShaderImport::Custom(name) = &shader.import_path {
            if !name.starts_with("forge_source_graph::") {
                return Err("Source graph library import name is not privately owned");
            }
        }
        if let Some((other, previous)) = pending.get(&uuid) {
            if *other != hash {
                return Err("Source shader truncated UUID collision within batch");
            }
            if previous.import_path != shader.import_path {
                return Err("Source shader import identity differs within batch");
            }
        }
        for (other_uuid, (_, other)) in &pending {
            if *other_uuid != uuid && other.import_path == shader.import_path {
                return Err("Source shader import namespace collision within batch");
            }
        }
        for (asset_id, existing) in assets.iter() {
            if asset_id != aid(uuid) && existing.import_path == shader.import_path {
                return Err("Source shader import name already belongs to another asset");
            }
        }
        if let Some(owned) = registry.owned.get(&uuid) {
            if *owned != hash {
                return Err("Source shader truncated UUID collision with owned full SHA");
            }
            let existing = assets
                .get(aid(uuid))
                .ok_or("Owned source shader is missing from native assets")?;
            let Source::Wgsl(text) = &existing.source else {
                return Err("Owned source shader source kind changed");
            };
            if bytes_sha256(text.as_bytes()) != hash
                || existing.import_path != shader.import_path
                || existing.path != shader.path
                || !existing.shader_defs.is_empty()
                || !existing.additional_imports.is_empty()
            {
                return Err("Owned source shader bytes or import settings changed");
            }
        } else if assets.contains(aid(uuid)) {
            return Err("Source shader UUID already belongs to an unowned asset");
        }
        pending.insert(uuid, (hash, shader));
        result.push(uuid);
    }
    for (_, (_, shader)) in &pending {
        for import in &shader.imports {
            if let ShaderImport::Custom(name) = import {
                if name.starts_with("forge_source_graph::")
                    && !pending
                        .values()
                        .any(|(_, library)| library.import_path == *import)
                {
                    return Err("Source shader private dependency is absent from atomic batch");
                }
            }
        }
    }
    for (uuid, (hash, shader)) in pending {
        if !registry.owned.contains_key(&uuid) {
            assets
                .insert(aid(uuid), shader)
                .expect("UUID asset insertion is infallible");
            registry.owned.insert(uuid, hash);
        }
    }
    Ok(result)
}
#[cfg(test)]
mod tests {
    use super::*;
    const A: &str = "@compute @workgroup_size(1) fn main() {}";
    fn artifact(source: &str) -> SourceGraphShaderArtifact {
        SourceGraphShaderArtifact {
            source: source.to_owned(),
            expected_sha256: bytes_sha256(source.as_bytes()),
        }
    }
    #[test]
    fn source_graph_registration_reimport_is_idempotent_and_mutation_refuses() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let a = register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A)]).unwrap();
        assert_eq!(
            a,
            register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A)]).unwrap()
        );
        assert_eq!(assets.len(), 1);
        assets.get_mut(aid(a[0])).unwrap().source = Source::Wgsl("modified".into());
        assert!(register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A)]).is_err());
        assert_eq!(assets.len(), 1);
    }
    #[test]
    fn source_graph_registration_bad_batch_does_not_insert_first_valid_shader() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let bad = SourceGraphShaderArtifact {
            source: "changed".into(),
            expected_sha256: bytes_sha256(A.as_bytes()),
        };
        assert!(
            register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A), bad]).is_err()
        );
        assert!(assets.is_empty());
        assert!(registry.owned.is_empty());
    }
    #[test]
    fn source_graph_registration_foreign_uuid_and_full_sha_collision_preserve_assets() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let hash = bytes_sha256(A.as_bytes());
        let uuid = id(hash);
        assets
            .insert(
                aid(uuid),
                Shader::from_wgsl("foreign".to_owned(), "foreign".to_owned()),
            )
            .unwrap();
        assert!(register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A)]).is_err());
        assert_eq!(assets.get(aid(uuid)).unwrap().source.as_str(), "foreign");
        let mut different = *hash.as_bytes();
        different[31] ^= 1;
        registry
            .owned
            .insert(uuid, ContentHash::from_bytes(different));
        assert!(register_source_graph_shaders(&mut assets, &mut registry, &[artifact(A)]).is_err());
        assert_eq!(assets.get(aid(uuid)).unwrap().source.as_str(), "foreign");
    }
    #[test]
    fn source_graph_registration_native_namespace_and_private_namespace_collision_refuse() {
        let mut assets = Assets::<Shader>::default();
        let mut registry = SourceGraphShaderRegistry::default();
        let native = "#define_import_path bevy_pbr::lighting\nfn a() {}";
        assert!(
            register_source_graph_shaders(&mut assets, &mut registry, &[artifact(native)]).is_err()
        );
        assert!(assets.is_empty());
        let a = "#define_import_path forge_source_graph::proof\nfn a() {}";
        let b = "#define_import_path forge_source_graph::proof\nfn b() {}";
        assert!(
            register_source_graph_shaders(&mut assets, &mut registry, &[artifact(a), artifact(b)])
                .is_err()
        );
        assert!(assets.is_empty());
    }
}
