use super::*;

struct Fixture(PathBuf);
impl Fixture {
    fn new() -> Self {
        let path =
            std::env::temp_dir().join(format!("forge-vault-storage-{}", crate::attempt().unwrap()));
        fs::create_dir(&path).unwrap();
        fs::create_dir_all(path.join(".forge-storage/packs")).unwrap();
        fs::write(
            path.join(".forge-storage/root.v1.json"),
            br#"{"schema":1,"vault_id":"11111111111111111111111111111111"}"#,
        )
        .unwrap();
        Self(path)
    }
    fn packed(&self, bytes: &[u8], codec: &str) -> (PathBuf, Descriptor) {
        let encoded = if codec == "zlib" {
            let mut writer =
                flate2::write::ZlibEncoder::new(Vec::new(), flate2::Compression::default());
            writer.write_all(bytes).unwrap();
            writer.finish().unwrap()
        } else {
            bytes.to_vec()
        };
        fs::write(
            self.0
                .join(".forge-storage/packs/22222222222222222222222222222222.pack"),
            &encoded,
        )
        .unwrap();
        let descriptor = Descriptor {
            schema: 1,
            vault_id: "1".repeat(32),
            bytes: bytes.len() as u64,
            sha256: crate::hash(bytes),
            segments: vec![Segment {
                pack: "2".repeat(32),
                offset: 0,
                stored_bytes: encoded.len() as u64,
                bytes: bytes.len() as u64,
                sha256: crate::hash(bytes),
                codec: codec.into(),
            }],
        };
        let path = self.0.join("model.glb");
        self.descriptor(&path, &descriptor);
        (path, descriptor)
    }
    fn descriptor(&self, path: &Path, descriptor: &Descriptor) {
        let mut bytes = MAGIC.to_vec();
        bytes.extend(serde_json::to_vec(descriptor).unwrap());
        fs::write(path, bytes).unwrap();
    }
}
impl Drop for Fixture {
    fn drop(&mut self) {
        // This path is a unique directory created by this fixture, never a caller path.
        if self.0.starts_with(std::env::temp_dir())
            && self
                .0
                .file_name()
                .unwrap()
                .to_string_lossy()
                .starts_with("forge-vault-storage-")
        {
            fs::remove_dir_all(&self.0).unwrap();
        }
    }
}

#[test]
fn plain_and_compressed_bytes_keep_the_same_hash_and_export() {
    let f = Fixture::new();
    let bytes = b"glTF exact bytes with original zero padding\0\0\0".repeat(4096);
    let plain = f.0.join("plain.glb");
    fs::write(&plain, &bytes).unwrap();
    for codec in ["raw", "zlib"] {
        let (path, _) = f.packed(&bytes, codec);
        assert_eq!(
            logical_size(&f.0, &path, bytes.len() as u64).unwrap(),
            bytes.len() as u64
        );
        assert_eq!(read_file(&f.0, &path, bytes.len() as u64).unwrap(), bytes);
        assert_eq!(
            file_hash(&f.0, &path, bytes.len() as u64).unwrap(),
            file_hash(&f.0, &plain, bytes.len() as u64).unwrap()
        );
        assert!(read_file(&f.0, &path, bytes.len() as u64 - 1).is_err());
    }
}

#[test]
fn corruption_wrong_root_truncation_and_logical_hash_are_refused() {
    let f = Fixture::new();
    let bytes = b"shared payload".repeat(100);
    let (path, descriptor) = f.packed(&bytes, "zlib");
    let mut invalid = descriptor.clone();
    invalid.sha256 = "0".repeat(64);
    f.descriptor(&path, &invalid);
    assert!(read_file(&f.0, &path, 4096).is_err());
    invalid = descriptor.clone();
    invalid.vault_id = "0".repeat(32);
    f.descriptor(&path, &invalid);
    assert!(logical_size(&f.0, &path, 4096).is_err());
    f.descriptor(&path, &descriptor);
    fs::write(
        f.0.join(".forge-storage/packs/22222222222222222222222222222222.pack"),
        b"corrupt",
    )
    .unwrap();
    assert!(read_file(&f.0, &path, 4096).is_err());
    assert!(read_file(&f.0.join("missing"), &path, 4096).is_err());
}

