# Vent lointain : contrôle borné non concluant — 7 septembre 2026

Pas de qualification ajoutée et pas de promotion du mode regroupé. Ne pas relancer
la même série en espérant obtenir un seuil favorable.

## Exécution

Même WASM que le correctif caméra :
`184EE180958152563336B557F805D5AEA1FDDF9FBAC70B880AC76C7CF307476F`.
Copie externe `web-far-wind`, seule modification : FOV de `valley_overview`
10° → 3°, sans changement de carte/asset. Hash du document de vues :
`a06ecd983dd8a370707959fbfc1c80eddc5629ed32caeba9ae9b7531888ad761`.
Chrome, High, clair, `forge_forest=batches`, 1408×741 ; pas un benchmark.

Trois captures à 0 s, trois à 2 s (huit pas de 250 ms), rechargement du même
candidat et trois captures à 0 s. Statuts caméra et horloge vérifiés ; une image
de chaque état ouverte et inspectée. Aucun saut de caméra ou disparition d'arbre.
Aucune erreur dans les logs navigateur consultés. Le chargement initial a donné
un timeout d'observation, sans relance ; le même onglet s'est ensuite initialisé.

Les trois diagnostics donnent near=0, medium=0, grass=0, HLOD=15 visibles CPU,
19 884 triangles visibles CPU. Un arbre gouverné est visible dans la région
mesurée ; ce comptage ne correspond pas à des draw calls GPU.

## Résultat du comparateur existant, sans modification du seuil

`scripts/Compare-OpticalSamples.ps1`, région feuillage [820,170,1060,410],
témoin terrain [500,180,700,400], pas 1. Région choisie sur la première image.

| Écart RGB moyen par canal | Feuillage | Terrain témoin |
|---|---:|---:|
| Maximum entre captures du même état | 1,06990 | 0,03366 |
| Minimum entre 0 et 2 s | 2,45891 | 0,00251 |
| Maximum entre initial et retour | 0,89123 | 0,03023 |

`separated_and_restored=false` : le changement ne dépasse pas trois fois le
bruit/plancher du protocole. La variation est localisée au feuillage et le retour
est proche, mais cela ne suffit pas à qualifier l'animation par ce test.
Pas de sélection de nouvelles régions, de captures favorables ou de seuil réduit.

## Échec supplémentaire de l'identité physique

`Collect-Far-Wind.ps1` échoue explicitement « Kit not ready » : les diagnostics
initial/retour ont 20/20 instances mais 0 colliders enregistrés ; celui à 2 s en
a 36 et est prêt. Les fichiers sont archivés malgré cet échec, pas promus PASS.

Explication vérifiée dans le code : les colliders du kit sont construits en
`Update`, mais leur enregistrement Rapier appartient à `FixedUpdate`, qui ne
s'exécute pas au temps virtuel gelé depuis Startup. Avancer le temps débloque
donc également leur enregistrement. Ce n'est pas une preuve d'échec du chargement
des assets ni une raison d'annoncer une readiness anticipée. La synchronisation
Rapier comprend aussi le contrôleur de personnage : la déplacer globalement en
Update pour contourner le gel modifierait la simulation et n'est pas effectuée.

Cette série reste non concluante. Une future preuve d'animation devra commencer
après initialisation physique et comparer un état témoin identique, ou observer
directement l'exécution du shader ; aucune nouvelle infrastructure GPU ajoutée
sur la seule base de ce test. Les défauts de transition/performance restent des
travaux indépendants prioritaires.

## Artefacts

Racine locale `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907`.
Sous `far-wind-isolation/`, PNG `forge-viewport-<id>.png` :

- initial : 1788782265617, 1788782297973, 1788782307160 ;
- 2 s : 1788782363203, 1788782372585, 1788782384189 ;
- retour : 1788782473658, 1788782482769, 1788782491034.

Diagnostics `forge-diagnostic-<id>.json` : 1788782320788, 1788782398466,
1788782500278. Aucun build ni passage de tests supplémentaire pour ces données
de qualification externes ; aucun push et aucune gate passée.
