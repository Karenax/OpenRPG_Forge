# ADR-0048 — Raccordement des surfaces à l'éclairage source commun

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Écart et décision

Le matériau de végétation du monde conserve la géométrie, le vent, les LOD et les
surfaces acquis, mais substitue encore la translucidité et l'éclairage PBR Bevy à
leur modèle source. L'environnement admis par ADR-0047 ne suffit donc pas.

Raccorder ce matériau au même éclairage direct, SH, cube HDR et aux récepteurs
d'ombres existants. Conserver les fonctions source et les mécanismes communs de
déformation, terrain retouché et motion vectors ; ne pas réécrire leurs calculs.
Les deux modèles d'éclairage possèdent une spécialisation explicite. Les scènes
historiques conservent leur chemin tant qu'aucune entrée source complète n'est
admise ; une entrée source incomplète ne justifie aucun repli silencieux.

L'exécution et la préparation des ombres de heightfield sont transférées de la
crate de carte vers `forge_engine::source_shadows::heightfield`. Les gardes,
formats, demi-précision, layouts, UUID et cadence sont conservés. Studio et
référence emploient le propriétaire commun, sans façade dans la carte.

La préparation/sélection des casters végétaux est également transférée dans
`source_vegetation::scene::shadow`, avec ses tests. Le kernel de relief existant
devient du code moteur attribué MIT : commit source `1438b1ab…`, corps WGSL de
SHA-256 `2f9a63f8…`. Seule sa notice est ajoutée. `PreparedInput::from_heights`
réemploie ce kernel et le packing existant sur les hauteurs de la scène ; il ne
recherche aucune acquisition sur disque. L'entrée explicite de shader reste
disponible pour les acquisitions compatibles et leurs contrôles de provenance.

Le layout commun requiert un vrai binding de profondeur avec sampler de comparaison,
même dans la spécialisation Bevy qui ne l'échantillonne pas. Un asset partagé de
1×1×3 `Depth32Float` fournit ce binding ; il n'active et ne qualifie aucune ombre
source. Il est compté dans l'inventaire GPU (huit images pour l'herbe historique,
contre sept auparavant). Les anciens reçus restent inchangés. La spécialisation
source remplace ce binding par la vraie génération admise.

## Preuves exigées et limites

Vérifier les fonctions conservées, le staging des ressources, les refus d'entrées
incomplètes, la géométrie/alpha/motion et les pipelines natif/WebGPU. Le module doit
consommer les ressources de sa scène réelle ; ni une extraction ni une définition
de matériau ne prouvent cette consommation. Vérifier ensuite le parcours et les
images, puis comparer comportement et performances à conditions identiques.
Les ombres terrain/objets/plantes, les surfaces hors végétation, Studio, profils
et critères généraux restent à rapprocher avant clôture de Gate 7.

## Activation dans le module

L'hôte végétal commun admet explicitement le profil source lorsque l'aire déclare
son environnement fermé. Il prépare les casters à partir de sa projection
canonique et le heightfield à partir du terrain effectif. L'environnement est
stagé avant ses consommateurs ; seuls les matériaux possédés sont raccordés.
Le shader reçoit le domaine du heightfield résident, sans coordonnées de carte
fixes. Les scènes sans cette capacité conservent leur spécialisation historique.

Les meshes opaques résidents de terrain et d'objets alimentent le graphe commun ;
les casters végétaux gardent la préparation et la sélection existantes. Les
changements de meshes renouvellent les ressources et leurs inventaires, sans
réutiliser un ancien acquittement. La disponibilité exige une génération encodée
et une completion GPU datée. Les meshes standards découpés par alpha ou skinnés
requièrent leur caster approprié ; l'adaptateur opaque refuse de les traiter
comme des solides non déformés. Leur couverture générale reste à réaliser.

Le vent des casters provient de la frame publiée des plantes visibles, sans
seconde horloge. Les 80 octets des paramètres de nuages rejoignent cette même
frame (176 octets au total), afin de conserver des matériaux stables pendant le
déplacement. Modifier tous les matériaux à chaque frame provoquait une forte
régression de spécialisation ; le natif 044 la conserve. Le natif 045 a révélé
une conversion incorrecte de caméra au retour. L'hôte appelle désormais
`CascadeView::from_camera_matrix`, déjà présent et testé dans le moteur ; le
contrôle strict du solveur reste inchangé. Aucun second adaptateur n'est conservé.

