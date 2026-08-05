# G11-H — Climat, eau et réverbération runtime

## Périmètre

- `ORF-AREA-017` — vent ;
- `ORF-AREA-018` — météo ;
- `ORF-AREA-020` — eau ;
- `ORF-GAME-018` — météo runtime ;
- `ORF-AUD-015` — réverbération.

## Résultat livré

- profils canoniques versionnés pour vent global/local, états météo, plans/rivières/volumes d'eau
  et zones de réverbération, sans type moteur dans le domaine ou l'application ;
- machine météo déterministe avec autorité serveur, révisions, transition, cue audio, effet gameplay,
  persistance et intention de réplication ;
- adapter partagé par le viewport Creator et le runtime : shader globals de vent, pluie/neige
  procédurales, surfaces et volumes d'eau, sélection spatiale du preset de réverbération et bus audio
  natif ;
- réévaluation runtime des zones locales pendant le déplacement du joueur ;
- onglet **Climat & eau** dans **Environnement de zone**, appliqué par la commande transactionnelle
  existante avec dry-run, audit, undo et redo ;
- snapshot météo réellement écrit et restauré dans `.orpsave` 1.1.0 ;
- schéma d'environnement 3, format projet `0.6.10`, workspace `1.4.0` et zone `1.3.0`, avec migrations
  testées depuis les formats pré-climat et commande CLI `workspace migrate` ;
- réponses impulsionnelles optionnelles refusées sans chemin sûr, licence SPDX, provenance et SHA-256.

## Preuves

- `AreaClimateProfileTests` vérifie valeurs par défaut, UUID, ressources confinées et gouvernance des
  impulsions ;
- `WeatherRuntimeStateMachineTests` vérifie autorité, déterminisme, révisions et snapshot portable ;
- `AreaClimatePlanBuilderTests` vérifie zones locales et budget d'eau mobile ;
- `SetAreaEnvironmentHandlerTests`, `ProjectJsonSerializerTests` et `ProjectSessionTests` vérifient
  commande, historique, round-trip et migrations ;
- `RuntimeSaveStoreTests` vérifie la persistance du snapshot météo dans `.orpsave` ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_PASS` : zone de vent, pluie GPU, surface et
  volume d'eau, réverbération cave, autorité, persistance et restauration d'historique ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11H_ENVIRONMENT_PASS` : workspace 1.4 chargé et plan climat
  appliqué ; le portail global rejoue aussi création/reprise de sauvegarde 1.1.

## Limites explicites

- l'eau visible est une référence PBR fonctionnelle ; shader animé, rivages, splines de rivière,
  reflets et composition artistique rejoignent G11-I et les benchmarks visuels ;
- profondeur, flottabilité et coût de navigation sont exposés sur le volume moteur, mais les systèmes
  de nage, physique et pathfinding ne les consomment pas encore intégralement ;
- les shader globals de vent sont actifs ; les matériaux de végétation curatés devront les consommer ;
- humidité, foudre, cue audio et effet gameplay sont canoniques et signalés par les transitions ; leur
  dispatch vers ForgeScript, surfaces et mixage adaptatif reste à relier ;
- la météo est sauvegardée. L'intention de réplication et la révision serveur sont portables, mais le
  transport Network Beta du snapshot n'est pas encore branché ;
- le runtime applique une réverbération native par preset. Les impulsions sont gouvernées, sans
  convolution ni sends par source dans ce lot ;
- l'interface compacte édite le profil global et le premier plan/volume ; les collections avancées
  de zones restent accessibles au modèle canonique et recevront un éditeur de liste dédié.

## Critère de passage

G11-H est fermé lorsque tests ciblés, smokes Toolset/runtime, migration, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
