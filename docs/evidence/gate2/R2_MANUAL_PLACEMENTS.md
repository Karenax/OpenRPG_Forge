# Placements manuels persistants — Gate 2

## État du 13 septembre 2026

Implémenté dans la tête de zone et les commandes existantes. Raccord Studio et
projection persistante ajoutés ensuite ; leur qualification est décrite séparément
ci-dessous, sans étendre rétroactivement les preuves du domaine.

- `ManualPlacement` : EntityId stable, nom borné, AssetId et hash de l'artifact,
  transformation Forge existante (mètres, quaternion unitaire, échelles positives),
  verrou de l'ensemble du placement. Aucun Entity Bevy ni chemin fichier.
- `prepare_placement` prépare Insert/Transform/SetLocked/Remove sur la tête exacte.
  Les données sont triées par ID, uniques et bornées à 4 096 placements, dans la
  limite globale de 4 MiB. Un nouvel ID est fourni explicitement : ni index de
  liste, ni position, ni nom affiché ne remplace l'identité.
- Preview/Apply, retry, inverse, audit, sauvegarde atomique et récupération sont
  ceux de la session agrégée. La session vérifie les verrous même pour une commande
  Edit directe. Déverrouiller conserve les autres propriétés ; modifier et
  déverrouiller simultanément un placement verrouillé est refusé.
- Les placements manuels restent dans les corrections, donc hors de la base
  remplacée par une régénération de terrain/végétation. Ils restent présents même
  non verrouillés. Ceci ne prouve pas encore les overrides d'une porte générée.

## Format et compatibilité

Champ optionnel `ZoneCorrections.placements`, omis quand absent : anciennes données
et hashes préservés. Une insertion prévisualisée explicite inaugure une tête v2.
Some(empty) conserve v2 ; Undo du premier ajout peut restituer v1. Une tête v1
portant des placements, ou v2 sans le domaine, est invalide.

Une archive ayant enregistré ces nouvelles corrections est v4 en encodage intégral
ou v5 en compact, même si Undo retire ensuite tous les placements : le journal doit
rester lisible. Archives v1/v2/v3 inchangées. Relecture vérifie version, commandes,
inverses/digests et tête ; une archive v4/v5 déguisée en v2/v3 est rejetée.
Aucune conversion à l'ouverture et aucun fichier utilisateur modifié par ce lot.

## Vérifié

- 3 tests `manual_placements` PASS : Preview sans mutation, retry, versions,
  Undo/Redo, verrou, refus de contournement, NaN, IDs triés/uniques, transformation
  exacte, conservation verrouillée/non verrouillée après régénération, relecture
  intégrale/compacte et refus de version falsifiée.
- Test `zone_host` enrichi PASS : placement sur tête agrégée, Preview/Apply,
  ancien fichier inchangé avant Save, écriture v5, Close/Open avec placement exact
  et état synchronisé ; refus du playtest sans projection des placements.
- Tests `zone_authoring` (3), `zone_regeneration` (3), `zone_history` (2) PASS.
- Clippy project/all-targets et Studio/lib WASM/web avec `-D warnings` PASS.
  Cargo conserve le signalement de compatibilité future de proc-macro-error2.
- Historique : résumé count/hash ajouté ; rendu du nouveau libellé non qualifié.

## Suite et limites

### Actions de zone regroupées pendant le défilement

Composant `CommandBar` extrait dans `authoring_controls.rs` : mêmes callbacks
Apply/Undo/Redo/Save/abandon, mêmes signaux et gardes. Aucun second bus ni état
canonique. Placé au début des outils, avec CSS sticky limité à l'espace auteur,
fond opaque, deux colonnes et hauteur bornée des sorties longues. Diff et état
de session accompagnent les actions ; résumé vide masqué. L'objectif est de
ne plus remonter toute la colonne après manipulation d'un objet ou du terrain.

Clippy Studio WASM strict PASS (5,37 s) après extraction du composant pour
respecter la limite de taille des fonctions ; formatage PASS. Lecture des
callbacks et gardes effectuée. Position sticky, focus, lisibilité des erreurs
et absence de recouvrement encore à vérifier dans le candidat suivant. Aucun
nouveau rendu ni replay de sauvegarde lancé par ce changement de présentation.

### Inspecteur unique et sélection explicite

Après le replay 09ea75b6, liste « Objet placé à inspecter » ajoutée au panneau.
Nom, ID et position distinguent les copies. Liste validée contre la tête courante
avant sélection ; même sélection partagée que le picking 3D. Sans sélection,
aucun inspecteur ; avec sélection, seul cet ID est monté. L'ancien affichage
de tous les inspecteurs à la suite est remplacé par « Fermer l'inspecteur ».
Ajout d'un asset replié dans une section dédiée. Changer d'objet annule les
outils transitoires et l'aperçu d'asset, sans commande de mutation de la zone.

Clippy Studio WASM strict PASS (7,73 s), formatage PASS. Vérification de code :
filtre `is_some_and`, ID recherché dans les placements de la tête et annulation
partagée des outils ; aucune qualification graphique ni mesure de performance
déduite. Le ciblage incohérent de l'essai précédent n'est pas déclaré résolu.
Changement volontaire de parcours : choisir l'objet avant de le cadrer/manipuler.

### Candidat regroupé 09ea75b6 — réouverture acquise, gestes non qualifiés

Trunk/WASM PASS (compilation Rust 3 min 12 s), assemblage web avec le kit local
gouverné puis Tauri debug/no-bundle PASS (1 min 04 s). Exécutable SHA-256
`09EA75B6C05991A52504C61CAE45DA220B80E863E73B9E5E6D794D91CF18F45A`.
Inclut les repères locaux/mondiaux, pas angulaire, saisie locale et pivot partagé.

Lancé à 15:17:15 le 13/09/2026, PID 17004, configuration écran secondaire,
sur `authoring-delta-ui-projects-20260913`. Catalogue de zones : une sauvegarde
disponible, ouverte depuis le Studio. Terrain affiché au hash
`7d816331a3647dd0edd03b9aecd6437c75fa471e34f19966c8fe0150d6ff6be2`,
deux arches visibles dans la vue générale. Panneaux de précision présents.
Cette observation ajoute la réouverture UI v6, pas une qualification des gestes.

