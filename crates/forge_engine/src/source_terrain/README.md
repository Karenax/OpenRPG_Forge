# Terrain source commun

Le moteur possède le matériau et ses liaisons, les textures, le CDLOD, les
normales/AO et la préparation/remplacement d'un terrain acquis. Le lecteur fermé
du paquet appartient à `forge_assets::source_terrain_package`. Il ne lit pas le
filesystem et ne cherche aucune carte : le consommateur fournit les octets épinglés.

Le profil acquis est borné à un raster 2048², une maille d'un mètre, l'origine
[-1024, -1024] et une étendue de 2048 m. Le paquet v1/v2 conserve ses identités
historiques, textures et programmes. La source des hauteurs est admise séparément
par le document canonique ; une géométrie compatible ne prouve aucune provenance.

`scene::register` installe les consommateurs. `scene::PreparedTerrain::new`
prépare la géométrie et les données avant `stage` ; `scene::Bindings` fournit
l'éclairage et l'eau de cette scène. Le source sous-jacent et ses corrections
restent dans le document, les assets/entités restant dérivés.

`lod::ViewGeometry` réutilise les bornes du document pour sélectionner les nodes.
`resident_tiles::ResidentTiles` conserve les patches inchangés et borne son cache
inactif à max(256, nombre de patches actifs). L'appelant possède sa durée de vie
avec la source et la racine ; ce cache ne constitue pas une admission atomique des
cellules/collisions ni une barrière de complétion GPU. Le consommateur de capture
et sa demande conservative appartiennent à `source_surface::capture` ; les entrées
matérielles et liaisons sous l'eau appartiennent à `source_surface::underwater`.

Les sondes communes `verify_source_terrain` (assets), `source_terrain_probe` et
`source_terrain_ao_probe` (engine) reprennent les vérifications existantes :
admission/refus, lectures asynchrones, aller/retour de caméra, remplacement/refus
atomique et AO exacte. Les quatre tests de résidence font partie des tests moteur.
La qualification du 26 octobre est cataloguée dans
`shared-terrain-owners-validation-001.json`, campagne centrale Gate 7 : 2 748 tests,
25 frontières, Clippy natif/Web, sondes réelles et captures natives inspectées.

Le viewport canonique des modules emploie encore son matériau auteur. Son raccord
au profil source garde les barrières de publication existantes. L'API de module 8
déclare maintenant cette liaison de ressources de zone ; les bilans centraux
`module-terrain-resources-validation-001.json` et
`module-terrain-transport-validation-001.json` qualifient admission/réexport puis
autorité native, acquisition commune et cache. Ils ne basculent pas le matériau
du viewport et ne qualifient ni peinture ni exécution WebView.

Le 27 octobre, `module-terrain-paint-validation-001.json` qualifie séparément
`paint::PreparedPaint` dans cette scène commune. Les retouches explicites du
document reprennent les poids herbe/terre/roche de l'évaluateur auteur existant.
Un buffer RGBA32f recadre les retouches avec une bordure nulle, sous 128 Mio ;
les trous restent neutres. Sans retouche, le programme source garde ses octets.
La palette WGSL existante réside dans `terrain_palette.wgsl` et les deux matériaux
l'appellent ; ses calculs sont conservés par reconstruction inverse vérifiée.
Le détail réutilise une liaison texture disponible et ne relève pas leur nombre.
Le champ conserve la diagonale du raster auteur et la normale macro du programme
source. La géométrie, les collisions et les gardes de publication restent séparées.

Les 2 757 tests principaux, Clippy natif/Web, 25 frontières et la sonde CPU passent.
Sur le GPU, le terrain sans peinture reste identique octet pour octet et les
459 échantillons peints changent 48 825 pixels localisés. Les captures sont
inspectées, comme celles du module de régression ; ce dernier conserve encore
son matériau auteur. Le garde de chargement du module shader respecte les apps
CPU sans renderer. Les premiers refus et corrections sont conservés au catalogue.
Le parcours Studio doit encore prouver peinture, relief, sauvegarde et réouverture.
La sélection Nature, les autres effets, les profils,
la comparaison de performances et le rejeu Studio restent ouverts. Aucun gain
ni parité complète n'est déduit de cette extraction.

`host::PreparedHost` est maintenant consommé par le jeu natif lorsque la zone
déclare son profil terrain et son environnement. Il reprend cette scène et le
cache existants, sans lire le filesystem. Les cellules admises gardent leurs
identités/collisions et la partition utilisée par les autres systèmes ; leur
ancien dessin est masqué dans ce mode. Les dépendances viennent de la scène
d'environnement/ombres courante. Le récepteur de lumières locales existant est
aussi repris. Une disponibilité tardive ou révoquée des normales impose une
complétion GPU postérieure à leur disponibilité observée.

`LodMaterial` emploie `material_composition::ComposedMaterial`, généralisation du
wrapper existant des surfaces éclairées : les champs de la base et de l'extension
sont visités. Le type composé du SDK seul ne les visite pas. Le premier rejeu
est donc refusé malgré une image rendue ; le rejeu corrigé observe réellement
13 textures et 5 buffers, avec 57 meshes de patches et la physique synchronisée.
Le cache conserve aussi les matériaux des enfants lors d'un changement et retire
ses anciens enfants si sa racine change, en conservant les racines de l'appelant.

Le catalogue `module-source-terrain-runtime-validation-001.json` rapproche
2 764 tests principaux, 87 tests natifs, 126 de livraison, Clippy natif/Web,
25 frontières et les captures inspectées du paquet API 8. La référence et la
peinture conservent leurs PNG exacts ; le module sans ce profil passe aussi.
Ce résultat porte sur l'installation initiale du profil 2048² déjà borné. Le
viewport Studio, les remplacements/retouches et transitions de caméra, les autres
profils et les performances appariées restent à terminer. Aucun critère Gate 7
général n'est clos par ce seul raccord natif.
