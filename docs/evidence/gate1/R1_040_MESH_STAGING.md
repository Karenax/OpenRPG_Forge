# R1-040 — publication progressive des meshes végétaux

11 septembre 2026, branche Rust, worktree préservé. **Implémentation en validation ;
aucun gain de fluidité ou verdict de Gate 1 acquis à ce stade.**

## Changement moteur

Le diagnostic de `R1_QUALITY_TRANSITION_CPU.md` a identifié la publication de tous
les meshes au même appel et exclu le simple limiteur Bevy comme solution suffisante.
La correction prépare un seul jeu de remplacement, puis ajoute au plus quatre meshes
par frame avec budget souple 8 Mio. Un mesh indivisible peut dépasser ce budget.
La géométrie et les profils ne sont ni simplifiés ni modifiés.

Les anciens bindings restent visibles pendant l'extraction et les transferts des
nouveaux assets. Un ticket propre à cette transition exige les RenderMesh et
allocations vertex/index, puis la callback de fin de soumission RenderQueue.
La bascule conserve les clés logiques et entités, remplace les handles dérivés,
retire les anciens meshes CPU et libère leurs handles pour le nettoyage renderer.
Les handles Bevy ne sont pas des identifiants authorés. Le chemin headless synchrone
conserve son comportement précédent ; le chemin avec RenderApp est commun natif/Web.

Une requête/politique remplacée invalide le ticket et retire les assets non publiés.
Les hashes terrain/végétation sont revérifiés avant la bascule ; sources changées,
bindings vivants manquants ou ticket invalidé ne donnent pas de projection réussie.
Une callback obsolète ne peut pas réactiver un ticket annulé. Un reset renderer
invalide le ticket extrait. Le reçu global du plan reste indépendant et doit encore
constater l'installation des effets, ressources et pipelines après la bascule.

Mémoire : au plus un remplacement préparé en plus du rendu courant, pas un cache
Low/Medium/High/Ultra. Surcoût transitoire réel, non encore mesuré. L'ancien jeu
ne doit pas être supprimé pour réduire ce coût avant le reçu de remplacement.
Le démarrage initial, le recalcul des HLOD forestiers gouvernés et les autres
domaines de qualité ne sont pas rendus progressifs par cette correction.

## Vérification en cours

Deux premiers tests ciblés PASS : anciens meshes intacts sans reçu, bascule et
retrait des anciens assets après reçu synthétique ; annulation, source changée,
libération des assets non publiés. Un troisième cas politique/reset renderer a
été ajouté ensuite. Ces tests n'installent pas de GPU réel.

Premier Clippy refusé : import glob, signatures système Bevy et longueur de la
fonction principale. Import explicite, annotations localisées aux paramètres
imposés par Bevy et extraction de la fin de préparation ont corrigé ces points.
Clippy workspace strict PASS ensuite en 28,04 s ; log initial rouge préservé.
Suite workspace/all-targets/all-features terminée : **714 PASS, deux sondes ignorées**,
compilation 3 min 39 s puis tests moteur 31,14 s (324 PASS/deux ignorés), autres
crates également réussies. Les trois tests de staging sont présents dans ce log.
Clippy moteur Web/WASM strict PASS, 24,89 s ; fmt et diff-check PASS.
Le script se termine avec `MESH_STAGING_CODE_PASS_NOT_RUNTIME_QUALIFICATION`.
Build frontend release PASS en 2 min 38 s, shell Studio PASS en 29,74 s.
Sources identiques revérifiées après build ; avertissement futur
proc-macro-error2 2.0.1 conservé, non présenté comme corrigé.

## Première acquisition — cycle non terminal

EXE `ea586801518f3545f57e88212839520d8518ceab28ef0feb0cf487665b09c03e`,
WASM `d6b76c3ebbe9f23ccc0119269e9e0a07bc3875fccef34a03e9043679371e7881`.
Studio réellement lancé, JSON et PNG 1408 × 966 acquis et image inspectée.
Forêt/chemin/personnage présents ; tramage visible, eau hors cadre final.
Préchauffage Ready 5/5 en 64,7885 s ; dernière observation High GPU prête,
740/740 meshes inventoriés et 134 pipelines sans attente/échec. Parcours
completed, 518/518 ticks au sol, 434 contacts, 42,530 m.

**Pas de cycle mobile qualifié** : champ `quality_motion=null`. Inspection du
code de `quality_motion_probe.rs` : le JSON n'est publié qu'à réussite ou à
échec terminal, avec délai de 120 s après préchauffage. L'essai a été fermé
avant ce délai et sans statut de cycle affiché. Ne pas traiter le parcours
terminé ou le reçu High final comme les trois reçus en mouvement.
Sortie normale 0 et huit processus fermés, aucun survivant ; originaux conservés.
La reprise `terminal-run/` a affiché `FAILED: délai ou parcours`. Le watchdog
360 s a fermé les huit processus avant l'export détaillé ; zéro survivant.
Cette acquisition ne possède ni JSON terminal ni PNG : ne pas lui attribuer
les exports d'un autre essai.

## Acquisition terminale et correction du débit