Limite d'essai : coordonnées de commandes hors fenêtre signalées après défilement
et un clic prévu sur Cadrer ouvrant Position précise. Aucun verdict sur le
comportement des gizmos ; la cause du ciblage n'est pas établie. Aucun Apply/Save
effectué. Session fermée par Alt+F4, liste des fenêtres Studio vide ensuite.
Archive inchangée : `FC2DBCAA59F8D6F1222C777ABDC8B0B1CA5C87FA9195769BF81AEC74D15F6F4F`.
Pas d'autre candidat ni benchmark lancé. Gate 2 ouverte.

### Pivot partagé entre saisie et anneaux

Le panneau de pivot est désormais unique par objet, utilisé par la rotation
numérique et l'activation des anneaux. Valeurs validées avant activation, point
capturé dans le mode de rotation. Centre du dessin, distance caméra servant à
dimensionner les anneaux, picking et origine du rayon utilisent ce même point,
fixe pendant le geste. Le résultat passe par le même `rotate_about` que la saisie.
Le repère initial local/mondial et le pas angulaire restent compatibles ; la
restauration complète du Transform lors d'une annulation reste inchangée.
Translation et échelle ne sont pas réinterprétées autour de ce point.

Clippy Studio WASM strict PASS (4,92 s), formatage PASS. Aucun domaine modifié
par ce raccord ; les preuves géométriques précédentes ne qualifient pas le
dessin ou le geste. Replay regroupé du pivot externe et Save/Undo encore dus.
Les paragraphes historiques ci-dessous décrivent leurs états antérieurs.

### Pivot de rotation numérique explicite

Panneau numérique : choix du pivot courant de l'objet ou d'un point monde X/Y/Z
en mètres. La règle `placement_pivot::rotate_about` applique une rotation rigide
de position et d'orientation autour de ce point ; échelle conservée. Axe local
issu de l'orientation initiale, indépendant de l'échelle. Pivot propre et tours
complets réutilisent exactement le résultat de la rotation existante. Valeurs
non finies et dépassements refusés. La transformation finale seule passe par
la commande et l'historique existants ; le réglage ne modifie pas le pivot GLB
et ne crée pas une charnière persistante de gameplay.

Deux tests géométriques PASS : arc autour d'un point décalé, orientation et
échelle, inverse et tour complet, point invalide, axe local après rotation
initiale et équivalence exacte au pivot propre. Première compilation corrigée
après références à Vec3::X/Z absentes du type Forge. Clippy Studio WASM strict
PASS (26,05 s), formatage PASS. Parcours UI/Undo/Save du pivot externe non rejoué ; les gizmos
continuent d'utiliser le pivot de l'objet, explicitement indiqué dans le panneau.

### Saisie numérique du déplacement local et équivalence de commande

Panneau « Déplacement local précis » raccordé : axe X/Y/Z, distance relative en
mètres, paramètres grille/surface communs aux poignées. Source relue sur la tête
courante ; même `translate_local`, refus de la surface et du résultat inchangé,
puis commande Transform/Preview existante. Verrou respecté côté domaine, bouton
désactivé côté UI. Aucun cumul avant Apply, aucune mutation directe de fichier.

Deux tests `placement_transform_authoring` PASS (12,18 s de compilation) :
contrat précédent conservé et nouveau cas comparant rayon déjà converti dans le
repère initial / saisie numérique avec grille, objet tourné et échelle non uniforme.
Même transform exacte, Preview sans mutation, retry identique, Undo/Redo, verrou,
régénération de base et relecture v6 avec tête/audit exacts. Ce scénario CPU ne
teste pas la conversion du rayon dans l'adapter ni une souris réelle.
Clippy Studio WASM strict PASS (4,93 s), formatage PASS. Panneau/geste et comparaison
de bout en bout R2-051 encore à qualifier ; aucun rendu relancé.

### Translation locale — implémentation

Choix mondial/local dans le panneau. En local, dessin des axes, sélection et
rayon utilisent l'orientation initiale ; le domaine `placement_axis::translate_local`
applique une distance en mètres indépendante de l'échelle de l'objet. Le pas
de grille existant quantifie cette distance depuis le pivot initial sur l'axe
sélectionné, et non les coordonnées mondiales. Position finale toujours absolue
dans la commande Transform existante. Aucun format, cache canonique ou dépendance
supplémentaire. L'alignement surface est refusé avant activation en mode local :
projeter ensuite Y pourrait sortir de l'axe choisi. Le mode mondial conserve ses
règles existantes grille X/Z et surface ; l'interface explique cette distinction.

Test `local_translation_uses_orientation_not_scale_and_snaps_relative_distance`
PASS : objet tourné de 90 degrés avec échelle non uniforme, distance arrondie,
rotation/échelle conservées, retour inverse, zéro exact, paramètres invalides,
équivalence des trois axes au repère mondial pour une orientation identité.
Clippy Studio WASM strict PASS (25,85 s), formatage PASS ; geste et annulation
UI encore dus. Avertissement de compatibilité future proc-macro-error2 conservé.

### Repère local/mondial des anneaux — implémentation

Choix explicite dans le panneau des gizmos. Le repère initial de l'objet est
partagé par le dessin des anneaux, leur sélection et la conversion du rayon
caméra. Il reste fixe pendant le geste pour ne pas réinjecter la rotation de
l'aperçu dans le calcul. Le rayon est recentré au pivot puis converti par la
rotation inverse, sans échelle ; une échelle non uniforme ne déforme pas le
plan d'interaction. La composition finale réutilise `rotate` avec le même
`RotationSpace` que le panneau numérique. Le pas angulaire fonctionne dans les
deux repères. Position et échelle ne sont pas modifiées.

Clippy Studio WASM strict PASS (4,83 s), formatage PASS. Aucun domaine ni format
persistant modifié ; les tests historiques de composition locale/mondiale ne
prouvent pas le picking de ces anneaux. Affichage, sélection et geste sur objet
incliné avec échelle non uniforme encore à qualifier dans le prochain replay
regroupé. Translation locale encore non implémentée ; Gate 2 ouverte.

### Alignement angulaire des anneaux — implémentation du 13 septembre