Le bilan central `module-surface-live-validation-001.json` rapproche les essais
et les limites courantes. Les récepteurs du terrain et des objets standards sont
encore PBR. Ce raccord végétal ne vaut ni parité complète, ni qualification des
quatre profils ou des performances release, ni clôture de Gate 7.


## Récepteurs communs et lumières locales — complément du 25 octobre

Le terrain et les objets standards consomment ensuite la bibliothèque commune de
soleil, SH/HDR et réception d'ombres, selon `shared-surface-validation-001.json`.
Les matériaux conservent leurs textures, palette et prépasses ; les dépendances
de la base et de l'extension sont déclarées explicitement.

La réception des lumières photométriques ponctuelles et spots et des ombres
ponctuelles existantes est rétablie par appel des fonctions Bevy, dans
`surfaces/native_lights.wgsl`. Le matériau composé garde la voie native des casters.
Le bilan `shared-local-lights-validation-001.json` conserve les refus de shader et
d'isolation des premiers essais, puis le résultat strict de l'essai isolé 008 :
contributions et témoin noir vérifiés, égalité exacte refusée sur un pixel pour
chaque comparaison ponctuelle/spot, avec un niveau maximal de canal. Aucune
tolérance ne remplace cette exigence. Les natifs de module 054/055 passent.

Cela ne transfère pas encore les lumières privées en unités source de Tidewater.
L'hôte des surfaces reste activé depuis la végétation : rendre son activation
indépendante demeure obligatoire. Les mécanismes réutilisables doivent avoir un
propriétaire moteur commun consommé par Tidewater et les autres modules ; chaque
module fournit ses données et réglages. Ce complément ne qualifie ni tous les
profils, ni Studio, ni la parité visuelle/performance complète, ni Gate 7.


## Cycle de vie indépendant de la végétation — complément du 25 octobre

`source_lighting::surfaces::scene` reprend la préparation du relief, la collecte
des casters opaques, le graphe, son invalidation et ses générations. La source de
casters végétaux est optionnelle ; la préparation d'un terrain admis ne demande
aucun paquet de plantes. `surfaces::live` pilote ces ressources après l'hôte
d'environnement, avant leurs consommateurs. La frame courante/précédente et ses
176 octets résident dans `source_lighting::frame`. Son adaptateur de vent est
déplacé à l'identique ; les plantes et l'herbe lient la publication commune.
Le renouvellement de leurs dépendances reste dans leur propre hôte.

L'observation des surfaces reçoit le token de l'inventaire réel du consommateur.
Son numéro de completion est lu dans ce même inventaire, primaire ou scoped ;
une observation de plantes absentes ne fournit plus cette information par défaut.
La preuve graphique exige aussi les surfaces lorsqu'un environnement source est
déclaré, et date leurs changements de génération et révocations sans gonfler les
quotas de trace. Les tests conservent les refus de dates, de quotas et de tokens.

`shared-scene-validation-001.json` rapproche le parcours complet 058, le module
historique 059 et l'essai 002 sans végétation. Ce dernier utilise un nouveau paquet
fermé, sans plantes ni leurs blocs, composé avec les codecs ordinaires ; les
originaux restent inchangés. Il prouve les 330 récepteurs, zéro lot végétal et des
cascades réellement encodées avec une completion GPU. Les captures sont inspectées.
La portée reste le profil source raster 2048 déclaré : les autres profils,
Studio, les casters alpha/skinnés, les lumières en unités source encore privées,
la parité d'images stricte et les performances appariées restent ouverts.


## Lumières locales en unités source — transfert du 25 octobre

`source_lighting::local_lights` reprend le collecteur f64 et son packing, la
collecte ECS, la projection des sources sur des owners vivants, le récepteur
bâtiment et son WGSL, ainsi que la publication du buffer partagé. Le UUID,
les huit slots, les 448 octets, l'ordre de sélection, les phases, les gardes de
pose/tag/révision et la remise à zéro après refus sont conservés. Les acquisitions,
attestations et associations spécifiques des paquets Tidewater restent dans
l'intégration. Les anciens modules ne servent pas de façades.