#[test]
fn path_traversal_lengths_unknown_codecs_and_expansion_bombs_are_refused() {
    let f = Fixture::new();
    let (path, value) = f.packed(b"payload", "raw");
    for field in ["path", "length", "codec", "offset", "expansion", "hash"] {
        let mut bad = value.clone();
        match field {
            "path" => bad.segments[0].pack = "../../elsewhere".into(),
            "length" => bad.bytes += 1,
            "codec" => bad.segments[0].codec = "unknown".into(),
            "offset" => bad.segments[0].offset = u64::MAX,
            "expansion" => {
                bad.bytes = MAX_BLOCK + 1;
                bad.segments[0].bytes = bad.bytes;
            }
            _ => bad.segments[0].sha256 = "garbage".into(),
        }
        f.descriptor(&path, &bad);
        assert!(
            read_file(&f.0, &path, MAX_BLOCK * 2).is_err(),
            "accepted {field}"
        );
    }
}

#[test]
fn shared_segments_reconstruct_different_models_without_merging_their_identity() {
    let f = Fixture::new();
    let (a, mut descriptor) = f.packed(b"same", "raw");
    let mut expected = b"same".to_vec();
    expected.extend(b"same");
    descriptor.bytes = 8;
    descriptor.sha256 = crate::hash(&expected);
    descriptor.segments.push(descriptor.segments[0].clone());
    let b = f.0.join("different-model.glb");
    f.descriptor(&b, &descriptor);
    assert_eq!(read_file(&f.0, &a, 8).unwrap(), b"same");
    assert_eq!(read_file(&f.0, &b, 8).unwrap(), expected);
}

#[test]
fn trailing_compressed_bytes_and_a_descriptor_over_budget_are_refused() {
    let f = Fixture::new();
    let (path, mut descriptor) = f.packed(b"payload", "zlib");
    let pack =
        f.0.join(".forge-storage/packs/22222222222222222222222222222222.pack");
    let mut bytes = fs::read(&pack).unwrap();
    bytes.extend(b"trailing");
    fs::write(pack, &bytes).unwrap();
    descriptor.segments[0].stored_bytes = bytes.len() as u64;
    f.descriptor(&path, &descriptor);
    assert!(read_file(&f.0, &path, 4096).is_err());
    let mut huge = MAGIC.to_vec();
    huge.resize(MAX_DESCRIPTOR as usize + 1, b' ');
    fs::write(&path, huge).unwrap();
    assert!(logical_size(&f.0, &path, 4096).is_err());
}

#[cfg(unix)]
#[test]
fn linked_pack_is_refused() {
    let f = Fixture::new();
    let (path, _) = f.packed(b"payload", "raw");
    let pack =
        f.0.join(".forge-storage/packs/22222222222222222222222222222222.pack");
    fs::rename(&pack, f.0.join("outside.pack")).unwrap();
    std::os::unix::fs::symlink(f.0.join("outside.pack"), &pack).unwrap();
    assert!(read_file(&f.0, &path, 4096).is_err());
}

fn export_descriptor() -> crate::manifest::Descriptor {
    crate::manifest::Descriptor {
        schema: "forge.delivery.package.v1".into(),
        id: "vault-export".into(),
        label: "Private test library".into(),
        version: "test-1".into(),
        kind: crate::manifest::Kind::Assets,
        platform: "any".into(),
        source_revision: "test".into(),
        rights: crate::manifest::Rights::PrivateLocalOnly,
        license_paths: vec!["license.txt".into()],
        files: vec![],
        product: None,
    }
}

