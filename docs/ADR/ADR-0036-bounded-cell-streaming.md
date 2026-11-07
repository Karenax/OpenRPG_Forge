# ADR-0036 — Streaming de projections par cellules bornées

Date : 23 octobre 2026. Statut : adopté pour la réalisation autorisée de Gate 7 ;
qualification native et performance à produire séparément.

## Besoin

Le terrain Visual Valley interprète volontairement un document de 512 m et
64 chunks. Ses constantes et ses preuves historiques restent valides. Les LOD
et le batching expérimental ne suffisent pas à qualifier le streaming d'une
région plus grande. Charger tout puis masquer ne prouve pas le déchargement.

## Décision

Ajouter dans `forge_world::cell_streaming` un contrôleur pur de **projections
dérivées**, sans nouveau document canonique, moteur, dépendance ou format de save.
La grille identifie chaque cellule par zone et coordonnées entières ; son identité
logique ne dépend pas du niveau de détail ni du hash de la source. Le hash source
épingle les entrées de chaque demande et chaque acquittement de chargement.

Un voisinage proche demande le détail, un voisinage extérieur demande HLOD.
Le plan contient explicitement les acquisitions, remplacements et retraits.
Ses bornes couvrent la grille, le nombre de cellules, les octets estimés résidents
et le pic de staging, anciennes cellules incluses. Un point extérieur, non fini,
un dépassement de budget ou une génération périmée est refusé sans changement.
La génération monotone et l'identité du contrôleur empêchent l'application d'un
plan préparé sur un autre état, même si les coordonnées visibles sont identiques.

L'adapter prépare les nouveaux payloads sans retirer les anciens. L'acquittement
doit fournir exactement les clés, niveaux et hash demandés, avec leur taille
réelle. Toute erreur laisse l'état résident intact ; le succès publie la nouvelle
sélection en une étape. Les handles GPU, colliders et tâches restent dans l'adapter.
Le contrôleur ne qualifie ni la fin du chargement GPU ni l'atomicité des commandes
ECS : ces deux garanties doivent être vérifiées par l'intégration.

HLOD désigne une représentation agrégée de la cellule, à construire et mesurer,
pas simplement une collection de meshes détaillés masqués. Sa génération est
dérivée, n'écrit aucun document auteur et ne remplace pas la physique proche.
Les états gameplay et sauvegardes restent indépendants de la résidence visuelle.

## Protection de la simulation pendant le déplacement

La sélection de vue ne suffit pas à protéger les PNJ et objets actifs éloignés.
Une demande peut donc fournir des cellules de simulation protégées ; leur
voisinage immédiat reste détaillé, avec collisions, et consomme les mêmes bornes
de résidence et staging. Le détail prévaut sur la représentation lointaine.
Un changement de protection invalide une préparation en vol, même si la caméra
n'a pas quitté sa cellule. Un refus ne retire aucune projection déjà résidente.

L'hôte jouable publie une enveloppe conservatrice des corps actifs, comprenant
leur géométrie, leurs positions et le prochain pas physique. Le contrôleur
joueur et le pipeline Rapier restent suspendus jusqu'à observation des vraies
formes statiques synchronisées ; un simple handle ECS ne suffit pas.
Les actuateurs KCC de Rapier sont aussi neutralisés avant SyncBackend lorsqu'un
pas est refusé, car ils ne dépendent pas du seul drapeau du pipeline dynamique.
Les chemins de corps composés ou formes personnalisées non pris en charge sont
refusés explicitement ; ils ne perdent pas leur collision en silence.
Le profil initial possède un seul contexte Rapier par défaut ; tout contexte
supplémentaire ferme la barrière au lieu de laisser une autre simulation active.
Les autres actuateurs doivent publier leurs déplacements avant la barrière finale.
L'adapter garde une référence partagée à la forme de collision préparée ainsi
que l'invariant de pose racine identité de ses cellules. L'accord du backend
avec une forme remplacée ou une cellule déplacée/reparentée ne prouve pas que
la projection attendue subsiste : ces changements ferment la barrière et
refusent une nouvelle admission avant toute publication. Le contenu d'un Mesh
muté sous un handle inchangé n'est pas hashé à chaque pas ; ces assets dérivés
restent possédés par l'adapter, sans mutation externe autorisée par cette API.
Les matériaux réutilisent le shader PBR Forge existant et les poids/retouches
du document canonique aux échantillons conservés. Leurs buffers appartiennent
au budget CPU ; aucun masque de peinture divergent n'est introduit.

