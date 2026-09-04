# Gate 1 — berges intégrées et relais des arbres

Date : **2026-09-04**. Amélioration visuelle bornée, pas passage de Gate 1.

- Moteur/natif exécuté : `df6d350b0bca572d34c790eb14ec99963fd7f8d3`, arbre
  `18882dda392bda5b06190dc1b885473574458041`.
- Browser exécuté après correction Studio : `d1895ecb707097d0ab146576d24c4ba0476b4f26`, arbre
  `86a91db3b1af13fc5a4a918eb95240bd80f01360`.
- Entre les deux, seul `apps/forge_studio/src/lib.rs` change : le contrôle de stabilité utilise
  désormais les nouvelles distances. Moteur, runtime, shaders et données sont identiques. Les deux
  acquisitions restent liées à leurs commits respectifs ; ce n'est pas une paire au même commit.

## Changement livré

Les berges réutilisent le matériau PBR du terrain, son détail procédural et sa projection météo.
Chaque rive compte cinq points transversaux au lieu de deux : **1 370 sommets / 2 176 triangles**
pour les berges ; l'eau reste à **411 / 544**, soit **1 781 / 2 720** au total. Les 137 sections et
les données canoniques sont conservées. Les requêtes terrain des berges passent à 1 096, total 1 507
avec l'eau : ce coût de construction est assumé, aucun gain de performance n'est annoncé.

Le bord intérieur reprend exactement le bord CPU de l'eau. Le bord extérieur conserve la hauteur
terrain + 1,5 cm ; les points intermédiaires suivent le terrain et amortissent l'élévation du bord
d'eau. Les poids terre se mélangent aux poids terrain sur **5 m effectifs**, bornés par l'épaule
existante. La demande canonique de **9 m** reste enregistrée, sans prétendre l'appliquer entièrement.
Normales reconstruites, winding positif et poids finis vérifiés. Aucun carving ni collider modifié.

Tous les arbres éligibles du disque existant de 140 m utilisent les trois modèles CC0 déjà présents.
Le Browser observe **161 arbres, 33 arbustes, 18 fougères, 22 rochers et 4 détails**, soit 238 instances
sous le plafond inchangé de 512. Les 27 fichiers gouvernés / 16 652 493 octets sont inchangés.
Aucun asset ajouté, aucun asset Cadle copié ; identités, positions, collisions et sources conservées.

Après chargement complet seulement, les Near des arbres remplacés restent cachés ; Medium et
Individual prennent le relais au seuil CC0, avant leur fondu HLOD existant. Bornes dérivées communes :
Low 35–82 m, Medium 43–104 m, High 51–120 m, Ultra 67–162 m. CC0 est visible strictement avant son
seuil ; le relais inclut celui-ci. `hidden_procedural_entities` compte les 161 Near, pas les relais.
Le fallback de chargement conserve la végétation procédurale. Qualité, relais et visibilité CC0
s'appliquent dans cet ordre avant propagation de visibilité ; les uniforms végétation consomment
la qualité appliquée dans le même PostUpdate. Aucun nouveau compteur dynamique ni shader glTF.

## Vérifications

- Formatage, Clippy workspace/all-targets strict verrouillé/offline : PASS.
- Suite workspace/all-targets : **380/380 PASS**, dont moteur 154, runtime 76, monde 53, Studio 35.
  Tests ajoutés/étendus : géométrie des berges, poids, déterminisme, sélection sans déplacement,
  relais et vraie transaction Low → Ultra → Low à 80 m, cycle météo sur deux matériaux distincts.
- Check moteur WASM et Clippy Studio WASM `web` seul : PASS. Après correction Studio : **35/35**
  tests Studio et Clippy WASM strict PASS. Avis tiers de compatibilité future `proc-macro-error2`
  conservé ; dépendances inchangées, pas de nouvel audit de licences revendiqué.
- Release natif : **3 min 22 s**, puis `Test-Gate1VisualProof.ps1 -Quality high -NoVsync -SkipVideo` :
  PASS automatisé, **29 PNG 1920×1080**, 12 diagnostics de passes, gameplay/Rapier exercé,
  stdout/stderr vides. Les 29 tailles/hashes sont revérifiés.
- Browser : `trunk serve --release --locked --no-autoreload --config Trunk.toml`, commit injecté
  par `FORGE_SOURCE_COMMIT`, Cargo offline. Driver `Invoke-Gate1BrowserWeatherMatrix.ps1` inchangé,
  Edge **152.0.4191.62**, profils temporaires isolés hors sandbox : **PASS automatisé Low/High**,
  6 checkpoints / 12 PNG, 4/4 bundles exacts, console page 0 erreur/0 warning, réseau 0 échec.
