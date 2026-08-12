# P1 R066 — Simulation gameplay 3D

## Périmètre

Lot canonique `R066 / FC-GAMEPLAY-P1-01`, limité à cinq fonctions :

- `ORF-GAME-002` — vol libre ;
- `ORF-GAME-008` — projectiles ;
- `ORF-GAME-009` — physique 3D ;
- `ORF-GAME-014` — ciblage ;
- `ORF-GAME-017` — temps de monde.

Le lot ferme uniquement les limites P1 de ces fonctions. Il n’ouvre ni P2, ni AV7–AV10,
ni le lot R067.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/19_GAMEPLAY.md` avant modification.

| Critère source | État avant R066 | Limite réelle fermée par R066 |
|---|---|---|
| Vol libre | calcul isolé de vitesse, accélération, altitude, portance et inclinaison | profil canonique rattaché au module, navigation volumétrique, atterrissage, commande, UI et `CharacterBody3D` |
| Projectiles | calcul balistique/homing/hitscan avec collision et autorité | collision et prédiction reliées au profil persistant, VFX/impact, révision/audit, handle script et `Area3D` moteur |
| Physique 3D | calcul de corps statiques, cinématiques et rigides | scène persistante avec corps, triggers, liaisons, bornes, interactions personnage, pas fixe déterministe et politique réseau |
| Ciblage | huit modes et bornes de validité calculables | géométrie d’aperçu, tags, ligne de vue, input, couleur, UI graphique et `ImmediateMesh` moteur |
| Temps de monde | horloge, calendrier, échelles, pauses et schedules isolés | profil module, révision autoritaire, événements planifiés, domaines de pause, échelles locales et round-trip de sauvegarde |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R066 |
|---|---|
| Domaine canonique avant moteur | `GameplaySimulationProfile` agrège cinq profils indépendants de l’UI et du moteur |
| Identité et déterminisme | UUID, schéma, provenance, références de liaisons, pas fixe, révision et ordre des schedules sont validés |
| Validation structurée | codes et chemins stables pour identité, vol, projectiles, physique, références, ciblage, temps et contenu requis |
| Interface visible, vide et erreur | onglet `Simulation 3D P1`, cinq cartes, gouvernance, aperçu graphique et statut de rejet |
| UI et commande uniques | `SetGameplayControlProfileHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | handles générationnels distincts pour vol, projectile, physique, ciblage et temps ; autorité exigée pour les mutations |
| MCP filtré | recherche par texte/type et plan gameplay complet avec diff, apply et revert |
| Runtime moteur | `CharacterBody3D`, `Area3D`, corps physiques, liaisons, mesh de ciblage et adaptateur d’horloge |
| Optionnel/requis | absence optionnelle ignorée ; profil requis absent signalé par diagnostic stable |

## Validation exécutée

- compilation Debug des 27 projets : 0 avertissement, 0 erreur ;
- tests ciblés : Domain 3/3, Application 6/6, ModuleFormat 1/1, MCP 1/1 et Scripting 59/59 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R066_GAMEPLAY_SIMULATION_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R066_GAMEPLAY_SIMULATION_PASS` avec nœuds physiques réels ;
- capture `docs/aurora-parity/evidence/r066-gameplay-simulation-ui.png`, 1440×900, inspectée :
  onglet lisible, aperçu non rogné, cartes sans chevauchement et défilement vertical explicite.
- tracker, backlog, documentation et graphe Roslyn régénérés ; graphe frais à 6 680 nœuds et
  42 177 relations, sans diagnostic ;
- portail `scripts/Verify.ps1` vert : 27 projets sans vulnérabilité, avertissement ni erreur,
  1 469 tests .NET, 39 tests Python, smokes Toolset/runtime, Blender, assets, exports, cartes 3D,
  réseau, MCP et sauvegarde/reprise ;
- verdict terminal : `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les limites réelles des cinq fonctions sont fermées et le portail intégré est vert. Ces cinq
fonctions, et uniquement elles, passent à `Tested`. R067 reste non audité et non promu.
