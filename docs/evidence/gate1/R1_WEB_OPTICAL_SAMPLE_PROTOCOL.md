# R1 — Comparaison optique avec variabilité témoin

7 septembre 2026. Branche Rust, worktree conservé, aucune publication. Gate 1 ouverte.

## Cause et périmètre

Lecture actuelle de `global_quality/effective.rs` : les fonctions de qualification native
de teinte et de reflets reçoivent `!cfg!(target_arch = "wasm32")`. Le blocage Web n'est donc
pas une preuve de shader absent. Trois capacités restent non qualifiées : teinte, IBL,
SSR. La quatrième, réfraction écran, est indisponible ; sa substitution ADR-0011 dépend
de la teinte encore non qualifiée. Ni suppression des garde-fous ni promotion opérée.

Le diagnostic précédent de `R1_STUDIO_FIXED_PROOF_VIEW.md` reste applicable : temps virtuel
figé mais TAA/SSR indexés par frames. Les différences de teinte observées auparavant ne
dépassaient pas clairement les résidus de restauration. Pas de nouvelle collecte inchangée.

## Outil ajouté et protocole exécutable

`scripts/Compare-OpticalSamples.ps1` lit les images, sans les modifier. Paramètres :
`-Baseline`, `-Changed`, `-Restored` (listes de chemins), `-Region`, `-ControlRegion`
(x0,y0,x1,y1 exclusifs), `-Step` (2 par défaut). Sortie JSON sur stdout.
Utilise System.Drawing déjà disponible ; aucune installation Pillow ou dépendance produit.

Exiger au moins trois captures distinctes par état, même bundle/scène/caméra/résolution,
paramètres et sources vérifiés séparément. Capturer les trois états successivement, après
stabilisation, avec une région eau et une région témoin non contaminée. Ne pas remplir
les listes avec des copies : les contenus SHA-256 dupliqués sont refusés. Des hashes
différents ne prouvent pas à eux seuls l'indépendance des captures.

Le calcul rapporte les MAE par canal RGB 8 bits (pas la somme RGB des anciens rapports) :
maximum intra-état, minimum baseline/changement et maximum baseline/restauration.
Heuristique conservatrice : effet >3×max(bruit,1 niveau), restauration ≤2×max(bruit,1).
Ces seuils sont un filtre descriptif, pas un test statistique ni une mesure photométrique.
Le témoin est calculé séparément et doit être inspecté : une variation de lumière globale
ne prouve pas un effet propre à l'eau. Aucune conclusion automatique d'IBL isolée à partir
de la réflectance, qui agit également sur l'éclairage direct.

Le JSON reste toujours `qualification=not_qualified` et
`capture_state_identity_verified=false`. Les états de commande, identités, images, ROI,
histoires temporelles et effets de post-traitement exigent leur propre vérification.
Une série temporelle bornée ne qualifie ni tous les profils ni tous les backends.

## Vérifications réellement exécutées

- `scripts/Test-OpticalSamples.ps1` : cinq contrôles synthétiques PASS : effet distinct
  restauré, absence de changement, bruit dominant, restauration ratée, trop peu d'images.
- Lecture du PNG réel `r1-studio-fixed-view-20260907/forge-viewport-1788740489800.png` :
  région eau historique lue, distance à elle-même nulle, ROI hors image refusée.
- Invocation CLI avec les mêmes trois PNG dans les trois groupes : rejet attendu des
  doublons après lecture du premier groupe, aucun résultat optique positif inventé.
- Diff check PASS. Pas de code Rust/shader, source de carte ou seuil moteur changé ;
  pas de recompilation Rust non pertinente à ce lot d'outillage.

Une première exécution synthétique a échoué : le nom local `changed` héritait du type
string[] du paramètre homonyme lors du dot-sourcing PowerShell. Variable du test renommée,
sans modification du calcul ni affaiblissement des assertions. L'import Pillow initial
a échoué (module absent) ; aucune installation effectuée.

