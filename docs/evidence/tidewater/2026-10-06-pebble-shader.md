# Tidewater â€” programme graphique des cailloux, 6 octobre 2026

## Travail rÃ©alisÃ©

Le prÃ©parateur `scripts/tidewater/prepare-debris-pebble-shader.py` conserve
les corps vertex et surface de la source figÃ©e 1438b1ab et les dÃ©pendances
acquises de densitÃ©, pierre, terrain et modulation solaire. Il adapte les
interfaces au shader Village partagÃ© : hauteur, dÃ©tail, densitÃ© et cellules.
Le programme produit compte 39 318 octets, SHA-256
`6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b`.

Le matÃ©riau `tidewater_debris::pebble_material` dÃ©clare les bindings 100â€“106,
les paramÃ¨tres terrain, le buffer de cellules et le matÃ©riau opaque source
(roughness 0.8). Il utilise les rÃ©cepteurs partagÃ©s de la scÃ¨ne et refuse une
interface shader modifiÃ©e. La couche source reÃ§oit les ombres sans en projeter.
L'adapter doit fournir des matrices modÃ¨le identitÃ©, des tags de cellule bornÃ©s
et les vrais pixels acquis avant publication des instances.

## Validation effectuÃ©e

`validate-debris-pebble-shader.mjs` construit rÃ©ellement le shader et le pipeline
WebGPU : une variante, aucun diagnostic de compilation, commande terminÃ©e 0.
Le rÃ©sultat est conservÃ© dans
`.tidewater-integration/pebble-pipeline-20261006-v1/result.json`.
Les imports camÃ©ra et mesh Bevy sont explicitement remplacÃ©s par des stubs.
Aucun bind group de scÃ¨ne ni dessin n'est exÃ©cutÃ©. La notification de destruction
du device provient du nettoyage final, aprÃ¨s le rÃ©sultat rÃ©ussi.

Le matÃ©riau Rust est formatÃ© ; son check Engine complet reste en attente.
La compilation du Studio figÃ© 805 et le contrÃ´le courant dÃ©jÃ  lancÃ©s ne sont
pas redÃ©marrÃ©s. Le candidat Studio figÃ© ne contient pas ce nouveau matÃ©riau.

## Limites et suite

Cette preuve porte sur la construction du pipeline acquis, pas sur le rendu
Bevy complet ni sur la fidÃ©litÃ© visuelle. Restent Ã  vÃ©rifier les rÃ©cepteurs
partagÃ©s adaptÃ©s, construire le mesh patch, publier les pixels et les cellules
GPU depuis le sÃ©lecteur validÃ©, raccorder le runtime et Studio, puis dessiner et
comparer avec la source. Le test Rust acquis ajoutÃ© au module n'a pas Ã©tÃ© exÃ©cutÃ©.

Les pins du prÃ©parateur, du validateur et du matÃ©riau ainsi que les rÃ©sultats
sont conservÃ©s dans
`.tidewater-integration/pebble-shader-adoption-20261006-v1.json`.


## Ressources et publication des cellules

Le module `pebble_mesh` construit le mesh et l'image de densitÃ© depuis le patch
acquis. Les paramÃ¨tres de slot sont portÃ©s par DATA ; UV et teinte restent des
attributs partagÃ©s inutilisÃ©s par le programme source. Les indices U16 sont
Ã©largis en U32 sans modifier leur ordre. L'image conserve les 1 024 Ã— 1 024 pixels
RGBA8Unorm linÃ©aires, un seul mip et le sampler clamp/bilinÃ©aire source.

Le test acquis utilisant les vrais modules et les dÃ©pendances Bevy/Assets dÃ©jÃ 
compilÃ©es passe : un test, aucune erreur, 0,31 s. Il vÃ©rifie octet pour octet les
positions, normales et slots, tous les indices, tous les pixels et le sampler.
V1 passe ; V2 sÃ©pare les refus de seed NaN et de masque tronquÃ©, puis repasse avec
pins source et artifacts inchangÃ©s. Les rÃ©sultats sont conservÃ©s dans
`pebble-mesh-rust-20261006-v[12]/result.json`. Ce test construit les vÃ©ritables
ressources CPU Bevy ; il ne reconstruit pas Engine complet et ne dessine pas.

