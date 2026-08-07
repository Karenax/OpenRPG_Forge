# Backlog MVP — Crypte des Cendres

## Lot G13 — Extensions post-1.0

- [x] G13-A à G13-AB : finition auteur, profondeur RPG et Monde vivant.
- [x] G13-AC à G13-AJ : mobilité, contenu, Live Director, réseau et plateforme Creator.
- [x] G13-AK à G13-AL : RPG/distribution/observabilité, génération média et soak tests.

Acceptation : 135/135 capacités G13 référencées, format migrable, Studio sans JSON, MCP borné,
smokes Toolset/runtime et portail intégré verts. Aucun lot suivant n'est ouvert automatiquement.

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
- [x] Blender noté mais non obligatoire au runtime — LTS `4.5.10` installé et détecté par Forge.

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
- [x] lancement interactif sans argument sur workspace scindé `1.0.0`, avec diagnostic visible en
  cas d'échec.

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
  spécifique à la Crypte ; l’assistant expose maintenant le choix Intérieur/Extérieur et les
  dimensions, avec terrain canonique activé dès la création d’un extérieur.
- [x] **G7-02 — coque Creator** : neuf workspaces, explorateur, sélection partagée et diagnostics
  navigables.
- [x] **G7-03 — catalogue Blueprints** : recherche, édition transactionnelle, dérivation depuis une
  instance et index des références.
- [x] **G7-04 — manipulation directe du Monde** : palette glissable, projection/snap, validation,
  fantôme vert/rouge, sélection, gizmo X/Z et undo/redo du document canonique.
- [x] **G7-05 — zones indépendantes et transitions** : documents de zone, contexte actif,
  transitions et validation.
- [x] **G7-06 — duplication et clipboard sémantique** : nouveaux UUID, remapping des références,
  aperçu puis application transactionnelle pour entités et zones.
- [x] **G7-07 — recherche et refactoring sûrs** : recherche globale, références inverses et renommage
  transactionnel avec aperçu, diagnostics et undo/redo.
- [x] **G7-08 — métadonnées et variables du module** : informations de publication localisées,
  blackboard global typé, persistance et édition dans le Creator.
- [x] **G7-09 — palettes Standard et Custom** : packs installés, contenu projet, généré, favoris et
  temporaire avec provenance et filtres cohérents.
- [x] **G7-10 — navigation caméra d’auteur** : orbite, panoramique, zoom, focus, vues cardinales et
  vitesse réglable sans mutation du projet canonique.
- [x] **G7-11 — événements de cycle de vie Core** : ProjectLoaded, WorldStarted, AreaEntered et
  AreaExited ordonnés, typés et observables sans dépendance au RPG Kit.
- [x] **G7-12 — build incrémental** : cache ForgeScript, graphe de ressources, impact transitif et
  analyse Aurora avant export.
- [x] **G7-13 — profils et distribution** : cinq cibles, registre tiers, validation de licence,
  NOTICE et export ciblé.
- [x] **G7-14 — navigation et validation spatiale** : profils d’agents, portes dynamiques, liens
  hors-maillage, rebuild partiel, reachability, diagnostics géométriques et overlay viewport.
- [x] **G7-15 — diagnostics actionnables** : UUID, sévérité, propriété source, navigation et quick
  fixes transactionnels pour la dernière exigence P0 Core non commencée.
- [x] **G7-16 — Content Browser et Asset Vault local** : manifeste versionné, catalogue adressé par
  hash, recherche à facettes, provenance/licence, aperçu et statut d’import dans l’espace Assets.
- [x] **G7-17 — localisation auteur** : catalogue canonique, langues et fallback, édition
  transactionnelle, extraction, pseudo-localisation, diagnostics et aperçu dans le Creator.
- [x] **G7-18 — mise à l’échelle dans le Toolset** : manipulation directe de l’échelle, validation,
  propriétés précises, commande transactionnelle et historique pour `ORF-EE-002`.
- [x] **G7-19 — pathfinding amélioré** : requêtes de chemin déterministes, coûts, blocages dynamiques,
  invalidation incrémentale et diagnostics pour `ORF-EE-028`.
- [x] **G7-20 — productivité de la coque Creator** : filtres combinés restaurés dans le viewport,
  placement répété atomique, onglets de zones, comparaison protégée, aide sourcée et sept profils
  d’éditeur persistants, couverts par le smoke moteur `OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS`.

