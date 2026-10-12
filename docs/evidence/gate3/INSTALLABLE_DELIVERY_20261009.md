# Installable recompilé — preuves locales du 9 octobre 2026

Livraison : `E:/OpenRPG_Forge/Forge-Installable-Preview-20261009`.
Version : `0.1.0-preview.20261009.1`. Ouvrir `Start-Forge.cmd`, puis préparer
et installer les trois packages dans le hub. La livraison du 8 octobre `-r2`
et les installations de test sont conservées. Aucun commit, push ni envoi
distant effectué par ce chantier de recompilation.

## Résultats sur les fichiers livrés

| Contrôle | Résultat et portée |
|---|---|
| CLI/installateur | Compilation release fraîche ; huit tests d'intégration PASS |
| Launcher | Compilation Tauri/Rust release fraîche ; processus installé vivant après 8 s |
| Studio | Frontend WASM release et worker navigation release reconstruits ; natif Windows reconstruit avec assets embarqués ; processus installé vivant après 8 s |
| Installation | PASS : hashes des trois archives, plan sans écriture, Apply rejoué idempotent, vérification de tous les fichiers installés |
| Workspace éditable | 27 fichiers de seed copiés dans une racine utilisateur isolée |
| Mise à niveau | Ancien `20261008.2` → nouveau `20261009.1` → rollback PASS ; révisions 1 → 2 → 3 ; package actif précédent restauré |
| Données utilisateur | 28 fichiers inchangés par hash après mise à niveau et rollback, dont une retouche de `authoring.json` et un fichier utilisateur ajouté |
| Jeu installé | Diagnostic natif `--walk-smoke` PASS, exit 0 : fonctionnel, backend, cadence, ombres et préparation poissons PASS |
| Bootstrap | `Start-Forge.ps1 -CheckOnly` sous Windows PowerShell 5.1 PASS ; intégrité launcher et WebView2 installé détectés |
| Prérequis fourni | Installeur WebView2 x64 hors ligne, signature Microsoft `Valid`, hash vérifié après copie |

Jeu en 1920×1080 : **64,34 FPS moyens**, 1 % bas **31,31 FPS**, p95 **18,03 ms**,
235 échantillons du protocole natif ; `test_throttle_30hz=false`, vitesse d'eau
canonique `1.5`. Le critère natif existant passe sans modification de seuil.
Ces intervalles de frames sur ce poste de développement ne sont pas des timings
GPU, un benchmark prolongé ou une garantie à 60 FPS constants. La capture
`game.png` produite par ce même binaire a été inspectée ; elle montre la scène
native, sans constituer une nouvelle acceptation humaine ou artistique.

Le launcher et le Studio sont vérifiés au démarrage natif. Aucun parcours visuel
complet de leurs interfaces n'est déduit de la survie des processus. Le jeu
embarqué reste la promenade native Tidewater ; le diagnostic ne qualifie pas
le clavier/souris OS, la traversée des bâtiments ou l'aventure RPG complète.

## Sources et profils exacts

Snapshot produit `rebuild-002`, HEAD
`ae999c2f3395e9472301baebcfcb3d1b2ec0ec98`, 2 823 fichiers / 52 838 335 octets.
Au gel, le dépôt était propre. Manifeste SHA-256 :
`5a544352566726369d65764bd8896ac1ac050d11861b616c5a23e7dfa19e2727`.
Les entrées figées et le frontend embarqué sont restés identiques durant les builds.
Rust `1.98.0`, Windows x64 MSVC, dépendances verrouillées, offline, builds lourds
sérialisés. Les preuves de compilation restent `BUILD_ONLY` ; les essais runtime
ont leurs propres reçus associés aux hashes exacts des exécutables installés.

Le CLI et le hub ont été **recompilés pendant ce chantier dans `rebuild-001`**, puis
copiés dans `rebuild-002` après comparaison exacte de toutes leurs sources,
lockfiles et configurations communes. Ils ne proviennent pas de la livraison
du 8 octobre. Le manifeste dérivé par le CLI Tauri (version shorthand → version
et tableau features vide, sans changement de features) et les logs sont conservés.