Sélecteur Libre / 1 / 5 / 15 / 30 / 45 / 90 degrés raccordé aux anneaux.
Le réglage est capturé à leur activation ; changer le sélecteur exige de
réactiver l'outil. La règle moteur-neutre `placement_rotation::snap_degrees`
arrondit le déplacement angulaire depuis la transformation initiale, jamais
depuis la frame précédente. Même résultat pour l'aperçu et le relâchement,
puis même commande Transform, Preview/Apply et historique que la rotation libre.
Arrondi des demi-pas à l'opposé de zéro ; pas choisis divisant 180 degrés pour
préserver l'orientation au passage +/-180. Valeurs non finies et pas inconnus
refusés avant activation. Aucun nouveau format persistant ou dépendance.

Test ciblé `angular_snap_is_relative_symmetric_and_rejects_invalid_settings`
PASS : arrondi positif/négatif et demi-pas, mode libre, passage +/-180 pour
chaque pas, refus des réglages invalides/non finis. Clippy Studio WASM strict
PASS (30,25 s), formatage effectué. Avertissement de compatibilité future
`proc-macro-error2` conservé. Affichage et geste avec alignement non rejoués.
Ce lot ne qualifie pas les axes locaux de rotation ni les autres modes gizmo.

Nettoyage vérifié au tour suivant : PID Studio 15356 absent et aucun des
descendants graphiques enregistrés encore présent. PID 31312 réutilisé par
`cargo-clippy.exe`, donc non arrêté. Aucun arrêt forcé effectué. Hash de
l'archive v6 toujours `FC2DBCAA59F8D6F1222C777ABDC8B0B1CA5C87FA9195769BF81AEC74D15F6F4F`.

### Replay regroupé du 13 septembre — conversion acquise, geste interrompu

Candidat Studio SHA-256
`5FC11A29F532C2F0B2DF6C146713F186AB341482A1351C19E5F93D97C61B28E2`,
construit par Trunk puis Tauri debug/no-bundle. Copie de travail isolée :
`E:/OpenRPG_Forge/.gate2-evidence/authoring-delta-ui-projects-20260913`,
zone `81de8d42-89b9-58b5-b090-406cf4c63d06`.

Observé dans le Studio : examen du journal, conversion explicite sans écriture,
puis Sauvegarder. Le fichier v5 était inchangé avant Save. Après Save, puis à la
reprise après interruption, lecture disque : version 6, 13 commandes, 2 560 237
octets, SHA-256 `FC2DBCAA59F8D6F1222C777ABDC8B0B1CA5C87FA9195769BF81AEC74D15F6F4F`,
tête `c5674eeaa0b4470b4a937338c6bed4c6897cd02ec56172265574afad49fb6088`.
La conversion UI et sa sauvegarde sont acquises ; aucune réouverture UI v6
n'est déduite de cette lecture disque.

Sur l'arche `63a0e640-ab20-53be-b3d7-7007135019e3`, la poignée Y était visible.
Son glissement a produit une proposition non appliquée, avec l'arche relevée
dans l'aperçu final. Captures avant/après seulement : continuité du geste non
qualifiée. Apply, Undo/Redo et Save/relecture de ce déplacement n'ont pas été
effectués. Rotation, échelle, snapping et cas négatifs restent à qualifier.

Le contrôle graphique a été interrompu par Échap. Aucun nouveau contrôle UI
n'a été lancé à la reprise. Le processus d'essai 15356, identifié par son chemin
Forge et sa création le 13 septembre à 14:43:09, était encore vivant au relevé
de reprise ; ne pas annoncer son nettoyage comme effectué. Aucun déplacement
supplémentaire enregistré sur disque, aucune publication Git. Gate 2 ouverte.

### Poignées d'échelle locale — implémentation non qualifiée graphiquement

Troisième mode du même gizmo, avec marqueurs cubiques et axes orientés comme
l'objet. Rayon caméra converti dans l'orientation initiale, sans division par
les échelles de l'objet. Facteur = 1 + déplacement axial / longueur de poignée
capturée au départ. Le facteur s'applique à l'échelle initiale de l'axe sélectionné,
jamais au résultat de la frame précédente ; les deux autres échelles restent intactes.
Pivot et rotation conservés. Zéro, négatif, non-fini et overflow refusés sans miroir.
Le cycle de restauration complète et la commande de fin sont communs aux trois modes.

Test `axis_scaling_keeps_pivot_orientation_and_other_scales` PASS sur transform
non identité et échelle non uniforme ; Clippy world/lib/tests et Studio WASM strict
PASS. Aucun rendu lancé : orientation/picking des axes locaux et manipulation
réelle restent dus. R2-029 reste ouvert, pas de verdict de qualification graphique.

### Anneaux de rotation — implémentation non qualifiée graphiquement

Trois anneaux d'axes mondiaux, 48 segments chacun, dessinés par le même renderer.
Picking par distance au segment projeté (12 pixels logiques), puis intersection
du rayon avec le plan de rotation. Composition depuis la transformation de départ,
pas depuis chaque frame précédente ; pivot et échelles conservés. Rotation du root
dérivé durant le geste, puis même Preview/Apply au relâchement. Restauration complète
translation/rotation/échelle lors de l'annulation, avec garde du transform canonique.

`placement_rotation` porte les calculs indépendants du renderer : rayon non fini,
plan derrière caméra, vue de profil et point au pivot refusés. Delta angulaire
équivalent au passage ±π ; pas de compteur persistant de tours. Test ciblé PASS
(quart de tour Z, passage +179°/-179°, cas invalides). Clippy world/Studio WASM strict PASS.
Pas de snapping angulaire ni d'axes locaux pour ces anneaux. Aucun rendu lancé :
picking, profondeur/occlusion, fluidité et annulation réelle restent à qualifier.
Ce code ne clôture pas R2-029 ; poignées d'échelle encore dues.

### Alignement des poignées de translation

Les réglages existants de Position précise sont capturés à l'armement du gizmo.
Plan de terrain effectif construit une seule fois, uniquement si une option est
active. Le même `PlacementSnap::resolve_axis` s'applique au déplacement dérivé
continu et à la valeur finale transmise au Preview, sans second calcul UI.

Pour une poignée X/Z, seule la coordonnée horizontale active est quantifiée ;
l'autre reste exactement conservée. Le suivi de surface échantillonne ensuite
le terrain aux coordonnées finales. Y reste libre sans suivi de surface ; Y et
suivi de surface simultanés sont refusés explicitement. Pas de contrainte persistante.
Test ciblé PASS sur terrain retouché : X/Z inactifs intacts, Y libre, surface finale,
conflit vertical refusé. Clippy world et Studio WASM strict PASS. Geste/rendu graphique encore dû,
donc R2-031 non clôturé.

