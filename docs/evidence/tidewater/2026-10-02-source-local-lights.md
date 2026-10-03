# Tidewater — collecteur des éclairages locaux source

Suite de la [préparation des pipelines Bevy](2026-10-02-boat-bevy-pipelines.md).
Les 60 sources lumineuses réelles sont acquises et fermées avec leurs phases et
sources originales. Le candidat v4 passe 15 tests assets, neuf tests Engine,
12 scénarios de transport CPU/GPU, Clippy ciblé et la compilation WASM. La nouvelle
acquisition identifie les onze pivots de lanternes du quai par identité source,
les parents des 32 lampes de bâtiments, des sept lampes de passerelle/accessoires,
des huit lumières du bateau et les déclarations de repères des deux vendeurs.
Les associations vers le catalogue et les placements,
le raccord Studio/shaders et le dessin de scène restent ouverts. Les échecs
antérieurs et la limite du contrôle sans features sont conservés ci-dessous.

## Contrat

Le collecteur reçoit des lumières ordonnées, une pose de caméra et les
paramètres explicites de temps, nuit, intensité et torche. Il ne possède aucun
document canonique, ne choisit aucun module et ne crée aucun renderer.
Les calculs gardent la précision des nombres JavaScript jusqu'à la conversion
f32 du bloc source `LocalLightParams` de 448 octets.

La sélection stable par distance conserve l'ordre d'enregistrement, réserve
le premier slot à la torche lorsqu'elle est active et conserve les slots
inactifs. Les valeurs initiales de Vector4 ont w=1, comme THREE. La torche
reprend son décalage, sa couleur, son lissage de visée et son atténuation diurne.
Les phases de scintillement doivent être acquises ou écrites dans les sources ;
le renderer ne les tire pas au hasard.

L'adaptateur ECS collecte les véritables GlobalTransform et refuse une caméra
inactive, 2D, de capture ou de preview. Il vérifie l'ensemble des ordres
attendus, les doublons et les transformations manquantes. Un parent de caméra
est aplati dans la convention de caméra racine de la référence.
Le buffer utilise seulement le RenderDevice et la RenderQueue Bevy existants.
Ses publications doivent suivre des tags et révisions croissants ; une
publication refusée ne laisse pas un ancien buffer prêt à être utilisé.

La validation est volontairement plus stricte que le JavaScript pour les
paramètres actifs invalides et le cutoff nul qui produirait des NaN. Les lampes
non lues en journée ou désactivées ne font pas échouer le collecteur CPU.
La collecte ECS exige cependant des projections transformées complètes.

## Entrées et essai en cours

Référence intacte : Tidewater au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c` ; LocalLights.js SHA-256
`b8d52a6ad3fb9ccc00bafa57a7f597b34fc64daaef462a8f70a3d86088188e90`.
L'oracle CPU v2 exécute le vrai collecteur et le vrai UniformBlock._pack de
l'upstream, sans GPU initialisé. Il couvre douze frames successives, dont
jour, crépuscule, nuit, égalités de distance, absence de remplacement d'un
slot éteint, bascules de torche, mouvement et désactivation globale.
Les scénarios et leurs neuf sources sont synthétiques ; ce n'est pas un
inventaire de la scène réelle.

Artefacts : `F:/OpenRPG_Forge/.build/source-local-lights-evidence-361`.
Oracle v2 SHA-256
`96ebd97c47bfddbcafc217e4fb386ef241fbdcafc0fabec3d6de7ffadcc7bfdb`.
Le runner source reproductible a pour SHA-256
`7dca4606bed6dfe6c43c824e37fdfd040474ebc7aa7b3fc8d1e9f3b5269b0029`.

Sources figées 363 : 917 pins, 10 763 682 octets. Le runner 364 utilise
Rust 1.98.0 et les dépendances verrouillées/offline ; les logs et retours
terminaux doivent être conservés dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-364`.
Cette première recette a été interrompue après diagnostic du profil Cargo :
`CARGO_PROFILE_DEV_DEBUG=0` provoquait une reconstruction des dépendances,
distincte du cache 338/358 malgré le même `RUSTFLAGS`. Son reçu explicite
`profile-correction-cancellation.json` reste dans 364 ; aucun résultat Rust/GPU
ne lui est attribué. La revue indépendante
`FINAL-REVIEW.md` ne trouve pas de nouveau défaut bloquant dans les trois
fichiers, mais elle ne remplace pas leur validation exécutable.

## Raccords encore ouverts

Aucun import de village, quai ou bateau ne fournit encore ce descripteur
complet dans la scène produit. Les données actuelles de place ne transportent
pas toutes les portées, cônes et phases ; le bateau n'importe pas ses six
émetteurs. La publication autoritative de l'état jour/nuit et du temps,
l'extraction RenderWorld et les call sites produit restent à ajouter.

L'accord de matrice de for_view ne prouve pas l'identité de l'Entity caméra ou
le serial capturé lors de la collecte. L'inventaire ECS est fourni par
l'appelant ; il ne prouve pas la complétude du module canonique.
Une erreur de collecte ou de préparation impose d'invalider le buffer et les
consommateurs doivent demander le tag de leur vue préparée courante.

Le vérificateur prévu compare les 448 octets source puis les relit sur le GPU
de Bevy. Il ne construit pas le bind group complet de la scène, ne dessine pas
le bateau et ne qualifie aucune apparence. Les autres ressources du matériau,
la vraie variante tardive des pièges, le dessin dans le Playtest et les
comparaisons natives/WebGPU gardent leurs critères. Aucun critère TW ni
l'objectif complet n'est clos ; aucune publication Git.

## Premier crochet d’export, remplacé par la suite ci-dessous

Le helper scripts/tidewater/capture-source-local-lights.mjs est maintenant
appelé par export-source.mjs, après la construction réelle du Village dans
l’exporteur headless GPU existant. Il appelle les deux fonctions source et
LocalLights.add, conserve les phases obtenues et les repères monde/bateau,
puis écrit source-local-lights.json fermé par le manifeste de l’export.
Aucune réacquisition n’a encore été exécutée : le constructeur Village
uploade les poissons et exige le GPU upstream. Ce crochet ne prétend donc
pas être une acquisition CPU autonome.

Le payload déclare explicitement le préfixe Village→Boat et l’absence des
lampes de gameplay et du stand/chandlery. Les phases ne sont pas
reproductibles entre acquisitions ; leur conservation dans un snapshot est
obligatoire. L’import typé et la fermeture dans les paquets autonomes restent
à raccorder avant toute disponibilité dans Studio.

La syntaxe des deux scripts et le contrôle du diff terminent avec retour 0.

## Descripteur typé et suite exécutée

`forge_assets::source_local_lights` lit désormais le JSON avec un hash attendu
fourni par l'appelant, une limite de taille et une version explicite. Il conserve
les valeurs nulles, phases, couleurs linéaires, cônes, ordres et repères locaux.
Le graphe de parents doit être fermé et sans cycle ; les matrices de capture
doivent être affines et inversibles. Les positions et directions monde sont
vérifiées contre les valeurs locales, sans devenir des poses runtime. Le graphe
de parents décrit l'ownership ; sans matrices locales parent/enfant, il ne prouve
pas leur composition géométrique.

