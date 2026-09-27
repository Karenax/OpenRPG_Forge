# Acquisition asynchrone de la population dans Studio

Date : 2026-09-27. Portee : donnees chargees, pas encore projection GPU.

Le module partage `imported_vegetation::acquisition` assemble les blocs par le
transport borne, puis resout toute la population avec les validations du domaine.
Il controle l'annulation avant et apres chaque attente. Aucune population
partielle n'est retournee. Les erreurs de transport, d'identite et de hash
interrompent le chargement.

Studio observe la tete canonique dans `authoring_imported_population`. Un
changement de tete ou une fermeture invalide la generation asynchrone precedente.
Une reponse ancienne ne peut publier sa population ni remplacer le statut de la
nouvelle generation. Les donnees immuables deja chargees sont reutilisables si
le document de base est strictement identique ; les corrections restent portees
par la tete courante, et ne sont pas incorporees au cache de population de base.

La voie vegetale procedurale est contournee pour les sources importees. Le statut
indique le nombre de placements verifies et precise que leur affichage/edition
n'est pas encore disponible. Le getter refuse une autre tete ou une autre base.

## Preuves

`verify_population_acquisition` exerce la meme fonction asynchrone que Studio,
avec le vrai host natif et un futur Pending une fois par fragment. Sur le
workspace canonique : 65 383 instances, 48 fragments. Les instances, identifiants
stables et transformations sont identiques a la resolution directe depuis les
blobs. L'etat auteur avant/apres reste identique.

Annulation avant premiere requete (zero appel), annulation avant reprise du
futur, reponse avec tete perimee, octets corrompus et erreur de transport : tous
refuses. Les quatre derniers cas s'arretent au premier fragment de ce fixture.
Ce probe qualifie le coeur d'acquisition ; il ne simule pas un parcours GUI.

- Clippy monde/probe strict --no-deps : PASS.
- Build et execution du probe reel : PASS (build 1 min 57 s).
- Studio WASM cargo check : PASS (2 min 14 s, attente de verrou incluse).
- Clippy Studio strict : seul diagnostic preexistant atelier_ui.rs:122,
  bind_main 103 lignes pour une limite de 100 ; aucun strict PASS global.
- Executable SHA-256 :
  fdcecbfbb43f28963aaf9a579f72b47e387150dcf7323063638ed0649a161a9e.

Logs locaux sous E:/OpenRPG_Forge/.tidewater-integration :
population-acquisition-clippy-001.log, population-acquisition-run-001.log,
population-acquisition-wasm-001.log, population-acquisition-studio-clippy-001.log.

## Limites

Appels IPC depuis un navigateur reel, changements de carte dans l'interface,
latence et consommation memoire non qualifies. Pas de nouveau frontend distribue.
La projection GPU, les IDs de picking, les corrections de terrain dans la vue,
les gestes d'edition, leur sauvegarde/reouverture et les ombres des plantes
restent a raccorder. Aucun nouveau verdict de parite visuelle ni de performance.
Aucun commit ou push. Objectif complet toujours ouvert.
