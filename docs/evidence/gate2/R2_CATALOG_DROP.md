# R2-027 — Glisser-déposer du catalogue

## Implémenté le 14 septembre 2026, parcours positif acquis

Les résultats du catalogue sont des sources de glissement souris géré par le
Studio (mousedown/move/up, seuil de 6 pixels avant de distinguer un drag du clic).
Le transfert HTML5 initial a été remplacé après l'essai décrit ci-dessous. Déposer dans
le canvas prépare un placement, sans Apply ni Save implicite. Le bouton de
placement par clic reste disponible pour le clavier et les réglages avancés.
Le dépôt utilise explicitement échelle 1, rotation 0, pivot sur la surface,
sans grille ; ces valeurs sont indiquées dans le catalogue avant le geste.

`authoring_assets/catalog_drop.rs` conserve une intention temporaire interne
avec la référence du catalogue et le hash de tête. Il n'y a plus de transfert
DataTransfer ni de marqueur à interpréter. Un texte/fichier externe, un dépôt sans intention locale,
une zone fermée ou le mode playtest ne produisent pas de placement.
Le canvas et l'espace auteur empêchent la navigation navigateur par dépôt.

Le point est obtenu par le picking terrain existant. Après contrôle SHA/GLB,
le ticket, la sélection et la tête sont revalidés ; le préparateur partagé
`placement_controls/insertion.rs` produit la même commande Insert que le clic.
Identités, transforms, validation domaine, aperçu, Apply, inverse et sauvegarde
restent ceux du parcours existant. Pas de mutation directe de l'ECS ou du disque.
Relâchement hors canvas, sortie de l'espace auteur, Échap, changement de catalogue/session et retrait
du panneau retirent l'intention. Un résultat asynchrone invalidé n'est pas appliqué.

Sur Windows, `dragDropEnabled=false` laisse les événements HTML5 au frontend,
conformément au commentaire du `tauri-utils-2.9.3/src/config.rs` local (1945).
Ce réglage de l'implémentation initiale reste configuré dans le Studio et pour les candidats `-Authoring` ; les candidats de
preuve non auteur conservent l'interception native. Aucun accès fichier ajouté,
aucune dépendance de package nouvelle : features DragEvent/DataTransfer de web-sys.

## Vérifié

- `cargo fmt --all` exécuté ; Clippy Studio WASM strict PASS, 5,65 s au passage
  final après correction d'un import inutilisé et d'une closure redondante.
- Le premier contrôle a recompilé des dépendances après activation des features
  web-sys ; ce n'était pas une campagne de benchmarks ou un essai graphique.
- Relecture du chemin commun clic/dépôt : mêmes clés déterministes et commande
  `prepare_placement`, mêmes valeurs par défaut pour un clic à réglages neutres.

## Encore à qualifier

### Reprise après abandon — candidat D2A2866F

