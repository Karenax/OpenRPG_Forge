# R1 — intégration Studio du milieu optique et du contact

9 septembre 2026, Rust, Gate 1 ouverte, aucune publication.

## Implémenté, en cours de qualification locale

Menu « Vallée avec eau claire RGB · candidat expérimental », route explicite
`forge_water=clear-medium-preview`. L'ancien candidat `depth-preview` reste
disponible. Choix inconnu, répété ou hors vallée refusé. Le profil synthétique
`examples/water/clear-medium.preview.json` est embarqué et appliqué uniquement à
la copie chargée, **avant** la publication du manifeste. Aucun fichier de carte,
terrain, palette, révision ou document sauvegardé n'est modifié. Ce n'est pas
encore un outil d'édition de paramètres ou de régénération dans le Studio.

Le moteur commun inclut désormais milieu optique, détail métrique sans étirement
et transition de contact. Profils qualité épinglés, SSR eau désactivé, candidat
explicitement non qualifié. Le profil est identique à la preuve native claire :
SHA-256 `7F85A535A2E22FDBDE5D116F8619D5962A4A5106ACFC02E7C6217DCC4B1F3840`.

## Vérifications réalisées

Trois tests ciblés Studio PASS : statut expérimental, sélection stricte, hash du
manifeste modifié pour la seule eau chargée puis restauré en retirant le milieu.
Entrées du script de packaging vérifiées en `ValidateOnly`, y compris le refus
des deux options d'eau simultanées. Cette vérification d'entrées utilise un ancien
frontend et **ne prouve pas** l'exécution de la nouvelle route.

Tests workspace : **656 PASS, un ignoré**. Clippy WebGPU strict PASS après ajout
d'un point-virgule dans le chemin d'erreur `cfg(wasm32)` ; premier diagnostic
conservé dans `web-clippy-first-failure.log`. La reprise réutilise les tests
natifs, non affectés par ce changement purement syntaxique dans l'initialisation
WASM, et rejoue Clippy WASM avant le build. Format et diff check PASS.

Build frontend/Tauri terminé, candidat lancé et exports réels inspectés.
Preuves locales :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-medium-contact-20260909/`.
Jobs Rust/Binaryen limités à deux, caches et sorties sur E:. Le fichier
`candidate/build-proof.json` reste `BUILD_ONLY` jusqu'à preuve séparée du rendu.
Ne pas présenter ce rapport intermédiaire comme une qualification artistique,
une validation humaine, un passage de gate ou une CI distante verte.

## Résultat du rejeu Studio — 9 septembre, 08:00 locale

Session de build 57969 terminée avec code 0, sans redémarrage du build actif.
EXE SHA-256 `6F0661B4957EA460C710EA0B90237CCC322BB7F48DF140B421986C5D0D81AC34` ;
WASM `43029A051371B42DC6C1036098090BA9D4BB5AA44B0672A90C859A9780923089`.
Entrée `water_close`, caméra candidate inchangée, `forge_water=clear-medium-preview`.

Exports locaux dans le dossier de preuves ci-dessus :

- `forge-diagnostic-1788933643274.json` :
  `55A6E9642FFD20B3262B68BB7F9FB5D82ABC8334D8BDDC7778AE5BDF28606B29`.
- `forge-viewport-1788933655502.png` :
  `95E204555C1B598A6075AC2D9FA3061A20C9B7D72A8F74545CA186CDE994AD23`.

Une initialisation, canvas/rendu 1408 × 966. Préparation 17 772,4 ms,
74 pipelines, zéro en attente et zéro en échec final ; pic transitoire de
18 erreurs rapportées pendant la préparation, non effacé du diagnostic.
Manifestes sélectionné/runtime et observation d'eau concordants :
`d30f5ae8cbcda62eaee985cd53dcf2d7fa7107050cba073f0018d5087bfe9434`.
Statuts effectifs `experimental_rgb_medium_transmission_not_qualified` et
`rgb_extinction_coefficients_replace_legacy_opacity`, uniforms concordants.
Kit 20/20, forêt 1662 arbres / 19944 primitives. Colliders du kit à zéro dans
cette vue à temps figé : aucune qualification physique déduite.

PNG GPU réellement exporté puis inspecté : fond visible au premier plan,
raccord adouci, eau lointaine encore régulière et claire, berges anguleuses,
horizon peu crédible. Intégration constatée, pas d'acceptation artistique.
Le diagnostic CPU indique toujours présentation GPU non observée : le PNG est
une preuve distincte, non atomique avec le JSON, et ne change pas ce statut.
Pas de comparaison temporelle ni de qualification des profils dans cet essai.

Cadence de boucle WASM sur 120 échantillons : moyenne 25,484 ms, p95 26,7 ms,
1 % low 32,31 FPS. Vue figée, résolution non Full HD et hors protocole :
**pas un benchmark Gate 1**, ni une preuve de 60 FPS. Mémoire linéaire WASM
1 545 863 168 octets ; ce n'est pas une mesure de VRAM.

Processus d'essai 27088 et descendants 33780, 8388, 33160, 15084, 26312,
20100, 2580 fermés ; absence vérifiée. Aucune session utilisateur fermée.
Lot d'intégration terminé, R1-022/023 restent ouverts. Ne pas rejouer ce même
cadrage inchangé ; prochaine qualification indépendante : effets/profils
atmosphériques dans le Studio, avant synthèse globale de clôture.