Les quatre tests assets passent : conservation phase/absence/pose, pin avant
décodage, champs manquants/inconnus, repères absents/cycliques/incohérents,
inventaires et paramètres lumineux invalides. Les quatre tests Engine passent
sur le même candidat : échec sans publication invalide, lampes non utilisées,
caméras refusées, transformation de parent et cycle de vie ECS.

Sources : `F:/OpenRPG_Forge/.build/source-local-lights-source-20261002-descriptor-v1`,
918 pins, 10 785 422 octets. Elles dérivent de 363 avec seulement le nouveau
module assets et son export dans lib.rs. Retours terminaux et logs :
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261002-profile-reuse`.
Les deux premières étapes `assets-tests` et `native-tests` terminent avec code 0,
quatre tests exécutés chacune. La nouvelle recette retire les deux variables
de profil ajoutées, conserve les overrides CLI et réutilise le target 338.

Le chargement reste un contrat de données. Le pin d'AssetId/package, la fermeture
des fichiers, le mapping des parents réels, l'ownership résident/retrait et les
consommateurs Studio/runtime restent à raccorder. Aucun type Bevy, filesystem,
slug de module ou unité photométrique substituée n'entre dans le document.

## Inventaire source complet

Le crochet a été complété après la première version décrite plus haut.
`source-app-light-factories.mjs` appelle la vraie méthode `Game.addFloodlights`
sans construire Game. Les deux lampes d'échoppes utilisent exclusivement la
boucle App originale, lignes 351–357, après contrôle du hash du fichier et du
bloc UTF-8/LF :
`c85d39c0d6dda27e51538c8106e093c7191dbce7d950971da68784c067db32af`.
Ni App, ni FishStand, ni Chandlery ne sont construits pour cette acquisition.
`defaultUpgrades` et `gearStats` fournissent l'état explicite sans sauvegarde,
avec deckLights=false et scale=0 pour les deux projecteurs.

L'ordre conservé est Village → six lampes Boat → deux projecteurs → deux lampes
d'échoppes, sur un seul LocalLights.add, avec les phases réellement attribuées.
Le champ full_application_inventory désigne l'inventaire des sources enregistrées
de LocalLights, dans cet état initial ; il ne désigne ni une exécution App,
ni les contrôles dynamiques de torche, ni une qualification de jeu. Le reçu
séparé déclare full_application_executed=false. Aucun inventaire complet acquis
n'est revendiqué tant que l'export réel n'a pas terminé.

`export-local-lights.mjs` prépare une acquisition limitée à ces données, sans
rebaker les textures de toute l'île. Il ferme les fichiers originaux de code,
leur licence et les deux JSON dans un manifeste ; il ne transforme pas les
modèles/audio publics ou les binaires npm en assets qualifiés. Les trois scripts
sont figés dans
`F:/OpenRPG_Forge/.build/source-local-lights-acquisition-20261002-v1`.
L'acquisition a attendu le résultat terminal du vérificateur natif, même si ce
dernier a échoué avant initialisation GPU. Elle termine avec code 0 le 2 octobre
à 17:55:31 UTC : 50 Village, 6 Boat, 2 projecteurs, 2 échoppes, soit 60 sources.
Les 212 sources originales et les 214 fichiers manifestés ont des tailles/hashes
concordants ; la revue indépendante ACTUAL-LIGHT-ACQUISITION-REVIEW.md vérifie
la fermeture exacte, LICENSE, identités et sets continus. Son SHA-256 est
`d4435087249c6d908d6ede866ebe662f4c0d655e8d0731726350f029facd4344`.

Manifest SHA-256 :
`d85d5640ea7da6e746a9f081d17451da950c97b55e3a4c56942c54fe98e3dbfb`.
Descripteur SHA-256 :
`dc23f20208ec2eb6044a90ac2e6f6b2de402fdcb59c20651f352f2edd2520902`.
Le device upstream headless sert à la vraie construction Village ; sa destruction
finale produit le message attendu de device détruit. Aucun rendu App, import
produit, comparaison visuelle ou verdict GPU Rust n'en découle.

## Échec de précontrôle et correction numérique

Le binaire natif du premier candidat a pour SHA-256
`41d0718829dcecb042339d04c71da62ac77381611da869c2e4ddbf3a71f6338f`.
Il refuse strength-quarter avec « source time after differs » après comparaison
du paquet de cette frame. Le renderer_app est après les douze précontrôles :
aucun GPU Bevy n'a été initialisé. Le retour 1 et ses logs sont conservés dans
la validation profile-reuse ; Clippy/WASM/base ont été arrêtés avant exécution.

Le parseur serde_json par défaut préarrondit le significand de
0.9159999999999999 et produit les bits 0x3fed4fdf3b645a1d. La valeur source et
le parseur f64 exact produisent 0x3fed4fdf3b645a1c. Le diagnostic indépendant
JSON-FLOAT-DIAGNOSTIC.md SHA-256
`db8d07bf636dfa3b842b197ac295a6039c75c2deaf5136d012cc936b49905fb9`
explique cette différence sans inventer de résultat exécutable.

forge_assets et forge_engine activent désormais float_roundtrip sur la même
version de serde_json, sans nouvelle dépendance ni modification du lockfile.
La comparaison reste exacte ; l'oracle source et les seuils ne changent pas.
Le nouveau test vérifie les bits d'une phase JSON non exactement décimale.
L'exemple assets verify_source_light_descriptor est aussi ajouté pour charger
les 60 enregistrements réels, vérifier les 212 sources et exposer les bits des
phases à une comparaison indépendante.

Le candidat v2, 919 pins, se trouve dans
`F:/OpenRPG_Forge/.build/source-local-lights-source-20261002-descriptor-v2`.
La nouvelle recette est en cours dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261002-float-roundtrip`.
Ses cinq tests assets passent avec le nouveau cas de régression. L'exemple
assets compile et charge le véritable descripteur : 60 sources et les 212
fichiers originaux sont vérifiés, code 0. Le binaire a pour SHA-256
`ec2cdda201650916b778adc102383a31f7cad4794a0af152f8f28a152af0f8c6`.
Une lecture indépendante Node JSON.parse/Float64 compare les bits des 60 phases
avec le rapport Rust : aucun écart. Le reçu actual-phase-bits.json conserve
explicitement gpu_executed=false et runtime_import_executed=false.

Le 2 octobre à 18:20:04 UTC, le candidat v2 termine aussi les quatre tests
Engine, la compilation native et le vérificateur avec code 0. Les douze
scénarios synthétiques correspondent exactement à l'oracle source sur CPU
et après lecture du buffer GPU Bevy ; les 36 contrôles négatifs passent.
Le binaire natif a pour SHA-256
`ec96b0365ed3d12f859c7b91c46f61ab7fd85a1fd60ae94146b252c58e005f9f`.
Le reçu native.json indique explicitement draw_executed=false,
live_scene_resources_complete=false et appearance_qualified=false : cette
preuve valide le calcul et le transfert du paquet de lumières, sans dessin
dans l'île ni qualification visuelle. Clippy assets et Engine et le check WASM
terminent avec code 0. Clippy relève huit avertissements dans les nouveaux
fichiers Engine ; leur correction appartient au candidat suivant. Les 919 pins
du snapshot v2 sont inchangés à la garde finale, final-source-integrity.json.
Le contrôle sans features termine avec code 101 : trente erreurs de modules
non conditionnés utilisant forge_assets, Rapier ou d'autres modules retirés par
les features. Ce résultat reste un échec de cette configuration, sans être
substitué par les succès native/web. Le raccord produit reste ouvert.
Le bateau de capture reprend la pose WORLD.boatDock de l’App. Le helper exige
le device/queue source réels ; payload et manifeste distinguent explicitement
le mode headless GPU, sans navigateur. La revue finale ne trouve pas de
consommateur strict incompatible avec les métadonnées ajoutées au manifeste
v1. EXPORT-HOOK-INPUTS.json épingle les scripts et les quatre entrées source
pertinentes ; EXPORT-HOOK-REVIEW-FINAL.md garde les limites. Ces contrôles
ne sont pas une acquisition de lampes ni une qualification Studio.

