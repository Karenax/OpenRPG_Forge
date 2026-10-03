# Chargement vérifié du paquet de lampes dans Studio

Suite du [transport GPU](2026-10-03-source-light-material-upload.md).
Cette tranche prépare le raccordement du paquet aux placements ; elle ne
projette pas encore les lampes et ne qualifie pas leur rendu visible.

Le candidat `forge.imported-building-library` version 1 accepte un nouveau pin
optionnel `source_local_lights_sha256`. Son absence conserve le comportement
des bibliothèques existantes. Son chargement utilise le transport local borné
déjà employé par les bâtiments, à `forge-imports/<pin>/manifest.json`, puis le
lecteur asynchrone SourceLocalLightsPackage pour les membres du manifeste fermé.
Le lecteur vérifie chemins, limites, tailles, hashes, provenance et licence.
BuildingLightBindings vérifie ensuite le commit commun, les identités catalogue
et les ancres source ; une association invalide refuse la publication.

Le ticket de génération est contrôlé avant chaque lecture de membre et après
le chargement. Une dernière vérification précède la publication groupée des
paquets. Le paquet de lampes est publié seulement avec les bâtiments vérifiés,
jamais pendant les téléchargements. L'adapter de scène conserve ce paquet
immuable dans VerifiedLocalLights et retire la ressource si la bibliothèque
publiée n'en fournit pas. Cela ne crée ni owner, ni lampe ECS, ni horloge.
Le chargement échoué conserve la précédente bibliothèque publiée, comme le
chemin existant des bâtiments ; aucun paquet partiel n'est publié.

Validation lancée sur le snapshot local studio-light-loader-source-20261003-v1 :
1339 fichiers de code et fixtures de compilation, deux fichiers possédés, garde
initiale PASS. Ce snapshot ne représente pas le dépôt complet, ses documents,
ses bibliothèques ou une qualification de gate. Le check Clippy WebGPU du frontend
est en cours ; aucun PASS compilateur ou parcours Studio n'est encore déduit.
Résultats : `F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v1`.

Le candidat déployé n'a pas été modifié pour ajouter le pin, aucun paquet n'a
été installé dans l'application courante, et aucun nouveau binaire portable
n'a été livré. La prochaine étape doit projeter et réconcilier les lampes sur
les racines vivantes, conserver les identités lors des retouches/copies/retraits,
collecter après propagation et installer le transport sur le renderer Studio.
Le publisher solaire et les autres familles d'owners restent ouverts.

## Préparation de la réconciliation des retouches

PreparedPlacementLights expose désormais une opération replace : elle exige
le même ID d'instance canonique que la projection résidente, résout à nouveau
la racine dans ResidentScene et vérifie le document, son Transform et l'unicité
de ManualPlacementProjection. Elle délègue ensuite la transaction au projection
générique, qui valide l'ancien inventaire et les nouveaux owners avant mutation.
Le même chemin de résolution est réutilisé pour stage. Il ne modifie aucun
document auteur, paramètre source ou identité d'asset.

La sonde CPU/ECS sur les vrais paquets est prolongée pour refuser le remplacement
d'une copie par une autre instance, refuser un ancien document après une retouche,
puis déplacer la copie de trois mètres en X et deux en Y et vérifier les positions,
phases et la survie de la copie après retrait de l'original. Ces vérifications sont
ajoutées, mais pas encore exécutées : le target de validation partagé est occupé
par le check WebGPU de Studio. Un nouveau snapshot building-edit-v1 est préparé
(938 fichiers figés, 24 fichiers possédés, garde initiale PASS), avec son runner
tests/Clippy/sonde. Il ne sera exécuté qu'après la terminaison du check en cours.
Cette API n'est pas encore appelée par le reconciler Studio.

L'inspection du reçu d'acquisition déjà fermé révèle quatre invocations
`buildShed`, ordinals 17 à 20, avec `root_scopes: 1` et `emitted_lights: 0`.
Le hash du manifeste et celui de l'acquisition référencée ont été revérifiés
avant cette lecture. Le tableau building_invocations conserve leurs poses et
leurs ordinals, mais pas le source_object_id explicite fourni aux owners des
lampes non vides. Ces zéros observés sont une base pour une attestation liée
à l'identité catalogue ; ils ne sont pas encore acceptés par BuildingLightBindings.
Il faudra conserver ce lien explicite dans l'export/lecteur, plutôt que déduire
zéro lampe de l'absence d'une entrée editable_ownership.

## Acquisition avec identités des invocations vides

L'export conserve désormais source_object_id et anchor dans chaque invocation,
selon la même identité déjà employée par les owners lumineux. La nouvelle
acquisition isolée termine avec retour 0 ; le checkout source reste propre.
Ses douze scripts figés sont inchangés après exécution. Les 23 identités sont
distinctes, avec quatre invocations explicitement vides. Le paquet garde
60 lampes, 39 frames et les 212 sources originales. Manifest SHA-256 :
`2501e3644e420c0c0d854c74bf2f82fa4ab59c50d9f04083f9d2277d04bae00d`.
Résultats :
`F:/OpenRPG_Forge/.build/source-local-lights-empty-building-owners-20261003-v1`.

Le lecteur Rust de paquet existant termine avec retour 0 (binaire
`573a03bfc5792572437d8844e63e06054b1e91a8b5165ae2a889189baf10f3a6`).
Il n'exécute pas le nouveau lecteur des attestations vides. La comparaison JS
échoue d'abord car la baseline fournie contient déjà les owners (0 rebound
contre 52 attendus). Cet échec de protocole est conservé. La reprise avec la
capture initiale passe : positions/photométrie conservées, 60 phases exactes
entre JSON et Rust, 60 owners résolus. Les 60 phases diffèrent de cette première
capture ; la reacquisition aléatoire reste non reproductible. Ce candidat
n'est pas installé et ne remplace pas les paramètres du paquet actuel.

Le nouveau lecteur typed lit les octets originaux du reçu, pour refuser les
champs dupliqués sans passer par Value. Il vérifie commit, source/hash vérifié,
claims de construction inchangée, ordinals contigus, identités uniques et
anchor construction-root avec une seule portée racine. Le join catalogue
compare aussi la pose, le nombre de lampes et le producteur des owners lumineux.
prepare_optional peut alors retourner None pour un zéro explicitement attesté ;
les révisions inconnues et les anciennes acquisitions sans identités vides
conservent leur refus. Un test ajoute onze cas invalides, un champ dupliqué et
le refus d'inférer une attestation depuis un ancien reçu.

