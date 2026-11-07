#[path = "../../../apps/forge_runtime/src/module_game/input.rs"]
mod input;

use forge_core::AreaId;
use input::ModuleGameOptions;
use std::ffi::OsString;

fn arguments() -> Vec<OsString> {
    let root = std::env::temp_dir().join("Forge Élan");
    vec![
        "--module".into(),
        root.join("aventure.orpmod").into_os_string(),
        "--zone".into(),
        AreaId::deterministic("module-game-input")
            .to_string()
            .into(),
        "--cache".into(),
        root.join("Cache").into_os_string(),
        "--save-root".into(),
        root.join("Sauvegardes").into_os_string(),
    ]
}

#[test]
fn standalone_start_intent_is_explicit_and_strictly_pinned() {
    use forge_gameplay::native_storage::StartIntent;
    assert_eq!(
        ModuleGameOptions::parse(arguments()).unwrap().start_intent,
        StartIntent::Legacy
    );
    let mut fresh = arguments();
    fresh.extend(["--start-intent".into(), "fresh".into()]);
    assert_eq!(
        ModuleGameOptions::parse(fresh).unwrap().start_intent,
        StartIntent::Fresh
    );
    let pin = forge_core::bytes_sha256(b"saved game");
    let mut resume = arguments();
    resume.extend(["--start-intent".into(), format!("resume:{pin}").into()]);
    assert_eq!(
        ModuleGameOptions::parse(resume).unwrap().start_intent,
        StartIntent::Resume(pin)
    );
    for invalid in ["", "legacy", "resume:", "resume:invalid", "fresh:ignored"] {
        let mut args = arguments();
        args.extend(["--start-intent".into(), invalid.into()]);
        assert!(ModuleGameOptions::parse(args).is_err());
    }
    let mut duplicate = arguments();
    duplicate.extend([
        "--start-intent".into(),
        "fresh".into(),
        "--start-intent".into(),
        "fresh".into(),
    ]);
    assert!(ModuleGameOptions::parse(duplicate).is_err());
}

#[test]
fn authored_default_quality_is_optional_and_accepts_only_the_four_tiers() {
    use forge_project::VisualQualityTier;
    assert_eq!(
        ModuleGameOptions::parse(arguments())
            .unwrap()
            .default_quality,
        None
    );
    for (value, expected) in [
        ("low", VisualQualityTier::Low),
        ("medium", VisualQualityTier::Medium),
        ("high", VisualQualityTier::High),
        ("ultra", VisualQualityTier::Ultra),
    ] {
        let mut args = arguments();
        args.extend(["--default-quality".into(), value.into()]);
        assert_eq!(
            ModuleGameOptions::parse(args).unwrap().default_quality,
            Some(expected)
        );
    }
    for invalid in ["", "High", "automatic", "../../low"] {
        let mut args = arguments();
        args.extend(["--default-quality".into(), invalid.into()]);
        assert!(ModuleGameOptions::parse(args).is_err());
    }
}

#[test]
fn authored_display_default_is_optional_and_strict() {
    use forge_project::workspace::launcher::DisplayMode;
    assert_eq!(input::parse_default_display_mode(None).unwrap(), None);
    for (value, expected) in [
        ("windowed", DisplayMode::Windowed),
        ("borderless", DisplayMode::Borderless),
        ("fullscreen", DisplayMode::Fullscreen),
    ] {
        assert_eq!(
            input::parse_default_display_mode(Some(std::ffi::OsStr::new(value))).unwrap(),
            Some(expected)
        );
    }
    for value in ["", "Windowed", "automatic", "../../fullscreen"] {
        assert!(input::parse_default_display_mode(Some(std::ffi::OsStr::new(value))).is_err());
    }
    #[cfg(windows)]
    {
        use std::os::windows::ffi::OsStringExt;
        let non_unicode = OsString::from_wide(&[0xd800]);
        assert!(input::parse_default_display_mode(Some(&non_unicode)).is_err());
    }
}

#[test]
fn unicode_roots_and_explicit_zone_are_preserved_without_resolving_a_workspace() {
    let parsed = ModuleGameOptions::parse(arguments()).unwrap();
    assert_eq!(parsed.area_id, AreaId::deterministic("module-game-input"));
    assert_eq!(
        parsed.package,
        std::env::temp_dir().join("Forge Élan/aventure.orpmod")
    );
    assert!(parsed.cache_root.is_absolute() && parsed.save_root.is_absolute());
    assert!(parsed.capture.is_none() && parsed.exit_after_frames.is_none());
}

