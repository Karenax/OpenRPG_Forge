# Atelier — inspecteur partagé, qualification en cours

Candidat unique : `output/studio-gold-20260924/candidate/forge-studio-candidate.exe`.
SHA-256 : `E5830A532B2DDCECFEB9F922764107ABE088FD8C5085680EC8F6CB3FCB81385B`.
Build WASM release PASS (2 min 30 s), Tauri PASS (14,99 s).

Code : 108 tests Studio PASS, 1 ignoré (comparaison CPU optionnelle), Clippy natif
et WASM strict PASS. Le dernier correctif de synchronisation frontend a été
recontrôlé par Clippy WASM avant ce build. Ces résultats ne prouvent pas le
parcours graphique.

Parcours natif Computer Use effectué dans `R2-004 Final B`, principal
1920 × 1080 CSS / DPR 1, secondaire ouvert sur l'autre moniteur :

- sélection du placement `vv-tree-oak-a` dans la scène ;
- X accepté −55.10344, saisie secondaire −50.10344 puis Prévisualiser :
  le principal propose Appliquer et signale l'aperçu non appliqué (PASS) ;
- Rapatrier ferme le secondaire, conserve l'aperçu et restitue X −50.10344
  dans le formulaire principal (PASS) ;
- saisie principale −49.10344 invalide l'ancien aperçu, puis un nouvel aperçu
  reste disponible après plusieurs sondages du relais (PASS) ;
- aucune application ni sauvegarde demandée ; sortie normale sans enregistrer,
  terminaison du candidat constatée ;
