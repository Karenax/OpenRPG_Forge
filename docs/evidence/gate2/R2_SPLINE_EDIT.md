# R2-021 — préparation des retouches de tracés

## Correctif du brouillon — 15 septembre 2026

Le PathInspector annule l'ancienne proposition et le geste armé lorsque le nom,
la largeur, l'accotement ou la liste de points changent. Cela couvre saisie,
insertion/retrait/réordre, placement et retracé. Le premier montage ne déclenche
pas d'annulation ; les signaux de session/statut ne sont pas des dépendances de
cet effet, pour éviter une boucle lors de la restauration de l'aperçu accepté.
Le brouillon reste conservé et exige un nouvel examen avant application.

Clippy Studio Web/WASM strict PASS (7,36 s), formatage PASS après ajout d'une
annotation de type nécessaire à l'effet. Aucun test graphique relancé : parcours
Examiner → modifier → nouvel examen encore dû. Code postérieur au candidat
42794BEB, aucune qualification supplémentaire de R2-021 ni publication.

Protection mutualisée ensuite dans `authoring_draft_scope::invalidate_on_change` :
chemins, rivières et bâtiments l'utilisent. Rivières : nom, coupe, accotement,
transition et tous les champs des points. Bâtiments : origine de création ;
nom/transformation/dimensions/porte de l'inspecteur ; nom/origine de duplication.
Premier montage sans annulation, restauration hors dépendances réactives.
Clippy Web/WASM strict PASS (7,31 s), formatage effectué. Pas de nouveau binaire
ni preuve GUI : modifier après examen et vérifier l'impossibilité d'appliquer
l'ancienne proposition reste un parcours regroupé à exécuter.

Replay C86B50ED : nouveau chemin (-5,0)→(5,0), largeur 3 m, accotement 1 m.
Examiner produit la proposition et l'aperçu cyan ; flèche de largeur vers 3,1 m
retire le cyan, efface la proposition et désactive Apply, champ 3,1 conservé.
Capture `E:/OpenRPG_Forge/.gate2-evidence/draft-species-ui-20260915/path-invalidated.jpg`.
L'écriture UIA directe a été refusée par le helper ; aucune modification déduite
de cet appel. Changement réel réalisé par la flèche visible du champ.
Pas de nouvel examen à 3,1, Apply ou Save ; archive 5717781F intacte. Essai fermé.
Rivières/bâtiments non testés : mutualisation du code ne vaut pas leur preuve GUI.

## Déplacement global du tracé — implémentation du 15 septembre

`path_stroke::translate` décale 2–128 points par un vecteur X/Z fini. Retour
atomique sans mutation de l'entrée, refus des résultats infinis, segments trop
courts et décalage sans effet à la précision disponible. Le Studio propose
« Déplacer tout le tracé » : seules les coordonnées du brouillon changent ;
l'invalidation existante retire l'ancienne proposition. Champs nom/profil et
source acceptée conservés. Les verrous désactivent le panneau ; Examiner conserve
les précontrôles de géométrie et de limites avant Apply, Undo et sauvegarde.
Les raccords avec d'autres chemins ne sont pas déplacés implicitement.

Six tests path_stroke PASS (13,45 s compilation, 0,00 s exécution), dont translation
globale, aller/retour exact sur la fixture et refus d'entrées/dépassements.
Clippy Studio Web/WASM strict PASS (29,84 s). Aucun changement de format.
Code postérieur à C86B50ED ; parcours bouton/Apply/Undo graphique encore dû.
Cette préparation CPU ne clôture ni R2-021 ni Gate 2.

13 septembre 2026, workspace Rust modifié, aucune publication.

## État courant — chemins persistants

### Insertion entre deux points — implémentée, contrôle graphique restant

Bouton « Insérer un point avant le suivant » raccordé au brouillon : subdivision
au milieu du segment choisi, voisins et texte des champs existants conservés.
Le nouveau point est repéré en orange et peut ensuite être placé dans la vue.
Le geste de picking éventuellement armé est annulé après insertion réussie.
Ni Apply ni Save implicite ; Examiner utilise le même PathEdit::Replace avec
précontrôle des limites, du verrou, de la surface et des dégagements végétaux.
Bouton indisponible sur le dernier point et à 128 points. La règle pure refuse
les données non finies, l'index absent et une subdivision produisant un segment
de moins de 1 mm, y compris par arrondi. Midpoint standard sans overflow.

Contrôles exécutés : `cargo test -p forge_world --test path_stroke --test path_surface
--locked` : 14 PASS, compilation 3,58 s, exécution 0,12 s. Trois nouveaux tests
couvrent interpolation, erreurs/bornes et Preview/Apply/retry/Undo/Redo puis
relecture des trois encodages d'archive en mémoire. Clippy world lib et ces tests
PASS (0,39 s), Clippy Studio WASM web strict PASS (8,56 s), formatage effectué.
Premier Clippy : midpoint manuel/cast puis comparaisons float des tests refusés ;
corrigés avec midpoint standard et comparaison bit-à-bit, sans désactiver de lint.
Recherche Experience sans résultat de type Experience exploitable ; aucune
procédure historique appliquée. Aucun processus graphique lancé pour ce lot.
Le candidat 5BE8BF4B précède cette insertion : contrôle du nouveau bouton dans
un prochain candidat groupé encore requis ; pas de qualification visuelle déduite.

