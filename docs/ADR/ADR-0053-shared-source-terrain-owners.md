# ADR-0053 — Terrain source et résidence CDLOD dans le moteur commun

Date : 26 octobre 2026. Statut : réalisation Gate 7 en cours.

## Décision

La consigne propriétaire confirme que les mécanismes réutilisables développés
pour Tidewater doivent être possédés par le moteur et accessibles à tout module.
Transférer les implémentations existantes, sans en écrire une seconde :

- `forge_assets::source_terrain_package` : lecteur fermé et épinglé du paquet ;
- `forge_engine::source_terrain` : matériau, textures, CDLOD, normales/AO et
  préparation/remplacement du terrain ;
- `source_terrain::resident_tiles` : résidence bornée des patches conservant
  maillages et entités lors des changements de vue ;
- `source_surface::{capture,underwater}` : demande conservative de capture,
  marqueur de consommateur, liaisons et entrées matérielles de lumière sous l'eau.

Référence, Studio et outils appellent ces propriétaires directement. Retirer les
anciens modules/types et réexports ; transférer aussi leurs tests et sondes
indépendantes. Les graphes océaniques restant dans l'adaptateur consomment les
types communs, sans dépendance inverse du moteur.

Les notices accompagnent les sources. Les identifiants GPU, programmes WGSL,
grilles, morphing, budget de résidence et gardes des paquets restent inchangés.
Le codec conserve ses identités de format et de provenance historiques ; elles
ne sélectionnent aucun module par son nom. Le profil acquis reste explicitement
borné au raster 2048², pas étendu silencieusement à d'autres géométries.

Cette décision précise ADR-0026 pour les mécanismes maintenant communs, selon
la consigne propriétaire actuelle. Les données, droits et particularités d'une
carte restent dans ses paquets. Une façade ou le simple déplacement d'un nom
ne constitue pas une adoption par le runtime de modules.

## Qualification et suite requise

Conserver un inventaire figé et une sauvegarde bornée des fichiers réellement
touchés dans la campagne centrale `gate7-monde-distribution`, sous
`shared-terrain-owners`. Budget de cette sauvegarde : 20 Mio / 250 fichiers ;
pas de copie du checkout, du cache Cargo ni des ressources immuables de test.

Vérifier le transfert des programmes et algorithmes, les frontières, les tests,
Clippy natif/Web, le lecteur sur paquet réel et les consommateurs natifs.
Les tests de résidence de la sonde deviennent des tests moteur courants.

Le viewport canonique de modules utilise encore son matériau auteur. Il reste
à y raccorder le terrain source par un contrat explicite de ressources de zone,
avec les mêmes algorithmes, sans héritage global ni déduction par géométrie.
Le parcours Studio arrêté par Échap, les profils, les autres effets et la parité
mesurée restent dus. Cette tranche ne clôture pas Gate 7 et n'ouvre aucune autre gate.


## Qualification locale du transfert — 26 octobre

Le catalogue `shared-terrain-owners-validation-001.json` rapproche 2 748 tests,
25 frontières, Clippy natif/Web, vérification lexicale des douze transferts,
lecteur sur paquet réel, AO exacte et remplacement/refus atomique. Le module
API-7 et le consommateur natif de terrain passent avec captures inspectées.
L'adoption du profil par le viewport canonique, les autres effets et la parité
restent en attente ; aucun des dix critères Gate 7 n'est déclaré clos.

## Premier consommateur de module natif — 27 octobre

La zone API 8 fournit désormais ses ressources admises au host commun. Celui-ci
reprend scène, CDLOD et cache, avec les sorties de l'environnement/ombres courant.
Les anciennes cellules conservent partition, identité et collisions ; leur dessin
est masqué quand le profil source est actif. Une génération tardive ou révoquée
des normales exige une nouvelle observation de disponibilité puis une complétion
GPU postérieure. Le profil acquis reste borné à la géométrie déjà définie.

La composition de matériaux déjà utilisée par les surfaces éclairées est déplacée
et généralisée dans `material_composition`, puis consommée aussi par le terrain LOD.
Les délégations au SDK sont conservées par transformation inverse vérifiée ; le
visiteur reprend les dépendances des deux composants. Le type composé du SDK
seul ne les déclare pas : un rejeu avec zéro texture/buffer est refusé, puis le
rejeu corrigé observe 13 textures et 5 buffers. Les seuils ne sont pas abaissés.

`module-source-terrain-runtime-validation-001.json` qualifie ce démarrage natif
réel, les régressions de la référence/peinture et du module sans ce profil,
2 764 tests principaux, 87 tests du consommateur, 126 de livraison, Clippy natif/Web
et 25 frontières. Le viewport Studio, les remplacements/retouches et transitions
de caméra, les effets restants et les performances appariées restent ouverts.
