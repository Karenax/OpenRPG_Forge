# Tidewater — propriété temporelle dans RenderWorld, preuve native270

Rapport local du2 octobre2026. **Le nouveau run270 passe les contrôles d'encodage natif pipeliné**, après réparation du redimensionnement. Son audit indépendant passe40 contrôles ; les preuves et sources sont figées dans271. Le run262 terminé à `2026-10-02T00:08:11.2065982+00:00` reste **FAIL** et sa chronologie est conservée ci-dessous. Les validations CPU et compilations concernent leurs entrées épinglées ; aucun résultat numérique, visuel, Studio ou de fidélité complète Tidewater n'est promu par ce rapport.

Les sources Tidewater de référence restent au commit `1438b1abfcaee3267092b75573014f4d9b4a983c`, avec Bevy0.19.1. Les reçus locaux sont conservés sous `E:/OpenRPG_Forge/.tidewater-integration`. Ce fichier est une création nouvelle : aucun rapport antérieur, planning, source produit, reçu ou mémoire n'a été modifié pour sa rédaction.

La fondation 255 place une seule `Sequence` sélectionnée derrière un `Mutex` dans `render_owner::Owner`. `prepare` prend la caméra réellement extraite sans avancer Halton ni la caméra précédente. Une réservation valide tag et epoch avant les mutations de résolution, puis garde le verrou jusqu'au commit ou au drop. Le commit accepte exactement un draw et les deux copies déclarées ; le token est cloné avant `Sequence::commit`, dont les erreurs précèdent toute mutation. Un refus conserve donc pending, phase et dernier historique accepté. Reset, switch et désélection invalident les anciens tokens ; le serial global reste monotone. Cette fondation ne constitue ni un système Render installé ni une preuve d'encodage GPU.

La tranche 260 ajoute `raise_serial_floor(&self, floor:u64)->Result<(),Error>` pour adopter les serials déjà émis par le chemin legacy. Seul `last_serial=max(last_serial,floor)` change. Un floor supérieur avec pending renvoie `SerialFloorWhilePending` sans toucher tag, historique, sélection, compteurs ou diagnostics ; un floor inférieur ou égal est un no-op. `floor=u64::MAX` empêche la préparation suivante avec `SerialExhausted`, sans avancer la séquence. Les quatre cas floor et le chemin réel `Error::Temporal` au commit ont été testés dans le petit harnais CPU. L'injection stale est privée au test ; elle ne crée pas une nouvelle voie produit. La séquence pure187 n'a pas été modifiée.

La revue 256 établit un écart conditionnel pour les modèles rigides après une frame beauté Bevy préparée/rendue sans résolution SourceResolve réussie. La caméra conservée peut venir de la dernière résolution A, tandis que le modèle précédent Bevy vient de la frame intermédiaire B. Dans la route GPU normale de Bevy, `MeshUniform.previous_world_from_local` provient du précédent `MeshInputUniform`, sauvegardé pendant la préparation mesh ; la valeur `.0` de `PreviousGlobalTransform` n'est pas la matrice utilisée dans cette route. Modifier uniquement ce composant ne réparerait donc pas ce cas. La revue est statique et n'observe pas cet écart dans un runtime donné.

La réparation bornée 258 conserve le Packet à neuf membres et 336 octets. Dans `phase_epoch_seed.w`, le masque 1u demande le seed d'historique, et le masque 2u impose explicitement previous-object=current avant multiplication par le VP précédent. Village, FishStand et StandardOpaque suivent ce helper. Les mondes déjà statiques terrain/plantes/eau et les transparences source conservent leur contrat ; Sky garde son trajet de direction W=0. Un resize sans trou reste flag 1, sans reset du modèle. Un reset/recovery exige à la fois le seed caméra cohérent et le flag 2 ; ce flag seul ne met pas arbitrairement la vitesse caméra à zéro. Les trois nouveaux tests 258 ont ensuite passé dans Tests247.3. Cette politique perd l'accumulation antérieure au trou ; elle ne démontre pas une continuité source ni un oracle GPU d'objet mobile A/B/C.

