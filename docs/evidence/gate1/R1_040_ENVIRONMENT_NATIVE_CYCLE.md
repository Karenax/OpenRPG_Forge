# R1-040 — Cycle natif des résolutions d'éclairage

2026-09-06 local, Rust, non publié. Gate 1 et R1-040 restent ouverts.

## Complément du 9 septembre — coût en marche sur le release optique

Comparaison bornée High/Medium/Medium/High du seul domaine environnement,
sans recompilation ni modification du produit. Même release optique
`2C507A416D46FD39835715DB18022CC845CAD3E190FFAC40353CD92CBFB15BCA`,
kit courant de 1662 arbres / 19944 primitives, végétation High, eau claire et
reflets 48 pas/96 m, ombres candidates 2048/trois cascades/128 m inchangés.
RX 7800 XT/Vulkan, 1920 × 1080, parcours vivant `1800:1:0.22`, sans VSync demandée.

High demande une carte 256² par face, Medium 128². Les autres paramètres
volumétriques du preset diffèrent aussi, mais le preview volumétrique n'est pas
activé : l'adaptateur conserve son brouillard de distance. Les logs confirment
le tier demandé et l'absence de blocage du cache, **pas les dimensions GPU ni
le binding final**, absents du rapport de marche de ce release. Ne pas transformer
cette comparaison de demandes en qualification complète des profils.

| Passage | Environnement demandé | FPS trajet | p95 ms | 1 % low FPS | FPS tiers forestier |
| --- | --- | ---: | ---: | ---: | ---: |
| A1 | High | 63,59 | 25,16 | 34,28 | 43,24 |
| B1 | Medium | 65,27 | 24,09 | 39,98 | 45,35 |
| B2 | Medium | 65,27 | 24,31 | 38,57 | 44,99 |
| A2 | High | 64,27 | 24,31 | 39,25 | 44,76 |

Recalcul indépendant des distributions en accord à 1e-9. 1330 ticks communs,
écart de position maximal 7,53 mm, bras caméra 1,89 mm, yaw/pitch identiques.
Arguments identiques hors tier environnement, population et hashes d'entrée
inchangés, budgets d'eau et ombres contrôlés dans les rapports. Quatre parcours
terminés au sol, sorties 0, aucun ERROR. PIDs 5872, 31156, 31380 et 5816 terminés,
absence contrôlée ; aucun navigateur ou build lancé.

Décision : **aucun gain majeur démontré**. Le témoin dérive lui-même de 43,24 à
44,76 FPS en forêt ; le résultat ne justifie ni une promotion Medium, ni un
changement de cache/fréquence de génération. Ne pas poursuivre cette paire
inchangée. La piste restante est la soumission et le remplissage du rendu
forestier, pas une réduction supplémentaire de la résolution d'éclairage.
Ce lot ne corrige pas le produit et ne ferme aucun seuil de Gate 1.

Racine externe :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-release-optical-environment-20260909/`.
Exécution de `Measure-Gate1OpticalRelease-20260909.ps1 -EnvironmentIsolation`, puis
`Compare-Gate1OpticalRelease-20260909.py --environment` ; arguments, requêtes,
populations, logs, rapports et pins conservés. Comparaison SHA-256 :
`9A3F38642284A6A9ECA5D9A9F67AA2766236DE6A2604A1EEEF80FBD8A193679F`.
Aucune nouvelle compilation, capture artistique, qualification Studio/GPU cible
ou publication. Les preuves de cycle ci-dessous restent historiques.

## Défaut reproduit, puis corrigé

Le premier cycle High → Low → Medium → Ultra échoue lors du passage à Ultra avec
`storage_view_mip_9` : le filtrage utilise une texture ne possédant que neuf mip levels.
Log rejeté conservé : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-environment-cycle-20260906.log`,
captures partielles dans le dossier homonyme. Code 1 et erreurs GPU, jamais preuve verte.
Binaire fautif : `d2c1fbb786c77266eaf2de157b274e5b662eddd29a8dd2150cba8b053d70e243`.

L'ancien raccord copiait `GeneratedEnvironmentMapLight` de la sonde vers la caméra : Bevy
lançait donc un second filtrage sur cette caméra persistante. `generate_environment_map_light`
dans `bevy_pbr-0.19.1/src/light_probe/generate.rs` ne crée les textures filtrées que sans
`EnvironmentMapLight` existant. Le cache caméra pouvait conserver les dimensions précédentes.

