fn main() {
    println!("cargo:rerun-if-env-changed=FORGE_SOURCE_COMMIT");
    if let Some(commit) = std::env::var_os("FORGE_SOURCE_COMMIT") {
        let commit = commit
            .into_string()
            .unwrap_or_else(|_| panic!("FORGE_SOURCE_COMMIT must be valid Unicode"));
        assert!(
            commit.len() == 40 && commit.bytes().all(|byte| byte.is_ascii_hexdigit()),
            "FORGE_SOURCE_COMMIT must be a 40-character hexadecimal Git object id"
        );
        println!("cargo:rustc-env=FORGE_SOURCE_COMMIT={commit}");
    }

    let profile = std::env::var("PROFILE").unwrap_or_else(|error| {
        println!("cargo:warning=cannot read Cargo PROFILE for forge_runtime: {error}");
        "unknown".to_owned()
    });
    println!("cargo:rustc-env=FORGE_BUILD_PROFILE={profile}");
}