## Paquet autonome et projection sur les parents réels

Le lecteur générique `SourceLocalLightsPackage` ferme désormais le manifeste,
le descripteur, le reçu d'acquisition et les sources originales, dont la licence.
Le SHA du manifeste vient de l'appelant ; il est distinct du SHA du descripteur.
L'index et toutes ses références sont vérifiés avant les lectures : chemins
relatifs portables, alias de casse, clés JSON uniques, limites par fichier et
cumulées, aucune charge utile non revendiquée. Les sources du document doivent
correspondre exactement à la table du paquet. La licence est conservée et
vérifiée comme source originale ; aucun texte de licence métier n'est substitué.
Les chemins disque et le transport navigateur appartiennent aux adapters.

Le reçu expose une déclaration source_application_executed_claim séparée. Même
une déclaration upstream positive ne qualifie ni un import Forge ni un dessin.
Les détails du producteur sont conservés comme provenance et ne pilotent aucun
comportement moteur. Les APIs synchrones et asynchrones ont le même contrat ;
le callback doit borner sa lecture/allocation au nombre d'octets fourni.

`PreparedSourceLights::from_package` reçoit l'AssetId du catalogue et un offset
d'enregistrement explicite. Les valeurs nulles sont résolues uniquement avec
les defaults acquis ; zéro, directions/cônes absents et phases sont conservés.
Le staging reçoit une table explicite frame→Entity réelle, vérifie son graphe
d'ancêtres et la propagation des transforms avant toute mutation, puis crée
des enfants à transform identité. Les positions restent locales : le collecteur
applique le GlobalTransform vivant une seule fois. L'identité dérivée conserve
asset, instance, reçu, descripteur et ID de source, sans branche sur kind/slug.

Une révision refusée conserve la projection précédente. Un ancien émetteur
dont l'identité, l'ordre, le parent ou les paramètres ont été modifiés empêche
le remplacement, même si les nouveaux ordres sont différents. Le retrait ne
supprime que les enfants portant encore leur identité attendue. La readiness
est invalidée après retrait, mutation du snapshot ou avant propagation d'une
retouche de parent. Les contrôles gameplay nécessiteront une couche dérivée
explicite ; ils ne doivent pas muter le snapshot importé pour conserver son reçu.

Le candidat v3 a échoué à la compilation d'un test avec E0282 sur une clé de
BTreeMap insuffisamment typée ; aucun résultat de paquet ou Engine n'en est
déduit. Le type a été explicité. La revue indépendante a aussi identifié le
cas d'ancien émetteur réattribué avec changement de plage d'ordres ; il est
corrigé et couvert par un test de refus sans mutation. Les sources v3 restent
conservées avec leur échec.

Le candidat v4 est figé dans
`F:/OpenRPG_Forge/.build/source-local-lights-source-20261002-package-v4` :
924 fichiers, seuls trois fichiers corrigés depuis v3 ; les autres pins sont
identiques. La validation sérialisée utilise le target 338 dans
`F:/OpenRPG_Forge/.build/source-local-lights-validation-20261002-package-v4`.
Quinze tests assets et neuf tests Engine passent avec code 0. Ils exercent
fermeture/refus du paquet, transport async arrêté à la première erreur, suivi
du parent translaté et mis à l'échelle, phase exacte, refus atomique, retrait,
identité réattribuée et readiness avant/après propagation.

Le vrai paquet, copié dans relocated-package, se charge par le nouveau binaire
Rust sans accès au clone upstream ni exécution Node : 214 fichiers, 212 sources,
60 lumières, licence et enveloppe d'acquisition vérifiés. Retour 0 à
18:54:13 UTC ; SHA du binaire
`fa7798b149adf3f30c38612338f16c48f197fbdb1cfdb689485182609c090c33`.
Les 60 phases ont les mêmes bits que le descripteur réel v2, déjà comparé à
JSON.parse/Float64 indépendamment ; aucun écart dans actual-package-phase-bits.json.
Cette preuve de transport n'est pas un import Studio ni une attestation GPU.
Le build/vérificateur natif, Clippy et le check WASM v4 terminent avec code 0.
Les douze scénarios retrouvent exactement le paquet source sur CPU et en lecture
GPU, et les 36 refus négatifs passent. Aucun diagnostic Clippy des nouveaux
fichiers n'est relevé dans les sorties JSON assets/Engine. Les corrections de
documentation, de comparaison du dernier rang affine, de clocks par bits et
de décodage hexadécimal ne changent pas l'oracle ni ses seuils.
La garde finale vérifie les 924 fichiers du snapshot et les neuf fichiers de
code concernés dans le checkout : aucun écart. Elle ne qualifie pas les autres
modifications du worktree. Le contrôle sans features v2 reste en échec et n'est
pas réexécuté à l'identique ; la matrice complète de features n'est pas qualifiée.
Le reçu GPU conserve draw_executed=false et appearance_qualified=false ; le
check WASM est une compilation, sans exécution de ce raccord en navigateur.

Le raccord de contenu reste ouvert : le paquet actuel ne déclare que world et
boat-root. Cela ne donne pas un parent éditable à chaque lampe de maison. La
revue source trouve une liaison par identité de vecteur pour les onze lampes
du quai et leurs pivots réels. Les autres producteurs Village ont perdu le
contexte du Builder après fusion ; leur owner doit être acquis à l'émission,
sans jointure par proximité ou kind. Il manque encore la table source→asset,
placement et ancre dans l'importeur, la publication du temps/nuit et des
contrôles, le raccord des uniforms aux shaders et le dessin dans la scène
complète. Aucun critère de parité, gameplay ou qualification Studio n'est clos.

## Acquisition des pivots réels des lanternes

L'export observe maintenant `Village.pierInfo.hung` et `Village.lanterns` de la
référence intacte `1438b1abfcaee3267092b75573014f4d9b4a983c`. Chaque entrée doit
conserver les mêmes objets `live`, `rest` et `pivot`, son vrai parent Group et
une unique lumière partageant `position === live`. Les noms et la proximité
ne servent pas de jointure. L'ordre de construction source donne l'ancre
`Village.lanterns[ordinal].obj`, à résoudre explicitement par l'importeur.

Le descripteur conserve les 60 ID/ordres et ajoute onze frames aux deux frames
existantes. La position de chaque lanterne devient locale à son pivot ; sa pose
monde acquise reste identique. Le parent déclaré world décrit la hiérarchie
source, pas une assignation implicite à une entité Studio. Aucun AssetId,
placement ou ancêtre ECS n'est inventé par l'export.

Le reçu ajoute une observation versionnée `editable_ownership` : 19 lumières
avec parent source (11 lanternes, 6 bateau, 2 projecteurs), 41 ID non résolus,
`source_binding_complete=false`, `catalogue_binding_executed=false` et
`studio_projection_executed=false`. L'inventaire complet des émetteurs et la
couverture des parents éditables sont deux faits distincts. Le lecteur conserve
cette observation comme provenance ; ce n'est pas une commande produit.

