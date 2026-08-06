# G12-E — Création de dialogue assistée

## Portée

- exigence : `ORF-AI-016` ;
- proposer un graphe de dialogue depuis un personnage, un synopsis, des beats et du lore sourcé ;
- normaliser ce graphe vers le dialogue Core déjà consommé par le runtime ;
- partager le même cycle plan/apply/revert entre Toolset et MCP.

## Résultat livré

Le service de création assistée relie le graphe avancé du RPG Kit au `DialogueDefinition` canonique.
Il génère des UUID déterministes, un ordre stable, des participants typés, des nœuds et arêtes, des
conditions et actions, des clés de localisation avec placeholders, des citations de contexte, des
règles de style, un rapport de lint, un diff sémantique, le coût et la provenance du provider.

Le provider hors ligne reste la référence reproductible de CI. Les transports locaux configurés en
G12-B sont utilisables dans la fenêtre lorsque le profil les autorise ; leur texte est toujours
normalisé vers les mêmes agrégats et ne devient jamais une mutation directe.

La fenêtre **Dialogue assisté** affiche brief, sources, mini-carte textuelle du graphe, lint,
localisation et simulation réelle du nœud d’entrée. Une proposition non approuvée reste consultable
mais ne peut pas être appliquée. Après approbation, une transaction de dry-run exacte précède
obligatoirement `SetDialogueHandler`. Sauvegarde, historique Creator et MCP utilisent le même diff et
le même inverse.

## Fichiers autorisés

- `src/Forge.RpgKit.Application/Dialogues/AssistedDialogue*` et handler associé ;
- fenêtre Toolset, Assistant et branchement plugin ;
- contrats, service et outils MCP ;
- tests, tracker, documentation générée, graphe et gate.

## Non-objectifs

- aucun texte généré n’est exécuté comme code ;
- aucune mutation parallèle du graphe moteur ;
- aucun fournisseur réseau obligatoire ;
- la création de quête assistée reste le périmètre de G12-F (`ORF-AI-017`).

## Preuves

- tests Application : déterminisme, graphe Core/RPG, citations, style, localisation, lint,
  simulation, approbation, permission, dry-run exact et historique atomique ;
- test MCP : propose/plan/apply/revert avec persistance du dialogue canonique ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_PASS` ;
- `scripts/Verify.ps1` protège le marqueur dans la porte intégrée.
