# R1 — Normales de rivière indépendantes du maillage

2026-09-06, branche Rust, arbre modifié conservé. Gate 1 ouverte, CI NOT RUN.

## Implémentation

Le shader conserve aux sommets la normale de base et calcule le gradient analytique des
deux vagues par fragment avant la préparation PBR, dans les branches forward et deferred.
Il n'interpole plus un gradient de vagues échantillonné seulement aux sommets des bandes
de rivière. Déplacement des sommets, phases courante/précédente, amplitudes, paramètres,
profondeur et documents authorés inchangés. Aucun asset ni dépendance ajouté.

Cette règle est partagée par les surfaces utilisant ce matériau ; aucune coordonnée ou
exception spécifique à la carte n'est introduite. Ce n'est pas une simulation hydrodynamique.

## Exécution et preuves

La première compilation a échoué avec `os error 112` : disque C: saturé. Aucun fichier
existant supprimé. Copie non destructive de 26 349 fichiers (18,250 Gio) du cache temporaire
vers `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906`, zéro échec Robocopy.
Le cache original reste sur C:. TEMP/TMP de ces commandes pointent vers le sous-dossier
voisin `terrain-water-temp-20260906`. Build natif sur E: PASS (1 min 22 s).

Premier lancement `r1-water-fragment-normals-20260906` : échec conservé, pipelines encore
en préparation avant la sortie à 360 images, aucune capture qualifiée. Le collecteur expose
désormais les bornes explicites avec `120 < capture-frame < exit-after-frames <= 3600` ;
valeurs par défaut inchangées, timeout réel 120 s inchangé et refus de readiness conservé.

Rejeu High, kit/forêt candidat courant, vue héroïque clair/pluie : capture à 900, sortie à
1800 images. Deux sorties 0, contrôles du collecteur PASS, deux PNG Full HD inspectés.
Les vagues croisées ne sont plus uniformément étirées sur toute la largeur près du pont.
Le motif reste périodique et le bord sombre de la rivière reste visible. La phase temporelle
diffère des anciennes captures à 240 images : pas de comparaison pixel à pixel synchronisée.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-fragment-normals-20260906-b`.
Manifeste : `3c52406d10d8784327e8a665377417900205497bd53e14e3f214602db414ac60`.
Runtime : `7ad39a7926cc46c8b27162ed2fa447e6731cf03074887fe76e97de33b29998b6`.
Le collecteur confirme les sources inchangées et la projection des budgets disponibles,
pas le preset complet ni les performances. Aucun gain FPS revendiqué.

`cargo test -p forge_engine --lib water:: --locked` : **26 PASS**, sortie 0, 2,82 s.
Test du collecteur vidéo PASS ; borne capture=120 refusée avec code 2 avant accès aux
entrées. Diff check PASS. Les 451 tests workspace précédents ne sont pas présentés comme
rejoués après ce changement WGSL. Pas de nouvelle qualification navigateur, mouvement,
anti-aliasing distant ou art humaine ; les deux branches modifiées ne sont pas qualifiées
sur toutes les combinaisons de pipelines. Tous les processus de ce lot sont terminés.
