# Backlog MVP — Crypte des Cendres

## Règles

- Exécuter dans l’ordre sauf dépendance clairement justifiée.
- Une tâche = un résultat testable.
- Ne pas ouvrir simultanément plusieurs grands chantiers.
- Chaque tâche doit préciser les tests.

## Lot 0 — Bootstrap

### T0.1 Vérifier l’environnement

- [x] Moteur 3D .NET compatible installé — version exacte dans `docs/ENVIRONMENT.md`.
- [x] SDK .NET compatible détecté — `10.0.302`.
- [x] `global.json` épingle une version présente — `10.0.302`.
- [x] Git disponible — `2.55.0.windows.2`.
- [x] CMake/compilateur natif notés mais non obligatoires au premier build — non détectés dans le `PATH`.
- [x] Blender noté mais non obligatoire — non détecté dans le `PATH`.

Acceptation : rapport de versions exact, sans installation automatique silencieuse. **Atteinte le 2 août 2026 après autorisation explicite.**

### T0.2 Créer la solution minimale

- [x] `Forge.Domain`
- [x] `Forge.Application`
- [x] `Forge.ModuleFormat`
- [x] `Forge.Domain.Tests`
- [x] solution
- [x] `Directory.Build.props`
- [x] nullable
- [x] warnings as errors pour projets propres
- [x] test trivial réel d’un value object

Acceptation : `dotnet test` vert.

### T0.3 Créer le projet Toolset

- [x] projet moteur 3D .NET.
- [x] référence aux bibliothèques.
- [x] addon C# activable.
- [x] main screen « Forge » minimal.
- [x] chargement sans erreur.

Acceptation : ouverture et capture/log de succès.

### T0.4 CI

- [x] restore.
- [x] build.
- [x] test.
- [x] validation format minimale.
- [x] cache raisonnable.

Le workflow principal exécute restore, format, build et tests .NET avec cache NuGet. Son exécution
distante attend le premier push ; la chaîne équivalente est verte localement.

## Outillage transversal

- [x] générateur local et déterministe du graphe d’architecture ;
- [x] requêtes bornées en formats texte, chemins, JSON et Mermaid ;
- [x] tests unitaires des relations, exclusions, déterminisme et fraîcheur ;
- [x] contrôle CI refusant des artefacts absents ou périmés.

## Lot 1 — Domaine monde

### T1.1 IDs et valeurs

- [x] IDs opaques.
- [x] Vector/Quaternion/Transform propres.
- [x] JSON converters.
- [x] equality.
- [x] tests.

### T1.2 Project/Module/Area

- [x] agrégats.
- [x] invariants.
- [x] create commands.
- [x] serializers.
- [x] round-trip tests.

### T1.3 Blueprint/EntityInstance

- [x] définition.
- [x] instance.
- [x] transform.
- [x] tags.
- [x] overrides.
- [x] validation de référence.

### T1.4 Révision et command bus

- [x] base revision.
- [x] command ID.
- [x] handler.
- [x] diagnostics.
- [x] patch.
- [x] inverse.
- [x] conflit de révision test.

## Lot 2 — Éditeur zone

### T2.1 Session projet

- [x] ouvrir dossier Forge.
- [x] manifest de travail.
- [x] chargement.
- [x] sauvegarde atomique.
- [x] diagnostics.

### T2.2 Reconstruction 3D

- [x] Area vers Node3D dans le runtime et l’aperçu Forge.
- [x] EntityInstance vers représentation dans le runtime et l’aperçu Forge.
- [x] conversion transform.
- [x] destruction/rebuild propre.

### T2.3 Placement

- [x] palette de blueprints.
- [x] placer.
- [x] déplacer.
- [x] tourner.
- [x] supprimer.
- [x] grille.
- [x] undo/redo via commands.

### T2.4 Kit Crypte

- [x] assets provisoires générés avec primitives internes.
- [x] sol.
- [x] mur.
- [x] porte.
- [x] pilier.
- [x] collision runtime sur sol, murs, porte et piliers.
- [x] conventions.

## Lot 3 — Runtime

### T3.1 Projet runtime

- [x] charge snapshot.
- [x] instancie area.
- [x] caméra 3/4.
- [x] joueur placeholder.
- [x] déplacement.
- [x] interaction.

### T3.2 Play from here

- [x] sélection spawn.
- [x] snapshot temporaire.
- [x] lancement runtime.
- [x] retour logs.

### T3.3 Headless

- [x] démarre sans rendu.
- [x] charge area.
- [x] exécute un scénario.
- [x] retourne code de sortie.

## Lot 4 — Dialogue et quête

### T4.1 Dialogue domain

- [x] nodes.
- [x] choices.
- [x] conditions.
- [x] actions.
- [x] validation.
- [x] parcours test.

### T4.2 Dialogue editor minimal

- [x] liste ou graphe simple.
- [x] ajout node.
- [x] ajout choice.
- [x] condition.
- [x] localisation.
- [x] preview.

### T4.3 Quest state machine

- [x] states.
- [x] transitions.
- [x] journal.
- [x] actions.
- [x] invalid transition test.

