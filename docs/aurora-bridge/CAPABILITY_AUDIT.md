# Audit — capacités multi-zone et sauvegarde d'OpenRPG

- **Tâche :** `AB-M0-16`
- **Date :** 7 novembre 2026
- **Dépôt :** OpenRPG Forge (`E:\OpenRPG_Forge\OpenRPG_Forge-Rust`)
- **Branche / HEAD :** `Rust` / `97176c19`
- **Statut :** audit terminé ; capacités constatées et testées ; **aucune preuve de jouabilité NWN**
- **Périmètre examiné :** `crates/forge_project/src/module_package.rs`,
  `crates/forge_project/tests/module_package.rs`, `crates/forge_project/tests/game_save_host.rs`,
  `crates/forge_project/tests/workspace_launcher.rs`, `crates/forge_gameplay/src/save.rs`,
  `crates/forge_gameplay/src/{scripts,narrative,factions}.rs`,
  `tools/forge_delivery/src/module_runtime.rs`, `apps/forge_runtime/src/module_game/mod.rs`
- **Hors périmètre :** installation NWN réelle, assets propriétaires, rendu, VM NCS,
  transitions inter-zones non implémentées.

## 1. Question auditée

La passerelle doit produire un module synthétique à **deux zones** et prouver sauvegarde/reprise.
L'audit devait répondre à quatre questions :

1. le manifeste OpenRPG représente-t-il réellement plusieurs zones, ou seulement une liste
   structurelle jamais validée avec plus d'une entrée ?
2. le runtime charge-t-il une zone choisie, ou fusionne-t-il les zones ?
3. la sauvegarde couvre-t-elle plusieurs zones ?
4. quels quotas natifs contraignent la conversion d'un module NWN ?

Conclusion : le manifeste **représente et valide plusieurs zones**, le runtime **charge exactement
une zone par `area_id`**, la sauvegarde est **mono-zone**, et les quotas natifs sont **nettement plus
bas** que les capacités d'un module NWN. La passerelle peut donc livrer deux zones dans un bundle,
mais ne peut pas encore prétendre à une visite continue ni à une sauvegarde inter-zones.

## 2. Manifeste multi-zone

`ModulePackageManifest.zones` est un `Vec<ModuleZone>` non vide, borné par
`MAX_MODULE_ZONES = 4_096`. Chaque `ModuleZone` porte `area_id`, `archive_path`,
`archive_sha256`, `head_hash`, `gameplay_sha256` et `source_blobs`.

`validate_zones` (`crates/forge_project/src/module_package.rs`) impose :

- unicité des `area_id` et des `archive_path` ;
- présence du fichier d'archive déclaré avec le rôle `ZoneArchive` ;
- concordance exacte entre `archive_sha256` et le fichier déclaré ;
- bornage des `source_blobs` et résolution de chaque blob vers un `SourceBlob` unique.

`validate_zone_archive` vérifie ensuite, pour une zone donnée : taille et SHA-256 de l'archive,
`project_id`, `area_id`, hash de la tête canonique, identité du module dans le gameplay,
hash du gameplay, et cohérence des catalogues visuels.

### Preuve existante à deux zones

Le test `declared_shared_vegetation_files_are_charged_once_across_two_authored_areas`
(`crates/forge_project/tests/module_package.rs`) construit un manifeste avec **deux zones
réellement authorées**, deux archives distinctes et deux entrées `ModuleZone`. Il appelle
`module.validate()` puis `validate_zone_archive` sur chacune des deux zones. Ce test n'est pas
seulement structurel : les deux archives sont décodées et vérifiées.

Le test `duplicate_case_paths_parent_conflicts_and_zone_ids_are_refused` prouve qu'un doublon
d'`area_id` est refusé (`ModulePackageError::Duplicate`).

Le test `two_independent_modules_roundtrip_without_an_engine_or_filesystem` prouve que deux
modules indépendants (une zone chacun) font un aller-retour JSON sans moteur ni système de
fichiers.

**Classement :** manifeste multi-zone = **testé** ; doublons = **testé** ; validation d'archive
par zone = **testé**.

## 3. Runtime : une seule zone à la fois

