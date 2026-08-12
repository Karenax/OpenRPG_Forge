# P1 R065 — Orientation, locators et diagnostic de routes

## Périmètre

Lot canonique `R065 / FC-WAYPOINT-P1-02`, limité à quatre fonctions :

- `ORF-WPT-009` — orientation au waypoint ;
- `ORF-WPT-014` — ancres audio/VFX ;
- `ORF-WPT-015` — groupes et requêtes ;
- `ORF-WPT-016` — visualisation des routes.

Le lot prolonge le profil waypoint et les patrouilles validés en R064. Il n’ouvre ni fonction P2,
ni AV7–AV10, ni gameplay du lot R066.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/14_WAYPOINT.md` avant modification.

| Critère source | État avant R065 | Limite réelle fermée par R065 |
|---|---|---|
| Orientation à l’arrivée | direction, cible, animation, attente, interaction et callback existaient dans une valeur isolée | comportement rattaché aux waypoints/locators, validé, persisté et exécuté seulement par l’autorité runtime |
| Ancres génériques | `GenericLocator` exposait position, but et tags sans parcours auteur complet | locators audio, VFX, spawn, objectif ou gameplay avec AssetId, BlueprintId, overrides, gizmo et nœuds moteur spécialisés |
| Groupes et requêtes | filtrage texte/tag/but et trois algorithmes existaient dans un service isolé | collections UUID persistées, membres explicites, aperçu sans écriture, ordre stable, nearest et pseudo-aléatoire déterministe |
| Diagnostic des routes | segments, direction, distance, coût, timing et liens cassés étaient calculables | couches graphiques, flèches, actions, projection navigation, libellés lisibles et overlay moteur reliés au document canonique |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R065 |
|---|---|
| Domaine canonique avant moteur | `WaypointAuthoringProfile`, `ArrivalBehavior`, `GenericLocator`, `AnchorCollection` et `RouteVisualizationProfile` ne dépendent ni de l’UI ni du moteur |
| Identité et déterminisme | UUID, schéma, provenance, ordre des membres, listes de tags ordonnées, seed et références typées sont validés |
| Validation structurée | diagnostics stables pour arrivée, locator, collection et profil de visualisation, y compris contenu requis au runtime |
| Interface visible, vide et erreur | onglet `Locators & diagnostic`, trois cartes, aperçu graphique, statut et rejet explicite |
| UI et commande uniques | `SetAreaNavigationHandler`, dry-run/apply/reject, undo/redo et round-trip `ProjectSession` |
| ForgeScript typé | handles sûrs `LocatorRef` et `AnchorCollectionRef`, requête et résolution bornées |
| MCP filtré | lecture par zone, UUID, texte, but ou tag avec segments diagnostiqués et plan sans écriture |
| Runtime moteur | arrivée autoritaire, `AudioStreamPlayer3D`, marqueurs VFX/objectifs et overlay de route |
| Optionnel/requis | absence optionnelle ignorée ; collection ou visualisation requise vide signalée par diagnostic stable |

## Validation exécutée

- tests ciblés : Domain 3/3, Application 4/4, ModuleFormat 1/1 et MCP 1/1 ;
- contrat MCP stdio : 1/1 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R065_WAYPOINT_LOCATOR_PASS`, R064 et restauration T32 ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R065_WAYPOINT_LOCATOR_PASS`, avec nœuds audio/VFX et overlay ;
- capture `docs/aurora-parity/evidence/r065-waypoint-locator-ui.png`, 1440×900, inspectée après
  correction du rognage, du pas numérique et du chevauchement des libellés ;
- tracker, backlog, documentation et graphe Roslyn régénérés avant le portail intégré ;
- portail `scripts/Verify.ps1` vert : 27 projets sans vulnérabilité, avertissement ni erreur,
  1 461 tests .NET, 39 tests Python, smokes Toolset/runtime, Blender, assets, exports, cartes 3D,
  réseau, sauvegarde/reprise et graphe Roslyn frais à 6 648 nœuds / 41 900 relations ;
- verdict terminal : `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les limites `Partial` d’intégration auteur, commande, persistance, script, MCP, runtime et preuve
visuelle sont fermées dans ce seul lot. Les quatre fonctions passent à `Tested` avec les preuves
R065 ci-dessus et le portail intégré vert ; aucune autre ligne P1 n’est promue.
