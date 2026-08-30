# Conventions d’assets — OpenRPG Forge

Ce document définit le contrat minimal des assets 3D du MVP. Il s’applique au kit provisoire
**Crypte des Cendres** et prépare son remplacement progressif par des modèles importés.

## Repère et dimensions

- une unité de scène représente un mètre ;
- l’axe vertical est `Y` et l’avant conventionnel d’un asset est `-Z` ;
- les modèles sont livrés à l’échelle `(1, 1, 1)`, transformations appliquées ;
- le pivot par défaut d’un prop ordinaire posé est centré sur sa base, au niveau du sol ; une porte,
  un module ou un élément de surface utilise respectivement un pivot de gond, de snap ou d’attache ;
- les dimensions modulaires utilisent de préférence un pas de `0,5 m` ou `1 m`.

## Identité et rangement

- les slugs sont en anglais, minuscules et `lower-kebab-case`, par exemple `crypt-pillar` ;
- les noms affichés peuvent être localisés et ne servent jamais d’identifiant technique ;
- les identifiants persistants restent des UUID opaques ;
- les tags structurels du kit sont `floor`, `wall`, `door`, `pillar` et `solid` ;
- les sources de travail, exports et métadonnées de licence restent séparés.
- chaque asset importé possède un profil d'intelligence v1 liant description, sens, placement,
  mesures, état de revue, confiance, preuves et SHA-256 du modèle et de ses dépendances ;

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

## Entrées de bâtiment

- un encadrement de porte porte les rôles `doorway` et `entrance` ;
- une porte mobile porte `interactive-door` et `entrance`, commence fermée dans l'ouverture et garde
  une collision active ; les tags décoratifs `open-door` et `nonblocking` lui sont interdits ;
- la porte expose `interaction.click.action=toggle-door`, utilise un pivot de gond et reste présente
  lorsqu'elle est ouverte ou refermée ;
- l'escalier extérieur porte `entrance-stairs` et `entrance`, partage la rotation de l'encadrement et
  place sa marche haute au seuil ;
- l'alignement se calcule sur les bornes réelles des maillages : le pivot de gond est volontairement
  décalé du centre de l'ouverture, mais le centre visuel de la feuille fermée et celui de l'escalier
  restent chacun dans l'axe à `0,10 m` près ; comparer seulement les origines des entités est interdit ;
- le build refuse une maison générée dont un rôle manque ou dont les tolérances d'alignement sont
  dépassées ; l'audit Forward+ vérifie les maillages, les collisions, puis produit des preuves
  rapprochées distinctes porte fermée et porte ouverte.

## Contrôle avant intégration

1. Vérifier axes, échelle et pivot suggéré dans l’aperçu Forge ; une suggestion automatique ne vaut
   jamais revue humaine.
2. Vérifier le nom, le slug, les tags et l’UUID.
3. Vérifier visuel et collision dans le runtime.
4. Exécuter `scripts/Verify.ps1`.
5. Documenter provenance et licence avant toute distribution.

Le kit T2.4 respecte ce contrat avec un sol, quatre murs, une porte et quatre piliers, soit dix
formes de collision statiques dans la zone de démonstration.
