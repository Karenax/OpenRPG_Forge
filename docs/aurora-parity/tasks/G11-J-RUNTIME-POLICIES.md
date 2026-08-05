# G11-J — Rythme, repos et abandon contrôlé

## Périmètre

- `ORF-PROJ-012` — événements de repos ;
- `ORF-PROJ-013` — scheduler global ;
- `ORF-AREA-023` — heartbeat local de zone ;
- `ORF-AREA-026` — interdiction conditionnelle du repos ;
- `ORF-PROJ-015` — abandon sûr d'une séquence.

## Résultat livré

- profils canoniques versionnés au niveau module et zone pour les timers globaux et locaux, avec
  cadence, budget, suspension hors zone active, API ForgeScript, handler et provenance ;
- scheduler déterministe qui borne le rattrapage à quatre appels, conserve le budget de chaque
  dispatch et produit des diagnostics stables pour cadence agressive et backlog tronqué ;
- cycle de repos serveur `requested`, `started`, `interrupted`, `completed`, règles individuelle ou
  de groupe, durée, sécurité, condition, cooldown et volumes prioritaires avec motif explicable ;
- coordinateur d'abandon qui restaure caméra, pile de contrôle, invulnérabilité temporaire, verrous
  et transaction narrative depuis l'instantané d'entrée, avec budget maximal de rollback ;
- fenêtre **Rythme, repos & séquences** accessible depuis Zones, avec trois onglets et une seule
  commande combinée dry-run/appliquer, audit, undo et redo ;
- runtime qui consomme les profils, avance les timers, expose leurs handlers et permet au joueur de
  demander un repos avec `R` lorsque la politique l'autorise ;
- ressource MCP en lecture seule et plan de mutation gouverné avec diff, inverse, application,
  persistance, audit et réversion ;
- formats zone `1.5.0`, projet `0.6.12` et workspace `1.6.0`, avec migrations testées et les trois
  workspaces fournis réellement migrés.

## Preuves

- `RuntimePolicyProfileTests` couvre défauts sûrs, identités, doublons, limites et bindings requis ;
- `RuntimePolicyServicesTests` couvre budgets, suspension, backlog, cycle de repos, volumes,
  sécurité, cooldown et restauration complète d'une séquence ;
- `SetRuntimePoliciesHandlerTests` couvre commandes unitaires et transaction combinée annulable ;
- `AreaJsonSerializerTests`, `ProjectJsonSerializerTests` et `ProjectSessionTests` couvrent les
  round-trips et migrations des trois formats ;
- `ForgeProjectAgentServiceTests` couvre lecture MCP, dry-run, diff/inverse, application persistée,
  audit et réversion ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_PASS` : timers globaux/locaux, budgets,
  suspension, quatre événements de repos, condition, volume, cooldown, abandon complet,
  persistance et historique restauré ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11J_POLICY_PASS` : lecture des profils canoniques et
  initialisation des services dans le vrai processus joueur.

## Limites explicites

- les dispatches identifient le handler ForgeScript et les événements de repos résolvent leur
  binding versionné. L'invocation de scripts de module depuis ces hooks reste à relier au cache
  compilé ; aucune exécution silencieuse n'est revendiquée ;
- l'abandon est entièrement déterministe et testé sur un état de séquence, mais le lecteur de
  cutscene, les pistes et les ancres caméra appartiennent à G11-K ;
- l'éditeur propose un timer global, un timer local et un volume dangereux centré. Les profils
  canoniques acceptent plusieurs timers et volumes ; leur éditeur de collection avancé reste une
  amélioration d'ergonomie ;
- l'évaluateur runtime de condition accepte actuellement l'égalité simple avec une variable du
  blackboard. Le moteur d'expressions data-driven complet relève de G11-O ;
- les timers produisent des dispatches bornés et inspectables ; le branchement de chaque handler à
  une fonction ForgeScript ne doit jamais contourner les quotas du sandbox.

## Critère de passage

G11-J est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
