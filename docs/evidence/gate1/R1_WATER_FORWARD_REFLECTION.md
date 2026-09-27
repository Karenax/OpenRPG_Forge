# R1-023 — réflexion du décor dans le candidat transmissif

9 septembre 2026, branche Rust. Prototype natif explicite, pas de promotion de
profil, pas de publication, Gate 1 ouverte.

## Isolation native des reflets par les profils existants — 9 septembre

Trois captures Full HD inspectées : eau Medium → Low → Medium ; autres domaines
High fixes, météo claire, temps virtuel/fixe zéro, caméra `water_close` identique.
Ces deux presets d'eau ont la même réfraction de surface ; seule la demande de
reflets écran diffère (Medium 24 pas/64 m, Low environnement seul). Aucun global
adaptatif, aucun benchmark. Le kit courant atlas recadré et les nuages sont présents.
1 662 arbres ; captures demandées après readiness des pipelines, chaque sortie 0,
aucun événement ERROR et entrées épinglées inchangées. PIDs 16316, 920 et 16824
fermés, absence vérifiée après la troisième capture.

Observation : reflet du pont visible en Medium, absent en Low, retrouvé au retour.
Écart RGB moyen échantillonné tous les quatre pixels dans la zone du reflet
`[600,520,400,180]` : 9,8484/255 entre Medium et Low, 0,0083/255 après restauration.
Contrôles hors eau : ciel 0,0175/0,0364, berge 0,0112/0,0376 (changement/restauration).
L'overlay est exclu des régions mesurées. Une capture par état ne remplace pas le
protocole statistique multi-échantillons et ne qualifie ni mouvement, réfraction
écran, Web, profils complets, art ou GPU cible. Ne pas déduire de performance des FPS
affichés dans les images. Aucun `Supported` ajouté sur la base de ce seul lot.

Répertoire `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-reflection-isolation-20260909` :
arguments, pins, journaux, états des processus, `Compare.ps1` reproductible et
`comparison.json`. Le snapshot `r1-reflection-isolation-inputs-20260909-b` ajoute
uniquement la caméra ; compilations CLI comparées, terrain/végétation et autres
dérivés inchangés hors liens de provenance. Premier essai de préparation rejeté
pour mauvaise racine des images, conservé sans rendu dans le dossier sans suffixe.

| Capture | SHA-256 |
| --- | --- |
| run-0.png | 5BE561A49B05B094BEBE499A1FB6A89F07FBEA33A74293EDD112A64FAF247FDB |
| run-1.png | 4BF6B85925131AE2DF455A1421B0371A712EB68F5F08E4BADF1AE905B45F2663 |
| run-2.png | CC4A7DCCB20477FE247AD3C657C61C9FB25F96C0809647FB55896CE14CB0809D |

Même release conservé SHA-256
`4C4BB888936653294D420A86CDEBE7E69BC2AE0E86E7203B8D5E0EE43BFC25EC` ;
il précède les derniers raccords d'observation/application des profils. Cette
séquence prouve une réponse optique du binaire nommé, pas l'exécution de ces
derniers raccords. Aucun rebuild, changement du shader, publication ou gate fermée.

## Commande indépendante de réfraction — 9 septembre

Implémenté : `WaterQualityRequest::preview_screen_refraction_disabled` supprime
uniquement le parcours réfracté et conserve la transmission approchée, les reflets
et les sources. Le retour suit le profil effectif, sans contourner son fallback.
Deux boutons Studio et un état observé distinct rendent cette commande accessible.
Ce n'est pas une promotion du renderer expérimental aux profils qualifiés.

Vérifié : 17 tests `forge_engine water::quality` PASS ; cinq tests Studio
`water_optics_request` et cinq `effective_quality_status` PASS. Check et Clippy
strict WASM PASS, Trunk et paquet Tauri construits. Warning de test natif sur
deux helpers `capture_readiness` non utilisés dans cette sélection ; avertissement
tiers futur `proc-macro-error2` conservé. Aucun benchmark relancé, shader inchangé.

