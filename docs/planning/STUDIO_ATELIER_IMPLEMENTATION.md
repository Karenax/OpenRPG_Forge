# Atelier Studio — objectif et plan d'exécution

Demande autorisée le 24 septembre 2026 : implémenter les maquettes ivoire/champagne
mono-écran et double écran, en Full HD, WQHD et 4K. Branche Rust uniquement.

## Objectif vérifiable

Un véritable atelier de mapping, pas un fond illustré : commandes existantes
fonctionnelles, carte éditable au centre, bibliothèque et inspecteurs structurés,
panneaux dockables et atelier secondaire facultatif. La direction reprend les
matières claires, accents bronze, surfaces fumées et hiérarchie des maquettes.
Les paysages et objets générés des maquettes ne sont pas des assets produit.

## Lots

1. **Structure mono-écran** — en cours : palette ivoire/champagne, navigation par
   activité, scène, viewport, inspecteur et bibliothèque ; états vides travaillés,
   typographie lisible, raccourcis et diagnostics accessibles sans dominer l'UI.
2. **Composants visuels** — en cours : cartes d'assets gouvernés avec vignettes
   réelles ou substituts explicitement identifiés, groupes de propriétés,
   sélection cohérente, animations courtes et mode réduction des mouvements.
3. **Atelier détachable** — en cours : fenêtre Tauri locale secondaire, disposition
   et sélection partagées, une seule autorité native de documents, pas de second
   renderer de carte. Rapatriement explicite, fermeture et disparition d'écran
   sûres. Les brouillons ne doivent pas être perdus au transfert.
4. **Qualification** — partielle : commandes, undo/redo, sauvegarde/réouverture,
   cycle mono/dual/mono, fermeture du secondaire, restauration des panneaux,
   captures et contrôles de géométrie pour chaque résolution et densité ciblée.

## Architecture et limites de portée

### Point de qualification du 25 septembre (objectif non clôturé)

| Exigence | Preuve disponible | Limite restante |
|---|---|---|
| Atelier mono-écran | Captures Full HD et WQHD ; inspecteurs compacts B51183D | Pas de validation humaine finale de la réalisation |
| Panneaux thématiques dockables | Déplacement, réduction, restauration et rappel bas/droite observés | Qualification exhaustive de tous les états avancés non faite |
| Secondaire synchronisé | Sélection d'objet, neuf champs, aperçu, fermeture et rapatriement observés | Formulaires métier complets des autres sujets restent dans le principal |
| Petites hauteurs logiques | Secondaire 1280 × 671 et 960 × 671 DPR 1, neuf champs visibles | Ne prouve pas le DPI Windows à 150 % |
| Full HD / WQHD | F45BDC5 : principal 1280 × 720 puis 2560 × 1369, secondaire 1920 × 1009 ; neuf champs et captures complètes PASS | Matrice des densités Windows et de tous les sujets non complète |
| 4K et DPI mixtes | Client natif 3840 × 2160 DPR 1 mesuré après redimensionnement, accueil et neuf champs PASS ; Projet corrigé et rejoué en WQHD aux zooms 125/150/175 % | Capture 4K complète indisponible ; zoom WebView distinct du DPI Windows mixte, non qualifié |
| Retour sûr mono-écran | Brouillons et sélection conservés lors des fermetures/rapatriements testés | Débranchement physique d'écran non effectué |

Le contenu des JSON de capture fait foi, pas leur nom de fichier. Les états à
groupes repliés ne prouvent pas l'ergonomie de tous leurs groupes dépliés. Les
tests Node sont des tests du contrat et de palette, pas des parcours utilisateur.

### Frontières de réalisation

- Leptos/Rust pour la présentation ; CSS pour les matières et transitions.
- Les commandes existantes restent l'unique chemin de mutation des documents.
- La fenêtre secondaire ne peut pas devenir un deuxième propriétaire concurrent
  des brouillons et du viewport. Un protocole de présentation typé doit relier
  les deux surfaces ; les actions périmées seront refusées.
