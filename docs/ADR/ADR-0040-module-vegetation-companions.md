# ADR-0040 — Dépendances végétales explicites du module

Date : 24 octobre 2026. Statut : réalisation Gate 7 en cours.

## Besoin

Les sélections auteur d'espèces épinglent leurs catalogues complets. Les
populations importées possèdent aussi leurs paramètres de prototype. L'API
de module 2 ferme les blobs d'archive mais refuse ces catalogues non résolus.
Les inclure comme fichiers auxiliaires sans rôle et sans lien de zone ne
prouverait ni leur fermeture, ni la fidélité à l'auteur, ni leur lecture autonome.

## Décision

L'API de module 3 ajoute une table optionnelle `vegetation`, par zone, qui
épingle le document végétal effectif et ses dépendances de présentation. Elle
référence les catalogues d'espèces et rapports de kit existants, ou l'index et
les ressources du package de prototypes importés. Ce sont des références de
livraison : aucun nouveau monde, catalogue éditable ou état renderer n'est créé.
Les archives auteur gardent leurs identités et leurs octets. Les catalogues
d'espèces conservent leur contenu et leur hash canonique : leur fichier livré
utilise le JSON canonique, de sorte que son pin de fichier et le pin auteur
désignent exactement la même révision, normalisation comprise.

Les fichiers végétaux ont un rôle explicite dans l'inventaire fermé. Les pins
doivent correspondre à cet inventaire et aux références auteur. Ressource
absente, surnuméraire, altérée, de mauvais rôle ou hors budget : refus avant
publication/admission. Les dépendances partagées entre zones ne sont stockées
et comptées qu'une fois. Les licences restent déclarées comme licences ; les
catalogues ne confèrent aucun droit de redistribution.

Les API 1 et 2 conservent leurs contrats et leurs refus historiques. Les
exports sans végétation embarquée peuvent continuer à annoncer l'API 2. Un
module avec la nouvelle table annonce l'API 3. Les schémas de manifeste v2/v3
restent distincts de cette API. Les anciens lecteurs refusent l'API 3 ; aucune
compatibilité implicite n'est annoncée. La fermeture des sources d'archive
définie par ADR-0039 s'applique aussi à l'API 3.

La validation pure des références ne suffit pas à autoriser le transport :
l'adapter doit décoder chaque type de catalogue, fermer ses dépendances et
vérifier ses modèles et droits réels. Le runtime lit les seuls octets installés,
sans fallback vers le Vault ou l'espace auteur. Un décodeur ou renderer absent
reste un refus explicite. Le chemin de prototypes importés conserve ses vrais
paramètres/shaders ; un prototype standard ne s'y substitue pas en silence.

## Qualification

La table décrit les dépendances de présentation de la tête jouée. Elle ne
prouve pas la restauration des catalogues/modèles des révisions historiques
pour undo/redo. La fermeture d'archive d'ADR-0039 concerne les blobs source ;
elle ne prouve pas cette fermeture de catalogues. Le bootstrap auteur reste
refusé pour ces compagnons tant que leur restauration complète n'est pas
raccordée et qualifiée. Cette limite n'est pas une qualification d'éditabilité.

Exiger stabilité des anciens modules, fermeture par zone, partage de fichiers,
pins auteur/catalogue, intégrité et rôles, budgets et refus atomiques. Puis
exporter un vrai module via Studio, le charger depuis son installation,
observer végétation, LOD, déformations, collisions, admissions GPU et retour
sur la carte complète. Aucun succès de métadonnées ne qualifie ces parcours.

Le premier consommateur natif du 24 octobre charge les espèces gouvernées
depuis des tampons de fichiers installés, via la source mémoire du chargeur
existant. Son module privé vérifie ressources GPU et bindings physiques, avec
captures originales et retour court, dans `plant-host-native-001`. Il ne
qualifie ni export Studio, restauration historique, prototypes à shaders
importés, végétation raster, animation du vent, HLOD ou candidat complet.

### Lecteur du profil importé — 24 octobre

Le validateur de package source est déplacé vers
`forge_assets::source_vegetation_package`, sans dépendance de monde ou de rendu.
La façade `forge_tidewater_assets::tidewater_plants` réexporte le même type.
Le schéma, le commit, les six groupes, les 65 383 instances de description,
les dimensions de textures et la fermeture de fichiers restent gardés. Ces
constantes qualifient ce profil source ; elles ne limitent pas la population
auteur du module à une carte ou un nombre d'instances. Le profil explicite
partage les clés et l'ABI des paramètres avec le packer existant, sans seconde
représentation persistante ou second renderer.

