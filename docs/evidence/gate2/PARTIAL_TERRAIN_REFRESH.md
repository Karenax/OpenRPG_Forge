# Gate 2 — recalcul partiel du terrain Studio

Date : 4 septembre 2026. Branche : `Rust`.

## Verdict et portée

**R2-014 PASS local : Gate 2 atteint 12/43 et reste ouverte.**

Le candidat source est `b290e7f2744a7872cfb6af6ed8ddb8d3d5deed1f`, arbre
`800848108b46ed9acc43cb184bad3f1862847bbe`. Un trait réel dans le Studio natif, puis son annulation
et son rétablissement, ont chacun remplacé **3 chunks sur 64** et conservé les **61 autres**. Les
snapshots enregistrés prouvent en plus que l'annulation restaure exactement le contenu de départ et
que le rétablissement restaure exactement le contenu édité, hors estampilles de révision attendues.

Ce PASS ne qualifie ni la peinture de matériaux, ni la performance interactive/GPU, ni toute la
Gate 2. La CI distante reste `NOT RUN — infrastructure billing` sous ADR-0006. Aucun audit PresentMon
n'a été relancé et aucun asset Cadle n'a été utilisé.

## Tranche implémentée

- `forge_world` projette un document déjà accepté sur un cache vérifié et calcule les remplacements
  par le diff exact des hauteurs. Le chemin de commande strict `apply_partial_update` conserve son
  exigence de révision suivante ; le rafraîchissement de projection accepte séparément les sauts
  d'historique déjà validés.
- Le backend Studio garde un unique cache dérivé et jetable. Il ne le publie qu'après validation et
  sérialisation bornée. Un changement structurel explicite provoque une reconstruction complète ;
  une corruption, un hash incohérent ou une requête périmée reste une erreur visible.
- L'IPC transmet le hash de la projection actuellement affichée. Il ne prétend jamais qu'un client
  inconnu possède la bonne base.
- Le renderer conserve les 64 chunks présentés pendant qu'il prépare les seuls nouveaux meshes,
  puis permute atomiquement les remplacements. Une préparation annulée ou invalide libère ses meshes
  cachés sans toucher à la scène visible. Les entités et handles des chunks non affectés restent
  identiques.
- Une mise à jour sans changement de hauteurs peut avancer la révision sans upload de mesh.

Aucune dépendance, seconde représentation canonique, copie de terrain ou voie de mutation propre au
frontend n'a été ajoutée.

## Vérifications automatisées

| Contrôle | Résultat |
|---|---|
| `cargo test --workspace --all-targets --locked --offline` | 462 PASS, 0 échec, 0 ignoré, 13 cibles |
| ciblage cache natif et parité géométrique | 4 PASS |
| Clippy workspace natif strict | PASS |
| Clippy Studio WASM strict final | PASS |
| `trunk build --release --locked --offline` | PASS, 1 min 58 s |
| `cargo build -p forge_studio --bin forge-studio --release --locked --offline` | PASS, 1 min 54 s |
| formatage et `git diff --check` du candidat | PASS |

Le premier Clippy WASM a correctement refusé une fonction de 108 lignes pour une limite de 100. Le
code a été découpé sans `allow`, puis le contrôle final est passé ; le log rouge est conservé au lieu
d'être masqué. Les six nouveaux tests couvrent notamment la parité avec la génération complète, les
sauts de révision, la corruption du cache, la permutation différée, les requêtes remplacées, le hash
de base erroné et une mise à jour de métadonnées sans mesh.

Binaires immuables du candidat :

| Fichier local ignoré | Octets | SHA-256 |
|---|---:|---|
| `bin/forge-studio-b290e7f.exe` | 33 982 464 | `12cc24a3fe8beb4a48d0bb6de42b7ddd73df646963eabae929f10b9f5657d581` |
| `bin/forge_studio-b290e7f.wasm` | 40 236 076 | `6d54b0b783ac75ae0aa72a723181fc49840b6f48d3ccbfdb8a44b2c48dfa8a71` |