Essai réel via Computer Use : High 24 pas observé, clic « Transmission approchée
sans rayon », zéro pas observé, puis retour au profil et 24 pas restaurés.
Reflets toujours 48 pas/96 m, kit 20/20 et 36 colliders prêt aux deux exports,
six hashes sources identiques, `projection_matches=true`. Rendu de la scène et
contrôles inspectés ; pas de comparaison optique rapprochée ou de fluidité déduite.
Instance PID 27904 fermée, absence vérifiée. Ne pas rejouer le cycle inchangé.

Artefacts locaux dans `r1-refraction-controls-studio-20260909` :

| Élément | SHA-256 |
|---|---|
| EXE essayé | 7601BC50C0B0AAB33C8363C5B48A66E751160A7BCA3B47E2862050171E7620B1 |
| WASM essayé | 8C2F293C029325C0ACB6755B60378003C7FAF72516C359CAF9C3667629C56FF4 |
| forge-diagnostic-1788967214615.json (sans rayon) | E96D34E3EAAA684D0EE86428D8F3F4DB25A317FC4DD436CB047A85B028DE8234 |
| forge-diagnostic-1788967252936.json (retour High) | 01DABED10DDA2A412D484FF2ECBBAAB2EF2F162B58CA8E93D03E4E79C2F2E92B |

Originaux Downloads conservés. Après essai, seul le libellé générique de teinte
est raccourci pour ne pas prétendre que le candidat profondeur sans reflets suit
les budgets ; les exécutables ci-dessus précèdent cette correction de texte.

## Complément rapproché Tauri — essai terminé le 9 septembre

Répertoire local `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-refracted-water-close-studio-20260909`.
Candidat EXE SHA-256 `2F58C064A977F8706517784FDF8AA81DEC96FDC843F697E48318970B7010920C`.
Exports par les commandes produit, originaux Downloads conservés et copies sur E: :

| Fichier | SHA-256 |
|---|---|
| forge-viewport-1788966092511.png | 4AAC98BDB83440174D53B84B49A50ED0103911A6ADAC6ADE414A0AE70CD8D543 |
| forge-diagnostic-1788966105129.json | CBB4AB239712D8587A7ACB8D82B80222E1B15CA07A8E8B256343D196CA419193 |
| forge-viewport-1788966420114.png | DCA547B568BC00451F85DC7F3B3DDBBC407A5884917E29B3D1C6B792A1576502 |
| forge-diagnostic-1788966430400.json | 2FCBC73D512973BFEFC78C32AE54D1A1FD2608D7A7E0C12AB04E5EE9DA2E3AC1 |

Les JSON donnent caméra `water_close`, temps 0 puis 0,25 s, mêmes six hashes
sources et mêmes budgets réflexion 48/96 m, réfraction 24/3,2 m, projection
concordante. PNG final 1408 × 966 réellement inspecté : fond visible au premier
plan, reflets du pont et des arbres, raccord de berge visible. Rendu encore
stylisé ; ni réalisme final ni absence de défaut temporel prouvés.

Limite décisive : kit 20/20 aux deux états mais colliders 0 (chargement) puis
36 (prêt). Ne pas qualifier cette paire comme isolation temporelle stable.
Le second diagnostic conserve `full_preset_applied=false` ; les exports ne
valident donc pas High. Pas de nouveau benchmark ni de modification de shader.
Instance PID 16980 fermée par Alt-F4, absence vérifiée par Get-Process.
Ne pas répéter cette paire inchangée pour tenter un verdict plus favorable.

## Suite — commandes Studio exercées, qualification visuelle globale ouverte

Reprise du même bundle sans reconstruction, le 9 septembre à 11:25 locale.
Route sélectionnée depuis le menu réel du Studio ; les commandes « Reflets
d'environnement seuls » puis « Suivre les reflets du profil » ont été cliquées.
Trois exports téléchargés par le produit ont été relus et contrôlés :