Ce raccord est opt-in. Son admission dans le jeu livré exige encore les essais
continus, les conséquences gameplay/save, le rendu des matériaux/retouches et
les budgets de carte réelle prévus ci-dessous. Aucun succès headless ne vaut
preuve visuelle, qualification générale HLOD ou clôture de gate.

## Retouches du terrain dans le même monde dérivé

Une retouche canonique peut demander une nouvelle projection sans reconstruire
la scène de l'éditeur. La demande épingle la source installée et conserve zone,
projet et grille. Elle ne change aucun document ni format de sauvegarde.
Le calcul asynchrone prépare toutes les cellules demandées pour la nouvelle
source ; les anciennes restent présentes et leurs octets CPU réels comptent
dans le pic de staging. Une nouvelle demande remplace la demande en attente,
sans cumuler de file de calcul. Revenir à la source courante annule l'edit dérivé.

L'admission exclusive réutilise les vérifications de projections vivantes et
d'acquittements avant toute mutation ECS. Elle remplace les meshes/colliders et
publie la source résidente ensemble ; un refus conserve l'ancien contrôleur et
ses projections. La source publiée ne qualifie ni upload GPU ni synchronisation
du backend physique. Le parcours Studio doit encore observer ces états et
suspendre les gestes reposant sur un relief qui n'est pas encore concordant.

Le jeu et le viewport raster de Studio consomment le même profil initial de
cellules, validé depuis les métadonnées de la source, sans grille propre à une
carte nommée. La caméra d'édition borne son observateur à l'intérieur de cette
grille. Le choix de la bibliothèque spécialisée conserve sa vérification du
dataset complet ; une autre géométrie utilise le renderer canonique, sans
substitution de raster ni admission de textures liées à une autre carte.

`source_is_synchronized` vérifie la source installée, l'absence de demande ou
calcul en attente et les cellules possédées : mesh présent, pose racine,
identité logique, coordonnées, détail et hash concordants. Chaque collider
proche doit correspondre à sa vraie forme et pose dans Rapier. Cette barrière
est réévaluée après une retouche et ne certifie pas le rendu ; le Studio attend
ensuite des observations fraîches des pipelines via `CaptureReadinessPlugin`.
Le picking et le contour de brosse lisent le plan raster actuellement admis.
Les couches ou objets sans adapter installé restent signalés comme incomplets,
et ne permettent pas de qualifier la scène entière ou Gate 7.

## Bords physiques d'une grille finie

Le host jouable dérive quatre limites solides verticales de la grille admise,
dans le même monde Rapier. Elles ne créent ni document, mesh, grille ou physique
parallèle. Les demi-espaces restent actifs indépendamment des cellules visibles
et de la hauteur du relief. Leurs plans sont arrondis vers l'intérieur lorsque
les coordonnées f64 de la grille ne sont pas exactement représentables en f32.

La caméra reste un observateur : seuls ces plans privés sans rendu sont exclus
de sa recherche d'occlusion, pour ne pas la refermer dans le personnage au bord.
Les murs et autres colliders de la scène gardent leur occlusion habituelle.

La barrière vérifie ces quatre formes, poses et registrations réelles avant
d'autoriser le pas. Une limite absente, déplacée, remplacée ou désactivée ferme
la simulation ; son seul marqueur ECS ne prouve pas la collision. Une fois ces
limites synchronisées, l'enveloppe conservative d'un corps dont les centres
actuel et prédit restent dans la grille protège son intersection avec celle-ci.
Son débordement de marge ne fige donc pas un joueur encore à l'intérieur.
Un centre hors grille, un déplacement prédit hors grille ou un autre profil
non admis conserve son refus explicite. Aucun corps n'est téléporté ou projeté
dans le monde pour transformer une entrée invalide en succès.