Le lecteur de livraison rapproche l'index et chaque pin de ressource de
l'inventaire fermé avant leur lecture. Le rôle de licence reste explicite.
Il vérifie les familles, le document végétal, la tête auteur exacte, les clés
de prototypes et l'ordre des paramètres. Il conserve les octets du package
et décode les géométries CPU ; la somme des payloads géométriques dérivés est
bornée avant allocation, en comptant les réutilisations par niveau. Ce lecteur
ne confère aucun droit supplémentaire et ne valide pas le rendu du shader.
L'admission des compagnons est distincte de la disponibilité du renderer :
le jeu natif conserve un refus explicite de plantes importées jusqu'au raccord
des projections, textures, shaders, collisions et barrières GPU.

### Projection dérivée sur la partition admise — 24 octobre

`tidewater_plants::scene::streamed` consomme les géométries du package fermé
avec la population et les retouches auteur. Il réutilise le packing source,
sans imposer à une population de module le compte de la scène de référence.
Les gardes de celle-ci restent inchangées. Le choix de modèle dérivé suit les
cellules déjà admises ; un niveau absent réutilise le dernier modèle source.
Il ne s'agit pas d'une qualification générale d'agrégation ou de performance.

L'observation d'allocations moteur est étendue aux dépendances déclarées des
matériaux personnalisés et aux buffers de stockage. La requête est bornée,
possédée, liée à une génération et à une durée de vie ; la fin d'une durée
de vie interdit la réutilisation d'un ancien reçu après recréation. Un token
opaque requiert les vrais assets GPU, la vue, les pipelines et la fence.
Il ne certifie pas les pixels, le dessin de chaque instance ou Rapier. La
publication reste séparée de la disponibilité de gameplay, qui attend aussi
la physique et une fence couvrant la frame publiée. Le raccord natif de
lumière/ombres/mouvement, la physique et ses preuves restent dus avant de
lever les refus importé/raster dans le jeu.

Le shader source possède aussi des seuils internes de canopée/fougères et un
éclaircissement des imposteurs. Élargir les uniformes de distance ne les
neutralise pas. Le dérivé explicite active la sélection par cellules dans
`lodEndFrame.z`, inutilisé dans la source épinglée. Ce mode conserve la forme,
les variantes, textures, éclairage et déformations ; il retire les seuls
refus de distance et l'éclaircissement qui contrediraient la sélection admise.
Le mode de référence garde toutes les expressions source et reste actif
jusqu'à la première publication. Une dérive des ancres est refusée avant
staging du shader ; le mode cellules et la sortie de surface normale sont
contrôlés dans l'intégrité du résident. La compilation/réversibilité CPU ne
remplace pas une observation de rendu et de couverture native.

### Proxies physiques importés — 24 octobre

`physics::imported_vegetation` projette les intentions de collision de la
sélection canonique, indépendamment des modèles et distances de rendu. Ses
formes, poses, profils et identités sont préparés avant mutation ECS. Le
résident possède les entités et garde les formes effectives ; la concordance
du backend avec une forme remplacée ne suffit pas à prouver la source attendue.
Les groupes, rôles, activation et contexte par défaut sont contrôlés aussi.
Un second contexte ferme la barrière. Les paramètres de déformation visuelle
ne réécrivent pas le proxy de collision défini par l'auteur.

Les enveloppes de mouvement sont partagées avec le streaming terrain et
couvrent tous les corps mobiles : hiérarchie, forme, vitesse sur deux pas et
commande KCC publiée. L'activation garde une marge pour la taille du proxy,
de sorte qu'un observateur proche de son extrémité reste protégé. Un mouvement
invalide garde les obstacles actifs et ferme la disponibilité. L'hôte vérifie
l'état avant les actuateurs et rejoue sa barrière finale avant la physique.
Le scaling reprend celui de Rapier ; les underflows et retours de formes
dégénérées sont refusés avant publication. Une perte de propriété ne donne
jamais droit de supprimer une entité réassignée.

