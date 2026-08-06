# G12-F — Création de quête assistée

## Portée

- exigence : `ORF-AI-017` ;
- proposer une machine à états de quête depuis une zone, un participant, un synopsis, des objectifs
  et du lore sourcé ;
- normaliser la proposition vers la quête Core déjà persistée et consommée par le runtime ;
- partager le même cycle plan/apply/revert entre Toolset et MCP.

## Résultat livré

Le service de création assistée produit des UUID déterministes, quatre états (`dormant`, `active`,
`completed`, `failed`), des participants et références résolus, des objectifs bornés, trois
transitions avec conditions/actions typées, une récompense de progression, un chemin d'échec
explicite et cinq scénarios reproductibles : nominal, échec, répétition, autorité multijoueur et
sauvegarde/reprise. La proposition conserve citations, règles de style, localisation, lint, diff
sémantique, coût, fournisseur et provenance.

La fenêtre **Quête assistée** sépare le brief et le monde, la gouvernance, puis le résultat. Elle
affiche états, transitions, objectifs, récompense, chemin d'échec, cinq scénarios, citations, lint,
coût et provenance. Une proposition non approuvée reste en lecture seule. Après approbation, le
dry-run exact lie transaction, plan et révision avant l'unique appel à `SetQuestHandler`.

Le MCP expose propose/plan/apply/revert avec la permission `ApplyApprovedPlan`. L'interface, le MCP,
la sauvegarde et l'historique Creator utilisent ainsi la même commande, le même diff et le même
inverse.

## Fichiers autorisés

- `src/Forge.RpgKit.Application/Quests/AssistedQuest*` et handler associé ;
- fenêtre Toolset, Assistant et branchement plugin ;
- contrats, service et outils MCP ;
- tests, tracker, documentation générée, graphe et gate.

## Non-objectifs

- aucun texte généré n'est exécuté comme code ;
- aucune mutation parallèle du document ou de la scène moteur ;
- aucun fournisseur réseau obligatoire ;
- la création de ForgeScript assistée reste le périmètre de G12-G (`ORF-AI-018`).

## Preuves

- tests Application : déterminisme, états, objectifs, transitions, récompense, échec, cinq scénarios,
  approbation, permission, dry-run exact et historique atomique ;
- test MCP : propose/plan/apply/revert avec persistance de la quête canonique ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12F_QUEST_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
