# Cadence des ombres dans le rendu Bevy en parallele

Portee : promenade native Tidewater, reference figee `1438b1ab`.
Suite du [suivi spatial des cascades](2026-09-27-live-cascades.md).

## Correction

La preparation attendait l'encodage de la pose precedente. Avec le rendu Bevy
en parallele, cette condition sautait souvent une frame main sur deux, alors
que le renderer avait deja conserve sa copie immuable de la pose precedente.

`State` distingue maintenant extraction et encodage. Son implementation de
`ExtractResource` clone la pose partagee avant de publier l'acquittement
d'extraction. Le producteur peut alors preparer la pose suivante. Le compteur
d'encodage reste distinct et les tests de poses controlees attendent toujours
les lectures GPU. Le chemin historique `submit` protege aussi une pose qui
n'a pas encore ete extraite. Une nouvelle generation recoit ses propres
acquittements et statistiques.

Si une pose extraite n'a pas ete encodee, le graphe suivant detecte le saut
de sequence et refait les trois couches avec les matrices conservees du
dernier etat. Cette recuperation utilise le meme masque pour les uniforms
opaques/terrain/plantes et pour les passes de profondeur. Elle est comptee
separement ; le benchmark de cadence exige zero recuperation dans sa fenetre.

## Mesure

Un compteur coherent partage enregistre les soumissions effectivement encodees
et les passes par couche apres la boucle de profondeur. Le smoke mesure ce
compteur par intervalle de frames main, apres 60 soumissions et une seconde
reelle, jusqu'au debut des poses controlees du vent. Il exige au moins 120
frames et 100 soumissions, un ratio soumissions/frames de 0,95 a 1,05, et les
periodes source 1/2/4. Les bornes tolerent un decalage de lecture d'une frame.
Ces mesures ne sont pas des timestamps GPU ni des evenements de presentation.

Une trace GPU de 16 octets accompagne chaque generation : sequence, masque
des couches encodees, nombre de soumissions et recuperations. Les cinq poses
de navigation lisent profondeur, recepteurs et trace, avec des callbacks
rattaches a leur sequence. Le verdict exige les masques attendus, leur
progression, les matrices GPU correctes et les profondeurs non dues conservees.
La trace est ecrite par le graphe CPU et relue sur GPU ; elle ne mesure pas
la duree GPU des passes. Avant la capture finale, le controle revient a la
camera du joueur et attend quatre nouvelles soumissions effectivement encodees.

## Verifications

Preuves locales sous `E:/OpenRPG_Forge/.tidewater-integration/`.

- `shadow-rate-cpu-001.run.log` : 16 tests PASS, 0,24 s. Modules reels
  d'animation, cadence, vent, cascades et recepteurs dans un harness borne.
  Conservation de la pose extraite, separation des generations, recuperation,
  rejet de la demi-cadence, des compteurs decroissants et du rendu force des
  trois couches. Seule la ressource de declenchement du vent est simulee dans
  le harness de cadence ; aucune suite moteur exhaustive n'est revendiquee.
- `shadow-rate-clippy-003.log` : moteur et quatre exemples, `-D warnings`,
  PASS 44,76 s avant l'ultime renforcement du compteur de restauration.
- `shadow-rate-clippy-final.log` : candidat final PASS, 6 min 46 s incluant
  l'attente du verrou Cargo. Quatre exemples : plantes, population, ocean, spray.
- `shadow-rate-build-001.log` : build natif PASS, 10 min 43 s incluant attente.
  SHA-256 du binaire :
  `ac67c714e22935aae0f8418fc1d7fdaeb4af80a4cc7d9fe1f00bc07b606313e7`.
- `shadow-rate-studio-wasm-002.log` : Studio lib avec `--no-default-features
  --features web`, cible WASM, PASS 58,41 s. Avertissement preexistant
  proc-macro-error2 ; aucune execution navigateur. Le premier lancement sans
  selection de la feature web a ete annule et ne constitue pas une preuve.
- `native-walk-shadow-rate-001` : PASS, RX 7800 XT/Vulkan/AMD 26.8.1,
  1920x1080, sans compilation ni autre probe durant l'essai. 1843 ticks,
  dont 1838 au sol, 14,887203 m. Sur 3514 frames main : 3514 soumissions,
  passes [3514,1757,879], aucune recuperation. Ratios 1 / 1 / 0,5 / 0,250142.
  Cinq lectures GPU updates 3581..3585 : traces, recepteurs et profondeurs
  valides, masques 3/5/3/1/3. Vent A/A identique, A/B modifie [0,31075,5662]
  texels. Eau : 119,926451 pas/s, ratio temps simule/reel 1,002910.
  Capture finale inspectee : maison, ponton, plage, vegetation et eau presents,
  retour a la camera du joueur ; aucune qualification d'animation par PNG seul.
- `native-walk-shadow-rate-30hz-001` : PASS, 29,763122 FPS, p95 34,4919 ms,
  1% low 23,529289 FPS. 830 soumissions pour 830 frames, passes [830,415,208],
  aucune recuperation. Eau : 29,787266 pas/s et ratio temps simule/reel
  1,000580. 5677 ticks, dont 5672 au sol ; distance 14,887203 m. Navigation
  GPU et vent passent, A/B modifie [0,31082,5666] texels. Les smokes signalent
  un canal de readback ferme lors de l'arret, apres leur verdict PASS.
- `native-shadow-rate-bootstrap-001` : replay statique PASS, exit 0 en 7,09 s.
  PNG, hill shadow, profondeur objets et IDs plantes identiques octet par octet
  a `native-cascades-bootstrap-001` (comparaison SHA-256 sauvegardee dans
  `shadow-rate-static-comparison-001.json`). 47 IDs resolus, occluders toujours
  `NOT_IMPLEMENTED`. Aucun processus de test ou compilation laisse actif.

## Cout observe

Le nominal mesure 120,364829 FPS, p95 14,2204 ms et 1% low 34,877889 FPS.
L'essai precedent avec attente d'encodage mesurait 143,717852 FPS, p95 8,1586 ms
et 1% low 76,658451 FPS sur ce poste. La cadence d'ombres est corrigee, mais
le cout et les frames lentes augmentent dans cette comparaison. Le seuil
fonctionnel de 60 FPS passe ; retrouver l'ancienne performance reste ouvert.
Sans timestamps GPU ni profil CPU, cette comparaison n'isole pas les causes
de chaque frame lente et ne prouve pas les performances sur toute l'ile.

## Limites

La parite animee complete avec la reference, le parcours integral de l'ile, les
occluders de picking, l'edition Studio et l'execution WebGPU navigateur ne
sont pas acquis par cette correction. Les nuages conservent leur propre
ordonnancement, qui n'est pas modifie ici. Aucune publication Git.
