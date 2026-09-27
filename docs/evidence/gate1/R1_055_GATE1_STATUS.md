# R1-055 — Rapport intermédiaire de Gate 1

## État courant — Gate 1 clôturée le 12 septembre 2026

**PASS automatisé local sous ADR-0006, avec les dérogations acceptées.**
Dérogations saccades pendant les changements de qualité et kit : ADR-0013 ;
comparaison Godot/Cadle validée explicitement. 738 tests PASS, 2 ignorés
préexistants ; builds et replays natif/Studio PASS. Révision propre qualifiée
`7386e00a1d9e8531470b57bcd8da190636a9e390`. [Rapport de clôture](GATE1_CLOSURE_20260912.md).
CI distante NOT RUN ; aucune publication ni qualification de distribution.
Le travail Gate 2 est suivi séparément et ne fait pas partie de ce snapshot.
Les états ouverts et attentes antérieurs ci-dessous sont historiques ;
ils ne doivent pas rouvrir les décisions soldées par ce rapport.


### Décisions de clôture — 12 septembre 2026

ADR-0013 consigne l'instruction explicite du propriétaire : saccades pendant les
changements de qualité dérogées ; point kit humain reporté sous dérogation ;
comparaison Godot/Cadle validée. Ces points ne sont plus en attente d'accord.
La qualification finale sur révision propre est en cours ; aucun PASS de test,
droit de distribution ou travail de Gate 2 déduit de ces décisions.
Voir `docs/ADR/ADR-0013-gate1-closure-owner-decisions.md` (depuis la racine).
Les paragraphes antérieurs ci-dessous restent l'historique des acquisitions.

## État de clôture vérifié — 11 septembre 2026

Complément postérieur au tableau : le correctif de réutilisation des berges a
son JSON mobile et son index `r1-final-dossier-bank-reuse-20260911/index.json`
(377 fichiers, neuf tests collecteur PASS). Les pics 143,5/264,5 ms ne démontrent
pas de gain sensible. La paire native/Studio du tableau est historique depuis
ce changement moteur ; elle n'est pas une qualification du nouveau natif.
PNG du nouveau Studio et sortie normale absents, limites explicitement conservées.
Retouche humaine toujours absente au contrôle ; détails R1_040_BANK_REUSE et
R1_FINAL_DOSSIER_POINT7. Ce complément ne ferme aucun critère humain ou temporel.

**Poursuivre Gate 1 ; pas de passage Gate 2.** Les détails de roadmap R1-040,
043, 055, 063 et 065 ont été actualisés sur leurs rapports existants : leurs
anciens paragraphes ne sont plus des tâches à recommencer. Aucun seuil ni
critère humain modifié. Branche Rust, 395 entrées Git modifiées/non suivies au
contrôle ; cet inventaire n'autorise ni nettoyage ni publication.

| Bloc convenu | Acquis / décision conservée | Ce qui reste pour la clôture |
| --- | --- | --- |
| 1. Profils | Quatre niveaux, eau/forêt et adaptation raccordés ; cycle Studio préchauffé High/Low/High mobile acquis le 11 septembre | Qualification de fluidité des transitions, pas nouvelle intégration des niveaux |
| 2. Performances | WAIVED / ACCEPTED sur RX 7800 XT | Aucune nouvelle campagne GPU cible comme préalable à ce point ; ne pas convertir la dérogation en PASS mesuré |
| 3. Mouvement | Matrice fonctionnelle, traversée et cycles acquis ; défaut de dérive corrigé | Réserves temporelles et tramage ; les vidéos à horloge imposée ne mesurent pas les hitches réels |
| 4. Natif/Studio | Paire épinglée du 11 septembre : cycle natif cinq étapes, plans High/Low puis Medium/Ultra comparés au Studio avec reçus GPU ; deux fichiers documentation/test ont changé depuis les builds | Révision finale et réserves optiques/temporelles ; comparaison fonctionnelle des quatre niveaux acquise, voir R1_043_CURRENT_NATIVE_STUDIO.md |
| 5. Mesures/préchauffage | Capacités natives et préchauffage acquis ; mesures GPU Studio dérogées | Aucun nouveau chantier de compteurs Web ; conserver les limites et identités des preuves natives |
| 6. Kit | Audit structurel, six GLB et six copies Blender réouvertes | Revue pivots/collisions/LOD et droits ; véritable retouche humaine, puis export/réimport gouverné et contrôle de conservation |
| 7. Dossier | Index à 378 fichiers, douze captures/deux vidéos historiques plus candidat Studio actuel séparé | Comparaison finale aux conditions équivalentes, revues requises et verdict sur révision propre exacte |

Sources : `R1_EFFECTIVE_PROFILES_INTEGRATION.md`, `R1_040_PREPARATION_BUDGET.md`,
`R1_PERFORMANCE_POINT2.md`, `R1_MOTION_POINT3.md`, `R1_NATIVE_STUDIO_POINT4.md`,
`R1_MEASURES_POINT5.md`, `R1_KIT_POINT6.md`, `R1_FINAL_DOSSIER_POINT7.md`.
Le nouvel index reste `partial_not_gate_pass`. Les derniers 710 tests Rust et
huit tests du collecteur sont les acquisitions des lots précédents, pas des
tests relancés pour cette actualisation documentaire.

### Ne pas demander de nouveau les décisions déjà données

- La vue intégrée et son ciel ont été acceptés en l'état ; ne pas rouvrir une
  finition artistique comme préalable implicite. Cette acceptation ne s'étend
  pas automatiquement aux autres vues, au mouvement ou à une comparaison.
- Point 2 : dérogation de performance documentée, pas matériel cible à réclamer.
- Mesures GPU Studio : ADR-0012, pas nouveau fork Bevy à entreprendre.
- CI distante : NOT RUN sous ADR-0006, pas CI verte supposée.

### Dépendances humaines et suite technique

`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-kit-human-ready-20260910/vv-rock-moss-a-human.blend`
est toujours absent au contrôle de cette reprise. La préparation existe ; ni
retouche humaine ni intention authorée ne sont donc attestées par ce fichier.
Le parcours à effectuer reste celui de `R1_CLOSURE_PROGRESS_20260910.md`.
Les étapes d'export/réimport après cette retouche dépendent de sa réception.

Les travaux techniques indépendants restent dans Gate 1 : traiter les réserves
temporelles selon leurs preuves, compléter la portée comparative/provenance
finale, et assembler la qualification des candidats exacts. Ne pas remplacer ces
travaux par la répétition des smokes déjà réussis. Une révision propre et les
revues requises ne peuvent pas être fabriquées par effacement de changements,
commit implicite ou acceptation supposée. Aucun blocage global n'est déclaré
tant que ces travaux indépendants restent réalisables.

## Actualisation préchauffage — 10 septembre 2026

R1-039 soldé pour les profils importants du candidat natif/Studio : préparation
explicite Low/Medium/High/Ultra puis retour High réellement exécutée dans les deux
environnements, clair/pluie. Point 5 terminé avec les dérogations de mesure déjà
accordées, aucune dérogation nouvelle. Preuves : `R1_PROFILE_PREWARM.md`.
696 tests PASS / un timing ignoré, Clippy et builds natif/Web PASS, sources
préservées, processus de preuve fermés. R1-040 et les réserves temporelles,
revues humaines du kit et verdict comparatif final restent ouverts.

## Actualisation mesures — 10 septembre 2026

R1-034, R1-037 et R1-038 soldés localement : overlay CPU/GPU et estimation mémoire
du candidat natif courant exécutés/exportés, capture GPU intégrée revérifiée,
parties GPU Studio dérogées explicitement sous ADR-0012. Aucun compteur global
de draws livré ni performance qualifiée par cette acquisition. Trois tests du
vérificateur PASS, dont dix cas invalides rejetés ; processus de preuve terminé.
Voir `R1_MEASURES_POINT5.md`. R1-039, réserves temporelles, revues humaines et
dossier final sur révision exacte restent dus ; **Gate 1 reste ouverte**.

## Actualisation profils — 9 septembre 2026

