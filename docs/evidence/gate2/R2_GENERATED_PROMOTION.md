# R2-020 — promotion d'une instance générée

## Verdict courant — R2-020 terminé localement, 16 septembre 2026

**R2-020 — promotion et retouche : PASS local. Gate 2 reste ouverte.**
Ce verdict remplace les réserves historiques pour les parcours décrits ci-dessous,
pas pour la qualité artistique, les performances ni les autres critères Gate 2.
CI `NOT RUN — infrastructure billing`. Aucun commit/push, branche Rust conservée.

### Qualification finale dans le produit

Même candidat EXE `1303A840FE90BAA2BE4AEA36D0635AD75AE48592E43D8C9ADBB53CF79E9B4D89`
et WASM `22F2774F66177C3C051F1747BABA767A9CB702871D41EF2665652D78CFA32E4C`
que le lot du 15 septembre détaillé plus bas. Aucune modification de production
dans ce contrôle ; seul un test de quotas a été ajouté. HEAD de travail
`f786a38aca2023f08d708687f99a47d14f34e6d4` avec modifications locales préservées :
ce HEAD seul ne représente pas le candidat, identifié par ses hashes binaires.

Essai via l'interface réelle, sur copie sous
`E:/OpenRPG_Forge/.gate2-evidence/r2020-final-ui-20260916/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json` :

- Ouverture de 3BCF877D (24 commandes) : session synchronisée, forêt affichée,
  arbre retouché `9e4a991a-ce2b-570f-a058-9f9955057ba9` retrouvé et cadré.
- Conversion du chêne voisin : aperçu en attente puis prêt ; Échap restaure les
  deux placements acceptés. Archive initiale inchangée après abandon.
- Nouvelle conversion, Apply, Undo puis Redo : inventaire 2→3→2→3, même nouvel ID
  `f7cd4f33-e2ab-51c2-8097-2210fc0be049`, origine
  `72cea2a8-5da8-56a3-9e4e-ad4b1c141541`. Huit primitives Rapier, zéro en attente
  après application. Arbre au même emplacement, aucun doublon manifeste à l'arrêt.
- Densité 35→40 % et altitude maximale 4096→50 m : aperçus compatibles puis Apply
  et Save, trois placements conservés. L'essai intermédiaire altitude maximale 2 m
  est refusé : deux origines promues introuvables, aucune retouche supprimée.
- Playtest lancé, personnage et forêt affichés, retour à l'édition ; archive
  inchangée. Pas de nouvelle qualification de locomotion/collisions par ce contrôle.
- Fermeture de zone puis réouverture de l'archive finale à 29 commandes : session
  synchronisée, forêt et les deux identités d'arbres promus présentes.

### Persistance finale et absence de perte

Archive finale SHA-256
`3F539AD8F48B039983A8B5B464C0BE081A9C716F35C2F09ABB0B5CA6ADDD8789`,
3 112 926 octets, 29 reçus ; tête reconstruite
`df8808ae4976b3a6d853821eaaf1fabc11a624f515616c98b86afbb3b2f3415a`.
Les 24 commandes initiales sont sémantiquement identiques ; cinq ajouts seulement
(promotion, Undo, Redo, densité, habitat).

Lecteur `inspect_zone_archive.exe`, modes `delta` et `summary` : PASS,
`lossless=true`, `files_written=0`. Trois placements ; les deux placements
initiaux complets (IDs, pins, transformations et verrous) sont strictement
identiques à la source, terrain inchangé. Le pin retouché reste exactement à
(-72.28150177001953, 3.018228054046631, 52.5119514465332) m.
Hash final inchangé après playtest, réouverture et fermeture du candidat.

### Panne réelle de chargement — essai négatif séparé

Même EXE, copie intacte de 3BCF877D sous
`E:/OpenRPG_Forge/.gate2-evidence/r2020-load-failure-20260916/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`.
Profil WebView jetable dans ce dossier, endpoint de diagnostic local port 19321.
Après chargement de la scène acceptée, `Network.setBlockedURLs` bloque uniquement
`http://tauri.localhost/forge-kit/*.glb` dans cette instance, sans modifier de fichier.
Promotion du même chêne par clic dans la vue : requête réelle
`http://tauri.localhost/forge-kit/assets/vv-tree-oak-a.glb`, requestId `34420.25`,
événement `Network.loadingFailed`, `blockedReason=inspector`.

PASS : arbre généré toujours visible, restauration à deux placements, bouton
Appliquer désactivé ; Ctrl+Entrée retourne « Réexaminer la promotion : aperçu
absent ou périmé ». Session synchronisée, aucune promotion ajoutée à l'archive,
SHA-256 toujours `3BCF877DDA4CE9E7811B6C4E4E14782E1C5B21F0E84EC4240F18570D05B29189`.
Une tentative ultérieure de Ctrl+S a été interrompue par détection d'entrée
utilisateur : elle n'est pas comptée comme preuve. La sûreté est démontrée par le
refus d'Apply et l'archive inchangée, non par cette tentative.
Le blocage a été retiré (acquittement CDP id 3), le client diagnostic s'est terminé.

### Couverture des critères

| Critère | Preuve retenue |
| --- | --- |
| Une origine devient un placement, voisins conservés | `promoted_instance_filter` et densités graphiques 0,1/0,5/1/1,25/2 déjà PASS ; conversion réelle ci-dessus |
| Retouches conservées en régénération et archive | Parcours seed/espacement antérieurs, densité/habitat et comparaison exacte finale |
| Undo/Redo sans duplication logique | Test de session/codecs antérieur et cycle Studio 2→3→2→3 |
| Sources périmées, pins absents/modifiés, quotas | Précontrôle Monde déjà PASS ; test AssetBrowser ci-dessous ; nouveau test des deux quotas |
| Tête commune Studio/natif et playtest | Commandes de session natives, archive relue par le lecteur natif, projection et playtest Studio ; aucun document parallèle ni asset Cadle |

