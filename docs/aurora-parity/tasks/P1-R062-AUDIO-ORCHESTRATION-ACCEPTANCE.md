# P1 R062 — Portée, contrôle, ambiance, musique et bus audio

## Périmètre

Lot canonique `R062 / FC-AUDIO-P1-02`, limité aux cinq fonctions suivantes :

- `ORF-AUD-011` — Portée audible ;
- `ORF-AUD-012` — Activation par script ;
- `ORF-AUD-013` — Ambiance jour/nuit ;
- `ORF-AUD-014` — Musique jour/nuit/combat ;
- `ORF-AUD-018` — Mixage et bus.

Les dépendances `AREA`, `GAMEPLAY`, `PROJECT`, `QA` et `SCRIPT` sont déjà disponibles. Aucun travail
P2, AV7–AV10, réverbération avancée ou streaming audio n'est ouvert par ce lot.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/15_AUDIO.md` avant modification.

| Critère source | État avant R062 | Limite réelle fermée par R062 |
|---|---|---|
| Atténuation avec rayons interne/externe, courbe, absorption et sphères de debug | calcul déterministe linéaire/inverse et absorption présents | courbe inverse-square partagée, aperçu échantillonné et deux sphères moteur interne/externe optionnelles |
| Handle runtime Start/Stop/Pause/Fade, politique d'instances, autorité et callbacks | état initial, single/restart/overlap, autorité, fondu et callbacks Start/Stop présents | handle générationnel, Start/Stop/Pause/Resume/Fade, rejet d'autorité structuré, événements configurables, API ForgeScript 1.0 et lecteur moteur piloté |
| Couches adaptatives heure, météo, lieu, danger, intérieur et volumes | sélection canonique des couches présente | contrat de volume et durée de fondu persistés, plan de transition et lecteurs moteur réellement fondus |
| États musicaux, stems, transitions, cooldown et synchronisation musicale | états exploration/tension/combat/boss/victory, stems et cooldown présents | BPM, temps par mesure et délai déterministe jusqu'à la prochaine mesure |
| Bus hiérarchiques, snapshots, plateformes, plage dynamique, accessibilité et monitoring | hiérarchie et profils persistés | vumètres pic/RMS/LUFS, gain effectif parent-enfant et lecture réelle des niveaux du moteur |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R062 |
|---|---|
| Domaine canonique avant représentation moteur | extensions versionnées de `AreaAudioProfile`, `AudioRuntimeControl`, `AdaptiveAmbienceLayer`, `MusicStateEntry` et `AudioMixBus` |
| UUID, schéma, provenance, manifeste, SPDX et SHA-256 | identité `AudioAuthoringProfile` conservée par la même commande et la même persistance |
| Diagnostics stables | `audio.runtime.authority_denied`, `audio.runtime.handle_invalid` et `audio.runtime.instance_rejected` avec chemin du handle |
| Interface visible, vide et erreur | onglet `Orchestration audio` : portée, contrôle, ambiance, musique et monitoring |
| UI et commande sans voie parallèle | `SetAreaEnvironmentHandler`, dry-run/apply, undo/redo et round-trip restent l'unique mutation |
| ForgeScript typé | `AudioEmitterRef` et API `audio_start`, `audio_stop`, `audio_pause`, `audio_resume`, `audio_fade` |
| MCP filtré et transactionnel | le plan audio existant conserve les nouveaux champs et les signatures sont exposées par l'API script MCP |
| Runtime optionnel/requis | le résolveur R061 reste le contrat de présence du profil audio, sans nouvelle dépendance obligatoire |
| Runtime jouable | sphères 3D, lecteurs adaptatifs, pause/reprise/fondu et mesure de bus sont exécutés par `GameRoot` |

## Validation exécutée

- build solution : 27 projets, zéro avertissement et zéro erreur ;
- suites ciblées Application/Scripting/ModuleFormat/MCP ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R062_AUDIO_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R062_AUDIO_PASS` avec sphères, lecteurs et vumètre moteur ;
- capture `docs/aurora-parity/evidence/r062-audio-orchestration-ui.png`, 1320×820, inspectée sans
  chevauchement : les trois colonnes et les actions critiques sont visibles ;
- tracker, backlog, documentation et graphe Roslyn régénérés à 6 581 nœuds / 41 356 relations ;
- portail intégré attendu à 1 440 tests .NET et 39 tests Python avant ouverture de R063.

## Décision

Les cinq limites `Partial` identifiées sont fermées sans promotion hors lot. Les cinq IDs passent
à `Tested` après validation du portail intégré R062 ; `R063 / FC-AUDIO-P1-03` devient alors le
prochain lot auditable.
