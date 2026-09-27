# R2-023 — remplacement d'eau authorée dans le moteur

13 septembre 2026, Rust, workspace modifié, aucune publication.

## Implémenté

L'ancien plugin préparait une rivière au démarrage. `authoring_water` ajoute
une préparation réutilisable sur la tête de zone : terrain creusé et toutes les
géométries d'eau sont préparés avant accès au stockage ECS/assets. Le lot conserve
le hash de tête et le terrain attendu. Remplacement refusé sur mauvais hash,
terrain non concordant ou stockage absent, avant toute mutation.

Un état propriétaire suit deux entités et deux meshes par rivière, matériaux
eau/berge, image de détail et animation indépendante. Après échange, seules les
ressources suivies de l'ancien lot sont retirées. Lot vide = retrait de l'eau.
Le hash de l'état confirme une projection CPU/ECS, **pas un upload GPU terminé**.

`AuthoringWaterPlugin` enregistre les shaders/matériaux existants sans instancier
la rivière de preuve, puis installe l'animation. Enregistrement partagé avec le
plugin historique et gardé contre une double installation du material plugin.
Le rendu initial authoring est opaque forward ; ni SSR ni transmission/profils
optiques complets annoncés comme raccordés. Aucune nouvelle pile graphique.

## Vérification acquise et limites

Deux tests headless PASS : deux remplacements ne font pas croître les compteurs,
retrait vide libère meshes/matériaux/images, entité étrangère conservée, source
inchangée ; tête périmée et terrain absent ne créent aucune ressource. Compilation
1 min 14 s, exécution 0,11 s. Pas d'exécution des shaders sur GPU.

Le partage de l'enregistrement des plugins a été ajouté après le démarrage de
cette compilation ; ces deux tests ne qualifient ni son installation réelle ni
les effets visuels. Clippy final engine lib/tests PASS (24,57 s), Studio WASM
strict PASS (21,32 s). Premier Clippy avait signalé l'API non exportée et un import
glob : export `authoring_water` et imports explicites ajoutés, sans lint désactivé.
Avertissement amont proc-macro-error2 conservé ; formatage/diff contrôlés.

Raccord Studio encore dû : une seule projection terrain/eau/végétation, cache
commun, cohérence des chemins et des outils de picking/snap, panneau d'édition,
aperçu/Apply/Undo/Redo/Save/relecture. Le playtest de rivière reste refusé.
Aucun candidat graphique ni résultat de Gate 2 déduit de ces tests.

## Raccord Studio suivant — 13 septembre 2026

Implémenté : `authoring_scene` partage une projection dérivée bornée à une tête,
prépare le lot d'eau hors de la boucle Update, attend son hash de terrain avant
échange ECS. Végétation reçoit les recettes rivière. Chemins, repères, aperçus
de retouche et snap des objets (insertion, inspecteur, glissement, gizmo)
échantillonnent le terrain creusé. Aucun changement du document canonique.

Vérifié : nouveau test `paths_use_the_carved_surface_without_rewriting_sources`
PASS, 0,01 s d'exécution après 1,63 s de compilation : identité du terrain dérivé,
hauteurs des sommets et source inchangée. Clippy Studio WASM strict final PASS
en 1,83 s. Erreurs initiales corrigées (variable inutilisée, commentaire doc,
taille de fonction et nom de méthode du nouveau test), sans désactiver de lint.
Avertissement futur amont `proc-macro-error2` conservé.

Non qualifié : aucune ouverture GPU ni nouveau candidat livré. Le panneau
rivière n'existe pas encore ; playtest toujours refusé. La brosse relief refuse
explicitement une tête contenant des rivières, car travailler sur la source
non creusée donnerait une retouche trompeuse ; conversion de retouche dérivée
vers correction canonique à implémenter. Ce garde-fou n'est pas une capacité
achevée. Erreurs d'échange eau encore journalisées, statut utilisateur à ajouter.
Retrait complet lors d'une navigation sans tête à qualifier avec le cycle de
rechargement Studio. Les anciennes preuves ne couvrent pas ce nouveau raccord.
Pas de test graphique lancé, donc aucun processus graphique ajouté à nettoyer.

## Retouches sur relief dérivé — lot suivant