La prochaine preuve reste une vraie série répétée aux états identifiés, avec contrôle
IBL distinct de la réflectance. Aucun `Supported` Web, adaptation effective, performance
cible ou passage de gate acquis. CI distante NOT RUN.

## Tentative de collecte réelle suivante

Même bundle Studio d'adaptation `CBDC437C0D3A92520EC4FF977A2520F65DD56408083F517668212CBB484CA3CA`,
servi en loopback 1432 ; `forge_scene=world-sketch-visual-valley&forge_view=human_bridge`.
Avant toute série, une capture de contrôle par backend :

- Navigateur intégré : readback 448 × 798, nom annoncé
  `forge-viewport-1788749222379.png`, fichier absent de Downloads après observation.
- Nouvel onglet Chrome dédié, autres onglets préservés : monde 1, six hashes alignés,
  caméra human_bridge fixe, temps virtuel figé, High/clair ; readback 2048 × 1157,
  nom annoncé `forge-viewport-1788749273870.png`, également absent de Downloads.
  Liste d'erreurs console retournée vide. L'onglet intégré a été fermé avant la capture Chrome.

L'accès à `chrome://downloads/` filtré sur ce seul nom a été refusé par la politique
du navigateur. Aucun accès indirect/CDP ni contournement tenté. L'absence dans Downloads
ne démontre pas que le fichier n'existe nulle part ; son emplacement reste inconnu.
Pas de comparaison ni de série de neuf images sans artefacts récupérables.
La capture native antérieure ne remplace pas cette preuve Web.

Onglet Chrome de preuve conservé pour aide utilisateur, serveur loopback 1432 maintenu
à cette fin (PID 40640 observé). Demander le fichier réellement enregistré ou son chemin.
Aucun changement produit, seuil ou capacité. La qualification optique reste ouverte.

## Collecte récupérée et vue rapprochée — 7 septembre

L'attente de fichier ci-dessus est historique : les téléchargements fonctionnent lors de
cette reprise et sont copiés/vérifiés sous `r1-web-optics-20260907` sur E:. Aucune réparation
de l'export n'était nécessaire. Les scripts locaux Collect.ps1 et Collect-Detail.ps1 gardent
les originaux, refusent d'écraser une collection et contrôlent l'identité des sources.

`human-bridge` : neuf PNG 1408 × 797, trois diagnostics, même bundle antérieur et sources,
teinte 0,18 → 0 → 0,18. ROI (800,468,880,476), témoin (600,650,700,700), pas 1.
Bruit intra-état 3,569, différence 3,493, restauration 4,191 : **non concluant**.

La caméra rapprochée native n'était pas embarquée dans le Studio. Son chargement opt-in,
sans changement de carte, est maintenant [implémenté et exercé](R1_STUDIO_CANDIDATE_VIEWS.md).
Bundle `4C4A22C6A55F862D793C40263FB8BED2F187823571755AB186B6DD0BC9050F24`,
caméra water_detail, High fixe/clair, PNG 1408 × 741. Quinze PNG distincts et cinq diagnostics
dans `water-detail`, sources et hash du document de caméra identiques à chaque état.
Shader inchangé SHA-256 `12A4A84EF36945D124A2027B38EC01A770D98293F38414B1874347F6F4C9273E`.

| Série 3/3/3 | ROI, pas 2 | Bruit maximal | Changement minimal | Restauration maximale | Heuristique |
|---|---|---:|---:|---:|---|
| Teinte 0,18/0/0,18 | 200,450,500,600 | 0,067 | 1,900 | 0,124 | false : plancher absolu 3 non atteint |
| Profil/IBL seul/profil | 835,325,900,440 | 0,849 | 3,910 | 0,989 | true |

Témoin commun (200,200,400,240) : changement teinte 0,0365 ; changement SSR 0,658,
restauration SSR 0,188. L'effet du SSR n'est pas extrapolé à l'IBL : sa coupure conserve
réflectance 0,36 et teinte 0,18. Inspection des PNG : reflet de l'arbre absent sans SSR,
présent de nouveau après restauration ; réponse lumineuse directe conservée. Cela établit
le chemin SSR Web sur cette vue, pas sa qualité artistique ou la readiness de toute frame.
Le comparateur conserve `qualification=not_qualified` : il ne connaît pas l'identité des états,
vérifiée séparément avec les diagnostics. Aucune modification de ses seuils.