`forge_delivery::module_runtime::load(package_or_directory, area_id, cache_root)` :

1. admet le paquet (`.orpmod` ou répertoire installé) ;
2. vérifie que l'`area_id` demandé existe dans `manifest.zones` (`require_zone`) ;
3. sélectionne **une seule** entrée `ModuleZone` ;
4. lit et vérifie l'archive pinnée de cette zone ;
5. décode un unique `ZoneEditSession` dans `LoadedModuleZone`.

`apps/forge_runtime/src/module_game/mod.rs` appelle `load` avec `options.area_id` et construit une
seule scène. Il n'existe ni liste de zones chargées, ni graphe de transitions, ni état « zone
courante » persistant. Une `area_id` absente produit l'erreur explicite
`Requested area is absent from this module`.

Le cache `.orpmod` est immuable par révision ; un répertoire installé est lu sur place. Aucun
mécanisme ne remplace une zone déjà chargée par une autre en cours de session.

**Classement :** sélection d'une zone par `area_id` = **constatée dans le code** ;
transitions inter-zones = **absentes** ; chargement simultané de plusieurs zones = **absent**.

## 4. Sauvegarde et reprise

`GameSave` (`crates/forge_gameplay/src/save.rs`) contient exactement un `module_id` et un
`area_id`, ainsi que `source_hash`, `functional_hash`, `state_hash` et l'état. `GameSession::resume`
refuse toute sauvegarde dont le `module_id` ou l'`area_id` diffère du `GameplayDocument` fourni.
`MAX_SAVE_BYTES = 4 MiB`.

Il n'existe :

- ni sauvegarde multi-zone ;
- ni état de groupe ou de progression inter-zones ;
- ni reprise qui recharge automatiquement la zone d'une sauvegarde ;
- ni format de sauvegarde de module complet.

Les 9 tests `game_save_host` couvrent la fermeture/réouverture, la reprise, les versions v2 à v6,
le refus d'une source périmée, le refus d'une sauvegarde incompatible et l'atomicité du répertoire
de sauvegarde. Aucun ne couvre deux zones.

**Classement :** sauvegarde mono-zone = **testée** ; sauvegarde/reprise multi-zone =
**absente**. La porte « sauvegarde/reprise » de la première livraison doit donc être satisfaite
**dans une zone**, ou attendre une tâche dédiée.

## 5. Quotas et budgets

### Manifeste et transport

| Quota | Valeur | Source |
| --- | --- | --- |
| Manifeste | 4 MiB | `MAX_MODULE_MANIFEST_BYTES` |
| Fichiers | 100 000 | `MAX_MODULE_FILES` |
| Assets | 65 536 | `MAX_MODULE_ASSETS` |
| Zones | 4 096 | `MAX_MODULE_ZONES` |
| Dépendances d'asset | 128 | `MAX_MODULE_DEPENDENCIES` |
| Fichier | 16 GiB | `MAX_MODULE_FILE_BYTES` |
| Payload total | 256 GiB | `MAX_MODULE_PAYLOAD_BYTES` |

### Runtime

| Budget | Valeur | Source |
| --- | --- | --- |
| Blob source | 64 MiB | `MAX_SOURCE_BLOB_BYTES` |
| Sources totales chargées | 256 MiB | `MAX_MODULE_EXPORT_SOURCE_BYTES` |
| Sauvegarde | 4 MiB | `MAX_SAVE_BYTES` |

### Gameplay natif (par document de zone)

| Quota | Valeur | Source |
| --- | --- | --- |
| Scripts | 32 | `MAX_SCRIPTS` (`scripts.rs`) |
| Pas d'exécution | 256 | `MAX_SCRIPT_STEPS` (`scripts.rs`) |
| Variables narratives | 128 | `MAX_NARRATIVE_VARIABLES` (`narrative.rs`) |
| Factions | 32 | `MAX_FACTIONS` (`factions.rs`) |

Ces quotas sont **par zone** pour le gameplay, mais le budget source de 256 MiB est **par
chargement de zone**. Un module NWN peut dépasser ces limites ; la passerelle doit produire un
diagnostic explicite et ne jamais tronquer silencieusement.

