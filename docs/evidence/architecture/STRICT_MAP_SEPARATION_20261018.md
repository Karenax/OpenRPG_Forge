# Séparation stricte du moteur et de la carte — 18 octobre 2026

Décision et implémentation : [ADR-0026](../../ADR/ADR-0026-strict-engine-map-separation.md).
Les résultats structurés et les empreintes des sources sont dans
[le reçu](STRICT_MAP_SEPARATION_20261018.json) et [l'inventaire](STRICT_MAP_INPUTS_20261018.json).

## Organisation obtenue

`forge_engine` fournit les capacités génériques ; `forge_assets` leurs contrats
d'assets. Ils ne possèdent plus de code, shader, fixture, API ou notice Tidewater.
Leur fermeture de dépendances locales ne contient aucun composant de cette carte.

`forge_tidewater` possède les ports et projections de la carte, ainsi que leurs
shaders et exemples. `forge_tidewater_assets` possède ses acquisitions, recettes,
ports CDLOD, bornes de culling et empaquetage de végétation. Ces composants
consomment le moteur et les documents de domaine génériques. Les anciennes API
ne sont pas réexportées par le moteur ou par une façade de compatibilité.

Les notices et provenances existantes accompagnent les fichiers portés, hors du
moteur. Leur extraction ne supprime pas les droits et licences des fichiers
sources. Le moteur conserve ses licences et celles de ses dépendances génériques.
Les 38 shaders et 9 fixtures déplacés ont conservé leurs octets ; les versions
et sommes de contrôle des 938 dépendances externes du lockfile sont inchangées.

Le moteur ne consulte plus l'état de joueur amphibie de la carte. Une extension
publie des observations neutres dans `PlayerMotionPublication`, entre résolution
et caméra. Le retrait de cette extension réactive le contrôleur standard. Les
captures hors écran portent un marqueur générique utilisé par les outils de
terrain. Le domaine n'expose aux ports que ses données validées en lecture seule.

## Vérifications locales

Le reçu détaille les commandes, codes de sortie et empreintes des journaux.
La passe complète fixe les mêmes entrées de compilation avant et après exécution.
Les gardes d'architecture vérifient le sens des dépendances et l'absence des
sources de carte dans le moteur, ses contrats d'assets et leur fermeture locale.
La compilation du moteur seul couvre le natif, la configuration sans
fonctionnalités par défaut et WebAssembly. Clippy strict couvre le workspace
natif et Studio WebGPU. Le workspace compte 2 232 tests réussis, zéro échec,
56 tests ignorés, répartis sur 117 suites ; les tests ignorés restent ignorés.

Le [runtime générique](strict-engine-runtime-20261018/build.json) a exécuté deux
scènes primitives indépendantes de Tidewater :
[cour](strict-engine-runtime-20261018/courtyard-scene.png) et
[plateforme déplacée](strict-engine-runtime-20261018/raised-platform-scene.png).
Chaque lancement se termine avec le code zéro et 43 pipelines prêts, aucun en
attente ni en échec. Les captures ont été inspectées et leur zone centrale
présente 45 994 pixels modifiés sur 540 000. Cela vérifie ce parcours concret,
sans constituer une mesure générale de performances ou de qualité visuelle.

Les probes optiques sont des extensions optionnelles, maintenant possédées par
la carte. Le test natif a vérifié grain, flare et AO sur deux scènes procédurales,
avec contrôles identiques et comparaisons de pixels réussies. Leurs dix images
ont été inspectées sur une planche de contact. Leur propriété distincte est
explicite : ils ne prouvent pas que ces ports font partie du moteur générique.

Le [rejeu WebGPU](strict-map-optical-20261018/webgpu/proof.json) a également
réussi dans le navigateur, avec dix images inspectées sur la capture de la page.
Le navigateur expose le backend BrowserWebGpu et masque l'identité matérielle :
aucun modèle de GPU ne lui est attribué. Le test natif utilise Vulkan sur
AMD Radeon RX 7800 XT, pilote 26.8.1. L'onglet temporaire et le serveur limité
à localhost ont été fermés après export des preuves.

Le [premier parcours de carte](STRICT_MAP_DEVELOPMENT_SMOKE_20261018.json),
construit avec le profil de développement, a réussi les contrôles fonctionnels
(déplacement 8,996 m, 345 ticks au sol, caméra, backend, eau, vent, ombres,
préparation des poissons et staging des ressources Pebble). Son verdict global
reste **FAIL** : moyenne 40,87 FPS pour le seuil existant de 60. La simulation
progresse au taux prévu de 1,5 ; l'échec concerne la cadence d'affichage. Les
reçus et captures de cet essai sont conservés, sans modification des seuils.
Le [rejeu optimisé](STRICT_MAP_OPTIMIZED_SMOKE_20261018.json) réussit les contrôles
fonctionnels, avec un déplacement de 8.996 m et
332 ticks au sol. Son verdict global reste **FAIL** :
44.81 FPS moyens, sous le seuil inchangé de 60. La réserve de cadence reste
ouverte. Les paramètres d'optimisation sont ceux prévus par le reconstructeur de
livraison, avec instrumentation locale de qualification. Les entrées et le
binaire sont inchangés pendant le parcours. Une capture de vent de chaque essai
a été inspectée ; la capture finale conditionnée au succès global n'est pas
produite. Aucun résultat favorable n'est déduit de son absence. Les ressources
Pebble préparées et les poissons préparés ne constituent pas, seuls, une preuve
indépendante de leurs pixels visibles. Ce parcours borné ne qualifie ni toute
la carte, ni son édition Studio, ni une parité visuelle complète avec la référence.
Cet échec de cadence ne remet pas en cause les contrôles de dépendances et de
sources, mais interdit de présenter ce parcours comme entièrement validé.

## Échecs conservés et limites

La première configuration sans fonctionnalités exposait des gardes de compilation
incohérentes, corrigées avec les dépendances requises. Un nouveau test d'extension
utilisait un helper exigeant l'avancement du contrôleur standard alors qu'il
vérifiait sa suspension ; son pilotage a été corrigé sans supprimer les assertions.
Le test ciblé puis la suite complète ont réussi. Une première capture du runtime
à 90 images se terminait avant la préparation des pipelines ; ce refus a été
conservé. Le rejeu a utilisé une capture à l'image 240 et une sortie à 600,
sans modification du produit ni suppression des conditions de capture.

Les anciennes preuves datées et leurs résultats ne sont pas réécrits. La
séparation des sources et du graphe ne qualifie pas toutes les fonctionnalités
graphiques, la parité visuelle de la carte complète, une installation existante,
Windows vierge, Linux ou une distribution publique. Aucune CI distante, nouvelle
gate, mise à jour d'application installée, création de commit ou publication Git
n'est incluse. Les modifications locales étrangères à cette passe sont préservées.