Preuves fraîches dans
`F:/OpenRPG_Forge/.build/source-local-lights-owners-20261002-v1` :

- Acquisition du Village réel et des quatre producteurs : retour 0, 212 sources,
  214 fichiers du paquet, clone canonique intact. Le diagnostic de destruction
  du device SDK est conservé ; il survient à la fermeture prévue du device.
- SHA manifeste `d7b36cf2999e974486af9d092a4ae1455fbc97ce281ac67dc03dbcdccb53ae2c` ;
  SHA descripteur `3630833d8237ac3163d8e24aa2258e1908e0ad6593c5b10e065e9a6603d49cec`.
- Lecture par le binaire Rust v4 inchangé
  `fa7798b149adf3f30c38612338f16c48f197fbdb1cfdb689485182609c090c33` : retour 0,
  fermeture du paquet, matrices/poses et enveloppe validées, sans clone/Node
  dans le chemin du lecteur. `actual-package-result.json` et `actual-package.json`.
- Test avec le vrai graphe/matrices de l'engine source, sans GPU : translation,
  rotation, échelle non uniforme et rotation du pivot conservent l'ancre locale.
  Sept refus couvrent position ambiguë, clone du vecteur, identité remplacée,
  parent absent, matrice singulière, valeur non finie et census incomplet.
  Une lumière non liée aux mêmes coordonnées reste non liée. Retour 0 dans
  `owner-tests.stdout` ; ce scénario ne prétend pas être une session Studio.
- Contrôle indépendant `owner-package-check.json` : les 60 phases Rust ont les
  mêmes bits que JSON.parse/Float64 du nouveau paquet ; ID, ordres, sources,
  poses monde et paramètres lumineux sont identiques à la capture précédente.
  Seules onze affectations de frame changent. Les 60 phases diffèrent de la
  précédente acquisition, conformément au hasard upstream non reproductible ;
  elles ne sont ni copiées de l'ancien reçu ni tirées à nouveau par le runtime.

Le prochain raccord exige la table catalogue/placement/ancre et une projection
par instance éditable. La préparation actuelle couvre tout le paquet ; elle ne
doit pas faire dépendre un quai placé seul de la présence du bateau. Les 41
autres bindings doivent être observés à l'émission avant fusion des Builders.
Les contrôles de transport ne qualifient aucun éclairage visible, aucune parité
graphique ni le lancement autonome du jeu.

## Acquisition des lampes des bâtiments avant fusion

Un overlay d'import de développement enveloppe les quatre générateurs originaux
`buildHouse`, `buildShed`, `buildBoathouse` et `buildMarketStall`. Les corps de
générateurs, leurs appels au hasard et le clone canonique restent inchangés.
L'ordre conserve les 23 invocations du précédent export de bâtiments, y compris
les quatre sheds sans lampe ; les IDs `tidewater:<commit>:building:<ordinal>`
restent ainsi alignés avec la recette de conversion existante.

Le hook observe le premier `Builder.push` au niveau d'entrée du générateur et
retient ce vrai repère racine. À chaque `lights.push`, il exige que ce repère
soit encore la frame active ou un objet présent dans la stack des ancêtres.
Il garde l'identité exacte des objets émis, restaure les deux méthodes d'instance
dans `finally`, puis vérifie le retour au repère/stack initiaux et la liste des
objets ajoutés. Les prototypes Builder/Array ne sont pas modifiés.

Le premier candidat a échoué sur l'hypothèse trop restrictive d'une seule scope
racine par bâtiment : certaines maisons ouvrent une seconde scope pour un
accessoire extérieur après fermeture de leur construction principale.
`source-local-lights-building-owners-20261002-v1/acquisition-result.json` conserve
le retour 1 et le stderr original. Le correctif conserve la première frame et
prouve l'ascendance de chaque émission, au lieu d'accepter arbitrairement la
dernière frame. Le scénario v2 s'exécute dans une nouvelle sortie.

L'observation originale et le Village sous overlay ont le même digest : les
huit cartes de terrain, tous les buffers d'attributs/indices, poses et draw ranges
de géométrie, lampes, bâtiments, footprints, foundation checks et collisions
boxes/cylinders sont comparés. Les phases aléatoires d'animation et l'état GPU
de rendu ne sont pas inclus ; aucune égalité de dessin n'en est déduite.

Les fenêtres restent liées à l'objet brut émis par leur générateur :
`addVillageLights` clone et décale leur position, mais parcourt la liste brute
une fois dans le même ordre. Le rattachement suit cette correspondance source
épinglée ; le calcul de direction vers le bâtiment le plus proche ne sert
jamais de preuve d'ownership. Les positions/directions finales deviennent
locales à la frame du bâtiment, et leurs poses monde acquises restent inchangées.

Preuves v2 dans
`F:/OpenRPG_Forge/.build/source-local-lights-building-owners-20261002-v2` :

- Acquisition originale + observée : retour 0, digest commun
  `813f2c14fd9a9715edb2850ee768d3ac38954a25540a4a8636bf41aaf9356dc9`.
  17 maisons, quatre sheds, un boathouse et un market stall ; 32 lampes émises.
- 60 émetteurs, 32 frames dont 19 de bâtiments lumineux ; 51 parents source
  identifiés, neuf ID toujours non résolus. Manifeste
  `a61bb14f702b5793f1bfdba87b788f2be54a94eb41ceef9e818a9c449829ee20`, descripteur
  `132ff46a5ee7088b6df990c0d6d7575de890256f68c8455216d16489bdee1bf5`.
- Le lecteur Rust v4 inchangé ferme les 214 payloads/212 sources, matrices/poses,
  licence et enveloppe : retour 0. Aucun nouveau build Rust n'est prétendu.
- Test dédié : propriétaire partagé par plusieurs lampes, ordinal préservé
  après invocation sans lampe, copie de frame indépendante, objet non lié aux
  mêmes coordonnées. Six refus couvrent duplications, objet cloné, ownership
  double, frame singulière/non finie et mauvais pin avant import. Retour 0.
- `owner-package-check.json` : 43 changements de frame (11 quai + 32 bâtiments),
  60 phases exactes Rust/JSON.parse, poses monde/photométrie/IDs/ordres/sources
  inchangés par rapport à la première capture. Les nouvelles phases aléatoires
  sont conservées comme nouvelle acquisition, sans promesse de reproductibilité.

La provenance des 32 lampes de bâtiments a donc progressé. Les neuf autres
restent trois lampes de passerelle, la lampe de place, trois accessoires pathLight
et deux lampes de vendeurs. La résolution catalogue/placement/ancre, le staging
par instance, les shaders et la preuve de rendu visible restent ouverts ; le
reçu conserve `source_binding_complete=false`, `studio_projection_executed=false`
et les qualifications visuelles à false.

### Isolation après revue et paquet courant v3

La revue indépendante confirme le repère racine, la garde d'ascendance et les
23 IDs/ordinals compatibles avec l'export de bâtiments. Elle relève le risque
de mélanger les observations si le module instrumenté construit plusieurs
Villages. Chaque préparation utilise désormais une scope de cache de module
distincte ; le wrapper refuse une seconde construction avant le générateur.
Cette scope temporaire n'est ni un ID source persistant ni une modification des
octets du module original. Le SHA du code observé reste identique entre deux
préparations ; leurs listes et constructeurs sont indépendants.