Ces modifications ne sont pas encore compilées ni exécutées. Le snapshot
building-edit-v2 est figé à 939 fichiers avec 26 fichiers possédés, garde
initiale PASS ; il contient la sonde de retouche prolongée et la couverture des
quatre zéros attestés. Les validations de building-edit-v1 n'ont pas été lancées.
Le check WebGPU de Studio reste actif sur son snapshot antérieur et conserve
la priorité sur le target partagé. La qualification de ces nouvelles API et
leur raccordement dans le reconciler restent ouverts.

## Identités enrichies avec les phases du paquet existant

Le helper `scripts/tidewater/enrich-building-light-identities.mjs` crée un paquet
dérivé du reçu original. Sa première approche tentait d'associer deux captures ;
elle refuse leurs digests globaux différents et ne produit pas de paquet. Cette
différence entre acquisitions demeure inexpliquée et ne qualifie aucune parité
géométrique entre elles. Les observations source des constructions fournissent
déjà, dans le reçu original, les ordinals et le nombre de lampes des appels vides.
Le helper courant dérive donc leurs IDs selon le contrat d'identité de l'importeur,
vérifié sur tous les owners lumineux de cette même capture : ordinal, source ID,
producteur, anchor et nombre de lampes. Il ne lit plus la seconde acquisition et
ne fusionne aucune de ses données. Le reçu dérivé conserve hashes parents et hash
du helper, en déclarant cette transformation de métadonnées.

V2 refuse aussi une hypothèse trop restrictive sur root_scopes : des maisons
lumineuses utilisent plusieurs portées de construction. V3 accepte ces portées
non vides, bornées, tandis que l'attestation de zéro exige toujours une seule
portée. Le lecteur Rust et son test sont corrigés de la même façon ; la liaison
des owners lumineux au premier frame et aux poses catalogue reste vérifiée.
Les refus des tentatives v1/v2 restent conservés, avant création des outputs.

Le dérivé v3 passe le lecteur de paquet Rust existant, sans exécuter encore le
lecteur d'attestations. Son manifeste a le SHA
`aecf137b54cc6e7d84501ed9deaf4fb41f47bf41144551064ec49770ae7f315f`.
Une comparaison indépendante des fichiers confirme que les 213 membres autres
que le reçu sont identiques au paquet vendor-owners, notamment le descripteur
`a395e666532bec49ea85612017bcc1b30480e60dca275184cbd05025ec7fd290`.
Les 60 phases, tous les paramètres des lampes, les sources et la licence sont
donc conservés octet pour octet. Le dérivé fournit les 23 identités explicites
et quatre observations vides ; aucune bibliothèque active n'est remplacée.
Résultats :
`F:/OpenRPG_Forge/.build/source-local-lights-preserved-phase-identities-20261003-v3`.

Le snapshot building-edit-v3 reprend les corrections et ce dérivé pour le prochain
essai natif. V1/v2 n'ont pas été exécutés ; la compilation WebGPU de Studio reste
active. Le check de formatage est disponible, mais aucun PASS des nouvelles API,
des cas négatifs ou de la sonde de retouche n'est annoncé avant leur exécution.

## Résultats terminaux du candidat building-edit-v3

Le contrôle natif ciblé termine avec 20 tests PASS (602 filtrés). Clippy natif
sur la bibliothèque et la sonde termine avec retour 0 : 29 diagnostics existants,
aucun dans les 26 fichiers possédés. La garde finale vérifie les 939 fichiers
figés et les 26 fichiers possédés dans le worktree, sans différence. Elle ne
couvre pas les modifications étrangères au périmètre.

La sonde ECS réelle termine avec retour 0, binaire SHA-256
`5db3b6a5f97c1cc6e912d4b26b55e967885e30f3c1a91cc87591d84e2f021bc7`.
Avec le paquet dérivé `aecf137b54cc6e7d84501ed9deaf4fb41f47bf41144551064ec49770ae7f315f`,
elle vérifie 19 bâtiments lumineux, 32 lampes, les quatre constructions vides
explicitement attestées et aucune identité bâtiment non résolue. Copie,
suppression de l'original et remplacement après déplacement canonique passent ;
les remplacements inter-instance et depuis une ancienne pose sont refusés.
Les phases sont conservées et l'erreur maximale de pose est
`3.0627995499799e-6` m. Ce résultat ne projette aucune géométrie, n'exécute pas
Studio ni le GPU et ne prouve aucune parité de rendu.

Résultats :
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261003-building-edit-v3`.

Le contrôle WASM Studio v1 termine avec E0433 : le derive Resource attend
l'alias `bevy_ecs`. V2 termine avec E0252 : cet alias a été ajouté deux fois.
Les deux échecs sont conservés. Le worktree retire uniquement le doublon ;
un candidat v3 distinct est préparé pour rejouer le contrôle. Aucun PASS WASM
du nouveau loader ni raccordement effectif des lampes dans Studio n'est encore
déclaré.

Le retry Studio WASM v3 termine ensuite avec retour 0. La garde finale passe
sur 1341 fichiers figés et neuf fichiers possédés dans le worktree. Résultats :
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v3`.
Cela qualifie la compilation du loader avec l'alias corrigé, pas son activation
dans la bibliothèque déployée ni l'affichage des lampes dans Studio.

## Reconciler des placements Studio en validation

Le module imported_scene/local_lights.rs dérive ses emitters du paquet vérifié
et des ManualPlacement courants. Il prépare les liaisons du catalogue, refuse
les IDs dupliqués et utilise les racines canoniques validées par le moteur.
Chaque premier exemplaire d'un asset conserve les ordres de la source ; les
copies ont des plages disjointes, conservées pendant les retouches. Aucune
phase n'est tirée à nouveau. Lors d'un changement de paquet, de document ou de
racine, la projection précédente est retirée sans enlever les racines auteur.
En cas de refus, les projections de cet adaptateur sont retirées et la collecte
caméra est invalidée avant toute mutation. Les identités étrangères modifiées
restent protégées par le retirement moteur et ne sont pas supprimées.

Le reconciler attend la propagation normale des nouveaux enfants avant de
publier la readiness. Il s'arrête et retire ses projections quand le viewport
ne peut pas projeter les sources. La readiness des bâtiments inclut désormais
cette étape uniquement lorsque le candidat contient un paquet lumineux ; les
anciens candidats sans paquet gardent leur parcours.

