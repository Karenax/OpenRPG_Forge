# ADR-0007 — Représentation végétation de Gate 1

Date : 2026-08-31
Statut : **accepté pour le prototype Gate 1**
Portée : branche `Rust`, Visual Valley, Bevy `0.19.1`, natif et WebGPU
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

Gate 1 demande un scatter déterministe, des arbres avec LOD/HLOD, une herbe dense sans entité par
brin, du vent shader, une interaction locale, du culling par chunks, des ombres bornées et des
métriques honnêtes. `PROJECT_CONTEXT.md` laisse la représentation GPU de la végétation ouverte et
interdit de la choisir implicitement.

Trois contraintes se croisent :

- les données canoniques doivent rester indépendantes de Bevy et reproductibles par chunk ;
- la même projection doit compiler en natif et en WebGPU sans seconde pile renderer ;
- Gate 1 doit produire rapidement une scène mesurable sans figer un renderer de monde définitif.

Bevy `0.19.1` fournit déjà le partage de handles mesh/matériau, le batching et l'instancing
automatiques des meshes compatibles, le preprocessing GPU lorsque le backend le supporte, le
frustum culling, `VisibilityRange` avec transition dithérée et les composants d'ombre. Il ne fournit
pas un compteur global de draw calls qualifié par ADR-0004. Un groupe logique ou une instance ne peut
donc pas être présenté comme un draw call réel.

## Décision

La végétation de Gate 1 utilise une représentation hybride fondée uniquement sur les chemins publics
Bevy existants.

### Source de vérité et génération

- `VegetationDocument` est un document Forge JSON strict et versionné, sans type Bevy, handle GPU ou
  asset Cadle.
- Le scatter utilise une grille entière et un mélange déterministe seed/cellule/couche/essai. Une
  génération globale et la concaténation ordonnée des générations par chunk doivent être identiques.
- Chaque placement ligneux ou fougère possède un identifiant stable, une position terrain, un yaw,
  une échelle et une phase de vent finis. L'herbe utilise les mêmes placements logiques, mais sa
  projection ne crée pas une entité par brin.
- Les meshes, colliders éventuels et buffers restent des caches dérivés et reconstructibles.

### Projection proche et moyenne

- Les arbres, arbustes et fougères réutilisent un petit ensemble de meshes procéduraux originaux Forge
  et les mêmes handles de matériaux par espèce/LOD.
- Une entité ECS représente un placement ligneux ou une fougère, mais plusieurs placements compatibles
  partagent exactement mesh et matériau afin que Bevy puisse les batcher et les instancier
  automatiquement.
- `MeshTag` transporte uniquement une phase/variation dérivée ; il ne devient pas une identité
  canonique.
- Aucun custom render phase, buffer raw wgpu, fork Bevy ou dépendance d'instancing parallèle n'est
  introduit pendant Gate 1.

### Herbe, LOD et HLOD

- Les brins d'herbe sont fusionnés en un mesh dérivé par chunk et niveau de qualité. Une entité
  représente un lot de milliers de brins, jamais un brin individuel.
- Les arbres proches et moyens utilisent des meshes distincts avec `VisibilityRange` et marges de
  transition identiques. Les distances proviennent du profil Forge et sont testées.
- Au-delà du LOD individuel, un mesh HLOD procédural regroupe les silhouettes d'un chunk. Il remplace
  les entités individuelles dans une plage complémentaire et finit par être cullé.
- Le frustum culling et le range culling standards restent actifs. Le preprocessing GPU de Bevy est
  utilisé lorsqu'il est disponible ; son fallback CPU/WebGPU reste accepté et visible dans les
  métriques de capacité.

### Vent, interaction et ombres

- Un `MaterialExtension` WGSL original Forge déforme les sommets selon leur hauteur locale, le temps
  global, la force profilée et une phase dérivée. Le tronc reste ancré ; l'amplitude est bornée.
- L'interaction joueur est un champ local borné publié par le contrôleur et consommé par la
  présentation. Elle courbe les lots herbe/fougères proches sans modifier le document source.
- Le LOD proche des arbres projette des ombres. Les LOD moyens, l'herbe et les HLOD désactivent les
  ombres selon les distances documentées afin d'éviter un coût lointain non borné.