#[test]
fn whole_library_export_is_autonomous_and_excludes_internal_storage() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let original = b"glTF autonomous exported model".repeat(200);
    source.packed(&original, "zlib");
    fs::write(source.0.join("license.txt"), b"private local provenance").unwrap();
    let descriptor = export_descriptor();
    let package = destination.0.join("export.forgepack");
    let (exported, revision) =
        crate::archive::pack(&source.0, descriptor.clone(), &package).unwrap();
    assert_eq!(exported.files.len(), 2);
    let model = exported
        .files
        .iter()
        .find(|entry| entry.path == "model.glb")
        .unwrap();
    assert_eq!(model.bytes, original.len() as u64);
    assert_eq!(model.sha256, crate::hash(&original));
    let restored = destination.0.join("restored");
    crate::archive::extract(&package, &restored, &revision).unwrap();
    assert_eq!(fs::read(restored.join("model.glb")).unwrap(), original);
    assert!(!restored.join(".forge-storage").exists());
    for (index, name) in [".forge-storage", ".forge-storage/root.v1.json"]
        .into_iter()
        .enumerate()
    {
        let output = destination.0.join(format!("bad-{index}.forgepack"));
        assert!(
            crate::archive::pack_selected(
                &source.0,
                descriptor.clone(),
                &output,
                &["license.txt".into(), name.into()],
            )
            .is_err()
        );
        assert!(!output.exists());
    }
}

#[test]
fn reserved_storage_is_excluded_without_a_marker_and_at_nested_depth() {
    let source = Fixture::new();
    let destination = Fixture::new();
    fs::remove_file(source.0.join(".forge-storage/root.v1.json")).unwrap();
    fs::write(
        source.0.join(".forge-storage/packs/private.pack"),
        b"private",
    )
    .unwrap();
    let nested = source.0.join("nested/.forge-storage/packs");
    fs::create_dir_all(&nested).unwrap();
    fs::write(nested.join("private.pack"), b"private nested").unwrap();
    fs::write(source.0.join("nested/visible.txt"), b"visible").unwrap();
    fs::create_dir(source.0.join("nested/file-branch")).unwrap();
    fs::write(
        source.0.join("nested/file-branch/.forge-storage"),
        b"reserved name",
    )
    .unwrap();
    fs::write(source.0.join("license.txt"), b"permitted content").unwrap();
    let descriptor = export_descriptor();
    let output = destination.0.join("clean.forgepack");
    let (packed, _) = crate::archive::pack(&source.0, descriptor.clone(), &output).unwrap();
    let paths: Vec<_> = packed
        .files
        .iter()
        .map(|entry| entry.path.as_str())
        .collect();
    assert_eq!(paths, ["license.txt", "nested/visible.txt"]);
    let selected_output = destination.0.join("selected-parent.forgepack");
    let (selected, _) = crate::archive::pack_selected(
        &source.0,
        descriptor.clone(),
        &selected_output,
        &["license.txt".into(), "nested".into()],
    )
    .unwrap();
    let selected_paths: Vec<_> = selected
        .files
        .iter()
        .map(|entry| entry.path.as_str())
        .collect();
    assert_eq!(selected_paths, paths);
    for (index, name) in [
        ".forge-storage/packs/private.pack",
        "nested/.forge-storage",
        "nested/file-branch/.forge-storage",
    ]
    .into_iter()
    .enumerate()
    {
        let output = destination.0.join(format!("reserved-{index}.forgepack"));
        assert!(
            crate::archive::pack_selected(
                &source.0,
                descriptor.clone(),
                &output,
                &["license.txt".into(), name.into()],
            )
            .is_err()
        );
        assert!(!output.exists());
    }
}

#[test]
fn storage_root_and_its_child_cannot_become_package_sources() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let storage = source.0.join(".forge-storage");
    let packs = storage.join("packs");
    // Both sources would otherwise contain the descriptor's required licence.
    fs::write(storage.join("license.txt"), b"private").unwrap();
    fs::write(packs.join("license.txt"), b"private").unwrap();
    for (index, root) in [&storage, &packs].into_iter().enumerate() {
        let output = destination
            .0
            .join(format!("internal-root-{index}.forgepack"));
        assert!(crate::archive::pack(root, export_descriptor(), &output).is_err());
        assert!(!output.exists());
        assert!(
            crate::archive::pack_selected(
                root,
                export_descriptor(),
                &output,
                &["license.txt".into()],
            )
            .is_err()
        );
        assert!(!output.exists());
    }
}