L'association 257 introduit `CaptureSource { view: Entity }`. `view` reste l'identité MainWorld de la caméra source, copiée par extraction sans remapping. Le raccord Render la compare à `MainEntity.id()`, puis écrit les composants sur la vraie Entity Render correspondante. Le mirror renseigne l'association avec la pose/projection de la capture jetable et la met à jour au retarget ; les caméras authored ne sont pas modifiées. Cette tranche ne possède pas de séquence et ne crée pas de nouveau jitter. Son harnais Bevy réel a passé quatre tests ; l'essai2 n'émet aucun warning.

Le raccord 259 installe Owner dans RenderApp et expose à Main un rapport de télémétrie. Les acks Main n'avancent plus la Sequence Render. Le passage legacy→Render adopte le floor 260 ; le retour automatique au legacy est refusé après adoption. Le packet/jitter Render est préparé après ExtractCommands **et avant PrepareAssets**. Cette position est nécessaire car la spécialisation mesh PBR lit `TemporalJitter` dans PrepareAssets, avant Queue et la préparation des uniforms. Attendre PrepareViews aurait pu combiner une clé de pipeline sans jitter et des uniforms jitterés. Les commandes d'insertion doivent être appliquées avant ce premier consommateur.

Le même raccord distingue le jitter fraîchement extrait d'un autre consommateur d'un ancien marqueur Forge. `JitterIntent` atteste Main après son système de jitter ; capture et source sont identifiées par `CaptureSource`/`MainEntity`. Un jitter étranger est préservé puis la voie Source refusée, sans le supprimer au motif qu'un ancien marqueur Forge persiste. La capture Source reçoit l'offset à l'échelle de sa taille, avec pose/projection source égales. Switch/désélection/cut retirent les allocations actives sans recycler leurs générations. Après un commit suivi d'un trou, la reprise ouvre une instance seedée cohérente avec 258. Ces faits sont issus de la revue 259 et des tests CPU ; le run 262 ne les qualifie pas globalement à l'exécution.

| Preuve locale | Résultat acquis et portée |
|---|---|
| Owner 255, harnais isolé | Typecheck/link CPU réussis ; 22 tests PASS : 15 Owner, 5 Sequence187, 2 ECS/ABI. |
| Owner 260, harnais isolé | Typecheck/link CPU réussis ; 27 tests PASS : 20 Owner, 5 Sequence187, 2 ECS/ABI ; aucune dérive entre pins. |
| CaptureSource 257, essai2 | Typecheck/link CPU réussis ; 4 tests PASS, journaux de compilation vides. |
| Check247.3 | FAIL conservé : enum `ManageViews` absent et `.after(resolve::encode)` ne satisfaisant plus le contrat système. Les réparations du parent restent des essais distincts. |
| Check247.5 | Compilation metadata 0, journal vide, `source_drift=[]`. |
| Library247.3 | Compilation 0, journal vide, `source_drift=[]` ; bibliothèque réelle du moteur construite avec les dépendances natives épinglées. |
| Tests247.3 | Compilation 0 et sortie tests 0. Sélection temporelle : 89 PASS, 0 FAIL, 1 ignored, 474 filtered out. Le test ignoré des imports source est ensuite exécuté explicitement : 1 PASS. Ceci n'est pas l'exécution de toute la suite moteur. |
| Example247.3 | FAIL conservé : résolution du module `temporal_owned` sans le chemin nécessaire. |
| Example247.4 | FAIL conservé : emprunts mutable/immuable concurrents dans `proof.counts[proof.case]`. |
| Example247.5 | Compilation 0, journal vide, `source_drift=[]` ; exécutable prêt pour le run 262. |
| GPU/runtime 262 | **FAIL**, processus fermé après 21,9033588 secondes ; aucun PASS global d'encodage ou de readback. |