- Les préférences de disposition sont distinctes des documents de carte.
- Les contrôles de la maquette sans implémentation métier ne seront pas présentés
  comme fonctionnels. Ni PNJ, ni gameplay Gate 3 ne sont ajoutés dans ce chantier.
- Les fenêtres hors écran doivent être récupérables depuis le principal.
- Aucune copie de code ou d'assets du simulateur macOS tiers.

## Résolutions et accessibilité

| Surface physique | Densités à vérifier | Attendus |
|---|---|---|
| 1920 × 1080 | 100 %, 125 %, 150 % | commandes principales visibles, panneaux compacts |
| 2560 × 1440 | 100 %, 125 %, 150 % | bibliothèque plus riche, carte agrandie |
| 3840 × 2160 | 100 %, 150 %, 200 % | texte lisible, absence d'agrandissement arbitraire du canvas |
| Deux écrans | résolutions/DPI mixtes | fenêtres indépendantes, sélection et retour sûrs |

Les dimensions CSS/logiques, les pixels physiques et le DPR sont consignés
séparément. Pas de promesse de 60 FPS 4K déduite d'un simple test de mise en page.
Navigation clavier, focus visible, contrastes et `prefers-reduced-motion` requis.

### Exception propriétaire du 26 septembre 2026 — écran 4K physique

Décision explicite : « je n'ai pas d'écran 4k, passons la vérification 4k ».
Les essais sur moniteur physique 3840 × 2160, y compris ses densités 100/150/200 %,
sont donc **non réalisés — matériel indisponible, exception acceptée**. Ils ne
bloquent plus la clôture de cette refonte. La compatibilité responsive 4K reste
dans l'objectif ; les mesures natives de géométrie déjà obtenues sont conservées,
sans les transformer en validation visuelle ou en mesure de performance 4K.
Ne pas redemander un écran 4K ni reconstruire un candidat pour contourner cette
absence. Cette exception ne valide ni les autres essais DPI, ni le hotplug,
ni les conflits de saisie, ni l'acceptation visuelle finale, ni la Gate 2.

## Discipline de livraison

Pas de sous-agents, commit ou publication implicite. Préserver le worktree sale
et les travaux Tidewater. Cache unique `E:\OpenRPG_Forge\.build\cargo-target`,
incrémental désactivé, candidat de revue existant remplacé sans historiques.
Vérifications rapides avant le build graphique coûteux. Ne pas fermer une session
utilisateur ni perdre ses brouillons pour remplacer un exécutable.

L'objectif reste ouvert tant que les lots et vérifications ci-dessus ne sont pas
réalisés. La validation des maquettes n'est ni une preuve d'implémentation ni une
clôture de Gate 2.

## Journal de réalisation

Premier incrément : thème et structure Atelier codés, bibliothèque extraite
visuellement de l'inspecteur en mode Objets, accueil de zone vide et activités
fonctionnelles. Protocole secondaire typé, quotas/ACL et fenêtre native codés ;
le premier raccordement transmet sujet et sélection, sans formulaires détachés.
Vérifications du code : Clippy natif et WASM PASS ; suite Studio 96 PASS,
1 test de comparaison CPU ignoré explicitement. Build WASM release PASS.
Première passe native effectuée : ouverture secondaire, changement de sujet et
rapatriement PASS ; ouverture de carte et géométrie WQHD observées. Voir
`docs/evidence/gate2/STUDIO_ATELIER_FIRST_PASS_20260924.md` pour le candidat exact
et les limites. Préférences de panneaux, récupération hors écran et bibliothèque
compacte ajoutées ensuite ; 99 tests PASS, 1 ignoré, Clippy natif/WASM PASS.
Le catalogue secondaire passe la validation code : 100 tests PASS, 1 ignoré,
Clippy WASM strict PASS. Le build release a abouti (8 min 47 s), puis le candidat
natif a été remplacé dans le même répertoire. Les inspecteurs détachés
et la matrice complète multi-écran/résolutions restent dus.