#[cfg(windows)]
#[test]
fn windows_storage_case_is_reserved_for_inventory_and_selections() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let original = b"logical model".repeat(50);
    source.packed(&original, "zlib");
    fs::write(source.0.join("license.txt"), b"private provenance").unwrap();
    let original_dir = source.0.join(".forge-storage");
    let temporary = source.0.join("storage-rename");
    fs::rename(&original_dir, &temporary).unwrap();
    fs::rename(&temporary, source.0.join(".FoRgE-StOrAgE")).unwrap();
    let (packed, revision) = crate::archive::pack(
        &source.0,
        export_descriptor(),
        &destination.0.join("case.forgepack"),
    )
    .unwrap();
    assert_eq!(packed.files.len(), 2);
    let restored = destination.0.join("restored-case");
    crate::archive::extract(&destination.0.join("case.forgepack"), &restored, &revision).unwrap();
    assert_eq!(fs::read(restored.join("model.glb")).unwrap(), original);
    assert!(!restored.join(".forge-storage").exists());
    for (index, name) in [".FORGE-STORAGE", ".fOrGe-StOrAgE/root.v1.json"]
        .into_iter()
        .enumerate()
    {
        let output = destination.0.join(format!("case-bad-{index}.forgepack"));
        assert!(
            crate::archive::pack_selected(
                &source.0,
                export_descriptor(),
                &output,
                &["license.txt".into(), name.into()],
            )
            .is_err()
        );
        assert!(!output.exists());
    }
    let internal_root = source.0.join(".FORGE-STORAGE/packs");
    fs::write(internal_root.join("license.txt"), b"private").unwrap();
    let output = destination.0.join("case-internal-root.forgepack");
    assert!(crate::archive::pack(&internal_root, export_descriptor(), &output).is_err());
    assert!(!output.exists());
}

#[cfg(windows)]
#[test]
#[ignore = "requires an actual Windows volume with 8.3 names enabled"]
fn windows_short_alias_cannot_select_internal_storage() {
    use std::os::windows::process::CommandExt;

    let source = Fixture::new();
    let destination = Fixture::new();
    fs::write(source.0.join("license.txt"), b"private provenance").unwrap();
    let storage = source.0.join(".forge-storage");
    let output = std::process::Command::new("cmd.exe")
        .args(["/D", "/V:OFF", "/C"])
        .raw_arg(r#"for %I in ("%FORGE_TEST_STORAGE%") do @echo %~sI"#)
        .env("FORGE_TEST_STORAGE", &storage)
        .creation_flags(0x0800_0000) // CREATE_NO_WINDOW
        .output()
        .unwrap();
    assert!(output.status.success());
    let short = String::from_utf8_lossy(&output.stdout);
    let name = short.trim().rsplit('\\').next().unwrap();
    assert!(
        !name.eq_ignore_ascii_case(".forge-storage")
            && fs::canonicalize(source.0.join(name)).unwrap()
                == fs::canonicalize(&storage).unwrap(),
        "No usable 8.3 storage alias on this volume"
    );
    let package = destination.0.join("short-name.forgepack");
    assert!(
        crate::archive::pack_selected(
            &source.0,
            export_descriptor(),
            &package,
            &["license.txt".into(), format!("{name}/root.v1.json")],
        )
        .is_err()
    );
    assert!(!package.exists());
    // The alias must also be refused when it is the selected source root.
    fs::write(storage.join("license.txt"), b"private").unwrap();
    let root_package = destination.0.join("short-name-root.forgepack");
    assert!(
        crate::archive::pack(&source.0.join(name), export_descriptor(), &root_package).is_err()
    );
    assert!(!root_package.exists());
}

// This portable boundary also reserves a distinct uppercase directory on a
// case-sensitive host, so a package has the same meaning on every filesystem.
#[test]
fn mixed_case_storage_names_are_reserved_on_every_platform() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let other = source.0.join(".FORGE-STORAGE");
    if !other.exists() {
        fs::create_dir(&other).unwrap();
    }
    fs::write(other.join("private.txt"), b"reserved data").unwrap();
    fs::write(other.join("license.txt"), b"private license").unwrap();
    fs::write(source.0.join("license.txt"), b"permitted content").unwrap();
    let (packed, _) = crate::archive::pack(
        &source.0,
        export_descriptor(),
        &destination.0.join("case-portable.forgepack"),
    )
    .unwrap();
    assert_eq!(packed.files.len(), 1);
    assert_eq!(packed.files[0].path, "license.txt");
    let selected = destination.0.join("case-selected.forgepack");
    assert!(
        crate::archive::pack_selected(
            &source.0,
            export_descriptor(),
            &selected,
            &["license.txt".into(), ".FORGE-STORAGE/private.txt".into()],
        )
        .is_err()
    );
    assert!(!selected.exists());
    let inner = destination.0.join("case-root.forgepack");
    assert!(crate::archive::pack(&other, export_descriptor(), &inner).is_err());
    assert!(!inner.exists());
}