Le snapshot Studio v4 contient ce module, l'adaptateur caméra et la sonde GPU
refactorisée : 1342 fichiers, 14 fichiers possédés, garde avant lancement PASS.
Le contrôle WASM est lancé dans
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v4`.
Le publisher solaire, la collecte/upload du produit, l'activation du paquet
dans la bibliothèque déployée, les gestes Studio et le dessin restent ouverts.

V4 termine avec E0599 sur acquisition_bytes, alors que cette méthode est bien
présente dans package.rs du snapshot et du worktree. Les artefacts WASM de
forge_assets précèdent ce lancement. La réutilisation d'une interface plus
ancienne par Cargo est une hypothèse à vérifier, pas une absence constatée du
code ni un verdict sur l'API. V5 copie les sources dans un nouveau candidat et
rafraîchit leurs mtimes avant lancement, sans changer leurs octets pour cette
opération. Aucun cache partagé n'est supprimé. Le reçu freshness-preparation.json
documente cette préparation. Le reconciler v5 attend aussi ResidentScene
synchronisé avant staging : les racines recréées après propagation restent
pending plutôt que de produire un refus de paquet.

Validation v5 :
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v5`.
Les échecs et snapshots antérieurs restent conservés.

V5 termine avec retour 0, garde finale PASS (1342 fichiers / 14 possédés).
La reprise recompile les dépendances Forge et ne reproduit pas E0599 ; cela
étaye l'hypothèse d'interface en cache, sans changer l'API. Six avertissements
possédés restent : cinq anciens Result retournés par RefCell::replace ignorés
et la taille de reconcile. Le worktree publie désormais la readiness par
affectation et extrait l'allocation des plages de copies dans registration_base.
V6 reprend ces corrections, avec un contrôle WASM puis Clippy et sonde GPU
natifs séquentiels sur le même candidat. La sonde reconnaît aussi les quatre
constructions sans lampe explicitement attestées et crée leurs racines ; elle
conserve le refus des anciennes observations sans identité.

Résultats v6 en cours :
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v6`.
Les 20 tests moteur précédents ne sont pas présentés comme rejoués : seuls le
reconciler Studio et les helpers de la sonde ont changé depuis leur PASS.

Résultat v6 : compilation/Clippy Studio WASM retour 0, aucun diagnostic possédé.
Clippy et construction natifs retournent aussi 0, avec un avertissement restant
sur la taille de main dans la sonde GPU (102 lignes). Les six lectures GPU passent
sur les 23 racines canoniques : 19 lumineuses, quatre explicitement vides,
32 lampes source et aucune identité bâtiment unresolved. Le binaire a le SHA
`d05d6111c45f381f40dc3c1ca25060f3a514a3324ec99af34d42ac132233b30d`.
Les octets sélectionnés restent identiques (60107ade). Aucune géométrie n'est
dessinée par cette sonde ; ce résultat n'exécute pas le reconciler dans Studio.

Le worktree ajoute ensuite la collecte après synchronisation : une seule caméra
Camera3d active, exclusion Capture/Preview, inventaire des résidents trié par
ordre source, delta réel Bevy et night du SkyMaterial présent dans la scène.
Ce champ z est le même que celui traduit par prepare-sky.py vers frame.night.
La collecte s'invalide d'abord à chaque tick ; refus, caméra ambiguë, ciel absent
ou enfants non propagés maintiennent le buffer neutre. L'installation réinitialise
la readiness entre Apps et raccorde l'upload au RenderApp existant. La torche
reste désactivée, sans nouveau raccourci clavier à ce stade. Le night lu est
le f32 du ciel rendu ; le publisher solaire complet et sa valeur f64 partagée
restent à raccorder pour la qualification des transitions du crépuscule.

V7 reprend ce collecteur et le staging de bibliothèque pour validation WASM ;
le PASS v6 ne qualifie pas ces ajouts. Les sources v6 restent figées ; leur
comparaison au worktree reflète désormais cette nouvelle version du module.

prepare-building-library-site.py accepte maintenant un paquet lumineux et son
pin explicites. Il vérifie manifeste, commit commun et chacun des 214 membres
avant copie, préserve les chemins imbriqués et ajoute source_local_lights_sha256
au candidat. Un staging isolé termine avec retour 0 (821 membres bâtiments,
214 membres lumineux), puis le lecteur Rust existant vérifie le paquet copié
avec retour 0. Résultat :
`F:/OpenRPG_Forge/.build/source-local-lights-library-site-20261003-v1`.
Ce staging ne contient pas les autres paquets du candidat complet et n'est pas
installé dans le serveur, le Studio actif ou le portable.

V7 termine avec retour 0 en WASM, 29 diagnostics préexistants et aucun possédé.
La garde finale passe : 1342 fichiers figés et 16 fichiers possédés, sans dérive.
Ce contrôle compile le reconciler, la collecte caméra/ciel et l'installation
du transport. Il ne lance pas leur schedule dans Studio, ne vérifie pas encore
le cycle de playtest et n'affiche aucun pixel des lampes.

Le staging suivant reprend uniquement forge-imports du frontend 283, dont le
candidat fournit aussi environnement, terrain, plantes, stand, océan, surface,
simulation côtière, rebond, passerelles, quais et place. Chaque fichier est
comparé à son pin dans le reçu du frontend avant copie ; les copies sont
revérifiées. Le candidat conserve ses champs et ajoute le pin lumineux. Cette
opération prépare un répertoire distinct, sans écraser le frontend parent ni
déployer le WASM 283 comme s'il contenait le nouveau code. Résultat en cours :
`F:/OpenRPG_Forge/.build/source-local-lights-library-site-20261003-v2`.

Le staging v2 termine : les 1532 fichiers forge-imports du frontend parent
correspondent aux pins et aucun membre supplémentaire n'est présent avant
copie. Les 1531 membres autres que buildings.json restent identiques après
copie ; ce JSON conserve tous ses champs et ajoute seulement le pin lumineux.
Le répertoire lumineux copié conserve chaque hash. Le stage-receipt.json
enregistre le hash du reçu parent, les pins conservés et le nouveau candidat.
Aucun serveur n'est modifié. Au moment de l'inspection, seul le serveur 1421
du frontend jfr1 est trouvé parmi les ports 8848/1421 ; 8848 n'écoute pas.

La construction réelle du WASM v7 est lancée séparément des checks Clippy dans
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v7/web-build-v1`.
Elle utilise le même snapshot figé et les dépendances offline, profil de
validation sans optimisation. Aucun binaire livré ni verdict de performance
ne peut être déduit de cette construction en cours. Bindgen, assemblage avec
le staging complet, backend natif et essai de scène restent à faire.

Le script d'assemblage est maintenant sauvegardé dans
`web-build-v1/Assemble-Frontend.py` (syntaxe Python vérifiée). Il exige le retour
terminal 0 du build et un unique artifact WASM déclaré par cette invocation
Cargo. Avant création du frontend, il vérifie tous les fichiers du snapshot,
le CLI bindgen épinglé (0.2.127), les membres exacts de la bibliothèque v2,
le HTML parent et les deux assets statiques issus du snapshot courant. Il
génère les nouvelles liaisons, recopie les ressources vérifiées, recalcule les
trois SRI HTML et écrit les pins individuels de la sortie. Le reçu conserve
explicitement frontend-build-only et les champs de rendu/Studio/parité faux.
Cette préparation n'a pas encore exécuté bindgen ni produit le frontend :
le build est toujours actif, avec progression entre dépendances confirmée.

