# G7-12 — Cache ForgeScript et build incrémental

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-PROJ-021` — scripts mis en cache ;
- `ORF-PAL-015` — dépendances automatiques ;
- `ORF-BUILD-005` — build incrémental.

## Résultat livré

`ForgeScriptArtifactCache` calcule une clé SHA-256 à partir du hash de source, de la version d'API et
de la version du compilateur. Chaque entrée possède un UUID déterministe, un schéma v1, une
provenance, le hash source et le hash bytecode. Métadonnées et bytecode sont écrits atomiquement ;
une version ou une intégrité incorrecte provoque une recompilation. Le runtime emploie ce cache pour
les scripts de module et journalise chaque hit ou miss.

Le modèle canonique de build fournit des ressources à UUID stable, type, chemin, hash, licence,
provenance et dépendances résolues dans un graphe acyclique v1. Le builder de workspace relie les
scripts aux zones qui les utilisent, les documents au package final et les blueprints installés
réellement utilisés à leur pack d'origine. Ces packs alimentent automatiquement le manifest
`.orpmod` minimal et trié.

Le planificateur compare le graphe courant au dernier état `.forge/build-graph.json`, propage les
changements vers les consommateurs et sépare ressources recalculées, réutilisées et retirées. Le
cache est jetable et exclu des packages. L'export et le CLI exposent le résultat ; `package analyze`
produit une sortie JSON structurée sans modifier le projet.

Dans le Creator, le workspace Scripts ouvre une fenêtre Aurora dédiée : cartes Graphe, Impact et
Cache ForgeScript, liste `REBUILD`/`CACHE`/`EXTERNE`, hashes courts, licences, provenance et packs
automatiques. L'analyse peut être actualisée sans lancer de build.

## Preuves

- `BuildDependencyGraphTests` couvre UUID déterministes, impact transitif, références et cycles ;
- `ForgeScriptArtifactCacheTests` couvre miss/hit, invalidation source/compilateur et corruption ;
- `WorkspaceBuildGraphTests` couvre usages script-zone, état persistant ou corrompu, rebuild minimal,
  packs automatiques et deux exports successifs ;
- `OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS`, `OPENRPG_FORGE_BUILD_ANALYZE_PASS` et
  `OPENRPG_FORGE_FORGESCRIPT_CACHE` prouvent les intégrations Creator, CLI et runtime.

## Limites conservées

- le ZIP final reste réémis entièrement, même si ses entrées amont sont classées réutilisables ;
- les transformeurs futurs de modèles, textures, navigation et audio ne consomment pas encore le plan ;
- les imports transitifs internes au langage ForgeScript ne sont pas encore modélisés ;
- aucun cache distant ou partagé entre machines n'est introduit.

Les trois capacités passent au statut `Implemented` partiel.
