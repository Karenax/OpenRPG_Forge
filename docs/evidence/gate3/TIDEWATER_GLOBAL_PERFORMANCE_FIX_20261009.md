# Tidewater — cadence globale et coût du ciel, 9 octobre 2026

Le runtime 010 atteint **76.58–82.57 FPS** dans le scénario natif mesuré à 2560×1440, contre 31,60–37,40 FPS pour le runtime 009. Deux causes CPU sont corrigées : la republication des 166 matériaux des poissons et la relance permanente des calculs d'atmosphère avec recréation des pipelines. La définition du ciel, les effets, les distances d'ombre et les contrôles GPU par défaut sont conservés. Cette correction ne clôture pas la Gate 3.

## Coût du ciel mesuré

Avant le correctif atmosphère, chaque image échantillonnée possède un nouvel échantillon CPU d'atmosphère : 207/207 puis 401/401 images dans les deux phases avec ciel visible de `perf-proof-006`. Le calcul coûte en moyenne **8.487 ms CPU** et environ **0,09 ms GPU**. Après correction, on observe 16, 32, 17 et 33 nouveaux échantillons dans des fenêtres de 4, 8, 4 et 8 secondes : environ quatre mises à jour par seconde. Chaque mise à jour coûte **0.02628 ms CPU**, avec le même calcul GPU d'environ 0,09 ms par mise à jour.

Les nuages vivants coûtent environ **0,47–0,51 ms GPU par image** dans ce parcours. Leur définition reste 240×135 pour le tracé et 960×540 pour l'historique. L'affichage du fond du ciel est testé séparément en masquant uniquement son entité dans les phases 2/3 de `perf-proof-006`, tout en conservant l'atmosphère, les nuages, les ombres et l'éclairage. Le passage opaque diffère seulement de 0,003–0,007 ms entre vues visibles et masquées, sans gain de cadence significatif. Cela ne donne pas un coût absolu isolé du shader de ciel : les passes sont partagées et la mer continue d'évoluer. La conclusion porte sur cette vue et ces réglages.

Les bandes CPU du graphe sont des frontières de systèmes et peuvent contenir d'autres travaux. Elles ne sont pas assimilées au coût exclusif de l'atmosphère : la bande avant nuages passe d'environ 9,5 ms à 2,7 ms, tandis que l'échantillon du système atmosphère proprement dit passe de 8,5 ms à 0,025 ms par calcul. Les timers GPU imbriqués ne s'additionnent pas. Un premier soupçon de compteur ancien a été écarté par les timers directs et le comptage des nouveaux échantillons : le recalcul à chaque image était réel.

## Corrections

`tidewater_atmosphere/live.rs` conservait le masque d'un travail non encore encodé mais lui attribuait une nouvelle révision même si les paramètres étaient identiques. Avec le programme principal en avance sur le rendu, ce décalage republiquait continuellement le calcul. Les demandes identiques déjà en attente gardent maintenant leur révision ; les vrais changements de soleil, les invalidations statiques et la cadence d'irradiance continuent de demander du travail.

Les quatre pipelines de l'atmosphère et leur sampler restent résidents dans chaque système de rendu. Un nouveau jeu est créé si les sources ou la génération changent. Les blocs de paramètres restent immuables par commande pour préserver les différents observateurs déjà encodés ; les shaders WGSL acquis, les dispatchs, les LUTs et leur résolution ne changent pas. Le chemin d'observateur des nuages suit le même principe de cache.

Les 83 poissons partageaient l'heure mais republiaient leurs 166 matériaux à chaque image. Une horloge de 16 octets par appelant transmet désormais l'heure float32 exacte et l'index uint32 complet, avec un mode neutre pour les anciens appelants. Les matériaux et la visibilité ne changent que si les receveurs ou les LOD changent. La préparation CPU de ces matériaux passe de 4,66–4,86 ms à 0,007–0,009 ms dans les mesures détaillées appariées `perf-proof-001` / `perf-proof-004`. Leur ordre est forcé uniquement pour cette attribution détaillée ; les mesures finales 007/008 utilisent l'ordre habituel.

## Cadence à réglages identiques

Ryzen 5 7600, Radeon RX 7800 XT, Vulkan, plein écran 2560×1440, présentation Fifo. Phases de 6/10/6/10 secondes après 240 images de stabilisation ; la première et la dernière seconde de chaque phase sont exclues. Aucune capture PNG pendant la mesure de cadence. Heure 09:30 et préférences graphiques réelles recopiées dans un profil isolé ; résolution des ombres 3×2048 et distances 10/60/400 m conservées.

