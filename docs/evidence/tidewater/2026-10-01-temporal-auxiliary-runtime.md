# Tidewater — entrées temporelles réelles et raccordement HDR

Reprise du [rapport d’exposition Studio](2026-09-30-studio-exposure-sweep.md),
après la [réfraction native 215–217](2026-10-01-native-refraction-capture.md).
L’objectif complet reste actif. La calibration Studio 096 et les preuves du
candidat 215 conservent leur portée ; les nouveaux fichiers ne les requalifient pas.

## Premier candidat réel 219

Le module `forge_engine::tidewater_temporal` est maintenant exporté dans le moteur.
L’installation reste inactive jusqu’à sélection explicite d’une caméra. Il utilise
les vraies géométries, déformations et liaisons de matériaux Bevy, un paquet de
caméra de 336 octets, cinq textures distinctes et une lecture GPU indépendante
de la profondeur principale. Le candidat natif désactive explicitement le rendu
pipelined ; la propriété des historiques dans le renderer de production reste
à terminer. Le module pur de caméra 187 est également exporté et testé dans le
vrai moteur.

Le [build 219](../../../../.tidewater-integration/temporal-aux-native-build-219/library.0.json)
compile le moteur courant avec les dépendances natives et macros déjà épinglées,
vérifiées puis copiées sur le SSD physique. Les reçus contrôlent les empreintes
avant et après compilation. `indexmap = 2.14.1` devient une dépendance directe
pour la signature publique de la phase triée Bevy ; cette version était déjà
présente dans le verrou et dans la pile Bevy. Aucun changement de version du
graphe de dépendances n’est associé à cette déclaration.

Résultats bornés du candidat 219 :

- métadonnées et bibliothèque natives compilées ;
- 18 tests temporels CPU réussis, un test d’import explicitement ignoré dans ce
  passage ; le reste des tests du moteur n’a pas été exécuté ;
- test d’import exécuté séparément : **14 variantes / 28 modules** avec les vrais
  imports Bevy et Naga ;
- première compilation de l’exemple échouée sur le chemin du nouveau sous-module
  (`E0583`), puis chemin explicite corrigé et `example.1` compilé ;
- aucune parité visuelle, qualification WebGPU de ce nouveau module ou distribution
  déduite de ces compilations.

## Essai GPU 220 — échec conservé

Le [reçu natif](../../../../.tidewater-integration/temporal-aux-native-runtime-220/run.json)
et le [diagnostic](../../../../.tidewater-integration/temporal-aux-native-runtime-220/probe.aux.json)
conservent l’échec après 180 secondes. Le binaire est
`a4648662e38decf5a0c5a19b6a9b80ca5d108e13b674b1ae0060421f6d950d6d`.
La source de scène est restée intacte et le processus de preuve a été fermé.

Aucune image auxiliaire n’a été acquittée et aucun readback n’a été capturé :
18 993 meshes visibles hors ciel, 18 991 mis en file, deux non pris en charge,
un en attente et 18 990 commandes `DrawMesh` réussies. Le shader d’eau installé
par la chaîne native de réfraction ne possédait plus le retour littéral attendu
par la dérivation. Ces nombres bloquent le verdict : aucun objet n’est exclu
pour obtenir artificiellement une couverture complète.

La correction reconnaît les deux variantes précises du retour Ocean : la
variante source et la variante native qui conserve le masque d’eau dans l’alpha
de l’image principale. Elle conserve le calcul réel `shadeWater`, la géométrie
et les discards. Le
[reçu CPU de la correction](../../../../.tidewater-integration/temporal-auxiliary-shader-cpu-native-mask-fix/receipt.json)
valide cinq tests, puis 15 variantes / 30 modules composés Bevy/Naga.
Ce reçu utilise les véritables fonctions des adaptateurs natifs ; il ne prouve
pas leur exécution GPU dans le candidat suivant.

Une prise en charge déclarative du matériau standard opaque rigide et des
diagnostics bornés par identité de mesh, matériau et commande sont ajoutées.
Leur qualification GPU et l’identification du dessin manquant restent en cours.
Les matériaux skinned, morphés ou hors contrat demeurent explicitement refusés.

## Resolve source 221 — contrôle CPU séparé