Les règles graphiques et d’interaction de G7-03 à G7-17 sont consignées dans
[`docs/UX_AURORA_2026.md`](docs/UX_AURORA_2026.md).

## Gate post-MVP — Contenu moderne G11

- [x] **G11-A** — profils de traitement et cache d'assets gouvernés.
- [x] **G11-B** — manifestes d'assets, plateformes, collisions, animations, VFX et LOD.
- [x] **G11-C** — round-trip externe et Blender réel avec source du Vault immuable.
- [x] **G11-D** — signatures, deltas, canaux et bundle Windows installable/désinstallable.
- [x] **G11-E** — imports/exports en masse, tables de données et fusion assistée.
- [x] **G11-F** — UI scalable, onglets de zone, favoris et compatibilité artistique.
- [x] **G11-G** — rendu PBR/HDR, éclairage par pixel, budgets dynamiques et ciel canonique.
- [x] **G11-H** — vent, météo, eau et réverbération runtime.
- [x] **G11-I** — terrain multi-biome, features et navigation volumétrique.
- [x] **G11-J** — timers globaux/locaux, repos conditionnel et abandon sûr d'une séquence.
- [x] **G11-K** — séquences, cutscenes, ancres caméra et transforms interpolés.
- [x] **G11-L** — nage, escalade, roue d'actions, temps tactique et modes de combat.
- [x] **G11-M** — progression, difficultés, perception et règles de zone.
- [x] **G11-N** — audio spatial, voix, patrouilles inter-zones et chargement accessible.
- [x] **G11-O** — règles data-driven, UI scriptable, JSON sûr et snapshots runtime.
- [x] **G11-P** — copie runtime, indicateurs AOE, NUI 2.0, compilateur et docs générées.
- [x] **G11-Q** — manifestes de tâches, crash artifacts, santé, récupération et synchronisation.
- [x] **G11-R** — laboratoire qualité : localisation, ForgeScript, dialogues, quêtes et rencontres.
- [x] **G11-S** — tests réseau/visuels, benchmarks, budgets et profilage intégré.
- [x] **G11-T** — accessibilité éditeur/runtime, fuzzing sécurité et gouvernance finale.

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
- [x] Versionner le schéma de métadonnées et les manifestes de packs.
- [x] Implémenter le catalogue local en lecture, l’adressage par hash et les contrôles de licence.
- [x] Ajouter stockage installé/désinstallable, déduplication et crédits agrégés au catalogue.

### AV2 — Acquisition et normalisation

- [x] Intégrer les trois premières sources autorisées : Poly Haven, ambientCG et packs Kenney CC0.
- [x] Étendre acquisition, reprise et cache actuels avec mise à jour et désinstallation locale.
- [x] Construire la normalisation GLB/Blender, les collisions, LOD, miniatures et audits qualité.
- [x] Persister les profils texture/modèle, produire des tâches et reçus déterministes et préparer
  un GLB et une texture réels sans écrire dans le Vault.
- [x] Produire un manifeste distribuable par asset avec chunks, variantes plateforme, collisions,
  animations, VFX, LOD, budgets et contrôle runtime des hashes.
- [x] Documenter les sources acceptées, refusées et celles exigeant une action manuelle.

### AV3 — Interface Asset Vault

- [x] Prototyper dans `forge_forest_map` la recherche du catalogue, la sélection d’un asset installé,
  sa copie dans `res://` et la conservation de `provenance.json`.
- [x] Ajouter une interface de peinture viewport pour forêt, clairière, fleurs, rochers et zones
  interdites avec Undo/Redo Godot.
- [x] Ajouter recherche, filtres de première tranche, catégories, provenance et prévisualisations.
- [x] Éditer les profils d'import dans le Content Browser par la commande transactionnelle commune,
  avec dry-run, undo/redo et validation de licence/provenance.
- [x] Préparer et enregistrer le manifeste d'un asset dans le Content Browser, avec état vide,
  résumé, VFX optionnel et historique transactionnel.
- [x] Configurer un round-trip Blender optionnel par commande transactionnelle, avec requête typée,
  validation confinée, surveillance déclarative et undo/redo dans le Content Browser.