- SHA-256 de `authoring.json` avant/après identique :
  `62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

Défaut observé : panneau Objets trop long (927 / 807 px avant même ouverture
du formulaire partagé ; davantage ensuite). Diagnostic `detached-preview` FAIL.
Correction source suivante : vues Transformer / Placer-manipuler / Fiche,
formulaires conservés montés, sélection et diagnostics secondaires repliés.
Cette correction n'est pas incluse dans le hash ci-dessus : nouveau build et
contrôle graphique requis. Fermeture secondaire par la croix, édition concurrente,
4K et DPI mixtes restent à qualifier ; aucun verdict de clôture de Gate 2.

## Séparation des espaces Objets : premier candidat

SHA-256 `2E0524AB85CE7448C6F87202A20716E1479A92F9B441BF17310B13784FB6C201`.
Clippy WASM PASS, build WASM 2 min 38 s, natif 38,90 s (cache partagé attendu).
Formulaire affiché sur le chêne sélectionné, panneau 807 / 807 px et bouton
Prévisualiser visible. Cependant les trois onglets n'étaient pas visibles :
la règle globale `.scene-navigation nav` les positionnait hors du panneau.
Le premier diagnostic ne regardait que les rectangles écran et a produit un
faux positif ; le fichier `inspector-tabs` a été remplacé par le verdict FAIL
après ajout du contrôle de visibilité. La revue visuelle a détecté le défaut.

Correction source : position statique explicite du sous-menu. Le diagnostic
sonde maintenant centre et coins intérieurs via `elementFromPoint`, afin de
détecter le masquage sans attribuer à tort un clipping au parent d'une bibliothèque
en position fixe. Nouveau candidat requis. Session d'essai fermée normalement,
archive inchangée (hash ci-dessus). Tests de workspace lancés dans le cache unique.

## Contrôle workspace : FAIL conservé

`cargo test --workspace --locked`, cache partagé, incrémental désactivé,
`RUSTFLAGS=-C debuginfo=0`, quatre jobs : compilation 20 min 57 s, puis arrêt
sur le moteur. Assets 28 PASS, CLI 3 + 4 PASS, Core 17 PASS. Moteur :
376 PASS, 1 FAIL, 2 ignorés. Les crates suivantes ne sont pas qualifiées par
cette commande interrompue ; ne pas la présenter comme un PASS global.

Échec reproduit isolément avec le binaire de tests produit par cette commande :
`vegetation::prototype_projection::tests::governed_quality_receipt_rejects_missing_batches_and_stale_profile_or_catalog`,
`prototype_projection.rs:843`, assertion `!quality_ready(app.world())` après
projection individuelle sans activation du batching lointain.
Le helper actuel `far_batches::quality_ready` retourne explicitement `true`
quand `GovernedFarBatchPreview` est absent ; son propre test attend ce comportement.
Il existe donc une contradiction entre ces attentes. Aucun test ni code moteur
n'a été modifié dans cette passe Atelier ; le verdict reste FAIL à traiter avec
le propriétaire du travail moteur, sans changer arbitrairement le contrat.

Le premier contrôle WASM des nouveaux pictogrammes a refusé un déplacement de
`asset.id` réutilisé par l'attribut title (E0382). Correction locale par clone du
libellé ; revalidation et reconstruction du même candidat relancées après sortie
101 confirmée du contrôle précédent, sans duplication de build.

Les deux exécutables de tests Studio produits par la compilation workspace ont
ensuite été exécutés directement, sans nouveau build ni attente du verrou Cargo :
`forge_studio_frontend-33d0c72c05470747.exe` : 101 PASS, 1 ignoré ;
`forge_studio-7f12e618a0866ca2.exe` : 8 PASS. **109 tests Studio PASS**, dont la
politique de bibliothèque compacte et les ACL/limites des fenêtres. Cela ne
neutralise ni le FAIL global moteur ni la validation WASM/GUI encore attendue.

## Candidat 5B0A48F : Full HD et fermeture secondaire

Clippy WASM strict PASS, build release WASM 2 min 30 s et Tauri natif 16,59 s.
SHA-256 exécutable :
`5B0A48FE7C897C6A872C89D822272BDF98AF2A55D7E3C279027B5009038EF695`.
WASM : `629F05EB707DE1469F1EFCF2E9ECE84420398AA0C28624E97E08DA0B70D07DBD`.

Computer Use, fixture R2-004 Final B, principal 1920 × 1080 logiques, DPR 1 :
trois sous-vues visibles, formulaire sans défilement, bibliothèque de huit
objets visible. `atelier-review/inspector-tabs-fixed.json` et
`inspector-manipulate.json` : PASS avec contrôle de visibilité par hit-tests.
La saisie X `-50.10344` survit à Transformer → Placer/manipuler → Transformer
et apparaît dans le secondaire. Secondaire 1920 × 1009 logiques, DPR 1 :
géométrie de l'inspecteur PASS, mais surbrillance des sujets FAIL : attribut
booléen rendu vide au lieu de la chaîne `true`. Correction source des trois
sélecteurs et contrat diagnostic renforcé ; dix tests Node PASS.
Le verdict géométrique antérieur de `secondary-inspector.json` ne constitue
donc pas un PASS de sélection. Revalidation du candidat corrigé requise.

Saisie secondaire X `-` puis Alt+F4 : secondaire fermé, principal conservé,
texte incomplet `-` retrouvé sans modification de la carte. Ce test couvre
CloseRequested natif, pas encore un clic littéral sur la croix ni les conflits.

Ancrage inférieur : FAIL (`dock-bottom.json/png`), 908 px de contenu pour
257 px disponibles ; prévisualisation hors de vue. Disposition horizontale
bibliothèque/inspecteur et groupes de transformation en colonnes ajoutés aux
sources. Recompilation et revue requises, sans transformer ce FAIL en PASS.
Tentative de redimensionnement natif sans effet : aucune preuve petite fenêtre.
Session fermée normalement sans sauvegarde ; archive de référence inchangée
`62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`.

## Revue suivante — 25 septembre, candidat B962D2D

Exécutable `B962D2DAB8F85CBE7EC050A665EF546003A0048F1F314E5CBB019DE028E72BF4`.
Clippy WASM strict PASS ; release WASM 5 min 09 s (moteur recompilé),
Tauri 20,03 s. `secondary-selection-fixed.json/png` : PASS 1920 × 1009,
DPR 1, huit sujets dont Objets seul actif, trois colonnes et aperçu visibles.
Clic réel sur la croix secondaire : fenêtre détruite, principal toujours ouvert.
Pas de saisie nouvelle dans ce second essai de fermeture.

Ancrage inférieur restauré au redémarrage : bibliothèque à gauche et neuf champs
en trois groupes horizontaux à droite ; aperçu visible, canvas 1666 × 421.
`dock-bottom-fixed.json/png` reste **FAIL** : 348 px de contenu pour 307 px,
bouton de reprise partiellement coupé. Ne pas confondre le nom du fichier avec
son verdict. Ajustement des marges internes ajouté après cette capture,
non encore qualifié. Configuration locale du candidat passée à 1280 × 720 pour
le prochain essai de petite surface logique ; ce n'est pas une mesure DPI 150 %.

## Petite surface logique — candidat 9FA4E27

Exécutable `9FA4E27FCB40DD1D349BD7A9D0F1C8B3AD7CD8291BF452DAE94F606FA7041976`.
Build WASM 2 min 38 s, natif 16,87 s. Fenêtre réelle 1280 × 720 CSS, DPR 1.
Ancrage inférieur : tous les champs tiennent désormais (307/307), mais le
canvas passe derrière le panneau. Le PASS initial de `compact-bottom.json`
est un **faux positif du diagnostic**, contredit par la capture Computer Use.
Le contrat exige maintenant une carte non recouverte par un panneau ancré ;
un panneau flottant reste intentionnellement superposé. Onze tests Node PASS,
incluant le refus du chevauchement et d'une observation manquante.

Ancrage droit : `compact-right.json/png` FAIL, 550 px de contenu pour 487 px.
L'aperçu reste visible, mais le bas du formulaire exige encore de défiler.
Le panneau Terrain présente également des commandes sous le pli à cette taille.
Réduction des espacements et densité des contrôles ajoutées pour les hauteurs
inférieures à 760 px. Préférence d'ancrage inférieur conservée mais présentée
latéralement sous 960 px de hauteur de corps d'atelier ; choix inférieur alors
indisponible avec explication dans le menu, réactivé en agrandissant. Aucun
changement des préférences enregistrées par cette adaptation.
Menu d'ancrage refermé après un choix. Clippy WASM strict PASS et sept tests
Rust de disposition PASS, dont conservation du choix lors de l'adaptation.
Rebuild lancé ; rendu de ces corrections encore à vérifier.
Session terminée normalement ; même hash d'archive de référence qu'avant essai.

Rebuild 6E24E06ACCA3AB4B5895DA1B8041D393B17756AFDA7759223B3FF6F6C2E432B8 :
WASM 2 min 33 s, natif 17,06 s, lancement 1280 × 720 DPR 1.
Menu inférieur explicitement indisponible à cette hauteur et ancrage gauche
effectif avec fermeture automatique du menu : PASS Computer Use. La carte
reste séparée du panneau ancré. `compact-right-fixed.json` reste FAIL
(535/487), `compact-terrain-fixed.json` FAIL (574/487), malgré les commandes
d'aperçu désormais visibles. Ajustement supplémentaire non encore compilé :
suppression des espacements génériques réintroduits dans le formulaire partagé,
en-tête d'objet compact sur une ligne, sous-vues sans retour à la ligne,
paramètres de relief en trois colonnes et retrait du titre Terrain redondant
avec l'en-tête du panneau. Aucun contrôle fonctionnel retiré. Nouveau build requis.

## Compact et WQHD — candidat 9E9AAB5

Exécutable `9E9AAB571DAAC9CD6F2E1D541026AB57B8EC0872E620C70002F31F70CC6946E4`.
WASM 2 min 30 s, Tauri 14,98 s. `compact-object-final.json/png` : **PASS**
1280 × 720 CSS DPR 1, inspecteur 487/487, neuf champs et commandes visibles.
Terrain : `compact-terrain-final.json/png` reste FAIL 503/487 ; les commandes
et conseils sont visibles, mais 16 px de débordement subsistent. Dernier réglage
des espacements et du texte d'aide ajouté après mesure ; à reconstruire.
Correction également du texte d'état de fermeture limité par erreur à 100 px
de largeur dans la fenêtre compacte. Aucun état de carte modifié.

Moniteur principal constaté par WebView : 2560 × 1440, surface disponible
2560 × 1392 ; fenêtre maximisée : 2560 × 1369 CSS, DPR 1. Pas d'émulation.
`wqhd-object.json/png`, `wqhd-library.json/png` et `wqhd-bottom.json/png` PASS :
inspecteur gauche, bibliothèque déployée, puis ancrage inférieur horizontal.
Sur l'inférieur : panneau 307/307, canvas 2270 × 710, aucune superposition.
Le défilement horizontal de la bibliothèque étroite reste intentionnel.

Cycle réel maximiser → restaurer 1280 × 720 → maximiser :
`compact-auto-dock.json/png` et `wqhd-dock-restored.json/png` PASS.
L'ancrage inférieur est temporairement présenté à droite en petite surface,
puis revient en bas sans perdre la préférence. Bibliothèque repliée au retour
en petite hauteur, réouverture manuelle possible. Une notification Windows
étrangère au produit est visible dans certaines captures Computer Use ; les
captures WebView de géométrie excluent cette notification. Aucun clic sur elle.
Cela ne prouve ni le 4K physique ni les DPI mixtes ; question matériel posée.

## Parcours de persistance — candidat AA5AF2B

Exécutable `AA5AF2B5862AA38A8DFAFB164F22A77716F80FE8B3EA6D39757CDC5474488775`.
WASM 2 min 33 s, natif 15,30 s. `compact-terrain-aa5.json/png` : **PASS**
1280 × 720 CSS DPR 1, panneau Terrain 487/487, canvas dégagé. Dialogue de
fermeture inspecté : texte d'état à largeur normale, trois commandes visibles.

Parcours Computer Use dans le seul workspace isolé `authoring-root`, zone
`03992311-9297-5219-8aa3-ed561640a312`, chêne `2c53a9af-7e03-5eb9-b402-da18f52fe427` :

- X initial `-55.10344` → saisie `-50.10344` → Prévisualiser → Appliquer.
- Annuler puis reprendre les valeurs de la carte : X `-55.10344` constaté.
- Rétablir puis Sauvegarder. Archive : reçus edit/undo/redo aux révisions 9/10/11,
  X respectifs `-50.10343933105469`, `-55.10343933105469`, `-50.10343933105469`.
- Fermeture native, redémarrage, ouverture de la même zone, sélection du chêne :
  X `-50.10344`, neuf champs visibles, session synchronisée. **Persistance PASS**.
  Preuve `transform-reopened.json/png`, diagnostic de disposition PASS.

L'archive de test a donc volontairement changé : SHA-256 avant
`62A89FB273E913782C50FDE56BD80E4A7D79BE32D4F60459993310239566AD44`, après
`A46FDC1EC2AE51FBDE77F72298156933A4A9B02FDA177B235637C2232EF221A8`.
Aucun projet utilisateur modifié ; ne plus employer le hash initial comme
baseline courante. Aucun aperçu ni brouillon non enregistré conservé après redémarrage.

**FAIL ergonomique distinct** : après édition puis changement de révision, le
message de brouillon périmé rallonge le panneau compact et repousse le bouton de
reprise sous le pli (`transform-undo.json/png`, layout FAIL). Le refus d'appliquer
un brouillon périmé fonctionne ; ne pas supprimer cette protection pour faire
passer la mise en page. Correctifs sources : reprise avant aide, texte d'alerte
actionnable, marges resserrées sans suppression de contrôle. Contrastes bronze
renforcés pour les commandes principales et sous-vues. Build lancé ; ces derniers
correctifs ne sont pas encore qualifiés graphiquement. Les onze tests du
diagnostic Node passent ; il relève désormais aussi les valeurs visibles.

Revue de cascade à poursuivre : des règles de survol de l'ancien thème subsistent
en début de `authoring_gold.css` (notamment commandes principales et dock).
Le contraste des fonds au repos ne suffit pas à qualifier les états survol/focus ;
les vérifier ensemble dans la prochaine passe avant tout verdict accessibilité.

## Brouillon compact — candidat 250048C

Exécutable `250048C936EB24E4D5A1C8A5C668056F2273C9646A06B2D374B9078AACD866BC`.
Clippy WASM strict PASS, WASM release 2 min 35 s, natif 30,30 s (attente de cache
comprise). Dans la zone isolée, saisie X `-`, puis Annuler : brouillon conservé,
champs périmés non applicables, alerte et bouton de reprise entièrement visibles.
`compact-stale-250.json/png` **PASS** à 1280 × 720 DPR 1 ; inspection Computer Use
confirmée après stabilisation. Fermeture sans sauvegarde : SHA de l'archive
inchangé `A46FDC1E…EF221A8`, aucune nouvelle révision persistée par cet essai.

Suite : survol des commandes principales/dock/activité et commande Jouer corrigés
dans les sources. Dix tests de palette source PASS (contraste texte sur les deux
extrémités des dégradés opaques >= 4,5:1), sans prétendre tester toute la cascade
ni toute l'accessibilité. Diagnostic étendu au bouton de reprise. Candidat de
test configuré en 3840 × 2160 logiques ; build lancé, taille effective à mesurer
car Windows peut borner la fenêtre. Ce n'est pas une preuve d'écran physique 4K.

## Limite de mesure 4K

Candidat `4E4EE1675C68B7D2770D03CF143495D11CB41D616B48AD2BF697AF7B16B2D9E0`,
WASM 2 min 42 s, natif 17,89 s. Demande native 3840 × 2160 : taille effective
**3840 × 1421 DPR 1**, moniteur 2560 × 1440 (`large-native-initial.json/png`).
La fenêtre dépasse horizontalement le moniteur ; ce n'est pas une surface 4K
complète utilisable et elle ne sera pas conservée comme configuration de livraison.

Essai ponctuel de métriques émulées via CDP : expiration de réponse, puis de
`Emulation.clearDeviceMetricsOverride`. Aucun verdict ni artefact 4K valide.
Session vide fermée normalement via le dialogue natif (absence du processus
vérifiée), pas de projet ouvert ou modifié. Mode expérimental retiré du diagnostic,
qui refuse maintenant les arguments d'émulation ; retour au diagnostic en lecture
seule. Configuration locale remise à 1280 × 720, reconstruction lancée.

Une dernière règle de survol d'activité ajoutée pendant le précédent build n'est
pas incluse dans 4E4EE167 (token absent du WASM vérifié) ; le rebuild suivant doit
la prendre en compte. **21 tests Node PASS** (11 géométrie, 10 palette). Le contrôle
4K et les DPI physiques restent dus ; ne pas convertir cet échec de mesure en PASS.

## Thème courant et panneaux flottants — candidat 7E9E353

Exécutable `7E9E353A120812B2B0E9F030FADCBBF7BDF46C985A1D297C337ECD4CBEC95ED8`.
WASM 2 min 31 s, natif 2 min 36 s, attente du build Tidewater dans le cache partagé
comprise ; aucun processus Tidewater interrompu. Candidat revenu à 1280 × 720.
CSS exact trouvé dans le WASM ; le thème lu dans la WebView correspond aussi au
fichier source, SHA-256 normalisé `74a34cfbe729fc62a14fc11e69a615c5c0a1765754fe03c2c106f8b95c7b1a5d`
(`theme-current.json/png`). Le diagnostic consigne désormais cette comparaison
et les styles calculés des commandes actives, sans changer l'application.

Parcours Computer Use, sans zone ouverte : grip Projet glissé de (1050,164) à
(600,220), panneau effectivement flottant ; bouton − le réduit à son en-tête,
bouton + restaure le contenu ; Ancrer → Réinitialiser la disposition replace
le panneau à droite et ferme le menu. **Comportements observés PASS**.
`floating-drag.json/png` et `docking-reset.json/png` restent **layout FAIL** :
le contenu Projet est plus haut que son panneau compact et défile. Ne pas présenter
les comportements d'ancrage comme une qualification globale de cette géométrie.
Aucune carte ni archive modifiée par ce parcours.

## Clavier et saisies entre fenêtres — 7E9E353

Après focus du sélecteur de zone : flèche bas, Entrée, Tab vers Ouvrir (contour
visible), Entrée ouvre R2-004 Final B. Dans le secondaire, Tab du champ X passe
au champ Y avec sélection et contour visibles. **Parcours clavier ciblés PASS**,
sans prétendre qualifier tous les raccourcis ou tous les contrôles.

Secondaire natif initial 1920 × 1009 DPR 1 : `secondary-current.json/png` PASS,
thème courant confirmé. Après réactivation, fenêtre restaurée 1280 × 840 DPR 1
sur moniteur 1920 × 1080 (surface disponible 1920 × 1032). Cette transition n'est
pas une preuve de changement physique de moniteur ou de DPI.

Saisies alternées : secondaire X `-51.25` reçu dans le principal ; principal Y
`8.5` reçu dans le secondaire, X préservé. `secondary-alternating.json/png` et
`main-alternating.json/png` PASS géométrie. Puis Y `-` dans le secondaire,
Alt+F4 : principal conserve X `-51.25` et Y `-` (`alternating-repatriated.json/png`,
PASS). Ce parcours n'est pas une collision simultanée de deux écritures ; celle-ci
reste couverte par le test CAS qui refuse l'écriture concurrente sans perdre le
brouillon accepté. Fermeture finale sans sauvegarde et absence du processus
vérifiées, hash de l'archive inchangé `A46FDC1E…EF221A8`.

**Défaut reproduit** : Prévisualiser avec Y `-` refuse correctement la commande,
mais seul le principal affiche « Nombre incomplet ou invalide » ; le footer du
secondaire reste vide (`secondary-invalid-preview`, `main-invalid-preview`).
Le PASS de géométrie ne couvre pas ce défaut fonctionnel. Correction : résultat
de commande publié en texte borné dans le snapshot de présentation, affiché dans
le footer secondaire ; aucune nouvelle autorité de document, aucun bypass de
validation. Test ajouté sur persistance du résultat au polling et rejet atomique
d'un texte hors quota. Protocole **6 PASS**, suite Studio complète **111 PASS,
1 ignoré**, Clippy WASM strict PASS.

## Rejeu du résultat secondaire — 6DD1C08

Candidat SHA-256 `6DD1C08BB038A5E3CBEBE872DD88AE48A07E723D5DF0DFFC65B3A8A225E8066D`.
Build WASM release 2 min 56 s et natif 49,71 s terminés avec succès ; même cache
et candidat remplacé, aucun processus Tidewater interrompu.

Computer Use : Y `-`, puis Prévisualiser dans la carte. Après synchronisation,
le footer secondaire affiche bien « Nombre incomplet ou invalide ».
`secondary-invalid-fixed.json/png` (1920 × 1009, DPR 1) et
`main-invalid-fixed.json/png` (1280 × 720, DPR 1) : **layout PASS**, thème rendu
identique au source. Le refus est désormais visible sur les deux écrans.

Y remis à 7 : retour « Zone edit has no effect » (texte anglais à améliorer).
Y changé à 7,5, puis aperçu : ancien refus remplacé par « Session synchronisée » ;
le principal présente un aperçu non appliqué et la commande Appliquer active.
`secondary-valid-fixed.json/png` et `main-valid-repatriated.json/png` : **PASS**.
Alt+F4 du secondaire conserve le brouillon Y 7,5 dans le principal. Aucune commande
Appliquer ni Sauvegarder utilisée. Fermeture normale sans enregistrer ; processus
candidat absent, archive isolée inchangée SHA-256
`A46FDC1EC2AE51FBDE77F72298156933A4A9B02FDA177B235637C2232EF221A8`.

Ce rejeu clôt le défaut de diffusion du résultat, pas la qualification globale :
4K/DPI physique, autres panneaux et contrôles d'accessibilité restent ouverts.

## Projet compact et audit Chemins — 0B766F9

Candidat `0B766F9A48C402EF4598B3D1B24EB02353BC1A0534B89BC5608BF5E25A7AF26E`,
WASM 2 min 51 s, assemblage natif 1 min 12 s attente cache comprise. Le cache
et le candidat existants sont réutilisés. `project-compact-fixed.json/png` :
1280 × 720 DPR 1, panneau 487/487 px (contre 536/487 auparavant), **layout PASS**,
thème rendu courant confirmé. Capture native inspectée après activation Studio :
toutes les rubriques Projet initiales visibles. Le premier instantané Computer
Use avant activation montrait une autre surface malgré l'arbre Studio ; il n'est
pas retenu comme preuve graphique. Le PNG CDP cible uniquement la route Studio.

Ouverture R2-004 Final B et sélection du chemin dans la hiérarchie :
`paths-compact-audit.json/png` **FAIL**, panneau 669/487 px ; Examiner le tracé
sous le pli. Correction source ajoutée : propriétés/points regroupés dans un
`details` conservant les contrôles montés ; bouton Examiner hors de cette section,
titre Chemins redondant retiré en compact. Rebuild et rejeu graphique encore dus.
Le libellé traduit sans effet est intégré à 0B766F9 mais son rejeu graphique reste
à faire. Aucune retouche appliquée ou sauvegardée dans cette session.

## Chemins corrigés et revue des sujets — 05B0119

Candidat `05B01194A3BBC853EF2D6B4BA942700BD4F5C67B72BA6C1230A0BE06205F084A` :
114 tests Studio PASS, 1 ignoré ; WASM release 2 min 29 s, natif 17,35 s.
`paths-compact-fixed.json/png` : **PASS**, 487/487 px en 1280 × 720 DPR 1,
Examiner le tracé visible. Computer Use : ouvrir Propriétés, saisir le nom
`Chemin brouillon QA`, replier puis rouvrir ; valeur conservée. Ni application
ni sauvegarde. Les propriétés avancées ouvertes restent volontairement défilantes.

Même candidat, même zone, preuves compactes :

- `nature-compact-audit` : PASS pour l'état sans couche végétale seulement.
- `water-compact-audit` : FAIL, 567/487 px et Examiner la rivière sous le pli.
- `buildings-compact-audit` : PASS panneau fermé ; ne qualifie pas le formulaire.
- `buildings-open-compact-audit` : FAIL, 565/487 px avec les outils ouverts.
- `followup-compact-audit` : PASS pour l'état initial, détails repliés.

Corrections source suivantes : coordonnées de création de pièce sur une rangée
XYZ étiquetée (Y sol fini), titre Construire compact redondant masqué ; nom de
rivière dépliable, titre Eau et introduction répétée masqués en compact. Le mode
d'emploi du tracé reste visible. Ces corrections nécessitent le prochain rebuild
et une nouvelle preuve visuelle ; aucun PASS extrapolé des captures précédentes.

## Eau et création de pièce compactes — 87424E8

Candidat `87424E8739CC2D05F6076BB1493B90D7887A858BFA93AC0368FC3EBA30EEE3ED` :
WASM release 5 min 38 s (moteur partagé recompilé), natif 15,19 s, sorties 0.
Même cache et même candidat remplacé, aucun processus concurrent interrompu.
Revue Computer Use dans la zone isolée R2-004 Final B, client 1280 × 720 DPR 1 :

- `water-compact-fixed.json/png` : **PASS**, panneau 487/487 px ; largeur,
  profondeur, dessin et Examiner la rivière visibles. Nom et détails repliés.
- `buildings-open-compact-fixed.json/png` : **PASS**, outils de création ouverts,
  panneau 487/487 px ; X/Y/Z, Prévisualiser une nouvelle pièce et sélecteur visibles.
  Le groupe accessible précise que Y est l'altitude du sol fini. Aucun bâtiment
  créé : cette preuve ne qualifie pas l'inspecteur d'un bâtiment existant.
- `feedback-noop-french.json/png` : **PASS**, aperçu sans modifier le chêne refusé
  avec le texte français « Aucun changement à prévisualiser… Modifiez une valeur
  pour créer une retouche. » ; Appliquer reste désactivé et géométrie correcte.

Thème rendu égal au source, SHA normalisé
`0d9813b1a476313aa2c8d114f4a6320a1382fc18cc36681ddbc6977829ec3427`.
Fermeture sans sauvegarde et absence du processus vérifiées ; archive toujours
`A46FDC1EC2AE51FBDE77F72298156933A4A9B02FDA177B235637C2232EF221A8`.
Les états avancés/peuplés, 4K et DPI mixtes restent hors de cette preuve ciblée.

## Inspecteur de bâtiment peuplé — audit 87424E8

Dans la session isolée : prévisualiser puis appliquer une pièce 8 × 6 × 3 m à
l'origine, sélectionner `Bâtiment · Pièce` dans la hiérarchie. Aucun Save.
`building-selected-compact-audit.json/png` : **FAIL**, 1873/487 px ; le premier
écran est occupé par création, identifiant et explications avant les réglages.
Cette pièce à Y=0 ne constitue pas une preuve de rendu au-dessus du terrain.
Le parcours sert uniquement à auditer l'inspecteur d'une entité acceptée.

Correction source : quatre rubriques montées Position / Volume / Porte / Gérer ;
les coordonnées XYZ partagent une rangée, les épaisseurs et identités sont
dépliables, le formulaire de création se replie à la sélection. Les commandes
existantes, verrous et signaux de brouillon restent utilisés. UUID accessible dans
Gérer, plus dans le titre ou chaque libellé du sélecteur. État technique regroupé
dans Conseils et état de construction. Clippy WASM strict PASS (34,60 s),
22 tests Node PASS incluant la palette du nouveau sélecteur de rubrique.
Rebuild et qualification visuelle/comportementale des quatre rubriques dus.

Fermeture normale sans sauvegarder la pièce temporaire ; processus absent et
archive inchangée `A46FDC1E…EF221A8`. Aucun fichier utilisateur supprimé.

## Quatre rubriques bâtiment — audit 94A392D

Candidat `94A392DDD62A5DC7D950C183C34A0FDBCD8593860E89B6DC0E7DF2FB3CC95FEE`,
WASM release 2 min 32 s, natif 1 min 49 s avec attente du cache partagé.
Pièce temporaire appliquée à (0, 7, 0), sans sauvegarder l'archive isolée.
Les preuves `building-pose-tabs`, `building-volume-tabs`, `building-door-tabs`,
`building-manage-tabs` et `building-preserved-preview` sont toutes **FAIL** :
largeur héritée de navigation 300 px, champs et actions rognés à droite.
La mesure inclut maintenant les actions et champs du bâtiment, pas seulement
la barre générale. Le survol d'une rubrique active manque aussi de contraste.

Computer Use : X=2 conservé en passant Position → Volume → Porte → Gérer →
Position, mais revenu à 0 après la prévisualisation de la pose X=2. La réponse
de commande reconstruisait l'inspecteur bien que le bâtiment accepté soit inchangé.
Thème rendu `1abab6208698dbce4466aade312306647f600b94ef86aaa2fe1d6725f4c50b86`.
Fermeture sans sauvegarde, processus absent et archive `A46FDC1E…EF221A8` inchangée.

Corrections source : mémo du bâtiment accepté pour préserver l'inspecteur lors
des réponses sans changement de source ; largeur de navigation 100 % ; contraste
actif-survol explicite ; dimensions en rangées de trois puis deux champs ; création
masquée lors de l'inspection (accessible via Nouvelle pièce…). Clippy WASM strict
PASS (11,59 s), 23 tests Node PASS. Rebuild/rejeu dus avant qualification visuelle.

## Inspecteur bâtiment compact corrigé — B51183D

Candidat `B51183D6B27C9AC85045A35F04B3B0643AB18AB54D2F52BE2953CDECE59A2041`,
WASM release 2 min 38 s, natif 41,07 s, sorties 0. Computer Use sur la même pièce
temporaire à (0, 7, 0), client natif 1280 × 720, DPR 1. Thème rendu/source
`f974ff9e5a52cd48996011346f927ff98f2879283aee47920b0909d2d614010a`.

- `building-pose-tabs-fixed`, `building-volume-tabs-fixed`,
  `building-door-tabs-fixed`, `building-manage-presented-fixed` : **PASS**,
  487/487 px, actions principales entièrement visibles, aucun débordement horizontal.
- Survol actif Volume : texte `rgb(255,247,232)`, dégradé `rgb(117,91,54)` vers
  `rgb(77,57,31)` ; rendu conforme à la palette contrôlée, sans texte clair sur clair.
- X=2 conservé entre les quatre rubriques puis après réponse de prévisualisation :
  `building-preserved-preview-fixed` **PASS**, Appliquer actif, champ X=2.
- Application, Annuler, Rétablir : X=2 → 0 → 2 confirmé dans l'UI et dans
  `building-pose-undo` / `building-pose-redo`, géométrie **PASS**.

Le premier `building-manage-tabs-fixed` reste **FAIL** dans les archives : le
diagnostic incluait Prévisualiser la copie, enfant d'un details fermé conservant
des rectangles dans WebView. Correction du diagnostic uniquement : exclure le
contenu explicitement replié, conserver son summary et tout contenu déplié, y compris
les contrôles hors écran/rognés. Test de régression incluant details imbriqués ;
24 tests Node PASS. La nouvelle capture `building-manage-presented-fixed` est le
rejeu valide ; aucun ancien résultat réécrit. Duplication dépliée non qualifiée ici.

Fermeture normale sans sauvegarder les opérations temporaires, processus absent,
archive toujours `A46FDC1EC2AE51FBDE77F72298156933A4A9B02FDA177B235637C2232EF221A8`.
Ce lot qualifie l'inspection compacte et le cycle de pose, pas les autres commandes
de bâtiment ni la matrice 4K/DPI mixtes ni la clôture de Gate 2.

Revalidation finale ciblée Studio : `cargo test -p forge_studio --locked`, sortie 0,
106 tests bibliothèque + 8 natifs PASS, 1 comparaison CPU explicitement ignorée.
Compilation de tests 1 min 24 s incluant attente du cache partagé. Aucun build
concurrent interrompu ; E: 133,7 Gio libres au contrôle de fin, candidat unique.

## Secondaire en fenêtre compacte — B51183D

Rejeu natif Computer Use, sans recompilation : `secondary-restored-audit`
**PASS**, 1280 × 840 DPR 1. Redimensionnement natif jusqu'à 1280 × 671 puis
960 × 671 : `secondary-1280x671` et `secondary-minimum-audit` **PASS**. Le nom
de ce dernier fichier n'est pas une preuve de hauteur minimale 640 : la mesure
effective reste 671. Les essais de déplacement de bord qui n'ont pas changé la
taille ne sont pas des preuves des dimensions visées. La bibliothèque centrale
se parcourt par défilement, les huit sujets et l'inspecteur restent accessibles.

Contrat de diagnostic renforcé : les champs effectivement présentés doivent
être mesurés et entièrement visibles sur les deux surfaces. 25 tests Node PASS,
incluant refus d'une mesure absente ou d'un champ rogné. Avec ce contrat,
`secondary-compact-preview` **PASS** à 960 × 671, neuf champs visibles, Y=7.5.
Après demande d'aperçu, Appliquer est actif dans le principal ; rapatriement
`compact-draft-repatriated` **PASS** avec Y=7.5 conservé. Aucun Apply ni Save.

Défaut de retour observé : le secondaire indique seulement Session synchronisée,
même quand le principal porte un aperçu non appliqué. Correctif source : publier
le statut de traitement et l'existence réelle de l'aperçu, avec instruction de
validation dans le principal ; conserver les diagnostics qui coexistent avec un
aperçu précédent. Rebuild et nouvelle preuve visuelle nécessaires.
Session fermée normalement, processus absent, archive `A46FDC1E…EF221A8` intacte.
Ces fenêtres DPR 1 n'attestent pas la mise à l'échelle Windows à 150 % ni la 4K.

## Retour d'aperçu secondaire explicite — 7BB2480

Candidat `7BB24802D09ADEC3AC930103CB32652E00B9FC956A12566792900CF87FE471DB` :
5 tests ciblés `authoring_feedback` PASS (version finale), Clippy WASM strict PASS,
25 tests Node de contrat/palette PASS. WASM release 5 min 30 s (dépendances moteur
recompilées dans le cache partagé), natif 24,49 s, sorties 0. Même candidat remplacé.

Computer Use : ouvrir le chêne, saisir Y=7.5, attendre sa conservation, demander
l'aperçu depuis le secondaire. `secondary-preview-status-fixed` **PASS** en
1920 × 1009 DPR 1 ; message visible : « Session synchronisée · Aperçu prêt · non
appliqué. Vérifiez la carte, puis cliquez sur Appliquer dans la fenêtre principale. »
`secondary-compact-status-fixed` **PASS** en 960 × 840 (pas 640), neuf champs
et message visibles. Le redimensionnement tenté n'a changé que la largeur.

Abandonner l'aperçu dans le principal : `secondary-preview-cancelled` **PASS**,
message remplacé par « Aperçu abandonné ; carte appliquée inchangée. »
Rapatriement `main-cancelled-repatriated` **PASS**, Y=7.5 conservé comme brouillon,
aucune application ni sauvegarde. Fermeture normale sans enregistrer, processus
absent et archive `A46FDC1E…EF221A8` inchangée. E: environ 133,2 Gio libres.

Essai Ctrl+KP_Add sur le secondaire : aucun changement mesuré, toujours DPR 1
et 1920 × 1009 ; Ctrl+0 envoyé ensuite. `secondary-zoom-attempt` n'est donc
**pas une preuve de zoom ou DPI > 1**, malgré son PASS de géométrie courante.
Les contrôles matériels 4K et DPI mixtes restent ouverts ; aucune clôture de gate.

## Contraste des petits libellés — correction source du 25 septembre

Audit des palettes opaques et de leurs arrêts de dégradé : eyebrow secondaire
3,11:1 sur `#eee5d7`, sous-titre projet 3,29:1 sur `#e5dbcc`, catégorie d'asset
principal sélectionné 2,99:1 sur `#e4cfaa`, catégories secondaire et sujet
sélectionnés 2,50:1 et 2,47:1 sur `#c6ad87`. Petits textes, seuil retenu 4,5:1.
Neuf nouvelles assertions initialement en échec (dont l'absence de règle dédiée
au petit texte sélectionné) ; ce constat est source, pas un audit WCAG complet.

