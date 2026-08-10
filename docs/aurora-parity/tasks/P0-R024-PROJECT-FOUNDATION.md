# P0 R024 — fondation de projet et démarrage joueur

Lot stable : `R024` / `FC-PROJECT-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-PROJ-001` — Assistant de création de projet.
- `ORF-PROJ-002` — Métadonnées localisées du projet.
- `ORF-PROJ-003` — Identité technique stable.
- `ORF-PROJ-004` — Première zone obligatoire.
- `ORF-PROJ-005` — Point de départ du joueur.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/02_PROJECT.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Wizard template, kits, plateforme, règles, rendu, réseau, première zone et résumé avant création | Le dialogue crée titre, description, zone intérieure/extérieure, dimensions, sol et PlayerSpawn via `NewModuleHandler` | Aucun choix canonique de template/kits/plateforme/règles/rendu/réseau, pas de résumé de plan ni ressource MCP | Profil de fondation v1 validé, wizard multi-choix avec aperçu, même commande Domain/Application, lecture et dry-run MCP |
| Nom, résumé, description, auteur, version, licence, langues, image, tags, classification et crédits localisés | `ProjectMetadata`, `ProjectLocalizedMetadata`, écran Module, commande dry-run, historique et round-trip couvrent déjà ces champs | Pas de ressource MCP dédiée ; l’identité de package n’est pas présentée avec les métadonnées | Relecture sans régression, ressource filtrée, mutation dry-run, image sûre, diagnostics localisés, capture/round-trip |
| UUID projet immuable, slug, namespace package, révision et compatibilité format | UUID, slug, `ProjectRevision`, format workspace et namespaces de localisation/packages existent | Pas de contrat d’identité transversal réunissant/validant ces valeurs ni namespace auteur dans le wizard | Identité canonique v1, UUID/slug immuables, namespace/compatibilité validés, révision exposée, legacy sûr |
| RPG avec zone initiale obligatoire ; Core menu/simulation sans zone selon cible | Le handler crée toujours une zone et le validateur exige l’entrée résolue | Aucun type de projet ne permet de distinguer RPG, menu et simulation sans zone | Politique de cible explicite, RPG refusé sans zone, Core menu/simulation accepté sans zone, runtime optionnel/requis |
| SpawnProfile zone, waypoint, orientation, mode, classe d’entrée et règles par joueur | PlayerSpawn générique, ancres de spawn, playtest et runtime existent | Aucun profil projet regroupant ces champs et aucune sélection typée par joueur | Profils UUID v1 bornés, références zone/spawn/waypoint résolues, sélection stable, lecture ForgeScript/MCP, runtime sûr |
| Configuration, persistance, diagnostics, commande unique, undo/redo | Métadonnées et nombreuses fenêtres suivent déjà la commande transactionnelle | Les choix de fondation ne sont pas persistés ni modifiables par le même contrat | Commande preview/apply/undo/redo, sérialisation legacy/round-trip, diagnostics code/chemin/UUID, capture et smokes |

## Contrat minimal de fermeture

- profil canonique de fondation projet v1 avec UUID, schéma, provenance, template, kits, plateformes,
  règles, rendu, réseau, politique de zone, identité technique et profils de démarrage joueur ;
- validation déterministe des champs, UUID, namespaces, versions, références zone/spawn/waypoint et
  cohérence template/cible ;
- extension du wizard existant avec aperçu explicite avant choix du dossier et création uniquement via
  `NewModuleHandler` ;
- commande d’édition dry-run/apply et historique undo/redo pour le profil, sans mutation de fichier directe ;
- métadonnées existantes réauditées, exposées avec la fondation en MCP filtré et mutation dry-run ;
- sérialisation canonique et legacy sûr, runtime ignorant l’optionnel et diagnostiquant le requis,
  lecture typée ForgeScript sans accès fichier/nœud ;
- capture inspectée et marqueurs Toolset/runtime R024.

## Risques et tests exacts

- template inconnu, liste de kits vide/dupliquée, plateforme/règles/rendu/réseau invalides ;
- namespace invalide, UUID vide/dupliqué, compatibilité future, tentative de changement d’identité immuable ;
- RPG sans zone ou sans spawn, Core menu/simulation avec entrée incohérente, référence de zone absente ;
- spawn hors zone, blueprint non PlayerSpawn, waypoint inconnu, orientation hors bornes, règle joueur ambiguë ;
- wizard preview qui écrit, UI et commande divergentes, conflit de révision, undo/redo incomplet ;
- round-trip exact, document legacy sans profil, extension optionnelle absente et extension requise invalide ;
- MCP borné sans shell, lecture ForgeScript typée, capture 1440×980 et portail global.

## Non-objectifs

- éditeurs PROJECT des lots R025/R026, P1/P2, cloud, Git distant, AV7–AV10, commit ou push Git.

## Clôture R024

- `ORF-PROJ-001` à `ORF-PROJ-005` ont été relus puis promus individuellement à `Tested` ; aucune
  autre ligne P0, P1 ou P2 n’a été promue.
- Le profil `ProjectFoundationProfile` v1 couvre template, kits, plateformes, règles, rendu, réseau,
  politique de zone, identité technique immuable et départs joueur contextuels.
- `NewModuleHandler` accepte les projets Core sans zone et refuse le RPG sans zone ; le défaut de
  fixture révélé par le portail a été corrigé et le starter canonique régénéré.
- `SetProjectFoundationHandler`, `ProjectFoundationScriptService` et les outils MCP assurent
  preview/apply/undo/redo, lecture typée, persistance et absence d’accès shell arbitraire.
- La capture `docs/aurora-parity/evidence/r024-project-foundation-ui.png` (fenêtre 980×900 lancée
  dans un viewport 1440×980) a été inspectée : deux colonnes lisibles, résumé visible et aucun
  débordement bloquant ; les champs supplémentaires restent accessibles par le défilement vertical.
- Tests ciblés : 3 Domain, 9 Application, 2 ModuleFormat, 1 MCP et 1 starter canonique, tous verts.
- Smokes : `OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_PASS` et
  `OPENRPG_FORGE_RUNTIME_R024_PROJECT_FOUNDATION_PASS` présents.
- Tracker/backlog/docs régénérés ; graphe final : 5 867 nœuds, 35 395 relations, zéro diagnostic.
- Portail intégré : 27 projets, zéro avertissement/erreur, 1 213 tests .NET, 6 tests Python généraux,
  33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.
