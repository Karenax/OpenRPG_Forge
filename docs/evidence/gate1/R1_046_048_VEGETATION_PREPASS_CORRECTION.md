# Gate 1 — correction de la prépasse végétation et réacquisition R1-046 à R1-048

Date : **2026-09-01**

Verdict : **CORRECTIF AUTOMATISÉ PASS — PREUVE RELEASE INTÈGRE — QUALIFICATION HUMAINE/GPU EN ATTENTE**

Gate 1 reste **OUVERTE**. Cette campagne corrige et mesure le symptôme bleu-noir refusé dans la
campagne native initiale. Elle ne constitue ni une acceptation artistique, ni un benchmark sur le GPU
cible, ni un parcours gameplay.

## Identité exacte

| Élément | Valeur |
|---|---|
| commit source | `6a24cabe69e242ff551b265d44da67ca8d017c98` |
| arbre source | `38a6990ce02baa651773d23d1038b3ef9ff9e76d` |
| binaire Release | 81 970 688 octets |
| SHA-256 du binaire | `af63ad658557b706159caffbf7e05fb517d72cdf495eed1c234c4243ce187ddf` |
| GPU / backend | AMD Radeon RX 7800 XT / Vulkan |
| pilote | `26.8.1` |
| résolution / qualité | 1920 x 1080 / High fixe / VSync faux |
| assets Cadle | aucun |

Le commit ne modifie que `crates/forge_engine/src/vegetation.rs` : 13 lignes ajoutées, dont un test.
Le matériau de végétation devient forward-only pour la prépasse caméra via
`MaterialExtension::enable_prepass() == false`. Le défaut provenait d'échantillons dithered et animés
qui pouvaient laisser une profondeur sans couleur forward correspondante. Les chemins de matériau et
de shader d'ombre restent présents ; ce lot ne revendique toutefois pas une qualification exhaustive
des ombres.

## Validation du correctif

| Contrôle | Résultat |
|---|---|
| `cargo fmt --all -- --check` | **PASS** |
| `git diff --check` | **PASS** |
| Clippy `forge_engine` + `forge_runtime`, tous targets, warnings en erreurs | **PASS** |
| tests végétation ciblés | **18/18 PASS** |
| tests `forge_engine` | **127/127 PASS** |
| tests `forge_runtime` | **36/36 PASS** |
| intégration structured logging | **1/1 PASS** |
| audit provenance strict | **PASS — 1 manifeste, 27 fichiers, 9 modèles** |
| auto-test du harnais de provenance | **7/7 PASS** |
| build Release exact-commit | **PASS** |
| acquisition visuelle Release | **PASS — 29 PNG et MP4** |
| logs runtime | **PASS — 0 warning, 0 erreur, 0 panic** |
| décor gouverné | **PASS — activation unique, 9 modèles, aucun fallback** |

Le clone Cadle de référence est resté extérieur au candidat, détaché et propre sur
`ea0da62549980816a6a27e08b75808b1f56d70e7`, arbre
`f0d52e434454fe302052d22eafde203bc361cc1f`. Aucun asset Cadle n'a été copié dans Forge.

## Mesure du symptôme corrigé

Le masque diagnostique historique compte les pixels `R < 20`, `G < 70`, `B > 45` et `B > G` aux
mêmes cadrages. Il mesure le symptôme bleu-noir ; ce n'est pas une métrique générale de qualité.

| Vue | Avant `e717c0e8` | Après `6a24cabe` Release | Réduction |
|---|---:|---:|---:|
| distant clear | 5 737 | 64 | 98,88 % |
| human clear | 4 735 | 6 | 99,87 % |
| water clear | 1 426 | 0 | 100,00 % |
| mouvement 000 | 4 295 | 1 | 99,98 % |
| mouvement 012 | 2 599 | 13 | 99,50 % |
| mouvement 023 | 3 000 | 8 | 99,73 % |