Correction : bronze texte `#68543a` pour les libellés concernés et l'accueil ;
`#4f3c24` pour les petits textes des boutons secondaires sélectionnés. Le premier
donne au moins 4,73:1 sur les fonds testés, le second 4,85:1 sur `#c6ad87`.
Les valeurs d'accueil sont testées contre la base opaque du canvas ; textures,
transparences et lissage ne sont pas qualifiés par ce seul test de palette.

34 tests Node PASS après correction. Le lecteur de règles de test conserve les
propriétés antérieures lorsqu'une règle responsive ne change que le padding.
Le diagnostic natif collecte désormais les styles calculés et fonds des libellés
concernés pour contrôler la cascade lors du prochain rejeu. Rebuild et preuve
visuelle encore dus ; aucun PASS graphique extrapolé de ces tests source.

Point à examiner au rejeu clavier : le contour global `#8a5f28` contraste à
1,64:1 et 2,47:1 avec les deux couleurs opaques du dock sombre, contre 4,44:1
sur le panneau clair `#ece4d6`. Les fonds réels du dock sont translucides ;
vérifier le focus affiché et son contexte avant de qualifier le repère clavier.
Ce point n'est ni corrigé ni qualifié par les 34 tests de palette/contrat.

## Rejeu du contraste et fermeture concurrente — candidat FEF82A6