Les sources et états ont été contrôlés séparément avant le raccord de support SSR ; ce
raccord distingue SSR Web vérifié d'IBL Web non qualifié et conserve les contrôles courants
du matériau, de la caméra, du budget et des choix manuels. Teinte Web toujours non promue,
IBL non isolé, réfraction écran indisponible, aucune adaptation Web réussie déduite.
Les tests/replays du raccord sont consignés après leur achèvement ci-dessous.

### Raccord SSR terminé et rejoué

- 5 tests ciblés de résolution PASS ; puis **564 tests workspace PASS / 1 timing préexistant
  ignoré**, Clippy workspace/all-targets/all-features strict PASS et Clippy Studio WASM strict PASS.
- Build natif et Trunk terminés. Nouveau runtime SHA-256
  `34C9E4BA4F5CAF20CF17E9D05224305DEAE61212E8D7101013BABB992C938281` ;
  nouveau WASM `71C1DD39C7644E41E57E56DC926B77880DADA1CC4C1131834B7E56ED7BF275A8`.
- Smoke natif sans capture, 420 frames, kit complet 20/36, forêt 636/7632, sortie 0 et aucun
  événement ERROR. Eau : teinte 0,18, IBL 1, SSR 48, réflectance 0,36 ; seul blocker du
  candidat natif `vegetation`, readiness false (forêt non qualifiée), pas de régression
  de support natif constatée. Exécution pendant compilation Web : **pas un benchmark**.
- Chrome sur le nouveau bundle : profil → IBL seul → profil, blockers **3 → 4 → 3**.
  `screen_reflection` est retiré de la liste en mode profil et réapparaît à sa désactivation.
  IBL/teinte/réfraction restent bloqués, `gpu_ready=false`, sources constantes, console error vide.
  Collect-Support.ps1 contrôle les trois diagnostics archivés `support-state-0..2.json`.
  Pas de recapture inchangée du shader pour ce raccord de déclaration.

Hash log workspace : `CD641AE6D6F3745A20D7B965CAC279D952090C1838CEEB0B9BB22CD5F4568AC1`.
Hash métriques smoke : `1DDF3479F176FA5B8152C909A96B328C055E2EBEEA290F29D09860F76747B864`.
Les sources de carte et le shader conservent leurs hashes ; la qualification SSR est celle
du chemin observé, pas un verdict de qualité sur toutes les caméras, profils ou WebView2.

### Candidat desktop préparé pour la revue WebView2

Tauri CLI 2.11.4, build debug `--no-bundle`, code 0, frontend ci-dessus et neuf fichiers
kit copiés/vérifiés identiques. Overlay local de configuration seulement : URL WorldSketch,
kit/forêt explicitement demandés. Configuration et sources produit inchangées ; pas de release,
d'installateur, de redistribution ou de publication. Compression et custom-protocol présents
dans le fingerprint tauri-macros de ce build.

Exécutable local : `r1-web-optics-20260907/forge-studio-candidate.exe`, SHA-256
`153803CAB269F3BB0EF87359CC93690D1469D0D7C3B8D1494FDC862BC26E0021`.
Lancé : PID 45684, fenêtre « OpenRPG Forge Studio · Gate 1 candidat local », Responding=true,
processus WebView2 enfant 29648, version installée 152.0.4191.66. Ces observations ne prouvent
**pas** les pixels ni le chargement des GLB dans WebView2. Les outils de cette session ne
permettent pas d'inspecter la fenêtre native ; capture/retour utilisateur demandé.
Application laissée ouverte pour cette revue, pas de validation Tauri ou humaine inventée.

### Échec Tauri signalé puis correction du déploiement