| Export JSON | Budget observé | SHA-256 |
| --- | --- | --- |
| `forge-diagnostic-1788946033023.json` | 48 pas / 96 m | `EE96055F0AC72BBDB4BF1D54B6C02E22E80EFEF8CDD01A411F719D8873E99C19` |
| `forge-diagnostic-1788946076550.json` | 0 pas / 0 m, environnement | `10060FCD163CD3BCA7A4BA4FCDCC84D029E6498F12FDBED3D04EE9887337FA07` |
| `forge-diagnostic-1788946109954.json` | 48 pas / 96 m restaurés | `C600671D3ABEF59DC0C3EDFE4532963AD8B1EC6A101B12FDB175DCD29EC55341` |

Réflectance 0,360 conservée, `projection_matches` et concordance des uniformes
vraies, observation finale exactement égale à l'initiale. Six hashes de sources
constants, manifestes sélectionné/runtime concordants. L'UI reste à un monde
initialisé ; même horodatage de disponibilité dans les trois exports. Ce sont
des observations CPU et d'interface, pas une mesure du parcours GPU par fragment.

94 pipelines préparés, zéro en attente et zéro échec final. Le compteur conserve
un pic de 18 erreurs transitoires : ne pas décrire le démarrage comme exempt de
toute erreur transitoire. `gate_passed` et `human_acceptance` restent faux.

PNG téléchargé et réellement inspecté : `forge-viewport-1788946173202.png`,
1408 × 966, SHA-256
`9D6FB5F9BABC7E94CCD723A45E3D97BD8B8AF9911DCAFC5426A8C8CAC67B5E66`.
La vallée s'affiche, mais cette vue de départ montre l'eau au loin : elle ne
qualifie ni le contact de berge ni les reflets rapprochés. Ce n'est pas une des
captures Full HD normalisées de la revue artistique. JSON/PNG non atomiques.

Ces quatre fichiers sont archivés dans la racine externe du lot indiquée
ci-dessous, originaux Downloads préservés. Les 14 empreintes de livraison ont
été revérifiées après exécution. Processus 28480 et sept descendants propres à
l'essai arrêtés, absence contrôlée. Aucun autre navigateur ou processus arrêté.
Le manifeste de construction historique reste BUILD_ONLY ; cette section apporte
la preuve interactive distincte, sans transformer sa portée en qualification globale.

Lot d'accès/commandes terminé. Ne pas répéter ce cycle inchangé ; stabilité en
mouvement, coût du candidat complet et acceptation artistique restent requis.

### État de construction et première session interrompue (historique)

Le menu Studio expose maintenant `forge_water=clear-reflections-preview` dans
la vallée gouvernée. Cette route réutilise le milieu clair et active explicitement
le prototype moteur ; le rendu par défaut et les sources sauvegardées ne changent
pas. L'inspecteur publie l'observation CPU `forward_reflection_quality` et rend
accessibles les commandes de réflexion existantes. Ce raccordement n'est pas
une qualification des profils globaux ou du GPU.