SHA exécutable `FEF82A6CF2416359507E396E321559DCA4D90128E344CD8D5C4BF2AD5DA915CB`.
`contrast-welcome-fixed` : principal 1280 × 720 DPR 1, géométrie PASS,
libellés d'accueil calculés `#68543a`. Le champ historique `fullyVisible=false`
sur ces textes reflétait le hit-test de leur conteneur `pointer-events:none`,
pas leur géométrie. Renommé `hitTestVisible` pour les échantillons de texte.
`contrast-secondary-rendered` et `contrast-secondary-selected-fixed` : secondaire
1920 × 1009 DPR 1 PASS, petits textes ordinaires `#68543a`, textes des boutons
sélectionnés `#4f3c24`, y compris catégorie d'asset sélectionné. Captures inspectées.
Le fichier `contrast-secondary-fixed` est une capture du **principal** par omission
de l'argument de surface ; exclu des preuves du secondaire.

`dock-keyboard-focus-audit` : Tab de Projet vers Terrain, focus présent mais brun
sur dock sombre. Correction source dédiée : contour ivoire `#fff0cc` uniquement
sur les boutons du dock. 35 tests Node PASS, dont un nouveau test de ce contraste.
Ce correctif n'est pas inclus dans FEF82A6 : les captures secondaires tardives
signalent donc correctement un hash de thème différent de la source courante.
Hash CSS rendu `2cde5dda63c23dc1ad6155deedf23e82fc5178141a6c3780e17c75e9616f229b`.

