# Chargement tardif des nuages

Le graphe moteur expose register(App) puis stage(World, Input, count, atmosphere).
L'entree extraite contient aussi l'atmosphere optionnelle de cette generation :
un remplacement par un mode sans atmosphere partagee ne conserve pas une ancienne
ressource render-world. Les shaders et volumes source sont partages par Arc.

Une nouvelle identite de cibles remet a zero le compteur temporel, les uniforms
et les caches GPU de shadow/trace/temporal/panorama. Les anciennes cibles restent
valides pour les consommateurs qui les conservent. Les nouveaux consommateurs
doivent adopter les handles retournes. Les entrees invalides sont refusees
avant allocation et avant modification de la generation active.

Le parcours install initial reste disponible et refuse une seconde installation.
Son adaptateur reprend l'atmosphere RenderApp des exemples existants. Le nouveau
parcours stage recoit explicitement l'atmosphere, sans dependance implicite a
une ressource mutable separee.

Preuve native reussie : tidewater_cloud_weather_probe --package-reload ROOT PIN OUTPUT.
Deux generations successives, chacune 120 pas ; readback des dix sorties de la
seconde. Refus d'entrees invalides sans changement du nombre d'images ni des
handles actifs. Le renderer demarre huit frames sans entrees.

Le cache de Studio n'est pas encore consomme. Cette etape ne prouve pas le
rendu de la carte dans l'editeur, ni le cycle horaire continu, ni la performance.

Generations installees aux frames 8 et 129. Dix sorties de native-late-clouds-001
identiques octet par octet a native-package-environment-001 ; seconde generation
120 pas et 120 soumissions. Parcours initial rejoue dans native-extracted-clouds-001,
dix sorties egalement identiques. Rapport local late-clouds-proof-001.json sous
E:/OpenRPG_Forge/.tidewater-integration. Clippy strict cible reussi.

Check Studio WASM reussi ; avertissement preexistant proc-macro-error2.
