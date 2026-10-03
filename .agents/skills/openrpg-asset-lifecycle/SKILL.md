---
name: openrpg-asset-lifecycle
description: "Portée : checkout historique C#/Godot uniquement. Gouverner le cycle de vie d'un asset OpenRPG Forge depuis l'acquisition jusqu'à la preuve visuelle et runtime. Utiliser pour les demandes d'Asset Vault, téléchargement CC0, licence, hash, catalogue, import 3D, GLB, Blender Worker, manifeste, collision, LOD, thumbnail, installation de pack, biome ou décoration de terrain. Toujours distinguer catalogué, téléchargé, normalisé, installé et prêt en production. Pour Rust, consulter ses instructions propres sans transposer les chemins ou validations historiques."
---

# Gouverner le cycle de vie d'un asset

Conserver provenance, reproductibilité et qualité sans ajouter les fichiers volumineux externes au dépôt par défaut.

## Classer l'état initial

Déterminer et annoncer le dernier état réellement prouvé :

1. référencé dans un catalogue ;
2. téléchargé et vérifié ;
3. normalisé pour Forge ;
4. installé dans un workspace ;
5. visible dans le Toolset ou le runtime ;
6. validé en production avec collision, LOD, performance et provenance.

Ne jamais assimiler un état à l'étape suivante.

## Préparer l'acquisition

1. Lire `AGENTS.md`, `docs/ASSET_CONVENTIONS.md`, `docs/ASSET_LICENSES.md`, `docs/assets/ASSET_PIPELINE_USAGE.md` et le document de tâche concerné.
2. Privilégier la source officielle et conserver URL, auteur, licence, version, date et hash.
3. Stocker le contenu volumineux dans l'Asset Vault externe configuré par `OPENRPG_ASSET_VAULT`. Garder dans Git uniquement code, manifestes, métadonnées et preuves de licence nécessaires.
4. Valider chemins, taille, type réel, archive et extraction sûre avant toute installation.
5. Préserver les snapshots tiers immuables ; placer les adaptations dans les répertoires Forge.

## Normaliser et installer

1. Produire un identifiant stable et une provenance traçable.
2. Normaliser vers GLB/glTF en conservant les textures et licences nécessaires.
3. Vérifier unités, axe, échelle, pivot, orientation, matériaux PBR, UV, animations et compatibilité Godot.
4. Générer ou documenter collisions, LOD, thumbnail et budgets applicables.
5. Passer les mutations par les services et commandes du produit ; ne pas écrire directement dans un module via une interface IA.
6. Pour Blender Worker ou un fournisseur 3D, vérifier schéma typé, confinement des chemins, réseau autorisé, timeout, résultat, hash et receipt. Distinguer dry-run, requête produite et véritable GLB généré.

## Prouver l'intégration

1. Utiliser `$openrpg-targeted-verification` avec le profil `Assets` ou les fichiers exacts pour
   exécuter les tests du pipeline et de `Forge.ModuleFormat` sans ouvrir les autres surfaces.
2. Vérifier l'intégrité des manifestes et du package.
3. Charger l'asset dans le Toolset ou le runtime approprié.
4. Utiliser `$openrpg-prototype-visual-review` pour une preuve visuelle quand le rendu compte.
5. Utiliser `$openrpg-delivery-gate` seulement pour un jalon transversal ou une préparation de
   publication, pas après chaque asset isolé.

Rapporter séparément licence, intégrité, normalisation, installation, preuve visuelle, preuve runtime, performances et travail restant.
