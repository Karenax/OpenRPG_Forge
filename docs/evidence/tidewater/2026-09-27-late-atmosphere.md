# Chargement tardif de l'atmosphere

Le moteur separe register(App) et stage(World, Input). L'enregistrement installe
une seule fois l'extraction et le calcul avant la camera. Le systeme reste
inactif sans entrees. stage remplace entrees et cibles dans le monde principal ;
Bevy les extrait ensemble. Les shaders sont partages par Arc sans recopier
leurs chaines chaque frame.

Chaque remplacement alloue des cibles neuves et un indicateur ready distinct.
Les consommateurs doivent adopter les nouveaux handles ; les anciens restent
valides tant qu'ils sont conserves. install preserve le parcours initial et
son refus d'une seconde installation.

Le mode tidewater_atmosphere_probe SOURCE NEW_OUTPUT --reload attend huit
frames sans entree, installe une premiere generation, attend son calcul puis
la remplace. Il verifie des handles distincts, l'ancien ready vrai et le nouveau
ready faux, puis relit les quatre sorties de la seconde generation.

Validation native reussie : generations installees aux frames 8 et 10, exit 0.
Les quatre sorties native-late-atmosphere-001 sont identiques octet par octet a
native-shared-atmosphere-001. Le parcours initial a aussi ete rejoue dans
native-extracted-atmosphere-001 avec les quatre sorties identiques.
Rapport local : E:/OpenRPG_Forge/.tidewater-integration/late-atmosphere-proof-001.json.
Clippy strict cible et check Studio WASM reussis. Avertissement preexistant
proc-macro-error2. La seule correction apres execution est le regroupement
stylistique de deux conditions imbriquees dans le systeme de preuve.

Cette etape ne raccorde pas encore le cache Studio et ne remplace pas
l'historique temporel des nuages. Pas de nouvelle qualification de scene
complete, de rendu WebGPU dans le navigateur ou d'edition dans l'interface.