Le [port isolé et son contrat](../../../../.tidewater-integration/source-temporal-resolve-port-221/SEAM.md)
conserve l’algorithme figé à `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Trois shaders passent Naga, les ABI 336/32 octets sont contrôlées et huit
comparaisons algébriques emploient la véritable Matrix4 source. Les adaptations
documentées portent sur la profondeur inverse infinie Bevy et les références
au jitter. Cela ne constitue pas une comparaison GPU en virgule flottante.

Le raccordement doit conserver deux historiques HDR, une profondeur précédente
initialisée à zéro, le far précédent et la caméra précédente après resize. Un
acquittement de resolve distinct doit avancer la phase Halton, après le dessin
et ses copies réels. Un acquittement auxiliaire ne peut pas faire avancer cette
instance. Le candidat initial reste à l’échelle 1 et sans rendu pipelined.

L’ordre courant des effets exige aussi une branche explicite pour la caméra
sélectionnée : GTAO, haze et underwater dans le HDR, puis resolve avant bloom
et tonemapping. Le haze historique applique déjà ACES à sa contribution et
un multiplicateur d’exposition ; déplacer simplement ce shader ne restituerait
pas une contribution HDR. Le raccordement de cette branche et son contrôle GPU
restent en cours. Les autres caméras conservent leur chaîne existante.

La parité complète avec la référence, l’historique des objets édités, le renderer
asynchrone Studio et le paquet partageable restent ouverts. Les preuves nouvelles
doivent porter leurs propres tags, empreintes et comparaisons ; aucune preuve
CPU ne remplace un readback ou une acceptation visuelle.

## Candidat 222 et essai 223

Le candidat 222 compile le vrai moteur, ses tests et l’exemple natif sans dérive
des sources pendant ces compilations. Les 28 tests temporels réussissent ; le
test d’import est ensuite exécuté explicitement et réussit également. La tranche
standard possède aussi son [contrôle séparé 224](../../../../.tidewater-integration/temporal-standard-shader-cpu-224/receipt.json) :
24 compositions réelles ShaderCache/Naga et les modèles courant/précédent Bevy.
Ces résultats restent CPU. Une nouvelle alerte de visibilité Rust sur le type
privé `Jobs` apparaît dans 222 ; sa correction de portée est préparée pour le
candidat suivant, sans modifier rétrospectivement ce build.

L’[essai 223](../../../../.tidewater-integration/temporal-aux-native-runtime-223/run.json)
conserve son échec et ses [diagnostics par matériau](../../../../.tidewater-integration/temporal-aux-native-runtime-223/probe.aux.json).
Les shaders ne sont plus en attente et les 18 991 commandes mises en file sont
toutes réellement dessinées. Deux matériaux demeurent hors contrat :
`TypeId(0x5e7ddfe38c7d31430a2f3c3474a1525e)` et
`TypeId(0xf9919c7c2f099718ffdbdac865488dbc)`. La crête transparente et les embruns
possèdent chacun un matériau natif encore absent des registrations 222. Leur
raccordement spécifique aux shaders réels est en cours, sans exclusion de ces
objets. Aucun readback numérique n’a encore pu être capturé.

## Candidat 222.1 et captures GPU 228

La crête des vagues et les embruns ont maintenant des registrations explicites
de leurs véritables matériaux transparents. Leurs dérivations conservent la
géométrie actuelle, le calcul d’alpha et les discards source. La vitesse statique
source projette la même position actuelle dans les deux caméras ; elle ne
remplace pas cette position par une animation évaluée au temps précédent.
L’alpha de masque nul conserve le masque d’eau situé derrière ces couches.
La correction de visibilité Rust de `Jobs` est incluse dans ce nouveau candidat.

Les reçus `check.1.json`, `library.1.json`, `tests.1.json` et `example.1.json` dans
`temporal-aux-native-build-222` conservent les sources et les arguments exacts :
pas de dérive des sources pendant les compilations ; **29 tests temporels CPU
PASS**, puis le test d’import source lancé explicitement dans
`source-imports-1-explicit.log` (**1 PASS**). Le premier filtre enregistré dans
`source-imports-1.log` ne sélectionnait aucun test : il ne constitue pas une
validation. Les autres 473 tests du moteur ne sont pas qualifiés par ce passage.

L’[essai 228](../../../../.tidewater-integration/temporal-aux-native-runtime-228/run.json)
utilise le binaire `76edd8b08cf69b323a36f1ad8c7ae83b6ece2661acbc7ea4c377bd6dc485f0d6`.
Il termine normalement en 10,76 secondes, avec le processus fermé et la fixture
intacte. Tous les **18 993 meshes visibles** hors ciel sont mis en file et
réellement dessinés, **zéro pending / unsupported**, 47 acquittements auxiliaires.
Les deux captures statique et caméra translatée contiennent chacune les six
plans GPU après leurs callbacks. L’image PNG est un contexte d’une autre frame,
pas une preuve couleur synchronisée avec ces plans.

Le premier [audit 220 inchangé](../../../../.tidewater-integration/temporal-aux-native-runtime-228/audit-220.json)
échoue sur le type de `view_bits` : la sortie native le sérialise en chaîne
décimale afin de préserver exactement les 64 bits. Le nouvel
[auditeur 229](../../../../.tidewater-integration/verify-temporal-aux-229.py)
accepte uniquement les entiers ou chaînes décimales ASCII canoniques dans
`0..u64::MAX` ; ses six tests de schéma réussissent. Les oracles et leurs seuils
numériques restent identiques au 220 ; original, captures et échec sont conservés.

L’[audit numérique 229](../../../../.tidewater-integration/temporal-aux-native-runtime-228/audit-229.json)
donne les résultats suivants :

- statique : tous les contrôles passent ;
- les deux cas : plans finis, profondeur dans `[0,1]`, profondeur auxiliaire et
  finale égales bit à bit à la profondeur principale, masque cohérent et vitesse
  finie avec Z nul et alpha unité ;
- caméra translatée : seul l’oracle de vitesse de l’eau échoue, sur
  **2 300 / 476 757 pixels** ; erreur maximale `0.0002000994 UV`, tolérance
  maximale conservée `0.0000039455 UV`.

L’oracle reconstruit la position d’eau depuis sa profondeur alors que la vitesse
finale peut avoir reçu plusieurs blends de crêtes ou d’embruns à une autre
profondeur. Le masque d’eau reste derrière eux et la profondeur n’est pas écrite
par ces couches. Cette hypothèse explique une limite de l’oracle ; l’attribution
des 2 300 pixels n’est **pas encore prouvée**. Aucun pixel, matériau ou seuil n’est
écarté pour obtenir un PASS. Le verdict global auxiliaire reste donc FAIL.

## Essai source resolve 230 — refus de caméra conservé

L’[essai 230](../../../../.tidewater-integration/temporal-resolve-native-runtime-230/run.json)
avec le même binaire conserve son échec après 184,96 secondes, fixture intacte
et processus fermé. L’auxiliaire dessine la scène complète mais `activate`
retourne `UnsupportedCamera`, sans acquittement de resolve ni capture HDR.

Le constructeur natif utilise `near=0.06` avec `..PerspectiveProjection::default()`.
Le plan standard conserve alors `w=-0.1`. Bevy 0.19.1 ignore explicitement `w`
quand `x=y=0` et `z=-1`, et produit la même matrice de projection standard ; le
nouveau guard demandait à tort `w=-near`. La correction doit suivre ce contrat
réel sans modifier la pose ou la projection de référence. Elle et l’observateur
à sept plans (entrées avant resolve, sortie avant les copies de profondeur/HDR)
restent à compiler et à exécuter dans un candidat suivant.

L’objectif complet reste actif. Ni ces readbacks auxiliaires ni les tests CPU
ne qualifient la couleur du resolve, la parité visuelle complète, la propriété
asynchrone des historiques Studio, les nouvelles passes WebGPU ou le paquet
distribuable.

## Candidat 232 et essai source resolve 233

L’observateur à sept plans est installé explicitement et reste inactif sans
demande. Les six entrées sont copiées après l’éventuel seed et avant le dessin
de resolve ; la sortie est copiée après ce dessin et avant les copies de
profondeur/HDR. Les mêmes tag, epoch, générations et uniforms encase 336/32 octets
sont figés dans le snapshot. Seuls sept callbacks distincts réussis et deux
hooks appariés rendent ce snapshot exportable. Le probe prépare les contrôles
statique, caméra translatée et premier seed après un reset explicite. Ce dernier
reset est effectué après l’acquittement précédent et avant la préparation,
puis sa demande est armée avant le rendu de la nouvelle epoch.

Les quatre compilations du
[candidat 232](../../../../.tidewater-integration/temporal-resolve-native-build-232/library.0.json)
réussissent sans dérive des sources : **38 tests temporels CPU PASS**, puis
**1 test d’import source PASS** exécuté explicitement. Aucun autre test moteur
n’est déduit de ces résultats. L’auditeur couleur indépendant
[231](../../../../.tidewater-integration/temporal-resolve-numeric-audit-231/PLAN.md)
possède **27 tests CPU PASS** et consomme les vrais sept plans sans exécuter le
shader moteur. Son seuil initial d’une valeur float16 adjacente reste un contrat
strict d’ingénierie, pas une borne universelle démontrée de précision GPU.

L’[essai 233](../../../../.tidewater-integration/temporal-resolve-native-runtime-233/run.json)
conserve son échec à la deadline de 180 secondes. Le guard caméra corrigé accepte
la projection native ; les 18 993 meshes sont toujours réellement dessinés.
Le diagnostic correspondant au même tag `3996` constate les cinq dessins GTAO
et le dessin haze HDR, mais zéro dessin underwater. `IncompleteHdrChain`
interdit donc correctement le resolve : zéro acquittement et zéro readback
couleur. Aucun PASS GPU de l’observateur ou de l’auditeur 231 n’est revendiqué.
Le contrat de la contribution underwater neutre au-dessus de l’eau reste à
vérifier et raccorder, sans transformer une ressource manquante ou un pipeline
en attente en contribution fictivement valide.

## Raccord natif réel et capture source resolve 236

La cause de 233 est propre à l’adaptateur statique : il n’installe ni le plugin
Underwater ni son marqueur caméra. L’adaptateur réutilise maintenant
`walk::underwater_plugin` avec les vrais paramètres optiques, les cartes
caustiques et le shader de surface. Le premier essai
[235](../../../../.tidewater-integration/temporal-resolve-native-runtime-235/run.json)
conserve son panic sur l’absence du resource de requêtes de surface, après
3,08 secondes. Il n’est pas effacé. Le raccord suivant installe explicitement
`spray::enable_water_queries` quand ce resource manque, sans activer le calcul
live ou modifier les 120 étapes figées. La query caméra0 suit la transformation
sélectionnée avant la mise à jour Underwater. L’absence des données requises
retourne désormais une erreur explicite plutôt qu’un panic dans la construction
commune du plugin.

Le [candidat natif `232/example.2`](../../../../.tidewater-integration/temporal-resolve-native-build-232/example.2.json)
compile avec la bibliothèque 232.0 inchangée. Ce nouveau reçu contrôle **331
fichiers source**, comprenant tous les `.rs`/`.wgsl` des examples, y compris les
modules partagés hors du dossier `tidewater_plant_probe` ; zéro dérive constatée.
Les anciens reçus conservent leur énumération initiale et ne sont pas élargis
rétrospectivement. Le binaire est
`6a4a7ed0da7fb7704e0354ac46a316b0aaa9245aa16ec553351c312ae5c44b0d`.

L’[essai 236](../../../../.tidewater-integration/temporal-resolve-native-runtime-236/run.json)
réussit en 17,79 secondes, fixture intacte et processus fermé : **49 resolves
acquittés, zéro rejet**, aucun acquittement auxiliaire n’avançant cette instance.
Les cinq dessins GTAO, le dessin haze HDR et le véritable dessin Underwater
précèdent le resolve avec le même tag. Le probe exporte **trois snapshots de sept
plans** et deux snapshots auxiliaires de six plans, tous après callbacks :

| Cas | Tag | Epoch | Génération resolve / auxiliaire | Frames encodées avant | Seed / clear0 |
| --- | ---: | ---: | ---: | ---: | --- |
| statique | 66 | 0 | 1 / 1 | 41 | non / non |
| caméra translatée | 68 | 0 | 1 / 1 | 43 | non / non |
| premier rendu après reset | 70 | 1 | 2 / 2 | 0 | oui / oui |

Cela qualifie l’acquisition et l’ordre de ces commandes/callbacks natifs, pas
leur identité visuelle avec l’application source. Le seed bilinéaire de la
troisième capture correspond **bit à bit** à Beauty à l’échelle1 ; la profondeur
précédente clear0 est également contrôlée par l’auditeur indépendant.

L’[audit couleur 231 inchangé](../../../../.tidewater-integration/temporal-resolve-native-runtime-236/numeric-audit-231/audit.json)
reste **FAIL** dans les trois cas. Aucun NaN/Inf sélectionné n’est masqué, aucun
pixel n’est exclu, et le seuil d’une valeur float16 adjacente n’est pas élargi :

| Cas | Pixels au-delà d’une valeur float16 adjacente | Maximum float16 ULP | Écart absolu maximal HDR |
| --- | ---: | ---: | ---: |
| statique | 1 214 / 2 073 600 | 24 | 0.00390625 |
| caméra translatée | 42 336 / 2 073 600 | 193 | 0.07177734 |
| premier rendu après reset | 2 742 / 2 073 600 | 190 | 0.02319336 |

La distinction entre divergence d’algorithme, précision de filtrage GPU et
modèle arithmétique CPU reste à établir. Le PNG contextualise une autre frame ;
il ne valide pas ces différences. L’[audit auxiliaire 229](../../../../.tidewater-integration/temporal-resolve-native-runtime-236/aux-audit-229.json)
conserve aussi son seul échec de vitesse d’eau après translation, désormais
2 935 / 476 758 pixels ; statique et autres contrôles restent PASS. Un readback
distinct avant les couches transparentes est préparé pour isoler cet oracle,
sans requalifier le composite final ou omettre les crêtes et embruns.

## Diagnostic numérique indépendant 237 et observer avant transparence

Le [diagnostic 237](../../../../.tidewater-integration/temporal-resolve-numeric-diagnostic-237/REPORT.md)
recalcule les verdicts 231 sans modifier leurs entrées ou seuils. Il reproduit
exactement les quatre canaux des trois pires pixels avec des hypothèses
arithmétiques distinctes : inverse float32 de 9 et conversion half vers zéro
pour la variance statique ; autre tap dont la profondeur convertie est égale,
filtrage bilinéaire quantifié et conversion half pour le mouvement ; changement
de branche `canLock` à moins d'une ULP float32 du seuil après reset. Ces
contre-exemples démontrent la sensibilité du modèle CPU, pas les instructions
effectivement exécutées par le GPU. Les variantes appliquées à toute l'image
échouent encore. **Les trois FAIL 231 restent inchangés.** Une observation GPU
des premiers intermédiaires divergents reste nécessaire.

L'[intégration 237 de l'observer de base](../../../../.tidewater-integration/temporal-base-readback-engine-integration-237/integration.json)
ajoute un observer opt-in : trois copies des MRT auxiliaires après les opaques,
le ciel et l'eau, puis trois copies des mêmes textures après toutes les
transparences. Les six callbacks, le packet réellement lié de 336 octets et les
compteurs d'encodage sont appariés par tag/génération/epoch. Les requêtes restent
inactives sans inscription et demande explicites. **13 tests CPU isolés PASS**,
typecheck Bevy et Clippy ciblé PASS ; cela ne qualifie pas encore le runtime
GPU ni la vitesse d'eau avant les transparences.

Les trois premiers builds complets
[239](../../../../.tidewater-integration/temporal-base-native-build-239/check.0.json)
conservent leur échec : le nouveau module gameplay du worktree réclame un extern
`forge_gameplay` absent du builder direct, et trois accès à `Velocity.linvel` ou
`Velocity.angvel` ne correspondent pas à l'API Rapier liée (`linear`, `angular`).
Le correctif ciblé ne change que ces trois accès et retire l'import inutilisé
`EntityId` ; le fichier antérieur est conservé dans le dossier 239. Aucun verdict
gameplay n'est déduit de cette correction de compilation. Le nouvel adaptateur
natif prépare deux captures `BaseAndFinal` avec les captures auxiliaires normales
des mêmes frames. L'audit de base reste distinct du verdict composite final.

## Capture de base 240 et qualification 244 — suite du 2 octobre

Les tentatives 239.1 et 239.2 restent FAIL : respectivement un binding Bevy
`Mut<StandardMaterial>` déclaré sans `mut`, puis une dépendance gameplay
antérieure aux nouveaux champs `rotation_xyzw`. Le premier correctif ajoute
uniquement `mut` ; le second recompile les vraies sources gameplay dans
[241/library.2](../../../../.tidewater-integration/forge-gameplay-native-dependency-241/library.2.json)
avec les mêmes dépendances natives vérifiées. Aucune suppression du module,
substitution de types ou modification des captures précédentes.

Le candidat [239.3](../../../../.tidewater-integration/temporal-base-native-build-239/library.3.json)
passe Check/Library/Tests : **46 tests temporels CPU PASS**, un test source
ignoré exécuté explicitement **PASS**, aucun autre test moteur revendiqué.
Zéro dérive et journaux de compilation vides. Le
[programme natif 239/example.0](../../../../.tidewater-integration/temporal-base-native-build-239/example.0.json)
passe également, 332 fichiers contrôlés, zéro dérive. Son SHA256 est
`5c2fbf1e3568162434c58c91e5aa8381337574d60ee3ae869b253dba477483c5`.

La [capture GPU 240](../../../../.tidewater-integration/temporal-base-native-runtime-240/run.json)
réussit en 20,88 secondes, processus fermé, fixture intacte : 47 acquittements
auxiliaires, zéro rejet, aucune erreur de shader/encodage. Resolve désactivé
explicitement sur cet essai. Deux snapshots de six plans avant/après late sont
appariés aux deux snapshots auxiliaires normaux : tags46/48, epoch0,
génération1. À chaque frontière, **18 985 opaques +1 eau =18 986 dessins de
base**, puis **7 transparents =18 993 dessins complets**, avec un dessin ciel.
Les six callbacks de chaque snapshot de base sont terminés avant export.

Le [premier audit réel 242](../../../../.tidewater-integration/temporal-base-native-runtime-240/base-audit-242.json)
reste FAIL de schéma : le lecteur exigeait `padded_path` sur les plans normaux,
dont le contrat historique 229 exporte le compact avec longueur/SHA/stride,
mais aucun fichier padded. Les six plans Base/Final possèdent bien leurs deux
fichiers et SHA. Le nouvel [auditeur 244](../../../../.tidewater-integration/temporal-base-numeric-audit-244/PLAN.md)
utilise le lecteur 229 inchangé pour les normales et conserve les validations
padded complètes pour Base/Final. **28 tests CPU PASS**, dont le schéma normal
sans `padded_path` et les refus des fichiers/SHA manquants. Aucun input240 ni
verdict242 n'est réécrit, aucun oracle ou seuil modifié.

L'[audit réel 244](../../../../.tidewater-integration/temporal-base-native-runtime-240/base-audit-244.json)
est **PASS pour l'étage eau avant transparences uniquement** :

| Cas | Pixels d'eau tous testés | Écart avant late | Écart final 229 | Pixels XY modifiés par late | Écarts hors pixels modifiés |
| --- | ---: | ---: | ---: | ---: | ---: |
| statique |474 990|0|0|0|0|
| caméra translatée |476 757|0|2 300|7 895|0|

Zéro exclusion grazing/homogeneous-W/nonfinite. En déplacement, l'erreur maximale
de base vaut `3.8645068736e-6` UV sous le seuil original pixel par pixel. Les
trois textures finales sont identiques bit à bit aux lectures normales des mêmes
tags. Profondeur et masque restent identiques avant/après late. Le packet336
lié, les formats, tailles, IDs, hashes, dépaddings et compteurs passent aussi.
Les 2 300 écarts du modèle eau final sont tous spatialement contenus dans les
pixels où les transparences ont changé XY. Cette attribution ne démontre pas
les poids, l'alpha ou la fidélité source du mélange late : **le verdict final229
reste FAIL**, distinct du PASS de base. Les trois FAIL couleur231 restent aussi
inchangés. Le PNG est une autre frame de contexte, pas une comparaison source.

Le [gel 245](../../../../.tidewater-integration/temporal-base-qualified-inputs-245/manifest.json)
fixe 240 artefacts et 331 copies source du candidat, SHA256
`42086e129aab45fe54fe851973d63af5cc74f735be45216e035b2e3f1fb55c2e`.
Une modification postérieure de `gameplay/mod.rs` est consignée séparément ; elle
ne requalifie pas le binaire capturé. La calibration096, la parité visuelle
complète, le mélange late, les tailles/scales nouvelles, Studio/WASM et la
distribution restent hors de cette preuve. L'objectif complet reste actif.

## Sonde de calcul 246 et compilation complète 247 — suite du 2 octobre

Le [prototype 243](../../../../.tidewater-integration/temporal-resolve-trace-design-243/README.md)
est raccordé comme sonde explicitement optionnelle dans
[246](../../../../.tidewater-integration/trace-engine-integration-246/README.md).
Après le véritable draw source, avant les copies HDR/profondeur, un shader
instrumenté utilise ses six textures et ses deux groupes réellement liés pour
écrire une texture privée RGBA16F et huit enregistrements float32. La sortie
source est le septième input, en lecture seule ; aucun historique ou framebuffer
visible n'est écrit par la sonde. Le parcours normal ne l'installe pas.

Les tests isolés de 246 passent : API Bevy réelle, 19 tests CPU (11 nouveaux et
8 existants), validation Naga et ABI 336/32/144. Ces contrôles n'exécutent pas
le plugin Core3d complet. Le premier Check/Library/Tests247.0 conserve donc son
**FAIL réel de visibilité** : `resolve::initialize` est visible au module frère,
mais son type de paramètre `Shaders` ne l'est pas. Le
[correctif 250](../../../../.tidewater-integration/trace-engine-visibility-fix-250/fix-250.json)
ajoute uniquement `pub(super)` à cette déclaration ; backup, diff et hashes
préservent le gel246. Aucun résultat GPU n'est déduit de ce correctif.

L'adaptateur natif prépare trois cas appariés, statique, déplacement de caméra
et réinitialisation réelle : sept callbacks source et deux callbacks privés
par cas. Il attend tous les callbacks précédents avant une nouvelle demande,
et conserve les captures si les sorties diffèrent. L'égalité de tous les bytes
scratch/source sera un verdict distinct du transport et du contrôle couleur231.
Même une égalité complète ne prouverait pas les instructions ni les valeurs
intermédiaires float32 du shader original : les enregistrements décrivent
uniquement la reproduction instrumentée. Les huit sites fixes ne limitent
jamais les audits source ou scratch de l'image entière.

Check/Library/Tests247.1 passent sans warning et sans dérive : **57 tests
temporels CPU PASS**, plus le test ignoré d'import shader exécuté explicitement
**PASS** (530 autres tests filtrés). Le premier lancement explicite conserve
son FAIL de variable d'environnement erronée ; le deuxième emploie
`FORGE_TIDEWATER_SHADER_FIXTURE`, sans modifier le test. Example247.0 conserve
un FAIL de parsing `serde_json::json!` : le tableau inline suivi de `.map()`
est déplacé vers une variable locale de noms de rôles, sans modifier les valeurs.
Example247.1 passe sans warning ni dérive ; SHA256 du binaire
`42ed1cf81874811f0010d486346b5bd79cff3a119aa6c37c3b7d4e1d347dc94d`.

L'[essai GPU 248](../../../../.tidewater-integration/temporal-trace-native-runtime-248/run.json)
conserve **FAIL sans capture**. Le source resolve progresse, mais la sonde attend
un identifiant MainWorld dans une collection d'identifiants RenderWorld.
`refresh_pipeline_ready` interroge `Entity` alors que les scopes et `FrameTag`
emploient le `MainEntity` d'origine. La clôture anticipée ne vise que le PID21448
et son chemin d'exécutable vérifié ; 95,10 s, processus fermé, fixture intacte.
L'égalité scratch/source et les lectures float32 restent **NOT RUN** sur 248.
Le correctif suivant devra traduire explicitement ces identités et conserver
cet échec avant un nouvel essai dans un nouveau dossier.

## Capture instrumentée 253 et audit 249 — suite du 2 octobre

Le [correctif 252](../../../../.tidewater-integration/trace-engine-view-identity-fix-252/fix-252.json)
interroge `&MainEntity` sur les vues de rendu sélectionnées et utilise son `id()`.
Un vrai test ECS crée deux Worlds avec des IDs distincts et vérifie le mapping,
sans inventer une disponibilité de PipelineCache. Check/Library/Tests247.2
passent sans warning ni dérive, **58 tests CPU PASS**, un test ignoré et
473 filtrés. Example247.2 passe également ; ses 334 inputs incluent les 234
inputs moteur identiques à ceux de Library247.2. SHA256 de l'exécutable :
`978f30fcdf5df71593bb868b5075118acb3a26d2110085add0a5fd0a499997ed`.

Le [runtime 253](../../../../.tidewater-integration/temporal-trace-native-runtime-253/run.json)
termine normalement en **16,27 s**, processus fermé, fixture intacte. Deux
captures auxiliaires normales et trois couples Source7/Trace2 sont terminés :
tags66/68/70, epoch0/0/1, générations1/1/2, reset seed et clear réellement encodés.
La scène possède toujours 65 383 plantes auteur sans exclusion/densité retirée
et 18 993 dessins visibles auxiliaires, sans pending/unsupported/erreur shader.
GTAO5, haze1 et underwater1 constituent la vraie chaîne HDR sélectionnée.
Les deux warnings Bevy de canal fermé apparaissent à la sortie sur les queries
ordinaires de hauteur ; les callbacks qualifiés sont déjà terminés.

L'[auditeur indépendant 249](../../../../.tidewater-integration/temporal-resolve-trace-audit-249/README.md)
passe 26 tests CPU et son
[audit réel 253](../../../../.tidewater-integration/temporal-trace-native-runtime-253/trace-audit-249/audit.json)
confirme le **transport** des trois couples : formats/strides, raw/compact/padded,
SHA, uniformes336/32/144, storage8192, marqueurs, tags/générations et sept IDs,
plus les deux jointures aux lectures normales. Il vérifie aussi le chargement
de la véritable sortie source à chaque site diagnostique. Ce PASS n'est pas
celui de la couleur ou de l'équivalence de la sonde.

| Cas | Pixels scratch/source différents, image entière | Écart absolu maximal HDR | Écart float16 maximal |
| --- | ---: | ---: | ---: |
| statique |753|0,0009765625|1|
| caméra déplacée |1 147|0,0009765625|1|
| reset seed |472 900|0,018310546875|138|

L'égalité bit à bit exigée est **FAIL dans les trois cas** ; l'attribution
des valeurs instrumentées au shader original est donc interdite. Le reset
présente surtout des différences d'alpha (472 524 canaux), mais ses différences
RGB et leur maximum restent consignés, sans les exclure. Les enregistrements
float32 préservent leurs bits et décrivent uniquement la sonde.

La [comparaison directe 236–253](../../../../.tidewater-integration/temporal-trace-native-runtime-253/trace-audit-249-source236-comparison.json)
vérifie les **21 payloads Source7 réellement byte pour byte identiques**, ainsi
que tous les uniformes336/32 et flags/epochs/générations/framesBefore. Les IDs
de textures ne sont pas comparés entre processus. La sonde n'a donc pas modifié
ces entrées ou sorties originales capturées ; elle ne requalifie pas 236.
Le lecteur et l'oracle231 inchangés retrouvent ses trois FAIL complets : maxima
24/193/190 float16 ULP, mêmes 1 214/42 336/2 742 pixels hors seuil. Les anciens
échecs248 et231, ainsi que les seuils, restent conservés.

Le PNG253 est inspecté comme contexte d'une frame ultérieure, sans comparaison
source appariée. La
[recommandation produit 251](../../../../.tidewater-integration/temporal-resolve-production-next-251/NEXT.md)
prépare le prochain raccord : choix temporel explicite, vrais matériaux et caméra
joueur Studio, puis propriétaire Sequence dans RenderWorld pour le natif
pipelined. Les preuves synchrones du probe ne qualifient pas ce chemin. La parité
visuelle complète, Studio/WebGPU, les fonctions restantes et la distribution
restent ouvertes ; l'objectif complet est toujours actif.

Le [gel 254](../../../../.tidewater-integration/temporal-trace-qualified-inputs-254/manifest.json)
préserve 279 artefacts et 334 copies source vérifiées du candidat247.2/253,
sans dérive actuelle lors de la copie. SHA256 du manifeste :
`80eb0f5e741b71b7226296ffddf0f69fa87a43f964207ec353fa15235f850df2`.
La sonde reste optionnelle et inactive dans le parcours normal. La prochaine
tranche prépare une instance Sequence propre au RenderWorld ; sa fondation et
son raccord ne sont pas couverts par ce gel.