Ces plans verticaux sans surface marchable ne sont pas des sources de triangles
pour la navigation. La navigation conserve la limite des vrais triangles du
terrain et leur érosion par profil ; elle ne fabrique aucun sol extérieur.
Leur exclusion porte seulement sur les marqueurs privés des limites possédées
par le host, dont la présence physique reste exigée pour toute simulation.
Les obstacles auteur conservent leur extraction géométrique existante.

La qualification doit montrer les quatre bords et un coin, le contact extérieur,
le retour vers l'intérieur, la conservation des autres corps et le refus d'une
limite altérée. Ces plans bornent physiquement la carte ; ils ne constituent
ni un habillage visuel du périmètre ni une transition vers une autre zone.

## Couverture hiérarchique lointaine du terrain

Complément du 24 octobre : les cellules résidentes de vue/simulation gardent
leur contrôleur et leur physique. Leur complément dans la grille finie est
couvert par un quadtree dérivé, sans trou ni recouvrement. Une tuile de niveau
L agrège 4^L cellules de base avec un seul mesh simplifié. La partition prend
en charge les grilles rectangulaires, les dimensions non puissances de deux et
les îlots détaillés des corps protégés ; aucune cellule auteur n'est créée.

Les voisins diffèrent d'au plus un niveau. Le bord d'une tuile plus grossière
retient les demi-pas de ses voisins plus fins, évalués avec les mêmes hauteurs,
normales globales et poids du document canonique. Les triangles sont divisés
aux raccords, sans jupe ni surface extérieure. Les 16 combinaisons de bords
sont vérifiables géométriquement. Les identités dérivées incluent zone,
coordonnées et niveau ; un changement de source remplace aussi le lointain.

La préparation asynchrone possède au plus 1 024 tuiles lointaines et une seule
demande en vol avec les cellules proches. Les nouveaux meshes sont préparés
avant admission ; les tuiles inchangées sont réutilisées. Les octets résidents
et de staging sont comptés avec les cellules proches dans les budgets existants,
anciennes projections encore vivantes incluses. Un refus de couverture,
budget, source ou acquittement ne publie aucun des deux ensembles. L'admission
exclusive retire les anciens assets et publie les deux sélections ensemble.
La physique proche ne consomme pas les meshes HLOD. Les statistiques séparent
cellules proches et tuiles agrégées ; leurs buffers CPU ne mesurent pas la VRAM.

Ce complément est une hiérarchie de **terrain**. Les bâtiments, placements
manuels et la végétation requièrent leurs contrats de représentation agrégée
et leurs mesures propres ; cette implémentation ne qualifie donc pas à elle
seule le HLOD général de R7-02. Continuité native, transitions et coûts réels
restent à observer sur les entrées courantes.

## Réduction dérivée des meshes statiques

Complément du 24 octobre pour la suite de R7-02 : `forge_world::mesh_hlod`
réduit une géométrie triangulaire dérivée par contractions d'arêtes dans des
cellules spatiales bornées. Chaque représentant reste un sommet original.
Les domaines discrets doivent être exactement identiques ; les canaux flottants
portent leurs tolérances explicites et mesurées. Les contractions conservent
l'orientation des faces survivantes, la condition de lien des arêtes admises,
les frontières d'un anneau et au moins une face par composant source connecté.
Aucun ratio de réduction n'est garanti pour une géométrie déjà minimale.

Chaque triangle contracté conserve une arête de support dans la surface ; une
contraction qui l'effacerait est refusée. Deux faces distinctes ne deviennent
pas deux feuilles superposées par la contraction d'un tétraèdre minimal. La
correspondance source-vers-sortie des sommets est exposée pour vérifier que
chaque triangle source garde une image dans une face, une arête ou un sommet
de sortie, sous la borne métrique annoncée. Les sommets sans support sont un
refus explicite, jamais une réduction présentée comme réussie.
Sur une surface courbe, un simple produit scalaire positif ne suffit pas : les
normales géométriques des faces survivantes restent aussi à 60° au plus de
leur orientation source. L'alignement minimal est rapporté séparément des
tolérances des attributs de normales et des erreurs de position.