Le candidat courant est dans
`F:/OpenRPG_Forge/.build/source-local-lights-building-owners-20261002-v3`.
L'acquisition réelle, la fermeture du paquet par Rust et les tests dédiés
terminent avec code 0. Le reçu confirme le refus de seconde construction,
51 bindings source/9 non résolus, et le contrôle indépendant retrouve les
60 phases exactement. Aucun corps de générateur ou source canonique ne change.
SHA manifeste `382141c1e62116f27f0ddda59be7c0008b42d03182ebdaeb47bb5e0cf2e2e731` ;
SHA descripteur `f8887cfe7962e27f603319aacd857342f1e5d500047625d1e18d6c27dc46861c`.
Les preuves v1/v2 restent conservées avec leurs limites. Le digest ne qualifie
pas les textures GPU, l'ensemble des métadonnées du renderer ou un dessin.

## Couverture des passerelles, accessoires et vendeurs

L'overlay observe maintenant les trois appels originaux `buildBoardwalk`, les
deux appels de bench précédant le lampPost de place, ce lampPost et les trois
pathLight de `Village._villageProps`. Il ne modifie ni les corps des producteurs
ni leurs appels au hasard. Les lampes de passerelle gardent l'identité de leur
objet brut ; les accessoires gardent l'identité du vecteur retourné par le
producteur. Une position égale sur un autre objet ne constitue pas une liaison.

Les repères de conversion sont explicites : première coordonnée de la polyline
avec Y=0 pour une passerelle, arguments x/y/z pour un accessoire. Ils composent
la frame Builder présente à l'appel. Ils correspondent aux recettes existantes
prepare-boardwalk-frames.py et package-plaza-props.py ; aucune rotation de bras
de lampPost n'est prise pour une rotation du placement. L'ordre des deux benches
conserve l'ID de place `plaza-prop:2`, et les trois appels de passerelle conservent
les ordinals même lorsque les deux chemins latéraux n'émettent aucune lampe.
Les nouveaux pathLight ont un token source, pas encore un asset de catalogue.

Le candidat side-owners-v1 conserve l'égalité du digest Village original/observé.
Il acquiert sept bindings supplémentaires (trois lampes sur la première
passerelle, une de place, trois accessoires), soit 58/60 et 37 frames. Le lecteur
Rust v4, le contrôle indépendant des 60 phases/poses et sept refus du mapper
passent. Preuves dans `source-local-lights-side-owners-20261002-v1` ; SHA manifeste
`61e501e9781cd74a1b1f77f55d6e3d32a911e91ba193045c9ee37089b128d136`.
Les scripts de cette acquisition restent copiés avec leurs pins avant la suite.

### Déclarations des repères vendeurs — couverture source 60/60

Les constructeurs complets FishStand/Chandlery déclenchent loadStallAssets et
des chargements asynchrones. Cette acquisition exécute seulement leurs cinq
lignes de déclaration du repère, allowlistées par SHA : déclaration y depuis
terrain.heightAt, création du Group, nom, position et rotation. FishStand utilise
la ligne 24 et les lignes 26–29 ; Chandlery la ligne 17 et les lignes 19–22.
Les sources originales et les blocs normalisés UTF-8/LF sont vérifiés avant
l'exécution du premier bloc. Aucun matériel, mesh, modèle NPC, fallback, fetch
ou constructeur complet de vendeur n'est exécuté par ce helper.

Les matrices sont celles des Groups obtenus par ces blocs originaux ; les deux
bindings suivent l'ordre explicite STAND/CHANDLERY du bloc App déjà allowlisté.
Le reçu distingue cette preuve de déclaration d'une construction du modèle ou
d'un import Studio : full_vendor_constructors_executed=false,
asset_fetch_executed=false, catalogue_binding_executed=false et
studio_projection_executed=false. source_binding_complete=true signifie la
couverture de toutes les déclarations de parents du paquet, pas leur présence
dans le runtime Forge.

Preuves courantes dans
`F:/OpenRPG_Forge/.build/source-local-lights-vendor-owners-20261002-v1` :

- Acquisition : retour 0 ; 60 émetteurs, 39 frames, aucun ID source sans binding.
  SHA manifeste `6a466956e0327b6c9fd337b9a230d3219be868255cbb2ef950f6191b9f35d5d9` ;
  SHA descripteur `a395e666532bec49ea85612017bcc1b30480e60dca275184cbd05025ec7fd290`.
- Lecture Rust v4 inchangée : retour 0, 214 payloads et 212 sources fermés,
  matrices/poses, licence et enveloppe vérifiées. Aucun nouveau build prétendu.
- Contrôle indépendant : 60 phases exactes Rust/JSON.parse, IDs/ordres/poses
  monde/photométrie inchangés ; les deux ancrages vendeurs retrouvent les valeurs
  locales du bloc App à moins de 1e-12 m. Cette borne teste le round-trip de
  matrices ; elle ne change pas l'oracle GPU ni un seuil de comparaison visuelle.
- Cinq refus vendeurs : mauvais commit, seconde source absente, octets altérés,
  SHA déclaré différent et taille différente. Une Group espion confirme que
  tous les pins sont vérifiés avant toute exécution, même quand le second échoue.
  Pose/rotation terrain et round-trip sont aussi contrôlés. Retour 0.

### Projection par placement — candidat selection-v1 en validation

`PreparedSourceLights::from_package_selection` prépare une sélection explicite
d'ID de lumières pour une instance. L'ordre fourni par l'importeur ne réordonne
pas les émetteurs : leurs ordres source, phases et paramètres sont conservés.
La sélection vide, les doublons, les ID inconnus et le débordement d'offset sont
refusés. `owner_frame_ids` expose les seules clés à résoudre dans la scène.

Cette projection dérivée omet les repères inutilisés et conserve la relation
au plus proche ancêtre sélectionné. Elle ne modifie pas le descripteur canonique
et ne réapplique pas les matrices monde capturées. L'importeur doit toujours
résoudre le véritable ancrage de chaque racine restante ; une sélection ne
constitue pas une preuve de cette résolution. L'API du paquet complet garde
son contrat antérieur avec tous les repères.

Quatre nouveaux tests couvrent l'ordre et les bits source, les refus d'entrée,
deux instances indépendantes avec déplacement/remplacement/retrait, et le refus
d'un ancêtre incorrect ou d'une liaison supplémentaire. Le candidat est figé
dans `F:/OpenRPG_Forge/.build/source-local-lights-source-20261002-selection-v1`
(925 fichiers, trois overlays Engine). Les 13 tests natifs ciblés passent
(13 PASS, 0 FAIL, 600 autres tests filtrés), dont les quatre nouveaux tests ;
`rustfmt --check` passe sur les trois fichiers. Le contrôle d'intégrité vérifie
les 925 fichiers figés et les trois fichiers root possédés, sans différence.
Les résultats et commandes sont conservés séparément dans
`source-local-lights-validation-20261002-selection-v1`. Clippy et la compilation
WASM sont terminés avec retour 0 pour selection-v1. L'analyse des diagnostics
Clippy relève toutefois un avertissement possédé : `double_must_use` sur
`owner_frame_ids`, dont le type Iterator impose déjà l'attribut. Les 29 autres
diagnostics compilateur concernent le code hors de cette tranche ; aucun verdict
Clippy sans avertissement sur tout l'Engine n'est annoncé.

