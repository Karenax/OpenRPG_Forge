# Recompilation de la livraison Windows — 9 octobre 2026

Demande propriétaire : recompiler l'ensemble installable après les mises à jour.
Livraison : `E:/OpenRPG_Forge/Forge-Installable-Preview-20261009`, version
`0.1.0-preview.20261009.1`. La livraison du 8 octobre `-r2` est conservée.

Les sources produit acceptées sont le snapshot `rebuild-002`, basé sur
`ae999c2f3395e9472301baebcfcb3d1b2ec0ec98`, inventaire SHA-256
`5a544352566726369d65764bd8896ac1ac050d11861b616c5a23e7dfa19e2727`.
Le premier essai a été conservé : les mises à jour concurrentes ont conduit à
reprendre le Studio et le jeu depuis ce second snapshot.

Le CLI et le launcher ont été recompilés pendant l'essai `rebuild-001`, puis
réutilisés dans `rebuild-002` après égalité exacte de toutes leurs sources et
configurations communes. Le Studio Windows, son frontend WASM, le worker de
navigation et le jeu ont été reconstruits depuis `rebuild-002`. Les preuves
de build précisent les profils ; ce chantier n'est pas un build release/LTO
de tout le workspace.

L'installable garde trois archives distinctes : applications, assets de référence
et module éditable. Les projets utilisateur conservent leur racine séparée et ne
sont pas remplacés par une nouvelle seed. WebView2 Microsoft x64 hors ligne reste
fourni. Le point d'entrée client est `Start-Forge.cmd`.

Le plan de transfert de la bibliothèque privée et son catalogue du 8 octobre
restent séparés, avec cette date de référence explicite. Cette recompilation ne
réinventorie pas le Vault complet et n'exporte pas ses 57 lots. Le contrat du
futur client conserve la portée et les limites décrites dans le
[plan de distribution](INSTALLABLE_DELIVERY_20261008.md) et
[ADR-0021](../ADR/ADR-0021-detached-delivery-installation.md).

La [recette de reconstruction](../../scripts/delivery/README.md) décrit le gel,
les builds, l'assemblage et les essais sur des installations isolées. Les scripts
réellement utilisés après le gel sont conservés dans `build-inputs/orchestration`
avec leurs propres hashes, distincts du manifeste des sources produit.

Les résultats et les limites de cette livraison figurent dans le
[rapport daté](../evidence/gate3/INSTALLABLE_DELIVERY_20261009.md).
La recompilation et l'installation locale ne clôturent pas Gate 3 ; les critères
RPG, CI distante sur le commit exact et validation humaine restent distincts.
L'objectif moteur générique précédemment mis en pause reste en pause.