Continuation : modèle de champs de transformation partagé ajouté (position et
échelle absolues, rotations relatives XYZ), 3 tests ciblés PASS et Clippy natif
strict PASS. Il n'est pas encore raccordé à un inspecteur détaché : ce n'est pas
une capacité utilisateur livrée. Clippy WASM strict PASS après ajout et adaptation
du glissement des panneaux à la hauteur réelle de l'en-tête.

Prise en compte des petites hauteurs logiques : minimum natif 600 px, en-tête et
dock compacts sous 760 px, repli réel de la bibliothèque restituant de la place
au viewport. Vérification graphique encore due.

Deuxième parcours Computer Use : ouverture secondaire, catalogue de 8 références,
sélection de `vv-tree-oak-a` dans le secondaire puis mise en évidence du même
asset dans le principal, rapatriement et conservation de la sélection observés.
Surface principale 1920 × 1080 CSS, DPR 1. Le diagnostic de bibliothèque reste
FAIL : 231 px de contenu pour 218 px disponibles. Les dernières règles de
compaction (cartes 96 px et suppression des marges héritées) n'étaient pas dans
ce binaire ; reconstruction demandée après fermeture normale de cette session
vide. Ne pas réutiliser la capture comme preuve du correctif.

Suite Studio complète après modèle de transformation : **103 PASS, 1 ignoré**.
Le contrôle fmt global rencontre des modifications Tidewater non formatées dans
`forge_engine/src/lib.rs`, laissées intactes ; formatage limité à `forge_studio`.
Correction du relais natif : seules les lectures/publications du consommateur
principal peuvent retirer une intention de sa boîte ; une réponse d'ouverture
ne doit pas perdre une sélection concurrente.

Correctif bibliothèque recompilé : candidat `E2810CA…016DA95`, test Full HD
1920 × 1080 CSS / DPR 1 PASS, huit cartes visibles sans débordement vertical.
Repli de bibliothèque : +180 px de hauteur viewport, PASS. Redémarrage et rappel
du panneau Objets replié : PASS. Voir le rapport de deuxième passe pour les hashes
et preuves. Le repli du panneau cachait la bibliothèque mais réservait encore son
espace : correction source ajoutée, avec suivi des ancrages gauche/flottant/bas ;
ces dernières règles restent à compiler et à vérifier avant qualification.

Inspecteur partagé raccordé dans les sources : neuf champs position/rotation
relative/échelle, propositions via les commandes PreviewZone habituelles, contrôle
de hash et verrou après IPC, brouillons natifs de session avec versions et quota,
fermeture secondaire différée jusqu'à accusé de conservation. Panneau de rappel
dans le principal, sans deuxième document canonique ni renderer supplémentaire.
Tests Studio **108 PASS, 1 ignoré**, Clippy natif et WASM strict PASS. Build candidat
en cours dans le cache unique ; aucun verdict d'édition détachée avant parcours GUI.
La sauvegarde sur disque des brouillons non appliqués n'est pas implémentée ;
ils survivent au rapatriement, pas à la fermeture de l'application.

Le premier build inspecteur a abouti (WASM 2 min 24 s ; natif 16,52 s), mais il
précède la correction de revue suivante : une saisie locale invalide immédiatement
l'ancien aperçu ; son accusé de conservation ultérieur n'invalide pas le nouvel
aperçu. Seules les versions distantes inconnues l'invalident au sondage principal.
Revalidation WASM et reconstruction du même candidat lancées après cette correction.
Ne pas lancer l'ancien binaire comme s'il contenait cette correction.

