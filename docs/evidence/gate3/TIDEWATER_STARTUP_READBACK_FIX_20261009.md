# Tidewater — correction du crash de démarrage et retrait des coupures

Date : 9 octobre 2026. Correction locale 013. Gate 3 reste ouverte et non qualifiée globalement.

Le jeu pouvait quitter quelques secondes après « Lancer l’île Tidewater ». Le crash intermittent
a été reproduit dans le parcours normal, sans drapeau de qualification : un callback GPU de
l’étape 73 était reçu alors que cette même étape était déjà acceptée (`encoded=73`, `latest=73`,
`already_completed=true`, 4 octets). Le code traitait ce doublon comme une erreur fatale.

Les composants Bevy `Readback` peuvent produire une lecture chaque frame tant que leur retrait
n’a pas atteint le monde de rendu. Le traitement de l’initialisation des embruns accepte désormais
chaque étape une seule fois. Les doublons, y compris après une étape plus récente, conservent le
premier résultat et n’avancent pas une seconde fois la barrière FFT/shore/spray. Les tailles invalides,
étapes inattendues et incohérences d’ordre restent des erreurs explicites.

Les deux coupures après 600 frames dans les lectures initiales de l’océan et des embruns sont
également supprimées, conformément à la demande propriétaire. Aucun délai de remplacement
n’est ajouté à ces étapes du jeu. Les simulations et copies GPU continuent jusqu’à leurs résultats.

## Validation

- Cinq tests ciblés PASS : premier résultat, doublon immédiat, doublon tardif, étape future invalide,
  ordre incohérent. Le banc compile les types et la méthode exacts du fichier produit avec ses tests.
- Régression des anciennes coupures : lecture initiale retardée dans un binaire de diagnostic,
  échec à la frame 601 après 6,472 secondes de temps application ; après retrait, les 120 étapes GPU
  et les lectures réelles terminent à la frame 804. Ce retard artificiel est absent du runtime installé.
- Trois démarrages ordinaires PASS via la fonction `play::launch` inchangée du launcher :
  90, 20 et 20 secondes, bootstrap complet et fermeture normale sans arrêt forcé.
- Exécutable installé vérifié directement dans son dossier réel pendant 30 secondes : PASS,
  bootstrap complet et fermeture normale.
- Menu Échap PASS sur les mêmes octets finaux : paramètres d’affichage, lumière, accès aux
  réglages graphiques et bouton « Quitter le jeu ». Capture inspectée visuellement.
- Analyse Rust/Clippy PASS sans nouveau diagnostic. Les avertissements antérieurs subsistent.
- Sources ciblées formatées, snapshots et dépendances épinglés vérifiés.

Le banc du launcher utilise sa fonction de lancement réelle et son environnement de jeu. Le clic
physique dans le WebView n’est pas exercé par ce banc. Les tests bornés et leurs protections de
ressources appartiennent au laboratoire ; ils n’ajoutent aucun seuil de mémoire disponible au jeu.

Pics attribués aux processus de test : 3.568 GiB privés,
1.923 GiB GPU dédiés et 0.072 GiB GPU partagés.
Tous les moniteurs se terminent sans protection forcée et sans descendant restant.

## Livraison

Runtime installé : `E:/OpenRPG_Forge/Tidewater-Launcher-Fullscreen-20261008/runtime/tidewater.exe`.
SHA-256 : `4428a37f19df035c4d895de92d36c8a92ddd040735f922383c916a16c950df9b`.

Version 012 sauvegardée dans `backups/tidewater-012.exe` du dossier de correction 013.
Le launcher conserve son SHA-256 `5c8e7c919893d28f4e40a0ca536b74316d32848d73b88b31653962e33b372e5b`.
Trois fichiers de source sont intégrés : les deux modules de lecture GPU et le fichier de tests.
Leurs octets correspondent au snapshot compilé. Les préférences réelles sont conservées.

Le moteur optimisé et le SDK fermé de 012 sont réutilisés, après vérification des sources et hashes.
Le ciel Dense et les optimisations de contexte de 012 sont conservés. Aucun nouveau comparatif FPS
n’est revendiqué. Il s’agit d’une compilation native locale du snapshot, sans qualification release
du workspace complet, sans validation WebGPU/WASM et sans CI distante (`NOT RUN`). La validation
humaine du runtime 013 reste à effectuer ; ce rapport ne clôture pas la Gate 3.

## Preuves archivées

Dossier : `E:/OpenRPG_Forge/.tidewater-integration/launcher-repair-20261009-013`.

- `launch-origins-005/runtime.log` : crash intermittent avec doublon de l’étape 73.
- `normal-regression-before/execution.json` et `launch-regression-after/execution.json` : coupure
  à 601 frames et reprise après 804 frames dans le scénario retardé.
- `feedback-tests-002/` : code testé, hashes, compilation et cinq tests.
- `build-007/` : exécutable final, paramètres, sources et dépendances épinglés.
- `rust-check-002/` et `clippy-comparison.json` : analyse statique et comparaison des diagnostics.
- `launch-fixed-001/`, `launch-fixed-002/`, `launch-fixed-003/` : démarrages ordinaires.
- `menu-proof-001/` : captures et contrôle du menu.
- `normal-installed-001/` : contrôle du fichier effectivement installé.
- `deployment.json`, `summary.json` et `backups/` : installation vérifiée et sauvegardes.