`scene_camera::FrameTag` possède le tag transitoire existant. Les producteurs
réels implémentent les protocoles `PreparedView` et `TemporalSelection` ;
`register_with_temporal<P,C>` lit leurs composants/ressources existants, sans
copier leur histoire. Les consommateurs hors séquence temporelle gardent leur
publication par caméra et révision. Une sélection temporelle exige toujours le
packet préparé de cette même caméra. Le type d'adaptateur enregistré ne peut pas
être remplacé silencieusement. La marque commune de caméra hors écran couvre
les captures de référence, dont le composant requiert déjà cette marque.

Le bilan `shared-source-lights-validation-001.json` relie la conservation exacte
des calculs et du shader, les paquets réels, les tests et les lectures GPU.
L'oracle de douze frames et 36 refus passe ; la sonde de publication passe ses
six lectures sur les 32 lampes de 19 bâtiments lumineux et ses six lectures
synthétiques. Elle ne dessine pas leurs meshes. Les consommateurs Studio sont
compilés en WebGPU ; aucune exécution Studio n'en est déduite.

La consommation non vide de ces paquets par les matériaux communs du jeu de
modules reste à raccorder avec les compagnons d'assets fermés et les vrais
repères d'instances. Ne pas remplacer leurs unités par des lumens Bevy ou
substituer la preuve de transport à celle du dessin. Studio, les profils,
la parité et la qualification release restent ouvertes.


## Réception locale standard et terrain — complément du 25 octobre

`surfaces::Receiver` déclare le buffer local partagé en binding 257, en plus des
trois buffers de scène existants. Sa visite de dépendances inclut cette identité.
Le staging crée le paquet neutre avec le propriétaire `local_lights::gpu`.
Les surfaces standard et terrain appellent le récepteur dérivé du même
`local_lights/receiver.wgsl`, dont les octets acquis sont conservés. La dérivation
contrôle les ancres uniques puis remplace les identifiants, le type de surface,
le binding et les accès au milieu par des paramètres explicites ; les calculs de
sélection, distance, spot, transmission et BRDF ne sont pas réimplémentés.
Les surfaces sèches admises passent des coefficients optiques nuls.

`verify_surface_local_lights --source-units` emploie le collecteur ECS et la
publication GPU existants, une lampe source déclarée et un inventaire attendu
indépendant. Le dessin standard/terrain passe avec une vraie contribution ; jour,
désactivation et inventaire contradictoire retrouvent exactement le noir, puis
la reprise reproduit exactement l'image éclairée. La sonde native photométrique
conserve ses critères stricts et son échec d'un pixel. Ses six images sont
identiques à la campagne 008. Le bilan central est
`shared-source-receivers-validation-001.json`. La fermeture/export des compagnons,
leur projection sur les instances réelles, la réception végétale, le Studio,
les profils et les performances release restent à qualifier.


## Référence persistante du compagnon lumineux — complément du 25 octobre

Le placement commun possède un `LocalLightPackageReference` optionnel, schéma 1.
Il fixe les empreintes du porteur, du manifeste du `SourceLocalLightsPackage`
existant et de son descripteur ; il sélectionne des IDs source triés et des repères locaux triés.
Un repère est relatif au parent déclaré par le descripteur, ou au modèle pour
une racine. Les positions/intensités/phases originales restent dans le paquet
immuable. Aucun identifiant Bevy, buffer, chemin local ou pose monde acquise
n'est sérialisé dans cette référence. L'absence reste omise dans les anciens
objets. Les données nouvelles exigent le head 18 et les archives 49/50/51,
ajoutées au registre figé des codecs ; les familles antérieures sont conservées.

Le remplacement compare aussi le compagnon lumineux attendu ; le remplacement
simple refuse de l'abandonner implicitement. La duplication, les transforms,
les verrous et l'historique suivent le contrat de placement existant. Le contrôle
Studio de retrait explicite des compagnons inclut l'éclairage.
`required_local_light_packages` rapproche manifest et descripteur pour le
checkpoint, le head, les états appliqués/inverses et les requêtes d'édition.
Les snapshots source et leurs versions retouchées participent à cette fermeture.
Un même manifeste ne peut pas être revendiqué avec deux descripteurs différents.

