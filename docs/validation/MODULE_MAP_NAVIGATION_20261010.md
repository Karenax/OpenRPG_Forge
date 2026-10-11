# Navigation modules et cartes — 10 octobre 2026

Le panneau de l’éditeur présente un sélecteur **Module**, puis les **Cartes** de ce
module. Le choix d’une carte ouvre directement sa session d’édition gérée.
L’ouverture du workspace existant devient automatique et ne crée aucune autorité
absente. Les anciens formulaires d’ouverture/création/adoption et la liste legacy
des zones ne font plus partie de ce panneau.

Les listes utilisent les identités réelles des modules et des cartes. Elles
affichent les noms effectifs des cartes, y compris après renommage. Des modules
homonymes affichent aussi le nom de leur projet. Les retouches appliquées mais non
enregistrées restent dans les sessions natives lors d’un changement de carte.
Un aperçu non appliqué ou une session de jeu bloque temporairement ce changement.

L’ouverture d’une carte importée acquiert automatiquement ses ressources de
terrain et de décor. Avant leur utilisation, le chargement vérifie les hashes et
l’association du raster : dimensions, origine, pas d’échantillonnage, encodage et
étendue. Le cache déjà chargé doit lui aussi correspondre à la carte. Un changement
de carte pendant l’acquisition relance seulement le nouveau chargement nécessaire.

## Contenu courant et modèles du module

Le candidat reprend le contenu courant du package local
`Tidewater-Launcher-Fullscreen-20261008`, dont la carte canonique est épinglée à
`8c40c05fe5dcb02fa7bab00317414c6cdd0b7edce8c7e14f62ce1afa0718eaf6`.
Les quinze placements de maisons utilisent les quatre GLB actuels et leur
catalogue, plutôt que les anciennes maisons procédurales du seed personnel.
Le catalogue est épinglé à
`db2a133da336bbcde7587d75e1ba2a751a25f107e0ecab5a59920d3e6cf514c5`.

Le chargement de ces modèles passe par le résolveur commun du module : identité
explicite, pin du catalogue, pin du modèle, chemin relatif borné et vérification
GLB avant publication du flux binaire. Le protocole ne reçoit aucun chemin fourni
par la WebView. Une ressource corrompue ou un pin périmé constitue une erreur ;
ils ne sont jamais convertis en simple entrée absente. Le parcours Vault existant
reste disponible avec son propre protocole.

## Profil temporaire d’édition

Le profil `FastEditing` est installé dans le viewport d’édition. Il désactive
MSAA, les ombres de contact et les shadow maps Bevy sans modifier les sources de
la carte, ses placements, les matériaux GLB ou les collisions. La vue importée
coupe aussi les simulations et passes récurrentes de l’océan, de la réfraction,
des nuages, des cascades d’ombres et du vent décoratif. Les ressources initiales
nécessaires aux gardes de disponibilité restent chargées.

Le profil conserve la résolution et les outils de sélection. Le mode Winit
réactif utilise 16 ms en premier plan et 250 ms en arrière-plan ; ces délais ne
constituent pas un plafond strict de FPS. Aucun gain mesuré de performance n’est
encore revendiqué.

En l’absence de soleil Bevy, un éclairage directionnel temporaire de 30 000 lux
est ajouté une seule fois. L’éclairage de travail optionnel utilise un ambiant de
1 200 en édition, contre 250 dans le playtest. Ces réglages visent la lisibilité
des maisons PBR, tandis que le terrain importé utilise son propre éclairage de
source. Ils restent dérivés et ne changent ni le contenu enregistré ni
l’environnement du jeu distribué. Le binaire final a été reconstruit et sa
capture locale montre les maisons actuelles éclairées. Cette inspection locale
ne constitue pas une acceptation visuelle par le propriétaire.

## Sélection et vignettes des objets

La sélection au clic des placeables rendus ne dépend plus de la disponibilité
globale d’un autre objet de la scène. Le picker commun est prioritaire ; le picker
des plantes reste disponible lorsque le clic ne touche pas de placeable commun.
Les caméras d’aperçu et les caméras inactives sont exclues. L’étal importé conserve
des entrées d’éclairage valides lorsque le profil d’édition coupe le ciel.
L’inspecteur principal suit immédiatement la sélection courante, sans attendre
la synchronisation de la fenêtre secondaire.

Le bandeau **Objets de la carte**, affiché par défaut dans l’outil Objets, présente
les objets réellement rendus. Leurs aperçus de 192 × 192 pixels sont capturés
progressivement avec le GPU déjà utilisé par la vue. Le cache associe identité
d’asset et hash du contenu ; changer d’outil conserve les aperçus. Un clic sur une
vignette sélectionne et cadre l’objet avec la même façade d’édition. Le bouton
**Bibliothèque** conserve l’accès au catalogue d’assets.

## Validation locale

Les douze tests des suites `workspace_host`, `workspace_tabs`,
`workspace_adoption` et `workspace_creation` passent sur le candidat isolé. Ils
couvrent notamment l’absence de création implicite, la consultation répétée sans
écriture canonique, les noms effectifs, les modifications non enregistrées, le
retour à une carte et la consultation pendant une session de jeu.

Les neuf tests `source_object_binding` passent également, ainsi que Clippy ciblé
strict pour cette suite. Les deux nouveaux cas vérifient l’association de cartes
issues de modules différents et le refus d’un mauvais pin ou d’une géométrie
différente.

