# ADR-0054 — Expérience runtime détenue par les modules

Date : 28 août 2026
Statut : accepté
UUID : `e75e06e7-e959-4cd9-91f9-165a23307b27`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

La tranche verticale Crypte des Cendres avait introduit des fabriques, branches de slug, résolveurs de
personnages et une composition de donjon directement dans Application, Runtime et Studio. Le contenu
était jouable, mais le moteur générique restait propriétaire d'éléments qui appartenaient au module.
Parallèlement, `GameRoot`, ses smokes, `StudioSessionController.Ux1E` et le plugin Toolset historique
avaient atteint des tailles qui rendaient toute évolution transversale risquée.

## Décision

- La migration ponctuelle de la Crypte reste dans `Forge.WorkspaceMigrator`; aucune fabrique de
  contenu Crypte ne demeure dans Application.
- Le catalogue `WorldFeatures`, le profil post-lancement, les assets visuels et les profils
  d'animation sont matérialisés dans le workspace canonique.
- Les tags d'environnement sélectionnent caméra, règle d'achèvement, gameplay systémique, atmosphère
  et kit artistique sans comparer le slug du module.
- Les événements de phase de boss sont sélectionnés par l'override de profil
  `runtime.phase_two.custom_event`; le runtime ne connaît aucun nom d'événement propre à la Crypte.
- Le smoke de mouvement accepte le contrat générique `--module-movement-smoke-test` et conserve
  l'ancien alias uniquement pour la compatibilité des preuves existantes. Le script R055 est découvert
  depuis les propriétés du contenu ouvert, sans branche sur le slug.
- Un kit artistique est décrit par `art-kits/<id>.layout.json`. Le parseur confiné et le constructeur
  `GodotArtKitBuilder` sont génériques ; Runtime et Studio consomment la même disposition.
- Le manifeste systémique est lu depuis la racine de contenu réellement ouverte, y compris après
  extraction d'un `.orpmod`, et son `moduleSlug` doit correspondre au module actif.
- La migration de contenu matérialise aussi les dépendances réelles de la rencontre de référence :
  faction active `ash-watch`, ancre de spawn, route de patrouille de zone et ForgeScripts `.forge`.
  Une route véhicule ou un identifiant de script sans fichier ne sont pas acceptés comme substituts.
- Le smoke Studio de validation de rencontre copie dans sa session isolée uniquement les scripts
  réellement présents dans le workspace source, en plus du script d'activation authoré par le test.
  Il ne masque donc plus une référence cassée en fabriquant silencieusement sa dépendance.
- Toutes les références ForgeScript matérialisées par la migration portent l'extension `.forge` et
  résolvent un fichier réel. Les preuves Studio Marchand et Compagnon réutilisent le même copieur
  confiné de scripts du workspace au lieu de perdre leurs dépendances dans la session isolée.
- Les budgets de performance ForgeScript de la Crypte sont qualifiés avec la tranche réelle de 27
  scripts : le stress reste sous les plafonds bloquants serveur/desktop et conserve ses avertissements
  explicites. La migration réapplique ces budgets authorés aux workspaces historiques.
- Le Toolset qualifie le nombre réel de ForgeScripts au lieu d'imposer un inventaire historique de
  deux fichiers. Ses preuves de navigation ajoutent liens et ancres sans remplacer le catalogue,
  les waypoints ou les destinations de portail déjà authorés.
- L'édition d'un marchand remplace seulement la devise, le compte, le service, le vendeur et la ligne
  de stock sélectionnés. Les autres variantes, devises, taux, scripts et fournisseurs restent ordonnés
  et sont conservés dans le round-trip.
- Un snapshot de playtest copie tout le contenu module gouverné, y compris les dispositions
  `art-kits`, et exclut les caches, VCS et artefacts de build. Le Runtime compare le routage script
  résolu au contrat déclaré par le module, sans imposer un nom de fichier propre à la Crypte.
