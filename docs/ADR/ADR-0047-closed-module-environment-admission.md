# ADR-0047 — Admission fermée de l'environnement des modules

Date : 25 octobre 2026. Statut : admission et parcours natif vérifiés ; qualification complète ouverte.

## Décision

L'API de module 4 ajoute une déclaration `environments` par aire. Elle référence
explicitement le manifeste d'environnement et sa fermeture de ressources, et
éventuellement les programmes HDR. L'absence de déclaration conserve les modules
existants ; une bibliothèque végétale n'active aucun environnement implicitement.
L'omission du champ conserve leur sérialisation historique. Un lecteur d'API 1–3
doit refuser le nouveau champ, y compris vide, plutôt qu'en ignorer le contenu.

Les codecs existants sont transférés dans `forge_assets::source_environment_package`.
Les identités de schéma, provenance, licences, limites et séquences acquises restent
inchangées. Le composant de carte conserve son acquisition d'animation, sans façade
de paquet. Les ressources livrées ont un rôle explicite et des pins dans le module.
Le lecteur ferme l'inventaire de chaque index avant ses lectures ; aucune recherche
dans le workspace, le Vault ou un ancien chemin d'acquisition n'est permise.
Le budget global d'environnement est de 64 Mio et 1 024 ressources distinctes.

`forge_engine::source_environment::host` reçoit des entrées déjà admises et possède
l'installation de l'environnement dans l'application. Il réemploie les graphes,
contrôleurs et publications communs. Les deux matériaux de ciel déclarent les deux
vues temporelles réelles : un changement de vue ne change pas silencieusement
l'inventaire couvert par la barrière GPU. Les handles d'images portent leurs vraies
dépendances. L'achèvement des étapes encodées, la publication HDR et une completion
de la file GPU restent des observations distinctes.

Le profil acquis fournit la lumière initiale. L'observateur suit la caméra réelle,
avec la quantification de hauteur source ; une mise à jour d'observateur seule ne
peut pas modifier le soleil ou la diffusion statique. Le changement solaire passe
par le contrat solaire existant, pas par une réinterprétation de ces octets.

## Vérifications du raccordement

Le bilan central `module-environment-validation-001.json` rapproche les reçus.
Le transport fonctionne après déplacement des sources et refuse une ressource
altérée, une fermeture incomplète ou un mauvais pin. Le paquet privé API 4 garde
104 des 106 payloads du précédent candidat à l'identique, ainsi que sa tête de zone ;
seuls le manifeste de module et ses crédits changent parmi ces anciens payloads.

Le premier essai 035 révèle que le contrôle graphique refusait le nouveau ciel.
Son arrêt ciblé et son échec restent consignés. La correction reconnaît uniquement
l'entité possédée par l'hôte commun ; les matériaux inconnus, ambigus et les ciels
sans propriétaire restent refusés. Une modification de profil exige une observation
GPU du ciel couvrant sa propre frame d'application, en plus du premier HDR publié.

Les natifs 036 et 037 passent respectivement six et 17 étapes sur le même paquet
autonome et le même binaire. Ils observent 120 étapes initiales de nuages puis la
continuation, 22 programmes préparés, une publication HDR et les deux matériaux /
cinq images de ciel préparés, avec completion GPU datée. Le 034 vérifie l'ancien
module sans environnement implicite. Les captures camp/bosquet sont inspectées ;
elles ne constituent pas une comparaison pixel du ciel ni un benchmark release.

## Limites à conserver visibles

Le ciel/nuages/HDR admis ne raccorde pas automatiquement les matériaux PBR du module
aux récepteurs PCSS/PCF et à l'éclairage source. Ce raccordement reste requis.
Les profils, changements de résolution, commande auteur d'environnement et parcours
Studio nécessitent leurs preuves propres. La présence de ressources ou une fin de
processus réussie ne prouve aucune parité de rendu.

L'exporteur de zone qui ne préserve pas encore la fermeture d'environnement d'une
origine doit refuser cette exportation ; il ne doit jamais produire silencieusement
un module qui perd son environnement. L'export fidèle reste un travail obligatoire
avant clôture de Gate 7. Aucune publication ni nouvelle qualification de gate n'est
implicitement accordée par ce contrat.


## Fermeture fidèle dans l'exporteur commun — 26 octobre 2026

La limite d'export décrite au 25 octobre est levée pour une origine admise et les
cartes conservées. `OriginDocuments` retient le lecteur du buffer immuable déjà
validé ; aucune recherche de ressource dans le workspace ou le Vault n'est ajoutée.
L'exporteur recopie les pins et rôles d'environnement, programmes HDR, végétation
et paquets lumineux requis, historique lumineux compris. Les déclarations ne
s'appliquent qu'à leur aire. Les données partagées sont dédupliquées et un conflit
avec une entrée d'export est refusé. Les codecs et inventaires fermés sont rejoués
sur le résultat ; le refus global d'environnement n'est retiré qu'avec ce chemin.

Les documents végétaux effectifs portent la révision de la tête : leurs pins sont
recalculés après la commande auteur, sans modifier les sources/prototypes. La
version d'API agrégée conserve le maximum requis, notamment 6 pour une horloge
solaire sur une carte secondaire suivie d'un ajout de lumières. Les candidats
lumineux du caller sont sélectionnés par le namespace existant avant fourniture
de l'origine ; l'admission complète et le refus des surplus restent obligatoires.

Le test privé `private_origin_keeps_all_source_companions_on_a_retouched_secondary_area`
exerce le même `build_module_with_origin` que le backend Studio : deux aires,
porteur retouché par commande normale, origine privée, conflit explicite et
relecture des compagnons. Les deux cas invalides sont refusés avant toute sortie.
Le paquet API 6 obtenu contient 629 payloads et passe le lecteur natif existant
sur l'aire retouchée avec ciel/HDR, lumières, plantes et collisions. La capture
est inspectée. Bilan : `shared-origin-companions-validation-001.json` dans la
campagne centrale. Le premier preflight du driver ne sélectionnait pas le namespace
de zones de l'exporteur ; son échec et le driver corrigé sont conservés.

Cette preuve de backend et compatibilité ne qualifie pas le bootstrap/viewport
Studio de ces compagnons, leurs commandes d'édition, un runtime fraîchement
reconstruit, le navigateur WebGPU ou une performance release. Le refus Studio
des compagnons végétaux reste explicite jusqu'au raccordement réel de ce parcours.