Le domaine borne l'entrée à 250 000 sommets, 500 000 triangles, 16 composantes
d'attributs et huit millions d'inspections de triangles. Un voisinage dépassant
128 faces conserve son détail au lieu de monopoliser le calcul. Les sources
non finies, indices invalides, triangles dégénérés et tolérances invalides sont
refusés. Les limites ne changent aucun document auteur ni mesh de collision.

`forge_engine::static_hlod_geometry` transforme et agrège des primitives dont
le matériau et le contrat de shader sont compatibles, puis consomme ce calcul.
Les attributs discrets restent exacts ; normales, tangentes et UV gardent leurs
tolérances exprimées dans leurs unités. Une tolérance UV n'est pas une garantie
de fidélité d'une texture ou un baking de matériau. Le soudage exact s'effectue
par primitive, sans relier deux objets sources distincts. Les opérations
publiques de Bevy compactent ensemble tous les attributs conservés.

L'adapter refuse skinning, morph targets, buffers CPU déjà extraits, formats
incompatibles et poses non admises. Il borne une préparation à 4 096 primitives,
100 000 sommets, 200 000 triangles et 16 Mio de buffers sources. Son estimation
du travail temporaire doit rester sous 256 Mio ; ce calcul ne mesure ni mémoire
totale du processus ni allocations GPU. Les primitives originales restent
intactes sur succès et refus. Les buffers de sortie rapportés sont des longueurs
packées, distinctes des compteurs de capacité des cellules de terrain.

Cette fondation de calcul ne constitue pas encore une sélection HLOD dans le
jeu. Son raccord doit grouper les sources par hiérarchie spatiale, matériau et
layout compatibles, conserver les identités physiques/auteur, maintenir les
originaux jusqu'à preuve de préparation des nouvelles ressources GPU, puis
publier une sélection complète. Les transitions, refus, retraits, profils,
coûts natifs et fidélité visuelle restent exigés. Le batching expérimental de
végétation existant reste non qualifié ; il n'est pas renommé par ce complément.

## Adapter de sélection statique

`forge_engine::static_hlod` réutilise exclusivement la partition admise du
contrôleur terrain. Les boîtes globales des primitives d'un même objet sont
réunies : toucher une cellule détaillée conserve l'objet entier au détail.
Les parties lointaines sont groupées par tuile, matériau, layout, couches et
indicateurs de rendu compatibles. Les clés de rendu ne deviennent pas un format
canonique ; identités, pins, racines et colliders restent sur les sources.

Une seule tâche prépare une transaction globale de groupes, depuis les meshes
originaux. Un changement de sélection restaure l'ensemble des originaux avant
la préparation suivante ; aucun sous-groupe de matériau ne bascule isolément.
Un résultat périmé est retiré sans admission. Le profil initial borne l'erreur
de position après transformation dans le repère agrégé à `0,05 × 2^niveau`,
plafonnée à 1 m, celle des composantes normales/tangentes à 0,1 et des UV à 0,01.
Ces bornes CPU ne prouvent pas la fidélité de la texture ou des pixels GPU.

L'inventaire global est borné à 4 096 primitives et 64 Mio de snapshots CPU de
sources uniques ; les sorties totalisent au plus 32 Mio, avec les limites de
travail par groupe de l'API géométrique. Les meshes originaux restent retenus
pour la restauration et leur coût est nommé séparément. L'enveloppe de staging
additionne conservativement sources, snapshots, sorties vivantes et entrées /
enveloppe de sortie du job ; elle ne mesure pas le working set ou la VRAM.

Les groupes restent cachés jusqu'à observation des allocations réelles de mesh,
matériaux et images par les inspections publiques partagées de qualité globale,
des ressources de la caméra courante, de huit observations stables des pipelines
et d'une fin de travail de la queue. Génération, caméra, cible, viewport et
inventaire font partie de la clé ; une observation périmée, une ressource manquante
ou une nouvelle sélection n'autorise pas la substitution. La transaction et
les captures recontrôlent la couverture. Sans renderer, le détail reste visible.