// Build a path without changing the process-wide current directory, so this
// fixture can run in parallel with the other delivery tests.
fn relative_to_current(path: &Path) -> Option<PathBuf> {
    let current = std::env::current_dir().ok()?;
    let target = std::path::absolute(path).ok()?;
    let from: Vec<_> = current.components().collect();
    let to: Vec<_> = target.components().collect();
    let common = from.iter().zip(&to).take_while(|(a, b)| a == b).count();
    if common == 0 {
        return None; // Different Windows volumes cannot be reached relatively.
    }
    let mut result = PathBuf::new();
    for component in &from[common..] {
        if !matches!(component, std::path::Component::Normal(_)) {
            return None;
        }
        result.push("..");
    }
    for component in &to[common..] {
        result.push(component.as_os_str());
    }
    if result.as_os_str().is_empty() {
        result.push(".");
    }
    Some(result)
}

#[test]
fn relative_source_with_parent_segments_remains_supported() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let target = source.0.join("ordinary-source");
    fs::create_dir(&target).unwrap();
    fs::create_dir(target.join("child")).unwrap();
    fs::write(target.join("license.txt"), b"allowed").unwrap();
    fs::write(target.join("visible.txt"), b"ordinary source").unwrap();
    let absolute_output = destination.0.join("absolute.forgepack");
    assert!(crate::archive::pack(&target, export_descriptor(), &absolute_output).is_ok());
    let empty_output = destination.0.join("empty.forgepack");
    assert!(crate::archive::pack(Path::new(""), export_descriptor(), &empty_output).is_err());
    assert!(!empty_output.exists());
    let Some(relative) = relative_to_current(&target) else {
        // A Windows relative path cannot cross drives. Retry in a child process
        // rooted on TEMP rather than changing this test process's current dir.
        assert!(std::env::var_os("FORGE_TEST_RELATIVE_CHILD").is_none());
        let mut child = std::process::Command::new(std::env::current_exe().unwrap());
        #[cfg(windows)]
        {
            use std::os::windows::process::CommandExt;
            child.creation_flags(0x0800_0000); // CREATE_NO_WINDOW
        }
        let result = child
            .current_dir(&source.0)
            .env("FORGE_TEST_RELATIVE_CHILD", "1")
            .args([
                "vault_storage::tests::relative_source_with_parent_segments_remains_supported",
                "--exact",
                "--nocapture",
            ])
            .output()
            .unwrap();
        assert!(
            result.status.success(),
            "relative-path child failed: {} {}",
            String::from_utf8_lossy(&result.stdout),
            String::from_utf8_lossy(&result.stderr)
        );
        return;
    };
    for (index, input) in [
        relative.clone(),
        relative.join("."),
        relative.join("child").join(".."),
    ]
    .iter()
    .enumerate()
    {
        let output = destination.0.join(format!("relative-{index}.forgepack"));
        let (packed, _) = crate::archive::pack(input, export_descriptor(), &output).unwrap();
        let files: Vec<_> = packed
            .files
            .iter()
            .map(|entry| entry.path.as_str())
            .collect();
        assert_eq!(files, ["license.txt", "visible.txt"]);
    }
    #[cfg(windows)]
    {
        use std::path::{Component, Prefix};
        let current = std::env::current_dir().unwrap();
        if let Some(Component::Prefix(prefix)) = current.components().next()
            && let Prefix::Disk(drive) | Prefix::VerbatimDisk(drive) = prefix.kind()
        {
            let mut drive_relative = std::ffi::OsString::from(format!("{}:", char::from(drive)));
            drive_relative.push(relative.as_os_str());
            let output = destination.0.join("drive-relative.forgepack");
            let (packed, _) =
                crate::archive::pack(Path::new(&drive_relative), export_descriptor(), &output)
                    .unwrap();
            assert_eq!(packed.files.len(), 2);
            let mut drive_rooted = PathBuf::from(r"\");
            for component in target.components() {
                if let Component::Normal(name) = component {
                    drive_rooted.push(name);
                }
            }
            let output = destination.0.join("drive-rooted.forgepack");
            let (packed, _) =
                crate::archive::pack(&drive_rooted, export_descriptor(), &output).unwrap();
            assert_eq!(packed.files.len(), 2);
        }
    }
}

