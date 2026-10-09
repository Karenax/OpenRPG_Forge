# Tidewater — correction du coût CPU des rotations, 9 octobre 2026

Le défaut de rotation est reproduit sur le runtime installé 008 puis corrigé : à résolution et qualité identiques, les deux phases de rotation passent de 18,09–18,15 FPS à 31,60–33,65 FPS sur le binaire final 009. Le GPU n'était pas le facteur limitant : la préparation CPU empêchait de lui soumettre les images assez vite. **60 FPS ne sont pas atteints**, et cette correction ne clôture pas la Gate 3.

## Protocole et portée

Rejeu natif Vulkan, AMD Radeon RX 7800 XT (pilote 26.8.1), Windows, écran principal 2560×1440 en plein écran sans bordure, `PresentMode::Fifo`. Préférences réelles copiées dans un profil isolé : heure 09:30, TAA et flou de mouvement désactivés, autres réglages, ombres, distances et shaders conservés.

Après disponibilité de la simulation et 240 images de chauffe : 6 s immobile, 10 s de rotation, 6 s immobile, 10 s de rotation. La première et la dernière seconde de chaque phase sont exclues. Même trajectoire en temps réel, événements Bevy `MouseMotion`, déplacement négligeable. Aucune injection clavier/souris système. **Aucun PNG ni capture complète pendant les mesures de cadence**. Les captures de régression sont un autre rejeu et leur HUD FPS ne constitue pas une mesure de performance.

L'intervalle First→First mesure la cadence réelle, First→Last le programme principal. Les étapes de rendu sont mesurées dans le RenderApp. Le span GPU est placé autour des commandes de `RenderGraphSystems::Render`, avec encodeurs début/fin soumis dans le bon ordre; il ne couvre pas les transferts hors graphe ni l'attente de présentation. Les résultats GPU sont asynchrones, dernière valeur disponible, sans appariement exact à chaque image CPU. Les phases imbriquées ne doivent pas être additionnées.

Une première tentative de span GPU (`perf-proof-001`) avait un ordre de soumission incorrect et est explicitement **exclue de la preuve GPU**. La référence exploitable est `perf-proof-002`.

## Causes identifiées et correction

1. Une rotation reconstruisait entièrement les maillages CDLOD terrain et océan, et relançait la sélection radiale des végétaux malgré une position fixe. En rotation, ces trois opérations coûtaient respectivement environ 2,55 ms, 2,63 ms et 3,96 ms par image. `ResidentTiles` conserve désormais chaque patch inchangé et ses handles; seuls les nouveaux patches sont construits. La sélection radiale des plantes suit le déplacement d'au moins un mètre, indépendamment de l'orientation, comme le budget de déplacement existant.

2. La publication des nuages recopiait les cinq lignes de paramètres caméra/ombre dans tous les matériaux à chaque rotation. Les shaders de scène utilisent seulement la cinquième ligne, qui décrit l'ombre des nuages. Cette modification inutile des matériaux invalidait des milliers de spécialisations de rendu. La scène reçoit uniquement cette ligne quand elle change; le ciel conserve la publication complète de ses paramètres caméra. Le contrat de lecture des sept shaders WGSL réellement chargés est archivé dans `shader-cloud-contract.json`.

Le cache terrain et le cache océan sont chacun bornés à `max(256, nombre de patches actifs)`, avec éviction des patches inactifs les plus anciens. Le représentant racine, les matériaux, les triangles et leurs attributs restent cohérents; les enfants inactifs sont masqués. L'ordre de publication avant `AssetEventSystems` et propagation de visibilité, qui corrigeait le sol noir dans 008, est conservé.

Le cache seul supprimait environ 9 ms de travail du programme principal mais gardait seulement 19 FPS : cette étape intermédiaire n'est pas considérée comme le correctif suffisant. Après correction des nuages, la spécialisation passe d'environ 17,2 ms à 0,8–0,9 ms, et la mise en file d'environ 8,5 ms à 0,7 ms. Le programme principal final coûte 5,7–6,2 ms en rotation, contre 16,4–16,8 ms avant. Le temps GPU du graphe reste autour de 6,3–6,4 ms.

## Résultats appariés

| Phase | FPS référence 008 | FPS final 009 | p95 ms avant / après | 1 % bas FPS avant / après |
| --- | ---: | ---: | ---: | ---: |
| 1 — immobile | 38.65 | 37.40 | 27.40 / 28.76 | 34.00 / 32.60 |
| 2 — rotation | 18.09 | 33.65 | 58.97 / 32.67 | 15.66 / 26.62 |
| 3 — immobile | 37.74 | 33.00 | 28.63 / 32.54 | 31.88 / 29.38 |
| 4 — rotation | 18.15 | 31.60 | 58.07 / 33.71 | 16.07 / 28.63 |

Le « 1 % bas » est l'inverse de la durée moyenne du centième d'images le plus lent, arrondi au nombre entier supérieur. Le p95 est celui des intervalles entre images. Les deux phases de rotation finales gagnent respectivement 86.0 % et 74.1 %.