Tests ciblés exécutés le 16 septembre : `cargo test -p forge_project --test
zone_species --offline --locked -j2` : **5 PASS**, dont
`promotion_quota_refusals_leave_the_authoritative_head_unchanged` (4096 placements
ou 4096 exclusions conservées après suppression ; ajout refusé, tête intacte).
`cargo test -p forge_assets --lib --offline --locked -j2
asset_browser::tests::content_change_is_not_a_silent_reimport_and_other_kits_cannot_substitute`
: **1 PASS** (contenu modifié, asset absent et autre kit refusés).
`cargo fmt --check -p forge_project` et Clippy strict du test `zone_species` : PASS.
Pas de nouvelle campagne globale ou benchmark ; preuves antérieures inchangées
réutilisées pour les chemins de production non modifiés.

Limites explicites : observations graphiques ponctuelles, pas de garantie
frame-par-frame sans trou GPU ; panne de transport GLB testée, pas tous les modes
de corruption interne du décodeur. Éclairage de travail actif : aucune acceptation
artistique nouvelle. Captures inspectées pendant le parcours, pas archivées en PNG.
Les processus des deux essais (racines 31260 et 29780 et descendants identifiés)
sont tous terminés, port de diagnostic fermé ; aucun processus utilisateur arrêté.

## Historique — bilan intermédiaire du 16 septembre 2026

**Lot borné terminé : réconciliation d'espacement et persistance. R2-020 et
Gate 2 restent ouverts.** Les paragraphes du 15 septembre conservés plus bas
sont un historique : leurs mentions « aucun candidat » ne décrivent plus l'état
courant. Aucun verdict global ni qualification graphique implicite.

### Candidat et essai Studio du 15 septembre

- EXE : `E:/OpenRPG_Forge/.gate2-evidence/reconciliation-recipes-studio-20260915/forge-studio-candidate.exe`,
  SHA-256 `1303A840FE90BAA2BE4AEA36D0635AD75AE48592E43D8C9ADBB53CF79E9B4D89`.
- Frontend : `reconciliation-recipes-recovery-web-20260915`, WASM SHA-256
  `22F2774F66177C3C051F1747BABA767A9CB702871D41EF2665652D78CFA32E4C`.
  Trunk réussi (compilation 1 min 29 s), packaging natif réussi (1 min 01 s).
  Construction précédente interrompue par redémarrage Windows : `.stage`
  préservé dans `reconciliation-recipes-web-20260915`, non qualifié.
  La preuve de packaging reste `BUILD_ONLY` ; ce bilan porte les preuves runtime.
- Copie d'essai : `E:/OpenRPG_Forge/.gate2-evidence/reconciliation-recipes-ui-20260915/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`.
  Source : même suffixe sous `reconciliation-ranked-ui-20260915`.
- Source SHA-256 `1F51F34201184878B88FCC05B25A363EDE75958CFA985CC3B03569E261A9329C`,
  rouverte dans le Studio avec l'arbre promu. Espacement saisi 5→8 m, densité
  conservée 35 %, seed 43. Liste native de 1279 candidats, 20 affichés.
  Correspondance explicitement choisie à 3,9 m :
  `80382b3d-c45b-503a-ac5c-07129147ab95` pour le placement
  `9e4a991a-ce2b-570f-a058-9f9955057ba9`.
- Aperçu puis Apply par Ctrl+Entrée, Save par Ctrl+S ; état accessible
  « Session synchronisée ». Le dernier retour screenshot ne correspondait pas
  à l'application : il n'est pas retenu comme preuve visuelle de sauvegarde.
  L'archive réelle confirme la persistance indépendamment de cette capture.
- Archive finale v27 SHA-256
  `3BCF877DDA4CE9E7811B6C4E4E14782E1C5B21F0E84EC4240F18570D05B29189` :
  24 commandes, les 23 précédentes sémantiquement intactes, ajout unique
  `d054d5ed-d576-5577-87ed-cf07e154a506` de type `reconcile_promotions`.
  Candidat PID 14180 et descendants possédés arrêtés après sauvegarde ; aucun
  navigateur utilisateur arrêté. Source intacte, confirmée à nouveau le 16.

### Contrôle de clôture du 16 septembre, sans GPU