#[cfg(any(unix, windows))]
struct SourceLink(PathBuf);

#[cfg(any(unix, windows))]
impl Drop for SourceLink {
    fn drop(&mut self) {
        #[cfg(windows)]
        fs::remove_dir(&self.0).unwrap();
        #[cfg(unix)]
        fs::remove_file(&self.0).unwrap();
    }
}

#[cfg(unix)]
fn source_link(target: &Path, link: &Path) -> SourceLink {
    std::os::unix::fs::symlink(target, link).unwrap();
    SourceLink(link.to_path_buf())
}

#[cfg(windows)]
fn source_link(target: &Path, link: &Path) -> SourceLink {
    use std::os::windows::process::CommandExt;
    let result = std::process::Command::new("cmd.exe")
        .args(["/D", "/V:OFF", "/C"])
        .raw_arg(r#"mklink /J "%FORGE_TEST_LINK%" "%FORGE_TEST_TARGET%""#)
        .env("FORGE_TEST_LINK", link)
        .env("FORGE_TEST_TARGET", target)
        .creation_flags(0x0800_0000) // CREATE_NO_WINDOW
        .output()
        .unwrap();
    assert!(
        result.status.success(),
        "The real-junction fixture requires a local volume supporting junctions: {}",
        String::from_utf8_lossy(&result.stderr)
    );
    SourceLink(link.to_path_buf())
}

#[cfg(any(unix, windows))]
#[test]
fn source_link_and_ancestor_before_parent_are_refused_without_touching_target() {
    let source = Fixture::new();
    let destination = Fixture::new();
    let target = source.0.join("target");
    let nested = target.join("nested");
    fs::create_dir(&target).unwrap();
    fs::create_dir(&nested).unwrap();
    fs::write(target.join("license.txt"), b"private").unwrap();
    fs::write(nested.join("license.txt"), b"private nested").unwrap();
    fs::write(target.join("sentinel.txt"), b"unchanged").unwrap();
    for (index, plain) in [&target, &nested].into_iter().enumerate() {
        let output = destination.0.join(format!("plain-{index}.forgepack"));
        crate::archive::pack(plain, export_descriptor(), &output).unwrap();
    }
    let link = source.0.join("source-link");
    let _owned_link = source_link(&target, &link);
    // Preserve the literal `..` even for Windows verbatim paths, whose
    // PathBuf::push may otherwise normalize the input before the test.
    let mut before_parent = link.as_os_str().to_os_string();
    before_parent.push(std::path::MAIN_SEPARATOR.to_string());
    before_parent.push("..");
    before_parent.push(std::path::MAIN_SEPARATOR.to_string());
    before_parent.push("target");
    let before_parent = PathBuf::from(before_parent);
    assert!(
        before_parent
            .components()
            .any(|part| part == std::path::Component::ParentDir)
    );
    let mut candidates = vec![link.clone(), link.join("nested"), before_parent];
    if let Some(relative) = relative_to_current(&link) {
        candidates.push(relative.join("..").join("target"));
    }
    for (index, input) in candidates.iter().enumerate() {
        let output = destination.0.join(format!("linked-{index}.forgepack"));
        assert!(crate::archive::pack(input, export_descriptor(), &output).is_err());
        assert!(!output.exists());
    }
    let selected_output = destination.0.join("linked-selected.forgepack");
    assert!(
        crate::archive::pack_selected(
            &link,
            export_descriptor(),
            &selected_output,
            &["license.txt".into()],
        )
        .is_err()
    );
    assert!(!selected_output.exists());
    assert_eq!(fs::read(target.join("sentinel.txt")).unwrap(), b"unchanged");
}