### Repères des points du brouillon — replay ciblé acquis

Case d'affichage volontaire et bouton « Repérer ce point » ajoutés à l'inspecteur.
Repères jaunes, point choisi orange, pied sur la surface triangulée du terrain.
Ils suivent les coordonnées du brouillon, y compris après picking, sans modifier
le tracé accepté ni constituer une cible de picking. Coordonnées invalides ou
hors terrain omises ; pas de connexion graphique trompeuse entre points invalides.
Repères masqués en playtest/projection périmée et libérés au démontage du panneau.
Au plus 128 repères Gizmos, aucun mesh/Entity persistant par point. Positions et
plan de terrain recalculés seulement après changement de brouillon/source,
taille du repère adaptée à la distance caméra. Ce ne sont pas encore des poignées
déplaçables directement ni des numéros 3D.

Clippy Studio WASM strict PASS (4,99 s), Trunk PASS (compilation Rust 1 min 50 s).
Candidat groupé `studio-authoring-web-path-guides-20260913` exécuté le 13/09 :
EXE SHA-256 `5BE8BF4B3537B57C3FBF1C61944F0026DD37B8E2EA05E1A09AA5D01DE3D0BEF9`,
WASM `90C925EB923B5E088A18AB36E92A3C5B58D7B4944DD671F386BA54FA14B94963`.
Le handle de construction Tauri n'était plus disponible à la reprise ; aucune
durée/code de sortie Tauri déduit. Le binaire daté 18:30:21 et ses nouveaux
contrôles ont été effectivement lancés et inspectés.

Replay sur copie `authoring-path-guides-ui-20260913`, écran secondaire
(origine -1920,52 ; fenêtre 1920×1032) : ouvrir mon-projet/zone-1, sélectionner
le chemin à trois points, « Repérer ce point » sur le premier. Trois repères
visibles, premier orange et autres jaunes, ancrés aux extrémités et au coude ;
désactivation de la case : les trois disparaissent, chemin accepté inchangé.
Captures inspectées dans la session, sans fichier PNG durable. Jouer affiche
le personnage au sol et le chemin dégagé ; retour : « Session synchronisée »
et Jouer réactivé. Ce passage vérifie le raccord de l'identité composite sur
ce chemin réel, pas la charge maximale de 64 chemins ni les quatre profils GPU.

Aucune sauvegarde effectuée : copie et original `authoring-multipoint-ui-20260913`
conservent le SHA-256 `8C7BE5D111DF6C3FA1F736BB21EC94A14FE8A1EC5440E727D94C2B2661521085`.
Processus 2128 (créé 18:33:51) et descendants 28912,25920,31812,21400,24864,
1304,32660 tous absents après fermeture. Aucun commit/push. Déplacement d'un
repère après nouvelle saisie, masquage automatique en jeu avec case active,
coordonnées invalides et performances des repères non qualifiés graphiquement.
Pas de nouvelle campagne FPS ; Gate 2 et R2-021 restent ouverts.

### Raccord des dégagements séparés — contrôles CPU/natif/WASM acquis

Le Studio conserve maintenant recette végétale et chemins séparément et soumet
`update_vegetation_sources_with_paths`. Le précontrôle et la barrière playtest
utilisent la même identité dérivée. Les préparations synchrones, worker natif,
cache incrémental WASM et reprise procédurale transportent les chemins ; le
garde de commit des uploads compare cette identité, pas le seul document végétal.
Toute mise à jour de source invalide le cache/préparation/upload précédent.
L'API historique sans chemins reste disponible et retire explicitement ceux-ci.

Treize tests world PASS (8 surfaces, 5 végétation ; 0,12 s et 0,09 s),
16 tests renderer PASS et 2 sondes ignorées préexistantes (compilation finale
40,55 s, exécution 5,57 s). Couverture : quatre qualités avec 32 exclusions +
64 chemins, source invalide/retry/retrait, identité de worker périmée et
acceptation différée d'une projection de chemins. Tickets GPU simulés dans les
tests headless : pas une preuve d'upload sur carte graphique.
Clippy natif lib/tests PASS (39,30 s), Studio WASM strict final PASS (4,81 s),
fmt/diff PASS. Avertissement amont proc-macro-error2 conservé.
Le test de précontrôle ancien a été adapté au nouveau contrat : 32 exclusions
manuelles n'interdisent plus un chemin supplémentaire ; les limites de chaque
source restent contrôlées. Replay graphique borné acquis ci-dessus ensuite.
La première compilation du nouveau test refusait un accès à un champ privé et
signalait un import superflu : corrigés dans le test sans élargir la visibilité.
L'adapter historique fusionné ne reste consommé que par les tests de référence.
La levée du quota partagé est raccordée en source ; le binaire 7107B3C5 précède
ce lot et n'en constitue pas une preuve graphique. Aucun processus graphique lancé,
aucune sauvegarde utilisateur modifiée, aucun commit/push ni passage de gate.