Les artefacts compilés ont été relus et leurs SHA comparés aux reçus : `Library247.3/libforge_engine.rlib` vaut `523f781f21ff13cdd3e3675ddbb3f72bb7ba963a2937665c61fe2cc168750f00` ; le binaire Tests247.3 vaut `e282fbec01bf08f52e4d570aa7ccecb091032584a2d15ed339462c10063e2db2` ; le binaire Example247.5 vaut `5050ef8e42531ca68fe650184f9d7aa6e04be7b04f47ee3b7102c734c87002f4`. Le run 262 désigne exactement ce dernier exécutable. Il utilise le vrai rendu natif pipeliné configuré par le probe 261 ; aucune compilation ni application n'a été lancée pour rédiger ce rapport.

Le probe 261 prépare les cas statiques sur 32 phases, déplacement caméra, reset explicite, resize 1919×1079, intervalle inactif de deux frames Render et reprise sur la même caméra. Ses observations joignent Owner, packet, caméra/capture extraite et observations auxiliaire/HDR/resolve du même tag/epoch/générations. Elles concernent des **commandes encodées** et une télémétrie de threads. Des compteurs ou observations CPU d'encodage ne sont pas des fences de complétion GPU ni des pixels lus. Le reçu 261 est initialement `prepared-not-compiled-or-run` ; Example247.5 puis le lancement262 sont des preuves ultérieures distinctes, sans modifier ce reçu historique.

Dans 262, le premier panic du journal est l'assertion `fixed water capture size` de `examples/tidewater_plant_probe/water_scene.rs:66` : taille réelle `(1919,1079,1)` contre attente fixe `(1920,1080,1)`. La profondeur Main redimensionnée était utilisée dans ce chemin du helper natif. Le journal montre ensuite un panic Vulkan pendant la destruction d'un `SwapchainAcquireSemaphore` encore associé à une `SurfaceTexture`. L'ordre des journaux établit que l'assertion de taille vient d'abord ; il ne démontre pas à lui seul une panne de device indépendante. Le reçu 262 conserve `status=failed`, exit `-1073740791`, `process_closed=true`, et les journaux d'origine. La réparation et sa prochaine exécution appartiennent à une preuve suivante ; 262 reste FAIL.

La parité numérique/source reste ouverte. L'audit 249 du runtime 253 indique `transport_passed=true` et `numeric231_status=failed`, avec `qualifies_original_f32_identity=false` et aucune qualification complète source. La comparaison 253/236 conserve également le FAIL numérique 236, même lorsque les 21 plans source compacts et uniforms 336/32 sont byte-exacts. Aucun oracle 231/249, seuil, entrée, plan ou verdict antérieur n'est modifié ici. Les réussites CPU, la compilation et l'avancement de l'architecture Render ne changent pas ces FAIL.

Restent à établir dans de nouvelles preuves : une exécution Render-owned complète après réparation du resize ; les nouveaux Source7 readbacks et leurs vrais callbacks couplés au tag Render ; l'oracle numérique sur les plans réellement lus ; le cas objet rigide mobile avec trou/reprise ; la synchronisation des captures et du jitter dans Studio, dont le retour vers BevyTAA ; le chemin WASM/WebGPU ; et l'invalidation Owner/allocateurs/caches sur perte et recréation du device. Le run 262 avait `FORGE_TIDEWATER_TEMPORAL_RESOLVE_READBACK=0` et n'apporte aucune nouvelle capture Source7. La revue 259 constate que les allocateurs persistants pourraient encore garder des textures d'un ancien device si leur description ne change pas ; aucun deviceLost n'a été provoqué. L'acceptation visuelle et la livraison exécutable restent des résultats séparés.

Les SHA256 des reçus ci-dessous ont été recalculés pendant la rédaction. Seize copies historiques et sorties compilées référencées ont aussi été comparées à leurs empreintes épinglées, sans mutation. Les empreintes décrivent ces preuves historiques ; elles ne prétendent pas figer les sources produit après les travaux suivants.