Le mode --validate-inputs de ce script est ensuite exécuté avec retour 0 :
1342 fichiers source, 1747 membres de bibliothèque (baseline conservée plus
paquet lumineux) et neuf fichiers du kit gouverné sont vérifiés. Il contrôle
également le HTML parent et l'identité/version du CLI bindgen. Le reçu
web-build-v1/assembly-inputs.json porte explicitement bindgen_executed=false,
wasm_build_qualified=false et studio_executed=false. Il ne lit pas un ancien
artifact comme remplacement du build actif. Les vérifications sont reprises
avant l'assemblage final, qui exige toujours le retour terminal du build.

Le script de candidat Tauri existant exige un frontend construit, le pin exact
du WASM traité, un kit gouverné explicite et un output neuf sur E:. Il distingue
ValidateOnly, BUILD_ONLY et runtime_verified=false. Son étape native utilise
le checkout où se trouve le script ; le snapshot de source WASM seul ne doit
donc pas être présenté comme une qualification du backend actuel. Les sources
backend et leurs pins devront être vérifiés séparément avant/après construction.

Le snapshot v8 contenant le contrôle d'intention temporelle et l'extraction
de save_report termine sa préparation avec retour 0. La garde du
2026-10-03T00:48:47Z vérifie 1342 fichiers figés et 16 fichiers possédés,
sans différence. Le runner natif est prêt dans
`F:/OpenRPG_Forge/.build/studio-light-loader-validation-20261003-v8` ; il n'est
pas encore lancé, car le target partagé reste occupé par le build WASM v7.
Ce PASS est une preuve d'intégrité, pas de compilation ou de rendu v8.

Les scripts web-build-v1 du v8 sont également préparés : Run-Build.ps1 reprend
le snapshot v8, et Assemble-Frontend.py exige son propre reçu terminal Cargo
avant de créer studio-light-frontend-20261003-v2. La syntaxe Python passe ;
le script a le SHA
`34650b1a543e24805f20cf0ca6f94e7e5fe70dab900d14ede20af94614fc4c91`.
Son contrôle --validate-inputs est lancé séparément pendant la compilation
v7 ; ni le build WASM v8 ni bindgen v8 ne sont encore exécutés.

Le contrôle d'entrées v8 termine avec retour 0 : 1342 fichiers source,
1747 membres de bibliothèque et neuf fichiers de kit vérifiés. Bindgen,
qualification du build WASM et exécution Studio restent explicitement faux.

Une condition supplémentaire du constructeur courant est identifiée :
Build-StudioCandidate.ps1 exige les trois artifacts du worker de navigation
et son manifeste, avec pins source comparés au checkout. Le frontend parent
283 ne contient aucun de ces fichiers ; reprendre uniquement ses sept fichiers
hors forge-imports ne satisfait donc pas ce constructeur. La construction
release du worker est lancée via Build-NavigationWorker.ps1, avec output et
target distincts dans
`F:/OpenRPG_Forge/.build/studio-light-navigation-worker-20261003-v1`.
Le script vérifie avant/après l'inventaire et les hashes de ses entrées ; ce
résultat restera build-only, sans preuve d'exécution de navigation. Ses artifacts
devront être intégrés et épinglés après le retour terminal, avant validation
des entrées du candidat natif. Aucun frontend actif n'est écrasé.

Attach-Worker.py est préparé à côté du build du worker ; syntaxe Python PASS,
SHA `d234748d4ca7d90ceff2d840eef35b11d97f6c62e87916541184c8cfb5147b9a`.
Il exige le retour terminal 0 du worker, son manifeste release et un reçu
frontend-build-only terminé. Il compare les pins du worker à l'inventaire
source courant et vérifie l'inventaire exact du frontend parent avant toute
création. Il conserve les membres parent et ajoute les trois artifacts et le
manifeste dans un nouvel output, puis les revérifie et émet de nouveaux pins.
Cette étape n'est pas encore exécutée. Ajouter ces fichiers ne prouve ni leur
invocation par l'interface ni un comportement de navigation dans le produit.

Run-Native-WhenReady.ps1 est lancé pour enchaîner la validation v8. Il attend
le processus Cargo original v7 (PID 39224 confirmé actif à son lancement),
puis exige son reçu terminal wasm-build avec retour 0. Il ne relance pas ce
build et refuse une fin sans reçu. Il vérifie les sources v8 avant les tests,
Clippy et les readbacks, puis après leur exécution ; le runner natif conserve
son refus d'utiliser un target encore actif. Au lancement, cette continuation
est en attente et ne constitue pas un résultat des tests v8.

La construction du worker termine avec retour 0 : release optimisé, 5 min 46 s
de compilation Cargo, puis bindings générés et manifeste écrit. La vérification
avant/après des sources passe. Le répertoire frontend-worker contient les
artifacts épinglés requis par le constructeur ; runtime_verified reste faux.
L'assemblage avec le frontend lumineux n'est pas encore exécuté, car le build
Studio est toujours actif. Le worker construit seul ne qualifie pas le routage
Studio, le playtest ou la navigation de la carte.

Le worker construit est ensuite exécuté réellement dans le navigateur intégré,
sur le serveur local isolé 127.0.0.1:17743. Les trois artifacts sont copiés après
comparaison avec leurs pins. Un sol synthétique de deux triangles utilise des
valeurs exactement représentables en f32 et le hash fournisseur de ce build ;
le hash géométrique est calculé selon la sérialisation du contrat Rust.
Le verdict lu dans l'AX de la page est PASS : JSON malformé refusé, tuile
générée avec deux polygones et deux meshes de détail, hash corrompu refusé,
puis géométrie identique régénérée dans le même worker avec nouveau token.
Le détachement du buffer transféré est vérifié après chaque postMessage.
browser-proof.json conserve le verdict observé ; browser-evidence.json épingle
les entrées de la page et les artifacts. Le manifeste build-only original
reste inchangé. Ce smoke navigateur n'exécute ni Studio, ni Rapier/Tidewater,
ni sélection de tuiles en playtest, ni parité visuelle.

