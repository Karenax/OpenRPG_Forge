# G12-J — Création 3D assistée via Blender

## Portée

- exigence : `ORF-AI-021` ;
- décrire un asset par une recette canonique de primitives et transforms bornés ;
- configurer matériau PBR, projection UV, LOD, collision, validation, miniature et export GLB ;
- interdire le Python libre et limiter le worker à une liste d'opérations typées ;
- conserver recette, configuration d'édition externe et requête worker dans une transaction atomique ;
- partager proposition, dry-run, permissions, application et revert entre Toolset et MCP.

## Résultat livré

Le service G12-J produit une `AssistedBlenderCreationDefinition` à UUID stable, schéma v1,
provenance et licence CC0 explicites. Une recette contient de une à 64 primitives cube, cylindre,
sphère ou cône ; position, rotation et échelle sont bornées. Le matériau métallique/rugosité,
la couleur RGBA, la projection UV, un à quatre ratios de LOD, la collision et le chemin de sortie
`assets/generated/*/model.glb` sont validés avant toute écriture.

La fenêtre **Création 3D assistée** expose ces champs, les règles de style, citations, coût,
diagnostics, cinq preuves et diff sémantique. Une correction invalide le plan. Après approbation,
le handler écrit atomiquement la recette, `assets/external-edits.json` et la requête worker ;
undo/redo restaure les trois fichiers et l'état canonique. Les quatre outils MCP
propose/plan/apply/revert utilisent le même service et le même handler.

Le worker Python ne transporte aucun programme généré. Il accepte uniquement
`create_primitives`, `validate_model`, `normalize_model`, `generate_lods`,
`generate_collision`, `export_glb`, `generate_thumbnail` et `audit_quality`. Le pont Blender
construit réellement les meshes et le matériau, génère UV, LOD et collisions, puis exporte un GLB,
une miniature PNG et un rapport qualité. La recette source est hachée et relue avant exécution ;
une divergence avec la requête bloque le job.

## Non-objectifs

- aucun script Python proposé par un auteur ou un agent n'est exécuté ;
- Blender n'est jamais une source de vérité canonique ;
- aucun fournisseur text/image-to-3D réseau n'est requis ; ce périmètre optionnel commence en G12-K
  (`ORF-AI-022`) ;
- l'exécution Blender reste un worker externe de production, pas une dépendance du runtime de jeu.

## Preuves

- 3 tests ModuleFormat : recette canonique, gouvernance, dry-run exact, persistance, altération,
  révision et undo/redo ;
- 13 tests du worker, dont recette source immuable, opérations interdites et validation sans Blender ;
- test MCP propose/plan/apply/revert avec restauration des trois fichiers ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G12J_BLENDER_PASS` ;
- smoke Blender 4.5 réel : `OPENRPG_FORGE_G12J_BLENDER_CREATION_PASS`, avec 2 primitives,
  4 LOD, 2 collisions, 136 triangles, miniature 512 × 512 et GLB 2 cohérent ;
- `scripts/Verify.ps1` protège les deux marqueurs dans la porte intégrée.