Sur branche Rust, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4` avec changements
locaux préservés (ce HEAD seul ne décrit pas le candidat), lecteur existant
`crates/forge_project/examples/inspect_zone_archive.rs` recompilé via :

```text
cargo run -p forge_project --example inspect_zone_archive --offline --locked -j2 -- <archive finale ci-dessus> delta
```

PASS : 3 104 155 octets, 24 reçus, `lossless=true`, `files_written=0`.
Tête reconstruite : `3c5bc7a006f0617e6e21be917576e88e6c0a7bc8f70cbb5a25c4cc3ac951d314`.
Deux lectures `summary` source/finale confirment les placements complets
strictement identiques (identités, pins, transformations, verrous), le hash de
terrain inchangé, seed 43→43 et espacement 5→8 m. L'arbre retouché reste à
(-72.28150177001953, 3.018228054046631, 52.5119514465332) m.
Hashes d'archives source/finale inchangés après lecture. Aucun fichier d'archive
écrit, aucun runtime graphique ouvert, aucun test global ou benchmark répété.

### Limites restantes, hors clôture de ce lot

La relecture métier ne remplace pas la réouverture graphique de l'archive finale.
Restent aussi la qualification des panneaux densité/habitat, l'échec de chargement
réel, la nouvelle promotion avec abandon/application, et les transitions/voisins
(aucune absence de trou entre frames affirmée). Les tests CPU antérieurs restent
distingués des parcours Studio. CI `NOT RUN — infrastructure billing` ; aucun
commit ni push. Ne pas recommencer le scénario espacement déjà acquis sans
modification pertinente ou doute identifié.

## Historique inspecté le 15 septembre 2026

### Réconciliation des réglages de couche après 97DF58D4

L'inventaire était limité à une nouvelle seed, alors que les réglages de densité,
espacement, altitude et pente peuvent aussi invalider des origines promues.
Extension fonctionnelle : `ReconciliationRecipe` décrit Seed, Layer ou Habitat.
Les deux nouveaux cas réutilisent les préparateurs de base extraits des commandes
ordinaires ; celles-ci gardent leur précontrôle strict des origines. Il ne s'agit
pas d'un contournement de validation ni d'une nouvelle tête de zone.

Le même sélecteur paginé est raccordé aux champs existants de chaque couche et
aux limites de répartition. Il ne s'affiche qu'en présence d'origines promues.
Changer ces paramètres invalide les choix locaux ; la réponse native est liée
à la recette, au hash de tête, à l'objet et à la page. Le cache d'une population
est désormais comparé au SHA-256 canonique de la recette entière, pas à la seed
seule. Aucun paramètre de terrain, placement ou verrou n'est modifié par ce lot.
Le transport de lecture Query/Page évolue ensemble (recipe au lieu de seed) :
reconstruire frontend et host ensemble ; aucun changement du format des archives.

Test ciblé existant étendu `reconciliation_preserves_manual_tree_across_regeneration_and_archives`
PASS (compilation 2,20 s, exécution 0,31 s). Pour Layer et Habitat : seed conservée,
base changée, choix explicite, Preview sans mutation, Apply/retry, placement
verrouillé et toutes autres corrections identiques, delta archive rouverte,
Undo/Redo. Paramètres invalides refusés ; aucune correspondance fictive lorsque
la recette ne produit plus d'arbre. Encodage des paramètres et différences de
génération contrôlés. Une première compilation du test utilisait un constructeur
inexistant ; corrigée en passant par l'archive vérifiée de la session source.
Clippy du test PASS 2,31 s, WASM PASS 7,66 s après correction d'un emprunt de
chaîne, natif final PASS 0,89 s. Pas de désactivation de lint ou nouvelle dépendance.

Pas de nouveau candidat packagé ni d'essai graphique dans ce lot. Le dernier
binaire 97DF58D4 ne contient pas cette extension. Qualification réelle des
panneaux, du cache entre recettes et des transitions encore due. Le cas sans
aucune origine de remplacement reste un refus explicite, pas une suppression
automatique de la retouche ou de son exclusion. R2-020/Gate 2 ouverts.

### Transition visuelle après la garde d'application

Inspection du projecteur : il révélait chaque nouveau placement dès qualification
GLB, avant la fin possible du retrait du scatter. Correction implémentée : les
nouveaux IDs de placement d'une promotion restent cachés jusqu'à l'acquittement
de la végétation exacte. Le chargement/qualification continue caché pour ne pas
créer d'attente circulaire. Les placements déjà acceptés restent visibles.
L'annulation invalide la clé et maintient les racines proposées cachées pendant
leur retrait ; un acquittement tardif ne les révèle pas. Une nouvelle observation
de session réinitialise cette garde dérivée, sans écrire dans les documents.

Deux tests CPU ciblés PASS (11,78 s de compilation, <0,01 s de tests), dont
acquittement périmé, apparition après acquittement, annulation et réinitialisation.
Clippy WASM final PASS (7,55 s), après correction de l'import ECS de la macro
Component et d'un nom signalé par Clippy ; aucune nouvelle dépendance. Le système
de visibilité ne change le composant que si nécessaire. Ces tests ne prouvent
ni absence de trou entre deux frames ni absence de chevauchement géométrique
avec les arbres voisins.

Candidat regroupé construit :
- `E:/OpenRPG_Forge/.gate2-evidence/promotion-handoff-studio-20260915/forge-studio-candidate.exe`
  SHA-256 97DF58D488098DA2BB0987C1809B692A11877B1E207AAE3C938C5B0E4AF4F712.
- Web `promotion-handoff-web-20260915`, WASM
  849C4CF7DBB9E937D1F62B90B8654A2D873FAC23074CE011B90D2E3D76264ABB.
- Trunk réussi (compilation 1 min 07 s), packaging Tauri réussi (16,07 s),
  kit gouverné existant vérifié, preuve `BUILD_ONLY` conservée hors Git.
- Copie isolée `promotion-handoff-ui-20260915` depuis l'archive finale du
  parcours précédent : 1F51F34201184878B88FCC05B25A363EDE75958CFA985CC3B03569E261A9329C.

Lancement sur écran secondaire 1600×900 : coque affichée, aucune zone ouverte.
Dialogue système Windows recouvrant l'application, annonçant un redémarrage à
05:53. Aucun clic ni changement de réglage Windows ; essai interrompu sans
mutation de la copie. La réouverture de zone et le parcours de promotion ne sont
donc pas acquis pour ce binaire. Captures inspectées dans l'outil, non archivées.
Arbre possédé terminé et absence vérifiée : PID 25064, 33524, 34300, 28624,
30424, 33684, 12016 et 13908. Aucun navigateur ou processus utilisateur arrêté.
Replay restant : réouverture finale, promotion/abandon/application avec les gardes,
échec de chargement et géométrie voisine. Gate 2 reste ouverte.

### Garde d'application post-11415DF8

Inspection : le reçu PreviewZone rendait Apply disponible sans attendre la
projection, malgré le texte demandant d'attendre. Correction de présentation :
`promotion_preview_gate` attend un acquittement lié au SHA-256 canonique de la
requête complète, pas seulement à son command_id. Début/remplacement/annulation
invalident l'acquittement ; un ancien résultat ne valide pas une nouvelle requête.
Le projecteur végétal acquitte après concordance des hashes terrain/végétation
et `governed_forest_ready`. La disponibilité actuelle des placements est aussi
vérifiée au dispatch, avant d'effacer l'aperçu ou d'envoyer ApplyZone.

Bouton désactivé avec explication pendant cette attente ; Ctrl+Entrée et bouton
utilisent la même garde dans `accepted_request`. La garde vise Edit changeant les
origines et ReconcilePromotions ; elle ne remplace pas les validations du host,
n'ajoute pas d'autorité GPU au domaine et ne change ni Undo/Redo ni les archives.
Erreur de chargement du placement, d'espèces, de qualité végétale ou de soumission
de la promotion : restauration demandée, acquittement invalidé. Les emprunts de
PENDING sont relâchés avant restauration, pour ne pas réentrer dans RefCell.

Test CPU `promotion_preview_gate::tests::both_projections_and_exact_live_proposal_are_required`
PASS (12,28 s compilation, test <0,01 s) : absent, attente, mauvais hash,
placement indisponible, acquittement exact, remplacement et annulation. Ce test
simule les acquittements ; ce n'est pas une panne GPU/GLB réelle. Clippy WASM
final PASS 7,56 s. Deux refus too_many_lines pendant l'édition ont été corrigés
par extraction du précontrôle de dispatch et des prérequis de projection, sans
désactivation de lint. Aucun candidat reconstruit, aucun replay graphique de cette
garde. Le binaire 11415DF8 et ses preuves précédentes n'incluent pas ce correctif.

### Candidat par proximité : retouche, régénération et playtest

- Candidat `E:/OpenRPG_Forge/.gate2-evidence/reconciliation-ranked-studio-20260915/forge-studio-candidate.exe` :
  11415DF82A25D88D41076B6403A07FDBF53180D7BC67E0E2B0657D2948A34C9A.
- Web `reconciliation-ranked-web-20260915`, WASM
  E196A1BC33FBCBC92B3CEE771ED908992D9CD67AD86C5C55FAE291BE5C9DCD54.
  Compilation Rust Web 2 min 44 s, Trunk et packaging réussis ; kit gouverné
  existant vérifié par Build-StudioCandidate. BUILD_ONLY reste distinct du replay.
- Copie `reconciliation-ranked-ui-20260915/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`,
  initialement DE03529471DA7984450ABA931CF34EAFD5128309C8DA04F0ECC06DC44827390D.

Parcours réel sur écran secondaire, via les outils ordinaires : ouverture,
inventaire Arbre promu, Cadrer, Déplacement local précis X/1 m, aperçu indiquant
une transformation et aucun changement d'asset, Apply puis Save. Position
acceptée et enregistrée (-72.28150177001953, 3.018228054046631, 52.5119514465332) m,
rotation et échelle conservées. Archive intermédiaire
8E47C999D69D3510E62E626D43F3BEBD8BC8970C1252693A47E27288E07A3FE9.
Commande 2eb4ca52-674e-50de-bb17-d127cdf7738f, révision attendue 29.

Seed 43 saisie dans la recette. L'API UIA set_value échoue encore (CacheRequest
0x80070057) ; clic, sélection « 42 » observée puis saisie littérale fonctionnent.
Ne pas assimiler le champ focused_element RootWebArea à un échec de saisie lorsque
la sélection et la valeur visibles prouvent le contraire. Aucun correctif produit
de focus déduit de ce comportement de l'outil.

Liste native : 1783 candidats, 20 affichés par distance croissante, premier à
3,9 m, ID 80382b3d-c45b-503a-ac5c-07129147ab95. Choix explicite de ce candidat
sur la copie, puis Examiner/aperçu/Apply. Seed acceptée 43 ; même arbre auteur à
la position retouchée, forêt environnante changée. Libellé de fin de préparation
corrigé visible. Undo ramène 42 sans retirer la retouche ; Redo ramène 43, toujours
sans la déplacer. Save donne une session synchronisée.

Archive finale v27 : 1F51F34201184878B88FCC05B25A363EDE75958CFA985CC3B03569E261A9329C.
19 commandes du fichier source comparées individuellement et conservées exactement ;
23 au total (Edit, ReconcilePromotions, Undo, Redo ajoutés). Dernière réconciliation
ae287df5-b547-5301-9cb8-ee46ad52cfab, révision attendue 30, génération
f6d4194abfc3a8d6b25282a2db8c581ce67ff2ac7133ad9855678ab5ba671a63.
Hash de tête enregistré : 071b1d46be47d2f27c0635c4abb804ad046f3d4bf5cd0c111dd587ff2bdf6c76.

Jouer : capsule et forêt rendues, sources gelées annoncées. Retour édition :
session synchronisée et retour à la vue d'ensemble. Archive finale inchangée
après ce passage et après fermeture ; source DE035294 également inchangée.
Processus 29800,32804,30804,12348,29520,25732,12504,25756 tous confirmés terminés.
Captures inspectées en conversation, non enregistrées en fichiers.

Portée : ce replay prouve retouche puis régénération et lancement/retour playtest,
pas un trajet de collision contre cet arbre ni une qualification des FPS ou de
l'éclairage de travail. Réouverture finale 1F51F342 encore due ; retrait de choix,
pagination suivante, plusieurs origines et échecs de chargement non rejoués ici.
Absence globale de chevauchement et doublon transitoire non déduite des captures.
R2-020 reste ouvert, aucune publication et aucune nouvelle gate validée.

### Suite : réouverture et sélection spatiale

Le même candidat AD6DDA38 a rouvert l'archive v27 DE035294. Inspecteur : placement
9e4a991a-ce2b-570f-a058-9f9955057ba9, asset 68534a54-c3cc-5c5c-b5d0-1b8141eb60ea,
pin c5c0ffe8676ab03b0d6dbb10a8e7deabee81cfd818997b364cebdf0cf09cc4a9 ; position
acceptée (-73.18, 3.02, 52.07) m. Cadrage réel du conifère inspecté. La tentative
de saisie X n'a pas obtenu de focus confirmé : ni retouche ni playtest revendiqués.
Fermeture de la fenêtre ; PID 28696 et descendants suivis 33316,30252,33756,31140,
32484,11788,26492 tous absents au contrôle. Archive toujours exactement DE035294.

Code ultérieur : `ranked_trees` classe les candidats par distance horizontale
depuis le placement autoritaire, avec départage par ID. Pas de réaffectation
automatique, déplacement, écriture ni nouvelle représentation persistante.
Une exclusion dont le placement est supprimé reste listable, sans fausse distance,
par ID stable. Cache de population réutilisé ; tri de références côté natif,
20 résultats transférés par page. L'identité de l'objet figure dans requête et
réponse ; changement d'objet recharge la première page sans perdre les autres
choix, réponse périmée ignorée. Origine déjà choisie pour un autre objet désactivée
dans l'UI ; choix révocable dans le brouillon sans suppression du placement.

Test `reconciliation_preserves_manual_tree_across_regeneration_and_archives`
étendu : ordre/distance, changement de position authorée, distance X/Z malgré
Y différent, refus tête périmée/objet inconnu, tombstone, absence de mutation et
de choix implicite. PASS : compilation 16,20 s, test 0,21 s. Clippy binaire natif
PASS 3,34 s ; WASM PASS 31,49 s ; test Monde strict PASS 2,46 s. Après ajout du
retrait de choix, Clippy a refusé le composant à 103/100 lignes : boutons de
candidats extraits dans un composant local, sans désactiver la règle. Contrôle
WASM final PASS 7,92 s ; avertissement futur proc-macro-error2 préexistant. Aucun build
packagé ni replay graphique de cette amélioration : R2-020 reste ouvert.

### Replay du candidat de réconciliation

Binaire `E:/OpenRPG_Forge/.gate2-evidence/reconciliation-studio-20260915/forge-studio-candidate.exe`
SHA-256 AD6DDA383E5A16E7009A7B126E415961CB15F349435673E1D75D0CB941DE9A85.
WASM 67FC3B3B768E59607F30ECE375A0F9DA7F49F1A9DBB0866EAF19277E920879C1.
Rust Web compilé en 2 min 47 s ; packaging réussi. Premier appel Trunk refusé
avant compilation par NO_COLOR=1 ; corrigé en true. Pas de publication.

Copie `reconciliation-ui-20260915/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`
initialement B171A07C29E5828669732B9CEE2EB2F03E1AC277D0CEDFA8718DF9558A891417,
identique à promotion-ui. Ouverture réelle : session synchronisée, Arbre promu
9e4a991a-ce2b-570f-a058-9f9955057ba9 présent, scène rendue. Nouvelle seed 42
(acceptée 44), liste native 1628 arbres, 20 boutons affichés. Origine choisie
070ed8a4-24f3-577b-bf41-7f0d708f77d4 ; examen active Apply et présente une nouvelle
population. Ctrl+Entrée : seed acceptée 42, objet auteur toujours présent ;
Ctrl+S : synchronisation acquise. Dernier Apply affiché 1284,9 ms (transport et
traitement, pas benchmark de réactivité). Ouverture affichée 3692,2 ms.

Archive finale v27 DE03529471DA7984450ABA931CF34EAFD5128309C8DA04F0ECC06DC44827390D :
18 commandes antérieures strictement conservées puis une reconcile_promotions,
commande 0ac40d18-e2c4-53e3-aa5b-ab3f5f79f816, révision attendue 28. Nouvelle origine
et base seed 42 enregistrées ensemble. Les archives delta n'ont pas de tête
matérialisée : aucune égalité de placements dérivée de champs JSON absents
n'est comptée comme preuve. Conservation exacte couverte par le test CPU précédent.

Fenêtre sur écran secondaire, captures inspectées en conversation, non persistées.
Processus suivis et tous confirmés terminés : 33024,15000,19012,33396,23416,33016,
29736,30144. Source promotion-ui restée B171A07C. Libellé « Calcul natif en cours »
restait après préparation : corrigé dans le code après essai (fmt/diff PASS),
pas dans ce binaire. Réouverture finale, retouche manuelle, Undo/Redo graphique,
playtest et absence de chevauchement géométrique restent dus. Aucun verdict Gate 2.

Aperçu Studio ReconcilePromotions raccordé côté code : vérification de la base
inverse, terrain inchangé, placements inchangés et hash final ; dérivation sur
la révision du terrain accepté, recette et exclusions proposées ensemble.
Pas de nouvelle proposition de placements ni d'attente de chargement pour cette
action ; la promotion initiale conserve son attente de placement. Abandon utilise
la restauration existante. Tout changement de terrain est refusé par ce chemin.
Clippy Web/WASM strict PASS (38,72 s puis 9,06 s après ajustement de révision) ;
avertissement futur de la dépendance proc-macro-error2 inchangé. Pas de build
packagé ni replay GUI : accès utilisateur non acquis.

Sélecteur ajouté ensuite dans authoring_zone/reconciliation.rs : nouvelle seed
commune aux contrôles de recette, génération des candidats, pagination 20 arbres
avec coordonnées, choix explicite pour chaque identité auteur puis PreviewZone.
La liste complète n'est pas rendue dans le DOM ; aucun choix automatique.
Changement seed/tête invalide le brouillon. ReconciliationChoices vérifie tête,
complétude, existence et unicité via la commande puis la garde Monde.
Test intégré étendu PASS (15,29 s compilation, 0,18 s test) : choix complet
accepté, incomplet/introuvable/périmé refusés. Clippy WASM strict PASS (7,52 s).
Transfert natif effectué ensuite : StudioCommand::Reconciliation typé,
head lue depuis le host autorisé et hash contrôlé ; calcul et préparation dans
spawn_blocking, sans nouvelle permission Tauri. Cache natif d'une seule population,
évincé aux commandes authoring/workspace ; pages de 20 arbres au maximum.
Le frontend n'importe plus ReconciliationChoices ; réponses hash/seed/page
contrôlées, réponses périmées ou après démontage ignorées. La commande Tauri
existante est désormais asynchrone, dispatcher synchrone conservé pour tests.
Clippy natif PASS (3,20 s), WASM PASS (6,69 s après extraction du chargeur).
Limites : pas de replay du transport/cache ou de mesure de réactivité ; sélection
textuelle par coordonnées et non clic 3D. Aucun binaire ni rendu de ce contrôle.

Commande ReconcilePromotions ajoutée : nouvelle base et correspondances dans le
même reçu/inverse, sans accès aux placements ni autres corrections. Ensemble
d'identités auteur conservé, y compris exclusions de placements supprimés.
Préparation Monde vérifie les origines proposées ; aucune sélection automatique
par proximité. Les contraintes préexistantes restent appliquées, notamment
le refus de suppression d'une couche source. Archives 25/26/27 ; anciens formats
inchangés sans cette action. ADR-0014 complété.

Test reconciliation_preserves_manual_tree_across_regeneration_and_archives PASS
(42,54 s compilation, 0,15 s test) : deux populations CPU réelles de seeds
différentes, correspondance choisie, placement verrouillé identique, Preview
sans mutation, Apply/replay, trois codecs, Undo/Redo et downgrade refusé.
Clippy Studio natif PASS (6,90 s) et test Monde strict PASS (0,40 s après
extraction de deux helpers de test trop longs, dont un préexistant).
Limites : aucun contrôle UI de correspondance ni aperçu combiné recette/origines,
pas de nouveau binaire ni preuve graphique ; ancien candidat AD6D inchangé.

Diagnostic de conflit post-AD6D : erreur typée UnresolvedPromotions donnant les
clés couche/instance exactes, calculées pendant l'unique génération. La garde
affiche le total et jusqu'à huit objets/origines ; une origine sans placement
est nommée « objet supprimé, exclusion conservée ». Aucun retrait automatique.
Tests ciblés PASS : promotion_preflight (41,99 s compilation, 0,05 s test),
promoted_instance_filter (0,21 s test, voisins et cinq densités conservés).
Clippy Monde lib strict PASS (25,30 s). Pas de nouveau candidat ni de replay GUI.
La résolution interactive du conflit n'est pas implémentée par ce diagnostic.

Protection native post-AD6D3D3F : AuthoringHost accepte une politique de validation
injectée ; le Studio y raccorde promotion_preflight::validate_origins. Chaque
PreviewZone/ApplyZone construit le candidat depuis le reçu validé avant toute
mutation, y compris Undo/Redo. Les replays déjà acceptés restent idempotents.
La garde précède les branches autonome et workspace géré ; le domaine Projet
ne dépend pas du générateur Monde. Les autres hosts sans politique n'acquièrent
pas implicitement cette protection ; aucune garantie universelle des imports
ou migrations n'est revendiquée.

Test zone_host PASS (35,24 s compilation, 0,05 s exécution) : politique refusant
un candidat sur aperçu et application directe, état et fichier inchangés ;
parcours accepté, sauvegarde/réouverture et replay conservés. Ce test emploie
une politique synthétique ; le calcul d'origines reste couvert par le test Monde
ci-dessous, pas par un replay intégré host + origine. Clippy binaire natif Studio
PASS (7,65 s), après simplification d'un type signalé par Clippy.
Limites : voie gérée non rejouée avec refus, candidat AD6D inchangé, aucun nouvel
essai graphique ni nouveau critère clôturé. Réconciliation et cycle auteur dus.

Préflight de régénération ajouté dans `forge_world::promotion_preflight`, appelé
par les six préparations editor_vegetation. Si des origines persistent, la future
recette est générée avec leurs exclusions avant de retourner la commande. Une
origine disparue refuse la proposition sans mutation ; aucun abandon silencieux.
Test ciblé PASS (14,99 s compilation, 0,06 s test) : changement seed refusé,
renommage compatible accepté, tête intacte ; inclut une origine dont le placement
a été supprimé. Clippy Monde lib strict PASS (2,77 s).
Limites : nouveaux appels absents d'AD6D3D3F ; ce contrôle n'est pas encore une
garde universelle du host pour commandes directes, relief, chemins ou rivières.
Réconciliation explicite d'origine perdue et parcours graphique restent dus.

Replay AD6D3D3F : garde débloquée. Clic sur pin proche => proposition native,
Apply actif, aperçu visible ; Ctrl+Entrée ajoute « Arbre promu », inventaire 3→4.
Ctrl+S confirme « Session synchronisée ». Archive delta v24 : une commande ajoute
placement `9e4a991a-ce2b-570f-a058-9f9955057ba9` et origine
`editor_trees/a4526253-0e2c-5b45-b242-b699c77d7a9d` ensemble ; autres deltas Keep.
Pin GLB `c5c0ffe8676ab03b0d6dbb10a8e7deabee81cfd818997b364cebdf0cf09cc4a9`.
Undo : inventaire 4→3 ; Redo : 3→4, même identité. Sauvegarde puis fermeture.
Archive finale `B171A07C29E5828669732B9CEE2EB2F03E1AC277D0CEDFA8718DF9558A891417`.
Source draft-species-ui inchangée, hash 5717781F. Le rendu reste visuellement au
même emplacement, sans mesure de superposition/occlusion ; pas de preuve stricte
GPU de non-doublon. Réouverture, retouche, régénération, échec GLB et playtest dus.

Candidat corrigé construit :
`E:/OpenRPG_Forge/.gate2-evidence/promotion-ready-studio-20260915/forge-studio-candidate.exe`,
SHA-256 `AD6D3D3FC0845838F0247B001D76B0247EED01FC50BE8C4C44F024AC1E80A1ED`.
WASM `A1AE2B1E5BF5460B08C3D68628167E7192959C601F8D93B8C0B38D70084DC764` ;
Trunk terminé, packaging natif PASS (55,32 s compilation). BUILD_ONLY : replay
de sélection/promotion pas encore acquis. Réutiliser ce candidat, pas C2AEB393.

Régression readiness exécutée :
`vegetation::far_batches::readiness_tests::inactive_far_batching_does_not_block_individual_tree_projection`
PASS (compilation moteur tests 2 min 47 s, exécution < 0,01 s). Option inactive
non bloquante ; option active sans BatchState ou sans entrées reste refusée.
Reconstruction Web `promotion-ready-web-20260915` lancée pour le replay ciblé.
Ce test ne prouve pas encore le parcours de conversion graphique.

Cause identifiée après essai : `prototype_projection::quality_ready` appelle
la garde far_batches, qui exigeait `GovernedFarBatchPreview` même absent.
Le Studio auteur active la forêt gouvernée mais pas cette option de batching.
La projection PENDING ne pouvait donc être acquittée. Correction : option
inactive => pas d'attente de lots ; option active => contrôle des lots inchangé.
Test de régression ajouté, exécution en cours ; correction pas encore dans
le candidat C2AEB393 ni rejouée graphiquement.

Essai réel du candidat `C2AEB393DAB2F46E2115F38C2C6A2935F43F1EE82A9E4C211C43595F504195E7`
(`promotion-studio-20260915`) : ouverture de la copie, mélange chêne/pin visible,
catalogue chargé, bouton de conversion disponible. Clic sur un pin individuel
refusé avec « Attendre la projection acceptée avant de sélectionner un arbre ».
Pas d'aperçu préparé, aucune application/sauvegarde. Ce résultat est un échec
du parcours attendu, pas une qualification de promotion. Garde PENDING/aperçu
à diagnostiquer ; ne pas répéter la campagne visuelle avant correction ciblée.

Construction regroupée : Trunk Web terminé avec succès (compilation Rust 2 min
40 s), sortie locale `E:/OpenRPG_Forge/.gate2-evidence/promotion-web-20260915`.
WASM SHA-256 : `66C482E2766785FD6C7882191DA8F40FA919C0F47353112226A4AEA28D6E5BB6`.
Packaging `promotion-studio-20260915` lancé ; aucun verdict binaire/GUI à ce stade.
Copie d'essai `promotion-ui-20260915`, archive initiale identique à la source :
`5717781F981031CD11D4B3DC65AFA1F282AE5BA88C57D6821F4B787FEA2FDB2B`.

Protection du chargement d'aperçu : le retrait scatter attend désormais le
statut READY du placement proposé, sans requête de chargement restante. Échec
déclaré par le chargeur : abandon des projections proposées et restauration
demandée, avec diagnostic. Clippy Studio Web/WASM strict PASS (6,53 s).
Pas de preuve GUI ni d'atomicité GPU : un doublon transitoire peut apparaître
entre placement prêt et retrait scatter. Cette garde ne qualifie pas Apply
pendant chargement et ne remplace pas le scénario de panne réel restant dû.

Aperçu combiné raccordé : les reçus modifiant les promotions passent avant le
chemin placement seul. Validation du reçu, hash résultant et périmètre limité
placements/promotions ; projection végétale avec exclusions proposées et
chargement des placements. Abandon restaure les deux adapters existants.
Clippy Studio Web/WASM strict PASS (6,50 s), après extraction de helpers de labels.
Limite : préparations asynchrones indépendantes, aucune atomicité de présentation
GPU revendiquée ; échec tardif de chargement et cycle graphique encore à vérifier.

Premier parcours Studio raccordé en code : bouton de conversion dans les espèces,
raycast du premier triangle visible avec occlusion, identification via marqueur
d'arbre gouverné, garde de projection acceptée puis `prepare_tree_promotion` et
`PreviewZone`. Échap/annulation du pointeur désarme le geste. Catalogue chargé
requis ; amas lointains sans arbre individuel et décorations non canoniques refusés.
Clippy Studio Web/WASM strict PASS (6,50 s) après corrections d'imports et lint.
Aucun binaire reconstruit ni parcours graphique acquis : aperçu combinant retrait
du scatter et placement, Apply, annulation, sauvegarde, réouverture et playtest
restent à vérifier et corriger au besoin. Ne pas annoncer R2-020 terminé.

Composition de commande implémentée : `prepare_tree_promotion` reçoit une
sélection liée au hash de tête et aux deux hashes de projection. Elle reconstruit
le terrain creusé, le scatter avec chemins/rivières/bâtiments et exclusions depuis
la tête autoritaire ; aucune pose ni plan fourni par l'appelant n'est accepté.
Résolution canonique, choix d'asset et normalisation précèdent la préparation
atomique origine+placement. Aucun fichier ni état de session modifié ici.
Cette entrée reste à appeler depuis le Studio et à couvrir par une preuve
comportementale intégrée ; la vérification GLB appartient toujours au chargeur.
Clippy Studio Web/WASM strict PASS (29,80 s), sans lancement graphique.

Normalisation de placement ajoutée : `promotion_transform::from_scatter`
compose position/rotation/échelle du scatter avec l'échelle de hauteur du
prototype et son pivot source. Le résultat vise la racine GLB brute ; les
transformations de nœuds restent dans le GLB. Le résolveur moteur renvoie
maintenant AssetId, hash et cette transformation dans `PromotedTreeAsset`.
Deux tests CPU PASS (14,54 s compilation) : pivot non nul, rotation 90 degrés,
échelles combinées, paramètres invalides et dépassement numérique refusés.
Parité avec le chargement de placement Studio pas encore vérifiée ; ne pas
déduire une conservation graphique de ces seuls tests mathématiques.
Clippy moteur natif lib strict PASS après raccord (22,72 s).

Résolution des métadonnées d'asset ajoutée dans le moteur :
`resolve_promoted_tree_asset` reconstruit le catalogue authoré validé, appelle
le même `select_binding` que le rendu pour la clé couche/instance et retrouve
l'AssetId persistant via AssetBrowser, sans inventer une identité. Hash GLB
comparé au binding ; absence d'espèces gouvernées refusée explicitement.
Clippy moteur natif lib strict PASS (26,40 s). Aucun test comportemental ni
parcours UI de cette nouvelle entrée acquis : sélection canonique préalable,
normalisation pivot/échelle du placement, composition de commande et vérification
des octets GLB restent à intégrer avant de revendiquer une promotion fonctionnelle.

Résolution canonique ajoutée : `resolve_promotion_source` vérifie les hashes
terrain ET végétation de la sélection, retrouve la clé couche/instance exacte
dans la population canonique et refuse arbre absent ou déjà promu. Aucun choix
d'asset déduit de cette vérification ; le résolveur de prototype reste dû.
Test ciblé enrichi PASS (14,33 s compilation, 0,19 s exécution), incluant les
deux sources périmées et l'origine déjà exclue. Clippy Monde lib strict PASS
(5,86 s). API non encore appelée par une sélection Studio ; pas de preuve GUI.

Raccord ultérieur : le moteur applique les promotions aux préparations native,
Web, incrémentale et de restauration. Les clés de cache et gardes de réutilisation
incluent ces exclusions. Le Studio les transmet avec ses sources acceptées et
aperçus végétaux ; le playtest les extrait du même snapshot validé que les
placements. Clippy Studio Web/WASM strict PASS (33,38 s), formatage PASS.
Cela qualifie la compilation, pas le rendu. Aucun nouveau candidat graphique
construit ; sélection d'origine, résolution du pin et gestion des conflits avant
commande restent à raccorder. R2-020 demeure ouvert.
Le résultat de la précédente session Clippy native n'a pas pu être récupéré :
ne pas la comptabiliser comme preuve PASS.
Test ciblé de promotion enrichi : le snapshot de playtest conserve l'origine
et les placements de la tête validée. PASS (34,35 s compilation, 0,01 s test).

Mise à jour après cette inspection : première persistance implémentée dans
`promotion.rs` et `ZoneCorrections.promotions`. Tête v8, archives 22/23/24,
champ omis pour les anciens hashes. Préparation atomique origine+placement,
unicité/quotas/couche arborée vérifiés ; origine conservée après suppression
du placement. Aucun filtrage de scatter ni interface encore raccordé.

Test ajouté : préparation sans mutation, idempotence, doublon refusé, trois codecs,
versions rétrogradées refusées, Undo/Redo, régénération et conservation de l'origine
après retrait du placement. Suite zone_species : 4 PASS (30,28 s compilation,
0,02 s exécution). Clippy Projet all-targets strict PASS (5,77 s).
Ce test de données ne prouve ni résolution de la source ni absence de doublon rendu.
Les étapes 2 (résolution), 3, 5 (conflits source) et 6 ci-dessous restent dues.
Qualification workspace/WASM complète non exécutée pour ce changement partagé.

Filtrage moteur-neutre ajouté ensuite : `VegetationGenerationPlan::with_promotions`.
Le retrait se fait après acceptation de l'espacement pour ne pas remplacer un
arbre supprimé par un voisin jusque-là refusé. Clés couche/instance exactes,
compteurs mis à jour, cache réinitialisé et hash de population intégrant les
exclusions. Liste vide conserve l'ancien hash et résultat. Origine non retrouvée
dans la génération : erreur explicite, aucun résultat partiel retourné.

Test ciblé `promoted_instance_filter` PASS : chunks et voisins exactement égaux
au résultat initial moins l'arbre choisi, compteurs, generate_chunk, doublon et
origine introuvable. Compilation 14,31 s, exécution 0,04 s ; Clippy Monde lib strict
PASS (2,11 s). Une conversion d'erreur de hash a été corrigée avant ces résultats.
Le hook n'est pas encore appelé par les adapters Studio/runtime ; pas de rendu
sans doublon qualifié. Résolution des pins et sélection auteur toujours dues.

Préparation du raccord : la voie de densité graphique pouvait réintroduire une
origine promue lors de l'ajout décoratif. Elle effectue désormais la sélection
de qualité avec les emplacements réservés, puis retire les origines exactes et
recompte. Test à 0,1/0,5/1/1,25/2 : chaque sortie égale celle sans promotion moins
l'arbre choisi, voisins et hash attendus conservés. Fixture finale sans collisions
pour couvrir réellement l'expansion décorative. PASS (1,47 s compilation,
0,19 s exécution), Clippy Monde PASS (2,10 s).
Coût actuel avec promotions : une génération dérivée temporaire non filtrée
supplémentaire par préparation de densité. Pas de nouvelle mesure de performance,
ni cache canonique parallèle persistant. Optimisation non revendiquée.
Le raccord adapters reste à faire ; aucune qualification graphique déduite.

État antérieur à l'implémentation :

Non implémenté. Les placements manuels existent, mais ils ne constituent pas
une promotion d'instance générée. Ne pas clôturer R2-020 sur leurs preuves.

Sources inspectées :

- `forge_world::vegetation::VegetationInstance` : ID stable, couche, position,
  rotation Y et échelle moteur-neutres ; pas de pin d'asset dans cette structure.
- `forge_project::zone_authoring::placements::ManualPlacement` : identité auteur,
  pin d'asset, transformation et verrou persistants.
- `ZoneCorrections` : aucun lien d'origine générée ni suppression d'instance.
- ADR-0014 prévoit déjà la conservation des suppressions comme exclusions.

## Plan initial conservé pour traçabilité — état antérieur à l'implémentation

1. Représenter dans la tête unique une origine générée supprimée du scatter,
   reliée au placement auteur. Couche et ID généré, identité/version du générateur
   pertinentes et preuve de source ; pas d'Entity Bevy ni de cache sérialisé.
2. Préparer une commande atomique : vérifier la révision, retrouver l'instance
   effective, résoudre son prototype réellement choisi et son pin, ajouter le
   placement et la suppression ensemble. Échec sans mutation si source périmée,
   référence introuvable, quota ou collision d'identité.
3. Appliquer la suppression au résultat généré dans les deux adapters et au
   playtest. Une simple copie de mesh ou un trou de densité approximatif ne
   satisfait pas ce contrat : les voisins ne doivent pas être effacés.
4. Versionner tête, trois codecs et deltas, y compris les reçus historiques
   après Undo. Préserver les hashes des anciennes archives sans nouveau champ.
5. Conserver placement et suppression lors d'une régénération ; définir et
   exposer le conflit lorsque la source ne se résout plus. Ne pas effacer
   silencieusement l'exclusion ni créer un doublon.
6. Sélection et aperçu dans le Studio, application puis outils ordinaires de
   placement. Prouver Undo/Redo, Save/Open, régénération et playtest.

## Critères initiaux — à rapprocher des preuves courantes en tête de document

- Une instance devient exactement un placement ; ses voisins restent identiques.
- Retouche manuelle conservée après régénération et relecture d'archive.
- Undo restaure le scatter sans placement double ; Redo rétablit la retouche.
- Sources périmées, pins absents et quotas refusés atomiquement.
- Même tête projetée dans Studio et natif ; aucune source Cadle copiée.

Cette dernière section est le constat initial, pas le statut actuel du lot.
Les implémentations et preuves acquises sont décrites en tête de document ;
ne pas recommencer les étapes du plan initial déjà couvertes. Gate 2 reste ouverte.