Run-Assemble-WhenReady.ps1 est lancé dans web-build-v1 du v7, pendant la
compilation réelle de forge_engine. Il attend le même Cargo original et exige
son retour terminal 0, puis exécute Assemble-Frontend.py et Attach-Worker.py.
Les sorties restent distinctes : studio-light-frontend-20261003-v1, puis
studio-light-frontend-with-navigation-20261003-v1. Le reçu de continuation
indique source_candidate=v7 et includes_v8_temporal_intent_fix=false ; ce
candidat ne doit pas être présenté comme la correction v8. L'assemblage n'est
pas encore exécuté à ce stade, et le backend natif n'est pas construit.

Le build WASM v7 termine avec retour 0 le 2026-10-03T01:12:22Z, après
60 min 22 s de compilation Cargo. L'assemblage est effectivement tenté et
échoue à bindgen : le module brut de 444960376 octets dépasse la limite du
parseur de 1000000 fonctions (offset 0xffb6). Le message est conservé dans
studio-light-frontend-20261003-v1/bindgen.stderr ; ce répertoire partiel ne
constitue pas un frontend utilisable. Aucun worker n'y est intégré.

La continuation native v8 démarre après ce retour terminal 0 ; sa garde
initiale passe à 2026-10-03T01:12:27Z (1342 fichiers figés, 16 possédés).
Elle est encore active au moment de la préparation suivante.

La reprise d'assemblage utilise un build V8 en release optimisé, conservant
le correctif, dans web-release-v1. Run-Build.ps1 passe explicitement --release
et Assemble-Frontend.py exige cet argument dans le reçu. Sa syntaxe passe,
SHA `05fceb93c7a13cda2b2f9857a9019355a2860fa9ac683634e0dfe952429eddd4`.
Les anciens scripts et l'échec non optimisé sont conservés. Les nouveaux
outputs prévus sont studio-light-frontend-20261003-v3 et
studio-light-frontend-with-navigation-20261003-v3.

Run-Release-WhenReady.ps1 est lancé et attend la continuation native V8
(PID 14636 confirmé actif). Il exige son retour réussi, contrôle les sources
avant/après le build release, puis assemble et intègre le worker. Le build
release n'est pas encore exécuté à ce stade. Aucune réduction de fonction
ou limite de parseur modifiée n'est utilisée pour contourner l'échec.

Inspect-WasmFunctions.py lit les sections du module V7 sans le modifier :
1051895 fonctions définies, ce qui confirme le dépassement signalé par bindgen.
Le reçu wasm-section-inspection.json conserve tailles et offsets ; il ne
prétend pas être une validation complète du WASM.
L'assemblage release V8 est renforcé avant exécution : il exige aussi un unique
artifact forge_studio_frontend de profil opt_level=3, sans debug_assertions ni
profil test, et conserve ce profil dans son reçu. Le seul argument --release
ne suffit donc pas à déclarer le module optimisé. Sa syntaxe est revérifiée.
Le SHA après ce renforcement est
`a51daeb7070f41cbf40c371d0bf5df18ef2fd608f00b38411412cdaa48b9d761`.

La continuation native V8 termine avec retour 0 le 2026-10-03T01:30:37Z :
21 tests, 29 diagnostics Clippy hors des fichiers possédés (zéro possédé),
sonde construite et six lectures GPU PASS. La garde finale reste sans différence.
Le rapport de transport détaille les pins et les limites du test. La continuation
release reprend après une nouvelle garde PASS à 01:30:43Z ; son rustc actif
est ensuite observé dans syn sur le target dédié partagé. Le build release,
bindgen et l'assemblage V8 n'ont pas encore de résultat terminal.
## Enchaînement vers le shell natif V8 — préparation du 3 octobre

Le coordinateur natif est lancé dans la session locale `76590`, processus
`44244`, avec
`F:/OpenRPG_Forge/.build/studio-light-desktop-20261003-v1/Run-AfterRelease.ps1`
(SHA-256 `229dd2a3170eb320fbb24c774186ffa2ebf091437badcd71e95b581579cd6021`).
Il attend le coordinateur release `46108` en contrôlant son chemin de commande
et son instant exact de création ; l'attente seule ne prouve aucun build natif.
Il exige ensuite le résultat réussi de la release V8 et le reçu du frontend
avec worker, vérifie les tailles et empreintes du staging, puis exécute
`Build-StudioCandidate.ps1 -Authoring -ValidateOnly` avant le vrai build Tauri.

Une nouvelle jonction possédée
`E:/OpenRPG_Forge/.tidewater-integration/studio-light-desktop-20261003-v1`
pointe vers `F:/OpenRPG_Forge/.build/studio-light-desktop-20261003-v1`.
Elle conserve le chemin relatif nécessaire à `frontendDist`, avec les nouvelles
sorties physiques sur F ; aucune ancienne sortie n'est déplacée ou supprimée.
Le candidat prévu est `attempt.0/forge-studio-candidate.exe`.
Le cache natif réutilisé est `F:/OpenRPG_Forge/.build/cargo-target`, distinct du
target release V8. Le coordinateur refuse de démarrer si un `rustc` observé
utilise déjà ce cache. Les sources/configurations actuelles du backend sont
épinglées séparément avant/après et toute dérive refuse le verdict.

Au dernier contrôle de ce passage, les sessions release `18123` et native
`76590` restent vivantes, sans reçu terminal release ni preuve de build natif.
Le coordinateur natif ne lance pas l'application. Le dessin des matériaux,
l'édition de l'île, le playtest et la parité visuelle restent à vérifier sur
le candidat effectivement construit.

La fixture dédiée au prochain essai est maintenant copiée dans
`F:/OpenRPG_Forge/.build/studio-light-desktop-20261003-v1/runtime-fixture-v1/authoring-root`.
Le script `Prepare-AuthoringFixture.py` contrôle la fermeture exacte et les
empreintes des 27 fichiers de la fixture historique intacte utilisée par285,
copie 54 608 403 octets, puis revérifie source et copies. Le reçu
`runtime-fixture-v1/fixture-preparation.json` porte `fixture-prepared-only` ;
un nouveau profil WebView2 vide est prévu à côté. Cela ne rejoue pas la recette
d'édition commune du 2 octobre et ne prouve aucune activation de zone.

L'API Windows du skill Computer Use est initialisée avec `@oai/sky` et retourne
actuellement des fenêtres Studio. Une observation sans interaction de l'instance
canonique existante obtient son arbre d'accessibilité ; la capture montre une
autre application qui l'occulte, donc aucun pixel Studio n'en est déduit.
Aucune entrée, activation ni fermeture de cette instance n'est exécutée.
L'ancien verdict `desktop-locked-ui-not-run` du candidat285 reste historique ;
le prochain essai devra sélectionner et observer la fenêtre du nouveau candidat.