Le module `pebble_scene::ResidentField` est ajoutÃ©. Il publie les cellules du
sÃ©lecteur vÃ©rifiÃ© dans le buffer du matÃ©riau, rÃ©utilise le patch et les entitÃ©s,
conserve les matrices identitÃ©, dÃ©sactive le culling automatique et les casters,
et retire ses propres draws/mesh au dÃ©chargement. Un frame vide conserve un
binding lÃ©gal non adressÃ© par aucun draw. Ces cellules transitoires ne deviennent
pas des placements persistants artificiels. Ce module reste Ã  compiler dans
Engine, Ã  adopter dans la scÃ¨ne et Ã  exercer sur GPU.

Le lecteur Assets `pebble_program::Program` vÃ©rifie l'acquisition du programme
avant publication, avec transports sync et async, pin du descripteur, des deux
paquets sources et du shader. Ses deux tests acquis et Clippy sont lancÃ©s sous la
session 3818 ; aucun verdict rÃ©ussi n'est attribuÃ© Ã  ce nouveau lecteur.

La vÃ©rification de la chaÃ®ne des rÃ©cepteurs partagÃ©s conserve les Ã©checs de
harness : V1 compte Ã  tort les accolades des marqueurs WGSL dans les strings
Rust ; V2 corrige l'extraction lexicale mais assemble des artifacts prÃ©compilÃ©s
Naga/WGPU incompatibles. Ce refus de compilation ne qualifie pas le shader.
V3 utilise le jeu de dÃ©pendances cohÃ©rent dÃ©jÃ  validÃ© et reste en cours (1994).
Le pÃ©rimÃ¨tre est celui des corps exacts extraits des fonctions actuelles,
composÃ©s avec les vrais imports Bevy ; il ne remplace pas le check Engine complet.

Restent le raccord natif/Studio, les vraies publications de terrain et de
cellules, le dessin, les effets temporels et la comparaison visuelle. La mise Ã 
jour des bounds des cellules aprÃ¨s Ã©dition du terrain reste Ã©galement Ã  prÃ©voir.
Le candidat Studio figÃ© 805 reste intact. Pins et Ã©tat dÃ©taillÃ© :
`.tidewater-integration/pebble-graphics-resources-20261006-v1.json`.


## Résultats complémentaires

Le scénario V3 des récepteurs partagés termine 0 : un test, quatre variantes
avec véritables imports Bevy (éclairage uniforme/storage, meshes uniforme/storage),
1,45 s, pins source inchangés. Il vérifie aussi le binding 106 en storage readonly,
la modulation solaire unique et le refus de double adaptation/de binding altéré.
Le programme adapté a le SHA-256
`acc0b40d2be4ef0af3a25a580a590daa65dbfefcc21ceedf248e865bda450d41`.
Les corps exacts des fonctions actuelles sont extraits ; les structures matériau
et le renderer complet ne sont pas remplacés par cette sonde CPU.

La construction de pipelines WebGPU repasse ensuite avec le programme acquis et
les deux variantes adaptées : trois pipelines, aucun diagnostic, commande 0.
Les imports Bevy sont stubés pour cette étape GPU. Aucun dessin ni bind group de
scène n'est exécuté. Résultat : `pebble-shared-pipeline-20261006-v1/result.json`.

Le contrôle courant 51875 termine 0 : check WASM Assets et check du véritable
example natif, incluant les modules Engine actuels. Un warning ancien du helper
isolated_lod reste présent. Ce résultat est antérieur au renommage privé
ShaderEntry et aux deux attributs must_use du lecteur Assets.

Le lecteur Programme V1 passe ses deux tests acquis ; Clippy refuse trois détails
(struct_field_names et deux must_use_candidate). La correction ne change ni le
format acquis ni son comportement ; V2 relance les tests et Clippy (56334).
Aucun succès Clippy courant n'est annoncé avant sa terminaison.

Le lecteur Programme V2 termine ensuite avec succès : les deux tests acquis et
Clippy Assets lib avec `-D warnings` passent (56334). Les pins source restent
inchangés. Le reçu est `pebble-program-assets-20261006-v2/result.json` dans le
dossier d'intégration du workspace. Aucun renderer n'est exécuté et aucun
candidat natif ou Studio n'est modifié par ces contrôles.