Capture utilisateur `codex-clipboard-dfd373eb-e9dd-481b-8897-f238afb920dd.png`, SHA-256
`86B9A4A7F24BB288C4730595007983F46BE7DE42999BE53618E9FEAC33E35A8E` :
fenêtre du candidat initial affichant **ERR_FILE_NOT_FOUND**. Verdict affichage initial FAIL,
pas une défaillance GPU établie. La présence du processus ne prouvait pas le frontend.

Cause reproduite avec le type réel Tauri : FrontendDist est un enum non taggé essayant Url
avant Directory. Le chemin Windows `E:/...` de l'overlay était une URL de schéma `e`, donc
aucun asset embarqué et démarrage sur une adresse invalide. Le défaut était dans notre
configuration locale de candidat, pas dans la carte ou le shader.

Overlay corrigé avec chemin relatif depuis apps/forge_studio. Build-Tauri.ps1 refuse
dorénavant un chemin absolu/URL et vérifie la destination résolue avant build. Nouveau test
shell démontrant le cas URL Windows et le cas Directory relatif ; quatre tests shell PASS.
Pas de changement de dépendance ou de configuration produit par défaut.

Build v2 code 0, frontend/kit inchangés ; exécutable **52 869 632 octets**, contre 8 332 800
pour l'ancien shell sans frontend. Nouveau fichier `forge-studio-candidate-v2.exe`, SHA-256
`10DC6215C8D30276510A8260806BC756A7DF52C1C89FD2281A6A85944BC6C62E`.
Ancien fichier et son log conservés comme preuves d'échec. Seul son processus identifié
45684 a été fermé ; v2 lancée PID 43264. L'affichage restait alors à confirmer :
le build, la taille ou la fenêtre répondante ne constituaient pas une qualification visuelle.

### Confirmation utilisateur de l'affichage v2

À la question « La nouvelle fenêtre affiche-t-elle maintenant le Studio et la vallée ? »,
l'utilisateur répond « oui ». L'affichage est donc confirmé par retour utilisateur pour ce
candidat ; aucune nouvelle capture ni mesure WebView2 n'est déduite de ce retour.
L'échec initial reste conservé. Les effets optiques, performances, parcours prolongés et
acceptations artistiques restent à qualifier ; Gate 1 demeure ouverte.

### Sonde IBL Studio et premier cycle visible — 7 septembre

La vue fixe expose maintenant deux commandes transitoires de coupure/restauration de
l'intensité `EnvironmentMapLight` de l'unique caméra active. Identités caméra/textures
conservées ; caméra absente/ambiguë, textures remplacées ou intensité initiale invalide
refusent la mutation. Seule l'intensité IBL change, pas le F0 eau, les lumières directes ou
les documents. Le diagnostic exporte la valeur observée avant écriture et la demande,
sans acquittement GPU inventé. Les refus ECS n'ont pas encore de nouveaux tests dédiés.

Premier essai navigateur : boutons interceptés par le `pointer-events: none` du bandeau ;
aucune coupure observée, captures exclues de la preuve IBL. Correction bornée : les boutons
du bandeau reçoivent les événements, son texte conserve le comportement précédent.
Build corrigé `web-ibl-probe-v2` terminé, 40 tests lib Studio PASS, Clippy WASM strict PASS
avant correction CSS, diff check PASS. Pas de nouveau total workspace revendiqué.

Cycle réel Chrome, vue candidate `water_detail`, 1408 × 741, SSR explicitement coupé :

| État | PNG | Diagnostic | IBL observée |
|---|---|---|---|
| Initial | 1788773307877 | 1788773310114 | 1.000 |
| Coupée | 1788773329439 | 1788773330647 | 0.000 |
| Restaurée | 1788773352870 | 1788773353958 | 1.000 |