Le lancement isolé est en attente dans la session `51311`, via
`F:/OpenRPG_Forge/.build/studio-light-desktop-20261003-v1/Launch-VerifiedCandidate.ps1`
(SHA-256 `90f5af977707b0eea4795548d3dac3392e632fd400029cf91f47b30dbd7df217`).
L'analyse syntaxique PowerShell passe, avec support de `Start-Process -Environment`
vérifié sur le runtime7.6.5. Le script suit le coordinateur natif par PID et
instant exact de création, refuse un résultat natif échoué ou une dérive du
binaire, des copies frontend ou de la fixture, puis utilise uniquement la
copie d'authoring et le profil WebView2 isolés. Aucun résultat historique ne
sert de remplacement au nouveau binaire. Le reçu attendu est
`runtime-fixture-v1/launch-result.json`, avec un état de lancement distinct de
l'observation UI, de l'ouverture de zone, du playtest et du dessin GPU.

## Release V8 et assemblage terminés

La session release18123 termine avec le code0. Cargo release démarre à
01:30:43 UTC et termine à02:27:26 UTC (56 min42 s). Le contrôle après build
passe sur1342 fichiers figés et16 fichiers possédés. Le module brut fait
113 891 095 octets, SHA-256
`58735c8c4a57e5e8bce8acba5b9b56e3790623821f7a239d044a5c37b5f860c9`.
L'assemblage optimisé passe cette fois sans assouplissement du parseur ;
le reçu conserve le profil réel opt_level3, sans debug_assertions ni mode test.

Le frontend assemblé contient1754 fichiers ; l'ajout vérifié du worker produit
1758 fichiers dans `studio-light-frontend-with-navigation-20261003-v3/frontend`.
Le WASM final fait121 843 769 octets, SHA-256
`e11f64f4f2b847b4bb6c1f514b811b38164e456d61d850950b27dc8be8692c58`.
Le coordinateur release termine à02:32:15 UTC avec `exit_code=0`.
Les reçus de staging conservent `frontend-build-only` et aucune qualification
de dessin, de Studio, de parité ou de distribution.

La session native76590 poursuit l'enchaînement. `validate-inputs.log` rapporte
`INPUTS_VALID`,1767 fichiers frontend/worker/kit, entrée `index.html?authoring=1`.
Ce reçu est un précontrôle sans compilation ; le verdict du vrai build Tauri
et le lancement restent séparés.

## Premier build natif refusé et reprise des gardes

Tauri termine sa compilation réelle en8 min52 s ; le candidat `attempt.0`
fait208 998 912 octets, SHA-256
`449f0d2843b0da8c2965ca7d763560b34e741b81c812da089b11a50fc07773e8`.
Le coordinateur76590 refuse ensuite le verdict à02:45:45 UTC, car sa garde
large détecte cinq différences : `authoring_assets/placements.rs`, les `lib.rs`
d'Assets et d'Engine, et le retrait de leurs deux modules `source_material_color.rs`.
Le résultat natif garde `exit_code=1`, et le lancement51311 est refusé ; aucune
fenêtre de ce candidat n'est lancée. Ces résultats restent conservés.

La lecture du vrai `debug/forge-studio.d` exclut ces cinq fichiers de ce binaire
natif. Cependant, l'audit séparé `Audit-NativeDependencies.py` refuse aussi sa
validation : `examples/visual_valley/player_controller.v1.json`, réellement
compilé, était absent des anciennes gardes. Aucun verdict réussi n'est déduit
de cet audit et aucune ancienne preuve n'est réécrite.

Une nouvelle construction est donc lancée dans la session96064 avec
`Run-ScopedNativeBuild.py`, SHA-256
`884f8f57332ebcbeae74c4b1106131315041187e89df4df63a3ca128c97caec2`,
sorties `native-scoped-build-v2` et candidat neuf `attempt.1`.
Elle capture188 entrées natives/métadonnées avant le build : les sources citées
par le dep-info précédent, les manifestes/configurations Cargo et le script de
construction. Elle inclut aussi les inventaires des répertoires capabilities
et permissions, ainsi que la fixture JSON précédemment manquante.
Après le nouveau build, l'inventaire natif réel doit correspondre exactement,
les188 entrées doivent rester identiques, et le dep-info doit couvrir les1767
fichiers effectivement embarqués. Un écart refuse le nouveau verdict.
Le frontend reste la source V8 figée et qualifiée séparément ; cette reprise
ne remplace pas les modifications concurrentes du worktree ni ne les publie.

Le lancement du second candidat attend la session96064 dans la session95779,
avec `Launch-VerifiedCandidate-v2.ps1`, SHA-256
`12dc7e4fb90575a10a9a0012d2d9dd4902d4f683978e5a624fa9061005374937`.
Il exige le nouveau `native-scoped-build-v2/result.json` réussi et le champ
`native_inputs_unchanged`, puis revérifie binaire, frontend et fixture avant
de lancer. Le reçu de lancement est distinct (`launch-v2-result.json`).
La syntaxe PowerShell passe ; au dernier contrôle, le processus Python30456
du nouveau build est vivant, sans verdict terminal. Aucun lancement de zone,
dessin visible ou playtest n'est déduit de cette attente.

## Audit corrigé des chemins et lancement réel du candidat

Le second build termine la compilation en 6 min 10 s. Son reçu initial refuse
la comparaison du frontend à 03:09:15 UTC ; ce refus reste intact. L'audit
indépendant `Audit-NormalizedNativeFrontend.py` établit que les deux inventaires
contiennent exactement 1767 membres, avec des séparateurs Windows dans le
manifeste et POSIX dans le dep-info. La normalisation des séparateurs conserve
les contrôles de doublons, de chemins relatifs, de membres exacts et de hashes.

`native-scoped-build-v2/normalized-audit.json` passe à 03:13:49 UTC : les 188
entrées natives/métadonnées avant, après et au moment de l'audit sont identiques ;
les membres embarqués, présents sur disque et attendus correspondent exactement.
Le binaire candidat et le vrai binaire Cargo ont le même SHA-256 :
`022b84097d6bd182c893688f3e26c6ad80522dae8bf43a06954a2bcbc910e51a`.
Ce résultat reste `desktop-build-only`, sans qualification visuelle.

`Launch-VerifiedCandidate-v3.ps1` revérifie les hashes du frontend et les 27
fichiers de la fixture source et copie avant lancement. Son reçu distinct
`runtime-fixture-v1/launch-v3-result.json` observe le PID 33784 lancé à
03:14:13 UTC. Computer Use sélectionne uniquement la fenêtre 17043788 de
`attempt.1/forge-studio-candidate.exe` ; la coque Studio est visible et utilisable.
L'ouverture via la liste historique des zones refuse correctement la zone gérée
par workspace (`This zone belongs to the workspace`). Le workspace est ensuite
ouvert par l'interface et son bouton de zone e620dd19-ead9-5913-a6d6-0f93109148c0
est actionné. Le chargement de la zone et le dessin restent à vérifier.
Aucun changement des fixtures originales ni publication.