`terminal-recovery/` exécute le même binaire, sans rebuild. JSON terminal et PNG
1408 × 966 exportés par les commandes du Studio ; image inspectée : forêt,
chemin et personnage présents, tramage encore visible, eau hors cadre.
Sortie normale 0, neuf processus arrêtés, zéro survivant. `Inspect.py` exécuté :
284 sources, EXE et frontend vérifiés avant toute nouvelle modification ;
verdict **not_functionally_qualified**, deux reçus en mouvement seulement.

| Observation | Précédent bank-reuse | Staging 4 meshes / 8 Mio |
| --- | ---: | ---: |
| Préchauffage 5/5 | 56,818 s | 68,111 s |
| Pic de frame Low | 143,5 ms | 89,6 ms |
| Pic de frame High | 264,5 ms | 101,4 ms |
| Premier reçu Low depuis début du cycle | 3,662 s | 4,330 s |
| Premier reçu High de retour | 8,225 s | 10,435 s |
| Cycle High/Low/High en mouvement | PASS | FAILED |

Les pics diminuent dans ces traces, mais la disponibilité High arrive trop tard
pour le parcours : **pas de qualification de fluidité**, ni d'amélioration globale
affirmée. La trace High actuelle inclut aussi l'attente après arrêt du personnage
(2 825 frames, dont 86 en mouvement), contrairement à la référence. Les deux plans
enregistrés correspondent à la référence ; cela ne prouve pas trois reçus mobiles.
Le commit végétal monte à 32,5/31,5 ms : ce coût n'est pas un timestamp GPU.

SHA-256 sous `terminal-recovery/` :
- JSON : `c165ad08750b9bbac3003dfe85cbf00e77f53f4abb79dee823716c0e0e670590` ;
- PNG : `a63a6a084a6fd317a4fc7e6b1ff8f6fa7bebc7518e555229e551818ab898e457` ;
- nettoyage : `6178d12d9aed49893ae1f154990de5345b33da10355302a07a5ba9e97bc478ec`.

Correction suivante implémentée, validée séparément dans
`r1-mesh-upload-throughput-20260911/` : plafond de seize meshes et 12 Mio/frame,
avec contrôle de la taille suivante **avant** insertion. Seul le premier mesh
indivisible peut dépasser le budget ; les petits meshes ne sont plus limités à
quatre. Test ajouté pour remplissage exact, dépassement, plafond et saturation.
Ce choix est un candidat de débit, pas une garantie de gain. Aucun changement
du délai, du parcours, des huit frames mobiles exigées ou de la qualité des assets.

### Résultat du candidat seize meshes / 12 Mio

Clippy workspace/all-targets/all-features strict PASS (22,74 s). Quatre tests
ciblés staging PASS (compilation 1 min 36 s, exécution 0,82 s), dont le nouveau
test de budget. Fmt initialement refusé puis corrigé et PASS ; diff-check PASS.
Les 714 tests globaux ci-dessus restent ceux du candidat initial, pas une nouvelle
exécution complète. Frontend release PASS (2 min 24 s), shell construit ;
avertissement futur proc-macro-error2 inchangé. Les 284 entrées source et fichiers
du build ont été revérifiés par `Inspect.py` après acquisition.

EXE `a2f07a0f2218a3ada362800a81f1436e8253a883aadb6b6383f97ba2c2688a13`,
WASM `68d7c870720ac6d649cde93e0f3e160eb957347bce74e494b75c27d74d2a721a`.
Préchauffage Ready 5/5 en 64,901 s. Low prêt à 3,695 s, High de retour à
9,255 s depuis le début du cycle. Pics Low/High 97,6/103,9 ms ; commits
végétaux maximaux 30,0/30,6 ms. Deux reçus mobiles, **FAILED: délai ou parcours**.
Le débit réduit le retard observé, sans le résoudre. Ne pas transformer ce résultat
en succès en prolongeant la marche ou en comptant les frames immobiles.
La trace High comprend 2 846 frames dont 102 mobiles ; ce périmètre reste différent
de la trace historique arrêtée au PASS. Aucune attribution directe au GPU.

JSON et PNG 1408 × 966 acquis et inspecté : forêt/chemin/personnage présents,
tramage encore visible, eau hors cadre. Sortie normale 0 ; huit processus fermés,
aucun survivant. SHA-256 dans `r1-mesh-upload-throughput-20260911/` :

- JSON `b3f1e6a29048a1f9232b09d7648d5866fd9768c821628d700d31f774cf8df318` ;
- PNG `ac97fec82ae8bdb7900aaebefbd82f573df217a52651f48af19e1c67a7623208` ;
- nettoyage `752e344a6e78dc1cc9ba1cd60a4d11e70566eb17aa16eead02aa93b0cde42c51`.

**Suite technique identifiée par cette acquisition** : réduire le délai séquentiel entre
préparation, transfert et publication, plutôt que poursuivre une hausse empirique
du budget de transfert. Conserver l'ancien rendu jusqu'au reçu, une seule version
en attente et les mêmes critères mobiles. Le candidat courant est conservé comme
travail non qualifié, pas promu comme candidat final. Qualification native de ce
nouveau chemin, fluidité, réserves temporelles/humaines et dossier final restent dus.