#[test]
fn bounded_native_capture_is_explicit_and_keeps_its_exact_output_path() {
    let mut args = arguments();
    let capture = std::env::temp_dir().join("Forge Élan/capture.png");
    args.extend([
        "--capture".into(),
        capture.clone().into_os_string(),
        "--frames".into(),
        "180".into(),
    ]);
    let parsed = ModuleGameOptions::parse(args).unwrap();
    assert_eq!(parsed.capture, Some(capture));
    assert_eq!(parsed.exit_after_frames, Some(180));
}

#[test]
fn missing_duplicate_unknown_and_relative_output_arguments_are_refused() {
    for missing in [2, 4, 6] {
        let mut args = arguments();
        args.drain(missing..missing + 2);
        assert!(ModuleGameOptions::parse(args).is_err());
    }
    for suffix in [
        vec!["--zone", "invalid"],
        vec!["--workspace", "elsewhere"],
        vec!["--frames", "0"],
        vec!["--frames", "-1"],
        vec!["--frames", "4294967296"],
    ] {
        let mut args = arguments();
        args.extend(suffix.into_iter().map(OsString::from));
        assert!(ModuleGameOptions::parse(args).is_err());
    }
    let mut args = arguments();
    args[5] = "relative-cache".into();
    assert!(ModuleGameOptions::parse(args).is_err());
    let mut args = arguments();
    args[7] = "relative-saves".into();
    assert!(ModuleGameOptions::parse(args).is_err());
}

#[test]
fn studio_expected_head_and_archive_pins_are_a_complete_strict_pair() {
    let head = forge_core::bytes_sha256(b"reviewed head");
    let archive = forge_core::bytes_sha256(b"reviewed archive");
    let mut args = arguments();
    args.extend([
        "--expected-head".into(),
        head.to_string().into(),
        "--expected-archive-sha256".into(),
        archive.to_string().into(),
    ]);
    let parsed = ModuleGameOptions::parse(args).unwrap();
    assert_eq!(
        parsed.expected_zone,
        Some(input::ExpectedZonePins {
            head_hash: head,
            archive_sha256: archive
        })
    );
    for flag in ["--expected-head", "--expected-archive-sha256"] {
        let mut args = arguments();
        args.extend([flag.into(), head.to_string().into()]);
        assert!(ModuleGameOptions::parse(args).is_err());
    }
    let mut args = arguments();
    args.extend([
        "--expected-head".into(),
        "invalid".into(),
        "--expected-archive-sha256".into(),
        archive.to_string().into(),
    ]);
    assert!(ModuleGameOptions::parse(args).is_err());
}

#[test]
fn child_accepts_only_the_reviewed_archive_and_head_while_unpinned_cli_remains_supported() {
    let archive = b"exact canonical zone archive";
    let head = forge_core::bytes_sha256(b"canonical head");
    let expected = input::ExpectedZonePins {
        head_hash: head,
        archive_sha256: forge_core::bytes_sha256(archive),
    };
    input::verify_expected_zone(None, head, archive).unwrap();
    input::verify_expected_zone(Some(expected), head, archive).unwrap();
    assert!(
        input::verify_expected_zone(
            Some(expected),
            forge_core::bytes_sha256(b"other head"),
            archive
        )
        .is_err()
    );
    assert!(input::verify_expected_zone(Some(expected), head, b"other valid archive").is_err());
}

#[test]
fn an_installed_transport_pin_requires_the_reviewed_zone_pair_and_is_strictly_parsed() {
    let hash = forge_core::bytes_sha256(b"installed descriptor");
    let mut args = arguments();
    args.extend([
        "--expected-head".into(),
        hash.to_string().into(),
        "--expected-archive-sha256".into(),
        hash.to_string().into(),
        "--expected-package-manifest-sha256".into(),
        hash.to_string().into(),
    ]);
    assert_eq!(
        ModuleGameOptions::parse(args)
            .unwrap()
            .expected_package_manifest,
        Some(hash)
    );
    let mut args = arguments();
    args.extend([
        "--expected-package-manifest-sha256".into(),
        hash.to_string().into(),
    ]);
    assert!(ModuleGameOptions::parse(args).is_err());
    let mut args = arguments();
    args.extend([
        "--expected-head".into(),
        hash.to_string().into(),
        "--expected-archive-sha256".into(),
        hash.to_string().into(),
        "--expected-package-manifest-sha256".into(),
        "invalid".into(),
    ]);
    assert!(ModuleGameOptions::parse(args).is_err());
}