### Premiers pixels réels de la carte dans ce candidat

L'ouverture du workspace puis de sa zone réussit. La scène présente 23 bâtiments
et 10 couches végétales. Le parcours Objets → Fiche → Bibliothèque de bâtiments
importés → Vérifier et charger la bibliothèque vérifie et charge les paquets
embarququés. Après préparation WebGPU, Computer Use observe réellement le terrain,
des bâtiments texturés, la jetée, de la végétation, un ciel nuageux et des ombres
projetées dans le viewport de `attempt.1`. La vue est celle de l'éditeur,
dirigée vers la jetée et la mer ; elle ne constitue pas une paire avec la référence.

Le journal affiche les collisions synchronisées pour le terrain
`803ec5de95974848985ae14871d8f40e384274539674e59818da6a300b485679`.
Le reçu `runtime-fixture-v1/ui-render-observation-v1.json` distingue ces observations
du parcours édition/sauvegarde/rechargement, du Playtest et de la parité visuelle,
tous encore non vérifiés. Les captures sont inspectées via Computer Use,
sans export de pixels. Le contrôle de diff du rapport passe ; le diff global
reste en échec sur des espaces/CRLF dans des modifications concurrentes, conservées.

### Playtest et refus de persistance sur exFAT

Le bouton Jouer entre réellement en Playtest ; après préparation, le village,
les bâtiments, les plantes et les ombres sont visibles depuis la caméra à pied.
Le clic du canvas acquiert le pointeur ; une touche W est envoyée mais aucun
 déplacement quantifié n'est établi par cette observation. Échap libère le
pointeur et Retour à l'édition restaure une session éditable. La bibliothèque
importée doit être rechargée en édition après ce rechargement de page.

L'inspecteur commun sélectionne le bâtiment 00231f2b-5f07-548e-9bb1-94cf0f742f14
et prépare X=77,5 au lieu de 75,5 : exactement une transformation proposée,
22 autres bâtiments inchangés. Appliquer échoue avec « Fonction incorrecte.
(os error 1) ». La commande Sauvegarder suivante sauvegarde la session inchangée ;
elle ne prouve donc pas la persistance de la retouche. Le contrôle disque avant
retouche confirme les 27 hashes source/copie inchangés. Les lectures du verrou
workspace actif peuvent être refusées ; ce n'est pas une perte de document.

Diagnostic confirmé : F: est exFAT, E: est NTFS. `ZoneObjectStore::publish`
utilise une publication par lien physique. Les probes jetables conservées
(`hardlink-capability-v1.json`) reproduisent WinError 1 sur F: et réussissent
avec bytes identiques sur E:. Aucune atomicité n'est affaiblie pour forcer un PASS.
Une nouvelle copie des 27 fichiers originaux est préparée sur E: dans
`studio-light-ntfs-runtime-20261003-v1`, avec profil WebView neuf ; les anciennes
fixtures et les refus restent conservés. `Launch-VerifiedCandidate-v4.ps1`
vérifie le même candidat avant de lancer cette copie. Validation UI/persistance
sur NTFS encore en attente à cette étape.

### Retouche et relecture réussies sur NTFS

Le nouveau lancement (PID 38776, fenêtre 24056400) utilise la copie NTFS et le
même exécutable vérifié. Le parcours workspace/module puis inspecteur commun
prépare et applique X=75,5 → 77,5 m pour le bâtiment
00231f2b-5f07-548e-9bb1-94cf0f742f14. Le journal indique Retouche appliquée puis
Zone sauvegardée. Après Ctrl+R, Session synchronisée et la nouvelle sélection
du bâtiment affichent X=77,5 m dans l'inspecteur.

`placement-save-reload-v1.json` vérifie directement le hash de l'archive publiée
`afdd3af8caabb9fd5b0f6bc2b696f47e25c68179d4ff7c4fdc3d18544f2bb3b5`,
head `84a870e7af19ec49b55f095f52c9f8e95c2b4fd9d20de690a04aecdd52765e22`.
Les 23 placements restent présents, les 22 autres restent identiques ; seul X
change sur la cible. Base terrain/végétation et autres corrections identiques.
Les 27 fichiers de la fixture originale restent inchangés.
Limites : rechargement de page, sans redémarrage complet du processus ; aucun
pixel du bâtiment retouché n'est qualifié dans cette instance. La persistance
sur exFAT reste non prise en charge par la publication par lien physique.

### Correctif produit de publication sur exFAT

Le code natif des magasins de zones et de sources utilise désormais un adapter
isolé `workspace/atomic_publish.rs`. Sous Windows, il publie le fichier de
staging déjà fermé et synchronisé par `MoveFileExW` avec flags=0, dans le même
répertoire : aucun remplacement et aucune copie inter-volume. Sur les autres
plateformes, la publication par lien physique est conservée. La fonction indique
si le staging a été consommé afin de ne pas traiter sa disparition normale comme
une erreur. Les vérifications de hash après publication et des objets déjà
présents restent intactes ; aucune durabilité de répertoire après coupure secteur
n'est promise. L'FFI Windows est isolée, avec commentaire de sécurité et tests,
conformément à AGENTS.md.
Référence API : https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw

Le harness Rust du vrai module passe quatre tests sur F: exFAT et E: NTFS :
publication complète, non-écrasement de la destination existante, refus
inter-répertoires et refus des NUL avant FFI. Le premier Cargo ciblé passe cinq
tests, incluant le vrai `ZoneObjectStore` sur TEMP exFAT. Deux tests supplémentaires
du magasin de blobs (round-trip/idempotence et refus de corruption sans remplacement)
sont ajoutés après ce premier binaire de test ; leur nouvelle exécution est en
cours et ne doit pas être déduite des cinq PASS précédents.

Le candidat graphique `attempt.1` n'embarque pas encore ce correctif natif.
Un script de nouveau build est préparé (`Run-ExfatFixedNativeBuild.py`, sorties
`native-scoped-build-v3`, candidat `attempt.2`) avec garde du nouveau module natif
et comparaison normalisée des chemins frontend. Aucun résultat runtime corrigé
n'est revendiqué avant la construction et le parcours Studio correspondants.


### Suite des tests exFAT et nouvelle construction native

La seconde exécution Cargo est terminée avec code 0 : sept tests réussissent,
dont les deux tests du vrai magasin de blobs ajoutés après la première exécution.
Clippy se termine également avec code 0. Inspection de son JSON : avertissement
cache de compilation sur exFAT et avertissement `items_after_test_module` sur
le nouveau bloc de tests de `source_blobs.rs`. Ce dernier reste à corriger ;
aucun verdict sans avertissement n'est revendiqué. La source native est gelée
pendant la construction gardée en cours.

