# Tidewater — optimisations contextuelles intégrées, 9 octobre 2026

La synchronisation incrémentale de la réfraction, la visibilité du village statique et la veille des poissons sont intégrées au runtime du launcher. Comparaison locale appariée : **82.93 FPS pour 010, contre 95.66 puis 95.10 FPS pour 012**, soit **15.0 %** en moyenne sur ce parcours. Les préférences de contraste, anti-aliasing et la capture d'eau sont conservées ; la densité du ciel est augmentée.

## Changements

- Les miroirs de réfraction ne republient les matériaux, les maillages ou les poses que lorsqu'ils changent. Le contrôle désactivé ou l'absence de consommateur visible suspend aussi leur préparation et la caméra secondaire ; les ressources restent résidentes. Au réveil, deux encodages frais sont requis avant de proposer le résultat au matériau d'eau.
- La demande tient compte des bornes transformées et élargies de **tous les patches océaniques**, y compris ceux créés ou réutilisés pendant la marche. Une borne inconnue conserve la capture. Ce filtre reste conservateur : il ne prouve pas l'occlusion de l'eau par le relief et ne promet pas une suspension à chaque vue vers l'intérieur de l'île. L'eau visible au loin doit conserver sa réfraction.
- Les 145 nœuds de village utilisent le filtrage Bevy pour leurs objets statiques. Les objets déformés, le terrain, l'océan et les végétaux conservent leur politique adaptée. Les miroirs statiques utilisent le frustum actuel de la caméra principale pour éviter le décalage de la caméra de capture. Les ombres partagées restent indépendantes de ce filtrage.
- Les nuages passent de 240×135 à 960×540 points de trace par image : chaque texel de l’histoire est recalculé à chaque image, sans grille temporelle intercalée. Le panorama de repli passe de 512×160 à 1024×320, avec un angle de raymarching adapté. Les gros blocs de contours relevés sur la capture initiale ne sont plus observés sur les découpes natives comparées. Des contours fins de nuages lointains, déjà visibles sur 010 pendant la transition de midi, restent observables ; cette passe ne constitue pas une acceptation artistique complète du ciel. La qualité est figée par génération ; les acquisitions d'origine et les autres appelants conservent le mode original. Le ciel détaillé est activé dans la scène du launcher.
- Les 83 poissons exposés ou décoratifs sont testés par une borne de groupe avant la boucle détaillée. Un groupe hors de toute contribution visuelle ou d'ombre devient dormant : aucun matériau détaillé ni pose à recalculer ; les parties et les ombres sont retirées une fois. La reprise utilise l'horloge absolue existante. Ce chemin ne contient pas de banc de poissons nageurs simulé.
- Les poissons invisibles évitent les copies de matériaux ; les poissons sans ombre évitent les écritures d'uniformes GPU. Les matériaux naturels, scannés et l'indicateur de disponibilité de la réfraction ne sont touchés qu'en cas de changement réel.

## Mesure appariée finale

Ryzen 5 7600 / RX 7800 XT, Vulkan, plein écran 2560×1440, Fifo, 09:30, mêmes entrées et préférences. Séquence **012 / 010 / 012**, sans compilation concurrente ni capture d'image pendant les mesures. Quatre phases de 6/10/6/10 s après 240 images de stabilisation ; une seconde exclue aux bords. Chaque cellule : **FPS moyens / p95 en ms / 1 % bas en FPS**.

| Phase | 012 première mesure | 010 référence | 012 seconde mesure |
| --- | ---: | ---: | ---: |
| 1 — Immobile | 95.11 / 12.23 / 71.57 | 83.61 / 13.38 / 53.07 | 95.59 / 12.20 / 73.22 |
| 2 — Rotation | 96.15 / 12.10 / 73.72 | 80.57 / 13.98 / 65.39 | 94.02 / 12.34 / 71.40 |
| 3 — Immobile | 96.14 / 12.39 / 77.97 | 85.53 / 13.05 / 67.13 | 95.99 / 12.18 / 78.00 |
| 4 — Rotation | 95.22 / 12.24 / 75.52 | 83.65 / 13.31 / 69.98 | 95.50 / 11.98 / 78.41 |

La capture est demandée et disponible pendant toutes les fenêtres retenues. Les miroirs publient au maximum 0 modifications de matériau par image dans ces fenêtres ; les objets immobiles ne sont plus réinsérés. Les invalidations globales du rendu sont de 209 à 322 objets par image sur le second essai, contre 1 135–1 247 dans l'analyse 011. Ce compteur couvre tout le rendu et n'attribue pas chaque gain à une seule optimisation. Le pass GPU de nuages mesure 0.485 ms sur 010, puis 1.224 et 1.217 ms sur 012. La densité supplémentaire a donc un coût réel, inclus dans la comparaison finale. Ces valeurs sont des diagnostics GPU récents, pouvant être décalés de quelques images. Les timers CPU et GPU ne sont pas additifs. Les gains sont locaux à cette scène et ce parcours.

