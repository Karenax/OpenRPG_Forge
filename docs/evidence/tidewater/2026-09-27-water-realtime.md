# Eau live : cadence d'affichage et reglage produit 1,25x

Ce rapport conserve la preuve du premier réglage à `1,25×`. Le réglage courant
est `1,5×` : voir [la qualification suivante](2026-09-27-water-speed-150.md).

Retour proprietaire du 27 septembre : mouvements de l'eau trop lents.
Portee : promenade native Tidewater. Vitesse reelle a 1,25x et non-regression
statique qualifiees localement ; integration complete et parite animee encore
ouvertes.

## Cause et correction

Le chemin live visait elapsed * 60, mais n'encodait qu'un pas couple par image.
Chaque pas ajoutait 1/60 s : a 30 images/s, l'eau avancait donc a demi-vitesse.
Le test precedent a observe 24,47 pas/s pour 24,10 images/s.

La reference figee `dgreenheck/tidewater@1438b1ab` utilise une transaction par
image, un delta reel borne a 0,1 s (`src/engine/Engine.js`) et un temps global
incremente de ce delta (`src/App.js`, `_frame`). La correction reprend cette
semantique pour FFT, rivage, emission et integration des embruns. Un triplet
atomique (index, temps f64, delta f32) reste disponible jusqu'a l'acquittement
du dernier consommateur. Une soumission en attente ne consomme pas le delta
suivant. Les 120 pas de l'acquisition fixe conservent leur chemin existant.

Le reglage produit final applique une seule multiplication par `1,25` au delta
reel, avant la borne : `sim_delta = min(raw_delta * 1.25, 0.1)`. Ce meme
`sim_delta` alimente a la fois l'increment du temps simule et `Frame.dt`. La
promenade active explicitement cette echelle ; l'horloge conserve `1,0` par
defaut pour les probes autonomes et le chemin d'acquisition fixe.

Le compteur GPU des embruns transporte maintenant son index ET son temps f64
dans un paquet de 16 octets. Chaque estampille utilise un buffer immutable ;
aucun horodatage n'est deduit de index/60. FFT, mousse FFT, rivage, mousse de
rivage, emission et integration des embruns, requetes de hauteur et materiaux
visibles recoivent le meme temps et le meme delta. Les trois materiaux d'eau
recoivent aussi le meme index, avec controle bit a bit dans le bilan.

En parallele, le vent visible utilise un unique buffer GPU stable, pour eviter
les reconstructions de materiaux par image. Voir [preuve vent](2026-09-27-live-wind.md).

## Protocole

Le verdict temps reel mesure secondes simulees / secondes reelles a partir
des horodatages relus sur le GPU. Les endpoints sont figes avant les captures
controlees A/A/B des ombres. Le debit de pas reste une mesure distincte : il
n'est plus suppose egal a 60. Le ratio observe est divise par l'echelle attendue
de `1,25`, puis doit rester dans [0,90 ; 1,05], en tenant compte de la latence de
lecture et des pauses bornees comme dans la reference.

Le smoke nominal exige aussi au moins 60 FPS moyens sur ce poste. Un second
mode de test, `FORGE_TIDEWATER_SMOKE_30HZ=1`, espace les frames avec une attente
du temps restant en fin de boucle. Il exige 20 a 35 FPS et le meme ratio temps
reel. Ce systeme n'est installe que dans les smokes ; Winit y reste continu
quel que soit le focus et conserve AutoVsync. Le jeu interactif garde la
politique Winit habituelle. A `1,25x`, la borne finale de 0,1 s commence a
reduire l'avance attendue lorsque le delta reel depasse 80 ms ; un gros stall
n'est jamais rattrape en boucle.

## Verifications acquises

Poste mesure : AMD Radeon RX 7800 XT, Vulkan, pilote AMD 26.8.1 (LLPC),
fenetre 1920x1080. Aucun compilateur ni autre probe GPU pendant les essais.

- Horloge pure Rust finale : 9/9 tests PASS
  (`water-time-scale-live-clock-tests-001.log`). Defaut `1,0`, reglage `1,25`
  a 30/60/144 Hz, borne finale a 0,1 s, refus des echelles invalides ou d'un
  changement pendant une transaction, conservation du delta en attente et
  pause/reprise sans saut. SHA-256 du log :
  `d19871cabaaf4857afa4a734820ace3ce8eb2eaa7f0346d49301d8a4cd27700c`.
- Check cible PASS en 29,65 s (`water-time-scale-check-001.log`, SHA-256
  `aa5ec934da15d391883d133717fb2344dc3c835b0243d10ded9f76763db4804d`).
  Clippy strict `--no-deps -D warnings` PASS en 45,22 s
  (`water-time-scale-clippy-001.log`, SHA-256
  `3458b69b6faf94c2804bc17c43ae8896bfec963feb05e0288a696eb1173830e0`).
