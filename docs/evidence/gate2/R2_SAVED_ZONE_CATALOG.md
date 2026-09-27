# Catalogue des zones sauvegardées — 12 septembre 2026

Implémenté : ListZones découvre et vérifie les archives sous la racine autorisée,
sans verrouiller, sauvegarder ni remplacer la session active. Retour : ID zone,
ID projet, nom et hash sauvegardés, IDs des archives indisponibles. Tri nom/ID.
Les chemins résolus sont contrôlés ; aucun chemin filesystem exposé au frontend.
Lecture bornée : 256 entrées, 4 Mio par archive, 16 Mio cumulés. L'inventaire est
consultatif ; Open revalide et conserve ses refus (session active, playtest).
Ce contrôle de chemins n'est pas une sandbox contre un processus local hostile
remplaçant simultanément les fichiers ou liens.

Studio : Actualiser la liste, sélecteur noms/IDs et Ouvrir la zone choisie,
sans ressaisir les clés. La liste décrit la sauvegarde, pas les retouches
volatiles. Aucun assistant projet/module ou système d'onglets déduit de ce lot.

Vérifié : `cargo test -p forge_project --test authoring_catalog --locked --offline`
PASS, 1 test intégré (0,01 s hors compilation). Cas valide/corrompu, nom sauvegardé
distinct du nom non sauvegardé, hash, conservation du document/dirty et des octets
du fichier, refus Open sur session active, ListZones pendant playtest sans sortie
du mode. Clippy project/all-targets PASS (0,36 s) et Studio WASM strict PASS
(25,64 s), moteur inclus. Formatage et diff contrôlés.

Non vérifié : parcours graphique du sélecteur, quotas et attaques concurrentes.
La qualification des contrats partagés ADR-0006 reste à faire avant verdict.
Terrain/archive persistants inchangés ; champ de réponse applicatif catalog
optionnel, nouveau message de lecture. Pas de publication ni de gate acquise.
ADR-0015 reste proposé ; ce travail indépendant ne vaut pas son approbation.

## Causes détaillées et sauvegarde interrompue

Les entrées indisponibles portent maintenant un motif typé : absence, première
sauvegarde interrompue, archive invalide, identité différente, chemin/fichier
refusé, taille excessive ou lecture impossible. Aucun détail de chemin natif
exposé. La liste Studio affiche ces causes par ID dans un panneau dépliable.
Une sauvegarde valide accompagnée d'authoring.pending porte un avertissement ;
la présence du staging ne prouve pas sa validité et n'entraîne ni promotion ni
suppression. La récupération reste distincte et explicite.

Deux tests ciblés PASS (0,01 s hors compilation), Clippy project/all-targets
PASS (2,36 s). Cas ajoutés : première sauvegarde interrompue, absence, mauvaise
identité, archive supérieure à 4 Mio, dossier à la place d'un fichier, sauvegarde
valide avec staging. Octets du staging et de la sauvegarde inchangés après liste.
Contrats partagés et UI restent à qualifier au niveau de la gate ; pas de test
graphique répété pour ce complément de causes. Aucun fichier utilisateur modifié.
Clippy Studio WASM strict également PASS (26,37 s), moteur inclus.

Replay graphique groupé effectué ensuite : archive initialement interrompue
repérée et sélectionnée, puis récupération et actualisation vers une zone valide,
ouverte depuis le catalogue avec le hash attendu et terrain affiché. Preuve,
binaire exact, copie isolée et fermeture des processus : `R2_STUDIO_RECOVERY.md`.