La préparation et les tests réels de registration/KCC ne qualifient pas le
rendu des plantes dans le jeu. L'hôte doit encore rapprocher tête, raster,
sélection, contexte de lumière/ombres/mouvement, inventaire GPU et physique,
puis attendre la fence couvrant la publication. Les gardes du jeu restent
actives jusqu'à ce raccord et ses preuves natives. Le contrôle commun de rôle
exige aussi l'accord ECS/backend : une demande de désactivation ou de passage
en capteur invalide immédiatement l'ancienne observation, avant `SyncBackend`.

### Éclairage du monde joué — raccord du 24 octobre

Le matériau `tidewater_plants::world::WorldPlantMaterial` adapte explicitement
le profil source épinglé à la lumière du monde Bevy. Le matériau de référence
`PlantMaterial`, son contexte capturé et son shader restent conservés. Les
résidents partagent leur projection par un contrat de matériau, sans nouvelle
population persistante ni second renderer. Le nouveau chemin accepte les
trois types de géométrie de plantes ; l'herbe, qui exige ses propres champs de
terrain, n'est pas admise par des textures neutres.

Les textures de surface, géométries, variantes et masques restent ceux du
package. L'environnement, les sources lumineuses et les cartes d'ombres sont
ceux de Bevy ; les champs capturés de relief, nuages et cascades du shader de
référence ne sont pas remplacés par des constantes ou images factices. Les
forces et masques de translucidité deviennent une transmission diffuse PBR ;
le lobe directionnel et la teinte de transmission du modèle d'éclairage source
ne prétendent pas être conservés pixel pour pixel. Le F0 diélectrique source
est converti dans le paramètre de réflectance Bevy.

Un buffer partagé porte caméra, vent, temps et gusts courants/précédents.
L'hôte le publie une fois par trame ; valeurs invalides et buffer absent
refusent l'actualisation sans avancer la pose précédente. Ombres et prépasses
réutilisent la déformation et les masques de surface ; la caméra principale
pilote les billboards même pendant la projection d'une lumière. Le sommet
précédent est redéformé pour les vecteurs de mouvement.

Le programme `verify_world_plants` vérifie ce matériau séparément avec les
ressources source installées, un sol de contrôle et de vrais éclairages Bevy.
Son éventuel succès ne vaut ni qualification du raster de module, ni mesures
de performance, ni équivalence visuelle avec le renderer de référence. Les
gardes du jeu restent dues jusqu'au raccord et à sa qualification complète.

Le contrôle natif refuse aussi un stretch vertical de canopée nul, non fini
ou dont l'inverse déborde, avant d'allouer meshes, matériaux ou buffers.
Le champ `iDat.y` est un stretch signé pour la canopée ; il reste une valeur
de lean pour les palmes. Un affichage proche ne suffit pas à admettre une
canopée dont l'imposteur divise par zéro. Ce contrôle ne réécrit aucun paramètre
et ne modifie pas le packer de référence. La fixture privée de transport
`plant-import-private-001` possède ce défaut ; son archive et son installation
historiques restent intactes. Le programme GPU prépare une autre tête et un
autre hash de bloc en mémoire, avec douze stretches explicites de 1, en
consignant cette correction et l'absence de modification du module installé.

### Enveloppes du renderer natif — 24 octobre

`Prepared::world_envelopes` dérive les enveloppes des meshes, matrices et
records de la projection préparée. Le profil de shader doit être celui du
matériau natif épinglé. Le résultat est lié à la tête auteur, au raster, au
document végétal complet et aux contenus/poses source f64 ; son `select`
consomme la partition de terrain effectivement admise. Une pose modifiée,
même sous la précision de packing f32, ou un autre document ne réutilise pas
ces enveloppes. Aucun rayon de requête source ne sert de taille de couronne.

Les palmes utilisent les longueurs des meshes de tige et de couronne et les
rotations qui conservent ces longueurs ; seuls les déplacements de leur axe
et centre ajoutent une marge de mouvement. La canopée utilise les parties
de géométrie du bon groupe, les déplacements possibles de lobes et les
maxima de poids de sway/branches/flutter, avec le vrai opérateur de normales
de la matrice non uniforme. L'imposteur inclut ses constantes source et tous
les angles de billboard. Tous les niveaux réellement présents sont réunis.
Le rayon statique est normalisé par l'échelle maximale de chaque instance ;
la déformation garde son unité monde, selon le contrat du sélecteur.

