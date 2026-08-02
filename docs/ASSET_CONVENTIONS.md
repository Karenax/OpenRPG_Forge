# Conventions d’assets — OpenRPG Forge

Ce document définit le contrat minimal des assets 3D du MVP. Il s’applique au kit provisoire
**Crypte des Cendres** et prépare son remplacement progressif par des modèles importés.

## Repère et dimensions

- une unité de scène représente un mètre ;
- l’axe vertical est `Y` et l’avant conventionnel d’un asset est `-Z` ;
- les modèles sont livrés à l’échelle `(1, 1, 1)`, transformations appliquées ;
- le pivot d’un élément posé est centré sur sa base, au niveau du sol ;
- les dimensions modulaires utilisent de préférence un pas de `0,5 m` ou `1 m`.

## Identité et rangement

- les slugs sont en anglais, minuscules et `lower-kebab-case`, par exemple `crypt-pillar` ;
- les noms affichés peuvent être localisés et ne servent jamais d’identifiant technique ;
- les identifiants persistants restent des UUID opaques ;
- les tags structurels du kit sont `floor`, `wall`, `door`, `pillar` et `solid` ;
- les sources de travail, exports et métadonnées de licence restent séparés.

## Géométrie et matériaux

- les primitives internes sont acceptées comme brouillons libres de dépendance externe ;
- un futur modèle importé cible le format glTF binaire (`.glb`) avec normales cohérentes ;
- les matériaux provisoires utilisent une palette sobre et ne dépendent d’aucune texture externe ;
- chaque asset importé doit déclarer sa provenance, son auteur et une licence compatible avec le projet ;
- un asset visible ne doit pas contenir de logique de gameplay embarquée.

## Collisions

- tout élément portant le tag `solid` possède une collision runtime ;
- la collision est une forme simple distincte du maillage visuel : boîte pour sol, mur et porte,
  cylindre pour pilier ;
- la forme suit l’échelle de l’instance et son pivot doit coïncider avec celui du visuel ;
- les collisions concaves sont exclues du kit MVP ;
- le smoke test runtime vérifie le nombre de formes statiques reconstruites.

## Contrôle avant intégration

1. Vérifier axes, échelle et pivot dans l’aperçu Forge.
2. Vérifier le nom, le slug, les tags et l’UUID.
3. Vérifier visuel et collision dans le runtime.
4. Exécuter `scripts/Verify.ps1`.
5. Documenter provenance et licence avant toute distribution.

Le kit T2.4 respecte ce contrat avec un sol, quatre murs, une porte et quatre piliers, soit dix
formes de collision statiques dans la zone de démonstration.
