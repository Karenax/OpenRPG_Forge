# P1 R061 — Atelier audio, modulation, mix et écoute 2D

## Périmètre

Lot canonique `R061 / FC-AUDIO-P1-01`, limité aux cinq fonctions suivantes :

- `ORF-AUD-003` — Recherche et aperçu des ressources ;
- `ORF-AUD-004` — Ordre séquentiel ou aléatoire ;
- `ORF-AUD-006` — Intervalle et variation ;
- `ORF-AUD-007` — Volume relatif ;
- `ORF-AUD-009` — Son partout.

Les dépendances `AREA`, `PALETTE` et `QA` sont déjà disponibles. Aucun travail P2, AV7–AV10,
spatialisation avancée, reverb ou streaming audio n'est ouvert par ce lot.

## Audit source avant modification

| Critère source | État avant R061 | Limite réelle fermée par R061 |
|---|---|---|
| Browser waveform, audition, metadata, tags, transcript, licence, loudness et batch preview | Catalogue audio gouverné, recherche MCP par nom/chemin/tag et saisie d'une playlist présents ; waveform et audition absents | navigateur applicatif par facettes et transcript, waveform persistée ou déterministe, plan d'écoute simple/par lot, métadonnées visibles et lecture réelle d'une ressource importée |
| PlaybackSelection sequential, shuffle bag, weighted, deterministic seed et no-repeat window | séquentiel et aléatoire déterministe seulement | quatre modes, pondérations par ressource/clip, seed stable et fenêtre anti-répétition |
| Modulation ranges, distributions, curves, seed, tempo sync et preview statistique | intervalle central et variation uniforme bornée | bornes intervalle/pitch/volume, distributions uniforme/triangulaire/gaussienne, courbe normalisée, seed, quantification BPM et aperçu sur 512 tirages |
| Loudness-normalized gain, bus routing, ducking, distance, accessibility cap et meter | volume relatif propagé et atténuation 3D | gain LUFS, bus, ducking, échelle de distance, plafond d'accessibilité et mesure |
| 2D/ambient bus with area scope, listener policies et multiplayer personalization | global 2D, scope et politique générique sans personnalisation | portée de zone, quatre politiques d'auditeur, gains par joueur/party et lecteur runtime 2D réel |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R061 |
|---|---|
| Domaine canonique avant représentation moteur | `AudioAuthoringProfile`, ses sous-profils et `AudioAuthoringProfileValidator` |
| UUID, schéma, provenance, manifeste, SPDX et SHA-256 | identité versionnée du profil et gouvernance des `AudioClipDefinition` ; rejet stable avant mutation/build |
| Interface visible, états vide et erreur | onglet `Atelier audio` de `AreaEnvironmentWindow`, recherche vide, erreur d'import et statut localisés |
| UI et commande sans voie parallèle | `SetAreaEnvironmentHandler` reste l'unique mutation ; smoke Toolset dry-run/apply/undo/redo |
| ForgeScript typé | `AreaAudioScriptService.Read` expose un snapshot sûr sans accès fichier ou nœud moteur |
| MCP filtré et transactionnel | `forge_get_audio_emitters` recherche aussi le transcript ; plan/apply/revert conserve l'auteur audio |
| Persistance exacte | round-trip projet et zone avec schéma environnement v5 et profil R061 |
| Runtime optionnel/requis | `AudioAuthoringRuntimeResolver` ignore l'absence optionnelle et émet `runtime.audio_authoring.required_missing` si nécessaire |
| Runtime jouable | `GameRoot.ConfigureAreaAmbientAudio` reconstruit `AudioStreamPlayer`, bus, gain, pitch et séquence temporisée |

## Validation exécutée

- build solution : 27 projets, zéro avertissement et zéro erreur ;
- suites ciblées Domain/Application/ModuleFormat/MCP ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R061_AUDIO_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R061_AUDIO_PASS` avec lecteur 2D actif ;
- capture `docs/aurora-parity/evidence/r061-audio-workbench-ui.png`, 1320×820, inspectée sans
  chevauchement : browser, waveform, séquence, modulation, mix, auditeurs et gouvernance sont visibles ;
- tracker et backlog frais : 445 fonctions `Tested`, 271 `Implemented`, 115 lots clos ;
- portail intégré : 27 projets, 1 435 tests .NET, 39 tests Python, smokes, assets, exports,
  sauvegardes, parcours 3D et graphe Roslyn frais, puis `OPENRPG_FORGE_VERIFICATION=PASS`.

## Décision

Les cinq limites `Partial` identifiées sont fermées sans promotion hors lot. Les cinq IDs passent
à `Tested` après le portail intégré vert de R061 ; `R062` devient le prochain lot auditable.