### Exclusions dérivées séparées — fondation CPU, après 7107B3C5

`path_vegetation::prepare` produit un jeu de dégagements dérivés non sérialisé,
distinct des exclusions authorées. Validation commune des chemins (64, IDs
uniques triés, points/profils bornés), sections contrôlées comme les surfaces.
`VegetationGenerationPlan::from_documents_with_paths` applique ces dégagements
avant l'acceptation d'espacement : masques, documents et placements manuels ne
sont pas réécrits. Hash de projection versionné incluant source et chemins ;
sans chemins, hash et génération identiques au générateur existant.
Un dégagement dérivé de virage peut dépasser le bord de zone, où aucun candidat
végétal n'existe ; cela ne change pas la validation des exclusions manuelles.

Cinq tests path_vegetation PASS (0,09 s exécution), dont deux nouveaux :
32 exclusions manuelles + 64 chemins, corridor et exclusion manuelle vides,
source intacte, refus 65 chemins/doublons/33 exclusions manuelles, identité
sans chemins et virage en bord de zone. Une erreur de mutabilité du test puis
un nom similaire Clippy ont été corrigés ; Clippy final lib/test PASS (0,35 s),
fmt/diff PASS. Pas de mesure de performance maximale ni de qualification GPU.

**Raccord restant** : renderer, changement de qualité, précontrôle Studio et
barrière playtest utilisent encore l'adapter historique `project` qui fusionne
les exclusions. Le quota de 32 reste donc effectif dans l'application actuelle.
Le nouveau constructeur n'est pas encore consommé en production ; prochaine
étape : transporter ces dégagements et leur identité jusqu'aux projections,
sans contourner le quota du document source ni perdre le garde playtest.

### Replay corrigé 7107B3C5 — clic, tracé, aperçu et Undo acquis

Trunk PASS (Rust 25,90 s), Tauri PASS (16,77 s). Exécutable SHA-256
`7107B3C5E0B0C394F2E85B9A9A6B31B92D6FCBBD6730350C056AE85F67B2E2D9`,
WASM `4BD5373834060299A71AFD406EDAE5E8DACFFC03DE94FC94D2907C643A9DD011`.
Frontend `studio-authoring-web-path-pickfix-20260913`, kit identique fichier par
fichier au candidat précédent ; aucune nouvelle acquisition.

Même copie jetable que 826A3551. Fenêtre sur écran secondaire X=-1920.
Le clic sur sol renseigne le point 1 : X=-103.31014, Z=20.20874, uniquement
dans le brouillon. Tracé continu au relâchement : deux points
(-121.96874,22.008835) → (-11.0289,97.47747), largeur 8 m, accotement 1 m.
Examiner conserve les champs et affiche le diff exact ; ruban cyan visible,
végétation inchangée. Abandon restaure le chemin brun à trois points et
désactive Apply. Nouvel examen du même brouillon puis Apply : ruban brun à
deux points et dégagement végétal visibles. Undo restaure les trois coordonnées
initiales et le chemin coudé, Rétablir devient disponible.

Pas de Save pendant ce replay : copie et original multipoint gardent SHA-256
`8C7BE5D111DF6C3FA1F736BB21EC94A14FE8A1EC5440E727D94C2B2661521085`.
Les retouches de session Apply/Undo ne sont pas une preuve de sauvegarde/relecture
du geste. Pas de qualification de tracé courbe complexe, de précision métrique
du clic, d'annulation Échap, de playtest pendant aperçu ou de Redo dans ce lot.
Captures inspectées dans la conversation ; aucun PNG durable créé.
PID 27892 et descendants 15920,27168,30832,18300,17232,20320,32272 fermés,
absence vérifiée. Gate 2 reste ouverte, aucun commit/push.

### Replay outils 826A3551 — défaut identifié et corrigé en source

Trunk PASS (Rust 1 min 29 s), Tauri PASS (27,86 s), kit précédent recopié
avec hashes identiques. Exécutable SHA-256
`826A35513CB94F0C8BC97F5E7D01E4F2EDF1B2FB52AF23EE43B506728639D820`.
Ouverture de la copie `authoring-path-tools-ui-20260913`, chemin à trois points
visible. Fenêtre déplacée puis maximisée sur l'écran secondaire (origine X -1920).
Le placement par clic a renvoyé « Tracé annulé : pointeur hors terrain » pour
des positions sur le terrain visible. Replay interrompu : tracé continu et cyan
non qualifiés, aucun Apply/Save exécuté.

