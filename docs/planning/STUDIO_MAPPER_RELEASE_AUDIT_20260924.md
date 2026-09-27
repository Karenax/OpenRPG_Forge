# Audit profond — Studio pour créateurs de cartes

Date : 24 septembre 2026. Verdict : **NO-GO pour une release destinée à des mappeurs autonomes**.
Ce verdict concerne le produit livré et son ergonomie, pas l'invalidation de toutes les fonctions du moteur.
Livrable d'analyse : aucune correction du produit, compilation, publication ou nouvelle gate pendant cet audit. Aucun sous-agent.

## 1. Périmètre, méthode et limites

Inspection du worktree `E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche Rust, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre déjà fortement modifié. HEAD seul ne décrit donc pas les sources inspectées.
Candidat Gold inspecté : SHA-256 `27ACAC157E113716679887F7ADC334D4FBDFE024ABEF807B7B03E9E620C2E363`, identité vérifiée pendant cet audit. Les sources peuvent évoluer indépendamment de ce binaire.

- Lecture ciblée des composants Rust, styles, commandes, garde-fous, roadmap et preuves locales.
- Observation par Computer Use du candidat en Full HD, ouverture de la copie isolée R2-004 Final B, navigation et dépliage de panneaux uniquement. Aucune nouvelle retouche, sauvegarde ou génération de carte.
- Mesures DOM et captures par le script de diagnostic existant, sans injection d'action produit.
- Relecture des preuves antérieures placement/tracé/Undo/sauvegarde/réouverture : elles ne sont pas des tests exécutés de nouveau aujourd'hui.
- Références ergonomiques primaires consultées en fin de document ; aucune proposition de retour à Godot ou de changement de pile.

Niveaux de preuve : **V** observé dans le candidat ; **S** établi par les sources ; **H** preuve historique datée ; **R** risque à reproduire, non présenté comme incident établi.
L'audit est transversal mais ne vaut ni pentest, ni test exhaustif de tous les états, ni étude utilisateur. Pas de mesure de FPS, de contraste global, de DPI multiples ou d'installation propre effectuée ici.
Le chantier Tidewater ajouté au contexte canonique est distinct : cet audit ne le modifie pas et n'attribue pas ses futures capacités au candidat Gold.

## 2. Diagnostic principal

La première passe a déplacé des formulaires dans une coque d'éditeur. Elle n'a pas encore transformé ces formulaires en interactions de mapping.
Le mappeur doit comprendre où il travaille, quel outil est actif, ce qu'il va modifier, comment terminer et comment revenir en arrière. Il ne devrait pas apprendre la projection Bevy, les reçus de commande ou la migration de zone pour poser un arbre.

Trois causes se renforcent :

1. **Architecture d'information technique** : l'organisation suit les sous-systèmes et leurs contrôles plutôt que les intentions « placer », « dessiner », « retoucher ».
2. **Dette visuelle structurelle** : couches de CSS qui se surchargent, formulaires emboîtés, boutons de même poids, couleurs et tailles héritées.
3. **Qualification orientée capacité** : pouvoir déclencher une commande et obtenir un résultat exact n'établit ni la découvrabilité, ni le confort, ni la productivité.

À préserver : commandes gouvernées, séparation aperçu/application/disque, Undo/Redo, intégrité, contrôles de révision, sélection et gestes déjà fonctionnels. La simplification doit masquer la plomberie, pas supprimer les garanties.

## 3. Preuves visuelles et mesures

Captures et JSON locaux : `output/studio-gold-20260924/audit/`.

| État | Mesure constatée à 1920×1080 | Conséquence |
| --- | --- | --- |
| Rivière développée | contenu 1281 px pour panneau 918 px | action finale sous le pli, sans même ouvrir les coordonnées détaillées |
| Bâtiments développés, sans bâtiment sélectionné | contenu 1272 px pour panneau 918 px | création de pièce et placement d'assets empilés |
| Nature / Peindre la densité, sans végétation | sélecteur visible, aucune action ni explication dessous | utilisateur abandonné dans un état vide |
| Bibliothèque repliée | toujours 280 px réservés ; canvas 1358×566 | fermer le tiroir ne rend pas l'espace à la carte |
| Canvas sans tiroir | 1358×846 | le tiroir retire environ un tiers de sa hauteur disponible |

Fichiers : `water.{png,json}`, `buildings.{png,json}`, `nature-empty.{png,json}`, `library-collapsed.{png,json}`.
Le contrôle actuel renvoie `layoutPassed=true` pour Rivière et Bâtiments alors que `panelFits=false` : son verdict ne couvre pas l'utilisabilité du panneau. `pointEditors` compte aussi les éléments montés mais cachés.

## 4. Registre des améliorations

Priorités : **P0** protection du travail / qualification indispensable ; **P1** nécessaire à la première release mapper ; **P2** extension après une boucle simple convaincante. Les tailles S/M/L indiquent une complexité relative, pas une promesse de délai.

### A. Confiance et continuité du travail

| ID | Priorité / preuve | Constat et impact | Correction proposée / acceptation |
| --- | --- | --- | --- |
| M01 | P0 · S/R · M | Le backend refuse `Close` avec des retouches non sauvegardées, mais `main.rs` ne raccorde pas la fermeture native de fenêtre à ce garde. La perte à la croix n'est pas reproduite ici. | Qualifier puis couvrir croix, Alt-F4, reload, changement de projet : Enregistrer / Ne pas conserver / Annuler ; aucune fermeture silencieuse d'un travail non sauvegardé. |
| M02 | P0 · S · L | La sélection dans la hiérarchie appelle `cancel_preview`; le changement d'onglet annule la brosse mais pas la même totalité d'états. Les transitions n'ont pas un contrat uniforme. | Modèle explicite outil/geste/brouillon/aperçu/appliqué/sauvé ; sortie garde le brouillon ou demande de le terminer/abandonner. Tester sélection, onglet, hors-canvas, Échap et focus. |
| M03 | P0 · S/R · M | Chaque modification de champ invalide un aperçu ; les inspecteurs sont parfois recréés depuis l'état accepté. Risque de brouillon perdu ou de champs revenus aux valeurs initiales. | Brouillon transitoire stable par zone/objet, indépendant des panneaux ; bannière claire si proposition périmée. Ne pas créer une seconde source canonique. |
| M04 | P0 · V/S · M | « Annuler » historique, « Abandonner l'aperçu », Échap et sauvegarde ont des sens différents difficiles à déduire. | Historique nommé « Annuler : déplacer Chêne », bouton de geste « Annuler le placement », état « Aperçu non appliqué » distinct de « Modifications non enregistrées ». Sauver avec aperçu doit expliquer ce qui est sauvegardé. |
| M05 | P0 · S/R · L | Sauvegarde atomique et récupération d'écriture existent ; cela ne prouve pas la récupération des retouches appliquées mais jamais sauvegardées après crash. | Définir une récupération bornée des opérations acceptées ou une sauvegarde automatique explicite ; tester crash, disque plein et fichier invalide sur copie. Limiter nombre et volume des secours, ne pas restaurer silencieusement un ancien état. |

### B. Premier contact et structure du Studio

| ID | Priorité / preuve | Constat et impact | Correction proposée / acceptation |
| --- | --- | --- | --- |
| M06 | P1 · V/S · M | Démarrage sur vaste canvas noir, commandes désactivées et formulaires Projet ; Actualiser puis sélectionner puis Ouvrir. | Accueil Créer / Ouvrir / Récents avec carte exemple. Une action évidente, état de chargement explicite et choix de dossier produit, sans variable d'environnement à connaître. |
| M07 | P1 · V/S · M | Créer/modifier zone, projets/modules, migration et diagnostics partagent l'inspecteur. Des UUID sont visibles dans les listes. | Gestion projet dans une vue dédiée ; nom humain au premier plan, identités dans Détails. Séparer création, ouverture et propriétés de carte. |
| M08 | P1 · V/S · L | Bibliothèque et panneau fixe mangent la scène ; repli inefficace. | Panneaux redimensionnables et masquables, limites minimales, réinitialisation du layout. Bibliothèque repliée : seule une barre courte reste ; espace repris immédiatement, sans perdre caméra ou sélection. |
| M09 | P1 · V/S · L | « Bâtiments » et « placer un objet » restent deux formulaires simultanés dans Objets. | Sous-outils exclusifs Décors / Construire, avec même sélection contextuelle. La sélection d'un bâtiment affiche sa fiche seule. |
| M10 | P1 · V/S · M | Nature vide si aucune recette végétale ; les prérequis disparaissent au lieu d'être expliqués. | État vide « Ajoutez une couche de végétation » avec action guidée et choix d'espèces ; aucun panneau actif sans prochaine action. |

### C. Gestes de mapping

| ID | Priorité / preuve | Constat et impact | Correction proposée / acceptation |
| --- | --- | --- | --- |
| M11 | P1 · V/S/H · L | Catalogue : mêmes cubes génériques, identifiants `vv-…`, chargement manuel. Choisir un décor exige de connaître son nom. | Miniatures des modèles réels, nom lisible, dimensions, catégories, récents/favoris. Chargement automatique local borné, état erreur/remplacement de vignette honnête ; ne pas faire passer des images inventées pour le modèle. |
| M12 | P1 · S/H · L | Placement aperçu après action mais pas de fantôme continu sous curseur ; sélection d'objet à armer séparément. | Outil Sélection par défaut ; fantôme au sol avant clic, état valide/invalide, orientation et snap visibles, mode placements répétés explicite. Échap termine sans supprimer les placements déjà acceptés. |
| M13 | P1 · S/H · M | Dupliquer, supprimer, grille et surface sont enfouis dans les réglages avancés. | Actions fréquentes en barre contextuelle ; W/E/R ou schéma choisi, F cadrer, duplication et suppression avec contrat sûr ; XYZ précis reste immédiatement accessible, les options rares seulement sont avancées. |
| M14 | P1 · S · L | Navigation limitée à orbit/pan et boutons de zoom dans ce contrôleur ; le clic droit annule la brosse. Pas de vue de dessus/orthographique identifiée ici. | Zoom molette centré, focus sélection, vue du dessus et axes, navigation temporaire qui suspend puis reprend l'outil. Tester clavier AZERTY et souris sans clic central ; commandes alternatives visibles. |
| M15 | P1 · S/H · L | Chemins : points visibles mais pas de poignées glissables ni segment suivant sous curseur. Des boutons Terminer/Examiner coexistent. | Un seul flux Dessiner → Ajuster → Valider ; segment provisoire, poignées, insertion/suppression contextualisées, largeur visualisée ; garder un seul point détaillé, 2 comme 128 points. |
| M16 | P1 · S/R · M | Dessin par points utilise un état `drawing` distinct de la brosse ; sortie de canvas/caméra annule la brosse sans forcément terminer ce mode. Recliquer Par points démarre un remplacement au premier clic. | Reproduire la sortie/entrée et navigation au milieu d'un tracé ; état suspendu/reprise explicite, jamais de redémarrage destructeur implicite ; restauration par Échap cohérente quel que soit le focus. |
| M17 | P1 · V/S · L | Eau expose coupe, accotement et textes avant le geste ; pas le même parcours que Chemins. | Même interaction de spline, préréglages Ruisseau/Rivière, largeur/profondeur/niveau avec coupe dessinée, réglages de creusement en avancé. Bouton terminer visible sans défilement. |
| M18 | P1 · V/S · M | Terrain : quatre façons de démarrer/finir et coordonnées très visibles avant le travail direct. | Choisir brosse puis dessiner ; rayon/intensité sous curseur et contrôles compacts, matériaux avec vignettes ; coordonnées dans précision. Une explication courte, pas un protocole permanent. |
| M19 | P1 · S · L | Nature répartit peinture, génération, espèces, couches et exclusions ; une clairière demande un ID stable et des coordonnées. | Flux choisir espèces → peindre/effacer → ajuster densité ; clairière dessinée, ID généré ; distinguer clairement modification locale et régénération. Aperçu d'emprise et verrous avant opération large. |
| M20 | P1 · V/S · L | Bâtiment créé via origine XYZ, puis long inspecteur de dimensions ; pas une interaction de construction accessible. | Préréglage de pièce, emprise au curseur, placement au sol, aperçu porte/façade ; largeur/profondeur/hauteur en tête ; parois/pivot/overrides en sections ciblées. Ne pas promettre un éditeur architectural complet. |
| M21 | P1/P2 · S · L | Hiérarchie plate et paginée, sélection par type dans plusieurs signaux ; organisation de grosse scène non qualifiée. | P1 : recherche, filtre de type, icônes, visibilité/verrou intelligibles, sélection unique cohérente. P2 : multi-sélection et groupes transactionnels ; ne pas les simuler par une série de mutations partielles. |

### D. Qualité visuelle, accessibilité et réponse de l'outil

| ID | Priorité / preuve | Constat et impact | Correction proposée / acceptation |
| --- | --- | --- | --- |
| M22 | P1 · V/S · M | Trop de cadres, presque chaque action est un rectangle pleine largeur ; trois niveaux de styles concurrents. | Design system unique : surfaces calmes, séparateurs espacés, une action principale par tâche, boutons secondaires discrets, icônes cohérentes. Remplacer les règles contradictoires, pas ajouter une quatrième surcouche. |
| M23 | P1 · V/S · M | Texte principal correct mais nombreuses explications 11–13 px et petites listes ; mélange champagne/bleu/vert hérité. | Typographie lisible, échelle d'espacement 4/8/12/16/24, palette sémantique limitée, 14–16 px pour les indications utiles ; or réservé à sélection/action, erreur et avertissement distincts. Mesurer les contrastes réels, pas les déduire du thème sombre. |
| M24 | P1 · S/R · M | Focus existe, mais texte saisi exclut tous les raccourcis globaux, y compris Ctrl+S ; accessibilité et DPI non qualifiés. | Conserver Undo textuel dans un champ, définir Save global sans ambiguïté ; labels, ordre Tab, focus restauré, contrôles accessibles au clavier. Tester 100/125/150/200 % et réduction des animations. |
| M25 | P1 · V/S/H · M | État de projection, source, commandes et GPU dispersé ; attente de démarrage historique de plusieurs secondes, pas de nouvelle mesure ici. | Indiquer Chargement / Calcul / Prêt / Erreur avec action de reprise ; montrer la dernière carte stable pendant les calculs si sûr. Réserver les traces longues au diagnostic, sans masquer les erreurs bloquantes. |
| M26 | P1 · S/H · M | Jouer est disponible, mais choix du départ et bilan noyés avec diagnostics/historique. | Jouer ici depuis la scène, départ identifiable, retour à la même caméra/sélection, mode édition/jeu évident et arrêt toujours accessible. Tester absence d'altération de source et collisions à jour. |
| M27 | P1 · V/S · M | La simplicité de la scène témoin ne permet pas de juger une release créative ; ciel/sol occupent presque tout, sans repère d'échelle. | Carte exemple composée et petite : relief, rivière, sentier, maison, végétation, repère humain ; modes de lecture pente/collision/grille. Séparer défaut de contenu artistique et défaut de coque ; ne pas cacher un mauvais geste sous un beau paysage. |

### E. Qualification et livraison

| ID | Priorité / preuve | Constat et impact | Correction proposée / acceptation |
| --- | --- | --- | --- |
| M28 | P0 · V/S · M | Diagnostic layout vert malgré débordement ; ancien parcours attend 6 onglets, « Tracés & eau » et « Bibliothèque d’assets ». | Recette basée sur actions métier et états visibles, comptage des seuls éléments visibles, visibilité des actions finales ; actualiser les sélecteurs sans abaisser les assertions. |
| M29 | P0 · H/S · L | 82 tests Studio ne couvrent pas toute l'interaction WASM ; temps de robot/IPC ne valent pas effort humain ni input-to-photon. R2-050/R2-052 et conditions transversales sont ouverts. | Rejouer les preuves affectées sur le candidat apparié, puis recette utilisateur et performance ; publier le matériel, résolution, cache froid/chaud et limites. CI distante ne devient pas verte par cet audit. |
| M30 | P1 · S/R · L | Candidat de revue, lancement via script/env, bundle inactif dans la config lue ; version frontend/native/kit doit rester appariée. Cela ne prouve pas l'absence de tout packaging ailleurs. | Avant distribution à des mappeurs : ouverture sans outil dev, choix de workspace, kit local cohérent/licencié, fonctionnement hors réseau, logs accessibles et version identifiable. Cadrer séparément packaging/signature/mise à jour selon les gates de distribution, sans avancer implicitement Gate 4/7. |

## 5. Direction artistique et architecture d'écran proposées

**Un atelier de cartographie sobre, pas un tableau de réglages ni une interface de jeu fantasy.**
Conserver charbon chaud, ivoire et champagne de la proposition approuvée ; la beauté doit venir de la proportion, de la cohérence des composants, des vraies miniatures et de la scène, pas d'un excès de dorures.

Disposition cible :

```text
Projet / Carte     Enregistrer · Annuler · Rétablir        Tester
Sélection · Terrain · Nature · Chemins · Eau · Construire
Scène / calques |       Carte + outils contextuels       | Propriétés
               |        (priorité à la surface)          | de la sélection
               | Bibliothèque ouvrable/redimensionnable |