| Phase | FPS 009 | FPS 010 final | p95 ms avant / après | 1 % bas FPS avant / après |
| --- | ---: | ---: | ---: | ---: |
| 1 — immobile | 37.40 | 82.57 | 28.76 / 13.28 | 32.60 / 68.65 |
| 2 — rotation | 33.65 | 76.58 | 32.67 / 15.65 | 26.62 / 55.93 |
| 3 — immobile | 33.00 | 80.35 | 32.54 / 16.44 | 29.38 / 51.76 |
| 4 — rotation | 31.60 | 79.87 | 33.71 / 13.98 | 28.63 / 64.79 |

Le « 1 % bas » est l'inverse de la durée moyenne du centième d'images le plus lent. Référence publiée : `launcher-repair-20261009-009/perf-proof-007/analysis.json`. Rejeu final : `perf-proof-009/analysis.json`. Le rejeu 007 précédant les ajustements de lint atteint 80,65–83,24 FPS. Les mesures se limitent au parcours décrit ; elles ne garantissent pas 60 FPS sur toute l'île ni dans toutes les scènes. Le CPU d'encodage du graphe final vaut environ 5,5 ms et le GPU 5,6–6,2 ms. Des tâches concurrentes et les changements dans la mer limitent les comparaisons entre périodes.

## Vérifications et portée

- 20 tests ciblés passent : 15 atmosphère (dont conservation d'une demande en attente et invalidation de la génération du cache), 2 horloges de poissons, 3 assemblage et receveurs des matériaux. Les 803 autres tests du moteur sont filtrés. Les tests compilent sur les sources de `build-002/inputs.json`, avant les changements de lint uniquement.
- Le replay de performance 009 utilise exactement le SHA256 du binaire installé. Les replays terrain, jour/nuit et menu utilisent build-003, avant la seule remise en ordre des déclarations de modules par rustfmt. Toutes les implémentations et tous les shaders conservent leurs SHA256 ; les correspondances des modules et le texte restant sont vérifiés dans `formatting/equivalence.json`. Les deux binaires et leurs entrées de compilation sont archivés. Aucun changement fonctionnel ne justifie de répéter ces trois contrôles graphiques. Sorties natives 0 ; aucun arrêt forcé et aucun descendant possédé actif à la fin. Les entrées du paquet et les préférences réelles gardent leurs SHA256. La surveillance n'applique aucun minimum de mémoire disponible.
- Terrain : 82 captures, 318 observations, tous les patches actifs prêts. Tiers inférieur presque noir au maximum : 0.2696 %. Planche-contact et capture terrain-176 inspectées : sol, océan, ciel et étal de poissons présents. La définition des nuages reste limitée, comme avant ; ce correctif améliore le coût, pas leur définition.
- Jour/nuit : réglages de lumière et persistance dans le profil isolé, captures midi/nuit inspectées, mêmes images résidentes d'atmosphère : PASS. Menu Échap, contraste, reprise et sortie par Quitter : PASS.
- Clippy moteur et exemple : codes 0. Aucun nouveau diagnostic sur les modifications ; trois diagnostics de style dans les fichiers touchés correspondent à des lignes déjà présentes dans 009 et sont consignés dans `legacy-lints.json`. Des avertissements préexistants restent ailleurs ; aucun PASS Clippy sans avertissement du workspace n'est revendiqué.
- Pic échantillonné du replay final : mémoire privée 3.623 GiB, GPU dédié 1.875 GiB, GPU partagé 0.072 GiB. La mémoire privée représente l'engagement privé, pas la RAM physique résidente.

## Livraison

Binaire final : `843f2d0359e675f1c39cd2eed65bf7af4fad6ffbe2a1939c15837a0d40b676f3`. Compilation du moteur et de l'exemple natif à opt-level=3, assertions Rust désactivées, avec le SDK fermé inchangé ; ce n'est pas une compilation Cargo Release/LTO complète. L'intégration ne concerne que les 15 fichiers inventoriés dans `source-delta.json`, dont trois nouveaux helpers. L'entrée principale et les autres travaux existants sont préservés. L'ancien runtime 009 est sauvegardé avant remplacement atomique. Launcher et préférences réelles conservés. Aucun commit ni push.

La validation humaine précédente est conservée pour sa version. La confirmation visuelle du correctif 010 reste à faire ; aucune clôture automatique de Gate 3 et aucune qualification complète du workspace ne sont revendiquées.

Dossier local : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261009-010`.

## Contrôle après intégration

Les 15 SHA256 des sources intégrées et celui du runtime installé correspondent au dossier de compilation. `rustfmt --check` sur les 13 fichiers Rust concernés et `git diff --check` sur le périmètre intégré : PASS. Le contrôle `cargo fmt --all --check` reste en échec dans 34 fichiers hors périmètre, consignés dans `final-checks.json`. Ces travaux existants sont préservés ; aucun PASS global de format n'est revendiqué.