`E:/OpenRPG_Forge/.gate2-evidence/catalog-cancel-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `D2A2866F5FEC71A68658C0835A92F691DBB338BA61D321EA4723667457D74F19`,
WASM `8817404C02426562F0FFC5290792CA3EA7A5442840EB39BB8E80A2EB6EAAA6EB`.
Trunk PASS (Rust 24,46 s), Tauri PASS. Essai de la commande PreviewZone partagée,
sur écran secondaire, sans Apply/Save : nom « Nouvelle zone - aperçu à abandonner »
prévisualisé ; Apply actif ; bouton Abandonner retire la proposition et désactive
Apply ; nouvelle prévisualisation réactive Apply avec une nouvelle proposition.
La reprise après annulation est acquise pour ce parcours. Aucun changement du nom
canonique revendiqué, uniquement un champ temporaire et une proposition.

Échap envoyé alors que l'aperçu était prêt n'a pas retiré la proposition :
échec de cet essai clavier, réception DOM/focus non établie. Ne pas le compter
comme annulation réussie. Le cas d'une réponse native réellement retardée et
l'activation clavier du catalogue après drag n'ont pas été reproduits ici.

Inspection du routage ensuite : `winit-0.30.13/src/platform_impl/web/web_sys/canvas.rs`
301–303 appelle `prevent_default` sur keydown lorsque sa prévention est activée.
`authoring_shortcuts::handle` refuse un événement déjà prévenu : le routage en
bubbling peut donc perdre les raccourcis provenant du canvas. Le handler du
workspace passe à `on:keydown:capture`, syntaxe confirmée dans le macro Leptos
0.8.17 local. Les exclusions de champs éditables/composition, modificateurs et
répétitions restent inchangées ; les touches non reconnues atteignent le canvas.
Format check et Clippy WASM strict PASS (5,91 s). Aucun nouveau candidat lancé.
Cette correction ne prouve pas la cause d'Échap sur un bouton hors canvas,
ni sa réussite : ne pas convertir l'échec D2A2866F en PASS sans preuve.

L'accès UIA set_value a échoué ; saisie normale après observation du curseur dans
le champ Nom. Pas de boucle de répétition des anciens tests.

Après fermeture : PID 11020 et descendants 2172/30756/32704/33072/31720/13812/2748
absents. Archive isolée inchangée : 1E48B2F6D8E61118D5D8CD1677ADC4C9848419FE3FEE7105963BEB20A62EB500.
Gate 2 reste ouverte ; ce contrôle ne qualifie pas tous les cas R2-027.

### Annulation et activation clavier — complément de code

Après la preuve positive 78005EAD, inspection de deux cas non couverts :
le drapeau de suppression du clic pouvait survivre jusqu'à une activation
clavier (pas de mousedown pour le remettre à zéro) ; le bouton Abandonner
retirait l'aperçu visible sans invalider une réponse Preview native en vol.

Corrections : la suppression ne concerne plus `MouseEvent.detail == 0`, utilisé
par l'activation clavier ; Échap et Abandonner partagent le même callback de
retrait du geste/picking, ticket GLB, proposition Apply et dessin d'aperçu.
Un drapeau d'annulation persiste jusqu'à la prochaine requête acceptée ; la
sérialisation par `busy` empêche qu'une nouvelle requête native le réinitialise
pendant la réponse précédente. Une réponse abandonnée reste observée pour son
état canonique, mais ne passe plus dans `reviewed_preview`. Aucun Apply/Undo
implicite ; les mutations déjà envoyées ne sont pas présentées comme annulées.

Format exécuté, Clippy Studio WASM strict PASS (5,61 s). Deux contrôles
intermédiaires ont signalé la taille du composant ; extraction de helpers de
préparation/annulation, sans désactivation de lint. Pas de nouveau build Tauri,
pas de lancement graphique ni écriture de carte. Ces cas restent à vérifier
dans le prochain candidat regroupé ; 78005EAD ne contient pas ce complément.

### Parcours positif corrigé acquis

Candidat `E:/OpenRPG_Forge/.gate2-evidence/catalog-radius-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `78005EADC2EF8AF1EDD988BE8ACA0C7304BE8D0FAD33A27DCE852B49F1520105`,
WASM `B1F9EEDFDB93406F4A85FC108D4E466D39E34DD17204D15EE1DA79E2E3E8B205`.
Trunk PASS (Rust 25,74 s), Tauri PASS, fenêtre secondaire 1600×900.
Un geste arche du catalogue vers terrain a produit l'aperçu 71→72, 1 ajout,
71 inchangés. Apply : 72 placements/78 primitives Rapier ; Undo : 71/75 ;
Redo : 72/78 ; Save : session synchronisée. Puis fermeture de la zone,
réouverture et arche visible à l'emplacement du dépôt, session synchronisée.
Une tentative d'ouvrir avant fermeture a été refusée explicitement, sans mutation.

La copie isolée `catalog-drop-authoring-20260914` contient 23 commandes,
archive SHA-256 `1E48B2F6D8E61118D5D8CD1677ADC4C9848419FE3FEE7105963BEB20A62EB500`,
tête `2ba43f71e91680e0b80408c73de7f6c59c2ba3b41e245e687ce534b66370a9ef`.
Comparaison des derniers ensembles persistés : les 71 objets d'origine sont
intégralement identiques (ID, nom, pin, transform, verrou). Seul ajout :
`dc7fdb5e-8458-52ae-9eb5-e0e5d20fd273`, arche pin 41552424, position
(-98,7672348 ; 3 ; 115,1952438), rotation identité, échelle 1, non verrouillée.
Original `authoring-path-tools-ui-20260913` toujours F432409E, non modifié.
PID 21668 et descendants 2364/12252/19204/5904/24136/31512/7124 fermés et
vérifiés absents. Pas de benchmarks ni campagne Gate 1 rejoués.

Acquis : parcours positif du dépôt et persistance. Restent non qualifiés dans
ce candidat : Échap/sortie pendant geste, point hors terrain, changement de tête
pendant contrôle GLB, fichiers externes et clic simple après un drag. Ne pas
confondre ce progrès avec la clôture complète de R2-027 ou Gate 2.

### Geste souris reçu, défaut de rayon corrigé