Reconstruction E5830A5…C81385B terminée. Parcours réel : transformation depuis
le secondaire, rapatriement du brouillon et aperçu principal stable PASS, archive
de référence inchangée après sortie sans enregistrer. Le panneau Objets déborde
encore ; séparation en trois vues ajoutée aux sources. Voir le rapport inspecteur
pour les valeurs exactes et les limites. Recompilation et revue de cette ergonomie
requises avant de réutiliser le candidat pour la qualification de disposition.

Passe suivante : séparation des espaces recompilée (2E0524A…FB6C201), mais
revue visuelle FAIL pour les sous-onglets, héritant d'une position absolue du
menu de scènes. Correction statique ajoutée, non encore dans ce candidat.
Diagnostic renforcé par hit-tests ; neuf tests du diagnostic PASS. Test global
`cargo test --workspace --locked` lancé dans le cache commun, quatre jobs maximum ;
pas encore de résultat. Aucun nouveau dossier de build, aucune publication.

Adaptation des petites hauteurs ajoutée : bibliothèque initialement repliée sous
820 px logiques, repli au franchissement de ce seuil pendant un redimensionnement,
réouverture manuelle toujours possible ; aucun changement du document ni du
brouillon. Formulaire de transformation compact sous 760 px. Le contrôle de
géométrie peut maintenant examiner séparément la fenêtre secondaire, avec
séparation des trois colonnes et visibilité des commandes : dix tests Node PASS.
Les catégories de bibliothèque ont des symboles distincts (arbre, roche,
architecture), explicitement non assimilés à des vignettes GLB. Ces changements
restent à qualifier dans le nouveau candidat ; build en attente du cache occupé
par le test workspace déjà lancé.

Revue complémentaire : les libellés accessibles des neuf champs distinguent
désormais groupe et axe ; une erreur d'action du secondaire n'est plus effacée
par un sondage de connexion réussi. Formatage Studio et diff-check PASS ; ces
correctifs sont inclus dans les sources en attente de compilation WASM.

Résultat global : FAIL moteur, 376 tests moteur réussis, 1 échoué, 2 ignorés.
L'échec isolé reproduit une contradiction de contrat sur le batching végétal
lointain ; aucun changement moteur effectué. Les deux binaires de tests Studio
produits par ce build ont été exécutés séparément : **109 PASS, 1 ignoré**.
Clippy WASM strict PASS après corrections de propriété/signature des pictogrammes.
Reconstruction du candidat lancée. Voir le rapport inspecteur pour le détail ;
ce résultat n'est pas une qualification workspace ni une clôture de Gate 2.

Candidat 5B0A48F compilé puis testé : sous-vues Objets Full HD PASS,
conservation du brouillon entre sous-vues et fermeture native secondaire PASS.
Surbrillance secondaire et ancrage inférieur FAIL ; corrections ajoutées aux
sources et reconstruction du même candidat lancée. Aucune qualification WQHD,
4K ou DPI mixtes supplémentaire à ce stade. Voir le rapport inspecteur.

25 septembre : B962D2D reconstruit. Sélection et géométrie secondaire PASS ;
fermeture par clic sur la croix PASS. Ancrage inférieur amélioré mais encore
41 px de débordement : marges corrigées dans les sources, à reconstruire et
vérifier. Prochain candidat configuré localement à 1280 × 720 logiques pour
examiner la densité compacte, sans changer les réglages Windows.

Essai réel 1280 × 720 DPR 1 : formulaire inférieur tient mais recouvre la carte,
inspecteur latéral encore trop haut. Corrections d'adaptation et de densité
ajoutées, diagnostic anti-chevauchement renforcé (11 tests PASS), disposition
Rust (7 tests PASS), Clippy WASM strict PASS. Rebuild en cours ; ne pas annoncer
encore le mode compact qualifié. Aucun changement de résolution système.