- [x] Exécuter le round-trip Blender réel sur un OBJ CC0, valider le GLB 2, ses LOD/collisions et
  l'immutabilité du Vault dans le gate global lorsque le binaire optionnel est disponible.
- [x] Refuser migrations ou désinstallations de packs sans code hôte de confiance, sauvegarde et
  résolution explicite des dépendants.
- [x] Enrichir les manifests avec styles, variantes, dimensions, métriques 3D et compatibilité ; la
  finalisation vérifie GLB, miniature, audit, licence et provenance avant la commande transactionnelle.
- [x] Ajouter installation à la demande, glisser-déposer et intégration au placement existant :
  un asset GLB gouverné devient un blueprint ProjectContent favori, le placement conserve
  `visual.asset` dans la même commande et Creator/runtime partagent le chargeur avec contrôle SHA-256.
- [x] Préparer les brosses de dispersion et recettes de biome sans dupliquer les systèmes de scène :
  la peinture matériau/biome pilote les recettes existantes, les blueprints d’assets installés sont
  sélectionnables et Creator/runtime partagent le même plan déterministe et le même chargeur GLB.
- [x] Faire passer toute mutation IA/MCP par les commandes transactionnelles et la validation de
  licence.

### AV4 — Terrain générique et carte Forgotten Forest

#### AV4A — Fondation de terrain générique multi-biome

- [x] Formaliser dans un ADR le contrat de terrain partagé : plan horizontal X/Z, altitude Y,
  séparation stricte entre terrain, couches de biome, gameplay et entités placées.
- [x] Ajouter au domaine C# versionné une définition de terrain associée à la zone : dimensions,
  découpage, résolution, seed et références contrôlées vers heightmaps, masques de matériaux et
  masque de trous.
- [x] Étendre le format `.orpmod`, les migrations et les tests de round-trip afin que le terrain soit
  inspectable et indépendant de toute scène ou ressource Godot.
- [x] Concevoir les commandes transactionnelles de sculpture : lever/abaisser, aplanir, lisser et
  ouvrir/fermer un trou, avec prévisualisation, persistance et Undo/Redo par trait.
- [x] Ajouter le masque canonique et les brosses de peinture locale des matériaux, sans stocker cet
  état dans une ressource ou une scène propre au moteur.
- [x] Évaluer Terrain3D face à un adapter interne, vérifier licence et compatibilité, puis épingler la
  solution retenue sans faire de ses nœuds la source de vérité.
- [x] Reconstruire dans l’adapter 3D la géométrie, les collisions et les requêtes d'altitude/pente depuis le
  document canonique, avec validation headless et génération déterministe.
- [x] Partager les requêtes de surface avec placement, eau, chemins, navigation et biomes ; traiter
  une grotte, un tunnel ou un surplomb comme un trou de terrain accompagné d'une géométrie 3D
  séparée.
- [x] Définir des recettes de biome réutilisables qui échantillonnent altitude, pente, matériau,
  humidité, eau, chemins et exclusions sans posséder ni dupliquer le terrain.
- [x] Migrer le prototype `forge_forest_map` en première couche de biome posée sur ce terrain et
  supprimer son hypothèse de sol plan sans créer une seconde source de vérité.
- [x] Mesurer mémoire, temps de reconstruction, sculpture, collisions, navigation, LOD et culling sur
  une zone extérieure représentative avant d'étendre la taille des cartes.

#### AV4B — Carte Forgotten Forest

- [x] Livrer une scène de création 128 × 128 m reconstructible, avec scatter cellulaire, masque RGBA
  versionné, kit de départ attribué et smoke test headless.
- [x] Concevoir une carte extérieure cohérente à partir d’une sélection de 22 GLB Kenney Mini Forest
  CC0 avec identité, hash, manifeste, palette externe et crédits.
- [x] Intégrer la fondation de terrain AV4A, végétation MultiMesh, bâtiments, ruisseau stylisé,
  éclairage, caméra orbitale et landmarks.
- [x] Finaliser le bake de navigation, une eau simulée et les traversées : 1 990 polygones runtime,
  58 triangles de rivière bloqués, pont lié, chemin moteur camp-sanctuaire et quatre volumes animés.
- [x] Réutiliser les briques existantes de dialogue, quête, combat, transition et sauvegarde.
- [x] Ajouter profil Forward+, validation automatisée, playtest headless, capture visuelle et métriques
  de reconstruction.