Candidat `catalog-mouse-studio-20260914/forge-studio-candidate.exe`, SHA-256
`64B87823E41B114F05AE10C3B02CA4301E48C7AEDC205DEC04385687AA0E262D`,
WASM `AFFB00BAAC55A26C2AE67292FE3931035A5971F079A793E3A0171451FEAF3933` :
Trunk/Tauri PASS. Sur écran secondaire, catalogue chargé puis un geste arche
vers terrain retourne « Tracé annulé : pointeur hors terrain ». Le nouveau
routage atteint donc le picker, contrairement au transfert HTML5 précédent.
Ni Apply ni Save. PID 15648 et descendants 8656/31028/21364/6160/27852/27940/7480
fermés et vérifiés absents.

Inspection : `pick_at` armait un rayon de 1 m ; `update_terrain_brush_cursor`
refuse tout rayon hors 2..=128 m avant le raycast. Correction à 2 m, sans
changement de coordonnées monde ni de règles de placement. Format check et
Clippy WASM strict PASS (5,88 s). Le rejet est expliqué par ce contrat ; le
parcours corrigé doit encore être observé, il n'est pas déclaré réussi ici.

### Événement manquant isolé, geste repris dans le Studio

Candidat 67BFB82F construit (Trunk Rust 24,20 s/Tauri PASS), même fenêtre
secondaire et copie F432409E. Chemin
`E:/OpenRPG_Forge/.gate2-evidence/catalog-drop-point-studio-20260914/forge-studio-candidate.exe`,
SHA-256 `67BFB82F592EC031DD9AEB03A2169F89BD3E40A6631D7EF0131E88ACA060C89F`,
WASM `F67E3B5A845CD3A2FF60681937F8581DEB9F1109397D1A258D0E45E8E7468ABB`.
Geste unique : statut terminal « Glissement terminé sans dépôt reçu sur le
terrain. Aucun placement préparé. » Aucun Apply/Save. Ce résultat situe l'échec
avant le picking et le contrôle GLB ; il ne prouve pas si le responsable est le
transfert Windows, le dispatch DOM ou l'automatisation. Ne pas réessayer le
même transfert pour qualifier le calcul de placement. Candidat et descendants
19488/10316/33552/14496/15824/9096/4244/11848 fermés et absents, archive F432409E.

Le code utilise ensuite un geste souris interne plutôt que DataTransfer :
clic gauche catalogue, déplacement minimal, relâchement sur canvas. Clic simple
et sélection au clavier conservés ; pas de drag natif, de seconde commande ou
de chargement de fichier externe. Seuil calculé en coordonnées client, cible
canvas et dimensions vérifiées, point transmis au picking existant. Sortie de
l'espace auteur et Échap annulent l'intention. La validation asynchrone reste
liée au hash/ticket et sélection. Clippy Studio WASM PASS (5,79 s), après
correction de visibilité Rust du point d'annulation. Ce nouveau geste n'est pas
encore reconstruit/essayé : aucune réussite graphique revendiquée.

### Fenêtre secondaire et premier geste réel

Le packaging accepte maintenant `WindowWidth/WindowHeight` (défauts 1920×1080
conservés, minimum 1100×700). Tests d'entrée du packaging PASS, sans build dans
ces fixtures. Reprise du WASM FC0F38F5 sans recompilation Web, candidat Tauri :
`E:/OpenRPG_Forge/.gate2-evidence/catalog-drop-secondary-20260914/forge-studio-candidate.exe`,
SHA-256 `67D52876B2A2846E588AA67619E96674ABF56B89A24CC5B30B0C981BF43DB09D`.
Configuration x=-1840/y=90, 1600×900 ; capture réelle x=-1833/y=90,
1602×932 avec bordure : entièrement sur écran gauche. Pas d'agrandissement.

Archive F432409E ouverte, catalogue à sept références rendu. Deux glissements
de l'arche vers le terrain : début détecté et fiche sélectionnée, mais aucun
aperçu ni bouton Apply actif après attente. Le second utilise la référence déjà
sélectionnée ; même résultat. Pas de verdict de dépôt réussi ni de cause unique.
Ni Apply ni Save. Archive inchangée, PID 30884 et descendants
23964/31764/15824/18348/27640/5928/18320 tous absents après fermeture.

Correction de robustesse ensuite au code : le dépôt extrait explicitement les
coordonnées du canvas cible et les remet via `authoring_brush_pointer::pick_at`
au même picking terrain (point borné, pas de point monde inventé). Plus de
silence en cas de cible/dimensions invalides. Fin sans dépôt, dépôt reçu et
contrôle GLB ont des messages distincts. Clippy Studio WASM PASS (5,94 s).
Ce changement n'est pas encore dans 67D52876 ; cause précise du premier échec
et réussite du geste restent à établir. Relecture des features : la délégation
Tachys n'est pas activée, donc son TODO currentTarget ne prouve pas la cause.

