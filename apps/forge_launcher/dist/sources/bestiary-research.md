# Horizons sauvages — recherche et parti pris de création

Date : 27 septembre 2026. Bestiaire Forge v0.3, vague `wild-realms.v0.3`.
Statut : propositions éditoriales et mécaniques ; aucun comportement runtime livré.

## Références consultées

- [D&D Beyond — How to Use a Monster, Basic Rules 2024](https://www.dndbeyond.com/sources/dnd/br-2024/how-to-use-a-monster), consulté le 27 septembre 2026.
- [Wizards of the Coast — System Reference Document](https://www.dndbeyond.com/srd), version 5.2.1 présentée par la page, consultée le 27 septembre 2026.

La première référence distingue quatorze grands types de créatures. Cette lecture
sert ici à diversifier le catalogue : organismes naturels, végétaux, êtres
féeriques, élémentaires, organismes amorphes, morts animés, constructions, géants
et dragons. La page SRD distingue un corpus réutilisable sous CC-BY-4.0 du reste
de la propriété D&D : tout le bestiaire de la marque n'est pas dans ce corpus.

Nous ne reproduisons ni n'adaptons un bloc du SRD. Aucun texte descriptif,
statistique, illustration, personnage ou lieu propre à D&D n'est repris.
Les archétypes généraux sont un point de départ, pas un modèle de conversion.
Cette note documente la méthode de création ; elle n'est pas un avis juridique
ni une autorisation de reprendre d'autres contenus de la marque.

## Ce que l'on retient pour Forge

1. **Diversité des rencontres** : une créature n'est pas seulement un adversaire.
   Elle peut défendre une ressource, réparer un milieu, garder un mandat,
   déplacer un danger ou négocier un passage.
2. **Identité lisible** : silhouette, locomotion, traces et télégraphes donnent au
   joueur des indices avant une décision risquée.
3. **Milieu causal** : humidité, sel, vent, sol, pression et matériaux modifient
   réellement la situation proposée ; pas de vulnérabilité sans explication.
4. **Règles communes** : les sept attributs, compétences Forge, cinq ressources,
   portées, coûts et préparations restent ceux du manuel. Aucun d20, facteur de
   puissance ou classe d'armure importé.
5. **Choix et responsabilité** : les êtres sapients ont des intentions et des
   circonstances, jamais un alignement moral imposé par leur ascendance.
   Les récupérations respectent les dépouilles, les propriétaires et le consentement.

## Les trente créations

| Entrée | Créature | Axe original de rencontre |
| --- | --- | --- |
| 22 | Chouette-cerf des brisants | Une faune de falaise à observer sans couper son passage. |
| 23 | Taupe-bélier de basalte | Un fouisseur dont les travaux modifient le terrain. |
| 24 | Raie des dunes | Un animal des sables et une lecture différente des appuis. |
| 25 | Tisseuse de verre | Des fils minéraux, un territoire et des routes à contourner. |
| 26 | Brochet-lanterne | Un prédateur aquatique dont la lumière signale aussi le danger. |
| 27 | Ronce-marcheuse | Un végétal mobile, ses besoins et ses barrières vivantes. |
| 28 | Couronne de spores | Une colonie dont la présence transforme un lieu. |
| 29 | Saule des voix | Un arbre sensible aux sons et aux perturbations du milieu. |
| 30 | Faune des gués | Le partage d'un passage naturel plutôt qu'un péage arbitraire. |
| 31 | Dame des lucioles | Une personne féerique incarnée et les conditions d'une cohabitation. |
| 32 | Lutine des greniers | Une présence domestique liée aux réserves et aux arrangements. |
| 33 | Cheval de rosée | Un être de condensation, de trajet et de confiance. |
| 34 | Porte-braise | La chaleur comme charge à transporter et à contenir. |
| 35 | Nœud d'averse | L'eau et la pression comme contraintes locales. |
| 36 | Fileuse de givre | Une tisseuse à affinité élémentaire dont les dépôts transforment les surfaces. |
| 37 | Berger d'éboulis | La stabilité des pentes comme enjeu de rencontre. |
| 38 | Limon d'ardoise | Une matière vivante, des fissures et des circulations. |
| 39 | Gelée de cuivre | Un organisme amorphe en interaction avec les métaux. |
| 40 | Nappe de sel | Un agrégat minéral animé et les cycles de saumure. |
| 41 | Veilleur d'ossuaire | Un mort lié à un lieu et à des responsabilités funéraires. |
| 42 | Porte-deuil des marais | Un cortège, des restes et une dette de mémoire. |
| 43 | Squelette des cloches | Un messager ambulant, ses relais et un mandat ancien. |
| 44 | Cendrelame lié | Un défunt retenu par une fonction, pas une hostilité universelle. |
| 45 | Arpenteur de bornes | Une construction qui lit et défend des limites. |
| 46 | Carillon de bronze | Une construction sonore et une infrastructure à comprendre. |
| 47 | Drapier d'argile | Une construction artisanale, ses matériaux et son usage. |
| 48 | Géant des moraines | Un éleveur migrateur et les passages saisonniers des glaciers. |
| 49 | Ogresse des fours | Un métier, un besoin de chaleur et des règles d'accueil. |
| 50 | Vouivre des salines | Un grand prédateur adapté aux bassins salés. |
| 51 | Dragon des sources closes | Un être intelligent dont le territoire est un réseau d'eau. |

Les axes ci-dessus résument une intention éditoriale ; les fiches JSON sont
l'autorité des détails et des chiffres. Les familles de navigation sont celles
de Forge, sans prétendre reproduire la taxonomie D&D.

## Périmètre de l'édition

- 21 fiches préexistantes conservées octet pour octet ; empreintes épinglées dans
  `bestiary.json`, sous `expansion_wave.preserved_entries`.
- 30 fiches nouvelles, chacune avec au moins deux attaques, une capacité,
  deux interactions causales et deux approches non violentes.
- 51 créatures au total ; les 26 planches antérieures restent inchangées.
- Premier lot illustré : Chouette-cerf des brisants, Ronce-marcheuse, Dame des
  lucioles et Dragon des sources closes, générés séparément avec l'outil intégré.
  Ces quatre fiches portent `illustrated`.
- Deuxième lot : 26 planches originales distinctes complètent les fiches restantes.
  Le corpus compte désormais 56 planches pour 51 créatures, sans portrait manquant
  ni réemploi du portrait d'une autre créature. Toutes les nouvelles fiches portent
  `illustration_status: illustrated` ; la couverture de la vague est `complete`.
- Les secrets restent exclus de la recherche publique et sont repliés à la
  lecture ; les données locales ne constituent pas un contrôle d'accès.

## Qualification et limites

`scripts/Test-Bestiary.py --source-only` valide le schéma, les autorités, les
ressources dérivées, les nouvelles entrées, les empreintes et les images existantes.
Le lanceur autonome compile ces sources avec `launcher-aube/build_content.py`.
Ses tests et les parcours navigateur doivent être exécutés après génération.

Les valeurs sont des propositions à équilibrer, pas une preuve de jouabilité.
La magie, les comportements et les chaînes causales ne sont pas implémentés par
cette livraison. Aucune gate n'est ouverte ou qualifiée.

Le PDF A4 et l'ancienne visionneuse dans `output/bestiary` ne sont pas régénérés :
leurs sorties antérieures ne représentent pas les 51 créatures. Les 26 planches
manquantes ont été créées, avec provenance et revue agent ; la validation humaine
reste à faire. La présente livraison cible le codex dynamique `launcher-aube`.