### T4.4 PNJ Crypte

- [x] dialogue.
- [x] condition objet/variable.
- [x] quête.
- [x] porte.

## Lot 5 — Combat data-driven

### T5.1 Stats minimales

- [x] health.
- [x] resource.
- [x] offense/defense simples.
- [x] faction.
- [x] team.

### T5.2 Ability

- [x] cost.
- [x] cooldown.
- [x] targeting.
- [x] effect.
- [x] visual cue séparé.

### T5.3 Combat

- [x] attack dans le prototype runtime.
- [x] damage dans le prototype runtime.
- [x] death dans le prototype runtime.
- [x] loot.
- [x] test simulation headless du prototype.

## Lot 6 — ForgeScript spike

### T6.1 Native build

- [x] AngelScript version épinglée.
- [x] licence/notices.
- [x] GDExtension compile.
- [x] C# wrapper.
- [x] smoke test.

### T6.2 Compiler

- [x] source.
- [x] diagnostics.
- [x] bytecode.
- [x] load.
- [x] version metadata.

### T6.3 API minimale

- [x] EntityRef.
- [x] health.
- [x] variable.
- [x] say.
- [x] cast.
- [x] spawn.
- [x] quest.
- [x] unlock.

### T6.4 Quotas

- [x] timeout.
- [x] recursion.
- [x] spawn cap.
- [x] failure diagnostic.
- [x] serveur continue.

### T6.5 Boss

- [x] phase 2.
- [x] deux esprits.
- [x] nova.
- [x] mort.
- [x] quête.
- [x] test.

## Lot 7 — Module

### T7.1 Manifest/schema

- [x] schema.
- [x] validator.
- [x] tests invalides.

### T7.2 Packager

- [x] dossier travail vers `.orpmod`.
- [x] hash.
- [x] atomic export.
- [x] round-trip.

### T7.3 Loader

- [x] validation archive.
- [x] safe paths.
- [x] cache.
- [x] domain load.
- [x] runtime.

### T7.4 Sécurité

- [x] path traversal fixture.
- [x] zip bomb ratio fixture.
- [x] DLL refusée.
- [x] duplicate IDs.
- [x] hash mismatch.

## Lot 8 — IA/MCP minimal

### T8.1 MCP Host read-only

- [x] SDK C# officiel.
- [x] stdio.
- [x] project summary.
- [x] current revision.
- [x] diagnostics.
- [x] tests.

### T8.2 Editor bridge

- [x] discovery.
- [x] loopback/pipes.
- [x] token session.
- [x] reconnect.
- [x] no external bind.

### T8.3 Dialogue plan

- [x] schema.
- [x] validation.
- [x] dry-run.
- [x] diff.
- [x] apply.
- [x] inverse.
- [x] audit.

### T8.4 Script workflow

- [x] get API.
- [x] compile.
- [x] diagnostics.
- [x] sandbox test.
- [x] bind plan.

### T8.5 Test agent

- [x] lancer scénario.
- [x] résultat structuré.
- [x] diagnostics.
- [x] artifact log.

## Lot 9 — Démonstration

- [x] zone finale.
- [x] PNJ prototype.
- [x] quête prototype.
- [x] boss prototype avec phase 2.
- [x] export.
- [x] runtime.
- [x] save/load.
- [x] IA dialogue.
- [x] undo.
- [x] headless vert.
- [x] rapport performance.
- [x] licences assets.
- [x] guide créateur minimal.

## Gate post-MVP — Creator Core G7

Le détail et les preuves des 717 capacités restent suivis dans
[`docs/AURORA_CAPABILITY_DELIVERY_PLAN.md`](docs/AURORA_CAPABILITY_DELIVERY_PLAN.md) et
`tracking/project/AURORA_PARITY_COVERAGE.csv`.

- [x] **G7-01 — second module générique** : création, export et démarrage headless sans contenu
  spécifique à la Crypte.
- [x] **G7-02 — coque Creator** : neuf workspaces, explorateur, sélection partagée et diagnostics
  navigables.
- [x] **G7-03 — catalogue Blueprints** : recherche, édition transactionnelle, dérivation depuis une
  instance et index des références.
- [x] **G7-04 — manipulation directe du Monde** : palette glissable, projection/snap, validation,
  fantôme vert/rouge, sélection, gizmo X/Z et undo/redo du document canonique.
- [ ] **G7-05 — zones indépendantes et transitions** : documents de zone, contexte actif,
  transitions et validation.

Les règles graphiques et d’interaction de G7-03/G7-04 sont consignées dans
[`docs/UX_AURORA_2026.md`](docs/UX_AURORA_2026.md).

## Backlog post-MVP — Asset Vault et Forgotten Forest

Ce chantier n’est pas inclus dans la fermeture du MVP Crypte des Cendres. Sa spécification de
référence est
[`docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md`](docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md).
Chaque phase doit être transformée en une ou plusieurs tâches bornées avant implémentation, sans
détourner une priorité P0/P1 Aurora déjà engagée.

### AV0 — Audit et intégration

