//! Bounded diagnostic labels, never key text or an alternate input controller.
pub fn physical_key_label(code: &str) -> &'static str {
    match code {
        "KeyW" | "KeyA" | "KeyS" | "KeyD" => "déplacement reconnu",
        "KeyV" => "bascule caméra reconnue",
        "Space" => "saut reconnu",
        "ShiftLeft" | "ShiftRight" => "course reconnue",
        "Escape" => "libération reconnue",
        "" | "Unidentified" => "code physique absent",
        _ => "code hors commandes du joueur",
    }
}

#[cfg(test)]
mod tests {
    use super::physical_key_label;

    #[test]
    fn distinguishes_physical_controls_from_text_without_echoing_input() {
        assert_eq!(physical_key_label("KeyW"), "déplacement reconnu");
        assert_eq!(physical_key_label("KeyV"), "bascule caméra reconnue");
        assert_eq!(physical_key_label(""), "code physique absent");
        assert_eq!(physical_key_label("Unidentified"), "code physique absent");
        assert_eq!(physical_key_label("w"), "code hors commandes du joueur");
        assert_eq!(
            physical_key_label("private text"),
            "code hors commandes du joueur"
        );
    }
}
