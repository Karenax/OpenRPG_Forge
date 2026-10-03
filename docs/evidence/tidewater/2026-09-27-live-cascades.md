# Cascades d'ombres suivant la navigation

Portee : promenade native Tidewater, source figee `1438b1ab`. Suivi spatial
et coherence GPU qualifies localement ; limites temporelles precisees ci-dessous.
Ce lot raccorde le controleur de cascades Rust deja compare a la reference.
Il ne ferme pas la parite visuelle complete ni l'integration du viewport Studio.

Ce document conserve les preuves de cette etape. L'attente d'encodage et la
limite de demi-cadence decrites ci-dessous sont depuis corrigees et qualifiees
par la [preuve de cadence suivante](2026-09-27-shadow-cadence.md), qui precise
aussi le cout de performance observe.

## Implementation

Un `Tracker` conserve `SunCascades` entre les images. Les couches 0/1/2 sont
refaites aux periodes source 1/2/4 par soumission acceptee, avec conservation des matrices et profondeurs
des autres couches. La premiere soumission initialise les trois couches.
L'adaptateur n'avance pas le controleur tant que sa soumission precedente n'a
pas ete encodee ; changer de generation du graphe efface la pose transitoire.

Les uniforms des batiments, du terrain et des plantes restent residents. Les
instances proches des plantes sont reselectionnees dans les donnees canoniques,
et les noeuds CDLOD du terrain dans les bornes deja calculees. Seuls ces petits
streams et les uniforms sont reecrits. Capacites fixes : 1500 noeuds de terrain,
65536 instances par lot de plantes ; les selections vides sont permises.
Les geometries, textures, pipelines et bind groups sont conserves.

Un buffer partage de 464 octets alimente les recepteurs terrain, vegetation,
batiments et eau. Le graphe ecrit ce buffer et encode les couches dues avant
la camera dans la meme soumission. Aucun asset de materiau n'est modifie par
image pour changer les matrices. Le mode statique utilise les uniforms source
tant que le controle live n'a pas ete active.

## Protocole GPU ajoute au smoke

Le controle du vent A/A/B garde ses poses immobiles pendant les readbacks.
Le passage a chaque pose tenue invalide ponctuellement les trois cascades,
afin de lire une pose complete ; ce flush ne sert pas a qualifier la cadence.

Un second controle separe conserve soleil, vent, index temporel, orientation
et projection. Il stabilise quatre mises a jour au point A, puis deplace la
vue de 48 m en X et 32 m en Z, soit 57,69 m. Apres chacune des quatre mises a
jour successives au point B, la pose reste tenue jusqu'aux deux lectures GPU :
tableau depth de trois couches et buffer des recepteurs.

Le verdict exige les masques dus au compteur continu, les matrices GPU egales
aux matrices soumises, la camera GPU egale a celle demandee et l'activation du
buffer live. Une couche non due conserve exactement sa matrice, son oeil et
son hash de profondeur. Chacune des trois couches doit avoir suivi le point B
avec une nouvelle matrice, un nouvel oeil et une profondeur differente de A.
Les deux callbacks portent l'identifiant de leur soumission ; une lecture
retardee d'une requete precedente ne peut pas remplir la suivante.

La conservation des hashes ne prouve pas l'absence d'une passe qui reecrirait
un contenu identique. La restriction des passes aux couches dues est aussi
controlee dans `graph::draw`; aucune instrumentation GPU des compteurs de
passes n'est revendiquee. Le deplacement controle du test ne deplace pas le
personnage et ne prouve pas sa traversabilite sur ces 57,69 m.

## Verifications

Preuves locales sous `E:/OpenRPG_Forge/.tidewater-integration/`.

- `live-cascades-cpu-004.run.log` : 9 tests PASS, dont cadence et conservation
  des matrices non dues durant la navigation, disposition du buffer, adaptation
  du shader, cinq tests du vent et validation Naga du filtre WGSL acquis adapte.
  Harness borne : modules CPU et types issus des sources courantes ; aucune
  compilation de l'ensemble de la suite de tests moteur n'est revendiquee.