L'inspection technique à résolution originale confirme la disparition des grandes silhouettes
bleu-noir et des stries sombres qui dominaient les vues humaine et lointaine. Les ombres de scène
restent visibles. La composition, les matériaux, les transitions temporelles et l'impact comparatif
doivent encore être jugés lors d'une revue humaine dédiée.

## Acquisition Release et intégrité

Le run local contient 34 fichiers et 61 285 528 octets. Le validateur lie les artefacts au commit et à
l'arbre exacts, vérifie 29 PNG 1920 x 1080, la séquence clair -> pluie -> clair, la wetness CPU/ECS
`0 -> 0,78 -> 0`, la qualité High fixe, l'identité GPU et l'absence d'entrée de log interdite. Le MP4
H.264 contient 24 frames à 8 FPS pendant trois secondes.

Le lot Git retient 14 artefacts et 18 004 279 octets : cinq vues fixes, les frames 000/012/023, le
MP4, les deux ledgers, les deux logs et l'audit de provenance. Les 21 PNG intermédiaires restent liés
par taille et SHA-256 dans le ledger de validation.

Artefacts :
[`native/6a24cabe69e242ff551b265d44da67ca8d017c98/visual-proof`](native/6a24cabe69e242ff551b265d44da67ca8d017c98/visual-proof).

Hashes principaux :

- validation : `251dc047784a47c55335cd79d3236927cb1fba9b9ea1d8a3c5480ea486175d0a` ;
- ledger : `f5cbae80c251684324aa374fcba81201d675fa7f1b5f025ddc17015dc1ff3bc2` ;
- MP4 : `afac26ca5b27afa363e03e9b256f87816541248d367a674ca92f2e449fc5118a` ;
- provenance : `781f682cf3aca8522302af96758b7efb9ae301619a37ac508ddb0b4e55a477f4`.

## Cartographie conservatrice

| Route | Avancée acquise | Ce qui manque | État |
|---|---|---|---|
| `R1-014` | aucune réacquisition Browser de ce correctif | Browser exact et inspection temporelle humaine | **OUVERTE** |
| `R1-023` | cadrage eau plus lisible | réflexion/réfraction par profil et qualification GPU/humaine | **OUVERTE** |
| `R1-025` | endpoints CPU/ECS réacquis | exécution GPU et acceptation visuelle dry/wet/dry | **OUVERTE** |
| `R1-026` | grade/bloom réacquis côté contrat | paramètres/exécution GPU et rendu accepté | **OUVERTE** |
| `R1-046` | vue humaine corrigée et réacquise | revue humaine comparative | **OUVERTE** |
| `R1-047` | vue lointaine corrigée et réacquise | revue humaine LOD/composition | **OUVERTE** |
| `R1-048` | MP4 Release réacquis | contrôleur gameplay, stutter représentatif et revue humaine | **OUVERTE** |
| `R1-037` | primitives/invocations opaques disponibles | instrumentation exhaustive des draw calls ou capture GPU dédiée | **OUVERTE** |
| `R1-041` | smoke sur RX 7800 XT seulement | RTX 3060 12 Go ou RX 6600 8 Go | **OUVERTE** |

Le ledger conserve honnêtement `weather_gpu_execution_observed=false`,
`human_review_performed=false`, `gameplay_controller_exercised=false` et
`gate1_routes_qualified=false`. Les FPS affichés dans les captures sont instantanés et ne remplacent
pas moyenne, p95, 1 % low ou analyse de stutter sur le matériel cible.

## Conclusion

Le défaut depth-only majeur est corrigé, testé, acquis en Release et versionné sans asset Cadle.
`R1-046` et `R1-047` deviennent candidates à revue humaine, mais aucune route ouverte n'est cochée par
ce run seul. Gate 1 reste **OUVERTE** et Gate 2 reste **INTERDITE** sans passage formel ou nouvelle
dérogation explicite.