## Validation

- Compilation optimisée du moteur et du runtime natif contre le SDK Bevy/wgpu fermé et vérifié : PASS. Sources finales et dépendances inchangées pendant la compilation.
- 34 tests ciblés moteur incluant qualité, publication et horloges des nuages, puis 5 tests des patches résidents avec leur déclaration de consommation : PASS. Tests de bornes, visibilité héritée, retargeting et jitter, miroirs inchangés, horloges indépendantes et contenu des poissons conservés.
- Contrôle Clippy moteur/exemple : aucune erreur et aucun nouvel avertissement dans les 22 fichiers modifiés. Deux avertissements préexistants restent dans l’entrée native (conditions imbriquées et type complexe) ; leur identité est comparée aux diagnostics avant changement. D’autres avertissements préexistants subsistent dans le snapshot ; ceci ne constitue pas un PASS Clippy global du workspace. Une annotation documente la transaction atomique de synchronisation.
- Terrain et rotations : 82 captures, ressources et pipelines actifs prêts. Fraction presque noire maximale du tiers inférieur : 0.3494 %. Captures et planche-contact inspectées ; aucun sol noir ou disparu observé dans ce parcours. Cette mesure est un indicateur, pas une preuve de chaque pixel de la carte.
- Réfraction : arrêt des nouveaux encodages et des publications pendant le masquage des consommateurs, puis reprise avec capture fraîche : PASS au rivage et à l'intérieur de l'île. Le masquage est une commande de preuve explicite, distincte d'une preuve d'occlusion naturelle. Les tests géométriques couvrent une surface hors champ puis de retour dans le champ.
- Poissons : le groupe est actif au rivage et entièrement dormant à la position intérieure (-60, -190), avec zéro instance préparée. Les tests conservent les ombres proches hors champ et le seuil de réveil avant affichage. Pas de qualification d'un parcours continu de retour vers l'étal déduite de ces deux lancements.
- Transitions jour/nuit, menu Échap et Quitter, nage et plongée avec comparaisons de captures : PASS natif. La comparaison pixel à pixel de nage utilise un profil isolé avec grain animé à zéro et compteurs masqués ; elle conserve les attentes strictes d’égalité ou de différence et tous les effets d’eau. L’essai 001 avec grain et compteurs actifs est conservé comme échec du protocole, sans qualification. Les preuves sont archivées par mode dans ce dossier.
- Toutes les surveillances finales se terminent sans arrêt forcé ni descendant restant. Pics observés sur les essais 012, captures incluses : 4.04 Gio privés, 2.28 Gio GPU dédiés et 0.07 Gio GPU partagés. Compteurs par processus, distincts de l'occupation totale de la machine. Aucun seuil de mémoire disponible réintroduit.

## Livraison et limites

Runtime final SHA256 : `15e15673f5b10e94d3d6ec52570a43efc6c7ed0937bdda920ce42365d3e58eb9`. Binaire précédent 010 sauvegardé avant remplacement. Launcher, préférences personnelles et paquets de contenu conservés ; seules les 22 sources ciblées sont intégrées après contrôle de leurs empreintes. Les sources livrées sont identiques au texte compilé après seule normalisation des fins de ligne CRLF vers LF pour conserver le style du dépôt. Les octets exacts du snapshot compilé restent archivés ; le runtime installé conserve exactement l’empreinte testée. Aucun commit ni push.

Les distances d'ombres 10/60/400 m et trois cascades 2048 sont conservées. La densité des nuages est augmentée sans flouter les autres objets ni réactiver le TAA. Il ne remplace pas la qualification Cargo release/LTO, workspace, WebGPU/WASM ou CI distante sur commit exact, ni la généralisation restante des modules moteur indiquée dans le contexte du projet.

**Validation humaine de 012 en attente ; Gate 3 reste ouverte.**

Preuves : `build-007`, `engine-tests-006`, `residency-tests-004`, `rust-check-005`, `perf-proof-002`, `perf-proof-baseline`, `perf-proof-003`, `terrain-proof-002`, `display-proof-001`, `menu-proof-001`, `swim-proof-002`, `context-proof-003`, `context-proof-004` et surveillances correspondantes. Résumé calculé : `summary.json`. Les essais préliminaires et leurs échecs sont conservés sans promotion en qualification finale.

Dossier local : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261009-012`.

## Mise à jour du runtime installé

Le runtime 012 décrit ici est remplacé par la correction 013 de démarrage : voir
[TIDEWATER_STARTUP_READBACK_FIX_20261009.md](TIDEWATER_STARTUP_READBACK_FIX_20261009.md).
Les mesures de performance et de ciel de 012 restent des preuves de cette version ; elles ne
couvraient pas complètement le démarrage interactif ordinaire, qui a révélé un crash intermittent.