9E9AAB5 : inspecteur Objet compact PASS (1280 × 720 DPR 1). Moniteur WQHD
réel identifié ; fenêtre maximisée 2560 × 1369, inspecteur gauche, bibliothèque
ouverte et panneau inférieur PASS. Cycle petite/grande fenêtre avec adaptation
latérale puis restauration de l'ancrage inférieur PASS. Terrain compact garde
16 px de débordement, correction CSS préparée. 4K et DPI mixtes encore non prouvés.

AA5AF2B : Terrain compact et dialogue de fermeture corrigés, revue PASS.
Parcours réel objet : prévisualiser, appliquer, annuler, rétablir, sauvegarder,
fermer puis rouvrir PASS ; position sauvegardée vérifiée dans l'archive isolée
et dans les neuf champs après redémarrage. L'archive de test est maintenant à
la révision 11, X du chêne -50.10344. Le cas de brouillon périmé conserve un
débordement compact : correction de hiérarchie/marges et contraste préparée,
nouvelle compilation en cours. Cette preuve fonctionnelle ne clôt ni la matrice
résolutions/DPI ni la Gate 2. Voir rapport inspecteur pour hashes et captures.

250048C : brouillon incomplet puis périmé en fenêtre compacte PASS, bouton de
reprise visible sans défilement. Test fermé sans sauvegarde, archive inchangée.
Survol/contrastes supplémentaires corrigés ; dix tests de palette source PASS,
validation des styles calculés encore due. Configuration candidate 3840 × 2160
préparée pour la prochaine mesure réelle de surface, sans réglage Windows.

Mesure native grande fenêtre : Windows donne 3840 × 1421, pas 3840 × 2160.
Essai d'émulation CDP non fiable (timeout), retiré et session vide fermée.
Retour de la configuration candidate à 1280 × 720 ; rebuild final de cette passe
en cours avec tous les survols corrigés. 21 tests Node PASS. 4K/DPI restent non
qualifiés, sans modification des réglages d'écran ni de l'archive de test.

## Point de contrôle de livraison — 25 septembre, après la passe compacte

Cette matrice n'est pas un verdict de clôture. Les références de candidats et les
limites exactes restent dans `STUDIO_ATELIER_INSPECTOR_20260924.md`.

| Exigence | Preuve disponible | Reste à établir |
|---|---|---|
| Atelier clair, structuré, activités et dock | Captures natives mono/secondaire ; thème implémenté | Revue des derniers styles calculés, survol et focus |
| Placement utilisable, formulaire compact | 250048C : neuf champs, alerte périmée et reprise visibles en 1280 × 720 | Régression finale avec le candidat courant |
| Commandes réellement reliées au document | AA5AF2B : aperçu/appliquer/undo/redo/save/restart, reçus 9/10/11 et X relu | Ne pas confondre ce parcours objet avec tous les outils de mapping |
| Panneaux par sujet, dispositions préservées | Tests Rust 7 PASS ; gauche/droite/bas et adaptation au resize ; 7E9E353 : glissement flottant, réduction/restauration/reset à la souris PASS | Géométrie compacte Projet défilante, revue des autres sujets |
| Dual avec sélection partagée | Ouverture, catalogue et sélection synchronisés observés | Concurrence de saisies GUI et dernier thème secondaire |
| Retour sûr en mono-écran | Rapatriement, Alt+F4 avec saisie incomplète, croix native observés | Disparition physique d'écran non simulée ; récupération testée par unités |
| Full HD et WQHD | Surfaces natives 1920 × 1080, 2560 × 1369 DPR 1 ; compact 1280 × 720 | Densités 125/150 % et dimensions utiles après chrome OS |
| 4K | Source responsive et tests de géométrie pure, pas de preuve visuelle 4K | Moniteur/rendu 3840 × 2160 fiable ; tentative native bornée, émulation retirée |
| Clavier, mouvements, contrastes | Libellés des axes, focus CSS, réduction des mouvements ; 10 tests palette PASS | Parcours clavier et contraste calculé des états affichés |
| Discipline disque et autorité | Cache/candidat uniques, archives isolées, aucun push, aucun sous-agent | Continuer à remplacer le même candidat et fermer chaque session de test |

