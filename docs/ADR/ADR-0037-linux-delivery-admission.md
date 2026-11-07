# ADR-0037 — Admission des livraisons Linux et permissions dérivées

Date : 23 octobre 2026. Statut : retenu dans la réalisation autorisée de Gate 7 ;
preuves de compilation, installation et exécution à établir séparément.
Portée : `forge_delivery`, transport et produit v1 existants, sans nouveau format.

## Problème et décision

Le transport accepte seulement `windows-x64` et `any`, ne contrôle pas l'OS au
moment d'installer et extrait des fichiers sans droit d'exécution Unix. Un build
Linux seul ne peut donc pas constituer une livraison installée utilisable.

Accepter la valeur explicite **`linux-x64`** dans le champ plateforme existant.
L'inspection et la création d'archives restent possibles depuis un autre OS ;
l'installation et l'utilisation d'un produit installé exigent la compatibilité
avec l'OS et l'architecture réels. `any` conserve sa sémantique existante pour
les paquets indépendants de l'hôte. Une désignation Linux exige la casse exacte
des points d'entrée dans l'inventaire. La validation historique des autres
plateformes et les hashes des reçus précédents restent inchangés.

La préparation refuse les archives incompatibles avant de créer l'installation.
Apply contrôle aussi les en-têtes avant le verrou/staging, puis revérifie la
plateforme sous le verrou avec l'inventaire et les hashes habituels : un plan
transporté depuis un autre OS ne contourne pas l'admission. Les snapshots actifs,
packs et rollbacks sont soumis au même contrôle de plateforme.

## Permissions et chemins

Le transport n'acquiert pas de champ de permissions arbitraires. À l'extraction
Unix, les seuls fichiers désignés par `product.apps[*].executable` reçoivent
`0755` ; les autres fichiers reçoivent `0644`. Aucun mode source, setuid/setgid,
script d'installation ou hook n'est repris. La vérification des fichiers
installés contrôle aussi les bits d'exécution et refuse les modes spéciaux.
Les programmes restent lancés directement par `Command`, sans évaluation shell.

Sous Linux, les chemins par défaut sont placés sous
`$XDG_DATA_HOME/openrpg-forge/{installation,userdata}` ou
`$HOME/.local/share/openrpg-forge/{installation,userdata}`. Les valeurs XDG vides
ou relatives sont ignorées selon la
[spécification des chemins de base](https://specifications.freedesktop.org/basedir/latest/).
Les overrides explicites doivent être absolus. Les chemins Windows existants
sous `%LOCALAPPDATA%/OpenRPG Forge` et la racine utilisateur épinglée dans chaque
installation ne changent pas. Aucun répertoire utilisateur existant n'est déplacé.

## Validation et limites

Contrôler le refus des plateformes, y compris avec un plan rehashé et transporté,
la casse des chemins Linux, puis les régressions Windows/Linux. Le test Linux
installe un véritable petit ELF système dans une fixture temporaire, l'exécute
et vérifie les refus après altération des droits, ainsi que l'immutabilité de la
source. Les paquets produit, dépendances dynamiques, profils, licences,
installation/mise à jour/rollback/désinstallation et machine propre doivent
ensuite être qualifiés avec les vrais binaires, sans assimiler la fixture à eux.

Cette tranche n'ajoute pas AppImage/DEB/RPM, ne qualifie pas toutes les
distributions Linux et ne change ni le lanceur canonique ni les rôles du Hub,
du Studio et du jeu. Elle ne ferme aucun critère de Gate 7 à elle seule.

## Raccord des sélecteurs du Hub

Le Hub ne peut pas appeler `powershell.exe` sous Linux. Son adapter de dialogue
réutilise GTK 3 déjà présent dans le graphe Tauri, version 0.18.2 inchangée :
aucune nouvelle version de paquet ni bibliothèque de dialogue supplémentaire.
Les fenêtres GTK sont créées sur le thread d'interface Tauri et répondent via
un canal asynchrone ; le thread d'interface n'attend pas la sélection.
Les fenêtres sont modales et liées au parent. Annuler ne change ni destination
ni plan. Entrée et les mnémoniques des boutons permettent la confirmation au
clavier. La sélection de fichiers est locale, multiple et filtrée pour les
contenus Forge ; le filtre n'est pas une admission de sécurité.

Après sélection, l'admission des destinations et archives reste dans les mêmes
commandes métier, avec leur sérialisation et invalidation des plans. Windows
conserve ses dialogues existants exécutés hors du thread d'interface, avec des
scripts fixes. La compilation et l'observation des vrais dialogues Linux sur
l'OS sans SDK sont des preuves distinctes, identifiées dans le suivi Gate 7.
