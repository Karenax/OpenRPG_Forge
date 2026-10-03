# Restauration de bibliothèque après rechargement du Studio

Suite du [chargement et de la sauvegarde exFAT](2026-10-03-studio-light-package-loader.md).

Le passage édition → Playtest → édition recharge la page. Les packages vérifiés
en mémoire disparaissent. Jusqu'au candidat `attempt.2`, seule l'entrée en
Playtest les recharge automatiquement ; le retour en édition nécessite le bouton
de bibliothèque. Ce candidat reste figé et n'embarque pas le correctif suivant.

## Correctif raccordé dans les sources

`library_resume.rs` décrit un choix de session versionné : identité de zone,
métadonnées complètes de sa source terrain et SHA-256 du manifeste de sélection
de bibliothèque. Le choix n'est mémorisé qu'après acquisition et vérification
réussies. Il est conservé dans le stockage de session du navigateur, pas dans
le document canonique de zone.

Le viewport demande la restauration dans les deux modes. L'édition conserve
le chargement explicite initial ; elle restaure seulement un choix correspondant
à la même zone et à la même source. Les retouches de placements ou d'altitude
ne changent pas cette identité de source. Une bibliothèque déployée remplacée
exige une nouvelle acquisition explicite : son manifeste est comparé avant
la lecture des payloads. Tous les contrôles existants des packages restent
exécutés. La portée de zone et le ticket de chargement sont revérifiés avant
publication en mémoire. Aucun contrat ne dépend du nom ou du slug d'un module.

Si le stockage de session n'est pas disponible, le parcours manuel reste
disponible ; sa restauration automatique n'est pas déduite d'un succès CPU.

## Vérification actuelle et limites

Le harness natif compile le vrai module Rust contre les dépendances natives
existantes, puis exécute quatre tests réussis : retouche/relecture du choix,
zone ou source différentes, manifeste remplacé ou version inconnue,
source absente ou invalide. Le premier essai de compilation échoue uniquement
dans le helper de fixture, qui avait oublié de déballer le résultat du document
embarqué ; le helper corrigé compile et les quatre tests passent.

Les fichiers de validation sont sous
`F:/OpenRPG_Forge/.build/studio-library-resume-20261003-v1`.
Le formatage et le contrôle scoped du diff passent. Ces tests ne couvrent pas
le stockage WebView, les futures asynchrones, le cycle complet Playtest/édition
ou les pixels. Le frontend corrigé doit encore être construit et exécuté.
La parité graphique complète et l'objectif Tidewater restent ouverts.

Le snapshot est maintenant terminé : 1 343 fichiers, 22 006 918 octets,
construits depuis le snapshot V8 intact avec seulement les quatre fichiers du
correctif en overlay. Le coordinateur release est lancé. Il contrôle les sources
avant/après Cargo, exige le vrai artefact optimisé, assemble avec wasm-bindgen
et ajoute le worker de navigation vérifié. Aucun lancement UI du frontend
corrigé ni résultat de compilation terminal n'est encore revendiqué.

Une copie neuve des 27 fichiers d'île (54 608 403 octets) et un profil WebView
séparé sont préparés dans `runtime-fixture-v1`. Le script de lancement v6
passe la vérification syntaxique, mais n'est pas exécuté.

L'assemblage passe son contrôle initial des sources, de la bibliothèque et du
kit (`web-release-v1/assembly-inputs.json`). La session release 45872 est
confirmée active dans le vrai Cargo WASM. La session 16768 attend ce même
coordinateur, PID 46768 avec date de création vérifiée, puis construira le
candidat natif `attempt.3` uniquement après son succès. Les sorties natives
prévues sont `native-library-resume-v1` sous le dossier de validation desktop.
Cette continuation conserve les refus et candidats précédents, vérifie les
entrées natives et le frontend embarqué et ne lance ni ne publie le produit.

## Fermeture des anciennes instances de validation

Les fenêtres 25235728 (PID 46324, candidat attempt.2 exFAT) et 24056400
(PID 38776, ancien candidat sur fixture NTFS) ont été fermées par le bouton
« Enregistrer et quitter ». Leur absence est confirmée dans les processus CIM
et la liste des fenêtres Computer Use. Les retouches appliquées étaient déjà
sauvegardées ; cette fermeture ne constitue pas un test de réouverture complète.
La fenêtre 17043788 (PID 33784) reste ouverte pour préserver son brouillon
non appliqué. Aucun fichier ou candidat n'a été supprimé.
Le reçu `old-window-cleanup-v1.json` conserve ces observations.

La continuation 45872 et l'attente native 16768 sont toujours actives lors de
ce contrôle ; le rustc enfant de Cargo est confirmé vivant. Aucun succès
terminal du nouveau frontend ou parcours UI du correctif n'est revendiqué.