Cause établie dans le code : appels chemins armés avec rayon 1 m, alors que
`update_terrain_brush_cursor` refuse les rayons hors 2..=128 m avant le raycast.
Les deux appels utilisent maintenant une constante 2 m pour le contour seulement,
sans changement de largeur du chemin, de précision du hit ou de simplification.
Le candidat ci-dessus précède cette correction ; replay corrigé encore dû.

Correction : Clippy Studio WASM avec feature web et warnings projet en erreurs
PASS (3,86 s), fmt PASS. Avertissement amont proc-macro-error2 conservé.

Processus 19272 et descendants 8712, 5148, 29324, 32016, 13756, 12588, 3560
fermés, absence vérifiée. Copie et source multipoint gardent toutes deux le SHA
`8C7BE5D111DF6C3FA1F736BB21EC94A14FE8A1EC5440E727D94C2B2661521085`.
Captures inspectées dans la conversation, aucun PNG durable ajouté.

### Aperçu de surface avant application — après FD7CC2D7

`path_surface::preview` dérive un reçu limité aux chemins : hash/révision de
départ, inverse, paramètres, révision suivante et hash résultant contrôlés.
Les reçus mélangeant terrain/nom/autres corrections sont refusés. La géométrie
proposée reste attachée au hash du terrain accepté, puisque seules les sources
de chemins changent. Aucun document ou état de session modifié.

Studio conserve séparément la tête acceptée et la projection temporaire. Après
réponse PreviewZone, le renderer remplace ses meshes de chemins par la proposition
cyan ; végétation et collisions restent acceptées. Abandon, nouvelle commande ou
nouvel état reçu restaurent la tête acceptée. Le mode aperçu participe à la clé
de reconstruction : Apply ne conserve pas la couleur cyan lorsque le hash de la
proposition devient le hash accepté. La barrière playtest refuse explicitement
le mode aperçu. Les meshes/matériaux possédés sont libérés lors du remplacement.

Huit tests path_surface PASS (4,42 s compilation, 0,12 s exécution), dont deux
nouveaux sur source intacte, identité de terrain acceptée, changement géométrique
et refus de reçus périmés/incohérents/mixtes. Clippy natif lib/test PASS (1,17 s).
Clippy Studio WASM strict final PASS (1,86 s), fmt/diff PASS. Aucun replay
graphique de cet aperçu : teinte, abandon, Apply et playtest
réel restent à vérifier avec les nouveaux outils de clic/tracé.
Cette vue ne préfigure pas le dégagement végétal final : des arbres peuvent encore
recouvrir le nouveau tracé tant qu'il n'est pas appliqué. Pas de comparaison
simultanée avant/après ni d'intersections complexes qualifiées.

### Tracé continu du brouillon — après les commandes de point

« Retracer les points dans la vue » réutilise la capture continue du terrain,
puis remplace les seuls points du brouillon au relâchement. Nom, largeur,
accotement et identité conservés ; aucune commande appliquée avant Examiner/Apply.
Garde du hash de zone et des points initiaux ; échec ou annulation laisse les
coordonnées inchangées. Les verrous et le playtest gardent le panneau désactivé.

`forge_world::path_stroke::simplify` utilise une simplification itérative par
distance au segment fini : extrémités exactes et sous-ensemble des coordonnées
capturées, résultat déterministe, pas de rééchantillonnage. Tolérance Studio 1 m
aux échantillons, 1024 points d'entrée maximum, 128 points éditables maximum.
Refus des entrées non finies, gestes sans longueur suffisante et dépassements,
jamais de résultat partiel. Ce n'est ni une garantie topologique, ni un lissage
du rayon des virages : la géométrie et la végétation sont contrôlées lors d'Examiner.

Trois tests ciblés PASS (compilation 5,47 s, exécution <0,01 s) : ligne de 1001
points, angles, faible déviation, déterminisme, tolérance zéro, retour collinéaire,
entrées invalides et quota de sortie. Clippy natif lib/test PASS (1,47 s).
Tracé réel, précision du picking et parcours complet d'application encore dus.
Le candidat FD7CC2D7 est antérieur ; aucun nouveau processus graphique lancé.
Clippy Studio WASM strict PASS (6,94 s), fmt/diff PASS ; avertissement amont
de future incompatibilité proc-macro-error2 conservé.

### Placement d'un point par clic terrain — après FD7CC2D7

Chaque point du brouillon dispose de « Placer ce point dans la vue ». Réutilise
le pont de picking terrain existant, mode clic unique, curseur 1 m ; aucune
entité ni mutation directe dans le handler. Capture du hash de zone et de la
liste des coordonnées saisies à l'armement ; résultat refusé si l'un change.
Le clic remplit uniquement X/Z du point choisi, puis l'auteur examine et applique
le tracé par les mêmes commandes. Les verrous/playtest désactivent l'inspecteur.
Un point hors terrain produit un message, Quitter/Échap/sortie de vue annulent
le geste existant ; démontage de l'inspecteur libère le picking armé.