- [x] Auditer le dépôt et produire `docs/audits/ASSET_AND_MAP_EXISTING_STATE.md`.
- [x] Identifier les composants de catalogue, import, terrain, navigation, gameplay et sauvegarde à
  réutiliser ou étendre.
- [x] Formaliser les critères d’acceptation, risques et dépendances des phases suivantes.

### AV1 — Politique, métadonnées et stockage

- [x] Formaliser la politique de licence du noyau CC0 et des packs optionnels.
- [ ] Versionner le schéma de métadonnées et les manifestes de packs.
- [ ] Implémenter le catalogue local, le stockage adressé par hash et les contrôles de licence.
- [ ] Couvrir filtres, hashes, extraction sûre, déduplication et crédits par des tests.

### AV2 — Acquisition et normalisation

- [x] Intégrer les trois premières sources autorisées : Poly Haven, ambientCG et packs Kenney CC0.
- [ ] Étendre acquisition, reprise et cache actuels avec mise à jour et désinstallation locale.
- [ ] Construire la normalisation GLB/Blender, les collisions, LOD, miniatures et audits qualité.
- [ ] Documenter les sources acceptées, refusées et celles exigeant une action manuelle.

### AV3 — Interface Asset Vault

- [x] Prototyper dans `forge_forest_map` la recherche du catalogue, la sélection d’un asset installé,
  sa copie dans `res://` et la conservation de `provenance.json`.
- [x] Ajouter une interface de peinture viewport pour forêt, clairière, fleurs, rochers et zones
  interdites avec Undo/Redo Godot.
- [ ] Ajouter recherche, filtres, catégories, styles, variantes, provenance et prévisualisations.
- [ ] Ajouter installation à la demande, glisser-déposer et intégration au placement existant.
- [ ] Préparer les brosses de dispersion et recettes de biome sans dupliquer les systèmes de scène.
- [ ] Faire passer toute mutation IA/MCP par les commandes transactionnelles et la validation de
  licence.

### AV4 — Terrain générique et carte Forgotten Forest

#### AV4A — Fondation de terrain générique multi-biome

- [ ] Formaliser dans un ADR le contrat de terrain partagé : plan horizontal X/Z, altitude Y,
  séparation stricte entre terrain, couches de biome, gameplay et entités placées.
- [ ] Ajouter au domaine C# versionné une définition de terrain associée à la zone : dimensions,
  découpage, résolution, seed et références contrôlées vers heightmaps, masques de matériaux et
  masque de trous.
- [ ] Étendre le format `.orpmod`, les migrations et les tests de round-trip afin que le terrain soit
  inspectable et indépendant de toute scène ou ressource Godot.
- [ ] Concevoir les commandes transactionnelles de sculpture et peinture : lever/abaisser, aplanir,
  lisser, peindre les matériaux et ouvrir/fermer un trou, avec prévisualisation et Undo/Redo.
- [ ] Évaluer Terrain3D face à un adapter interne, vérifier licence et compatibilité, puis épingler la
  solution retenue sans faire de ses nœuds la source de vérité.
- [ ] Reconstruire dans Godot la géométrie, les collisions et les requêtes d'altitude/pente depuis le
  document canonique, avec validation headless et génération déterministe.
- [ ] Partager les requêtes de surface avec placement, eau, chemins, navigation et biomes ; traiter
  une grotte, un tunnel ou un surplomb comme un trou de terrain accompagné d'une géométrie 3D
  séparée.
- [ ] Définir des recettes de biome réutilisables qui échantillonnent altitude, pente, matériau,
  humidité, eau, chemins et exclusions sans posséder ni dupliquer le terrain.
- [ ] Migrer le prototype `forge_forest_map` en première couche de biome posée sur ce terrain et
  supprimer son hypothèse de sol plan sans créer une seconde source de vérité.
- [ ] Mesurer mémoire, temps de reconstruction, sculpture, collisions, navigation, LOD et culling sur
  une zone extérieure représentative avant d'étendre la taille des cartes.

#### AV4B — Carte Forgotten Forest

- [x] Livrer une scène de création 128 × 128 m reconstructible, avec scatter cellulaire, masque RGBA
  versionné, kit de départ attribué et smoke test headless.
- [ ] Concevoir une carte extérieure cohérente à partir d’une sélection d’assets homogène.
- [ ] Intégrer la fondation de terrain AV4A, végétation, bâtiments, eau, éclairage, navigation et
  landmarks.
- [ ] Réutiliser les briques existantes de dialogue, quête, combat, transition et sauvegarde.
- [ ] Ajouter profils graphiques, validation automatisée, playtest et rapports de performance.
- [ ] Vérifier export du module, dépendances, crédits et ouverture depuis un clone propre.

### AV5 — Livraison et extension

- [ ] Livrer la documentation, le journal d’implémentation et les procédures d’ajout de sources/packs.
- [ ] Mesurer catalogue, téléchargements, contenu utilisé, taille, licences et performances réelles.
- [ ] N’élargir massivement le catalogue qu’après validation de la cohérence visuelle et du pipeline.