`projection::placement` prépare la fermeture des repères, contrôle les pins et
la racine canonique réelle, crée uniquement les anchors/émetteurs dérivés et
appelle le `PreparedSourceLights` existant. Les phases et ordres source sont
conservés ; le décalage d'ordre reste fourni explicitement. Le contrôle global
d'unicité de racine est effectué au staging, sans recherche globale par résident
à chaque frame. Toute tentative de staging/retrait invalide la collection.
Le retrait refuse les descendants étrangers et préserve le modèle.

Cette entrée de projection est validée par les tests de racines canoniques et
de conservation des phases. Son raccordement aux producteurs et chargeurs de
paquets du Studio et du jeu reste à réaliser. Les API de module 1–4 refusent les
références lumineuses dans l'archive complète, y compris lorsqu'elles restent
uniquement nécessaires à Annuler. Le futur contrat d'export doit fermer leurs
manifests, ressources, licences et preuves avec le codec de paquet existant.
`shared-light-bindings-validation-001.json` date les entrées et les contrôles.
Cette tranche ne produit aucune nouvelle qualification GPU, Studio installé,
profils, performance release ou distribution finale.


## Transport commun des compagnons lumineux — 25 octobre 2026

L'API de module 5 ajoute `local_light_packages` : manifeste original épinglé,
descripteur épinglé, fichiers fermés et liste triée des zones qui en ont besoin,
historique compris. Les API 1–4 refusent aussi le champ explicitement vide.
Les limites restent bornées : 4 096 ressources par paquet et 512 Mio de transport
lumineux unique par module. Les pins des porteurs historiques doivent être
présents dans les assets déclarés ; les IDs sélectionnés et la fermeture complète
des repères sont contrôlés avec le codec de source existant.

Les ressources originales, dont des scripts acquis, sont des données inertes
`b64:` sous extension `.source-data` et chemin dérivé de leur SHA-256 original.
La licence originale reste lisible et déclarée. Le manifeste original et ses
noms restent inchangés ; le codec reçoit les octets décodés et vérifie leurs
longueurs et hashes originaux. Le validateur général de programmes de `.orpmod`
n'est ni retiré ni assoupli. La version encodée possède aussi ses propres pins.

L'exporteur commun ferme les dépendances avant publication locale ; les zones
secondaires peuvent seules exiger un paquet, les zones qui le partagent sont
dédupliquées, et toute ressource transportée en surplus est refusée. Le chargeur
installé expose les paquets depuis les seuls fichiers admis. Ce contrat ne
qualifie pas le rendu : le lecteur refuse encore explicitement les références
lumineuses tant que son producteur moteur n'est pas raccordé. Le bilan
`shared-module-lights-validation-001.json` conserve les essais et leurs limites.


## Producteur moteur et consommateurs des références enregistrées — 25 octobre 2026

Le producteur `local_lights::projection::placement::host` reçoit les placements
canoniques et les `SourceLocalLightsPackage` fermés. La préparation ordonne les
instances par identité et décale leurs ordres source sans changer leurs phases.
Le hash de l'inventaire attendu vient des documents triés ; aucune liste attendue
n'est reconstruite depuis les émetteurs effectivement présents. Le staging se
fait sur les racines `ManualPlacementProjection` réelles et uniques, puis attend
la propagation normale. Une recherche globale de racines par frame évite les
recherches globales répétées par résident. Les paramètres et repères restent
contrôlés par le projecteur existant ; les sources étrangères et les manques
bloquent la collection. Le producteur publie son état CPU et ses erreurs.

Le lecteur de modules charge ses paquets admis et installe ce propriétaire
commun ; il ne contient pas de solveur lumineux. Le contexte de réception actuel
exige l'environnement source déclaré. Le Studio conserve son join d'acquisition
spécifique, mais emploie le même lecteur commun des entrées de frame. Les vues
hors écran sont exclues par le marqueur commun, également requis par les captures
de réfraction. Une réservation exclusive interdit deux producteurs de scène,
indépendamment de l'adaptateur temporel du transport existant.

La collection transfère aussi le numéro de frame principale à la télémétrie de
mise en file. Le contrat de préparation du jeu refuse identité, inventaire,
caméra ou observation de file manquants, périmés, futurs ou contradictoires.
La fraîcheur conserve la borne de huit frames déjà employée par l'observation
du renderer ; une lumière sélectionnée n'est pas exigée de jour ou en mode
désactivé. Cette télémétrie ne remplace pas un fence, un dessin ou une lecture
GPU. Les étapes, essais et réserves sont dans
`shared-placement-light-host-validation-001.json`.