Le correctif retire uniquement cet attribut redondant. Un nouveau snapshot
`source-local-lights-source-20261002-selection-v2` conserve le premier candidat
et ses résultats. Son contrôle Clippy natif et sa compilation WASM passent
(retours 0) dans `source-local-lights-validation-20261002-selection-v2` ;
aucun des 29 diagnostics compilateur restants n'a de span primaire dans les
fichiers source-local-lights. Les 13 tests CPU
attestent le candidat v1 ; aucune nouvelle exécution des tests n'est prétendue
pour ce seul changement d'attribut. Aucun dessin GPU ni parcours Studio nouveau
n'est attesté par ces candidats.

Le contrôle hors Studio `Test-BuildingBindingJoin.ps1` joint exactement les
`source_binding.source_object_id` au `logical_name` du catalogue building-003
épinglé `73d29acb060eb991d7b94cd7468c3a764e8e0c440e486fa328c4748b6c5113b1`.
Les 32 lumières de 19 repères lumineux retrouvent un asset unique et son
descripteur hashé. Le repère de construction de ce descripteur concorde avec
la matrice du repère lumière (écart maximal 2.7755575615628914e-17). Ce contrôle
diagnostique n'est ni une qualification des payloads géométriques complets,
ni une résolution des instances live Studio, ni une qualification graphique.
Les autres propriétaires ne sont pas déclarés résolus par ce contrôle.

La revue indépendante du chemin de sélection relève deux protections de cycle
de vie à compléter : conserver l'ascendance attendue pour refuser une readiness
après reparenting propagé, et refuser `replace` sur un résident déjà retiré.
Les corrections et un cinquième test sont ajoutés dans le candidat selection-v3.
Ses 14 tests natifs passent ; Clippy natif et compilation WASM terminent avec
retour 0. Les 29 diagnostics compilateur restants ne concernent pas les fichiers
source-local-lights. Le contrôle final confirme les 925 fichiers figés et les
trois fichiers root possédés. Aucun nouvel essai GPU ou Studio n'est exécuté.

### Index d'identités pour l'importeur — ownership-v1

`SourceLightOwnership::from_package` interprète explicitement l'observation
`editable_ownership` d'un paquet déjà fermé et hashé. L'index vérifie la partition
exacte des ID résolus/non résolus, l'appartenance du producteur aux sources
épinglées, l'égalité de l'owner frame avec celle de l'émetteur et l'absence de
références contradictoires pour un même repère. Les assertions d'acquisition
catalogue/Studio restent false ; cet index ne constitue aucune liaison live.

La sélection par `source_object_id` est exacte et sensible à la casse, rend les
ID dans l'ordre source et n'utilise ni kind, ni nom d'affichage, ni distance.
Les propriétaires sans ce token restent disponibles comme preuves d'ancrage,
mais ne deviennent pas des identités de catalogue inventées. L'importeur devra
résoudre explicitement ces cas et les instances/copies du document de zone.

Le candidat ownership-v1 fige 927 fichiers avec sept overlays possédés. Ses 18
tests Assets ciblés passent (dont trois nouveaux tests d'ownership et seize
cas invalides dans l'un d'eux). Clippy, compilation WASM, compilation du lecteur
et lecture du vrai paquet 60 lumières terminent avec retour 0 dans
`source-local-lights-validation-20261002-ownership-v1`. Le lecteur natif a le SHA
`573a03bfc5792572437d8844e63e06054b1e91a8b5165ae2a889189baf10f3a6`.
L'index valide 60 records et retrouve 24 clés source explicites sélectionnant
39 lumières. Les 21 autres ont un propriétaire source, mais pas encore de token
de catalogue explicite ; aucun token n'est fabriqué pour les faire disparaître.
Les résultats de selection-v3 ne sont pas présentés comme des validations de
ce nouvel index.

Clippy signale seulement `doc_markdown` dans un commentaire possédé. Le candidat
ownership-v2 corrige uniquement ce commentaire. Son premier lancement démarre
avant la fin de la copie et échoue sur le manifeste forge_world absent (retour
101, conservé dans `source-local-lights-validation-20261002-ownership-v2`).
Après terminaison de la copie et garde des 927 fichiers figés/sept root possédés,
la reprise est lancée dans `source-local-lights-validation-20261002-ownership-v2-retry1`.
Clippy et WASM terminent avec retour 0 pour cette reprise, sans aucun diagnostic
compilateur dans la sortie Clippy. La garde finale des 927 fichiers/sept root
possédés passe. Les 18 tests et la lecture
du paquet attestent ownership-v1 ; aucun nouveau test/rendu n'est prétendu pour
le seul commentaire corrigé de v2.

La revue indépendante ne trouve pas de défaut bloquant dans l'index ni dans les
protections de selection-v3. Le rapport du lecteur emploie
`studio_binding_executed=false` pour la liaison live à faire par l'adapter ;
ce champ de rapport ne remplace ni ne renomme le champ d'acquisition
`studio_projection_executed`, vérifié séparément par l'index.

### Raccord des racines de bâtiments — building-bindings-v2 en validation

L'adapter Engine `tidewater_assets::local_lights` joint les tokens source de
l'index au `logical_name` exact du catalogue de géométrie. Il vérifie la même
révision source des deux paquets, un asset/révision unique, l'ancre déclarée
`construction-root` et la concordance de sa pose capturée avec le repère local
du descripteur bâtiment. Cette vérification f64 de repère utilise la même borne
1e-12 que le contrôle de jointure hors Studio ; elle ne constitue pas un seuil
GPU ou une qualification visuelle.

La préparation d'une instance conserve son `ManualPlacement`. Le staging exige
la même identité, le même asset/hash et le composant `ManualPlacementProjection`
sur sa vraie racine, avec sa transformation locale canonique. Il appelle ensuite
la projection par sélection sur cette racine propagée ; aucune matrice capturée
n'est substituée à une transformation auteur. Les copies utilisent leur propre
ID et un offset d'ordre explicitement fourni par l'adapter de scène.

Une absence de token ne prouve pas l'absence de lampes : la préparation échoue
explicitement dans ce cas. Les quatre bâtiments sans émetteur observé devront
recevoir une attestation explicite de ce fait avant une qualification complète
de cette liaison, plutôt que disparaître derrière une sélection vide. Le plugin
Studio ne consomme pas encore cet adapter ; le collecteur par vue, la publication
jour/nuit et les uniforms shader restent ouverts.

Le snapshot building-bindings-v1 conserve le premier adapter avant ajout de la
vérification de commit ; il n'a pas été validé. Le candidat building-bindings-v2
fige 929 fichiers avec onze overlays, dont un accesseur immutable de commit dans
`BuildingPackage`. Le formatage et la garde de ces entrées passent. Les tests
Engine ciblés `local_lights` passent (15 PASS, 0 FAIL, 600 autres tests filtrés).
Clippy natif et compilation WASM terminent avec retour 0 dans
`source-local-lights-validation-20261002-building-bindings-v2`. Aucun des 29
diagnostics compilateur n'a de span primaire dans les fichiers possédés de
lumières ou dans `tidewater_package.rs`. La garde finale des 929 fichiers figés
et des onze fichiers root possédés passe. Le nouveau test
du repère couvre translation/yaw, matrice différente, valeur non finie et pose
singulière. Il ne remplace pas les futurs essais de staging des vrais bâtiments.