Preuves de construction conservées dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-forward-water-20260909/` :
53 tests Studio PASS, Clippy WASM strict PASS, bundle Trunk release et shell
Tauri debug construits. Journaux `tests.log`, `clippy.log`, `trunk.log` et
`candidate/build.log`. Avertissement futur Rust préexistant conservé.

- WASM : `E8BE76136F003CE1941DE83AEA3B014C99718BE514594DB558ED56E29AD980F3`.
- EXE : `B38D80078E7A2C9846145190307A2A7CD6A0C8AEACB7941C5F002733E73FE37F`.
- Contrôle après essai : les 14 empreintes EXE/frontend/kit du manifeste de
  construction concordent toujours avec les fichiers livrés.

La session interactive a été interrompue avant la constitution des exports de
qualification et du cycle repli/restauration. Ne pas assimiler l'ouverture du
menu à une preuve complète des commandes. `candidate/build-proof.json` reste
volontairement `BUILD_ONLY`, `runtime_verified=false`, `gate_qualified=false`.
Le processus 8608, identifié par chemin et heure de création, et ses descendants
ont été arrêtés : huit processus propres à l'essai, aucun restant au contrôle.
Aucune nouvelle capture ou compilation lancée pour cette consolidation.

À reprendre sur ce même bundle tant que ses entrées restent valides : exporter
le diagnostic initial, exercer le repli environnement puis le retour au profil,
contrôler leurs observations et l'absence de reconstruction du monde, conserver
une capture et fermer la session. Ensuite seulement qualifier mouvement et coût
du candidat complet. Ne pas reconstruire ni répéter les paires natives figées
pour compenser cette preuve interactive manquante.

## Suite — budgets de qualité raccordés au prototype

Le prototype prend maintenant les demandes `WaterQualityRequest` existantes,
y compris les demandes effectives et les désactivations manuelles, sans modifier
le document de politique. La voie optique historique sans option de réflexion
reste inchangée. Pour le prototype, la politique livrée demande :

| Eau | Pas linéaires | Portée | Repli |
| --- | ---: | ---: | --- |
| Low | 0 | 0 m | Environnement |
| Medium | 24 | 64 m | Environnement si absence de hit |
| High | 48 | 96 m | Idem |
| Ultra | 64 | 128 m | Idem |

Plafonds de sécurité du prototype : 64 pas / 128 m. Une demande effective sans
portée disponible n'active pas le parcours. Sans politique de qualité installée,
le budget expérimental historique reste 32 / 64 m. Caméra active absente ou
ambiguë : parcours coupé et observation non qualifiée. Désactivation des reflets :
parcours et réflectance à zéro ; désactivation du seul SSR : environnement conservé.

`WaterProjectionStats.forward_reflection_quality` distingue demande, valeurs
de matériau et concordance CPU. Le champ `quality` historique reste absent pour
ce candidat : aucune fausse qualification globale ni confusion avec les pas du
composant SSR différé. Le contrôle général des uniformes tient compte du réglage
appliqué sans confondre changement de budget et modification des sources.

Un test ECS couvre Low/Medium/Ultra/High, désactivation/restauration et absence de
caméra active : budgets, handle/nombre de matériaux, hashes eau/terrain et absence
de SSR caméra contrôlés. 664 tests workspace PASS / un ignoré avant extraction
du calcul d'extension attendue et suppression d'une lecture de matériau dupliquée.
Première compilation refusée pour mutabilité manquante, corrigée ; puis deux
refus Clippy de longueur du diagnostic conservés, sans désactivation du lint.
Après extraction : Clippy strict moteur/Runtime all-targets, build natif, check
Studio WASM, fmt et diff-check PASS. Pas de seconde suite workspace inchangée ;
les captures finales exercent la projection extraite et affichent l'observation
main-world complète. Logs dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forward-water-quality-20260909/`.

Deux captures natives Low/High réellement inspectées, même EXE, caméra, temps
figé et kit courant. Seul le domaine eau change de qualité ; les six autres
domaines restent High. Le reflet du pont est absent en Low et visible en High,
le fond reste visible dans les deux. Cela prouve la différence de chemin rendue,
pas le nombre exact de pas réellement parcourus par chaque fragment ni le coût.
Medium/Ultra : preuve ECS seulement dans ce lot. Aucun benchmark déduit des FPS
ponctuels affichés. Avertissement wgpu sur la sortie vertex sans `@invariant`
pour une passe Equal conservé dans les logs ; aucun événement ERROR.

Captures aux frames 600, sorties 0 à 900, pins du kit/monde/milieu inchangés,
PIDs 8908 et 6056 fermés, absence contrôlée. `Capture.ps1 -Tier low|high`.

- EXE : `431AA74043CB27D0C6312C0295D917FC9DB4F7253301C89B6A1C821B6B693534`.
- Low : `4025F48B68E78187171D4975C669BA921D2CD966567A2C294E1F99E2AFCE7C42`.
- High : `67A7726C2B69CFEF6BF4CF8EADA2640C7CA52FFC9BA5E0D5C620C2D43FF94E56`.