Clippy Studio WASM strict PASS (4,90 s), avertissement amont proc-macro-error2
conservé. Aucun nouveau build graphique ni clic réel qualifié dans ce lot.
Le replay FD7CC2D7 précédent ne contient pas ces boutons. Reste à vérifier le
ciblage du terrain, l'annulation et le parcours clic → aperçu → Apply/Undo/Save.
Ce n'est pas encore un glissement de poignée ou un tracé continu de spline.
Le statut textuel d'armement n'est pas notifié par l'annulation globale Échap ;
il peut rester affiché après annulation, sans laisser de callback actif.

### Replay multipoint FD7CC2D7 — 13 septembre 2026

Trunk terminé (compilation Rust 2 min 13 s), puis Tauri PASS (45,97 s).
WASM SHA-256 `5C1C2DB0DE96CBBD56EA43E9F76A2F01BC1A483884E71CE921AE7291E633C6D6` ;
exécutable `FD7CC2D7BFB5105491A1B6B9E5FE183004FD0C714F64AE655CB82624CCD61AF2`.
Frontend dans `.gate2-evidence/studio-authoring-web-multipoint-20260913`, kit
précédent recopié et vérifié fichier par fichier, aucun nouvel asset.

Copie jetable `authoring-multipoint-ui-20260913`, même zone/chemin que le replay
précédent. Ouverture catalogue, sélection du chemin, Ajouter un point : le point 3
apparaît, bouton utilisable et plus de texte Rust parasite. Saisie (90,90),
Examiner : diff exact (-90,0) → (90,0) → (90,90), largeur 8 m, accotement 1 m.
Les champs et le troisième point restent présents après réponse PreviewZone.
Apply : chemin prolongé avec un raccord visible sans trou discernable dans la
vue d'ensemble. Pas de qualification rapprochée ou de réalisme déduite.

Save puis fermeture de zone et Ouvrir par clés mon-projet/zone-1 : Session
synchronisée et chemin en virage visible sur capture inspectée. Archive v7,
15 commandes, 2561577 octets, tête
`bbd613d4812b235609c837724456bd0036055d3f23aae63a787b189c9e2fcfac`,
SHA-256 fichier `8C7BE5D111DF6C3FA1F736BB21EC94A14FE8A1EC5440E727D94C2B2661521085`.
Dernière commande `f349979d-b265-56f4-a7dd-37cb95844438` : paths Set avec les
trois points, terrain/densité/matériaux/placements Keep. Original `authoring-paths-ui-20260913`
inchangé, SHA-256 `CE17A4252F235194E2BA4D0203B03EF0889BE2EBC2B68BE1167D7854D4B00099`.

Essai effectué sur écran secondaire après déplacement de la fenêtre initialement
agrandie sur l'écran principal. Les erreurs de coordonnées hors fenêtre pendant
le défilement n'ont pas été assimilées à des commandes exécutées. Captures
inspectées dans la conversation, aucun fichier image durable ajouté.
PID Studio 3028 et descendants 4276/28736/25964/17336/14168/31976/10568 fermés ;
absence vérifiée. Aucun navigateur utilisateur arrêté, aucun commit/push.

Non acquis dans ce replay : refus d'un tracé invalide via l'interface, maintien
du focus pendant frappe, Undo/Redo multipoint graphique, inspection rapprochée,
intersections complexes et gestes 3D. Les tests CPU correspondants ne sont pas
une preuve UI. Gate 2 et R2-021/R2-024 restent ouverts.

### Contrôle préalable des nouveaux tracés

Le bouton Examiner passe par `path_surface::prepare_edit` : commande de domaine
inchangée, puis projection CPU de la tête candidate temporaire et contrôle des
exclusions végétales dérivées. Si le ruban se plie, sort du terrain, dépasse son
budget ou exige une végétation dérivée invalide, aucune requête PreviewZone n'est
envoyée. Message local explicite, aucune application ni sauvegarde. La recette
vide est désormais fournie par `editor_vegetation::empty`, commune au contrôle
et au Studio, sans changement de ses valeurs.

Le contrôle est destiné à Insert/Replace. Unlock/Remove restent préparables pour
réparer une zone déjà non projetable. Ce contrôle de présentation ne remplace pas
les validations transactionnelles du host ; les autres clients peuvent toujours
préparer une commande moteur-neutre via `prepare_path`. Il ne prouve ni le GPU,
ni le budget d'archive, ni toutes les intersections. Le calcul reste synchrone
dans le frontend et sera refait après Apply : coût interactif non qualifié.

Six tests path_surface PASS (0,12 s, compilation 4,73 s), dont deux nouveaux :
commande identique au préparateur domaine, source inchangée, refus d'un demi-tour,
déverrouillage/suppression encore possibles et quota végétal refusé avant retour
de la commande. Clippy natif lib/test PASS (1,18 s). Aucun essai graphique relancé ;
message et maintien du brouillon à qualifier dans le candidat regroupé.
Clippy Studio WASM strict PASS (5,38 s), fmt/diff PASS ; avertissement amont
de future incompatibilité proc-macro-error2 conservé. Aucun processus graphique lancé.