### Candidat réel du 14 septembre

Trunk PASS (compilation Rust 5 min 03 s après nouvelles features web-sys),
packaging Tauri PASS. Candidat réutilisable, ne pas refaire cette compilation
sans changement pertinent :

- `E:/OpenRPG_Forge/.gate2-evidence/catalog-drop-studio-20260914/forge-studio-candidate.exe`
  SHA-256 `4B9C121102E866823A363C5000439F7C5AB5CE5E4B5B3719B0478728E5B2045F`.
- WASM `FC0F38F58056CAE075F45D7F6E3197DA1F737B9ADCCF15F5AE1A9F7BA5766F96`.
- Copie isolée `E:/OpenRPG_Forge/.gate2-evidence/catalog-drop-authoring-20260914`,
  archive initiale F432409E, identique à l'original de `authoring-path-tools-ui-20260913`.

Lancement PID 6648 : ouverture de mon-projet/zone-1 et affichage du terrain,
rangée de murs et arches acquis. Aucun dépôt ni Apply/Save réalisé. Le candidat
configuré x=-1920/y=0 a été observé restauré à x=33/y=26 ; agrandissement sur
l'écran principal 2560×1392, contrairement au placement secondaire souhaité.
Cause non établie : ne pas déclarer les coordonnées de configuration comme
preuve de placement réel. Le contrôle natif refuse un déplacement de titre
dont l'arrivée est hors des bornes de la fenêtre. Essai fermé pour libérer
l'écran principal ; nouvelle qualification du geste reste due.
Après fermeture : candidat, conhost et parent WebView absents, aucun descendant
direct restant de ces parents ni processus candidat/WebView portant le chemin
du candidat. Archive isolée et originale toujours F432409E, hashes relus.

Retour UIA parfois décalé ; une capture supplémentaire 16×16 de l'icône peut
être retournée avant la capture de fenêtre. Lors d'une action par coordonnées,
choisir explicitement le screenshotId de la grande capture observée, sans
supposer que screenshots[0] est la fenêtre principale.

Le parcours positif réel du candidat corrigé est acquis ci-dessus ; les cas
négatifs restent ouverts. La compilation seule n'est pas une preuve du geste
Windows. R2-027 et Gate 2 restent ouverts.
CI distante : **NOT RUN — infrastructure billing**. Aucune publication Git.

## Qualification locale composite — 22 septembre 2026

Le parcours positif et persistant reste celui du candidat
`78005EADC2EF8AF1EDD988BE8ACA0C7304BE8D0FAD33A27DCE852B49F1520105` :
Preview 71→72, Apply, Undo/Redo, Save, fermeture/réouverture et nouvel objet
visible, avec les 71 placements antérieurs inchangés.

Le candidat v6
`7CE5EE6706CD255247176FD23F927FCE45B0C32DBB6CC641C73B911D4DA69D08`
complète les cas restés ouverts par une preuve Tauri/WebView2-CDP automatisée :

- mouvement de 5 px, relâchement hors canvas et Échap pendant le geste : aucune
  PreviewZone, aucun Apply/Save, diff masqué et placements acceptés inchangés ;
- geste positif : une seule PreviewZone, diff exact 72→73 avec un ajout non
  appliqué, Apply actif et aucune mutation acceptée ou sauvegardée ;
- Échap après Preview : diff retiré, Apply désactivé, placements acceptés et
  archive inchangés.

L'archive v6 conserve exactement son SHA-256
`83E41F31FAD3EAD12E44F172C26A331C7B015AF203145975207A0D7635810D58`,
ses octets et son mtime. Le processus est fermé gracieusement, le listener CDP
disparaît et l'audit `window.fetch` est restauré. Ledger et captures :
`artifacts/forge-runtime/gate2-resume-20260919/r2-027-cdp-v6-20260922-evidence-retry2/`.

Verdict : **PASS local composite R2-027**. Apply/Save/Close/Open n'ont pas été
rejoués sur v6 ; la preuve positive persistante et les négatifs actuels sont
donc issus de deux candidats distincts. Il ne s'agit pas d'une revue humaine
ni d'un PASS formel de Gate 2 : `gate_qualified=false`. CI distante :
**NOT RUN — infrastructure billing**. Aucun commit, push ou publication.

Reproductibilité du harness : son décodage JSON adapte désormais les paramètres
réellement disponibles de `ConvertFrom-Json`. `-SelfTest` PASS sous PowerShell
7.6.5 et Windows PowerShell 5.1 ; SHA-256 du script
`7822B6E5D1E6BBA15A3B06CB665A918382E1B37FCFF17F9631A8090BC217B88F`.
