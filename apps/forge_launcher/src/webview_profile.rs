//! Validate a per-game `WebView` profile before constructing its window.
use std::{ffi::OsString, io, path::Path};

fn reject_webview_environment(keys: impl IntoIterator<Item = OsString>) -> Result<(), String> {
    for key in keys {
        let key = key.to_str().ok_or("Variable d'environnement invalide.")?;
        if key.to_ascii_uppercase().starts_with("WEBVIEW2_") {
            return Err("Une variable WebView héritée empêche l'ouverture isolée du jeu.".into());
        }
    }
    Ok(())
}

#[cfg(all(windows, feature = "standalone"))]
pub fn explicit_app_id() -> Result<Option<String>, String> {
    use winsafe::co;
    // Never set or change the process identity. Windows does not expose the
    // automatically assigned implicit identity through this API.
    let id = match winsafe::GetCurrentProcessExplicitAppUserModelID() {
        Ok(id) => id,
        Err(error) if error == co::HRESULT::E_FAIL => return Ok(None),
        Err(error) => {
            return Err(format!(
                "Identité Windows du lanceur inaccessible : {error}"
            ));
        }
    };
    if id.is_empty() || id == "*" || id.encode_utf16().count() > 128 || id.contains('\0') {
        return Err("Identité Windows du lanceur invalide.".into());
    }
    Ok(Some(id))
}

#[cfg(all(windows, feature = "standalone"))]
pub fn show_native_error(reason: &str) {
    use winsafe::{self as w, co};
    eprintln!("{reason}");
    if let Err(error) = w::HWND::GetDesktopWindow().MessageBox(
        reason,
        "OpenRPG Forge",
        co::MB::OK | co::MB::ICONERROR,
    ) {
        eprintln!("Affichage du message impossible : {error}");
    }
}

// Names and types only: do not read or allocate registry value data. A name
// whose Windows case equivalence is uncertain is treated as potentially ours.
#[cfg(any(all(windows, feature = "standalone"), test))]
fn reject_policy_names(
    app_id: Option<&str>,
    filename: &str,
    names: impl IntoIterator<Item = Result<String, String>>,
) -> Result<(), String> {
    const LIMIT: usize = 256;
    for (index, name) in names.into_iter().enumerate() {
        if index >= LIMIT {
            return Err("Trop de stratégies WebView pour vérifier le profil isolé.".into());
        }
        let name = name?;
        // Microsoft explicitly excludes this wildcard for UserDataFolder.
        if name == "*" {
            continue;
        }
        let Some(app_id) = app_id else {
            return Err("Une stratégie WebView existe et l’identité Windows implicite ne peut pas être vérifiée. L’ouverture isolée est suspendue.".into());
        };
        if !name.is_ascii() || !app_id.is_ascii() || !filename.is_ascii() {
            return Err("Une stratégie WebView ne peut pas être comparée sans ambiguïté. L’ouverture isolée est suspendue.".into());
        }
        if [
            app_id,
            filename,
            "forge-launcher-prototype.exe",
            "forge-game-launcher.exe",
        ]
        .iter()
        .any(|candidate| name.eq_ignore_ascii_case(candidate))
        {
            return Err(
                "Une stratégie WebView applicable empêche l’ouverture isolée du jeu.".into(),
            );
        }
    }
    Ok(())
}

#[cfg(all(windows, feature = "standalone"))]
fn reject_profile_policy(app_id: Option<&str>) -> Result<(), String> {
    use winsafe::{self as w, co};
    const KEY: &str = r"Software\Policies\Microsoft\Edge\WebView2\UserDataFolder";
    let executable = std::env::current_exe().map_err(|error| error.to_string())?;
    let filename = executable
        .file_name()
        .and_then(|name| name.to_str())
        .ok_or("Nom du lanceur introuvable ou non Unicode.")?;
    for hive in [&w::HKEY::LOCAL_MACHINE, &w::HKEY::CURRENT_USER] {
        for view in [co::KEY::WOW64_64KEY, co::KEY::WOW64_32KEY] {
            match hive.RegOpenKeyEx(Some(KEY), co::REG_OPTION::default(), co::KEY::READ | view) {
                Ok(key) => {
                    let names = policy_names(&key)?;
                    if names != policy_names(&key)? {
                        return Err("La stratégie WebView a changé pendant sa vérification.".into());
                    }
                    reject_policy_names(app_id, filename, names.into_iter().map(Ok))?;
                }
                Err(error)
                    if error == co::ERROR::FILE_NOT_FOUND || error == co::ERROR::PATH_NOT_FOUND => {
                }
                Err(error) => {
                    return Err(format!(
                        "Lecture de la stratégie WebView impossible : {error}"
                    ));
                }
            }
        }
    }
    Ok(())
}

#[cfg(all(windows, feature = "standalone"))]
fn policy_names(key: &winsafe::HKEY) -> Result<Vec<String>, String> {
    const LIMIT: usize = 256;
    let values = key
        .RegEnumValue()
        .map_err(|error| format!("Lecture de la stratégie WebView impossible : {error}"))?;
    let mut names = Vec::new();
    for entry in values.take(LIMIT + 1) {
        let (name, _) = entry
            .map_err(|error| format!("Lecture de la stratégie WebView impossible : {error}"))?;
        names.push(name);
    }
    if names.len() > LIMIT {
        return Err("Trop de stratégies WebView pour vérifier le profil isolé.".into());
    }
    names.sort_unstable();
    Ok(names)
}