Pins, résultats et limites :
`.tidewater-integration/pebble-graphics-validation-20261006-v2.json`.

## Raccord natif écrit — contrôles en cours

Le bootstrap natif consomme désormais le paquet Debris déjà vérifié pour la
couche Pebble optionnelle. `FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT` désigne le
programme acquis ; sa présence exige aussi le paquet Debris et le graphe
d'ombres partagé. Le descripteur et le shader sont contrôlés par le lecteur
Assets commun, sans seconde lecture des milliers de payloads géométriques.

Le nouveau module `examples/tidewater_plant_probe/pebbles.rs` prépare la grille
à partir de l'origine du terrain résident, adopte le véritable patch, le masque
linéaire, les hauteurs R32Float et le détail RGBA8 avec dix mips/aniso-4. Les
formats/dimensions/sampler sont vérifiés avant publication. Il installe le
matériau sur le renderer Bevy existant, puis sélectionne les cellules après
mise à jour du frustum, avec position, direction arrière, projection et hauteur
CPU de la caméra réelle. Le champ partagé publie les tags et records Float32
avec matrices identité, sans casters ni culling automatique. L'éclairage du
matériau suit la base Village résidente. La disponibilité du playtest attend la
première publication de la couche lorsqu'elle est demandée.

Le check du véritable example natif est lancé sous 13495 ; il attend le cache
Cargo partagé déjà utilisé par les validations des ombres. Les sources sont
épinglées dans `native-pebble-adoption-20261006-v1/request.json`. Aucun succès
de compilation, exécutable reconstruit ou dessin GPU n'est encore attribué.
Le candidat Studio figé n'est pas modifié par ce raccord.

L'adoption temporelle et Studio reste à faire. Le champ natif refuse pour
l'instant une révision de terrain différente de celle de ses bounds acquis ;
le recalcul des bounds après retouche reste ouvert et ce refus ne qualifie pas
l'édition de cette couche. Le lancement, les captures inspectées, le parcours
caméra et la comparaison appariée avec la référence restent nécessaires.

L'inspection de l'adapter temporel confirme qu'il ne faut pas enregistrer cette
couche comme `Family::Village` : cet adapter réserve le varying location 5 aux
positions précédentes, tandis que le shader Pebble acquis utilise déjà cette
location pour `vPebble`. La couche source suit en outre `staticVelocity`.
Une dérivation stricte reprojectant la position monde courante, sans ajouter ce
varying de bâtiment, est nécessaire avant l'adoption temporelle. Aucun fallback
caméra ni verdict sur motion blur/masques n'est attribué au raccord courant.

Une dérivation Pebble dédiée est écrite dans `tidewater_temporal/pebble.rs`.
Elle conserve le varying location 5 et les corps vertex/surface acquis, vérifie
le binding des cellules et le chemin position monde, garde le test alpha source
et reprojette la même position monde courante pour `staticVelocity`. Elle refuse
un shader déjà adapté, une capture de réfraction, un binding déplacé ou un
discard supplémentaire après le calcul source. Elle n'est pas encore déclarée
dans le module temporel ni enregistrée sur le matériau runtime ; les builds
Engine/Studio déjà actifs gardent leur périmètre.

