# Tidewater — raccordement de la nage dans Studio

Suite : [contact de la capsule sur les pentes](2026-10-01-studio-slope-contact.md).
Le défaut de forêt ci-dessous est conservé comme résultat du candidat 129.

Suite des [hauteurs GPU de surface](2026-10-01-studio-surface-water-queries.md),
référence figée `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Le parcours aquatique Studio passe ; ce rapport ne clôture ni TW-008/TW-012 ni l'objectif
complet de l'[ADR-0017](../../ADR/ADR-0017-tidewater-fidelity-integration.md).

## Changement

Le Playtest importé qui dispose de l'optique d'océan utilise maintenant le
`TidewaterPlayerPlugin` existant, avec son registre partagé de hauteurs GPU
et le plan de terrain dérivé des documents d'auteur. La capsule, les vitesses,
les limites de caméra et la vue initiale horizontale suivent le parcours natif.
Le joueur et Rapier existants restent la projection physique ; aucun deuxième
simulateur ni plan d'eau constant n'est introduit.

La barrière de Playtest attend le premier paquet comprenant caméra et joueur.
Le panneau indique Espace pour remonter, C ou Ctrl pour plonger, et publie le
mode marche/nage. Le diagnostic des cascades contient le mode, l'ancre, la
vitesse, le fond, les transitions et la position/heure du point d'eau consommé.

Le premier parcours réel 128 a révélé que la mesure du joueur restait à son
départ : le contrôleur allouait son point, mais le nouveau producteur Studio
n'actualisait que celui de la caméra. Le contrôleur expose désormais l'index
qu'il a effectivement alloué ; le producteur met à jour ce point depuis son
ancre courante avant l'envoi GPU, sans supposer que l'index vaut toujours 1.
Le [contrôle natif corrigé](../../../../.tidewater-integration/studio-swimming-native-check-129.log)
passe en 52,02 s.

## Échecs conservés et contrôle de l'empaquetage

Le candidat 124 avait été compilé sans `--no-default-features --features web`.
La construction était valide mais ne contenait pas le démarrage de l'éditeur.
Le [smoke 125](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-125/fish-stand-studio-smoke-ledger.json)
échoue avant ouverture. `Build-StudioCandidate.ps1 -Authoring` refuse maintenant
un module sans les points de démarrage Web attendus. La [vérification directe](../../../../.tidewater-integration/studio-frontend-feature-guard-127.json)
rejette ce frontend et accepte celui du 121, sans construire ni créer de sortie.

La construction Web initiale du 127 échoue aussi sur l'accès à une structure de
caméra privée. Cet accès est supprimé ; le raccordement conserve les interfaces
publiques et la vue initiale du contrôleur natif. La construction corrigée et
l'empaquetage passent. Le [parcours 128](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-128/tidewater-studio-swimming.json)
échoue avant la nage : en plus du point figé décrit ci-dessus, le passage en
plein écran avait changé le cap pris par le test. Le parcours suivant choisit
les touches vers la mer selon le cap réellement observé, puis leur inverse
pour le retour. Il exige aussi un écart point joueur/ancre inférieur à 75 cm.

Clippy Studio strict passe de 32 à 31 diagnostics après simplification du
nouveau prédicat de disponibilité. Les fonctions longues, paramètres nombreux,
types complexes et autres diagnostics restent ouverts ; aucun lint global
n'est désactivé. Le contrôle de la version corrigée 129 retrouve ces 31 diagnostics.
[Journal final](../../../../.tidewater-integration/studio-swimming-clippy-129.log).

L'essai 130 du 129 entre effectivement en nage, avec une mesure qui suit le
joueur, mais s'arrête sur une assertion de contact incorrecte. Le champ brut
`State::grounded` reste celui de la marche pendant la nage ; le contrôleur
publie déjà le contact physique normalisé dans `PlayerRuntimeSnapshot`.
Le test est corrigé pour lire ce dernier état dans le statut du Playtest,
sans modifier les règles de nage. Le [ledger 130 échoué](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-130/fish-stand-studio-smoke-ledger.json)
reste conservé. Les échecs 125, 128 et 130 ne sont pas des preuves de réussite.

## Candidat 129 et parcours 131

La [construction WASM corrigée](../../../../.tidewater-integration/studio-swimming-build-129.log)
passe en 6 min 54 s ; les avertissements de copie du cache incrémental et de
compatibilité future de `proc-macro-error2` restent consignés. Le
[reçu du candidat](../../../../.tidewater-integration/studio-tidewater-swimming-candidate-129/build-proof.json)
reste `BUILD_ONLY`, distinct de l'exécution ci-dessous.

SHA-256 exécutable :
`f558abdf7aadf588d83e6b336743d51032df7ed8a2638416f9bf25cb54ca6036`.
SHA-256 WASM :
`36908ce1b5c982a546863b85dd6adf04a6291c6803e418ab07e4f639dd3afefe`.

Le [smoke 131](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-131/fish-stand-studio-smoke-ledger.json)
passe : ouverture, pose du stand, sauvegarde/reouverture, départ temporaire par
l'interface X/Z 40/−50, capture réelle de souris, parcours clavier et retour à
l'édition. Le canvas mesure 1920 × 1080. Le cap observé après plein écran conduit
à utiliser S vers la mer, puis W au retour, avec Maj ; le test ne téléporte pas
le joueur et ne réécrit pas son état de simulation.

La [vérification des huit étapes](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-131/swimming-verification.json)
passe, avec une entrée en nage et un retour à la marche. Le point consommé
par le joueur parcourt 52,012 m jusqu'à la flottaison ; son écart à l'ancre aux
captures reste inférieur à 0,000042 m, très en deçà du seuil de 0,75 m. Les
paquets comportent deux points, avancent sans rejet et leur heure correspond
exactement au f32 du shader.

| Étape | Mode | Ancre Y | Surface Y | Fond Y | Contact physique |
| --- | --- | ---: | ---: | ---: | --- |
| Plage initiale | marche | 0,399 | 0,000 | 0,399 | oui |
| Flottaison | nage | −0,105 | −0,144 | −2,394 | non |
| Plongée C | nage | −1,497 | −0,271 | −2,394 | non |
| Maintien sans entrée verticale | nage | −2,012 | 0,134 | −2,394 | non |
| Remontée Espace | nage | 0,358 | 0,423 | −2,394 | non |
| Plongée Ctrl | nage | −1,280 | −0,293 | −2,394 | non |
| Seconde remontée | nage | 0,286 | 0,236 | −2,394 | non |
| Retour plage | marche | 0,485 | 0,000 | 0,485 | oui |

Après trois secondes sans entrée verticale, la vitesse Y vaut
−0,000038 m/s et l'ancre reste 0,382 m au-dessus du fond. Il s'agit d'un témoin
local de maintien sous la surface, pas d'une qualification de toutes les
profondeurs ou conditions. La cadence rAF du parcours aquatique n'est pas
revendiquée comme benchmark ; Clippy a chevauché son chargement initial.

Les [captures de flottaison](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-131/swim-float.png)
et de [maintien sous la surface](../../../../.tidewater-integration/studio-tidewater-swimming-smoke-131/swim-neutral.png)
ont été inspectées. La seconde montre encore une eau bleue avec un fond très
clair et un flare visible : le post-traitement d'immersion reste absent.

La source demeure intacte dans le ledger et dans le contrôle final, SHA-256
d'`authoring.json` :
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.

## Forêt 132 : rendu passé, défaut de locomotion ouvert

Le [smoke 132](../../../../.tidewater-integration/studio-tidewater-swimming-forest-132/fish-stand-studio-smoke-ledger.json)
passe en 1920 × 1080, douze segments de cinq secondes, sauvegarde/reouverture
et retour à l'édition inclus. Aucun compilateur ne tourne pendant les mesures.
Les trois cascades progressent sans récupération ni remplacement de génération,
et le compteur d'éclairage reste à 2. Les points GPU caméra/joueur sont valides,
sans rejet ; l'écart maximal du point joueur consommé à l'ancre vaut 0,187 m.
La [capture finale](../../../../.tidewater-integration/studio-tidewater-swimming-forest-132/fullscreen-forest-end.png)
a été inspectée : plantes, terrain et ombres présents.

La [synthèse du rendu](../../../../.tidewater-integration/studio-tidewater-swimming-forest-132/plant-cache-forest-summary.json)
donne 124,72 images/s rAF à l'arrêt, puis 110,76 sur onze segments non profilés.
Les p95 par segment vont de 15,50 à 16,00 ms ; les 1 % low de 28,45 à
53,96 images/s. Ce n'est ni la présentation GPU ni un gain comparé au 121 : le
contrôleur, la vitesse de marche, le pitch initial et la trajectoire diffèrent.
Le verdict « passed » de cette synthèse porte sur le rendu et le cache, pas sur
la qualité du contrôleur.

Le premier segment montre une anomalie : mode marche, contact physique publié
faux, vitesse verticale −216,145 m/s, mais ancre à 64,825 m et fond à 64,699 m,
soit seulement 0,126 m d'écart. Les onze fins de segment suivantes retrouvent
le contact au sol et une vitesse Y nulle. La [revue de locomotion](../../../../.tidewater-integration/studio-tidewater-swimming-forest-132/forest-locomotion-review.json)
est donc **partial**, avec le défaut conservé. Il faut diagnostiquer le contact
Rapier et l'adaptation de l'ancre source sur pente, puis ajouter un contrôle
qui détecte l'accumulation de gravité. Le smoke d'interface passé ne qualifie
pas la locomotion de forêt.

## Qualification restante

Priorité suivante : diagnostic et correction du contact sur pente, avec preuve
du KCC réel et relecture de ce parcours. La nage côtière 131 est qualifiée dans
son périmètre ; la locomotion sur toute l'île ne l'est pas encore.

Le post-traitement d'immersion, le masque de surface visible et les échelles
logiques ne sont pas encore raccordés dans Studio. La comparaison temporelle
directe avec la référence, les autres comportements du plan et la qualification
complète de distribution restent ouverts. Aucun de ces échecs n'est présenté
comme un test passé.