Au clic Rapatrier depuis le secondaire vide, retour effectif au principal mais
message `runtime error: failed to receive message from webview`. Le polling natif
peut garder un handle de la fenêtre pendant sa destruction : correction bornée
de récupération, qui ne tolère l'erreur que si la fenêtre n'existe plus après
l'appel. Les erreurs d'une fenêtre encore présente restent retournées. Test
de régression ajouté ; cause probable à confirmer par rejeu, pas encore PASS GUI.
Session fermée normalement, processus absent, archive isolée `A46FDC1E…EF221A8`
inchangée. Reconstruction unique lancée avec configuration de revue 4K temporaire.

Validation après correction native : `cargo test -p forge_studio --locked`,
117 PASS (108 bibliothèque + 9 natif), 1 comparaison CPU explicitement ignorée.
Le nouveau test distingue disparition de fenêtre, vraie erreur de fenêtre encore
présente et succès sans relecture inutile. Build WASM puis natif encore en cours.

## Zoom natif du candidat 28291A4 — limites constatées

Build terminé : WASM 5 min 57 s, natif 27,47 s. SHA
`28291A42F9C355BA77467A414F1626AD756F44AAB266F13BDF7FA863C3B9D840`.
Thème rendu identique à la source `42932acc…63f425e`.
`native-4k-welcome` mesure encore **3840 × 1421 DPR 1**, malgré une hauteur
demandée de 2160 et des limites maximales explicites. Ce n'est pas du 4K complet.

