# ADR-0055 — Passage optique solaire dans le moteur commun

Date : 27 octobre 2026. Statut : transfert qualifié localement, adoption complète en cours.

## Décision

Le passage optique solaire existant appartient à `forge_engine::lens_flare`.
Ses deux shaders WGSL, UUID, bindings, réduction de visibilité sur le disque
solaire et composition sont conservés. Les paramètres restent ceux du
consommateur ; aucune donnée ou coordonnée de carte n'entre dans le moteur.

Le passage emploie les entrées communes déjà établies : `SceneSun`, observation
du milieu aquatique, profondeur échantillonnable et ordonnancement du haze.
Le repli existant vers la lumière directionnelle native reste disponible pour
une scène sans publication explicite du soleil. Les consommateurs Studio,
référence et sondes procédurales appellent directement le propriétaire moteur.
La crate de carte ne conserve ni implémentation ni façade de réexport.

Ce transfert ne modifie pas les réglages auteur, la politique de profils ou
le format des modules. L'installation effective de toute la chaîne optique
dans le viewport canonique et le jeu de modules reste exigée par la reprise
des capacités communes. Un propriétaire générique ne prouve pas cette adoption.

## Vérification

La baseline native `shared-lens-baseline-native-001/proof.json`, compilée sur
les sources 018, exécute deux scènes procédurales sans assets importés. Les
captures témoins sont identiques et le flare modifie respectivement 23 035 et
20 773 pixels. Les passes grain et AO sont observées dans le même graphe.

`shared-lens-transfer-001.json` épingle les originaux sauvegardés et le transfert
des trois fichiers ; les deux shaders gardent exactement leurs octets. Les
sources 019 identifient le nouvel état. Vérifier Clippy natif/Web, les tests des
entrées de scène indépendantes et le rejeu GPU avant/après. Les images flare et
témoins doivent rester identiques à la baseline ; les anciennes preuves restent
attribuées à leurs sources. La qualification complète de la référence, du
Studio, des modules, de la performance et de Gate 7 reste distincte.

Clippy strict natif (moteur/carte, bibliothèques et tests), Clippy Web du Studio,
format, cinq tests des entrées de scène indépendantes et 25 frontières passent.
Les trois premiers refus de frontières lisaient encore l'ancien emplacement ;
les contrôles suivent maintenant le propriétaire moteur et interdisent une copie
ou un réexport dans la crate de carte. Leurs premières erreurs sont conservées.

`shared-lens-native-comparison-001.json` vérifie les mêmes entrées optiques et
huit PNG identiques octet pour octet : flare, témoins et AO des deux scènes.
Les deux captures de flare avant/après sont inspectées. Les deux PNG de grain
diffèrent ; l'assertion supplémentaire initiale portant sur les dix images est
refusée et conservée dans le bilan. Le code et shader de grain restent intacts
et son germe avance avec les frames ; aucune parité exacte de ce passage n'est
revendiquée. Les 2 528 entrées 019 restent inchangées après qualification.
