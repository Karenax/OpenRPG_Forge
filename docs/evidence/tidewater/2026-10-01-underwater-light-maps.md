# Tidewater — cartes d'éclairage des surfaces immergées

Reprise du [rapport d'exposition](2026-09-30-studio-exposure-sweep.md), après
[les rayons sous-marins](2026-10-01-studio-caustic-shafts.md).
Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/UnderwaterLighting.js` et `Caustics.js`.

**Calcul partagé, raccordements et parcours natif/Studio validés.** Les
échecs sont conservés. Ce lot ne constitue pas un verdict de parité de toute
l'île, ni une qualification de gate.

## Implémentation

Le terrain Studio passait encore `caustics: None`, et les matériaux village,
pontons et passerelles recevaient des entrées désactivées. Le terrain natif
calculait des vagues simplifiées par fragment sans les vagues côtières, l'écume
ni le facteur local de vent. Ces trois raccordements lisent maintenant les
cartes GPU partagées avec leur matériau d'eau préparé.

La passe reproduit deux couvertures de 128 m et 1 024 m, chacune à 512² :
hauteur/pente/écume et niveau moyen/détail. Deux textures RGBA16F à deux couches
conservent les quatre cartes source et occupent 8 MiB. Les origines sont
calculées depuis la caméra du **GPU**, calées sur des texels de 0,25 m et 2 m,
et publiées avec le véritable horodatage du matériau optique. Les matériaux
lisent ce même buffer après la passe ; aucune origine de caméra n'est recopiée
dans chaque bâtiment à chaque frame.

Le calcul utilise le terrain auteur et ses corrections, l'atténuation des
cascades FFT, les vagues côtières, la simulation d'écume et le détail de surface.
Le terrain reçoit le terme direct avec photons, pente, écume et dispersion,
puis l'atténuation ambiante. Le mode léger des objets solides reçoit les deux
atténuations depuis le niveau moyen. Les plantes et l'eau gardent leur éclairage.

Studio expose « Éclairage des surfaces immergées » et « Caustiques » (0 à 4,
défaut source 0,75). Le menu natif possède aussi ces réglages. Les anciennes
préférences locales gardent leur exposition et obtiennent ces valeurs par
défaut ; aucune mutation du format auteur.

## Contrôles acquis

- Compilation native initiale et après correction de caméra : PASS ; cette
  dernière dure 52,48 s.
- Tests caustiques : 7 PASS ; filtre sous-marin : 11 PASS, dont les deux
  contrôles du bake et du reçu GPU.
- Clippy strict du moteur : échec conservé, 64 diagnostics préexistants après
  correction des deux nouveaux diagnostics du lot. Aucun diagnostic restant
  dans `tidewater_underwater_lighting.rs`. Le premier essai sans `--no-deps`
  échoue auparavant dans `forge_assets`/`forge_project`.
- Clippy strict Studio : 31 diagnostics préexistants. Le lot avait ajouté un
  quatrième booléen aux préférences ; son état d'éclairage devient un enum à
  deux variantes, sérialisé comme le même booléen pour conserver les
  préférences. Le contrôle 150 retrouve les 31 diagnostics antérieurs, sans
  désactivation d'une règle.
- Formatage ciblé des sources du lot : PASS. Le blocage connu de `cargo fmt`
  global sur le fichier manquant du probe reste distinct.

Le premier lancement natif 146 **échoue** : le layout du matériau d'eau natif
ne rend pas ses textures visibles en COMPUTE (`group 1, binding 3`). Le reçu
et l'identité du binaire sont conservés ; le processus est fermé. La correction
étend la visibilité des liaisons du matériau natif à cette passe, sans changer
les octets source du shader ni les documents. Studio possédait déjà ces flags.
[Reçu 146](../../../../.tidewater-integration/native-underwater-light-maps-146/run.json).

## Défaut de caméra native détecté après le run 148

Le run 148 passe le parcours et les shaders, mais son audit découvre une
origine GPU `[-22, -154, -470, -602]` restée autour de la caméra initiale. Le
shader d'eau visible remplaçait sa caméra privée depuis la vue Bevy ; les
passes compute lisaient encore la capture optique d'origine. Son reçu PASS
initial reste consultable ; [l'audit caméra](../../../../.tidewater-integration/native-underwater-light-maps-148/camera-audit.json)
limite explicitement ce qu'il prouve.

Le matériau natif reçoit désormais les matrices et positions après la
propagation des transformations, chaque image, même lorsque CDLOD ne
renouvelle pas sa sélection. Le protocole des trois paires impose des
origines GPU **exactement** égales aux positions caméra arrondies au texel et
des étapes/temps égaux aux vagues, à leurs requêtes et au matériau optique.
Il attend la lecture GPU ; le compteur de commandes n'est pas une clôture GPU.

Le lancement 150 échoue avant la scène : une substitution du numéro de lot
dans le wrapper avait aussi altéré l'UUID de zone. Son reçu reste conservé.
Le wrapper suivant reprend les identités intactes et change seulement son
répertoire de sortie ; aucune donnée auteur n'est modifiée.

Le run 151 passe les sept étapes : 3 808 ticks, 791 ticks au sol, une entrée
en nage et un retour, backend stable et aucune dérive en nage neutre. Les
trois paires Float/Dive/Neutral conservent leurs signatures. La vérification
indépendante retrouve `[-24.25, -34.75, -474, -484]` depuis la caméra réelle
`[39.96768, 29.47117]`, avec le même temps source. Float conserve des PNG
identiques, Dive et Neutral montrent l'effet du milieu. Les images sous le
ponton ont été inspectées ; ce test désactive **le milieu entier**, et ne
mesure pas isolément les surfaces ou les rayons natifs. Les processus sont
fermés.
[Parcours](../../../../.tidewater-integration/native-underwater-light-maps-151/probe.walk.json),
[identité du binaire](../../../../.tidewater-integration/native-underwater-light-maps-151/run.json),
[audit indépendant](../../../../.tidewater-integration/native-underwater-light-maps-151/light-maps-verification.json).

## Studio : premier candidat 146, parcours 147

WASM release : PASS en 8 min 07 s. Le candidat porte le SHA-256
`42a0ebb715d7bc7ad2395ed611240a05e2d4ca56fc860929f1970dafd091897a` ;
le WASM `51192c11204a2ac39943bdea17ddb7c99b04530e11d8427fdce37e69ff3ade23`.
Son parcours réel WebView2 passe l'ouverture auteur, le jeu, les huit étapes
de nage, la sauvegarde/réouverture et le retour à l'édition. Les deux nouveaux
réglages persistent ; les anciennes préférences récupèrent true/0,75 tout
en gardant une exposition personnalisée de −0,75 EV.

Les cinq triplets activé/désactivé/répété fixent caméra, vent et horloge
source, conservent le milieu, mettent les rayons à zéro et vérifient le
véritable reçu GPU, un matériau terrain et sept matériaux solides légers.
La reprise de simulation après chaque triplet est observée. Les répétitions
retrouvent exactement le même PNG. Les vues de fond activées/désactivées
ont été inspectées ; le réglage Caustiques à zéro retire les traînées de
photons tout en conservant l'atténuation sous-marine.

| Vue 147 | Écart RGB moyen /255 | Pixels modifiés de plus de 2 niveaux | Répétition |
|---|---:|---:|---:|
| Air | 0 | 0 % | PNG identique |
| Bande de surface | 11,450 | 42,361 % | PNG identique |
| Immergée horizontale | 15,373 | 43,873 % | PNG identique |
| Fond | 47,024 | 99,422 % | PNG identique |
| Fond, photons seuls | 14,918 | 91,878 % | PNG identique |

[Parcours Studio 147](../../../../.tidewater-integration/studio-tidewater-light-maps-runtime-147/fish-stand-studio-smoke-ledger.json),
[vérification GPU et pixels](../../../../.tidewater-integration/studio-tidewater-light-maps-runtime-147/light-maps-verification.json).

Les deux fenêtres indicatives de cadence rAF restent à 128,79 puis 124,84
images/s (candidat précédent 143 : 129,79 puis 126,14). Elles précèdent le
passage en plein écran ; les heures/caméras ne sont pas synchronisées entre
les runs. Elles ne mesurent ni un coût GPU isolé ni une performance garantie
en 1 920 × 1 080.

## Candidat Studio final 150, parcours 151 : PASS

La reconstruction release passe en 3 min 54 s. Le binaire final possède le
SHA-256 `c9ea1e504935968649cd0bee1b733a6155fa7eb0d7ee20a3175cb02b2f6cfb35` ;
le WASM `db31b1233148d7ead997daa072f384c48497213d3dddb8fd760bb597049b9b84`.
Le contrôle 151 refait le parcours complet et la migration/persistance des
préférences avec leur représentation finale, les mêmes shaders et les mêmes
liaisons de terrain/objets. Les cinq triplets et leurs lectures GPU passent,
et toutes les répétitions sont des PNG identiques. Les captures du fond et
des photons seuls ont été inspectées.

| Vue 151 | Écart RGB moyen /255 | Pixels modifiés de plus de 2 niveaux | Répétition |
|---|---:|---:|---:|
| Air | 0 | 0 % | PNG identique |
| Bande de surface | 14,078 | 41,021 % | PNG identique |
| Immergée horizontale | 15,595 | 44,094 % | PNG identique |
| Fond | 42,981 | 99,422 % | PNG identique |
| Fond, photons seuls | 15,924 | 91,738 % | PNG identique |

Les vues 147 et 151 ne partagent pas leurs temps de simulation ou leurs
profondeurs exactes ; ces métriques décrivent l'effet à l'intérieur de chaque
triplet et ne mesurent pas une régression pixel entre candidats.
[Parcours final](../../../../.tidewater-integration/studio-tidewater-light-maps-runtime-151/fish-stand-studio-smoke-ledger.json),
[vérification indépendante](../../../../.tidewater-integration/studio-tidewater-light-maps-runtime-151/light-maps-verification.json),
[captures du fond](../../../../.tidewater-integration/studio-tidewater-light-maps-runtime-151/lighting-floor-on.png).

Les 24 empreintes d'entrées de build 150 sont inchangées après les essais.
L'archive auteur source conserve
`50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
Les processus d'essai natif et Studio sont fermés.
[Entrées figées](../../../../.tidewater-integration/underwater-light-maps-inputs-150.json).
[Identités des binaires, scripts et reçus](../../../../.tidewater-integration/underwater-light-maps-artifacts-151.json).

La cadence indicative du candidat final est de 127,30 puis 124,54 rAF/s ;
les p95 sont de 8,2 et 8,5 ms, les 1 % bas de 78,92 et 62,28 rAF/s. Le
même périmètre limité avant plein écran s'applique à ces deux fenêtres.

## Limites

Au terme du candidat 150, le hook source qui déplace l'entrée d'ombre par
réfraction dans les vagues et la variante monochrome du rendu de réfraction
ne sont pas encore raccordés.
La preuve séparée de tous les rochers/récifs reste ouverte. Ces cartes ne
qualifient pas la torche, le TAAU, les autres comportements, le package Aube,
ni une comparaison de toute l'île à horloge source synchronisée. L'objectif
complet reste actif.

Le prochain raccordement visuel porte sur le point d'entrée des ombres dans
l'eau : hauteur/pente de la carte complète pour le terrain, niveau moyen
pour les objets légers, rayon réfracté selon l'indice 1,333, puis consultation
des ombres au point d'entrée. Le jitter temporel source dépend aussi d'une
résolution temporelle qui reste à qualifier ; recopier ce bruit sans cette
résolution ne prouverait pas le même effet.

Suite : [réfraction du point d'entrée des ombres](2026-10-01-refracted-underwater-shadows.md).
Le raccordement est ajouté et prouvé en natif et dans Studio sur les piles
du ponton, après ajout des casters manquants des accessoires. La preuve terrain
séparée reste ouverte. Le statut des cartes décrit plus haut reste celui
du candidat 150 / runs 151.
