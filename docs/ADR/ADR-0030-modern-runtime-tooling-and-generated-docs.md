# ADR-0030 — Outillage runtime moderne et documentation générée

Date : 5 août 2026
Statut : accepté

## Décision

La copie dynamique, les indicateurs de ciblage, le toolkit NUI, le compilateur et la documentation
sont configurés dans `ModernRuntimeToolingProfile`, sous le profil d’extensions runtime du module.
Le Creator modifie ce document avec la commande transactionnelle G11-O existante ; le runtime,
ForgeScript, le CLI et MCP consomment le même contrat versionné.

La copie runtime produit toujours un nouvel UUID, applique quota et autorité, choisit explicitement
ownership, références et composants d’état. L’indicateur AOE sépare la sélection déterministe des
cibles du mesh moteur jetable. NUI 2.0 étend les widgets sans exposer de nœud moteur aux scripts.
Le compilateur ordonne les dépendances, invalide transitivement son cache et stabilise les
diagnostics. Les six documents de `docs/generated` sont reconstruits depuis les métadonnées source
et protégés par un mode `check` non mutant.

## Conséquences

- projet `0.6.18` et workspace `1.12.0` migrent les politiques absentes vers des UUID déterministes ;
- le cache de compilation et les meshes restent jetables ;
- la confirmation serveur demeure obligatoire par défaut pour copie et ciblage ;
- les fichiers de `docs/generated` ne sont jamais édités à la main ;
- les formes AOE utilisent une géométrie de prévisualisation générique, remplaçable par des decals
  artistiques sans modifier la sélection canonique.