- [x] Vérifier export du module, dépendances, crédits et ouverture de l’archive depuis le cache runtime.

### AV5 — Livraison et extension

- [x] Livrer la documentation, le journal d’implémentation et les procédures d’ajout de sources/packs.
- [x] Mesurer catalogue, téléchargements, contenu utilisé, taille, licences et performances réelles.
- [x] N’élargir massivement le catalogue qu’après validation de la cohérence visuelle et du pipeline.

### AV6 — Personnages animés et finition de production

- [x] Produire depuis le Vault immuable un personnage GLB skinné avec texture et clips gouvernés.
- [x] Valider hashes, licence, rig, clips, miniature, audit qualité et réutilisation idempotente.
- [x] Relier le profil d’animation aux entités, au viewport Creator et au contrôleur du joueur.
- [x] Prouver cinq personnages et trois clips dans le smoke jouable puis inspecter une capture Forward+.
- [x] Ajouter cinq variantes fantasy cohérentes et une séquence combat attaque/impact/mort bornée.
- [x] Prouver le retargeting d’un clip entre deux rigs incompatibles, avec audit de déformation :
  course Kenney 58 os vers chevalier KayKit 41 os, 19 articulations mappées, 9 poses et 63 216
  sommets contrôlés, puis réouverture du GLB exporté dans Blender.
- [x] Composer la carte 512 × 512 en 64 secteurs, puis refaire budgets, navigation et revue visuelle.
- [x] Remplacer les couleurs terrain simples par une finition PBR procédurale, des routes continues et
  une preuve Forward+ réelle partagée entre Creator et runtime.
- [x] Enrichir l’habillage rapproché des secteurs avec une sélection plus variée de modèles et textures
  gouvernés, puis refaire une passe de direction artistique.

## Gate post-MVP — Assistant de création avancée G12

- [x] G12-A/B : Assistant intégré, fournisseurs locaux optionnels, prompts versionnés et lore gouverné.
- [x] G12-C/D : plans de crypte/village et construction atomique approuvée.
- [x] G12-E/F : dialogues et quêtes sourcés, simulés, persistants et réversibles.
- [x] G12-G : création/réparation ForgeScript compilée et testée avant écriture.
- [x] G12-H : boss canonique multi-domaines validé par 32 simulations.
- [x] G12-I : capacité canonique validée par cinq scénarios du résolveur de combat.
- [x] G12-J : création 3D gouvernée via Blender (`ORF-AI-021`).
- [x] G12-K : fournisseurs text/image-to-3D optionnels, staging, licence et acceptation humaine (`ORF-AI-022`).
- [x] G12-L : politique canonique des workers externes, allowlist Blender et interdiction de Python libre (`ORF-AI-023`).

Acceptation G12-I : le Toolset et MCP partagent la même proposition, le même dry-run, la même
transaction et le même undo/redo pour le profil de combat et son document runtime.

Acceptation G12-J : le Toolset et MCP partagent la même recette canonique, le même dry-run et le
même lot réversible ; le worker Blender réel génère primitives, matériau, UV, LOD, collisions,
miniature, audit et GLB sans accepter de Python libre.

Acceptation G12-K : le provider offline, TRELLIS local et TripoSR local partagent un contrat borné.
Le Toolset, la CLI et MCP présentent licence, provenance, staging, topologie, coût et diff avant
acceptation humaine ; le lot OBJ/reçu/configuration/requête Blender est atomique et réversible.

Acceptation G12-L : le Toolset et MCP partagent une politique persistante, la même commande avec
transaction, plan, dry-run, permission, diff, audit et undo/redo. Les requêtes et le worker refusent
`exec`, `eval`, shell, Python libre, chemins non confinés, réseau, opérations hors allowlist et
budgets non bornés ; timeout et taille de sortie sont appliqués avant publication.

## Gate post-1.0 — Extensions avancées G13

Le plan borné des 135 exigences est suivi dans
`docs/aurora-parity/G13_DELIVERY_PLAN.md`. Chaque tranche conserve une à cinq feature IDs et laisse
les démonstrations Crypte des Cendres et Forgotten Forest jouables.