**Classement :** quotas = **constatés et testés** pour le manifeste
(`quotas_are_checked_before_an_archive_or_asset_is_read`) ; budgets runtime = **constatés dans le
code**, couverts par un seul test de bornage.

## 6. Niveau launcher / distribution

`crates/forge_project/tests/workspace_launcher.rs` contient
`distributable_refuses_partial_modules_until_the_exporter_can_close_them` : une configuration de
jeu peut référencer plusieurs zones, mais la dérivation d'un manifeste distribuable échoue tant
que toutes les zones du module ne sont pas déclarées dans les contenus du jeu. Cela prouve que
la chaîne de distribution **comprend** plusieurs zones, sans prouver leur chargement.

**Classement :** déclaration multi-zone côté launcher = **testée** ; distribution jouable
multi-zone = **non prouvée**.

## 7. Conséquences pour la passerelle Aurora

1. **Bundle à deux zones :** représentable et validable dès le manifeste actuel. La première
   livraison peut donc contenir deux zones dans un seul bundle.
2. **Visite :** le runtime ne visite qu'une zone par session. La première livraison doit soit
   restreindre la preuve de visite à une zone, soit ajouter explicitement une transition
   inter-zones (hors périmètre M1 actuel).
3. **Sauvegarde/reprise :** conserver le format mono-zone pour M1 ; documenter que la sauvegarde
   lie `module_id` + `area_id`. Une sauvegarde multi-zone est une capacité ultérieure.
4. **Sélection :** le bundle doit exposer des `area_id` stables et un point d'entrée explicite ;
   le runtime refuse une zone absente. Aucune fusion implicite de zones n'est autorisée.
5. **Quotas :** mapper les scripts, variables, factions et pas d'exécution vers les quotas natifs,
   avec diagnostics `unsupported`/`limit` plutôt que troncature. Les 256 MiB de sources runtime
   imposent de sélectionner les blobs par zone.
6. **Objets sources :** `ModuleObjectBinding` lie déjà un `area_id` à des surfaces sources et à
   des assets pinnés ; c'est le précédent structurel pour importer une zone NWN comme objet
   source plutôt que comme terrain Forge.

## 8. Vérifications exécutées

```text
cargo test -p forge_project --test module_package --test game_save_host
58 passed (module_package), 9 passed (game_save_host), 0 failed
```

Tests cités :

- `declared_shared_vegetation_files_are_charged_once_across_two_authored_areas` ;
- `duplicate_case_paths_parent_conflicts_and_zone_ids_are_refused` ;
- `quotas_are_checked_before_an_archive_or_asset_is_read` ;
- `two_independent_modules_roundtrip_without_an_engine_or_filesystem` ;
- les 9 tests `game_save_host`.

Le test du runtime est exécuté séparément, car `tools/forge_delivery` est exclu du workspace :

```text
cargo test --manifest-path tools/forge_delivery/Cargo.toml --lib
95 passed, 2 ignored, 0 failed
```

Il inclut `module_runtime::source_tests::source_metadata_is_bounded_before_opening_or_allocating_any_payload`.
Les deux tests ignorés exigent des privilèges Windows (mode développeur ou volume avec noms 8.3) ;
ils ne concernent pas le runtime de module.

Preuves : `F:\OpenRPG_Forge\Preuves-et-analyses\actives\aurora-bridge\m0-16-module-package-save.log`
et `...-m0-16-runtime.log` (+ `.receipt.json`).

## 9. Limites de l'audit

- Aucun chargement réel d'un `.orpmod` à deux zones n'a été exécuté : le test à deux zones porte
  sur le manifeste et les archives, pas sur la sélection runtime d'une zone parmi deux.
- Le test `source_metadata_is_bounded_before_opening_or_allocating_any_payload` borne les métadonnées
  de sources ; il ne charge pas de module complet.
- Aucune transition inter-zones n'existe ; l'audit ne prouve donc pas un parcours continu.
- Aucune sauvegarde multi-zone n'existe ; les 9 tests couvrent une seule zone.
- Les quotas gameplay sont lus dans le code et non éprouvés par un module NWN converti.
- Aucun test de performance, de rendu ou de navigation NWN n'a été exécuté.
