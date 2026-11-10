# Tachys 0.2.18 — dépendances de liste et d'effacement de type

Le paquet [tachys 0.2.18](https://static.crates.io/crates/tachys/tachys-0.2.18.crate)
est épinglé au SHA-256
`a92ba81187437cc5df4281f2326a2e13cc81e8f96448292d1112388e2025ca66`.
Ses métadonnées Cargo désignent
[Leptos 7c0d1c1](https://github.com/leptos-rs/leptos/tree/7c0d1c169b4353ed169006b1d4a1563ce2e6f3d5/tachys).
Le fichier [LICENSE](tachys-0.2.18/LICENSE) vient de cette révision,
blob Git `77d5625cb3656d2a3b63aa2f0c9b27190330e4eb`.

Le delta Tachys de la [PR Leptos 4422](https://github.com/leptos-rs/leptos/pull/4422)
remplace l'unique appel au polyfill par `children.retain(Option::is_some)`.
Les entrées présentes gardent leur ordre et leur contenu. Le manifeste Cargo
normalisé retire la dépendance directe. Le manifeste source d'origine
`Cargo.toml.orig` est conservé pour la provenance et le verrou Cargo du paquet
amont est remplacé par celui du workspace Forge pour la résolution effective.

Dans `src/erased.rs`, Forge remplace `erased::ErasedBox` par les boîtes
standard `Box<dyn Any + Send>` et `Box<dyn Any>`. Les vues transférables
et locales conservent leurs bornes distinctes. Les conversions gardent
le contrôle de type, y compris avec `erase_components`. La destruction
de la valeur relève de `Box`. Le manifeste effectif retire `erased`.

Ces deltas concernent les nouveaux builds. Le WASM 049 conserve ses
réserves historiques. Retirer le backport après qualification d'une
version Tachys publiée qui permet de lever les deux réserves.