Le bundle WASM contient l'identifiant complet du candidat. Les copies archivées empêchent une future
reconstruction de rendre cette preuve ambiguë.

## Acquisition native réelle

Le Studio Release a été lancé dans une session neuve puis le snapshot r25 qualifié précédemment a été
ouvert explicitement. La zone `Clairière du bureau` a été sélectionnée et agrandie dans Monde.

| Étape | Action | Résultat affiché |
|---|---|---|
| r25 | premier chargement, aucune base client | 64 recalculés / 0 conservés |
| r26 | Lever, rayon 16 m, intensité 50 %, amplitude 4 m ; trait (920,450) → (960,450) | 3 recalculés / 61 conservés |
| r27 | annuler avec Ctrl+Z hors champ éditable | 3 recalculés / 61 conservés |
| r28 | rétablir avec Ctrl+Maj+Z hors champ éditable | 3 recalculés / 61 conservés |

À chaque transition, les 64 chunks sont restés visibles ; aucun écran vide ni erreur n'a été observé.
Les captures PNG et leurs arbres d'accessibilité ont été enregistrés après stabilisation. r26, r27
et r28 ont chacun été sauvegardés par l'interface dans un dossier neuf, sans écrasement.

## Validation des sources enregistrées

La normalisation retire uniquement la révision du workspace et celle de chaque terrain. Pour le
contrôle des données non ciblées, elle retire en plus seulement `height_deltas` de la zone éditée.

| Snapshot | Révision | Deltas ciblés | Octets | SHA-256 |
|---|---:|---:|---:|---|
| r25 | 25 | 5 609 | 826 654 | `467470990459c12c33a96093b2cad80d6dccd5368185a10d9276c52e6af469b3` |
| r26 | 26 | 6 120 | 901 351 | `86fcd8df81d5b18053c4e089210ad3f7dfd444cbf47deb0a0fac86407523a145` |
| r27 | 27 | 5 609 | 826 654 | `2bca57091a0a6d1863cd709d45d71ea2514ffc47d3fced66b05a9b2c64f72d1b` |
| r28 | 28 | 6 120 | 901 351 | `f872c2bd901a5cb293d5d3ec9e238e233d078a4e583deb055d9da4402f1191c3` |

Résultats exacts : r26 diffère de r25 ; r27 = r25 ; r28 = r26. Le projet, les modules, les deux
autres zones, les seeds — dont `u64::MAX` — les métadonnées, les matériaux et tout champ hors hauteurs
de la zone ciblée restent identiques.

## Preuves reproductibles

Le manifeste [PARTIAL_TERRAIN_REFRESH_ARTIFACTS.json](PARTIAL_TERRAIN_REFRESH_ARTIFACTS.json) fixe
20 artefacts locaux par taille et SHA-256. Le validateur :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate2PartialTerrainEvidence.ps1
```

Il vérifie le candidat Git, tous les hashes, les résultats automatisés, les compteurs natifs et les
comparaisons de sources. Il ne relance pas le Studio et ne transforme pas les captures en verdict GPU
ou de performance.

## Limites conservées

- Les temps de boucle affichés sont des observations instantanées, pas un benchmark ni des timestamps
  GPU. R2-F reste ouverte.
- Le snapshot r28 est sauvegardé et vérifié, mais cette acquisition ne revendique pas sa réouverture
  dans un nouveau processus ; le chargement r25 prouve volontairement le repli complet 64/64 sans base.
- Le rendu vert uniforme et les pics de bruit appartiennent à la tranche précédente. R2-012 et les
  outils de biomes/végétation doivent apporter les matériaux et la densité naturelle demandés.
- L'historique reste borné en mémoire ; crash recovery, audit général et bus transactionnel complet
  restent des routes distinctes.
- Gate 2 reste ouverte ; Gate 3 n'est pas commencée par ce lot.