Cette suite raccorde les budgets de réflexion seulement. Réfraction, profils
globaux, coût, mouvement et Studio restent non qualifiés. Les captures historiques
ci-dessous utilisaient 32 / 64 m : ne pas les présenter comme les nouvelles valeurs.

## Implémenté

`--water-reflection-preview` exige le candidat optique WorldSketch, une sortie
bornée et l'absence d'isolation optique. Sans ce drapeau, le rendu de référence
reste sélectionné. Aucun document de carte, kit ou matériau source modifié.

L'adapter importe les fonctions du module `bevy_pbr::raymarch` de Bevy 0.19.1 :
32 pas linéaires au maximum, quatre bissections, sécante, portée de 64 m et
tolérance de profondeur de 0,2 m. La voie sans `USE_DEPTH_SAMPLERS` utilise les
lectures de profondeur Bevy existantes ; aucun binding/sampler/texture/passe ou
second rendu de forêt ajouté par Forge. Le module Bevy conserve sa licence et
son attribution Tomasz Stachowiak dans la dépendance ; aucun code de parcours
ou asset externe copié dans Forge.

La couleur provient de `view_transmission_texture`, copie exposée de la scène
avant la passe transmissive. Le reflet trouvé est pondéré par Fresnel sans
réappliquer l'exposition. L'occlusion spéculaire passée à l'éclairage PBR diminue
la contribution d'environnement selon la confiance du reflet : pas une simple
addition de deux réflexions. Transmission et éclairage solaire restent actifs.

Repli environnement sans hit, aux bords de l'image et pour les surfaces trop
rugueuses. Les sous-viewports sont explicitement exclus pour l'instant : le
parcours Bevy utilise les UV de toute la cible. Le budget est un budget de
prototype fixe, **pas** l'application de Low/Medium/High/Ultra. Le statut expose
`experimental_forward_opaque_scene_reflections_not_qualified` ; le SSR différé
de caméra reste désactivé, et son compteur ne compte pas ce parcours de fragment.

## Réellement observé

Deux lancements du même EXE, même caméra `water_close`, temps figé, milieu RGB,
kit courant de 1662 arbres, résolution 1920×1080. Captures aux frames 600,
arrêts bornés à 900. Les deux PNG ont été inspectés : le candidat montre le
reflet des poteaux/parapet du pont là où le témoin ne montre que l'eau et son
ombre. Fond toujours visible. Les défauts de bord/continuité du reflet ne sont
pas qualifiés par cette seule vue ; aucune acceptation artistique déduite.