- [x] G13-A : profils de raccourcis Standard/Aurora/Blender, commandes recherchables, conflits,
  persistance locale et exécution réelle (`ORF-SHELL-022`).
- [x] G13-B : identité, portrait, corps, couleurs, ailes/queues et voix des créatures
  (`ORF-CRE-006`, `007`, `009`, `010`, `029`).
- [x] G13-C : progression de créature data-driven (`ORF-CRE-016`, `017`, `018`, `019`, `020`).
- [x] G13-D : butin de créature, pickpocket et cadavres (`ORF-CRE-023`, `024`, `032`).
- [x] G13-E : informations de rôle, animations ambiantes et auto-level/templates
  (`ORF-CRE-030`, `047`, `048`).
- [x] G13-F : événements avancés de créature (`ORF-CRE-039`, `041`, `042`, `044`, `045`).
- [x] G13-G : factions avancées (`ORF-FAC-002`, `008`, `009`, `010`).
- [x] G13-H : animations, voix, caméra, bookmarks et interjections de dialogue
  (`ORF-DLG-014`, `015`, `016`, `020`, `023`).
- [x] G13-I : choix temporisés accessibles et variantes procédurales déterministes
  (`ORF-DLG-025`, `027`).
- [x] G13-J : journal priorisé, quêtes répétables et partagées, choix exclusifs, notifications
  (`ORF-QST-009`, `016`, `017`, `018`, `021`).
- [x] G13-K : économie, prérequis, identification, propriété/vol et On Hit des objets
  (`ORF-ITEM-007`, `010`, `011`, `012`, `017`).
- [x] G13-L : armes, armures, munitions, durabilité et conteneurs imbriqués
  (`ORF-ITEM-019`, `020`, `021`, `022`, `023`).
- [x] G13-M : loot contextuel, objets uniques, loot instancié, artisanat et génération procédurale
  (`ORF-ITEM-025`, `026`, `027`, `028`, `029`).
- [x] G13-N : faction, conversation et accès consommable des placeables (`ORF-PLC-010`, `011`, `013`).
- [x] G13-O : événements et physique dynamique des placeables (`ORF-PLC-021`, `022`, `023`, `024`).
- [x] G13-P : dégâts, chargement et événements physiques des portes (`ORF-DOOR-007`, `010`, `014`, `015`).
- [x] G13-Q : événements avancés, social et assemblages de portes (`ORF-DOOR-016`, `017`, `018`, `020`).
- [x] G13-R : découverte, clic, accès, surbrillance et repos des triggers (`ORF-TRG-005`, `008`, `012`, `013`, `014`).
- [x] G13-S : heartbeat/custom, pistes, interjections et commande GM (`ORF-TRG-009`, `010`, `015`, `016`, `018`).
- [x] G13-T : respawn, faction et événements des rencontres (`ORF-ENC-008`, `010`, `014`).
- [x] G13-U : renforts, visibilité et nettoyage des rencontres (`ORF-ENC-016`, `017`, `019`).
- [x] G13-V : routes de véhicules (`ORF-WPT-012`).
- [x] G13-W : trésorerie, achats, biens volés et réapprovisionnement (`ORF-MER-005`, `006`, `007`, `010`).
- [x] G13-X : monnaies, négociation, marché, services et audit (`ORF-MER-013`, `014`, `015`, `016`, `018`).
- [x] G13-Y : dialogue, formation, progression, relation et banter des compagnons (`ORF-CMP-004`, `011`, `013`, `014`, `015`).
- [x] G13-Z : quête, pièges, capacités, contrôle et regroupement des compagnons (`ORF-CMP-016`, `019`, `020`, `021`, `023`).
- [x] G13-AA : caméra/cutscenes et routage par tag (`ORF-SCR-018`, `024`).
- [x] G13-AB : flags de module et réglages IA (`ORF-SCR-025`, `026`).

Acceptation G13-A : un auteur configure les raccourcis depuis l’écran Aurora, prévisualise le diff,
applique la commande canonique et peut annuler/rétablir. Le round-trip `.forge/editor-keymap.json`
est exact, les conflits sont refusés avec diagnostic stable, et les bindings déclenchent les actions
Toolset existantes sans intercepter les champs texte ni entrer dans le module exporté.

