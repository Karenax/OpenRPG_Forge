# Lecteur du paquet vegetal et acquisition Studio — 27 septembre 2026

Le nouveau PlantPackage lit le paquet sans dependance filesystem, moteur ou
reseau : l'appelant fournit le transport synchrone/asynchrone et l'empreinte
attendue. Le manifeste borne noms, types, nombre de ressources, tailles et
budget total (256 ressources, 128 Mio ; 64 Mio maximum par binaire). Les trois
descriptions sont lues/verifiees en premier. La fermeture exacte des dependances,
les groupes source, les dimensions et les tailles des textures sont controlees
avant les requetes binaires. Chaque payload doit correspondre a sa taille et
son SHA-256. Les controles geometriques du renderer restent distincts.

Studio accepte maintenant plants_sha256 optionnel dans le candidat de
bibliotheque. Le transport existant meme origine et borne est reutilise. Les
resultats ne sont exposes dans le cache qu'apres reussite du chargement et de
la preparation complete de la bibliotheque ; les tickets obsoletes sont refuses.
Une bibliotheque suivante sans plantes retire la precedente reference vegetale.
Le message produit distingue les ressources chargees de leur affichage.

## Preuves

- Clippy forge_assets/probe --no-deps strict PASS.
- Probe natif PASS sur le paquet reel de 83 payloads : lectures sync/async
  identiques, attente asynchrone par ressource, index invalide refuse sans requete,
  fermeture/tailles incorrectes refusees apres les trois descriptions et avant
  tout binaire ; ressource absente/corrompue refusee, arret asynchrone au premier
  echec. Les refus incluent les chemins sortants et les champs manifestes inconnus.
- Le paquet copie dans la nouvelle bibliotheque statique a ete rouvert et soumis
  aux memes controles Rust : PASS.
- Studio WASM compile. Clippy strict conserve uniquement le diagnostic
  preexistant atelier_ui.rs:122 (bind_main, 103 lignes). Le diagnostic introduit
  sur la longueur de load a ete corrige en isolant l'acquisition des plantes.

Bibliotheque locale preparee :
E:/OpenRPG_Forge/.tidewater-integration/building-library-site-plants-001/forge-imports.
Le candidat inclut batiments, environnement, terrain et plantes. Son SHA-256 est
fc226864e5ae5bd06bdf443eee03aae5ab8e20b40936acb72ab09630fe01232b.
Paquet vegetal : 1fec8a063214ad05ceac19f277dbd17e131e04d2714378bae9239efd12224f83.
Executable de verification :
f8cab69266e145ffd2e51d7dd0e0e7f0a47fa55e3d2725b69b4aa4783de4d3b3.
Preuve machine : plant-package-rust-proof-001.json dans le dossier d'integration.

## Limites

Bibliotheque seulement preparee localement, pas de nouveau frontend distribue.
Acquisition navigateur et affichage GUI non observes. Les textures/buffers GPU,
la population de la zone, les identites de picking, les projections dynamiques
et les gestes d'edition restent a raccorder. Cette livraison ne qualifie pas
les arbres comme editables dans Studio. Aucun commit ni push.
