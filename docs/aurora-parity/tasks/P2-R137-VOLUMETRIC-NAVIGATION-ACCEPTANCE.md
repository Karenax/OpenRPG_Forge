# P2 R137 — Navigation volumétrique

`ORF-AREA-032` disposait déjà du profil 3D vol/nage/espace, de la grille voxel, des coûts,
obstacles, A* et overlay. Le palier ferme sa limite runtime avec un contrôleur déterministe : suivi
de waypoint 3D, vitesse bornée, séparation de foule ordonnée par UUID, évitement dynamique et arrêt
avant cellule bloquée, avec diagnostics stables.

Tests ciblés : `VolumetricNavigationTests` (3/3). Preuve Toolset :
`OPENRPG_FORGE_TOOLSET_R135_AREA_PRODUCTION_PASS` avec contrat `volumetric=...`.

P3/P4, AV7–AV10, commit et push sont exclus.
