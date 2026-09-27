# Préparation moteur de l'environnement depuis le paquet

`forge_engine::tidewater_scene_environment::PreparedEnvironment` prépare les
entrées atmosphère/nuages, le cube HDR, le shader du ciel, les paramètres de
sampling et l'éclairage du village à partir du seul EnvironmentPackage vérifié.
Aucun accès filesystem ni téléchargement n'est présent dans l'adaptateur.

Le calcul des paramètres atmosphériques et l'adaptation numérique de frontière
sont partagés avec la preuve native. L'adaptateur de métadonnées du contrôleur
nuageux est également partagé ; les exemples conservent leurs acquisitions IO.
Les tailles, hashes d'acquisition, 120 pas, atmosphère constante et dimensions
FHD sont vérifiés avant publication des inputs. Le cube utilise le décodeur HDR
partagé et l'éclairage utilise SceneLighting::new.

Studio effectue maintenant cette préparation avant l'adoption conjointe des
caches. Les types préparés sont stockés en mémoire ; ils ne sont pas encore
installés sur le renderer de l'éditeur. Le test navigateur précédent couvrait
le transport ; cette nouvelle préparation n'a pas encore été rejouée dans l'UI.

Le mode `tidewater_cloud_weather_probe --package ROOT PIN NEW_OUTPUT` lit un
seul paquet, puis utilise les graphes moteur atmosphère/nuages et les readbacks
existants. Il n'accède pas au clone upstream ni aux dossiers de fixtures.

## Validation

Check et Clippy strict ciblés passent. Build et exécution du mode paquet passent,
ainsi que le check Studio WASM (avertissement préexistant proc-macro-error2).

`native-package-environment-001` produit dix textures identiques à l'octet à
`native-shared-clouds-001`, avec 120 pas et 120 passages du graphe. Le cube HDR
préparé conserve exactement les bytes du paquet. Rapport local sous
`E:/OpenRPG_Forge/.tidewater-integration/package-environment-engine-proof-001.json`.

Deux copies de paquet à manifestes et hashes cohérents ont été testées :
`environment-package-invalid-light-001` contient une direction solaire nulle,
`environment-package-invalid-cube-001` contient un infini RGBA16F. Elles sont
refusées respectivement par SceneLighting et le décodeur HDR, exit 1, avant
construction de l'App GPU et sans création de répertoire de résultats. Les
logs négatifs et leurs pins sont conservés. Le paquet source reste intact.

Aucune capture de scène complète supplémentaire : cette preuve porte sur les
entrées préparées et les dix sorties GPU, pas sur un affichage dans Studio.
Les limites de la séquence restent explicites : 120 pas,
caméra/résolution fixes, paramètres globaux acquis, cube HDR acquis ; pas de
cycle horaire continu ni de scène complète Studio qualifiée.
