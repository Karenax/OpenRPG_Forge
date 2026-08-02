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