### Poignées de translation — implémentation non qualifiée graphiquement

`placement_axis` calcule le point le plus proche entre rayon avant de caméra et
axe mondial passant par le pivot. Rayons invalides, résultat derrière la caméra,
overflow et axes quasi parallèles (sin² < 0,01) refusés. Déplacement = différence
des paramètres depuis le début, pas déplacement du pivot sous le pointeur.
Test mathématique ciblé PASS ; Clippy world/lib/tests strict PASS.

Adapter Bevy/Studio : poignées X rouge/Y verte/Z bleue, sélection sur segment projeté
(tolérance 12 pixels logiques, axes projetés < 20 pixels ignorés), taille dépendant
de la distance caméra, bornée entre 0,5 et 20 mètres. Root dérivé déplacé pendant
le geste, pas de nouvelle archive ni commande par événement. Relâchement prépare
une unique retouche liée à la tête d'origine ; Apply/Undo/Save restent les commandes
existantes. Un aperçu de placement en attente interdit l'armement. Échap, blur,
sortie de vue, autre outil ou nouvel état accepté annulent. File d'événements ≤ 64,
saturation annule. Restauration protégée par identité ET transform canonique pour
ne pas écraser un nouveau root créé par une autre proposition.

Clippy Studio/lib WASM/web strict PASS. Aucun essai graphique : la compilation ne
prouve pas les coordonnées de picking, la profondeur/occlusion des traits, le rendu
du déplacement continu ni l'annulation au focus. Qualification UI groupée requise
avec le journal v6 et les nouveaux contrôles numériques. Snap sur ces poignées,
axes locaux et poignées rotation/échelle restent à réaliser ; R2-029/R2-031 ouverts.

### Glissement horizontal relatif — 13 septembre 2026

`placement_transform::drag_xz` conserve le décalage entre pivot et point de saisie,
l'altitude, la rotation et l'échelle. Coordonnées non finies et overflow refusés.
Dans l'inspecteur, bouton de glissement terrain réutilisant la collecte de geste
existante : premier/dernier point, aperçu au relâchement puis Apply, une commande.
Options grille/surface capturées au départ ; tête de zone identique exigée à la fin.
Verrou, geste vide et absence de déplacement refusés. Échap/blur/sortie annulent
via le pont de pointeur existant. Aucun nouveau chemin de mutation/persistance.

Test mathématique ciblé PASS, test intégré `placement_transform_authoring` enrichi
du déplacement PASS (Preview/retry/Undo/Redo/verrou/régénération/relecture v6).
Clippy world et Studio WASM strict PASS. Aucun processus graphique lancé : annulation
DOM, geste réel et rendu restent à qualifier. Ce geste utilise le terrain comme
plan de saisie ; ce n'est pas encore une poignée d'axe ni une prévisualisation
continue de l'objet. R2-029/R2-031 restent ouverts.

### Rotation et échelle numériques — 13 septembre 2026

Règles partagées `forge_world::placement_transform` ajoutées : composition de
rotation autour d'un axe X/Y/Z local ou mondial, toujours autour du pivot propre.
Aucune conversion Euler de l'orientation existante, translation et échelle
non uniforme conservées. Échelles locales absolues strictement positives ; pas
de miroir/cisaillement. Sources invalides refusées, pas de normalisation silencieuse
d'une source invalide. La composition validée est renormalisée pour éviter la dérive.

Inspecteur raccordé à PreviewZone/PlacementEdit::Transform existants, sur la tête
courante : angle relatif, axe/espace, échelles X/Y/Z. Plusieurs previews ne cumulent
pas l'angle ; seule l'application change la base. Verrous et sauvegarde restent
contrôlés par le domaine. Aucun nouveau format ni nouvelle dépendance.

Deux tests mathématiques PASS : ordre local/mondial sur orientation non identité,
inverse, tour complet sans changement, pivot et échelles conservés, entrées invalides.
Test intégré `placement_transform_authoring` PASS : Preview sans mutation, retry,
Undo/Redo exact, verrou, régénération puis relecture v6 avec tête/audit identiques.
Clippy world/lib/tests et Studio/lib WASM/web strict PASS (avertissement de compatibilité
future tiers proc-macro-error2 conservé). Affichage et manipulation Studio non essayés,
aucun nouveau processus graphique lancé. Ce panneau ne remplace pas les gizmos
translation/rotation/échelle de R2-029, qui restent à implémenter/qualifier.

### Correction du journal saturé — archive v6

Encodeur typé de différences par champ de corrections, base = précédent Edit
enregistré, pas tête après Undo. Keep/Set explicites, Set(None) conservé. Décodeur
reconstruit chaque ZoneRequest puis vérifie révision, verrous, inverse et hash du
reçu complet et de la tête finale. Requêtes hors Edit conservées sans transformation.
Refus de doublons de commande, de hash altéré, de contenu altéré et de Full Edit
dans la variante Other. Taille fichier 4 Mio inchangée ; commandes ≤ 1 024 et
cumul des requêtes dépliées ≤ 64 Mio en lecture ET écriture. Ce plafond ne borne
pas à lui seul toute la mémoire des reçus dérivés/timeline ; qualification encore due.

`review_compaction` propose v6 aussi depuis v3/v5 ; conversion sans disque puis
Save explicite avec la session fichier existante. Revue périmée refusée. API
historique `compact_archive_bytes` conserve v3/v5 ; lecture ancienne n'écrit ni
ne convertit. Aucun journal effacé ni migration des documents eux-mêmes.

Preuves : test synthétique delta PASS (audit complet, Undo/Redo, représentation
stable, taille inférieure à moitié de v3 sur fixture, refus de falsifications).
Test host PASS : revue/convertir sans écriture, Save v6, réouverture et placements
exacts ; attentes de version actualisées uniquement pour cette conversion explicite.
Quatre tests historiques placements PASS ; Clippy project/all-targets strict PASS.

Replay local opt-in en lecture seule `zone_delta_archive::real_studio_archive_can_continue_without_losing_its_history`
sur `authoring-selection-ui-projects-20260913` : ancien Preview reproduit le refus
4 Mio ; v6 conserve la tête et les 13 reçus à l'identique ; déplacement de la copie
vers (-91,4,84) appliqué puis archive relue avec tête/audit identiques. Source disque
inchangée. Mesures Rust : 3 963 115 → 2 560 237 octets, puis 2 561 438 après retouche.
Test 6,50 s, aucun rendu lancé. Cela ne remplace pas le replay UI d'alignement.