Le garde-fou global de brosse avec rivière est remplacé par
`river_projection::prepare_brush`. Le geste est calculé sur le relief visible,
puis seuls les échantillons dont la valeur change sont reportés dans la source
de retouches. Le reste du lit demeure dérivé et disparaît au retrait de la
recette. Les retouches existantes sont conservées. La reprojection du candidat
doit correspondre exactement au geste demandé, sinon la préparation échoue
sans mutation. Abaisser le lit et peindre à l'extérieur sont possibles ; relever
le lit contre la profondeur de la recette provoque un conflit explicite.
Pas de changement de schéma ni de règle de verrou/régénération.

Trois nouveaux tests PASS : deux géométriques (0,02 s), un parcours transactionnel
(0,04 s) Preview/Apply/retry/Undo/Redo, régénération compatible et relecture des
archives intégrale/compacte/delta. La première hypothèse du test de régénération
(changement de seed) était incorrecte : la règle existante renvoie
TerrainHeightReferenceChanged ; le test vérifie désormais ce refus et l'absence
de mutation, puis la régénération avec référence de hauteur identique. Aucune
protection affaiblie. Clippy world/test PASS (0,33 s final), Studio WASM PASS
(5,08 s, avant le dernier ajout exclusivement de test). Le cas sans rivière
produit le même edit que la brosse historique. Compilation initiale des deux
tests : 4,60 s ; du parcours final : 1,31 s.

Studio utilise ce préparateur et son circuit PreviewZone existant. Aucun rendu
graphique ni manipulation humaine de ce raccord n'est encore acquis. Le panneau
rivière, la gestion visible des erreurs d'eau et le playtest restent dus.

## Panneau Studio — lot suivant

`authoring_rivers` et son insertion dans SourceControls ajoutés : créer ou
sélectionner une rivière, éditer nom, points X/Y/Z, largeur/profondeur par point,
accotement, transition et budget de coupe. Ajouter/retirer des points du brouillon
(4–128), verrouiller/déverrouiller ou supprimer via examen. Un point ajouté
duplique le dernier avec avertissement : il faut renseigner ses coordonnées ;
le précontrôle refuse un segment dégénéré. Coordonnées numériques, pas encore
de picking dédié. Les valeurs de rendu initiales réutilisent le profil Forge
embarqué existant ; identité, nom, seed, tracé linéaire et niveaux sont remplacés
pour la zone. Aucun asset acquis/importé.

Les demandes sont des PreviewZone avec identité déterministe et hash courant,
précontrôlées par river_projection::prepare_edit ; Apply/Undo/Save restent ceux
de la zone. Verrous/invalides revalidés par le domaine. Memo de sélection conserve
le brouillon tant que la recette inspectée ne change pas. Lecture des champs
bornée même pendant suppression d'une ligne réactive. Résumé avant/après contient
points/profil/verrou/retrait. Aperçu du relief uniquement ; eau et végétation
acceptées ne sont pas modifiées avant Apply, annoncé explicitement dans le texte.

Clippy Studio WASM strict PASS final (2,07 s), après correction d'un point-virgule
de style sans désactiver de lint. Aucun test de domaine historique répété : cette
modification est un raccord UI. Aucun candidat graphique lancé pour ce lot ;
compilation ne qualifie ni interaction, ni shaders, ni réouverture par le panneau.
À grouper au prochain replay : panneau et brouillon, Examine/Abandon/Apply,
eau/terrain/végétation concordants, retouche locale, Undo/Save/Open, retrait et
nettoyage des ressources. Picking rivière, état utilisateur des erreurs d'eau,
profils optiques et playtest restent dus. R2-023/Gate 2 ouverts.

## Candidat construit ; replay non acquis

13 septembre, branche Rust. Trunk PASS (compilation Rust 1 min 23 s) puis Tauri
debug/no-bundle PASS (34,29 s). Première invocation Trunk refusée avant build
par NO_COLOR=1 ; relancée avec la valeur booléenne attendue true.

- Frontend : `E:/OpenRPG_Forge/.gate2-evidence/studio-authoring-web-rivers-20260913`.
- WASM SHA256 : `95E441C8D07E5082692576FBFCDF97C7893E4807D05429CDF511DE7A3D7DBF81`.
- Config : `E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring-rivers-20260913.conf.json`.
- Exécutable conservé : `E:/OpenRPG_Forge/.gate2-evidence/forge-studio-rivers-133FE370-20260913.exe`.
- EXE SHA256 : `133FE370165344AA6E2BF223303776A7003F72620ECE434E9F68A9D1D86D0323`.
- FORGE_AUTHORING_ROOT : `E:/OpenRPG_Forge/.gate2-evidence/authoring-rivers-ui-20260913`.

