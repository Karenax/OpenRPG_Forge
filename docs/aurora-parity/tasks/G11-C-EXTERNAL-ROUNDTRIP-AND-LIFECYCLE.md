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
  pivot au sol/centre, collisions, LOD, export GLB, miniature et audit qualité ;
- sélection de variantes par plateforme et validation de parité des rôles requis ;
- plans purs de migration et désinstallation : code hôte de confiance, dry-run, sauvegarde,
  changelog, analyse des dépendants, remplacement et refus si l’impact reste non résolu ;
- commandes CLI `assets roundtrip` et `assets finalize-roundtrip`, puis surface Content Browser avec
  état vide, surveillance, configuration Blender, métriques finales et undo/redo.

## Preuves

- tests domaine : round-trip, opérations interdites, parité, migration et désinstallation ;
- tests application : dry-run, application, conflit de révision, undo et redo ;
- tests ModuleFormat : persistance déterministe et requête worker dépourvue de code exécutable ;
- tests Python : confinement, allowlist, source immuable, transport d'arguments sûr sous Windows,
  remontée des erreurs `bpy`, Blender absent et hash invalide ;
- smoke réel sur l’OBJ `ambientcg:3DApple001` : préparation, manifeste, configuration C# puis
  validation de la requête par le worker Python sur le fichier du Vault ;
- Blender LTS 4.5.10 produit réellement un GLB 2 de 1 696 444 octets depuis
  `ambientcg:3DApple001`, avec collision et deux LOD ; le SHA-256 source reste
  `500623c45cdd14514fae358b646724c49b78dad7a601be862fbfc3782f040c95` et le GLB produit porte le
  SHA-256 `0f582c05e5479780c1cb14f1fda4197c375d5f1dc384820ab94dd4dbc7572e1c` ;
- une miniature locale 512 × 512 confirme visuellement géométrie et texture ; ses métadonnées
  variables sont retirées pour rendre son SHA-256 reproductible ;
- l’audit réel compte 500 triangles, deux LOD, une collision, aucune texture manquante et produit un
  score global de 99/100 ;
- le finaliseur persiste dimensions, métriques, styles, variante, compatibilité et preuves de sidecars
  après vérification transactionnelle ;
- `scripts/Verify.ps1` exécute ce round-trip lorsque Blender est détecté et émet
  `OPENRPG_FORGE_G11C_BLENDER_SMOKE_PASS` et `OPENRPG_FORGE_AV2_NORMALIZATION_SMOKE_PASS` ; sans
  Blender, il conserve le mode validation confiné.

## Limites explicites

- le watcher de fichiers est configuré canoniquement mais son service temps réel reste à raccorder ;
- les migrations et désinstallations sont analysées et refusées de manière sûre ; leur application
  physique avec création/restauration de sauvegarde sera livrée avec le gestionnaire de packs ;
- les éditeurs image/audio restent hors de cette tranche 3D ;
- aucun outil de création externe n’est requis ni chargé par le runtime joueur.

## Critère de passage

G11-C est fermé lorsque le build, les tests ciblés, le smoke CLI/worker, le smoke Toolset, la revue
visuelle, le tracker Aurora, le graphe et `scripts/Verify.ps1` sont verts sur le même état Git.

**Critère atteint le 5 août 2026** avec Blender 4.5.10 LTS, le GLB 2 inspecté et le portail global.