## Publication solaire consommée en commun — 25 octobre 2026

Le nouveau propriétaire `source_sky::publication` orchestre les fonctions déjà
présentes : `solar::Clock`, contrôleur/readback d'atmosphère et `FrameLighting`.
Il publie les paramètres de ciel et l'éclairage direct. Les SH vivants restent
la propriété du graphe HDR GPU. La présentation de référence conserve explicitement
son approximation par ratio d'irradiance sur les SH acquis ; ce chemin n'est pas
substitué aux SH vivants du Studio ou du module. Les joins propres aux matériaux
de carte restent dans leurs consommateurs. Les assets de ciel ne sont marqués
modifiés que lorsque leurs valeurs changent réellement.

L'API 6 ajoute `ModuleSolarClock` : minute acquise, minute initiale demandée,
azimut et durée de jour optionnelle. Les bornes sont validées ; une ancienne API
refuse aussi la présence explicite du champ nul. Le soleil acquis doit correspondre
exactement au calcul initial converti en f32. Une première lecture d'atmosphère
amorce la pose acquise ; un changement demandé attend ensuite sa propre lecture
avant l'avancement de l'horloge. L'hôte réutilise `submit_solar` pour les nuages,
le vrai soleil pour le HDR et la direction de la lumière principale pour les ombres.

Le contrôle d'architecture vérifie les appels directs de Studio, référence et
hôte moteur. Les 31 fichiers préexistants des sous-répertoires atmosphère,
nuages, ciel et lumières locales sont identiques à l'entrée de cette tranche ;
aucun shader ou solveur n'est réécrit. Le bilan central
`shared-solar-publication-validation-001.json` relie les tests et les captures.
Le natif prouve un paquet nocturne non vide et sa contribution sur terrain et
faces exposées de modèles après comparaison au retrait de la référence. Les faces
opposées restent des observations, sans exiger qu'elles s'éclairent. La preuve
ne qualifie pas encore plantes, skinned/alpha-cutout, profils, Studio exporté,
porteur original ou performance release.


## Réception végétale des lumières locales — 26 octobre 2026

Le générateur commun `receiver::plant_shader_source` raccorde les quatre surfaces
végétales à `surface_shader_source`, déjà consommé par terrain et modèles. Les
coefficients optiques du cas sec restent nuls. Le paquet, ses unités source,
ses équations et son transport par caméra sont conservés. Le matériau de monde
et le matériau acquis déclarent le même buffer partagé, au binding 56 visible
du fragment, avec dépendance d'asset explicite et installation neutre contrôlée.
L'hôte de monde refuse une substitution de ce handle. Le Studio et la référence
construisent aussi ce champ ; leur exécution visuelle spécifique reste à rejouer.

Les instances et matrices sont visibles du vertex ; les cascades et lumières
locales du fragment. Éclairage de scène et frame/motion gardent les étapes qui
les lisent réellement. Le probe GPU comptabilise les layouts complets, y compris
ceux de Bevy : maximum six buffers vertex et huit fragment, inférieur ou égal
à la limite WebGPU par défaut de huit. Aucune limite demandée n'est relevée.
Ce comptage et le contrôle WASM ne remplacent pas une exécution WebGPU navigateur.

L'inventaire strict exige maintenant huit buffers préparés pour l'herbe source,
six pour sa spécialisation PBR. Les nombres précédents et les inventaires en
surplus restent refusés. Les fixtures CPU créent le buffer qu'elles déclarent.
La composition privée conserve les ressources et recalcule le pin du document
végétal effectif après changement de révision par la commande de zone normale.
Son premier refus et le premier refus natif de l'ancien inventaire sont conservés.

Le probe PBR avait encore une hypothèse d'ancien bypass de LOD pour son palmier
synthétique. Il renseigne désormais la plage acquise, comme `Resident::stage`,
et conserve les seuils visuels. Le probe passe sans neutraliser la sélection du
moteur. Les modules nocturnes conservent le vent auteur animé : leur comparaison
visuelle n'est ni une parité de géométrie figée ni une mesure de performance release.
