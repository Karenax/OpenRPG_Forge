# AV6-C — monde 512 × 512 sectorisé

Dernière mise à jour : 5 août 2026.

## Résultat

Forgotten Forest occupe maintenant une vraie emprise de 512 × 512 m. Le terrain canonique contient
129 × 129 échantillons, soit une résolution constante de 4 m, 16 641 vertices et 32 768 triangles.
Le `chunkSize` de 16 cellules produit 64 secteurs de 64 × 64 m ; ce découpage vient de la source de
vérité du terrain et ne crée ni scène ni format parallèle.

Le runtime et le Creator construisent un mesh indépendant par secteur. Chaque origine est locale au
secteur, chaque géométrie possède une distance de visibilité dérivée du rayon de chargement, et une
collision globale conserve la continuité physique et navigable. Les petites cartes gardent les
anciens plafonds de dispersion ; les surfaces de production disposent de plafonds MultiMesh adaptés.

## Composition

- cinq couches : clairières, canopée, sous-bois, fougères et roche humide ;
- 5 564 instances MultiMesh avec cinq groupes de culling ;
- 39 modèles d’entités, dont les cinq personnages KayKit animés ;
- quatre ensembles de landmarks extérieurs : bosquet ancien, tour nord, camp sud-ouest et sanctuaire
  englouti ;
- deux routes, un ruisseau de dix segments animés et deux ponts physiques/navigables ;
- relief multi-octave et corridor central adouci pour préserver le parcours de quête.

## Navigation et budgets

Le navmesh de surface contient une région, 32 454 polygones, 314 triangles bloqués par le ruisseau et
deux liens de pont. Le moteur trouve réellement un trajet camp–sanctuaire de 70 points avant de
valider la quête et le combat.

La mesure locale de référence réussit seize contrôles :

- reconstruction : environ 251 ms ;
- collision : environ 94 ms ;
- navigation : environ 64 ms ;
- sculpture transactionnelle : environ 7 ms ;
- mémoire de travail supplémentaire : environ 35 Mo ;
- 28 assets, 72 niveaux LOD et cinq groupes de culling.

Les budgets 512 × 512 sont distincts du profil historique 128 × 128 et restent bloquants dans le
smoke et `scripts/Verify.ps1`.

## Preuves

- export `.orpmod` : 100 fichiers, 28 manifestes, 50 chunks et 44 références gouvernées,
  validation sans diagnostic ;
- smoke complet : quête, combat, animations, navigation, eau et sortie réussis ;
- capture aérienne Vulkan Forward+ sans HUD :
  `artifacts/verification/forgotten-forest-av6c-world-forward-plus.png` ;
- marqueur : `OPENRPG_FORGE_AV6C_WORLD_VISUAL_PASS ... sectors=64 scatter=5564`.

## Limites explicites

AV6-C livre une composition stylisée fonctionnelle et inspectable, pas une finition artistique
définitive. Le terrain emploie encore des couleurs vertex simples ; feuillage, sol PBR, variations de
silhouettes, éclairage atmosphérique, eau avancée et habillage fin par secteur restent à améliorer.
Le retargeting inter-rigs demeure également un incrément séparé.