Ordre de la prochaine passe : terminer le build actuel (sans interrompre
Tidewater), vérifier le hash du thème rendu, tester flottant/réduction/reset,
puis clavier et concurrence secondaire. La matrice physique 4K/DPI restera
explicitement ouverte si le matériel disponible ne permet pas de la vérifier.

7E9E353 reconstruit : thème rendu identique au source (hash enregistré), glissement
flottant, réduction/restauration et reset observés. Le diagnostic distingue ces
comportements réussis du débordement compact Projet (layout FAIL, pas masqué).
Prochaine passe : navigation clavier et concurrence secondaire ; aucun build de
cette tâche encore en cours à ce point.

Parcours suivant : ouverture de zone par Tab/Entrée et passage X→Y au clavier
observés ; saisies alternées secondaire→principal→secondaire et rapatriement
d'un nombre incomplet PASS. Refus d'aperçu invalide invisible dans le secondaire
reproduit, correction par diffusion du résultat autoritaire ajoutée. 111 tests
Studio PASS, 1 ignoré ; Clippy WASM strict PASS.

6DD1C08 : rebuild terminé et rejeu Computer Use PASS. Le refus de nombre incomplet
apparaît maintenant dans le secondaire ; une nouvelle prévisualisation valide
remplace ce refus, Appliquer est disponible dans le principal. Retour mono avec
brouillon conservé, fermeture sans sauvegarde et archive inchangée vérifiés.
Preuves `secondary-invalid-fixed`, `main-invalid-fixed`, `secondary-valid-fixed`,
`main-valid-repatriated` : géométrie PASS et thème courant confirmé.
Aucun build ni candidat de cette tâche laissé en exécution. Restent notamment
la revue compacte des autres sujets, les densités et la preuve physique 4K ;
le message sans effet encore en anglais doit être rendu compréhensible.

Passe suivante : `authoring_feedback` traduit exactement les refus natifs sans
effet et périmés, avec une action de reprise ; les autres diagnostics restent
intacts. Trois tests ciblés PASS, Clippy WASM strict PASS. Le résultat continue
de passer par le signal partagé principal/secondaire ; aucune validation métier
changée. En compact (hauteur <= 760), le titre Projet et son paragraphe introductif
redondants sont retirés, sans masquer de commande ni de diagnostic. La capture
`theme-current` montrait 536 px de contenu pour 487 px utiles : cette correction
doit encore être mesurée dans le prochain candidat. Les 21 tests Node passent,
mais ne prouvent pas le rendu de ce changement CSS. Le candidat 6DD1C08 reste
le dernier binaire qualifié ; ces deux ajustements source ne sont pas encore
reconstruits ni validés graphiquement.

0B766F9 reconstruit : Projet compact **PASS** (487/487 px) avec preuve native et
thème courant. Revue Chemins **FAIL** (669/487 px), action Examiner sous le pli.
Correction suivante en source : propriétés dépliables avec champs toujours montés,
prévisualisation hors du groupe et titre redondant masqué en compact. Ne pas
réutiliser le PASS Projet pour qualifier Chemins ; prochain build/rejeu requis.

05B0119 : Chemins compact PASS, contrôle de conservation du nom au repli/dépli
PASS avec Computer Use. Suite Studio 114 PASS, 1 ignoré. Revue élargie : Nature
sans couche et Suivi initial PASS ; Eau et Construire ouvert FAIL, corrections
source regroupées (rangée XYZ, réduction de titres répétés et nom de rivière
dépliable). Prochain rebuild/rejeu dû pour ces deux sujets. Les états avancés et
peuplés ne sont pas qualifiés par les captures d'états vides/repliés.