### Stabilité du brouillon dans l'inspecteur

Le panneau reconstruisait PathInspector à chaque notification AuthoringState,
notamment lors de Preview/Inspect/Save, ce qui remettait les champs à leur valeur
initiale. La cible de l'inspecteur passe désormais par un Memo comparant le chemin
sélectionné, le mode insertion et une génération de réinitialisation explicite.
Une notification qui ne change pas cette cible ne remonte plus le composant.
« Nouveau chemin » réinitialise volontairement le brouillon, même si le mode
nouveau était déjà sélectionné. Une modification acceptée du chemin, un changement
de sélection ou le passage en playtest conserve le comportement de remontage.
La commande reste préparée depuis la tête courante ; aucun brouillon ne devient
une seconde source persistante et aucune sauvegarde implicite n'est ajoutée.

Clippy Studio WASM web strict PASS (2,65 s). Maintien réel du focus et de la
saisie pendant Preview/Save à qualifier avec l'ajout de point et les virages
au prochain candidat regroupé ; pas de nouveau lancement graphique dans ce lot.

### Raccords multipoints — code après le replay AF3EF695

Les rubans partagent maintenant une section en onglet à chaque point intérieur,
avec réutilisation des indices de la rangée commune. Largeur perpendiculaire
conservée ; subdivisions ajustées à l'allongement des bords et des sections.
Les sommets restent drapés sur le terrain, sans mutation des sources ni collision
ajoutée. Demi-tour/onglet de plus de quatre demi-largeurs refusé ; triangle plié
ou dégénéré refusé avec conseil d'espacer les points ou réduire la largeur.
Pas de résolveur d'auto-intersection ni de jonction entre chemins distincts.

Végétation : cercle dérivé autour de chaque virage non droit, calculé depuis
la même section pour dégager l'extérieur de l'onglet. Ce dégagement est conservateur,
plus large que le polygone exact. Chaque cercle consomme une exclusion du quota
existant de 32 ; refus explicite si quota/bornes dépassés, aucune suppression de
masques ou d'objets manuels. Cette limitation n'est pas une solution finale aux
tracés complexes ou proches de la limite terrain.

Tests ciblés : path_surface 4 PASS (0,12 s), path_vegetation 3 PASS (0,03 s),
compilation 20,85 s. Virages gauche/droite à 90 degrés : surface totale attendue,
triangles positifs, arêtes de couture partagées par deux triangles, déterminisme
et sources intactes. Demi-tour, pli local et budget refusés ; instances absentes
du dégagement de virage. Les autres intersections ne sont pas couvertes.
Clippy natif lib + ces deux tests PASS (0,32 s) et Studio WASM strict PASS (5,45 s).
Un contrôle Clippy intermédiaire a révélé une comparaison flottante exacte dans
le test historique d'alpha ; comparaison des bits désormais explicite, sans
affaiblir l'exigence d'opacité complète. Avertissement amont proc-macro-error2 conservé.

Aucun nouveau build applicatif ni essai GPU : rendu des virages et correction du
bouton Ajouter un point restent à inspecter ensemble au prochain candidat.
La preuve AF3EF695 ci-dessous porte uniquement sur le chemin droit précédent.
R2-021/R2-024 et Gate 2 restent ouverts ; CI distante NOT RUN — infrastructure billing.

### Replay Studio regroupé — 13 septembre 2026