| Reçu, chemin relatif à `E:/OpenRPG_Forge/.tidewater-integration` | SHA256 |
|---|---|
| `render-temporal-owner-foundation-255/freeze-255.json` | `b1e580bb0a634e416caba00220b8fec5bee674622349f7ee3466004bba5f511d` |
| `render-temporal-object-history-review-256/receipt.json` | `2e7f4d639aea86b992d0046f37cee536d2cd1aece6a819c79ad36603a738cdb7` |
| `refraction-render-association-257/freeze-257.json` | `35593482565d591a97015e704b427b71f7a5c228ad220319aeae11bdbeccfcb9` |
| `render-temporal-recovery-model-258/receipt.json` | `7bb43bf0659be467bf3cb5c1a0de259b96c98cb385e310c5a27aaa522ae0f770` |
| `render-temporal-adapter-review-259/review-inputs.json` | `9cea4666dc2a625f820bc9c0b268da119e25957e5f3d6c271eda5144945a027d` |
| `render-temporal-owner-serial-floor-260/freeze-260.json` | `845ce137285cfa62fc70c2362df2f13f17ed7913ab5b9ad838dbd196ace2fb5b` |
| `render-temporal-pipelined-probe-261/receipt.json` | `1bd7471f82c2aab5d53e595231c89d98ff6364f792fa6017e955d2b49c9821f8` |
| `temporal-trace-native-build-247/check.3.json` | `cedc402bf1c095b05c49f27ffea6218789ebf918f3b4af0dc0067f85cb81bff2` |
| `temporal-trace-native-build-247/check.5.json` | `9ebca232bac1db83f919d9a849a27127a31c7b07a43ada8850fb9db4c9142660` |
| `temporal-trace-native-build-247/library.3.json` | `fea8795dd95c76817b2a30c16073706eba566d63909d405f127a8c9f3a30fedd` |
| `temporal-trace-native-build-247/tests.3.json` | `ac66a4f1586a49014b9404ea6051168475b35d7a525a3b97e07648d4cedb7a31` |
| `temporal-trace-native-build-247/tests.3/source-imports-explicit-3.log` | `0be65a1d223922f4fba1a0cf009b242731c4e852440b0417d68bbc919dee212e` |
| `temporal-trace-native-build-247/example.3.json` | `a8cc6c59fb94ae43a85a24a2f6cc4a7ace80b96ce7b7a9425d81571eb287f120` |
| `temporal-trace-native-build-247/example.4.json` | `53c1520e5e2266812547229a75ec5a745980ef41ee3cec353cde8f88627bde4b` |
| `temporal-trace-native-build-247/example.5.json` | `dc5f72859f7bbdd1d894dde143d1f58cd527a7863b4159803803587a3626457b` |
| `temporal-owned-native-runtime-262/run.json` | `ffda7d06f7994eba476cdeb36089cecd48490dda30eca6847dbff78924cdec43` |
| `temporal-owned-native-runtime-262/stderr.log` | `2fd77a016516a8c18546d3c4d51ec8bb77d3c50475a4a3e2f03410d826c31681` |
| `temporal-trace-native-runtime-253/trace-audit-249/audit.json` | `07223edd3353ffeb2ae9e9e33da869767552a440e444e452fe918aec5ec322d1` |

Copies source vérifiées : Owner 255`00cc5bca…06bde0d`, Owner 260`c710fbe0…0cf1ef`, Packet 258`bdd5654d…b50346`, shaders/tests 258`79e64203…8f5710`, raccord 259`78f30a04…caeb08`, probe 261`6dae219f…af7934`. Les copies complètes et empreintes non abrégées sont dans les reçus et leurs dossiers associés.

## Correctif 264, contrôles 268 et exécution pipelinée 270