87424E8 : Eau et création de pièce ouverte compactes **PASS** avec Computer Use,
487/487 px pour chaque panneau et actions principales visibles. Refus d'aperçu
objet sans changement vérifié en français. Archive inchangée et session fermée.
Les correctifs compacts identifiés précédemment sont ainsi reconstruits et rejoués.
Reste à qualifier notamment l'inspection de contenu peuplé/avancé, les densités,
4K, l'accessibilité complète et les transitions physiques d'écran ; aucune clôture
globale ni de Gate 2 déduite de ces PASS ciblés.

Audit suivant sans rebuild : inspecteur d'une pièce temporaire acceptée **FAIL**,
1873/487 px. Refonte source de l'inspection en Position / Volume / Porte / Gérer,
sans démontage des champs lors du changement de rubrique ; création repliée à la
sélection et explications détaillées dépliables. Clippy WASM et 22 tests Node PASS.
Le prochain candidat doit vérifier les quatre rubriques, la conservation des
brouillons et la prévisualisation d'une modification. Pièce de test abandonnée
à la fermeture, archive isolée inchangée.

94A392D : quatre rubriques reconstruites et auditées, **FAIL** horizontal (nav
héritée de 300 px), contraste actif au survol insuffisant. X=2 conservé entre
rubriques mais réinitialisé à 0 après aperçu. Corrections : mémo du bâtiment
accepté, largeur contrainte, palette active-survol, rangées de dimensions, création
masquée pendant inspection. Clippy WASM strict et 23 tests Node PASS ; nouveau
rebuild/rejeu nécessaire. Session isolée fermée sans sauvegarde, archive inchangée.

B51183D reconstruit : Position / Volume / Porte / Gérer **PASS** en 1280 × 720
logiques DPR 1, 487/487 px sans débordement. X=2 conservé entre rubriques et après
aperçu ; Appliquer → Annuler → Rétablir donne 2 → 0 → 2. Survol actif lisible.
Correction du diagnostic pour exclure les descendants de details explicitement
fermés (sans exclure les vrais contrôles rognés), 24 tests Node PASS. Captures
et limites consignées dans le rapport inspecteur. Session fermée sans sauvegarde,
archive isolée intacte. Les contrôles avancés et la matrice 4K/DPI restent dus.

Secondaire B51183D : dispositions natives 1280 × 840, 1280 × 671 et 960 × 671
DPR 1 **PASS** ; neuf champs visibles, brouillon Y=7.5 conservé au rapatriement.
Contrat de visibilité des champs renforcé, 25 tests Node PASS. Retour d'aperçu
secondaire trop vague constaté ; correction source du message (traitement / aperçu
prêt non appliqué / refus conservé) en cours de reconstruction. Pas de preuve
physique DPI mixte ou 4K déduite de ces essais de dimensions logiques.

7BB2480 : retour explicite d'aperçu reconstruit et vérifié par Computer Use,
1920 × 1009 puis 960 × 840 DPR 1 **PASS**. L'abandon depuis le principal remplace
le message prêt par le statut d'annulation dans le secondaire ; brouillon conservé
au rapatriement. 5 tests ciblés de statut et Clippy WASM PASS. Le raccourci de zoom
n'a pas modifié le DPR ; il n'apporte aucune qualification haute densité. Session
fermée, archive intacte, aucun commit/push et un seul candidat.

Audit de lisibilité : neuf contrôles supplémentaires sur les petits libellés,
plusieurs palettes entre 2,47:1 et 3,29:1 avant correction. Textes bronze foncés,
petits textes sélectionnés explicités ; 34 tests Node PASS après correction.
Reconstruction du candidat lancée pour vérifier les styles calculés et le rendu.
Il ne s'agit pas d'une certification d'accessibilité complète.