#[cfg(not(all(windows, feature = "standalone")))]
#[allow(
    clippy::unnecessary_wraps,
    reason = "The platform stub keeps the same fallible interface as the Windows policy check"
)]
fn reject_profile_policy(_app_id: Option<&str>) -> Result<(), String> {
    Ok(())
}

/// Do not attempt window creation when the profile was not admitted.
pub fn create_after_validation<T, E>(
    validate: impl FnOnce() -> Result<(), E>,
    create: impl FnOnce() -> Result<T, E>,
) -> Result<T, E> {
    validate()?;
    create()
}

/// Report setup failure from inside Tauri's setup callback, before it can
/// convert that failure into a panic. The caller must terminate on `true`.
#[cfg(any(all(windows, feature = "standalone"), test))]
pub fn report_setup_failure<E: std::fmt::Display>(
    result: &Result<(), E>,
    report: impl FnOnce(&str),
) -> bool {
    if let Err(error) = result {
        report(&format!("Impossible d’ouvrir le lanceur : {error}"));
        true
    } else {
        false
    }
}

/// Does not create, modify, or follow a profile or its ancestors.
pub fn validate(
    path: &Path,
    historical_default: &Path,
    must_be_fresh: bool,
    app_id: Option<&str>,
) -> Result<(), String> {
    reject_webview_environment(std::env::vars_os().map(|(key, _)| key))?;
    reject_profile_policy(app_id)?;
    forge_delivery::manifest::safe_chain(path)?;
    forge_delivery::manifest::safe_chain(historical_default)?;
    if !forge_delivery::install::physically_disjoint(path, historical_default)? {
        return Err("Le profil du jeu recouvre un profil existant du lanceur.".into());
    }
    match std::fs::symlink_metadata(path) {
        Ok(metadata) if metadata.is_dir() && !must_be_fresh => {
            forge_delivery::manifest::no_links(path)
        }
        Ok(_) => Err("Le dossier de profil est déjà occupé ou invalide.".into()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(format!("Profil WebView inaccessible : {error}")),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejected_profile_is_reported_without_creating_a_window() {
        let created = std::cell::Cell::new(false);
        let result = create_after_validation(
            || Err::<(), _>("stratégie WebView refusée"),
            || {
                created.set(true);
                Ok(())
            },
        );
        let report = std::cell::RefCell::new(None);
        assert!(report_setup_failure(&result, |message| *report
            .borrow_mut() =
            Some(message.to_owned())));
        assert!(!created.get());
        assert!(
            report
                .into_inner()
                .unwrap()
                .contains("stratégie WebView refusée")
        );
    }

    #[test]
    fn window_creation_failure_is_reported_without_retry_or_fallback() {
        let attempts = std::cell::Cell::new(0);
        let result = create_after_validation(
            || Ok(()),
            || {
                attempts.set(attempts.get() + 1);
                Err::<(), _>("création impossible")
            },
        );
        let report = std::cell::RefCell::new(None);
        assert!(report_setup_failure(&result, |message| *report
            .borrow_mut() =
            Some(message.to_owned())));
        assert_eq!(attempts.get(), 1);
        assert!(report.into_inner().unwrap().contains("création impossible"));
        let success: Result<(), &str> = Ok(());
        assert!(!report_setup_failure(&success, |_| panic!(
            "unexpected error report"
        )));
    }

    #[test]
    fn inherited_webview_overrides_are_refused_case_insensitively() {
        assert!(reject_webview_environment([OsString::from("PATH")]).is_ok());
        assert!(reject_webview_environment([OsString::from("WebView2_User_Data_Folder")]).is_err());
    }
    #[test]
    fn unknown_identity_allows_only_wildcard_and_fails_closed() {
        let check =
            |names: Vec<Result<String, String>>| reject_policy_names(None, "Jouer.exe", names);
        assert!(check(vec![]).is_ok());
        assert!(check(vec![Ok("*".into())]).is_ok());
        assert!(check(vec![Ok("ms-teams.exe".into())]).is_err());
        assert!(check(vec![Ok("*".into()), Ok("unexpected".into())]).is_err());
        assert!(check(vec![Err("inaccessible".into())]).is_err());
        assert!(check((0..257).map(|_| Ok("*".into())).collect()).is_err());
    }
    #[test]
    fn explicit_identity_refuses_applicable_and_ambiguous_names() {
        let check = |names: Vec<Result<String, String>>| {
            reject_policy_names(Some("Forge.Game"), "Jouer.exe", names)
        };
        assert!(check(vec![Ok("ms-teams.exe".into()), Ok("*".into())]).is_ok());
        assert!(check(vec![Ok("FORGE.GAME".into())]).is_err());
        assert!(check(vec![Ok("JOUER.EXE".into())]).is_err());
        assert!(check(vec![Ok("forge-game-launcher.exe".into())]).is_err());
        assert!(check(vec![Ok("équipe".into())]).is_err());
        assert!(check(vec![Err("inaccessible".into())]).is_err());
    }
}
