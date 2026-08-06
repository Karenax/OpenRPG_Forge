# G12-A — Assistant intégré, fournisseurs et prompts versionnés

## Périmètre

- `ORF-AI-001` — Assistant intégré au Toolset ;
- `ORF-AI-012` — fournisseurs interchangeables ;
- `ORF-AI-030` — prompts versionnés.

## Résultat livré

- profil Assistant canonique v1, persisté dans `assistant/profile.json`, avec UUID stables,
  provenance, permission maximale, politiques réseau et partage du contenu projet ;
- fournisseur hors ligne déterministe utilisable en CI et contrats extensibles pour une API
  compatible OpenAI, LM Studio, vLLM et Ollama, sans service payant ni connexion obligatoire ;
- configuration Aurora 2026 depuis le workspace Assistant : type de fournisseur, endpoint, modèle,
  nom de variable d’environnement du secret, budgets, réseau et partage du contexte ;
- contexte explicite du projet, de la sélection et de la transaction ; aucun secret n’est persisté ;
- prompts avec version, gabarits système/utilisateur, schéma JSON de réponse, capacités requises,
  tests déterministes et changelog ;
- prévisualisation produisant un plan lié à la révision et à l’empreinte exacte du profil ;
- application refusée sans plan prévisualisé, permission suffisante et validation réussie ;
- undo/redo, sauvegarde atomique et rechargement exact du profil ;
- identité du fournisseur, version et hash du prompt, permission, provenance et coût estimé visibles
  avant application.

## Preuves

- `AssistantWorkspaceProfileTests` couvre valeurs sûres et diagnostics de gouvernance ;
- `AssistantWorkspaceProfileCommandTests` couvre dry-run, liaison du plan, permission, historique,
  undo et redo ;
- `AssistantPromptAndProviderTests` couvre rendu déterministe, tests de prompt, hash, fournisseur
  hors ligne et refus explicite d’un fournisseur non installé ;
- `AssistantWorkspaceProfileStoreTests` couvre profil absent, round-trip JSON atomique et rejet
  d’une configuration invalide ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_PASS` sur le workspace réel ;
- `scripts/Verify.ps1` exige ce marqueur et conserve le mode hors ligne comme preuve de base.

## Limites explicites

- l’adaptateur hors ligne est exécutable ; les transports HTTP compatibles OpenAI et Ollama sont
  configurables mais restent à installer dans G12-B avec consentement réseau, timeout, annulation et
  tests contre des serveurs factices locaux ;
- l’assistant configure aujourd’hui son propre profil. Il ne génère pas encore de zone, dialogue,
  quête, capacité ou ForgeScript ; ces workflows restent des lots G12 séparés réutilisant les
  commandes manuelles existantes ;
- le schéma de réponse impose un plan et un diff sémantique, mais aucun texte produit par un modèle
  n’est appliqué directement au projet ;
- cette fondation ne remplace aucun workflow manuel et reste désactivable sans dégrader Creator.

## Critère de passage

G12-A est fermé lorsque tests ciblés, smoke Toolset, persistance, tracker, documentation, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git. Le prochain lot borné est **G12-B — transports
locaux compatibles OpenAI/Ollama et index de lore gouverné**.