- Les trois cycles sec → pluie → sec enregistrent wetness **0 → 0,78 → 0**, deux matériaux terrain
  présents et uniforms conformes. Les champs d'attribution précise GPU et de revue humaine restent
  faux. Canvas Browser 1408×966 : comparaison qualitative, pas pixel à pixel avec le natif.

### Échec conservé

Premier Browser `df6d350b0bca-20260904T090401883Z` : **ABORT après 120 s, zéro PNG**. Le contrôleur
avait atteint son premier checkpoint, mais le fingerprint Studio comparait encore les anciennes
distances et réinitialisait sa fenêtre ; le driver ne recevait pas `frame.warmup_samples`.
La correction ciblée `d1895ec` utilise la même dérivation que la readiness, sans modifier le driver
ni ses seuils. [Reçu d'échec](banks-trees-df6d350.browser-abort.json), original SHA-256
`4a83ea81d35f100f5d4d6bb462defe3a1f148ed43a409215b6f51bccd5e9e242`.

Les deux tentatives affichent aussi les messages hôte Edge de service de mise à jour et
`GPU state invalid after WaitForGetOffsetInRange` en fin de tier. La console de page du rejeu est
propre ; cela ne prouve ni une absence de messages hôte ni leur cause. Serveurs et navigateurs du lot
arrêtés après acquisition.

## Preuves

Racine native : `E:\OpenRPG_Forge\.gate1-tools\evidence\banks-trees-df6d350-high-release-20260904`.

- [Validation native](banks-trees-df6d350.native-validation.json), original SHA-256
  `bf99c7a1809199a1facebcd41e863b6a4b712e261a025a105c35e594a24d685b`.
- Ledger original SHA-256 : `ba44b79bd672fec9dc5616af48c486103d804bd8a5edece3821a6daba0c0dcb6`.
- Binaire conservé : `E:\OpenRPG_Forge\.gate1-tools\binary-controls\banks-trees-20260904\forge_runtime-df6d350.exe`,
  SHA-256 `fbd95e3076bf0d9b2a76839cec14d2fc9ac32e3bc7c9255103b9186527277fd6`.
- PNG copiés sans transformation : [vue humaine](native/df6d350b0bca572d34c790eb14ec99963fd7f8d3/banks-trees/human-clear.png)
  et [berges](native/df6d350b0bca572d34c790eb14ec99963fd7f8d3/banks-trees/water-clear.png).
  Leurs tailles/hashes sont ceux du reçu natif.

Racine Browser : `E:\OpenRPG_Forge\.gate1-tools\evidence\banks-trees-df6d350-browser-20260904`.
Run PASS : `d1895ecb7070-20260904T091028020Z`.

- [Manifeste](banks-trees-d1895ec.browser-manifest.json), 28 artefacts revérifiés par taille/hash ;
  original SHA-256 `e318b5472bd290b378bf86cdd4f57a767f5520ce507a7124a18cd86fc980d023`.
- Ledger `r1-023-025-026-browser-weather-matrix-ledger.json`, SHA-256
  `5029f2fb045b47526d99b1db482112d464834ae0345871044c14db75eb2c9f33`.

Les trois JSON suivis sont des copies sémantiques ; leurs chemins relatifs sont ancrés dans les
racines/run externes indiqués, pas dans ce dossier Git.

## Inspection et limites

Avant/après natif contre `68af815` : cinq poses fixes identiques vérifiées, même machine et profil,
animation non figée. Deux inspections d'agent des vues humaine/eau concordent : les larges bandes
brunes s'intègrent mieux au sol, les arbres proches gagnent en volume et variété. Vues distante,
pluie native, Low clair et cycle High Browser inspectés ; aucune corruption flagrante observée.

La rivière reste anguleuse, des contours géométriques persistent, l'herbe proche reste épaisse et
anguleuse et le lointain est répétitif. Les modèles glTF n'héritent pas du vent/wetness procédural.
Le passage CC0 → procédural reste abrupt ; ce lot rétablit la couverture, pas une transition de
silhouette parfaite. Clearance aux sommets seulement, pas preuve d'absence d'intersection entre
triangles ni scellement des vagues ±0,12 m. Pente canonique, érosion et bord humide permanent différés.

**Gate 1 reste ouverte à 43/55 ; Gate 2 non commencée.** Pas de qualification humaine, de parité
Grassworks/Cadle, de benchmark cible ou de PASS supplémentaire pour R1-014/023/025/026/046–052.
ADR-0010 reste appliqué : aucune campagne PresentMon, micro-optimisation ou qualification de cadence.
CI distante **NOT RUN — billing**, sous ADR-0006.