Le contrôle suivant confirme Cargo PID 3748 et son rustc PID 48472 :
la compilation porte désormais sur `forge_studio_frontend` en release WASM.
Le reçu `release-live-observation-v1.json` décrit le journal observé, sans
transformer une compilation vivante en verdict final.

Cargo release WASM termine avec retour 0 à 2026-10-03T05:00:48.9255646Z,
après 25 min 52 s. L’avertissement de future incompatibilité concerne
proc-macro-error2 v2.0.1. Le coordinateur 45872 reste vivant pendant les
contrôles et l’assemblage ; ce succès Cargo seul ne prouve ni frontend
assemblé ni exécutable natif ni restauration UI.

L’assemblage avec wasm-bindgen réussit ensuite : le reçu
`frontend-stage/receipt.json` compte 1 754 fichiers vérifiés, le profil Cargo
optimisé et les nouvelles intégrités SRI. Le worker de navigation est en cours
d’ajout par le PID 34780 avant la garde finale et la continuation native.
La restauration dans la WebView reste non exécutée.

La continuation frontend 45872 termine avec retour 0 à
2026-10-03T05:11:42.671739Z : worker ajouté et garde finale des sources PASS.
Le reçu `web-release-v1/resume-continuation-result.json` confirme
`frontend-build-only`, `source_unchanged=true`, `studio_executed=false`.
La continuation native 16768 lance le coordinateur PID 44492 et le script
Build-StudioCandidate PID 47272 pour attempt.3. La construction est encore
en cours au contrôle suivant ; aucun succès natif ou parcours UI revendiqué.

## Candidat natif et cycle UI observé

La construction native termine avec retour 0 à 05:28:14 UTC : 175 fichiers
source et 1 767 fichiers frontend vérifiés, entrées inchangées. Le candidat
attempt.3 porte le SHA-256
`d20adfdc81481b35a6091cea3291562f87b502dc7b5dcc2b2c791f7528737e1e`.
Il est lancé sur `runtime-fixture-v1`, avec profil WebView séparé, PID 38820.

Le parcours Computer Use ouvre le workspace puis sa zone Tidewater island.
La scène contient 23 bâtiments et 10 couches végétales. Une seule activation
manuelle de « Vérifier et charger la bibliothèque » est effectuée. Les bâtiments,
le ponton, le terrain, le ciel et leurs ombres sont observés dans l'éditeur.
Le passage « Jouer » recharge la page en Playtest ; le village, le chemin en
planches et les plantes apparaissent après préparation. Le bouton « Retour à
l'édition » recharge ensuite l'éditeur : les bâtiments, le ponton et le ciel
réapparaissent automatiquement, sans deuxième activation du chargement manuel.

Le reçu `runtime-fixture-v1/library-resume-ui-observation-v3.json` conserve
ce verdict limité. Les 27 fichiers source originaux sont inchangés et les
26 fichiers lisibles de la fixture sont identiques aux pins de préparation.
Le verrou workspace actif est illisible et reste non vérifié ; la tentative v1
de lecture échouée et les drapeaux UI nuls de v2 sont conservés ; v3 répare
explicitement ces drapeaux. Aucun déplacement ni nouvelle sauvegarde
n'est effectué dans ce parcours. L'eau manque encore dans la vue éditeur.
Ce test prouve la restauration observée du rendu après retour, sans qualifier
la parité graphique, toute l'édition, le gameplay complet ou la distribution.

## Retouche sauvegardée puis retour du Playtest

Un second parcours sur la même copie isolée déplace le bâtiment 8
(`00231f2b-5f07-548e-9bb1-94cf0f742f14`) de X 75,5 à 77,5 m, applique
l'aperçu puis sauvegarde. Après « Jouer » puis « Retour à l'édition »,
le village et le ponton réapparaissent sans chargement manuel supplémentaire.
La sélection dans la scène relit X 77,5, Y 0 et Z -132,5 dans l'inspecteur commun.

Le workspace actif référence l'archive immuable
`workspace-v1/objects/5da543d0d0af548e6a338c142161d3a0eab04ac1f11c926fc31f725f2eb953f2.json`.
Le fichier historique au chemin direct de la zone demeure inchangé ; il ne
représente pas la retouche sauvegardée du workspace. Le contrôle de l'archive
effectivement référencée vérifie son SHA-256, le déplacement seul, les 22 autres
placements identiques, la base identique et les 27 fichiers source préservés.
`runtime-fixture-v1/Verify-RetouchReturn.py` termine avec retour 0 et écrit
`retouch-playtest-return-v1.json` (SHA-256
`603498577939f22b87fa396961f209e454aff4b6ca3fa8000ba10a78bcc45607`).

Ce parcours ne prouve pas les pixels du bâtiment déplacé isolément, un redémarrage
complet du processus, l'eau en édition ou la parité visuelle. Le reçu précédent
sans retouche reste conservé et décrit son propre parcours.