Bloc fonctionnel des profils terminé : raccord forêt/eau, cycles natif/Studio
des quatre niveaux avec acquittement GPU et retour High, montée automatique
native, descente Studio et priorité manuelle vérifiées. Sources préservées,
processus et enfants WebView de test fermés, sans publication. Détails, hashes
et limites : `R1_EFFECTIVE_PROFILES_INTEGRATION.md`.
R1-040 garde la réserve de fluidité des transitions ; les performances cible,
les six autres blocs et les validations humaines restantes demeurent dus.
Gate 1 reste ouverte, acceptation artistique antérieure inchangée.

## État historique ci-dessous

État inspecté le 7 septembre 2026, branche **Rust**, HEAD de base
`f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié conservé.
Ce rapport rassemble les preuves existantes ; il ne remplace ni la roadmap ni le contexte canonique.

## Verdict

### Acceptation visuelle du propriétaire — 9 septembre

Réponse explicite à la revue de la vue intégrée : « ca va meme si le ciel
n'est pas exceptionnelle je valide en l'état ».
Le rendu présenté est donc **accepté en l'état**, avec réserve qualitative sur
le ciel. L'attente de cette réponse humaine est levée ; ne pas redemander la
même acceptation ou reprendre une finition du ciel comme préalable implicite.

Référence acceptée :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-integrated-menu-studio-20260909/forge-viewport-1788973757724.png`,
SHA-256 `BBB710A94B824028E7CD5BAFF80920E66F70E1465489AD0398B4DFEE6B3B8B81`.
Cette décision actualise les mentions historiques d'acceptation artistique
absente pour ce rendu. Elle ne prouve pas une comparaison Godot/Cadle, les
autres vues ou le mouvement, une retouche/réimport humain, les profils complets
ou les performances. Elle n'accorde ni dérogation technique nouvelle ni publication.
**Gate 1 reste ouverte pour les critères restants ; Gate 2 non ouverte.**

### Installation forward et prérequis GPU — 9 septembre

Raccord suivant implémenté, sans promotion des capacités : l'observation d'eau
forward porte l'identité séquence/hash/génération du plan installé. L'application
compare cette identité, le profil source, le niveau, les paramètres effectifs et
les budgets réellement projetés ; les uniformes du document seuls ne valent pas
installation. Un réglage manuel divergent ou une ancienne séquence ne peut pas
acquitter le plan courant.

Limite identifiée : le fallback ADR-0011 retire le plafond métrique de réflexion.
Dans le chemin forward, un plafond absent désactive le tracé. Ce cas reste refusé
par l'acquittement, même si ses uniformes correspondent à cette désactivation :
ne pas annoncer des reflets écran appliqués. Aucun fallback ni critère assoupli.

L'inventaire des prérequis GPU distingue maintenant l'eau transmissive du SSR
classique. Il exige pour la première la profondeur de la vue et sa texture de
transmission mono-échantillon aux dimensions de la cible complète (pas du viewport).
Le chemin classique conserve ses exigences de pipeline, uniformes, prépass et bruit.
Les matériaux/pipelines et la fin de soumission restent contrôlés séparément.
La présence de ressources publiques ne prouve pas le contenu des pixels ni la
bonne exécution optique ; les capacités de l'aperçu restent non qualifiées.

Contrôles finaux après ajout du refus du fallback incompatible : **680 tests
workspace PASS / un timing ignoré**, Clippy workspace/all-targets/all-features
strict PASS, check frontend WASM PASS, sorties 0. Format/diff-check PASS.
Le test d'installation utilise des capacités synthétiques : Low correspond,
ancienne séquence/installation absente/paramètres divergents sont refusés ;
High avec plafond absent reste refusé. Le test GPU porte sur les dimensions
requises, pas sur une allocation matérielle ou des pixels. Le premier passage
moteur précédait ce dernier garde-fou ; seule la suite finale ci-dessous le couvre.
Aucun nouveau binaire graphique lancé, aucune readiness GPU réelle ou Gate 1
acquise. Avertissement futur `proc-macro-error2 2.0.1` conservé ; sessions terminées.

Journaux dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forward-application-prerequisites-20260909` :

| Journal | SHA-256 |
| --- | --- |
| tests.log | 1BC93B7791A7665827CF5741B22FEA418D47EC70AADCFA0A3E342109A5AB94A2 |
| clippy.log | AACEFCA90C1C63965DDE0178A4FBC115FCFB90F9557ADF3902FB04D020D93867 |
| wasm.log | 4B414EE31CCF745D2B702D8070F316B1850F07038F72B03365ECF5DD3F21DB87 |

### Raccord CPU des profils d'eau forward — 9 septembre

Le coordinateur global ne consultait que `WaterProjectionStats.quality`, absent
par construction dans le candidat forward. Ses quatre budgets pouvaient être
correctement appliqués tout en laissant le domaine eau `Pending`.
L'observation forward porte maintenant le hash de politique et le niveau demandé.
Le coordinateur reconnaît une projection **partielle CPU** lorsque ces identités
correspondent au profil global et que les uniformes observés correspondent.
Une identité absente/périmée, un autre niveau ou une caméra inactive ne suffisent pas.

Ce raccord n'ajoute aucune capacité `Supported`, ne qualifie pas les réflexions
ou la réfraction et ne rend pas le plan installable à lui seul. Les garde-fous
de `effective.rs`, l'acquittement GPU et `full_preset_applied=false` sont conservés.
Aucun shader, budget, asset, source de carte ou rendu modifié.

Test ciblé du cycle Low/Medium/Ultra/High enrichi : PASS, avec refus des identités
absentes/périmées et des niveaux différents, puis de la caméra inactive. Sources
et handle matériau conservés. Contrôles terminés, sorties 0 :
`cargo test --workspace --locked -j 2` : **679 PASS / un timing ignoré** ;
Clippy workspace/all-targets/all-features strict PASS ; check frontend Web
`--lib --no-default-features --features web --target wasm32-unknown-unknown` PASS.
Format et diff-check PASS. Avertissement de compatibilité future préexistant
`proc-macro-error2 2.0.1` conservé. Aucun nouveau bundle Studio, lancement
graphique ou passage de gate revendiqué. Sessions de contrôle terminées.

Journaux dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forward-profile-identity-20260909` :

| Journal | SHA-256 |
| --- | --- |
| tests.log | 2448F198D035B18EB28548B428252769F68EF78E50804CA3BA3C855E5321554E |
| clippy.log | 76B94DA43EF13971ED8D4AD9AD975F8BEEA69F7F4813B75A1471C9FA038E6AB6 |
| wasm.log | 8D3E29CD9B402130D961008A127794D48B9C94F30CEF491389AA002925E904F9 |

### Audit de clôture borné — 9 septembre, après abandon de l'essai bois

**Gate 1 ouverte : le blocage n'est pas uniquement une attente de validation humaine.**
Inspection documentaire et du code courant, sans nouveau build ni essai graphique.
Branche `Rust`, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié
conservé ; divergence locale `HEAD...origin/Rust` = `0 / 103`, sans fetch ni publication.
Cette référence distante locale ne prouve pas l'état actuel du serveur.

Correspondance avec les dix conditions finales de la roadmap :

| Condition | État probant et manque de clôture |
| --- | --- |
| Supériorité visuelle sur Godot | Non acquise : revue humaine finale absente, pas déduite des tests. |
| Impact comparable à Cadle | Non acquis : comparaison finale équivalente et appréciation artistique dues, sans copie d'assets. |
| 60 FPS moyens 1080p High GPU milieu de gamme | Non qualifié : mesure disponible sur GPU de développement, pas RTX 3060/RX 6600. |
| p95 ≤ 16,7 ms | Contredit par le parcours release conservé : 21,3064 ms. |
| 1 % low ≥ 45 FPS | Contredit par ce parcours : 43,0097 FPS. |
| Absence de stutter récurrent | Non démontrée par une seule distribution de 29,98 s ; parcours final dû. |
| Profils qualité fonctionnels | Partiels : commandes présentes, mais `global_quality/effective.rs` conserve explicitement `Unqualified` pour la forêt prototype ; ne pas remplacer ce statut pour obtenir PASS. |
| Parité native / Studio | Preuves ponctuelles présentes ; qualification du même candidat complet et de ses profils encore due. |
| Sans dépendance propriétaire obligatoire | Qualification finale de chaîne d'approvisionnement due ; les déclarations de provenance ne suffisent pas (voir R1-053). |
| Rapport et captures versionnés | Dossier intermédiaire présent, pas de dossier final qualifié sur commit/arbre exact selon ADR-0006. |