Le jeu de module marque les primitives admises des placements et bâtiments.
Il sépare leur barrière de source / physique de leur couverture visuelle, sans
affaiblir la barrière de visibilité du Studio. Le matériau standard est le
premier contrat installé ; un autre shader exige une revue de compatibilité du
baking de pose et de ses composants supplémentaires. Skinning, morph targets,
shear, réflexions de pose, tags d'instance non nuls, visibilité par distance et
transparence triée restent explicitement refusés pour cette route. Une erreur
conserve les sources détaillées et empêche la qualification de la substitution.
La végétation, les profils et la qualification native restent dus.

## Finesse du raster — raccord des profils du 24 octobre

`SampledMeshQuality` porte deux strides validés, pour les cellules grossières
et les intérieurs des tuiles HLOD. Les valeurs autorisées sont 2, 4, 8, 16 et
32 ; les bordures partagées gardent leurs échantillons, normales et poids de
matériaux canoniques. Les cellules complètes restent au stride 1, y compris
leur surface de collision. Le constructeur et le renderer historiques gardent
leur défaut 8/8 ; aucune donnée auteur ou sauvegarde n'est réécrite.

`request_mesh_quality` prépare une nouvelle projection sur le hash du raster
actuellement admis. Le profil fait partie de la clé des jobs : une ancienne
préparation ne vaut pas acquittement d'un autre profil. L'ancienne projection
reste visible pendant le travail borné. La publication des cellules et du HLOD
reste atomique, avec anciens buffers inclus dans les limites de staging ; les
formes proches reconstruites sont identiques et doivent être resynchronisées
avec Rapier avant reprise. Les profils ne réduisent pas la protection des corps.

Une modification auteur en attente n'est pas annulée par un réglage graphique :
ce dernier est refusé jusqu'à admission ou annulation explicite de l'édition.
Inversement, une édition du terrain reprend le profil déjà demandé. Annulation,
remplacement et erreur de budget conservent leurs contrôles avant mutation.
`admitted_mesh_quality` expose seulement le profil CPU installé ; il ne constitue
pas un acquittement GPU. Le contrôleur global et l'interface doivent encore
rapprocher leurs capacités et attendre les ressources/fences réelles.

La tranche `cell-quality-*` prévoit **16 Mio / 96 fichiers de preuves**, sans
nouvelle VM, conteneur, module autonome ou copie du raster. Le mode diagnostic
`cell_streaming_region --mesh-quality` réutilise le raster synthétique de 2 km,
les budgets géométriques de la table existante et le cache commun. Il doit
observer les cellules, tuiles, collisions proches, ressources GPU et fences
après capture pour Low, Medium, High, Ultra et retour au défaut. Cette preuve
géométrique ne vaut ni application de la politique complète de la vallée sur
une autre zone, ni menu fonctionnel, profils globaux ou performance qualifiés.

Le rapprochement `cell-quality-validation-001.json` passe désormais sur les
entrées 007 : 851 tests, contrôles natif/WASM et cinq captures natives avec
ressources réellement préparées et fences couvrant la capture. Les cinq strides
conservent leurs bords/normales/poids et les cellules complètes leurs formes.
La table High/Ultra utilise les mêmes strides ; leur réutilisation ne vaut pas
équivalence des profils complets. Le getter CPU et cette preuve géométrique ne
doivent pas servir d'acquittement global au menu ou à la persistance futurs.

## Preuves exigées

Tests de bords, budgets et pic de staging, refus atomiques, aller/retour,
identités stables, source périmée, acquittement incomplet ou dupliqué et conflit
entre contrôleurs. Puis parcours moteur : chargement asynchrone borné, terrain,
HLOD, déchargement effectif, collisions proches, jeu/save conservés, captures,
mémoire et frame times sur une région plus grande. Les tests de domaine ne
cochent pas à eux seuls R7-01/R7-02.
