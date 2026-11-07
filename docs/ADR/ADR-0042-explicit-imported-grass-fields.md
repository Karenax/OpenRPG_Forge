# ADR-0042 — Champ d'herbe importé déclaré par l'auteur

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Besoin

Le package de prototypes contient aussi la grille, le masque et les géométries
d'herbe de référence. Employer sa bibliothèque de modèles ne signifie pas que
l'auteur demande d'ajouter ce champ au terrain du module. Une activation fondée
sur le nom de la carte, le nombre de plantes ou la seule présence du package
introduirait du contenu implicite et couplerait d'autres terrains au dataset.

## Décision

Le document végétal canonique reste la source de vérité. La version 1 de
`forge.imported-vegetation` conserve exactement ses blocs et sa sérialisation,
sans champ d'herbe. Sa version 2 ajoute une référence explicite `grass` vers le
SHA-256 de l'index du profil importé gouverné. Elle conserve les blocs et leurs
identités ; aucune instance par brin, entité renderer ou chemin local n'est
sérialisé. La version 1 avec un champ et la version 2 sans champ sont refusées.
Les anciens lecteurs refusent la nouvelle version ; aucune compatibilité de
document n'est déduite du maintien de l'API 3 du conteneur et de sa table de
compagnons. Leurs contrats de fermeture de transport restent ceux d'ADR-0040.

Le lecteur de livraison rapproche cette référence de l'index déjà fermé par
la table des dépendances végétales. L'hôte vérifie de nouveau le profil et son
raster avant installation. Sans déclaration, il conserve les seules plantes
demandées, sur leur terrain admis ; il ne fabrique aucun masque neutre ni champ
de référence. Le premier profil repris garde ses 256² cellules de 8 m, son
raster unitaire 2048² et son masque 1024². Ces contraintes de profil ne sont
pas une qualification de tous les formats ou de l'herbe procédurale existante.

Le moteur réutilise le sélecteur/résident commun, le shader de géométrie source,
le correcteur terrain existant et les vrais champs installés. Raster d'origine
et corrections de coins restent séparés. Sa disponibilité requiert une
génération d'inventaire GPU achevée après publication, indépendamment de la
physique des autres plantes. La preuve de ressources ne vaut pas lecture des
pixels de chaque frame.

## Vérification et réserves

Conserver le refus d'absence/altération de ressources et d'index discordant,
les hashes des documents v1, les octets des blocs et les identités auteur.
Qualifier un ancien module sans activation implicite, puis un module déclarant
le champ avec ses ressources autonomes et les acquittements GPU datés.
Le helper de composition de qualification possède une option explicite ; il
ne migre pas les archives historiques. Le parcours d'import auteur Studio,
l'édition/picking, les profils, les autres formats et la distribution restent
à qualifier séparément. Cette décision n'ouvre pas de nouvelle gate.
