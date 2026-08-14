# Architecture IA et MCP

L’IA est facultative et interchangeable. Elle ne reçoit aucune voie directe de mutation des fichiers de projet ou de module.

```text
Client MCP / LLM
       |
       v
 Forge.McpHost
       |
 canal local authentifié
       |
       v
Forge.EditorBridge
       |
       v
bus de commandes transactionnel
```

## Workflow de mutation

```text
contexte -> plan structuré -> validation -> dry-run -> diff
        -> approbation -> application -> validation -> test -> audit
```

Les lectures, plans et applications ont des niveaux d’autorisation distincts. Une application exige un `plan_id` lié à la révision de base ; elle produit un patch inverse ou un snapshot.

## Surface initiale

Commencer par quelques ressources read-only : résumé du projet, révision, sélection, contenu ciblé et diagnostics. Les premiers outils couvrent lecture, validation, planification de dialogue et application/réversion. Compilation de scripts, scénarios headless et pipelines 3D viennent après stabilisation de leurs workflows manuels.

Les providers de texte, image, 3D, voix et embeddings sont abstraits. Aucun modèle ou fournisseur n’est une dépendance du cœur.

## Prototype livré

`apps/Forge.McpHost` utilise le SDK C# officiel `ModelContextProtocol` 1.4.1 et communique en JSON-RPC
sur `stdio`. Les journaux sont isolés sur stderr. La surface testée comprend :

- `get_project_summary`, `get_current_revision` et `get_project_diagnostics` en lecture seule ;
- le schéma, la préparation, l'application et la réversion d'un plan de dialogue ;
- `query_world_features` filtre les profils canoniques du Monde vivant et
  `preview_world_features` valide/dry-run un catalogue v1 sans l'appliquer ;
- `forge_get_post_launch_capabilities` lit une section du profil post-lancement et
  `forge_preview_post_launch_capability` simule mobilité, protocole, génération ou soak test ;
- l'API ForgeScript, la compilation, les diagnostics, le test sandboxé et le plan de liaison ;
- un agent de scénario borné qui retourne résultat, marqueurs, diagnostics et journal d'artefact.

Le pont local `ForgeEditorBridgeServer` publie une découverte atomique et utilise un pipe nommé
`CurrentUserOnly`, un jeton de session aléatoire de 256 bits, une expiration et une comparaison en
temps constant. Il accepte plusieurs connexions successives et ne crée aucune écoute TCP.

Exemple de démarrage du host depuis la racine du dépôt :

```powershell
dotnet run --project apps/Forge.McpHost -- --workspace apps/Forge.Runtime/content/crypt-of-ashes
```

Le scénario d'intégration complet est exécuté par `scripts/Verify.ps1` et produit
`artifacts/verification/mcp-scenario.log`.

La génération VFX/audio/image reste un plan gouverné : provider allowlisté, droits, provenance,
quota, staging et fallback. MCP ne reçoit aucun accès direct à un fournisseur ni aux fichiers.

## Contrat de création de maps

La planification assistée de zones utilise le contrat versionné
`docs/ai/MAP_AUTHORING_CONTRACT.md`. `Forge.Application` l'embarque afin que Creator et MCP injectent
exactement le même contenu et son SHA-256 dans `area-plan@2.0.0`. Les données changeantes du module
restent dans un paquet JSON borné séparé : brief, zone cible, blueprints allowlistés, terrain,
navigation, entités existantes et lore autorisé.

Un provider de modèle doit retourner les nœuds et connexions du schéma JSON. Forge attribue les UUID
stables, rejette les références inventées et exécute les validations de domaine avant tout dry-run.
Une sortie invalide ne déclenche aucune mutation et n'est pas remplacée silencieusement. Le provider
`DeterministicOffline` conserve sa topologie locale reproductible pour les tests et le fonctionnement
sans IA.