Entrées épinglées et vérifiées inchangées par le script. Sorties 0, aucun événement
ERROR, captures vérifiées. PIDs 30960 et 32792 fermés et absence contrôlée.
Les métriques glissantes des captures ne sont pas un benchmark : une compilation
CPU coexistait avec une partie de l'essai. Aucun gain/coût chiffré revendiqué.

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forward-water-reflection-20260909/`.
`Capture.ps1 -Reflection` et sans drapeau décrivent les deux lancements.

- EXE : `955F494EA6C860A1DD2416727325BEEF00B1BFEAC63089D72B1BC84169D8396F`.
- Reflet : `547740A8D455A5866706307420A73D6EAC37A8809964212D3EE46C17E8C912BC`.
- Témoin : `FFB78659B8E04F46DA6ED1BCAAD653941225A83971F3CA8878B7324CA9D723D3`.

Le message de démarrage a ensuite été corrigé pour ne plus annoncer « environnement
seulement » quand ce prototype est actif ; aucun shader ou paramètre visuel changé
après les captures. Trois tests CLI ciblés PASS. Contrôles élargis terminés :
663 tests workspace PASS / un ignoré, Clippy strict moteur/Runtime all-targets
PASS, check Studio WASM PASS, fmt et diff-check PASS. Logs `tests.log`,
`clippy.log`, `wasm.log`, commande composée sortie 0. Avertissement futur Rust
préexistant `proc-macro-error2 2.0.1`, pas un warning projet ni une validation
de cette dépendance pour une future toolchain. Aucun build/exécution Studio.

Shader de parcours Forge :
`BA0EDD7EEC9FDD6E7D739D54BD2BB1A2A1AF6B5FDA9B693215BB25026464363D`.
Shader de surface :
`BE179C40EBF82EF1EAF4427116C126235C7A25DDCED1CB54AB0A762586CFDDD8`.

## Encore requis

Stabilité en déplacement et hors écran, sélection des budgets, coût mesuré sans
compilation concurrente, intégration/exécution Studio WebGPU et revue artistique.
Les buffers utilisés ne constituent pas une représentation complète des objets
hors écran ni du feuillage BLEND rendu ultérieurement. La réfraction est toujours
l'approximation bornée du candidat précédent. Pas de revendication de réflexion
complète de forêt ou de passage R1-022/023.
# Complément du 9 septembre — rayon réfracté raccordé au candidat

### Parcours intégré enregistré avec le milieu optique

`Capture-BridgeVideo.py --water-medium` épingle désormais le JSON de milieu et
active explicitement le candidat forward. Première tentative refusée en phase
arguments, conservée dans `r1-refracted-ray-motion-20260909` : aucun rendu réussi
déduit. Les validateurs autorisent maintenant le pont **avec enregistrement**,
borné par son protocole existant ; pas le pont libre, ni une combinaison avec
isolation. Un `--exit-after-frames` indépendant reste incompatible avec le pont.
95 tests runtime + un test d'intégration PASS ; quatre tests Python PASS.

EXE `r1-refracted-ray-recorder-build-20260909/runtime.exe` :
`93527A05E2B46D3289D96F5237702D66E6F4D88EA21EF3766C3977E115323973`.
Même shader que le candidat lissé ; changement de lanceur seulement.
Sortie `r1-refracted-ray-motion-20260909-b` : 271 PNG Full HD, traversée et
vérifications automatiques PASS, vidéo 9,033 s à 30 images/s imposées, processus
terminés (session 93269 sortie 0, aucun runtime de ce chemin actif).
Vidéo SHA-256 `3301770eaf24fdb58880fe1a59bfcbbc65e6f9867fe485a008b828289403307b` ;
journal `b1e2e3b24baa78f11b319d66b76a26b0a4ee09dc262c95f79b7b70d88c400219`.

Images 100/162/230 inspectées : parcours et scène lisibles, mais l'eau devient
latérale puis presque hors champ. **Ce cadrage ne qualifie pas la stabilité de
la réfraction rapprochée** et ne justifie pas de répéter ce parcours inchangé.
La vidéo est conservée pour la revue du candidat intégré, pas comme benchmark
ou acceptation humaine. La preuve temporelle d'eau devra garder le fond visible.

### Coût du candidat corrigé — ABBA dev terminé

`r1-refraction-dev-abba-20260909` : A ombres B `3f0abde6…`, B réfraction
lissée `AD9007CC…`, même kit atlas recadré, milieu, profils et parcours 1800 ticks.
Quatre processus séquentiels, pas de build simultané. Pas qualification release
ni matériel cible ; le parcours général n'est pas un stress-test plein écran d'eau.

| Passage | FPS trajet | FPS forêt | p95 forêt ms | 1 % low forêt FPS |
| --- | ---: | ---: | ---: | ---: |
| A | 67,27 | 50,51 | 22,74 | 42,54 |
| B | 66,69 | 49,72 | 23,06 | 42,05 |
| B | 66,69 | 49,81 | 22,91 | 42,41 |
| A | 67,25 | 50,69 | 22,61 | 43,08 |

Coût observé ~0,85 % de FPS trajet et ~1,65 % forêt, p95 forêt +0,31 ms.
Ni gratuit ni gain ; seuil forêt 60 FPS toujours non acquis. Ne pas répéter
cette série inchangée ni transformer ces chiffres dev en qualification cible.
1503 ticks communs, écart position maximal 7,53 mm, caméra 1,88 mm ; pins communs
identiques. PIDs 30184/31520/12412/30392 terminés et absents.

Comparateur initial refusé honnêtement : le candidat ajoute deux observations
de budgets de réfraction absentes de l'ancien EXE. Option explicite
`--refraction-candidate` ajoutée : impose ces champs uniquement côté candidat,
leur concordance avec la demande et `projection_matches=true`, puis compare
tous les autres champs à l'identique. Le mode historique reste strict.
Sept contrôles PASS : égalité autorisée, mode strict, mauvais pas/profondeur,
projection non conforme, champs inattendus en baseline, source divergente.
Les rapports bruts ne sont pas modifiés. Analyse finale PASS ; SHA-256
`comparison.json` : `CAD1475FB3FD9526BAF650211630E3E722DFF50E48DDCABECB71111EC83E8CCC`.

### Intégration Studio du candidat corrigé

Frontend Trunk puis paquet Tauri construits dans
`r1-refracted-ray-studio-candidate-20260909`, kit atlas recadré courant.
WASM `F1EC8F2E335D1CF6522698B848912A9120EE52B812459A0DBFBF6C3923E4BB52`,
EXE `14BC5956A211348BA9F07A29CD101406B432EC4FF7C141342A45F2BE35C033AF`.
Fenêtre réelle ouverte avec Computer Use, scène affichée, exports UI récupérés
et copiés dans le candidat (originaux Downloads préservés) :

- `forge-viewport-1788965004974.png` :
  `195839997D7A35D904A6C4FFC2305F495C8FB293B6F67F30401BB2E85478A813` ;
- `forge-diagnostic-1788965036980.json` :
  `E4818503D8BD4A8839E1002632D2B33404E9A16210B94A5FB929400C6F3E3777`.

Diagnostic : 99 pipelines prêts, zéro pending/failed, huit observations stables,
préparation 32,66 s ; réfraction 24 pas/3,2 m et réflexion 48/96 m projetées,
uniformes concordants. Kit 20/20, 36 colliders, 1662 arbres, un monde/une caméra.
Le pic de 18 attentes shader transitoires n'est pas un échec final. Le PNG
1408×966 montre la vue de départ : intégration réelle acquise, pas qualification
rapprochée, mouvement, coût ou profil High complet. Exports non atomiques.
Warning futur `proc-macro-error2` conservé dans le build. Aucun gain FPS déduit.
Instance fermée par Alt+F4, PID 28080 ensuite absent. Aucun serveur lancé.

### Comparaison rapprochée et correction du repli

Trois captures natives même caméra `water_close`, temps figé, High, même kit
historique linear-color-repair (pas le dernier atlas recadré), entrées épinglées
inchangées. `r1-refracted-ray-close-20260909/Capture.ps1` conserve les commandes.
Avant : EXE ombres B `3f0abde6…`. Premier rayon : EXE `B2E2825F…` ci-dessous.
Inspection : larges frontières nettes entre intersections et approximation,
donc premier rendu rapproché **refusé**, malgré compilation/smoke réussis.

Correctif retenu pour la suite : confiance progressive aux limites de portée,
d'écran, d'écart au rayon et de l'interface. Transmission Bevy conservée à 1
pour ne pas créer un lobe diffus ; seule sa couleur transmise est réduite par
le complément de confiance. La radiance réfractée est pondérée par la confiance,
sans additionner deux transmissions pleines. Trajet optique interpolé également.

Build natif dev PASS en 1 min 33 s. Troisième PNG inspecté : grandes frontières
du premier candidat fortement atténuées, fond et reflet du pont présents.
Ce constat ne qualifie ni mouvement, ni tous angles, ni coût, ni Studio.
Aucun benchmark déduit de l'overlay et aucune acceptation artistique ajoutée.

- EXE corrigé `r1-refracted-ray-smooth-build-20260909/runtime.exe` :
  `AD9007CCBBE8D62891ED5AB7D20156A5636721965887F03606C7791901F8EEF2`.
- `before.png` : `7F7E992EAB1C85382B30FE891601CACAA9A31B6EA8887638D755B610A011741F`.
- `after.png` refusé : `A7F95CA66A873283359F302C916646B961CFA114D93687D42CFF036E95F4204E`.
- `smoothed.png` : `1C9D540A2407E12D5B8EDF42A8E07D090C6282E168D9E39598A9188EF6911E13`.

Trois sorties 0, captures vérifiées, pas d'ERROR runtime, PIDs 13356/21184/10636
terminés et absents. Les 41 tests ci-dessous précèdent ce correctif WGSL ;
la preuve nouvelle est le build et l'exécution réelle du shader, pas un replay
des tests CPU. Prochaine qualification sur ce candidat corrigé, pas répétition
du candidat à frontières franches.

Le candidat forward avec reflets appelle maintenant ce rayon : High 24 pas /
3,2 m, Ultra 40 / 4 m ; Low/Medium, aperçu de teinte, repli effectif ou caméra
ambiguë désactivent le parcours. Aucun changement du chemin opaque ordinaire.
Les observations CPU exportent les pas et la longueur maximale du trajet ;
ces bornes ne sont pas une mesure de profondeur verticale ni une preuve GPU.
Sur intersection valide, la radiance réfractée remplace la transmission Bevy,
avec extinction sur le trajet reconstruit et Fresnel. Sur miss, approximation
Bevy conservée. Le contact de berge conserve la profondeur non déviée.

41 tests eau PASS (cycle quatre profils, indépendance réflexion/réfraction,
repli effectif, sources/handles préservés), build natif dev PASS en 1 min 34 s.
Runtime épinglé `r1-refracted-ray-build-20260909/runtime.exe`, SHA-256
`B2E2825F8303DFFDC879EC7FDF481D1DAA5E6093829457C37D46B2ED3A598769`.
Essai `r1-refracted-ray-capture-20260909` : sortie 0, arrêt réussi et aucun ERROR
dans le journal ; sources épinglées inchangées. PNG Full HD inspecté : scène
affichée, eau visible au loin. SHA-256
`A4A5DB92611D0850FBCBE922093FE76F0C62AD70CF389EDF3526EA5B12457592`.
PID 4480 terminé et absence vérifiée. Warning Bevy/wgpu concernant position
sans invariant dans une passe Equal conservé, pas de qualification sans réserve.

Ce smoke confirme le chargement du shader intégré, pas le taux d'intersections
ou la qualité rapprochée. Il reste la preuve optique rapprochée avant/après,
la stabilité en mouvement, le coût comparable et le chemin Studio/WebGPU.
Les FPS de l'overlay ne constituent pas un benchmark. Gate 1 reste ouverte.

## Préparation précédente (désormais raccordée ci-dessus)

`water_reflection_preview.wgsl` contient désormais `water_scene_refraction`,
un appel borné au marcheur Bevy déjà utilisé par les reflets. Réfraction air/eau
selon l'IOR, 2–64 pas et quatre raffinements ; distance fournie par l'appelant.
Le résultat contient radiance et longueur du trajet réel vers le texel de
profondeur reconstruit. Rejets : vue sous l'interface, sous-viewport, hors écran,
fond absent, géométrie au-dessus de l'interface locale, trajet incohérent ou
hors budget, dimensions couleur/profondeur divergentes et couleur absente.

La fonction n'est **pas encore appelée par le shader de surface**. Le raccord
aux budgets High/Ultra et le remplacement de la contribution transmise Bevy,
sans double composition, restent à faire. En cas de miss, l'appelant devra
conserver explicitement l'approximation existante. Aucun profil ou capability
n'est promu par cette préparation ; aucun changement visible revendiqué.

Validation ciblée : `cargo test -p forge_engine --lib --features native,qualification
refracted_lookup_wgsl --locked -j 2`, sortie 0, un test PASS. Naga analyse le
texte réel de la fonction avec une fixture explicite des interfaces Bevy ;
cela prouve syntaxe/types, **pas** l'intersection GPU ni le linkage complet Bevy.
Compilation 1 min 31 s, processus terminé. Pas de navigateur lancé.
La preuve GPU numérique, l'intégration, les captures et le coût restent dus.