- Buffer de vent : 3/3 tests PASS (`motion-tests-004.run.log`), portant sur
  les quatre branches source, le refus de derive et le buffer fixe de 48 octets.
- Clippy strict des exemples plante, population, ocean et spray PASS, 9,27 s
  (`live-delta-clippy-002.log`). Premier passage refuse pour l'adaptateur Resource
  Bevy 0.19 ; corrige par un wrapper derive, sans ajouter Bevy a l'horloge pure.
- Build natif final PASS, 3 min 41 s (`live-delta-build-001.log`), apres le build
  du buffer de vent (20 min 02 s). Binaire SHA-256
  `1bb91f33647a7b86ef89a4fa4974bcbea6a2ca03fb22e5628b79e1ef884b67e8`.
- Check Studio WASM avec moteur WebGPU PASS, 4 min 20 s incluant l'attente du
  verrou Cargo (`live-delta-studio-wasm-001.log`). Avertissement preexistant de
  compatibilite future dans proc-macro-error2 ; aucune execution navigateur.
- Essai GPU nominal final `native-walk-water-speed-125-001` PASS : echelle
  relue `1,25`, ratio temps simule / temps reel `1,2527549`, moyenne
  `142,58 FPS` et `142,63 pas/s`. Les controles fonctionnel et de cadence
  passent ; les readbacks FFT, rivage/embruns et requetes de hauteur sont finis
  et evolutifs, et les trois materiaux partagent index, temps et delta.
  SHA-256 : log
  `367057195bda8786b776e1e0e0c1f6a0d03fa2c687da7f21eafa09d58c0d8f89`,
  bilan `.walk.json`
  `de3c1842de2930c134bc4e196bc4a6efb41fa7f4b590fab2ff9acdddabff79b6`,
  capture PNG
  `5230b2385bac66b29d879041f8e8f12b50c18c43aa76b912b1c41f53d4072fa4`
  et requetes de hauteur
  `cbdbdda6de76d9d6a858adec98efec63cf0935ffe78bae2b2d1e65a26b2c309c`.
- Historique avant le reglage produit : essai GPU nominal
  `native-walk-realtime-001` PASS, 1809 ticks, dont 1804
  au sol ; 14,887203 m. Moyenne 143,353 FPS, p95 7,811 ms, 1% low 69,553 FPS.
  Debit 143,311 pas/s ; ratio temps simule / temps reel 1,003182. Mesure du
  feedback GPU entre temps simules 2,064084 et 31,877317 s, sur 29,718681 s
  reelles. Les trois materiaux partagent index/temps/delta ; champs FFT,
  rivage, spray et nuages finis et evolutifs. PNG final et captures vent A/B
  sauvegardes ; scene inspectee, batiment et ponton presents.
- Historique avant le reglage produit : essai volontairement ralenti
  `native-walk-realtime-30hz-001` PASS, moyenne
  29,765 FPS, p95 34,705 ms, 1% low 22,928 FPS ; 29,790 pas/s, mais ratio temps
  simule / temps reel 1,000388. L'eau conserve donc sa vitesse naturelle sous
  60 FPS. Les trois materiaux et les controles fonctionnels passent egalement.
  Le test attend aussi les readbacks des nuages et termine apres environ 105 s.
- Non-regression statique `native-realtime-bootstrap-001` PASS, exit 0 en 7,57 s.
  PNG, hill shadow et profondeur objets identiques octet par octet a
  `native-resident-plants-001`. SHA-256 respectifs :
  `243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`,
  `a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288`,
  `0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1`.
  Picking GPU : 47 IDs resolus, toujours sans occluders terrain/batiments.
  Aucun processus de test conserve, aucune publication Git.

Empreintes SHA-256 des trois sources du reglage final :

- `live_clock.rs` :
  `b3926f8685dcc4d41ea9070341a2e98f88dfd7e833e7aacc9d697e9559ff8e15` ;
- `walk.rs` :
  `651b4bad22075d010f656e3685f9a8066ab13a2dad78291de63c0246e7679e20` ;
- `walk_smoke.rs` :
  `625f125b9a159e03243f46b5559f6a0fdc5f1766ac8f7d94a2c430776c40beda`.

Ces controles ne prouvent ni la parite animee complete avec Tidewater, ni le
parcours de toute l'ile. Le reglage `1,25x` n'a pas ete execute sous WebGPU ni
dans le viewport Studio ; le controle WASM historique est une compilation,
pas une execution navigateur.