Complément ciblé PASS : Edit, retrait explicite des hauteurs, Regenerate, Undo/Redo,
nouvel Edit puis sauvegarde avec Redo encore disponible et relecture exacte.
Set(None) et le signe de -0.0 sont conservés. Test de frontière PASS : 64 Mio
exactement acceptés, dépassement et overflow usize refusés. Clippy project strict PASS.

À compléter : qualification workspace ADR-0006 et coût mémoire/temps. Conversion et alignement
graphiques restent dus ; pas de nouveau verdict de gate ni publication.

### Candidat v6 construit ; essai graphique empêché

Trunk/WASM PASS (3 min 57 s compilation Cargo), Tauri debug/no-bundle PASS
(1 min 51 s). Exécutable `forge-studio.exe`, SHA-256
`a8344c0d95c29fa8483d52b6688efe28aefba65242c663ebcc3ab4e5779d5b2a`.
Clippy project/all-targets strict PASS ; test frontière expansion et signed-zero PASS.

Lancé avec `FORGE_AUTHORING_ROOT` sur la copie isolée
`E:/OpenRPG_Forge/.gate2-evidence/authoring-delta-ui-projects-20260913`, fenêtre
configurée sur écran secondaire. Activation échouée : `GetCursorPos failed:
Accès refusé. (0x80070005)`. Relecture de fenêtre possible mais capture limitée
au fond d'écran. Aucun clic d'édition effectué, aucune conversion/sauvegarde.
Archive toujours `31fa61357b2c285aebf0d8f3fac9641412b5500aa55d36e44f88987888bd9936`.
Stderr vide. PID 29600 et descendants 24080, 22632, 17968, 16928, 18068, 30436,
19460 arrêtés avec vérification d'identité et d'absence. Aucun processus utilisateur
extérieur à cet essai arrêté. Parcours graphique encore dû : ne pas conclure PASS.

### Replay sélection/alignement — 13 septembre 2026

Trunk et Tauri debug/no-bundle terminés avec succès ; SHA-256 de l'exécutable
`34bca4ce7b795bf550becf551b63df849a971cb161c630e860c692633f0c085d`.
Essai sur écran secondaire, copie `authoring-selection-ui-projects-20260913`.
Ouverture des deux placements sauvegardés, cadrage, outil de sélection puis clic
sur la pile droite de l'arche gauche : ID `b9ed8cc5-3ac5-55cc-946c-23131e28b440`
affiché, liste filtrée sur ce seul placement à (-90, 3, 85.115966796875).
Captures affichées et inspectées dans la conversation. Pas de PNG exporté.

Réglage de grille à 7 m et option surface avec offset +1 m, puis Preview :
**refus** « Zone archive exceeds 4 MiB before mutation ». Aucune proposition
appliquable ni transformation visible, aucune sauvegarde effectuée.
Archive SHA-256 toujours `31fa61357b2c285aebf0d8f3fac9641412b5500aa55d36e44f88987888bd9936`.
Ce résultat ne qualifie pas l'alignement graphique.

Cause inspectée dans `session/compact.rs` : v5 élimine les inverses dérivables,
mais chaque `ZoneRequest::Edit` sérialise encore toutes les corrections, y compris
les masques/material overrides inchangés. Fichier 3 963 115 octets, 13 commandes ;
dernière commande re-sérialisée en JSON compact PowerShell : 387 102 octets,
1 couche de densité et 576 overrides matériaux. Cette mesure auxiliaire n'est
pas un hash canonique Rust. Prochaine correction : encodage sans répétition avec
reconstruction/vérification de chaque reçu, conservation du journal et des verrous,
lecture historique et bornes d'expansion. Ne pas jeter Undo ni augmenter la limite.

Studio PID 29052 et descendants 20132, 24692, 11164, 19480, 31564, 5292, 31592
fermés puis vérifiés absents ; stderr vide. Pas de processus graphique conservé.
R2-028 partiellement prouvé ; clic vide/occlusion et autres objets encore dus.
R2-031 et Gate 2 ouverts. Aucun push, aucune source de preuve précédente modifiée.

### Sélection directe — implémentation du 13 septembre

Outil explicite « Sélectionner un objet dans la vue », clic canvas normalisé,
rayon de la caméra et `MeshRayCast` de Bevy déjà présent via feature `3d/picking`.
Aucune dépendance ajoutée. Les primitives LOD0 qualifiées reçoivent un marqueur
dérivé EntityId ; ni UCX ni LOD masqués ne sont des cibles authorées. Le premier
mesh touché doit porter ce marqueur : les autres meshes visibles consultables
font obstacle, plutôt que de sélectionner un objet derrière eux.

L'inspecteur est filtré sur l'identité sélectionnée ; « Afficher tous les objets »
retire le filtre. Clic sans cible retire la sélection. Échap, sortie/blur canvas,
changement d'outil et démontage du panneau annulent le clic en attente. Une cible
retirée des sources invalide le filtre. Une proposition non appliquée ou projection
périmée refuse la sélection ; aucun appel de commande d'édition ni écriture.

Clippy Studio/lib WASM/web strict PASS après import explicite de l'alias ECS pour
la macro Component. Compilation du candidat regroupant sélection et alignement
lancée ; pas encore de parcours UI observé. Limites à qualifier : occlusion,
arche creuse, hiérarchies GLB, mise à jour après manipulation/suppression et sortie
d'outil. Le raycast teste la géométrie CPU, pas l'alpha des textures, les déformations
shader ou les instances GPU sans mesh individuel. Pas de multi-sélection ni gizmo.
R2-028 et Gate 2 restent ouverts.

Copie isolée prête pour le replay groupé :
`.gate2-evidence/authoring-selection-ui-projects-20260913`, zone
`81de8d42-89b9-58b5-b090-406cf4c63d06`, archive initiale SHA-256
`31fa61357b2c285aebf0d8f3fac9641412b5500aa55d36e44f88987888bd9936`.
La compilation Trunk/Tauri est encore active au dernier contrôle ; aucun nouvel
exécutable déclaré qualifié, aucun Studio graphique lancé pour cette étape.

