# G11-M — Progression, difficulté, perception et règles de zone

## Périmètre

- `ORF-PROJ-017` — progression data-driven par ruleset, courbe, groupe et source ;
- `ORF-PROJ-023` — profils de difficulté couvrant combat, IA, économie, repos, sauvegarde et accessibilité ;
- `ORF-GAME-024` — sélection de difficulté et mutateurs composables ;
- `ORF-AREA-025` — modificateurs de perception par sens, ruleset et condition de milieu ;
- `ORF-AREA-027` — PvP de zone, duel, tir allié, crime, aggro et autorité serveur.

## Résultat livré

- agrégats canoniques `ModuleRulesProfile` et `AreaRulesProfile` v1 avec UUID, schéma, provenance,
  valeurs par défaut génériques et validation d’identité ;
- progression résolue depuis une courbe et des multiplicateurs de source, groupe, difficulté et
  mutateur, sans règle de jeu codée en dur ;
- difficulté composable couvrant dégâts sortants/entrants, intensité IA, économie, récupération,
  restrictions de sauvegarde, tir allié et accessibilité ;
- perception environnementale appliquée aux sens des créatures du RPG Kit par condition active ;
- autorité réseau de combat par zone pour PvP désactivé, duels, factions hostiles ou mode ouvert,
  avec règles de crime, aggro et dégâts environnementaux ;
- commande atomique module + zone avec dry-run, diff, inverse, audit, undo et redo ;
- fenêtre **Progression, difficulté & règles** accessible depuis Zones, avec édition des sources,
  mutateurs, sens et politiques de combat ;
- API MCP gouvernée en lecture, plan, application persistée et réversion ;
- handles ForgeScript typés donnant des instantanés copiés en lecture seule ;
- runtime joueur consommant difficulté, dégâts, progression, perception et PvP, avec HUD et
  sauvegarde `.orpsave` 1.2 de l’expérience et du niveau ;
- formats zone `1.7.0`, projet `0.6.15` et workspace `1.9.0`, avec migration déterministe des trois
  workspaces fournis.

## Preuves

- `RulesProfileTests` couvre identités, validation, difficultés et mutateurs ;
- `RulesRuntimeServiceTests` couvre progression, composition et perception ;
- `RulesScriptViewServiceTests` couvre les handles et l’absence de mutation par référence ;
- `EnvironmentPerceptionServiceTests` couvre l’intégration réelle du RPG Kit ;
- `SetRulesProfilesHandlerTests` couvre dry-run, transaction, undo et redo ;
- `RulesProfilePersistenceTests` couvre migrations et round-trip JSON ;
- `WorldAuthorityTests` couvre la décision de combat serveur par zone ;
- `ForgeProjectAgentServiceTests` couvre lecture, plan MCP, persistance et réversion ;
- `RuntimeSaveStoreTests` couvre le snapshot de progression 1.2 ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11M_RULES_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11M_RULES_PASS` sur la Crypte réelle.

## Limites explicites

- le profil générique fourni sert de démonstration ; un créateur doit pouvoir définir ses propres
  courbes, sources et difficultés sans modifier le code ;
- la fenêtre édite une sélection bornée de paramètres fréquents. Les collections complexes pourront
  recevoir ultérieurement une grille dédiée sans changer le contrat ;
- la politique PvP est appliquée par le service d’autorité Network Beta ; une session multijoueur
  distribuée reste nécessaire pour éprouver latence, reconnexion et UX de consentement au duel ;
- les modificateurs de perception affectent le calcul RPG Kit, mais les effets visuels de brouillard
  ou d’obscurité restent produits par les profils de climat et de rendu ;
- ce lot ne remplace pas la composition artistique finale de la carte 3D.

## Critère de passage

G11-M est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
