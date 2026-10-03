# Tidewater — locomotion du joueur et nage native

## Portée

La promenade native utilise désormais la locomotion de `Player.js` pour la
marche, le pataugeage, l'entrée en nage, le flottement, la plongée, la remontée
et le retour à pied. L'adaptateur conserve le joueur Forge et son contrôleur
cinématique Rapier ; il consomme les hauteurs GPU de la surface Tidewater et le
relief canonique. Le branchement est optionnel et ne change pas les sessions qui
n'installent pas `TidewaterPlayerPlugin`.

Référence : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/player/Player.js`, SHA-256
`1838be4d10e6e4df64fb07b709d75c1c106d02c6b5b6beac24b6e3b644900bbb`.
Le module dérivé `tidewater_locomotion.rs` conserve l'attribution et la notice
MIT de Tidewater.

## Port et raccordement

`forge_engine::tidewater_locomotion` porte les équations sans ECS : capsule de
1,75 m en marche et 1 m en nage, rayon de 0,3 m, marche à 3 m/s, sprint à
6,2 m/s, nage à 1,5 ou 2,5 m/s, saut à 4,6 m/s, accélérations, gravité,
ralentissement du pataugeage, filtre moyen de l'eau sur quatre secondes,
seuils d'entrée/sortie, flottement, plongée, remontée et amortissement de la
caméra. La position de cet état est l'ancre aux pieds de la source, séparée du
centre de la capsule Forge.

`TidewaterPlayerPlugin` adapte cet état au joueur existant. Il réutilise le slot
de requête d'eau nommé `player`, demande les translations au
`KinematicCharacterController`, puis résout la pose réellement écrite par
Rapier. Le sol retenu est le maximum entre le relief canonique et les contacts
du village. Le passage marche/nage modifie le collider de la même entité et
conserve le handle backend ; l'autostep et l'accrochage au sol sont retirés sous
l'eau puis restaurés à la sortie. La caméra utilise la hauteur d'œil amortie de
la source. La barrière de disponibilité gèle à la fois le mouvement et
l'ingestion des requêtes d'eau tant que la scène physique/GPU n'est pas prête.

Le mode interactif démarre à la position `WORLD.start` de la référence,
X 53,6 / Z −77. WASD dirige le joueur, Maj accélère, Espace saute ou remonte,
C ou Ctrl plonge, V change de caméra et Échap libère la souris. Le smoke de
nage emploie une route distincte X 40 / Z −50 vers l'océan puis revient sur la
plage ; ses commandes passent par les mêmes ressources que le contrôleur, sans
translation directe de l'entité.

## Comparaison directe avec la source

L'oracle `player-swim-source-oracle-001.mjs` importe et exécute le vrai
`Player.js` gelé. Le comparateur Rust rejoue 39 scénarios et 1 110 frames aux
pas 1/60 s, 1/30 s et 0,1 s : marche, pataugeage, sprint, saut, entrées depuis
le sol ou une chute, vagues, flottement, C/Ctrl, plongée orientée par le regard,
remontée avec Espace, sortie en faible profondeur et caméra.

Il ne relève aucun désaccord de mode, contact au sol ou flottement. Aucun seuil
n'est utilisé pour déclarer les trajectoires égales ; les erreurs absolues et
distances ULP sont consignées. Erreurs absolues maximales observées :

| Grandeur | Erreur maximale |
|---|---:|
| Position X / Y / Z | 3,31e-9 / 8,68e-7 / 3,62e-7 m |
| Vitesse X / Y / Z | 3,41e-9 / 2,05e-6 / 6,00e-7 m/s |
| Œil X / Y / Z | 3,31e-9 / 9,14e-7 / 3,62e-7 m |
| Moyenne d'eau | 1,72e-7 m |
| Phase du balancement | 9,61e-7 |

La hauteur d'eau et le facteur de pataugeage sont identiques après conversion
f32 sur les échantillons concernés. Artefacts :

- `player-swim-source-oracle-001.json`, SHA-256
  `e15e8d248102245c621192bcb91da16ce5f24f783e634615c6e43bd9dfe48c27` ;
- `player-swim-rust-comparison-001.json`, SHA-256
  `99d534c93a5bf698e0ef49e5088183f54d5ca14e6ca89809cb62a33806003460` ;
- module Rust comparé, SHA-256
  `f9ae6b8261caade973c01b2cdcfb7963baa6ea122c5d941a337f21cce56063ee`.

Cette comparaison emploie des hauteurs d'eau CPU déterministes et des sols
contrôlés. Elle isole les équations ; elle ne remplace pas l'essai Rapier/GPU
ci-dessous.

## Vérifications CPU, native et WASM

Le harness direct `swim-controller-cpu-001` compile les modules réels avec les
dépendances du projet : 36 tests passent en 1,69 s. Quatre tests nouveaux
exercent un vrai `App`, les fixed updates, Rapier et le KCC : entrée depuis le
sol, changement de capsule sans remplacement du handle, plongée/flottabilité/
remontée, sortie en eau peu profonde et barrière de disponibilité. Les six
tests purs de locomotion sont inclus ; les 26 tests existants de contrôleur et
caméra restent verts.

- `cargo clippy -p forge_engine --lib --no-deps -- -D warnings` : PASS ;
- Clippy strict de l'exemple `tidewater_plant_probe` : PASS ;
- build natif de l'exemple : PASS en 6 min 40 s ; binaire SHA-256
  `f6f28f9e0f69f2b96f0219bdefa3b8a18f2cf52071f8cc984e69775adc70c3ec` ;
- Studio `--no-default-features --features web --target wasm32-unknown-unknown` :
  PASS ; avertissement futur préexistant de `proc-macro-error2`, sans exécution
  dans un navigateur.

Le dernier Clippy global avec `--tests` a signalé 26 diagnostics : l'import
inutilisé propre aux tests de nage, retiré depuis, et 25 diagnostics dans
d'autres tests/modules du chantier partagé (`cloud_controller`, `spray_budget`,
nuages, mesh, ombres, plantes et requêtes d'eau). Ce Clippy global n'a pas été
relancé après la correction locale ; la bibliothèque et l'exemple livrés ici
passent strictement.

## Parcours natif avec eau GPU

`native-swim-002.walk.json` termine avec `pass: true` après 3 739 fixed ticks.
Le trajet réalise une entrée en nage et un retour en marche. Il vérifie sept
étapes : entrée, flottement, plongée, neutralité, remontée, surface et retour.

- un seul backend physique pendant tout le trajet ;
- facteur de pataugeage maximal 1 ;
- dérive mesurée pendant la phase neutre : 0 m ;
- plus grand déplacement vertical de caméra entre deux ticks : 0,05921 m ;
- 885 ticks avec contact au sol ; aucun timeout ;
- les paquets GPU suivent la position du joueur jusqu'au pas 8 929 et au temps
  d'eau 64,2861929 s ; les hauteurs varient avec la surface animée ;
- retour en mode marche à X 40,1015 / Y 0,3807 / Z −47,0944, sur le relief
  canonique et avec le collider de marche restauré.

Les captures FHD `Float`, `Neutral`, `Surface` et finale ont été inspectées :
la caméra se place au ras de la surface, passe sous l'eau, remonte avec les
vagues puis revient sur la plage. Le ponton, le fond, la surface animée, le
ressac et l'écume restent visibles. SHA-256 des quatre PNG :

- `native-swim-002.Float.png` :
  `1200419cb14d33f1dae00254d48acad994dcc880364ae8901f2465bf27763868` ;
- `native-swim-002.Neutral.png` :
  `1a182ff18cafa8545b8767e514eaf16c853f7e7cd7a78e6464311ca10445b8cb` ;
- `native-swim-002.Surface.png` :
  `d9dfbe33275f0508285917d7b150e18e3fdf2eb0537b82b2b95fb74bbe6453e3` ;
- `native-swim-002.png` :
  `7f551786802d18208aba99d85688c512e04f5b95a92ac1efced2f22615d7bee0`.

Régression marche `native-walk-swim-controller-001` : PASS, 8,994282 m,
1 834 ticks au sol, backend/caméra/cadence/cascades valides. La capture FHD a
été inspectée ; elle conserve plage, végétation, village, ponton et océan.

Les deux avertissements de canal readback fermé arrivent pendant l'arrêt, après
le PASS et les écritures des reçus. Les processus se terminent avec le code zéro.

## Limites

Ce lot qualifie la locomotion jouable par commandes injectées dans Forge ; il
ne constitue pas une preuve de clavier/souris au niveau du système d'exploitation.
L'optique sous-marine finale, les gouttes, sons, sillages `WakeSim`, échelles,
bateau, pêche, commerce, progression et sauvegarde restent ouverts. Les sillages
ne contribuent pas encore aux requêtes de hauteur d'eau.

L'exécution WebGPU dans un navigateur, l'édition de cette carte dans Studio, la
distribution Runtime autonome, la parité artistique animée et la revue du
propriétaire restent également à qualifier. Les artefacts de preuve sont locaux
sous `E:/OpenRPG_Forge/.tidewater-integration/`. Aucun commit ni push.