Lancement PID 3208 à 19:31:08, fenêtre 8457316 « Rivières Gate 2 ».
Coque capturée dans la conversation, état « Aucune zone ouverte » ; fenêtre
déplacée vers l'écran secondaire. Pas de PNG durable produit. La tentative de
création a été bloquée avant action : point couvert par Chrome, puis protection
répétée « user input was detected in this window » lors de l'activation, malgré
actualisation. Aucun autre input insisté ; pas de création, de panneau ouvert,
de rendu de rivière ou de test de sauvegarde acquis. Racine d'essai toujours vide.

Arrêt ciblé de 3208 et descendants vérifiés par parenté/chemin : 24676, 3840,
28792, 32464, 15356, 15636, 31400. Vérification suivante : tous absents.
Aucun navigateur utilisateur arrêté. Candidat gardé pour reprendre directement
le replay lorsqu'il n'interfère pas avec l'utilisateur, sans nouvelle compilation.
Ce problème d'interaction ne bloque pas les autres travaux Gate 2 autorisés.

## État d'eau et fin de session — lot suivant

Statut réactif au-dessus de la vue : préparation en attente du hash terrain,
installation côté moteur (explicitement pas une validation GPU), échec avec
raison et action de reprise. Les erreurs invalident les lots en attente ; les
anciennes eaux sont retirées plutôt que présentées comme concordantes. Pas de
retry par frame : « Relire la session » déclenche une nouvelle préparation.
L'abonnement UI est retiré au démontage et une mise à jour tardive ne panique pas.

`authoring_water::clear` retire les seules ressources possédées, remet le hash
à None, accepte un monde sans terrain ou stockage déjà retiré, et est idempotent.
Le Studio traite la demande de nettoyage avant la garde de projection, donc
même après invalidation de la vue. Aucun changement des recettes/archives.
Le candidat conservé 133FE370 précède ces changements ; pas de nouveau build
graphique ni interaction Windows pour ce lot. Qualification visuelle encore due.

Trois tests moteur ciblés PASS (342 hors filtre), 0,12 s d'exécution après
1 min 42 s de compilation. Nouveau cas : retrait sans TerrainRuntimeState,
deux appels successifs, compteurs d'assets nuls et entité étrangère conservée ;
appel sur monde vide accepté. Les deux régressions couvrent échange/retrait et
rejet de tête/terrain périmés. Clippy moteur lib/tests PASS (6,36 s), Studio WASM
PASS (4,68 s), après correction d'un point-virgule de style. Formatage et diff
contrôlés. Aucun processus graphique démarré.

## Placement direct des points — lot suivant

Bouton « Placer X/Z dans la vue » par point, via le ray picking terrain existant.
Rayon 2 m utilisé pour le contour seulement ; le résultat est l'intersection
exacte. Niveau Y, largeur, profondeur et autres points restent inchangés.
Hash de tête et brouillon comparés au retour du clic, erreur explicite si périmés.
Terrain indisponible/playtest refusés ; Échap/sortie de vue via le mécanisme commun,
bouton Quitter et nettoyage au démontage. Aucun Apply implicite.

Insertion au milieu du segment suivant : moyenne finie des cinq paramètres,
voisins/textes inchangés, 128 points maximum, dernier segment absent refusé.
Annule le picking avant insertion. Les validators/preflight existants demeurent
l'autorité lors de l'examen du tracé. Aucun changement de format ni de writer.

Clippy Studio WASM strict PASS, 3,17 s pour picking puis 2,91 s après insertion.
Formatage/diff contrôlés. Pas de nouveau test de domaine (contrats inchangés),
pas de build/capture/interaction Windows pour ce lot. Ces gestes UI ne sont pas
encore qualifiés. Le candidat 133FE370 conservé ne contient pas ce lot ni le
statut d'eau précédent ; il reste utile uniquement pour le panneau initial.

## Barrière préalable au playtest — lot suivant

`authoring_water::ready` vérifie identité de tête, terrain attendu et terrain
accepté, liste exacte ordonnée d'IDs, existence des entités/meshes/matériaux/image,
liaison des composants aux bons handles, texture de berge et transforms identité.
Un hash de préparation seul ne suffit plus à ce contrôle. L'état garde le hash
terrain et les IDs de recettes ; clear remet les identités à None.

