# P1 R063 — Sous-titres de sons importants

## Périmètre

Lot canonique `R063 / FC-AUDIO-P1-03`, limité à une fonction :

- `ORF-AUD-019` — Sous-titres de sons importants.

Les dépendances audio R061–R062 sont déjà disponibles. Ce lot n'ouvre ni fonction P2, ni
AV7–AV10, ni reconnaissance vocale, ni transcription automatique.

## Audit source avant modification

Les critères complets ont été relus dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/15_AUDIO.md` avant modification.

| Critère source | État avant R063 | Limite réelle fermée par R063 |
|---|---|---|
| Métadonnées de sous-titre pour dialogues, effets et ambiances | texte, langues de repli, importance et direction étaient persistés | type de signal, identité stable, durée de lecture et exigence runtime sont désormais canoniques et validés |
| Localisation et repli | modèle localisé présent | résolution déterministe de la langue demandée vers le repli, avec durée bornée selon la longueur du texte |
| Indication de direction | booléen présent | direction avant/droite/arrière/gauche calculée depuis l'auditeur et affichée dans le HUD |
| Affichage runtime | absent | surcouche HUD accessible réellement déclenchée par les signaux audio importants puis effacée après la durée calculée |
| Gouvernance et distribution | non complète au niveau de chaque sous-titre | UUID, schéma, provenance, manifeste, SPDX et SHA-256 contrôlés ; ressource non gouvernée refusée |

## Contrat transversal et preuves

| Critère commun immuable | Preuve R063 |
|---|---|
| Domaine canonique avant représentation moteur | `SoundCaption` versionné et `AudioCaptionValidator` indépendant du HUD |
| UUID, schéma, provenance, manifeste, SPDX et SHA-256 | champs persistés et contrôlés avec diagnostics stables |
| Références et budgets | ressource reliée à une playlist, un émetteur gouverné ou une ressource intégrée ; plafond de 512 sous-titres |
| Interface visible, vide et erreur | carte `Accessibilité audio` avec aperçu, état et gouvernance |
| UI et commande sans voie parallèle | `SetAreaEnvironmentHandler`, dry-run/apply, undo/redo et round-trip exact |
| ForgeScript typé | handle sûr `AudioCaptionRef` et fonction `audio_caption` |
| MCP filtré et transactionnel | signature exposée par le catalogue script MCP ; plan/apply/revert restent portés par la commande audio existante |
| Runtime optionnel/requis | absence optionnelle ignorée ; absence requise signalée par `runtime.audio_caption.required_missing` |
| Runtime jouable | `GameRoot` déclenche `RuntimeHud.ShowSoundCaption` avec texte, flèche directionnelle et métadonnées accessibles |

## Validation exécutée

- build solution : 27 projets, zéro avertissement et zéro erreur ;
- suites ciblées Domain, Application, ModuleFormat et MCP ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_R063_AUDIO_CAPTION_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_R063_AUDIO_CAPTION_PASS` ;
- capture `docs/aurora-parity/evidence/r063-audio-caption-ui.png`, 1320×820, inspectée sans
  chevauchement ni texte tronqué ;
- tracker, backlog, documentation et graphe Roslyn régénérés avant le portail intégré ;
- portail `scripts/Verify.ps1` attendu à 1 444 tests .NET et 39 tests Python avant ouverture de R064.

## Décision

La limite `Partial` identifiée — l'absence d'affichage runtime — est fermée sans promotion hors lot.
`ORF-AUD-019` passe à `Tested` après validation du portail intégré R063 ; le prochain lot P1 ne peut
être ouvert qu'après ce passage complet.