État court : Prêt / Aperçu / Non enregistré / Calcul / Erreur
```

- Projet, aide, historique et diagnostic deviennent des espaces auxiliaires ; pas des outils de sculpture.
- Barre de transformation compacte près du viewport : déplacer/tourner/échelle, repère et snap. Les mêmes commandes doivent garder la même place.
- Inspector : titre humain + sélection, 3–6 propriétés fréquentes, avancé explicite ; duplication/suppression ne sont pas des paramètres experts.
- Aucune obligation absolue « tout tient sans scroll » pour un inspecteur expert : cette exigence porte sur le parcours principal et son action finale. Une longue liste de résultats peut défiler.
- Animations courtes 120–180 ms proposées, limitées à apparition/focus/feedback ; pas de déplacement des cibles pendant un geste, pas d'animation décorative permanente, reduced-motion respecté.
- États essentiels : vide, chargement, prêt, sélection, geste actif, aperçu, conflit, erreur, non enregistré, lecture seule. Chaque composant doit être dessiné dans ces états avant intégration.

## 6. Contrat d'interaction à adopter

Un seul modèle partagé par tous les outils :

**Choisir → prévisualiser le geste → ajuster → valider → enregistrer.**

La préparation/validation native reste exécutée. Le bouton visible porte l'intention : « Placer », « Créer le chemin », « Appliquer le relief ». Les détails du diff restent disponibles, et obligatoirement mis en évidence pour les remplacements larges/conflits. Une réponse tardive ne doit jamais réactiver un ancien aperçu.

La caméra doit suspendre un outil plutôt que l'effacer. Échap annule le geste courant ; si aucun geste n'est en cours, il ferme le mode actif ; il n'annule pas silencieusement une retouche déjà appliquée. L'historique conserve ce rôle séparé.
Le focus clavier est explicite : texte, viewport et navigation ne consomment pas les mêmes touches. La configuration choisie doit être testée en AZERTY, pas seulement affichée dans une aide.

## 7. Plan de réalisation ordonné

| Lot | Contenu | Preuve de sortie |
| --- | --- | --- |
| L0 — Filet de sécurité | M01–05, contrat d'état, fermeture, brouillons, identité du candidat | tests transitions + annulation + relance, aucune perte silencieuse ; scénarios négatifs reproductibles |
| L1 — Vrai système visuel | M06–10, M22–24 ; composants Rust réutilisables, tokens CSS uniques, panneaux | captures d'une scène réelle, vide/erreur/sélection ; variantes Full HD/QHD/4K et DPI ; comparaison à la maquette approuvée |
| L2 — Décorer une carte | M11–14, M21 essentiel ; miniatures, sélection, placement, snap, duplication, caméra | poser et ajuster 20 décors sans chercher un UUID ni ouvrir l'avancé |
| L3 — Façonner la carte | M15–20 ; chemins/eau homogènes, terrain et végétation guidés, bâtiment simple | réaliser relief + forêt + chemin + rivière + maison, puis corriger chaque élément |
| L4 — Consolider la release | M25–30 ; performance, accès test, recette Gold complète, livraison bornée | critères de sortie ci-dessous ; paquet unique identifié, pas de validation par capture seule |

Les tailles L concernent surtout interactions et états, pas une simple retouche CSS. Ne pas annoncer un calendrier avant L0/L1 et un premier parcours chronométré.
Pas de nouveau moteur, ni de JavaScript métier, ni d'import massif d'assets pour masquer les problèmes. Miniatures : dérivés identifiés par hash/version, réutilisés et purgés selon un quota ; aucun historique de compilations supplémentaire.

## 8. Définition proposée de « release mapper »

Première cible : **preview locale de mapping Windows**, sans promesse de RPG complet, multijoueur, marketplace ou outil narratif. Une release publique distribuable exige en plus les validations de distribution applicables ; fermer Gate 2 ne les remplace pas.

### Recette fonctionnelle obligatoire

Un utilisateur ouvre le produit sans terminal, crée une carte, sculpte un relief, peint un sol et une forêt, crée un sentier et une rivière, place une maison et des décors, corrige position et largeur, teste la traversée, revient éditer, annule/rétablit, sauvegarde, ferme et retrouve le même résultat.

Pour chaque famille de mutation : aperçu valide/invalide, abandon, application, Undo/Redo, sauvegarde/réouverture ; aucun delta inattendu sur les éléments non concernés. Conflits et résultats périmés doivent être visibles et refusés. Tester aussi annulation pendant une opération et disque non inscriptible sur fixture.

### Recette ergonomique — cibles proposées, non résultats acquis

- 5 participants minimum, dont novices du Studio et personnes habituées au mapping ; consigne d'objectif, pas une liste de boutons à cliquer.
- Tous réussissent à sauvegarder/rouvrir et revenir en arrière sans perte ; au moins 4/5 terminent le scénario principal sans intervention du développeur.
- Relever temps actif, temps d'attente, erreurs, recherches de contrôle, demandes d'aide, satisfaction. Une intervention est consignée, pas masquée par le temps total.
- Après une courte prise en main, cible de premier décor posé en moins de 2 minutes et de carte simple en 30 minutes de travail actif ; calibrer avec le pilote, pas transformer ces objectifs en faux seuils déjà validés.
- Zéro impasse sans explication ; aucun défilement requis pour terminer le geste principal ; distinction aperçu/appliqué/sauvé comprise par les participants.
- Revue de beauté sur captures équivalentes à la maquette : hiérarchie, densité, typographie, miniatures, cohérence et scène. Ne pas remplacer l'avis utilisateur par une note automatique.

### Performance et accessibilité

- Full HD, QHD et 4K, avec mise à l'échelle Windows : relever pixels physiques ET surface CSS. Le minimum d'écran ne garantit pas un viewport CSS de même taille.
- Cibles initiales : retour visuel de sélection/survol inférieur à 100 ms p95 ; opérations longues avec état visible ; benchmark viewport selon les budgets canoniques, latence geste→image mesurée séparément de l'IPC.
- Scènes de petite et moyenne densité, puis charge limite documentée : objets uniques/répétés, chemins longs, eau et végétation. Tester une session prolongée, mémoire/VRAM, retour jeu/édition et annulations répétées.
- Vérification contraste du texte normal 4,5:1 et contrôles pertinents 3:1, focus visible, indication autre que la couleur ; tailles de cible et espacements selon WCAG 2.2. Ce sont des critères à vérifier, pas une déclaration de conformité actuelle.

### Discipline de preuve et de stockage

- Un candidat actif et un cache partagé pendant le développement, pas un répertoire de build par essai.
- Captures compactes, rapport lié au hash exécutable/WASM/kit, logs bornés ; taille disque suivie avant/après qualification.
- Adapter les tests Gold sans assouplir les assertions métier ; tests natifs, WASM, intégration réelle et revue humaine distingués.
- Ne pas qualifier « fluide » par le temps de commande seul ; ne pas qualifier « beau » par l'absence de débordement.

## 9. Points d'entrée du code et preuves

- `apps/forge_studio/src/authoring_gold.css` : trois colonnes fixes, tiroir et réserve de 280 px, surcharges visuelles.
- `apps/forge_studio/src/authoring.css` et `apps/forge_studio/styles.css` : héritages de styles, tailles de texte et contrôles.
- `apps/forge_studio/src/authoring_controls.rs` : barre de commandes, changement d'onglet, état de session.
- `apps/forge_studio/src/authoring_zone/hierarchy.rs` : sélection, pagination et annulation d'aperçu.
- `apps/forge_studio/src/authoring_assets.rs` : catalogue, cartes génériques, chargement explicite.
- `apps/forge_studio/src/authoring_assets/placement_controls/inspector.rs` : séparation simple/avancé, duplication et suppression.
- `apps/forge_studio/src/authoring_camera.rs`, `authoring_viewport.rs`, `authoring_shortcuts.rs` : navigation, annulation et focus.
- `apps/forge_studio/src/authoring_paths.rs`, `authoring_path_guides.rs`, `authoring_rivers.rs` : dessin, repères et formulaires.
- `apps/forge_studio/src/authoring_density.rs:166` : rendu conditionné à une végétation existante, sans état vide.
- `apps/forge_studio/src/authoring_exclusions.rs:146` : identifiant stable demandé à l'auteur.
- `apps/forge_studio/src/authoring_buildings.rs:116` : création numérique et empilement des contrôles.
- `apps/forge_studio/src/main.rs:169` et `crates/forge_project/src/authoring/host.rs:274` : fermeture native et garde de fermeture métier distincts.
- `scripts/Inspect-StudioGoldLayout.mjs` : verdict layout incomplet ; `scripts/Invoke-Gate2WorldEditingProof.ps1:996` : six onglets encore attendus.
- `docs/evidence/gate2/STUDIO_GOLD_IMPLEMENTATION_20260924.md` : preuve de la première passe, pas release complète.
- `docs/evidence/gate2/GATE2_CLOSURE_WORK_20260923.md:39` : chronométrages automate distincts de l'effort humain.
- `docs/planning/MIGRATION_ROADMAP.md:3899` et `:3932` : R2-050/R2-052 ouverts ; aucune clôture changée ici.

## 10. Références de conception

- [Godot — espace 3D](https://docs.godotengine.org/en/stable/tutorials/3d/introduction_to_3d.html) : outils de transformation, sélection, snap et navigation rendus accessibles dans le viewport. Référence de gestes, pas de technologie à réintroduire.
- [Blender — Asset Browser](https://docs.blender.org/manual/en/5.0/editors/asset_browser.html) : bibliothèque organisée et aperçus. Page repérée par recherche ; récupération intégrale refusée par l'outil, pas de prétention d'audit détaillé de sa version.
- [W3C — contraste minimum](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum) et [taille des cibles](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) : critères mesurables à intégrer à la recette.

## Conclusion

La priorité n'est pas d'ajouter encore des menus. C'est de livrer une boucle de mapping complète, visuelle, prévisible et sûre. La prochaine implémentation devrait commencer par le contrat des gestes et un seul parcours Objet réellement soigné, puis décliner les mêmes composants sur Terrain, Nature, Chemins, Eau et Construire. La maquette approuvée reste une référence de rendu, pas une preuve de disponibilité.