Le [correctif de profondeur264](../../../../.tidewater-integration/render-temporal-probe-depth-resize-264/REPORT.md)
conserve le même Handle d'Image et redimensionne sa texture à la taille physique
réelle après CameraUpdate, avant AssetEvent. Les résolutions des uniformes eau
et embruns, ainsi que les bindgroups des trois matériaux consommateurs, suivent
ce remplacement. Dans Render, le vrai GpuImage est préparé et contrôlé avant les
préparations de ces matériaux. La copie garde les assertions de taille entière,
format et sample count ; aucun rectangle partiel ou simple suppression du refus
262 n'est utilisé. Seul `examples/tidewater_plant_probe/water_scene.rs` est modifié
pour ce correctif ; `water_optics.rs` reste byte-exact. Son SHA source est
`f5e7459a0b00d1ad6d83a59882ab8650fa90bcbc80aabf6ed9e5b5e3a4b11090`.

Le probe reçoit séparément des contrôles supplémentaires : phase suivante
comparée au dernier commit Render réel, epoch/seed de resize, seed initial/reset
en phase0, puis compteurs stables entre deux reçus Render sans caméra active.
Tous les échantillons restent conservés jusqu'à une limite de4096 ; tout
débordement de mailbox ou de preuve refuse un PASS incomplet. Six fichiers
create-new `probe.owned.stage-N.json`, synchronisés avant publication de l'intent
suivant, portent uniquement `incomplete-progress-only`. Ils préserveraient les
diagnostics si une étape ultérieure paniquait. Ils ne remplacent jamais le verdict
final. SHA du probe : `90070ca23d42c03d43d4fb6c3813338ba6b2cbdf2ff52af08681c1b2b153e568`.

Example247.6 compile sans warning ni dérive, mais le
[préflight265](../../../../.tidewater-integration/temporal-owned-native-runtime-265/preflight.json)
refuse de lancer le GPU : son input `src/lib.rs` diffère de celui de Library247.3.
La comparaison aux copies245/254 retrouve uniquement le déplacement du bloc
`#[cfg(feature = "physics-rapier")] pub mod gameplay;` ; les contenus sont exactement
égaux une fois ce bloc unique retiré. Aucun changement préexistant n'est restauré.
Library247.4 et Tests247.4 sont reconstruits avec le fichier actuel. Les89 tests
temporels et l'import réel de shaders exécuté explicitement passent à nouveau,
sans warning ni dérive. Example247.7 possède337 inputs, dont les236 inputs moteur
identiques à Library247.4. SHA du binaire effectivement lancé :
`3d591b9afa6f46967682ac5b179a8ededc006034ba9bace740436e08cef95a21`.

Le [harnais CPU268](../../../../.tidewater-integration/render-owned-probe-tests-268/example.0.json)
compile le véritable exemple avec Library247.4 et son test runner. Les deux tests
de resize/Image passent, puis le test de matrice rejette effectivement les valeurs
non finies et une pose différente. Seuls ces3 tests sont exécutés ; les autres
tests du probe restent filtrés. Le runner n'exécute pas `main`, ni de scène GPU.

Le [runtime270](../../../../.tidewater-integration/temporal-owned-native-runtime-270/run.json)
termine en **16,8444468 s**, exit0, processus fermé et fixture inchangée.
Le [reçu final](../../../../.tidewater-integration/temporal-owned-native-runtime-270/probe.owned.json)
porte `passed-render-encoding-only`. Main tourne sur ThreadId(1), le vrai driver
Render sur ThreadId(18), avec PipelinedRenderingPlugin installé. La végétation
auteur reste à65 383 plantes, sans exclusion, densité retirée ou reprojection.