Piste de qualification 4K à essayer après le build courant : le code local
épinglé Tao 0.35.3 (`platform_impl/windows/event_loop.rs`, WM_GETMINMAXINFO)
remplace `ptMaxTrackSize` lorsque des limites maximales explicites existent.
La configuration Tauri utils 2.9.3 expose `maxWidth` / `maxHeight` en pixels
logiques et `zoomHotkeysEnabled` (false par défaut, expliquant le raccourci sans
effet). Un essai borné peut modifier uniquement la configuration du candidat de
revue : client 3840 × 2160, limites 4096 × 2304 et zoom activé pour mesurer les
DPR, puis restaurer la configuration habituelle. Ne pas confondre cette fenêtre
potentiellement hors écran avec un moniteur 4K ni le zoom WebView avec le DPI
Windows. Aucun résultat de cette piste n'est acquis à ce stade.

FEF82A6 : contraste des libellés vérifié dans l'accueil et le secondaire ; focus
clavier du dock encore trop sombre, règle ivoire ajoutée et 35 tests Node PASS.
Rapatriement secondaire vide effectué mais message natif intermittent observé :
la récupération tolère maintenant un handle disparu pendant la fermeture, sans
masquer une erreur de fenêtre toujours présente. Rejeu requis. Configuration
4K de revue appliquée, reconstruction en cours ; aucune qualification 4K acquise.

Suite Studio 117 PASS, 1 ignoré. Candidat 28291A4 : zoom WebView réellement
mesuré jusqu'à DPR 2, mais Projet déborde à 2560 × 947 / 2194 × 812 CSS.
Correction ciblée du panneau Projet sous 1000 px logiques, reconstruction en cours.
Deux configurations natives 4K (maximum explicite, puis minimum 2160) restent
limitées à 3840 × 1421 ; aucune preuve 4K complète. Configuration de revue remise
en 1280 × 720, sans minimum/maximum 4K ni raccourcis de zoom expérimentaux ; elle
sera réintégrée au remplacement en cours. Sessions de test fermées normalement.

Rejeu 0550930 : focus ivoire observé, Projet sans débordement aux zooms WQHD
125/150/175 %, secondaire 1920 × 1009 synchronisé puis rapatrié sans erreur.
Redimensionnement natif permettant finalement 3840 × 2160 : géométrie accueil
et neuf champs PASS, mais captures complètes en timeout ; pas de qualification
visuelle 4K physique. Diagnostic renforcé contre les captures tronquées,
41 tests Node PASS. Voir le rapport inspecteur pour les preuves et limites.
Configuration expérimentale effectivement retirée puis candidat normal F45BDC5
reconstruit : ouverture 1280 × 720, géométrie et capture complète PASS.
Archive de test intacte. Objectif et Gate 2 non clôturés.

Rejeu final F45BDC5 sans rebuild : même objet et neuf valeurs visibles dans le
principal compact puis WQHD, et secondaire Full HD. Fermeture par croix native,
secondaire absent de l'inventaire, bouton Deux écrans rétabli sans erreur et
valeurs conservées. Quatre captures complètes PASS ; archive inchangée. Le
principal est laissé ouvert avec la zone isolée et l'objet sélectionné.
Prochaines preuves indépendantes : navigation clavier des ancrages et résolution
de conflit de brouillons ; limites matérielles inchangées (4K complet, DPI, hotplug).

Contrôle clavier : Tab puis Entrée active bien l'ancrage gauche, mais le focus
retombe sur BODY après fermeture du menu. Correctif local de retour sur Ancrer,
également après réinitialisation. Clippy WASM strict PASS (1 min 11 s), build
release en cours ; résultat graphique du correctif non encore qualifié.
Session candidate fermée normalement, archive inchangée, cache unique conservé.

Correctif de focus rejoué dans F343120 : après Tab/Entrée sur À gauche,
focus visible sur Ancrer ; Entrée rouvre le menu sans souris. Réinitialisation
à droite aussi observée. Deux captures complètes 1280 × 720 PASS. WASM/natif
compilés, candidat unique remplacé et laissé ouvert sur l'accueil normal.