- `live-cascades-clippy-008.log` : quatre exemples (plantes, population, ocean,
  spray) et bibliotheque, Clippy final avec `-D warnings` PASS, 1 min 16 s
  incluant l'attente du build natif.
- `live-cascades-build-001.log` : build natif PASS, 7 min 01 s. Binaire SHA-256
  `685e29e51de746110fa454fc3b38754b096817703d872065b6adb2fc6b8383fe`.
- `live-cascades-studio-wasm-001.log` : check Studio lib WebGPU PASS, 42,98 s.
  Avertissement preexistant proc-macro-error2 ; aucune execution navigateur.
- `native-walk-shadow-final-001` : PASS natif, RX 7800 XT/Vulkan/AMD 26.8.1,
  1920x1080, sans compilation ni autre probe pendant la mesure. 1847 ticks,
  dont 1842 au sol, 14,887203 m. Moyenne 143,717852 FPS, p95 8,1586 ms,
  1% low 76,658451 FPS. Eau : 143,796775 pas/s et ratio simulation/reel
  1,001562. Cinq couples de lectures depth/receiver valides, aux updates
  2154..2158 ; masques [0,2], [0,1], [0], [0,1], [0,2]. Les trois couches
  suivent B, les couches non dues gardent exactement leur profondeur.
  Selection terrain 58 -> 59 noeuds ; plantes [56,81,2,48,3] -> [50,41,0,19,0],
  ce qui exerce aussi les selections vides. Vent A/A identique ; A/B change
  [0,31108,5657] texels, sans revendiquer un changement de la couche proche.
  PNG final inspecte : maison, ponton, plage, vegetation et eau presents ;
  vue revenue au joueur. Cela ne qualifie pas l'absence de scintillement.
- `native-walk-shadow-30hz-001` : PASS sous limitation volontaire. Moyenne
  29,765264 FPS, p95 38,6647 ms, 1% low 20,624146 FPS ; eau 29,790722 pas/s
  et ratio simulation/reel 1,000385. 5681 ticks, dont 5676 au sol ; les cinq
  lectures des updates 452..456 passent aussi le suivi de cascades. Vent
  A/A identique, A/B change [0,31054,5642] texels. Cette qualification porte
  sur la scene et le poste mesures, pas sur toute l'ile.
- `native-cascades-bootstrap-001` : replay statique PASS, 7,22 s. PNG,
  hill shadow, profondeur objets et IDs plantes identiques octet par octet
  a `native-realtime-bootstrap-001`. Hashes respectifs :
  `243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`,
  `a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288`,
  `0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1`,
  `0ddd1e26fbe397b17f087916405486413942d53b47e86de1de316d44545ae344`.
  Picking : 47 IDs resolus, toujours sans occluders terrain/batiments.
  Aucun processus de build ou test conserve ; aucune publication Git.

## Limites

Soleil constant de la scene courante ; cycle jour/nuit et changements de soleil
en promenade restent a raccorder. Le port accepte deja une invalidation, mais
ce lot ne qualifie pas l'edition des casters en direct. Le parcours complet de
l'ile, les collisions des arbres, l'herbe hors des cellules initiales, le
temporel/post-traitement, l'edition Studio et l'execution WebGPU navigateur
restent ouverts.

Le graphe attend l'accuse d'encodage avant d'accepter la pose suivante. Cette
garde peut reduire la frequence des mises a jour d'ombres sous le rendu Bevy
pipeline, meme lorsque l'affichage reste a 144 FPS : 2162 soumissions au verdict
nominal. La cadence 1/2/4 est qualifiee par soumission, pas comme un refit proche
sur chaque image presentee. Le debit de soumission et la parite temporelle
avec la reference restent a qualifier et optimiser.