Référence : `perf-proof-002/analysis.json`. Décomposition intermédiaire : `perf-proof-004/analysis.json`. Correctif sous profil original : `perf-proof-005/analysis.json` (32,04–34,75 FPS en rotation). Binaire optimisé final : `perf-proof-007/analysis.json`. Les fluctuations et tâches concurrentes de la machine limitent la précision de la comparaison entre processus; les quatre phases au sein de chaque rejeu rendent visible le défaut spécifique à la rotation.

La compilation finale optimise le moteur et l'exemple natif à `opt-level=3`, assertions Rust de développement désactivées, sur un SDK fermé inchangé. Elle n'apporte pas de gain net supplémentaire mesuré par rapport au correctif sous `opt-level=1`. Ce n'est pas une compilation Cargo Release/LTO de l'ensemble du workspace.

Un essai séparé `perf-proof-006` avec `WGPU_DEBUG=0` et `WGPU_VALIDATION=0` atteint 35,37–38,89 FPS et réduit d'environ 3 ms l'encodage CPU du graphe. C'est une expérience diagnostique : **ces variables ne sont pas appliquées au binaire installé**; les contrôles GPU par défaut sont conservés.

## Régressions et ressources

- Quatre tests ciblés du cache passent : conservation des handles sur permutation/recouvrement, parcours borné sans éviction active, sélection vide/récupération, identité des attributs et triangles avec le maillage terrain source fusionné. Le lancement `--test` est archivé dans `tile-tests-001`; 29 autres tests de l'exemple sont filtrés, aucun PASS global n'est revendiqué.
- Rejeu natif terrain final : 82 captures 2560×1440, 314 observations de rendu, aucun maillage, matériau ou pipeline attendu manquant, tous les patches actifs prêts. Fraction maximale de pixels presque noirs dans le tiers inférieur : 0.2541 %. Planche-contact et capture `terrain-075.png` inspectées visuellement : sol et océan présents, pas de trou visible sur le trajet.
- Menu natif final : ouverture Échap, réglages, contraste persistant dans le profil isolé, retour/reprise, bouton **Quitter** visible et sortie réelle du processus : PASS, code natif 0.
- Clippy ciblé : code 0, aucune erreur ni diagnostic dans les sept fichiers concernés. 22 diagnostics préexistants restent ailleurs dans l'exemple. `rustfmt --check` sur les sept fichiers : PASS.
- Sources, dépendances du SDK et entrées du paquet contrôlées par SHA-256. Les trois replays finaux sortent normalement, les préférences réelles ne sont pas modifiées.
- Gardes des replays : aucun arrêt forcé, aucun seuil de RAM disponible; limites uniquement de ressources du processus possédé et de durée. Pour la mesure finale 007, pics échantillonnés : mémoire privée 3.511 GiB, GPU dédié 1.806 GiB, GPU partagé 0.072 GiB. La mémoire privée n'est pas la RAM physique résidente. Ce sont des compteurs par processus; les valeurs globales ou lignes de processus déjà terminés ne sont pas attribuées au jeu. Aucun descendant possédé encore actif aux états terminaux.

Contrôle final après intégration : `git diff --check` ciblé PASS et empreintes des sept fichiers intégrés/binaire installé PASS. `cargo fmt --all --check` renvoie 1 avec des écarts dans 32 fichiers hors des sept sources concernées (travaux Studio/source graph et autres sources du workspace). Aucun écart dans les fichiers du correctif. Ces sources concurrentes sont conservées; le détail figure dans `cargo-fmt-all-check.log` et `final-checks.json`.

## Limites et suite technique

Le coût de rotation évitable est corrigé, mais la cadence finale reste autour de 32–34 FPS. La préparation CPU des assets vaut encore environ 7,5–9,8 ms et l'encodage CPU du graphe environ 12,7–12,9 ms. Ce sont les postes restant à détailler pour viser 60 FPS; leur cause précise n'est pas attribuée sans mesure supplémentaire. Le CPU total de la machine et la mémoire vidéo occupée ne suffisent pas à caractériser ces postes.

Le build et les vérifications concernent le snapshot fermé de l'exécutable installé. Le point d'entrée et d'autres sources du workspace principal contiennent des travaux concurrents différents : seules cinq sources épinglées et deux nouveaux helpers sont intégrés, avec protection contre écrasement. Aucun PASS du workspace complet, de CI distante, de Release complète ou de Gate 3 n'en découle. L'acceptation humaine antérieure n'est pas transférée à ce nouveau binaire.

Runtime final SHA-256 : `81b5a5745c795ceeb1135b55832ea2fb7a953af68602dd48b4b6852b6b82809f`. Le reçu `deployment.json` confirme séparément les octets réellement installés, les fichiers intégrés et la préservation des préférences. L'ancienne version 008 est sauvegardée localement avant remplacement. Launcher conservé, aucun commit ni push.

Dossier local : `E:\OpenRPG_Forge\.tidewater-integration\launcher-repair-20261009-009`.
