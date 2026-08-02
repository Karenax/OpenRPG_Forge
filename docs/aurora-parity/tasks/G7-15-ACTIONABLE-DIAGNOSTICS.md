# G7-15 — Diagnostics actionnables

Date d’exécution : 2 août 2026.

## Capacité bornée

- `ORF-QA-021` — diagnostics identifiables, localisés, navigables et corrigibles sans quitter Forge.

## Résultat livré

`ProjectDiagnosticCatalog` dérive désormais un contrat canonique indépendant du moteur. Chaque
diagnostic possède un UUID déterministe, un schéma v1, une sévérité typée, un code, un message, un
chemin de document, l’UUID du sujet, la propriété, la source du validateur et une provenance. Le
catalogue réunit la validation du projet, de la géométrie et de la navigation, déduplique puis ordonne
les résultats de façon stable.

Trois correctifs guidés sont disponibles lorsque la mutation est sûre : ramener une entité dans les
limites de sa zone, créer un `PlayerSpawn` manquant et alléger une grille de navigation trop dense.
`ApplyDiagnosticQuickFixHandler` revalide l’identité courante du diagnostic et du correctif, refuse
les références périmées et produit dry-run, patch, inverse, audit, undo et redo sur le document
canonique.

La coque Aurora affiche les métadonnées utiles dans le diagnostic, cible l’entité ou la zone dans la
sélection partagée et propose un panneau **Correctif guidé** avec actions **Prévisualiser** et
**Appliquer**. La prévisualisation ne change pas la révision ; l’application emprunte exactement le
même bus de commandes que les autres outils du Creator. `forge package diagnose <workspace>` et la
ressource MCP `project.diagnostics` exposent le même modèle structuré, y compris les correctifs.

## Preuves

- `ActionableDiagnosticTests` vérifie identité stable, sévérité, sujet, propriété, source, provenance
  et correctif localisé ;
- `ApplyDiagnosticQuickFixHandlerTests` couvre dry-run, application, UUID de spawn déterministe,
  diagnostic périmé, undo et redo ;
- `ForgeProjectAgentServiceTests` couvre le contrat MCP canonique et ses correctifs ;
- `OPENRPG_FORGE_DIAGNOSTICS_PASS` couvre la commande CLI sur le module de référence ;
- `OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_PASS` couvre navigation, aperçu, application, historique et
  round-trip dans le prototype Creator.

## Limites conservées

- les profils de validation, catégories, filtres et export d’un bundle de diagnostic restent à
  livrer ;
- seuls trois problèmes spatiaux disposent d’un correctif automatique ; les autres diagnostics
  restent navigables et explicites ;
- l’application d’un correctif n’est pas encore exposée comme mutation MCP dédiée ; MCP publie son
  identité et sa description pour une planification sûre ;
- les diagnostics d’import d’assets rejoindront ce catalogue avec le Content Browser.

La capacité passe au statut `Implemented` partiel et il ne reste plus aucune P0 Core au statut
`NotStarted`.