Cette barrière est CPU/ECS : elle ne prouve pas l'upload GPU ni une physique de
l'eau. Pas encore appelée par le playtest : son snapshot forge_project fige le
terrain canonique et refuse actuellement toute rivière. Le raccord doit
distinguer identité canonique et projection creusée, construire l'identité
végétale composite, attendre cette barrière puis utiliser le collider creusé.
Le refus existant n'a pas été supprimé ; aucun lancement de jeu rivière déduit.

Test ciblé PASS (1, 345 hors filtre), 0,05 s après 1 min 02 s de compilation :
absence de lot, mauvais hash tête/terrain/ID/nombre, transform modifié, mesh retiré
puis restauré, entité supprimée et clear. Clippy moteur lib/tests PASS (4,95 s),
Studio WASM PASS (48,86 s incluant attente du verrou Cargo pendant le test).
Le temps d'attente ne représente pas une durée d'exécution des tests. Pas de GUI,
de nouvelle acquisition ou de changement de snapshot/format.

## Raccord playtest — lot suivant

PlaytestSnapshot fige désormais les recettes rivière dans un champ privé sans
désérialiseur (getter read-only). Le hash et terrain du snapshot natif restent
canoniques. from_state vérifie toujours document, hash et identité de la réponse.
Le refus global est levé ; une recette n'atteste aucune représentation dérivée.

Studio, après cette validation, dérive une identité de projection distincte à
partir du terrain creusé. La végétation passe par from_documents_with_waterways
(chemins + rivières). La barrière attend terrain, végétation, objets/collisions,
chemins et authoring_water::ready. Toute zone agrégée attend l'eau, même vide,
afin qu'une suppression de rivière soit projetée avant simulation. Ancien mode
terrain seul inchangé. Le plugin de terrain/joueur existant reçoit le terrain
creusé, avec sa construction de collider et son calcul de position de départ ;
pas de nouvelle simulation ni physique de l'eau, nage non implémentée.

Tests project : 2 authored_rivers PASS (0,06 s), snapshot conservé après Undo ;
zone_host enrichi PASS (0,04 s final) : Prepare/Start/Stop avec recette, protection
des mutations pendant le jeu, réponse falsifiée refusée et fichier intact.
Première extension de test oubliait de rouvrir la session fermée par le helper
précédent : précondition corrigée, aucun comportement produit assoupli.
Clippy project lib/tests PASS (0,38 s), Studio WASM PASS (7,26 s), formatage/diff
contrôlés. Deux clones de test remplacés par from_ref pour Clippy.

Non qualifié : aucune nouvelle compilation graphique ni marche sur lit/berge,
aucune preuve Rapier en scène rivière et aucun verdict GPU/performance. Les
tests host portent sur recettes et gel de sources, pas leur rendu. Le candidat
133FE370 précède ce raccord ; il ne prouve pas ce playtest. Gate 2 reste ouverte.

## Replay C4C14623 et correction du panneau

13 septembre : Trunk PASS (Rust 1 min 26 s), Tauri PASS (33,17 s).
Frontend `E:/OpenRPG_Forge/.gate2-evidence/studio-authoring-web-river-playtest-20260913`,
config `E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring-river-playtest-20260913.conf.json`.
EXE SHA256 `C4C1462355A52D2738621AB253EFFB49C9944AF0702DD274B898446AD414BE2F` ;
WASM `1E4C8374452E63B983841B8A59C8EF9F391979FC170DAC234AFA4A5012D2AD4C`.

PID 13392 à 19:50:54, fenêtre 6033238, déplacée/agrandie sur l'écran secondaire.
Création plane via UI puis Save, examen/conversion vers zone unifiée, rendu du
terrain et état « Eau : 0 rivière(s) installée(s) » observés. Ouverture du panneau
révèle des libellés contenant le code des comparateurs et 128 points au lieu de
4 : attributs `disabled=move || ... >= ...` sans bloc, parsing view! ambigu.
Clippy avait accepté cette expansion. Le brouillon seul est affecté ; aucune
commande rivière soumise. Conversion sauvegardée, « Session synchronisée » acquis.

Archive `E:/OpenRPG_Forge/.gate2-evidence/authoring-rivers-ui-20260913/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`
version 1/head 1, zéro rivière, SHA256
`7583AACD170763F9821915F61CA218E2026C5DDF7FA40F24B784B37EED6CE01E`.
Elle permet de reprendre directement sans refaire création/migration.
Captures inspectées dans la conversation, pas de PNG durable.

