# Apercu hors ecran du ciel importe

Le renderer existant produit une image RGBA8 sRGB 1920x1080 a camera de reference
fixe, depuis ResidentEnvironment. Aucun second moteur ni seconde application
Bevy n'est cree. Une camera et un quad temporaires utilisent la couche 31,
distincte de la scene. La materialisation reprend les memes LUTs, panorama,
historique nuageux et parametres lunaires que le prototype natif.

La lecture GPU attend 60 frames puis quatre images non uniformes identiques.
Le calcul possede un delai maximal ; camera, quad et lecteur sont supprimes
apres resultat. L'image utilise la vue originale, donc les rayons sous l'horizon
sont sombres en l'absence de terrain et d'eau : cet apercu n'est pas une carte.

Studio ajoute un bouton explicite dans la bibliotheque, puis affiche les pixels
dans un canvas 2D. La navigation, l'eclairage de travail, les selections, gizmos,
guides de chemins et brosse de terrain excluent la camera d'aperçu. Le contexte
2D sert seulement a afficher les pixels calcules par Bevy. Les capacites web-sys
HtmlCanvasElement, CanvasRenderingContext2d et ImageData sont declarees.

Preuve native : tidewater_cloud_weather_probe --package-preview ROOT PIN OUTPUT.
Sortie native-sky-preview-001/sky.rgba8.bin, image native-sky-preview-001.png,
rapport sky-preview-proof-001.json sous E:/OpenRPG_Forge/.tidewater-integration.
SHA256 pixels : 6ed61470d938e4e8f90b46b648e3676c547e71b5e2a200bdb2a5074b21244418.
Les 200 premieres lignes sont identiques pixel par pixel a
native-shared-cloud-scene-001.png. L'image complete a ete inspectee : ciel bleu,
nuages diffus, horizon, puis fond sombre sans geometrie. Cette comparaison ne
couvre ni la carte, ni la partie inferieure masquee dans la scene assemblee.

Le check Studio WASM et Clippy strict moteur passent. Clippy a demande une
iteration as_chunks sur les pixels, corrigee sans changer le calcul GPU. L'action dans un navigateur avec
une zone active reste a qualifier ; aucune validation UI implicite.

Une image en cours n'est adoptee que si l'identite de sa generation correspond
toujours a ResidentEnvironment. Un remplacement masque l'ancienne image. Une
zone active est requise pour disposer du renderer ; le bouton indique cette
condition au lieu de creer une application de rendu separee.

Les nouveaux diagnostics Clippy Studio (type de filtre camera et ponctuation)
ont ete corriges. Le lint strict reste bloque par le seul diagnostic preexistant
atelier_ui.rs:122 (bind_main, 103/100 lignes). Aucun contournement de ce lint.
Les changements apres capture native portent sur les filtres des outils et
l'iteration CPU des pixels ; ni shader ni camera de reference n'ont change.
Le remplacement du chargement invalide aussi les captures en attente quand
la nouvelle bibliotheque ne contient aucun environnement.
