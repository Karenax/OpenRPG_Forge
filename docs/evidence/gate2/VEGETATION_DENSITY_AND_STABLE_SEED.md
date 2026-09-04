# Gate 2 — densité de végétation et seed stable

Date : 4 septembre 2026. Branche : `Rust`.

## Verdict et frontière

**R2-015 et R2-018 sont `PASS` sur qualification fonctionnelle automatisée. Gate 2 atteint 15/43 et
reste ouverte.**

Le candidat est le commit `d8dd10c4f9ef2c2e270955ff23fc08a8d90ff24e`, arbre
`a12c29477efb9ab775ee7d709a7469da159fcf7a`, construit directement sur
`4a69a43f9641bbf89007b56266c39a36f56f8fc7`. Il ne modifie aucun manifeste Cargo, aucune
dépendance, aucun asset et aucun chemin Cadle.

La qualification interactive native n'est pas revendiquée : le paquet Tauri autonome a été
construit, lancé, observé réactif puis fermé, mais la surface CUA de cette session ne contrôle que les
navigateurs. À la demande du propriétaire de ne plus multiplier les vérifications d'interface, le
parcours UI peinture → undo → redo → sauvegarde → réouverture est noté pour une vérification humaine
groupée ultérieure. Il ne bloque pas ces deux routes de données et de génération ; la condition de
capture globale de Gate 2 reste, elle, ouverte.

La CI distante reste exactement **`NOT RUN — infrastructure billing`** sous ADR-0006. PresentMon est
**`NOT RUN`** : aucune mesure GPU, cadence ou performance n'est déduite de ce lot.

## Fonctions livrées

- Le workspace v3 porte, pour chaque zone, une recette Forge et un masque de densité clairsemé,
  versionné, quantifié sur 8 bits et lié aux mêmes projet, zone, révision, grille et ordre de couches.
  Une nouvelle zone démarre à densité nulle : aucun décor végétal n'est inventé avant peinture.
- Les workspaces v1/v2 migrent sans perdre terrain, IDs, révisions, noms ou seed. Les anciens IDs de
  matériau du rôle herbe sont conservés et la recette migrée s'y rattache au lieu de supposer
  `meadow_grass`.
- La brosse de densité est une commande pure et bornée : couche typée, rayon/intensité/cible finis,
  falloff radial, au plus 64 empreintes, patch canonique lié au hash source et dry-run sans mutation.
- Le Studio expose cette commande sur le picking et les traits existants. Une application est une
  seule révision et une seule entrée d'historique ; undo, redo, sauvegarde immuable et réouverture
  passent par le service canonique, sans reconstruction de JSON dans l'interface.
- La génération multiplie l'acceptation de la recette par la densité. Un masque absent, étranger,
  hors grille, hors budget ou désynchronisé de la recette échoue explicitement ; une densité pleine
  reste bit-identique à l'ancienne API.
- La seed reste dans la source de recette et dans l'entropie des tentatives. Un replay à seed égale
  reproduit chunks, positions et IDs ; une seed différente change le hash et les IDs. Amincir ou
  repeindre conserve l'identité et l'ordre des tentatives survivantes.
- Le cache compare les 64 chunks complets et conserve les handles CPU égaux. Une peinture locale de
  preuve change 1 chunk et en conserve 63. Le viewport envoie un résultat reconstructible complet,
  vérifie les trois hashes et groupe chaque chunk peuplé dans au plus une entité de rendu, sans une
  entité ECS par brin d'herbe.

## Vérifications reproductibles

| Contrôle | Résultat |
|---|---|
| `cargo test -j 1 --workspace --all-targets --locked --offline` | **519 PASS**, 0 échec, 0 ignoré, 13 cibles |
| Clippy workspace natif, `-D warnings` | PASS |
| Clippy Studio WASM, `-D warnings` | PASS |
| Trunk Release lié au commit exact | PASS |
| Tauri Release autonome | PASS |
| lancement du paquet Windows | démarré, réactif, puis fermé |
| interaction UI native | **NOT RUN — reportée** |

La première tentative globale parallèle a rencontré `LNK1318` sur deux PDB Windows et a laissé un
éditeur de liens orphelin. Les processus issus de cette tentative ont été identifiés puis arrêtés ; la
commande reproductible ci-dessus a été relancée avec un seul job et est entièrement verte. Seul son
log final vert appartient au manifeste.

Les 519 tests incluent les contrats indispensables : forme sparse/canonique, limite JSON, mismatch de
couches, migration d'ID de sol historique, brosse pure et bornée, dry-run égal au candidat appliqué,
rejets périmés sans mutation, undo/redo/réouverture, densité locale 1/63, replay exact à seed égale,
divergence à seed différente, conservation des survivants, hash densité transmis au renderer et
batching par chunk.

Le manifeste
[VEGETATION_DENSITY_AND_STABLE_SEED_ARTIFACTS.json](VEGETATION_DENSITY_AND_STABLE_SEED_ARTIFACTS.json)
fixe les cinq logs, le reçu de lancement et les deux binaires par taille et SHA-256. Le validateur en
lecture seule :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate2VegetationDensityEvidence.ps1
```

Il vérifie le commit, l'arbre, le parent, l'inventaire Git, les hashes, les 519 résultats, les tests
fonctionnels requis, les builds et les formats WASM/PE. Il ne relance ni build, ni application, ni
PresentMon et n'invente aucun verdict visuel ou GPU.

## Routes laissées ouvertes

- `R2-016` : les quatre couches Forge fixes ne forment pas encore un catalogue d'espèces éditable.
- `R2-017` : le moteur sait appliquer les exclusions existantes, mais aucun outil d'édition
  d'exclusions n'est qualifié.
- `R2-019` : la projection est rafraîchie après la commande ; aucun aperçu continu avant le
  relâchement n'est revendiqué.
- `R2-020` : aucune promotion, cuisson ou retouche manuelle d'instances n'est livrée.
- La densité localisée améliore la capacité à créer des zones naturelles, mais ce lot ne qualifie ni
  direction artistique, ni texture de sol, ni revue humaine de naturel.