Le correctif laisse génération et filtrage sur la seule sonde renouvelable. La caméra reçoit
`EnvironmentMapLight` déjà filtré, jamais un nouveau générateur. Avant le raccord, l'adaptateur
vérifie dimensions/six faces du brut et du spéculaire, et présence du diffus. Le remplacement
retire les anciens bindings de caméra. Aucun accès aux types privés, nouvelle dépendance ou
changement des sources. Le test de sonde vérifie aussi l'absence de générateur sur la caméra.
La suppression du double filtrage est structurelle ; aucun gain de performance chiffré revendiqué.

## Protocole exécuté

`--environment-cycle-proof <dossier-neuf>` exige `--environment-quality` et refuse les autres
automatisations/contrôles joueur concurrents. La commande utilise `--world-sketch-visual-valley`,
`--environment-quality high` et les mêmes kit/preview human_bridge que le protocole
`R1_040_ENVIRONMENT_QUALITY_IN_PROGRESS.md`. RX 7800 XT, Vulkan, 1920×1080, météo clear.

Une seule session demande High/Low/Medium/Ultra/High, soit 256/64/128/512/256 pixels. Chaque étape
attend 180 observations CPU consécutives du tier et de la texture raccordée, puis demande un PNG.
Le retour de capture et le fichier non vide doivent être constatés avant de changer de tier.
Le dossier doit être neuf ; un ancien PNG ne satisfait pas la preuve. Le hash d'environnement
reste identique à chaque étape. Délai de 120 s contrôlé dans la boucle ; ce n'est pas un watchdog
indépendant si le thread principal se fige. Toute sortie avant cinq étapes est un échec, y compris
une fermeture normale de fenêtre. Les erreurs renderer restent propagées par le Runtime.

Ce parcours ne réutilise pas le garde des captures uniques : les 180 observations portent sur
les bindings CPU, pas le cache de pipelines. La preuve GPU repose sur l'exécution sans erreur et
l'inspection des PNG retournés, pas sur ce compteur seul.

## Résultat natif

Cycle corrigé : **code 0, cinq étapes et cinq readbacks, zéro ERROR**.
Artifacts `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-environment-cycle-fixed-20260906`, log adjacent
avec suffixe `.log`. Les cinq PNG sont inspectés : ciel, pont, eau et terrain présents à tous les
niveaux, retour High rendu. Végétation tramée, eau granuleuse et géométrie stylisée restent visibles.
Le vent et l'eau évoluent entre vues : pas d'égalité pixel attendue. Pas de vidéo des transitions,
pas de promesse d'absence de variation transitoire ou de benchmark long. Les FPS d'overlay sont
instantanés et ne constituent pas une distribution de performances.

Source environnement inchangée : `845ae5b80b6cb1ef2390b0cc79efb121a5b8ffbd2e911f67f7a5b5ce38f00f0f`.
Binaire corrigé : `d7c119f93744cfed6186751d187d37377b80fa3347d25a370e7b3a888fb51915`.

SHA-256 :

- harness : `b4790a41c3d06096a035b551b56db2145c0e0ce6b09659804827faa5c7dc8155` ;
- gestionnaire sonde : `3a14f5be48d589e7f50fb749ade500085c105db75d7b6e676acbc4d5a03b6c7c` ;
- log : `c0617de0b7cc1956a9ed2e5066cc41b6ccf2bd3c8c14650852f49fc0eedee2a0` ;
- High initial : `c03979e784881f1a0f9c89e5be955681e919da0d8dce4979a6985d19e0f40bb5` ;
- Low : `8a63734c0be7262fc7bfa2ebc11f055115997576097e493dacd93380f262a861` ;
- Medium : `bf5327e3a831badae65cc5855857400c1e39975636424d599275a349525b3c7c` ;
- Ultra : `01fdb0d430c09a85826c93d246934e0ebebef33f95434a698f2470880ac53b19` ;
- High retour : `99df858e88b1926304dad46171aa691688349df8c13df423031dcbd61c8dc9ab`.

Restent : suspension/restauration totale d'atmosphère en natif (ECS déjà testé), Studio,
volumétrie ou fallback accepté, végétation/profil global, qualification longue et humaine.
CI NOT RUN, arbre local modifié, aucun passage de gate, aucun commit/push ou asset Cadle.

Validation finale : `cargo test --workspace --locked -q` **373 PASS**, dont le refus d'automatisations
concurrentes et de sortie prématurée du harness ; Clippy workspace tous targets/features avec
`-D warnings` PASS ; check moteur web/WASM PASS ; formatage et diff PASS. Même target isolé,
incremental=0 et `RUSTFLAGS=-C debuginfo=0` que les preuves précédentes.