Fichiers reçus et inspectés, puis archivés avec contrôle SHA-256 dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-web-optics-20260907/ibl-smoke/`.
`Collect-Ibl-Smoke.ps1` contrôle les trois états, association capture/export, SSR désactivé,
réflectance 0.360 constante, caméra unique, sources identiques et readiness false.
Les images montrent l'assombrissement de l'eau et des ombres à la coupure, puis leur
restauration ; le reflet lumineux direct persiste. Console error vide. Cela démontre
le contrôle et un effet visible, pas encore la série répétée ni le support IBL global.
Le kit affiche encore 0 colliders/chargement : aucune qualification de physique n'est déduite.
Teinte, IBL dans le plan effectif, parité WebView2 et Gate 1 restent non qualifiées.

### Série IBL répétée et raccord de support

Même bundle WASM `A05866DE2D66555449E39A209022D0854EA99F945DC1C33F3F223B021F8A0B2C`,
caméra et dimensions conservées. Neuf PNG distincts archivés avec contrôle de copie dans
`r1-web-optics-20260907/ibl-repeated`, trois par état :

- initial : 1788773352870, 1788773507265, 1788773523557 ;
- coupé : 1788773547057, 1788773557592, 1788773571887 ;
- restauré : 1788773595634, 1788773608055, 1788773616386.

Diagnostics 1788773353958, 1788773548155, 1788773596720 vérifiés par
`Validate-Ibl-Identity.ps1` : sources/caméra constantes, intensités 1/0/1, SSR désactivé,
F0 0.360 constant, readiness false. Captures représentatives coupée/restaurée inspectées,
reflet direct persistant ; console error vide. Le témoin initial est l'état restauré du smoke
précédent, sans changement de scène ou de commande entre ses trois échantillons.

Comparateur inchangé, ROI eau (200,450)-(500,600), pas 2 : bruit maximal 0.111733,
écart minimal 33.234696, retour maximal 0.068741 niveaux RGB/canal : séparation/restauration
vraies. La ROI terrain (200,200)-(400,240) change aussi (9.7995, bruit 0.468833,
retour 0.1685) : **pas un témoin non affecté**, puisque l'IBL est coupée pour toute la scène.
L'attribution repose aussi sur la commande isolée, les identités, le SSR coupé et l'inspection ;
le comparateur conserve son verdict automatique `not_qualified`, sans seuil affaibli.

Cette preuve établit le chemin IBL Web indépendamment du SSR, pas une qualité artistique
ni une disponibilité GPU courante. Le raccord `reflection_support` admet désormais ce chemin
en Web comme en natif, en conservant tous les garde-fous d'observation, textures, intensité
positive, matériau et désactivation manuelle. Le test nouveau exige le refus à intensité zéro
puis le retour du support à restauration, SSR restant indisponible. Teinte Web non promue.
Validation terminée : **566 tests workspace PASS / 1 timing préexistant ignoré**,
Clippy workspace/all-targets/all-features et WASM strict PASS, build Web code 0.
Log workspace SHA-256 `378452CFD3DA5131A58E4008B502A4AE8D9F22ED51B509A608503F27209725C8`.
Bundle final `web-ibl-support`, WASM SHA-256
`57F2E63F580E20DF61D946C8AAA367BAFEE3E5E568B20BD88637E09FEFBDA85D`.
Shader eau inchangé (`12A4A84EF36945D124A2027B38EC01A770D98293F38414B1874347F6F4C9273E`).

Rejeu réel du raccord dans Chrome : SSR coupé et forêt candidate non qualifiée, blockers
4 → 5 → 4 ; `environment_reflection` apparaît seulement à intensité nulle. Les réserves
végétation/SSR/teinte/réfraction restent explicites, `gpu_ready=false`, sources constantes,
console error vide. Diagnostics 1788774044424/1788774058749/1788774071542 reçus, vérifiés
et archivés par `Collect-Ibl-Support.ps1`. Aucun rendu identique recapturé pour ce seul
raccord de déclaration. Pas de nouveau binaire natif exécuté, ni de qualification WebView2.

### Décision de support technique de la teinte — contraste non accepté

Relecture des neuf PNG de teinte déjà archivés, des diagnostics
1788769837093/1788769913742/1788769966011 et du shader inchangé : sources et caméra
identiques, teinte 0.180/0/0.180, F0 0.360 et choix de reflets constants. Le témoin terrain
ne suit pas la commande. La variation de couleur est discrète dans les images inspectées.

Le résultat conservateur **`separated_and_restored=false` reste conservé** : l'effet
1.900059 niveaux/canal ne dépasse pas le plancher de contraste de 3. Ni le script,
ni le seuil, ni les images, ni la valeur authorée 0.18 ne sont modifiés. Ce n'est pas
une réussite de ce filtre et aucune acceptation artistique n'est acquise.

Le support technique est désormais retenu séparément, selon le même raisonnement
d'exécution que la preuve native : effet répété sur l'eau (1.900059) largement au-dessus
du bruit intra-état (0.066756), retour faible (0.123674), témoin stable (0.0365 à la coupure),
commande/identités vérifiées et shader exécuté. La formule `d + s*d*(1-d)` explique
l'amplitude bornée et exclut l'attente erronée d'une disparition de la palette de profondeur.
Ce verdict ne transforme pas une détection de variation en contraste suffisant.

Le raccord `surface_tint_supported` conserve les refus de domaine/uniformes incohérents,
observation absente, valeur invalide, projection non acquise ou désactivation manuelle.
Il autorise le chemin Web comme le natif, et donc la substitution ADR-0011 déjà acceptée
« réfraction écran indisponible → teinte/profondeur ». Il n'implémente pas de réfraction du
décor et ne change pas le sens de `full_preset_applied` ou de la readiness GPU.
Validation terminée : 566 tests workspace PASS / 1 timing préexistant ignoré, Clippy strict
workspace/all-targets/all-features et Studio WASM PASS, build Trunk code 0.
WASM final `AF7812D8DF92FC2BFDBB9859DAB13DA7CA05A065486C579F69D524BB36E8DEA1` ;
log workspace `8CA4D4BD7E4DDBCD1328576E4F2C84422AE0AD9516ECB88501C5EE500F392B07`.
Shader et sources inchangés.

Rejeu Chrome avec forêt candidate : blockers 1 → 3 → 1 à la coupure/restauration de teinte.
`screen_refraction_to_surface_tint` est présente seulement quand la teinte est active ;
réserve végétation conservée, readiness et full preset false. Diagnostics
1788774643477/1788774657815/1788774671867 archivés/vérifiés par `Collect-Tint-Support.ps1`.
Sources constantes, console error vide. Cela ne rend pas le filtre optique positif.

### Premier acquittement des profils Web sans forêt candidate

Même bundle, WorldSketch/caméra candidate inchangés, mais sans opt-in kit/forêt : ce
périmètre exclut explicitement le projecteur expérimental toujours bloquant. Ce n'est
pas une qualification du candidat complet. Plan High résolu puis acquitté par le renderer :
502 meshes, neuf matériaux, cinq images préparés, 69 pipelines sans attente/échec,
11 groupes d'effets préparés, queue terminée et identités courantes concordantes.

Profils exercés par boutons avec acquittements distincts :

| Profil | Séquence acquittée | Diagnostic |
|---|---:|---|
| High initial | 1 | 1788774750049 |
| Low | 2 | 1788774766919 |
| Medium | 3 | 1788774784829 |
| Ultra | 6 | 1788774858057 |
| High restauré | 7 | 1788774879914 |

Le premier Ultra (séquence 4, diagnostic 1788774803597) était encore pending quand
notre commande High l'a interrompu ; **pas un PASS ni un échec renderer**. Cet état est
archivé. Après High, Ultra a été redemandé et observé jusqu'à readiness avant le retour final.
`Collect-Web-Plan.ps1` vérifie chaque identité application/soumission/hash/génération,
les ressources, l'absence de pipeline pending/failed, les sources et full preset false.
PNG final 1788774881086 reçu/inspecté ; aucune erreur console. Captures/profils intermédiaires
non inspectés visuellement. Caméra figée, 1408 × 741 : ni benchmark, ni fluidité des
transitions, ni adaptation, ni WebView2, ni acceptation artistique. Artefacts conservés sous
`r1-web-optics-20260907/web-plan-cycle`. Gate 1 reste ouverte.
