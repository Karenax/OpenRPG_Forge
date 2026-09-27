//! Keyboard mapping independent of the DOM; never executes a document mutation.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum EditorShortcut {
    Apply,
    Save,
    Undo,
    Redo,
}

#[derive(Clone, Copy, Default)]
pub(super) struct Modifiers {
    pub control: bool,
    pub shift: bool,
    pub other: bool,
}

/// Text keeps its own undo/apply keys; saving the project is always explicit.
pub(super) fn allowed_in_text(shortcut: EditorShortcut) -> bool {
    shortcut == EditorShortcut::Save
}

pub(super) fn resolve(key: &str, modifiers: Modifiers) -> Option<EditorShortcut> {
    if !modifiers.control || modifiers.other {
        return None;
    }
    match (key.to_ascii_lowercase().as_str(), modifiers.shift) {
        ("enter", false) => Some(EditorShortcut::Apply),
        ("s", false) => Some(EditorShortcut::Save),
        ("z", false) => Some(EditorShortcut::Undo),
        ("y", false) | ("z", true) => Some(EditorShortcut::Redo),
        _ => None,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn text_fields_keep_editing_history_but_allow_project_save() {
        assert!(allowed_in_text(EditorShortcut::Save));
        for shortcut in [
            EditorShortcut::Apply,
            EditorShortcut::Undo,
            EditorShortcut::Redo,
        ] {
            assert!(!allowed_in_text(shortcut));
        }
    }
    #[test]
    fn editor_shortcuts_are_explicit_and_do_not_capture_other_chords() {
        let ctrl = Modifiers {
            control: true,
            ..Modifiers::default()
        };
        assert_eq!(resolve("s", ctrl), Some(EditorShortcut::Save));
        assert_eq!(resolve("Enter", ctrl), Some(EditorShortcut::Apply));
        assert_eq!(
            resolve(
                "Enter",
                Modifiers {
                    shift: true,
                    ..ctrl
                }
            ),
            None
        );
        assert_eq!(resolve("Z", ctrl), Some(EditorShortcut::Undo));
        assert_eq!(resolve("y", ctrl), Some(EditorShortcut::Redo));
        assert_eq!(
            resolve(
                "z",
                Modifiers {
                    shift: true,
                    ..ctrl
                }
            ),
            Some(EditorShortcut::Redo)
        );
        for key in ["s", "z", "y", "Enter", "Escape", "a"] {
            assert_eq!(resolve(key, Modifiers::default()), None);
            assert_eq!(
                resolve(
                    key,
                    Modifiers {
                        other: true,
                        ..ctrl
                    }
                ),
                None
            );
        }
        assert_eq!(
            resolve(
                "s",
                Modifiers {
                    shift: true,
                    ..ctrl
                }
            ),
            None
        );
        assert_eq!(
            resolve(
                "y",
                Modifiers {
                    shift: true,
                    ..ctrl
                }
            ),
            None
        );
    }
}