Avec les raccourcis de zoom activés uniquement dans la configuration de revue :

| Capture | Dimensions CSS | DPR | Verdict de disposition |
|---|---|---|---|
| enabled-zoom-110 | 3491 × 1292 | 1,10 | PASS |
| enabled-zoom-125 | 3072 × 1137 | 1,25 | PASS |
| enabled-zoom-150 | 2560 × 947 | 1,50 | FAIL : panneau Projet 721 / 676 px |
| enabled-zoom-175 | 2194 × 812 | 1,75 | FAIL : panneau Projet 731 / 541 px |
| enabled-zoom-200 | 1920 × 710 | 2 | PASS, règles compactes actives |

Accueil sans document ouvert ; ces mesures ne qualifient ni un parcours de mapping,
ni le DPI Windows, ni un déplacement physique entre moniteurs. Canvas et dock
passent à tous ces paliers ; compaction du panneau trop tardive à corriger.
Zoom remis à 100 %, fermeture au clavier confirmée après vérification du focus
sur Quitter sans enregistrer. Captures `close-large-window-focus*` sont des
diagnostics du dialogue modal, pas des preuves de disposition de l'éditeur.
Essai natif suivant : minimum temporaire de hauteur 2160 dans le candidat seul,
sans recompilation WASM ; restauration de la configuration normale ensuite.

