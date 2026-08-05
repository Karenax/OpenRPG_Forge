# G11-G — Rendu de référence et ciel

## Périmètre

- `ORF-EE-005` — rendu PBR ;
- `ORF-EE-006` — tone mapping et HDR ;
- `ORF-EE-007` — éclairage par pixel ;
- `ORF-EE-008` — nombre accru de lumières dynamiques ;
- `ORF-AREA-019` — skybox et ciel.

## Résultat livré

- `AreaRenderingProfile` canonique et indépendant du moteur : workflow metallic-roughness, instances
  de matériaux, éclairage par pixel, Forward+, qualité, AgX/ACES/Filmic/Linear, exposition, point
  blanc, ressource LUT et sortie HDR ;
- budgets versionnés par plateforme pour les lumières dynamiques, les ombres et l'atlas, avec seuil
  d'avertissement et diagnostics stables lorsque la scène approche ou dépasse son profil ;
- `SkyProfile` physique, procédural ou panorama avec chemin HDRI confiné, couleurs de zénith/horizon/
  sol, étoiles, nuages, lune, énergie et durée de transition ;
- adapter unique consommé par l'aperçu Creator et le runtime pour construire environnement, ciel,
  tone mapper, brouillard, soleil, atlas d'ombres et defaults PBR par pixel ;
- lancements interactifs `Run-Prototype.ps1` et `Open-Toolset.ps1` demandant explicitement Forward+ ;
  le profil mobile conserve son budget réduit et le backend de compatibilité déclaré par plateforme ;
- projets et smokes sans écran conservant explicitement le backend de compatibilité, car
  l'initialisation Vulkan/Forward+ du binaire 4.7.1 plante sans surface graphique sur le poste de
  validation ;
- surface **Environnement de zone** responsive et organisée en onglets Rendu & ciel, Brouillard et
  Audio ; toutes les modifications rejoignent la commande `SetAreaEnvironment` existante, son
  dry-run, son audit et son historique ;
- schéma d'environnement 2, format projet `0.6.9`, workspace `1.3.0` et zone `1.2.0`, avec migration
  déterministe des projets antérieurs qui reçoivent les profils sûrs par défaut.

## Preuves

- `AreaRenderingProfileTests` couvre les valeurs de référence et les invariants de budget/ciel ;
- `AreaRenderPlanBuilderTests` couvre la sélection de plateforme et les avertissements ou
  dépassements de lumières ;
- `ProjectJsonSerializerTests` et `ProjectSessionTests` couvrent round-trip et migration des formats
  projet, workspace et zone pré-rendu ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11G_RENDER_PASS` : profil ACES Ultra, HDR, PBR, Forward+,
  éclairage par pixel, ciel procédural, budgets 48/12, persistance et restauration d'historique ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_RENDER_PROFILE_PASS` : AgX, HDR, PBR, ciel procédural et
  budgets desktop appliqués à la Crypte ; `OPENRPG_FORGE_RUNTIME_RENDER_BACKEND` consigne la méthode
  et le driver réellement actifs pour distinguer validation headless et lancement Forward+.
- portail Windows : `toolset-forward-plus` et `runtime-forward-plus` exigent tous deux
  `method=forward_plus` avant de déclarer le lot vert ; le poste de référence consigne `driver=vulkan`.

## Limites explicites

- le champ LUT est gouverné et persisté mais son application GPU et la certification sur un écran
  HDR physique rejoignent les tests visuels et benchmarks de G11-S ;
- les matériaux procéduraux reçoivent les conventions PBR ; la curation reliera ensuite les canaux
  des GLB importés aux matériaux de production ;
- les étoiles, nuages, lune et transitions sont des paramètres canoniques ; leur animation avec le
  vent et la météo appartient à G11-H ;
- les budgets produisent des diagnostics déterministes, sans encore offrir l'overlay profiler GPU.

## Critère de passage

G11-G est fermé lorsque tests ciblés, smoke Toolset, smoke runtime, migration, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
