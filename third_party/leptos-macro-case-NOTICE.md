# Leptos macro 0.8.17 — comparaison de casse directe

Le paquet [leptos_macro 0.8.17](https://static.crates.io/crates/leptos_macro/leptos_macro-0.8.17.crate)
est épinglé au SHA-256
`96de6e8da9d4f1a7b74b447b317d590ebabb38709f588f7ee20564b773ccbcce`.
Ses métadonnées Cargo désignent
[Leptos 30fa4e0](https://github.com/leptos-rs/leptos/tree/30fa4e039455bea69c9467e8ec45cad7fce0722f/leptos_macro).
Le fichier [LICENSE](leptos_macro-0.8.17/LICENSE) vient de cette révision,
blob Git `77d5625cb3656d2a3b63aa2f0c9b27190330e4eb`.

Les deux appels hôte à `convert_case_extras::is_case` dans `component.rs`
et `view/mod.rs` comparent directement le nom à sa conversion `Snake`
via `convert_case`, déjà présent et inchangé. Cette comparaison conserve
le prédicat et les noms produits. Le manifeste Cargo effectif retire
`convert_case_extras`. Le manifeste source `Cargo.toml.orig` est conservé
et le verrou Forge gouverne la résolution.

Le WASM 049 conserve son inventaire historique. Retirer ce delta après
qualification d'une version publiée qui permet de lever cette réserve.