| Cas observé dans Render | Résultat de commande acquis |
|---|---|
| Statique |34 échantillons retenus pour progression ; masque32 phases complet. Le premier commit seedé porte phase0/epoch0/flags3, générations1/1. |
| Déplacement |3 échantillons de progression ; la caméra réelle passe de x42 à x42,75 et la caméra précédente suit le dernier resolve encodé. |
| Reset explicite |3 échantillons ; seed phase0/epoch1/flags3, générations2/2, clear/draw/copies observés. |
| Resize1919×1079 |3 échantillons ; seed phase5 après phase4, epoch2/flags1, générations3/3. La copie eau reste de taille entière ; l'historique caméra/modèle n'est pas réinitialisé. |
| Caméra inactive |3 frames Render réellement sans packet. Reçus72/73 : committed47, prepared71 et last_serial71 stables, pending absent. Au second reçu le cut efface last_frame/last_commit et recovery_resets passe à1. |
| Reprise |3 échantillons ; premier commit48, phase0/epoch3/flags3, générations4/4, seed et copies réels observés. |

Le reçu contient **75 échantillons, dont51 valides et24 invalides**, sans omission.
Les24 invalides appartiennent au démarrage statique avant le premier commit :
passes auxiliaire/HDR/resolve encore incomplètes, avec un join de réfraction absent
sur l'un d'eux. Ils sont conservés et ne qualifient aucune frame. Tous les
échantillons ultérieurs enregistrés sont valides ; aucun dérivé shader en erreur
n'est signalé. La réfraction rejoint la pose, projection et le jitter Render à
l'échelle de sa taille, avec un compteur de profondeur nouvellement encodée.
Ce compteur global n'est toujours pas un tag GPU de la capture.

Le PNG270 est inspecté comme **contexte d'une frame ultérieure** en1919×1079,
après les cas ; il ne constitue pas une comparaison source appariée. Les deux
warnings Bevy de readback en canal fermé apparaissent à la sortie, après
l'écriture du reçu final ; leurs journaux seuls n'identifient pas une requête
précise. Ce parcours ne demande aucun Source7/Trace2 ; son verdict
ne prouve ni complétion GPU, ni pixels/objets mobiles, ni fidélité numérique ou
visuelle source. Les FAIL262,231 et249 restent inchangés.

La [revue du raccord Studio267](../../../../.tidewater-integration/studio-render-owned-seam-review-267/NOTES.md)
identifie maintenant les sites réels : choix exclusif Off/BevyTaa/SourceResolve,
matériaux Studio, caméra joueur et désélection à la sortie du playtest. Le mode
Source n'est pas encore appliqué dans Studio. Les prépasses BevyTAA, le jitter de
sa capture, la couverture GLB et les contrats underwater/hulls exigent leurs
propres contrôles. Source7 sous propriété Render, l'oracle strict, l'objet mobile,
WASM et la livraison restent ouverts ; l'objectif complet reste actif.

L'[audit indépendant270](../../../../.tidewater-integration/temporal-owned-native-runtime-270/audit267/REVIEW270.md)
passe40 contrôles des reçus, journaux, phases, transitions, intégrité et provenance.
Les236 pins moteur sont identiques entre Library247.4, Tests247.4, Example247.7 et
CPU268 ; les337 pins complets des exemples sont également identiques. L'égalité
intégrale du packet lié et des matrices Main/capture dépend aussi des assertions
du probe source épinglé : le JSON ne sérialise pas chaque objet interne. L'audit
ne transforme pas ces assertions en lecture GPU. SHA de son JSON :
`21cbd4ed46512bdfa677075eabd48e4a321e8fc861e0b4745e08560dffd0713e`.

Le [gel271](../../../../.tidewater-integration/temporal-render-owned-qualified-inputs-271/manifest.json)
conserve337 copies source et155 artefacts, incluant les vrais binaires, les tests,
les reçus/audits et les échecs précédents. Les492 copies sont relues et rehashées ;
les337 sources courantes restent identiques aux pins après la copie. Le manifeste
porte uniquement `frozen-native-command-encoding-only`, SHA256 :
`55f1abdf3f281d5da689a8384e3e30a7bdfacd4dcfb369efd29af1d8c3c6d66c`.
La sonde et SourceResolve demeurent optionnels, sans activation implicite dans le
launcher ou Studio. Ce gel ne requalifie pas les preuves couleur antérieures.