Candidat Trunk/Tauri construit avec succès, SHA-256 exécutable
`AF3EF695BDB54648A2569F396DB5C4F03715C5021A347D4491EDF5CA1A82F428`.
Essai sur écran secondaire, copie dédiée dans
`E:/OpenRPG_Forge/.gate2-evidence/authoring-paths-ui-20260913`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`. Archive originale du dossier
`authoring-delta-ui-projects-20260913` conservée : SHA-256 avant/après
`FC2DBCAA59F8D6F1222C777ABDC8B0B1CA5C87FA9195769BF81AEC74D15F6F4F`.

Parcours produit acquis : ouverture v6, saisie du chemin de (-90,0) à (90,0),
largeur 8 m et accotement 1 m, examen du diff exact, Apply, surface visible et
Save. Chemin `2bbddf62-5688-5bd9-9abd-4bf34970bfc8`, tête
`6e2f0738f69fae3ae8d5619474109e1c79ff0c9c30ac78dc3cfc4c48829d8606`.
La copie sauvegardée passe de 13 à 14 commandes, format v7, 2560895 octets,
SHA-256 `CE17A4252F235194E2BA4D0203B03EF0889BE2EBC2B68BE1167D7854D4B00099`.
Deux arches et terrain antérieurs restent visibles. Le corridor paraît dégagé ;
la preuve quantitative de dégagement reste celle des tests CPU, pas la capture.

« Jouer à cet endroit » avec X/Z=0 : capsule observée sur le chemin,
diagnostic « Simulation active », tick 16873, position (-0.00,3.91,0.00),
au sol true, caméra ThirdPerson. Retour édition puis fermeture de zone,
actualisation du catalogue et réouverture : Session synchronisée, chemin et
arches visibles sur capture inspectée. Hash terrain acquitté
`73c35ed944e1f4173debdbf5ec3fa076735d02d1c126c578d01af52191cd236e`.
Archive identique après playtest et réouverture. Aucun déplacement clavier,
contact avec l'arche, départ non nul ou métrique GPU qualifié par cet essai.
Captures inspectées dans la conversation ; pas de fichier image durable ajouté.

Défaut réel trouvé : le bouton d'ajout affichait du texte Rust et était inutilisable.
La comparaison `>=` non délimitée dans l'attribut Leptos fermait prématurément
la balise. Comparaisons des boutons ajout/retrait maintenant entourées d'accolades.
Ce correctif source est postérieur au candidat ci-dessus : son comportement UI
reste à vérifier au prochain candidat regroupé, sans refaire le parcours acquis.
`cargo fmt -p forge_studio --check` PASS ; Clippy Studio WASM web strict
avec `--locked -- -D warnings` PASS (36,68 s). Avertissement amont de future
incompatibilité `proc-macro-error2` conservé. Pas de nouvelle qualification workspace.

Fin d'essai : Studio PID 7280 fermé ; absence vérifiée des PID possédés
7280, 28328, 27968, 16724, 27068, 16636, 20296 et 30320. Aucun navigateur
utilisateur arrêté. Aucun commit/push. R2-021 reste ouvert (édition multipoint,
gestes et limites) ; raccords, intersections et rendu final ne sont pas qualifiés.
Les paragraphes suivants décrivent les lots antérieurs et leurs limites à leur date.

### Végétation et raccord playtest — dernier lot

`path_vegetation::project` ajoute uniquement des exclusions dérivées de corridor
(demi-largeur + accotement), ID issu de l'EntityId du chemin. Aucun changement
des couches, masques ou placements manuels enregistrés. Source existante clonée,
exclusions antérieures conservées ; collision d'ID ou quota invalide refusés,
jamais écrasés. Le renderer existant accepte 32 exclusions au total : une tête
peut stocker 64 chemins mais leur projection végétale peut donc être refusée.
Cette limite reste ouverte, non assimilée à une qualification des 64 chemins.

Deux tests `path_vegetation` PASS (0,03 s ; compilation 13,75 s) : présence
d'instances avant, corridor vide après génération, couches/hash source inchangés,
retour sans chemins identique, collisions/quota/NaN refusés. Les arbres placés
manuellement ne sont pas retirés automatiquement.

Snapshot natif transporte les chemins immuables, sans refus systématique.
Le Studio dérive la même végétation pour l'édition et le playtest, puis attend
le hash terrain, le hash végétal et les surfaces de la tête exacte. Barrière
chemins : succès de génération, nombre attendu, entités/handles mesh/matériau
existants, pose identité et absence de parent. Ce n'est pas un acquittement GPU
ni une inspection graphique des ombres. Échec de génération reste bloquant.
Clippy WASM strict PASS (26,92 s). Replay réel et barrière exécutée restent dus.
Après évolution du snapshot : tests project authored_paths (2) et zone_host (1)
PASS, 29,19 s de compilation puis 0,03 s de tests. Snapshot conserve ses chemins
après Undo ; aucune source éditée par sa construction.

### Projection après application — dernier lot

`forge_world::path_surface` génère des rubans subdivisés à <=1 m dans les deux
directions, chaque sommet échantillonné sur les triangles exacts du terrain,
levé de 2 cm. Largeur centrale opaque et accotement alpha ; aucune retouche de
hauteur, matériau canonique ou collision. Budget 250000 sommets total, corridor
hors terrain ou segment <1 mm refusés explicitement. Les jonctions entre segments
ne sont pas encore soudées ; la précision entre sommets sur un relief abrupt,
transparence/ombres et intersection restent à qualifier. Matériau terre uniforme,
sans nouvelle texture ni asset importé. Aucun réalisme final revendiqué.

`authoring_path_projection` reçoit la tête acceptée, attend le hash terrain
acquitté, retire ses anciennes entités/meshes/matériau et prépare les nouvelles
surfaces une seule fois par tête. Les hashes sont calculés à réception, pas à
chaque frame ; erreur affichée, pas de faux statut GPU. Génération encore
synchrone et remplacement de tous les chemins à chaque tête : reconstruction
partielle et coût interactif à traiter. Aperçu 3D avant Apply toujours absent.
Le playtest conserve son refus transitoire ; végétation sur le tracé non dégagée.

`cargo test -p forge_world --test path_surface --locked` : 2 PASS, exécution
0,12 s, compilation 13,84 s. Déterminisme, source inchangée, sommet/terrain,
orientation triangles, alpha des bords, corridor invalide et budget vérifiés.
Clippy Studio WASM final PASS (4,70 s). Aucune capture ou mesure GPU et aucune
preuve de gestes UI acquises. Pas de publication ou de processus graphique lancé.

Panneau `authoring_paths` raccordé dans SourceControls : un inspecteur choisi
dans une liste, création, saisie de nom/largeur/accotement et points X/Z,
ajout/retrait/réordre de points, verrou/déverrouillage et suppression.
Toutes les actions préparent PreviewZone ; Apply/Undo/Redo/Save restent communs.
Diff textuel avant/après avec ID, profil, verrou et tous les points, pas un
simple comptage. Limites et absence de projection explicitement affichées.
Clippy Studio WASM strict PASS (final 4,63 s), fmt/diff PASS. Aucun lancement,
capture ni geste UI encore acquis ; ne pas présenter ce panneau comme qualifié.

`forge_project::zone_authoring::paths` définit les chemins authorés dans les
corrections de la tête unique : EntityId, nom, points X/Z, largeur, accotement,
verrou. Insert/Replace/Remove/SetLocked utilisent ZoneRequest/ZoneEditSession.
Les verrous sont revalidés dans la session, y compris pour une commande brute.
Les chemins manuels ne sont pas les features générées d'un WorldSketch : son
préparateur ci-dessous reste une source de propositions non intégrée.

Versions explicites : tête v3, archive v7 delta, v8 intégrale, v9 compacte.
Le domaine absent est omis ; anciens documents/hashes conservés. Undo ne fait
pas oublier le format nécessaire au journal. Toute réétiquette v4/v5/v6 d'un
journal contenant des chemins est refusée. Aucune conversion de fichier réel
ni création d'un second fichier canonique dans ce lot.

Vérifié : `cargo test -p forge_project --test authored_paths --test
manual_placements --test zone_host --locked` : 9 tests PASS (compilation 30,76 s).
Tests chemins : Preview sans mutation, retry exact, Undo/Redo, trois archives
et audits identiques après relecture, conservation après régénération, verrou
non contournable et refus d'entrées invalides. Extension des cas invalides à
NaN/Inf/900 m puis replay authored_paths : 2 PASS, 0,02 s.
Clippy `-p forge_project --all-targets -- -D warnings` PASS. Contrôle élargi
project+world/all-targets NON PASS : `exercise_transform_history` préexistant
dans `placement_transform_authoring.rs` dépasse 100 lignes (122). Pas de waiver
ni de qualification workspace déduite. Un clone superflu dans le test placement
du lot précédent corrigé par `std::slice::from_ref`.

Reste : commandes de points/gestes et panneau Studio, géométrie/material/terrain
et caches identifiés, réouverture réelle dans le produit, profils de rivière,
intersections. Le playtest refuse explicitement les chemins non projetés.
Le refus est transitoire, pas une solution finale de R2-041. Ces tests de domaine
ne prouvent pas encore une édition visible ou une sauvegarde par l'utilisateur.

## Lot précédent — proposition WorldSketch

Implémenté : `forge_world::spline_edit` prépare Insert/Replace/Remove par ID
sur WorldSketch existant. Hash source exact, refus des verrous, doublons,
cibles absentes, remplacement sans changement et profils/points invalides.
L'aperçu immutable expose avant/après et proposition complète ; les autres
features et la source restent intactes. Aucun compteur de révision publié,
aucun fichier écrit, aucun second journal ou modèle canonique créé.

Vérifié : 2 tests `cargo test -p forge_world --test spline_edit --locked` PASS,
round-trip de la demande/proposition, insertion/suppression, édition ciblée,
source inchangée, hash périmé, verrou, ID absent/doublon et NaN refusés.
Premier essai : identifiant de fixture avec tirets refusé conformément au
schéma ; fixture corrigée en underscores. Clippy strict world/lib + test PASS.
Le contrôle natif a révélé deux warnings du host du lot playtest : extraction
de lecture d'historique et remplacement d'une closure redondante, sans changement
de règles ni suppression de lint. Aucun build graphique relancé.
Régression host après extraction : `cargo test -p forge_project --test zone_host
--locked` PASS (1 scénario, exécution 0,03 s ; compilation 29,02 s).
`cargo fmt -p forge_world -p forge_project --check` et `git diff --check` PASS.

Reste pour les propositions WorldSketch : conservation de la recette acceptée dans la tête de zone,
raccord à la revue de régénération et au journal Undo/Redo/Save, commandes de
points et panneau/gestes Studio, projection et qualification visuelle. Ne pas
écrire WorldSketch à côté de l'archive comme seconde autorité. Les corrections
terrain/densité/placements doivent traverser la revue existante sans perte.
Le compilateur WorldSketch actuel reste limité à 512 m et conserve des valeurs
de démonstration : ce préparateur ne le qualifie pas comme générateur générique.
R2-021 à R2-025 restent ouverts ; aucune clôture de Gate 2 déduite.
