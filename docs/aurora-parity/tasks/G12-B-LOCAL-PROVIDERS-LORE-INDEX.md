# G12-B — Fournisseurs locaux et index de lore gouverné

## Périmètre

- `ORF-AI-003` — index de lore ;
- `ORF-AI-012` — fournisseurs interchangeables, fermeture des transports locaux.

## Résultat livré

- transport HTTP natif compatible OpenAI pour LM Studio, vLLM et serveurs locaux équivalents ;
- transport natif Ollama `/api/chat`, sans bibliothèque ni service cloud obligatoire ;
- réseau refusé sans consentement explicite, endpoints `localOnly` confinés au loopback même après
  redirection, timeout borné, annulation propagée et réponse plafonnée à 4 Mio ;
- clé API lue uniquement depuis la variable d’environnement configurée, jamais persistée ni écrite
  dans les diagnostics ;
- estimation de coût depuis l’usage retourné et les tarifs configurés, avec tokens, fournisseur,
  modèle, provenance et hash du prompt visibles avant application ;
- index de lore canonique v1 dans `assistant/lore-index.json`, couvrant bible du monde, factions,
  personnages, chronologie et terminologie ;
- chaque entrée porte UUID, clé, contenu, tags, provenance, IDs sources et relations résolues ;
- catalogue de sources dérivé du projet, du module, des blueprints, dialogues/nœuds et quêtes/états,
  sans copie opaque de leur contenu canonique ;
- recherche déterministe, graphe de relations et paquet de contexte borné, sourcé et hashé ;
- fenêtre Aurora 2026 dédiée, accessible depuis l’Assistant, avec état vide, liste, édition,
  recherche, plan, diff, transaction, appliquer, annuler et rétablir ;
- service MCP complet en lecture, recherche, plan, application et réversion, utilisant la même
  commande et la même persistance que l’interface humaine.

## Preuves

- `AssistantProviderTransportTests` couvre contrats OpenAI/Ollama, consentement, secret, usage,
  coût, JSON invalide, timeout et annulation avec serveurs factices en mémoire ;
- `LoreIndexTests` couvre modèle canonique, valeurs sûres, sources et relations ;
- `LoreIndexWorkflowTests` couvre sources projet, recherche, contexte hashé, transaction liée au
  dry-run, permission, undo et redo ;
- `LoreIndexStoreTests` couvre absence, round-trip atomique et fichier invalide ;
- `ForgeProjectAgentServiceTests.LoreIndexMcpUsesReadPlanApplyQueryAndRevertContract` couvre le
  parcours MCP complet jusqu’à la restauration ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_PASS` sur le workspace réel ;
- `scripts/Verify.ps1` exige désormais le marqueur G12-B.

## Limites explicites

- aucun modèle local lourd n’est installé automatiquement : LM Studio, vLLM ou Ollama sont des
  providers optionnels choisis par l’auteur ; le mode déterministe hors ligne reste le défaut ;
- les transports sont éprouvés contre leurs contrats HTTP avec des serveurs factices locaux. La
  compatibilité d’un modèle précis dépend de son respect du schéma JSON configuré ;
- l’index de lore est prêt à alimenter les workflows de génération, mais G12-B n’applique encore
  aucune géométrie, quête ou dialogue produit par modèle ;
- la classification détaillée des projets et la rédaction automatique de termes sensibles restent
  à compléter avec `ORF-AI-013`, qui n’est pas promu artificiellement à `Tested` par ce lot.

## Critère de passage

G12-B est fermé lorsque transports, lore, UI, MCP, persistance, tests ciblés, smoke Toolset,
tracker, documentation, graphe et `scripts/Verify.ps1` sont verts sur le même état Git. Le prochain
lot borné est **G12-C — plan de zone assisté, sans génération de géométrie avant validation**.
