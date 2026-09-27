# Navigation — préparation exécutée le 26 septembre 2026

Verdict : `not_selected`, `blocked_pending_contract_and_governance_gaps`.
Recherche documentaire primaire et inspection locale, sans build, changement de
dépendance, intégration runtime, publication ni nouvelle exception d'advisory.

## Résultat utile pour la reprise

Un simple bump de version n'a pas été identifié comme solution. Le laboratoire
épingle `rerecast 0.4.0`, `bevy_rerecast 0.5.0`, `landmass 0.9.2`,
`bevy_landmass 0.12.0` et `landmass_rerecast 0.3.0`. La release et le changelog
amont consultés correspondent à cette génération de pile :
[Rerecast v0.5.0](https://github.com/janhohenheim/rerecast/releases/tag/v0.5.0),
[changelog Landmass](https://github.com/andriyDev/landmass/blob/main/CHANGELOG.md).
Une version ou un champ de configuration présent ne suffit pas à qualifier la
capacité requise.

| Condition ADR-0009 | Constat vérifié | Conséquence pour la prochaine expérience |
|---|---|---|
| Sortie globale valide | La preuve locale rejette toujours la conversion brute de la vallée au polygone 2819 ; aucune preuve nouvelle de correction | Isoler un reproducer crop/full et conserver le défaut visible |
| Tuiles et reconstruction partielle | Fonctionnalités encore non cochées dans la documentation de la génération examinée | Mesurer frontières/padding, ajout/retrait et hashes des régions inchangées ; ne pas assimiler `tile_size` à un système complet |
| Collisions/Rapier | Backend physique documenté : Avian ; backend Rapier discuté dans un PR, non acquis | Adapter commun aux collisions ou backend maintenu à qualifier, notamment rotations/échelles |
| Dépendances | `bevy_rerecast_core 0.5.0 -> bincode 2.0.1`, advisory de non-maintenance sans version corrigée | Migration maintenue ou décision de gouvernance explicite ; aucune exception automatique |
| Dynamique/browser | Compilation WASM historique, pas preuve d'exécution navigateur | Obstacles dynamiques et readiness à mesurer réellement |
| Benchmark/acceptation | Rapport historique conservé ; aucun binaire réutilisable trouvé dans les emplacements inspectés | Reconstruction bornée du labo puis nouveau replay lorsque les conditions ont évolué ; acceptation ADR séparée |

### Sources primaires des blocages

- [Rerecast, état des fonctionnalités](https://github.com/janhohenheim/rerecast/blob/v0.5.0/crates/bevy_rerecast/src/lib.rs#L173-L203) : génération de tuiles et régénération partielle non acquises.
- [Générateur global](https://github.com/janhohenheim/rerecast/blob/v0.5.0/crates/bevy_rerecast_core/src/generator/mod.rs#L157-L253) et [contrat backend](https://github.com/janhohenheim/rerecast/blob/v0.5.0/crates/bevy_rerecast_core/src/backend.rs#L10-L22).
- [Contraintes Landmass](https://github.com/andriyDev/landmass/blob/main/crates/landmass/src/nav_mesh.rs#L16-L64), [conversion des indices](https://github.com/andriyDev/landmass/blob/main/crates/landmass_rerecast/src/raw_conversion.rs#L3-L55), [validation lors de la conversion](https://github.com/andriyDev/landmass/blob/main/crates/landmass_rerecast/src/lib.rs#L171-L214). Les liens `main` sont des observations de recherche et non des pins de dépendances.
- [Backend physique documenté](https://github.com/janhohenheim/rerecast/blob/v0.5.0/crates/bevy_rerecast/src/lib.rs#L136-L161) ; [PR Rapier #55](https://github.com/janhohenheim/rerecast/pull/55), discussion de scaling et statut non intégré observés.
- [RUSTSEC-2025-0141](https://rustsec.org/advisories/RUSTSEC-2025-0141) : bincode non maintenu, pas de version corrigée indiquée. L'absence de bincode dans le lockfile produit ne résout pas son usage dans le laboratoire isolé.

## Preuve locale conservée, non rejouée

Rapport :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/navigation-benchmark-r1-068-isolated-final-20260901-190000/navigation-provider-benchmark.v1.json`.

SHA-256 : `208ab622a9bab445135c528a9f6356afd1a8b0b29605f4e10cb0ab8c8dd3bf19`.
Lockfile du laboratoire observé :
`d6e13aa548ccaaaac0a783b3f775ebab8ea0c4d7f5f0c7ef7468cef096571dd6`.

Le rapport de septembre décrit 74 337 triangles source, 2 834 polygones et
17 203 triangles de détail. La normalisation diagnostique inverse un polygone
et 18 triangles ; elle reste `winding_normalization_accepted=false`. Ni son
existence ni le succès du programme de benchmark ne valent sélection fournisseur.

Recherche bornée d'un exécutable existant : `.gate1-evidence-runtime`,
`.gate1-build-cache`, `.build/cargo-target`, `OpenRPG_Forge-Rust/target` et target
local du laboratoire. Aucun exécutable navigation rejouable trouvé ; aucune
reconstruction redondante n'a été lancée alors que les blocages de contrat sont
déjà établis. Cela ne signifie pas que toute la machine a été inventoriée.

## Frontières

[ADR-0009](../../ADR/ADR-0009-navigation-provider-benchmark.md),
[preuve historique R1-068](../gate1/R1_068_NAVIGATION_PROVIDER_BENCHMARK.md),
[README du laboratoire](../../../tools/forge_navigation_benchmark/README.md).

Le travail autorisé n'exige pas une nouvelle permission pour chaque commande de
laboratoire raisonnable ; il exige en revanche de ne pas adopter silencieusement
une architecture, un fallback de winding ou une dérogation de sécurité. Aucun
contact amont/PR n'a été émis. La Gate 3 n'est pas ouverte par cet audit.

Recherche confiée à Luna max. L'outil de délégation n'expose pas de champ
`service_tier` ; aucun niveau de service particulier n'est affirmé.