Les neuf tests natifs du résolveur de modèles et les six tests du protocole
binaire passent sur leur source figée. Ils vérifient notamment les quatre GLB
réels, les pins, les chemins refusés, les erreurs de contenu et l’association
exacte de la réponse à sa requête. Le total de ces tests Rust ciblés est de
**trente-six** : douze workspace, neuf associations de source, neuf résolveur et
six protocole. Ils ne qualifient ni les pixels WASM ni le nouvel éclairage.
Clippy complet et Clippy du résolveur figé ne sont pas revendiqués ; sa preuve
locale précise la limite des deux paramètres privés passés par référence.

Les fichiers de navigation passent la vérification des espaces du diff. Les
travaux d’assets concurrents ne sont pas inclus dans le candidat. Les sources de
l’éditeur livré le 9 octobre servent de base figée. Les overlays de navigation,
de résolution des modèles et de rendu d’édition sont identifiés dans leurs
manifestes et preuves propres ; les assets statiques du frontend et le worker
d’assets de la base sont conservés.

Les preuves de reconstruction, de contrôle visuel et de mise à jour sont retenues
dans `E:\OpenRPG_Forge\.module-navigation-20261010\candidate-001`, notamment
`backend-tests.log`, `source-object-binding-tests.log`,
`frozen-module-model-resolver-proof.json` et `fast-editing-imported-proof.json`.
Les contrôles du binaire final sont retenus dans le dossier voisin
`candidate-002`. Les clics dans le canvas sélectionnent deux maisons et un objet
de décor importé ; l’inspecteur correspond à la sélection. Les quinze maisons
actuelles disposent de vignettes chargées. Le parcours UI prévisualisation,
application, sauvegarde, annulation puis sauvegarde a déplacé une maison de
0,25 m et restauré sa position dans une copie isolée, en conservant sa rotation
et son échelle non uniforme. Un changement Terrain → Objets conserve les 101
aperçus alors chargés sans modifier le document. Aucune erreur de console n’a
été observée pendant la fenêtre de contrôle.

Ces interactions sont automatisées dans la WebView privée du candidat. La
capture `final-selection-thumbnails.png` a été inspectée localement. Les trois
preuves `navigation-runtime-proof.json`, `fast-editing-runtime-proof.json` et
`selection-thumbnails-runtime-proof.json` épinglent le binaire
`293b6f44129e7878a338a6caac5583b745fa28171929b73ff8eb4fa9f584097f`
et le manifeste de ses 2 831 sources vérifiées. Un brouillon présent dans une
autre fenêtre est conservé ; ces tests n’écrivent pas dans cette copie.
L’installation locale a ensuite été mise à jour et vérifiée ; ses preuves sont
décrites ci-dessous.

## Limite de la mise à jour

Le candidat reprend le core actuellement installé, reconstruit Studio et prépare
une mise à jour coordonnée de `reference-assets` et `reference-module`. Le binaire
du jeu courant est réutilisé à son SHA-256 épinglé ; il n’est pas reconstruit et
son ancienne preuve locale ne constitue pas une nouvelle validation du jeu.
Les autres applications et le contenu des packs hors delta sont conservés.

Les préparations de contenu et les inspections des archives sont retenues dans
`current-module-preparation.json`, `current-assets-preparation.json`,
`reference-module-inspect.json` et `reference-assets-inspect.json`. Elles ne
prouvent pas à elles seules une installation. Le plan coordonné des trois packs
a ensuite été appliqué à `F:\instal test\Installation` : révision 5, version
`0.1.0-preview.20261010.4`, core
`3f14c51281023b4baa843d268172c52c2a450d7fd70bd0a9c899c1afb08d96eb`.
Le contrôle `post-install-verification.json` confirme les pins des fichiers
installés, les vingt-huit fichiers du workspace courant et les sauvegardes
inchangées. Les 1 005 fichiers du core hors changements autorisés restent
identiques à la base installée.

L’entrée normale `delivery editor` a lancé le binaire installé, avec son hash
vérifié (`candidate-002/installed-launch-observation.json`). Cette entrée nettoie
les paramètres de diagnostic WebView ; les interactions et la capture restent
donc les preuves de la copie isolée exécutant exactement ce même binaire, et ne
sont pas présentées comme des interactions dans le workspace personnel.

La migration préparée exige les vingt-cinq pins personnels attendus, conserve
leur historique, ajoute trois objets immuables puis remplace seulement
`workspace-v1/authoring.json` de façon atomique. Elle garde le verrou d’auteur et
une sauvegarde privée vérifiée pendant l’application du plan de livraison ; si
cette application échoue, l’autorité du workspace n’est pas modifiée. Les sept
cas de refus du plan ont été testés avec des fichiers factices, sans application
de livraison ni migration réelle (`migration-delivery-guards-test.json`). La
migration réelle a ensuite réussi sous ce verrou. La sauvegarde des vingt-cinq
fichiers précédents et le reçu complet restent dans
`candidate-001/workspace-backups/workspace-before-20261010-230404-6d1df32b` et
`workspace-before-20261010-230404-6d1df32b-receipt.json`.

Ce travail n’implique ni publication Git ni qualification d’une release ou
fermeture de Gate.
