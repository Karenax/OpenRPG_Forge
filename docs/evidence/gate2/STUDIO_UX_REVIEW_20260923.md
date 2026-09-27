# Revue de l’interface Studio — 23 septembre 2026

## Objectif

Rendre l’édition de zone Gate 2 lisible et utilisable avant le parcours humain.
Cette revue ne clôt ni R2-052 ni la Gate 2 et ne vaut pas validation humaine.

## Défauts corrigés dans cette passe

- Navigation susceptible de sortir de l’écran lors d’un `scrollIntoView` dans les
  historiques : les ancêtres utilisent `overflow: clip` (non défilables), le
  panneau central conserve son propre défilement. La boîte anonyme de `<details>`
  et son contenu sont explicitement bornés à 100 % de la hauteur disponible ;
  `flex: 1` seul ne contraignait pas ce contenu dans WebView2. Le test conserve volontairement
  le geste qui révélait le défaut et exige les six onglets visibles après celui-ci.
- Contrôles natifs gris sans hiérarchie : palette ardoise, actions principales
  dorées, transport de test vert, pictogrammes vectoriels avec libellés conservés,
  focus clavier visible et états désactivés distincts.
- Jouer/Retour enfouis : transport persistant partageant les mêmes commandes,
  garde-fous et diagnostics que les anciens boutons. Réglages du départ et bilan
  restent dans Tester & suivi ; aucun second propriétaire de session.
- Brosse trop longue : paires de champs, grille d’actions, dessin prioritaire,
  consigne brève, explications détaillées repliées sans retirer de capacité.
- Peinture encombrée : migration affichée seulement quand nécessaire, paramètres
  appariés et aide repliée. Contrat de migration et aperçu avant application conservés.
- Mode de geste invisible : bandeau dans la vue indiquant Placer/Tracer et rayon,
  piloté par l’armement et l’annulation réels, sans bloquer les événements du canvas.
- Interface inerte : transitions courtes de survol et apparition des panneaux,
  signal d’activité uniquement pendant une requête native ; animations désactivées
  avec `prefers-reduced-motion`.
- Absence de contrôle systématique des panneaux : capture de chaque onglet,
  unicité de la catégorie active, remise en haut, absence de débordement horizontal,
  navigation persistante et préférence de mouvement testées dans le parcours CDP.
- Relecture des captures : bilan du dernier test replié et en pleine largeur,
  titre Tester remis avant les outils techniques, conseils de construction et de
  tracé repliés pour rapprocher les actions utiles. Le rayon de sculpture saisi
  aux coordonnées est désormais validé comme celui du geste dans la vue.

## Suggestions distinguées des défauts de cette interface

La revue indépendante propose aussi minimap, visibilité par couches, simulation
du vent/heure, caméra avancée et nouveau profil de rendu. Ce sont des extensions
de l’éditeur ou du moteur avec leurs propres contrats et validations ; elles ne
sont pas annoncées implémentées par cette passe de présentation. Le cercle de
brosse et les gizmos existants restent ceux du moteur, sans simulation graphique
ajoutée dans le HTML. Une capture seule ne prouve pas leur comportement en mouvement.

## Vérification

`cargo fmt --all -- --check`, Clippy WASM avec `-D warnings` et autotest du harnais
PASS. Avertissement de compatibilité future préexistant de `proc-macro-error2 2.0.1`.
Le candidat intermédiaire `polish-candidate-v2` a passé 9/9 étapes R2-043, le volet
historique R2-037 et provenance rivière R2-049. Ses six panneaux n’ont aucun
débordement horizontal ; navigation visible après historique, mode de brosse,
Échap et préférence de réduction des animations PASS. Les captures ont conduit
aux derniers allègements décrits ci-dessus.

Le candidat final `polish-final-candidate/forge-studio-candidate.exe`, SHA-256
`D01F0005D8B9346E6235C9D9ACF423F24E847A4219B05DDD1AE5D7363B76E080`,
a été lancé puis vérifié avec `FullAuthorRoute` : 9/9 étapes R2-043 PASS,
volets R2-037 et R2-049 PASS, six panneaux sans débordement, navigation persistante,
bandeau de geste/Échap et réduction des animations PASS. Source hash/octets/mtime
inchangés, processus et CDP fermés. Captures des six onglets relues après exécution.
Preuves : `E:\OpenRPG_Forge\.gate2-evidence\gate2-ux-20260923\polish-final-proof`.
WASM : `B0BF5FABC3513E56B2C9A3B203C32E7763AC3A829F19357A8B2857D0E7153CF8`.
Le verdict global demeure `failed_closed` uniquement pour la mesure humaine et
les statistiques de clones non produites par ce parcours unique R2-052. Aucun
verdict de clôture Gate 2, performance interactive ou nouvelle acceptation humaine.
Le lanceur `scripts/Start-Gate2HumanReview.cmd` cible ce SHA final ; son mode
`-PrepareOnly` a vérifié le candidat et conservé la copie humaine existante.

Cache Cargo partagé sur E:, incrémental désactivé, aucune copie d’historique de
compilation. Deux premiers essais ont conservé le défaut de hauteur et sont des
preuves d’échec, pas des résultats qualifiés. La fixture source est demeurée intacte.

## Repères pour l’auteur

La zone courante, Appliquer, Annuler/Rétablir, Sauvegarder et Jouer/Retour restent
en haut à gauche. Les six boutons avec pictogrammes choisissent un métier :
Projet, Terrain, Nature, Tracés & eau, Objets, Tester & suivi. Seul le contenu
situé sous ces boutons défile ; ouvrir une section ne déplie pas toutes les autres.

Pour sculpter : Terrain → choisir l’outil et son rayon → Tracer dans la vue →
maintenir le clic gauche dans le terrain → relâcher → examiner → Appliquer.
Sauvegarder conserve la retouche ; Échap abandonne le geste ou l’aperçu. Les
conseils détaillés sont repliés au pied de chaque outil. Jouer utilise le départ
enregistré ; Tester & suivi permet de régler ce départ et de lire le bilan.
