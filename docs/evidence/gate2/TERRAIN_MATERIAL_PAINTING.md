# Gate 2 — peinture persistante des matériaux du terrain

Date : 4 septembre 2026. Branche : `Rust`.

## Statut et portée

**R2-012 est `PASS`.**

Le candidat automatisé est le commit
`1b70be6d2927b06a693e169cfc00d345be61bf70`, arbre
`c021d2a9411f814626505de6fd8b05fa1be3688d`, construit directement sur
`cf30e12b54a860813e67c09440b2118996b446b2`. Les tests, contrôles statiques et builds locaux sont
verts et leurs artefacts sont figés. La qualification finale a ensuite été exécutée avec le paquet
Tauri Release autonome du même candidat : peinture terre, recalcul partiel, undo, redo, sauvegarde,
fermeture et réouverture exacte sont validés.

La CI distante reste **`NOT RUN — infrastructure billing`** sous ADR-0006. PresentMon est
**`NOT RUN`** : aucune campagne de cadence, mesure GPU ou qualification de performance n'appartient à
ce lot, conformément à ADR-0010. Aucun asset Cadle et aucune nouvelle dépendance n'entrent dans le
commit.

Le log `native-release.log` du build Cargo avec `devUrl` est volontairement exclu des preuves : son
exécutable de 8,8 Mo n'était pas autonome. Seuls `tauri-release.log` et l'EXE Tauri figé de 34 Mo
qualifient ici l'application Windows autonome.

## Tranche fonctionnelle construite

- Le document terrain passe à la version 3 et porte des poids de matériaux authorés, clairsemés,
  canoniquement ordonnés, finis, positifs et normalisés dans l'ordre typé herbe/terre/roche.
- Les versions terrain 1 et 2 sont relues puis migrées sans inventer de poids authoré. La migration du
  workspace conserve les identités, seeds et hauteurs existantes.
- La brosse matériau est une commande pure, versionnée et bornée. Un trait continu produit une seule
  révision, un reçu sérialisable et un snapshot inverse ; le dry-run calcule le même candidat sans
  modifier la session.
- L'IPC Studio expose les trois outils `Peindre · Herbe`, `Peindre · Terre` et `Peindre · Roche` sur
  le même picking, le même rayon/intensité et le même chemin de trait que la sculpture.
- L'application native exécute aussi cette brosse sur le pool bloquant. L'undo, le redo, la sauvegarde
  atomique et la réouverture passent par le service de workspace existant, sans source canonique dans
  le frontend ou dans Bevy.
- Le recalcul partiel remplace uniquement les chunks dont les poids de sommets changent. Une
  modification de matière ne reconstruit aucun collider Rapier ; un point intérieur touche un chunk
  et un point de couture peut en toucher quatre.
- Une suppression des poids authorés restaure exactement la projection procédurale précédente. Les
  changements combinés hauteur/matière restent égaux à une reconstruction complète.

Le lot n'ajoute ni texture, ni asset, ni moteur, ni voie de mutation spécifique à l'interface. Les
trois apparences utilisent encore le matériau procédural Forge déjà qualifié ; l'import ou le choix
d'assets PBR reste une capacité distincte.

## Vérifications automatisées figées

| Contrôle | Résultat |
|---|---|
| tests workspace, toutes cibles | **481 PASS**, 0 échec, 0 ignoré, 13 cibles |
| Clippy workspace natif strict | PASS |
| Clippy Studio WASM strict | PASS |
| build Trunk Release | PASS |
| paquet Windows autonome Tauri Release | PASS |
| format du binaire WebAssembly | valide, magie `\0asm` et commit complet embarqué |
| format du binaire Windows | valide, en-têtes `MZ` et `PE` |

