# Tidewater — comparaison visuelle et réglages graphiques du candidat 021

Référence de nos mesures : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c` ; la [démo publique](https://dgreenheck.github.io/tidewater/) a continué d'évoluer et le `main` public vérifié le 29 septembre est à `4811ba48d795197de5621985f404e765c0b7c0ef`. Son README décrit un moteur WebGPU/WGSL propre au projet, sans framework de rendu ; ce n'est pas une scène Three.js ordinaire. La capture fournie par le propriétaire montre un ciel, des nuages, une lumière et une finition plus riches que les prises natives disponibles. Les prises de vue de la demande ne sont pas alignées pixel à pixel ; cette conclusion est visuelle, non une mesure générale de qualité ou de vitesse. Les comparaisons chiffrées ci-dessous portent sur le commit figé, pas sur le rendu du `main` récent.

Le menu de promenade native `H` ou `F10` expose 15 réglages actifs, modifiables à chaud, sauvegardés dans `%LOCALAPPDATA%\OpenRPG Forge\Tidewater\graphics.json`. La molette parcourt le panneau sur les écrans moins hauts ; `R` rétablit les valeurs par défaut. Les dix valeurs visibles dans l'onglet Effects de la capture de référence correspondent désormais à une commande du rendu Rust :

| Effet | Source affichée | Valeur native initiale | État |
|---|---:|---:|---|
| Occlusion ambiante | 1,00 | 1,00 | GTAO demi-résolution, deux flous bilatéraux et remontée sensible à la profondeur ; [A/B native](2026-09-28-gtao.md) |
| Lumière rebond | 1,00 | 1,00 | carte de rebond source pour le village ; influence mesurée faible |
| Netteté | 0,45 | 0,20 | contraste adaptatif Bevy, algorithme et force visuelle différents |
| Flou de mouvement | 180° | 180° | passe native avec vecteurs de mouvement, pas identique à la passe source |
| Bloom | 0,050 | 0,050 | bloom Bevy, chaîne de convolution différente |
| Lens flare | 1,00 | 1,00 | visibilité GPU et optique solaire ; [A/B native](2026-09-28-lens-flare.md) |
| Saturation | 1,06 | 1,06 | étalonnage de la caméra |
| Contraste | 1,04 | 1,04 | étalonnage de la caméra |
| Vignette | 0,28 | 0,28 | effet caméra |
| Grain photo | 0,012 | 0,012 | passe GPU native |

Le même menu donne aussi adaptation automatique, FXAA, TAA expérimental, exposition EV et brume marine. Le TAA reste désactivé par défaut, car son rendu de l'eau et des nuages n'est pas encore qualifié. Les nombres identiques à la source ne garantissent pas une intensité visuelle identique quand les algorithmes ou l'ordre des passes diffèrent.

Cette couverture concerne l'onglet **Effects** montré dans la capture et ces cinq commandes supplémentaires. Les autres onglets de la démo (état de la mer, ressac, météo/heure, caméra et qualité de rendu) n'ont pas encore un panneau de réglages complet dans l'exécutable natif. Il ne faut pas interpréter les 15 commandes comme une reproduction intégrale des réglages du site.

Sur le candidat 021, une A/B native à caméra fixe et exposition fixe établit que l'AO modifie 29,124 % des pixels de plus de deux niveaux sur au moins un canal (`native-village-ao-ab-021.json`). Le flare solaire modifie 3,726 % des pixels de ce seuil dans sa scène de diagnostic (`native-sun-flare-ab-021.json`). Trois relances identiques de chaque effet produisent chaque fois des PNG de même empreinte ; l'ordre des passes concurrentes de post-traitement a été fixé après des résultats variables du candidat 019. Les autres vues de la demande montrent des nuages source plus doux et mieux intégrés. L'objectif de carte et d'effets exactement identiques reste ouvert.

Dans trois rectangles de l'ancienne paire village, la luminance sRGB moyenne source/native est 60,98/45,91 pour la colline, 106,61/76,68 pour les maisons et 157,43/110,61 pour le premier plan. **Cette paire ne qualifie pas la luminosité relative des moteurs** : la source active l'auto-exposition, le profil natif de cette A/B la désactive (`auto_exposure=0`), et le probe de capture statique n'activait pas `FORGE_TIDEWATER_STATIC_POSTFX`, donc pas la chaîne HDR/ACES native complète. Ces nombres ne doivent pas servir à régler les valeurs par défaut.

Une [nouvelle paire village avec la chaîne HDR/ACES et l'adaptation actives](2026-09-29-village-postfx-pair.md)
ramène les moyennes de luminance source/native à 60,35/60,92 (colline),
106,84/110,62 (maisons) et 157,06/154,16 (premier plan). Les différences de
détail et de traitement temporel restent visibles ; cette mesure ne remplace
pas une comparaison appariée de la côte, du ciel et de la promenade animée.

Correction du diagnostic nuageux : `src/App.js` de la référence choisit `SkyProClouds` par défaut et `Clouds` seulement avec le paramètre `oldClouds`. `SkyProClouds.js` échantillonne `cloudsView` linéairement pour le ciel principal, comme le `scene/sky.wgsl` natif issu de ce module. Son filtre Catmull-Rom sert à reconstruire l'historique temporel et figure aussi dans la passe nuageuse exportée. Le filtre Catmull-Rom de `Clouds.js` concerne l'ancien mode ; il n'explique donc pas, à lui seul, l'écart observé avec la démo courante. Il faut appairer caméra, météo, heure, exposition et résolution temporelle avant d'attribuer cet écart à une passe précise.

Le contrôle figé `cloud-sky-scene-comparison-001.json` mesure, pour la seule
composition nuage/ciel à caméra et temps donnés, 0,134 niveau sRGB moyen par
canal sur la région d'effet, un 99e centile de 1 niveau et un maximum de 4.
Ce résultat ne qualifie ni la météo animée, ni l'éclairage de l'île, ni l'image
complète de promenade ; il évite d'attribuer l'écart global au seul filtre du
ciel.

L'[audit du `main` public récent](2026-09-29-live-source-visual-delta.md)
identifie de nouveaux traitements temporels pour l'anticrénelage et la brume,
une correction du soleil derrière les nuages et des modifications de l'eau.
Ces différences restent à intégrer ou qualifier contre une capture actuelle
appariée avant de parler d'identité visuelle avec le site.

Archive Windows locale : `E:\OpenRPG_Forge\.tidewater-integration\OpenRPG-Forge-Aube-Tidewater-Win64-20260928-021.zip` (422 554 847 octets, SHA-256 `8A0B1CD6B6888BA4357CC6F790EEDF64D4925B13B8F3346E9E791C815EFD7A63`). Ses 2 508 entrées passent le CRC ; le runtime fraîchement extrait a la même empreinte que le binaire compilé (`CBBD3C9676436AB138DEAFB25D436947AA313AC8CA55218CE78C11283D30B722`). Le lanceur Aube répond `FORGE_LAUNCHER_AUBE_FRONTEND_READY` ; le parcours extrait `--walk-smoke` donne `pass=true`, `cadence_pass=true`, `backend_stable=true`, 8,994 m. Reçu : `E:\OpenRPG_Forge\.tidewater-integration\package021-extracted-walk-smoke.walk.json`. La qualification sur un autre PC et la revue des notices de licences restent ouvertes avant l'envoi public.

Mise à jour du 29 septembre : une [paire côte/ciel appariée](2026-09-29-seaward-postfx-pair.md)
confirme la proximité de la luminance globale sur la révision figée, tout en
mesurant un ciel Rust plus sombre et des décors de plage absents. Le paquet
[023](2026-09-29-windows-playtest-package-023.md) corrige le manifeste interne
des données distribuées et passe le parcours depuis une extraction fraîche ;
il conserve le même binaire runtime et donc les mêmes réglages graphiques.
Le [candidat 024](2026-09-29-windows-playtest-package-024.md) embarque le
binaire reconstruit avec l'échoppe et un panneau défilable ; l'extraction,
le parcours et la lecture du profil graphique passent localement. Les gestes
interactifs `H`/`F10` et molette restent à vérifier sur ce candidat.

Le [candidat 026](2026-09-29-windows-playtest-package-026.md) incorpore le
filtrage et l'éclairage corrigés de l'échoppe. Après extraction neuve, son
parcours de marche et les touches `H`/`F10` du panneau fonctionnent ; `R`
rétablit Bloom de `0,080` à `0,050` et la valeur persiste après redémarrage.
Le contrôle souris envoyé par messages Windows en arrière-plan n'a pas validé
le clic ni la molette ; une vérification avec entrée OS réelle reste nécessaire.