Le candidat de cet essai est `B6FC2FE2E957D6F08228C1D40A4CADBEBC423A943B3D559D85B7F153B80D51D2`
(natif 17,48 s). `native-4k-minheight` reste 3840 × 1421 DPR 1 : piste non
concluante, sans manipulation des paramètres Windows. Fermeture au clavier
confirmée et aucune fenêtre candidate restante. Configuration source de revue
restaurée à 1280 × 720 / minimum 1100 × 600, sans maximum ni zoom QA.

Correction source suivante : panneau Projet compact sous 1000 px logiques,
avec espacement réduit et titre/description déjà présents dans l'en-tête retirés.
Les règles du dock/canvas à 760 px ne changent pas. Validation native Clippy et
reconstruction WASM/natif lancées ; les deux FAIL de zoom restent ouverts jusqu'au
rejeu de cette correction. Focus ivoire et course de rapatriement également à rejouer.

### Rejeu 0550930 — dimensions, focus et retour mono-écran

Candidat `05509302BECBF3F504D96B6F73FC85D0FE0857EEC5278A6B6104B7B2D04AEB63`.
Clippy natif strict PASS ; WASM release 2 min 38, natif 15,42 s.
CSS rendu/source concordant : `5dfc7fdce02b3edd188b562f00af53deb891c2afc3916e199b621492f44f94e6`.

- `ivory-focus-fixed` : navigation clavier vers Terrain, contour ivoire
  rgb(255,240,204), 2 px, visible ; capture inspectée.
- `project-wqhd-125-fixed`, `-150-fixed`, `-175-fixed` : respectivement
  2048 × 1095 / 1707 × 913 / 1463 × 782 CSS, DPR 1,25 / 1,5 / 1,75.
  Panneau Projet sans débordement, PASS ; PNG physiques 2560 × 1369.
  Ce rejeu valide la correction dans ces dimensions, pas le DPI Windows.