Le total passe de 462 à 481 tests. Les nouveaux contrôles couvrent notamment : bornes et
normalisation du document, migrations, hash attendu, payloads invalides, inverse, convergence de la
brosse, trait continu, restauration procédurale, changements hauteur/matière combinés, absence de
reconstruction des colliders, couture de quatre chunks, IPC périmé ou invalide, dry-run, undo/redo,
sauvegarde et réouverture exacte.

Artefacts d'exécution locaux immuables du candidat :

| Fichier ignoré | Octets | SHA-256 |
|---|---:|---|
| `bin/forge_studio-1b70be6.wasm` | 40 253 102 | `615998535850a8cae5ae66f2df1812ffaedfcd92ed38839d3d62d831123aea0f` |
| `bin/forge-studio-1b70be6.exe` | 34 105 856 | `cf25f4f818b84f62445b647acd9d9d86b0e199e0f66669a0eb8745c294ff8113` |
| `snapshots/98271e45-19a8-562c-a19e-4100f270da33-r4.forge.json` | 359 477 | `6f83e7efa0ab1432f3c40232b4d7122c2ec7a62ec1bff6730e92499b18cab7f3` |

## Qualification native finale

Statut : **PASS** sur l'EXE Tauri autonome lié au commit `1b70be6`.

| Étape native | Révision | Résultat |
|---|---:|---|
| projet frais | r1 | 64 chunks recalculés, 0 conservé |
| trait `Peindre - Terre` | r2 | 1 409 points, 8 chunks recalculés, 56 conservés, bande de terre visible |
| undo | r3 | 8 chunks recalculés, 56 conservés, bande retirée |
| redo | r4 | 8 chunks recalculés, 56 conservés, bande restaurée |
| fermeture puis réouverture | r4 | fermeture propre, matériau visible, message `Projet rouvert depuis le snapshot vérifié`, chargement 64/0 |

Les snapshots distincts, non écrasés, sont conservés dans le répertoire ignoré des artefacts :

- `snapshots/98271e45-19a8-562c-a19e-4100f270da33-r2.forge.json` ;
- `snapshots/98271e45-19a8-562c-a19e-4100f270da33-r4.forge.json`.

Le validateur confirme leurs révisions 2 et 4, les 1 409 poids de matériau persistés et leur égalité
exacte après normalisation des seuls champs de révision attendus (`workspace.revision` et
`terrain.revision`) : le redo r4 restaure donc la matière du r2. Après la fermeture propre, le Studio
a rouvert exactement r4. L'état final et la bande de terre restaurée ont été observés via CUA, mais
la capture n'est pas versionnée. Cette observation ne constitue aucune mesure GPU ou de performance.

## Intégrité et reproduction

Le manifeste [TERRAIN_MATERIAL_PAINTING_ARTIFACTS.json](TERRAIN_MATERIAL_PAINTING_ARTIFACTS.json)
fixe cinq logs, deux binaires et le snapshot r4 par taille et SHA-256. Le build Cargo avec `devUrl`
est exclu de cet inventaire. Le validateur en lecture seule :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate2MaterialPaintingEvidence.ps1
```

Il vérifie le commit, le tree, le parent, l'inventaire du diff, chaque hash, les 481 résultats de
tests, les tests matériels indispensables, les fins de builds et les formats WASM/PE. Il valide aussi
la cohérence du reçu natif, les révisions et le contenu des snapshots r2/r4. Il ne relance ni Studio,
ni build, ni PresentMon et n'infère aucun verdict GPU ou de performance.

## Limites conservées

- La capture observée via CUA n'est pas versionnée ; les preuves durables sont les reçus structurés,
  snapshots et hashes.
- Aucun temps de boucle affiché ne serait une mesure de performance ; PresentMon reste volontairement
  hors portée.
- La peinture modifie des poids de matériaux, pas un catalogue de textures ou d'assets PBR.
- Les outils de végétation, biomes, splines, placements et playtest restent des routes Gate 2
  distinctes.
- La CI distante n'a exécuté aucun job et ne doit pas être décrite comme verte.