### Résolution depuis la scène courante — building-bindings-v3

La revue de v2 relève qu'un handle `Instance` fourni par un appelant pouvait
encore désigner une ancienne racine portant le même document, notamment si un
parent live lui ajoutait un décalage. Le résultat de compilation v2 ne qualifie
pas ce cas de cycle de vie. Le candidat v3 supprime ce handle de l'API de staging :
il résout l'instance par son ID dans le `ResidentScene` courant, exige un seul
composant `ManualPlacementProjection` correspondant dans le world et refuse
toute racine avec `ChildOf`. Le document et sa transformation restent vérifiés.

La nouvelle sonde native `verify_building_source_lights` charge les deux paquets
fermés et épinglés. Elle prévoit de projeter les 32 lampes des 19 bâtiments
lumineux dans un ECS avec les vrais `ManualPlacement`, de vérifier les phases et
les poses, puis d'exercer copie/retrait et refus des racines parentées/dupliquées.
Les quatre assets sans token explicite restent signalés comme non résolus.
La sonde ne projette aucun mesh et ne crée aucun renderer ou appareil GPU.

Entrées de v3 : 930 fichiers figés, douze overlays possédés, garde PASS. Paquet
bâtiments `a53b6987563b0b0d5bb5b9113e1c3f27142baf53658a9e1a78338854f99599e6`
et paquet lumière `6a466956e0327b6c9fd337b9a230d3219be868255cbb2ef950f6191b9f35d5d9`.
Tests, Clippy incluant la sonde, compilation/exécution native et compilation WASM
sont lancés dans `source-local-lights-validation-20261002-building-bindings-v3`.
Les 15 tests ciblés passent ; Clippy termine avec retour 0 mais relève un
`needless_pass_by_value` dans le helper de pose de la sonde, en plus des 29
diagnostics préexistants hors tranche. La compilation/exécution de la sonde et
WASM restent en cours. La revue indépendante confirme que la nouvelle résolution
ferme le défaut de lifecycle ; elle ne constitue pas une exécution de la sonde.
Aucun résultat de v2 ne valide cette nouvelle résolution ni cette sonde.

Le candidat building-bindings-v4 est figé (930 fichiers) pour corriger le seul
argument de pose de la sonde, emprunté au lieu d'être passé par valeur. V3 et ses
diagnostics sont conservés. Les contrôles de v4 attendent la terminaison de v3
afin de ne pas lancer deux builds sur la même cible Cargo.

Résultat terminal de v3 : compilation native de la sonde, exécution sur les deux
paquets réels et compilation WASM terminent avec retour 0. Le reçu
`actual-ecs-proof.json` confirme 19 bâtiments lumineux et 32 lampes, les phases
préservées bit pour bit, le refus des racines parentées et dupliquées, ainsi que
la copie puis le retrait de l'original. L'écart de pose maximal est
`3.0627995499799e-6 m`, sous la borne de conversion f32 de 0,1 mm. Les quatre
identités de sheds sans token restent non résolues. L'exécutable de preuve porte
le SHA-256 `0d3ff2679486defa9c79b9748633cd3b97c2c5ca3218f6c0cb64a4d0f7c72cd0`.
Le reçu déclare explicitement aucune projection de géométrie, aucun GPU,
aucune projection Studio et aucune parité de rendu. Le succès ne qualifie donc
pas l'éclairage visible. V4 démarre ensuite sur la même cible sérialisée ; les
15 tests de bibliothèque v3 restent applicables puisque seul l'emprunt du helper
de la sonde change. Sa garde initiale et ses nouveaux résultats sont conservés
dans `source-local-lights-validation-20261002-building-bindings-v4`.

Résultat terminal de v4 : Clippy natif, build de la sonde, exécution ECS réelle
et check WASM passent (retour 0). Les 29 diagnostics compilateur préexistants
restent hors des fichiers possédés ; aucun diagnostic possédé n'est relevé.
Le nouveau reçu ECS reproduit les contrôles et le même écart maximal que v3.
La garde finale des 930 fichiers figés et douze fichiers root possédés passe.
Les résultats ne couvrent pas les modifications indépendantes du worktree.

La prochaine étape est la résolution générique package/instance/ancre par
l'importeur : déplacer ou supprimer un bâtiment ne doit pas exiger tous les
autres assets de la carte.

### Récepteur local dans le matériau bâtiment — material-lights-v1

Le matériau `VillageMaterial` reçoit une nouvelle liaison uniforme 54, neutre
par défaut. Sa conversion vérifie le paquet source avec le packer existant avant
de produire les champs Bevy. Un test compare tous les octets encase avec les
448 octets source et vérifie les refus des valeurs non finies, hors f32 et des
comptages invalides. Le récepteur WGSL adapte le hook de `LocalLights.js` du
commit `1438b1ab` : fenêtre de portée, atténuation inverse-carré adoucie, cône à
centre chaud et halo, diffuse et GGX, absorption/scattering sur la portion
immergée. Il utilise les paramètres aquatiques déjà présents sur le matériau.
Le corps local de `src/materials/LocalLights.js` est identique à HEAD du commit
figé (diff ciblé nul), SHA-256
`b8d52a6ad3fb9ccc00bafa57a7f597b34fc64daaef462a8f70a3d86088188e90`.
Les lumières locales s'ajoutent après les termes soleil/environnement ; elles
ne reçoivent pas leurs ombres, caustiques ou modulation nuageuse. L'émissif
reste inchangé. La couture shader absente, répétée ou déjà adaptée est refusée.

Le test Naga porte sur le récepteur avec une surface minimale et une entrée
fragment, pas sur les sept variantes complètes du paquet ni sur une exécution
GPU. Les nouveaux champs sont initialisés par le constructeur partagé des
matériaux, préservant le rendu sans lampe tant que le publisher de scène n'est
pas installé. Aucune collecte par caméra ni publication jour/nuit n'est ajoutée
par cette tranche. Les paramètres neutres ne sont pas une qualification de
l'éclairage visible.

Le candidat figé `source-local-lights-source-20261002-material-lights-v1`
contient 932 fichiers et seize overlays possédés, garde initiale PASS. La copie
et les hashes terminent avant le lancement des contrôles sur la cible Cargo
sérialisée. Tests Engine `local_lights`, Clippy natif de bibliothèque et check
WASM sont en cours dans le dossier de validation correspondant.

Premier résultat de material-lights-v1 : compilation des tests refusée (101),
le type `Vec` du buffer encase est ambigu entre octets initialisés et
`MaybeUninit<u8>`. Ce résultat est conservé ; Clippy et WASM ne sont pas lancés
après cet échec. Le candidat material-lights-v2 précise `Vec::<u8>` dans ce
test, sans changer le calcul shader ni la conversion du paquet.

Les 17 tests ciblés de v2 passent, dont l'ABI encase complète et le récepteur
Naga. Clippy termine avec retour 0 mais relève trois closures redondantes dans
la conversion des vecteurs ; ses 29 autres diagnostics sont hors tranche.
Material-lights-v3 remplace ces closures par la même méthode `DVec4::as_vec4`.
Le calcul WGSL est inchangé. Les contrôles de v3 attendent la terminaison de v2
et la fin de copie de leur snapshot avant de réutiliser la cible Cargo.