- `native-4k-resized`, `object-native-4k-restored`, `object-native-4k-view` :
  client natif 3840 × 2160 DPR 1 après redimensionnement Windows, géométrie
  PASS, neuf champs visibles pour les deux derniers. Fenêtre hors limites
  physiques du moniteur ; les trois modes de capture CDP expirent.
  Aucune preuve visuelle 4K complète. `object-native-4k` sans suffixe est
  en réalité WQHD : ne pas le citer comme preuve 4K.
- `restored-wqhd-object` : 2560 × 1369, neuf champs, capture complète PASS.
- `dual-final-0550930` : secondaire 1920 × 1009, neuf valeurs identiques,
  session synchronisée, géométrie et capture complète PASS. Clic natif
  « Rapatrier les panneaux » : secondaire fermé, bouton « Deux écrans »
  rétabli, sélection et neuf valeurs conservées, aucune erreur dans l'en-tête
  après rafraîchissement. Un rejeu réussi ne prouve pas tous les entrelacements.

Le diagnostic écrit désormais la géométrie avant la capture ; un timeout
conserve cette preuve limitée. Le contrôle PNG vérifie dimensions physiques
attendues et refuse de qualifier un recadrage. Six nouveaux tests : 41 tests
Node PASS au total. Une géométrie PASS avec capture incomplète renvoie exit 1.

Configuration QA 4K réappliquée pendant cet essai (contrairement à l'intention
de restauration du journal précédent), puis retirée après fermeture normale :
1280 × 720, minimum 1100 × 600, sans maxima ni raccourcis de zoom expérimentaux.
Reconstruction native du candidat normal en cours ; un seul candidat remplacé.
Gate 2 et objectif global restent ouverts, notamment DPI mixtes et hotplug.

Remplacement normal terminé en 23,91 s : candidat
`F45BDC50E2E51442724374CA24628AA334E66F5383BFDB6F4DD04F5BE54D7D29`.
`normal-final-f45bdc5` : ouverture native 1280 × 720 DPR 1, accueil Projet,
panneau 487 / 487 px, huit sujets, canvas dégagé, CSS concordant,
géométrie et capture complète PASS. Archive isolée inchangée, SHA-256
`A46FDC1EC2AE51FBDE77F72298156933A4A9B02FDA177B235637C2232EF221A8`.
Les 41 tests Node ont été relancés PASS et le diff ciblé ne contient pas
d'erreur d'espacement. Candidat normal laissé ouvert pour la suite des essais.

### Rejeu final sur F45BDC5 : mono / dual / fermeture native

Sans compilation supplémentaire, ouverture de R2-004 Final B via le sélecteur
Projet puis sélection du chêne dans la scène, au moyen de Computer Use.

| Capture | Client natif, DPR 1 | Champs visibles | Géométrie / PNG complet |
|---|---|---|---|
| final-compact-object | 1280 × 720 | 9 / 9 | PASS / PASS |
| final-wqhd-object | 2560 × 1369 | 9 / 9 | PASS / PASS |
| final-fullhd-secondary | 1920 × 1009 | 9 / 9 | PASS / PASS |
| final-after-secondary-close | 2560 × 1369 | 9 / 9 | PASS / PASS |

Toutes conservent X -50.10344, Y 7, Z 89.75291, rotations 0 et échelles 1.
Captures natives inspectées après stabilisation (l'image immédiate pendant
l'animation Windows de maximisation n'est pas une preuve de géométrie finale).
La croix du secondaire déclenche sa fermeture : après rafraîchissement, seul
le principal reste dans l'inventaire des fenêtres, bouton Deux écrans rétabli,
aucun message d'erreur dans l'en-tête. Aucun aperçu/appliquer/sauvegarder effectué.
Hash d'archive recontrôlé identique à A46FDC1E…21A8. Principal laissé ouvert,
secondaire fermé. Ces preuves portent sur F45BDC5 et ne certifient ni le DPI
Windows mixte, ni un conflit simultané de saisies, ni le débranchement matériel.

### Ancrage au clavier — défaut de continuité du focus

Sur F45BDC5, ouverture du menu Ancrer à la souris, Tab vers À gauche,
puis Entrée : panneau réellement déplacé à gauche, neuf champs conservés,
canvas séparé, capture `keyboard-anchor-left` complète et géométrie PASS.
`keyboard-anchor-left-focus` prouve le focus clavier visible sur À gauche
(contour bronze 2 px) mais son verdict global est FAIL : le menu ouvert
recouvre temporairement Placer / manipuler et Fiche. Ce diagnostic intermédiaire
n'est pas une preuve d'éditeur dégagé ; le verdict n'a pas été assoupli.

Défaut observé après Entrée : `focusEvidence.tag=BODY`, le bouton sélectionné
étant caché par la fermeture de details. Correction dans `authoring_dock.rs` :
référence explicite au summary et retour du focus vers Ancrer après chaque
ancrage et après Réinitialiser la disposition. Aucun document métier modifié.
Clippy WASM lancé ; remplacement du candidat et rejeu clavier restent requis
avant de qualifier cette correction. Le candidat F45BDC5 reste ouvert à gauche.

Clippy WASM strict PASS en 1 min 11 s (avertissement futur proc-macro-error2
préexistant). Build release lancé dans le cache partagé. Session F45BDC5 ensuite
fermée normalement et absence du processus vérifiée, archive inchangée. Aucun
nouveau répertoire candidat ni suppression d'historique effectuée.

Rejeu du correctif : WASM release PASS en 5 min 26 s, natif PASS en 22,95 s.
Candidat remplacé : `F3431207451D4D681E75B5AE7FB24D95420662C624E5117B61D9151EF0C46603`.
`keyboard-anchor-focus-fixed` : Projet 1280 × 720, ouverture souris du menu,
Tab puis Entrée sur À gauche. Panneau à gauche et focus sur SUMMARY « Ancrer »,
focus-visible vrai, contour bronze 2 px entièrement visible. Entrée rouvre
ensuite le menu sans souris : continuité clavier observée. Géométrie et PNG PASS.
`anchor-reset-focus-fixed` : clic sur Réinitialiser, disposition à droite,
géométrie et PNG PASS. Cette seconde action a été faite à la souris, pas présentée
comme un parcours entièrement clavier. Aucun document ouvert ou modifié pendant
ce rejeu. Le correctif ne change pas les dimensions CSS ni le protocole secondaire.