Correction : blocs explicites autour des trois conditions de taille/segment du
panneau rivière et de la condition similaire d'insertion médiane du chemin.
Clippy Studio WASM final PASS (2,76 s), formatage/diff contrôlés. Recherche ciblée
des autres comparateurs ; celui de l'historique est déjà dans un appel parenthésé.
Correctif pas encore inclus dans un candidat graphique : pas de réussite de
création rivière, save/reopen rivière, picking ou playtest revendiquée.

Fermeture Alt+F4 et absence vérifiée de 13392 et descendants 20808, 28280, 27216,
26464, 14208, 8372, 18252. Aucun processus utilisateur arrêté. Prochain replay :
ouvrir cette zone convertie, contrôler quatre points stables et boutons, puis
reprendre Examine/Apply/Save/Open/Jouer. Ne pas refaire la migration acquise.

## Replay 15C48C0E — révision dérivée de l'eau

13 septembre : frontend corrigé présent, Tauri build PASS (15,11 s).
EXE `15C48C0EE617ECFAC59EC030B5134B285BC72CC09BEB8D7C12C560F12F64086D`,
WASM `A2BF852F1CCC4AAC0D1F09277FDB28355ADB132410C2DE41C17615A15C38432D`.
Racine frontend/config `studio-authoring-web-river-ui-fix-20260913` /
`tauri-authoring-river-ui-fix-20260913.conf.json` sous `.gate2-evidence`.
Zone convertie rouverte, panneau à quatre points stables, libellés propres,
retrait désactivé à quatre points et insertion après le dernier désactivée.
Examen acquis : proposition à quatre points, aperçu de relief 247/247 points.

Apply accepte la recette mais révèle une erreur renderer : terrain révision 2,
eau révision 1. Le contrôle moteur n'est pas affaibli. `river_projection::project`
aligne maintenant la seule copie dérivée d'eau sur la révision effective du
terrain, comme la végétation effective. La recette canonique reste intacte.
Test de régression moteur ajouté pour les révisions 2 puis 3 et la non-mutation.
Qualification du correctif en cours ; pas de rendu/playtest rivière acquis ici.

Test ciblé `preparation_projects_current_revision_without_rewriting_recipe`
PASS (0,03 s, compilation 1 min 35 s). Clippy moteur/lib/tests PASS (37,15 s).
Le contrôle de paire eau/terrain reste strict. Texte obsolète du panneau de
migration actualisé : playtest soumis aux projections/collisions concordantes.

Save via Studio a conservé une rivière dans l'archive existante : archive 10,
tête 4, révision 2, recette eau révision 1 ; SHA256
`3E1860AAE183907B61AB50C149A5B7435D2B12A845D2A6825DE4F92E1E254B1A`.
L'erreur de projection reste visible après Save ; le fichier prouve l'écriture,
pas un affichage réussi. Reprendre par Open après correction, sans recréation.
Captures inspectées dans la conversation, pas de PNG durable. Essai PID 8700
fermé ; absence vérifiée de 8700, 6880, 18948, 30252, 25560, 2572, 10792, 29896.

## Candidat B2E07599 conservé — contrôle interrompu

Trunk PASS (Rust 1 min 22 s), Tauri PASS (21,38 s). Avertissement transitif
de compatibilité future `proc-macro-error2 2.0.1`, pas un échec de compilation.
EXE `B2E07599BEC48DBDF4CF8B5444CBDFEA20005FEBEB83DB1BE9CBF74E477AA5B5`,
WASM `86D33BCAF992B79F2EA2438CEB38E2DFBE35FFF3CF481F0316F0529D915E0EFA`.
Frontend/config `studio-authoring-web-river-revision-20260913` /
`tauri-authoring-river-revision-20260913.conf.json` sous `.gate2-evidence`.
Lancement PID 4784 à 20:18:32 ; contrôle arrêté par Échap avant sélection de
fenêtre, aucune réouverture ou interaction de zone effectuée. Nettoyage vérifié
des processus possédés 4784, 23228, 10692, 28240, 28444, 32096, 12844, 16992.
Le binaire est disponible pour reprendre la sauvegarde existante ; sa présence
ne qualifie ni le rendu corrigé ni le playtest. Aucun relancement automatique
de cette interface après l'interruption ; travail non graphique poursuivi.