### Métriques

Le runtime publie séparément :

- placements logiques générés par type ;
- entités de projection ;
- lots herbe et HLOD ;
- instances dans/hors plage selon la caméra de preuve ;
- triangles source par LOD et triangles logiquement sélectionnés ;
- état du culling/preprocessing demandé et résolu.

Ces nombres ne sont jamais renommés « draw calls ». `R1-037` reste ouvert jusqu'à une instrumentation
exhaustive ou une capture GPU externe reproductible conforme à ADR-0004.

## Alternatives évaluées

### A. Buffer d'instances et render phase raw wgpu dès Gate 1

Avantage : contrôle explicite des buffers, du culling et des indirect draws.

Rejet initial : duplique des fonctions déjà fournies par Bevy, accroît le risque natif/WebGPU et crée
une limite renderer avant qu'un profil n'ait démontré que le chemin standard est insuffisant.

### B. Une entité par brin d'herbe

Avantage : implémentation immédiate et interaction ECS simple.

Rejet : viole explicitement `AGENTS.md`, augmente le coût ECS/extraction et ne démontre pas une
représentation dense soutenable.

### C. Un unique mesh combiné pour toute la vallée

Avantage : très peu d'entités et de soumissions logiques.

Rejet : culling grossier, reconstruction partielle coûteuse, interaction locale et LOD difficiles,
et perte des identités stables par chunk.

### D. Assets d'arbres ou imposteurs texturés externes

Avantage : qualité visuelle potentiellement plus rapide.

Rejet pour cette tranche : aucune provenance ou licence supplémentaire n'est nécessaire pour prouver
la technique. Les silhouettes, couleurs et shaders de Gate 1 restent procéduraux et originaux Forge.
Aucun asset Cadle n'est admissible.

## Preuves obligatoires

La décision n'est qualifiée que si :

- le document passe round-trip, hash, version, champs inconnus, bornes et valeurs non finies ;
- deux replays et le changement de seed démontrent le déterminisme ;
- la génération par 64 chunks égale exactement la génération globale, IDs et ordre inclus ;
- les positions restent dans l'étendue, sur le terrain et sous les limites pente/matière ;
- l'adapter réutilise les handles attendus et ne crée aucune entité par brin d'herbe ;
- les plages LOD/HLOD sont complémentaires et testées aux marges ;
- le shader compile en natif et en WASM/WebGPU, conserve les bases ancrées et borne le déplacement ;
- un probe natif mesure placements, entités, lots, plages et stabilité des identités ;
- une capture 1920 × 1080 est inspectée, puis comparée à la capture terrain/joueur précédente ;
- aucune dépendance, texture, shader, nom, lore ou asset Cadle n'entre dans Forge.

Les tests logiques ne suffisent pas à cocher les critères visuels ou de performance finaux.

## Déclencheurs de réouverture

Cette décision doit être réévaluée si l'un des faits suivants est mesuré :

- le profil High ne tient pas le budget après préchauffage à cause de l'extraction ou du batching des
  entités individuelles ;
- le nombre de placements ligneux dépasse durablement la capacité ECS mesurée ;
- le backend WebGPU ne peut pas utiliser le chemin d'instancing/culling retenu ;
- le HLOD par chunk produit un popping ou une densité visuelle inacceptable ;
- l'éditeur Gate 2 exige des mises à jour partielles que les lots dérivés ne peuvent pas appliquer ;
- Bevy expose une API publique d'indirect instancing/culling plus adaptée et compatible aux cibles.

Une réouverture compare alors le chemin présent à un buffer d'instances spécialisé sur la même scène,
avec moyenne, p95, 1 % low, temps d'extraction, mémoire, taille WASM et complexité. Elle nécessite un
nouvel ADR avant toute extension raw wgpu.

## Conséquences

La tranche garde une seule pile graphique, partage les données natif/Studio, permet une preuve dense
et mesurable et conserve un chemin d'évolution si Bevy standard devient le goulot. Le coût assumé est
un nombre d'entités ligneuses plus élevé qu'un buffer spécialisé ; ce coût doit rester mesuré, jamais
masqué par une affirmation de draw calls ou d'instancing non observée.