Relecture directe du JSON existant
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-current-release-20260909/walk/report.json` :
2173 échantillons, 72,4741 FPS moyens, SHA-256
`83DB1553636418DA7DCC492899EBDE728D2A94D37507FB950C0B5DDE54EEBB6D`.
Il s'agit du parcours sans nuages décrit dans [la preuve release](R1_RELEASE_PREPARATION.md),
pas d'un nouveau benchmark ni d'une qualification du candidat complet.

Conséquence pour la suite : ne pas poursuivre les micro-optimisations du bois ou
le regroupement spéculatif des matériaux. Le prochain travail produit reste la
finalisation des capacités/profils du candidat intégré, en séparant readiness
technique et acceptation artistique. Les mesures finales suivent sa stabilisation,
pas chaque ajustement. La performance reste un écart à résoudre, non une dette
implicitement acceptée. Les conditions humaines, GPU cible et révision de preuve
restent explicites ; aucune dérogation nouvelle n'est accordée par cet audit.
Gate 2 non ouverte, CI distante `NOT RUN` sous ADR-0006.

### Réapplication du profil global — 9 septembre

Défaut reproduit par test avant correction : après envoi global Low, une demande
individuelle de post-traitement High conservait le bloom même après une nouvelle
commande globale Low. Le cache `sent == tier` ignorait la commande explicite.
Le test `manual_same_tier_reapplies_after_individual_override_without_continuous_fanout`
a échoué sur cette assertion (commande en échec), puis réussi après correction.

Le coordinateur réémet maintenant les sept demandes une seule fois, y compris
pour le même niveau. L'application précédente est invalidée sans remettre le
compteur de séquence à zéro : pas de réemploi d'un ancien acquittement GPU à hash
de plan égal. Les réglages individuels ultérieurs restent conservés et les
commutateurs optiques ne sont pas effacés. Aucun shader, asset ou document de
carte modifié, aucune capacité promue en `Supported`.

Vérifié : 39 tests `global_quality::` PASS, dont deux nouvelles régressions,
puis `cargo test --workspace --locked -j 2` : **678 PASS / un timing ignoré**.
`cargo clippy --workspace --all-targets --all-features --locked -j 2 -- -D warnings`,
`cargo fmt --all --check` et `git diff --check` : **PASS**.
Le check WASM du frontend (`cargo check -p forge_studio --lib --no-default-features
--features web --target wasm32-unknown-unknown --locked -j 2`) passe ; avertissement
de compatibilité future dans la dépendance `proc-macro-error2 2.0.1` conservé.
La première invocation WASM, sans sélection Web, a échoué en ciblant à tort le
shell desktop Tauri ; son journal `wasm.log` est conservé, aucun code adapté pour
masquer cette erreur de commande.

Journaux sur E:, `r1-profile-reapply-20260909` :

| Journal | SHA-256 |
|---|---|
| tests.log | 8892BE4C322ED7D02C696CA19506D502082CD077F8CAE71DCEC4ACC661748BC6 |
| clippy.log | A151088A12C9DDCA7122D608216AAFF151EDBCD48DDEBAAB81DB8F6A2905C4ED |
| wasm-web.log | AD887761C092D9A6D54C567CA7FFBF378C21951890F911E55A354994CED69AAE |

Intégration ensuite construite et lancée avec Computer Use dans Tauri :
`r1-profile-reapply-studio-20260909`, frontend `r1-profile-reapply-frontend-20260909`,
kit existant `r1-atlas-cropped-runtime-kit-20260909`, aperçu eau forward.
`trunk build` puis `Build-StudioCandidate.ps1` réussis. Aucun changement de carte
ou d'asset et aucune relance de la suite workspace inchangée.

Cycle produit High→Low→High, puis clic explicite High : monde initialisé une fois,
kit 20/20 et 36 colliders prêts, 1662 arbres. Exports Low/High relus : six hashes
source identiques, caméra active unique, 99 pipelines prêts, zéro pending/failed.
Herbe : 154048 brins en Low, 620640 en High. Budgets forward concordants :
réflexion 0/0→48 pas/96 m, réfraction 0/0→24 pas/3,2 m ; réflectance conservée.
Le PNG High 1408×966 est inspecté : personnage, terrain et lisière visibles.
Cette vue éloignée n'est pas une qualification rapprochée de l'eau ni du vent.

| Artefact dans le dossier candidat | SHA-256 |
|---|---|
| forge-studio-candidate.exe | AD15B9BA5583DA026B13F085256DA829F8F698B377D01A8062EEB00A92C8B37B |
| web/forge_studio_bg.wasm | 7E0D9A2B6A68F2D9FA58DE1911E0EBC7F456B5A1408BA69D8EC29F1C3068D57F |
| forge-diagnostic-1788969833825.json | CC8E2F69940F0A63C6D1D8A00CDCE182A7A4682E2343D86156046389D88DE6CB |
| forge-diagnostic-1788969937069.json | BC8874DC633C40FCEDE52F0E5E8EE74CF87327936EEC7D8E0A1EC614056663A3 |
| forge-viewport-1788969949177.png | DF93BC403C1294BA93D9FBC87989AA200C9DF0A47F7AA270F786D5AEEADD8A08 |

Copies sur E: vérifiées contre les téléchargements originaux conservés. Instance
PID 29560 fermée normalement, absence du processus et des WebViews associés vérifiée.
Le cas précis « réglage individuel puis même niveau global » reste prouvé par
le test de coordinateur, pas par ce cycle UI global. Les deux plans exportés
restent `unresolved_capabilities`, domaine eau `pending`, aucune readiness GPU
globale revendiquée. Ni profils complets, performance, art, ni Gate 1 qualifiés.

L'essai de vent lointain a été arrêté sans nouveau verdict optique. Son seul
processus identifié (PID 5000, candidat `r1-far-wind-forest-studio-20260909`) a
reçu une fermeture normale ; absence vérifiée ensuite. Fichiers conservés.
La preuve non concluante antérieure reste telle quelle, pas un succès implicite.

### Intégration logicielle — 9 septembre, après les commandes de réfraction

Contrôles transversaux terminés sur le worktree Rust conservé, HEAD de base
`f786a38aca2023f08d708687f99a47d14f34e6d4` :

- `cargo test --workspace --locked -j 2` : **676 PASS, un ignoré**, sortie 0.
  L'ignoré reste la sonde explicite de préparation CPU, pas un test en échec.
- `cargo clippy --workspace --all-targets --all-features --locked -j 2 -- -D warnings` :
  **PASS**, sortie 0.
- `cargo fmt --all --check` et `git diff --check` : **PASS**.

La suite couvre notamment les contrats, générations et retouches conservées,
projections eau/forêt, contrôles, garde-fous de capture et entrées Studio/runtime.
Elle ne remplace pas le rendu réel, les cibles matérielles ou la revue humaine.
Pas de rebuild release, nouvelle série de captures ni benchmark dans ce lot.

Correction bornée : les deux helpers du recorder dans `capture_readiness.rs`
sont compilés sous la même feature `qualification` que leur unique consommateur
(le helper de test exige aussi `test`). Pas de suppression de lint ou de test,
pas de changement du comportement du recorder. Cela retire l'avertissement du
build moteur sans qualification observé au lot précédent.

Journaux sur E:, dossier `r1-integration-check-20260909` :

| Journal | SHA-256 |
|---|---|
| tests.log | 5D5C915B34BCCCF2E74372B8E8299EE80FB40471FFE0F37E13937F0D30929D5B |
| clippy.log | EE2D72F4491F7F77776D56F45DA958AC957E261BA69F7AC087776DAB80FCFD02 |

Les sessions de commande sont terminées ; aucun processus Cargo/rustc restant
au contrôle final. Aucun essai graphique ouvert. **Gate 1 reste ouverte** et
la CI distante reste `NOT RUN` selon ADR-0006 ; ce worktree sale n'est pas le
clone propre requis pour le verdict final. Aucun commit, push ou asset Cadle.

Diagnostic de suite : `global_quality/effective.rs` refuse toujours explicitement
la qualification des prototypes de forêt et n'acquitte pas le chemin d'eau forward
à partir de ses seuls uniformes. Ne pas convertir ces garde-fous en PASS.
Le vent lointain possède déjà un raccord ; sa preuve reste celle non concluante
de `R1_FAR_WIND_ISOLATION.md`, pas une absence d'implémentation à réécrire.

### État de reprise — 9 septembre, après le correctif d'ombre B

**Poursuivre Gate 1 ; réorienter la suite vers le candidat complet, sans
réouvrir les diagnostics résolus. Gate 1 non validée, Gate 2 non démarrée.**
Cette section actualise les suivis historiques ci-dessous, sans changer les
critères ni convertir une preuve technique en acceptation humaine.

| Sujet | Acquis dans les preuves existantes | Reste réellement dû |
| --- | --- | --- |
| Forêt | 1 662 placements déterministes, échelle corrigée, atlas recadré intégré | Qualité artistique et transitions globales, vent lointain |
| Ombres | Saut précis 161/162 absent du replay natif B ; chargement Studio réussi | Mouvement Studio et autres profils ; pas de nouvelle recherche du même caster |
| Eau | Raccord rapproché sans bande séparée et reflets d'arbres observés dans le Studio | Finition, stabilité en mouvement et capacités effectives par profil |
| Performances | Coût du fondu mesuré : environ -2,3 % de FPS forêt en ABBA dev | Seuil final et GPU cible ; les ~50,4 FPS forêt dev ne prouvent pas 60 FPS |
| Livraison | Candidats natif et Studio conservés avec empreintes | Dossier final cohérent sur révision identifiée, validations humaines et conditions ADR-0006 |

Sources : [forêt et candidat courant](R1_FOLIAGE_CLUSTER_ATLAS.md),
[raccord de berge](R1_WATER_SHORE_OVERLAP.md). Les preuves de coût et de rendu
précèdent la centralisation de la règle LOD ; celle-ci a 22 tests distincts
réussis, mais n'a pas encore été reconstruite dans ces exécutables. Ne pas
présenter les binaires archivés comme contenant ce dernier changement.

Ordre de sortie restant :

1. Terminer le candidat intégré eau/forêt/environnement et ses profils effectifs.
   Partir des défauts encore observés, pas des anciens essais refusés désormais
   remplacés. Ne pas acquitter artificiellement les capacités manquantes pour
   faire fonctionner l'adaptation. Le ciel reste provisoire, sa finition différée
   n'est pas une acceptation.
2. Une fois ce candidat stabilisé, qualifier ensemble les parcours natif et
   Studio, les profils et le dossier visuel final. Réutiliser les preuves dont
   les entrées restent valides ; ne pas refaire douze vues après chaque correctif.
3. Conserver explicitement les conditions externes/humaines : RTX 3060/RX 6600,
   retouche/réimport humain, comparaison artistique/Cadle, provenance finale et
   décision de passage. L'arbre propre requis ne permet pas de supprimer ou
   committer les changements présents. CI distante `NOT RUN` sous dérogation,
   aucune publication autorisée.

Cette actualisation est documentaire : aucun nouveau test graphique, build,
gain de performance ou critère de gate clos n'en découle.

### Historique du verdict — 8 septembre

**8 septembre, retour propriétaire : forêt refusée**, pas simplement en attente
de revue. Arbres trop petits/trop espacés, aspect de plaine ; rapport demandé
arbres adultes/personnage 5–10. Personnage canonique 1,80 m : cible 9–18 m.
La suite prioritaire est la correction réutilisable de densité forestière et
d'échelle, puis une vue à hauteur humaine avec référence de taille comparable.
Ne pas demander de nouveau l'acceptation des mêmes images ni refaire le dossier
de qualification du candidat refusé. Correction ensuite implémentée : masque
Forest moins Buildable raccordé à une densité sérialisée optionnelle, arbres
9,04–17,80 m et 1 662 placements déterministes dans la vallée. 619 tests PASS /
un timing ignoré, Clippy strict PASS, captures native et Studio inspectées.
Tauri construit et lancé, 1 662 arbres et kit 20/20, PNG/JSON contrôlés ; instance
fermée. Mesure native dev High 39,77 FPS, pas de qualification performance
ou artistique. Voir `R1_FOREST_REGION_SCALE.md`. Les revues historiques ci-dessous
ne valent aucune acceptation du nouveau candidat ; Gate 1 reste ouverte.

R1-025 clos le 8 septembre comme capacité de mouillure réversible : cycle natif
déjà réussi le 7 septembre, journal et trois PNG recontrôlés, puis cycle Tauri
du 8 septembre et tests partagés. Aucune nouvelle exécution native nécessaire
pour établir cette propriété ; candidats distincts, pas parité optique finale.
Finition artistique et validations globales restent ouvertes. Voir
`R1_024_026_WEATHER_NATIVE.md`. Le nouvel essai clavier Tauri reste non concluant,
sans défaut moteur établi ; sa preuve et le nettoyage sont consignés dans
`R1_STUDIO_EXPLORATION.md`. Pas de nouveau build ou replay de suite de tests.

R1-035/036 clos le 8 septembre **comme capacités de mesure** : histogramme et
statistiques réellement affichés dans Tauri, export cohérent, tests partagés PASS.
Les seuils de performance restent non acquis. Cycle pluie/sec Tauri ensuite
exercé dans une session : humidité 0 → 0,78 → 0 exacte, apparence restaurée,
sources constantes, instance fermée. Soleil constant/ciel clair sous pluie encore
non cohérents artistiquement. Voir `R1_TAURI_WEATHER_AND_METRICS.md` ; ne pas
rejouer ces contrôles inchangés ni transformer ce résultat en Gate 1 passée.

Candidat optimisé du 8 septembre ensuite assemblé et testé dans Tauri : sélection
locale, cycle High → Low → High, kit conservé et une seule initialisation.
WASM 41,1 Mo, aucun gain FPS qualifié ; série courte encore au-dessus de 16,7 ms.
Preuve `R1_STUDIO_OPTIMIZED_CANDIDATE.md`. Continuité d'éclairage ensuite corrigée,
616 tests PASS / un timing ignoré et nouveau cycle Tauri capturé : aucune coupure
observée dans les images de transition, premier chargement encore sombre.
Capture/libération du pointeur acquise, bascule clavier non démontrée ; instance
fermée. Forêt non qualifiée et conditions humaines/cible inchangées.

Complément Tauri du 8 septembre : lancement et capture directs réalisés par
l'agent, exports PNG/JSON reçus, cible diagnostique `tauri_studio`, kit/forêt
chargés. La demande précédente de lancement humain pour ce smoke est levée :
Computer Use fonctionne. Instance fermée et terminaison vérifiée. Voir
`R1_STUDIO_REPRODUCIBLE_BUILD.md`. Cela ne qualifie ni les performances ni
l'ensemble des effets/parcours, et ne remplace aucune acceptation humaine.

### Suivi de clôture actualisé — 8 septembre, après acquisition RenderDoc

La mention antérieure « captures NOT RUN » ne décrit plus R1-037 : une capture
Vulkan du candidat courant existe (frame 305, SHA-256
`B5BAA8D980362DC85D9F5507A9CE8657C6ED98EBB59259ED9904961AA35AB62D`).
Après crash concurrent et deux arrêts isolés par garde-fou mémoire, le replay
isolé a réussi avec davantage de mémoire disponible : 1 641 draws, 3 065 instances,
5 755 071 triangles soumis toutes passes. Le processus est terminé, aucun test
encore en cours. Détails : [preuve R1-037](R1_037_RECOVERED_TOOLING.md).
Seconde acquisition/relecture indépendante également PASS, frame 304, entrées
épinglées identiques : mêmes draws/instances ; seul l'UI ajoute deux triangles.
Les dix-sept autres passes ont des comptages identiques. Répétition native acquise,
sans preuve d'identité des pixels ; processus arrêtés. Ne pas refaire ces essais
à entrées inchangées.

Les prochains résultats requis restent distincts :

- **Technique R1-037** : qualification de la provenance et de l'état capturé,
  couverture des backends ; répétition native acquise ci-dessus. Le reçu
  atomique est une solution envisagée, pas une exigence supplémentaire d'ADR-0004.
  La capture ponctuelle ne
  livre pas un compteur d'overlay et ne qualifie pas le navigateur.
- **Technique/visuel** : candidat complet forêt/eau/profils, dont les chemins
  Tauri/WebView2, puis dossier final cohérent ; aucun PASS déduit des smokes isolés.
  La finition du ciel reste différée à la demande du propriétaire, pas acceptée.
- **Externe** : matériel cible RTX 3060/RX 6600 non qualifié. Les performances de
  la RX 7800 XT ne remplacent pas cette exigence.
- **Humain** : retouche/réimport Blender, revue artistique et décision de passage
  explicites restent dues. Les validations synthétiques ne remplacent pas ces gestes.
- **Révision de qualification** : arbre exact/propre selon ADR-0006, sans nettoyer
  ou publier le worktree courant. Aucun nouveau commit ou push autorisé par ce suivi.

Ce suivi ne clôt pas Gate 1 et n'ouvre pas Gate 2. Les listes historiques suivantes
conservent leurs dates et binaires ; elles ne doivent pas déclencher un nouveau
cycle de vérification déjà réussi à entrées inchangées.

### Priorité et contrôle du lancement — 8 septembre

Le retour propriétaire reporte la finition du ciel « le moment venu » : arrêter
les essais nuageux isolés pour reprendre le candidat complet. Aucun essai 128³
n'a été implémenté ; l'aperçu reste provisoire et R1-019 ouvert. L'ordre ci-dessous
décrit les obligations de clôture, pas une obligation de terminer les nuages avant
tout autre travail. Aucun critère artistique ni passage de gate n'est accordé.

Contrôle en lecture seule du déploiement Tauri existant : le binaire déclaré dans
`apps/forge_studio/Cargo.toml` est `forge-studio`, malgré le message de sortie CLI
utilisant un underscore. La copie v2 et le binaire du cache ont tous deux le hash
`10DC6215C8D30276510A8260806BC756A7DF52C1C89FD2281A6A85944BC6C62E`.
L'hypothèse d'une copie erronée fondée sur ce nom est écartée ; aucune correction
supplémentaire ni reconstruction n'est justifiée par cette différence de texte.
L'ancien exécutable reste conservé avec le hash
`153803CAB269F3BB0EF87359CC93690D1469D0D7C3B8D1494FDC862BC26E0021`.

La cause `frontendDist` Windows interprété comme URL, sa correction relative et
la confirmation utilisateur de l'affichage v2 sont déjà consignées dans
[R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md](R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md).
Ce contrôle ne relance pas l'application, ne qualifie pas un nouveau frontend et
ne remplace pas les mesures/effets/parcours WebView2 encore dus. Ne pas présenter
le défaut initial comme toujours non corrigé, ni Chrome comme preuve WebView2.

### Lecture courante et ordre de clôture — 7 septembre

**Gate 1 reste ouverte : les travaux restants ne sont pas uniquement des tests.**
Le ciel est explicitement signalé peu réaliste par le propriétaire. Le volume
expérimental existe, mais ne constitue ni un environnement final accepté ni un
profil de qualité complet. Ne pas transformer ce retour en validation implicite.

Ordre de travail pour éviter les replays sans effet sur le verdict :

1. **Finaliser le candidat visuel réutilisable** : silhouettes/vent de la forêt,
   raccords sol/berges/eau et ciel cohérent avec le soleil et la météo. Pour le ciel,
   les objectifs sont des masses nuageuses lisibles, une profondeur atmosphérique
   crédible et un éclairage cohérent, pas une image de fond propre à cette carte.
   Ces objectifs restent à implémenter/qualifier ; aucun nouveau modèle de ciel
   n'est livré par cette mise à jour. Conserver le mode expérimental explicite.
2. **Qualifier le candidat complet sur les chemins réellement livrés** : profils,
   transitions et parcours natif puis Studio/Tauri. Le cycle Web High/Low/High
   rev.4 est acquis ; il ne prouve ni le mouvement, ni Tauri. La forêt reste
   `Unqualified` : ne pas retirer ce refus pour permettre au harness de passer.
3. **Clore les conditions externes et humaines** : GPU cible RTX 3060/RX 6600,
   retouche/réimport Blender humain et revue du kit/provenance, comparaison artistique
   finale et décision explicite. R1-037 exige encore le traitement prévu par
   ADR-0004 ; la dérogation draw calls de Gate 0 ne s'étend pas à Gate 1.
4. **Acquérir le dossier final une fois le candidat stabilisé** : douze vues,
   vidéos, mesures et replays applicables sur les mêmes entrées identifiées,
   puis verdict selon ADR-0006. Ne pas refaire toute la série après chaque
   observation intermédiaire. Gate 2 ne commence qu'après le passage requis.

Le diagnostic du contrat de fondu est **clos sans défaut établi**, pas la
qualification artistique de la forêt. Les traces écran localisent maintenant
9/25 racines dans l'image pendant les bandes proche/lointaine ; elles ne qualifient
pas les silhouettes ni les images intermédiaires. Voir
[le diagnostic](R1_FOREST_FAR_PATH_DIAGNOSIS.md) et
[la preuve vidéo](R1_048_BRIDGE_MOVEMENT_VIDEO.md).

Intégrité revérifiée lors de cette synthèse : runtime natif
`a40938c3142bb1ce822b4386112c28aa8f7fcfe1f073682fcad95fce4b44a680`,
bundle Studio `r1-bank-road-studio-20260907` WASM
`10726ed3e97520e788fd4f1fbe3ae00f1d584d210d320b68d97c4827d5e9a2e5`.
Ce contrôle de hashes n'est ni un nouveau lancement ni une validation des pixels.

### Historique des validations techniques

Reprise R1-063 complète sous Blender 4.5.10 restauré : deux reconstructions après
une seconde retouche synthétique PASS, six GLB/33 textures concordants et 49 rendus
dans le contrat existant. Test de provenance corrigé sans changer le worker ;
six refus après acceptation du cas valide. Les sources et le candidat runtime
sont préservés. Cette preuve ne remplace pas la retouche/revue humaine du kit.

Rejeu après raccord routes/berges : **612 PASS, 0 FAIL, 1 timing ignoré**,
Clippy/check frontend WASM/build natif PASS. Traversée native filmée réussie ;
deux vues avant/après montrent la suppression de la bande verte sur la route,
avec une couture résiduelle. Cette preuve ne clôt aucun critère global d'art,
de performance ou de Web/Tauri ; voir `R1_ROAD_SURFACE.md`.

Contrôle global antérieur du 7 septembre : **611 tests PASS, 0 FAIL, 1 timing ignoré**,
commande `cargo test --workspace --locked --offline -q`, sortie 0 ; fmt/diff PASS.
Il couvre ensemble les correctifs de reprise des prototypes, de morphologie
nuageuse et d'invalidation terrain au remplacement du profil à niveau constant.
Ce dernier défaut a été reproduit puis corrigé sur les chemins synchrone et détaché
(15 tests terrain, Clippy/check frontend WASM PASS) ; voir `R1_QUALITY_TRANSITION_CPU.md`.
Ce résultat de non-régression ne ferme aucune réserve visuelle ou humaine.

Rejeu suivant également 611 PASS : même défaut corrigé côté végétation, y compris
reprise après un échec au même niveau avec une politique corrigée. Cycle ECS existant
étendu, Clippy/check frontend WASM PASS ; aucun nouveau bundle ou rendu qualifié.

**Gate 1 non validée ; Gate 2 non autorisée par ce rapport.** Gate C et Gate 0 conservent
leurs verdicts locaux documentés, Gate 0 sous ADR-0004/0005/0006. CI distante **NOT RUN**.
Le compromis ADR-0011 autorise des profils effectifs explicitement dégradés, pas un passage de gate.

Recommandation de travail : poursuivre Gate 1 en concentrant les prochains lots sur la qualité
visible et les capacités Web/Tauri manquantes. Aucun changement de moteur/pile n'est proposé.
Le verdict final poursuivre/réorienter/arrêter vers la suite de la roadmap reste à décider sur
les critères complets et avec les acceptations explicites. R1-055 reste donc ouvert.

Le socle technique est fonctionnel, mais les preuves disponibles ne démontrent pas encore la
qualité visuelle et les performances requises. Ajouter des tests à ce socle, isolément, ne ferme
pas ces manques. Les retouches réalisées hors du Studio ne prouvent pas le parcours d'auteur
dans le produit ; édition générale, undo et sauvegarde restent dans les gates suivantes.

## Matrice des critères de décision

Précision forêt vérifiée sur les GLB déployés rev.4 : les LOD visuels sont déjà
réduits (chêne 34 158/8 152/662 triangles, pin 46 508/7 522/1 228). Le batching
n'ajoute aucune réduction, mais ne signifie pas absence de LOD simplifié. La reprise
doit qualifier transitions/silhouettes/vent et coût du candidat existant avant de
prescrire une autre simplification. Hashes et portée du comptage dans
`R1_FOREST_FAR_PATH_DIAGNOSIS.md`. Aucun statut Unqualified retiré.

Complément terrain natif : préparation détachée implémentée, 14 tests ciblés et
Clippy strict PASS ; workspace 608 PASS / 1 ignoré et check WASM PASS. Ancienne
géométrie visible mais non qualifiée pendant un changement de berges au même niveau,
avec rétablissement vérifié après calcul. Build/rejeu natif PASS : cinq étapes GPU,
Low/retour High inspectés, appel terrain final 4,60–5,98 ms. Ni fluidité globale,
forêt complète ni chemin Web asynchrone qualifiés. Voir `R1_QUALITY_TRANSITION_CPU.md`.

Les liens ci-dessous sont des preuves de périmètre, pas des PASS globaux. Les résultats anciens
restent liés à leur binaire et à leurs entrées, même lorsqu'un test actuel revalide leurs artefacts.

| Critère de sortie | Preuve disponible | Ce qui manque au verdict |
|---|---|---|
| Qualité supérieure à la base historique et comparable à Cadle | [Comparaison descriptive](R1_052_CADLE_VISUAL_GAP.md), corrections végétation/sol ultérieures | Comparaison finale et acceptation artistique ; un écart important a été constaté, pas une équivalence démontrée |
| Matériaux, végétation, eau, composition | [Douze vues inspectées](R1_065_FIXED_VIEWS.md), [forêt Web](R1_STUDIO_FOREST_WIRING.md) | Sol/berges encore réguliers, silhouettes répétitives, brillance pluie et motifs d'eau ; revue globale à obtenir après corrections |
| 60 FPS moyens, p95 ≤16,7 ms, 1 % low ≥45 FPS, 1080p High sur cible | Trois runs release RX 7800 XT passent les seuils locaux, sans le nouvel aperçu nuageux : [preuve](R1_RELEASE_PREPARATION.md) | RTX 3060/RX 6600 non qualifiée ; candidat final avec effets retenus encore à mesurer. Ne pas confondre les anciens smokes dev avec cette preuve release |
| Absence de stutter récurrent | [Préparations terrain/végétation détachées en natif](R1_QUALITY_TRANSITION_CPU.md), cycle cinq niveaux réussi hors forêt | Dernier appel terrain 4,60–5,98 ms, végétation 75–85 ms ; cela exclut attente worker et lancement. Saccades restantes, chemin Web synchrone et transitions du candidat complet non qualifiés. Les 1,11–1,99 s antérieures ne décrivent pas le nouveau chemin |
| Profils qualité fonctionnels | [Plans effectifs, acquittements et adaptation native](R1_EFFECTIVE_QUALITY_PLAN.md) ; cycle sans forêt GPU acquitté | Forêt candidate toujours explicitement Unqualified, aucun HLOD conforme déduit du regroupement LOD2. Baisse sous charge réelle non déclenchée dans les deux essais 4K ; Web/adaptation et fluidité non qualifiés ; preset complet non revendiqué |
| Parité natif/Studio | Hashes/caméras alignés dans les smokes ; [navigation de scènes](R1_STUDIO_SCENE_NAVIGATION.md) | Exécution équivalente Tauri/WebView2 et qualification des effets ; compilation Web ≠ preuve Tauri |
| Contacts et exploration | [Traversées/vidéos](R1_048_BRIDGE_MOVEMENT_VIDEO.md), [36 requêtes Rapier sur kit réel](R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md) | Gestes humains prolongés, contacts et surfaces complètes du kit ; raycasts ≠ acceptation dynamique/artiste |
| Kit retouchable et reprise des corrections | [Pipeline gouverné](R1_063_GOVERNED_ASSET_KIT.md), conservation synthétique et reprise des références de prototypes ; copie portable 4.5.10 restaurée, réimport des deux arbres rev.4 PASS | Blocage de disponibilité Blender levé, installation 5.2 conservée. Retouches Blender humaines et revue retopo/UV/PBR/LOD dues ; ni reconstruction complète ni édition/sauvegarde Studio qualifiées par ce smoke |
| Provenance, aucun contenu Cadle requis | [Chaîne de provenance auditée](R1_053_PROVENANCE_CHAIN.md), [indépendance upstream](R1_054_NO_UPSTREAM.md) | Revue des droits/dérivations et statut de redistribution ; preuve d'indépendance historique à relier au candidat final, pas au seul chemin de fichier courant |
| Rapport et captures de la révision candidate | Série complète existante, présent rapport intermédiaire | Les douze captures ne portent pas le dernier binaire ; finaliser le candidat avant un rejeu nécessaire. Artefacts locaux ne signifient pas publication Git |
| Qualification et décision humaine | Critères inchangés dans la roadmap | Arbre de qualification propre sans nettoyer ce worktree, revue humaine et décision de passage explicites ; dette CI ADR-0006 conservée |

## Preuves actuelles à ne pas recommencer par erreur

- **Inventaire GPU nuageux exercé en natif** : cycle cinq niveaux/retour High,
  captures 1080p inspectées, `gpu_ready=true`, `full_preset_applied=false`.
  Sans forêt expérimentale : preuve isolée, pas du candidat complet. Nuages
  masqués/restaurés ; transitions saccadées, Web/Tauri et art non qualifiés.
  Voir `R1_CLOUD_LAYER_PREVIEW.md` ; ne pas relancer ce scénario inchangé.

- **Cycle global natif avec forêt candidate : échec borné** (7 septembre).
  Délai 120 s, sortie 1, aucune étape capturée. Le contrat des capacités classe
  explicitement `prototype_preview` en `Unqualified`, alors que ce cycle exige
  zéro blocker et readiness GPU. Ne pas réessayer à l'identique ni affaiblir le
  garde-fou. Ce n'est pas un simple retard de téléchargement Chrome.
  Voir `R1_CLOUD_LAYER_PREVIEW.md`.

- **Chaîne du kit rev.4 actualisée** : onze pins d'assemblage, six GLB runtime et
  six copies Studio, deux bindings et rapports candidat/réimport concordants.
  Contrôle d'intégrité uniquement ; l'ancien audit binaire rev.2 ne qualifie pas
  les nouveaux dérivés. Droits et art pending : `R1_053_PROVENANCE_CHAIN.md`.

- **Nuages expérimentaux natif/Chrome** : champ 3D, éclairage indirect de la vue,
  captures inspectées et cycle IBL 1→0→1 exécuté à caméra/temps fixes dans Chrome.
  Effet et retour visibles, kit rev.4 complet ; contrôle qualitatif ciblé terminé.
  Pas de promotion aux profils, de coût release ou de qualification Tauri/art.
  Voir `R1_CLOUD_LAYER_PREVIEW.md`. Ne pas rejouer ce cycle sans changement pertinent.

- **Compilation release PASS, smoke PASS** : build offline/locked terminé en
  12 min 50 s, sources inchangées. Premier smoke trop court pour le retour GPU,
  échec conservé ; smoke allongé chargé et capturé, 69,846 FPS moyens sur RX 7800 XT.
  Trois mesures sans capture terminées : 69,711–71,055 FPS moyens,
  p95 15,256–15,902 ms, 1 % low 57,076–58,607 FPS, aucun hitch >33,3 ms.
  Seuils locaux PASS ; aucun gain comparatif, qualification GPU cible ou passage
  de gate déduit. Voir `R1_RELEASE_PREPARATION.md`.

- **Dérivés bois et conifère** : rev.3 chargée natif/Web, comparaison native
  partiellement invalide pour resize ; rev.4 en rameaux chargée en natif puis
  dans Chrome avec les preuves de ciel (mêmes vingt poses, six GLB vérifiés).
  Sources/poses conservées, mais art et transitions non qualifiés. Voir
  `R1_WOOD_LOD_DERIVATIVE.md` et `R1_CONIFER_SPRAYS.md`.

- **Coût des variantes mesuré** : deux paires natives, même binaire/High ; mixte
  -3,59 à -6,09 % FPS moyens, +~84,2 Mio alloués. Pas de cible qualifiée ; candidat
  non promu, réduire les dérivés et revoir le conifère avant une nouvelle mesure.
  Protocole et limites : `R1_FOREST_VARIANT_SELECTION.md`.

- **Variantes Web v2 rejouées** : Clippy Web/Trunk PASS, High/Low/High avec six
  compteurs restaurés, deux PNG inspectés et kit prêt, sources/temps constants.
  Pas de qualification artistique/performance/Tauri : `R1_FOREST_VARIANT_SELECTION.md`.

- **Variantes raccordées au moteur ensuite** : 597 tests workspace PASS / 1 ignored,
  Clippy strict PASS, cycle ECS High/Low/High stable et capture native v2 inspectée.
  Deux silhouettes visibles, sources et anciens catalogues conservés ; conifère
  sombre, coût comparatif et Web v2 non qualifiés. Voir `R1_FOREST_VARIANT_SELECTION.md`.

- **Variantes gouvernées préparées** : catalogue v2 explicite et sélection stable,
  596 tests workspace PASS / 1 ignored, Clippy strict PASS. Pas de projection v2 :
  renderer encore en refus explicite, catalogue candidat inchangé. Raccord effectif
  et preuve visuelle restent à réaliser : `R1_FOREST_VARIANT_SELECTION.md`.

- **Dégagements authorés** : le spawn pilote sa clairière ; plus de corridor de fixture
  hérité dans le WorldSketch. 581 tests workspace avant extraction du helper, 109 monde
  après, Clippy natif/Web et Trunk PASS. CLI et capture native vérifiées, kit complet.
  Capture Web et diagnostic archivés, kit prêt et comptages concordants :
  650 arbres et 19 395 touffes ; ni art ni performance cible validés.
  Voir `R1_AUTHORED_CLEARINGS.md`.

- **Surface des routes** : masque compilé raccordé aux poids visuels terre, géométrie
  et colliders inchangés. 584 tests PASS / 1 ignored, Clippy strict natif PASS ;
  capture native inspectée, chemin jusqu'au pont visible. Palette et prairie encore
  à améliorer, performances cibles non acquises. Voir `R1_ROAD_SURFACE.md`.
  Complément Web : Trunk PASS, capture Chrome inspectée, kit prêt et console error vide.
  Retour humain : ciel peu réaliste, correction et acceptation toujours dues.

- **Aperçu nuageux natif** : pipeline exécuté et deux captures inspectées, défaut de
  tri devant le feuillage corrigé. 592 tests avant le correctif de tri, test ciblé et
  Clippy ensuite PASS. Nuages encore plats/répétitifs : ciel non accepté, mode
  expérimental désactivé par défaut. Voir `R1_CLOUD_LAYER_PREVIEW.md`.
  Complément filtrage/épaisseur : deux tests ciblés et Clippy moteur PASS, capture
  inspectée avec horizon moins rayé ; réalisme et performance cible toujours ouverts.

- **Prairie à budget High redistribué** : 580 tests PASS / 1 ignored, Clippy natif/Web
  strict et build Web PASS. 19 138 touffes, 612 416 brins, anciens placements préservés,
  catalogue candidat validé. Captures native/Chrome inspectées, compteurs Web cohérents ;
  naturel et performance cible non acquis.
  Voir `R1_MEADOW_COVERAGE.md`.

- **Sol éclairé en microrelief** : shader partagé, deux captures natives corrigées
  sec/pluie inspectées, dix tests terrain et Clippy moteur PASS. Build et capture Web
  vérifiés, kit prêt, console error vide. Premier essai rejeté
  pour quadrillage, interpolation adoucie. Ni prairie dense, ni acceptation artistique,
  ni performance cible acquises : `R1_TERRAIN_MICRORELIEF.md`.

- **Adaptation et visibilité** : 579 tests workspace PASS / 1 ignored, Clippy
  strict et build Web PASS. Focus/pause/reprise Chrome avec même High acquitté ;
  masquage réel et baisse automatique non qualifiés, refus de pointer lock
  conservés. Voir [visibilité adaptative](R1_ADAPTIVE_WINDOW_VISIBILITY.md).

- **Réutilisation des lots lointains** : 578 tests workspace PASS / 1 ignored,
  Clippy strict et build Web PASS, invalidation d'entrées modifiées testée. Cycle
  Chrome High/Low/High avec kit prêt et six compteurs restaurés. Comparaison CPU
  synthétique favorable, pas de qualification des transitions complètes ni du GPU.
  Voir [réutilisation lointaine](R1_FAR_BATCH_REUSE.md).

- **Caméra fixe et vent lointain** : conflit de suivi corrigé (575 tests PASS /
  1 ignored, Clippy et build Web PASS, rejeu deux pas vérifié). Contrôle optique
  suivant 0/2/0 s non concluant et identité physique différente au temps gelé ;
  ne pas relancer cette série à l'identique. Voir [contrôle borné](R1_FAR_WIND_ISOLATION.md).

- **Lots lointains expérimentaux** : 573 tests workspace PASS / 1 ignored,
  Clippy strict natif/Web et build PASS ; modes individuel/regroupé du même bundle
  exécutés/capturés dans Chrome. Cycle regroupé High/Low/High ensuite vérifié avec
  restauration exacte des compteurs et sources constantes. Plus de géométrie unique,
  aucun gain FPS ou HLOD optimisé qualifié ; mode par défaut inchangé.
  Voir [chemin lointain](R1_FOREST_FAR_PATH_DIAGNOSIS.md).

- **Comptages mesh réels de végétation**, 570 tests workspace PASS / 1 ignored,
  Clippy strict natif/Web et build PASS. Cycle Chrome High/Low/High archivé : sources
  constantes, comptages restaurés et aucun mesh non comptable. Anciennes estimations
  procédurales toujours invalides ; rendu lointain et performance non qualifiés.
  Voir [télémétrie réelle](R1_FOREST_LIVE_TELEMETRY.md).

- **Bindings forêt stables aux transitions**, cinq tests ciblés et cycle réel Chrome
  High/Low/High, sans modification de source ni promotion de capacité. Dernier passage
  **567 tests workspace PASS / 1 ignored**, Clippy strict natif/Web et build PASS.
  Voir [stabilité forêt](R1_FOREST_QUALITY_BINDINGS.md) ; visibilité/comptages et arbres
  lointains restent à qualifier, pas de gain FPS déduit.

- **Candidat complet accessible dans le menu Studio**, clic et affichage kit/forêt exercés
  dans Chrome ; 42 tests Studio, Clippy WASM et build PASS. Deux vues natif clair/pluie du
  dernier binaire inspectées, pas douze nouvelles vues ni un benchmark final. Voir
  [Candidat intégré et accès produit](R1_INTEGRATED_CANDIDATE_ACCESS.md).
- **SSR Web isolé en vue rapprochée**, trois captures par état avant/coupure/restauration,
  sources et caméra identiques. Support de chemin distinct d'IBL ; pas de readiness GPU
  déduite. Les vues natives sont chargeables opt-in sans altérer d'autre donnée de carte.
  Voir [vues candidates](R1_STUDIO_CANDIDATE_VIEWS.md) et [protocole optique](R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md).
- **IBL Web isolé ensuite**, neuf captures et sources/caméra/F0 contrôlés, SSR coupé ;
  effet net et restauré. Raccord vérifié dans Chrome avec refus de support à intensité zéro.
  Dernier passage : **566 tests workspace PASS / 1 ignored**, Clippy strict natif/Web,
  build Web et rejeu PASS. Pas un PASS Gate 1.
- **Support technique de teinte Web retenu sur la série existante**, sans rendre positif
  son filtre de contraste ni valider l'esthétique. Raccord et refus manuel rejoués, 566 tests
  workspace/Clippy/Web PASS. Sans kit/forêt expérimentaux, les quatre profils et le retour
  High reçoivent un acquittement GPU courant. Forêt candidate, adaptation, fluidité et Tauri
  restent distincts/non qualifiés ; détails et premier Ultra pending conservés au protocole optique.

- **Douze captures existent**, six poses sous clair/pluie, pas huit captures incomplètes.
  Manifeste local `r1-twelve-views-20260906/manifest.json`, SHA-256
  `889BD5510064CA52D72A9A98C31467338E0641CEE0F0E02D0119B720379CA98C`.
  Il identifie le binaire historique `f03282cf6df610909a27b7d0ba1d04308b030e9d0c4bc580adb44b5621959442`.
  Le binaire récent des sondes est différent : ne pas relabelliser ces images comme ses sorties.
- **Adaptation native raccordée**, opt-in, montée Low → Medium → High → Ultra avec
  acquittements distincts. Deux essais 4K ultérieurs sans baisse : résultat non concluant,
  pas un motif de modifier les seuils pour obtenir un PASS. Adaptation Web non qualifiée.
- **Kit et forêt déjà affichés dans le Web** dans leur périmètre de smoke ; les anciens
  paragraphes « branchement à faire » sont l'historique, pas l'état courant de ces chemins.
- **Passage précédent : 564 PASS / 1 ignored** au raccord SSR Web et vues candidates,
  Clippy strict/WASM, build/rejeu natif et bundle Chrome terminés. Blockers Web 3 → 4 → 3
  selon le choix SSR, sans readiness acquise. Exécutable Tauri candidat construit et lancé,
  premier affichage en échec puis déploiement corrigé : l'utilisateur confirme ensuite
  l'affichage Studio/vallée du candidat v2. Pas une qualification complète Tauri.
  Voir `R1_WEB_OPTICAL_SAMPLE_PROTOCOL.md`.
- **Passage précédent : 562 PASS / 1 ignored** au garde-fou empêchant
  l'adaptation dans une vue de preuve fixe : `R1_STUDIO_ADAPTATION_CONTROLS.md`,
  Clippy et check WASM PASS. Pas de nouveau replay Web déduit de ce test ECS.
  Le raccord météo Studio précédent a 42 tests Studio et un cycle par boutons documenté
  dans `R1_STUDIO_WEATHER_INPUT.md`. Ne pas additionner ces totaux.

## Prochaines étapes et conditions d'arrêt

Priorité technique identifiée dans le dernier journal natif (sans nouvelle campagne) :
préparation végétation synchrone 277–816 ms par transition, terrain 355–776 ms.
Les préparations végétale et terrain détachées sont maintenant implémentées en natif :
608 tests PASS / 1 ignoré, cycle réel hors forêt acquitté GPU. Appel terrain final
4,60–5,98 ms, végétation 75–85 ms sur le dernier run ; Web toujours synchrone.
Un smoke du même binaire avec forêt complète charge 650 arbres et le kit 20/20,
36 colliders prêts, terrain High et 20 chunks protégés. Fenêtre redimensionnée :
pas de benchmark 1080p déduit. Le refus forêt reste explicite : regroupement LOD2
sans simplification/qualification HLOD, aucune exception forêt ADR-0011 acceptée.
Ne pas recommencer le diagnostic comme un échec de chargement ou de terrain synchrone.
Exigences de bornage/invalidation et limite WASM : `R1_QUALITY_TRANSITION_CPU.md`.
Ne pas confondre ce gel visible avec les micro-audits CPU précédemment écartés.

1. Traiter les défauts visuels par des règles réutilisables (éclairage/matériaux sous pluie, silhouettes,
   berges), en gardant identités, poses, retouches et sources gouvernées. Une amélioration doit
   être visible sur les vues pertinentes ; ne pas se limiter à un compteur de tests.
   Le [diagnostic météo actualisé](R1_024_026_WEATHER_NATIVE.md) confirme que la correction
   de rugosité est déjà en place ; le soleil répond maintenant à un profil météo opt-in,
   avec cycles natif et Studio documentés dans `R1_WEATHER_NATIVE_CYCLE.md` et
   `R1_STUDIO_WEATHER_INPUT.md`. Aucun profil canonique activé ou choix artistique promu.
   Ne pas refaire ces raccords ni confondre pluie et ciel couvert implicite.
2. Lever les capacités Web réellement manquantes : isoler les effets d'eau avec un témoin
   exploitable, sans confondre résidu temporel SSR/TAA et effet de teinte. Puis qualifier leur
   intégration/adaptation et le shell Tauri ; aucune promotion de capacité à partir d'un bouton.
   Commandes d'activation et retour fixe désormais raccordées et exercées dans le navigateur :
   `R1_STUDIO_ADAPTATION_CONTROLS.md`. L'attente à quatre blockers eau appartient à
   un ancien bundle. Les raccords optiques et le tracking Web hors forêt expérimentale
   ont depuis leurs preuves bornées en tête du rapport ; ne pas les recommencer comme
   s'ils étaient absents. Baisse automatique, masquage réel, fluidité et Tauri restent dus.
3. Stabiliser un candidat cohérent, préparer sa qualification finale (captures, parcours,
   provenance, parité et performance cible), puis solliciter les acceptations requises.
   Ne pas refaire périodiquement une collecte inchangée ou un audit CPU général.

L'accès au matériel cible, les revues humaines et la décision finale exigent une intervention
externe ; ils n'autorisent ni à ignorer les travaux indépendants ci-dessus, ni à commencer Gate 2.
Toute préparation d'un arbre propre doit conserver intégralement le worktree présent ; aucun
commit/push/nettoyage n'est autorisé implicitement par cette liste.

## Contrôle documentaire de ce rapport

### Reprise et interventions externes — historique du début du 7 septembre

Le total à cette étape était 564 tests, pas une qualification complète de Gate 1.
Les totaux plus récents figurent en tête du rapport et dans le contexte canonique ;
ils ne s'additionnent pas. Cette ancienne mise à jour était documentaire uniquement.

| Étape | Élément à fournir ou à observer | Ce que cela ne valide pas |
|---|---|---|
| PNG récupérés | `forge-viewport-1788749273870.png` et `forge-viewport-1788761091797.png` présents et inspectés ; dimensions différentes | La réception ne prouve ni répétabilité, ni IBL/SSR/teinte ; pas une paire A/B comparable |
| Après récupération et comparaison contrôlée | Répétitions avant/changement/restauration, état/source/caméra identifiés, dispersion témoin et inspection | Pas de promotion sur un compteur CPU ou une différence comparable au bruit |
| Revue artistique du candidat cohérent | Avis humain sur végétation, sol/berges, eau/pluie, composition ; défauts précis à corriger | Ne pas demander de valider comme final le prototype procédural ni des captures d'un autre binaire |
| Parcours et retouches | Essai humain prolongé, contacts du kit, retouche Blender et conservation au réimport avec preuves | Les tests synthétiques et raycasts seuls ne satisfont pas ces critères |
| Plateformes et performances | Essai WebView2/Tauri et mesure RTX 3060 ou RX 6600 selon le protocole | Les smokes RX 7800 XT et la compilation WASM ne sont pas ces preuves |
| Décision de passage | Revue explicite des critères complets et candidat précisément identifié | Le compromis ADR-0011 n'est pas une validation Gate 1 |

Le fichier reçu lève l'attente de récupération ; le timeout d'observation n'est pas une
preuve de défaut produit. Les finitions visibles et qualifications listées restent dues.
Le replay intégré du 7 septembre, distinct de la synchronisation documentaire initiale,
est décrit dans `R1_INTEGRATED_CANDIDATE_ACCESS.md`. Préserver le worktree et les sources.

### Contrôle initial conservé — non rejoué lors de la synchronisation ci-dessus

Relecture des rapports liés et de la roadmap, branche/HEAD/diff inspectés. Pas de build,
nouveau rendu ou nouveau benchmark dans ce lot. Revalidation des artefacts existants :

```text
python scripts/Test-WorldSketchCapture.py E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-twelve-views-20260906
```

**4 tests PASS**, couvrant la série réelle et le refus de copies corrompues. Les originaux ne
sont pas modifiés ; les corruptions sont isolées dans les dossiers temporaires du test.
Log : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gate-status-20260907/capture-verification.log`,
SHA-256 `080AC43DB3622ED71808DB9EC8C7774BDF24AD7AC0758207DDA1E9D5F6C9AEA9`.
Cette vérification prouve l'intégrité/les contrats de cette série, pas la qualité artistique
ni sa représentativité du dernier code. Aucun contenu Cadle copié et aucune publication Git.