Le matériau et les enveloppes partagent la limite de vent source **0 à 25**,
correspondant à une force jusqu'à 2,5, tempête de son profil. Une trame hors
plage est refusée avant actualisation du buffer ; le renderer de référence
garde sa politique existante. Les calculs f64 sur données f32 réservent 512
epsilons f32 et 1 mm pour l'accumulation numérique. Les matrices de normales
non représentables, rayons au-delà de 10 000 m avant échelle et mouvements
au-delà de 128 m sont refusés. Au plus 128 résumés géométriques sont scannés,
pour éviter une analyse complète du mesh par instance sur des classes de
seed adverses. Ces bornes de préparation ne valent pas mesure de mémoire
ou de frame time du candidat complet.

Les oracles numériques réévaluent séparément les équations de sommets source
sur vent, temps, gusts, lobes et angles de vue. Ils ne sont pas une lecture
des positions produites par le GPU. Le programme natif est étendu à dix
profils synthétiques partageant les vrais prototypes installés, sous une
tête de fixture distincte ; sa qualification et les refus restent consignés
dans la campagne `plant-bounds-*`. Le raccord au jeu et sa barrière physique
commune restent nécessaires avant qualification du parcours raster.

### Hôte raster de module — raccord du 24 octobre en qualification

`scene::streamed::host::PreparedHost` ferme population, tête, terrain,
géométrie, textures et shader avant installation. Il conserve une vue dérivée
immuable des placements déjà reprojetés : ce cache n'est ni sérialisé ni une
nouvelle source auteur. Seule une nouvelle partition terrain admise recalcule
la sélection visuelle ; les collisions utilisent les mêmes sources canoniques,
indépendamment de leur LOD. Les contrôles de disponibilité inspectent encore
les résidents possédés et les vrais backends à chaque pas.

Le vent reprend la direction normalisée (0,82 ; 0,57) du renderer végétal
générique. La force auteur 0–1 couvre la plage source 0–25 ; la fréquence
auteur fait avancer le temps source de 2π fois sa valeur par seconde. Le
déplacement de gust suit cette direction et cette vitesse, modulo 8 540 m,
période de la texture source. Le delta réel de présentation est plafonné à
0,1 s. Caméra principale, vent et états précédent/courant sont publiés ensemble
après propagation des transforms ; aucune pose source n'est modifiée.

La barrière précoce intervient après la demande terrain et avant les contrôles
du gameplay et du personnage. La finale intervient après les actuateurs,
la barrière terrain et l'activation des colliders végétaux, avant SyncBackend.
Elle peut suspendre la simulation, retirer les translations KCC et désactiver
les actions ; un succès ne lève jamais une suspension détenue par un autre
adapter. La première publication attend l'inventaire GPU, puis une fence
couvrant la publication et la concordance Rapier. Une attente de 120 s ou une
entrée invalide termine l'essai en erreur explicite.

Le jeu ne lève le refus raster que pour un `PreparedHost` lié exactement à sa
tête et son terrain. Le chemin procédural des espèces et les refus sans cet
objet restent conservés. Son rapport de projection rapproche séparément
identités, nombre d'instances, LOD, collisions et admissions. Les DTO du rapport
ne dépendent pas du renderer ; les tests de livraison les utilisent aussi.

La fixture `plant-game-world-001` construit un nouveau document importé v2
sur le raster privé de 2 km, avec douze stretches explicites de 1. Elle conserve
les vrais prototypes et leurs pins ; le module de transport historique reste
intact. L'intégration et ses rapports ne qualifient pas seuls le parcours,
les quatre profils, les coûts ou l'export Studio. Les résultats du rejeu réel
et les refus sont conservés sous `plant-game-*` dans la campagne centrale.

Le rapprochement final `plant-game-validation-001.json` vérifie désormais le
raccord sur une fixture privée raster distincte, `plant-game-world-002` : les
arbres et le départ sont éloignés des PNJ conservés afin que leur protection
physique ne maintienne pas artificiellement le bosquet au détail. Le premier
cas demeure une preuve de ce maintien, sans transition lointaine revendiquée.
Le second observe détail → groupe → détail et retour, après inventaire GPU et
concordance physique. Un autre parcours observe les contraintes normales du
KCC, son glissement tangent autour du cylindre auteur, le contournement et le
retour. Les rapports d'analyse conservent les reçus des protocoles initiaux en
échec. Ces cas ne qualifient pas les profils, les performances, le candidat
complet ou l'export Studio des compagnons.
