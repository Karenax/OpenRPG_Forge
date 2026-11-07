#[cfg(test)]
mod tests {
    use forge_engine::source_fish::shadow::{masked_mesh, shader_source};
    use forge_tidewater_assets::tidewater_fish_props::FishPropPackage;

    #[test]
    #[ignore = "Requires the byte-pinned acquired package; run the documented external-fixture replay"]
    fn depth_entries_validate_and_retain_exact_source_mask() {
        let root =
            std::path::PathBuf::from(std::env::var("FORGE_TIDEWATER_FISH_PROP_PACKAGE").unwrap());
        let package = FishPropPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            forge_tidewater_assets::tidewater_fish_props::package_hash(),
            |name, _| std::fs::read(root.join(name)).map_err(|error| error.to_string()),
        )
        .unwrap();
        let shader = shader_source(&package).unwrap();
        let mask = std::str::from_utf8(package.file("material/main-shadow.wgsl").unwrap()).unwrap();
        assert!(shader.contains(mask));
        assert!(!shader.contains("lodFade"));
        let module = naga::front::wgsl::parse_str(&shader).unwrap();
        naga::valid::Validator::new(
            naga::valid::ValidationFlags::all(),
            naga::valid::Capabilities::all(),
        )
        .validate(&module)
        .unwrap();
        assert_eq!(module.entry_points.len(), 2);
        let prepared = super::super::PreparedFishProps::from_package(&package).unwrap();
        assert_eq!(prepared.templates.len(), 78);
        for template in prepared.templates {
            let mut mesh = template.lod_meshes.into_iter().nth(2).unwrap();
            let packed = masked_mesh(&mesh).unwrap();
            assert_eq!(packed.vertices.len(), mesh.count_vertices() * 28);
            let bevy::mesh::VertexAttributeValues::Float32x4(data) =
                mesh.attribute(super::super::FISH_DATA).unwrap()
            else {
                panic!("source fin data changed")
            };
            for (vertex, fish) in packed.vertices.as_chunks::<28>().0.iter().zip(data) {
                assert_eq!(
                    &vertex[12..],
                    &fish
                        .iter()
                        .flat_map(|value| value.to_le_bytes())
                        .collect::<Vec<_>>()
                );
            }
            mesh.remove_attribute(super::super::FISH_DATA);
            assert!(masked_mesh(&mesh).is_err());
        }
        if let Ok(path) = std::env::var("FORGE_FISH_SHADOW_SHADER_OUTPUT") {
            std::fs::write(path, shader).unwrap();
        }
    }
}