### Alignement grille/surface — première implémentation du 13 septembre

`forge_world::placement_snap::PlacementSnap` calcule une transformation absolue
sans mutation ni dépendance au renderer. Grille mondiale horizontale d'origine
(0,0), pas fini entre 0,01 et 128 m ; demi-pas arrondis en s'éloignant de zéro,
symétriquement pour les coordonnées positives/négatives. Y n'est pas quantifié.
Le mode surface échantillonne ensuite les triangles du terrain au nouveau X/Z,
puis ajoute l'offset explicite. Hors surface, pas de clamp silencieux.
Rotation et échelle non uniforme sont préservées. Pas de suivi permanent du relief,
de changement de pivot, d'alignement sur une normale ou sur un autre objet.

L'inspecteur propose les deux options indépendantes, désactivées par défaut,
pour déplacer ou dupliquer. Le terrain provient de la tête effective courante ;
le résultat passe par PreviewZone/Apply existants, avec contrôle du verrou et de
révision. Aucun format d'archive ni journal supplémentaire. L'insertion au clic
conserve son ancien comportement ; raccord de grille initiale et gizmos encore dus.

Preuves : trois tests `cargo test -p forge_world --lib placement_snap --offline --locked`
PASS (0,00 s d'exécution) : grille symétrique et mode désactivé ; orientation non
identité/échelle non uniforme conservées ; relief avec delta authoré, altitude
échantillonnée après grille et offset négatif ; pas invalides, NaN et hors-zone
refusés. Clippy world/lib/tests et Studio/lib WASM/web avec `-D warnings` PASS ;
formatage PASS. Diagnostics initiaux de conversion/égalité de floats et mise à
jour de struct inutile corrigés, sans affaiblir les assertions exactes.

Qualification UI de ces options non acquise : le candidat `35580363…` précède
l'alignement. Aucun build graphique ni processus GPU lancé pour ce lot ; preuve
de persistance/Undo spécifique à l'alignement encore à compléter. R2-031 et
Gate 2 restent ouverts, qualification workspace ADR-0006 encore due, aucun push.

Complément suivant : l'insertion au clic propose désormais la grille facultative
et réutilise `PlacementSnap::resolve`, comme l'inspecteur. Le pas et l'offset sont
validés avant toute lecture GLB ; les réglages sont figés pour le geste et la tête
est recontrôlée au clic. L'altitude est calculée au point aligné, jamais à l'ancien
point puis déplacée horizontalement. Paramètres désactivés par défaut, comportement
antérieur sans grille préservé. Aucun changement de format canonique.

Test `placement_snap_authoring` PASS : préparation sur tête exacte sans mutation,
Apply/retry identique, Undo/Redo, puis changement d'altitude du terrain et relecture
compacte avec transformation authorée intacte. Nouvelle commande sur ancienne tête
refusée sans mutation. Ce test complète les trois tests mathématiques ; il ne vaut
pas réouverture disque/UI. Clippy world/lib/tests et Studio WASM strict PASS,
formatage effectué. Aucun processus graphique lancé. Le replay UI groupé des
options d'alignement et l'intégration aux gizmos restent requis pour R2-031.

### Manipulation précise et duplication — étape suivante implémentée

`PlacementEdit::Duplicate` copie une source identifiée vers un nouvel EntityId et
une transformation explicite. L'original reste inchangé, y compris son verrou ;
la copie conserve nom et paire asset/hash mais démarre déverrouillée. Identité
déjà utilisée, source absente et transformation invalide sont refusées. La commande
produit le même ZoneAction::Edit que les autres préparateurs ; aucun nouveau format
de tête/archive ni deuxième journal n'est introduit.

Le panneau de chaque objet contient maintenant X/Y/Z absolus en mètres,
prévisualisation du déplacement et copie aux coordonnées saisies. Orientation et
échelle, y compris non uniforme, sont conservées pendant l'édition de translation.
Les déplacements d'un original verrouillé restent interdits, la duplication ne
déverrouille pas cet original. Ces gestes utilisent Preview/Apply existants.

Quatre tests `manual_placements` PASS, dont le nouveau scénario duplication :
aperçu sans mutation, retry, conservation de la source verrouillée, identité propre,
Undo/Redo, réouverture compacte, source absente, doublon d'ID et NaN refusés.
Clippy project/all-targets et Studio/lib WASM/web strict PASS ; formatage et diff
contrôlés. Le replay `ff785805…` ci-dessous précède ce panneau :
la nouvelle manipulation précise et la duplication UI restent à qualifier sur un
candidat reconstruit. Aucun test graphique relancé pour cette étape de code.

Complément de navigation : bouton « Cadrer cet objet dans la carte », centré sur
les bornes LOD0 effectivement chargées et transformées. Le mode de caméra conserve
la distinction avec l'aperçu individuel jetable ; pas de déplacement de source.
Une référence non prête reste diagnostiquée. Clippy Studio WASM strict PASS ;
Construction du candidat regroupant cadrage, déplacement et duplication terminée.

#### Replay ciblé du panneau — 13 septembre 2026

Clippy Studio/lib WASM/web strict, Trunk et Tauri debug sans bundle PASS.
Exécutable SHA-256 `35580363b1be3e9c0af46317af25cd20f433569537e07df0391931f225e4e43e`.
Même configuration et kit gouverné que le replay précédent. Essai unique sur écran
secondaire dans `.gate2-evidence/authoring-precision-ui-projects-20260913`, copie
de la sauvegarde précédente, sans modifier celle-ci.

Parcours observé : ouvrir la zone sauvegardée, cadrer l'arche à partir de ses bornes
réelles, saisir X = -84 m puis prévisualiser/appliquer le déplacement ; saisir
X = -90 m puis prévisualiser/appliquer une copie. Les captures affichées et inspectées
montrent le cadrage rapproché, le déplacement puis les deux arches simultanées.
Ce sont les objets du monde, pas l'aperçu individuel jetable. Sauvegarde réussie,
état « Session synchronisée », puis fermeture de la zone et du Studio.

- Original `63a0e640-ab20-53be-b3d7-7007135019e3` à X = -84 ; copie
  `b9ed8cc5-3ac5-55cc-946c-23131e28b440` à X = -90. Les deux conservent
  Y = 3, Z = 85.115966796875, rotation identité, échelle unitaire et paire asset/hash.
- Archive inchangée pendant l'aperçu du déplacement (`ff89547b…`). Après Save,
  archive v5 SHA-256 `31fa61357b2c285aebf0d8f3fac9641412b5500aa55d36e44f88987888bd9936`,
  tête `c5674eeaa0b4470b4a937338c6bed4c6897cd02ec56172265574afad49fb6088`.
  Les deux placements exacts sont présents dans la dernière commande sauvegardée.
- Archive de référence `authoring-placements-ui-projects-20260913` toujours
  `ff89547be4dfbf6c9345a2d4e9efe340c51495158a69a08ab63c5d435ed868c0`.
- PID 31548 et descendants 29444, 19544, 22588, 9772, 16268, 27824, 17188
  vérifiés absents après fermeture ; stderr vide. Aucun navigateur utilisateur fermé.

Limites : pas de nouvelle réouverture UI ni Undo/Redo UI dans cet essai ; les tests
domaine correspondants restent des preuves séparées. Aucun benchmark, mesure GPU,
verdict artistique ou qualification de collision. R2-029/R2-030 et Gate 2 ouverts.
Le constat « panneau non qualifié » ci-dessus décrit l'étape avant ce replay.

### Placement Studio et projection — implémentation en qualification

- Bibliothèque raccordée à la session native : sélection d'un asset du catalogue,
  vérification GLB, clic terrain avec le picker existant, altitude sur les triangles
  effectifs, rotation Y, échelle uniforme et offset vertical explicites.
- Le clic prépare PreviewZone sur la tête capturée ; une tête modifiée entre la
  demande et le clic est refusée. Appliquer, Annuler/Rétablir et Save restent les
  commandes existantes. Liste des placements avec verrouillage/déverrouillage et
  suppression prévisualisés. Pas encore de gizmo ni de duplication.
- La proposition puis les objets acceptés utilisent la même projection dans le
  monde Bevy courant et la même source mémoire que l'aperçu individuel. LOD0 du
  kit identifié, transforms Forge convertis sans recentrage ; pas de renderer
  supplémentaire. Abandon restaure les placements acceptés.
- Fichiers uniques chargés séquentiellement et partagés entre placements, budget
  de payload cumulé 256 Mio, délai moteur 30 s. Les résultats obsolètes sont ignorés.
  Ce budget ne borne pas toute la mémoire GPU après décompression. Un changement
  de liste reconstruit encore tous les placements ; réconciliation fine à faire.
- Une erreur de résolution/chargement laisse un diagnostic et ne modifie aucune
  source. Pas de collision manuelle générée ; le garde playtest reste actif.
- Clippy Studio/lib WASM/web strict PASS. Replay graphique en cours, aucune preuve
  d'utilisation réelle encore acquise pour ce raccord à ce stade.

Premier essai sur candidat `4517b415…` : ouverture et catalogue embarqué acquis,
mais placement refusé « pointeur hors terrain ». Cause identifiée dans le code :
curseur demandé 0,5 m, hors contrat partagé 2–128 m. Corrigé à 2 m, sans modifier
le picker moteur ; erreur désormais affichée aussi dans le panneau de placement.
Le premier essai ne prouve donc aucun placement. Archive de la copie inchangée
`0b3bf389…`. Fenêtre fermée, PID 25740 et sept descendants vérifiés terminés.
Le build/replay corrigé est requis avant de qualifier le geste.

### Replay corrigé acquis — candidat ff785805

Trunk et Tauri debug terminés code 0. EXE SHA-256
`ff785805668a9aa97e4de4fbb6f914ba41091064d39efb1e3c7d53adb3488b7b`.
Configuration `.gate2-evidence/tauri-authoring-zone.conf.json`, frontend
`studio-authoring-web-zone` avec le kit copié depuis `studio-authoring-web-assets/forge-kit`.
Manifeste kit SHA-256 `06a4ecb34e03aca33ba410a4378b36b38c272d78610364024dbaa2bf27566c1a`.

Parcours réel, écran secondaire, dans la copie isolée
`.gate2-evidence/authoring-placements-ui-projects-20260913` : ouvrir la zone,
charger le catalogue, choisir `vv-architecture-arch-a`, placer sur le terrain,
observer 0 → 1 objet non appliqué et l'arche dans la clairière, appliquer, sauvegarder,
fermer la zone puis la rouvrir. Arche reprojetée automatiquement sans recharger
manuellement le catalogue ; session synchronisée et position affichée identique.
Captures affichées/inspectées dans la conversation (vue d'ensemble, pas de PNG exporté).

- Placement `63a0e640-ab20-53be-b3d7-7007135019e3`, AssetId
  `9f2a9ead-817a-52a9-a22e-1f190fa62baa`, hash GLB
  `41552424a1f15be548fffbdf78a765a5d486f39440d272f954acccf485d0d759`.
- Translation exacte sauvegardée `(-88.3867416381836, 3, 85.115966796875)` m,
  rotation identité, échelle `(1,1,1)`, non verrouillé.
- Avant Save, y compris après Apply, archive SHA-256 `0b3bf389…` inchangée.
  Après Save : archive v5, 3 189 281 octets, SHA-256
  `ff89547be4dfbf6c9345a2d4e9efe340c51495158a69a08ab63c5d435ed868c0`,
  tête `6388c1867b4699e22060e6252ecebc2b3099fcc019c503c2c3339db619366b7b`.
  Hash inchangé après réouverture ; archive source de référence `0b3bf389…` intacte.
- Fermeture du Studio puis contrôle des PID 30324, 6332, 29824, 17112, 29876,
  9912, 31472, 17700 : tous absents. stderr vide. Aucun autre navigateur fermé.

Ce replay qualifie le parcours de base d'insertion/persistance, pas le panneau
complet : rotation/échelle autres que valeurs par défaut, annulation de proposition,
verrouillage/suppression UI, Undo/Redo UI, cas de chargement négatifs, collisions,
plans rapprochés et performances restent à qualifier. Les preuves domaine séparées
ne sont pas présentées comme des replays UI.

### Résolution de l'asset épinglé — complément du 13 septembre

`AssetBrowser.reference/resolve` fournit maintenant le raccord entre l'identifiant
local du kit et la paire AssetId/hash du placement. Identité déterministe Forge
sur `governed-kit/v1:` et les deux chaînes UTF-8 préfixées par leur longueur en
octets : kit puis identifiant local. Révision, chemin GLB et ordre du catalogue
n'entrent pas dans cette identité. Renommer l'identifiant local ou celui du kit
change donc l'identité ; aucune correspondance approximative n'est recherchée.

Un contenu différent renvoie `ContentChanged` avec les deux hashes ; une référence
absente renvoie `Missing`. Aucun remplacement ou téléchargement implicite.
La résolution ne prouve pas la présence des octets : le Studio enchaîne le même
contrôle SHA-256 et précontrôle GLB autonome qu'auparavant. Vérification individuelle
et aperçu temporaire utilisent ce chemin commun, disponible pour le futur raccord
des placements. Aucun format d'archive supplémentaire et aucune dépendance ajoutée.

Quatre tests AssetBrowser PASS, dont deux nouveaux : stabilité après déplacement,
réordre/révision et round-trip, refus de remplacement de contenu, de kit étranger
et de référence retirée. Clippy forge_assets/all-targets et Studio/lib WASM/web
avec `-D warnings` PASS (avertissement futur proc-macro-error2 inchangé). Aucun nouveau rendu
ni geste de placement qualifié par ces tests ; la qualification workspace reste due.

L'AssetId/hash épingle une référence mais ne prouve pas à lui seul disponibilité,
licence ou approbation. Le geste d'insertion et sa projection sont désormais
raccordés et ont le replay borné ci-dessus. Restent les manipulations précises
gizmo/snap, duplication avec nouvel ID, inspecteur complet et inclusion des
placements/collisions dans le snapshot/playtest.

`PlaytestSnapshot::from_zone` refuse actuellement toute tête contenant des placements
manuels : ne pas jouer une zone amputée de ses objets. Le playtest terrain/végétation
sans placements conserve son fonctionnement antérieur. Pas de preuve graphique
de porte placée, franchissable ou conservée après régénération depuis Studio.
Qualification workspace ADR-0006 encore due, aucun passage de Gate 2 ni publication.
# Sélection et changement de projection — 15 septembre 2026

Complément inspecteur : les événements input/change remontant des champs du
placement invalident désormais le reçu via l'annulation commune. Cela couvre
coordonnées, snap et sous-panneaux (rotation/échelle/pivot/remplacement), sans
écriture canonique. Une nouvelle saisie ne doit pas laisser applicable une
ancienne proposition. Clippy Web/WASM PASS (6,97 s), formatage effectué ;
comportement GUI encore à qualifier, changement absent du candidat 1551BFCA.

Le clic de sélection mémorise maintenant l'époque de la projection au moment
du geste. Le traitement refuse une époque remplacée ou une requête de projection
encore en attente. L'observation d'une nouvelle tête acceptée annule le geste
en attente, même si les IDs se retrouvent dans la nouvelle zone. Enfin, un mesh
touché n'est sélectionné que si son ID appartient encore aux placements acceptés.
Les autres meshes restent des obstacles au rayon : aucun picking à travers eux.

Clippy Studio Web/WASM strict PASS (7,06 s), formatage effectué. Contrôle
statique uniquement : course de clic/changement de zone et occlusion non rejouées
dans le Studio. Aucun nouveau build ni processus graphique ; R2-028 reste ouvert.

## Qualification native groupée R2-028 à R2-031 — 22 septembre 2026

Verdict local : **R2-028, R2-029, R2-030 et R2-031 PASS par automatisation
native CDP**. Candidat figé :
`forge-studio-candidate.exe`, SHA-256
`268B824946C53323C899276BC918BF5348F87E1A8CC2F6B50CB7A7C99B3804F5`.
Le harnais exécuté pour le v14 avait le SHA-256
`83DDBB14498202A3C7AE3E8A71603BFC7BE09189EAEF9F53C248D4584723C1E4`.

Le scénario `placements` acquiert 18/18 sous-cas :

- R2-028, 4/4 : picking physique exact du mesh visible, clic vide, occlusion
  par un bâtiment non-placement et annulation du picker par Échap ;
- fraction principale R2-029, 9/9 : translation monde et locale, rotation monde
  et locale, échelle locale, aperçu transitoire puis Apply exact, annulations
  Échap/mouseleave/blur et changement de tête ;
- R2-030, 1/1 : duplication puis suppression, Undo/Redo, Save, fermeture,
  rafraîchissement du catalogue et réouverture avec identités exactes ;
- R2-031, 4/4 : snap X/Z sur grille mondiale et surface, refus Y/surface et
  refus axe local/surface sans IPC de mutation.

Le verrouillage R2-029 est exécuté séparément dans `placement-lock`, avec une
copie de fixture, un processus Studio et un profil WebView2 frais. Cette isolation
évite de confondre l'instabilité d'une longue chaîne de projections avec un défaut
fonctionnel. Le sous-cas 1/1 applique le verrou, constate le bouton gizmo désactivé,
effectue Undo, re-sélectionne l'identité exacte, puis exige le gizmo actif et
l'action « Prévisualiser le verrouillage » restaurée. Le ledger agrégé ne marque
R2-029 acquis que lorsque les deux scénarios passent : 10/10 au total.

Le ledger agrégé et le rapport sont conservés dans
`artifacts/forge-runtime/gate2-resume-20260919/r2-028-031-world-edit-v7-20260922-v14/`.
SHA-256 du ledger :
`AE8A311DF37D6B6B96F5CD79864BB8B67E9ED8982E9E72EC49901845B905D475` ;
SHA-256 du rapport :
`3D1B3A5C3AFA7439176E263F1E52EE465A3E43D0C4A7DE58451A9974AA3A1588`.
La fixture source est inchangée par hash, octets et mtime ; le candidat reste
byte-identique ; les audits fetch sont restaurés ; les processus possédés et
listeners CDP sont absents après chaque scénario.

Luna max a inspecté les sept captures PNG : verrouillage et gizmo désactivé,
restauration après Undo, réouverture R2-030, picking positif, occlusion bâtiment
et états finaux cohérents. Il s'agit d'une inspection visuelle indépendante par
agent, **pas d'une revue humaine**. `human_review_performed=false`,
`gate_qualified=false`, CI `NOT RUN — infrastructure billing`. Cette preuve clôt
localement les quatre critères ; elle ne scelle pas formellement Gate 2 sous
ADR-0006 et n'ouvre aucune activité Gate 3.