- `GameRoot`, ses smokes, le contrôleur Studio de placeables et `ForgeEditorPlugin` sont répartis en
  partials nommés par domaine. Les budgets automatiques bornent chaque famille à 2 000 ou 2 200 lignes.

## Preuves

- Tests : `tests/Forge.EngineAdapter.Tests/Rendering/GodotArtKitLayoutJsonTests.cs`
- Tests : `tests/test_crypt_systemic_gameplay.py`, `tests/test_crypt_dungeon_rebuild.py`
- Suite : `tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj` (64 tests)
- Build : Runtime Debug, Studio Release et Toolset Release, zéro warning/erreur
- Godot 4.7.1 : `OPENRPG_FORGE_RUNTIME_ART_KIT_PASS` avec 340 modèles et 7 zones
- Godot 4.7.1 : parcours critique, caméra, personnages animés et mouvement au sol validés
- Godot 4.7.1 : événement de phase piloté par le module et script R055 `source=module` validés
- Tests : `CryptOfAshesSampleTests` vérifie faction, route de patrouille, scripts et résolveurs
  lifecycle/tactique (4/4 ciblés)
- Studio : `OPENRPG_FORGE_STUDIO_UX2_LW_L_PASS` puis `OPENRPG_FORGE_STUDIO_SHELL_PASS pages=37`
- Studio : `OPENRPG_FORGE_STUDIO_UX4T_PASS` et `OPENRPG_FORGE_STUDIO_UX4U_PASS`
- CLI qualité : 27/27 ForgeScripts compilés, exécutés et couverts
- CLI performance : 9 benchmarks, 9 scénarios réseau, 8 cas visuels, 0 budget bloquant et 5
  avertissements ; `CryptOfAshesSampleTests` protège cette qualification.
- Studio/Godot 4.7.1 : `OPENRPG_FORGE_STUDIO_UX7_H_PASS` avec 8 cas et 25 artefacts visuels confinés.
- Toolset/Godot 4.7.1 : 230 marqueurs de réussite, aucun échec ; G712 inventorie 27 scripts,
  G714/R009 préservent navigation et ancres, R091 conserve les deux stocks et T32 termine avec
  `snapshot=deleted`.
- Tests : `PlaytestSnapshotStoreTests` (7/7 ciblés) puis `Forge.ModuleFormat.Tests` (458/458).
- Runtime/Godot 4.7.1 : `OPENRPG_FORGE_RUNTIME_G13AA_SCRIPT_CAMERA_ROUTE_PASS` et
  `OPENRPG_FORGE_RUNTIME_SMOKE_PASS` depuis le contenu canonique.
- Gate : `scripts/Test-MaintainabilityBudgets.ps1`

## Conséquences

La Crypte reste un module de référence jouable, mais elle n'est plus une branche fonctionnelle du
moteur. Un autre module peut fournir ses propres tags, disposition artistique et manifeste systémique
en respectant les mêmes contrats. Une disposition invalide ou un chemin de modèle non confiné est
refusé avant création des nœuds Godot.

Les partials réduisent le risque de conflit et rendent les responsabilités visibles sans changer le
cycle de vie Godot. Ils ne constituent pas à eux seuls une extraction en services ; les futurs lots
doivent privilégier des composants autonomes lorsque l'état partagé peut être réduit.

Les migrations de blueprint utilisent désormais `WithCreatureEvents`, qui préserve les extensions
ajoutées après la création du constructeur historique (fondations créature/item et métadonnées auteur).
Les migrations de navigation remplacent leurs éléments par identifiants stables et préservent les
waypoints, routes et ancres voisins ; elles restent donc idempotentes lors d'une reconstruction.

## Remplacement

Cette décision remplace les conventions implicites qui associaient la reconstruction de la Crypte à
des classes spécialisées du moteur. Elle ne remplace aucun ADR numéroté.