Studio : natif Cargo dev sans symboles de debug, frontend et worker release,
entrée `index.html?authoring=1`, kit gouverné et imports statiques conservés et
épinglés depuis le candidat de référence existant. Le natif utilise
`tauri/custom-protocol` avec configuration `TAURI_CONFIG` explicite. Ces assets
existants ne sont pas présentés comme de nouvelles conversions ou qualifications.

Jeu : Cargo dev avec SDK de registre en cache ; dépendances locales du jeu
recompilées depuis les sources figées, moteur et exemple optimisés à niveau 3,
assertions du moteur/exemple désactivées selon le profil de cette preview.
Ce n'est pas un build release/LTO de tout le workspace. Les deux avertissements
WASM, l'avertissement de code inutilisé du jeu et l'avertissement existant de
compatibilité future `proc-macro-error2` restent visibles dans les logs.

| Exécutable | SHA-256 |
|---|---|
| `forge-delivery.exe` | `93938a7d90390684368698a3e32682c4768f93f39f1179097f8378314fef3c2c` |
| `ForgeSetup.exe` / `forge-hub.exe` | `d8720021d49c01e4117f0487a27fb7e32d4ef08629cd508cb860c200205bcc70` |
| `forge-studio.exe` | `643509dbc91c8a5a2b99a428aa0d5a005b33295a7edef0b628902bbfb0218605` |
| `forge-game.exe` | `e44ab6dcf166d1507fc12566637ba337c72ce7db1b0fe786c7d2d1298742f2a6` |

Deux fichiers de transport des textures importées ont été modifiés dans le dépôt
**après le gel**, pendant la compilation : `source_graph_bundle.rs` et
`source_graph_bundle/imported_storage.rs`. Le relevé `current-source-parity.json`
les identifie. Ces modifications concurrentes non commitées ne font pas partie
de cette version construite sur le snapshot accepté ; aucune parité avec le
working tree postérieur n'est revendiquée. La prochaine recompilation pourra
prendre un nouveau snapshot de ce chantier d'import lorsqu'il sera stabilisé.

## Assets séparés et preuves conservées

Trois packages : applications **370 645 707 octets**, assets de référence
**475 808 350 octets**, module éditable **54 614 767 octets**. Index et manifestes
regénérés ; le manifeste obsolète du playtest n'est pas utilisé comme preuve
du nouveau jeu. Aucun Cargo, Python ou Node requis côté client.

Le plan de transfert séparé de bibliothèque conserve sa **base du 8 octobre** :
108 233 fichiers client / 485 634 632 475 octets, 57 lots planifiés et catalogue
séparé déjà exporté. Le Vault complet n'a pas été réinventorié ni ses lots tous
exportés/envoyés par cette recompilation. La composition des lots par le futur
client et sa surface de transport restent à implémenter selon ADR-0021.

Preuves principales sous `E:/OpenRPG_Forge/.delivery-evidence/20261009/` :

- `rebuild-002/` : sources, delta, logs, commandes, preuves des exécutables,
  pins frontend et scripts réels dans `orchestration/` avec manifeste séparé.
- `rebuild-001/` : build/test CLI et build du launcher réellement utilisés ;
  premiers essais frontend conservés, sans transfert de leur verdict.
- `installation-test-001/verification.json` : installation fraîche.
- `upgrade-test-001/verification.json` : upgrade, rollback et données utilisateur.
- `entrypoint-test-001/entrypoints.json` et logs : probes natifs.
- `installation-test-001/user-data/captures/game.walk.json`, `game.water-query.json`
  et PNG : reçus/captures du jeu exact.
- `bootstrap-check.json`, `webview2-signature.json` : détection et prérequis.

La livraison contient des copies des preuves et scripts dans `build-inputs/`,
ainsi que `verification.json`, `VALIDATION.md` et `INTEGRATION-CLIENT.md`.
La [recette](../../../scripts/delivery/README.md) et le
[plan daté](../../planning/INSTALLABLE_DELIVERY_20261009.md) décrivent la reproduction.

Statut : **preview privée locale installable**, diagnostics locaux PASS.
Gate 3 reste ouverte : CI distante sur le candidat exact, critères RPG et
validation humaine ne sont pas remplacés par ces essais. L'installation du
prérequis sur Windows propre n'a pas été exécutée. Aucun nouveau verdict de
release ou de clôture de Gate 3 ; le chantier moteur générique reste en pause.