Le check WASM de v2 termine avec retour 0. La copie de v3 est ensuite terminée,
avec les mêmes 932 fichiers et seize overlays ; sa validation est lancée dans
`source-local-lights-validation-20261002-material-lights-v3`. V2 et ses trois
avertissements sont conservés, sans les présenter comme une qualification de
la correction v3.

Résultat terminal de material-lights-v3 : 17 tests ciblés PASS (600 autres
filtrés), Clippy natif de bibliothèque et check WASM terminent avec retour 0.
Aucun diagnostic primaire dans les fichiers possédés ; les 29 diagnostics
préexistants hors tranche restent visibles dans le reçu. La garde des 932
fichiers figés et seize fichiers root possédés passe. Ces résultats couvrent
le récepteur et son ABI, sans exécution GPU, variante complète de scène,
publication par caméra, rendu visible ou acceptation visuelle.

La publication temps/nuit et des uniforms par caméra, le bateau visible et la
comparaison graphique restent ouverts. Aucune gate, parité ni livraison de
launcher n'est clôturée par cette couverture de provenance.

### Horloge solaire source — solar-v1

Le helper d'acquisition `scripts/tidewater/capture-solar-oracle.mjs` vérifie le
commit `1438b1ab`, le diff ciblé de cinq fichiers source et leurs hashes. Il
exécute uniquement `Sky.sunDirectionFromTime` et le corps audité de
`App.updateSun`, avec les vraies classes mathématiques de la référence. Il ne
démarre pas App ni le renderer. Le premier appel refuse la frontière de méthode
à cause des fins de ligne CRLF ; une normalisation des fins de ligne du texte
de parsing, conservant les hashes des octets originaux, permet l'acquisition.
Le reçu contient 48 couples heure/azimut, dont aube, crépuscule et nuit, ainsi
que les directions soleil/lune/key et la valeur night.

Le nouveau `tidewater_sky::solar::Clock` dérive ces valeurs en f64. Il conserve
les valeurs App par défaut (16,2 h, azimut 0, vitesse 0), la latitude 24° et la
déclinaison 6° du helper appelé par App, la rotation par quaternion, le ramp
night smoothstep 0,02–0,18 et le basculement soleil/lune à sun.y > -0,07.
L'horloge utilise le delta App réel et les heures par seconde, sans multiplicateur
océan. La pause conserve exactement l'heure ; l'avancement garde l'ordre
`(hours + dt * speed + 24) % 24`. Les éditions invalides et l'overflow de delta
refusent sans modifier le dernier état valide. Les heures 0–24 et azimuts
-180–180 suivent le domaine de contrôle de la référence ; les vitesses finies
non négatives sont acceptées, avec vérification de la prochaine heure calculée.

Deux tests ciblés sont ajoutés : comparaison aux 48 résultats acquis (borne
absolue 1e-12), et pause/passage de minuit/refus atomiques. L'oracle fermé est
embarqué dans le test Rust ; Node et le clone source ne sont pas nécessaires
pour le moteur ou l'exécution des tests. Le candidat solar-v1 fige 934 fichiers
et dix-neuf overlays possédés, garde initiale PASS ; tests, Clippy natif et check
WASM sont en cours dans `source-local-lights-validation-20261002-solar-v1`.

Cette horloge n'est pas encore installée dans Studio ou Runtime. Son frame doit
être publié dans le même ordre que les LUTs/readbacks atmosphériques, la clé
directe, les ombres, le ciel et les lampes ; le nouveau calcul seul ne prouve
pas un changement jour/nuit visible et ne rend pas les lampes actives en jeu.

Résultat terminal solar-v1 : les deux tests ciblés passent (617 autres
filtrés), Clippy natif de bibliothèque et check WASM terminent avec retour 0.
Aucun diagnostic possédé ; les 29 diagnostics préexistants hors tranche restent
dans le reçu. La garde finale des 934 fichiers figés et dix-neuf fichiers root
possédés passe. Le contrôle séparé d'acquisition confirme les cinq hashes source
et l'identité de l'oracle acquis avec la fixture embarquée ; il conserve aussi
le hash du helper d'acquisition. Aucune exécution GPU ou publication de scène
n'est couverte par ces résultats.

### Collecte liée à la caméra et transfert — collection-v1

L'inspection de Studio confirme que le mode temporel source prépare la vraie
`ViewPacket` dans RenderWorld ; il retire alors la ViewPacket de Main. Un
publisher limité à lire ce composant dans PostUpdate ne couvrirait donc pas
le mode source réellement installé. Aucun publisher de ce type n'est ajouté.

`source_local_lights::collection::Collection` est ajouté comme cache dérivé,
clonable et extractible vers le renderer existant. L'adapter fournit la caméra,
l'inventaire explicite et les Settings (dont night issu de l'horloge solaire).
La collecte utilise `scene::harvest` après propagation puis le vrai Collector.
L'identité Main de caméra est conservée ; `for_view` refuse une autre identité
même avec une pose identique, avant la comparaison de pose existante. Échec de
collecte et changement de lampe torche invalident la publication ; l'historique
numérique reste disponible pour une nouvelle collecte valide. Aucun renderer,
phase aléatoire, caméra de secours ni modèle auteur ne sont créés.

Le récepteur matériau ajoute `Uniform::from_frame(frame, expected_tag)`, qui
refuse un tag différent avant conversion des 448 octets. Le tag courant reste
fourni par le véritable owner de préparation ; cette méthode seule ne prouve
pas que l'appelant utilise la dernière frame.

Le nouvel essai ECS utilise deux vraies Camera3d de même pose, une lampe live et
les Frames préparées : transfert cloné, refus de l'autre caméra, refus d'un
serial matériau différent, inventaire incomplet puis récupération, et
invalidation/réactivation de la torche. Il ne démarre pas RenderApp, ne projette
pas de mesh et ne valide pas l'extraction GPU. Collection-v1 fige 935 fichiers
et vingt-et-un overlays possédés ; tests ciblés, Clippy et WASM sont lancés après
la fin de copie et la garde d'intégrité initiale.

La collecte n'est pas encore installée dans Studio/Runtime. Il reste à charger
le paquet de lampes, projeter/réconcilier ses vrais owners, publier night et les
positions en ordre, extraire cette collecte, puis transférer le paquet vers les
liaisons effectivement préparées des matériaux dans le monde de rendu. Le
matériau neutre et les essais CPU ne constituent pas ce raccord GPU.

Résultat terminal collection-v1 : 18 tests ciblés PASS (602 autres filtrés),
Clippy natif de bibliothèque et check WASM terminent avec retour 0. Clippy
relève trois `doc_markdown` dans deux commentaires de Collection, en plus des
29 diagnostics préexistants hors tranche. Collection-v2 corrige uniquement les
backticks de ces commentaires ; le résultat v1 et ses avertissements restent
conservés. La validation v2 reprend Clippy et WASM après fin de copie et garde
d'intégrité, sans répéter les essais comportementaux inchangés.

Résultat terminal collection-v2 : Clippy natif et check WASM passent avec
retour 0 et aucun diagnostic possédé (29 préexistants hors tranche). Le reçu
`doc-only-change.json` confirme que le texte fonctionnel de Collection est
identique à v1 après exclusion des commentaires de documentation ; les 18
tests v1 sont donc conservés comme preuve comportementale, sans prétendre les
avoir réexécutés sur v2. La garde finale 935 fichiers/vingt-et-un overlays passe.
Les limites d'installation, d'extraction réelle et de transfert GPU restent
inchangées.
