# Tidewater — programmes source sur le RenderDevice Bevy

Suite du [lecteur et packing](2026-10-02-boat-shader-loader.md).
Les vingt programmes WGSL existants créent des pipelines validés sur le GPU
natif de Bevy. Aucun bateau n'est encore dessiné par cet adaptateur.

## Contrat et intégration

`forge.source-mesh-shaders` v2 conserve la politique matériau `raster`, les
overrides de passe et le descriptor GPU explicite. Le lecteur compare topology,
cull/front-face, ordre/blends/write-masks des cibles, profondeur/biais et
multisample aux règles du SDK épinglé. `late` dépend de la couche d'objet et
reste indépendant de l'opacité. Les overrides de biais de passe s'appliquent
uniquement Depth, conformément au SDK ; Main/Color utilisent les biais matériau.
La présence du fragment Depth correspond à `NEEDS_DEPTH_FRAGMENT`.

Le schéma v1 reste lisible pour le transport historique ; il ne permet pas la
préparation GPU sans politique v2. Le module générique `source_mesh_pipeline`
reçoit le `RenderDevice` et le `RenderAdapter` du RenderApp existant. Il ne crée
ni Instance, ni device, ni queue. Il contrôle les limites déclarées, même pour
des ressources inutilisées par le WGSL, et les capacités des formats.

La préparation utilise l'API de shader validée, conserve les layouts 0/1 et le
Draw dynamique 2, les streams vertex et l'identité `(material, pass, late)`.
Les scopes WGSL puis layout/pipeline sont réellement attendues. Elle ne fournit
pas de textures, buffers de scène ou bind groups et ne soumet aucun dessin.

## Entrées de la preuve

Fixture privé `F:/OpenRPG_Forge/.build/boat-shader-pipeline-fixture-356` :
manifest SHA-256 `aa13279bf0e1bbc30e6d2334ffd0c251fb790a4b747420439eba4752c10d78b3`.
Les vingt WGSL sont inchangés par rapport au paquet source 350.
Les politiques sont dérivées de six fichiers SDK intacts au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`, et non acquises dans un navigateur.
Le reçu `DERIVATION-RECEIPT.json` épingle ces fichiers et le manifeste ; SHA-256
`56f16e46a19b9af6191512885bf51797616d67761484d92dfc8fcc5033d1a120`.

Son inventaire App reste explicitement incomplet : `boatTrap/main/late=true`
manque, la variante historique `late=false` est conservée. Le rapport natif
inclut ce reçu vérifié et garde `application_variant_inventory_qualified=false`.
Voir l'[erratum de l'acquisition 344](2026-10-02-boat-source-shaders.md).

## Validation locale

Sources figées 357, 869 pins ; artefacts dans
`F:/OpenRPG_Forge/.build/boat-shader-pipeline-validation-358`.
Rust 1.98.0, dépendances verrouillées/offline, profil sans optimisation ;
ce profil ne mesure pas la performance du jeu.

Les essais `native-first` puis `native-final` terminent avec retour 0 : vingt pipelines créés et
validés sur AMD Radeon RX 7800 XT, backend Vulkan. Quatre refus attendus
passent : manque de budget textures, manque de budget uniformes, WGSL invalide
dans la scope shader, visibilité Frame incompatible dans la scope pipeline.
La revue indépendante du SDK/fixture trouve zéro écart sur les vingt politiques.

| Contrôle final | Résultat |
|---|---|
| build exemple Engine natif | exit 0 |
| check Engine lib WASM/web | exit 0 ; sans exécution navigateur |
| Clippy Engine lib/exemple natif | exit 0 ; aucun diagnostic dans les deux nouveaux fichiers, 29 ailleurs |
| assets : tests lib, build exemple, Clippy strict, check WASM | exit 0 ; 51 PASS, 3 ignores préexistants |
| lecture v2 et packing original | 20 programmes, 18 blocs, 188 sites et 2 336 octets exacts |
| négatifs de contrat/intégrité v2 | 33 refus attendus avec manifeste re-signé pour les mutations sémantiques |
| overrides raster cohérents | cull de passe et compare matériau acceptés |
| compatibilité lecteur v1 | paquet original 350 : PASS, 11 refus attendus |
| compatibilité empaqueteur v1 | paquet 360 : manifeste byte-identique au 350 |
| CLI GPU : v1, hash reçu faux, arguments absents | trois sorties 1 attendues, avant préparation GPU |

Les 869 pins figés restent intacts. Les quatre sources propres à ce raccord
correspondent au checkout. Le `lib.rs` Engine et cinq fichiers navigation ont
évolué ailleurs : cette preuve ne qualifie pas l'intégralité du checkout courant.
Les logs et échecs précédents restent conservés, notamment l'import manquant
du premier vérificateur v2 et les huit warnings initiaux corrigés.

Binaires conservés dans `boat-shader-pipeline-validation-358/artifact` :
`verify_source_mesh_pipelines.exe`, 155 254 272 octets, SHA-256
`c901cd6e8dad200551a0de9c2fece72878f9e30ddda069bd8385d489463bab18` ;
`verify_source_mesh_shaders.exe`, 1 823 232 octets, SHA-256
`ed9c195fdf5a860948765e9133028ef5714f4b924c4135899f8b80f67fa0ec64`.
Le reçu d'artefact, `source-guard-final.json`, les résultats terminaux et
`native-final.json` conservent identités et limites.
L'audit final indépendant vérifie les empreintes et ces scopes sans relancer
le GPU : `FINAL-INDEPENDENT-AUDIT.md`. Il confirme les vingt programmes, les
quatre refus GPU et la propagation de la variante manquante ; aucun verdict
de scène complète n'en est déduit.

## Limites et suite

`gpu_pipeline_creation=true` ; `scene_resources_supplied`, `draw_executed` et
`appearance_qualified` restent faux. Les limites très larges de l'adaptateur
Vulkan ne prouvent pas l'admission sur un device WebGPU de navigateur. Une
compilation WASM ne remplace pas une exécution WebGPU.
La capture source 344 enregistrait un device avec 32 textures, 16 samplers,
12 uniform buffers et 10 storage buffers par stage. Les variantes main déclarent
20 textures fragment : un device limité à 16 doit être refusé par l'adaptateur,
pas traité avec des bindings supprimés ou neutres.

Restent à acquérir la vraie variante tardive des pièges et à fournir les
ressources vivantes : Frame, textures matériau/environnement, éclairage,
ombres et eau. Puis raccorder les passes au bateau résident, à la scène
jouable et aux objets éditables Studio, et comparer les frames natives/WebGPU.
Les contrôleurs, le gameplay maritime et la parité de l'île restent ouverts.
L'objectif complet reste actif, sans publication.

Le diagnostic statique `LIVE-RESOURCE-MAPPING.md` identifie les providers
existants, les repacks et les vues D2 à ajouter. Le principal provider absent
est le packet source de lumières locales de 448 octets. `CPU-SOURCE-COMPOSITION-PATH.md`
constate que le compositeur source construit directement des layouts GPU ;
il ne fournit pas une voie CPU autonome pour la variante tardive manquante.
Ces deux diagnostics ne constituent ni des bind groups vivants ni un dessin.

Suite : [collecteur des éclairages locaux source](2026-10-02-source-local-lights.md).
Le raccord de 448 octets est implémenté et en validation ; ses sources de scène
et sa publication RenderWorld restent à fournir. Aucun dessin ni verdict visuel
n'est déduit de cette nouvelle tranche.
