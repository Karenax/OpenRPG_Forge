# G11-C — Round-trip externe et cycle de vie sûr

## Périmètre

- `ORF-CONT-021` — External edit round-trip ;
- `ORF-CONT-022` — Blender bridge ;
- `ORF-CONT-025` — Compatibilité de plateforme ;
- `ORF-CONT-026` — Migrations de package ;
- `ORF-CONT-027` — Désinstallation sûre.

## Résultat livré

- configuration canonique v1 d’un round-trip externe, liée à l’UUID du catalogue et au manifeste ;
- commande transactionnelle avec révision, dry-run, patch inverse, undo/redo et sauvegarde atomique ;
- requête Blender JSON typée, déterministe et sans champ de script arbitraire ;
- worker Python confiné aux racines source/workspace, avec allowlist d’opérations et formats, SHA-256
  avant/après, source immuable, rapport JSON et codes de diagnostic stables ;
- point d’entrée Blender livré par le projet pour import OBJ/FBX/DAE/GLTF/Blend, normalisation,
  pivot au sol, collisions, LOD et export GLB ;
- sélection de variantes par plateforme et validation de parité des rôles requis ;
- plans purs de migration et désinstallation : code hôte de confiance, dry-run, sauvegarde,
  changelog, analyse des dépendants, remplacement et refus si l’impact reste non résolu ;
- commande CLI `assets roundtrip` et surface Content Browser avec état vide, surveillance,
  configuration Blender et undo/redo.

## Preuves

- tests domaine : round-trip, opérations interdites, parité, migration et désinstallation ;
- tests application : dry-run, application, conflit de révision, undo et redo ;
- tests ModuleFormat : persistance déterministe et requête worker dépourvue de code exécutable ;
- tests Python : confinement, allowlist, source immuable, Blender absent et hash invalide ;
- smoke réel sur l’OBJ `ambientcg:3DApple001` : préparation, manifeste, configuration C# puis
  validation de la requête par le worker Python sur le fichier du Vault ;
- `scripts/Verify.ps1` rejoue ce round-trip en mode validation sans modifier le Vault.

## Limites explicites

- Blender n’est pas installé sur la machine de validation : l’invocation headless et le script `bpy`
  sont livrés, mais la preuve de production d’un nouveau GLB par Blender reste à exécuter dès que le
  binaire optionnel est disponible ;
- le watcher de fichiers est configuré canoniquement mais son service temps réel reste à raccorder ;
- les migrations et désinstallations sont analysées et refusées de manière sûre ; leur application
  physique avec création/restauration de sauvegarde sera livrée avec le gestionnaire de packs ;
- les éditeurs image/audio restent hors de cette tranche 3D ;
- aucun outil de création externe n’est requis ni chargé par le runtime joueur.

## Critère de passage

G11-C est fermé lorsque le build, les tests ciblés, le smoke CLI/worker, le smoke Toolset, la revue
visuelle, le tracker Aurora, le graphe et `scripts/Verify.ps1` sont verts sur le même état Git.