`Run-ExfatFixedNativeBuild.py` est lancé (session outil 87949). Une nouvelle
fixture `runtime-exfat-fixed-v1` est préparée sur F: à partir des 27 fichiers
épinglés, 54 608 403 octets, tous vérifiés avant/après copie.
`Launch-VerifiedCandidate-v5.ps1` est préparé pour le résultat natif v3 et un
profil WebView neuf. À ce stade, ce lancement et la sauvegarde UI sur exFAT
corrigée restent non exécutés.


### Diagnostic du retour Playtest vers édition

La lecture actuelle du produit confirme le mécanisme observé :
`authoring_controls::replaces_projection` inclut `StopPlaytest`, puis
`replace_session_projection` accepte la réponse native, invalide la projection
et appelle `location.reload()`. Les packages de la bibliothèque sont conservés
dans des `thread_local` du frontend et disparaissent donc au rechargement.
`authoring_viewport::observe` appelle `ensure_for_playtest()` uniquement si
`playtest.is_some()`. La nouvelle session édition n'active pas ce chemin.

Le correctif attendu doit restaurer une acquisition explicitement choisie et
épinglée, en conservant ses vérifications de hash et sa politique d'annulation ;
il ne doit pas conditionner le contrat commun d'édition à un slug de module.
Aucun correctif de restauration automatique ni preuve runtime associée n'est
revendiqué à ce stade. La construction native v3 reste active ; pas de mutation
des entrées gardées durant sa compilation.


### Observation de construction et anciennes instances

Le build v3 atteint les compilations réelles de `forge_project`, `forge_studio`
et `forge_world`. La session 87949 est toujours active, sans verdict terminal.
Le script de lancement v5 passe la vérification syntaxique PowerShell.

Les anciens PID 33784 et 38776 et leurs chemins `attempt.1` sont revérifiés
avant une tentative de fermeture propre pour libérer des ressources. Dans
l'instance NTFS 24056400, l'inspecteur indique encore X=77,5 et le journal
Session synchronisée. Alt+F4 ouvre le dialogue « Quitter le Studio ? », avec
« Les retouches appliquées sont enregistrées ». Le clic Enregistrer et quitter
échoue avant géométrie d'entrée disponible, puis la récupération par activation
expire. Aucune fermeture n'est donc revendiquée, aucune autre entrée UI n'est
exécutée après cet échec de récupération. Les fixtures et preuves sont conservées.
Cet échec de l'outil ne vaut pas verdict de sauvegarde ni échec du build en cours.


### Construction v3 et sauvegarde UI exFAT réussies

La session native 87949 termine avec code 0. `native-scoped-build-v3/result.json`
vérifie 175 sources natives, 189 entrées natives/métadonnées, 1 767 fichiers
frontend et aucune dérive durant la construction. Exécutable `attempt.2` :
`a45692fedff9e7ad0f0d776d7d9e6fe96356f0eb12fb8522daf5b99ae95f5124`.
La compilation Cargo réelle termine en neuf minutes ; le script complet dure
environ quinze minutes avec préparation et vérifications. Verdict build-only,
sans parité graphique déduite.

Le lancement v5 passe les contrôles avant ouverture de la fixture neuve exFAT,
PID 46324, fenêtre 25235728. Le parcours Projets et modules → Ouvrir le workspace
→ Ouvrir Tidewater island · zone 1 ouvre l'île. L'inspecteur commun sélectionne
00231f2b-5f07-548e-9bb1-94cf0f742f14 et prépare X=75,5 → 77,5 m. La proposition
contient une transformation et 22 objets inchangés. Appliquer réussit ; le journal
indique Retouche appliquée puis Zone sauvegardée · Session synchronisée. Après
Ctrl+R, un nouvel arbre UI et une nouvelle sélection affichent X=77,5 m.

`runtime-exfat-fixed-v1/placement-save-reload-v1.json` vérifie directement
l'archive `2dd017226aa8916dce33c4a80dd8d83102dae910034924d3091fe0e790bc89d3`,
head `84a870e7af19ec49b55f095f52c9f8e95c2b4fd9d20de690a04aecdd52765e22`.
Les 23 placements restent présents, seul X du bâtiment ciblé change ; les autres
22 placements, la base et les autres corrections restent identiques. Les 27
fichiers de la fixture source restent inchangés. Le défaut exFAT reproduit dans
le candidat précédent est donc corrigé dans ce parcours UI réel.
Limites : rechargement de page, sans redémarrage complet du processus ; aucune
preuve de pixels du bâtiment déplacé ni de parité visuelle. La bibliothèque
graphique n'est pas chargée dans ce parcours consacré à la persistance.

Après la fin de la construction gardée et du parcours ci-dessus, le bloc de
tests de `source_blobs.rs` est déplacé après l'implémentation pour résoudre
l'avertissement `items_after_test_module`. Ce déplacement concerne uniquement
le code `cfg(test)` ; il n'est pas attribué rétroactivement au candidat figé.
Une nouvelle exécution Clippy est en cours (`clippy-v2.jsonl`).


Clippy v2 termine avec code 0. Inspection complète du JSON : tous les
avertissements concernent le cache incrémental exFAT (copie au lieu de lien
physique), aucun diagnostic de source. L'avertissement de position du bloc
de tests a disparu. La mise en forme explicite en LF résout les erreurs
CRLF du diff scoped introduites par le rustfmt Windows ; ce diff scoped
passe à nouveau. Le code de production du candidat ne change pas.

Le chargement graphique explicite de la bibliothèque est ensuite demandé
dans la fenêtre exFAT v5 ; il est encore en cours à cette observation.
La preuve de persistance ci-dessus ne dépend pas de sa réussite.


Le chargement graphique explicite termine dans le candidat v5 : 23 bâtiments
vérifiés, environnement et ressources végétales acquis. La capture native de
la fenêtre 25235728 montre effectivement des bâtiments texturés, le ponton,
le terrain, le ciel nuageux, des arbres et des ombres de bâtiments. Le journal
confirme les collisions pour le hash de projection
`b385ce1263e803d33a6511a12a1768c6e32e5866aec6a2385ac81fde2c5b36ae`.
`ui-render-observation-v1.json` conserve le périmètre : pas d'identification
pixel individuelle du bâtiment déplacé, pas de qualification de l'eau ni
de la végétation complète, pas de caméra appariée à la référence ni de
parité complète. Aucun nouveau Playtest n'est exécuté dans cette instance.