Le harness V1 refuse sa génération : l'extracteur des fonctions ne reconnaît
pas le lifetime générique de `through`. L'échec est conservé sans compilation
Rust. V2 corrige cette extraction et lance la compilation réelle sous 94568.
Il inclut le fichier de dérivation courant et extrait les helpers temporels
actuels ; le programme d'entrée est l'adaptation partagée acquise au hash
`acc0b40d2be4ef0af3a25a580a590daa65dbfefcc21ceedf248e865bda450d41`.
Son scénario vise huit compositions Naga avec imports Bevy réels (deux stages,
deux modes d'éclairage et deux modes de données mesh), la conservation exacte
des fonctions source et trois refus indépendants. Aucun succès de ce scénario,
adoption renderer ou dessin GPU n'est encore attribué.

## Préparation de distribution — code et chemins vérifiés

Le launcher courant reçoit le dossier `content/debris/pebbles` relatif à son
exécutable extrait, remplace la variable de développement héritée et exige les
deux fichiers du programme. Un marqueur CLI distinct `--pebble-runtime` permet
au packaging de refuser l'ancien launcher. Le constructeur Windows exige le
programme acquis, vérifie ses deux hashes et copie ses deux fichiers avec
vérification après copie ; ils entrent dans le manifeste global du portable.
Les ZIP et exécutables précédents restent inchangés.

Les deux tests du `play.rs` courant passent : chemins/environnement après
déplacement dans un dossier avec espaces, et présence des fichiers exigés.
Le harness inclut le vrai fichier source et une bibliothèque DTO launcher
précompilée épinglée ; il ne reconstruit ni n'exécute Tauri ou le jeu. Reçu :
`launcher-pebble-paths-20261006-v1/result.json`, pins inchangés.
Le constructeur Windows et le verifier de promenade passent l'analyse syntaxique
PowerShell, ainsi que les contrôles de whitespace ciblés.

`Verify-NativeDebrisWalk.ps1 -WithPebbles` sélectionne et épingle ce programme,
exige son staging effectif dans le journal et conserve ce constat dans le reçu.
Sans ce switch, il retire toute variable Pebble héritée puis la restaure en fin
de scénario. Cette préparation n'est pas un test de dessin des cailloux : le
runtime nouveau doit encore compiler, être reconstruit, lancé et capturé. Le
nouveau launcher doit aussi recevoir son build complet, son clic réel Jouer et
le contrôle d'un portable extrait. Aucune nouvelle distribution n'est produite.

Le constructeur exige ensuite `NativePebbleProof`, le reçu d'une promenade avec
cailloux sur le hash exact du runtime embarqué. Une preuve sans demande/staging
Pebble, un hash différent ou un champ requis absent refuse le packaging avant
exécution du launcher et avant création de la distribution. Le manifeste final
conserve le hash de cette preuve et celui du runtime. Ce contrôle ne transforme
pas une promenade bornée en qualification graphique complète.

Le scénario `windows-pebble-negative-20261006-v1` lance le constructeur réel sur
ces trois refus, avec les anciens exécutables épinglés, et exige l'absence de
dossier final/temporaire et de ZIP. Il est actif sous 14869 ; aucun succès n'est
encore attribué. La syntaxe PowerShell du constructeur modifié repasse.

V1 s'arrête avant le contrôle de preuve : le paramètre Python par défaut n'est
pas présent dans le PATH. Aucun launcher/runtime n'est exécuté, aucun paquet
n'est créé ; cet échec d'environnement est conservé. V2 transmet explicitement
le Python fourni, puis les trois cas obtiennent le refus attendu et confirment
l'absence de dossier/ZIP final ou temporaire. Commande 0, pins du constructeur
et des deux anciens exécutables inchangés. Reçu :
`windows-pebble-negative-20261006-v2/result.json`. La construction positive du
nouveau portable reste non exécutée et non qualifiée.

## Résultats temporels V2 et V3 — vérifiés le 6 octobre

Les reçus V2 et V3 confirment compilation et test Rust à code 0, avec les
sources épinglées inchangées. V2 vérifie l'adaptateur isolé. V3 inclut le
véritable dispatcher de production, avec `Family::Pebble` désormais déclaré,
et passe le test `acquired_pebble_static_velocity_and_real_imports` en 1,18 s.
Le scénario compose huit variantes Naga avec les imports Bevy réels et
vérifie la conservation des fonctions source et les refus des coutures invalides.
Les chemins d'enregistrement du matériau temporel natif ont été ajoutés.

Preuve : `.tidewater-integration/pebble-temporal-20261006-v3/result.json`
et `tests.log` dans la racine du workspace parent. Ce test ne compile pas
l'application Engine entière et n'exécute pas le renderer. La compilation
native, le dessin GPU et les captures restent à valider ; le succès du shader
ne prouve pas encore la présence visible des cailloux en jeu.

## Launcher de production figé — construction lancée

Le snapshot `launcher-pebble-production-20261006-v1` copie les sources actuelles
du launcher dans le workspace de validation, sans modifier son dépôt. Le frontend
est régénéré depuis `launcher-aube`, source de la page actuelle sur 8848 :
`AUBE_FRONTEND_READY`, 68 illustrations et HTML source SHA-256
`35a7a2d5b6ab088d4a539655c5112f0eabdfaec1586cf35a5c6f0be61c104c8b`.
Les fichiers source et frontend sont épinglés. Le build Tauri de production,
offline et locked, est lancé avec le hook frontend désactivé après cette
préparation vérifiée ; il partage le cache Cargo des builds natifs actifs.

`FinalizeProduction.py` est préparé et passe l'analyse syntaxique Python, sans
exécution à ce stade. Après succès Cargo, il vérifiera les pins, refusera les
changements hors des cinq sorties Tauri générées attendues, archivera ces sorties
et restaurera la copie de validation. Il contrôlera la provenance/fraîcheur du
binaire et ses trois marqueurs CLI avant de conserver un exécutable distinct.
Ces contrôles ne prouvent ni l'ouverture de la fenêtre ni le clic Jouer.
Aucun succès du build, nouveau portable ou parcours de jeu n'est encore attribué.

## Check de l'application native — V1 échoue, V2 relancé

`native-pebble-adoption-20261006-v1/result.json` termine à code Cargo 101, avec
les trois pins inchangés. La compilation de l'exemple refuse l'affectation à
`material.base` : la liaison du `Mut<PebbleMaterial>` n'est pas déclarée mutable
(E0596, ligne 250 du fichier avant correction). Aucun nouveau runtime n'est
construit ou exécuté. Les avertissements de copie du cache incrémental sont
conservés séparément de cette erreur de code.

La liaison devient `let mut material`. Le chargement valide aussi maintenant
la dérivation `Family::Pebble` du shader adapté avant de retourner son Input,
afin de refuser une couture temporelle invalide avant publication des ressources.
Le formatage ciblé et le contrôle whitespace passent. Le check V2 de l'exemple
est relancé sous 42764, avec le fichier corrigé épinglé au SHA-256
`e6850a1f37c7f2cb179ddf0038dddd4d63fdd4ed98065ae8b53d98c909671802`.
Son résultat n'est pas encore attribué. Les candidats Studio figés antérieurs
ne contiennent pas ce bootstrap Pebble et ne sont pas modifiés par cette réparation.

## Launcher de production — build, marqueurs et interface réelle PASS

La construction `launcher-pebble-production-20261006-v1` termine à code 0.
Le finalizer termine aussi à code 0 : source originale et code figé inchangés,
frontend inchangé, aucune sortie Tauri différente à restaurer, provenance du
depfile et fraîcheur de l'exécutable vérifiées. Les trois marqueurs CLI passent.
Le binaire conservé dans `desktop/OpenRPG Forge.exe` a pour SHA-256
`434321efa0282c53acd8b24eb056c252952a91d2380bab93743eea1262ffb0c7`.
Reçu : `production-result.json`. Aucun ZIP nouveau n'est produit.

Le contrôle d'interface utilise un profil WebView neuf, un port loopback réservé
et le seul processus dont l'identité/hash est vérifiée. La préparation du helper
rencontre d'abord une erreur de quoting PowerShell, puis V1 refuse l'utilisation
du nom réservé HOME avant lancement. V2 ouvre le launcher mais ne reconnaît pas
le titre calculé par `app.js` : il attend le titre HTML initial. Son FAIL reste
dans `ui-v2/result.json`, processus fermé et exécutable inchangé.

V3 attend le titre réel `L’Aube des royaumes — OpenRPG Forge` et vérifie la page
Tauri locale exacte, la présence du bridge natif, l'accueil et le clic sur Jouer.
Le résultat `ui-v3/result.json` est PASS, processus propre fermé et hash inchangé.
Les ressources observées viennent de `http://tauri.localhost/`, sans accès au
serveur 8848. Les captures `ui-v3/home.png` et `ui-v3/jouer.png` ont été ouvertes
et inspectées : accueil Aube et menu de l'île réellement affichés.

Dans ce dossier de validation, runtime et données ne sont pas encore assemblés :
le menu signale correctement les fichiers absents/incomplets et désactive le
bouton. Aucun clic de lancement effectif du jeu, parcours extrait ou parité
graphique n'est attribué à cette preuve d'interface. Le nouveau runtime Pebble
et le portable complet restent nécessaires.

## Préparation du build runtime après check corrigé

`build-native-pebbles-20261006-v1.py` est préparé et passe l'analyse syntaxique
Python. Il n'est pas exécuté tant que le reçu du check V2 n'est pas à code 0
avec les trois pins inchangés. Avant construction, il exigera aussi ces mêmes
pins dans le worktree courant et la branche Rust.

Le constructeur épingle les sources Rust/WGSL/TOML des crates ainsi que les
fichiers racine Cargo, lock et configuration/toolchain, puis lance le véritable
`cargo build --example tidewater_plant_probe --offline --locked -j1`. Il refuse
une modification de ces entrées, un exécutable ancien ou un depfile ne désignant
pas le checkout attendu avant conservation d'une copie distincte. Cette portée
ne prétend pas épingler chaque entrée transitive externe au workspace. Même en
cas de build réussi, la promenade avec cailloux, le dessin réel, les captures
comparées et le portable complet resteront des étapes séparées.

Le check natif V2 termine à code 0, les trois pins étant inchangés. Il vérifie
la compilation de l'application corrigée, sans reconstruire son exécutable ni
exécuter le renderer. Le constructeur runtime décrit ci-dessus est ensuite
lancé sous 19763 ; il compile actuellement le véritable Engine. Aucun résultat
de cette construction ou dessin des cailloux n'est encore attribué.

## Passes encore manquantes — inspection du code courant

La capture native `tidewater_refraction::register` installe `TerrainCapture`
et `VillageCapture`. Son `mirror.rs` conserve les matériaux Village et terrain
et collecte leurs composants concrets `MeshMaterial3d`. Les types étendus
`PebbleMaterial`, `ScannedMaterial` et `NatureMaterial` ne sont pas ces types
concrets et n'ont pas de miroir correspondant dans ce collecteur. Le rendu
principal et le raccord temporel Pebble ne suffisent donc pas à prouver leur
contribution correcte à la réfraction. Une simple couche de visibilité sur les
entités ne remplace pas ce miroir de matériau/shader ; ce raccord reste à faire.

Les deux adaptateurs temporels natifs enregistrent désormais Pebble et Nature,
mais pas `ScannedMaterial`. L'enum de familles ne contient pas encore de famille
Scanned dédiée. Il faut préserver son interface et sa couverture source dans
une dérivation adaptée, puis enregistrer et vérifier cette contribution. Ces
constats ne sont pas un échec du check de compilation V2 : ils identifient une
portée de fidélité non couverte par ce check et par la future promenade bornée.

## Intégrité de la prochaine promenade et de son packaging

`Verify-NativeDebrisWalk.ps1` neutralise temporairement les variables héritées
`FORGE_TIDEWATER_*`, puis configure ses seules entrées et restaure le contexte
dans `finally`. Les anciens modes de diagnostic ne peuvent ainsi remplacer le
parcours ou désactiver silencieusement ses effets. Le vérificateur contrôle
à nouveau le binaire et les cinq entrées épinglées après exécution, et consigne
les hashes du reçu de promenade et de la capture. Ces cinq entrées ne représentent
pas un inventaire de tous les fichiers du bundle.

`Build-WindowsPlaytest.ps1` exige désormais ces invariants et les deux artefacts
intacts avant de lancer les contrôles du launcher ou créer un paquet. Le contrôle
réel `walk-artifact-guards-20261006-v2/result.json` passe les quatre refus attendus :
entrées déclarées modifiées, capture absente, capture modifiée et reçu modifié.
Aucun launcher/runtime exécuté et aucun dossier de paquet créé lors de ces
tests négatifs. L'analyse syntaxique PowerShell du vérificateur passe aussi.

V1 reste conservé avec son échec de harnais : Windows PowerShell ne résolvait
pas `Get-FileHash` dans le contexte hérité. V2 utilise le PowerShell 7 fourni
par le runtime local ; aucun contrôle de production n'a été supprimé.

Le readiness du contrôleur inclut `pebbles::ready` par `debris::ready` : une
promenade réussie attend la publication de caméra des galets. Ce critère et
le marqueur de ressources chargées ne prouvent toutefois pas séparément les
pixels des galets. Une preuve de dessin/readback spécifique reste nécessaire,
ainsi que le parcours natif réel et le test du portable extrait.
