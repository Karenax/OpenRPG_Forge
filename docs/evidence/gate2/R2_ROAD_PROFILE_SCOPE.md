# R2-022 — portée constatée du profil de route

## Parcours Studio acquis — 15 septembre 2026

Candidat 284B775A61486FA858BA6C61691CBFAC1A8DABA1525CE0DF5B1D4C6132F03BF7,
réutilisé sans rebuild, copie isolée `E:/OpenRPG_Forge/.gate2-evidence/road-profile-ui-20260915`.
Chemin e7a8bec0-82a9-59d9-b938-cf650d2d9d05 : largeur 3 → 6 m,
accotement 1 m et deux points conservés. Examiner affiche les valeurs avant/après ;
aperçu cyan visible. Appliquer élargit la surface, Annuler la rétrécit,
Rétablir l'élargit à nouveau. Sauvegarder retourne « Session synchronisée ».

Archive de zone 81de8d42-89b9-58b5-b090-406cf4c63d06 relue sur disque :
SHA-256 5B81DDA3EF207F94B869DEB18ED2DC31B7DABDE02816087D28B684ED11A3FA89,
largeur 6, accotement 1, points (-6.3684387, 11.61441) et (13.870575, 92.470436).
Captures locales : width-applied.jpg, width-undone.jpg, width-saved.jpg.
Fenêtre fermée après Save ; PID 8120 et WebView 11612/17040/23108/2736/8888/21480
absents au contrôle final. Aucun fichier de la scène source édité.

Portée : geste de largeur, aspect distant, Undo/Redo et sauvegarde ; pas de
réouverture GUI ni de qualification des virages, intersections, pentes et
accotements variés. Ce parcours ne clôt pas R2-022 à lui seul.

Inspection du code courant le 15 septembre 2026, sans build ni mutation de carte.

## Implémenté

`crates/forge_project/src/zone_authoring/paths.rs` : le document AuthoredPath
conserve ID, nom, points X/Z, largeur, accotement et verrou. La largeur est
bornée à 0,1–64 m, l’accotement à 0–32 m. Les retouches passent par le bus
Insert/Replace/SetLocked/Remove déjà utilisé pour les chemins.

`crates/forge_world/src/path_surface.rs` : section transversale et raccords de
virages, maillage échantillonné sur le terrain, avec limite de sommets et refus
des replis. La position verticale est explicitement surface.height_m + 0.02.
L’accotement contrôle l’alpha ; il ne modifie pas l’altitude du terrain.

`apps/forge_studio/src/authoring_paths.rs` : largeur et accotement modifiables
dans le brouillon, puis examen et application. Ce sont des règles réutilisables
sur chaque tracé, pas une retouche de la seule scène de preuve.

## Non démontré / non implémenté par ces sources

Pas de profil vertical indépendant, de terrassement, de dévers ou de limite
de pente dans AuthoredPath. Ne pas annoncer ces fonctions sous le mot profil.
L’inspection seule ne qualifie pas l’aspect ni les raccords d’une route réelle.
Les preuves de chemins ne valent pas automatiquement verdict R2-022.

La roadmap ne précise pas les composantes supplémentaires attendues du profil.
Le prochain verdict doit nommer la portée examinée (largeur/accotement sur
terrain) ; toute extension verticale doit conserver les retouches, l’atomicité,
Undo/Redo et des collisions concordantes. Aucun nouveau critère obligatoire
ni réduction du périmètre de gate décidé par cette note. R2-022 reste ouvert.
