# Tidewater — calcul de surface complet de l'échoppe

Le rendu Coast natif du 6 octobre confirme des matériaux d'échoppe distincts de
la source, même après isolation correcte de l'exposition. Le shader actuel est
un aperçu d'arrays avec lumière diffuse compacte. L'objectif reste de dessiner
le matériau complet, ses ombres et son éclairage dans Runtime et Studio.

## Calcul acquis et bibliothèque préparée

L'acquisition `stall-material-source-contract-20261006-v1` exécute la vraie
factory `createStallMaterial` du clone propre Tidewater
`1438b1abfcaee3267092b75573014f4d9b4a983c`. Elle conserve le corps de surface
généré, les modules, les paramètres et la provenance/licence. Les sept arrays
réels du paquet FishStand sont épinglés. Détails dans le
[rapport Studio/scène](2026-10-06-studio-scanned-library.md).

`PrepareStallSurfaceLibraryV1.py` prépare une bibliothèque callable contenant
les structs/defaults `Surface` originaux, tous les helpers StallKit et tout le
corps généré. La seule adaptation du corps remplace sept noms de textures et
deux noms de samplers ; la reconstruction inverse retrouve exactement les
octets source. L'appel reçoit temps et nuit explicitement. Le matériau conserve
ses paramètres de rugosité, métal, occlusion, émission, transmission, clearcoat,
sheen et indice de réfraction.

Le premier essai GPU échoue sur les deux intensités `mat` ajoutées par la
factory source. La preuve V1 et son `failure.json` sont conservés. V2 ajoute le
paramètre `StallMaterialParameters` contenant `emissiveIntensity` et
`envMapIntensity`, sans changer le corps source. Les deux valeurs valent1 dans
le contrat acquis ; l'appel de qualification transmet ces valeurs explicitement.
Les outputs V1 ne sont pas réécrits.

Bibliothèque V2 : `stall-surface-library-20261006-v2/stall-surface.wgsl`,
11313octets, SHA
`2e760711aef571e16b8c9cc26a5fa7b0dcef27024718914dacd59778dc8e110d`.
Les sources de production ne sont pas modifiées par cette préparation.

## Comparaison GPU réellement dessinée

`VerifyStallSurfaceGpuV2.mjs` termine code0 sous Node24.19.0 avec le device
WebGPU headless de la source. Les arrays épinglés sont chargés réellement,
selon leur format sRGB/linéaire ; le test ne substitue pas des textures factices.
Deux variantes, noms source et noms adaptés, dessinent les mêmes entrées dans
des cibles `RGBA32Float`, puis lisent leurs octets terminés sur le CPU.

Le probe couvre19couches :0–3,100–107 et−1–−7. Chaque couche couvre256pixels ;
les28champs de `Surface` sont conservés dans sept sorties, avec temps2 et
nuit0 puis1. Les deux lectures source/adaptation sont identiques bit à bit :

| Nuit | Octets par variante | SHA-256 commun |
|---|---:|---|
| 0 | 544768 | `d614d989622c1747f55c21495d369854d1aaa3bf5ab45e1c5ee70e7fb41aec24` |
| 1 | 544768 | `11c241a2568e265fa7a22aa512862a9f3a781781447763b493c8a05d40f91368` |

Une relecture indépendante Python vérifie taille, égalité source/adaptation et
SHA des quatre fichiers. Les valeurs float32 sont finies. La politique
`diagnostic(off, derivative_uniformity)` reproduit celle du composeur source
`Shader.js:585`, nécessaire pour ses dérivées dans les branches ; ce n'est pas
une relaxation ajoutée à l'adaptation pour contourner une validation.
La notification de perte du device à sa destruction volontaire est conservée.

## Portée et raccord restant

PASS du calcul de surface isolé, sur géométrie plane synthétique et deux états.
Le comparateur source retrouve le corps généré via la reconstruction inverse
des noms ; il ne dessine pas l'application source complète. Niveaux de texture
de base et samplers linéaires : équivalence des mips source et de l'anisotropie
non qualifiée. Pas encore de matériau Bevy installé, d'éclairage de scène,
d'ombres ou de pixels Runtime/Studio nouveaux.

Le raccord suivant doit consommer ces champs complets dans la lumière source,
y compris clearcoat/transmission, et les ressources communes d'environnement,
nuages, ombres et rebond. Il doit fournir le temps/nuit partagés et préserver
les éditions/identités de l'objet dans la façade commune. Le dessin natif puis
Studio doit couvrir l'enseigne usée, glace, cadran, cordages, flotteurs et
lanterne, avec ombre de l'échoppe et comparaison Coast contrôlée. Les poissons
exposés doivent aussi consommer leur vrai matériau acquis. Aucun critère de
carte complète, parité globale, performance, gate ou distribution n'est clôturé.

## Éclairage physique complet : comparaison GPU isolée

`PrepareStallLightingLibraryV1.mjs` prépare ensuite `stall-lit.wgsl` depuis le
module original `lighting.js` et son préprocesseur, sur le même commit figé.
Le corps de `shadeSurface` est conservé ; seul son argument explicite
`StallLightingFrame` remplace la dépendance globale du cadre. La reconstruction
inverse est exacte. Le mode monde conserve clearcoat, transmission, compensation
multiscatter, occlusion diffuse/spéculaire, lumière directe, environnement,
rebond et lumières locales. Les neuf hooks de scène restent obligatoires.
SHA du shader complet :
`f1d1395ebeb2011d52453c17fef95a723917b27f02810d468217efa6e4ff1dc2`.

`VerifyStallLightingGpuV1.mjs` termine code 0, PASS, avec dessin GPU effectif.
Il charge les sept arrays réels et les 36 faces/niveaux `rgba16float` de
l'environnement Coast acquis. Chaque face est vérifiée contre le SHA du
contrat `source-environment.json`. Lumière solaire et SH sont ceux de Coast.
Les versions source et adaptée du corps d'éclairage dessinent 19 couches,
256 pixels chacune, dans six cas ; leurs sorties RGBA32Float sont identiques
bit à bit et finies. Sorties : `stall-lighting-gpu-20261006-v1`.

| Cas | SHA-256 de la lecture commune source/adaptation |
|---|---|
| Jour | `4ee718fcbe812274de430651eb3b0cd405b764c6961e8259346712c112efeabb` |
| Nuit | `12b3a9f822a2e314ee8cdc27586ff4bb9ec2fa8adec1184b050a5ad507d79029` |
| Visibilité solaire nulle | `a48b4be4d9742ff1ab2caa5f6109567b6cff3a6194cfb72c3680d18e824d1060` |
| Clearcoat forcé à zéro | `383d39f18e4beaeeded9ca09be2ba4b4f13200cb9f3d16e2bbcfd3c419f837b1` |
| Clearcoat forcé à un | `f6b24e4efef237e0fb76144e38e702209b7a7ca9e97990713656b6cfa718fdc6` |
| Transmission imposée | `874f239e27bce0bccace5f9353a5ec0569dd781c28159c25e80f7bb2cf836902` |

Les changements de visibilité, clearcoat, transmission et nuit changent bien
les valeurs calculées. `VerifyStallLightingReadbacksV1.py` relit indépendamment
les douze fichiers de 77824 octets, leurs SHA et tous les float32 ; code 0 PASS.
Il confirme en particulier que le clearcoat **naturel** de la glace source
(couche −1), sans le forcer à un, change le résultat : delta absolu maximal
0.1348528862 entre jour normal et clearcoat forcé à zéro.

Limites : géométrie plane synthétique et niveaux de base des arrays du matériau.
Visibilité solaire, contact, modulations directe/ambiante, rebond et lumière
locale utilisent des entrées de test explicites et non neutres. Ces fonctions
ne sont pas des receivers de production et ne doivent pas être installées dans
Runtime/Studio. Le cas nuit modifie la surface/lanterne, avec environnement
Coast jour conservé ; ce n'est pas une qualification du ciel nocturne. Le
comparateur vérifie l'adaptation de signature et ressources du calcul source,
pas l'application Tidewater complète. Aucun matériau Bevy nouveau installé,
aucune comparaison de shadow maps réelles et aucune parité globale revendiquée.

Suite concrète : fournir ces neuf hooks avec les ressources communes du moteur,
sans doubler l'albedo/AO dans le rebond, puis installer le matériau et comparer
les pixels de l'échoppe dans Runtime et Studio. L'API commune existante utilise
encore `LightingSurface` sans clearcoat ; conserver cette version simplifiée
comme éclairage final de l'échoppe ne satisfait pas le contrat source acquis.

## Bibliothèque intégrée au code moteur et assemblage Rust vérifié sur GPU

Le moteur expose désormais `tidewater_stall::full_shader`, avec la bibliothèque
embarquée `tidewater_stall/full_library.wgsl`. Ses octets correspondent exactement
au SHA qualifié ci-dessus ; aucun accès au clone source ou aux scripts ignorés
n'est nécessaire pour assembler le shader en production.

`SceneHooks` impose explicitement les neuf receivers. `shader_source` refuse
les receivers absents/dupliqués, une collision avec le matériau embarqué, un
assemblage trop volumineux et l'absence d'adapter de rendu. Il conserve tout le
corps du matériau et applique la politique de dérivées du composeur source.
Il ne fournit pas de hooks neutres et n'installe pas implicitement de pipeline.

Deux tests Rust d'interface passent. Le test d'export à fixture explicite passe
également lors de son invocation dédiée ; il assemble via le **code Rust du
moteur** les hooks/ressources d'essai et écrit `production-assembled.wgsl`.
Compilation de ce module et tests effectués avec `rustc --edition 2024 --test`
sur le fichier réel ; cela ne constitue pas un build complet de `forge_engine`.

`VerifyStallProductionGpuV2.mjs` termine ensuite code 0 PASS. Il dessine le
shader issu de cet assemblage Rust, face au calcul source, avec les mêmes six
cas, arrays, faces d'environnement et contrôles de sensibilité. Les six lectures
restent identiques bit à bit aux valeurs du tableau précédent. SHA du shader
assemblé : `cd95ee853301d8baeff9db8ca90e5938525299a21988ee47b3472a1b28ed10cb`.
Sorties : `stall-lighting-production-gpu-20261006-v2`.

Cette intégration concerne la bibliothèque et l'assemblage, **pas encore le
matériau résident ni le dessin Bevy de l'île**. `FishStandMaterial` et son chemin
actuel restent inchangés ; la nouvelle API n'est pas encore appelée dans
Runtime/Studio. Les hooks des fixtures GPU restent des fonctions de test.
Le raccord aux receivers réels, notamment l'ombre de contact, la déclaration des
bindings du matériau et la synchronisation des ressources sont toujours requis
avant remplacement du shader actuel. Pas de nouveau binaire de jeu livré,
de performance mesurée ou de qualification globale à cette étape.

## Ombre de contact source : receiver acquis et dessin GPU contrôlé

Inspection du chemin existant : `tidewater_ocean_depth::SceneDepth` reçoit la
profondeur après `main_opaque_pass_3d` et avant le passage transmissif. Pendant
le passage opaque suivant, cette texture contient donc encore l'image opaque
précédente. C'est la catégorie de profondeur attendue par `ContactShadows.js`.
La profondeur précédente de `tidewater_temporal::AuxTargets` est au contraire
copiée depuis la profondeur finale après l'eau et ne doit pas lui être substituée.
Le chemin `SceneDepth` actuel est une ressource globale ; la possession par vue,
les redimensionnements et l'amorçage doivent être traités avant consommation
de cet historique par le matériau.

`AcquireContactShadowHookV1.mjs` exécute l'installation originale du module sur
le clone propre figé, puis son préprocesseur en mode opaque, sans exclusion.
Le premier contrôle de reconstruction inverse échoue avant écriture, sur l'ordre
de remplacement des deux noms de paramètres ; l'ordre est corrigé et le contrôle
termine PASS. Huit pas à moins de 12 m, quatre au-delà, portée de 32 m conservée.
La seule adaptation du corps remplace les noms du cadre et de la force.
Sources/licence et reçus : `contact-shadow-hook-20261006-v1`.

Le receiver est embarqué dans `tidewater_stall/contact_shadow.wgsl`, exposé par
`full_shader::CONTACT_SHADOW_RECEIVER`. Bindings explicites 56 profondeur et 57
cadre ; SHA `7c3db9c4dda2599ddfa54a87e938f560288f43d3c5389f6ca2e27c8f4fc846e1`.
Le cadre attend vue, VP actuelle, VP précédente sans jitter, position caméra,
near, direction/couleur solaire, niveau marin, force, résolution et numéro de
frame. Le receiver exige également le bruit `interleavedGradientNoise` source.

`VerifyContactShadowGpuV1.mjs` termine code 0 PASS : les fonctions source et
adaptée dessinent quatre cas dans RGBA32Float en lisant de vraies textures
Depth32Float initialisées par un passage GPU. Comparaisons byte-identical,
valeurs finies dans [0,1]. À 5 m : profondeur vide → visibilité 1 ; obstacle
proche depth=.0204 → visibilité 0 ; depth=.05 hors épaisseur → visibilité 1.
À 20 m, profondeur vide → visibilité 1. La réponse effective d'occlusion et
le rejet d'une fausse ombre par l'épaisseur sont vérifiés.

Textures de profondeur et caméra sont ici synthétiques : aucun historique de
scène capturé n'est utilisé. Pas encore d'uniforme Rust résident, de publication
du cadre par vue ni de matériau consommant ce receiver dans Runtime/Studio.
L'exécutable courant conserve son rendu antérieur. Le raccord doit utiliser
l'image opaque et la caméra précédente de la même vue, avec amorçage/invalidation
explicites ; un simple handle de texture disponible ne prouve pas cet invariant.

## Cadre Rust et reçu de copie opaque : raccord de code en cours

`tidewater_stall::contact_frame` ajoute l'uniforme Rust correspondant aux
256 octets du cadre WGSL. Sa préparation prend le `ViewPacket` réel et un reçu
de la copie opaque précédente ; elle refuse une vue différente, un serial non
consécutif, une taille/époque différente, une matrice précédente incohérente,
un historique à réamorcer et des paramètres non finis. Le numéro de frame est
transmis explicitement : il n'est pas remplacé par la phase Halton32.

Le propriétaire du reçu est le chemin de profondeur partagé
`tidewater_ocean_depth`, indépendant de l'identité d'une échoppe. `DepthCopy`
contient le tag de vue/frame, la taille, l'époque et la VP sans jitter.
`OpaqueDepthHistory` partage son dernier reçu entre les mondes principal/rendu.
`copy_opaque_depth` enregistre ce reçu après encodage de la vraie copie et
seulement pour un packet de taille compatible. Une copie par vue non étiquetée
efface le reçu ; un redimensionnement invalide l'historique. La réception d'un
tag ne représente ni une clôture GPU ni une vérification des pixels.

Deux tests ajoutés doivent vérifier le packing Encase (256 octets, offsets,
matrices et compteur) et le rejet des historiques invalides. La commande
`cargo test --locked --offline -p forge_engine --lib tidewater_stall::contact_frame::tests -j 1`
est lancée, session 97287, processus Cargo 39652/42944. Au dernier contrôle,
elle est encore vivante et n'a pas rendu de verdict. Son output montre une
compilation native de `forge_navigation` ; le `CARGO_TARGET_DIR` effectif est
`F:/OpenRPG_Forge/.build/cargo-target`, distinct du cache utilisé par l'autre
check WASM 43196/16368 (`F:/OpenRPG_Forge/relocated-work/Rust-target`).
L'hypothèse initiale d'une attente du même cache est donc corrigée. Les warnings
de hard-link du cache incrémental sont conservés ; Cargo utilise une copie.
Le check WASM préexistant n'a pas été interrompu.
Ne pas relancer le test du seul fait de cette attente : interroger son handle.

Le formatage ciblé et `git diff --check` terminent code 0. Le nouveau raccord
de copie/cadre n'est pas encore qualifié par compilation complète, tests
terminés ou capture Runtime/Studio. Le matériau actuel ne consomme pas encore
les bindings 56/57 ; aucun effet visuel supplémentaire annoncé pour l'exécutable.

## Matériau résident complet : ressources déclarées, consommation à terminer

`tidewater_stall::full_material::FullMaterial` ajoute le bind group Bevy pour
la surface et l'éclairage complets : sept arrays source, temps/nuit/intensités,
environnement cube, terrain/objets/nuages, rebond, lumière locale, lumière de
scène partagée, paramètres sous-marins et profondeur/cadre de contact 56/57.
Il conserve les attributs de la géométrie originale et utilise une identité de
shader séparée du matériau compact existant. Sa préparation prend les vrais
`LightingBindings`, le packet caméra et le reçu de copie opaque.

Le reçu `DepthCopy` conserve désormais aussi le handle exact de l'image copiée.
La préparation du matériau refuse une texture de contact différente, même si
la vue, les matrices et la taille concordent. Temps/nuit non valides et textures
source absentes échouent avant publication. Les intensités source restent 1.

Le matériau n'est pas encore enregistré/instancié dans Runtime ou Studio, ni
son shader complet assemblé avec tous les receivers réels. La soumission de sa
géométrie dans le graphe source d'ombres, la publication des cadres et les
updaters d'éclairage restent requis. Aucun pipeline Bevy nouveau n'a encore
dessiné l'île. La commande de tests de la section précédente reste vivante,
session 97287 ; son dernier output compile `forge_assets`. Pas de verdict de
compilation complète ni de tests terminés à cette étape. Les changements de
ce tour précèdent le démarrage observé de la compilation de `forge_engine`.
Formatage ciblé et contrôle des espaces terminent code 0.

`full_adapter.wgsl` fournit maintenant les entrées Bevy vertex/fragment du
matériau complet : attributs source conservés, position/normale mondiales et
caméra réelle, paramètres de surface binding 9, appel de `stallSurface` puis
du `shadeSurface` complet avec la lumière de scène. L'API
`full_shader::bevy_shader_source` l'assemble avec les receivers obligatoires.
Cet adapter n'a pas encore été compilé/dessiné par un pipeline Bevy réel ; les
comparaisons GPU précédentes utilisent leurs propres entrées synthétiques.

## Assemblage des receivers réels de scène ajouté

`full_scene::shader_source` accepte le même corps de lumière de village vérifié
que l'adapter du poisson. Il applique le raccord commun aux ressources vivantes,
conserve les receivers de terrain/nuages/objets/sous-marin et remplace les BRDF
compactes par celles de la bibliothèque complète. Les neuf hooks sont fournis,
dont le receiver de contact acquis. Le cube utilise les mêmes six mips ; les
SH et le soleil restent issus du cadre de scène, sans direction inventée.

Le hook de rebond appelle le helper commun avec couleur/occlusion unitaires pour
obtenir l'irradiance divisée par PI ; la lumière complète applique ensuite la
couleur diffuse et l'occlusion une seule fois. Les lumières locales passent par
le helper commun ; sa somme diffuse/spéculaire est ajoutée au total direct avant
la composition du clearcoat. Aucune identité de map/objet ne branche ces calculs.

La fermeture brute déjà utilisée par la preuve poisson est retrouvée et son SHA
vérifié : `28dd6f58d3f9e51982810e438b9e219f867ceaf8ea34bb5bc2ab9aa2e2bc9e0b`,
`village.wgsl` du paquet `a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`
dans la fixture `.placeables-smoke-beach-20261004-v1`. Elle contient les seams
`LightingSurface`, `VillageSceneLighting`, binding 18 et `sourceSceneLight`.
Son assemblage complet/dessin avec la nouvelle API reste à exécuter ; une simple
inspection de ces marqueurs ne constitue pas une validation WGSL ou GPU.

La session de tests 97287 est toujours active ; son output atteint maintenant
`Compiling forge_engine`. Le filtre lancé cible les tests du cadre de contact,
pas les nouveaux tests d'assemblage de scène. Le nouveau shader complet n'est
toujours pas activé dans le jeu/Studio. Il faut terminer la compilation, corriger
ses erreurs éventuelles, qualifier l'assemblage réel puis effectuer le changement
de matériau et la soumission au graphe d'ombres avec captures.

## Assemblage réel exécuté et pipelines GPU compilés

`ValidateStallSceneAssemblyV1.py` compile/exécute les fonctions pures actuelles
de composition Rust et leurs véritables dépendances WGSL, sans implémentation
factice des receivers. Le wrapper extrait les fonctions de composition et
charge `full_shader`/`full_scene` depuis leurs fichiers réels ; il n'installe pas
Bevy et ne remplace pas une compilation complète du moteur. La fermeture de
village épinglée ci-dessus est assemblée avec succès. Trois tests d'interface
passent ; le test d'export à fixture spécifique reste ignoré dans ce wrapper.

Le premier contrôle GPU de ce résultat échoue sur `common`, mot réservé WGSL,
dans le raccord des lumières locales. Renommage en `sharedSurface`. L'assemblage
V2 passe, puis le contrôle GPU échoue sur `TWO_PI`, nécessaire aux helpers des
ombres et auparavant fourni par l'adapter du poisson. La même constante est
ajoutée à l'adapter complet. V1/V2 et leurs échecs restent conservés.

`ValidateStallSceneAssemblyV3.py` termine code 0 : compilation, assemblage et
trois tests PASS, inputs inchangés. Shader assemblé SHA
`05e9b8bcffbac4dd8bf9ab7d37a3effcf573d773f36967a4ddae542cf4d0dffb`.
Le GPU V3 compile deux fois, mais l'audit découvre un défaut du test : `#ifdef`
teste la présence du define, donc une valeur 0 sélectionnait aussi le storage.
Le résultat V3 prouve uniquement la branche storage ; son affirmation des deux
branches est invalidée. La production n'est pas modifiée pour résoudre ce défaut.

`ValidateStallSceneGpuCompilationV4.mjs` omet réellement le define dans le cas
uniforme, le fournit dans le cas storage et vérifie la déclaration sélectionnée
avant compilation. Il termine code 0 PASS : deux modules et pipelines WebGPU
compilés, shaders distincts : uniforme
`12a1e58a45d0ddb411150edb3265e5708b6808feeef1d83b3142f27dd49f2423`,
storage `fea3bf0e9b1b783877b6e2977fba6da29fc4e9a9f622a9bda0a40d03572d89ae`.
Sorties : `stall-scene-gpu-compilation-20261006-v4`.

Portée exacte : surface complète, lumière complète et vrais receivers de scène,
avec entrées fullscreen diagnostiques. Les imports Bevy et ses fonctions
vertex/fragment sont retirés pour ce contrôle isolé ; aucun bind group de
ressources, dessin, lecture GPU ou pixel de l'île n'est soumis. Cela ne prouve
pas le pipeline Bevy ni le matériau résident en jeu/Studio.

La session Cargo 97287 est toujours active, sans verdict. Lecture autoritative
des processus : rustc 43096 est l'enfant de notre Cargo 42944 et compile
`forge_engine`; son temps CPU progresse. Le check WASM préexistant compile
séparément ses dépendances. Aucun build n'a été interrompu ou relancé sur la
seule base d'une attente. Les fichiers ciblés passent `rustfmt --check`.
Les modifications de shader postérieures au lancement de rustc devront être
reprises dans la validation suivante, même si le test courant passe.

## Raccord natif et couverture temporelle du matériau complet

Le probe natif enregistre désormais `MaterialPlugin<FullMaterial>` et stage son
shader depuis le même `village.wgsl` vérifié que les bâtiments, uniquement si le
contenu du stand est présent. `full_material::stage_shader` compose avant de
modifier le store de shaders et installe le receiver de lumière commun. Ce
raccord prépare le pipeline ; il ne remplace pas encore le matériau des entités.

`FullMaterial::update_scene` copie les ressources vivantes d'un `VillageMaterial`
résident : environnement, relief, ombres objet, nuages, caustiques, rebond, lumières
locales et storage de scène. Les tableaux de textures source restent résidents.
Les paramètres de surface et le couple caméra/profondeur opaque sont validés
avant toute modification de ces bindings. Aucun succès de compilation complète
ou d'exécution Bevy n'est encore acquis pour cette nouvelle API.

La famille auxiliaire explicite `FullStall` conserve les attributs du mesh et
ajoute une varying de position calculée avec le vrai précédent transform rigide.
Le masque reste opaque comme la phase source ; le shader beauté n'est pas évalué
pour fabriquer les buffers de vitesse. La dérivation refuse une couverture avec
`discard`, une collision de varying, une sortie beauté modifiée et une seconde
dérivation. Les deux chemins temporels du probe, auxiliaire et render-owned,
enregistrent le type complet dans cette famille quand le stand est présent.

Validation isolée exécutée avec `rustc` sur les modules de production actuels,
sans Bevy : wrapper `.tidewater-integration/validate-full-stall-temporal-20261006.rs`.
Compilation code 0 (warnings de fonctions/familles inutilisées propres au wrapper),
exécution code 0 : dérivation du shader complet réellement assemblé V3, quatre
refus de couverture/historique et régression de l'ancien adapter PASS. Outputs :
`full-stall-temporal-20261006-v1/vertex.wgsl` SHA
`5f595b2c8d0f14301f32f724aad8e81d8a555b9fcb938985a40f1ee1007498e2`,
fragment SHA `c29eaa1164cd55c6033f1cb1e611dcd9fb3839c856147c2ff4efa5dcd28341fc`.
Ce contrôle ne compile pas les imports GPU Bevy et ne dessine pas l'île.

Les fichiers touchés passent rustfmt et diff whitespace. La session Cargo 97287
reste active : processus Cargo 42944 et rustc 43096 observés vivants, CPU rustc
55,65625 s et mémoire 3 012 067 328 octets au contrôle de ce tour. La compilation
lancée avant ces changements ne pourra pas les qualifier. Aucun build préexistant
interrompu, aucun paquet redistribué ni publication. Restent le publisher au bon
moment du RenderWorld, le traitement explicite du premier frame/reset, le
remplacement réel du matériau, la soumission au graphe d'ombres et les captures
comparatives native/Studio.

## Publisher RenderWorld et adoption native écrits

Le cadre de contact peut maintenant être sélectionné explicitement par le define
`FORGE_RENDER_CONTACT_FRAME`. Le chemin uniforme 57 reste disponible pour les
probes indépendants. Le chemin live lit un storage 58 : les 256 octets du cadre
de contact, suivis d'un vec4 de paramètres de surface (272 octets attendus).
Le receiver live prend direction et couleur de la même `sceneLighting` que le
BRDF, afin de suivre une génération d'atmosphère contrôlée par le GPU. Le corps
de l'algorithme contact est conservé, avec uniquement l'accès au cadre adapté.

`contact_gpu` enregistre un publisher dans le device Bevy existant, après
PrepareAssets et avant Queue. La caméra vient du `ViewPacket` réellement préparé
dans RenderWorld ; les inputs CPU sont extraits. Le receipt doit correspondre à
la vue, au serial précédent, à la taille, à l'epoch, à la matrice et au handle
opaque courant. Premier frame/reset : suspension explicite avec force 0. Un
receipt absent/incohérent invalide un ancien cadre et conserve une erreur dans
`Observation`. Ce comportement ne doit jamais être qualifié comme des ombres
actives. Les tests de layout 272 octets et de suspension sont écrits mais non
encore exécutés dans Cargo.

Le probe natif conserve désormais le résident du stand. Quand son chemin
temporel est activé, `full_stall::install` stage l'adoption en PostStartup :
remplacement du composant matériau simplifié par `FullMaterial`, avec les
véritables ressources du village. Last actualise les bindings, le temps de la
pose de vent/source, le night du sky (composante Z vérifiée dans prepare-sky.py),
le compteur de frame et la taille de profondeur de la caméra active. Les scènes
sans passe temporelle continuent pour l'instant à utiliser l'ancien adapter.
Les scripts de playtest/package n'activent pas actuellement ces switches : cette
écriture n'est donc pas encore une correction livrée du portable courant.

Validation shader finale sur inputs inchangés : assemblage V5 termine code 0,
quatre tests PASS, un export explicite ignoré ; shader complet SHA
`2a40778692cad334ef976691586f1ae1a124778b175fd05b37f43dba13d5bae5`.
GPU V6 termine code 0 : les quatre combinaisons lumière uniforme/storage et
contact uniforme/render-owned compilent un pipeline RGBA16Float, avec contrôle
des déclarations 18/55 et 57/58 réellement sélectionnées. Les outputs sont dans
`stall-scene-gpu-compilation-20261006-v6`. Pas de bindings de ressources, draw,
readback ou imports Bevy dans ce contrôle isolé.

La dérivation temporelle du shader V5, wrapper std-only V2 compilé et exécuté
code 0, conserve la position du modèle précédent, refuse les quatre changements
de couverture/historique et conserve la régression de l'ancien stand. Outputs
`full-stall-temporal-20261006-v2` : vertex SHA
`5dfe05476e9546f00a8ebf466d4a3da835773f9dba7d76e8fb2116d77262d65d`,
fragment SHA `8574a18b05a33b141f2f1d1b565821bda80e98b03928f5ee3f4957ed447eded3`.

La session 97287 reste active et compile son ancienne génération de sources ;
rustc 43096 est vivant avec CPU 413,59375 s lors du dernier contrôle. Un check
du nouvel exemple est lancé séparément, session 52824 :
`cargo check --locked --offline -p forge_engine --example tidewater_plant_probe -j 1`.
Il attend le lock du même répertoire de build ; aucun job n'est redémarré ou tué.
Les nouveaux raccords de types/ressources, le publisher et l'adoption n'ont donc
pas encore de verdict Cargo ni d'exécution native. Restent également la soumission
du stand au graphe d'ombres, l'intégration Studio, les captures réelles, les modes
sans passe temporelle et le paquet actualisé. Objectif complet toujours ouvert.

## Géométrie projetée dans les ombres native et Studio

Vérification de la référence locale : `src/game/FishStand.js` construit le mesh
`FishStandStall` et définit `mesh.castShadow = true`. Son shader complet ne fait
pas de discard ; la géométrie rigide source appartient donc au lot opaque.
L'ancien probe chargeait les casters village et Debris, mais ne soumettait pas
ce mesh supplémentaire.

`OpaqueDraw::from_mesh` fournit maintenant une préparation commune pour un mesh
TriangleList, avec positions Float32x3 et indices source conservés (U16 élargis
en U32, ou triangles non indexés). La validation existante refuse coordonnées,
indices, cardinalité et matrices invalides. Un test de conservation des octets,
du winding et de la pose, avec trois refus de géométrie invalide, est écrit mais
pas encore exécuté par Cargo.

Le probe ajoute le stand aux `AdditionalOpaqueDraws` AVANT l'installation du
graphe d'ombres, en conservant la contribution Debris existante. Le caster et
le rendu visible partagent désormais le même helper de pose. Les coordonnées
JSON originales restent en f64 dans `FishStandPrepared.height_sample_xz` pour
l'échantillonnage du relief, avant leur projection f32 dans le transform GPU ;
cela évite un arrondi des coordonnées avant de calculer la hauteur source.

Dans Studio, le mesh du stand porte le contrat de couverture générique
`scene::OpaqueCaster`. Le collecteur commun inclut ces meshes sans décision sur
un nom, slug, tag de module ou identité d'asset. Il utilise les transforms locaux
et toute l'ascendance actuelle, avec déduplication et ordre stable des entités
marquées. `buildings` réutilise la même préparation de géométrie. Le publisher
Studio passe après la projection du composite afin de suivre ses transformations,
duplications et suppressions dans le même cycle d'édition ; les documents
`ManualPlacement` et leurs outils communs ne sont pas remplacés. Un test de
collecte, retouche parent, retrait/rétablissement du contrat et suppression de
la racine est écrit ; il n'a pas encore de verdict Cargo ni de preuve UI.

Rustfmt et diff whitespace ciblés PASS. Aucun pixel nouveau ni paquet actualisé
n'est revendiqué. Les sessions 97287 et 52824 sont toujours suivies. Inspection
read-only du processus 43096 : son enfant 10676 est bien le linker MSVC,
démarré à 16:01:13 et utilisant le response file du même target natif. CPU et
mémoire du linker progressent ; aucun redémarrage sur timeout ni interruption
d'un autre build. Le check de l'exemple attend encore le lock. Les nouvelles
préparations, marqueurs et tests attendent la compilation complète puis les
captures native et Studio.

## Dessin GPU du receiver contact publié — V2

`VerifyRenderContactGpuV2.mjs` termine code 0, avec 32 dessins/readbacks GPU :
huit cas de 256 pixels, chacun comparant le hook source, l'adapter uniforme,
le storage de 272 octets et ce storage avec une ancienne lumière volontairement
incorrecte. Les quatre sorties sont identiques octet par octet dans chaque cas.
Le receiver et le getter sont extraits du shader de production assemblé V5 ;
ses inputs sont vérifiés par SHA avant le test. Shader assemblé SHA
`2a40778692cad334ef976691586f1ae1a124778b175fd05b37f43dba13d5bae5`,
receiver SHA `fd8ac5069cdfbcc89cf59f3641c56b4c8549783130f9bbc9f33ffa0761a17a88`.
Outputs conservés dans `.tidewater-integration/render-contact-gpu-20261006-v2`
à la racine E:/OpenRPG_Forge.

Les cas couvrent profondeur libre, occlusion proche, épaisseur rejetée,
distance lointaine, suspension force 0, force 0.5, changement time/night et
lumière éteinte. Le chemin live suit bien la lumière commune malgré les données
périmées dans le cadre CPU. La V1 a échoué à la concaténation du harness
(`}#ifndef` sans newline) ; son failure.json est conservé. V2 ajoute uniquement
la séparation des morceaux WGSL. Aucun shader produit n'a été changé pour
corriger ce défaut du harness.

Ce test utilise de vrais buffers, textures depth et readbacks GPU, avec caméra,
profondeur et lumière synthétiques. Les octets sont construits indépendamment
en JavaScript : il ne qualifie ni la sérialisation ShaderType Rust, ni le
publisher Bevy, ni un historique de caméra native, ni les pixels de l'île.

Le probe `temporal_owned` exige désormais une observation contact quand un
stand est demandé. Chaque sample contrôle le même FrameTag et l'absence
d'erreur ; les frames seed doivent constater la suspension explicite, les
autres un historique réellement apparié. Les receipts JSON enregistrent ce
verdict sans confondre la suspension avec des ombres actives. Le suivi de
l'historique caméra continue à avancer sur un véritable commit temporel,
même si le contact échoue, afin de ne pas créer artificiellement une autre
erreur d'historique. Ce raccord reste écrit, non exécuté dans Bevy, en attente
du check natif ; aucune nouvelle qualification native ou Studio revendiquée.

## Adoption du matériau complet dans Studio — écriture en cours de qualification

La session Cargo 97287 termine finalement code 0 après 118m02s : deux anciens
tests `contact_frame` PASS. Elle avait compilé les sources antérieures au
publisher, à la suspension et aux casters ajoutés ; ce résultat ne couvre pas
ces nouveaux raccords. Le check de l'exemple natif 52824 a commencé à compiler
les dépendances après libération du verrou, sans verdict terminal à ce stade.

`fish_stand_scene` conserve son `ManualPlacement`, ses enfants sélectionnables,
ses colliders et le caster commun, mais son mesh utilise désormais `FullMaterial`.
Le module `fish_stand_scene/full.rs` compose le shader à partir du `village.wgsl`
du paquet vérifié et copie les bindings d'un matériau village effectivement
résident de la génération importée. Last actualise les receivers, le WindFrame
commun, le night du ciel et la profondeur à la taille de la caméra active.
Les tableaux de textures et le mesh du stand ne sont pas reconstruits lors
d'une retouche de transform. La famille temporelle `FullStall` est enregistrée
dans le chemin SourceResolve existant. Aucun document canonique remplacé.

Le publisher peut être installé avant que les inputs asynchrones soient
résidents (`register_deferred`). Leur absence publie un cadre explicitement
inactif avec erreur observable. L'extraction enlève également le resource du
RenderWorld quand il disparaît du MainWorld : vérification dans Bevy 0.19.1,
le plugin ExtractResource générique conserve autrement l'ancien resource.
Le retrait du paquet invalide donc les inputs, sans conserver une ancienne
caméra/animation. La copie opaque refuse aussi une texture sans COPY_SRC ;
la caméra du stand reçoit cet usage en conservant ses usages existants.

Rustfmt ciblé et diff whitespace PASS. Check Studio WASM lancé, session 27367 :
`cargo +1.98.0 check --locked --offline -p forge_studio --lib --no-default-features
--features web --target wasm32-unknown-unknown --target-dir
F:/OpenRPG_Forge/relocated-work/Rust-target -j 1`.
Il attend le build externe déjà actif dans ce même target, sans interruption
ou relance de celui-ci. L'adoption Studio n'a pas encore de verdict de types,
de dessin WebGPU ni de capture UI. Hors SourceResolve (édition, Off, BevyTaa),
un ViewPacket/historique approprié n'est pas garanti ; les contacts peuvent
être inactifs avec erreur observable. Cette réserve reste à résoudre avant
parité complète et avant nouvelle distribution.

## Régression d'extraction et suivi des compilations

Le test `extraction_retires_removed_scene_inputs_and_accepts_the_next_scene`
exécute le véritable système d'extraction dans un schedule Bevy : scène initiale,
retrait du resource MainWorld, puis nouvelle scène avec time/night/index distincts.
Il doit constater l'absence des anciens inputs RenderWorld après retrait et les
nouvelles valeurs après réinsertion. Ce test est écrit, pas encore exécuté.
La suite native filtrée `tidewater_stall::contact_` est lancée session 17586
(même target F:/OpenRPG_Forge/.build/cargo-target) pour couvrir cadre, suspension,
ABI storage et extraction ; elle attend le check 52824 encore vivant.
Ce dernier a atteint `Checking forge_engine` après forge_assets ; pas de verdict
terminal à ce contrôle. Le check Studio 27367 attend toujours son target WASM.
Ces jobs ne sont pas redémarrés sur leur attente. Rustfmt et whitespace ciblés
PASS ; ces contrôles ne remplacent pas les tests de types ni les dessins Bevy.

## Préparation contact indépendante du mode temporel

`contact_frame::CameraFrame` contient maintenant seulement les métadonnées
nécessaires au receiver : tag, taille/epoch/seed, matrices actuelle rasterisée
et non jitterée, matrice précédente, position et near. `prepare_camera` et
`suspend_camera` n'exigent ni phase Halton, ni séquence ou commit de resolve.
Le chemin SourceResolve passe par une conversion explicite de son ViewPacket
vers ce contrat ; l'ABI Uniform reste identique et conserve sa taille attendue
de 256 octets. Les contrôles de profondeur restent stricts : même vue, serial
précédent, taille, epoch et matrice précédente réellement appariés.

Le nouveau test caméra construit ces données sans packet temporel, avec une
projection raster distincte de la projection non jitterée et une pose déplacée.
Il vérifie que les paramètres envoyés conservent cette projection et cette
pose, ainsi que la suspension après reset. Il est écrit, pas encore exécuté.
La suite 17586 déjà lancée attend le même target natif et doit compiler la
génération actuelle des tests ; aucun build relancé simplement pour une attente.
Le raccord RenderWorld de ce contrat pour édition/Off/BevyTaa reste à écrire :
ce refactoring ne prétend pas activer ces contacts, ni produire de nouveaux
pixels. Les sessions de check 52824 et 27367 sont encore suivies vivantes.

## Raccord caméra hors resolve source et adoption native par défaut

Le check 52824 termine code 0, `Finished dev`, après 59m51s. Il couvre
l'exemple et les raccords présents lors de sa compilation, avec deux warnings
(cache incrémental par copie et fonction de probe inutilisée). Il précède
le nouveau module `contact_camera` et l'adoption native par défaut ci-dessous ;
il ne les qualifie pas.

Le nouveau module prépare un `ContactView` uniquement en RenderWorld, à partir
des véritables ExtractedView/ExtractedCamera/MainEntity. Il reprend l'ordre
de calcul de Bevy 0.19.1 pour projection raster et jitter, sans insérer un
ViewPacket ni avancer une phase Halton. Une vue source déjà taguée conserve
son chemin existant. Le chemin indépendant refuse plusieurs caméras, une
capture de réfraction, une taille/viewport incompatibles et une projection
hors convention perspective reverse-Z. Near est reconstruit depuis la
projection, pour la forme infinie et la forme finie reverse-Z.

Le compteur de préparation rend les copies sautées obsolètes ; changement de
vue, taille ou interruption de copie provoquent un seed explicite. Une receipt
indépendante, distincte de la receipt source temporelle, est publiée seulement
après encodage de la même copie opaque. Une copie source retire la receipt
indépendante. Le publisher choisit la receipt correspondant à l'origine de la
caméra ; le probe SourceResolve exige désormais `source_owned=true`, pour ne
pas accepter accidentellement le chemin indépendant comme preuve de resolve.

L'installation `full_stall` native est écrite hors du switch TEMPORAL_AUX :
le matériau complet est donc demandé aussi dans le parcours natif ordinaire.
La caméra conserve ses usages et reçoit COPY_SRC/TEXTURE_BINDING pour cette
profondeur. Aucun script de launcher ou paquet déjà construit n'est modifié
par cette écriture. Pas de dessin ou d'exécutable actualisé revendiqué.

Rustfmt et whitespace ciblés PASS. La suite 17586 est toujours suivie, désormais
en compilation du moteur, mais sa génération précède une partie des changements
de ce tour. Un nouveau check de l'exemple, justifié par ces nouveaux changements,
est lancé session 74324 dans le même target natif. Le check Studio 27367 demeure
actif. Le raccord doit encore recevoir verdicts Cargo, tests de lifecycle,
captures native/Studio et comparaison source avant qualification.

Le test de cycle de vie du nouveau ContactView exécute le système réel dans
un schedule ECS : seed initial, copie précédente simulée, frame suivante
appariée, copie sautée, deux caméras ambiguës et capture de réfraction. Il
vérifie les seeds/epochs et l'absence de ViewPacket ajouté. Ce sont des
métadonnées de test ; la copie simulée ne prouve pas un encodage GPU.
Le test est écrit, pas encore exécuté dans la génération actuelle. La lecture
de prepare_view_uniforms Bevy confirme que le VP non jitteré reste projection
* view. Un override raster divergent est donc refusé explicitement par ce
chemin, pour ne pas rapprocher une profondeur d'une projection différente.

## Vérification GPU avec caméra déplacée et contrôle périmé

Les scripts `VerifyRenderContactGpuV3.mjs` puis V4 terminent code 0. V3 ajoute
aux huit cas précédents une translation caméra/world, un VP raster jitteré,
un seed dans cette pose et le near source 0.06 m. Les treize cas conservent
les sorties source/uniforme/storage exactes. V4 conserve ces cas et ajoute
trois contrôles volontairement faux : les positions du monde sont déplacées,
mais le buffer conserve l'ancienne caméra. Ces trois contrôles deviennent
entièrement éclairés (visibilité 1) là où la référence correctement appariée
est entièrement occluse (0). Le fixture détecte donc une caméra périmée.

`AuditRenderContactGpuV4.mjs` relit et vérifie les 55 fichiers GPU de 4096 octets,
leurs SHA et leurs 256 pixels chacun. PASS : 52 comparaisons positives, treize
cas multipliés par les quatre variantes source/uniforme/render/render-ancienne
lumière ; trois contrôles négatifs distincts. Le champ historique
`all_variants_byte_identical` concerne ces quatre variantes de comparaison.
`audit.json` nomme explicitement le groupe positif et les contrôles négatifs
exclus de ce groupe, pour éviter de prétendre que la caméra périmée concorde.
SHA du result.json audité :
`22c1d106974a7a42a97bb6916869648b8e0b97b0c411dacbb08a5d3b5be60669`.
Outputs : `.tidewater-integration/render-contact-gpu-20261006-v3` et `v4`
à la racine E:/OpenRPG_Forge. Shader assemblé V5 et receiver restent inchangés.

Limites conservées : matrices et octets construits dans le harness JavaScript,
profondeur synthétique, aucune preuve de sérialisation Rust, publication
RenderWorld, copie de scène native ou capture Studio. Aucun rendu complet de
l'île ni paquet distribuable promu par ces contrôles isolés.

Inspection read-only des command lines : 10100 est bien le rustc enfant du
Cargo 36704 de la suite contact 17586, target natif .build/cargo-target.
Le check 74324 attend ce target. Le target relocated-work/Rust-target est
actuellement occupé par le test externe `normal_binding_tests` (Cargo 34668,
rustc 41132 compilant thiserror_impl), et non par le précédent check WASM.
Le check Studio 27367 attend ce travail externe ; aucun de ces processus
n'est interrompu ou relancé pour une attente. Pas encore de verdict terminal
pour ces trois validations suivies.

## Écart restant sur les cinq poissons natifs de l'étal

Lecture du consommateur actuel `tidewater_plant_probe/fish_stand.rs` : ses cinq
poissons utilisent encore StandardMaterial avec couleur par espèce, métallique
0.15 et roughness 0.32. Cela ne consomme ni skin source, ni pattern/seed/flags,
ni les attributs REST_POSITION/FISH_DATA du matériau dédié. Le mesh du stand
complet ne corrige donc pas automatiquement les poissons exposés.

`AuditNativeStandFishInputsV1.py` termine code 0 : les deux paquets canoniques
GameFish et FishProps concordent avec leurs SHA épinglés, la révision source
et la référence material_source_package_sha256. Les 498 payloads (7 245 956
octets) passent taille et SHA ; la table skin fait bien 168 vec4 (2688 octets).
Le paquet GameFish contient 23 enregistrements, dont exactement cinq poissons
d'étal non garés dans le catch pool. Le reçu conserve leurs poses et paramètres
GPU f32 capturés, ainsi que le SHA du consommateur natif encore simplifié.
Output : `.tidewater-integration/native-stand-fish-input-audit-20261006-v1/result.json`.

Les lecteurs et préparations moteur déjà présents permettent de reprendre
`PreparedFishProps::from_game_package`, FishMaterial, la table skin, les bindings
communs, les LOD source et la sélection d'ombres masquées. Ce raccord natif reste
à réaliser et à dessiner ; l'audit Python ne prétend pas exécuter les lecteurs
Rust ni qualifier un matériau Bevy. Le packaging actuel atteste son bundle
scene/data existant : ces nouveaux sidecars devront être copiés et attestés
séparément, sans retoucher silencieusement ce bundle. Aucun paquet publié ou
profil utilisateur modifié par cet audit.

## Verdicts Cargo et reprise des validations

Les handles suivis donnent désormais leurs verdicts terminaux :

- 74324 : check natif de `tidewater_plant_probe` PASS, code 0, 46m11s.
- 27367 : check Studio lib Web/WASM PASS, code 0, 117m36s.
- 17586 : tests contact non exécutés, compilation FAIL, code 1 :
  `contact_camera.rs:190` utilisait un booléen pour `msaa_writeback`,
  alors que Bevy 0.19.1 exige `MsaaWriteback`.

L'inspection du fichier courant trouve déjà `bevy::camera::MsaaWriteback::Off` ;
la génération ayant échoué ne valide donc pas ce fichier actuel. Cette correction
existante est conservée. Le raccord Studio `full.rs` remplace l'affectation via
`RefCell::replace`, dont l'ancien `Result` était ignoré, par une affectation via
`borrow_mut` ; l'erreur reste publiée dans READY et dans le rapport de placements.
Rustfmt ciblé et diff whitespace PASS.

Suite contact relancée sur cette génération : session 74561. Recheck Studio après
la correction de l'avertissement : session 74174. Build de l'exemple natif pour
permettre une prochaine capture du matériau complet : session 17184. Ces nouvelles
commandes n'ont pas encore de verdict terminal ; les builds attendent les verrous
des travaux déjà actifs sans interruption de ceux-ci.

Les deux checks terminés prouvent le typage des parcours intégrés natif et Web,
pas leur rendu GPU, les tests de lifecycle, la comparaison de l'île ou un nouveau
paquet distribuable. Les cinq poissons natifs restent à raccorder au matériau
source ; aucune adoption ni capture supplémentaire n'est revendiquée ici.

## Récepteurs courants des matériaux poisson dans Studio

L'inspection complémentaire distingue les deux consommateurs : le LOD d'étal
actualisait temps/frame/fade ; `fish_props_scene` recopiait déjà une partie de
l'éclairage sur tous les FishMaterial lorsqu'il était actif. Cette copie ne
transportait cependant pas `live_shadows`, `scene_buffer`, `local_lights` ni
la sélection `shared_scene_lighting`. Il serait donc inexact d'affirmer que
tous les poissons conservaient systématiquement tout l'éclairage initial.

`FishMaterial::update_scene_receivers` reprend désormais les ressources du
VillageMaterial résident : lumière, environnement, terrain, cascades d'objets,
buffers dynamiques, nuages, caustiques et rebond. Il valide SceneLighting avant
toute mutation et conserve skin, info, flags, LOD et compteur de frame.
Le LOD d'étal utilise ce raccord pour ses copies privées ; un inventaire vide
ne demande pas de ressource d'éclairage. `fish_props_scene` utilise la même
méthode à la place de sa copie partielle, afin qu'un autre système ne rétablisse
pas ensuite des bindings différents sur les poissons de l'étal.

Le test existant des 78 instances source est étendu : remplacement réel de
l'environnement et de l'éclairage, conservation des paramètres propres au
poisson/fade, puis refus d'une lumière NaN sans mutation des bindings existants.
Session 41644 lancée avec le paquet FishProps canonique ; elle attend le target
natif. Les sessions 74561 (tests contact), 74174 (check Studio) et 17184 (build
natif) restent actives au dernier poll ; 74561 indique compilation de forge_world.
Rustfmt et whitespace ciblés PASS. Pas encore de verdict du test étendu ni de
capture de cette génération. Le raccord natif des cinq poissons, distinct de
cette correction du consommateur Studio, reste ouvert.

## Raccord natif source des poissons : code et restauration des entrées

Le nouveau module d'exemple `tidewater_plant_probe/source_fish.rs` charge les
sidecars GameFish et FishProps via leurs lecteurs Rust et pins canoniques.
Les deux variables de racines sont obligatoires ensemble ; les fichiers
résolus doivent rester sous leur paquet. Le préflight prépare exactement cinq
templates, leurs trois LOD, leur table skin et leur shader d'ombre masquée.
Le parcours historique sans ces variables demeure identifiable ; le script
Walk sélectionne explicitement les deux paquets source et vérifie leurs pins.

Quand ces entrées sont présentes, `fish_stand::setup` conserve Joe mais ne
publie plus les cinq StandardMaterial. Le nouveau PostStartup prépare les
matériaux dédiés depuis le VillageMaterial résident, les poses relatives au
stand capturé, dix enfants LOD visibles et cinq meshes masqués conservés pour
le graphe de cascades. Last applique les distances/fondus source et les
bindings courants ; il publie un `shadow::Frame` vers le vrai target de
cascades, ou retire ce frame quand celles-ci sont désactivées. Pas de mesh
opaque de remplacement pour les nageoires. `material::prepare_scene` valide
les mêmes paramètres source que `prepare`, via `source_settings`, puis utilise
les ressources résidentes réelles. Ces changements sont écrits, non qualifiés
par une compilation ou un dessin de cette génération à ce stade.

Une vérification des chemins découvre un changement externe au chantier :
les deux anciennes racines de paquets sont maintenant des jonctions vers le
catalogue `F:/OpenRPG_Forge/relocated-work/Tidewater/catalogs/group-0008-60e48581e71b`,
qui contient INDEX.json et LIRE.md, mais pas package.json. LIRE.md indique
l'archive tar.gz vérifiée et le script de restauration. Le script local
`.storage-audit/Restore-Tidewater-20261006.ps1` et son Python ont été lus :
contrôle des chemins, hashes des payloads et de l'archive, extraction dans un
nouveau dossier restored, puis remplacement de la seule jonction concernée.
Restauration GameFish lancée session 87751 ; encore active au dernier poll.
FishProps n'est pas encore restauré. Les anciens audits d'intégrité restent
datés et ne sont pas présentés comme vérification des nouvelles jonctions.

Rustfmt du nouveau module et whitespace ciblés terminent code 0 (session
68622). Le parse PowerShell de Walk PASS, mais son contrôle des manifestes
échoue actuellement faute de fichiers extraits. Nouveau check natif 5668
lancé pour ce raccord ; encore actif, sans verdict. Les sessions 74561,
74174, 17184 et 41644 restent suivies ; pas de relance sur simple attente.
Aucun nouveau binaire, capture ni paquet distribuable n'est revendiqué.

La restauration GameFish 87751 termine code 0 : archive SHA-256 vérifiée et
249 fichiers vérifiés ; la jonction originale est remplacée par le dossier
restauré. Une relecture indépendante de package.json retrouve le pin
`f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d`.
Restauration FishProps désormais lancée, session 98188, encore active au poll.

Le raccord natif publie un Receipt explicitement limité au MainWorld : pins,
identités des cinq sources, dix slots LOD, cinq meshes masqués, nombre de
préparations, publications d'entrée d'ombre et derniers temps/frame de scène.
Le walk smoke conserve ses contrôles existants et ajoute une condition : si
les sidecars source sont demandés, ce reçu doit montrer une préparation valide.
Le JSON contient `source_fish_requested`, `source_fish_preparation_pass` et
`source_fish`. Ni le compteur de préparation ni celui de publication ne sont
présentés comme acquittement d'encodage GPU, pixel readback ou parité visuelle.
Le test de matériau est aussi étendu à `prepare_scene`, incluant le refus de
la même lumière NaN. Ces derniers changements attendent leur validation Cargo.

Le contrôle complémentaire des entrées de Walk trouve également quatre racines
archivées, dont les jonctions n'exposent actuellement que INDEX.json et LIRE.md :
`preview-scene-bundle-fish-stand-content-fish-011` (catalogue group-0024),
`debris-native-input-20261005-v1` (group-0022),
`debris-scanned-package-20261005-v1` (group-0027) et
`debris-scan-instance-program-20261005-v1` (group-0003).
Il faut restaurer ces seules entrées actives via le script vérifié avant le
prochain lancement natif ; leur présence comme dossiers n'est pas une preuve
de disponibilité des assets. Aucune réacquisition ou réexport aléatoire ne
remplace les contenus capturés. La restauration FishProps 98188 reste active.
Le formatage et whitespace du reçu natif terminent code 0 (74878).

La lecture CIM bornée confirme les processus actuels : restauration FishProps
Python 40040 (commande source exacte, 35 733 504 octets résidents), Cargo natif
37568 pour les tests contact, Cargo Web 38312 pour Studio, Cargo build 27208,
Cargo test matériau 42316 et Cargo check exemple 40168. Les autres commandes
n'ont pas été interrompues. Le dernier poll Studio annonce vérification de
forge_engine ; aucun verdict terminal supplémentaire n'est encore disponible.

FishProps 98188 termine ensuite code 0 : archive SHA-256 vérifiée et 251 fichiers
vérifiés ; sa jonction originale dessert désormais les fichiers restaurés.
Restauration séquentielle du bundle de l'île et des trois entrées de débris
lancée dans la session 27725, via le même script audité et les quatre sources
originales explicitement bornées. Cette session n'a pas encore de verdict.

## Raccord de distribution des sidecars poisson

Le launcher courant est bien le prototype Rust/Tauri qui embarque l'interface
`launcher-aube` du port 8848, pas l'ancien projet Godot. Ses trois fichiers déjà
modifiés sont préservés. `play.rs` exige désormais les deux manifestes dans
`content/fish/game` et `content/fish/surface` et fixe les deux variables runtime
depuis le dossier extrait. Le test existant de distribution déplacée avec espaces
inclut ces deux variables. `main.rs` expose le marqueur de production
`--verify-distributable --source-fish-runtime` / `FORGE_LAUNCHER_SOURCE_FISH_RUNTIME_V1`.
README précise que le binaire et les ZIP existants ne sont pas actualisés.

Build-WindowsPlaytest exige les deux racines, leurs pins et le nouveau marqueur
du launcher. La promenade associée au hash du runtime doit aussi contenir un
reçu de préparation source valide : cinq identités, dix slots LOD, cinq proxies
masqués et au moins une préparation. Les deux paquets sont copiés hors de data,
avec contrôle du nombre et des hashes de tous les fichiers, puis inclus dans
le manifeste final avec un scope explicitement limité au MainWorld.
Verify-NativeDebrisWalk ajoute `-WithSourceFish`, les racines de développement
ou de distribution, leurs pins avant/après et une condition de refus si le
runtime n'annonce pas cette préparation. Les variables héritées sont restaurées
en finally comme auparavant. Une ancienne preuve sans poisson source est refusée
par l'assembleur ; ceci ne qualifie toujours pas l'apparence ou le paquet extrait.

Parse PowerShell PASS pour les trois scripts et whitespace ciblés PASS.
Rustfmt/whitespace ciblés du launcher terminent code 0 (74344). Test launcher
`cargo test --locked --offline --manifest-path prototypes/forge_launcher/Cargo.toml
--all-features play::tests -j1` lancé session 80850, en attente de verrou au premier
résultat. Aucun nouveau launcher de production ni distribution n'est produit.

`TestPackagingSourceFishGuardsV1.ps1` termine code 0 : trois contrôles négatifs
appellent l'assembleur réel et obtiennent exactement les refus attendus pour
une promenade ancienne sans les nouveaux champs, un reçu poisson null et un
pin GameFish incorrect. Chaque dossier de paquet reste absent. Les fichiers
binaires/images de ces fixtures sont explicitement non exécutables et marqués
synthétiques ; aucune exécution native ou preuve positive n'est produite.
Résultat : `.tidewater-integration/packaging-source-fish-negative-20261006-v1/result.json`
à la racine E:/OpenRPG_Forge. Ce contrôle qualifie ces trois refus seulement,
pas la copie complète, le launcher de production, le GPU ou un ZIP extrait.

## Inventaire natif complet des poissons décoratifs

La lecture des consommateurs révèle que le raccord natif ne préparait que les
cinq poissons de l'étal. `prepare-village-probe.py` conserve explicitement la
limitation `village_fish_props_missing` : les poissons instanciés ne font pas
partie des maillages statiques du village. `export-fish-props.mjs` capture
séparément les 78 enregistrements du batch `village_fish`, avec contrôle des
géométries et enregistrements contre la référence intacte. Cette séparation
justifie le raccord natif sans ajouter de copies des maillages statiques.

`source_fish.rs` prépare désormais les 78 templates FishProps en plus des cinq
GameFish affichés. Les premiers conservent leurs poses mondiales source ; les
seconds restent enfants du véritable étal avec conversion depuis sa pose
capturée. Les 18 poissons du pool de prises restent exclus de cet inventaire
statique. Les 83 instances utilisent le matériau source, deux slots visuels
chacune, le LOD de profondeur masqué, la lumière commune et le même producteur
d'entrée de profondeur. Les limites GPU existantes acceptent cet inventaire
(4096 poses maximum et 256 MiB de géométrie masquée), sous réserve des tailles
effectives et de l'exécution à venir.

Le reçu MainWorld conserve les cinq identités GameFish et ajoute
`village_source_ids` pour les 78 identités FishProps. Sa validation exige les
ordinaux exacts et ordonnés, 166 slots visuels et 83 géométries masquées.
L'assembleur de distribution vérifie aussi les identités exactes : un reçu
ancien limité à l'étal ou contenant un doublon est refusé.

Le premier essai de contrôles négatifs v2 échoue sur une apostrophe typographique
interprétée comme délimiteur PowerShell dans un nouveau message. Le message est
corrigé, puis le parse et rustfmt --check terminent code 0. Le nouvel essai v3
termine code 0 : cinq refus attendus, dont le reçu limité à l'étal et le doublon
d'identité du village ; aucun dossier de distribution créé. Son résultat relu
est `.tidewater-integration/packaging-source-fish-negative-20261006-v3/result.json`
avec SHA de l'assembleur
`c16d1c90f4400319e4295ac281f4c6b35b538e4e6a9c693db62eae332621ce11`.
Les fixtures restent synthétiques, sans exécution de binaire ni preuve GPU.

L'audit indépendant `.tidewater-integration/audit-native-fish-inventory-v1.py`
est lancé en session 22489 pour relire tous les payloads restaurés, les records
binaires, les identités sélectionnées et les limites des géométries d'ombre.
Il n'a pas encore de verdict au dernier poll. Les validations Cargo natives
74561, 17184, 41644 et 5668 restent actives ; aucune qualification de ces
dernières modifications ni nouveau runtime n'est revendiquée. Le check Web
74174 atteint forge_studio et émet deux warnings, sans verdict terminal. Les
sessions launcher 80850 et restauration 27725 restent également actives.

L'audit 22489 termine ensuite code 0 : 498 payloads, 7 245 956 octets vérifiés
contre les manifestes épinglés après restauration ; les 83 identités sélectionnées
sont uniques et les records JSON correspondent bit à bit aux streams float32.
Les géométries masquées sélectionnées représentent 1 448 376 octets après
interleaving position/data et conversion des indices uint16 en uint32, sous la
limite du batch. Résultat local :
`.tidewater-integration/native-fish-inventory-audit-20261006-v1.json`.
Cette preuve porte sur les inputs source et leurs bornes, pas sur l'exécution
de l'adapter Rust, le GPU ou l'apparence de la scène. Whitespace ciblé PASS.

## Check Web terminal et préparation du prochain parcours complet

La session 74174 termine code 0 en 95m25 :
`cargo +1.98.0 check --locked --offline -p forge_studio --lib --no-default-features
--features web --target wasm32-unknown-unknown --target-dir
F:/OpenRPG_Forge/relocated-work/Rust-target -j1`.
Elle annonce trois warnings Studio et une incompatibilité future de
proc-macro-error2. Ce verdict qualifie le check de compilation exécuté, pas un
rendu Studio ni les modifications ultérieures du parcours natif.

La restauration 27725 termine sa première entrée : le bundle de l'île expose
de nouveau ses payloads, avec archive SHA et 1656 fichiers vérifiés. Les trois
entrées de débris suivantes sont encore en cours. La lecture CIM retrouve
rustc 15896, enfant du Cargo contact 37568, en compilation de forge_engine lib
test ; les autres commandes natives sont encore présentes. Aucune attente
observée n'est traitée comme échec ni redémarrée. Le transfert de stockage
externe et les autres compilations ne sont pas interrompus.

Le programme des cailloux était lui aussi une jonction de catalogue sans
payloads. Restauration bornée 58199 terminée code 0 (deux fichiers vérifiés),
puis relecture indépendante des deux pins : program.json
`0f6ea25a3fdcc20371241db9729e2b58b39f7e8e29500f064da5ffc3a92b3201`
et pebbles.wgsl
`6c9e1f3659f384fe58d33405713ae185ae334db1d1ca7d3e620f8d690def557b`.

Le script Walk-Tidewater ne transmettait pas explicitement cette racine, à la
différence du launcher et du vérificateur avec -WithPebbles. Il exige désormais
les deux pins et active FORGE_TIDEWATER_PEBBLE_PROGRAM_ROOT, avec restauration
de la variable héritée en finally. Parse PowerShell et whitespace terminent
code 0 (60568). Aucun nouveau parcours exécuté n'est revendiqué.

La lecture du graph confirme le filtrage de la cible Fish Frame, la préparation
du batch masqué, les poses/horloges suivies, la sélection des draw.enabled et
les commandes draw_indexed dans les cascades. Elle ne prouve pas leur exécution
GPU. Le parcours natif refuse désormais des paquets poisson explicitement
sélectionnés lorsque le graph partagé de profondeur n'est pas installé, comme
il le faisait déjà pour les débris. La préparation seule ne peut ainsi pas
masquer silencieusement l'absence de cette passe dans un mode diagnostic.

Rustfmt --check du fichier principal signale ensuite uniquement l'ordre du
nouveau module source_fish. Rustfmt limité au fichier (skip_children=true)
applique ce tri ; son contrôle séparé termine code 0. Les derniers polls des
six sessions natives/launcher/restauration restantes sont encore actifs, sans
autre verdict terminal. Le build/check natif doit valider ces dernières sources.

## Même contrat poisson pour la promenade et la distribution

La lecture de Verify-NativeDebrisWalk révèle une différence avec le packaging :
son verdict poisson ne contrôlait que source_fish_requested et
source_fish_preparation_pass. Un ancien reçu à cinq poissons pourrait donc y
passer malgré le refus ultérieur de l'assembleur. Le contrat de préparation est
extrait dans `scripts/tidewater/Assert-SourceFishProof.ps1`, appelé par les deux
consommateurs. Il conserve les champs obligatoires, les deux pins, les 83
identités exactes, les 166 slots visuels, les 83 géométries masquées et le
compteur de préparation positif. Le vérificateur conserve désormais une erreur
source_fish_preparation_error et refuse son verdict lorsque ce contrat échoue.
Les contrôles existants du binaire, des inputs, de l'image et des cailloux sont
préservés. Il s'agit toujours de préparation MainWorld, jamais de preuve GPU.

Les trois scripts parsés et whitespace ciblé terminent code 0. Les cinq tests
de refus contre le véritable assembleur repassent code 0 en v4, sans exécution
de leurs fixtures ni création de distribution. Résultat relu :
`.tidewater-integration/packaging-source-fish-negative-20261006-v4/result.json`,
SHA de l'assembleur
`4a37d7393c7ffc83046ca0a66e4c783e2afb8b8cff8ead54ef631a99c280c050`.

Un essai séparé du contrat partagé utilise les identités lues dans les paquets
restaurés et un reçu de préparation explicitement synthétique. Le reçu complet
est accepté ; un doublon GameFish est refusé avec le message attendu. Résultat
code 0, relu dans
`.tidewater-integration/source-fish-proof-contract-test-20261006-v1.json`, SHA
de l'assertion
`0192938acca0ee9503397b70124197d1b1e1cdd74fdd650a727903b22948ec0c`.
Ce test ne simule ni dessin, ni terminaison GPU, ni parité visuelle. Les six
sessions restantes sont encore actives au dernier poll ; aucun nouveau build,
parcours natif ou paquet extrait n'est présenté comme validé.

## Observation des portées d'ombre des arbres dans le prochain parcours natif

Le paquet de végétation restauré conserve les ranges source des géométries
proches : palmiers 0/120/120.01 m, bananiers 0/100/120 m, monsteras et broadleaf
0/85/105 m, canopée 0/65/65.01 m. Les rayons CPU sont respectivement 130, 130,
115, 115 et 75 m. Le groupe understory n'est pas un caster dans l'adapter
source. Ces seuils de géométrie/dither ne sont pas une mesure de la distance
d'ombre visible ni une justification d'un arbre voisin sans ombre.

Studio expose déjà SelectionObservation des streams CPU, mais le reçu natif
n'indiquait pas cette sélection. Le Source du probe ajoute select_observed
autour de la sélection existante : mêmes ids ordonnés et même limite, avec
nombre de candidats, soumis, rejets de capacité, range, distance maximale des
bases et nombre de bases au-delà de la fin de range. Le type partagé de Studio
est réutilisé ; aucune modification des seuils, masques ou géométries n'est
appliquée sur cette seule lecture. Les distances de diagnostic sont 3D, tandis
que la sélection source reste fondée sur XZ et ses qr2 ; elles ne doivent pas
être interprétées comme critère de sélection supplémentaire.

Le producteur natif Sources garde une observation typée avec index de frame et
caméra après réussite de Tracker::next. La sérialisation JSON ne se fait qu'au
reçu final de walk_smoke, champ plant_caster_selection. Il n'alloue pas de
tableau temporaire de distances ni de JSON à chaque frame. Le scope précise
qu'il s'agit des streams CPU fournis au frame vivant, pas de couverture GPU.
Ces diagnostics permettront de distinguer un culling/rejet de capacité d'un
problème de passe, cascade ou récepteur sur la capture suivante.

Rustfmt ciblé et whitespace terminent code 0. Ces nouveaux raccords attendent
le check/build natif déjà en file ; aucun résultat d'observation en jeu n'est
encore revendiqué. Les six sessions suivies restent actives au dernier poll.

## Déverrouillage du contrôle launcher et tests de commandes réels

La lecture de configuration trouve CARGO_TARGET_DIR hérité à
`F:/OpenRPG_Forge/.build/cargo-target`, également utilisé par le test launcher
80850. Il attend donc le même verrou que les commandes Engine malgré son crate
indépendant. Il n'existe pas de cache launcher séparé dans le checkout.
Le rustc Engine 15896 est toujours présent et son CPU cumulé progresse de
50.921875 à 54.1875 secondes entre deux observations ; il n'est pas déclaré
terminé ou relancé. Un échantillon de compteurs disque retrouve sept requêtes
en attente sur F:, avec environ 5 MB/s de lecture et 65 MB/s d'écriture ; ce
n'est pas un benchmark ni une attribution exclusive de toute la lenteur.

Un harnais local distinct importe les véritables src/lib.rs et src/play.rs
du launcher, avec serde/serde_json aux mêmes versions, sans mocks ni Tauri.
Son cache et son temp sont bornés sur E: ; les tests existants ne suppriment que
leur fixture unique dans ce temp. Lock offline de 11 paquets généré, puis
session 9965 terminée code 0 en 12.35 secondes : les deux vrais tests play
(dossier déplacé avec espaces et variables source, données requises) passent.
Les cinq inputs restent inchangés. Résultat relu :
`.tidewater-integration/launcher-contract-20261006-v1/result.json`, SHA play.rs
`8a3143af59c50cba7c8f9c4f338f8cbbb35d697aaa9df3ae903d035715fc057a`.
Cette preuve ne qualifie ni Tauri, ni WebView, ni le launcher de production,
ni l'exécution du jeu ou d'une distribution portable.

Le premier Stop-Process du Cargo launcher en attente retourne une erreur
NullReference malgré un message suivant et un code shell 0 : il n'est pas
traité comme arrêt acquis. Son identité est revérifiée par CIM (39040,
parent 36416, commande exacte et aucun compilateur enfant), puis Terminate
retourne 0 et l'absence du processus est vérifiée. La session 80850 ferme ensuite
sans sortie de tests ; son code shell 0 n'est PAS un verdict de tests réussis.
Cette interruption cible uniquement notre attente connue ; aucune compilation
Engine ni transfert externe n'est interrompu. Aucun rejet d'approbation n'a eu
lieu. La replanification répond au verrou partagé constaté, pas à un silence ou
à une expiration d'observation.

Le même test Tauri complet est lancé en 38080, avec --all-features play::tests,
locked/offline/j1 et le cache dédié
`E:/OpenRPG_Forge/.tidewater-integration/launcher-full-check-target-20261006-v1`.
E: expose environ 539.7 GB libres au contrôle préalable. Il compile ses
dépendances au dernier poll, sans verdict final. Les huit inputs sont capturés
durant cette compilation de dépendances dans
`.tidewater-integration/launcher-full-check-inputs-20261006-v1.json` ; leur
invariance devra être recontrôlée avant qualification. Les cinq sessions
natives/restauration précédentes restent actives. Aucun nouvel exécutable de
production ou ZIP n'est revendiqué.

## Tests Tauri terminés et candidat launcher avec interface embarquée

38080 termine finalement code 0 : compilation test en 9m29 puis deux tests play
PASS, trois tests bibliothèque filtrés et aucun ignoré. Le test de fixture de
fichiers prend 162.25 secondes ; TEMP/TMP hérités pointent eux aussi vers
`F:/OpenRPG_Forge/.tmp/codex`, ce qui reste une observation d'environnement et
non une preuve d'attribution exclusive de la durée. Les huit inputs suivis sont
inchangés après le terminal, vérification 59192 code 0. Reçu relu :
`.tidewater-integration/launcher-full-check-result-20261006-v1.json`, SHA de son
reçu d'inputs
`b6c7cd3b8f21144c0d94584205b4f9cc35d46f847b0a28cfc4baa1de2d58daad`.

Le script build-aube.mjs et ses chemins sont inspectés : dist est généré dans
le projet, garde de chemin et refus des liens, contrôle du titre Aube, copies
des assets et seul ajout desktop.js. La cible absolue dist est vérifiée dans le
workspace, sans ReparsePoint, avant son remplacement par ce script. Un premier
appel npm.cmd échoue car absent du PATH, sans exécuter le script. Le Node du
runtime fourni est ensuite localisé via load_workspace_dependencies et exécute
directement le même build-aube.mjs ; aucun paquet installé ou acquis.

FORGE_AUBE_SOURCE est fixé à E:/OpenRPG_Forge/launcher-aube, et TEMP/TMP du
nouveau build sont bornés à un dossier E: dédié. La construction frontend
termine AUBE_FRONTEND_READY, 68 images, source HTML SHA
`35a7a2d5b6ab088d4a539655c5112f0eabdfaec1586cf35a5c6f0be61c104c8b`.
Les sources et tous les 76 fichiers frontend générés sont épinglés avant build.
65962 termine code 0 : cargo build locked/offline/j1, features production,
profil dev, bin forge-launcher-prototype, même cache E: ; durée 32.23 secondes.

Le binaire est
`E:/OpenRPG_Forge/.tidewater-integration/launcher-full-check-target-20261006-v1/debug/forge-launcher-prototype.exe`,
236234240 octets, SHA
`d9e5e82b5b105cc149698956500b7ebe96d18def71253fa87e5dbed52c86e6b7`.
Les quatre commandes connues --verify-distributable, puis variantes debris,
pebble et source-fish retournent chacune le marqueur attendu et code 0. Les
inputs et le binaire restent inchangés. Résultat relu :
`.tidewater-integration/launcher-executable-result-20261006-v1.json`, SHA du
reçu d'inputs
`a94473932d062b091dc0e0e6c17c51333496e56e7dce7f0e879d5cbac51714a9`.

Il s'agit d'un candidat exécutable dev avec protocole/assets embarqués, pas
d'un build release optimisé ni d'une validation visuelle WebView. Le moteur
natif actualisé, sa promenade, la distribution complète et le lancement depuis
son extraction restent ouverts. Aucun ancien ZIP n'est actualisé ; ce launcher
seul n'est pas une île distribuable. Les cinq sessions natives/restauration
restent actives au dernier poll, sans redémarrage ni verdict terminal nouveau.

## Vérifier les vrais assets du contexte Tauri dans le candidat V2

La lecture des crates SDK installés confirme le chemin de features :
tauri/custom-protocol active tauri-macros/custom-protocol ; le générateur de
contexte choisit alors frontendDist et EmbeddedAssets plutôt que les assets
vides du mode dev avec dev_url. Cette inspection porte sur les versions locales
épinglées, sans preuve d'affichage WebView.

Le marqueur de disponibilité ne vérifiait auparavant que cfg production.
main.rs construit désormais un contexte unique par launcher_context, transmis
au lancement normal. La commande --verify-distributable lit dans ce véritable
contexte index.html, son titre Aube, la référence desktop.js et les scripts/
styles non vides. Elle refuse le marqueur si ces données embarquées manquent.
Un test production_context_contains_the_embedded_aube_frontend exerce ce même
contexte sans ouvrir de fenêtre. La disponibilité des données d'île reste
contrôlée séparément par play.rs ; le marqueur n'annonce pas une carte jouable.

49519 termine code 0 : compilation 14.24 secondes, six tests du binaire réellement
exécutés, zéro filtré/ignoré, tous PASS en 0.03 seconde. Les cinq tests existants
de commandes/état et le nouveau test des vrais assets passent ; TEMP/TMP sont
bornés sur E:. Rustfmt et whitespace du fichier modifié PASS.

Le candidat V1 est conservé bit à bit et vérifié dans
`.tidewater-integration/launcher-executable-retained-20261006-v1/forge-launcher-prototype.exe`
avant remplacement de l'output Cargo. Les inputs V2 sont épinglés dans un reçu
distinct. 45563 termine code 0 en 15.93 secondes pour la même construction
features production / profil dev. Les quatre marqueurs V2 passent ensuite
code 0, avec véritables assets de contexte vérifiés et inputs/binaire inchangés.
SHA du nouvel exécutable :
`67b1e3e317c82ddf3b296855680c4993cfe1d7e2be5daa8310f361d2d5a303c4`.
Reçu local : `.tidewater-integration/launcher-executable-result-20261006-v2.json`.
Il ne qualifie toujours pas l'affichage WebView, le runtime d'île, la copie
portable complète, la release optimisée ou les comparaisons visuelles.

## Candidat V3 : retirer les diagnostics herites au lancement portable

Inspection du raccord play.rs : seul STATIC_POSTFX etait retire. Les autres
variables FORGE_TIDEWATER_* pouvaient encore activer une capture de diagnostic,
modifier la camera ou desactiver les effets sous-marins dans le jeu portable.
Le raccord retire maintenant ces variables du processus enfant, avec comparaison
insensible a la casse pour Windows, puis applique les six racines/programmes
contenus dans la distribution. Il ne modifie pas l'environnement du launcher
ni les variables etrangeres a ce prefixe.

43609 termine code 0 : sept tests du vrai binaire launcher passent, zero filtre
ou ignore, compilation 13.40 s et tests 0.03 s. Le nouveau test exerce le
nettoyage de diagnostics en majuscules/minuscules et la preservation des autres
reglages, sans mutation globale de l'environnement. Les tests existants des
racines relocalisees et du contexte Aube embarque passent aussi. Rustfmt et
diff whitespace du fichier PASS.

V2 est conserve bit a bit et verifie avant reconstruction dans
`.tidewater-integration/launcher-executable-retained-20261006-v2/forge-launcher-prototype.exe`.
70250 termine code 0 : build locked/offline/features production, profil dev,
18.60 s, meme cache dedie E:. Les quatre marqueurs du candidat V3 passent avec
code 0 ; les inputs epingles et le binaire restent inchanges.
SHA executable : `156ecbef7cb3bb166c546cb01d1ba93306f2a3ff65bdd82e55fa2d25d17897bd`.
Recu : `.tidewater-integration/launcher-executable-result-20261006-v3.json`.
SHA recu inputs : `f37077a0f41a4f1c1e86cb01e0e61f89b3e2980857fcaa21480700aebe9ff901`.
Il ne qualifie ni l'affichage WebView, ni le moteur natif actualise, ni la copie
portable complete, ni la release optimisee. Les sessions natives/restauration
74561, 17184, 41644, 5668 et 27725 restent vivantes au dernier poll, sans verdict
terminal nouveau ni redemarrage.

## Reverification du 7 octobre : attentes et preuve scannee archivee

Les cinq sessions 74561, 17184, 41644, 5668 et 27725 sont repollees sur leurs
handles effectifs : chacune retourne encore un session_id, sans nouveau verdict
terminal. Cette attente ne motive aucun redemarrage. Les trois inputs debris
ne sont pas encore disponibles aux chemins de la prochaine promenade ; la
restauration reste en cours, et aucun essai du nouveau runtime n'est revendique.

Le rapport de preparation Studio scanne mentionne d'autres checks historiques.
Leurs handles 10967, 15095 et 24461 ne sont pas disponibles dans cette session
(Unknown process id), ce qui n'est pas une preuve de leur resultat. Le chemin
combined-scanned-candidate-20261006-v2 expose desormais INDEX.json et LIRE.md ;
son native-check-v1/result.json n'est pas accessible. Aucun PASS courant ni
adoption de ces overlays n'est deduit de la seule narration du rapport.
Les preuves archivees et le travail prepare sont preserves. Les validations
natives courantes restent la voie vers le prochain essai sur l'ile.

## Empreintes avant le build natif en attente du 7 octobre

Le diagnostic CIM en lecture seule confirme Cargo 27208 pour le build natif
17184, sans compilateur enfant, et rustc 15896 parent 37568 pour les tests
contact. Le compilateur a consomme 286.234375 secondes CPU au relevé, contre
les valeurs plus basses précédemment observées : aucune panne ou immobilité
n'est déduite du silence des logs. Le runtime au chemin cache reste celui du
6 octobre 12:49:48 et n'est pas utilisé comme nouveau candidat.

La capture native-walk-inputs-20261007-v1.json enregistre 1 738 fichiers et
97 634 803 octets : crates/apps/tools/examples/third_party/scripts/.cargo,
plus Cargo.toml, Cargo.lock et rust-toolchain.toml. Les dossiers target, .git
et node_modules sont exclus de la liste rg. Les chemins résolus doivent rester
dans le dépôt. Deux lectures SHA-256 de tous les fichiers concordent.
Cette capture intervient avant la compilation de l'exemple encore en attente
et devra être comparée après le terminal ; elle n'est pas un verdict de build,
de provenance complète des dépendances externes, de GPU ou de parité.

Le premier essai Python échoue WinError 5 en lançant rg comme subprocess ;
aucun reçu n'est créé par cet essai. La liste est ensuite produite par rg dans
le shell, avec code vérifié, puis le même hashage est effectué par Python.
85085 termine code 0 et conserve liste et reçu distincts. Aucun source de
production ni processus de compilation existant n'est modifié.

## Promenade native avec defaults graphiques explicites preparee

PrepareNativeWalkGraphics20261007.py extrait les 20 champs f32, les defaults
litteraux et leurs plages depuis le graphics.rs courant epingle dans le recu
native-walk-inputs-20261007-v1. Le profil complet est ecrit uniquement sous
native-walk-default-graphics-20261007-v1/local-appdata/OpenRPG Forge/Tidewater.
Exposition +0.4 EV, sharpen 0.2 et tous les autres defaults courants conserves.
Ce n'est pas une adoption des preferences utilisateur ni une declaration de
parite. La correspondance de schema/plages est un controle Python, pas encore
une deserialisation Rust ou un rendu.

La premiere extraction refuse le schema car l'expression des champs omettait
le mode multiline ; elle echoue avant creation du dossier. La correction borne
cette extraction au struct Values, exige 20 champs uniques et une egalite
exacte des noms entre declaration, defaults et validateurs. L'execution suivante
termine code 0, runtime_executed=false. Le profil et le recu sont conserves.

RunNativeWalkDefaults20261007.ps1 est prepare et passe le parseur PowerShell.
Il exige le hash du nouveau candidat, refuse explicitement le hash de l'ancien
exe en cache, controle les 1 738 fichiers de build et le profil avant/apres,
puis appelle le vrai Verify-NativeDebrisWalk avec WithPebbles et WithSourceFish.
LOCALAPPDATA est redirige seulement pendant cet essai et restaure dans finally.
Le profil utilisateur est observe par hash avant/apres, jamais modifie par le
runner. Le resultat de la promenade est conserve a part, avec visual_parity=false.

Le test negatif reel old-binary-guard.json termine PASS : l'ancien candidat est
refuse avant invocation du verifier, l'environnement reste identique et aucun
walk_output n'est cree. Il ne qualifie ni une promenade, ni l'affichage des
defaults, ni le nouveau moteur. Les compilations/restauration restent distinctes
et l'essai reel est conditionne a leur resultat terminal.

## Restauration complete et launcher release en cours

27725 termine code 0 : debris-native-input-20261005-v1, 4 910 fichiers verifies ;
debris-scanned-package-20261005-v1, 83 fichiers ; et programme scanne, 2 fichiers.
Chaque archive SHA-256 et les fichiers restaures sont verifies par le helper,
puis leurs jonctions d'origine publiees vers les seuls dossiers restaures.
Le bundle de scene avait deja termine sa restauration dans ce meme cycle.

Une relecture independante confirme les sept pins courants des metadata/programmes
Debris, Scanned, Pebble et GameFish/FishProps. Recu
restored-walk-input-pins-20261007-v1.json, pass=true. Ce controle ne qualifie pas
le GPU ni le dessin ; il termine le besoin de disponibilite de ces inputs pour
la prochaine promenade.

18491 lance le launcher optimise : cargo build release/locked/offline,
features production, bin forge-launcher-prototype, j1, dans le cache E: dedie
launcher-full-check-target-20261006-v1. TEMP/TMP restent bornes sur E:.
Les sources, configuration et 76 fichiers Aube embarques concordent avec les
inputs V3 avant lancement ; recu release distinct
launcher-executable-inputs-20261007-release-v1.json. Ce build a une sortie live
et compile ses dependances ; aucun executable release final ni marqueur PASS
n'est encore attribue. Le candidat debug V3 est preserve.

Le runner de promenade avec defaults conserve aussi le hash du runtime.log et
refuse un resultat global si le log rapporte des erreurs GPU wgpu/pipeline_cache
ou Validation Error. Le PASS borne du controleur reste distinct du verdict de
ce runner. La modification passe le parseur PowerShell mais n'est pas encore
exercee par un nouveau runtime. Les quatre validations natives 74561, 17184,
41644 et 5668 restent actives au dernier poll.

## Launcher release et vrai demarrage WebView : 7 octobre

18491 termine code 0, release optimise en 6m21. Les inputs suivis sont inchanges
et les quatre marqueurs de contenu embarque passent. Binaire 230 939 136 octets,
SHA 4bd71f2e514c33d07f8c12ebdd963622b66258da2049b6900fc2c0cc34935217.
Il est conserve bit a bit dans launcher-release-retained-20261007-v1 avant
les iterations du diagnostic. Recu launcher-executable-result-20261007-release-v1.
Les anciens recus n'inventorient pas la capability default.json ; leur garde
porte sur les fichiers effectivement listes, pas sur une provenance exhaustive.
Le nouvel inventaire V5 ajoute explicitement cette source de permissions.

Le binaire ajoute --verify-webview <recu-neuf>. Hors de ce mode, son etat de
diagnostic est inactif et la commande supplementaire refuse les appels. Dans
ce mode, profil WebView neuf sous le dossier de preuve, fenetre masquee,
controle de session et de la fenetre main, delai borne a 25 s et sortie JSON
sans remplacement d'une preuve existante. Le test charge l'interface reelle,
navigue vers Aventures et utilise le vrai Snapshot et le vrai raccord desktop.
Il ne demande aucun lancement de jeu et ne qualifie pas les pixels du launcher.

46750 : sept tests du vrai binaire passent, 0 filtre/ignore, 18.12 s de compile.
99361 : premiere release avec diagnostic compile, code 0 en 1m42 ; son test
WebView V1 expire. Le code shell 0 n'est PAS un PASS : webview.json porte false.
72636 : diagnostic de traces compile en 23.05 s ; V2 expire dans le sandbox,
avec seul evenement setup. V3 introduit un demarrage explicite au setup ; son
premier build 90276 echoue E0599 (get_webview reserve a l'API unstable).
L'API stable get_webview_window/as_ref verifiee dans le SDK corrige cela :
74641 compile code 0 en 27.31 s. Son run V3 dans le sandbox expire encore,
avec setup/script-dispatched mais sans evenement de navigation.

37247 execute ensuite ce meme candidat hors du sandbox : V4 atteint les vrais
evenements Started/Finished sur http://tauri.localhost, mais expire sans IPC du
diagnostic. Aucun PASS n'est attribue a cette amelioration. Inspection actuelle :
build.rs et la capability n'autorisaient que execute_launcher_command ; le
nouveau complete_launcher_probe manquait dans ces deux listes. La permission
bornee est ajoutee a la fenetre locale main. Le controle du nonce, de l'etat
CLI actif et de la fenetre reste requis avant toute terminaison du test.
Le titre attendu est aussi aligne sur le vrai render Aventures du frontend,
et le controle CSS respecte sa query string styles.css?v=3.

50452 compile cette correction, code 0 en 17.63 s. Le vrai run 36c526 hors
sandbox termine en 3.33 s : launcher-webview-run-20261007-v5/webview.json PASS.
URL http://tauri.localhost/#aventures, titre Aventures - OpenRPG Forge, styles
charges, bridge present, IPC script-started/snapshot-returned/finished observes.
Le runtime est absent dans ce cache de build : bouton present et desactive,
message exact de ZIP incomplet conformes au vrai Snapshot runtime_connected=false.
execution.json controle aussi le code du processus et l'invariance du binaire.
Ce PASS est celui du candidat debug/production ; aucune nouvelle release avec
la correction ACL, aucune preuve de lancement natif ou de paquet extrait n'est
encore acquise. Les runs V1 a V4 FAIL sont preserves. Aucun rejet d'approbation.

## Tests contact Engine enfin termines

74561 termine code 0 : 7 tests contact_frame/contact_gpu/contact_camera PASS,
774 filtres, aucun ignore, compilation 194m14 et tests 0.00 s. ABI/layout,
projection raster, absence de dependance a une phase TAA, refus d'histoires
perimees/incompatibles et retrait d'entrees de scene sont couverts. Ce ne sont
pas des nouveaux dessins GPU de l'ile. Le build natif 17184 reprend et atteint
forge_engine ; ses avertissements de copie incrementale sont conserves.
41644 et 5668 restent en attente sur leur meme target. Aucun redemarrage.

Le check Clippy du launcher courant est lance sous 20249, all-features/bin,
locked/offline/no-deps/j1/-Dwarnings, cache E: dedie. Il compile/check ses
dependances au dernier poll ; aucun verdict final n'est encore attribue.

## Corrections Clippy du diagnostic : 7 octobre

20249 a termine FAIL avec huit diagnostics ; 10248 a ensuite isole la
fermeture redundante restante. Corrections : references empruntees pour le
rapport et l'erreur, methode Path::is_dir directe, documentation Markdown et
exceptions Clippy locales motivees pour les observations booleennes independantes
et les signatures imposees par Tauri. Aucun avertissement globalement masque.
29687 termine code 0 : clippy locked/offline/all-features/bin/no-deps/-Dwarnings.
Le formatage et git diff --check passent aussi. README documente la portee
exacte du diagnostic et l'obligation de lire le verdict JSON.

65501 reconstruit la release production avec la capability corrigee et les
sources inventoriees dans launcher-executable-inputs-20261007-release-v3.json.
Au dernier poll, ce build est live, sans resultat terminal. Les trois jobs
natifs 17184, 41644 et 5668 sont egalement toujours live. Aucun nouveau dessin
GPU, paquet extrait ou verdict de parite visuelle n'est attribue.

## Release V3 : vrai chargement WebView valide

65501 termine code 0 en 2m02, release production. Avant le test, tous les
fichiers de launcher-executable-inputs-20261007-release-v3.json sont revalides.
9b58d3 termine code 0 : vrai lancement hors sandbox, profil WebView neuf,
launcher-webview-run-20261007-release-v3/webview.json et execution.json PASS.
Aventures charge depuis http://tauri.localhost/#aventures, styles charges,
bridge et vrai Snapshot observes. Le cache de build ne contient pas le jeu :
runtime_connected=false, bouton desactive et message ZIP incomplet conformes.
Ce resultat ne prouve ni pixels, ni lancement du jeu, ni distribution extraite.

Binaire SHA 910b4f72c7527f0df71cbc616a5573c6bc5dec115c6c93c1cb7d07654b01afa1,
conserve et rehash identique dans launcher-release-retained-20261007-v3.
Les quatre marqueurs Aube/Debris/Pebble/SourceFish passent sur ce meme binaire.
Recu launcher-executable-result-20261007-release-v3.json. Les trois validations
natives 17184, 41644 et 5668 restent live au poll suivant. Le prochain parcours
avec les defaults exige toujours leur nouveau candidat et ses inputs epingles.

## Revalidation des sources avant la promenade

25100 termine code 0 : le controle courant des 1 738 fichiers detecte deux
changements depuis native-walk-inputs-20261007-v1.json :
crates/forge_assets/src/source_graph_bundle.rs et
crates/forge_engine/src/source_graph_glb.rs. Ces modifications sont conservees.
L'ancien inventaire ne peut donc plus qualifier le prochain binaire ; le runner
refuserait correctement ces sources modifiees. Aucun PASS de promenade n'est
attribue et aucune recompilation n'est lancee avant resultat des jobs existants
et inspection de ces differences. La lecture d'inspection reste active sous 6289.

26912 termine code 0 : sept tests du binaire launcher courant passent,
aucun filtre/ignore, compile 44.09 s et tests 0.03 s. Ils couvrent les commandes,
le contenu Aube, la disponibilite du jeu et les chemins/env de distribution.
Ils ne qualifient pas le moteur ou la parite visuelle. 17184 reste live au poll.

## Preparation V2 apres changements concurrents

6289 termine code 0 : les deux sources sont non versionnees, conservees, et
portent des extensions d'import de stockage de textures source. La comparaison
Git ne restitue pas leur ancienne version non versionnee ; aucune equivalence
avec le premier inventaire n'est supposee.

Nouveau native-walk-inputs-20261007-v2.json : 1 763 fichiers, 97 812 092 octets,
deux lectures stables pendant capture, aucune attestation de build. Les 25
ajouts incluent deux modules Rust imported_storage/captured_texture et des
outils de travail asset independants. Ce nouvel inventaire n'est pas attribue
a 17184 : il faudra le revalider avant et apres un build des sources actuelles.

Les helpers acceptent maintenant des chemins de preuve explicites ; les recus
V1 restent intacts. native-walk-default-graphics-20261007-v2 conserve les vingt
defaults courants et un profil LOCALAPPDATA isole. 39692 termine code 0,
preparation et parseur PowerShell PASS. Le controle reel old-binary-guard.json
V2 passe : ancien candidat refuse avant test, aucun dossier walk/result cree,
environnement inchange. Aucun moteur ni rendu nouveau execute par ce controle.
17184, 41644 et 5668 restent live au dernier poll ; aucun restart.

## Verification des compilateurs actifs et des dates d'entree

Inspection CIM en lecture seule : cargo 27208 porte toujours le build natif
17184 et rustc 26628 son forge_engine ; le second rustc 45472 appartient au
test source_graph_glb::normal_binding_tests dans le target distinct
relocated-work/Rust-target. Aucun de ces processus n'est interrompu.
Les disques E/F disposent respectivement d'environ 540/1642 Go libres.

Les deux sources modifiees portent 2026-10-06T22:39:40Z ; les metadata
forge_assets-f5c96f9761ed2f44.rmeta portent 22:47:40Z et
forge_engine-23db4a8070da30da.rmeta 23:21:42Z. Le rustc du moteur a commence
a 22:51:04Z. Son CPU progresse de 274 a 313 secondes dans ce controle.
Ces observations prouvent une compilation active avec metadata posterieures
aux changements ; elles ne remplacent ni le resultat terminal de Cargo, ni
son prochain controle de fraicheur, ni le test reel. Le poll 17184 reste live.
La lecture optionnelle du rlib est encore active sous 7902 ; aucun rlib final
ni nouveau runtime n'est attribue sur cette lecture sans resultat.

## Compatibilite WebGPU et attente native verifiee

419921 termine code 0 : les trois tests existants Node du raccord
apps/forge_studio/tidewater-webgpu-compat.js passent. Ils couvrent la directive
terrain/plantes, la preservation des descripteurs non concernes, l'installation
repetee et l'absence de WebGPU. Ils n'exercent pas les branches eau/motion-blur
et ne compilent pas de shader sur un vrai GPU ; aucun dessin Studio n'est
qualifie par ce resultat. Aucun changement de source accompagne cette execution.

7902 termine code 1 sans rlib observe ; cela ne termine pas le build 17184.
Les trois sessions natives restent live, et le CPU de rustc 26628 progresse
jusqu'a 424 secondes. La production finale et le parcours de l'ile restent
conditionnes au resultat terminal et aux inputs actuels.

## Relecture du raccord Studio courant

Lecture actuelle de authoring_playtest.rs, authoring_playtest_sources.rs et
authoring_assets/objects.rs : le Playtest derive ses placements du head canonique
epingle, re-resout les imports/populations acquis et suspend une projection
incoherente. La facade commune couvre les placements manuels et les instances
de vegetation importee ; les etats Removed restent exclus. Cette inspection
ne lance aucun test et ne prouve ni picking, ni gizmo, ni sauvegarde/reouverture
de l'ile complete. Les anciens constats de refus Tidewater doivent etre compares
au contrat courant plutot que repris comme un etat actuel sans lancement.

Le poll natif suivant reste live ; rustc 26628 progresse jusqu'a 661 secondes
CPU. Aucune source du moteur ou de Studio n'est modifiee durant cette relecture.

## Build natif passe a l'executable

2506 observe la nouvelle sortie de 17184 : forge_engine (lib) generated
1 warning. Le poll suivant reste live. L'inspection CIM da25f7 confirme
rustc 37476, enfant du meme cargo 27208, demarre a
2026-10-07T00:03:03Z et compilant effectivement tidewater_plant_probe.rs
en binaire contre le nouveau libforge_engine-23db4a8070da30da.rlib.
La bibliotheque a donc laisse place a la compilation de l'executable.
Le resultat terminal du build et le parcours reel restent ouverts.

## Cache natif exFAT confirme

2512 observe aussi l'avertissement de copie incrementale de l'executable,
dans F:/OpenRPG_Forge/.build/cargo-target/debug/incremental. L'inspection
de volumes 408557 confirme F: exFAT et E: NTFS (SSD), avec environ 540 Go
libres sur E:. E:/OpenRPG_Forge/.build est une jonction vers F: ; choisir un
nouveau chemin sous .build ne changerait donc pas de volume. Le cache du
launcher sous .tidewater-integration est sur E:.

Le build actif est conserve. Une correction de cache future doit utiliser
un dossier reel sur E: hors .build, apres verification des tailles et des
processus ; aucun deplacement, suppression ou changement global de target
n'est effectue dans ce controle. Les copies signalees expliquent une charge
supplementaire ; aucune mesure comparative d'acceleration n'est acquise.

## Echec du build et correction du rapport JSON

17184 termine code 1 : recursion limit reached dans serde_json::json! du
rapport walk_smoke.rs. L'avertissement de cache exFAT reste distinct de cette
erreur bloquante. Aucun nouvel executable n'est qualifie.

Le champ plant_caster_selection est construit hors du gros macro objet puis
ajoute au meme rapport, comme les autres diagnostics deja ajoutes separement.
Tous les champs et predicates de PASS sont conserves ; la limite globale de
recursion n'est pas augmentee. rustfmt du fichier et diff --check passent.
La compilation de cette correction reste a prouver sur le vrai executable.

562949 verifie avec rustc 1.98 que le dernier -C incremental choisit bien son
cache : premier dossier absent, second cree. Ce controle porte uniquement sur
les options du compilateur. La relance 73108 utilise cargo rustc locked/offline
et -C incremental=E:/OpenRPG_Forge/.tidewater-integration/native-example-incremental-20261007-v3
pour ce seul exemple. Les dependances restent dans le target F: existant ;
aucun cache actif n'est deplace et aucun reglage global n'est modifie.

native-walk-inputs-20261007-v3.json : 1 783 fichiers, 97 935 235 octets,
capture stable et commande exacte. Preparation graphique V3 distincte, vingt
defaults, profil utilisateur isole. 73108 est live et attend le verrou des
validations deja actives ; aucun verdict de build ou de promenade n'est acquis.

## Inventaire partiel du cache pour le prochain passage SSD

60307 inventorie en lecture seule debug/deps, debug/build et debug/.fingerprint
du cache F: ; au dernier retour, debug/deps compte 15 100 fichiers et
63 378 021 145 octets. La mesure des deux autres dossiers est encore active.
Ce n'est pas la taille totale du target, ni une preuve d'immutabilite des
artefacts. Aucun cache n'est copie, deplace ou supprime par cet inventaire.
La relance 73108 et les validations 41644/5668 restent actives au poll suivant.

## Inventaire termine et garde des logs GPU

60307 termine code 0 : debug/deps 63 378 021 145 octets / 15 100 fichiers,
debug/build 5 717 982 925 / 12 694 et debug/.fingerprint 11 727 324 / 22 262.
Somme de ces mesures : 69 107 731 394 octets, 50 056 fichiers. Le cache etait
actif pendant le parcours ; ce n'est ni un snapshot coherent ni un recu de copie.
Le dossier incremental est volontairement hors de cette mesure de reutilisation.

Le SDK serde_json 1.0.151 epingle dans Cargo.lock est relu pour sa construction
recursive des objets ; aucun changement du SDK ou de limite globale n'est fait.
La correction du rapport attend toujours la compilation du vrai executable.

La garde externe du prochain parcours reconnait maintenant aussi les prefixes
ERROR wgpu_core et wgpu_hal, en plus de wgpu, Validation Error et pipeline_cache.
Le parseur PowerShell et sept cas de classification passent sous bc63a2 ;
native-walk-gpu-log-guard-20261007-v2.json reste explicitement une preuve de
classification, sans invocation du moteur ni qualification GPU ou visuelle.

## Sources concurrentes et preparation native V5 — 7 octobre

Le controle 38c9ca puis le recu native-walk-input-drift-20261007-v1.json
constatent deux changements depuis le snapshot V3 : crates/forge_engine/src/lib.rs
(2026-10-07T00:43:53Z) et scripts/hivemind/Run-NestedMaterialTextures.ps1.
Aucun changement concurrent n'est annule. Le test natif 41644 a lance son
rustc avant le changement de lib.rs ; son resultat eventual ne qualifie pas
par lui-meme cette nouvelle entree de bibliotheque.

Le premier inventaire V4 applique les exclusions Git et omet 108 chemins
precedemment suivis, notamment le dist Studio et des fichiers ignores.
Il est conserve comme evidence mais n'est pas retenu pour le parcours.
Le nouvel inventaire V5 utilise rg --files --hidden --no-ignore dans les memes
sept zones, avec exclusions target/.git/node_modules. Capture stable :
1 797 fichiers, 98 049 018 octets ; aucun des 1 783 chemins V3 n'est omis,
et quatorze chemins sont ajoutes, dont source_graph_scene_geometry.rs.

native-walk-default-graphics-20261007-v5/preparation.json contient les vingt
valeurs par defaut courantes, exposition +0.4 EV, dans un profil LOCALAPPDATA
isole. Aucune preference utilisateur n'est modifiee. Le chemin OpenRPG Forge/
Tidewater/graphics.json concorde dans graphics.rs, la preparation et le runner.
La commande de build 73108 et son cache incremental E: restent inchanges.
V5 ne prouve ni compilation reussie ni image ni parite ; la coherence des
sources et de l'executable devra encore etre controlee au resultat terminal.

Le controle CIM 66087 termine code 0 : rustc 44500 est bien l'enfant du test
materiaux cargo 42316 et compile forge_engine --test dans le target F:.
Les commandes du check 5668 et du build 73108 sont toujours presentes ; elles
n'ont pas encore produit de resultat terminal. Le target relocated-work d'un
autre travail demeure distinct et n'est ni interrompu ni modifie.

## Test reel des materiaux source termine

41644 termine code 0. Le profil test finit sa compilation et son edition des
liens en 351m 37s, puis execute effectivement :
tidewater_fish_props::material::tests::every_source_instance_keeps_surface_parameters_and_scene_bindings.
Resultat : 1 passed, 0 failed, 0 ignored, 781 filtered out ; execution 26.18s.
Le test utilise le package FishProps reel selectionne pour cette session.
Ce resultat couvre ce test de parametres de surface et de bindings de scene ;
il ne prouve ni dessin GPU ni parite visuelle ni edition de toute la carte.
Le changement concurrent de lib.rs releve plus haut reste distinct : le test
compile a partir d'une entree anterieure et ne remplace pas la verification
du nouvel executable sur les sources courantes. Aucun resultat terminal du
check 5668 ou du build 73108 n'est encore acquis au poll suivant.

## Check de l'exemple : E0308 corrige localement

5668 termine code 1 : une erreur E0308 dans source_fish.rs:214.
lighting renvoie Result<VillageMaterial, String>, tandis que son expression
finale ok_or utilise une chaine &str. Le raccord renvoie maintenant
ok_or_else(|| "Native fish common material unresolved".to_owned()).
L'allocation reste sur le chemin d'erreur et le message est conserve.
Rustfmt 1.98 edition 2024 et diff --check passent sous 65636.
La construction du gros rapport JSON n'a plus produit l'erreur de recursion
dans ce check ; cela ne qualifie pas encore l'edition des liens ni le rendu.

Le build 73108 demeure actif. Sa compilation doit encore confirmer la
correction ; aucun nouvel executable n'est atteste. Capture V6 stable :
1 803 fichiers, 98 098 173 octets, avec la correction source_fish et les
sources concurrentes courantes. Preparation graphique V6 distincte : vingt
defaults, profil LOCALAPPDATA isole, aucune execution du moteur.
Les snapshots et preparations anterieurs sont conserves et ne remplacent
pas V6 pour le prochain parcours. Le resultat final et la coherence des
inputs seront verifies avant toute qualification du nouvel executable.

## Build natif : bibliotheque terminee, exemple en compilation

Le poll 73108 sous f55b0e annonce `forge_engine (lib) generated 1 warning`,
sans resultat terminal. Le controle CIM 332c1c confirme ensuite rustc 46140,
enfant du cargo 42284, compilant effectivement tidewater_plant_probe.rs avec
les sources courantes et le cache incremental d'exemple sur E:.
Le precedent rustc de la bibliotheque n'est plus present dans cet inventaire.
Le processus link 32612 appartient au rustc 33812/cargo 45220 d'un autre
travail dans relocated-work ; il ne prouve pas la liaison de notre exemple.
Aucun de ces processus n'est interrompu ou relance.

Le dernier controle du fichier executable 5f5f1a trouve encore l'ancien
binaire de 134 270 464 octets, date du 6 octobre a 10:49:48 UTC. Le poll
f602b4 confirme que 73108 demeure actif sans nouveau resultat. Le parcours
V6 attend donc toujours la fin reussie du build et la verification du nouveau
hash ; aucune execution GPU ni qualification visuelle n'est ajoutee ici.

## Attente de l'exemple : mesure du stockage

La verification d8918f rehash les 1 803 entrees V6 : aucun changement.
Le processus rustc 46140 est encore confirme enfant de cargo 42284 sous
577d3c ; ses compteurs restent faibles (937 593 octets lus, 196 714 ecrits).
Le controle 4d1551 mesure sur le disque physique F: une file courante de
six operations, environ 12.9 Mo/s en lecture et 11.6 Mo/s en ecriture.
Ces compteurs portent sur tout le disque et ne permettent pas d'attribuer
chaque operation au build. La nouvelle bibliotheque forge_engine rlib mesure
2 070 329 076 octets, date du 7 octobre 04:58:26 UTC.
L'attente de stockage est donc plausible, sans preuve de blocage definitif.
Aucun cache actif n'est deplace, aucune compilation concurrente n'est lancee.
73108 reste actif sous db6b9c ; aucun executable final ni rendu GPU acquis.

## Progression memoire de l'exemple

Les deux controles CIM c623fc et 0c0f48 portent sur le meme rustc 46140,
enfant de cargo 42284, de part et d'autre du poll de 45 secondes e8611c.
PageFaults progresse de 21 979 a 27 727 et WorkingSetSize de 76 570 624 a
99 688 448 octets ; les compteurs ReadTransferCount/WriteTransferCount restent
inchanges. Cela montre une activite de chargement memoire, sans attribuer ces
fautes de page a un support particulier ni prouver la fin de compilation.
73108 demeure actif sous b4e40b : le prochain verdict attend toujours
l'executable final et le parcours GPU reel.

## Liaison du nouvel executable natif confirmee

Le controle 3a8559 constate que tidewater_plant_probe.exe a ete remplace
par une sortie partielle de 2 097 152 octets datee du 7 octobre 05:46:18 UTC.
Ce fichier encore en ecriture n'est pas lance et ne constitue pas un candidat.
Le controle CIM 3707c8 confirme link.exe 36268 enfant de rustc 46140,
avec linker-arguments dans le target courant debug/examples/rustc5WYLdn.
Il s'agit bien de la liaison de notre exemple, distincte du linker du travail
relocated-work releve precedemment. 73108 demeure actif sous 566455.
La qualification attend toujours la fin de build code 0, le hash final stable,
la coherence V6 et le parcours natif reel. Aucun verdict visuel ajoute.

## Echec de liaison MSVC et relance LLVM isolee

73108 termine code 1 sous 78d262 : link.exe echoue code 1 sans diagnostic
LNK ni erreur precise. Le conseil generique de Rust sur Visual Studio ne
prouve pas une installation defectueuse ; aucune reparation ou installation
n'est lancee. Le fichier exe restant de 2 Mo et le PDB de 3 587 756 032 octets
ne sont pas qualifies ni executes. La requete d'evenements dc0eec ne retourne
pas de message de crash correspondant ; elle ne determine pas la cause.

Le controle aaf699 confirme la fin des anciens processus de notre build,
l'absence du fichier temporaire linker-arguments et l'espace disponible :
C: 52 058 976 256 octets, E: 538 853 920 768, F: 1 581 356 417 024.
La verification d52cbb rehash les 1 803 fichiers V6, aucun changement.

21377 relance cargo rustc locked/offline/j1 dans le meme target, avec le
meme cache incremental d'exemple sur E:, rust-lld 1.98 fourni par le toolchain,
linker-flavor=lld-link et link-arg=/DEBUG:NONE. Ce dernier parametre evite
la generation des symboles PDB ; aucun contenu, effet ou comportement du jeu
n'est supprime. Les dependances deja compilees sont conservees.
Aucune compilation concurrente propre ni nettoyage ni migration de cache.

Snapshot V7 sous 56b299 : 1 826 fichiers, 98 265 657 octets, capture stable,
commande exacte et session 21377. Preparation graphique V7 sous 26f875 :
vingt valeurs, exposition +0.4 EV, profil utilisateur isole, runtime_executed
false. Son old_binary_sha256 designe le fichier incomplet de la tentative
echouee ; un candidat ne sera selectionne qu'apres build code 0 et verification
du hash final. Le controle 8fe8c4 confirme cargo 39464/shim 27320 pour cette
commande ; les processus relocated-work appartiennent a un autre travail.
21377 reste actif sous 1d7f97, sans nouveau verdict de compilation ni GPU.

## Relance LLVM : liaison effectivement atteinte

Le controle 5b9060 confirme rustc 47040 enfant de cargo 39464, compilant
uniquement tidewater_plant_probe avec les memes rlib et les flags LLVM prevus.
65ea69 affiche l'avertissement connu dead_code de isolated_lod.rs, sans erreur.
7109f6 puis c91e34 identifient rust-lld 47184 avec -flavor link et le fichier
linker-arguments du target courant rustcOrfiQe. Ce n'est pas encore une
liaison reussie : 21377 demeure actif sous cb72c2, aucun candidat execute.
Le processus Python egalement releve concerne queue_hdrp_snapshot_v3.py ;
aucune action sur ce travail distinct n'est effectuee.

## Sources concurrentes : entree Studio modifiee pendant la liaison LLVM

Le controle 4a1e1c rehash les 1 826 fichiers V7 et trouve une divergence :
apps/forge_studio/src/authoring_assets.rs. 965137 releve sa date courante
2026-10-07T06:27:51Z ; aucun changement concurrent n'est annule.
Le recu native-walk-input-drift-20261007-v7-v1.json conserve les hashes
attendu/courant. Le profil graphique isole n'est pas execute.

Le manifeste forge_engine ne declare pas de dependance forge_studio ; ce
constat ne transforme pas le controle complet V7 en PASS. Le runner garde
son refus des inputs divergents. La fin du build 21377 sera recueillie avant
une nouvelle preparation et un controle Cargo sous sources coherentes ;
aucune compilation parallele ni relance sur simple timeout n'est effectuee.
Les preuves natives resteront distinctes de la qualification Studio de ce
nouveau fichier. Aucun verdict graphique ou d'edition de carte acquis.

## Preparation rafraichie V8 sans perdre le rejet de l'ancien executable

PrepareNativeWalkGraphics20261007.py accepte maintenant --previous-preparation.
Le schema, la version, la racine et le SHA de l'ancien executable sont verifies,
et le hash du recu parent est controle avant/apres preparation puis enregistre.
Cela permet de rafraichir les inputs sans prendre le nouvel executable en cours
de construction pour l'ancien fichier a refuser. Le runner et son controle
complet des sources ne sont pas assouplis. Analyse Python AST PASS sous 1d2ea2.

3d1041 capture V8 stable : 1 831 fichiers, 98 296 847 octets. b89363 cree une
preparation V8 effective avec vingt valeurs, +0.4 EV et LOCALAPPDATA isole,
en reprenant le recu V6 et son hash de l'ancien executable original.
Aucune preference utilisateur modifiee, aucune execution du moteur.
V8 est capture pendant la liaison 21377 ; il ne prouve pas que le Studio
modifie a ete compile. Apres le resultat terminal du build, un controle Cargo
avec les memes flags devra confirmer les sources courantes avant le parcours.
La preparation V7 divergente reste conservee, sans qualification retroactive.

## Controle courant V8 et limite du raccord Studio

c409f2 rehash les 1 831 fichiers V8 : aucun modifie ni manquant. L'inventaire
courant ajoute scripts/hivemind/resume_villa_scene_transform_reconciliation.py
et scripts/hivemind/tracking_io.py. Le recu local
native-walk-input-review-20261007-v8-v1.json conserve ce verdict complet false,
sans effacer les ajouts ni reduire la portee du controle. Une capture fraiche
reste necessaire avant le parcours, apres confirmation Cargo sous sources
courantes. 08c630 confirme la session 21377 toujours active ; 07ac07 observe
rust-lld 47184 a 3.34 secondes CPU et 1 458 171 904 octets residents. Ce sont
des observations de liaison, sans resultat terminal ni preuve GPU.

La recherche actuelle caa3da/bbb7f3 dans apps/forge_studio/src trouve les
declarations de source_graph_intake, source_graph_resources et source_graph_scene,
mais aucun appel de leur intake/registration/attachment depuis l'apercu ou le
placement. Le code d'admission refuse les declarations source invalides et le
host conserve la racine commune cachee jusqu'au raccord des passes. Ces adapters
seuls ne prouvent donc pas une entree utilisable dans l'editeur. Cette observation
concerne ce nouveau chemin de materiaux source ; elle ne declare pas absents
les parcours ManualPlacement existants ni ne les qualifie pour toute l'ile.
Les modifications concurrentes de Studio sont conservees. La selection,
les gizmos, la sauvegarde/reouverture et le rendu effectif doivent encore etre
verifies sur le candidat correspondant ; aucune qualification Studio ajoutee.

## Le runner refuse aussi les ajouts de fichiers apres capture

RunNativeWalkDefaults20261007.ps1 complete Assert-PreparedInputs par une
enumeration rg des memes sept zones et exclusions que le constructeur de
snapshot, avec les trois fichiers Cargo/toolchain racine. La comparaison des
chemins normalises refuse ajouts et retraits avant les controles de contenu.
Le repertoire de travail est restaure meme en cas d'echec d'inventaire.
Cette modification est hors sources Rust et ne relance pas le linker actif.

67ea5b valide la syntaxe PowerShell et execute la vraie fonction du runner
contre V8 : les deux ajouts sont refuses, repertoire restaure, moteur non lance.
8ec279 execute la meme fonction contre une fixture d'inventaire complete
de 1 833 fichiers, enrichie des deux vrais nouveaux hashes : PASS, hashes et
inventaire acceptes, repertoire restaure. Les deux recus
native-walk-inventory-guard-20261007-v1.json et -v2.json restent locaux ; la
fixture v2 est uniquement un controle de garde, pas un snapshot de build.
SHA du runner controle :
e398d1499c38b22f3401d09629e78483befbd4fa56c58878282379d2439fecd5.
Ces controles ne qualifient aucun executable, rendu ni paquet portable.

## Instructions du paquet alignees sur les vingt reglages

Le texte LISEZ-MOI produit par Build-WindowsPlaytest.ps1 ajoute les cinq
libelles existants du menu natif : rayons sous-marins, surfaces immergees,
caustiques, refraction des ombres et capture de refraction. Les consignes
d'extraction complete, du launcher Aube et du bouton Jouer sont conservees.
5cfe16 valide la syntaxe PowerShell et le diff cible ; aucun packaging execute.
Cette modification du script est maintenant une divergence connue de V8 :
le prochain parcours exige un nouveau snapshot et une preparation fraiche
apres le resultat terminal et la confirmation Cargo. 254067 confirme 21377
encore actif. Aucun nouveau verdict de rendu ou de distribution.

## Liaison LLVM terminee et confirmation des sources courantes

985fe5 recueille le resultat terminal de 21377 : code 0, dev profile termine
en 79 min 02 s, avec le seul avertissement connu de l'exemple. ec78eb mesure
le nouvel executable : 133 874 688 octets, UTC 2026-10-07T07:32:00.38Z,
SHA-256 16a34e8b5a9283f5b00db587a0d8e55511387f87f1f97214e00501b9e919eb3c.
Le fichier incomplet de l'echec MSVC n'est pas utilise comme candidat.

5972c9 capture V9 stable : 1 835 fichiers, 98 317 211 octets. ac3a12 prepare
les vingt valeurs par defaut, exposition +0.4 EV, LOCALAPPDATA isole et ancien
hash V6 conserve. Le controle Cargo de confirmation reprend exactement les
flags du build reussi dans la session 28720, apres fin naturelle du precedent.
1257c7 identifie cargo 45124 enfant du shim 41232 pour cette commande ; aucune
compilation parallele propre n'est lancee. Ce controle est encore actif sous
22b905. Le parcours V9, les images et le nouveau paquet restent non executes.

## Confirmation Cargo reussie et premiere execution du nouveau candidat

e3f1c9 recueille 28720 code 0 en 2 min 49 s, sans nouvelle compilation
annoncee ; les avertissements existants sont rejoues. Le parcours V9 sous
0e43a1 est refuse avant lancement : un script concurrent,
scripts/hivemind/capture_villa_native_prefab_hierarchies.py, a ete ajoute.
6a6a69 confirme runtime_verifier_invoked false, environnement restaure et
preferences utilisateur intactes. Ce refus est conserve, pas converti en PASS.

8578e8 capture V10 stable : 1 836 fichiers, 98 324 432 octets, puis prepare
les vingt valeurs et l'ancien hash V6. c37135 compare V9/V10 : aucune entree
precedente changee ni manquante, seul le nouveau script ci-dessus est ajoute.
Les sources compilees du candidat ne changent pas. Le runner V10 commence
dans la session 15967 avec le SHA exact 16a34e8b...9eb3c. 6e3444 identifie
le runtime tidewater_plant_probe 36108 ; f54ccf acquiert la vegetation workspace
pour la zone attendue. Le parcours reste actif, aucune capture ni verdict
GPU/visuel ou nouveau paquet n'est encore qualifie par ces observations.

## Echec GPU reel des poissons et correctif de namespace

16bac7 recueille 15967 termine code 1. Le runtime a initialise Vulkan sur
RX 7800 XT, le village (145 nodes), les 805 debris proceduraux, les 141 scans
et les ressources Pebble. La creation du programme tidewater-fish.wgsl echoue
avec WgslParseError : invalid field accessor `view`. Les recus V10 conservent
pass false, aucune image/walk receipt et les pins d'entree/binaire inchanges.
Le wrapper confirme environnement restaure et preferences intactes ; son log
SHA est 3146f6430cdc14618e917cc8192f54e0ac0ac293fce136363ad8a7960e19e3ac.

Le shader compose montre que l'import nu de mesh_view_bindings::view renomme
aussi le champ view de FishFrame, alors que frame.view reste un acces simple.
Le correctif des deux fichiers material_vertex.wgsl/material_fragment.wgsl
importe le module sous fish_camera et qualifie ses deux acces a view.
Le champ et les fonctions source sont conserves, avec les memes matrices,
surfaces, instances et effets. Aucun poisson n'est retire pour contourner
l'erreur. Le prochain parcours complet reste necessaire pour valider le fix.

c7862f capture V11 stable : 1 838 fichiers, 98 335 120 octets, puis prepare
vingt valeurs et +0.4 EV dans un profil isole. db22d7 lance la reconstruction
70753 avec debug=0 uniquement pour forge_engine via --config, meme opt-level,
SDK, features et cache de dependances, LLVM et cache d'exemple v4 sur E:.
[Overrides Cargo](https://doc.rust-lang.org/cargo/reference/profiles.html#overrides)
documente ce reglage par package ; aucun fichier de configuration global
n'est modifie. Le volume et le gain de temps restent a mesurer. Aucune nouvelle
compilation terminee, image, distribution ou parite n'est qualifiee a ce stade.

## Regression du namespace avec le vrai composeur Naga Oil

Le petit harnais fish_view_namespace_regression_20261007.rs utilise le
composeur cache libnaga_oil-d4bfec58dd85ebda.rlib et le toolchain 1.98.0,
sans reconstruire les dependances ni Engine. 864bed termine sa compilation
code 0 ; 35ba37 execute ses deux tests, 2 PASS en 0.01 s : l'import nu
reproduit exactement invalid field accessor `view`, le module fish_camera
permet la compilation et l'acces au champ source. Le recu local
fish-view-namespace-regression-20261007-v1.json conserve source/binaire/log
hashes et gpu_draw_executed false. Ce test minimal ne contient pas toutes
les fonctions de surface des poissons ni le rendu GPU : il valide la cause
de namespace, pas la parite ni le prochain parcours complet.

3e52c7 identifie rustc 1168 de la reconstruction 70753 : bibliotheque Engine,
opt-level 1, sans debuginfo=2. 0a706e confirme le build toujours actif.
Le candidat corrige et son parcours restent a recueillir ; le FAIL V10 est
conserve et aucun paquet n'est construit sur le binaire ayant echoue.

## Bibliotheque corrigee terminee, executable en compilation

362f8b identifie rustc 44068 enfant de cargo 39648 pour tidewater_plant_probe,
avec libforge_engine-45456b33c988b816.rlib, opt-level 1, debug-assertions on
et les memes features default/native/physics-rapier. Le cache d'exemple v4
reste sur E: et LLVM est conserve. La bibliotheque Engine precedente a termine.
1a3016 mesure la nouvelle rlib : 278 434 742 octets, UTC 2026-10-07T08:27:10Z,
contre 2 070 329 076 octets pour la variante avec debuginfo complet. Cette
reduction de volume est acquise ; elle ne prouve pas encore un gain total de
temps ou un resultat GPU. 72c34f observe le nouveau rustc actif et f085f8
confirme 70753 toujours en cours. Aucun nouveau runtime n'est lance avant
resultat terminal et verification de l'identite du candidat corrige.

## Liaison corrigee active et divergence concurrente V11

47aff9 identifie rust-lld 25852 enfant de rustc 44068 avec le fichier
linker-arguments rustcTAz3TG. f279b8 confirme la liaison toujours active.
09054c rehash les 1 838 fichiers V11 : authoring_assets.rs,
authoring_assets/source_graph_resources.rs et le script
record_sequential_batch_progress.py divergent ; neuf chemins sont ajoutes,
dont les adapters Studio source_graph_frame.rs/source_graph_host.rs.
Aucune entree precedente manque et aucun fichier Engine ne diverge.
Le recu native-walk-input-review-20261007-v11-v1.json conserve tous les
chemins et hashes, avec pass false. Aucun changement concurrent n'est annule.

Le runner V11 n'est pas lance ni assoupli. Apres resultat terminal du linker,
un nouvel instantane complet et un controle coherent de compilation seront
necessaires avant le parcours. La nouvelle entree Studio et sa qualification
restent distinctes du build natif ; aucun rendu ou paquet ajoute a ces preuves.

## Candidat corrige compile et nouveau parcours V13 (7 octobre)

2560be termine la reconstruction 70753 code 0 en 89 min 20 s. Le candidat
mesure 133 875 200 octets ; SHA-256
`e1e6ef2841547604bd6e0e154f75c29621a3765bedf2311a52bf165654bbb50d`.
f07915 retrouve les trois marqueurs fish_camera dans le binaire. La confirmation
29267 termine code 0 en 8 min 33 s (f4eb62). Cela prouve compilation et identite,
pas encore le rendu GPU corrige.

ee3327 prepare V12 sur 1 847 fichiers stables. a37aa6/71dbca refusent son
execution avant lancement : deux scripts concurrents hivemind ont ete ajoutes.
Le resultat V12 conserve runtime_verifier_invoked false, profil utilisateur
inchange et environnement restaure. 1ff8af capture V13 : 1 849 fichiers,
98 410 761 octets, aucune entree V12 modifiee ou supprimee, seuls
verify_medieval_skin_normals.py et verify_medieval_skin_positions.py ajoutes.
Ces scripts Python ne changent aucune entree du moteur compile ; une nouvelle
recompilation du moteur ne se justifie pas pour cet ajout. La garde complete
reste active et aucun travail concurrent n'est retire.

404f5d prepare V13 avec les vingt valeurs et +0.4 EV, conserve l'ancrage de
l'ancien binaire refuse, puis lance le parcours natif sous session 15037.
7d2b39 confirme ce processus toujours actif, sans resultat terminal. Les poissons,
pebbles et effets source sont demandes. Aucune capture, parite ou nouvelle
distribution n'est declaree valide avant lecture de son resultat effectif.

## Diagnostic de demarrage V13, processus conserve

Le parcours 15037 reste actif (51de48, 1b7bf4, 71c32a) ; processus natif
47872 lance le 7 octobre a 09:39:27 UTC. La vegetation canonique est chargee
sans exclusions : 65 383 instances ; Vulkan RX 7800 XT est initialise
(0fcad4). Aucune capture ni preuve finale n'est encore disponible.

process-sample-01.json puis process-sample-02.json conservent les mesures.
c7a76d releve 482 821 122 octets lus, 877 195 264 octets de working set et
21 threads du seul processus natif. La RAM disponible globale vaut 19 784 MiB ;
le disque logique F: affiche une file de 17 requetes et 1 353 163 octets/s
lus. Ces compteurs globaux ne permettent pas d'attribuer tout le trafic a Forge
ni de prouver la cause exacte du delai. Les lectures du processus continuent
de progresser ; un timeout d'observation n'est pas traite comme un arret.
Le test n'est pas relance, les autres processus et caches sont preserves.
La qualification GPU et visuelle demeure ouverte.

## Parcours V13 termine : correction shader effective, qualification FAIL

15037 termine code 1 (d75bd1). Le shader fish_camera permet maintenant au jeu
de rendre et au test de produire island.wind-a.png/island.wind-b.png ; aucune
erreur de pipeline GPU n'est listee dans result.json. La capture wind-a a ete
inspectee : village, eau, vegetation, ombres et capsule de test visibles.
Cette vue n'est pas une comparaison source alignee et ne prouve pas la parite.

island.walk.json conserve functional_pass true, 361 ticks grounded,
cascade_navigation_pass true, source_fish_preparation_pass true et 83 meshes
masques prepares. La vitesse d'eau observee vaut 1.52015 secondes simulees par
seconde reelle pour la consigne 1.5. Le FAIL cadence comprend deux conditions :
44.7476 FPS moyens (seuil conserve 60), p95 25.7499 ms / 1% low 14.7744 FPS ;
la preuve shadow_rate gele apres seulement 110 frames, alors que son validateur
requiert au moins 120. Les rapports de soumission/layers doivent etre verifies
sur une fenetre suffisamment longue, sans diminuer les seuils existants.

Le runner execution.json refuse donc le packaging de ce binaire ; sa preparation
poissons vaut false a ce niveau, car le validateur exige aussi le PASS global.
Ne pas confondre cette exigence avec le champ preparation true du recu MainWorld.
Les pins et le binaire restent inchanges, le profil utilisateur et l'environnement
sont restaures. Les captures et le FAIL demeurent conserves. Prochaine correction :
coordonner la fenetre de mesure des ombres avant gel du vent, puis mesurer la
performance en gardant le contenu et les effets source. Aucun nouveau ZIP produit.

## Fenetre de mesure des ombres corrigee, regression en cours

La correction de walk_shadow_rate.rs ajoute window_ready : attendre au moins
120 main frames entre les snapshots debut/fin, avec checked_sub. Le demandeur
walk_wind_readback.rs attend cette readiness avant de commencer son gel A/A/B.
La readiness n'exige pas un verdict valide, afin de finir et rapporter une
cadence invalide ; les seuils du validateur et le critere 60 FPS restent intacts.
Deux tests couvrent la fenetre V13 de 110 frames puis 120, ainsi que les mesures
absentes/inversees. Les cinq tests de cadence anterieurs restent presents.

1c7d0c confirme rustfmt --check des deux fichiers et diff --check code 0.
Le fmt workspace (218113) reste FAIL sur plusieurs fichiers concurrents ;
ils ne sont pas reformates par ce chantier. Le harnais local
shadow_window_regression_20261007.rs inclut le vrai module et les SDK retenus,
avec une ressource wind minimale. Sa premiere compilation 67446 signale E0428
(doublon de nom de module tests), corrige par window_readiness_tests. 8f7783
confirme cette tentative encore active : aucun test PASS ni build natif corrige
n'est atteste. Attendre le resultat terminal puis rejouer sur la source corrigee.
Le parcours V13 reste FAIL ; aucun seuil n'est diminue pour le promouvoir.

## Instrumentation du prochain parcours et limites du harnais

8a79ee rehash les entrees retenues V13 : seuls walk_shadow_rate.rs et
walk_wind_readback.rs divergent, aucune entree manquante. Ce constat est date
avant l'ajout suivant ; il ne couvre pas les nouveaux chemins.

walk_render_diagnostics.rs utilise RenderDiagnosticsPlugin de Bevy 0.19.1,
dont le code et l'API DiagnosticsStore ont ete lus localement. Il est installe
uniquement par le parcours smoke. Le recu expose des timings elapsed_cpu/GPU,
avec historique de chaque passe, sans additionner des spans imbriques ni traiter
les timestamps GPU absents comme zero. Les passes Forge sans spans demeurent
hors mesure. walk_smoke.rs ajoute le recu hors macro pour eviter la limite de
recursion precedente. bee666 confirme le formatage et diff --check cibles.
Cette instrumentation n'a pas encore compile ni ete executee.

Le harnais rustc direct 67446 rencontre aussi un environnement Cargo absent
pour les derives Bevy et une mauvaise variante serde (StableCrateId en collision).
Les erreurs Resource qui suivent sont des consequences des derives avortees.
Aucun PASS de test n'est revendique ; la validation suivante devra utiliser le
graphe Cargo exact apres fin terminale de cette tentative, en conservant les
preuves. Aucune dependance, configuration globale ou cache n'est modifiee.

## Tests du vrai exemple via Cargo, baseline preservee

155b3f termine le harnais direct 67446 code 1. ad0d4f lance 22838 via
cargo rustc --locked --offline, override debug=0 Engine, exemple --test,
LLVM et incremental de test sur E:, sortie locale distincte
native-example-tests-20261007-v1.exe. 4f685d identifie rustc 4572 enfant de
cargo 46188 : Bevy 57800355777dc27b, Engine 45456b33c988b816 et variante
serde 272a0b2c07569019. Ce sont les dependances du graphe Cargo exact, sans
reconstruction de bibliotheque ou installation de SDK observee. Les warnings
-o/out-dir sont attendus pour cette sortie de test explicite ; ce fichier
ne doit jamais etre package comme le runtime de jeu.

ddad8e produit performance-baseline.json dans le cas V13 : hashes des trois
recus, valeurs graphiques, pins, mesures de frames, vitesse d'eau et compteurs
d'ombres. Il conserve overall_pass false et les limites du benchmark unique.
d3822c confirme la compilation de tests toujours active avec le warning
preexistant isolated_lod::run inutilise ; aucun test execute ou PASS revendique.
Le jeu et le packaging corriges restent a construire et valider ensuite.

## Correction de portee : profil graphique absent du smoke V13

c179c9/8513b0/b3dec1 relisent walk::install/configure et graphics::install.
Le mode smoke appelle with_first_person(false) et ne passait pas dans le
if !smoke qui installait GraphicsState et les plugins de grading/exposition.
Ainsi les vingt valeurs de result.json V13 sont les valeurs PREPAREES par
le wrapper, pas la preuve de valeurs chargees par le renderer. Le recu V13
reste une baseline fonctionnelle du mode de test avec sa camera de capsule,
pas une qualification des vingt reglages ni de la performance du jeu interactif.
La baseline performance-baseline.json doit etre lue avec cette correction de
portee : son champ graphics provient de la preparation, non du runtime.

La correction appelle graphics::install pour toutes les promenades, y compris
smoke. GraphicsState expose un recu de ses valeurs chargees ; walk_smoke le
conserve dans graphics_values. Le wrapper RunNativeWalkDefaults exige desormais
la presence des vingt champs et leur concordance avec la preparation (tolerance
1e-6 pour les floats), sans confondre recu de valeurs et preuve de chaque pixel.
86bb2c confirme formatage/diff cibles et syntaxe du wrapper. Aucun nouveau test
GPU ni gain FPS n'est revendique pour cette correction non reconstruite.

22838 est deja en liaison des tests precedents, avec rust-lld 40084 confirme
actif (e4d7c5). Son eventuel PASS de sept regressions de fenetre/cadence ne
validera pas le nouveau raccord graphique ajoute apres compilation. Une
reconstruction native sur instantane frais est requise avant la prochaine
promenade et le packaging. Le script RunNativeShadowWindowTests20261007.ps1
attend le binaire termine, enumere les sept noms attendus, refuse les ignored
et conserve hashes/logs ; seul son parseur a passe (54ef88), pas son execution.

## Sept regressions PASS et reconstruction graphique V14 lancee

35c0d5 termine 22838 code 0 en 20 min 38 s. 257857 execute le script de tests
sur native-example-tests-20261007-v1.exe : sept tests enumeres et PASS,
zero failed/ignored, 22 filtres. Les cinq tests de cadence existants et les deux
nouvelles regressions de fenetre sont executes, hashes et logs conserves dans
native-shadow-window-tests-20261007-v1/result.json. Ce PASS CPU ne valide pas
le raccord graphique ajoute apres compilation ni une performance GPU.

1c3084 capture V14 stable : 1 859 fichiers, 98 497 851 octets. Le profil isole
conserve les vingt valeurs, +0.4 EV et l'ancrage de l'ancien exe refuse. La
reconstruction native 96545 est lancee avec les memes dependances, debug=0
Engine, cache d'exemple v4 sur E: et linker LLVM, sans --test et sans sortie de
test explicite. Le nouveau runtime doit encore terminer, etre identifie par
hash, passer la garde de sources puis produire graphics_values concordantes
et un parcours complet valide. Aucun nouveau paquet ou verdict visuel ici.

### Instantane V15 et revue de la cadence des ombres (2026-10-07)

La liaison native de la session 96545 reste active : rustc 20268, rust-lld 35748 et Cargo 45540 ont ete observes vivants. Aucun succes de compilation ni nouveau parcours GPU n'est encore affirme.

Le controle V14 a trouve sept ajouts et une modification uniquement sous `scripts/hivemind/`. Tous les fichiers retenus des autres chemins sont identiques. La capture V15 contient 1866 fichiers (98542370 octets), avec deux lectures identiques. `native-walk-input-refresh-20261007-v15.json` conserve les deltas ; le cas `native-walk-default-graphics-20261007-v15` prepare les vingt valeurs courantes pour la prochaine execution. Ces outils Python concurrents ne sont pas des entrees de compilation de l'exemple natif ; le moteur et l'exemple n'ont pas ete remplaces pour accepter leur travail.

La revue `shadow-cadence-source-review-20261007-v1.json` compare `Shadows.js` du commit fige `1438b1abfcaee3267092b75573014f4d9b4a983c` au graphe Rust. La source selectionne les cascades selon les periodes 1/2/4, les changements du soleil et leur indicateur dirty. Le graphe Rust force actuellement les trois couches lorsque les poses de poissons changent. Ce cas peut expliquer les couches lointaines supplementaires du parcours V13, mais ni l'attribution runtime ni son effet sur les performances ne sont prouves. Aucun changement moteur n'a ete fait sur cette hypothese. Le prochain parcours doit mesurer la cadence avec la fenetre minimale corrigee et la chaine graphique reellement installee.

### Candidat graphique construit et parcours V16 lance (2026-10-07)

La session native 96545 termine avec le code 0 apres 44m29s (4244fa), avec un avertissement existant. Le nouvel executable compte 133979648 octets ; SHA-256 `249478450fad94a775393342e10077e6f2e0db43e3980e71f27ced1f0bb72382`. Sa copie conservee dans `.tidewater-integration/native-graphics-retained-20261007-v1/tidewater_plant_probe.exe` est identique au fichier F: avant et apres copie. `retention.json` conserve cette preuve, qui ne vaut pas validation GPU ni parite visuelle.

Le cas V15 refuse le lancement avant execution : un nouveau script `scripts/hivemind/check_current_studio_wasm.py` a ete ajoute apres sa capture. Le refus et la restauration de l'environnement sont conserves dans son `result.json`. La capture V16 (1867 fichiers, 98547413 octets) confirme que les seules differences depuis les entrees compilees V14 restent les scripts Python concurrents sous `scripts/hivemind/`. Le nouveau cas V16 est prepare et son controle natif est lance sous la session 49925, avec les vingt valeurs explicites, Pebbles et SourceFish. Aucun verdict runtime ni package nouveau n'est encore qualifie.

### Parcours V16 termine FAIL ; cadence des poissons corrigee (2026-10-07)

La session 49925 termine code 1. Le runtime charge 65383 elements de vegetation sans exclusion ni reduction de densite, les 805 decors proceduraux et les 141 scans. Le recu V16 confirme `runtime_graphics_values_verified=true`, environnement restaure et profil utilisateur inchange. Aucun diagnostic de pipeline GPU n'est detecte. `functional_pass`, navigation des cascades, preparation des 83 poissons et profondeur du vent passent. Les PNG wind-a/wind-b existent ; wind-a a ete inspecte et montre le village, la mer, les ombres et la capsule du parcours en troisieme personne. Cette vue ne constitue pas une comparaison appariee a la source.

La fenetre corrigee compte maintenant exactement 120 frames main et 120 soumissions, sans recovery ni compteur invalide. Les couches passent de [60,31,19] a [180,94,53], soit [120,63,34] ; les couches lointaines restent trop souvent encodees par rapport aux periodes source 1/2/4. Le parcours mesure 39.972923 FPS moyens, p95 28.5283 ms, 1% low 16.6206 FPS sur 148 intervalles. Le temps d'eau observe vaut 1.527253 secondes de simulation par seconde pour le reglage 1.5. La cadence/performance et le verdict global restent FAIL, sans baisse de seuil.

Les diagnostics Bevy GPU sont disponibles (120 echantillons par passe), notamment exposition, bloom, FXAA et flou ; ils excluent encore les passes Forge non instrumentees et leurs spans imbriques ne sont pas additifs. Le graphe des ombres est corrige pour que les changements de pose des poissons suivent les couches live planifiees, comme les scans ; les changements de geometrie, les reprises et le fallback sans frame live conservent les trois couches. Les poses continuent d'etre publiees dans les buffers. Un span `tidewater_object_shadow` mesure desormais l'encodage des couches selectionnees via l'API officielle locale de Bevy. SHA du graphe corrige : `4cffc6c071266406ee260cae6093f86cad6bf79e878574a8fd5d198469573bc4`. Fmt cible et diff-check passent ; ce changement moteur n'est pas encore compile ni valide sur GPU.

La premiere capture V17 refuse une mutation concurrente entre ses deux lectures, avant compilation ; aucun recu stable V17 ni verdict de build n'est invente. V18 capture 1868 fichiers (98555484 octets), dont seules les modifications du graphe d'ombres et les scripts Python hivemind different de V16. La reconstruction native est lancee sous 76372 avec les memes dependances, debug Engine 0 et liaison LLVM. Aucun nouveau package n'est produit.

Les preuves Studio du catalogue commun sont archivees, et non effacees : INDEX.json du chemin historique pointe vers `F:/OpenRPG_Forge/relocated-work/Tidewater/archives/group-0072-fee1061891d8.tar.gz`. Le bundle de recus liste le ledger v2 conserve (1025211 octets, SHA `2d86bc1d2a1d350ee61da349787ea817efa30e68d349aff526546352f1fa8995`). Son verdict historique reste celui du rapport du 5 octobre ; le contenu du ledger n'a pas ete extrait ou requalifie pendant le parcours GPU.

Le bilan V16 est maintenant conserve dans `native-walk-default-graphics-20261007-v16/performance-baseline.json` (SHA `ee491fb7fb3088a0166e402e5a34b57ab7b1e42dcf00a6ae4e8e40276adbc446`). Il distingue les valeurs demandees des vingt valeurs reellement chargees et epingle les recus, le journal et les deux PNG. Il conserve le verdict global FAIL, la fenetre 120/120/[120,63,34], les timings Bevy non additifs et la limite explicite : ce resultat concerne l'ancien graphe avant le correctif de pose des poissons, pas la reconstruction active 76372.

### Raccord du compteur GPU corrige ; reconstruction V19 (2026-10-07)

La session 76372 termine code 1 : E0599 dans le nouveau span d'ombres, `diagnostic_recorder()` renvoyant un `Option<Res<DiagnosticsRecorder>>`. Le comportement de cadence n'est pas encore qualifie. L'implementation officielle locale de Bevy 0.19.1 (`bevy_core_pipeline/src/upscaling/node.rs`) utilise le trait `RecordDiagnostics` et la conversion `as_deref()` avant `time_span`. Ce raccord est applique au graphe. Une correction equivalente est apparue en parallele ; les doublons d'import/conversion sont retires, en conservant le nom `recorder` du raccord concurrent. Le premier fmt sur le doublon echoue ; le controle cible et diff-check suivants terminent code 0. SHA du graphe actuel : `ba9c8135d59a8a45394f49450dcc566ebf4be1d8e275627172654a691c3e1384`.

V19 capture 1872 fichiers (98579018 octets). Depuis V18, les differences sont le graphe corrige, `apps/forge_studio/src/authoring_assets/source_graph_scene.rs`, un script hivemind modifie et quatre ajouts hivemind. La premiere garde refuse le changement Studio avant compilation. L'inspection confirme qu'il ne fait pas partie des entrees de l'exemple natif Engine ; tous les autres fichiers crates sont conserves. L'inventaire V19 complet et toutes ses empreintes sont ensuite reverifies sans difference, avant preparation des vingt valeurs et lancement de la reconstruction 58082. Le travail Studio et les scripts ne sont pas annules. Aucun nouveau verdict GPU ni portable n'est produit par ce lancement.

### Bibliotheque moteur V19 construite ; exemple natif en compilation

La reconstruction 58082 passe le raccord `RecordDiagnostics` : le compilateur moteur 44664 est retire et Cargo lance le vrai exemple natif sous rustc 30472 (15:00:15 locale), observe comme enfant de Cargo 46524. La bibliotheque `libforge_engine-45456b33c988b816.rlib` compte 278442690 octets, modification UTC 12:59:18 le 7 octobre. Le bilan moteur ne signale qu'un avertissement de cache incremental. Cette preuve concerne la bibliotheque ; la liaison de l'executable, le resultat final Cargo, les vingt valeurs runtime et le parcours GPU du nouveau graphe restent attendus. Le candidat retenu V16 ne devient pas retrospectivement ce nouveau binaire.

### Binaire V19 construit et parcours V21 lance (2026-10-07)

La session 58082 termine code 0 en 106m22s (7a7cf0), avec un avertissement existant de l'exemple. Le nouvel executable compte 133981696 octets ; SHA-256 `3b3ee007d14349396932f19ba951bab9dbace344722208d423854ed659961813`. Sa copie dans `.tidewater-integration/native-shadow-cadence-retained-20261007-v1/tidewater_plant_probe.exe` est identique au fichier source avant et apres copie ; retention.json conserve la preuve. Le succes du build ne constitue pas un PASS GPU ni de parite visuelle.

Le cas V19 refuse avant execution neuf nouveaux scripts hivemind (result.json, runtime_verifier_invoked=false). V20 refuse une mutation entre les deux lectures et ne produit aucun instantane JSON stable. V21 capture 1881 fichiers, 98657962 octets avec deux lectures identiques. Le delta V19/V21 comprend ces neuf ajouts et deux modifications uniquement dans `scripts/Build-StudioCandidate.ps1` et `scripts/hivemind/record_batch_followup_oct07.py` ; aucun fichier crates, exemple, manifeste ou dependance epinglee ne change. Ces scripts ne sont pas les sources compilees de l'exemple natif. Aucun travail concurrent n'est annule et la garde integrale des entrees reste active.

Le cas frais `native-walk-default-graphics-20261007-v21` conserve les vingt reglages et +0.4 EV. Le parcours natif est lance sous 81535 sur la copie identifiee ci-dessus, avec Pebbles et SourceFish. Le verdict de cadence, les timings de la passe d'ombres et les performances de ce nouveau graphe restent attendus. Aucun nouveau paquet portable n'est qualifie ici.

### Parcours V21 termine ; cadence source PASS, performance encore FAIL

La session 81535 n'est plus accessible au polling ; les recus terminaux conserves font autorite : execution.json indique exit_code=1 et pass=false, result.json indique runtime_verifier_invoked=true et runtime_graphics_values_verified=true. Le journal est epingle SHA `406993a719df818cef9ed113a435357d10787809eaf7b965a7f909e0eff560f8`. Environnement restaure, profil utilisateur inchange, aucun diagnostic de pipeline GPU detecte. Le nouveau binaire reste identique.

island.walk.json confirme functional_pass=true, cascade_navigation_pass=true, wind_depth_finite=true et source_fish_preparation_pass=true dans le recu MainWorld. La qualification SourceFish du wrapper reste false car elle exige aussi le PASS global. Les cascades passent exactement de [60,30,16] a [180,90,46] sur 120 frames main et 120 soumissions : delta [120,60,30], zero recovery, shadow_cadence_pass=true. Le correctif des poses de poissons est donc valide pour cette cadence bornee, pas pour toute la carte.

La performance reste insuffisante : 41.4927429193 FPS moyens, p95 26.9837 ms, 1% low 15.9092662726 FPS, 152 intervalles. Le seuil de 60 FPS n'est pas baisse. L'eau observee vaut 1.5267334228 secondes de simulation par seconde pour le reglage 1.5. Les deux PNG de vent sont conserves ; ils ne constituent pas une comparaison appariee a la source.

Le span tidewater_object_shadow manque dans les diagnostics retenus. L'inspection du SDK local Bevy 0.19.1 confirme que begin_diagnostics_frame est dans RenderGraphSystems::Begin et la resolution apres Render ; le systeme Forge n'etait rattache a aucun de ces sets, seulement ordonne avant camera_driver. Son rattachement explicite a RenderGraphSystems::Render est ajoute en conservant cet ordre. Cela garantit sa position entre Begin et la resolution, mais l'explication effective du timing manquant et son apparition doivent encore etre verifiees apres reconstruction. Fmt cible et diff-check passent (e46592). Ce nouveau raccord n'est pas encore compile ni teste sur GPU. Aucun gain FPS ni package portable n'est qualifie.

### Reconstruction du raccord de mesure V22 lancee

La reprise verifie que le raccord RenderGraphSystems::Render est present et que le dernier verdict reste V21 FAIL performance / PASS cadence. La compilation concurrente identifiee concerne forge_studio pour wasm32-unknown-unknown ; elle n'est pas interrompue. V22 capture 1900 fichiers (98823424 octets) avec deux lectures identiques. La reconstruction native du vrai exemple tidewater_plant_probe est lancee sous 9549 avec le cache natif existant, debug Engine 0, incremental d'exemple v4 sur E: et rust-lld. Aucun nouveau verdict de compilation, runtime, timing GPU ou package n'est revendique au lancement.

### Reprise V23 apres disparition de la session V22

Le polling de 9549 renvoie Unknown process id. L'inventaire CIM ne trouve aucun cargo, rustc ou rust-lld actif. Le binaire F: est toujours celui de V21, SHA `3b3ee007d14349396932f19ba951bab9dbace344722208d423854ed659961813`, modification UTC 13:58:14.97 ; aucun succes ni code de sortie final V22 n'est invente. Le helper sandbox echoue a creer les processus de lecture avec setup refresh had errors ; la lecture escaladee autorisee confirme ces faits.

V23 capture 1901 fichiers (98830571 octets) avec deux lectures identiques. La reconstruction est reprise sous 32597, avec la meme commande native, cache incremental v4 et linker LLVM. Cette reprise suit un arret constate, pas un timeout d'observation. Le dernier verdict GPU reste V21 : cadence source PASS, performance FAIL a 41.49 FPS. Aucun nouveau paquet n'est produit.

### Build V23 termine ; coherence des sources a reconstruire (7 octobre)

La session Cargo 32597 a termine avec le code 0 (`Finished dev` en 53m30s). Le nouvel executable est conserve dans `.tidewater-integration/native-shadow-timing-retained-20261007-v1/`, SHA-256 `989ac6c324ee7ef895c4700b0b3a20d177e5b1e11758dcb24f5ff04e23d1201b`, 133981696 octets ; les hashes source/copie sont egaux. Ceci prouve le build, pas le rendu ni la qualification des sources actuelles.

L'inventaire post-build V24 (1910 fichiers) differe de V23 notamment sur `crates/forge_engine/src/source_graph.rs` et `source_graph_position_frame.rs`, modifies a 19:39:32 UTC, apres la compilation de la bibliotheque moteur. La garde a donc refuse la qualification runtime : aucun parcours GPU n'a ete lance sur une attribution source incoherente. Les changements concurrents sont preserves.

Un nouvel inventaire stable V25 (1910 fichiers, 98907262 octets) precede la reconstruction lancee dans la session Cargo 42229 avec la meme commande et le meme cache, apres terminaison confirmee de 32597. Son verdict est en attente. Le dernier parcours GPU demeure V21 : ombres 120/60/30 PASS, performances environ 41.49 FPS FAIL. Aucun nouveau paquet portable ni verdict de fidelite visuelle.

### Build V25 termine ; isolation des sources pour sortir des derives concurrentes

La session 42229 a termine avec le code 0 en 89m29s. Executable conserve : `.tidewater-integration/native-shadow-timing-retained-20261007-v2/tidewater_plant_probe.exe`, SHA-256 `ebe22f2692614aeb76e6427d7589a897bbafc3f02c0f62daf910fb96e5ebab1e`, 133983744 octets. La copie et le fichier produit ont des hashes identiques.

Le controle V25/V26 detecte de nouveaux changements dans six fichiers moteur : source_graph.rs, source_graph_position_frame.rs, source_graph_program.rs, source_graph_shadow_routing.rs, source_graph_uv.rs, source_graph_uv_requirements.rs. Le controle refuse donc la qualification runtime des sources actuelles. Aucun test GPU n'est attribue a ce build et aucun paquet n'est qualifie.

Pour eviter une nouvelle boucle de reconstruction invalidee par les changements concurrents, une copie isolee de l'inventaire V26 a ete creee dans `.tidewater-integration/native-shadow-isolated-source-20261007-v1` : 1922 fichiers, chaque copie verifiee contre son hash epingle, receipt `isolated-inputs.json`. Les sources partagees ne sont pas modifiees. Cette copie reste a compiler et a utiliser dans un parcours explicitement epingle a sa racine ; elle ne constitue pas une preuve de build ni de rendu a ce stade.

### Snapshot isole installe et compilation lancee

La copie figee V26 est maintenant dans `E:/OpenRPG_Forge/Tidewater-Validation-Snapshot-20261007-v1` (deplacement du seul dossier de validation cree ci-dessus, aucun fichier du depot partage deplace). Son receipt `isolated-inputs.json` indique la nouvelle racine et conserve l'origine ainsi que les hashes des 1922 entrees. Le voisinage du depot permet au verifier inchange de retrouver les paquets dans `.tidewater-integration`.

Compilation lancee depuis cette racine dans la session 88164, meme profil optimise, cache Cargo F existant et incremental de l'exemple distinct `native-example-incremental-isolated-20261007-v1`. Verdict en attente. Le helper derive `PrepareIsolatedNativeWalkGraphics20261007.py` accepte une racine explicite, exige sa correspondance au receipt, conserve la garde des 20 reglages et permet de reprendre l'ancre de rejet depuis le depot d'origine identifie. Il ne modifie pas les entrees compilees ni les seuils du parcours. Syntaxe Python verifiee ; preparation et GPU non executes a ce stade.

### Preparation du parcours isole verifiee

Le cas `native-walk-isolated-default-graphics-20261007-v1` est prepare avec les vingt valeurs graphiques explicites et +0.4 EV. Son preflight du 7 octobre a 21:33:10 UTC confirme les 1922 empreintes des sources figees, les sept empreintes d'entrees des paquets et la presence des repertoires de scene requis. Ces controles ne couvrent ni tous les assets ni le rendu GPU ; runtime_executed reste false. Le helper derive a ete corrige avant la preparation effective pour conserver la comparaison des deux racines autorisees, sans modifier les entrees compilees.

La session 88164 est toujours active lors de cette reprise : le handle renvoie une session en cours, et CIM confirme Cargo 37204 et son enfant rustc 8072 apres la compilation de forge_navigation, pendant forge_world. Il ne s'agit pas d'un nouveau succes de build. Le prochain parcours doit utiliser exclusivement l'executable termine et conserve issu de cette compilation, puis reverifier les empreintes et les valeurs chargees. Aucun seuil, inventaire de vegetation ou verdict global n'est abaisse pour contourner le FAIL de performance V21.

### Bibliotheques du snapshot compilees ; moteur en compilation (8 octobre)

La session isolee 88164 reste active et annonce maintenant `Compiling forge_engine` depuis `Tidewater-Validation-Snapshot-20261007-v1`, apres les bilans forge_world et forge_assets sans erreur terminale. Cela confirme la progression vers le moteur incluant le raccord de mesure des ombres ; le resultat Cargo final, la liaison de l'exemple et le test GPU restent non obtenus. Aucun verdict runtime ou portable n'est ajoute sur cette seule sortie.

### Lenteur de compilation : mesure bornee des entrees/sorties

Pendant la compilation forge_engine de la session 88164, CIM confirme Cargo 37204 et rustc 2156 actifs. Un echantillon des compteurs Windows du seul rustc 2156 donne 0 % CPU, 3479443 octets/s lus et autant ecrits, working set 25071616 octets. Le volume F: donne environ 1002785 octets/s lus, 4058147 ecrits et une file disque de 7. Cet echantillon confirme des entrees/sorties pendant l'attente ; il ne prouve pas a lui seul leur cause ni une saturation permanente. Les avertissements Cargo precedents indiquent un fallback de hard links vers des copies sur F:. Le build actif est conserve, sans nettoyage ou deplacement de cache. Ces chiffres concernent la compilation, pas les FPS du jeu.

### References visuelles recuperees separement (8 octobre)

Le dossier neuf `.tidewater-integration/paired-reference-baseline-20261008-v1` contient les 14 fichiers de baseline-003, recuperes depuis l'archive group-0022-d6bc3cbfa086.tar.gz. Chaque contenu extrait puis relu a une empreinte identique aux deux empreintes du recu archive ; extraction session 77220 code 0. recovery.json conserve les noms, tailles, empreintes et provenance. Les archives, jonctions et anciens dossiers sont inchanges. La verification couvre les charges recuperees, pas une nouvelle verification du SHA global de l'archive ; cette limite est explicite. Les PNG et poses sont des references historiques, jamais une preuve de rendu du binaire en compilation.

### Bibliotheque moteur isolee terminee ; exemple natif actif

La session 88164 emet le bilan forge_engine (lib) avec un avertissement, puis le compilateur moteur 2156 est remplace par rustc 38964, enfant du meme Cargo 37204. Le resultat final Cargo et la liaison de l'executable restent attendus. Aucun verdict GPU ne decoule du seul bilan de bibliotheque ; le dernier parcours mesure demeure V21 et sa performance FAIL.

### Build isole PASS ; parcours GPU lance

La session 88164 termine code 0, Finished dev en 86m45s (6dc9f7). Les 1922 fichiers compiles du snapshot et son inventaire sont reverifies sans difference. Le binaire nouveau est conserve dans `.tidewater-integration/native-shadow-isolated-retained-20261008-v1/tidewater_plant_probe.exe`, 134003200 octets, SHA-256 `5d53139238cd024ab1c4fea69066a4fedf4c5d30afbb9486cacb32ab030e577f`. Les hashes avant copie, copie et apres copie sont egaux ; retention.json epingle la racine isolee et isolated-inputs.json SHA `01031bce2b3a3b92717f1d48d2ddf0d604c95691e367b6976528610e6a491041`.

Le parcours `native-walk-isolated-default-graphics-20261007-v1` est lance sous la session 45589 via le wrapper inchange, avec les vingt valeurs, +0.4 EV, Pebbles et SourceFish. Il utilise la racine figee, pas le depot partage concurrent. Le resultat GPU, l'apparition du timing tidewater_object_shadow, les FPS et les captures restent attendus. Le build PASS ne qualifie pas automatiquement la carte entiere, Studio ou le paquet portable.

### Parcours isole termine FAIL performance ; timing des ombres maintenant visible

La session 45589 termine code 1 (e32cc0). result.json confirme les vingt valeurs reellement chargees, environnement restaure, profil utilisateur inchange et aucune erreur de pipeline GPU detectee. execution.json confirme binaire et sept entrees de paquets inchanges. Le recu walk SHA `1d2a8ff085b58e68507e45f95958c804bb11b2a4f278342ee882b1aa65529ae3` conserve functional_pass, cascade_navigation_pass, shadow_cadence_pass, wind_depth_finite et source_fish_preparation_pass MainWorld vrais. Le wrapper SourceFish reste refuse car le PASS global est requis.

La fenetre d'ombres est de 133 frames main / 133 soumissions, delta de couches [133,67,33], zero recovery, invalid=false, shadow_cadence_pass=true. Ce n'est pas la fenetre 120/120 du precedent candidat. Les historiques de diagnostics contiennent maintenant 120 echantillons de tidewater_object_shadow : CPU moyen 0.1959541667 ms, GPU moyen 0.2763516667 ms. Le raccord du span est donc verifie sur GPU. Les autres passes Forge sans span restent exclues ; les spans imbriques ne s'additionnent pas en temps de frame total.

La performance reste FAIL : 44.9699318388 FPS moyens, p95 26.3034 ms, 1% low 19.2751391183 FPS sur 160 intervalles, 1920x1080, test_throttle_30hz=false. La difference avec V21 n'est pas un gain causal qualifie. performance-baseline.json conserve le bilan du binaire isole. Les PNG wind-a/wind-b existent ; ils restent a inspecter et ne correspondent pas a la camera Village de reference. Aucun paquet portable n'est qualifie. Suite : mesurer les autres passes Forge et le cout CPU pour localiser le temps de frame restant, puis comparer les vues/horloges source.

### Quatre passes de calcul instrumentees ; candidat isole V2 lance

Le diagnostic du candidat V1 montre une passe d'ombres courte mais ne couvre pas encore toutes les passes Forge. L'inspection de walk.rs confirme que la selection residente des plantes est declenchee apres un deplacement/rotation suffisants, pas a chaque frame. Les PNG V1 ont ete inspectes : ombres de batiments, village, eau et vegetation sont visibles, mais la camera du parcours differe de Village ; aucune parite n'est deduite.

Des spans optionnels RecordDiagnostics sont ajoutes dans les sources actuelles a tidewater_ocean, tidewater_atmosphere, tidewater_clouds et au calcul couple tidewater_shore_spray. Leurs systemes sont places dans RenderGraphSystems::Render, avant camera_driver, en conservant les dependances existantes. Les retours anticipes apres encodage du spray ferment le span avant sortie. Aucun shader, reglage, densite, seuil ou ordre des kernels n'est modifie. Le span nuages exclut la reconstruction d'observateur precedente ; le span shore_spray regroupe simulation de rive, emission/integration, feedback et publication. Les spans imbriques restent non additifs.

Les gardes de patch confirment que les quatre fichiers avant modification correspondent aux empreintes V1. Le premier controle PowerShell est refuse par sa syntaxe avant execution ; la commande corrigee passe rustfmt cible et diff-check (ac6ac2). Il s'agit de controles de syntaxe/format, pas d'un build ou test GPU.

`Tidewater-Validation-Snapshot-20261008-v2` conserve les 1922 sources V1, avec uniquement ces quatre changements diagnostiques ; chaque copie est reverifiee (58527 code 0). Le snapshot V1 et ses recus restent intacts. Le nouveau parcours `native-walk-compute-timing-20261008-v1` est prepare avec les memes vingt valeurs et +0.4 EV (215837), runtime_executed=false. La compilation du vrai exemple est lancee depuis V2 sous 68138, cache Cargo natif existant et incremental d'exemple V2 distinct. Son verdict et l'apparition effective des quatre mesures restent attendus. Aucun gain de performance ni paquet nouveau n'est qualifie.

### Diagnostic Village : camera corrigee, preparation incomplete detectee (8 octobre)

Le binaire V1 teste est lance dans deux cas neufs via CaptureVillageDiagnostic20261008.py, avec environnement enfant isole et profil graphique copie. La pose Village provient exactement du metadata recupere ; les sources du snapshot V1 et les sept pins de paquets sont verifies avant lancement. Aucun fichier compile du snapshot V2 actif n'est modifie.

Le premier cas native-village-diagnostic-20261008-v1 termine code 1, sans PNG : le reframe de scene seul omet les dependances voisines du stand. Le second cas v2 copie et verifie fish-stand, fish-stand-content et fish-stand-fish, puis termine code 101, sans PNG. Le journal indique une assertion grass.rs:193 : les cellules selectionnees pour la camera Village different de l'oracle conserve pour seaward-clear004. Il signale aussi scene/joe.glb absent : ce payload existe dans la scene originale mais n'est pas copie par l'outil, qui ne copie que fixture.files. Ces echecs concernent la preparation de comparaison ; ils ne remplacent pas le verdict du parcours V1 a 44.97 FPS.

Le generateur export-grass.mjs permet d'acquerir les cellules directement depuis le JavaScript source fige, sans utiliser le resultat Rust comme oracle. Il exige un arbre source propre. Le clone reference est au bon commit 1438b1ab mais contient un HTML de capture non suivi ; il reste intact. Suite : utiliser une copie propre du source pour regenerer l'oracle camera, verifier les autres payloads byte-identiques, inclure les dependances GLB hors manifeste, puis relancer un nouveau cas. Aucune suppression d'assertion, omission d'herbe, image ou parite revendiquee. Les recus et journaux des deux echecs sont conserves.

La compilation V2 68138 reste active au dernier polling : forge_core compile, forge_gameplay en cours, fallback de hard links vers copies annonce. Aucun verdict final de build ni nouvelles mesures GPU obtenus.

### Village V3 capture sur GPU ; diagnostic au cadrage source obtenu

La copie locale reference-village-clean-20261008-v1 est au commit source 1438b1ab, propre ; ses dependances sont reutilisees par une jonction node_modules ignoree, sans modifier le clone precedent. export-grass.mjs refuse la regeneration complete : Terrain differs. Ce refus est conserve et reste a expliquer ; aucune equivalence du terrain regenere n'est revendiquee.

VillageGrassSelection20261008.mjs execute la methode GrassField.update intacte du source sur flags/min/max de la grille exportee figee. Un controle independant retrouve exactement toutes les cellules de l'ancien cadrage avant de calculer Village [9,28,90]. Le recu village-grass-selection-20261008-v1.json epingle source, methode, harness et donnees. Il qualifie cette selection camera sur grille figee, pas la regeneration complete ni le rendu source.

CaptureVillageDiagnostic20261008.py prepare le cas neuf native-village-diagnostic-20261008-v3 : seuls camera et cellules attendues de grass.json changent avec hash fixture actualise ; assertions natives conservees, Joe GLB et trois dossiers voisins copies et verifies. L'execution 89156 termine code 0, PNG natif SHA b99fee966e4aa269cfc99d8ec5d0106bb78e0a4336b2beb4b06bce472f9399be, binaire et sept pins inchanges. Les deux PNG ont ete inspectes : cadrage et principales constructions concordants, differences de feuillages et aspect plus granuleux du natif. Le journal ne contient pas d'ERROR/panic/Validation Error ; un avertissement de readback sur canal ferme apparait a la fermeture.

comparison.json donne, en RGB affiche 1920x1080, luma moyenne native 112.81786 / reference 109.82385 (+2.73 %), MAE 9.43020 et RMSE 13.78457 sur 255, fraction de pixels avec un canal delta>16 de 0.217446. Ces chiffres sont diagnostiques sans seuil d'acceptation. Horloges et tous les reglages d'eclairage ne sont pas encore apparies ; profil prepare ne remplace pas un recu des vingt valeurs chargees. Aucune parite globale, qualification de performance, Studio ou portable. Suite : synchroniser les horloges et isoler post-traitement/vegetation avant ajustements.

### Vent statique divergent identifie ; raccord de pose acquise ajoute, non compile

Le diagnostic du code confirme que setup du probe V1/V2 utilise wind=[1,0,6,2] et gustLod.xy=[0,0]. Frame.js source definit force 7 et direction normalisee [0.35,0.94]. L'acquisition animation-reference-village-1438b1ab.json retient global_time 2 s, direction [0.3489373591,0.9371460502], gustOffset [4.6152781366,12.3953184240], frame 124. La phase temporelle seule ne suffit donc pas a apparier les plantes.

Dans les sources canoniques, un mode facultatif FORGE_TIDEWATER_STATIC_SOURCE_FRAME=1 lit AcquiredFrame valide et refuse les projections Walk/WalkSmoke/WalkLongSmoke. setup publie cette pose dans les materiaux visibles ; install_input du graphe d'ombres applique la meme pose comme initial_motion aux plantes. Les six bits de frame sont conserves. Les anciens modes et le vent de jeu conservent leurs chemins. Fmt cible et diff-check passent (ec80d8). Ce raccord est non compile et non teste sur GPU ; il n'est pas ajoute au snapshot V2 actuellement en compilation. Eau, nuages, pendules et autres horloges restent hors de cette correction.

### A/B grain et accentuation : contribution faible sur Village

Le nouveau cas native-village-no-grain-sharpen-20261008-v1 termine code 0 (48969), PNG SHA 67fd69ef00924ef28b67b44936cc35e323ec42106966f61795fd277735aa7838. Les deux seules valeurs preparees modifiees sont film_grain=0 et sharpen=0 ; le profil utilisateur et les sources compilees restent intacts. PNG inspecte et aucun ERROR/panic/Validation Error dans le journal.

comparison.json donne MAE reference 9.141663 contre 9.430199 avec les reglages prepares standards ; MAE entre les deux images natives 0.584570 sur 255. La suppression conjointe des deux effets ne supprime donc pas le principal ecart observe ; elle n'isole pas leur contribution individuelle et n'autorise aucun changement par defaut. Il manque toujours le recu runtime des valeurs statiques et l'appariement complet des horloges. Pas de verdict de parite ni de performance ajoute.

### Relief canonique et regeneration complete de l'herbe verifies

Le refus precedent export-grass.mjs Terrain differs ne prouvait pas un ecart numerique : la scene moderne n'a plus de fichier heights.bin, fourni au runtime par workspace_vegetation::load depuis SourceBlobStore. Le document terrain.json epingle sampled_source.samples_sha256=be03f5db50e1fb328ac2c030348626103b52887efad27a3014505d37583594a3. Le diagnostic initial cherche l'ancien nom et echoue avant generation ; aucune sortie de comparaison n'est revendiquee pour cet essai.

DiagnoseTerrainPhases20261008.mjs verifie maintenant le descripteur et le blob canonique du workspace. Son resultat terrain-source-phases-20261008-v1.json confirme les 4194304 hauteurs strictement identiques apres Village et apres Vegetation : SHA identique, zero echantillon different, delta maximal zero. Avant Village, 3324 echantillons differaient autour des pads, delta maximal 0.568967 m ; cet aplatissement est bien celui du source. Cela leve l'incertitude du relief regenere CPU dans cet environnement Node, sans qualifier son rendu ou toute l'edition.

Le generateur canonique scripts/tidewater/export-grass.mjs accepte desormais un WORKSPACE_ROOT explicite pour les fixtures modernes. Il valide terrain.json par son pin, encoding/dimensions du raster et SHA/taille du blob avant GPU, puis compare la regeneration source a ce pin. Le chemin historique heights.bin reste accepte ; aucune garde de terrain ou source propre n'est supprimee. Le snapshot V2 actif reste inchange.

L'extraction complete village-grass-complete-oracle-20261008-v1 termine code 0 (081090), syntaxe Node verifiee. validation.json (260d5f) confirme les 26 payloads de geometrie, programmes d'herbe, flags, min/max et masque tous byte-identiques aux fichiers retenus dans la scene native, sans manque ni difference. Les cellules source [9,28,90] concordent avec la methode source independante et l'oracle utilise par la capture V3. Le metadata complet a SHA ef01549b48aff7a4f6498b048166327382107df2483540218b9b8b81cc9bd927. Les limites des anciens recus ne sont pas effacees ; ce nouveau resultat apporte la preuve manquante de regeneration des donnees CPU, pas de vent synchronise, de GPU source appaire ou de parite globale.

### Recu runtime statique ajoute aux sources ; verification preparee

La sonde canonique publie maintenant StaticVegetationPose depuis les valeurs effectivement affectees aux materiaux dans setup. Au moment de demander la capture, un nouveau fichier *.capture.json conserve les vingt valeurs de GraphicsState charge, la camera configuree, le vent/rafales/frame visibles et le numero de frame MainWorld. L'ecriture create_new refuse d'ecraser une preuve et son echec termine la sonde en erreur. Le recu distingue les valeurs CPU publiees, la demande de capture et l'image asynchrone ; il ne revendique ni lecture d'uniformes GPU ni synchronisation des autres horloges. Les parametres supplementaires sont groupes dans un tuple pour conserver l'arite du systeme existant.

CaptureVillageDiagnostic20261008.py accepte un mode acquired explicite, attend alors le recu nouveau, compare toutes les valeurs graphiques preparees et la pose de vent acquise (direction/force/temps, decalage des rafales et six bits de frame), et refuse de valider ce mode sur un ancien binaire sans recu. Les cas anterieurs et leurs verdicts restent conserves. Syntaxe Python PASS, rustfmt cible skip_children et diff-check PASS (012249). Ce nouveau code et le raccord de pose precedent restent a compiler et tester sur GPU ; aucun mode acquired n'est lance sur V1/V2.

La compilation 68138 du snapshot V2 immutable reste active au polling suivant forge_assets ; elle ne contient pas ces nouvelles modifications statiques. Son verdict final et les quatre timings demeurent attendus.

### Candidat V3 fige ; regression preparee, aucune compilation concurrente lancee

La comparaison directe V2/canonique des deux fichiers Rust confirme uniquement les ajouts de pose acquise et de recu statique attendus. FreezeStaticPoseSnapshot20261008.py cree le snapshot distinct Tidewater-Validation-Snapshot-20261008-v3, 1922 fichiers copies et verifies, seulement trois changements importes. Le snapshot V2 en compilation reste intact. Les changements concurrents hors de cette liste ne sont pas importes.

La session 6002 termine code 0. isolated-inputs.json conserve les empreintes avant/apres : tidewater_plant_probe.rs 7e67d6994de92546f2e590e570fb46affefe730b45f0a775b489303fb7542c37 -> 26e28554add3c84363ae35a57bcaa2d6ba28717493956aaf1e985b3a155963df ; object_shadow_probe/mod.rs d811b7f0bfbe784e60281168b316b0eaaf5a180f517d53cc1db6af3ca5db5918 -> f9a608ffe3575ba305c0de86ac619beca8826345a29958959e963203b48a71f5 ; export-grass.mjs a3dc404f47157199745cf088bee4b909850803ed8528b214ae017da30531556b -> 9f7af6bf6e05ce6eb131e1203195fb74de4a798ab5760afc26d7fdaf2a24fa92. Aucun verdict compile/runtime attribue a cette copie.

Le cas native-walk-static-pose-regression-20261008-v1 est prepare depuis V3, vingt valeurs standard et +0.4 EV, runtime_executed=false (12502a). Il servira a verifier que les ajouts statiques ne perturbent pas la promenade. La capture acquired devra ensuite choisir exclusivement le nouveau binaire conserve et attribue a V3 ; le helper ne doit pas reutiliser V1 pour ce mode.

La session 68138 du candidat V2 reste en cours ; CIM confirme rustc 17080, enfant du Cargo 9128, pendant forge_assets (0b5fdc). Aucun autre Cargo n'est lance contre le meme cache. Attendre son vrai verdict, conserver et verifier son executable, puis executer ses mesures GPU avant de construire V3.

### Selection du binaire de comparaison rendue explicite ; gardes rejouees

CaptureVillageDiagnostic20261008.py accepte maintenant le retention.json du candidat selectionne. Il exige build_exit_code=0, compiled_inputs_unchanged=true, hash/taille du binaire, hash du manifeste et correspondance de sa racine, nombre et empreintes des sources. Le mode acquired exige aussi la presence des modifications static_pose_patch attribuees au candidat. Les sources sont reverifiees apres capture. Sans recu build, le refus precede creation du cas et lancement GPU (14cc2f PASS).

Le premier controle negatif avec le recu V1 echoue sur le message attendu : le candidat est refuse avant creation/GPU par KeyError static_pose_patch, car l'assertion explicative etait trop tardive. Elle est deplacee avant l'acces ; le rejeu c02c91 passe, ancien build V1 refuse explicitement, aucun cas cree et aucun GPU lance. Les recettes precedentes restent conservees.

### Essai TAA statique : ecart RGB reduit, aucun changement de valeurs par defaut

Le source PostFX.js emploie TemporalUpscale dans sa chaine et son jitter a chaque frame, tandis que le profil natif standard utilise FXAA et temporal_aa=0. Le cas native-village-temporal-aa-20261008-v1 prepare seulement temporal_aa=1 et fxaa=0 dans une copie de profil ; sources et profil utilisateur intacts. Session 74782 termine code 0, PNG SHA a69bc614b855ad27282ca24cff79b4cda4e84863455bbbd0096d8cf496168c5b, image inspectee. Aucun ERROR/panic/Validation Error dans le journal.

comparison.json donne MAE reference 8.159292 avec TAA, contre 9.430199 pour la capture standard, RMSE 12.298069 ; difference entre images natives MAE 4.581160 sur 255. L'essai n'est pas une reproduction du resolve TAAU source ; V1 n'emet pas encore de recu des valeurs statiques chargees, le vent n'est pas appaire et aucune mesure en mouvement ou FPS n'est fournie. Aucune parite ni valeur par defaut modifiee. Suite : comparer les nouvelles captures acquises et verifier l'historique temporel pendant le jeu avant toute promotion.

### Analyse des futurs timings preparee ; cout CPU hors spans explicite

L'inspection de walk_wind::update confirme que Sources::frame selectionne le terrain et les plantes puis construit les streams avant animation::submit_frame. Les geometries statiques sont deja retenues ; le chemin select_observed reconstruit les enregistrements/modeles et leurs observations a partir des IDs selectionnes. Ce travail MainWorld est hors du span render/tidewater_object_shadow/elapsed_cpu. Aucun cout mesure ni cause dominante n'est deduit de cette inspection, aucune densite/portee/cadence changee.

AnalyzeComputeTiming20261008.py lit les recus result/preparation/execution/walk, reverifie les empreintes du binaire, du walk et du journal, controle des statistiques finies et la presence separee des cinq spans CPU/GPU attendus. Il conserve les intervalles d'affichage et liste les spans GPU sans les additionner ; --require-spans refuse une couverture incomplete. Le rejeu sur V1 (bce95a) conserve global_walk_pass=false et required_spans_present=false : seul object_shadow est mesure, les huit mesures CPU/GPU des quatre nouveaux calculs manquent bien. compute-timing-coverage.json est une analyse des preuves V1, pas un nouveau parcours.

La session 68138 reste active en compilation forge_engine. Aucune optimisation n'est attribuee a ce travail preparatoire, aucun seuil de 60 FPS baisse et aucune qualification portable ou visuelle ajoutee.

### Selection CPU des casters mesuree independamment, pas de reduction de portee

MeasureCasterSelection20261008.py extrait sans modification les fonctions select/value du fichier plants.rs du snapshot V2. Il verifie les dix blobs canoniques de vegetation (65383 instances), reconstruit seulement les positions iPos et rayons qr2 de base selon le packing documente et source_order, puis mesure les cinq groupes de casters. Understory est exclu comme dans le chemin courant ; aucun fichier compile par Cargo ni reglage graphique n'est change.

Les sessions 535137 et 0d19e3 terminent code 0. Les recus caster-selection-cpu-20261008-v1/result.json (opt-level=3) et caster-selection-cpu-opt1-20261008-v1/result.json (opt-level=1 du profil dev courant) conservent les pins de sources, blobs, streams, harness et executable, 25 mesures de 500 repetitions chacune. profile-comparison.json confirme les fonctions, inputs et harness identiques entre profils. Au point [42,-90], la somme des moyennes individuelles vaut 0.124885 ms (opt3) et 0.142477 ms (opt1). Les cinq points opt1 vont de 0.120486 a 0.168753 ms.

Ces mesures ne couvrent ni packing des streams/observations, ni terrain CDLOD, ni ECS/GPU, ni projection/corrections d'auteur. Les cameras sont diagnostiques et le build actif peut concurrencer le CPU. Il ne s'agit pas d'un span du vrai parcours, d'un gain runtime, ni d'une preuve que toute la preparation des ombres est peu couteuse. Rien ne justifie sur cette seule mesure de reduire la portee ou la densite des arbres ; aucune optimisation de ce chemin n'est promue.

La compilation 68138 reste effectivement live : Cargo 9128 et rustc 17080 forge_engine (opt-level=1) sont confirmes par CIM a 02:03:51 heure locale le 8 octobre. Les compteurs CPU/IO ont progresse depuis l'observation precedente, F est exFAT et conserve environ 1.49 To libre. La session n'est ni arretee ni redemarree sur timeout ; le verdict final et les timings GPU V2 restent attendus.

### Ecarts des entrees statiques ocean/nuages identifies avant raccord acquis

AuditStaticAnimationInputs20261008.py termine code 1 (1b8979) et conserve static-animation-input-audit-20261008-v1.json. Le frame-120-params.bin courant est verifie contre ocean.json : temps FFT final 2.0 s, reference App 2.066666666666664 s, ecart -0.06666666666666421 s. Les entrees nuages sont cold_120_frames, sans controller_frames explicites ; camera opposee de 180 degres a Village, direction du vent [1,0] differente de la reference acquise. Les quatre controles sont faux. C'est un audit de donnees preparees, pas une lecture d'uniformes GPU ni un nouveau verdict visuel. La pose vegetation V3 seule ne corrige pas ces ecarts.

Le rapport 2026-10-05-animation-clock-and-wind.md documente deja l'acquisition complete et les paquets residents environnement/ocean a 124 frames. Leur preuve individuelle ne vaut pas adoption dans le chemin statique actuel, qui utilise encore les anciens adapters des exemples et les anciennes entrees. Il faut raccorder la scene complete a ces paquets/historiques et comparer ensuite les images, sans simplement remplacer le temps final ou reinitialiser les historiques.

RecoverAcquiredVillage20261008.py selectionne 41 fichiers capture du run source v11. Le premier essai 718861 refuse avant creation de destination : deux membres archive sont des hardlinks de deduplication. Le diagnostic 13f37a confirme les memes SHA source/retained, village.json b69d7129cb8ba7a4ed2a17045255838bb31c90f5a01e575a29386bd9d6f6cb73. Le lecteur corrige resout uniquement les liens explicites du recu, exige empreintes/tailles identiques a chaque lien et lit le membre regulier correspondant, sans extraction de liens ni de chemins tar non controles. La session 64557 de recuperation est en cours ; aucun PASS final attribue. Build 68138 demeure active et son snapshot intact.

### Acquisition complete recuperee et controleur courant rejoue exactement

La recuperation 64557 termine code 0 (8363dd) : 41 payloads verifies, 124 frames source, village.json SHA b69d7129cb8ba7a4ed2a17045255838bb31c90f5a01e575a29386bd9d6f6cb73. recovery.json conserve les membres reguliers et les empreintes ; le SHA complet de l'archive n'est pas recontrole. PNG source village.png inspecte : village et montagne, ombres de maisons et palmiers, herbe. Cela ne valide aucune image Forge nouvelle.

ReplayRecoveredCloudSequence20261008.py est une adaptation explicite du helper existant ReplayAcquiredCloudSequenceV2.py : entree v11 recuperee, recu recovery verifie, nouvelle destination et controleur du snapshot V3 fige. replay-recovered-cloud-adaptation-20261008-v1.json conserve les quatre substitutions et empreintes des deux drivers ; l'original est inchange. Le controleur source est copie sans modification, compile seul avec Rust 1.98 et rejoue les 124 inputs binaires observes.

Session b40dfe code 0, cloud-controller-acquired-replay-20261008-v1/result.json statut EXACT : les 372 blocs frame/settings/public correspondent bit a bit aux miroirs uniforms effectifs de l'App source, zero bloc different. Les fichiers du controleur et de l'acquisition restent intacts. Aucun Cargo concurrent ni GPU lance ; native_gpu_replay_verified=false et visual_parity=false. Cette preuve confirme la capacite du controleur courant a reproduire la sequence complete, mais son raccord dans la capture de scene et la comparaison des textures/images restent a faire.

### Paquets acquis recuperes pour le raccord de scene

RecoverAcquiredPackage20261008.py restaure uniquement package/ des deux candidats historiques, dans de nouvelles destinations E. Environnement : session 25655 code 0 (a95d62), 275 fichiers dont manifeste SHA b688075e65db9f46b2fefab8a5cd982adead6496096ce0c26b764d75a4e2b8df. Ocean : 4d13eb code 0, 261 fichiers dont manifeste SHA b54f26065b796c6e4731a415048739263c3416ab8bdd3a364927c26961f1656e. Chaque payload est reverifie contre les empreintes source/retained du recu archive ; les liens dedupliques sont resolus vers un membre regulier verifie sans extraction de lien. Les archives et anciens candidats sont preserves.

Le controle 40da70 relit ensuite les manifestes et les 534 payloads (274 environnement, 260 ocean), SHA et tailles exacts, version 2 et bootstrap_frames=124. acquired-packages-source-sequence-20261008-v1.json confirme tous les blocs globaux/atmosphere des 124 frames environnement et le dernier public byte-identiques a l'acquisition v11 ; chaque temps FFT ocean correspond au temps source observe a 1e-6 pres. Pas d'execution GPU nouvelle ni adoption de scene. Les shaders, le controleur, les cubes et le pipeline resident existants permettent maintenant le raccord aux entrees acquises sans recreer ou remplacer le moteur.

La compilation 68138 reste live ; forge_engine lib termine avec un warning (3904f2), executable de sonde encore attendu. Le candidat V2 immutable n'est pas modifie et aucun second Cargo n'est lance contre son cache. Les nouvelles recuperations concernent les donnees seulement et ne sont pas presentees comme des corrections runtime deja validees.

### Adapter de scene statique vers le paquet environnement acquis

Le nouveau module examples/tidewater_plant_probe/acquired_environment.rs charge un EnvironmentPackage par chemin et SHA explicites, via le lecteur canonique, puis PreparedEnvironment::new. Il refuse path/hash incomplets, mode non statique, absence du mode acquired, camera finale differente (position/bases/FOV) ou fichiers absents. Apres validation complete, seuls cube HDR, village-lighting.json, cloud-view-params.bin et sky.wgsl remplacent les derives de la scene. Les documents et assets sources restent intacts. Le graphe atmosphere/nuages commun est installe avec les inputs/schedule complets du paquet et la continuation conservee ; le chemin standard est conserve lorsqu'aucun paquet explicite n'est choisi.

Le recu capture conserve manifest_sha256/bootstrap_frames/camera_checked/remplacements et le compteur cloud_completed_steps reel au moment de la demande d'image. CaptureVillageDiagnostic20261008.py ajoute un mode acquired-environment : il exige un build attribue au patch, verifie le paquet et ses 274 payloads avant lancement, prepare les variables uniquement pour le processus enfant et exige au retour le recu du manifeste et 124 etapes encodees, en plus des valeurs graphiques/pose vegetation. L'ocean et les autres historiques ne sont pas pretendus synchronises par cette modification.

Premier type-check standalone 0f5c97 echoue sur proc-macro Resource : CARGO_MANIFEST_DIR absent dans le harness, pas une erreur d'API de l'adapter. Le rejeu a90dde fournit le contexte manifeste du snapshot V3, copie sans changement le vrai module et utilise les bibliotheques SDK retenues : emit metadata code 0, sources intacts. Ce PASS de types ne compile pas toute la sonde, ne lie aucun jeu et ne lance aucun GPU. Rustfmt cible et diff-check PASS (1aa730). Syntaxe helper Python PASS ; le controle negatif 2ec008 refuse un mode acquired-environment sans recu build avant creation de cas/runtime.

FreezeAcquiredEnvironmentSnapshot20261008.py termine code 0 (9f6af1) : snapshot V4, 1923 fichiers verifies, V3 plus exactement deux fichiers revus. main probe SHA c72e353f67071b5b0f28f587c7f049fb1d518a17a9841b588bcbe61ab5dc5c4e ; nouveau adapter SHA 90f6060dfcdf5aca773cf384ef6ef72cfa9ef3e06b43669b1aaec73e97e77557. acquired_environment_patch conserve avant/apres, V2/V3 immutables et autres changements canoniques exclus. Aucune compilation V4 ou adoption GPU encore acquise. La session 68138 reste live avec rustc 37608 de la sonde V2 ; aucun nouveau Cargo partage n'est lance.

### Le chemin FFT de la sonde etait hors des mesures V2 ; raccord diagnostic corrige

L'inspection confirme que la scene tidewater_plant_probe installe examples/tidewater_ocean_spectrum_probe/mod.rs, et non le graphe resident src/tidewater_ocean.rs instrumente dans V2. Les mesures V2 ne pourront donc pas prouver la couverture de ce FFT. Aucun timing absent ne sera interprete comme un cout nul ; le controle --require-spans conserve son exigence.

Le graphe de l'exemple est rattache a RenderGraphSystems::Render sur ses deux installations, en conservant OceanComputeSet et l'ordre avant camera_driver. Un span optional tidewater_ocean entoure les commandes FFT avant publication du compteur. Il se termine avant les branches de retour ; aucune horloge, cadence, densite, portee ou seuil n'est change. Les copies finales de readback diagnostic sont hors du span. Rustfmt cible/diff-check PASS (d472ea).

Le premier harness metadata 5ff2c3 omet le SDK wgpu et echoue sur les references wgpu deja presentes dans le module. Le rejeu 910ec6 ajoute la dependance exacte retenue et passe code 0, avec le vrai module et son live_clock, sources intacts, aucun jeu lie ni GPU execute. Ce PASS de types ne prouve pas que le span apparait dans une execution.

Le snapshot V5 derive de V4 ajoute uniquement ce fichier, 1923 fichiers verifies, ocean_example_timing_patch SHA avant 7c5c7dc83ac486e2b872f899548c71f81c4431f3a7f3832d91bb3a0e4fb99e5b / apres 0af4d17667af31d4d5677a6ec8306f289eccac7d8a1ed062bf327e117f2beee5 (0293fe). V2/V3/V4 restent intacts. native-walk-all-compute-spans-20261008-v1 est prepare (0a2756), vingt valeurs standard/+0.4 EV, runtime_executed=false. Apres les mesures du build V2 actif, preferer ce candidat combine pour la prochaine compilation plutot que compiler successivement les candidats intermediaires V3/V4 ; ces copies restent conservees et non qualifiees.

L'etude du raccord ocean acquis confirme que le consommateur couple shore/spray suppose encore 120 etapes et suit le permitted_step de l'ancien FFT. Passer uniquement le FFT a 124 sans adapter cette transaction interromprait l'historique ou lirait une mauvaise etape. Le paquet ocean acquis est pret ; son adoption dans la scene complete reste a realiser avec ce consommateur. Aucun temps final n'est force pour simuler une parite d'historique.

A 02:32:44 locale, Cargo 9128/rustc 37608 sont live et rust-lld 24248 est enfant du rustc : build V2 en liaison, sans redemarrage ni second Cargo.

### Durée de preparation couplee explicite ; default 120 conserve, raccord 124 encore absent

Le Target FFT de l'exemple expose bootstrap_steps provenant de ses inputs. Le consommateur spray lit cette valeur a la configuration, refuse une duree hors 1..4096 et refuse un budget enregistre dont le nombre ne correspond pas. Sa valeur historique initiale reste 120. Allocation du buffer des compteurs, transition vers le live, limite du readback strict, publication ready, snapshot et taille attendue des sorties suivent maintenant la meme duree. Les noms/modes/recus restent identiques pour 120 et deviennent explicites pour une autre duree ; aucune sequence 124 n'est activee par cette seule modification. Le paquet acquis et ses globals restent a installer dans le FFT et ses consommateurs.

L'application initiale de patch spray est refusee sur une ligne de metadata longue absente du hunk ; seul le changement FFT precedent est applique. Le patch spray est reapplique avec des sections precises et remplacements bornes verifies par compte (769ffc), puis fmt cible passe (aa307c). Diff-check PASS (9c748a). Ce changement n'est pas presente comme une regression GPU deja validee.

Snapshot V6 fige (84e649) : 1923 fichiers, V5 plus deux fichiers revus. FFT example SHA 9d2416263f9de8c65a0a5ef650d31fff95302aed6f2baec594cf7c962a9ec55f ; spray SHA 26143dfc627474ee3e662acc24305e61c42653dfdbaefeda861a9f5973447b4d. coupled_bootstrap_patch conserve avant/apres ; les candidats precedents restent intacts. native-walk-coupled-bootstrap-20261008-v1 prepare vingt valeurs/+0.4 EV (72a959), aucun runtime encore execute.

CheckFrozenNativeExampleMetadata20261008.py relit les 1923 pins et reprend les arguments du rustc V2 observe, avec emit=metadata, sortie privee et incremental retire. Le premier essai 4bee82 refuse avant creation de sortie sur le chemin relatif attendu trop court. La garde est corrigee vers crates/forge_engine/examples/tidewater_plant_probe.rs ; la session 16033 reste en cours, vrai controle complet de la sonde, sans Cargo, liaison ou GPU. Ne pas attribuer de PASS avant son code final et la reverification des sources.

La comparaison v6-shared-library-input-equivalence-20261008-v1.json (c8c136) confirme 670 fichiers de crates hors examples byte-equivalents V2/V6 dans les manifestes. Cela permet d'envisager la prochaine compilation de l'exemple avec les bibliotheques V2 plutot que reconstruire les crates inchangees, apres succes du build et verification des artefacts exacts ; cette equivalence source ne qualifie pas a elle seule une reutilisation de binaire.

Build 68138 encore live ; rust-lld 24248 observe le 8 octobre a 02:47:27, working set 1512026112 octets, compteurs kernel/user en progression, pas de sortie finale. Le cache F est sur WD Elements USB/exFAT ; l'observation de disque mesure 763012 octets/s en lecture et une file de 4 (fd970f). Ces observations motivent une attribution plus precise du temps d'attente, sans redemarrage sur timeout ni changement systeme/cache, et ne remplacent pas le verdict build. E est un SSD SATA Samsung 870 QVO.

### Build V2 termine ; erreurs du candidat V6 corrigees dans V7

La session Cargo 68138 termine code 0, sortie 319626, duree 104m03s. RetainComputeTimingBuild20261008.py verifie l'inventaire complet et les 1922 empreintes du snapshot V2, puis controle le SHA source/copie/source de l'executable. Recu native-compute-timing-retained-20261008-v2/retention.json : executable 134010880 octets, SHA 06a39da4a3d3a97bcb135f18a7a7f831674c2307a36759815245d6471e535670, manifeste f07032371a4cdabe0f3d0c6405942ed419c7dd1d2b96e63ad87ac52d651d858e. Ce build ne contient pas les raccords V3-V6 et son FFT example demeure hors du span ocean V2.

Le controle metadata V6 16033 avait finalement echoue code 1 : E0433 sur graphics::GraphicsState depuis la racine, E0521/E0373 sur la reference Targets capturee dans l'observer de readback spray. Les preuves d'echec sont conservees. Le correctif rend accessible au parent le module graphics et son etat/receipt, reference walk::graphics dans capture et calcule la taille du buffer d'historique depuis Output.step, deja initialise a la duree du bootstrap. Aucun reglage ou algorithme visuel n'est modifie.

FreezeCompileCorrections20261008.py derive V7 de V6 par exactement ces quatre fichiers, verifie que le canonique correspond uniquement aux substitutions bornees, puis les 1923 fichiers copies (27054b). CheckFrozenNativeExampleMetadata20261008.py accepte maintenant la revision explicite V7 ; le controle complet termine code 0 (69bdc6), 1923 pins intacts, manifeste 21f88b9847285c4af1f2bceb1d9af5b806bb69175a4b065167f7fb4185752421, log fb6da5812517f0ada4f08733a4f5d2ad1bd5a374dfe6c62e2bc406a618b42353. Il s'agit d'emit metadata contre les bibliotheques retenues, sans liaison ni GPU V7.

RunNativeWalkDefaults20261007.ps1 lance le binaire V2 retenu sur le cas frais native-walk-compute-timing-20261008-v1, vingt reglages standard et exposition +0.4 EV. Session 85669 en cours, pas de verdict runtime attribue. Un build CPU independant est observe avant lancement ; les mesures devront conserver cette limite. Aucun autre jeu Tidewater actif observe. Les snapshots precedents, reglages utilisateur et travail concurrent sont preserves.

Diff-check cible termine code 0 (04f31c) et rustfmt --check des quatre fichiers code 0 (e91997). CaptureVillageDiagnostic20261008.py prend maintenant en compte compile_corrections_patch apres acquired_environment_patch, exige l'empreinte precedente pour les fichiers deja attribues et conserve les controles complets du manifeste/binaire ; syntaxe AST PASS (2831db). Aucune capture V7 executee.

Le parcours 85669 est confirme live : processus tidewater_plant_probe 42692, adapter RX 7800 XT/Vulkan et 65383 instances/zero exclusion/zero reduction/zero correction de terrain consignes dans runtime.log. Les compteurs CPU progressent et le jeu est encore au chargement ; aucun resultat FPS ou span GPU nouveau disponible. Le processus CPU independant est cargo test du loader Studio source frame admission, target-dir F:/OpenRPG_Forge/relocated-work/Rust-target, distinct du cache de cette sonde ; il n'est pas interrompu.

### Parcours V2 termine et mesures partielles conservees

La session 85669 termine code 1 (8bb88f), candidat conserve, profil utilisateur et environnement restaures. Le parcours est fonctionnel, navigation des cascades et cadence des ombres PASS, vent/depth, nuages et rivage/spray finis ; cadence generale FAIL et global pass=false. Aucune erreur de pipeline GPU. Moyenne 45.747196 FPS, p95 24.6576 ms, 1% low 20.047332 FPS sur 163 intervalles 1920x1080. Cette observation ne prouve aucun gain causal par rapport a V1, notamment avec le build CPU independant.

AnalyzeComputeTiming20261008.py conserve compute-timing-coverage.json apres controles des SHA binaire/log/walk. required_spans_present=false : ocean et atmosphere absents des mesures retenues. Le FFT manquant etait attendu car V2 instrumentait le resident et non l'exemple ; l'atmosphere calculee au bootstrap n'est pas observee dans la fenetre live. Les absences ne valent pas cout nul. Les spans disponibles ont 120 echantillons : ombres CPU 0.197058/GPU 0.326905 ms ; nuages CPU 0.136784/GPU 0.653306 ms ; rivage/spray CPU 0.137848/GPU 0.291777 ms. Ne pas additionner les spans imbriques ni attribuer la cadence globale a l'un de ces effets. Le controle --require-spans refuse bien cette couverture ; la commande PowerShell suivante d'affichage termine code 0 sans transformer le verdict du controle.

island.wind-a.png inspecte directement : capsule de test, village cotier, ponton, mer, ecume, herbe et ombres. Ce n'est pas une comparaison Village a camera appairee ni une acceptation visuelle finale. Aucun processus tidewater_plant_probe restant observe (dc081b).

### Raccord FFT acquis et uniforms de rivage partages, candidat V8

Nouveau acquired.rs dans l'exemple FFT : paire chemin/SHA explicite FORGE_TIDEWATER_STATIC_OCEAN_PACKAGE / SHA256, mode STATIC_SOURCE_FRAME obligatoire, lecteur canonique OceanPackage avec controle chemin/taille/SHA et validation PreparedOcean::new avant installation. Les neuf shaders et chaque couple params/global des 124 etapes sont les entrees du paquet acquis ; aucun resultat de displacement ou de foam acquis n'est injecte. Le chemin historique demeure selectionne en absence de ces variables.

Input et Targets conservent la sequence sous Arc ; le FFT charge chaque params/global acquis dans la boucle, et les passes couplees rivage/spray utilisent les memes blocs a l'etape correspondante. Le compteur GPU source de spray provient du global acquis. La duree suit le raccord explicite V6, les buffers et copies conservent leurs controles. Le recu statique ajoute manifeste ocean, bootstrap et etapes encodees, ainsi que le partage des globals. Les autres uniforms du spray, budgets/cameras, continuation live et preuve complete des historiques restent a qualifier ; ce raccord statique ne pretend pas les avoir reproduits integralement.

FreezeAcquiredOceanSnapshot20261008.py preserve V7 et fige V8, quatre fichiers revus dont un nouveau, diffs et empreintes avant/apres conserves, 1924 fichiers verifies (34b404). Rustfmt cible et diff-check PASS (2899b3). Controle metadata de toute la sonde V8 code 0 (e98d5a), 1924 pins intacts, manifeste 657d3aea1538b969a00a54fa7c6d5b3e09b006215f0c997e467975b3391ba742, log 231bc48638f6e5e5bdbc083e52eaafb0ba32c77c84a3962c0aa2ceb44a898ca8. Pas encore d'executable lie ou de GPU V8.

CaptureVillageDiagnostic20261008.py ajoute acquired-environment-ocean : empreintes des deux paquets et tous leurs payloads avant/apres, attribution successive des patches et recu runtime ocean avec exactement 124 etapes. Syntaxe et refus sans recu build PASS (0d0395), aucune creation de cas/GPU pour ce controle negatif. Le prochain essai doit compiler/lier V8 attribue, conserver son binaire, puis executer ce mode frais ; aucune parite source ni qualification Studio/distribution/WASM nouvelle n'est attribuee.

### Compilation directe V8 preparee contre bibliotheques V2 equivalentes

BuildFrozenNativeExample20261008.py verifie les 1924 sources V8 et le recu du build V2 termine, puis confirme 670 fichiers de crates partages hors examples identiques entre les deux manifestes. Il reprend les arguments exacts du rustc V2 observe ; seuls out-dir et incremental vont vers native-example-linked-20261008-v8, sur E. Aucun Cargo ni ecriture dans le cache F. Les variables CARGO du manifeste V8 sont fournies au processus enfant. Le build est direct rustc, pas un PASS Cargo V8.

La session 20704 est active (e47fe1/3e8efe). Python 26700 est confirme live, ReadTransferCount progresse de 942602282 a 2153838030 octets (9e6fbb/45abb9). Avant de lancer rustc, le helper pinne les 8204 fichiers candidats presents dans les chemins de recherche des dependances et bibliotheques natives. Le repertoire deps contient 8199 rlib/rmeta/dll/lib representant 42016667402 octets (6f9b61) ; la verification de ce cache USB est donc encore en cours. Ces candidats incluent des artefacts anciens non necessairement selectionnes par le linker. Ils ne sont ni supprimes ni presentes comme tous effectivement lies. Pas encore de verdict liaison V8 ou GPU nouveau. Les autres processus detectes utilisent relocated-work/Rust-target ou le target prive Gate3, sont preserves et ne sont pas attribues a ce build.

Inspection du wrapper atmosphere (45abb9) confirme qu'il appelle bien le module moteur instrumente ; le span atmosphere absent de la fenetre live est compatible avec son calcul de bootstrap et non avec un second chemin non instrumente. Il reste necessaire de mesurer ce bootstrap si le verdict de couverture exige cette phase ; aucun controle n'est affaibli.

### Reference image appairee a l'acquisition complete et comparateur prepare

CaptureVillageDiagnostic20261008.py selectionne maintenant, pour les modes environnement acquis, capture/village.json et capture/village.png du recu de recuperation v11. Il exige les SHA b69d7129cb8ba7a4ed2a17045255838bb31c90f5a01e575a29386bd9d6f6cb73 et 762bffb4f752368109e540e323c0b6827ef212a8aa6ff2bd1f54902048180d49, conserve les pins dans execution.json et les recontrole apres le processus. Le mode historique garde sa baseline precedente. Les deux images source ne sont pas confondues, malgre leur camera identique ; les anciennes metriques restent attribuees a leur ancienne image.

Le controle 39695d confirme camera normalisee et resolution strictement identiques entre baseline historique et v11. Les animation_clocks et gpu_frame de l'acquisition retenue sont identiques au document de reference fige dans V8 ; frame_history contient 124 entrees. Il ne s'agit pas d'une mesure GPU.

CompareAcquiredVillage20261008.py exige une capture terminee avec diagnostic_checks_pass, les recus acquis environnement/ocean/vegetation/reglages PASS, les SHA effectifs des images et du sidecar, la camera et 1920x1080 appaires. Il calcule MAE/RMSE RGB, fraction des pixels avec erreur de canal >16 et luminances moyennes, tout en conservant source exposure et native EV separes. visual_parity reste false : ces metriques ne font pas une acceptation automatique et ne qualifient ni chaque uniform GPU, ni budgets/cameras spray, ni toute la map, Studio ou distribution. Syntaxe et dependances numpy/Pillow PASS (33fd1e), aucune nouvelle comparaison executee ; syntaxe du helper capture PASS (702dbc).

La session 20704 reste live en verification des bibliotheques, sans sortie finale (61048f). Python 26700 reobserve, ReadTransferCount 5643192036 octets (39695d), progression depuis 3976121281 (e9a4f4). Aucun rustc V8 ni verdict liaison attribue avant la fin de cette phase ; aucun redemarrage sur duree d'attente.

### Camera d'emission spray acquise raccordee dans V9

L'inspection montre que BreakersParams.cameraPos etait encore celle du fixture cotier pendant tout le bootstrap, alors que l'acquisition reference passe de [53.6,4.447713719743241,-77] a [42,14,-90] apres les quatre etapes de preparation. Les parametres FFT initiaux acquis et ceux du fixture spray sont byte-identiques hors temps (1f89ec), donc le controle de compatibilite des options reste applicable. L'historique v11 ne contient aucun champ budget/spray ; la parite du budget ne peut pas en etre deduite.

Premier controle de packing camera a l'offset zero echoue pour les 124 frames (4c5f26). Le diagnostic 1ed880 lit les shaders : Frame commence par huit mat4, cameraPos est a 512, alors que BreakersParams commence par cameraPos. La copie temporaire incorrecte est corrigee avant tout gel/build/GPU. Le controle de77c3 confirme que chaque global[512..524] correspond exactement aux trois f32 de la camera effectivement acquise, pour les 124 etapes.

Le correctif spray copie ces douze octets dans BreakersParams uniquement pour les etapes acquises, conserve les autres parametres et le budget natif, et reserve LiveCamera.apply aux seules transactions live. Les globals et params ocean restent ceux du raccord V8. Rustfmt cible/diff-check PASS (3b2b9b).

FreezeSprayCameraSnapshot20261008.py derive V9 de V8 par deux substitutions bornees dans un seul fichier et verifie l'absence de derive concurrente, 1924 fichiers copies (27ff78), spray SHA 6211ea71f95ecd5ed8ff7712d3d33d4b0e5b85bf02d593e580e5216ce8167d73. Le controle metadata complet V9 termine code 0 (329c1c), sources intacts, manifeste 6c7716e38c286c05e842031a5ab476e7ec9b7de10d381cd4dc8deabeb75423f4, log cb7c5b95ece73b2cb5e21cd609f13386647cdb1de3ee08666f92f79b4814c1e5. Aucun executable lie ou GPU V9 ; les differences du budget, autres uniforms et images restent a verifier.

Le build 20704 garde V8 intact et reste live en verification d'artefacts. Python 26700 progresse jusqu'a 12263105081 octets lus (9bb9ba), depuis 10969793406 (e4cf39). Aucun lancement Cargo concurrent dans son cache, aucun redemarrage ou verdict de liaison nouveau.

### Horloge du budget d'emission : reset source observe et rejet Rust reproduit

Inspection du Breakers._budget original dans le clone fige et de spray_budget.rs (c30ca7) : la reference associe GPU.frame au temps global G.time.value, puis exploite les heads asynchrones completes, y compris repetition de latest. La sonde associe encore son head a progress.1, temps continu FFT. Le controleur Rust refuse les timestamps decroissants ; aucune politique n'est affaiblie sur simple supposition.

AuditAcquiredBudgetClock20261008.py relit le metadata v11 et les 124 globals du paquet ocean, verifie leurs SHA et le temps f32 packe a l'offset 580 contre le global_time_s acquis. Recu acquired-budget-clock-audit-20261008-v1.json (f24e0a), SHA 6fb0da868205dcc892856dd8c05bb5f956dba4ed4707fd9f9cc5ce07089902f9 : reset a l'etape 5, de 0.06666666666666667 a 0.016666666666666666 s ; delta final entre accumulation ocean native et global source 0.06666666666666643 s. clock_matches_source=false et aucune trace de heads/budgets source inventee.

ProbeBudgetClockRewind20261008.py compile directement le module Rust inchange du snapshot V9, SHA d3e811f88240a54dd7865f9723f93239c3952e9db3a622b0f763203bed397795, et lui fournit les timestamps acquis avec des heads synthetiques nuls. Le probe termine code 0 parce qu'il reproduit exactement le rejet attendu a l'etape 5 sans mutation de la valeur du controleur (8123b5). Ce n'est ni une parite avec les heads GPU source, ni un PASS du budget dans la scene. Recu budget-clock-rewind-probe-20261008-v1/result.json, aucun Cargo/GPU/cache partage modifie.

Le raccord global du budget ne peut donc pas se limiter a remplacer progress.1 par un temps global plus bas. Il faut expliciter l'ordre des echantillons et le reset de l'horloge de l'acquisition, puis qualifier les heads/latest et la latence reelle contre la reference. Cette correction reste ouverte ; aucun faux historique monotone ou suppression du reset source n'est applique. Le build 20704 reste actif et ses entrees V8 sont conservees.

### Budget a ordre GPU explicite et timestamps globaux acquis, candidat V10

spray_budget.rs ajoute SequencedHeadSample et after_sequenced_emission : les sequences GPU anciennes sont refusees, une repetition de la meme sequence exige les memes timestamp/head, mais une nouvelle soumission peut suivre un reset du temps de simulation. Les modes temporel et sequence ne peuvent pas etre melanges dans un meme controleur. Le chemin after_emission historique conserve ses rejets ; la formule de budget et la gestion d'historique sont partagees sans effacer l'historique lors du reset.

CheckSequencedBudget20261008.py conserve une copie privee du vrai module, compile ses tests sans Cargo et execute dix tests (9ff52b), tous PASS : six regressions existantes et quatre controles de reset/repetition/rejets/melange. Source SHA 67c91d0d22007d7581882a72d27853c5047ab6e538cc888321ad23de28923f7f, sources inchanges. Ce PASS CPU ne prouve pas l'equivalence d'une trace de heads source ou l'execution du renderer.

Le loader ocean acquis exige maintenant un metadata reference et son SHA explicites, borne a 2 MiB, avec le meme commit et le nombre d'etapes du paquet. Chaque temps global f64 after/global_time_s doit etre fini, non negatif et se repacker exactement dans les octets global[580..584]. Les retours head acquis utilisent ce timestamp complet et leur sequence GPU ; les autres chemins gardent leur horloge historique. Les lignes de diagnostic distinguent temps global utilise pour le budget et temps ocean. Le helper de capture fournit ce metadata deja pinne et reverifie avant/apres. Syntaxe helper, fmt-check et diff-check cibles PASS (915c67/d8f1f1).

FreezeBudgetClockSnapshot20261008.py adapte explicitement le freezer precedent (recu budget-clock-freezer-adaptation-20261008-v1.json) : V9 vers V10, trois fichiers et nouveau champ acquired_budget_clock_patch. Gel 261db3 : 1924 fichiers verifies, controleur SHA 67c91d0d22007d7581882a72d27853c5047ab6e538cc888321ad23de28923f7f, loader baffb1ce5c2f503a9958bc0d382b9eb2809ffb3aeed22cc21538443c786ba656, spray 7c78322285d5878891846901147d5bbda08f64f6d4ca0b0d5451298df8251919. V8/V9 restent intacts.

V10 modifie une source partagee du moteur : les anciennes bibliotheques V2 ne suffisent plus pour son nouveau type/API. Le controle complet de la sonde V10 et la liaison doivent utiliser une bibliotheque moteur reconstruite et attribuee a ce candidat ; ne pas appeler le check metadata precedent contre l'ancien SDK pour en tirer un verdict V10. Aucun jeu lie/GPU V10, aucune parite de budget ou image nouvelle acquise. Le build 20704 reste V8 et continue sa verification ; Python 26700 avait atteint 30068184171 octets lus (eeab7e), sans redemarrage ni mutation de ses inputs.

### Comparaison du calcul original JS : divergence V10 corrigee dans V11

CompareSequencedBudgetSource20261008.py extrait sans modification la methode _budget du Breakers.js du clone fige (SHA source 0f0119cd0f24a87b46d296f6847aec8145aa5048334b1f50869c08be13ee1d3b, methode 362c8bad88050845b069765c62bcfde945b2d09e995f282b54ac3f56a1a17570). Node execute ce vrai calcul avec Readback simule ; Rust execute le module fige. Les timestamps du cas 124 sont acquis, mais les heads et disponibilites de callbacks sont synthetiques. Aucune trace de heads source/GPU n'est substituee.

Premier rejeu V10 termine code 1 (6a3005), conserve sequenced-budget-source-comparison-20261008-v10/result.json : zero ecart >1e-14 sur les 124 timestamps, mais deux ecarts dans le cas nouvelle soumission/meme timestamp. JS deduplique l'historique par timestamp et calcule le taux depuis la tete de sa derniere entree retenue ; Rust utilisait le nouveau sample.head meme sans inserer d'entree. Cette divergence n'etait pas couverte par les dix tests precedents.

Le correctif utilise la derniere entree de l'historique pour temps/head dans le calcul du taux et ajoute newer_submission_at_same_time_keeps_source_history_head. Le mode temporel historique conserve son comportement (il refuse deja une repetition de timestamp avec head different). Une allowance Clippy ciblee documente le helper de test optionnel. CheckSequencedBudget20261008.py v2 : onze tests PASS, sources intacts (ab6ab9), source SHA 92c9a09e4c6c922230e01da4810501fef5dc33ee4344ab1ed62ba7c38772f5ca.

FreezeBudgetTimestampHistorySnapshot20261008.py fige V11 de V10, un seul fichier, 1924 pins controles (fd3453). La comparaison au vrai calcul JS V11 termine code 0 (6075cb) : 124 + 4 valeurs, aucun ecart >1e-14 ; controller_comparison_pass=true pour ces donnees synthetiques uniquement. Les echecs V10 restent conserves. Diff-check cible PASS, aucune comparaison visuelle ou trace GPU reelle acquise et aucun executable lie V11.

Le build V8 20704 est encore actif : Python 26700 progresse a 41859618258 octets lus (3d484d), depuis 37290558379 (e3def2). Pas encore de rustc V8 observe ni de verdict liaison. Les snapshots du build et les autres travaux restent intacts.

La phase de pins termine ensuite : sortie 21b855 indique le lancement du compile/link prive V8. CIM 1ed3b6 confirme rustc 13908, enfant de Python 26700, creation 03:55:02 locale. inputs.json est conserve (1785360 octets) avec les arguments, 8204 artefacts verifies et l'equivalence des 670 sources partagees. build.log existe ; aucun code de fin du compilateur ni executable qualifie encore attribue. Le prochain suivi doit reprendre la session 20704, sans relancer le job.

### Controle complet V11 contre metadata moteur reconstruite lance

CheckRebuiltEngineExample20261008.py verifie les 1924 sources V11, confirme que la seule source partagee differente de V8 est spray_budget.rs, et lit le fingerprint du forge_engine V2 retenu. Les features default/native/physics-rapier et ses treize dependances sont verifies contre les externs/pins deja retenus. Le helper adapte les arguments du rustc d'exemple en commande library metadata : lib.rs, crate-type lib, metadata privee, les seules dependances declarees du moteur, sans incremental ni sortie partagee. La commande d'exemple suivante remplace seulement l'extern forge_engine par cette nouvelle rmeta, et reste emit metadata dans son propre dossier.

Session 88768 lancee (7a8724), puis verification d'entrees terminee et checking full V11 engine annonce (e1d5ca). inputs.json est conserve dans rebuilt-engine-example-metadata-20261008-v11 avec les deux commandes exactes, source/fingerprint/pins SDK. Ce controle est adapte direct rustc, pas Cargo ni liaison/GPU. Aucun PASS avant les codes de fin et reverifications des sources/SDK.

CIM de34e7 confirme rustc 26012 enfant de Python 34740 pour V11 (801087488 octets resident, compteurs CPU actifs), et rustc V8 13908 enfant de Python 26700 (user CPU 1746406250 ticks, contre 70468750 dans 68f641). V8 conserve son seul warning known dead_code dans isolated_lod::run ; aucun code terminal de session 20704. Les deux operations ecrivent dans des sorties E privees et lisent les bibliotheques F sans les modifier. Les autres travaux et snapshots restent preserves ; aucun nouveau verdict visuel, performance, Studio, distribution ou WASM attribue.

### Controle complet V11 termine et construction liee lancee

La session metadata 88768 termine code 0 (35d93b). Le recu rebuilt-engine-example-metadata-20261008-v11/result.json confirme engine_exit_code=0, example_exit_code=0, 1924 fichiers sources verifies, compiled_inputs_unchanged=true et sdk_unchanged=true. SHA moteur rmeta 1e9cbbe7fc040b715287792c0dbffdacd2f6112ad36afa972a1fca6b1ceb590e. Le seul warning connu est isolated_lod::run unused. Ce verdict couvre le controle direct rustc du moteur et de la sonde complete, pas leur liaison ni leur execution GPU.

PrepareLinkedV11Build20261008.py derive de facon explicite BuildRebuiltEngineExample20261008.py du helper metadata reussi ; recu v11-link-build-adaptation-20261008-v1.json (bcb9db). Le moteur passe a emit link et sa rlib privee ; l'exemple passe a dep-info,link et utilise cette nouvelle rlib. Toutes les bibliotheques candidates retenues du build V8 sont reverifiees avant et apres, ainsi que les sources V11. Aucun Cargo/cache partage n'est modifie. La conservation du binaire n'est ecrite qu'apres deux codes de fin zero, sources/SDK intacts et presence de l'executable ; gpu_executed reste false.

Session build V11 81346 lancee (5cc396), processus Python 192 confirme live et lisant les entrees (bda9ef, ReadTransferCount 242615168). Sortie privee attendue rebuilt-engine-example-linked-20261008-v11. Pas encore de verdict build, de binaire retenu V11 ni de capture GPU. Le build precedent V8 20704 reste live (5725eb), linker 45176 reobserve avec compteurs CPU en progression ; ses entrees sont preservees. Reprendre ces sessions, ne pas les relancer sur simple attente.

### Observateur passif des budgets source prepare et liaison V8 produite

PrepareBudgetObservationOverlay20261008.py prepare un overlay prive sans modifier reference.html canonique. Premiere version syntaxe PASS f33cae, puis V2 syntaxe PASS b06fee : SHA source ea4f4a2183566d64252aa8c7ca6ce07868f9cd760a5b5b90d92ace959143c388, overlay V2 1420a3347765f86b9eba48964b6c33c89c9fb4c8c270b7b1c957f919e3a91ec6. Le wrapper observe la vraie methode Breakers._budget, conserve son appel et restaure la methode dans finally. Aucun nouveau Readback, attente GPU ou changement d'historique ajoute. Il releve latest/frame/head/time associe, budget avant/apres, demande effectivement inscrite dans _simT et bornes d'historique. L'upload Breakers est capture avant l'ajustement CPU du budget, apres dispatch de l'emetteur ; les mirrors de fin de frame sont conserves separement pour ne pas confondre emission et rendu. Spray.simParams est le vrai bloc, pas le simple objet params.

validate-source-budget-observation-20261008.mjs prepare les controles de sequence, heads u32 et timestamps reels, bornes d'historique, bloc emission 32 octets avec budget f32 avant ajustement et SprayParams 240 octets. Node syntaxe PASS (16c4bf) uniquement : aucune trace GPU acquise ni qualification du validateur sur de vraies donnees. La future acquisition doit conserver sa propre image/paquets et ne pas reutiliser le SHA image de l'acquisition precedente ; l'observation peut influencer le calendrier asynchrone.

La liaison V8 emet son executable dans build.log (956e2d), taille 134265856 octets, horodatage 04:19:44 locale. Rustc et linker precedents absents, mais Python 26700 encore live et en reverification (ReadTransferCount 49605681862) ; session 20704 non terminale (316fe9), aucun result.json final attribue et aucune execution GPU lancee. V11 session 81346 reste live, Python 192 a lu 6954529536 octets (16c4bf) dans la phase de pins ; aucun redemarrage.

### Acquisition source des budgets reelle lancee dans une copie archivee

PrepareBudgetAcquisition20261008.py adapte le runner AcquireReferenceClocksV12 et le driver canonical dans budget-acquisition-tools-20261008-v1, originals preserves (a09e79). Il ajoute le validateur des observations avant toute sauvegarde de capture, utilise l'overlay passif V2, fournit aussi reference-ocean-output.js et demande all-effects. Python et deux scripts Node syntaxe PASS. SHA runner ab43127129a5c934d5ff01a0d66763962811b98a2f48de52b478201a3e95ae60 ; driver 4491ee3c61dff7af826a37c075a7b72ce2e88b206a047dfcd0fff8196d7775e7. Ceci reste une preparation, aucun PASS GPU deduit de la syntaxe.

Avant lancement, CIM releve 26138 MiB physiques disponibles, CommitLimit 72119635968 et CommittedBytes 56234065920 octets (a09e79). Session 19061 lancee (c7b011), Python 42400 confirme live, Vite 41332 et driver Node 42504 actifs (e6bc3e). Source exportee par git archive du commit 1438b1ab..., source.zip 51212611 octets ; ports locaux dynamiques 55826/55827, profil navigateur prive et processus appartenant a ce runner. served-entry.html est le HTML transforme par Vite avec module html-proxy, pas une capture tronquee de l'overlay (9bb0e3). Le log confirme le demarrage et un warning source ShoreWaves de fallback ; capture.stderr encore vide. Aucun verdict terminal, aucune trace validee ou nouvelle image attribuee.

Sessions natives 20704 et 81346 restent live (4530e5/057c4a). Python V8 26700 lit maintenant 52095698030 octets dans la reverification des SDK, V11 Python 192 9841876306 octets dans ses pins (e6bc3e). La presence du binaire V8 ne remplace pas le verdict final ; aucun redemarrage, aucune qualification visuelle/native/Studio/distribution/WASM nouvelle.

### Vrais retours GPU source acquis et calcul Rust compare ; image rejetee

Acquisition V1 session 19061 termine code 1 (822752) : CDP timeout Page.captureScreenshot apres 120 s, source prete confirme par lecture de son onglet isole (b7d1d3). Source archivee/driver/validateur intacts ; aucun fichier metadata/image sauvegarde avant l'erreur. Cet echec est conserve, pas traite comme une attente encore active.

PrepareBudgetAcquisition20261008.py V2 enregistre le metadata valide avant screenshot et essaie fromSurface=false (2e0d04). Nouveau runner SHA c510edba8eeae82bfa51c52493777e324f3dc82b21390107c1f2157acf023a96, driver 741aeb470c2db7384e7f90cf1781697ff4d7e0aed2f55ef6496237e50d77f51e. Session 49604 lancee (25e25f), Python 40796 confirme live (f50337). village.json 761293 octets et village.png 72735 octets sauvegardes. Image effectivement ouverte : fond pastel vide, aucune scene Tidewater visible. image-review.json accepted=false ; dimensions/signature PNG ne prouvent pas un rendu valide. La suite all-effects reste en cours, aucun verdict terminal d'acquisition attribue.

CompareAcquiredBudgetTrace20261008.py utilise exclusivement les observations de cette nouvelle acquisition, source commit fige et inventaire source verifies, module Rust V11 SHA 92c9a09e4c6c922230e01da4810501fef5dc33ee4344ab1ed62ba7c38772f5ca. Il conserve la copie du module, trace binaire avec absence/repetition/latest reels, compile un probe CPU et compare budget avant/apres pour les 124 frames, capacite 32768. Resultat 380a5b code 0 : controller_comparison_pass=true, inputs_unchanged=true ; SHA metadata 4fe7193d92bde5521b4c7792b833ecd8996991a335a0393c07ce68202b1fdf57. Aucun ecart superieur a 1e-14. Contrairement au test precedent, les heads et disponibilites viennent bien de readbacks source GPU reels ; le rejeu est CPU, pas GPU natif. acquisition_result=null au moment du probe, car acquisition encore active. Aucun verdict de heads natifs/latence native/rendu/live continuation ou parite visuelle deduit.

### Capture directe de la derniere passe source preparee et lancee

L'acquisition V2 session 49604 termine code 1 (dc44ce), Runtime.evaluate timeout pendant la suite all-effects ; elle conserve bien le metadata reel et l'image rejetee. Source/driver/validateur intacts selon son result.json. Le rejeu CPU deja acquis reste attribue a son metadata reel, pas a un PASS complet d'acquisition.

Lecture de PostFX.js source confirme outputTexture comme chemin prevu pour les essais headless, _finalPass utilisant outputFormat et meme shader de grade/tonemap. GPU canvas a RENDER_ATTACHMENT|COPY_DST, sans COPY_SRC ; une lecture arbitraire du canvas actuel n'est donc pas retenue. PrepareDirectFinalImageOverlay20261008.py utilise un Texture de meme format et 1920x1080 comme sortie uniquement pour la derniere frame source 124, sans nouvel App.frame. La lecture readTexture apres bootstrap exige validation GPU et horloges frame/global/ocean identiques avant/apres, puis convertit BGRA en RGBA pour une image PNG sans affichage. Syntaxe PASS 3c0d27, overlay SHA 370ca3bc16730f157ff1b52242c630879e45f10e39c64710558b89ac905e9913. Ce changement de cible finale est explicite ; ce n'est pas une capture du compositeur.

PrepareBudgetAcquisition20261008.py V3 syntaxe PASS (577516), runner SHA e12eacd72af58ab5fcde0915665f52a6bf33fc1f8832c7016754a19e860256c4 ; driver 022112d1def6cedc9ee4c70d56161545aaced22f15a6ea32253c36b6f5a8b9fb. Session 79581 lancee (5e1011), Python 2668 confirme live (7b6604), metadata 761293 octets sauvegarde avant lecture finale ; aucune image ou qualification nouvelle encore attribuee. Ressources relevees 28478 MiB disponibles, commit 53702164480/72119635968 (21045a). RetainLinkedExampleBuild20261008.py prepare pour V8 mais non execute : exige resultat terminal, sources/SDK verifies et copie/hash avant/apres du binaire ; pas de conservation avant fin des controles.

Audit supplementaire de la vraie trace V2 (2d3cd8) : 122 observations completees sur 124, zero repetition latest dans ce run, 123 valeurs de head distinctes en comptant null initial, premiers heads 84/124/174, dernier head 14499, budget de 0.3 a 1. La concordance CPU n'est donc pas un simple historique de heads nuls. Les repetitions restent couvertes par les tests synthetiques precedents, pas observees dans cette acquisition. Session 79581 toujours live (52a4cc) ; aucun redemarrage de build.

### Diagnostic du timeout final : GPU et PNG terminent, transport await reste ouvert

Acquisition V3 session 79581 termine code 1 (76a468), Runtime.evaluate timeout pendant lecture finale ; metadata conserve, pas d'image. V4 ajoute des marqueurs passifs avant/apres readTexture, popErrorScope, image 2D et PNG, et une observation device.lost. Preparation syntaxe PASS 996ec9/98f2c1 ; overlay 30038c5865e3ed001076bbb5694e7e36bf0ac1493ae9a1dd3029b54ee77b312d. Session 65042 lancee (beea2d).

Le stdout source V4 (dcbe05) confirme toutes les etapes jusqu'a complete : readTexture, validation GPU et encodage PNG reviennent effectivement. Pourtant le driver n'a pas encore recu le resultat Runtime.evaluate ni ecrit final-image-readback.json ou village.png. Son onglet repond toujours a une lecture simple et reste ready_reference_only (bd2c8f). Le diagnostic de processus Chrome (609a35) ne trouve que le profil V4 parmi les essais budget, aucun navigateur orphelin V1/V2/V3 a arreter. V4 finit ensuite code 1 (a021a9), timeout Runtime.evaluate conserve, source/driver/validateur intacts.

PrepareChunkedFinalAcquisition20261008.py prepare V5 en gardant exactement l'overlay source et les shaders V4. Le driver demarre la lecture async sans attendre son gros resultat par CDP, observe un etat court, puis transporte le PNG base64 par tranches de 65536 caracteres, longueur bornee et chaque tranche verifiee. Syntaxe PASS (5dfc09), driver SHA 365c026e0e996bff63d9a7e01aba41c376747e6958ccec938127be91a01e7507, runner e8baf64696a5100982d3c15fadbb7f92958de21146d009bc5de9a69f579a5451. Aucun verdict image deduit de cette preparation ni de complete source. Nouvel essai V5 lance apres la fin terminale V4, pas sur timeout d'observation d'un job encore actif.

Les builds natifs restent actifs et leurs lectures progressent (bd2c8f) : Python 26700 67650867326 octets dans la reverification V8, Python 192 25397045602 octets pour les pins V11. Aucun resultat terminal de ces builds, aucun lancement natif ou qualification nouvelle attribue.

### Image finale source recuperee et vue ; export nuages encore bloque par transport

V5 produit effectivement final-image-readback.json et village.png de 5032757 octets (52a713). Image ouverte et inspectee : village complet dans cette vue, maisons et etal, palmiers/herbe, montagne/foret et ombres visibles ; aucune image vide. Le recu (020c88) confirme 1920x1080, bgra8unorm, GPU frame 124, global 1.9999999999999978 et ocean 2.066666666666664 avant/apres exactement identiques, gpu_validation_error=null. Ceci couvre la derniere passe source via outputTexture, pas le compositeur ni une comparaison avec le moteur natif.

CompareAcquiredBudgetTrace20261008.py V2 termine code 0 sur la trace appairee V5 (3d993e), 124 frames, sources intacts et aucun ecart >1e-14. Metadata SHA 4fe7193d92bde5521b4c7792b833ecd8996991a335a0393c07ce68202b1fdf57, identique au run V2 ; les commentaires instrumentaux ne changent pas les observations conservees. Le probe garde acquisition_result exit1 : la suite complete ne passe pas. V5 session 42917 termine code 1 (0548a2), Runtime.evaluate timeout apres environment exporte (38 fichiers, 392957) et avant export de nuages. Les fichiers environnement restent un export partiel attribue, pas une qualification full acquisition.

PrepareChunkedEffectsAcquisition20261008.py applique aux trois exports GPU le meme principe de demarrage async/etat court/JSON par tranches de 65536 caracteres ; borne 128 MiB, chaque longueur de tranche verifiee et tous les validateurs source originaux conserves. Overlay, rendu et historiques ne changent pas. Syntaxe PASS d4157c, driver SHA e1b36f0a22eaed9d9e2d930e6fb50629c525f3c03d297b256f3c5d54f587108f, runner 52facebf17174750ab44058d663ff299f09277e43d9d52aeaf0b0ef733320265. Nouvel essai V6 lance apres fin terminale V5. Aucun nouveau verdict complet source/GPU natif/visuel/Studio/distribution/WASM attribue.

CIM 392957 confirme les builds toujours live avec progression : Python 26700 70914110266 octets lus en reverification V8, Python 192 28660288542 octets en pins V11. Aucun redemarrage ni lancement natif avant les recus terminaux.

### Acquisition complete V6 PASS et correspondance avec entrees Rust verifiee

Session source 24610 termine code 0 (96dc45), failure null, source archivee/capture script/validateur inchanges. Image finale, environnement, cloud-gpu et ocean-gpu conserves ; acquisition.json blocking_errors vide et adapter AMD RDNA3. Ceci qualifie cette acquisition diagnostique complete de reference, pas le rendu natif ou une gate.

AuditCompleteBudgetReference20261008.py controle le resultat terminal, les 428 fichiers source exportes/overlay, les SHA de 256 payloads d'effets, leurs commit/reference_sha et validations GPU/horloges. Premier passage 2fbe3f echoue sur nom ocean-step001-global.bin inexistant ; inspection du manifeste (ab6db2) corrige en ocean-step001-frame.bin, sans toucher aux paquets. Audit termine code 0 (7884ed) : 496 comparaisons de blocs (124 globals/atmosphere environment et 124 globals/params ocean), toutes byte-identiques aux paquets charges par Rust. Recu complete-budget-reference-audit-20261008-v1.json SHA 1463ced571c6c741c03715df5b2f45e538bc27960e0486a3d94e299b596c649c.

Image V6 SHA cb1111221fdf6919ebd28f94b1e4e811975acdea667d01a0e4fe48b6d8f23a66, metadata SHA 4fe7193d92bde5521b4c7792b833ecd8996991a335a0393c07ce68202b1fdf57. Nouvelle image comparee pixel RGB a l'ancienne reference recuperee 762bff... : MAE 3.2150205761316875e-7, ecart maximal 1/255. Pixels pas tous strictement identiques ; nouvelle image distincte conservee sous son SHA, pas remplacee par l'ancienne. L'image precedente V5 n'est pas byte-identique non plus, aucun SHA re-utilise abusivement. Derniere passe bgra8unorm 1920x1080, frame124/global1.9999999999999978/ocean2.066666666666664 identiques avant/apres ; validation GPU nulle.

Rejeu CompareAcquiredBudgetTrace20261008.py V3 sur l'acquisition complete termine code 0 (7884ed), controller_comparison_pass=true et sources intacts, 124 frames avec vrais callbacks GPU ; acquisition_result maintenant exit0. Le controleur CPU Rust n'est pas une preuve de heads/callbacks ou images du GPU natif.

PrepareCompleteReferenceNativeCapture20261008.py cree deux helpers distincts en preservant les anciens : CaptureCompleteReferenceVillage20261008.py et CompareCompleteReferenceVillage20261008.py. Ils pinnenent explicitement les nouveaux SHA image/metadata, le recu d'audit et le resultat d'acquisition, tout en gardant les exigences de build retenu, reglages reellement lus, bootstrap acquis et camera. Syntaxe PASS (7c8e0f), aucun lancement natif effectue. Python 26700 et 192 restent live, respectivement 73420719139 et 31166897415 octets lus ; reprises des sessions 20704 et 81346 requises, pas de redemarrage.

### Ordre des retours GPU source compare au bootstrap natif : exception initiale observee

Lecture du chemin V11 (b8da60) confirme budget mis a jour apres emission avec latest, assertion de latest==step-1 sauf premiere frame, read_budget de chaque etape terminee et permission suivante uniquement apres son retour. Ceci prouve une politique native controlee d'une etape de latence, pas l'observation de sa valeur GPU dans la scene actuelle.

AuditControlledFeedbackLatency20261008.py reutilise le probe CPU V11 deja attribue et les heads reels source disponibles pour isoler cette politique. V1 (d20636) s'arrete des la premiere tete source manquante et ne conserve que deux decisions ; ce perimetre est trop court pour caracteriser toute la sequence. V2 (0231a1) conserve les 124 decisions mais arrete toujours le calcul arithmetique au premier head indisponible. Resultat : un seul ordre different, frame2 ; source pas de latest, politique native exige head de frame1. Les frames suivantes source utilisent effectivement le precedent ; aucune inference de latence de deux frames sur toute la sequence n'est retenue. Le head source frame1 n'est jamais observe dans latest et n'est pas invente. all_frames_replayable=false, arithmetic_frames_comparable=1, aucun ecart arithmetique ou visuel natif attribue par cette sonde.

Cette exception initiale peut changer la premiere entree de l'historique du controleur natif, meme si son budget reste 0.3 a cet instant. Le prochain raccord acquis doit pouvoir observer l'absence source a la frame2 tout en conservant les vrais heads natifs et les controles de readback ; pas de budget ou head source injecte a la place d'un calcul GPU natif. Le chemin live historique reste distinct du rejeu diagnostique acquis.

Les builds restent live avec progression (0231a1) : Python V8 26700 79246446410 octets lus, Python V11 192 36992624686 octets. Aucun binaire retenu/runtime nouveau ou verdict de parite attribue avant les recus terminaux. Pas de redemarrage des sessions 20704/81346.

### Ordre acquis raccorde aux heads natifs, candidat V12 fige

Les deux sources canoniques avant edition sont reverifiees (fcdc8b), sans derive depuis V11. Le loader acquired ajoute feedback_frame avec deux niveaux Option : metadata ancien sans observations, observation explicite sans latest, ou sequence source precedente. feedback_order valide l'uniformite des observations, la transaction/frame GPU, le clock f64 exact de la soumission, les heads u32 et les repetitions/ordres ; une tete source est seulement validee pour la coherence du metadata, jamais copiee dans le budget natif. Le budget natif conserve ses propres heads completes dans BTreeMap et selectionne la sequence acquise quand elle est observee. Une observation None a la frame2 ignore le head natif1 pour ce calcul, sans supprimer le readback natif ni affaiblir sa barriere. Anciennes acquisitions gardent le chemin historique ; live au-dela du bootstrap conserve latest.

Le recu spray distingue source_feedback_observed/source_feedback_frame, native_latest_frame et feedback_frame consomme. source_schedule_loaded reste false (aucun budget source injecte), source_feedback_order_replayed explicite le seul ordre observe rejoue. Les assertions de previous native GPU completion restent en place. Fmt cible/diff-check PASS 0aff3e/6c1363/4db7ae ; modules concurrents preserves.

FreezeAcquiredFeedbackSnapshot20261008.py fige V12 de V11 par seulement deux fichiers (f424f3), 1924 sources verifies, core partage inchange. Loader SHA 30c3052a8fa4e24ff6c5ba4167e7754c94f68aeb91bf13c81b6cee7ac6298ff5, spray SHA 2b8992332599918376ffb73116cc8fcfbbd38088eba04ea48649ae87fae464cf. V8/V11 et leurs builds restent intacts.

CheckAcquiredCallbackParser20261008.py extrait sans modifier la vraie fonction figee (SHA 7a5cc7778a0edb5f01e653da5ad3f48d7b1eeb6d39a625451e9a77da81d7488b). Dix controles CPU PASS 6f875b : 124 frames acquises avec deux premiers latest absents, anciennes observations absentes, mixture refusee, futur/clock/head u32/transaction invalides refuses, repetition coherente acceptee et incoherente refusee, absence apres latest refusee. Source inchangee, aucune preuve GPU tiree de ces controles.

CheckAcquiredFeedbackExampleMetadata20261008.py lance la session 89709 (c8a16e/25b101) contre la rmeta moteur V11 deja verifiee ; toutes sources partagees V11/V12 identiques et externs pinnes. Compile.log emet le rmeta complet V12 avec seul warning connu (4db7ae), mais helper encore en reverification au moment de cette observation : attendre resultat terminal avant PASS.

### Build V8 termine, binaire conserve et premiere capture acquise native lancee

Session 20704 termine code 0 (75feb0), 1924 sources et tous SDK intacts, log SHA ae150d4a07a13c97f2c04965089af85c2ac25c3a20980845125fbbab5635dbc0 ; direct rustc link, pas Cargo/GPU. RetainLinkedExampleBuild20261008.py termine code 0 (1bfe0d), copie de 134265856 octets et SHA avant/cop ie/apres identique 37fe24d401e381e1ea429d3bc3686944304ce991a46b27be946957fae23e6457. Recu native-example-linked-retained-20261008-v8/retention.json ; source manifest 657d3aea1538b969a00a54fa7c6d5b3e09b006215f0c997e467975b3391ba742. Ce V8 precede les corrections camera/budget de V9-V12.

Ressources avant lancement (1bfe0d) : 22066 MiB disponibles, commit 60964315136/72119635968. CaptureCompleteReferenceVillage20261008.py lancee en session 57578 (768c19), profil utilisateur isole, reference complete V6 pinnee, defaults maintenus. Fixture Village prepare et processus natif16824 annonce (0b6647), puis confirme live resident601133056 octets (4db7ae). Case native-complete-reference-village-20261008-v8-v1. Pas encore de capture terminee/image/diagnostic PASS ou parite visuelle attribuee. Build moteur V11 session 81346 continue, rustc45768 enfant de Python192 confirme actif (4db7ae), aucune liaison V11 terminee annoncee.

Le controle metadata V12 session 89709 termine ensuite code 0 (d261e1) : 1924 fichiers sources verifies, compiled_inputs_unchanged=true, sdk_unchanged=true, log SHA 0992cc856418fee3598e31b1ec5c170fce9a243183d66cb40df839187b028b65. PASS du controle complet direct rustc, toujours aucun binaire lie ou GPU V12. Capture V8 session 57578 encore live (11e3a1).

### Capture acquise V8 terminee et essai TAA appaire : amelioration mesuree, parite encore ouverte

La capture native-complete-reference-village-20261008-v8-v1 termine code 0 (e01f01), diagnostic_checks_pass=true, camera et 20 reglages lus verifies, 124 etapes environnement/ocean completes et pose acquise verifiee. Binaire et entrees inchanges. Image c3b029cd7fd4ed2307a8ce808fce84f2d542206880ef6f966f0daa0b13787714 inspectee ; village, vegetation et ombres visibles. Comparaison controlee 360a55 : RGB MAE 9.09311824845679, RMSE 12.413480772403496, proportion pixels avec erreur >16 sur un canal 0.21165702160493827 ; visual_parity=false. Warning de readback apres fermeture conserve dans runtime.log, pas attribue a une erreur de validation de pipeline.

L'essai isole native-complete-reference-village-20261008-v8-taa-v1 ne change que temporal_aa=1 et fxaa=0 ; profil utilisateur preserve. Session 7374 termine code 0 (846353), image 1a251d363d9e344b8b00d334e594fa87b747ad0bf8fc6371d9dd9579e46d5e12, binaire et pins intacts. Comparateur 97f6b8 termine code 0 avec camera/reference/acquisitions/reglages verifies : RGB MAE 7.695343364197531, RMSE 10.568842621085803, proportion pixels >16 0.14380449459876543. Baisse de MAE d'environ 15.4% sur cette paire statique ; aucune qualification temporelle/live/performance/toute carte deduite. Image ouverte et inspectee. Luminance moyenne native 111.39912467554014 contre reference 109.82583881134258 ; controls d'exposition distincts conserves. visual_parity=false, pas de modification des defaults produit sur la seule base de cette mesure.

### Verification V12 contre vraie bibliotheque moteur et decouverte des dependances

Build V11 session 81346 reste actif : le marqueur conditionnel 94cbe2 etablit la sortie zero du build moteur puis le lancement de la liaison exemple ; PID192/PID45612 verifies live (29d3a2). Ne pas assimiler cette etape a un recu terminal complet ni redemarrer le job.

TrackV12BinaryDependencies20261008.py construit uniquement les metadonnees et dep-info du snapshot V12 contre la vraie rlib V11 deja produite. Les 1924 sources sont verifiees avant/apres et les sources partagees correspondent a V11 ; externs verifies contre les pins SDK existants, bibliotheque moteur hashee avant/apres. RUSTC_BOOTSTRAP=1 est limite a l'environnement enfant diagnostique pour -Z binary-dep-depinfo=yes ; aucun flag produit modifie. Resultat ba7dff code 0, sources_unchanged=true, explicit_dependencies_unchanged=true, log 2e15760dbc54b5e6c70e4727e61cca0b93aa3ce176c7e59e8f2f5b5bed56863c. Liste dep-info emise et inspectee (da759f), contenant aussi des rmeta transitives et DLL proc-macro. Cette liste doit encore etre analysee et sa fermeture de liaison prouvee avant de remplacer les longues reverifications du SDK complet. closure_qualified=false, linked_game=false, gpu_executed=false. Aucune qualification V12 GPU, Studio, distribution, WASM ou gate deduite.

### Preparation d'un SDK prive pour le binaire V12 corrige

L'inventaire diagnostique 61a3cb identifie 697 artefacts SDK (environ 5.58 GB) avant ajout des externs explicites, fichiers natifs et chemins Windows longs. Espace E verifie : environ 506 GB libres (1c624a). Les trois chemins -L existants sont preserves par remappage prive ; bibliotheques natives deja pinnees verifiees (f2b617).

BuildV12ClosedSdk20261008.py selectionne les artefacts reellement observes, leurs paires rmeta/rlib, les externs explicites et les bibliotheques natives pinnees. Deux essais prealables echouent avant creation du repertoire de sortie (f74893/c3480c) sur un extern explicite non enumere sous la meme forme puis un chemin Windows long. Corrections gardent les assertions : externs compares aux pins SDK, normalisation du seul prefixe Windows \\?\ avant resolution de chaque token ; aucun token manquant ignore. Le lancement suivant 438af0 annonce 739 artefacts et session 5717 ; PID22480 confirme actif avec 1450504459 octets lus (e157f3).

Chaque copie exige SHA source avant, copie et source apres identiques au pin existant ; la bibliotheque moteur est pinnee par le diagnostic V12 precedent. Les dependances sysroot observees et leurs paires disponibles sont pinnees et reverifiees. La liaison finale utilisera les options produit stables sans RUSTC_BOOTSTRAP ni -Z, uniquement des chemins -L/extern prives. Les sources figees, SDK prive et sysroot seront reverifies apres ; retention.json ne sera ecrit que si liaison et controles terminent zero. Au moment de cette entree, seule la copie est en cours ; aucune fermeture de SDK, liaison ou qualification GPU nouvelle n'est attribuee. Build V11 81346 laisse actif sans redemarrage.

### Audit GPU du budget acquis prepare pour V12

AuditV12AcquiredNativeBudget20261008.py prepare un controle distinct de la comparaison image : exige capture complete PASS et SHA de reference/binaire, controle 124 sequences de callback, horloges globales/ocean, disponibilite precedente native et absence du retour source aux deux premiers pas. Le probe doit confirmer budget natif actif, aucun head/etat initial/particule source injecte et execution du kernel de crete. Les heads consommes du rapport natif seront rejoues avec le vrai controleur Rust fige et compares aux budgets avant/apres ; les differences de heads source/natif resteront diagnostiques, sans les masquer ni les transformer en preuve d'image. Syntaxe et noms de champs source verifies (70f124), mais aucun PASS de cet audit n'est attribue avant execution GPU V12.

Git courant verifie (2ff0f0), branche Rust et fichiers cibles preexistants preserves. Source/native Vue Village inspectees a nouveau ; ecart visible de couleur et details persiste malgre amelioration TAA. Build prive session5717 reste confirme live PID22480 (c2b7e7) : 17965914687 octets lus, 4440230417 ecrits pendant les copies et leurs controles SHA. Build V11 session81346 encore live (be701a) ; aucune relance. Objectif complet et qualifications restantes inchanges.

### V12 lie avec SDK prive PASS, test GPU acquis lance

Session5717 termine code 0 (583afd) apres copie/verification des739 artefacts puis liaison avec options produit stables, sans -Z/RUSTC_BOOTSTRAP. Resultat closed-sdk-example-linked-20261008-v12/result.json : linked_game=true, compiled_inputs_unchanged=true, sdk_unchanged=true,1924 sources, compile_log_sha256 77b08b6430e7a33217a15fe4d423f94c580ac42ea2a97712cc22918fd665119f. Cette liaison prouve la disponibilite des dependances necessaires dans les chemins prives pour ce candidat ; pas Cargo, GPU, CI distante ou qualification de gate. retention.json ecrit uniquement apres tous ces controles.

CaptureCompleteReferenceVillage20261008.py lance ensuite session36196 (5a5d6b) avec retention V12, reglages defaults sans override et les memes paquets acquis complets que V8. Fixture preparee et PID45036 annonce (9d7a13). runtime.log confirme les identites vegetation/workspace (52cf11). Au moment de cette entree le test n'est pas termine ; ni image V12, ni audit des124 retours GPU, ni parite nouvelle attribuee. Build V11 session81346 continue sa reverification (230987), pas de redemarrage ni assimilation au resultat V12.

### Acquisition cote complete preparee independamment

PrepareCoastCompleteAcquisition20261008.py conserve les outils Village et derive un runner/driver distincts avec uniquement dossier de sortie, vue Coast existante et assertion/noms de fichiers Coast. Overlay exact et validateur budget source conserves ; controles des horloges,124 frames, validation GPU, dernier readback sans frame supplementaire et transfert par tranches restent identiques. Preparation47b02e PASS syntaxe Python/Node et outils originaux intacts. Runner c2fbe4fc836ea7d1cb61652c4d7eb7af3a152e182f20b741be29e24deacbdec5, driver dfe27b67b768276dd4c5f01a8034bd8b1f1de3570805793baf3c823465684295. Acquisition non lancee pendant la validation GPU V12 ; aucun paquet Village dependant de la camera ne sera attribue a la cote.

### GPU V12 termine ; ordre de feedback corrige prouve dans la scene complete

Session36196 termine code0 (62f3b7), diagnostic complet PASS et binaire/entrees intacts. Binaire V12 SHA600a63d099db848b9922c472b7b7b7faec2474b2829d10d1a9e3fb29cf18dc7b,133099520 octets, source manifest f25b9f6477656f7cb25a484e70e722c25ad1f49015aba5f336977f1718fed947. Image46c47230a3945d7c8e2e5c978f70f6e97992aa690e87e9ae5e8751e7a0fdd581 ouverte et inspectee ; aucune qualification visuelle deduite de la seule capture.

AuditV12AcquiredNativeBudget20261008.py termine0 (be253e), acquired_callback_order_verified=true, actual_native_heads_cpu_replay_pass=true. Les124 etapes, horloges globales/ocean et disponibilites natives precedent la consommation acquise attendue. Aux frames1/2 aucun head consomme ; frames3/4/5 heads natifs84/124/174 identiques aux observations source. Aucun budget, head ou particule source injecte. Budget SHA570f441c31d7008ebd26ee89531d48966b4b6cc15e422621eb1dcea59279b3cb, probe cdae0e969cfeb1fefce3c362a21d51eb8c1d483a70c76e879d806fc8fbd263ff.

La parite des heads reste fausse :75 retours differents, premiere difference frame27 (1234 natif/1235 source), ecart absolu maximal2 (9dd18e). Dernier retour14499 identique ; maximum d'ecart de budget avant/apres0.0001696663514844099. Ceci est un ecart GPU reel conserve, pas masque par le PASS d'ordre/arithmetic replay. Comparaison image e5d820 : MAE9.057514949845679, RMSE12.383476444192826, fraction pixels >16 0.2101904899691358, visual_parity=false. Rendu Village avec defaults quasiment inchange ; ne pas presenter la correction de feedback comme une correction de couleur. Essai TAA V12 lance session12056 (4bba43), seules temporal_aa=1/fxaa=0 modifiees dans profil prive ; resultat encore attendu.

### Reference cote complete recuperee et paquets d'entrees prepares

Acquisition cote session27151 lancee ensuite comme acquisition diagnostique independante pendant le runtime Village ; aucune performance/cadence qualifiee dans ces conditions. Session termine0 (8aa1dc), failure=null,428 sources/overlay et driver/validateur intacts. Image cote vue ; dernier readback1920x1080 bgra8unorm frame124/global1.9999999999999978/ocean2.066666666666664 avant/apres identiques, validation GPU nulle. Metadata b831dec7271258c9f6e36525fd53beb106f9a095b796a12391e306e552ff1ea8, PNG c83d3f4d10bf77bbbc49f884ee6de95b9e17d102d3d9e1768edb43452d513d0a.256 payloads environnement/nuages/ocean ont leurs SHA verifies (6102e5),122 observations de callbacks sur124, aucune repetition dans ce run. Pas de preuve native cote tiree de cette acquisition source.

PackageCoastAcquiredInputs20261008.py termine0 (3ef340) :428 sources et256 payloads verifies, paquets distincts coast-acquired-input-packages-20261008-v1/environment et ocean. Manifeste environnement05729dcd2939a863f033c04e37c5d10c1dfaa04271ee9e499c73970f646cc319 ; ocean604a474ccb197550b6b959b7f0b90aaf56d08360e943b3dc709c710263966cfc. Programmes/ressources du paquet precedent gardent leurs pins ;124 cameras preserves en bits f64, globals/atmosphere issus de la cote, public final80 octets et params atmosphere source64. Seul rayon observateur varie par rapport aux parametres statiques verifies. Cube environnement et SH captures sont explicitement des entrees lumineuses appairees ; aucune texture de nuages/FFT ou sortie de particules source injectee. production_loader_verified=false et gpu_executed=false ; les futurs controles de loader/rendu cote restent obligatoires.

### Capture native cote V12 preparee et lancee avec controles camera propres

PrepareCompleteCoastCapture20261008.py termine0 (07e582) : reframe de la scene figee avec metadata cote, execution du vrai GrassField.update source intact sur la grille originale et controle camera original PASS. Selection cote6/1/0 cellules selon niveaux, recu7221fb8ee63bc0a3e24eaed494cc8ac703318d9dbc8eec97450e76f83c9ef102. Audit source cote verifie428 sources/256 payloads et496 blocs packages/uniformes tous identiques ; recu babd3b0eb18e5f8ef84f4e60242d877348f0290262005c7f6e59fbb3cb0b5428.

Helpers capture/comparaison cotes distincts prepares par changements explicites de chemins/pins/vue, controles precedents gardes. Capture SHA2e964233d02b313fd707006c198d0bff617d1e71d31cc03c2b47e30710e2e3ac, comparateur81e293bc2f1ff17a083ccb530031285d85a65087c752c69f24acb9350d0fb3b2. Les outils Village et leurs preuves restent intacts. CaptureCompleteReferenceCoast20261008.py lance session44195 (e0d425), PID34932 annonce et confirme live (a524ab), meme binaire V12 retenu et defaults isoles. Le runtime charge65383 elements, zero retrait/deplacement, adapter RX7800XT Vulkan. Candidat TAA Village session12056/PID5980 toujours live (a524ab/4ccdae) ; ces captures diagnostiques simultanees ne qualifient aucune performance/cadence.

Le script d'audit budget accepte maintenant une vue explicite Coast ou Village, avec pin metadata fixe distinct et noms de sorties correspondants ; aucun changement de son controle des heads natifs/arithmetic replay. Audit Coast non execute tant que capture non terminee. Aucun resultat terminal cote, nouvelle image native cote ou parite visuelle attribue au moment de cette entree.

### Captures V12 TAA Village et cote defaults terminees, comparaisons attribuees

Session12056 termine0 (dfa03b), image7935b09a28e6a5c175d6218aab4b6c61ce01cfd77db0ff61bd40fb5e9593bb7e, pins/binaire intacts. Comparaison883f96 : MAE7.689667888374486, RMSE10.562802428280522, proportion pixels >16 0.14352141203703703 ; contre defaults V12 MAE9.057514949845679, baisse environ15.1%. Cela confirme le candidat TAA sur cette image statique, pas la continuite temporelle ou toute la carte. visual_parity=false.

Session44195 cote defaults termine0 (83b7e6), image eb7ccc69ff45fc479d492ec93234a0e84dbeb402879c1dd72d0eacb88963544a, diagnostic_checks_pass et binaire/entrees intacts. Image ouverte et inspectee : scene complete presente, vague/ecume et ombres visibles ; sable plus marque et eau plus crenelee que source. Premier comparateur193ea4 refuse tolerance absolue1e-6 sur position53.6. Diagnostic47098e montre exactement53.599998474121094, soit la quantification f32 attendue, sans autre decalage. Correction du comparateur remplace le seuil arbitraire par egalite exacte en f64 des valeurs f32 source quantifiees, signed-zero inclus par pack ; aucun seuil visuel change. Le FAIL initial reste consigne.

Comparaison077825 termine0 : camera_matches_source_f32_exactly=true, MAE7.381509452160494, RMSE9.981534906749072, proportion pixels >16 0.10323109567901234. Luminance source119.10301773379629/native113.93022073842592, expositions distinctes conservees, visual_parity=false. Le schema historique du comparateur conserve son nom Village mais les pins/vue/camera/image cote sont controles explicitement. Son SHA de preparation81e293... decrit la version precedente ; la correction de quantification est un changement ulterieur documente, pas une preuve retroactive.

Audit budget cote76fc85 termine0 :124 etapes, ordre des callbacks et replay du vrai controleur Rust sur heads GPU natifs PASS, source heads identical=false. Budget d6f6748384b932cbdf690ca4585aa120c8ddd2f28efa0654494089e1d9b96846, capture receipt5334500e5f31529dd83a9c9859506218e6d73b9349f1b4962de81329da5d9238. Pas de parite particules ou image deduite de cet audit. Essai TAA cote lance session49413 (0836bf), PID45368 annonce, profil prive ne change que temporal_aa=1/fxaa=0 ; resultat encore attendu.

Inspection source PostFX.js (1999f8), commit fixe, etablit chaleur0.02 appliquee par multiplication HDR(1.02,1,0.98), puis saturation et contraste autour0.18 avant ACES. Le raccord natif graphics.rs(bbdecc) utilise ColorGrading.post_saturation et midtones.contrast, sans cette multiplication de chaleur. C'est une difference de traitement concrete a corriger/qualifier ; son effet causal sur les metriques n'est pas encore mesure. La chaine inclut aussi positions de grain/vignette et exposition distinctes : ne pas annoncer une parite de traitement final sur la seule enum AcesFitted. Aucun nouveau shader/defaut produit modifie a ce stade.

L'essai TAA cote session49413 termine ensuite0 (b758f4), PNG15d7e0f361ca7cb2a2bf4e7519e545e3430a6a44dd5e84984ff89044b5589803, pins/binaire intacts. Comparaison cb7be0 et image ouverte : camera exactement quantifiee f32, MAE7.450574524176955/RMSE10.025977030014511/fraction pixels>16 0.0998316936728395, luma113.24651486545137 contre source119.10301773379629. L'eau est visuellement moins crenelee, mais MAE globale est legerement plus elevee que defaults cote7.381509452160494. Ne pas generaliser l'amelioration Village de15% a toutes les vues ni changer les defaults produit sur ces seules mesures. Parite cote encore fausse, differences sable/couleur visibles. Build V11 session81346 encore actif en reverification(43b37d) ; captures V12 terminees, aucun job capture a redemarrer.

### Calcul final source prepare puis compare reellement sur GPU

Inspection Bevy56af0d confirme que sa mesure auto-exposure est une fonction privee ordonnee avant tonemapping ; un nouveau ping-pong place arbitrairement avant tonemapping peut changer l'image mesuree. Ne pas raccorder une correction de chaleur apres ACES ni introduire ce changement de topologie sans controle. Le prochain raccord doit traiter les couleurs HDR et l'exposition dans l'ordre reel source.

PrepareSourcePostGrade20261008.py premier essai3be9f8 refuse l'ambiguite de plusieurs fonctions fragment dans PostFX.js avant creation d'output. Selection corrigee par l'ancre exacte de la passe finale lensDroplets ;994ae0 termine0. Bibliotheque source-post-grade-library-20261008-v1/source-post-grade.wgsl SHA14f4a0c94dafd2bee63272f2bc1a1ad71cda4369c1b2f1d4bd06fa465c94ef3b, corps source a92928cfca91f16972b7566360a16d6bc4fd91830a0a89f92720421dcfca2c3e. Helpers source et licence MIT conserves ; seul passage des entrees HDR/UV/pixel/frame/exposition en parametres explicites. La reconstruction inverse retrouve exactement le fragment original. Warmth/saturation/contraste HDR, vignette elliptique, grain triangulaire, ACES source, sRGB et dither final restent dans leur ordre source.

VerifySourcePostGradeGpu20261008.mjs termine0 (31aee2) sur le vrai device WebGPU headless source : cinq configurations et2048 pixels HDR par configuration, noirs/negatifs/faibles valeurs/mid-grey/highlights jusqu'a100, exposition automatique variee et seeds0/1/124/u32max/7919. Dix buffers originaux/adaptes retains ; sorties finies et identiques bit a bit pour chaque configuration. Perte de device annoncee a sa destruction volontaire, pas echec de pipeline. Cette preuve compare le fragment source et son adaptation callable ; lens/bloom/RCAS sont des entrees fournies directement et leurs algorithmes ne sont pas qualifies ici. native_renderer_adopted=false, aucun changement des defaults/couleurs du produit attribue a ce test isole. L'installation et son ordre/mesure d'exposition dans le renderer Rust restent a faire et a comparer sur les deux vues.

### Raccord natif source final V13 : implementation, shaders et liaison verifies

Reprise apres le point d'etat utilisateur : l'objectif est confirme actif (get_goal), le rapport courant et AGENTS/PROJECT_CONTEXT/roadmap/ADR-0017 sont relus. Aucun sous-agent lance. Le build moteur/exemple V11 precedemment encore indique actif termine bien zero : result.json confirme engine_exit_code=0, example_exit_code=0, 1924 sources, SDK et entrees inchanges, moteur SHA56059c366030d0aafe6e66c9f13f6c0a4abb2066dedb0baacaba464879ee13f6. GPU non execute par ce build.

PrepareNativeSourceFinal20261008.py verifie les pins de PostFX.js et de la bibliotheque callable puis materialise cinq shaders dans examples/tidewater_plant_probe/source_final_post/. Bloom : cinq downsamples RGBA16Float (premier Karis) et quatre upsamples tent source, dimensions arrondies comme Math.round positif, compteur d'exposition sur D3. La reduction WGSL conserve les huit barrieres source et les parametres refLum .25/min .6/max6/up1.6/down1.1. Le fragment final appelle la bibliotheque verifiee, apres ajout bloom, puis decode sa sortie sRGB en lineaire pour l'attachement final Bevy sRGB. Cette derniere frontiere doit encore etre mesuree dans la scene ; pas de parite globale attribuee. Les annotations @fragment/@location(0) Bevy sont explicites, sans changement du calcul source. Licence et attribution conservees.

Module source_final_post.rs installe la chaine sur le device Bevy : extraction Controls, cibles/64 octets uniformes/exposition GPU16 octets propres a chaque vue, exposition initialisee1, liberation de l'historique lorsque Controls disparait et recreation sur changement de taille. Apres tous les PostProcess et avant la fonction publique upscaling : bloom, grade lisant l'exposition precedente, puis compute AE ecrivant pour l'image suivante. Aucun post_process_write ni compteur avance tant qu'un pipeline manque. Aucun buffer prive Bevy, fork ou device separe. Le frame seed compte les passes finales reellement soumises ; dt provient de Time, night vaut actuellement0. L'historique n'est pas encore appaire a l'amorcage source124 frames : candidat de jour uniquement, pas preuve temporelle source.

graphics.rs ajoute un opt-in FORGE_TIDEWATER_SOURCE_FINAL_POST=1 ; pour cette camera uniquement, suppression AutoExposure/Bloom/Vignette/FilmGrain natifs, Tonemapping::None et ColorGrading defaut, source Controls alimentes par les reglages existants. Mapping d'exposition explicite .55*exp2(EV-.4), chaleur .02 ; ce mapping n'est pas une egalite numerique entre EV et scalaire source. CAS/lens/haze/GTAO/motion restent des entrees natives dont les ecarts doivent encore etre qualifies. Le chemin produit par defaut est preserve jusqu'aux comparaisons. Cette implementation exemple devra ensuite etre promue dans l'adapter commun moteur/Studio apres validation, sans logique de slug.

ValidateNativeSourceFinalShaders20261008.mjs termine0 (f45305) : cinq vrais pipelines GPU crees avec le vertex fullscreen Bevy, aucun message de validation en echec, shaders inchanges. SHA down_first284c7fd54e3a019a70089deaa30b9e6e0156cf8c4a38df9ac9b96a9188636ed2, down2168627e018f9d356a83fef371ccdaa2452e0e52c04524db509de43beecb3ef1, up4ea90eb177239367985b940ca10fa5e307a5cc4b2c17957286b8cd9413b1b029, meter791e27566b36e715f38aba9a8633313015efe97c7d79cca6c39535b2ba0031cd, final23a09687d7128a9b1d6264f715574f9f11848268012dbff97f1d171df0147891. Perte de device a sa destruction volontaire. Ce controle ne dessine pas la scene et ne qualifie pas le rendu final.

Premier helper metadata echoue apres freeze (b48566) : exec du freezer avait remplace la variable base du helper, creant seulement un repertoire diagnostique vide hors manifeste dans V12 puis cherchant inputs.json au mauvais endroit. Aucun rustc lance par cet essai, sources V12/V13 conservees ; execution isolee du freezer corrigee. Metadata V13-v1 termine1 (68c8e7), SDK et1930 sources intacts : nom Core3dSystems::Upscaling inexistant. Correction utilise la fonction publique bevy::core_pipeline::upscaling::upscaling. Nouveau snapshot V13-v2 conserve v1 ; metadata termine0 (09689a), 1930 sources et SDK avant/apres intacts, log5478a7eac51ee28e1b570e2cc5bf2f6295d39eaeed2e72dd1de01b77059233c3. Fmt cible et diff-check PASS, warning existant conserve.

LinkNativeSourceFinal20261008.py session15602 termine0 (0a7eb4). Binaire source-final-native-linked-20261008-v13-v2/example/tidewater_plant_probe.exe SHA2fa3864515fa5e9c2f7af5c2da0cc1c084f0fc64baa7de2d74407bc5ed671cf6,133279744 octets. Manifest523c3feddaeb406a92074e1e2266815a476ed9796d9023aff609f581cca55d85, 1930 sources/SDK intacts, logaf8cf421ed944266a9c63394344a08dfc3ba36f754b38da0e17537db94392964, result1d2b8504ff50bc198ea05fb2ef2f344205cf1b4ca83d7d6ec1c02e329d3cdf06. SDK prive V12 reutilise sans recompilation du moteur partage ni recopie du cache complet. Stable rustc link uniquement ; pas Cargo, CI, scene GPU, Studio, distribution ou gate qualifies.

PrepareSourceFinalCapture20261008.py termine0 (5aecc8) : derive un outil Village distinct, outil precedent SHA66e1a4f44947a5df5123c47bd588e8476d47dfce40f64078100111832a452504 intact. CaptureSourceFinalReferenceVillage20261008.py SHA db6a4559b4c8d46f3c183580d7a71638d304a7d84077079424f05881b8f185c6 ; ajoute flag enfant explicite et exige les marqueurs natifs submitted1 et124, garde tous les controles precedents de camera/20reglages/paquets/binaire. history_paired=false et limites jour/CAS/lens declarees dans le recu, sans fausse parite temporelle. Aucun test scene lance a ce stade : marge commit mesuree d'abord21447196672 octets puis15131439104 octets pendant une compilation concurrente independante, sous seuil16GiB. Cette condition interdit seulement le lancement lourd immediat, pas les travaux independants. Objectif toujours actif ; aucun job propre a redemarrer.

### Premiere scene native avec source final terminee ; ecart mesure, histoire encore non appairee

Marge commit remontee a21851664384 octets (dab5f1), puis lancement capture source-final Village session77893. Verification pendant l'execution de1a98 : marge20951576576 octets, PID22120 confirme live ; aucun processus concurrent ferme. Le GPU RX7800XT Vulkan charge65383 elements. Session termine0 f49726, diagnostic_checks_pass=true, binaire/1930 sources/reference/paquets/profil/siblings intacts. Marqueurs natifs soumis1 et124 verifies : dt .0167429 et .0176813, night0, exposition lineaire .55, bloom.05, AE1 (1ade14). Ceci prouve l'execution de la nouvelle topologie dans la scene ; pas une egalite de son histoire AE avec la source. Image c8e8f7e5dfc432e50ff4536ec2414d27e464adab8235563e26c40e374e280dd3 ouverte et inspectee ainsi que la reference. Recu capture0da2a139583256cffd9681442f8e9d0d8cf65dd31449bc9c43879ab3904dfdda, runtime log29d3b2f12fe663ec21cfaecf3e6f0a437ebba766cc5b7be03ab9df3c133a4fbf, runtime capture aa5bf0bfadec2d0b629c2769b7fe4e8065c65a2aab2b8027604db030a08055ea. Warning fermeture readback conserve.

Comparateur existant conserve termine0 (a4a85d) sur cette image : MAE8.868539094650206, RMSE11.866416623934892, proportion pixels >16 .17371190200617284. Defaults V12 MAE9.057514949845679 et fraction .2101904899691358 : baisse MAE environ2.1%, sans attribuer causalement ce resultat a la chaleur seule. Luminance native116.01867860619211 contre source109.82583881134258 ; ecart de luminosite plus grand que V12 defaults112.81595972955247. Le raccord ne suffit donc pas, malgre baisse de RMSE/grands ecarts. image visual_parity=false. Ne pas classer ceci comme mieux que le candidat TAA Village7.6897. source_final_history_paired=false explicite dans execution.json ; le comparateur historique ne verifie pas cet historique postFX et sa phrase sur les histoires acquises couvre seulement les recettes environnement/ocean verifiees. Aucun nouveau default produit adopte. Prochaine action utile : lire et appairer l'exposition GPU native, dt/nombre de passes et entree bloom/HDR dans l'ordre source, sans injecter une valeur AE finale source ni ajuster arbitrairement la luminosite.

Clippy direct sur tout l'exemple fige V13-v2 termine0 (432aa6),19 warnings dont un nouveau wildcard_imports dans source_final_post.rs. Correction canonique limitee aux imports explicites, aucune expression de calcul changee ; v2 teste et sa capture sont preserves. Nouveau snapshot V13-v3 :1930 fichiers, source_final_post.rs SHAfdba5d01613cfa87c776cf435e4334ade031b28fc78061fd7555fa0fc3988960, graphics/shaders identiques a v2. Metadata33976 termine0 (57d236), logb098d635bed8812c7ce89b48b3697697ff899cb239f566a1e58ccff03e15a1b5, SDK/sources intacts. Clippy79788 termine0 (9e7fa3) : zero diagnostic dans le nouveau module,18 warnings preexistants conserves dans leurs fichiers ; project_warning_free=false, aucune qualification Clippy workspace stricte attribuee. Loge394c82dbcc99537a23b6f6dd95f1617355cabab00d25c4cbbb7fdd0d13f2487. V13-v3 pas encore lie/executé ; le runtime ci-dessus correspond exactement a v2, pas a un binaire post-correction invente.

Tous les jobs propres de ce tour sont termines (metadata36549/7599/74275/33976, link15602, Clippy18995/79788, capture77893). Le premier helper36549 a echoue avant rustc ; ne pas relancer ces sessions. Objectif actif, pas de publication ni nouvelle validation de gate.

### Exposition source et native observees sur GPU ; ordre final incomplet identifie

Tour precedent classe progress : nouvelle chaine native, shaders, liaison et capture reelle verifies. Ce tour ajoute un traceur opt-in source_final_post/trace.rs via le Readback Bevy existant. Chaque ligne80 octets copie le vrai buffer Controls64, exposition precedente4 avant grade, exposition suivante4 apres compute, puis marker de fin4. Capacite2048, compteur source de la ligne issu du buffer GPU, selection d'une seule RenderWorld view par owner atomique afin que deux cameras ne partagent pas le trace. Lecture asynchrone vers JSON, aucun exposure source ou donnees de trace reutilisees en entree du rendu. Historiques par camera du renderer restent independants. Le trace declare history_paired=false et peut preceder la capture finale. Controle CPU valide longueur, markers consecutifs, bits f32 et continuite avant(n)=apres(n-1).

V13-v4 fige1931 sources : source_final_post.rs aa1ef2da5721a3d763878ca59b972ef0414f1ce33cadd649cca14748a8bbed77, trace.rs f31bbe238192542d8da8e30c776c0cad9fd1225df590a92cc2d5443d0cc1fa0a. Metadata64529 termine0 (5cad23), log7eeb5d2466b92e896c047c289f1ed29907d126b15eee303e9d2e724fffeb7865, sources/SDK intacts. Link60166 termine0 (e884e1), log3e4db3ca054a37bf340b919dfee8ca4cf7fdc4e819f3d4f7fbdb42c966fa01ed, binaire07f4c8263acfc7d1aafdce7a42badb3c652a82178295fe0912e79d61cb730522,133321728 octets ; manifest1fa795d84cbf3a71f79a621b957696a3298450dc1b0fb77d299fb33406d6b939, sources/SDK intacts. La ligne stdout du helper garde par erreur V13-v2/1930 : les fichiers/root et result.json identifient exactement v4/1931 ; cette ligne est corrigee pour les prochains appels, sans reecrire la preuve historique. Clippy61779 termine0 f4045e :18 warnings preexistants, aucun dans source_final_post.rs ou son enfant trace.rs selon inspection de all_diagnostics. Le filtre de classement du helper est ensuite elargi aux enfants ; pas de PASS workspace warnings-as-errors deduit.

PrepareSourceExposureAcquisition20261008.py b528bb prepare une acquisition source distincte, via archive git du commit intact et overlay instrumental propre. L'overlay intercepte _finalPass.render et meterKernel.dispatch pour copier avant/apres vers un buffer de32 octets par frame ; stamp GPU immuable, aucun wait ajoute dans une frame, aucune frame supplementaire. Lecture unique apres frame124 avec validation GPU et invariant des horloges. Driver/runner precedente conserves, option all-effects retiree : ce recu ne qualifie pas les256 payloads d'effets. Preparation : overlay a79c6fb5b9bde1b3cd72610754ce0045ac0988a3cd2f6ca084faea07ed68c93a, driver96ad2b56da988df2fcd5d1924a53c2c8c4f008e605cf8e2b62f87eb85bec93d5, runner22e931f86a82d7a0c6659666eadbaf2d5bcaba16370d3b4120db56218eb639c2.

Acquisition source60875 termine0 (51c280) : archive/export/driver/validateur intacts, owned browser/server fermes, aucune erreur GPU. AuditSourceFinalExposure20261008.py 5ebbf5 verifie428 fichiers sources,124 lignes et tous les bits copies, premier exposure1, continuite exacte, AE uniforms source32 octets identiques aux defaults attendus. Trace SHA4499498763c4dc61e452d047e59bbb72418263212cd8665cb009fa0dc8bf01a4. Frame124 lit1.752136468887329, compute suivant ecrit1.753116488456726 ;124*1/60=2.0666666666666664 s. Nouvelle image source0f340510a09e3a564af27ba807a19746384b05f8322f0333fde426d01f8d1f00 non identique en octets/RGB a V6 : MAE3.2150205761316875e-07, soit deux valeurs de canal differees d'un LSB sur6,220,800. Ne pas attribuer retrospectivement le nouveau trace au runV6 exact, meme si la difference est minime.

CaptureSourceFinalTraceVillage20261008.py prepare543786f56c7d298bcb3daa1d2d31ab433828256cc5d97bf5731178486b1be404, garde tous les controles precedents et exige trace GPU >=124. Session85298/PID17644 termine0 (6bb318), diagnostic complet PASS, binaire/sources/paquets/profil/refs intacts, capture a55dcd8fa37123c1eeb1d287114265cf76a7fa8bbb554f49d9fad0b1e09535f6 vue. Trace aa79abf0e1dbef25073349fcbd2f60462db94bee50bfaf64c907212b79a44749,433 lignes validees par audit9ffcac. A l'ordinal124 exposure precedente1.8563798666000366, suivante1.8564906120300293, dt cumule2.0076587982475758 ; derniere ligne lit1.877882719039917 puis ecrit1.8772574663162231, dt total7.483360501006246. Seule la premiere exposition est bit identique au source par ordinal. Cela etablit des histoires/entrees differentes ; la duree seule n'explique pas l'ecart present des l'ordinal124. Aucun readback ne prouve a lui seul la valeur exacte du dernier screenshot asynchrone.

Limite de lancement conservee : la marge avait remonte a20267438080 octets puis tombe a16122441728 avant l'appel de capture85298 ; l'ancien helper ne controlait pas le seuil16GiB a Popen. PID17644 etait deja lance quand l'observation suivante donnait13743972352. Ne pas qualifier ce lancement comme respectant le guard16GiB ; aucun processus utilisateur concurrent ferme. PrepareGuardedSourceFinalTraceCapture20261008.py ajoute maintenant deux mesures CIM immediatement avant le vrai Popen, enregistre les bytes/seuil et refuse sans lancer le natif si marge<17179869184. Nouveau helper CaptureGuardedSourceFinalTraceVillage20261008.py SHAe95598263c6202b0202953f106dffa6357833c3798147173e8e745ad3b89c3f1, anciens tests preserves. Cette correction rend les prochains lancements concrets et bornes ; aucune performance qualifiee par le test85298.

Comparaison c17e7f : MAE16.929675604423867/RMSE20.75430970973927/fraction>16 .6277001350308642, luma93.74589202035106 contre source109.82583881134258. Regression nette, malgre aucune modification arithmetique entre v3 et v4 en dehors du traceur : le traitement de luminosite n'est pas qualifie. Inspection du source reel compile (05da9d/5bd738) revele un ordre incomplet : flare et haze sont eux aussi after(PostProcess), haze precede ui/upscaling, notre final etait seulement after(PostProcess)/before(upscaling). Il peut donc courir avant ou apres haze/flare. Hypothese causale a confirmer par nouvelle capture, pas transformation du FAIL en PASS. Le mode haze legacy utilise display_fog/ACES pour une entree display-linear ; il faut aussi raccorder la branche HDR existante avant de revendiquer une chaine source exacte. Source RCAS est applique apres la pyramide de bloom, tandis que CAS Bevy est aujourd'hui avant le meter : difference supplementaire concrete a corriger. Aucune valeur d'exposition de remplacement ajoutee.

Canonique/frozen des deux modules engine sont verifies identiques avant edition (c9362f) : haze e91f58662e8f3698bfe4f4ab131dc6fba50549d21ee8f1344e4d815454ec8f16, flare6a9662cdb1c4d62cffd15760c82958ca3881b8aa22cac5eb525a03ca263f4d44. Ajout de deux SystemSet publics TidewaterAirHazePass/TidewaterLensFlarePass, sans exposer les fonctions internes ; final ordonne explicitement apres les deux et avant ui/upscaling. Aucun autre travail SourceHdr/temporal modifie. Fmt cible/diff-check PASS ea86de. Snapshot V14-v1 derive de V13-v4 par ces trois seuls fichiers. CheckOrderedSourceFinal20261008.py lance session13220 ; reconstruit les vraies metadonnees moteur puis exemple contre SDK prive, pas vieille rmeta ignorant les nouveaux sets. Au dernier constat03357e moteur rmeta emis et exemple emet son warning connu ; helper encore en reverification SDK, pas de PASS terminal, de nouvelle liaison ni GPU attribues. Sessions precedentes de ce tour terminees ; conserver/poller13220, ne pas la relancer. Objectif toujours actif.

CheckOrderedSourceFinal session13220 termine ensuite0 (0d8959) : engine_exit_code=0, example_exit_code=0,1931 fichiers/SDK intacts. Log moteur63901cc64ad2def6045d8b16faccc0dd9cf0070d92c7ad471fe447f142f0dd0e, exemple42bc047d8d721afd0b2f874981ef9a6d0cb62fe753a0054c62f57e7e4191c021. Source V14 : haze a47d58a15ffb63aaa7a8f8fd8a15e040d4d486de554b8bba0b3ebfae307cbdc2, flare4a8f3bc44c025636a59ba8da0d4c2ae76391806c2465cd6d9ada7d42aea1c9fc, final f826f48c7e970b1afe6c319384f37a814ee891b1d65625fd0516ac51f9044783. Metadata seulement, pas de preuve visuelle de correction. BuildOrderedSourceFinal20261008.py lance session14750 (5c4a24), compile la vraie rlib moteur puis l'exemple avec SDK ferme/stable, SDK et sources controles avant/apres, retention seulement au succes. Job lance, aucun PASS de liaison V14 attribue. Prochaine continuation : poll14750 sans relance, puis capture gardee avec/sans trace sur ce meme binaire pour mesurer la stabilite de l'ordre ; brancher HDR et logique source d'historique ensuite. Le verdict de regression V13-v4 reste visible et les defaults produit sont conserves.

### Etat verifie lors du point utilisateur du 8 octobre

BuildOrderedSourceFinal session14750 termine0 (900f12) : engine_exit_code=0, example_exit_code=0,1931 fichiers verifies, compiled_inputs_unchanged=true, sdk_unchanged=true. Rlib moteur33c27a7cc95e66982af3d03c7133a64e157ebd66ad23534ce53387eb4da298ee ; log moteur0ffef0ba177fcb4ea49ebf002fb19efa3784df76ab5dcebc543c3e61c2c4187c ; log exemple87915a4dacb7ff278f969b803d3ef2831b288d1751dd64fe6f66d5f152d127d6. linked_game=true, gpu_executed=false. Le build V14 est termine ; aucune capture ou parite visuelle V14 encore attribuee. Objectif confirme actif. Prochaine verification : nouvelle capture gardee, puis correction des entrees HDR et de l'historique d'exposition dans l'ordre source. Les validations gameplay, Studio, performances et paquet distribuable restent distinctes.

### V14 GPU : ordre explicite, paire avec/sans instrumentation terminee

Le point utilisateur precedent est classe progress : build14750 confirme terminal0 et preuve de liaison ajoutee. Reprise : AGENTS/contexte/roadmap courants relus, arbre modifie inventorie sans nettoyage, aucun agent lance. Capture gardee native-source-final-ordered-trace-village-20261008-v14-v1 session96422/PID24264 termine0 (b25332). Meme binaire retenu bf7f070812f7d5143ca1795c9e0552e01a134869fcee71e05426c14a6667413d, controles de sources/entrees/paquets/profil/binaire intacts. Deux mesures immediates avant Popen21068718080 et21066887168 octets, toutes deux >=17179869184 ; cette execution respecte le guard, sans requalifier retrospectivement le lancement V13-v4.

PNG33c710b4734f69a67158e2570928bb57e4f7c6eb60f5e41fe46bfc8ce86182bb ouvert et compare au PNG source fige. Comparaison05cd2e : MAE6.562745466820988, RMSE9.810955120386007, fraction>16 .14163049768518518 ; luma110.11723659027777 contre109.82583881134258. Amelioration nette contre V13-v4 MAE16.9297, aussi contre ancien candidat TAA Village7.6897, mais limites temporelles differentes conservees. Carte presente, ombres projetees visibles ; nettete/vegetation et couleurs demeurent differentes. visual_parity=false ; aucune generalisation a la cote ou au jeu anime.

Audit GPU exposition5fb1b9 :445 lignes valides, trace ecabc2afbe85fded919d1ae82dc27b8222b9809ce9aed22b8a70e74d5b205b88. Frame124 lit1.7872645854949951/ecrit1.7880882024765015, contre source1.752136468887329/1.753116488456726 ; dt cumule2.096768702380359 contre2.0666666666666664. Derniere ligne1.8157356977462769, dt total8.048445806838572 ; histories non appairees, aucune attribution certaine du dernier exposure au screenshot asynchrone. Aucun exposure source injecte.

PrepareOrderedFinalNoTrace20261008.py derive un helper distinct du guard dont SHA avant transformation est verifie. Il retire l'activation du trace et efface aussi la variable heritee ; absence de trace exigee, tous les controles de scene/paquets/sources et guard gardes. Helper CaptureGuardedSourceFinalNoTraceVillage20261008.py SHA92b17911661330727e6e6776afd426dcdfd4db81dfcedd1a1b873161220e4f23. Capture native-source-final-ordered-notrace-village-20261008-v14-v1 session86116/PID43156 termine0 (652b48), diagnostic_checks_pass=true, aucun trace GPU cree. PNGa97ef0b37a9f2558d5419909ca23106abe59bddd44d482bd538afe917c238461 ; comparateur7629c0 MAE6.5629967206790125/RMSE9.810907186389995/fraction>16 .1416063850308642, luma110.11667848215664. Les deux gardes >=16GiB sont verifies par l'audit, aucun test de performance attribue.

AuditOrderedFinalPair20261008.py31d171 valide meme binaire, cameras/reference, recus et deux gardes pour chaque execution. Paire RGB MAE.4921735146604938/RMSE.7353001516485654/max canal3, images non bit identiques. Cela soutient une stabilite observee apres ordre explicite, pas un determinisme temporel ni une preuve causale exhaustive : deux runs wall-clock, grain et exposition distincts. Rapport ordered-final-instrumentation-pair-20261008-v14-v1.json. V13 FAIL conserve. Defaults produit toujours inchanges ; tous les jobs propres de ce tour sont termines, aucun nouveau build en attente. Prochaine tranche : reutiliser la brume HDR existante et remettre RCAS a sa place source (apres bloom, proxy Reinhard et sky depth), puis synchroniser l'historique logique, comparer les deux vues et les sequences animees. Le moteur/Studio commun, gameplay, performance et distribution restent a qualifier. Objectif actif.

### V15 : RCAS source raccorde apres bloom, sans CAS natif cumule

Tour V14 classe progress par deux captures GPU terminees et audit de paire. PrepareSourceFinalRcas20261008.py verifie les trois fichiers canoniques identiques aux pins V14 avant edition. Premier essai5ce91e refuse le hash du texte normalise CR/LF avant toute mutation ; correction utilise les vrais bytes source, qui retrouvent9e81a6de... (a1da42). Le shader extrait tm/loadResolved/rcas du source fige, renomme seulement bindings/fonction, remplace l'interpolation JavaScript de constante par -0.1875 ; calcul proxy Reinhard, lobe, inversion et skyK profondeur preserves. Source RCAS applique au fragment final apres les passes bloom ; pyramide et meter consomment l'image non accentuee. Binding profondeur natif ajoute au final, force Msaa::Off uniquement dans ce candidat, CAS Bevy retire uniquement dans ce candidat. Le reglage graphique sharpen alimente timing.z, sans nouveau champ de persistance ni deux nettetés cumulees. Defaults hors opt-in inchanges.

Fmt cible et diff-check PASS be12ef. Snapshot V15-v1 derive de V14 par trois fichiers seulement,1931 inputs : source_final_post.rs812256f35214c29bc9f4f2faef665ea89b5b5e6ac4b1c8b9370af89f4bba1015, graphics573815ca1e8d0b5561d248c1b3803eb820d0c6894bff3bec6f9a32cf7ba226ad, shader finalbb9dfc9c939123c99ce290b5281b6f7cabbd70fa7f051ab0710dc2382e9ad507. BuildSourceFinalRcas20261008.py session3792 termine0 (e9d9ba) : metadata et liaison exemple reelles PASS, SDK/sources intacts ; reutilise la rlib V14 engine inchangée et pinnee33c27a... au lieu d'une nouvelle compilation moteur inutile. Logs d1a0933c5fe2b6175a0a349c703ef305a3951050a399acf8d550e61b53f1a0ee/d9a1b4588a57049ea0d144a949c6e83dcebf6ffa5216730d3275170102aa8926. Binaire631d95b79f69086d3a837dedebc5580280f709ae4d1f32bc752de188f0c325dd,133330432 octets.

Capture gardee native-source-final-rcas-trace-village-20261008-v15-v1 session63570/PID45600 termine0 (a03925), controles sources/binaire/paquets/profil intacts, marge19828027392 puis19836784640 avant Popen >=16GiB. PNG42b3777cb2ab0f7b5780c7ecb141913045eceb0141c7954a216b51a483a51252 ouvert. Comparaison7a3d12 : MAE6.629913998199588/RMSE9.890280272680384/fraction>16 .14430025077160494, luma110.18215424816744 contre109.82583881134258. Legere degradation contre V14 trace6.5627 ; ne pas annoncer d'amelioration visuelle de ce changement. Le calcul et son ordre source sont plus fideles, mais l'entree HDR et l'historique restent differents.

Audit exposition7a3d12 verifie483 lignes, tracef5f01be5eff0b7d69be8e4edb770fa2bae0b8d385d6db01e3088d19de04e930c. Frame124 lit1.894652009010315/ecrit1.893216609954834, dt cumule2.0953117925673723 ; derniere ligne1.816308856010437/1.8163073062896729, dt total8.923983484506607. Histoires non appairees. Verification21e988 du vrai uniforme GPU : toutes les483 valeurs timing.z bit identiques a0.20000000298023224. C'est le profil historique, pas le0.45 du source. Le texte historique du helper disant CAS natif reste une limite datee de sa version, la retention V15 et sources identifient la substitution effective.

PrepareSourceRcasStrengthCapture20261008.py prepare un helper distinct verifiant les bytes PostFX et son default sharpen0.45 avant d'autoriser exactement ce second essai ; aucun ajustement arbitraire de luminosite. CaptureGuardedSourceRcasStrengthVillage20261008.py garde tous les controles et guard, clarifie la limite RCAS au lieu de native CAS. Session27581 lancee (57fae7), resultat encore attendu ; seul override sharpen0.45 et meme binaire V15. Ne pas attribuer de nouvelle image/resultat avant terminal. Objectif actif, aucune publication.

### RCAS a la valeur source0.45 : execution terminee, ecart visuel conserve

Session27581/PID30492 termine0 (bbc7eb), aucun job capture restant. Helper distinct SHA32e8ff5841e195bbb68f83ad469c6c0ae2f69d4bb403201b986cfca5aa5249b1. Meme binaire V15 ; seul reglage sharpen0.45, declare et verifie par recu runtime56317e9e6dbaac49a060d8d839007329bccd51ff30cbcceaa28621831ef13554. PNG0f59fe22b8bf53114ad6546c307ae64d1b33a30b476f78bbeafd64c10e650a10 ouvert. Comparaison45ecb6 : MAE7.134575135030865/RMSE10.528896319024101/fraction>16 .16570987654320987, luma110.10160529388503 contre109.82583881134258. La valeur et le calcul source ne suffisent pas : resultat moins proche que V15 a0.2 et V14 ; conserver ce resultat, sans reverter l'algorithme source uniquement pour optimiser une metrique statique ni adopter ce profil produit. Les entrees HDR, AA et historiques divergent encore.

Audit45ecb6 verifie495 lignes, tracead1cae42f8f67fdcf9d5fbc670f3d0152f5b0f66eb32ccbc10111d0eb30eb04a. Frame124 lit1.7968367338180542/ecrit1.7973754405975342, dt cumule2.1957226991653442 ; dernier1.8162670135498047/1.8162668943405151, dt total9.34356639161706. visual_parity=false et history_paired=false conserves. Les warnings readback a la fermeture ne sont pas des erreurs de compilation du pipeline, mais restent dans les logs.

Prochaine tranche requise : rebrancher la brume HDR existante et le parcours temporel source sans confondre le mode legacy display-linear avec une entree HDR. Source RCAS attend une image resolue TAA, tandis que ce profil historique reste FXAA1/TAA0 ; ne pas generaliser les deux essais de nettete a une chaine source complete. L'ordre final V14 et sa paire stable sont acquis dans leur perimetre, pas les sept vues ni l'animation. Defaults ordinaires inchanges, nouvelle nettete seulement opt-in. Tous les builds/captures de ce tour termines ; le controle final des uniforms et gardes est en session52186, a poller sans relance si pas encore terminal. Objectif actif, aucune publication, aucune nouvelle qualification de gate.

Controle final session52186 termine0 (c895af) :495 valeurs uniformes GPU bit identiques a0.44999998807907104, marge avant Popen20749398016 puis19569291264 >=17179869184 ; diff-check cible PASS. Aucun job propre encore actif. L'exposition source n'a pas ete injectee. Cette preuve complete le controle du reglage et du guard, sans changer le verdict visuel.

### Sonde de la chaine HDR existante : refus temporel correctement conserve

Le tour RCAS precedent est classe progress (code, build, deux captures et audits termines). Reprise : inspection du parcours source existant resolve.rs/temporal_aux.rs. SourceHdrSystems ordonne GTAO/Haze/Underwater/Resolve apres MainPass et auxiliaires, avant EarlyPostProcess. La presence de ResolveEnabled selectionne deja le vrai shader haze HDR ; inutile de creer une seconde pile. La sonde doit verifier aussi la couverture auxiliaire, pas seulement voir une image plus proche.

PrepareSourceHdrFinalProbe20261008.py derive un helper diagnostique distinct du guard RCAS verifie, SHA46dfdc1508ef8a7a6636fe7503c23f918ea80e142ef5d9d6d1f26d1f5f9689c7. Active TEMPORAL_AUX/RESOLVE/RESOLVE_READBACK, meme binaire V15, profil prive sharpen0.45/fxaa0. Le parcours historique impose non-pipelined rendering et possede sa capture avec mouvement camera et reset ; les checks de beauty statique du helper restent inchanges pour ne pas transformer cette sonde en PASS comparatif. Session68609/PID11816 termine1 (064384), source/binaire/entrees intacts, aucune image finale. Sonde reelle, pas une validation visuelle.

Recu village.aux.json : timeout borne180.0352739s, committed_aux=0/committed_resolve=0, 19264 objets visibles,19211 enregistres/queues/draws,53 unsupported,0 pending,0 errors/derivation_errors. Les53 diagnostics exacts, aucun omis, sont tous queue.material-type pour Unregistered TypeId(0xae44ccc982455ce7147c81633c1f0790), raison Visible material type has no explicit auxiliary contract (9b2b45). GTAO5 vrais draws, haze1, underwater1, sans erreur, meme tag2824 ; resolve garde IncompleteAuxiliary. Ce sont des recus d'encodage HDR, pas completion GPU ni couleurs source equivalentes. Refus conserve, pas de masquage des objets ni bypass du controle complete().

Le rapport 2026-10-06-scanned-temporal.md local decrit une contribution preparee non adoptee pour ScannedMaterial : deriveur strict Scanned, dispatcher et enregistrement dans les deux adapters. Le chemin historique contient maintenant INDEX/LIRE, pas les sources actives ; archive conservee sur F:, group-0025-6c4bde485de1.tar.gz, recu d2fc22f743e2c7e9d1a1. Recuperation independante RecoverScannedTemporal20261008.py verifie le SHA archive entier puis les fichiers selectionnes vers un nouveau dossier E:, sans modifier F: ni la junction originale et sans executer l'ancien tests.exe. Session28836 lancee (a14de1) et toujours observee active (bf78ca), PID11564 confirme live (943c39). Ne pas relancer ni annoncer les sources recuperees avant terminal. Aucun fichier canonique modifie dans ce tour jusqu'a ce point. Prochaine action : poll28836, inspecter les diffs/preparation historiques contre le courant, integrer seulement la contribution Scanned compatible puis reconstruire et rejouer la sonde. Les53 objets ne sont pas encore prouves Scanned par le TypeId seul ; la nouvelle inscription et les diagnostics devront le confirmer. Objectif actif, aucune publication.

### Contribution Scanned recuperee, fusionnee et compilee avec le courant

Recuperation28836 termine0 (fa3be9) : SHA global archive8b6c42c3ab5253f501ce8def207b6f1e2a81eab524535e648e9ce25057098f4e verifie,11 fichiers sources/recus recuperes et hashes individuels verifies dans scanned-temporal-recovered-20261008-v1. Aucun ancien tests.exe execute ni recu transforme en nouvelle qualification. F: et junction originale inchanges. Lecture de preparation et diffs contre courant (cbfe91/a9d9a8) montre que recopier les anciens fichiers retirerait FullStall et le controle stall-contact-same-camera-tag. La fusion ne retient donc que Scanned, son module strict et les deux enregistrements de materiau. Les fonctions vertex/surface/LOD courantes sont derivees a l'execution, pas remplacees par des shaders anciens.

AdoptScannedTemporal20261008.py premier essai f1b384 refuse une ancre Ocean presente deux fois avant toute ecriture ; correction utilise l'ancre unique du deriveur Pebble, c2e824 termine0. Les trois hashes canoniques courants controles juste avant ecriture : shadersfa43b7fa..., temporal_aux919c9feb..., temporal_ownedbd089c9e.... Nouveau scanned.rs issu du fichier archive verifie31be0a... ; rustfmt cible reformatte seulement la presentation. Le manifeste scanned-temporal-adoption-20261008-v1.json contient les diffs et atteste conservation FullStall/contact, pas un resultat GPU. Diff-check cible PASS5349f9.

CheckScannedHdrFinal20261008.py session44003 termine0 (4315b4), reconstruit vraies metadonnees engine puis exemple via SDK ferme. Snapshot V16-v1 derive de V15 par quatre fichiers uniquement,1932 sources : shaders4b97b66f5aa5cd003f324f6b007edb9b79445ae8f9327b92a797124e8797bda3 ; scannedc4b2906d7564b893688672441bb2eedaac23db867b38bbd4ead6c85dd1dd844b ; temporal_auxbdcceaf895330ef217a322206caa99e614fa338d37f9b4ddef294e866d974d10 ; temporal_owned01c8a3ce0a2b9576ef60001a49dde4a7aff925424ce942ea4f61fd4038609a58. Engine/example exit0, SDK/sources intacts ; logs774cb02188f0301b1a9f74e8866d2a52b5e73195f47bf49af0343b5efff298da/a531c484699b1b0f6050afdf0b763a0de45367b401811603f65ecbfec45299b8. linked_game=false/gpu_executed=false dans ce resultat de metadata : ne pas qualifier la correction des53 objets par compilation seule.

BuildScannedHdrFinal20261008.py lance ensuite session15067 (69fa43), vraie compilation rlib moteur puis exemple contre sources V16 et SDK verifie ; PID rustc34348/parent17240 confirme live4a57ec, session encore activebcf168. Pas de nouveau binaire ou PASS GPU attribue. Prochaine continuation : poll15067 sans relance, puis nouvelle sonde gardee ProbeGuardedSourceHdrFinalVillage20261008.py avec la retention V16 et un case neuf ; examiner couverture/materiau, recus GPU sept plans/reset et HDR avant toute comparaison de beauty. Une acquisition temporelle de preuve qui deplace la camera ne remplace pas la capture statique Village. Le refus V15/53/timeout reste conserve. Objectif actif, aucune publication ni cloture de gate.

### V16 lie et teste : les53 objets refuses sont identifies comme FishMaterial

Tour precedent classe progress (fusion Scanned et vraies metadonnees Engine/exemple PASS). Build15067 confirme termine0 (5bc92c) :1932 sources/SDK intacts, rlib730a539b7c98bae8020c3887a66823d222f23b17c7a736e8b984cd54641ebc75, logs577262ae02b3dd858d5e8f007aa6f99d4e4a703341f4be13ea09488027e1e82c/d60a5d07eb104f1e942ffd0a7a0a52724dd29bcdd240684bcb34a5010387d778. Binaireaf4060bd445cc9afa9c7344ac6ad344f2d9259bea9d577dee9827a9e03192cc9,133298176 octets. Premier essai V16-v1 session82017 terminal (bd76d6) refuse avant Popen, runtime_executed=false : marge17049161728 <17179869184. Aucun PID/binaire GPU execute dans cet essai. La sortie de transport indique1 alors que le helper leve SystemExit3 ; le recu identifie explicitement le refus, sans inventer une execution.

LintScannedHdrFinal20261008.py utilise vraies arguments Engine puis exemple du check V16. Sessions57726 et12481 terminees0 (de5dbd/f04f4c). Les chemins heritent du nom du template v13-v16-engine/example ; un premier read attendait a tort v16-engine et ne trouvait pas le fichier, corrige par5a5f54.119 diagnostics engine,18 exemple ; sources/SDK intacts, zero diagnostic dans nouveau scanned.rs et source_final_post.rs/enfant. Aucune qualification whole-workspace warnings-free. Logs a6ac6e73473357560d7b14971399879713f4e16e6610e31fa4048d38877dac4c/9f8a466f611475fe0bb302f34e37fcaba4f556970274cb56943603a52503287c. Les119 warnings engine sont conserves, pas classes retrospectivement tous preexistants sans baseline equivalente.

Memoire remontant, nouveau case V16-v2 lance session21662/PID39456 (879b95), meme helper/binaire et seuls sharpen0.45/fxaa0. Deux mesures immediates18776866816 puis18764087296 >=17179869184 (c3b635). Chargement/initialisation Vulkan lent : PID et session verifies vivants plusieurs fois, pas de relance sur observation sans output. Adapter RX7800XT/driver26.8.1 annonce avant warmup. Puis vrais passes : GTAO5/haze1/underwater1, mais toujours19264 visibles/19211 draws/53 unsupported. Session termine1 (20136b), timeout preuve180s, aucun screenshot final. Auditf35ecf confirme sources/binaire inchanges, committed_resolve0, dernier tag2662, preuve57afd0fc371964f179192bcb8f02f9be3c31331b8bff8fd1bd70e8bab722c989. Aucun PASS HDR complet, output pixels ou performance attribue. La contribution Scanned ne corrige donc pas ce refus ; ne pas la presenter comme correctif des53 poissons.

Identification decisive via petit executable CPU lie a la rlib V16 exacte : IdentifyAuxiliaryMaterials20261008.py18be37 termine0. ScannedMaterial TypeId(0x83ad9785f06699836926462654fa488b), FishMaterial TypeId(0xae44ccc982455ce7147c81633c1f0790), FullMaterial TypeId(0xbc3e4096457b4b27952ff061d5b4b72c). Meme engine SHA controle avant/apres, aucun GPU. AuditUnsupportedFishMaterial20261008.py fbbbfb joint exactement les53 diagnostics non omis avec ce TypeId et cette rlib : tous sont le vrai forge_engine::tidewater_fish_props::material::FishMaterial, pas Scanned. Recu unsupported-fish-material-audit-20261008-v16-v1.json, identification9ae108762fbc3de5d4bd2009352bf0c54da86ba92ec2e8f74eacc5221966ae2b. C'est une classification par vrai type compile, jamais par nom/slug d'asset. correction_applied=false conserve.

AuditSourceHdrFinalProbe20261008.py prepare pour des recus reellement complets : deux captures six plans, trois captures sept plans, SHA/buffers/packet336/params32/reset et coverage complete requis ; pas execute comme PASS sur cette sonde echouee. Le gate n'est pas assoupli. Inspection pinned MeshShader.js etablit reprojection source wp=model*lp/pwp=prevModel*lp sur meme position locale deformee ; Fish vertex natif utilise input.position et vrai world_from_local, fragment conserve lodFadeVisible et frameIndex. Prochaine correction : famille Fish stricte dans le dispatcher, prev-model varying pour vrais meshes, garder la couverture LOD source et les layouts/skin/rest attributes, enregistrer le vrai FishMaterial dans les deux adapters ; compiler shaders/imports et crate, puis nouvelle sonde complete. Le frame HDR/final historique ne doit pas etre appaire tant que resolve est incomplet.

Un read-only exec_command a rencontre un timeout de review automatique (cell1159), pas un refus de danger ; retry unique identique a passe66ea45. Aucun autre blocage d'approbation. Tous les jobs propres de ce tour sont terminaux (build15067, refus82017, lints57726/12481, GPU21662, reads47006 et identification18be37) ; aucun processus de preuve a relancer. SourceFinal defaults restent opt-in, source-frame exposure history non appaire, objectif actif, aucune publication ni nouvelle qualification de gate.

### V17 : vrai FishMaterial derive et enregistre, validation CPU PASS

Le point utilisateur precedent est un etat verifie, sans changement de code ; reprise concrete du correctif identifie. Sources courantes/AGENTS/contexte/roadmap et diff Git relus, travail concurrent preserve. Ajout fish.rs et Family::Fish, enregistrement du vrai FishMaterial dans temporal_aux et temporal_owned. Aucun branchement sur nom d'asset/module. La derivation garde Input/Output rest/data et skin storage19, vertex original, matrice precedente du mesh sur la meme position locale courante, frameIndex et fonction de surface/LOD originale ; varying5 refuse s'il est occupe. Aucun fallback camera ni assouplissement de couverture.

Extraction CPU avec rlib V16 exacte : premiere recherche de village.wgsl dans le dossier scene echoue, car ce shader vient du BuildingPackage charge par --building-package. Aucun shader fictif utilise. Reexecution du meme petit outil avec preview-scene-bundle-fish-stand-content-fish-011/buildings/village.wgsl reussit (5b56eb/0c6c1b). Source installee fish.wgsl SHA37969ba8353b222571869798b8426cbc6552f105269d04b9d761dc742eecc259 ; seul import mesh_functions/fish_camera, sans import PBR de lighting a retirer. La fonction fishSurface inclut aussi une couverture dither ; elle reste intacte. Le helper d'extraction est corrige pour une future acquisition distincte v2 ; la source v1 conservee est celle effectivement validee.

CheckFishHdrFinal20261008.py session89818 termine0 (12a18b) : engine et exemple metadata0,1933 fichiers sources/SDK intacts, logs fedf0f81bb1ea4c93777ea52bda01ad1584bcad7711579172fdd3ca5d3bc131b/6326a5c50b466a0cec93c941c31eda7daf4d7abc9fafc439c0a339f8d73766bb. Snapshot V17-v1 derive de V16 uniquement par shaders/fish/temporal_aux/temporal_owned. Hashes respectifs f83134df850b3c63da77e29754c45fb3c70764d11c27d2d88b098490519fd048/b93085d28557f4e1a287ecf846b18272e4ac81a3eff63e8c04c6b3297009f89f/fe8dab73739d3d6a3a71148c2cbdb83f2ebca062b4134fdd39d59c8ef628d763/61cb3b02dd9d8d91403d8217244522b8560524a1c9fe01e73e7ef9dee7168e2d.

VerifyFishTemporal20261008.py 7aad59 termine0 : deriveur canonique actuel, actual installed source, huit variantes Naga vertex/fragment x shared-lighting x mesh uniform/storage avec vrais imports Bevy0.19.1. Le validateur exige exactement le packet en group1/binding0. Rejets de varying incompatible, LOD drift et vertex seam double verifies. CPU seulement, aucune execution GPU ; source_pins intacts. Diff-check cible PASS fcbdcf. La correction de la couverture des53 poissons sur GPU reste NON PROUVEE.

BuildFishHdrFinal20261008.py lance session11191 c29e6f : vraie rlib moteur puis exemple contre SDK ferme. Au dernier poll48dcb1 session active, processus rustc confirme fcbdcf. Ne pas relancer sur observation sans output et ne pas attribuer de PASS liaison avant terminal. Prochaine continuation : poll11191, puis sonde HDR gardee sur retention V17, verifier zero unsupported, completions/readbacks/reset, avant comparaison visuelle. Defaults produit inchanges, objectif actif, aucune publication ni nouvelle qualification de gate.

### V17 liaison PASS, sonde GPU gardee effectivement en cours

Reprise de session11191 sans relance :82d47b terminal0, vraie engine rlib et exemple lies,1933 sources/SDK intacts. Engine SHA89144b54c75af1d7b6309aa9c704c6c5dff4ac8b5c073a60e4eacdc06e3ee51e, binaire0f4ec89da891dc6acaaf1d747b53b96ad6c92eafcc992b8bccb9eaff25f704ab,133350912 octets. Logs471f40418ab056fa48c64f071833fdfdff61eae0a5997420a878dfe65a5d348a/8e78e228582b0775143f8939c76e680348cebad01c3d6fb07338421f1a34a0fd. Build result89c723ef57b1ac96272ebc790abc4768204bae9d56a881659c5d17f209d79b06.

Sonde ProbeGuardedSourceHdrFinalVillage20261008.py lancee sur cette retention V17, case native-source-hdr-final-probe-village-20261008-v17-v1, sharpen0.45/fxaa0, acquired-environment-ocean. Session91921, PID15144 parent42384 confirme live194b7b/e2ba20. Mesures juste avant Popen17539158016 et17514348544 >=17179869184. RX7800XT Vulkan/driver26.8.1 annonce, vegetation canonique65383. Initialisation lente ; derniers polls9986b3/be1ad5 confirment session/processus toujours vivants CPU3.72s/WS895111168, aucun village.aux.json encore. Ceci est une attente verifiee, pas un timeout terminal ni une raison de relancer. Prochain tour doit poller91921/memePID et garder les recus GPU futurs comme verdict, sans annoncer couverture complete maintenant.

LintFishHdrFinal20261008.py session11777 terminal0 (ce70fe), resultat lu4e29b8 : SDK intact, aucun diagnostic dans fish.rs/scanned/source_final modules. Log7f6c231b0a841a326c1bf3b2bf4cd26252da2298afe47b317a5dfc411cfbe00b. Warnings du reste du moteur conserves ; pas de qualification workspace warning-free. Aucun job compilation propre encore actif ; seule sonde91921 reste en cours. Objectif actif, correction GPU/histoires/parite visuelle toujours non prouves, aucune publication.

### V17 GPU : refus Fish resolu, vraie chaine HDR/readbacks/reset PASS

Tour precedent classe progress et attente verifiee : build et Clippy terminal, sondePID15144 encore live. Session91921 poll011e6c/83f442 puis9fae5d : meme PID confirme, pas de relance sur initialisation lente. Terminaison e087de : runtime natif exit0, image04102b70c50a41d2a84d3c919dc6c866d7c437d39250bf1c7ad14b845ba29c41. Helper global exit1 conserve car les checks historiques de beauty requierent village.capture.json et124 frames/trace, tandis que la preuve auxiliaire possede une camera deplacee/reset et sa capture avant ces124 frames. Aucun changement des checks pour obtenir un vert.

AuditSourceHdrFinalProbe20261008.py503eb6 termine0 : tous les pins binaire/source/input/profile/siblings/reference intacts, deux guards16GiB, proof gpu-readbacks-captured/error null,49 commits resolve,exact19264 visibles/queued/draws,0 unsupported/0 pending/0 erreur. Deux snapshots six plans et trois snapshots sept plans, soit33 plans GPU, chemins/buffers/SHA/packet336/params32/completions verifies, history-reset-seed vrai et previous-depth-clear de cette capture confirme. Resolve observe acknowledged/output/depth copie ; GTAO5+haze1+underwater1. Aux committed0 normal avec ownerResolve, jamais annonce comme fail temporel. Le refus des53 vrais FishMaterial est corrige dans ce perimetre GPU. ProofSHA46b5a70c11869ec1e5d2fc129297863cc0e21194d0505191970e7240dd62a9c2, executionSHAe557f96ccd318af4ec8d742f763295cf4efc09d14145f0cc88db541e6d2ab8c2. Image ouverte/inspectee4e20d8. source_frame_history_paired=false et visual_parity=false/performance_qualified=false ; cette camera apres mouvement ne remplace pas la paire Village source. Warnings readback fermeture conserves. Processus15144 absent au611b9f ; sonde terminee, ne pas relancer91921.

### V18 : exposition finale conditionnee a la vraie resolution de la meme image

Inspection source_final_post : auparavant avancait final/exposure meme avec ResolveEnabled et resolve incomplet. Correction opt-in ajoute deux composants optionnels ResolveEnabled/ViewPacket et resource optionnelle ResolveObservations. Dans la lane resolve uniquement, exige snapshot present, tag exact, history_epoch exact et complete(), avant submitted, uniform writes ou ping-pong. Lane sansresolve preservee ; ne fournit pas de valeur d'exposition capturee et ne declare pas un historique appaire. CodeSHA6e08671dd14f85dd4b7456fe007155f63f23bbc9ca8470001c9ba5a96a49169e. V17 GPU ci-dessus a execute le binaire precedent sans cette garde, jamais qualifie retrospectivement comme V18.

CheckResolvedFinalGuard20261008.py session48219 termine0 (7da4cc) : snapshotV18 derive deV17 par ce seul fichier,1933 sources/SDK intacts, compilelog4f3748f359f1cdf377131c749824e24a803b7be9224ac325451124b85967bcc9. Reutilise vraie engineV17SHA89144b54..., pas une ancienne rmeta. Diff-check PASS5666f1. LinkResolvedFinalGuard20261008.py98334 termine0c9e179 : vraie liaison exemple, sources/SDK intacts, loga8748d01d636893280b8b8596daffaa69ef04fbf0aea862338c50aed16b843a8.

Nouvelle sonde gardee V18-v1 lancee seulement apres V17 termine, session26238 (94127d), meme helper/parametres/paquets avec nouvelle retention. Preparation encore en cours au dernier constat ; ne pas attribuer de lancement GPU avant PID/guards effectivement recus. Prochaine continuation : poll26238, auditer completions/source-final trace guard et readbacks ; puis acquisition beauty source HDR a camera/histoire appairees. Defaults produit inchanges, objectif actif, aucune publication ni nouvelle qualification de gate.

V18 lancement effectif confirme c77a5d : session26238/PID20484. Conserver ce meme job pour le prochain poll ; la preparation n'est plus seulement prevue. Verdict GPU encore en attente.

### V18 et V19 : chaine HDR complete et vraie histoire finale GPU verifiees

Tour precedent progress : V18 construit/lance. Poll26238 conserve memePID20484, puis07078f terminal : natif0/image3ef4b8f7556f95883be241f47a0f98072dc9332017a0ff739aa176d5dcefb78e, helper1 car checks beauty historiques non adaptes a la lane camera-moved/reset. AuditSourceHdrFinalProbe f7a5dc terminal0 :33 plans GPU verifies,49 commitsresolve, coveragecomplete/resetseed vrai, binary565bc6f5138ceb8d2a8ce62127c483b4c65a7b27d1338c23b24322bab8a4784e, proofd55e0d2adb76514e1f019ec3058baf3ad72eb8c1e19b0d083d0431c1b822dc25/execution5ba2503ee883649e201411182d7dfda1739254862db228d8448dd66c0a209ae3. Image inspectee b15e40. Source-final log soumis1 existe ; absence fichiertrace expliquee par observer qui ne publie qu'apres124 traitements finaux, tandis que preuve termine vers49. Ce n'est pas une preuve de panne de la garde. Source124/exposition continue encore non qualifies par V18.

Correction V19 limitee a temporal_aux.rs : avant capture/exit, si FORGE_TIDEWATER_SOURCE_FINAL_TRACE est explicitement configure, attend le vrai fichier GPU completed>=124, actual_gpu_copies=true et source_exposure_injected=false, sous la meme borne180sec. Ne retire pas le seuil124 de l'observateur, ne force pas un nombre de frames, ne fournit aucune exposition au rendu. SourceFinal guard V18 inchange. Hashauxd23481b7fb03cd8f95954c96fc6d85ddf74b3edbf3d87b53ce6eaae0b2e4833f. CheckCompleteFinalTrace session67171 terminal0 9dbf11 :1933 sources/SDK intacts, vraie engineV17 reutiliseeSHA89144b54..., metadata log3727df162754592c0c0e95ba400a2129113fbb1746d52e9405854c98c36a46a2. Diff-check51e0f4 sans sortie, code1 du batch seulement parce que Get-Process20484 confirme absent. Link78294 terminal0 474483 : sources/SDK intacts, log0e3d563c0e2f86ab04f518edd40129ebab6fb70c632f094f3b729e36ce2ce9c7, vrai binaire280551082f15232ccfa9d4d733c4f9f7a5976896b9bf3f566464044b81f39570.

Sonde V19-v1 session64827/PID47768 lance apres V18 terminal, puis88182f terminal (natif0/helper1 beautychecks conserves). Imagec400de4f117d3a87eabda02f63c5c679bf7e066733c1de48f354b636d1b16f96. AuditSourceHdrFinalProbe et nouveau AuditCompleteFinalTrace20261008.py d1d3e6 passent :130 commitsresolve,33 plans, zero unsupported/pending, completecoverage/resetseed, 128 traitements finauxGPU observes. Trace48d0b04a1a16de03f411e25b2489b0053b906c7352a3949ef92c37f2d1434764, continuity before(n)=after(n-1) bit exact, initialexposure1, controle RCAS.45 bitexact, tous uniforms/markers finis/coherents, submitted1/124 logs verifies. HDR auditSHA4d1f9bb88d9870617f76bc82b2a9722a104c202268ec3ef324e9d613463eca71. Execution97c58c97df0b1b7e3d681c35bd619488067bdf5b907814329afcca3cd33b3201/proofaa29267fbde263bd9e00575258ec9ba6f681d5ce732730cdda4e1c1c64985369.

Native exposure avantimage124=1.792187213897705, dt128 cumule7.560813199728727s ; source acquise124frames2.0666667s/exposureavant124=1.752136468887329. Histoires/entrees encore differentes, camera deplacee/reset dans cette preuve. Ne pas comparer ce screenshot comme paire statique ni attribuer traceasynchrone a son frame exact. visual_parity=false/source_frame_history_paired=false/performance nonqualifie conserve. Prochaine tranche : acquisition visuelle a camera/histoires/clocks appairees et comparaison des entrees HDR/sourcefinal, sans retoucher arbitrairement l'exposition. Defaults produit inchanges. Tous les jobs propres de ce tour termines67171/78294/64827/26238 ; aucun relancement de ces sessions. Objectif actif, aucune publication ni nouvelle qualification de gate.

### V20 : horloge finale acquise rejouable, code/build/CPU PASS ; GPU refuse avant lancement

Tour precedent classe progress : V19 chaine HDR +128 final GPU observes, tous jobs terminal. Nouvelle correction vers la comparaison fidele : source_final_post/clock.rs charge seulement dt/night mesures par ordinal, opt-in FORGE_TIDEWATER_SOURCE_FINAL_CLOCK + SHA256. Document strict deny_unknown_fields, lecture bornee256KiB, schema/version/provenanceSHA, sequencement1..N et valeursfinies dt(0,.25]/night[0,1] valides. Resource uniquement RenderApp. Les controls dt/night sont copies pour la vraie soumission finale apres garde same-tag complete et pipelinesready ; les autres valeurs/exposurebuffers restent natives. Au-dela des124pas acquis, repete explicitement le dernierpas pour terminer les callbacks de preuve ; ces images ne sont pas pretendues acquises/appairees. Lane ordinaire sans ces variables conserve son dt reel. Aucun slug/nombre d'asset utilise comme regle moteur.

PrepareMeasuredFinalClock20261008.py60a61b extrait uniquement ordinal/dt/night de la source GPU exposure-trace SHA4499498763c4dc61e452d047e59bbb72418263212cd8665cb009fa0dc8bf01a4 verifiee,124pas, pas de valeurs d'exposition copiees. Manifest clock.json SHAa7eb95b09b5c401c186009dc1e1004c41c8c1739cf8db6b3d2f151715728adc9, historique source intact. SnapshotV20 derive deV19 par source_final_post.rs aee5c5bcca0fc3c03350a0e396c6ff4705f36084e0be6cc3afe0e574d835967d et nouveauclock.rs1d802ebfac4c0ec434d01e159bdbbd1d59cc16cb4e1465bae46148ec8d3bc059 seulement.

Check96549 terminal0 1c8009 :1934 fichiers/source/SDK intacts, vraie engineV17 reutilisee89144b54..., metadata log1d6a3a312cfce15d446788d4911d1f2b2a209a7098f7ed90a1bf550cadceaa70. Link96237 terminal0 0576b4, log786884539394cdb953a5def8d6c465014ef0d43d6a72813521c94ed85085f4cd, binaire64e1a5966ccdfc701bdb3d8e439948ab25095110efdb2005c8510a0faf49df0e/133400576octets, result9251b1c3ec356f80a49293ff4d2b9ae0a34f8dbcb9231f523757008f9f35b19c. Clippy40730 terminal0 6a1df8, source/SDK intacts, diagnostic nouveaux modules vide, logdaace6a06683d78a6f18ef2deb529a38e59a331d8887a3325cc85f0d6d2a3380 ; warnings ailleurs conserves. Diff-check4f19d1 PASS.

ProbeMeasuredFinalClock20261008.py caseV20-v1 refuse2bd2c5 avant Popen : marge12305657856 <17179869184, runtime_executed=false, pas de runtimePID/GPUscreenshot. Meme refus garde16GiB que precedemment ; aucun processus utilisateur ferme et aucun seuil abaisse. Derniere mesure54133112269535232 confirme condition externe encore presente. Ce refus n'est pas une validation de la nouvelle horloge sur GPU.

VerifyMeasuredClockLoader20261008.py premiere tentative84072/9072f3 : harnessCPU refuse a la compilation car CARGO_MANIFEST_DIR absent pour deriveBevyResource ; ce n'est pas un echec de la compilation produit V20. Helper corrige uniquement pour envmanifest du snapshot verifie, nouveloutputv2 conservant v1. Session89665 terminal0 1c2220 : vrais loader/serde production, pas mock, trois tests acquispas1..124/bits et tail explicite, rejet exposure_before meme SHAcorrect, rejet ordinalmalforme/nightinvalide/hashincorrect. Sources/SDK intacts, GPU=false. Nouveau AuditMeasuredFinalClock20261008.py prepare pour exiger dt/night GPU bit-egaux aux124pas et declarer tail/histoiresdistinctes ; pas execute comme PASS faute lancementGPU. AuditCompleteFinalTrace et checksHDR historiques conserves.

Prochaine continuation : verifier margecommit avant nouveau caseV20-v2 avec meme binaire/clock ; si >=16GiB deux gardes justePopen, executer sonde puis auditsHDR/finaltrace/measuredclock. Ne pas relancer le case refuseV20-v1 ni inventer une sessionlive. Les horlogesTAA/scene etcamera restent differentes ; scene/source pixel equivalence toujours non acquise. Tous les jobs propres de ce tour terminaux96549/96237/40730/84072/89665 et tentative2bd2c5. Objectif actif, defaults produit inchanges, aucune publication ni nouvelle qualification de gate.

### Acquisition HDR source preparee pendant la contrainte memoire V20

Tour precedent progress : mode clockV20 construit/Clippy/testsCPU PASS, tentativeGPU refusee. Mesure initiale38bce9 : marge7986290688, sous16GiB ; pas de relance immediate. Travaux independants : inspection des vrais source PostFX/TemporalUpscale/Texture/Readback, lecture78122 observee en cours puis terminal6f3095 sans relance. Source PostFX SHA9e81a6de89f37808c51c5f939f4ed74fdccc65f0427733ce1f6dd6be4f454528 reconfirme3e79b9. La sortie taau.texture pointe bien vers history[_cur], index bascule dans render, pas endFrame. Les RenderTargets ont COPY_SRC par defaut. Aucun changement des equations source.

PrepareSourceHdrPlanes20261008.py9670c8 termine0 : nouveaux outils source-hdr-acquisition-tools-20261008-v1, syntaxchecks Node overlay et driver PASS, runner Python AST PASS. Hook observational PostFX.render memorise au vraiGPUframe124 cinq textures : scene-hdr, beauty avantTAA, resolved, bloom-meter niveau1/16, bloom. Lecture asynchrone apres les124frames via Readback.readTexture source, formatrgba16float/COPY_SRC exacts, longueur/transport/roles/dimensions/clocks before-after verifies, SHA des bytes recus persistes. Pas de wait ajoute dans un frame ni simulation supplementaire. Driver exporte chaque plan separement ; chunksbase64 multiples3 evite padding interne. Runner exporte archivegit sourcecommit1438b1ab intact dans nouveaucase, preserves provenance/overlays source historiques et ajoute deux guard16GiB avant navigateur/serveur. Ni navigateur ni GPU source lances ce tour ; ces outils ne sont qu'une preparation, pas une qualification des textures. Receipt hashes overlay4d51a685906b040fdec0c5791602f5f73eab36ed6af136d65648b1b71c340018/driveredd22fff27cce1cd31ee3ddf446e8a996de2aa0d33bd5de93ecd1f3c8ab618da/runner1f86c95e754797adfd37feaff5f917d38093541a6a6d29412055f81bf0d15291. Originalsource/acquisitions intacts.

Inspection native/source tailles bloom2e982b : native utilise deja arrondissement JS positif ((size+divisor/2)/divisor), sourceMath.round ; aucun correctif de taille invente. A cette mesure marge17668476928 >17179869184, nouvelle tentative V20-v2 meme binaire/clock/parametres et guard reel. e87835 termine refuse avant Popen : marge16551464960 <17179869184, runtime_executed=false, pas de PID/GPUscreenshot. Conserve V20-v1 refuse et V20-v2 refuse separement. Pas d'abaissement de seuil, pas de suppression/processus utilisateur ferme.

Prochaine action : nouvelle mesure stable et lancement natif V20 dans un case neuf (pas reutiliser v1/v2), puis auditsHDR/finaltrace/measuredclock. AcquisitionsourceHDR seulement quand guard satisfait, puis audit des vrais pixels/buffers/clocks de la source et comparaison avec une camera native coherente ; ne pas utiliser le screenshot camera-moved du probe comme paire source. Les historiques de scene/TAA restent distincts. Tous jobs propres de ce tour terminaux, aucun handle live a poller. Contrainte GPU memoire encore presente mais travaux de comparaison avancent ; objectif actif, defaults inchanges, aucune publication ni gate cloturee.

### V20 GPU et acquisition HDR source : nouvelles preuves du 8 octobre

Le dernier tour de statut n'a pas modifie le produit : classe no progress, objectif confirme active. Revalidation et reprise executable dans ce tour. Resultats du tour GPU precedent conserves ici : V20-v3 session87337/PID41140 terminal (25ca09), natif0/helper1 car les checks beauty historiques exigent leurs propres captures et ne sont pas affaiblis pour la lane moved/reset. Deux guards effectifs19459596288/19442761728 >=17179869184. Binaire64e1a5966ccdfc701bdb3d8e439948ab25095110efdb2005c8510a0faf49df0e. Audits HDR/finaltrace/measuredclock510279 PASS :33 plansGPU,130 commitsresolve,128 traitements finauxGPU, couverturecomplete/resetseed, zero unsupported/pending/erreur. Proof9380f1178eb48c38a57ee9b2a501f8eae75587bf67df2a601a4fe1c8d2e55ccb, trace64705a4623ec57f7c81dc4d0a766644f1fcb8fc46a35ca33d161f8e3f9798c05. Les124 pas dt/night GPU sont bit-egaux aux124 pas source mesures ; quatre pas tail explicites. Native exposure avantimage124=1.754796028137207 contre source1.752136468887329, ecart environ0.15%, sans injection d'exposition. Scene/TAA/camera histories toujours distincts ; visual_parity=false. Processus confirme absent546a98, pas de relance.

AuditNativeHdrColors FAIL : un pixel RGB NaN dans beauty camera-moved, alpha1, depth0.0005357855698093772, mask[0,0,0,0], pixel913/341. Reproduit V19 etV20, sortie resolve finie. Les audits d'encodage/completion precedents ne prouvent pas la finitude de chaque couleur. DiagnoseNativeHdrNonFinite et LocateHdrInvalidPixels conservent cette anomalie, non corrigee : position monde36.66686376982475/16.03628359309976/-204.55049796503437. PacketSHA394e4fcfdbc9119aa020052d49c46a45c1e8a3976e25b1e905cb65af05ac74ca. Nouveau InspectHdrInvalidNeighborhood20261008.py, execute05fe69 terminal0 : archive workspace et tous blocs canoniques verifies parSHA,65383instances ; voisinage depth/RGBA persiste. Origine la plus proche : young_palms source_key cb6d237b581ef16443843ebc5be3d8f3c6fc126c9d460526197cdb8b9bce87f5, distancehorizontale0.1855252822m. Point0.2750314056m au-dessus de hauteur bilineaire source. Ceci identifie une piste seulement : geometrie rasterisee/materiau/pass producteurs NON identifies, morphLOD/wind/alpha pertinents. Aucun clamp ni remplacement des NaN introduit.

PrepareSourceHdrPlanesV2 ajoute deux mesures16GiB juste avant Popen Chrome. Node overlay/driver et AST Python PASS c34f03. SourceHDRcasev2 refuse3c4c80 avant serveur/browser a15557017600 <17179869184, aucunGPU. Ce case refuse reste intact. Dans le present tour, mesures11840249856 puis13992779776 sousseuil ont conduit au diagnosticCPU independant, pas a une baisse de garde. Mesure20463656960 permet nouvel AcquireSourceHdrV3_20261008.py : hash runnerV2c65365a540fb7f1d9ebb6330ed1310e02d7204cd87318ce052c393d374a777a8 verifie ; seule racinecase remplacee v2->v3, memes overlays/drivers/source. Session22932, serveurPID8184/browser17736, guards prelaunch19919450112/20386660352 et prebrowser19970347008/19975987200. Terminal1e4acb exit0/failureNone, sources/archive/script/validator intacts, processus propres absents8b7985. Aucun processus utilisateur termine.

AuditSourceHdrPlanes20261008.py6af307 terminal0 :428 fichiers source verifies, cinq vrais plansRGBA16F aGPUframe124, clocks before/after inchanges, pas de frame ni wait intraframe ajoute, aucune valeur nonfinie. Scene-hdr/beauty/resolved1920x1080, bloom-meter120x68, bloom960x540. AuditSHAd5cc929be2573601acce5de80bc9106ae5e45e9659216302eb8f01d4e5ecc066 ; receipt818e37559cb1b9185829a656bbdf6f2b2bfa0a98c05c7821fc6fd2719bb7a087. Source beautySHAb9929b76e35554a1a745f83667bf725910a1c1cc4b1326aafe4447ddd1270326, resolvedbbab7c9420f3bac4a62e9fe0bc9129708705b9a38f0d608faadd2c715ded1992. Cette acquisitionGPU reussie leve la contrainte de memoire pour ce lancement uniquement, pas une garantie future.

Nouveau CompareHdrStagesDiagnostic20261008.py53a141 terminal0 : sourceHDRv3 versus V20 snapshotstatic. Origines camera agree <1e-4m (42/14/-90), paquets/bytes verifies, couleursstatiques toutesfinies. Source frame124 versus native41 traitementsresolve precedents ; orientation/jitter et histories non appaires. Moyennes luminancelineaire beauty source0.1845635921/native0.1842400283 ; resolvedsource0.1831782609/native0.1829043627. MAERGB diagnostiques0.012678789/0.009097072, jamais qualifies comme metrique d'acceptation pour histoiresdistinctes. Preview stages.png SHA74de174779182ae8abc5ce3c9e3e7ce39deba56bd6f7d88f540672df9a878c50 ouverte et inspectee : composition proche, teinteforet native plusbleutee ; cause non determinee. Meme courbe diagnosticACES/sRGB exposure1 pour quatrepanneaux, pas rendu finalsource. Artifacts sous native-source-hdr-final-probe-village-20261008-v20-v3/source-hdr-stage-diagnostic.

Suite : capture native statique au vrai124e traitement avec camera/jitter/histoire correspondants et comparaison des stages ; isoler l'origine du pixel nonfini par sorties de passes/intersections reelles, pas par nom d'asset. Tous jobs propres terminaux, aucun handlelive. Objectifcomplet resteactif, defaultsproduit inchanges, aucune publication ni nouvelle qualification de gate.

### Capture native HDR au vrai traitement final124, camera conservee statique

Tour precedent progress : cinq plans sourceHDR acquis et comparaison diagnostique. Ce tour ajoute uniquement une sonde opt-in FORGE_TIDEWATER_FINAL_HDR_CAPTURE : source_final_post/capture.rs installe un ShaderBuffer de lecture, copie les vrais controls64 et trois textures juste apres les cinq down/four up, avant final grading/exposure update, seulement quand history.submitted==124. Entree finaleHDR1920x1080, bloom-meter120x68, bloom960x540, RGBA16float/COPY_SRC verifies. Rows GPU alignes256 puis padding retire au callback. Le buffer est gele, lecture armee apres encoding puis validation du marqueur GPU copie depuis uniform.frame.x==124. Les couleurs/expositions source ne sont jamais des entrees. Ajout COPY_SRC aux textures bloom seulement dans cette lane. temporal_aux garde la camera statique jusqu'au callback valide124, avant ses demandes static/moved/reset. Sans opt-in, comportement ordinaire conserve. Attention : role resolved nomme l'entree native du final, pouvant inclure ses passes postprocess ; ce n'est pas une preuve d'identite avec la sortie TAA source.

SnapshotV21-v1 deriveV20 par trois fichiers : source_final_post c4ec3de38b07d394eeb565cdff3f20f0053bee0619dd4a9c5c7916a9a4fd4477, nouveaucapture6ac081593e51779641c8789e34d1d3aa696249537a2df8f69a506bd4634ebf4e, temporal_aux df996ea61694ce6e09d5828bfdbdbc541026a777aecc6f5f142ac74e21a69c10.1935 fichiers/source/SDK intacts. Check10956 terminal59e5e8 exit0/logb78df8e6c6d241519b3a32ead73e4193ad40502705908fafb8adf11a18c1fa7b. Link15755 terminalc31675 exit0/log492d730dfa330607b2fde151c2cf2bdc73a4758293ad57d695c201ea81154497, vraie engineV17 reuse89144b54..., binaire293e5fc141bad413406aa55745c195c3b6ad6a1024611c64d7ddd47630446174. Première tentativehelper43571 terminale6e149 : argument graphics errone fourni comme float au lieu d'objetJSON, TypeError avant Popen ; caseV21-v1 garde avec preparation-failure.json/runtime_executed=false. Pas un echec produit, pas de processus a relancer. CaseV21-v2 distinct avec vrais overrides sharpen.45/fxaa0.

V21-v2 session5806/PID10096 lance apres deux guards18966224896/18960302080 >=17179869184. Termine e0bc08 : natif0/helper1 beautychecks historiques conserves ; processus absent33af15, capturecontext8e8c422375441f2597306e047596e11a36f45c5499d0e0ca591d087c68ea45bf, toutes pins runtime/source/profile/reference intacts. AuditFinalHdrCapture20261008.py7a1437 PASS : trois plansGPU finis, bytecounts/SHA/roles/dimensions exacts, controls64SHA343154de9f105062c678542098621f2ea2432c6ff072c91531bc8fd193ccd52d et leurs12 motsf32 bit-egaux a la vraie traceGPU row124. ReceiptSHA c1e906e4273b4ea2f84f90e1c5f3be7f4bed3776475325dc0cdcada9525e8053. FinalinputSHA414a6b45ae5547dad584750bd4e6e792a6032ab9f1431710eb85cff3a288fa1c, meter6bf8b6de429e1a86780386d5c7e3e0f2ab267c5c49a215f7c3cff584781d12ba, bloom7be7ea2823c470b391941aa0348b8dedc75c4e4a9ebf3441d3f809ed827995b2. Native/source luminance moyennes : finalinput.1828815788/.1831782609, meter.1822404414/.1825353056, bloom.9106038213/.9120831490 (~0.16% chacune). Histoires/jitter/geometrie animee non appaires ; MAE diagnostiques.008593751/.004972570/.025582299 seulement.

AuditsHDR/finaltrace/measuredclock ab6dc5 PASS :33 plansresolve,136 commitsresolve,coveragecomplete/resetseed,134 finalGPU, exposurecontinuity bitexact,124dt/night bit-egaux,10 tail explicites. Proofb0e3d082564a5dcc3ac7132648e5f12c1695f79afa2377b6387170e60c18cbcb, trace8b173eb45e91839d6005538088b0d35e492bbe77744ae71d877f7cc8ffe0bfe5. Exposureavant124=1.7695950269699097 versus source1.752136468887329 (~1%). Ce resultat corrige la comparaison0.15% precedente : V20 avait bouge/reset la camera avant124 ; V21 maintient la camera statique jusque124. Les deux resultats historiques sont conserves, ne pas reutiliser0.15% comme bilan statique. DiagnosticNonFinite ab6dc5 : neuf plans static/moved/reset finis dans cette nouvelle execution ; cause du NaN V19/V20 toujours inconnue, pas declare corrige car le mouvement survient a un autre moment/phase.

Preview GPU124 native/source realisee9ea026/27d20c, comparaison.png SHA5041180d946b8e5a38bb1344ba9e0a66f1261523e2be9d24eeeb024c732d6c02 ouverte/inspectee55c542. Meme affichage diagnosticACES/sRGB exposure1, pas finalpass complet. Composition proche, foret native plusbleue. MoyennesRGB finalinputsource[.21260545,.18192296,.10895948] native[.20969051,.18199289,.11275958] : ecart chromatique avec vertstable, pas uniquement expositionglobale. Ne pas compenser par reglage de grading arbitraire pour masquer l'origine. Next : relever paquets camera/jitter exacts et sorties avant/apres HDR passes, puis apparier histoires scene/TAA.

ClippyV21 session27198 termine e3fc7d code0 mais deux nouveaux used_underscore_binding sur Target._texture desormais lu. Correction de nom seulement _texture->texture, snapshotV22-v1 derive du seul source_final_post SHA138cd45322d4e63f5da3bb8040998d296436112e828509cbddfb2ec336f15a63. Check56365 terminal03fe8f,1935pinsintacts/log3be5d6e167ed44db38c63c5c8dd0defaddc9c35cb70dfb3771ae8c2c55bc8aff. Clippy56692 terminal2f176a : modules sourcefinal sansdiagnostic, autres warnings conserves, log72b9eb650f5f58bad1fba92505cc1a87f55e389b9e89870ac10f42450d1c9e32. Link42251 terminalf4b911, sources/SDKintacts/log209c9356f05d461d19ea37082e507e8df8307586d2ad58f96d49584b80a6e646. V22 nonexecuteGPU ; la preuveGPU estV21. Rustfmt check detecte ensuite compression Target sur une ligne, format appliquee sans changement de comportement ; canonical sourcefinalSHA4e17ed913cb49f83ee917f1df57e1f3ca0627de3b89cc3aa63864ccde7a54a49, rustfmtcheck/diffcheck PASS e66caf. Snapshots construits gardes intacts, pas de qualification retroactive du source reformate.

Tous jobs propres du tour terminaux10956/15755/43571/5806/27198/8623/56365/56692/42251. Objectif complet ACTIVE, aucune publication, aucune cloture de gate ni qualification Studio/gameplay/portable/performance par ces seules captures.

### V23 : sonde scene HDR et ecart de projection prouve, GPU non lance

Tour precedent progress : capture final124GPU V21 qualifiee dans son perimetre et liaison V22 corrigeant les noms. Ce tour ajoute le plan scene-hdr avant GTAO au meme buffer gele que final124. Systeme Core3d apres MainPass/avant SourceHdrSystems::Gtao, seulement si opt-in et history.submitted==123 ; tag selectedViewPacket memorise et compare exactement au tag du final124 avant validation. Une tentative finale sans copie scene de meme tag echoue explicitement. Enregistre aussi packet336, tag serial/view et controls64. Packet est un upload diagnostique du ViewPacket selectionne, PAS une copie depuis son buffer lie ; cette limite est dans le receipt. Schema captureversion2/quatre plans. SourcefinalSHA3460abc524345b3f6e568f4868c56c4f3693d4323a06010e1b137601b6f04e6c, captureSHAab45b2a694170f693b1470fe19a2682d55d07d0aac1f3f1f2b34540f8aa36804. Defaults produit inchanges, aucune nouvelle image GPU declaree acquise.

Premier wrapperCheckSceneHdrCaptureV23-v1 a remplace aussi le needle de sa liste de fichiers, omettant capture.rs du snapshot. Session82151 terminal632c7b exit1, erreurs fonctionbefore_hdr absente/E0061 six arguments contresept (da5e83), sources/SDK intacts, log4b93d609a218743063f59403f5b672686b0e863db03140e1be81d3dcdd1445a5. Echec du montage du snapshot, conserve ; aucune fonction retiree ni check affaibli pour masquer ce resultat. WrapperV2 repart du template mesured-clock et remplace seulement la liste destination, avec assertcount1 ; les deux fichiers sont effectivement stages. Check18245 terminal33f3d9 exit0,1935 sources/SDK intacts, engineV17 reutilisee89144b54..., logd67ad0f19fffa94b1a1c3a27c4a89f1ed873eefae8cf17396967e39c082bade3. Link8783 terminalbd3b73 exit0/log94668c82ddec5151bd18196e403aa95b769ddae4695d3ffb64f99ef103ea1447. Clippy17025 terminalf00ee0 exit0, nouveaux modules sansdiagnostic, warnings externes conserves/log12435506ce4042d2d8c080edfa3aded749d140522c70e83e8d9183312c93ff56. Rustfmt/diffcheck cibles passes, aucun nettoyage de travail concurrent.

Sonde nativeV23-v2 session16865 terminal15cbc9 : refusee avantPopen, runtime_executed=false, guard12475265024 <17179869184 (receipt e07671). AucunPID lance, aucun plan nouveau. Mesures suivantes12343689216 puis11978625024 confirment contrainte. Case refuse conserve et ne sera pas reutilise. Aucun processus utilisateur ferme, seuil16GiB intact.

Inspection source AirHaze.js et implementation native : source applique attenuation du fog par fSun/near, marche volumetrique16pas ombres cascades/terrain/clouds, upsample depth-aware lit/all, deficit et rayonsradiaux ecran. Native tidewater_air_haze.wgsl realise seulement aerial-perspective/marine-haze avec horizonLUT ; ces termes volumetriques source restent a porter/qualifier. C'est un ecart de comportement source, pas une preuve que toute la teinte vient de la brume. Pas d'ajustement couleur arbitraire applique.

TravailCPU independant du guard : les124 rows de source exposure-trace contiennent deja global_gpu_params, pas besoin d'inventer une phase source depuis un numeroRAF. AuditCapturedCameraProjection20261008.py f4c91c terminal0 verifie Frame.js contre les pins du sourceHDRv3, schema720 octets/positions/trace124 et nativepacket336SHA, puis compare les matrices unjittered. Source observee124 near0.10000000149011612/far60000, nativeV21 snapshotstatic near0.05999999865889549/far60000. Origines identiques42/14/-90 ; projection non appairee. Source matrice proj m22=0.0000016666693909428432 et m32=0.10000016540288925 (reverse-Z fini60000) ; native infinite-reverse utilise planproche.06 et n'atteint pas zero a60000m. Exemples projetes : point100m sourcez.0009983349164/native.0005999999544 ; point60000m sourcez environ0/native.00000099999994 ; point.08m sourcez1.24998 horsplanproche/native.749985 dansfrustum. Positions/FOV proches ne prouvaient donc pas projection equivalente. Rapport camera-projection-diagnostic.json sous caseV21-v2. Contribution de cet ecart a la dominantebleue NON etablie.

Point d'architecture verifie sur API Bevy0.19.1 installee : CustomProjection existe, mais adapters temporel/underwater/haze attendent actuellement Projection::Perspective. Remplacer simplement par Custom sans raccorder ces consommateurs supprimerait des effets et serait incorrect. Frame::prepare_main_view lit Camera.clip_from_view avec near/far dePerspective, tandis que GTAO lit projection.get_clip_from_view. L'alignement doit fournir la meme projection effective a ces consommateurs et a la reconstruction de profondeur, pas seulement changer une constante near. Aucun faux alignement partiel marque comme PASS.

Suite concrete : reprise GPU V23 dans nouveaucase quand marge suffisante, audit scene/final124 et camera/jitter ; correction coherente de la projection finie et port complet des termes AirHaze identifies. Tous jobs propres terminaux82151/18245/8783/17025/16865/55172, aucun GPUlive. Objectif complet ACTIVE, aucune publication ni qualification globale/gate.

### V24 : consommateurs de profondeur raccordes a la camera effective

Le dernier tour utilisateur etait une demande de statut, sans modification : no progress pour l'objectif, pas un blocage. Cette continuation apporte une modification produit ciblee. GTAO utilise maintenant Camera.clip_from_view(), comme Frame::prepare_main_view, au lieu de Projection.get_clip_from_view(). GTAO, AirHaze et Underwater attendent explicitement CameraUpdateSystems dans PostUpdate. Haze/Underwater lisent les echelles et les coefficients de profondeur de la matrice effective. Reconstruction reverse-Z : distance axiale = B/(depth+A), avec B=clip.w_axis.z et A=clip.z_axis.z. L'ancien near/depth reste le cas infini A=0, tandis que le terme fini est maintenant supporte. Haze conserve son ABI en utilisant control.w auparavant inutilise ; Underwater ajoute depth_projection Vec4 dans les deux definitions Rust/WGSL, avec default B=.1/A=0. La branche de ciel et les bornes de distance ne changent pas. Les rayons caustiques sous-marins reutilisent view_z et suivent donc le meme calcul. Aucun changement des plans proche/lointain ni activation de projection finie ce tour : coherence des consommateurs preparee, alignement complet encore NON acquis.

CheckEffectiveDepthConsumers20261008.py session6810 terminal dcac78 exit0 : snapshotV24-v1 derive de V23-v2 par ces cinq seuls fichiers, 1935 sources et SDK fermes verifies avant/apres, vraie compilation engine ET exemple, logs930731da8ebde4beacb055eb0c29e07b3bb716305a9709b9a79fdc22d9bbfbe6 et33a8976f0ef15b5dc83893af73615262bee7f419e2819946750f8525aa8241e2. Aucune ancienne engine reutilisee pour ces changements moteur. Rustfmt cible sans diagnostic ; diff-check des cinq fichiers PASS ba4fc0. Le controle global610858 a revele des espaces/CRLF dans gameplay_navigation.rs preexistant et hors perimetre ; ne pas annoncer le depot entier propre ni modifier ces travaux concurrents.

VerifyEffectiveDepthConsumers20261008.py : premiere validation CPU v24-v1 echoue sur UNDERWATER_PI absent dans le montage de fixture shaft (1795a6), premiere variante underwater deja validee. Le montage v24-v2 reprend le renommage PI effectue par la composition/test produit, sans modifier les equations ni masquer un diagnostic produit. f5936e exit0 : Naga parse/validate des quatre variantes underwater, underwater-shafts, haze-HDR et haze-display ; ABI WGSL underwater192octets/depth_projection offset96/absorption112 et haze160octets verifies. Interface optique des shafts fixture explicite, pas une execution du materiau marin complet. Douze profondeurs finies/infinites sont comparees a une inversion independante des matrices, tolerance relative1e-9, depuis les matrices/provenances acquises V21/sourceHDRv3. Source trace et uniform SHA verifies contre diagnostic camera. Shaders/pins intacts ; ni GPU ni parite visuelle revendiques.

LintEffectiveDepthConsumers20261008.py session13227 terminal3a1b96 exit0 : vraie engineV24/SDK fermes verifies, aucun diagnostic dans les trois consommateurs modifies. Warnings preexistants ailleurs conserves, project_warning_free=false, log77b2abf7eeec49d1b6a050806f4278aa8464124aa2fc869bf79a63201bfc63f4. BuildEffectiveDepthConsumers20261008.py session78174 confirme engine construite puis liaison de l'exemple (1e5d96) ; conserver ce meme handle jusqu'a verdict terminal. Aucun lancement GPU, aucune publication, aucun changement de gate. Suite : projection finie generique et ordonnee avant tous les consommateurs/frustum/extraction, tests de schedule et actualisation de camera pres de .1/far60000 seulement pour le fixture acquis ; ensuite comparaison GPU scene/final a conditions appairees. La brume volumetrique source et la validation complete Studio/gameplay/portable restent requises. Objectif ACTIVE.

Poll du meme build78174 termine7531f5 exit0 : vraie engine et exemple lies, sources1935/SDK intacts, engine_library SHA5f2af669a9ecdf6e8f13330bdbf9bc17489f4cbb67c51cd394f0276e736d1e6a, logs325609b535248338d28e2678070d5efac5ddf3251c39f7121f3a32a88dee1dd2 et027b1d323950967aab1ab23847431d7cb7e0d2aea920d3ae53b10a6312dc9be4. Retention sous source-final-ordered-linked-20261008-v24-v1. Tous jobs propres6810/13227/78174 maintenant terminaux ; GPU=false, pas de nouveau rendu qualifie.

### V25 : projection finie generique, frustum reverse-Z et cycle de camera

Tour precedent progress : consommateurs V24 compiles/lies/tests CPU, tous jobs terminaux. Nouveau module generique finite_perspective.rs : FinitePerspectiveDepth opt-in, FinitePerspectivePlugin et EffectivePerspectiveSystems. PerspectiveProjection conserve near/far/fov canoniques ; la matrice effective est derivee a chaque PostUpdate en conservant les offsets de sous-camera. Les deux coefficients reverse-Z finis sont calcules en f64 puis convertis vers f32, sans seconde source mutable. Plages invalides retiennent la projection standard ; Applied derive suit le retrait du marqueur et restaure la matrice meme sans nouvelle mise a jour automatique de camera. Systeme apres CameraUpdateSystems/TransformPropagate/UpdateFrusta et avant CheckVisibility ; temporal/GTAO/haze/underwater explicitement apres ce set, lens-flare suit underwater et lit maintenant Camera.clip_from_view. Refraction synchronise deja apres visibility/temporal et copie la matrice effective ; ne pas confondre ses tests Projection.get_clip avec le chemin rendu. Le plugin est installe dans le probe ; seuls ses cameras statiques avec camera_pose acquis recoivent le marqueur et near=.1/far60000. Cameras ordinaires non marquees et cameras de marche non modifiees. Ce raccordement n'est pas encore une qualification du produit Studio ou du jeu portable.

Premier harnessCPU V25-v1 session20099 terminal70dc38 exit1 : trois appels tests a PerspectiveProjection.get_clip_from_view manquaient l'import trait CameraProjection ; source/SDK intacts. Import corrige, fixture de camera-update rendue one-shot pour que le test de retrait ne puisse passer grace a un overwrite automatique ; ajout d'une modification de near/aspect sans reset de camera. V25-v2 session67120 terminaldfeeb8 compile0/tests101 : invalid ranges/offsets PASS, mais source m22 exact differe d'un ULP et test du plan proche revele une erreur de frustum. Aucun echec efface. Verification primaire du helper Bevy0.19.1 ViewFrustum : from_clip_from_world_custom_far reprend un near row3+row2, convention -w<=z ; le reverse-Z WebGPU proche est z<=w. Correction produit limitee a l'option finie : near HalfSpace(row3-row2), far explicite inchange. Source Matrix4.js calcule avec near decimal0.1 f64 ; Bevy PerspectiveProjection stocke deja near arrondi f32. Test declare un ecart maximum d'un ULP pour les coefficients, au lieu de pretendre bit-parity. Ce n'est pas une preuve de camera/histoires totalement identiques.

VerifyFinitePerspective20261008.py V25-v3 session92532 terminalf91871 exit0 : vrais module/schedule ECS production, vrai update_frusta Bevy et camera-update fixture CPU explicite. Trois tests PASS : mapping proche/loin et points horsrange, plages invalides et conservation des offsets angulaires, frustum/remplacement near-aspect/retrait sans mise a jour automatique. Source SHA b1f9367d4d6b4639e48666dd564eaa166f1e28c438e616b6fefb8bca82041831 et SDK intacts. Pas de GPU ni d'extraction rendu dans ces tests.

CheckFinitePerspective V25-v1 session78759 terminalae2511 metadata engine/exemple PASS ; V25-v2 session51428 terminal691dda PASS ; snapshots historiques intacts avec leurs limites tests. CheckFinitePerspectiveV3 session43725 terminalf6c92e : vraie engine et exemple compiles,1936 sources/SDK verifies avant/apres, logs db69ca024e1dbf7bc9287deddf59cd99a4b53570b638e23c2d04c275aac7d8b4 etfc8ce2ea776dd24e1a389902b7ccfdd4163541090c946ca05fe22b00a6f607b1. Rustfmt et diff-check cibles sans diagnostic. LintFinitePerspective session44508 terminalc8272b exit0, modules modifies sans diagnostic, warnings preexistants hors perimetre conserves/project_warning_free=false, log4a874dff0de5091b34cc240b847aa7e4e14b4c708e459c818912412d862d5800.

BuildFinitePerspective session82411 confirme en cours au poll ef4652 ; conserver ce handle, pas de duplication du job. Mesure memoir7977ab : marge18641444864 au-dessus de17179869184, mesure informative seulement. Aucun lancement GPU avant binary/retention terminal et deux gardes immediates du helper. Suite : meme nouvelle capture scene/final124 avec projection finie, audits de plans/trace/horloge/camera puis comparaison ; brume volumetrique et histoires encore incompletes. Objectif ACTIVE, aucune publication ni qualification de gate.

Build82411 terminalf91c14 exit0 :1936 sources/SDK intacts, nouvelle engine SHA b3002c578611c0e7585a294f118df9acbb86e3cf3fdbcd5fb815f897bed7240b, logs86cdc39645095726ce155501f3e4956c7513b151dbc6304c5acb1d2ccb1679e0 et8f1f64d332715eeb2858fc898bfde659451ce57f76da36628a9ad53484a3dcfa. Retention V25-v3/binaire0368778a7d76d92619de268cb5411d67a2a2e9a9d56b49d17950e0ba39257412/133498368octets (96f854).

Premiere invocation ProbeFinitePerspective c79562 exit1 AVANT creation du case et Popen : guard historique source-attribution ne connaissait que static_pose/compile_corrections/acquired_ocean, et refusait le nouveau hash du probe. Ni GPU ni PID ; pas de dossier preexistant remplace. Helper derive local uniquement, historique inchange : ajoute finite_perspective_patch a cette chaine en exigeant que chaque predecesseur connu corresponde exactement a before_sha256 avant de suivre after_sha256 ; tous pins source/retention existants et seuil16GiB conserves. Invocation corrigee session31157/PID8240, case native-source-hdr-final-probe-village-20261008-v25-v3 ; guards19309666304 et19330027520, runtime_executed=true (96f854), meme handle conserve. cd76e3 confirme PID8240 tidewater_plant_probe vivant ; dernier poll98d56c encore en cours, log workspace initialise. Ne pas pretendre captures/packet/histoire deja passes. Prochaine continuation : poll31157 et processus8240, audits seulement une fois les artefacts disponibles/terminal, jamais relancer sur un simple timeout. Objectif actif.

### V26 : echec GPU V25 conserve, guard resolve fini et diagnostic de preparation

Tour precedent progress : projection finie/frustum CPU et buildV25 passes, sonde31157/PID8240 effectivement lancee. Cette continuation a conserve le meme handle : polls a0812a/a86a72/452881 encore live, processus confirme aad88f/b4aac1. Avant progression de logs, compteurs Windows282a07 montraient1.63Go dedies/11%3D : pas de preuve suffisante de processus arrete, aucun relancement. Les logs finissent par exposer la vraie panne : HDR GTAO[1,1,1,1,1]/haze1/underwater1 observes,19264 dessins supportes, mais committed/resolved0 et MissingBinding a chaque tag (d4f603/c960e0). Sonde finit par sa borne180secondes, pas de timeout augmente. Poll31157 f23172 terminalexit1 ; preuve village.aux.json statusfailed, image absente, processus8240 absent5c7d58. Inputs/binaire/source/profile/reference restent intacts. Aucune qualification de camera GPU, sceneHDR/final124 ou parite par cette execution.

Inspection resolve.rs standard_perspective : seuls z_axis=(0,0,0,-1) et w_axis=(0,0,near,0) etaient acceptes, excluant explicitement la nouvelle projection finie. prepare() supprimait Targets/Prepared en Err sans publier la cause, et encode() la transformait en MissingBinding. Cette incompatibilite de code est prouvee ; V25 n'a pas capture le packet lie permettant de lui attribuer exclusivement l'echec. V26 partage standard_reverse_perspective dans le module generique : matrice perspective centree/non oblique/finie/positive, plages valides, puis coefficients EXACTS correspondant soit au mapping infini Bevy soit au mapping fini derive des near/far canoniques. Pas d'acceptation arbitraire de profondeur, jitter d'objectif decentre ou matrice oblique. Les autres guards camera, viewport, MSAA, HDR/owner/tag/generation, ressources/pipelines restent intacts. finite_matrix rejette maintenant aussi les plans proches obliques, conservant leur projection Bevy au lieu d'ecraser partiellement leur clipping. Le shader resolve reconstruct deja depuis l'inverse effective et compare dans le depth standard source : aucune equation shader remplacee pour contourner la panne.

PreparationFailure conserve la vraie Error ECS rendue sur la vue quand prepare echoue ; encode la republie si Targets/Prepared manquent, plutot qu'un MissingBinding generique. Success/retirement retirent ce composant derive avec la meme preparation atomique. Pas de source canonique supplementaire ni nouvelle representation GPU, aucun changement d'ABI Packet336. Module/source guard SHA avant/final traces par snapshots.

VerifyFiniteResolve session7269 terminal732b59 exit0, source f583fa84f70bec8659196c42eed5e3ea1d36b5e516a9a1ac9485c0628d7571c4/SDK intacts : quatre vrais tests CPU module production, dont acceptance infinie/finie et refus near/far inconsistants, profondeur arbitraire, offsets et clipping oblique. Trois tests camera/frustum/lifecycle V25 toujours passes. CheckFiniteResolve V26-v1 session83451 terminal9092e2 compile engine/exemple0,1936sources/SDK intacts, logs f542933129ece9f0de2141f75555526a80723cd4f69556bc49b61b1afb8db3a9 et7bb058dab72945612b436cef1524ac9339d0e4801e280cd1e2fbf09889fe6753. Build43981 terminal283459 exit0, library33773fac60014d53ac95e75851dd09a4a2dcef1849652cf4dcfe972043756f11/logs56bee8a9b6179650426eda86ad4c479be69650ba399540a89c7ba53933be5e37 etab4f519e6a76656241b0471335ec14882a3cf8b4e8f9ed2678a7923e6357ca73 ; ce V26-v1 n'a pas ete lanceGPU.

Lint45543 terminal0e8e0f exit0 mais nouveau float_cmp sur normal.z exact de finite_matrix. Ajout d'une justification locale pour l'egalite structurelle du normal (0,0,-1), pas de suppression de sa validation, seul changement V26-v2. CheckV2 session52372 terminal77762e engine/exemple0, sources1936/SDK intacts, finite sourceb4205e5c58d5e4d1f0bf21eb40b3a8f236a55514519525585911fcb6a61ccf13 etresolve7f0db192480e275180fe678918947066d2e323e2f849872c9fe1e4bb380e7b01, logs5f486474ff1a4bf848726add8a9b3feb0e82a652ea87337dd8ff19b51a0aeb62 et6300bf08e38726c5e13d030e9bd0dc5beb4b5ad23aa392d321b1b2bf524e86cc. Rustfmt check7f2780/diff-checkresolve ef81b7 sans diagnostic. LintV2 session75534 terminala81fc2 exit0/log48f60ed909e8db0b18f9d7b8dc0d3ba0157a0f07f662701c9e12e43e8e1bc994 ; controle complementaire3e1aa8 normalise les separators Windows dans TOUTES les diagnostics pour verifier finite_perspective ET tidewater_temporal/resolve : aucun diagnostic dans les deux, warnings hors perimetre conserves, projet entier pas annonce warning-free.

BuildFiniteResolveV2 session31550 encore live au poll1927b5. Conserver ce handle jusqu'au terminal, puis nouvelle sonde ProbeFiniteResolveV2_20261008.py dans case native-source-hdr-final-probe-village-20261008-v26-v2. Ce helper conserve V25 echoue et trace successivement finite_perspective_patch puis finite_resolve_patch en exigeant before/after ancestry ; deux gardes16GiB obligatoires, aucun seuil modifie. Ne pas lancer le V26-v1 ni un second build sur timeout. Les captures/audits GPU V26 restent a faire. Objectif complet ACTIVE ; brume volumetrique, appariement des histoires et qualification totale Studio/gameplay/portable/performance toujours ouverts, aucune publication/gate nouvelle.

Build31550 terminaldeebfb exit0 : sources1936/SDK intacts, nouvelle engine a30d773501794325555356ace16239a8df6795691982fe15b79b3e1f1b415b1a, logs855844dddb8cf55dff066b8f01a8cea42004d5d47f1ef94e6ef810e35b86ef6a etf1f935a77bbdea4345e51558a6ef0160475ee0f1fce555ae7285ba781f3354a0. Binaire V26-v2 c9de64e9a8a44f6d1f051e314630b642c11b8385e1755306dc052e6bbffabe02/133465088octets (fcdd2d). Nouvelle sonde b4b59e/session6394 : scene preparee76a30f, deux guards18163822592/18167566336 ; a cette lecture fcdd2d aucunPID encore attribue. Poll du meme handle d89890 et receipt710cf4 confirment ensuite PID47004/runtime_executed=true. Sonde en cours, pas de captures declarees. Tous builds/checks/lints propres precedents terminaux ; seul handle6394/PID47004 a suivre. Prochaine continuation : poll6394 sans redemarrer, inspecter processus/log/artefacts, attendre terminal puis audits HDR/finaltrace/clock/scene/camera ou diagnostic d'erreur precise. Objectif ACTIVE.

### V27 : resultats GPU V26 et identites des captures corrigees

Le dernier tour utilisateur etait un statut : no progress, pas un blocage. Reprise de l'action sur l'instrumentation des captures, sans modifier les equations de rendu ni masquer la dominante bleue.

Sonde V26 session6394 terminal5d7da4 : native0/helper1 (anciennes exigences de captures beauty supplementaires non satisfaites, conservees). Audit resolve d2bcb7 PASS : 33 plans GPU,136 commits, couverture complete/reset seed ; preuve073b9efec333c6e3f0790914fa434e207dcf2aba84c5e7e9675e094d305f9908. Trace f13110 PASS134 finales ; exposure avant124=1.7697131633758545, source124=1.752136468887329. Horloge421302 PASS dt/night124 plus10 steps tail explicites ; pas d'appariement scene/TAA/cloud/ocean. Camera5c8078 diagnostique : near.1/far60000 et profondeur finie proche de la source ; projection complete/histoires non qualifiees.

AuditSceneHdrCapture historique359c5f echoue : view_bits main-world4294947806 compare a render_view_bits4294966874. Ces espaces ne sont pas interchangeables. Le diagnostic separe DiagnoseSceneHdrPlanes/8c5847 conserve qualified_scene_hdr_capture=false/render_main_mapping_verified=false, verifie controls GPU124 et mainID via preuve resolve liee, sans inventer le lien rendu. Quatre plans finis ; scene luminance native.1853555/source.1831528, final input.1828770/.1831783 ; RGB final native[.2096847,.1819878,.1127571] source[.2126054,.1819230,.1089595]. Cela ne prouve pas que l'ancien NaN materiau est corrige.

Nouvelle capture schema3 : stocke mainID72..80 et renderID80..88, packet diagnostique128..464 et header512 inchanges. before_hdr enregistre (FrameTag, ViewQuery.entity), encode exige exactement la meme paire avant les copies finales. Provenance des identites explicitement diagnostique ; aucune presentation du packet comme copie de son buffer lie. AuditSceneViewIdentity exige schema3/renderID trace/mainID resolve et controles GPU ; l'ancien audit et les anciens artefacts restent intacts.

V27-v1 metadata0/logf49d9427c799882a3df4a47b716dfa47a5643b28612b7c9138cfe8bc06840572 ; lien83725 terminalf25b71 exit0/log4be6d4bb0ebdae125340946b4f78e64883ab249954e9470089d7e954f3665ead. Lint49999 terminal530756 revele nouveau too_many_arguments8/7. Corrige par argument identite coherent (tag,renderEntity), pas suppression du check. V27-v2 metadata96999 terminal6ba9fa exit0/log2d424eef1ef8e18490e11b9e7fce1a4f1892f0c0e67e583755fe0741a09a09c2 ;1936sources/SDK intacts, engine V26 a30d7735 reutilisee car seules les deux sources exemple changent. Lint9702 terminal952404 : aucun diagnostic source_final, warnings preexistants ailleurs conserves/log14f659dfcc4b01c0f5c5bf778c9c02f4698e147a9f80280d1d344dbb82be82d0. Lien20682 terminalc718f1 exit0/log847f556be33792f09298dfd913e8a7b190d9f51cff1d046cd2a772cc3a46ea65, binary72a5a7a1b1081e28fb991f666b910a775a6e3c900901d5fbede1dfe44902032f/133477376bytes. Rustfmt/diffcheck cibles d0c4f9 PASS.

PreviewSceneHdrPlanes1e116c genere comparaison deux stages/source vs Rust depuis V26, SHA c5b739f421ce75e1aa39dac8d0183ba6fc46c63e7716c0fd5e96e36a35e12e25, inspectee visuellement : scene brute proche, foret native plus bleue apres postpasses. Transformation commune ACES/sRGB exposure1, PAS finale complete ni histories paired ni camera identity qualifiee V26. Suite : sonde fraiche V27 schema3 avec guards16GiB inchanges, audits puis port coherent AirHaze complet. Studio/gameplay/portable/performance et qualification globale restent ouverts ; objectif ACTIVE, aucune publication ni gate close.

Premiere invocation V27 bbffc0 echoue avant case/Popen : transformation imbriquee du wrapper conservait retention ordered-linked au lieu de resolved-guard-linked. Chemin corrige directement dans le template ProbeFinitePerspective, attribution finite_perspective/finite_resolve/scene_view_identity exige les predecesseurs exacts. Invocation e56168 session87855 encore en preparation au poll e726c1, aucun GPU acquis a ce stade. Conserver ce handle ; ne pas relancer sur timeout ni reutiliser l'ancien binaire. Builds/checks/lints precedents tous terminaux.

### V28 : capture schema3 verifiee et synchronisation du consommateur final

Tour precedent progress (correction identites/build/sonde). Sonde V27 meme handle87855/PID45968 verifie live a479b4 ; gardes20502642688/20163948544, seuil17179869184 intact. Terminal80e367 : native0/helper1 (meme ancien check beauty supplementaire, non affaibli), image9d2b17ad3f508d038de4f20b6a816136c7f81762ac4d3404417d6e962404c0c2. Audit resolve5a1696 PASS33plans/136commits/couverture/reset ; preuve2413f14f886d6fcc95e116f14a042e94518ba897c18d3e0be77d9fa3d6174370, execution1c2127542d67617d4609d13ca1a98b3c377496395eb86d3a156ec268d392f79f. Trace2a8ec8 PASS134 images/continuite exposition, trace19f25312c7a2166d39c77041ef86978df21a371dbcf8e2981be08e68926efc11. Horlogedac259 PASS124dt/night +10tail explicites, exposure124native1.7697126865386963/source1.752136468887329.

AuditSceneViewIdentity9b0806 PASS scope capture : scene/final tag et renderID egaux, main4294947806/render4294966874 raccordes aux preuves distinctes ; packet e80e3e5d3219e18e8339d7d90b8dd49a9aa443b0e8b0a8cbac149eeeaf1940f4 reste upload diagnostique. Receipt e37c5a8a0ba1cfd05e73bafc5ade86c4fa0d52ceb6bb357fd3fc9eb8fbffdd2e/controls343154de9f105062c678542098621f2ea2432c6ff072c91531bc8fd193ccd52d GPU124, quatre plans finis. Ecart RGB final pratiquement identique a V26. Identite capture verifiee, PAS histories/pixel/fullmap parity ni correction du NaN historique.

AuditFinalTemporalPhase d51dd7 compare le packet selectionne de cette capture au trace sourceHDRv3 f8b23edd038f9be289b4e51d8c61ecdf8c6ff57b51ed7c0947e207efd5b8d36b. Les124 phases source sont retrouvees univoquement depuis jitterNDC observe et Halton32 double, sequence0..31 cyclique ; sourcefinal124 phase27/nativefinal124 phase29, decalage2. Nativejitter[-.03125,-.37654322385787964], epoch0/seedflags0, near.1/far60000. Pas de deduction depuis seul RAF ni assimilation du packet upload a un bound GPU readback.

Inspection source_final_post::render : ses pipelines First/Down/Up/Final ne se specialisaient qu'APRES un resolve reussi ; deux commits temporels pouvaient donc preceder la premiere finale. Correction V28 : ConsumerGate RenderWorld optionnel, absent conserve standalone ; present exige readiness publiee pour EXACT FrameTag avant tout seed/draw/copy/reservation/commit du resolve. Absence/pending/stale/autre tag refuse ConsumerPending sans avancer phase. Preflight du final en Core3d apres MainPass/avantGTAO specialise/verifie tous render pipelines +meter compute et History dimension/down5/up4 pour le packet courant. Pas de phase initiale forcee, pas de reset arbitraire, aucun garde existant supprime. Ready est preparation, jamais receipt de draw final ni GPU completion.

VerifyFinalConsumerGate25167 terminaldbf72e compile0/test0 : vrai module consumer production, vrai FrameTag engine V26, SDK ferme intacts, source1795d663d315b9e3b65a1f641a346544c8fb3fc2be6b4aebd8a3e9d54d68e04f. Tests absence/pending/meme tag/foreign view/next tag/stale publication. Check74305 terminalb18e2e engine ET exemple0/1937sources intacts, logs34551f92eba30d7c3b78ec5349b861575a57f3da681916f3f88452170a9904e4 et4736c5081a66b25fccabbe5f4efdb930d86732fc820b5c9a605946a076e929e3. BuildV1 60273 terminal09d6eb exit0/engine8107e5ece325178f8c74e112df4f2a52443ed100ef2a31966ff3a598fac54841, logs964e58b580307517438aead9805d2c06134611398c9739c76c1f7d9c90b3cb83 et2ce4dea89d549195b38a803a1dc1e0675a45fb12cec4bd9d1f4b413227900635, pas lanceGPU.

LintsV1 87723/77288 terminaux4357fe/b2e3e4 : doc RenderWorld sans backticks dans consumer et ancienne annotation render attachee accidentellement au nouveau preflight, donc trois warnings renderer. Corrige scopes annotations existantes sur render et annotation query seulement pour preflight, docs backticks. Aucun algorithme change pour masquer le lint. SnapshotV28-v2 check15838 en cours ; conserver ce handle. Rustfmt/diffcheck ciblesV1 f1b71c PASS ; verification GPU phase apres synchronisation encore requise. Objectif complet ACTIVE ; AirHaze complet/animations/Studio/gameplay/portable/performance et gates non qualifies par ces travaux.

CheckV2 15838 terminal962428 exit0 :1937sources/SDK intacts, engine log11daea930a18bff2449a70e82d54f81e1aa8cf1e8ee9c886ed6b9c102e692f7a/exemple65687550d93d85fdea1380d3f546ecad08db2f0dad866d3ec974adb25dec68f5. Nouveau consumerSHA d2d9779a6ffa5c516a908c7670b41025e704ca55f07b0e0cc4651aae52ee51ca (doc seulement depuis CPU test), final949565c753f77117d50cbdb9696bfe3de1ff8ef49bff000aa63a2e4bfd191628. Rustfmt/diffcheck cibles0ec394 PASS. Lint exemple72153 terminald09189 exit0 : aucun diagnostic source_final, warnings autres conserves, loga46b6f085184d551dfe356c85fa65bf10bb6a1be054ad95483f87cbf0893bd00. Build70500 et lintengine39386 en cours aux polls8916db/0d727d ; conserver les memes handles, pas de relancement sur timeout. ProbeFinalConsumerGate20261008.py prepare pour le nouveau binaireV28-v2 et attribution chainecomplete, NON lance tant que build/lint terminaux non verifies. GPU V27 termine, aucune nouvelle phase appariee GPU encore declaree.

Lintengine39386 terminalc362ae exit0 : aucun diagnostic dans consumer/resolve/module temporel avec chemins Windows normalises, autres warnings preexistants conserves, log426b291d1f353a1ac64aa198d1ae136246089813ff65a8fabab4042085a1d6fe. Build70500 seul job propre encore live au pollc9be7d, rustcPID43416 confirme. Prochaine continuation : poll70500 jusqu'au terminal puis guards/sondeV28-v2, audits HDR/schema3/clock/phase ; n'annoncer phase27 ou parite qu'apres mesure effective. Objectif complet reste ACTIVE.

### V29 : noyaux AirHaze portes, lancement V28 refuse puis nouvel essai

Tour precedent progress : consumer preflight/tests/checks/lints. Build70500 suivi sans relancer (bb5fed/6b5166), terminal93f9f4 exit0,1937sources/SDK intacts, engine d339015604ff19b118d102693f018cd05f62ea146e1cb2e85ff9f92e3f5fb22b, logs964462adbe26839e2d625e5a5b1c120da8841e289a52efe997fa1f204a59e04a et04e0a0ecbda5d8612f0156a596c0decc6d2594b4e65ef0d6f32f4ded3faeb382. Binary ab05d6067d4488147c4a03e3b5167ef8a3bf814db205728ea9fe798239ba1dc7/133564928bytes.

ProbeFinalConsumerGate session8914 terminal141029 exit1 AVANT Popen : headroom14937825280 <17179869184. CaseV28-v2 execution.runtime_executed=false, aucun nouveau PID/capture/phase qualifie. Seuil inchange, aucun processus utilisateur arrete, case refuse conserve. Travail CPU independant poursuit l'objectif, pas un impasse/blocked.

Nouveau tidewater_air_haze_core.wgsl enregistre via load_internal_asset, import NagaOil explicite dans le shader existant ; seul helper analytique haze_layer_depth y est effectivement utilise pour l'instant (equations precedentes conservees). Port des noyaux complets source : phase Cornette-Shanks, jitter IGN/golden ratio,16pas quadratiques/2500m avec lit/all/tau/sea height, bilinear depth-aware4samples, composite coherente fSun/900m/near/deficit/sky/radialgain3.5, masque solaire et24poids/3normalizers/steps de blur radial. Inputs enabled/lens in_air explicites ; visibility16samples et fog incluant moon sont fournis par l'adapter futur, aucun faux stub neutre installe dans le renderer. Passes half/quarter-res et vrais bindings cascades/terrain/clouds/frame/medium restent A BRANCHER ; aucune amelioration visuelle ou volumetrie active revendiquee par ce port partiel.

Source AirHaze.js verifie identique a reference acquise 7aec6d356b50e86da65ee025333d86db43177ae8055e77eef7ad8daf034138e2. AuditAirHazeCoreSource b56c61 PASS428files au commit1438b1ab, constants/tableaux radial GPUf32 verifies contre source et poids Node inspectes. Common.js77eafb3c943019038170b592b9bc7347c66bea60c053c5135bc952aa96e1d458 ; audit n'est PAS execution des equations ni equivalence algorithmique qualifiee.

VerifyAirHazeCoreV1 adee16 compile1 : borrow composer dans harness diagnostic (E0502), sources/SDK intacts ; V2 session69591 terminal1ee20c compile0/validate101 revele keyword WGSL pass reserve. Renomme pass_index. V3 session95143 terminal5081b4 compile0/validate101 revele active reserve ; renomme enabled. Echecs preserves, aucune fonction eliminee pour faire passer le controle. V4 20069f compile0/validation0 : vrai Composer NagaOil avec module fullscreen Bevy0.19.1, vrai core importe, vraies variantes hazeHDR/display et fixture appelant tous les kernels avec samples explicites. ABI HazeSettings160 conservee. Core6729371a18aaaf26a6cf8d8a2f79c74117b269c06c5b0bc06b15343b98dd5bbb, Rust626364922fdf84c96d0f47f2392361a96792e6f08158f50936a90c007088b3e4, haze d35e4f0eceab00ef8d58d206789ac142deda9c0b56d9b74ce54718d9a315f5e6. Pas de GPU/core execution, pas de shadow bindings qualifies.

Premier CheckAirHazeCore25778b echoue dans wrapper avant snapshot (needle names trouve a l'interieur du code s.index). Needle borne a ligne d'affectation ; check26089 terminal8ccf45 engine ET exemple0,1938sources/SDK verifies, logs a3fa2b6b50e9a424ac4aa89e42d38fd833015825b413360093e3c91cb998c1d4 etdc2c6b94164da7ffe294f8388adee77251094b39e87ca9342932a00a312c634d. Rustfmt/diffcheck cibles sans diagnostic. Lint75565 terminal5f0ef6 engine0/SDKsources intacts/loge81e977460e76318c0615d40ff4e7b4342df4d7174651932f235ef24f4b9a765. Filtre own du wrapper avait tuple singleton sans virgule, iterait caracteres et classait tous les warnings comme own ; corrige pour futures runs. Controle complementaire des ALL diagnostics avec suffixe normalise /tidewater_air_haze.rs ne trouve aucun diagnostic. Resultat historique AEBC2B4BEE8A6852936A89C84FBF9B0664CDBC08E93F4DC82ADEEFED001C91C8 conserve, projet warning-free=false.

Mesure actuelle headroom21740863488 apres travaux CPU, donc nouveau RetryFinalConsumerGate dans caseV28-v2-retry1/retentionV28-v2, pas remplacement case refuse ni nouveau build ; deux guards immediates restent obligatoires. Session82995 en preparation (0ee53d), conserver le meme handle jusqu'au terminal. Ce binaire V28 sert a mesurer synchronisation phase ; il ne contient PAS le nouveau core V29, qui a seulement des preuves CPU. Objectif complet ACTIVE ; suite GPU V28/schema3/phase et branchement de l'AirHaze complet puis scopes map/Studio/gameplay/portable/performance, sans publication implicite.

### V30 : synchronisation mesuree et passes volumetriques en preparation

Le dernier tour de statut seul ne constitue pas une progression du but. Reprise sur les fichiers et preuves actuels : le retry V28-v2 est terminal, puis le port des etapes AirHaze continue. Aucun processus utilisateur arrete et aucune publication. Objectif complet ACTIVE ; ces preuves ne soldent ni la parite visuelle, ni Studio/gameplay/WebGPU/distribution.

RetryFinalConsumerGate82995 terminal0dc3ee : runtimePID35140 exit0, helper exit1 sur ses controles historiques de capture supplementaires, conserves sans affaiblissement. Deux mesures immediates avant lancement : 21634838528 et21622972416 bytes disponibles, seuil17179869184 inchange. Absence du PID35140 reverifiee lors de V30. Binaire V28-v2 ab05d6067d4488147c4a03e3b5167ef8a3bf814db205728ea9fe798239ba1dc7 ; ne contient ni core V29 ni nouvelles passes V30. Image context cc953def5c9fea8ff70ca866b75305992af8cdbdd50197820c15a518b4170042 apres mouvement/reset, pas une capture beauty de reference.

Case local ignore `.tidewater-integration/native-source-hdr-final-probe-village-20261008-v28-v2-retry1` : AuditSourceHdrFinalProbe8e3ad8 PASS33plans GPU/134commits resolve ; AuditCompleteFinalTrace1c07ab PASS134images finales, soit134/134 contre136/134 auparavant. AuditSceneViewIdentityf0ca76 PASSschema3, main4294947806/render4294966873, meme tag et render view entre scene/final ; mapping RenderWorld/MainWorld verifie. Controls64 GPU343154de9f105062c678542098621f2ea2432c6ff072c91531bc8fd193ccd52d bit-identiques a la trace. Packet e1750bf4ac38c4bf91073ae6ef554b69e961b44088e5d93e2f340b8383c5878a reste un UPLOAD diagnostique, pas une copie du buffer effectivement lie. Scene audit6671221917ca37ec0fbff5df0bec35b309779ffb766ec40af86f24aa0b8bc24f ; trace d64d09d85d1fd1402cc445c27606f441d93cef163cd389c570ea1757d177c0ce. AuditMeasuredFinalClockb5e3cc PASS124dt/nuit plus10repetitions finales, sans injection d'exposition source.

AuditFinalTemporalPhase932b58 PASS : source final124 phase27 et native phase27, decalage modulo32 zero (V27 phase29/decalage2). La phase source est reconstruite depuis les observations de jitter ; la phase native vient du packet diagnostique. Aucun historique complet source/TAA/animations declare appaire. Les quatre plans mesures sont finis, sans resoudre retrospectivement les anciens NaN de materiaux. MAE diagnostique du scene-HDR .005890406668 contre .02182128094 en V27 ; entree finale .00840132218 contre .00859129522. Exposition native avant124 1.7697207927703857/source1.752136468887329, ecart conserve. PreviewSceneViewIdentity129582 terminal0, `scene-final-preview/comparison.png` inspecte : brut proche, foret encore bleue apres effets. Mapping declare depuis l'audit reel ; ACES/sRGB commun a exposition1 seulement, pas le composite final complet ni une qualification de parite.

Core actuel cec53a59fd82783862ceeddd659f8521c0308c35ee575c2bb44059c2cc993b61 : visibilite objets par cascade/un seul tap dur avec biais2e-5, lookup bilineaire du relief et penombre, ombres nuages avec projection le long du soleil et fade source. Frontieres de cascades suivent `lighting.js` reel (UV <=0 ou >=1), pas le fallback AirHaze inutilise. Structures settings448/live464 ; camera416/forward432/frame_index412 et control live448 verifies dans le module compose. VerifyAirHazeVisibility51419 terminal692d11 : compile0/validation0, sources et SDK intacts ; textures declarees dans la fixture, aucune lecture de ressources GPU reelles revendiquee.

Nouvelles etapes enregistrees via load_internal_asset dans le plugin : `tidewater_air_haze_volume.wgsl` (camera160, volume80 et rayons profondeur finite reverse-Z), `tidewater_air_haze_march.wgsl` (16positions quadratiques, vrais appels objets/relief/nuages, sortie lit/all/distance/1), `tidewater_air_haze_radial.wgsl` (8taps chacun des trois indices). Formats prevus RGBA16Float demi-resolution et R16Float quart-resolution. L'adapter devra conserver trois slices immuables distinctes pour le blur, les trois ecritures d'un meme buffer avant submit n'etant pas trois valeurs GPU distinctes. Ces assets ne sont PAS encore executes par le renderer : ni targets allouees, ni bind groups residants, ni branchement au composite ajoutés ici. L'effet existant reste analytique ; aucune amelioration visible attribuee a V30.

VerifyAirHazeStages176d37 compile0/validation0 : vrais imports NagaOil/fullscreen Bevy0.19.1, haze HDR/display et points d'entree march/radial ; tous les bindings et layouts160/80/464 verifies. Rust8a8e720c294e6d153490c1c4623ce803a458ce663770fe9672d93695afc2a9ef ; volume418099cdadcf5bc34031865b803102b073cf0d4b459649bc0542a3d5f8b49fb2 ; march daa825fe5941c0d2a7dadc3c0d935d53b7d290b50dac0179b2bb6a117e6f237b ; radial25f9c746eb51e3ffd65d04299a2c6019c4a2dacc52c97e5d4df72f74af1cd4cb. Haze existant d35e4f0eceab00ef8d58d206789ac142deda9c0b56d9b74ce54718d9a315f5e6 inchange.

CheckAirHazeStages87635 terminal6343f9 engine0/exemple0 : snapshot V30-v2/1941sources et SDK intacts ; logs402b75212f3b4ea960efd4419bf4d1d34d9e91f7d4c7a785c5c8f5a3d9298d4e etdefd1a5a387334e079f2cf463a854df7c33bdf14c4f71729d6df8c2b9c43afb8. Compilation metadata uniquement, aucun nouveau binaire lie ni essai GPU V30. LintAirHazeStages66890 terminal1469ba exit0, sources/SDK intacts, aucun diagnostic du raccord haze ; warnings ailleurs conserves, project_warning_free=false. Log7e56406acbe5aedf8bcc66259b011c45baf1e806cb76c3a4ee44d5ecde452769. Rustfmt/check et diffcheck limites au fichier Rust affecte PASS ; pas de revendication workspace propre.

Prochaine etape : extraction du material resident exact (hill/cloud domain), reuse du depth-array et du live receiver de la meme generation, allocation des cinq targets, preflight pipelines avant progression du resolve, frame haze source accepte, soleil/sky actuels, masque de milieu a la lentille et cloud-view transmittance pour le masque solaire ; puis composite avec reconstruction depth-aware et moon sky. Ne pas assimiler alpha surface eau au milieu de la lentille. Ensuite nouvelle compilation liee, guards memoire et comparaison GPU du nouveau code. Les autres vues/materials/effects, edition commune/save/reopen, gameplay complet, performances/native-WebGPU et portable launcher Aube restent dans le but original.

### V31 : selection des ressources residentes et correction du contrat lumineux

Tour precedent progress (kernels/pass stages V30, preuves CPU et audit GPU V28 archive). Nouveau module `tidewater_air_haze/scene.rs` et extraction MainWorld/RenderWorld enregistree par le plugin. La selection vient de `ResidentTerrain.material` exact, sans premier materiau arbitraire ni slug : hill-shadow, depth array/live receiver, shadow nuages et rectangle actuel. Les handles sont compares aux sorties actives des graphes ; aucun substitut neutre installe. L'extraction de `None` efface les inputs lors du dechargement. `encoded_for` compare les identites Arc des generations objets/nuages/cartes optiques, les handles et le materiau optique prepare, puis les flags de commandes encodees. Ce controle est une readiness de GENERATION, pas une preuve d'image/frame tag, de completion GPU, de phase d'animation ou de bind group effectivement utilise.

Inspection du producteur reel AVANT activation : `underwater_map_frame` du terrain reference un recu de cartes de 32 octets (origins vec4 + stamp vec4u), cree par `tidewater_underwater_lighting::register` et rempli par `forge_underwater_light_maps`. Ce n'est PAS le Frame.js720 de l'eau, qui est dans le bind group du materiau optique. Premiere hypothese V31-v1 incorrecte pour la provenance lumineuse, conservee comme etape diagnostique ; ses checks/tests ne prouvaient que types/layouts/guards. Aucun shader volumetrique actif ni lecture hors buffer effectue. Correction avant GPU : le nouveau champ est `optical_stamp` (32bytes) et `optical_surface` est l'UntypedAssetId selectionne explicitement par `tidewater_underwater_lighting::Surface`. Aucun champ de ce recu n'est interprete en eclairage.

Contrat shader corrige : HazeVolume64bytes (control/terrain/cloud/screen) et binding7 `HazeLighting`48bytes (sunSea/sunColor/skyImmersion). Il devra etre PRODUIT SUR GPU depuis les champs du Frame.js du materiau optique prepare, dans la meme generation/submission, puis lu par march/composite. Ce producteur n'est PAS encore branche : ni buffer48 ni lecture effective de l'eclairage revendiques. La branche march lit l'immersion et le key/sea de ce snapshot futur, pas une approximation CPU ni le recu32. Le compteur propre a haze (mod1024) reste a synchroniser aux passes acceptées ; ne pas le remplacer implicitement par le serial de preparation ou le stamp optique.

VerifyAirHazeScene60063 terminal4a87b9 compile0/validation0 pour l'etape720 initiale, limite de provenance ci-dessus. CheckScene93084 terminale8e34d engine/exemple0/1942sources et SDK intacts, logs f5ff42aef9376b471e53a8ad7e0a99e3f4db6be9912531f6bfe701a17658490a etae573cb36f54a463f50b8777b27c2c2b1a370014630c69f5b406c0aaf207502d ; CPU Inputs28057 terminal1bb1c2,2tests PASS sur module1176c404a136faf1f828da51c834e6c99588be249f64f6f4b8d3a8a987d7ac0a. Lint19206 terminaldb04ad exit0/own diagnostics0/loge73f5c3d207dab51994a55b3700859016d61d51b5fa3d58246c8094b54b2c9a0. Ces etapes restent historiques et ne qualifient pas le raccord lumineux initialement suppose.

Version finale du module scene af9011f461d9c3e519a63257e0adf4b2e8cdc99b7fc22c02dc61f5a481a83bfa. VerifyInputsV2 session65005 terminal80cc26 compile0/tests0 :2tests de production, type GPU/ressources reels du moteur V28 conserve d339015604ff19b118d102693f018cd05f62ea146e1cb2e85ff9f92e3f5fb22b et Bevy du SDK ferme. Rejette chacun des trois anciens Arc encore ready, les handles differents et un autre material Surface ; nettoyage ECS sur absence du resident. Sources/SDK intacts. Le harnais standalone signale une warning dead_code pour les champs publics qu'il ne consomme pas ; pas de masquage de warning ni faux dessin GPU.

CheckSceneV2 14718 terminal210dfc engine/exemple0 avant le remplacement final du prototype720 par48, traces conservees. VerifySceneV2 shader42574 terminalf24d4f compile0/NagaOil0 sur octets finaux : camera160, volume64, liveShadow464, lighting48 et bindings march0..7 verifies, vraies variantes HDR/display et imports. Rust8d53ac10c387536b2ac8ddf994e8c6b12e048f85fb4c60d3b79db5df34323e37 ; volume f9e536af7068b08e3bb14e4bfd07a7bc7c8b20a88df61f8b7db268a99c6348b4 ; march0e57168cc459cfe246fc2eec5287b9b62c05451ac91f77f374a30da668e9b3e4. Haze/core/radial precedents inchanges.

CheckSceneV3 4548 terminal6770b3 : engine/exemple metadata0,1942sources et SDK verifies, snapshotV31-v3 portant les octets finaux ; logs ca5a8c5805f7a7150f75467a5ec8e2e5879916159d5998d642123daaf33c5b3f et2ced6177609f6dcd63d5f76032f4f7053aae8149f174f6d6de53696116d0f12b. LintSceneV3 20990 terminalc59565 exit0/own diagnostics0/sourceSDK intacts/log52c7fa42e6e8795353b31867895f46fb01781db563890346e0caf8544361243d ; autres warnings conserves, projet warning-free=false. Rustfmt check et diffcheck cibles PASS. Tous les jobs de ce tour terminaux, aucun binaire final lie ni scene GPU V31 executee ; la passe visible reste analytique.

Suite concrete : publier le snapshot lumineux48 depuis le dispatch optique existant et son bind group prepare (champs frame.sunDir/seaLevel/sunColor/skyIrradiance/cameraUnderwater), avec handle/generation explicites, sans dupliquer le frame720 sur CPU. Puis preparer les targets aux dimensions effectivement observees (tenir compte du scale reference, pas supposer simplement la moitie du canvas), preflight des pipelines, encode march/masque/3blurs, masque lentille, moon fog et composite depth-aware. Guard16GiB intact pour prochain GPU. Source packet/tag, source haze clock, performance et comparaisons completes restent a prouver. Objectif complet ACTIVE, aucune publication ou cloture de gate.

### V32 : producteur lumineux dans le dispatch optique reel

Tour precedent progress : provenance32/720 corrigee, selection residente et contrat lighting48. Nouveau `tidewater_air_haze/lighting.rs` : buffer GPU48 (trois vec4), usage STORAGE/COPY_SRC, publication fail-closed par identite du materiau optique et compteur de dispatch de la generation exacte. Les anciens counters nonzero, mauvais Surface, stamp incompatible ou ancien dispatch ne constituent pas une readiness valide. Publication CPU = commandes encodees seulement, pas completion GPU ; view/frame tag et source haze clock restent des conditions separees a prouver.

Producteur branche dans le CODE d'`underwater_lighting` : allocation/extraction de ce target avec le meme Arc encoded_frames que les cartes optiques, quatrieme sortie du layout group2/binding3/min48, invalidation du cache de bind groups incluant son BufferId. Le compute `forge_underwater_light_maps`, dans son unique invocation gid(0,0,0), ecrit sunDir/seaLevel, sunColor et skyIrradiance/cameraUnderwater apres `setupUnderwaterHeightFrame`, qui charge le vrai waterFrame du materiau prepare. Meme device, meme dispatch que les cartes, aucun frame reconstruit sur CPU ou second renderer. L'encoder publie Surface/sequence apres fermeture de la passe compute ; aucun faux `ready` avant dispatch. SceneInputs selectionne le handle du snapshot et verifie sa provenance pour les futurs consommateurs. Ces consommateurs march/composite ne dessinent toujours pas ce resultat.

Premier apply_patch refuse atomiquement sur nom uwMeanLevel inexistant : verification e92194 confirme aucun fichier lighting ni module ajoute. Nom reel uwOutLevel relu puis patch applique. CheckLighting59762 terminal54006c engine/exemple0/1943sourcesSDK intacts, logs36fbdff859bbc509875b11f801d75e0b7da8cfdaa2447390a45e339d54b05f9c et5343496ffbcf42fada32c7b0e7ad3b50ffa68c75a510671082a6a60916dee4a0. TestsInputs11089 terminal127566 compile0/tests0,2tests sur modules d'etape ; pas de GPU.

VerifyAirHazeOpticalProducer97478 terminal039f0f compile0/validation0 : shader optique REEL acquis c80a0a521379acea2f8f72a61eb78d9fa0eb8c9c900946903d67c2ad51540c59, vrais imports Bevy0.19.1 et Naga, deux formes (constants originales et pack_optical_uniforms reel pour Studio). Composition publique du moteur conserve V28 d339015604ff19b118d102693f018cd05f62ea146e1cb2e85ff9f92e3f5fb22b verifiee identique a la fonction courante ; seul suffixe BAKE historique remplace par les octets WGSL courants. Helpers de composition du test temporel reutilises avec verification adaptee au contrat de CE shader : entry compute existe, group2 bindings0/1/2/3, storage lighting READ_WRITE, span48/offsets0/16/32. Pas de stub d'eclairage ou de materiau dans ce controle. Aucun dispatch/readback GPU prouve, aucune coherence de view/tag ou parite visuelle. Nouveau bake5de8686e40c340fb48cd60d9f232ce03aae33fe60ea199ed671ee82437bc34f7 inchange depuis ce controle ; les modifications suivantes sont uniquement Rust de parametres/cache/allocation, pas la fonction de composition ni les shaders.

LintLighting47227 terminalfe7e05 exit0 avec2diagnostics own type_complexity (Scene optical tuple et cache de sorties). Remplaces par OpticalResources/SystemParam et OutputKey ; aucun allow ou suppression ajoute. CheckV2 93315 terminal0511c8 engine/exemple0/1943sourcesSDK intacts, logs1b8238bc31c460e6aedafec4627e7554516c25a47e3ec1795af683f733a86ff6 et32c23ed30503d8725d97017cb0efa1e366e94211a61d665f0623224c607b936b. TestsInputsV2 48658 terminal5565f2 compile0/tests0,3tests : span/usage exacts du buffer effectivement alloue, provenance generations/handles/material et rejection du snapshot apres nouveau dispatch jusqu'a publication correspondante, dechargement ECS. Modules scene0d81262f950c5a0f4aaf8afbbc907165b6e84411d766e5e3f3b9f614bcb0a732 et lighting fcea5862cda58bd235b8f9aa44c3a6220011488540a0e58aa798ebe65e1bd8ea ; vrais types Bevy/moteur SDK ferme, pas GPU. Warning dead_code du harnais prive pour champs publics non consommes conserve.

LintV2 41849 terminal256eca exit0 laisse1diagnostic own sur tuple RenderState, localise span328..334 (f0758a), puis remplace par SystemParam nomme. CheckV3 69906 terminal46b106 engine/exemple metadata0/1943sourcesSDK intacts, logs d30d2b00cb6664be7b363e959a2bb6e9c225ee3a90a285338ddb49b6e2bf4df2 et0fc065339d05b665e4a0dc548e4ebea832882e72c21822aedd8be93c0373ca84. Rust producteur final900792f9726b6d618f8ccdff818ffa836da93ca57896b6d284bd33bcc896d6d4 ; plugin5f91d4ccbab000c691058495cbe2c5fa4654051f8b5d0d9d868fe6c729e1c3e1. LintV3 55978 terminalffbb20 exit0/own diagnostics0/sourceSDK intacts, log352ec37aa44165f5542f92198292ac89182b4fa5288de4ae26a116b1dd9dcacf. Warnings ailleurs conserves, project_warning_free=false. Rustfmt/check et diffcheck cibles PASS, controles readonly38991/73359 terminaux suivis jusqu'a sortie, pas de nettoyage global.

BuildAirHazeLightingV3 lance sur snapshot V32-v3 et SDK ferme : session65911 LIVE au poll125b4c, PythonPID26576/rustcPID43988 confirmes par CIM38207 terminaldf3241. Inputs.root reverifie3b6ffe = `Tidewater-Validation-Snapshot-20261008-v32-v3`. Message imprime V32-v2 est un libelle historique du wrapper, pas le root reel du build ; ne pas relancer pour ce libelle. Suivre65911 jusqu'au terminal puis verifier retention/sourcepins et lancer la sonde sous les deux guards16GiB inchanges. Aucun nouveau GPU lance et aucun pixel V32 qualifie pendant ce tour ; code producteur present mais execution effective encore a mesurer. Aucun objectif reduit, aucune publication/cloture ; objectif complet ACTIVE. Prochaine preuve : dispatch/binding48 sur binaire exact puis lecture GPU du snapshot, sans attribuer ces valeurs a une image sans tag/sequence mesures ; ensuite activation du march et composite complet, autres scopes du but original conserves.


### V32 : compilation liee terminee et preparation de la sonde GPU

BuildAirHazeLightingV3 session65911 terminalffc34e : engine/example exit0,1943sources verifies, compiled_inputs_unchanged=true et sdk_unchanged=true. Retention relue f2ab46 : binaire tidewater_plant_probe.exe SHA25629bb8ec189cca126e308e5162048dc4cd10d3be3c72d61c926461693c93ac7e1,133737984bytes, snapshot V32-v3. Resultat85c87cad9c505f879784d3234c66eb25d9f06c3f94f6138aaf9444e90c50127f. Ceci remplace l'etat LIVE historique du paragraphe precedent, pas une qualification GPU.

ProbeAirHazeLighting premier essai c3ad1b exit1 AVANT creation de case/runtime : assertion ancestry. Diagnostic b742ed montre que les patch records haze Scene/Lighting sont des deltas d'etapes intermediaires et non une chaine complete depuis Stages (certains before==after). Aucun manifeste historique modifie. Wrapper corrige : chaine historique capture/finite/scene/final consumer conservee telle quelle ; les derniers after_sha256 haze par chemin sont verifies separement contre l'inventaire de source et les fichiers du snapshot ferme. Ne pas revendiquer une chaine haze complete depuis ces records.

Nouvel essai session74698 encore LIVE au poll39afb3. CIM9c7949 confirme parent Python44300 ProbeAirHazeLighting et enfant24256 reframe-scene-probe.py sur V32-v3/case native-source-hdr-final-probe-village-20261008-v32-v3. execution.json pas encore produit, donc pas de lancement GPU ni de resultat GPU revendique. Suivre la MEME session, ne pas relancer pendant cette preparation. Les deux guards immediates16GiB restent identiques dans le helper. Aucun processus utilisateur arrete. Lecture48_GPU et coherence view/tag restent a mesurer ; le buffer COPY_SRC est present mais cette sonde existante ne capture pas encore ses octets. Objectif complet ACTIVE.


### V33 : mesure lumineuse GPU activable, sans nouvelle qualification visuelle

Tour precedent de statut : no progress pour le but ; reprise effective par lecture des preuves et actions ci-dessus. Sonde V32 session74698 terminalc7ea7b exit1 : reframe termine, execution.json relu b2e06c confirme runtime_executed=false. Guard refuse avec headroom14998695936bytes <17179869184. Pas de PID moteur, ni pixel V32 produit. Seuil conserve, aucun processus utilisateur arrete ; ce refus ne bloque pas le travail CPU independant.

Ajout reel dans lighting.rs : diagnostic uniquement si FORGE_TIDEWATER_HAZE_LIGHTING_READBACK=1, Readback::buffer sur le handle48 effectivement alloue. ObservedBuffer conserve le handle et l'Arc de generation ; observer ignore absence/remplacement du target. Decode exige exactement48octets et12f32 finis, conserve tous les champs et journalise valeurs GPU + identite buffer. delivery_encoded_count est explicitement le compteur AU RETOUR, PAS le tag/sequence de production ni une association a l'image. Zero initial n'est pas declare eclairage valide. Aucune modification WGSL ni activation march/composite.

VerifyAirHazeReadbackInputs session45082 terminal6d24d8 : compile0/tests0,4tests reels PASS (allocation48/usage, decoder tous champs et rejet taille44/52/NaN dans chacun des12slots, generations/handles, unload ECS). Sources et SDK intacts. Lighting22390b9db542e23add6c4a6b3a265fe7df1d91b41f71cedcf67ede0e634da703, scene0d81262f950c5a0f4aaf8afbbc907165b6e84411d766e5e3f3b9f614bcb0a732. Ces tests ne soumettent rien au GPU.

CheckAirHazeReadback session30003 terminal03900a : snapshotV33-v1,1943sources verifies, engine/exemple metadata0, source/SDK inchanges. Logsdf59c1b16551b8db15358cf62d7140e0265032a7f3200d8baf0d2bb72a28f743 et dcebe0279b5f13e392e2ef317289a67e5ffcb37e52ca28b21af3891f2706b549. Rust producteur9eabac98ac206648bd98c9d8e39ccb7c360040185f531782e07bd9979524851a. Rustfmt check/diffcheck cibles PASS. Lint session99318 terminal3d2b5b exit0/own diagnostics0/sourceSDK intacts/log301bdbba16ec063d0b3a2436431855d1b97cd8712fb119c7ea7aecfaaa0c92fa ; autres warnings conserves, projet warning-free=false.

BuildAirHazeReadback lance sur les inputs V33-v1 : session59812 LIVE au retourdd3c20, suivre le meme handle jusqu'au terminal. Le libelle stdout V32-v2 vient du wrapper historique, verifier le root reel dans inputs/retention. Nouveau GPU V33 non execute ; prochaine sonde devra ajouter explicitement le flag readback dans l'environnement isole du helper, qui efface sinon les variables FORGE_TIDEWATER_ externes. Preserver les deux guards16GiB. Ensuite analyser valeurs reelles et raccord march/masque/blur/composite, sans remplacer le but complet par ce diagnostic. Objectif ACTIVE, aucune publication ni qualification de gate.


### V34 : retour lumineux GPU reel et cibles des cinq passes

Tour precedent progress : observateur GPU reel,4tests et compilation metadata, construction liee en cours. BuildV33 session59812 terminalfab077 engine/example0,1943sources et SDK intacts, lib25350af322a4b5ecd057d843fd5e3738fe5bfd794867cb478812df9061dfa0c7. Retention15383d : binaire0ea3fa31a2deabc4c60753f46c9e7e66ac9238c168fe3610bccbc84365c58f51,133764608bytes, resultat30ca988ba7513702437268bf30ed95ed6050a337604d29b378e1b471cb59682d. Le libelle de build V32-v2 historique ne correspond pas au snapshot V33-v1, confirme par retention.

ProbeAirHazeReadback preserve checks historiques et pins finaux haze, active le flag DANS l'environnement isole (pas via variable externe effacee). Session1599 terminalfcc95c : moteurPID45308 exit0, helper exit1 sur controles supplementaires historiques conserves. Guards20127846400 et21148459008 >17179869184. Moteur absent du CIM4599e8 apres terminal. Imagecf77d2164271f5b6ecb9ab0c0ca610bf066eec64c1d1b6e28538a6dba9933e13 ; pas de revue beauty ou de parite revendiquee. Execution/inputs/binary/profile/siblings/sources/reference inchanges, source_final_native_submitted etgpu_trace true ; runtime_graphics_values/acquired_pose/environment/ocean verified restent false, diagnostic_checks_pass=false. Ne pas qualifier la sonde globalement PASS.

AuditAirHazeGpuReadback a1d0d8 :157retours48nonzero,12f32 finis par retour, aucune erreur de validation/rejet dans les categories controlees, GPU values observed=true. Lumiere soleil (-.8861255,.45498452,.088151515), sea0, sunColor(9.6353855,8.087042,6.0993576), sky(.3,.4,.6), immersion0. Source packets124 verifies par leurs pins ; all_nonzero_match_acquired_lighting=false. Source step1246655b5 : meme direction/couleur soleil, sky(.0904579163,.1746875644,.3673058450). Provenance diagnostique4599e8 : water-frame.bin de la scene240fdb36a67899f22ac46dbafbd4dae8120a7c6b9d35ff826ec69bd090b97e97 conforme au fixture, contient deja(.3,.4,.6). Ce n'est donc pas une corruption du nouveau snapshot ; l'optical Frame de la SONDE est ancien pour ces champs. Le comptage au retour ne prouve toujours pas tag/sequence de production. Auditexecutiond59073e45088ca38582ae2f39c6ba58f295ce373afbc630c93d48f2c02774749, log211e6a4edf9e7396787203ff3a6e9b69d37b5d230cb884a18b4fb18ad09524e5.

Inspection du contrat produit : tidewater_sky/frame_lighting.rs met deja a jour13champs de lumiere depuis solaire + readback atmosphere termine. tidewater_ocean_optics::update_lighting applique ce contrat au ResidentOptics ; Studio authoring_imported_terrain/solar.rs appelle ce raccord. La sonde utilise son propre WaterMaterial charge depuis water-frame.bin, ne consomme pas ce contrat ici. Ne pas remplacer sky par une constante source juste pour verdir le test, ni conclure que toutes les routes Studio ont cet ecart. Prochaine correction : raccorder les vrais inputs lumineux de la route native partagee et de la preuve (avec generation/frame cohérents), puis lire de nouveau le buffer GPU. Une contribution a la dominante bleue est une hypothese, pas une causalite pixel qualifiee.

Nouvelle API publique tidewater_air_haze/targets.rs, enregistree comme module : Dimensions calcule Math.round(output*.5*scale) et Math.round(output*.25*scale) en f64 positif, refuse zero/nonfinite/limites GPU au constructeur. Allocateur utilise RenderDevice/TextureCache existants :1RGBA16Float demi-resolution +4R16Float quart-resolution, single-sample/mip, attachments/sampling/COPY_SRC, caches par vue a raccorder. Pas de second device ou copie authoring. Aucun systeme ne l'alloue encore dans Core3d et aucun march/masque/blur/composite nouveau execute. Source setSize relue sur AirHaze7aec6d356b50e86da65ee025333d86db43177ae8055e77eef7ad8daf034138e2. La reference acquise indique render/resolution_scale1 ; ne pas confondre taille drawing buffer et taille interne TAAU.

TestsTargets session68525 terminal832906 compile0/tests0,2tests PASS (dimensions impaires et scale.75, zero/NaN/limite, formats/usage cinq targets). Moduleb760b0121a991725671ea10a1118367fd7116a0138689bd8342c62039d1621d8, sources/SDK intacts. CheckTargets13147 terminala7ac53 engine/exemplemetadata0,1944sources verifies, snapshotV34-v1, source/SDK intacts ; logs7b83d425181d87101f814f399e73fd98a7e52908638b4e4c1c5f2207a66aaf6a et a17c6c1d136d4390d58abb5dd06c55bb17d90181779ddc26f94ffc06937222c0. Plugin a745798e65afab27e82ac0f6475a893b6a95c756c6b3e1edacea4c61af12a288. Lint22260 terminal0e2e1c exit0/own diagnostics0/sourceSDK intacts/logd9b4b66b1cd76d0ab89766df11542a4641c48769fbe2ba4b352373547fbcbaae, autres warnings conserves. Rustfmt et diffcheck ciblesbf4729 PASS. V34 pas de binaire lie/GPU ; GPU preuve porte exclusivement V33. Tous les jobs propres de ce tour sont terminaux ; aucun processus utilisateur arrete, aucune publication ni cloture de gate. But complet ACTIVE.


### V35 : publication lumineuse partagee sur le materiau optique natif

Tour precedent progress :157retours GPU V33 revelent le sky fixe du fixture, allocateur cinq targets et preuves CPU V34. Nouveau water_optics::update_lighting(World), ordonne PostUpdate apres update_camera : attend latest du readback atmospherique partage, demande le vrai buffer irradiance apres ready/absence de pending, refuse completion invalide/ancienne generation, compose FrameLighting et ecrit ses13champs sur le Frame du WaterMaterial resident. Filtre transmittance exact de la generation active ; camera/time/sea/immersion restent ceux du materiau, pas de copie du paquet source124, aucune modification des donnees auteur. Atmosphere.params du materiau mises a jour ensemble. Source solaire native reste le default source pause16.2h, valide par contrat readback (un autre soleil acquis est refuse). Cette etape ne prouve pas une cadence solaire mobile ou tous les autres consommateurs spray/lip/underwater ; ne pas qualifier tout le lighting.

CheckNativeOpticalLighting30985 terminalb4ce68 : snapshotV35-v1/1944sources, engine/exemplemetadata0/sourceSDK intacts, logs4841c205aa474f8a9e8a837199daf42e224d4bee4e5195e4315c07e1452aed24 et b8bafe731ee6420fe0109305cac1fdc3074b48571f6c7076009e1fb4d8b060db. Mainexample35bc1452fba69f0099b52e908198662cda2e389cf7a9c27f202b49a227064cc8, water_optics3c1d16a067e40d63e1dd67bda5e42b10a8000cfdb667950ad7c292d8c2ff027d. LintNativeOpticalLighting89190 terminal5fedaf : commande exemple REELLE clippy-driver sur metadata engine epinglee, exit0/source/dependencies inchanges, logd9b9eef5b2a0d29e5e5ba78f3df03ef308ec3b1fd9327ababea0061bf40b076d. Aucun diagnostic dans water_optics ni le nouveau systeme ; deux warnings du main hors modification (bloc AdditionalOpaqueDraws899 et tuple capture1521) conserves, pas de warning-free global. Bloc ombres deja present V34, relu2373c4. Rustfmt et diffcheck cibles PASS.

BuildNativeOpticalLighting22345 terminald06727 : engine/exemplelink0,1944sources et SDK intacts. Engine lib07ba8dddf4b363e2babe3e68d8b370b639e60d0ce2816a9f6d6ef1bb09ce09ab ; logs1964035b3ce50bc256b70d1f19210ee8bd6ad48e5e6aa234123b572e8108c381 et4273a3129078c34f89e56ce0e06a85348d933c32f2016d19ce34c66d2260cc4b. Retention fba37e : binaire7351df70cf0ea79639096730475f4c4bf31a4abaec1612b8c2c2b0941fa8ec07/133731328bytes, result4dc4ea2b7b1b716eee281433fe3fabacb7de5101abd59595fca05961896d4d63. Pas de GPU V35 qualifie par ce build.

ProbeNativeOpticalLighting premier7df40f exit1 AVANT case/runtime sur chaine identity. Diagnostic5d6305 : native_optical_lighting_patch inclut aussi le plugin haze inchange, dont les transitions intermediaires ne font pas partie de la chaine pose. Correction du wrapper : ajout a cette chaine strictement pour les DEUX sources natives modifiees (main+water_optics), leurs before hashes controles ; tous les haze et targets restent controles par derniers after_sha256+inventaire+fichiers fermes. Aucun manifest historique ou ancien garde affaibli.

Reprise sonde session81423 LIVE au poll6b7fa1. CIM4e7277 confirme Python43540 ProbeNativeOpticalLighting et enfant18920 reframe-scene-probe V35-v1, preparation active. Pas encore execution.json/runtime.log, donc pas de resultat nouveau GPU. Suivre MEME handle jusqu'au terminal ; ne pas relancer sur silence/timeout. Helper garde deux controles16GiB identiques et profil/source dt acquis. AuditNativeOpticalLighting prepare pour conserver warmup/all-match et ajouter nombre/dernier readback match, sans transformer un dernier match en preuve de frame tag ou historie appairee. Tous les autres jobs propres terminaux. Objectif complet ACTIVE, pas de publication, cloture de gate ou nouvelle amelioration visuelle qualifiee ; suite preuve GPU puis activation complete march/masque/3blurs/composite et tous les autres scopes du plan.

Mise a jour V35 : poll9ada3a de session81423 annonce runtimePID17356 et preparation terminee ; moteur LIVE, resultats GPU toujours a attendre. Gardes et receipt a relire pour audit terminal. Suivre81423 sans nouveau lancement.


### V36 : lumiere native mesuree et pipelines march/radial residents

Tour precedent progress : publication atmospherique native, builds/Clippy et sonde GPU lancee. Session81423 terminal72f175 : moteur17356 exit0, helper exit1 sur controles supplementaires historiques inchanges. Moteur absent au CIM8317c7. Guards21053001728 et21056425984 >17179869184 relues462677. Binary/source/profile/siblings/reference invariants true, submitted-final ettrace true ; controles runtime graphics/pose/environment/ocean toujours false etdiagnostic_checks_pass=false. Aucun verdict global PASS.

AuditNativeOpticalLighting cc416d :157retours GPU nonzero48 finis,4avec le sky initial(.3,.4,.6),153avec le vrai retour natif(.09064671,.17491752,.36751175), groupes mesures e24c0d. Nouveau sunColor(9.63549,8.087231,6.0996475), directionsoleil/sea/immersion stables. Le sky acquis attendu(.0904579163,.1746875644,.3673058450) n'est PAS identique ;0match exact dans124packets epingles, dernier match=false. Cette preuve etablit la publication du retour GPU natif, pas l'identite source/historique. Faible ecart conserve a investiguer (parametres/derniere integration et cadence source), aucune injection constante pour passer. Auditexecution2b23ac51e189b5c4a9877e4a697e57d5ee60beef628895ea328c4719c4ecbc51 ; logc25232d2b9f02e3ad97128f8e122b98451736de4db15d85a6c4022b4ec2648fd. Warnings de fermeture readback conserves. Image etfinaltrace BIT-identiques a V33 (cf77d216... etfccf23d3...), normal tant que la passe visible analytique ne consomme pas ce snapshot ; aucune amelioration visuelle qualifiee.

Nouveau module public tidewater_air_haze/passes.rs et init RenderStartup sur LE renderer existant : vrais descriptors du shader march RGBA16Float et du radial R16Float, fullscreen Bevy, layouts exactement8/3bindings. March : depthScene, cameraUniform160 dynamique, Volume64, profondeur array objets, hill/cloud textures float manuellement filtrees, liveShadow464 readonly etlighting48 readonly. Volume Rust ShaderType64 contient controle/sourcehazeclock/domaines/sun-screen explicites ; aucune assimilation a FrameTag.serial. Uniforms::upload cree QUATRE buffers distincts (march et3radial) ; les trois indices0/1/2 ne se remplacent pas via3writes d'un meme buffer avant submit. Pipelines::march_and_radial_ready couvre SEULEMENT ces deux pipelines, pas mask/composite, graphes ni tags. Aucun draw volumetrique n'est encore schedule : masque/composite, bindings residents, allocation par vue, profondeur/lentille/moon et preflight global restent a raccorder. Le nouvel init est en code, son execution GPU V36 n'est pas encore mesuree.

Premier apply import+test correction refuse atomiquement sur ligne zip formatee,048424 relu puis corrige. CheckV1 session49849 terminal854b56 engine1, E0432 texture_depth_2d_array absent de Bevy. Test16237 terminal321833 compile1. Corrige vers texture_2d_array(TextureSampleType::Depth), sans texture neutre ou retrait du binding. CheckV2 session1648 terminaldf6c97 engine1, lifetime label &str requiertstatic ; test V2 cellule1764 terminal compile1,5a7efa detail. Corrige label &'static str fourni par literals. Echecs conserves, sourcesSDK intacts.

TestV3 session14818 terminal4df3a2 compile0/test0,1test reel PASS du span64 et des3variants preservant frame/domain/UV/fade. Type camera reel importe du moteur V35 (lib07ba8...) et definition actuelle verifiee identique, constantes ShaderIDs extraites du parent reel, pas de types inventes. Rustfmtcheckb5309b signalait uniquement reformatage closure ; applique puis TestV4 session92823 terminalbbedb4 compile0/test0,1test PASS sur octets9e8fd647907a0ff2559b4096b7378ff82157ee07314ec0f00181bd26fd300834,sourceSDK intacts. Pas de GPU/upload execution dans ce test.

CheckV4 session12639 terminald06427 engine/exemplemetadata0,1945sources et SDK intacts, snapshotV36-v4. Plugin7c4c9f540641c371330e61122013d8a578e7f6bdd70f7cd3c23f1a54aafc22a0. Logs3b19581151d7b1b1e10cf5a6c32194ada7dd8342bda133e59599fcd2501fed52 etca25b181621b046d320763f7c1cc7934b5eb3f0b43edc2f5acfe3bb23272b668. Lint15952 terminal297fd7 exit0 avec1own doc_markdown HazeVolume64,log6bda0276c26f52b24470565297c2b87a8ebdd806bcde2a105016d24d9bc02d18. Corrige UNIQUEMENT commentaire doc par backticks, aucun shader/code/layout change. CheckV5 session13245 terminal611632 engine/exemple0,1945sources/SDK intacts, passesf6736ec8aa3e677a2ebaed7222a928e261152f3991170fae653ac3306b898a1a,logs00cc128feed58f9429584ae802cdfbbc10e4de9971a85cc1f7fa229cc23ef098 et4a4847aecee93fb30dc566050bcff67c2c80905d992887d7cc01c91441f377c2. LintV5 session54120 terminalca4300 exit0/own diagnostics0/sourceSDK intacts/logb802e829a405f58f14030ecd49fe77cf53c8eb871e81fc959f6295755eee1249 ; autres warnings conserves, project_warning_free=false. Rustfmt/diffcheck cibles930505 PASS.

BuildPipelines session43232 terminal5197a2 engine/examplelink0,1945sourcesSDK intacts, RETIENT V36-v4 avant modification du seul commentaire. Inputs.root e24c0d etretention930505 confirmentV36-v4, pasV36-v5. Libff2c8243791d4c11c35e3e34c64cd4eafdea86cf0c18900062081f9b0babefe4, binaire4f37331ba8e288a253c01964260b33ba759fad19746cce33398bed9dd25dc76b/133768704bytes, result6e0fc1a97c4606be5ac335b32486aed5dad0a2706affae6265b5b5f58a28a3cb. Pas de scene GPU V36 lancee ; ne pas attribuer observations V35 a ce nouveau binary. Tous les jobs de ce tour terminaux, aucun processus utilisateur arrete, aucun push/publication. Objectif complet ACTIVE. Suite : shader masque solaire avec vrais clouds view, composite complet depth-aware/fog+moon/medium, branchement per-view etpreflight avant resolve, sourcehazeclock accepte, puis nouvelles preuves GPU/pixels et autres exigences map/Studio/gameplay/WebGPU/performance/portableAube toujours ouvertes.

### V37 : masque solaire et echantillonnage des nuages portes, validation CPU

Tour de statut precedent : no progress pour le but complet ; verification du rapport V36 et objectif actif, puis reprise concrete. Ajout de tidewater_air_haze_cloud_view.wgsl et tidewater_air_haze_mask.wgsl, derives de la reference locale 1438b1abfcaee3267092b75573014f4d9b4a983c. Le masque reprend le seuil ssFade .001, le test sky reverse-Z 1e-7, le disque/aureole et la transmittance cloudsSampleView. Le module nuages reprend Catmull-Rom cinq taps, projection dans la camera qui a trace la texture, limites inclusives UV et horizon, fallback panorama avec samplers clamp/repeat distincts. Son ABI compacte 80 bytes est render-only ; elle doit encore etre remplie depuis la vraie generation et la vraie camera du cloud history, pas depuis la camera courante par defaut.

VerifyAirHazeMask20261008.py session48794 terminala84d44 : compile0/validation0, composition NagaOil et validation Naga reelles avec SDK ferme. Neuf bindings verifies ; spans/offsets camera160, volume64, sourceLight48 et cloudView80 verifies. Sources et SDK inchanges. Cloud shader SHA256 dfea2f0576c215064a08a4e910ff300f153c403bfe4a97541d36f43696cdcb19 ; mask 66b55c9bc75001f0c73d8914df6ded912692787bec6c53b4bed17a77c5e485f4. Evidence locale .tidewater-integration/air-haze-mask-cpu-20261008-v37-v1/result.json et verify.log. Aucun nouveau draw, asset registration ou pipeline masque ajoute ; aucune soumission GPU et aucune amelioration visuelle qualifiee. Les passes precedentes restent inchangees.

Suite : enregistrer ces shaders et le pipeline masque, adapter la vraie selection du cloud history et sa camera productrice ; composite complet avec atmosphere/moon/medium, targets par vue et preflight de toutes les passes avant resolve. L'ecart lumineux natif/source mesure V35 reste ouvert. Les autres exigences carte/Studio/gameplay/native-WebGPU/performance/portable Aube restent ouvertes. Objectif complet ACTIVE, aucune publication ni cloture de gate.

### V38 : masque enregistre et composite HDR complet porte, pipelines residents

Tour V37 precedent : progress (deux shaders et composition Naga validee). Ajout reel des assets cloud-view/mask puis composite dans TidewaterAirHazePlugin ; pipeline masque R16Float neuf bindings et composite RGBA16Float onze bindings sur le renderer Bevy existant. CloudView ShaderType80, Moon ShaderType16 ; programmes_ready ne revendique que la presence des quatre programmes, pas la disponibilite des inputs, du tag ou des passes. Aucun nouveau draw volumetrique schedule et aucune texture neutre substituee.

Composite source 1438b1ab : ciel atmospherique horizon avec LUT192x108, ciel lunaire Sky.js utilisant vraie direction/night, profondeur reverse-Z finie, quatre texels march depth-aware, attenuation/near shafts/sky deficit et rayons radiaux, alpha conserve. Nouveau shader tidewater_air_haze_composite.wgsl SHA9506cd1167e791078de2f1b10bea3e140c2672253529a25f467c2ab95cbd614b. Exige lens_medium reel ; lecture du code underwater montre que lens_medium est actuellement une fonction du post-process, pas une texture partagee existante. Il faudra exporter ce calcul avec ses vrais inputs optical/FFT/query/depth et conserver son guard near lens ; ne pas brancher le masque de surface alpha directement comme milieu.

Masque Check94154 terminalf0bc5c : moteur/exemplemetadata0,1947sources/SDK intacts, snapshotV38-v1, logs b2c506e1c9b6acea1e2814c64708a90170f2b228a46797d2cf33faaedae91404 /8f6631ad72d128122cbe44fd229479a351d75a491d7a7a28738a7b2121f55ac4. Test8396 terminal14f0a7 compile0/test0 spans64/80 et radial variants verifies, passes596e5296ffde5c27d7fcf0a305a2e3171ff6a939c5e018e982f60bf1a67f538b. Lint37241 terminalafed20 exit0/own diagnostics0, autres warnings conserves, log ba7d3185a2d0aaf9f5e4a62e20ffb9bab0546af50b4840520add436b9e123a7f.

VerifyComposite68481 terminal92ef79 compile0/Naga0 : onze bindings et spans/offsets camera160/volume64/light48/moon16 verifies, sourceSDK intacts. Ce harnais ne soumet pas au GPU et ne prouve aucun binding resident. Test pipeline88382 terminal4bb8f4 compile1 : lecture regex d'une seule ligne tronquait la declaration COMPOSITE_SHADER_HANDLE reformatee sur deux lignes ; erreur du harnais, pas moteur. Nouvelle extraction jusqu'au point-virgule, test46294 terminalcd13fd compile0/test0 sur passes261b1e3035c8bb1ba4bb97bc60b0eb8edd8219d5bf85f00b0d1f03b3e1ba0083. Check36722 terminal2799cf moteur/exemplemetadata0,1948sourcesSDK intacts V38-v2 ; logs df6522bb60ae9193edc8ef8e45e7cb2c9d090fe41ebc459a1aef8c9d507475ca /04578a0d8ec67cca9d81ac139d7cfa688e350cb75d758fc479d8c6c81b6fc033.

Lint93144 terminal5240e6 exit0 laisse un own too_many_lines114/100 dans init, loge4763b95e81fb93bd371897e948a54bf1134654037d695c6858c6a275eac86e4. Extrait uniquement le layout composite dans un helper, aucun allow ni changement de bindings. Rustfmt73984 terminaldaaf3c. Test final35802 terminal1d5853 compile0/test0/sourceSDK intacts, passesac5ab049b8eaa24abd54fd587c29e989e113443d435360be20e9e1a23e56f8d6. Check29545 terminal26c26b moteur/exemplemetadata0/1948sourcesSDK intacts, snapshotV38-v3 ; plugin93455926a9597d97a459d670b667d84acfd158ec67958d24273ac94ead9466f5, logs b82e1038cbf2c377499568a1c7deeea35dbfcf863f62c6452c91f8c34045e338 /561fb02f4e4416c8ce9f479d2b3b3d6537c384e2e21df0ba8527c7833a5e996f. Scoped rustfmt/check et diffcheck297169 PASS.

Lint final13262 lance ceb834, encore LIVE a cet instant : suivre le MEME handle jusqu'au terminal avant verdict. Pas de build lie ou lancement GPU V38, pas d'amelioration visuelle qualifiee. Producteur cloud trace.rs confirme write=view_index*2 et slot resident5+2*view_index ; frames sans trace n'ecrivent pas l'historique. submitted_frame est un snapshot soumis, pas une preuve d'encodage : dependants doivent adopter la vraie publication generation/step, sa camera et sa taille. Suite allocation/bindings par vue, export du milieu, publication coherente cloud history, preflight de toutes les passes avant resolve, activation et nouvelles captures appairees. Ecart lumineux natif/source et tout le scope carte/Studio/gameplay/native-WebGPU/performance/portable Aube restent ouverts. Objectif complet ACTIVE, aucun push, aucune cloture de gate, aucun processus utilisateur arrete.

V38 mise a jour terminale : Lint13262 terminalfc77c0 exit0/sourceSDK intacts/own diagnostics0, log0a20dd898556219aa46a0c7f6e53c2ea63b586b9f1d8d3d00d35c8e2c2c282e8. Autres warnings conserves, project_warning_free=false. Remplace uniquement le statut LIVE precedent ; tous les jobs propres de ce tour sont maintenant terminaux. Aucun GPU V38 execute.

### V39 : export reel du milieu de lentille raccorde au rendu, avant la brume

Tour V38 precedent progress : masque/composite HDR enregistres et programmes valides. Ajout de lens_medium_fragment dans le vrai shader underwater : appelle exactement lens_medium, et setup_lens_medium_frame remplace par setupUnderwaterHeightFrame dans le shader optical combine. Ancien composite sous-marin conserve ; pas de second solveur. Nouvelle specialisation Pass::{Composite,Shafts,Medium} avec layout medium distinct sans texture shafts obligatoire. Nouveau module medium.rs : cible R16Float par vue selectionnee ResolveEnabled, taille de texture principale revalidee contre limites appareil, generation resize remplacee avec receipt vide. Rend apres Gtao et avant Haze sur meme bus/tag HDR, lit image/depth/query/settings et groupe material optical prepare exact. N'utilise PAS post_process_write et ne change pas l'image principale. Receipt encoded_for exige taille et tag identiques ; il prouve des commandes encodees, pas une fence GPU ni l'appairage historique du water query. La brume ne consomme PAS encore cette cible.

CheckLensMedium14694 terminal6b2da8 moteur/exemplemetadata0,1949sourcesSDK intacts V39-v1 ; logs e1ac0100db7f8d4ae9d44dc125076f1336b9f158754a4a7f8f4a391a3a8e37f3 /ab86b6cfd3193090f1370cd8ce39dc0dd1379307c08923092a7b0ba0f0704ca2. Shader b4bb5f39f14ba7dec33383de72ad337e6eea3b31d6a75e3de082cc5419c2b879 ; underwaterRust aefd92ffecc10d5158208076c333007b38b3132723407cc1d6b5fadf138f1670.

VerifyLensMediumShader premierabc8d1 compile0/Naga valide mais validation_exit101 sur attente erronee de cinq bindings LUS. Logc6b675 montre exactement0/2/3/4 : visible_water_medium utilise textureLoad et aucun sampler, contrairement au composite. Layout partage garde slot1 sampler reel, declaration inactive autorisee ; aucune texture shafts lue. V2 session62861 terminal074f8e compile0/validation0/NagaOil+Naga et usages reels quatrebindings PASS, sourceSDK intacts ; aucune scene GPU ou optique combinee qualifiee par ce test standalone.

Lint77013 terminal6f27a8 exit0, sourceSDK intacts/log65232de540ce2eec4050f121e52c62b494e1afd62dda3d268076751595fba9e4. Le wrapper heritait du filtre haze, donc own_diagnostics0 n'etait PAS un verdict underwater. Inspection de TOUS les diagnostics7d7b33 trouve wildcard_imports medium.rs. Corrige avec imports explicites. Check81073 terminal207b0e moteur/exemplemetadata0/1949sourcesSDK intacts V39-v2 ; medium edd76bb468973567c003e56ea522a29b1b48ee0e6e442fa7be3976d84eee8ec1, logs0f7fdb62561c5081bfaa0ced34341f92a87b5e49c0cce20d63f1f9d7c7b41fe0 /8d5f79ab9a9cc3ece93527465100fe5397bbbe34dc9d200670f83bdad7e210a2. Scoped rustfmt/check et diffcheck843f57 PASS.

Lint final42095 lance49411b sur V39-v2 avec filtre EXPLICITE tidewater_underwater.rs et tidewater_underwater/. Encore LIVE a cet instant, suivre MEME handle. Aucun executable V39 lie/lance, export draw pas encore mesure sur GPU. Suite cloud-history publication generation/step/camera/slot coherente, allocation et bindings de brume par vue, preflight avant resolve, activation de la chaine volumetrique puis capture/test scene.

Instruction utilisateur de ce tour : pause propre UNE FOIS les nouveaux effets implantes ET testes. Interpretation annoncee a l'utilisateur : terminer activation brume/rayons complets et test dans la scene, puis mettre l'objectif en pause et fournir rapport de reprise. Cette condition n'est pas encore satisfaite ; objectif reste ACTIVE. Ne pas declarer pause ou qualification sur les seules validations CPU, ne pas poursuivre les autres scopes apres satisfaction de cette condition sans reprise utilisateur. Le but complet reste intact et les autres exigences restent ouvertes. Aucun push, publication ou cloture de gate, aucun processus utilisateur arrete.

V39 mise a jour terminale : Lint42095 terminal786527 exit0/sourceSDK intacts/underwater diagnostics0 avec filtre correct, logc61f2427fed359f48d45537aef6c6e474acfa5e96e7145f4140e8fb5d35c827a. Autres warnings conserves, projet warning-free=false. Tous les jobs propres de ce tour sont terminaux. Pas de GPU V39 execute ; condition de pause apres activation et test de la brume/rayons encore non satisfaite.

### V40-V41 : publication nuageuse apres encodage et ressources de brume par vue

Tour V39 precedent progress : passe export lens_medium en code et validations CPU. Condition utilisateur conservee : pause uniquement apres activation brume/rayons et test dans scene, pas encore satisfaite.

V40 nouveau sampling.rs : Camera decodee depuis les544octets clFrame REELLEMENT lies au trace (basis1/2/3, tanY et aspect0/1, historySize8ZW, clock10). Le produit tanX est celui des f32 du shader, pas une reconstitution double des params publics JS ; ce fait doit rester visible lors de future comparaison bit-exacte des public viewTan. Rejet tailles/slot/step/non-finite/frustum invalide. Les dimensions960x540 sont le contrat du graphe cloud existant, pas un fallback arbitraire. Temporal encode retourne un receipt seulement APRES son dispatch ; shadow le propage, compute publie alors camera/slot/handles/generation/step. FrameView step explicite pour bootstrap et live. Reset publication sur nouvelle generation ; frame sans tracing ne relabelle pas une texture avec la camera courante. encoded_for valide generation ready/handles et completed_steps>=traced step pour permettre les frames qui sautent le tracing. Pas de fence GPU, ni appairage avec image finale ou tag Haze revendique.

CheckCloudSampling36287 terminalff209e moteur/exemplemetadata0/1950sourcesSDK intacts V40-v1 ; logs a64085da7f82b6d4650dc2915bae86b525a3eebdd464b63ebbff2999ac5bfab0 /a3077460ffa471b1fbadcdb90371f4e0bfa8411d7ab6faadd2bd55a253ef533f. VerifyCloudSampling63427 terminal1538e1 compile0/tests0 deux tests reels PASS (decode/invalidation, slot et generations/skip tracing). Sampling SHAadd8c334b823967ca8ec5e96154e5c654ad62d63882067ddb67038e246fbaa3e ; type Targets retenu du vrai moteur V35 et definition actuelle verifiee identique, Bevy Assets/handles reels ; aucun GPU.

Lint20360 terminal098663 exit0 mais filtre du wrapper contenait un tuple a un seul element sans virgule : iteration des caracteres, own diagnostics n'etait pas un scope cloud fiable. Tous diagnostics relus ee0d11 : similar_names trace.rs (encoded/encoder) introduit, et too_many_lines validation.rs102/100 preexistant. Renomme uniquement encoded en latest_history, sans modifier les passes ni suppressions de lint. Check63294 terminal338d58 moteur/exemplemetadata0/1950sourcesSDK intacts V40-v2 ; trace de6fba098031cbe075cc8eaf53bb24aeb80a4cf41f257c6357006cd19c4aa72a, logs f4ec3e5739653e8d6bc6580988392e3eabd46c645b7b38e0aea6bb23b81107dc /40c2e07e8f6ba81f028e19f97a1ea180e594cd86677e89f1d7d0771f1029b4b9. Lint66350 terminalb745ce exit0/diagnostics des CINQ chemins modifies0 avec filtre correct, sourceSDK intacts/logc2b16b658701a72d776ef482b2e7df317cab7ae491f2da1b0eb637ed8930dbcc. Ancien warning validation.rs conserve. Scoped format/diff0c9431 PASS.

V41 : targets::prepare maintenant enregistre dans Render PrepareBindGroups. Cree et remplace les cinq CachedTextures selon ViewPacket.packet.sizes.ZW (sortie physique avant TAAU), source hazeScale1 et Math.round, valide limites du vrai RenderDevice ; retire la cible pour taille invalide. Moon Inputs extrait depuis les MeshMaterial3d<SkyMaterial> effectivement attaches et correspondant aux handles du ciel/transmittance de l'atmosphere active. Aucun premier asset choisi ; plusieurs materials correspondants distincts => aucun choix implicite. moon direction depuis settings.moon_disk.xyz et night depuis stars_time_night.Z (PAS W). Generation actuelle verifiee avant consommation. Ceci est selection/extraction CPU, pas preuve tag source ni GPU completion.

CheckHazeViewInputs78829 terminal21763d moteur/exemplemetadata0/1951sourcesSDK intacts V41-v1 ; plugin4bd3467ea4f8daea432a62a45a8024635bc86eb70154e446a9e8ca00a0a9ea80, targets39d04ea3eab7c29018600041c49cc9ae60888ea40a67c43fa9b0a2f548e4985b, moon4b5597aed5a5772e1a05b493527397e7bbe4e7bb9ee9f1eba9135e6aed71d82c. Logs4a44c64697091a9da0694effc663c83e299fb556b63178f726bd14d063c1e56b /af415aa8259cdc0b3065d92a24a5fd0479e18f213b0453238cde1ab96713bd44. Lint66297 terminal6639b7 exit0/own diagnostics0/sourceSDK intacts, logd6a3b4af1bef58cbd2a7c18d43f5813651f9014b9078661ecefe5d691005542a. Autres warnings conserves, projet warning-free=false. Scoped format/difff3011e PASS.

Tous jobs propres de ce tour terminaux ; aucun nouveau binaire lie ou GPU V40/V41 lance, aucune amelioration visuelle qualifiee. Toujours ancien render analytique HDR actif : nouvelles ressources presentes mais march/mask/radial/composite ne sont pas encore draws. Prochaine action : render volumetrique avec bindings reels et checks globaux AVANT post_process_write ; sourcehazeclock distinct (ni cloud clock, ni mapstamp, ni FrameTag.serial), sunUV/fade source et controle de generation/medium/tag. Resolve.rs955..962 confirme que chaine HDR incomplete et ConsumerPending sont refuses AVANT reservation/draw/phase, a conserver. Sonde finale scene apres build exact et guards16GiB puis pause demandee. Objectif complet ACTIVE ; aucune publication/cloture et aucun processus utilisateur arrete. Les autres scopes originaux restent ouverts et attendent la reprise apres cette pause conditionnelle.


### V42 — branchement effectif de la chaîne volumétrique (8 octobre 2026)

L’ancien système HDR analytique sélectionné a été remplacé par `render_volume::render` dans `SourceHdrSystems::Haze`. Le chemin hors Resolve reste inchangé. La vue prépare et encode march RGBA16, masque R16, trois flous radiaux R16 et composite RGBA16. La publication HDR Haze reste une seule contribution, enregistrée seulement après le composite. Les six draws réels utilisent le renderer Bevy existant.

Préflight avant permutation HDR : pipelines, tailles, classification de lentille du même FrameTag, générations d’ombres/optique/nuages/atmosphère et handles GPU effectifs. Le nuage emploie la caméra et le slot réellement tracés. La lune vient du matériau affiché. Les offsets uniformes caméra s’appliquent aussi aux nouvelles entrées radiales. Une horloge par caméra avance dans PostUpdate modulo 1024, indépendamment des compteurs clouds/TAA ; son alignement historique exact avec les captures source reste non prouvé. La projection solaire et son fade sont calculés avec l’éclairage GPU réel (arithmétique f32, contrairement au calcul JS CPU f64 source), sans délai de readback. Les passes écran sont encodées même quand le fade est nul, avec sorties nulles ; le source les omet alors.

Contrôles : métadonnées engine/exemple PASS sur 1953 fichiers intacts ; link engine/exemple PASS ; NagaOil/Naga composite et radial PASS ; SDK intact. Les deux validateurs shaders réutilisent des limites historiques annonçant encore adapter disabled/analytic only : ces champs sont obsolètes pour le branchement V42 et ne sont pas une preuve de runtime. Clippy : compilation PASS, une nouvelle alerte `too_many_lines` dans render_volume (230/100), avertissements préexistants conservés. Diff check ciblé PASS. Le diff check global rencontre des fins CRLF/espaces préexistants hors portée ; aucune correction globale.

Binaire isolé SHA256 : `c3a1ec4d8e50d268f01ae20d3f97ca10f3af9c1c282776452494bf1b210647f9`; rlib : `13f08d833a076d603d49c84a52e25a6e051669bc393aa5092f05d47f32fcde18`.

Test GPU lancé via `ProbeFullHaze20261008.py`, cas `native-source-hdr-final-probe-village-20261008-v42-v1`, PID 28644, session 94938. Deux mesures avant lancement : 20889948160 et 20873576448 octets de marge, chacune au-dessus de 17179869184. Aucune conclusion GPU ou visuelle à ce stade ; suivre ce handle, ne pas redémarrer sur silence. La pause demandée reste conditionnée à l’activation et au test de scène, pas aux seuls contrôles CPU.


#### V42-v1 échec GPU conservé ; correctif V42-v2

Le premier runtime PID28644/session94938 est terminé avec code1, aucune image. Erreur réelle wgpu lors de création des quatre pipelines : les entrées storage étaient LOAD|STORE alors que les shaders demandent LOAD. Bevy material 0.19.1 `binding_types::storage_buffer_sized` crée `read_only:false` ; son booléen signifie `has_dynamic_offset`, pas readonly. Correction ciblée : `storage_buffer_read_only_sized(false, size)` pour toutes les entrées storage des nouveaux pipelines, en conservant les offsets dynamiques des seuls uniforms caméra. Cela corrige également le mauvais offset dynamique des buffers storage.

V42-v2 métadonnées engine/exemple PASS, 1953 fichiers et SDK intacts ; sources snapshot `Tidewater-Validation-Snapshot-20261008-v42-v2`. La seule mutation depuis V42-v1 est passes.rs SHA256 `f779eda0ced6a90917d9ad544b35eca5935aabcfffae1534a736d36665a0c998`. Construction corrigée lancée par `BuildFullHazeV2_20261008.py`, session66771. Le runtime n’est pas relancé avant terminaison de ce build. Le test raté ne satisfait pas la condition de pause.


#### V42-v2 test de scène terminé — condition de pause atteinte

Build corrigé terminé code0 engine/exemple, SDK et 1953 fichiers intacts. Runtime PID47572/session9381 terminé code0 ; helper code1 parce que son verdict historique global reste faux. Aucun `ERROR` ni erreur de validation GPU dans le runtime. À 17:16:31Z, journal effectif « march + mask + 3 radial + composite », haze frame124/cloud step124 : égalité observée sur ce cas, pas preuve que ces horloges soient interchangeables. La capture finale a été ouverte et inspectée : village complet, maisons/palmiers/ombres/terrain présents, image exploitable. 134 copies GPU du final, contrôles et expositions finis ; exposition finale1.7677924633026123.

Image SHA256 `50882770fcb4124e18e44f62417a51fe1ed2e9916f2beefa25c841ab765019d1`, binaire `7e04c33efa1b456df3ff5251be27d7df36f8a3b2ecd1274d628f704c415e7c9e`. Différence RGB moyenne vsV35 : 3.0149935699588477 sur255 ; 2010472 pixels changés. Ceci prouve un changement de sortie, pas une amélioration ni une parité exacte avec la référence.

Preuve détaillée : `.tidewater-integration/native-source-hdr-final-probe-village-20261008-v42-v2/volumetric-haze-scene-smoke.json`. Vérifications historiques : {"runtime_graphics_values_verified": false, "runtime_acquired_pose_verified": false, "runtime_acquired_environment_verified": false, "runtime_acquired_ocean_verified": false, "source_final_native_submitted_verified": true, "source_final_gpu_trace_verified": true, "diagnostic_checks_pass": false}. Le test local activation/scène passe ; le diagnostic global, la qualification visuelle, WebGPU, gameplay, Studio et Gate3 restent ouverts.

Pause explicitement demandée par le propriétaire après implantation et test : ce jalon local est atteint. Ne pas poursuivre d’autres chantiers après passage de l’objectif en paused. Aucun nouveau build ou runtime laissé en cours dans cette tranche, aucune publication. Reprise : conserver V42-v1 échoué et V42-v2, corriger/resserrer l’alignement des horloges et la provenance d’éclairage, vérifier source-paired HDR/histoires, autres vues/jour/nuit/eau et paramètres rays/scale/godRays, puis reste du plan intégral Studio/gameplay/WebGPU/performance/portable. Réduire aussi render_volume (alerte Clippy230/100). La parité complète et l’objectif intégral ne sont pas déclarés achevés.


### Amendement propriétaire du 8 octobre 2026 — moteur générique

Les éléments liés au moteur doivent être intégrés aux systèmes communs et utilisables par les futures cartes. Leurs noms de modules, fichiers, types, API, shaders, paramètres et contrats moteur ne doivent pas contenir d’indicatif Tidewater. Les références Tidewater sont réservées au contenu de cette carte, aux acquisitions, aux scènes de référence, aux preuves et aux mentions de provenance/licence nécessaires.

Portée complète conservée ; exigence ajoutée au contexte canonique et au plan. La pause est maintenue, aucun code ou test lancé. Les outils de gestion d’objectif exposés permettent seulement le changement de statut, pas du libellé existant : le texte automatique reste inchangé. La demande propriétaire et cet amendement font autorité pour la reprise.


### V43 — reprise du 10 octobre 2026 ; réglages communs de brume

État objectif revalidé ACTIVE avant mutation. La pause du 8 octobre reste un événement historique ; l’exigence moteur générique demeure entière. Création `forge_engine::atmospheric_haze::AirHazeSettings`, composant indépendant du contenu, par caméra : densité, shafts, enabled, god_rays et resolution_scale. Valeurs non finies refusées sans détruire le réglage précédent ; limites GPU des dimensions conservées. Cette configuration renderer n’est pas une nouvelle représentation canonique de carte.

Les données GPU160 existantes ne portent plus l’API de réglage : required component générique et copie du réglage effectif dans les uniforms de caméra. Le rendu volumétrique lit réellement density/shafts/enabled/god_rays ; la préparation des cinq cibles et le préflight utilisent resolution_scale. Les trois radiaux conservent god_rays lors de la variation du pass index. Le shader masque/fade tient compte de god_rays sans couper la brume volumétrique. Exemple natif et raccord Studio modifiés pour insérer et éditer ce composant commun ; aucun appel `TidewaterAirHaze::new` restant dans engine/Studio. Pas de copies mutables concurrentes de densité : les données caméra sont dérivées.

Deux tests CPU du module produit passent (refus des valeurs non finies ; isolation entre deux caméras dans un World Bevy indépendant). Cette dernière preuve ne qualifie PAS une autre carte GPU. Métadonnées engine/exemple passent sur1954 fichiers intacts ; SDK inchangé. NagaOil/Naga du composite actualisé passe. Diff-check ciblé passe. Clippy exit0 ; aucune alerte nouvelle dans atmospheric_haze, alerte render_volume235/100 encore ouverte (230/100 avant cette tranche), avertissements préexistants conservés. Les validateurs réutilisés ont encore des descriptions héritées camera/frustum ou adapter disabled ; ne pas utiliser ces limites textuelles obsolètes pour attribuer un test caméra/GPU non exécuté.

La configuration est réellement consommée, mais les plugins/types/fichiers du backend, les shaders et plusieurs producteurs/contrats restent tidewater_*. L’exigence sans indicatif n’est PAS achevée. Les réglages supplémentaires disposent de l’API moteur ; leurs contrôles UI persistants complets et le check/run Studio/WASM restent à faire. Le profil Studio existant air_haze demeure sa source auteur pour la densité.

Construction native V43 lancée par BuildGenericHaze20261010.py, session74629 ; suivre ce handle et vérifier terminaison avant ProbeGenericHaze20261010.py. Test GPU V43 pas encore effectué ; V42-v2 ne prouve pas le candidat modifié. Aucun runtime lancé dans cette tranche pour l’instant. Ensuite : vérifier rendu par défaut, généraliser les contrats de ressources et les noms backend, tester une scène indépendante sur GPU/natif/WebGPU et poursuivre le plan intégral. Aucune publication, aucune nouvelle qualification Gate3.


#### V43 construction terminée ; lancement refusé par la garde mémoire

Construction session74629 terminée code0 engine/exemple ; sources et SDK intacts. Binaire SHA256 `0b004395ed3987a53e06e254dd85f1467c8e53e67fe15a6565206994110e9350`, rlib `0147aeeef55ff68b77857aa4335b449bc5be62367e96fcb1f584d145feb5d84d`. ProbeGenericHaze20261010.py session11086 terminé code1 avant runtime, sans processus GPU ni capture. execution.json confirme runtime_executed=false et launch_blocked_reason=Commit headroom below the 16 GiB native launch threshold. Mesure : 10774966272 octets de marge, seuil17179869184. Ce n’est pas un échec GPU du code : aucun lancement effectué. Seuil conservé, aucun processus utilisateur arrêté. Ne pas réutiliser le même dossier de cas pour un futur lancement ; préparer une nouvelle capture fraîche après revalidation stable de la marge.

Résultats V43 : compilation/métadonnées/link engine/exemple PASS ; 2tests CPU PASS ; NagaOil/Naga composite PASS ; Clippy compilation PASS avec warning de longueur backend conservé. Aucun verdict Studio/WASM, aucune nouvelle validation GPU ou comparaison visuelle. Le premier blocage mémoire ne justifie pas de marquer l’objectif bloqué : état ACTIVE conservé, objectif complet et amendement générique inchangés. Prochaine reprise : adapter les contrats de ressources et noms backend communs ; vérifier intégration réelle sur scène indépendante et natif/WebGPU ; reprendre la capture V43 sous la garde16GiB.


### V43-v2 GPU et V44 contrat de ressources commun — 10 octobre 2026

Le blocage mémoire précédent a été revalidé : 18069430272 octets disponibles lors du contrôle initial. Cas neuf native-source-hdr-final-probe-village-20261010-v43-v2, même binaire V43-v1. Gardes avant runtime : 17880887296 et 17900290048, chacune supérieure à17179869184. Session18163/PID37816 terminé : runtime0, helper1 car son diagnostic global historique reste faux. Aucun ERROR/validationGPU ; chaîne volumétrique réelle journalisée, 134 copiesGPU finales, valeurs/expositions finies. Capture ouverte et inspectée. Image strictement identique octet pour octet àV42-v2, SHA25650882770fcb4124e18e44f62417a51fe1ed2e9916f2beefa25c841ab765019d1. Preuve default-haze-regression.json du nouveau cas. Cela qualifie la non-régression locale des réglages par défaut sur cette vue de jour, pas les réglages modifiés, une autre carte, Studio/WebGPU ou la parité source complète.

V44 : le contrat de ressources a été déplacé dans atmospheric_haze::scene. Resources utilise uniquement les handles Bevy communs, propriétaire de type UntypedAssetId, domaine terrain et rectangle cloud ; aucune dépendance à LodMaterial ni indicatif Tidewater. Bindings/Current/Readiness/Inputs et la vérification des générations appartiennent maintenant au moteur commun. Le pipeline lit réellement cette ressource commune et ses textures/buffers ; le fournisseur actuel se limite à sélectionner les données du terrain et à adapter les identités des producteurs. Les checks de génération, handles, tampon lumière effectivement encodé et compteurs non nuls restent conservés. Aucun changement WGSL ni d’ABI des buffersGPU dans cette extraction.

Protection supplémentaire : publication exclusive par identité de fournisseur (TypeId renderer, jamais donnée persistante de carte). claim/publish/release empêchent le fournisseur du terrain d’effacer les données d’un autre fournisseur. Quand son terrain disparaît, il libère seulement sa propre publication. Les tests de déchargement précédents sont adaptés dans le dépôt ; leurs tests intégrés complets ne sont pas déclarés exécutés ici.

Trois tests CPU du module commun PASS : générations remplacées et lumière non encodée refusées ; domaines invalides et handles modifiés refusés ; fournisseur concurrent incapable de remplacer/décharger la scène courante, transfert après libération acquis. Ces tests n’importent aucun producteur Tidewater et utilisent de vrais handles Bevy, mais ne sont pas une scène indépendante GPU. V44-v1 puis V44-v2 métadonnées engine/exemple PASS, 1955 sources/SDK intacts. V44-v2 Clippy exit0, aucun diagnostic atmospheric_haze ; warning backend render_volume239/100 toujours ouvert (235 avant). Diff ciblé et rustfmt scoped passent. Modules communs et snapshot retenus ; aucune publication Git.

Restes : pipeline/plugins/types/shaders nommés tidewater_* et producteurs spécialisés à généraliser ; état courant des graphes encore fourni par l’adapter historique, scène indépendante sur GPU pas testée ; paramètres UI/persistance complémentaires et Studio/WASM ouverts. Le test GPU V43 ne qualifie PAS le contrat V44 modifié. Aucun build link ou runtime V44 lancé dans cette tranche ; aucun processus d’essai laissé actif. Prochaine étape : réduire/couper le backend de rendu, généraliser ses entrées de caméra/ciel/nuage et le contrôle des générations de producteurs, puis construire V44 pour capture sous gardes et vérifier une scène indépendante. Objectif complet maintenu ACTIVE ; aucune clôture ni Gate3 qualifiée.


### V44 link et suppression propriétaire du seuil mémoire — 10 octobre 2026

Session65688 terminée : engine/exemple0, 1955 sources intactes, SDK intact ; rlib443849763f71b548220a27bbf8be3e0ce2cdf9053e4f655f64be2e3f64ded082. Premier essai V44-v2/session79652 refusé avant GPU : marge12883587072, runtime_executedfalse. Le propriétaire révoque ensuite explicitement la limite16Gio. Onze scripts actifs corrigés, analyse syntaxique Python PASS, acquisitions/natif/audits ne font plus de ce seuil une condition. Mesures informatives conservées ; aucun ancien reçu modifié. Inventaire SHA : .tidewater-integration/launch-memory-threshold-removal-20261010.json.

Extraction supplémentaire en cours : horloge de haze dans atmospheric_haze::clock, sélection par AirHazeSettings plutôt que composant historique. Backend encore raccordé via réexport temporaire ; nouveaux tests ECS indépendants écrits, pas encore exécutés. Cette modification est postérieure au binaireV44 et n’est pas qualifiée par son test.
