# P0 R004 — objets sonores, clips, boucles, position et validation

Lot stable : `R004` / `FC-AUDIO-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AUD-001` — Objet sonore.
- `ORF-AUD-002` — Liste de clips.
- `ORF-AUD-005` — Lecture unique ou répétée.
- `ORF-AUD-008` — Position spécifique.
- `ORF-AUD-020` — Validation audio.
- Source immuable relue pour les règles de domaine et les cinq sections :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/15_AUDIO.md`.
- Dépendances réévaluées : `CONTENT` et `QA`. R001–R003 fournissent le build, la gouvernance des
  assets et les preuves d’acceptation nécessaires.

## Audit avant implémentation

| Fonction | Présent | Limite réelle fermée |
|---|---|---|
| AUD-001 | ambiance globale `AreaAudioProfile`, commande d’environnement et runtime audio 3D pour les cues | aucun `AudioEmitterDefinition/Instance` canonique, placé et reconstructible |
| AUD-002 | playlist simple de chemins | aucun `ClipSet` à UUID, poids, tags, variante, langue, loudness, aperçu, licence, provenance et hash |
| AUD-005 | politique d’instances et événements des cues | aucune politique one-shot/loop/région seamless/compte fini/événement avec crossfade validé |
| AUD-008 | atténuation globale et scatter spatial | aucun transform propre à l’instance, cône, occlusion, hauteur et virtualisation |
| AUD-020 | validation générique des packages et budgets | aucune validation audio dédiée des fichiers, licences, loudness, régions, bus, portée, hash et mémoire |

## Contrat minimal de fermeture

- ajouter un catalogue audio v1 indépendant du moteur, avec définition, instance et UUID stables ;
- conserver `SetAreaEnvironmentCommand` comme unique mutation UI avec dry-run, diff, undo/redo et
  persistance exacte ;
- exposer l’objet principal dans un onglet Toolset avec états vide, valide et erreur explicites ;
- reconstruire un lecteur audio 3D depuis le modèle canonique, ignorer le catalogue optionnel vide
  et diagnostiquer une ressource requise absente ;
- supporter la sélection ordonnée/pondérée déterministe et les cinq modes de boucle ;
- vérifier avant distribution le confinement, l’existence, la taille et le SHA-256 des fichiers ;
- exposer des handles ForgeScript typés et des outils MCP filtrés puis plan/apply/revert ;
- ne pas ouvrir AUD-003/004/006/007/009 ni une fonction P1/P2.

## Matrice des 30 critères d’acceptation

| Fonction | Configuration sans généré | Save/close/reload | Invalide refusé/localisé | UI = commande | Undo/redo | Asset non gouverné bloqué |
|---|---|---|---|---|---|---|
| AUD-001 | onglet Objets sonores | JSON exact | `audio.emitter.definition_invalid` | handler d’environnement | historique projet | licence/provenance/hash requis |
| AUD-002 | clip principal ou catalogue MCP | JSON exact | `audio.clip.invalid` | handler d’environnement | historique projet | métadonnées par clip |
| AUD-005 | cinq modes et paramètres | JSON exact | `audio.loop.invalid` | handler d’environnement | historique projet | validation avant build |
| AUD-008 | transform, portée, cône, occlusion | JSON exact | `audio.spatial.invalid` | handler d’environnement | historique projet | référence résolue |
| AUD-020 | budgets et métadonnées visibles | JSON exact | codes `audio.*` avec chemin | même validateur | historique projet | export refusé avant écriture |

## Non-objectifs

- navigateur waveform, batch preview et recherche d’assets audio (`AUD-003`, P1) ;
- shuffle bag/no-repeat (`AUD-004`, P1), modulation (`AUD-006`, P1), volume avancé (`AUD-007`, P1) ;
- audio global, aléatoire, voix, réverbération avancée ou mixage P1/P2 ;
- P1/P2, AV7–AV10, publication Git ou shell MCP.

## Résultats avant promotion

- tests ciblés réussis : Domain 2, Application 5, ModuleFormat 3 et MCP/ForgeScript 5 ;
- compilations Toolset, Runtime et MCP Host sans avertissement ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_R004_AUDIO_PASS`, avec commande, persistance et historique ;
- smoke Runtime : `OPENRPG_FORGE_RUNTIME_R004_AUDIO_PASS`, lecteur 3D placé et région WAV active ;
- preuve visuelle inspectée : `docs/aurora-parity/evidence/r004-audio-emitter-ui.png` (1320 × 820),
  trois cartes lisibles, sans chevauchement ni troncature des actions ;
- portail intégré pré-promotion, autorités et résultats de clôture à consigner avant R005.

## Résultats de clôture

- portail pré-promotion intégral : `OPENRPG_FORGE_VERIFICATION=PASS` avec 1 026 tests .NET,
  6 tests Python généraux et 33 tests Python du pipeline d'assets ;
- graphe Roslyn régénéré : 5 271 nœuds, 30 714 relations, zéro diagnostic et contrôle déterministe ;
- normalisation canonique du projet exemple : ajout unique du catalogue `emitters` v1 vide, puis
  hash de `project.json` stable au smoke suivant ;
- `ORF-AUD-001`, `002`, `005`, `008` et `020` promus individuellement à `Tested` avec preuves
  d'implémentation, tests, smokes et capture liées dans l'audit projet ;
- autorités régénérées : 226 fonctions `Tested`, 172 P0 non closes, 57 lots clos et 46 lots P0
  ouverts ;
- portail post-promotion intégral : `OPENRPG_FORGE_VERIFICATION=PASS` sur les mêmes 1 026 tests
  .NET, les 39 tests Python et tous les smokes Toolset/runtime/packaging/assets/réseau/MCP ; R005
  peut être ouvert.
