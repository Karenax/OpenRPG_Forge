# Nuages continus dans la promenade native

Le graphe partage `tidewater_clouds` peut continuer apres ses 120 pas de reference.
Il conserve les dix textures, les pipelines et l'historique temporel. Le controleur
Rust repris depuis la meme acquisition garde son temps, ses offsets de vent, sa
camera precedente et son alternance de textures. La sequence initiale conserve
ses flags et sa validation stricte ; la continuation utilise les flags explicites
du controleur (ombre/panorama complets ou partiels, vue active, index d'historique).

Un seul frame attend l'encodage. `completed_steps` est publie apres encodage,
pas apres une fence GPU : plusieurs soumissions peuvent encore etre en file sur
la meme RenderQueue. Les uniforms par frame sont immuables. Le remplacement d'une
generation efface la frame en attente et reconstruit l'historique comme auparavant.
Le producteur doit attendre ses eventuels readbacks initiaux avant de continuer.
La promenade ne demande pas ces readbacks nuageux initiaux ; le probe de reference
reste separe et termine ses dix lectures sans activer la continuation.

L'adaptateur de promenade fournit la camera globale du joueur apres propagation
des transforms. Parametres publics et texture d'historique affichee sont publies
au meme PostUpdate que la frame de calcul, avant extraction. Le ciel, l'eau, les
embruns, le relief et les plantes utilisent ainsi les memes parametres nuageux.
Les textures de panorama et d'ombre restent residentes. Le temps suit les images
affichees, avec la borne dt de 0,1 s deja presente dans le controleur source.

## Eclairage et limites

Les helpers camera du bloc global WGSL ne sont pas atteignables depuis les points
d'entree cloud-shadow/trace/pano/temporal. Les kernels utilisent la camera locale
`scf`. Les globals effectivement lus sont sunDir, sunColor, night, skyIrradiance ;
ils restent ceux du pas 120, coherents avec les LUT atmospheriques fixes. La suite
du cycle jour/nuit doit mettre a jour ces donnees ensemble. Resolution de calcul
240x135, historique 960x540 et sortie de preuve 1920x1080 restent fixes.

Pas de qualification de nage/retour sous l'eau, redimensionnement, teleportation,
changement de generation pendant le jeu, cycle solaire, Studio interactif ou
parite animee upstream dans ce lot. Les flags underwater sont conserves dans le
graphe partage, mais l'adaptateur de promenade reste au-dessus de l'eau.
Le vent visible des plantes et leurs casters sont encore figes ; leur prochaine
integration doit partager temps et gust entre materiau visible et profondeur.

## Verification

Clippy strict cible plant/cloud PASS (`cloud-live-clippy-002.log`, 6,74 s).
Le premier check a trouve un conflit d'emprunt dans le collecteur de preuve GPU ;
corrige avant execution. Le premier Clippy a demande de regrouper les parametres
du systeme de preuve et d'extraire le controle de deplacement de camera.

Build initial PASS 10 min 52 s (`cloud-live-build-001.log`). Le probe
`native-cloud-live-bootstrap-001` termine avec 120 pas / 120 soumissions et ses
dix textures sont identiques a l'octet a `native-shared-clouds-001` :
`cloud-live-bootstrap-comparison-001.json`.

Premier parcours `native-walk-clouds-001` : ECHEC conserve. Les trois textures
cloud shadow/color0/panorama changent entre les pas observes 151 et 181, toutes
finies. Le controleur atteint 212 frames live, temps 19,394 s, camera deplacee
14,9 m et une invalidation initiale. Mais la marche a termine avant la fin du
bootstrap eau/embruns : les positions camera envoyees aux emissions 121 et 183
sont identiques. La mesure courte est 24,715 FPS (41 intervalles seulement),
cadence simulation 23,167 pas/s. Cela ne valide pas la fluidite attendue.

Corrections apres diagnostic : la disponibilite du joueur attend maintenant
les readbacks initiaux ocean/embruns et les nuages ; la continuation cloud attend
cette meme disponibilite. La publication des parametres nuageux utilise une
inspection immutable des materiaux et ne modifie que les valeurs changees :
`Assets::iter_mut()` marquait auparavant tous les assets Modified a chaque frame,
meme lorsqu'aucun parametre ne changeait. La texture de vue du ciel continue bien
d'alterner a chaque frame. Aucun budget de raymarch/resolution n'a ete reduit.
Clippy strict cible apres corrections PASS (`cloud-live-clippy-003.log`, 46,25 s).

Tests CPU : 7/7 PASS, 0,19 s. Harness `rustc --test` hors depot chargeant directement
les modules source reels : trois tests du controleur, deux de continuation
d'acquisition et deux de validation/attente d'encodage. Les assertions couvrent
camera/coupure/immersion/invalidation, bornage dt, equivalence du pas 120, pas 121
et ping-pong, refus d'acquisition vide, refus sans mutation et attente du pas
precedent. Journal `cloud-live-cpu-tests-001.log` ; source temporaire du meme nom
en `.rs`. Ce n'est pas un lancement de toute la suite Cargo. Les derives exigent
CARGO_MANIFEST_DIR et le lien Windows exige les deux chemins natifs normalement
fournis par Cargo ; ces parametres ont ete repris de la compilation parent sans
changer de configuration ni les assertions. Les echecs preliminaires de ce
harness sont conserves dans le journal, execution finale exit 0.

## Verification native finale

Build corrige PASS 10 min 04 s (`cloud-live-build-002.log`). Le probe initial
rejoue avec ce binaire, `native-cloud-live-bootstrap-002`, donne encore 120 pas /
120 soumissions et les dix sorties identiques a l'octet a la baseline partagee
(`cloud-live-bootstrap-comparison-002.json`).

Commande de promenade executee, exit 0 :

```powershell
$env:RUST_LOG='warn,bevy_render::renderer=info'
./scripts/tidewater/Walk-Tidewater.ps1 -SkipBuild -LongSmoke -CapturePath E:/OpenRPG_Forge/.tidewater-integration/native-walk-clouds-002.png
```

Le log identifie l'adaptateur effectivement utilise : AMD Radeon RX 7800 XT,
Vulkan, pilote AMD proprietary driver 26.8.1 (LLPC). Aucun compilateur ni autre
test GPU du present travail n'etait actif pendant cette mesure.

Resultats `native-walk-clouds-002.walk.json` :

- 1800 ticks physiques, 1795 au sol (mise au sol initiale), distance 14,887203 m ;
- camera et backend stables ; camera cloud et camera d'emission suivent la marche ;
- 2133 frames cloud live, dernier pas soumis 2253, temps 31,933006 s,
  une invalidation initiale de l'historique ;
- lectures GPU shadow/color0/panorama aux pas observes 1501/1531 : trois hashes
  changes, toutes les valeurs finies ; ces numeros sont observes cote CPU lors
  de la reception, pas des timestamps GPU embarques dans les textures ;
- FFT/rivage/particules changent aussi ; rivage et positions finis ;
- simulation eau : 1794 pas live, cadence observee des tags GPU 60,087720 pas/s,
  trois materiaux synchronises au pas 1914 ;
- 4118 intervalles d'affichage : moyenne 142,784752 FPS, p95 8,365 ms,
  1 % low 67,594840 FPS ;
- duree externe 40,212226 s, initialisation et capture incluses.

La moyenne retrouve l'ordre de grandeur de la promenade precedente avec nuages
figes (143,887 FPS). Le premier essai a 24,715 FPS et ce dernier ont des durees
et gates de demarrage differentes : ce n'est pas un benchmark A/B isole de chaque
correction. Les changements evitent cependant les invalidations de materiaux
inutiles et le demarrage premature sans baisser la resolution ou le raymarch.
La mesure finale reste une marche de 14,9 m puis une camera stationnaire,
FHD native en dev optimise avec symboles, pas un benchmark de toute l'ile ni des
timers GPU. Le 1 % low demande encore un suivi sur navigation et rotations longues.

Capture finale inspectee : plage, ponton, maison, ocean, relief et plantes
visibles ; ciel clair avec un petit nuage visible dans ce cadrage. La capsule
bleue est toujours le personnage de test. Pas de verdict de parite artistique.
Le warning de fermeture `Failed to send readback result: sending into a closed
channel` est encore present apres PASS ; aucune erreur de validation GPU observee.

Executable SHA-256 :
3886a272d04ec912edb0974d1f447b00d2768534dd89c494774991e95d02577d.
PNG SHA-256 :
cb02f4adfb72ef73d2a4ee0990efc2570878fbafd263d3842f2d9d32ce3219c1.
Le lanceur direct utilise cette version. Les processus de test sont termines.

Compilation WASM du moteur partage PASS (`cloud-wind-wasm-001.log`, 23,39 s),
avec les premiers ajouts de ressources residentes pour le prochain lot vent.
La premiere commande a ete interrompue sans verdict (handle disparu, aucun
processus Cargo restant) ; la reprise a trouve un double emprunt dans le nouveau
code de casters vegetaux, corrige avant ce PASS. Aucune execution navigateur
de cette continuation revendiquee. Aucun commit ni publication.