## Chevauchement préparation/transfert — candidat suivant

Implémentation en validation dans `r1-mesh-preparation-overlap-20260911/`.
La préparation Web fournit ses chunks terminés à une file propre à ce travail ;
les buffers sont déplacés, non clonés. Le transfert utilise le même plafond
seize meshes/12 Mio, pendant que la préparation des chunks suivants continue.
Le chemin natif de préparation en worker reste inchangé. Les données sources,
profils et résultat géométrique final ne sont pas modifiés.

Aucun ticket GPU n'est créé avant la fin réussie de la préparation entière.
Une nouvelle identité de préparation (même politique comprise) retire les uploads
partiels obsolètes ; une préparation rejetée ou invalidée par ses sources les
annule aussi. Le rendu courant reste intact jusqu'au reçu de la version complète.
La file et les uploads appartiennent à une seule version en attente, pas à un
cache de profils supplémentaires. Aucun changement du parcours de qualification.

Deux tests ajoutés : transfert réellement commencé avant fin de préparation,
géométrie finale exacte (attributs et indices), ancien rendu conservé sans reçu ;
abandon/reprise de même politique sans conservation des uploads précédents.
Clippy workspace strict PASS (22,25 s). Premier passage de la suite : test d'abandon
refusé car sa fixture lisait `prepared.meshes`, déjà déplacé vers `remaining` par
`start`. Fixture corrigée pour transmettre de vrais meshes ; assertion conservée.
Log rouge initial préservé. Rejeu sous `validation-2/` : Clippy strict PASS (22,07 s),
**717 tests PASS, deux sondes ignorées**, compilation 2 min 50 s, puis Clippy Web
strict PASS (19,18 s). Fmt/diff-check PASS. Build frontend release PASS (2 min 22 s),
shell Studio PASS ; sources inchangées après build, avertissement futur
proc-macro-error2 conservé.

### Acquisition réelle du chevauchement : cycle mobile PASS

EXE `8f1e20030f14be5b2b2b84a29408394252600b7e2b0872df9fadac9ef8b13d90`,
WASM `5fe12b0cd747d317511b1ea6db9e704ed370e157ab296b2973a511b26d533745`.
`Inspect.py` exécuté : 284 sources et fichiers du build vérifiés ; verdict
`functional_prewarmed_moving_cycle_verified`. Trois reçus High/Low/High,
plans/sources/politiques identiques à la référence, positions distinctes et
huit frames mobiles stables pour chaque reçu. Aucun seuil ou trajet modifié.

| Observation | Séquentiel 16 meshes/12 Mio | Chevauchement, même budget |
| --- | ---: | ---: |
| Préchauffage 5/5 | 64,901 s | 60,864 s |
| Premier reçu Low depuis début du cycle | 3,695 s | 3,655 s |
| Premier reçu High de retour | 9,255 s | 8,418 s |
| Pic de frame Low | 97,6 ms | 97,8 ms |
| Pic de frame High | 103,9 ms | 101,8 ms |
| Cycle mobile | FAILED | PASS |

Low 74/74 frames mobiles, High de retour 89/89. La comparaison du pic High à
la référence antérieure sans staging reste 264,5 → 101,8 ms ; ce sont des pics
observés, pas une mesure de FPS moyens/p95/1 % low ni un succès de fluidité globale.
Le reçu High reste légèrement plus tardif que la référence sans staging (8,225 s).
Le maximum de préparation végétale High est 36,7 ms et celui du commit 30,6 ms :
coûts encore présents, non cachés par le PASS fonctionnel.

JSON et PNG 1408 × 966 acquis via le Studio ; PNG inspecté : forêt, chemin,
personnage présents, tramage encore visible, eau hors cadre. Parcours completed,
518/518 ticks au sol, 434 contacts, 42,530 m. Sortie normale 0 ; huit processus
fermés, zéro survivant. SHA-256 sous `r1-mesh-preparation-overlap-20260911/` :

- JSON `1be33875721d511bd005cc66b0e0ad22710f25bb15fb0f9f6a78422f28accbb9` ;
- PNG `8f85581ff11112545e89ab6df1b544b04ebeffc0ca3f963d36aee954dbe66b9e` ;
- nettoyage `e77c636030ef04424e3b4bcc0161d567ddb9fc9ee3835179395eaf1ba9854394` ;
- manifeste sources `2cde74b63bb77f2e7a44110d502026c72b075cec6c84f002bd30b8235ef2b661`.

La régression de délai est résolue dans ce protocole Studio. Conserver ce candidat
pour la qualification native et la consolidation suivante plutôt que poursuivre
des ajustements empiriques de débit. La fluidité complète, le tramage temporel,
les revues/retouche humaines du kit et le dossier final sur révision exacte restent
distincts et ouverts. Gate 1 ouverte ; Gate 2 non démarrée, aucune publication Git.

Logs/scripts :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-vegetation-mesh-staging-20260911/`.
Les anciens exports et index demeurent historiques : ils n'exécutent pas ce code.
Aucun changement d'asset canonique, publication Git ou passage Gate 2.