Acceptation G13-B : un auteur configure depuis le Studio de créature genre, corps, pronoms,
portraits gouvernés, slots modulaires, couleurs, ailes/queue et voix. La même donnée canonique est
prévisualisée en 3D, appliquée par commande, annulable, sérialisée dans le projet et exportée dans
le `.orpmod`. Portrait absent ou altéré bloque l’export ; l’absence optionnelle du profil conserve
le fallback jouable.

Acceptation G13-C : un auteur configure sans JSON pistes/classes génériques, niveaux, axes de
disposition propres au projet, compétences, talents et loadout de pouvoirs. Les références au
profil de combat sont validées avant application ; dry-run, undo/redo, rechargement, `.orpmod`,
résumé UI et handle ForgeScript utilisent le même profil canonique. L’absence optionnelle reste
jouable et l’absence requise produit un diagnostic stable.

Acceptation G13-D : un auteur configure sans JSON entrées de butin, probabilité, propriété,
instanciation, vol à la tire et politique de cadavre. La même commande assure dry-run, diagnostics,
undo/redo et persistance ; le runtime historique reste compatible et la Crypte produit une
dépouille visible dont l’interaction accorde le butin résolu.

Acceptation G13-E : un auteur configure sans JSON champs de lore, tags, idle sets, horaires,
interactions et interruptions, puis prévisualise le résultat reproductible d’un template de
progression. Seed, diff, verrouillage manuel, dry-run, undo/redo, persistance et handle script sont
prouvés ; la Crypte avance réellement la routine ambiante du Gardien avant le combat.

Acceptation G13-F : un auteur configure sans JSON les frontières de combat, événements d’inventaire,
repos, heartbeat avec cadence/suspension/budget et événements utilisateur namespacés/typés. La même
commande assure dry-run, undo/redo et round-trip ; ForgeScript et MCP restent typés et sans mutation
directe. La Crypte déclenche réellement les cinq familles tout en conservant quête, combat et butin.

Acceptation G13-G : un auteur configure sans JSON les parents et héritages d’une faction, ses règles
de témoins, plusieurs affiliations et une faction dynamique. La même commande assure validation des
cycles, dry-run, undo/redo et round-trip projet/workspace/`.orpmod`. ForgeScript et MCP restent typés ;
la Crypte prouve héritage, témoin, guerre → trêve, expiration et autorité serveur.

Acceptation G13-H : un auteur configure sans JSON animation, voix/sous-titre, caméra accessible,
bookmarks de revue et interjections d’un dialogue. La même commande assure dry-run, diagnostics,
undo/redo et round-trip projet/workspace/`.orpmod`. ForgeScript et MCP restent typés ; la Crypte
applique réellement les cinq familles à l’interaction avec Elyra, avec fallback sous-titre et
sélection d’interjection côté serveur.

Acceptation G13-I : un auteur configure sans JSON timer, choix par défaut, pause accessible, révision
serveur, hook de timeout et variantes localisées conditionnelles/pondérées. Simulation, commande,
dry-run, undo/redo, persistance, ForgeScript et MCP partagent le même profil. La Crypte prouve
timeout, pause, seed stable et anti-répétition tout en conservant son smoke jouable.

Acceptation G13-J : un auteur configure sans JSON ordre du journal, répétition, partage, choix mutex
et notifications. Validation, simulation, dry-run, undo/redo, projet/workspace/`.orpmod`,
ForgeScript, MCP et runtime consomment le même profil optionnel et déterministe.

Acceptation G13-K à G13-M : un auteur configure sans JSON l’objet depuis sa valeur et ses prérequis
jusqu’à son équipement, sa durabilité, ses conteneurs, son loot, son unicité, son artisanat et ses
affixes. Le runtime prouve calculs, autorité et transactions ; l’absence du profil conserve le
fallback historique.

Acceptation G13-N à G13-AB : un auteur configure sans JSON les huit familles du Monde vivant.
Sélection de cible, résumé, diagnostics, dry-run, application, audit et undo/redo partagent le même
`WorldFeatureCatalog`. Projet `0.6.32`, workspace `1.26.0`, `.orpmod`, ForgeScript et MCP conservent
ou lisent ce contrat. Les résolveurs purs déterministes sont testés tranche par tranche et la Crypte
émet quinze preuves runtime avant de terminer son scénario jouable.

Ce run s’arrête après G13-AB. Aucun lot suivant n’est démarré.
