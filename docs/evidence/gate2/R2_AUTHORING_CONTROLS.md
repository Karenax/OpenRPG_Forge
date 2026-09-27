# Gate 2 — premiers contrôles Studio, candidat non qualifié

12 septembre 2026, Rust, sans publication.

## Code ajouté

Menu « Édition de zone · Gate 2 » : clés projet/zone, nom, création d'un terrain
plan 512 × 512 m (corrigé depuis le premier candidat 128 m), ouverture, retouche du nom et de l'altitude en mètres, Undo/Redo,
sauvegarde et fermeture. Le constructeur métier crée un terrain neuf, pas une
copie de Visual Valley ; clés bornées ASCII, identités déterministes par projet/zone.

Les contrôles utilisent les commandes du backend, avec une requête à la fois,
état retourné par le natif et diagnostic d'erreur. Les IDs de mutation sont dérivés
du hash attendu et de l'action pour rendre un retry identique idempotent.
La racine est configurée côté natif via FORGE_AUTHORING_ROOT, pas dans la WebView.

## État des vérifications

- `cargo check -p forge_studio --all-targets --locked --offline` natif : PASS,
  journal `E:/OpenRPG_Forge/.gate2-evidence/authoring-studio-check.log`.
- Core/project : 98 tests PASS (17 + 81), zéro ignoré ; test du constructeur neuf,
  déterminisme, séparation des projets et refus des clés invalides.
  Journal `authoring-controls-core-tests.log` dans le même dossier.
- Clippy core/project strict PASS après correction d'une assertion flottante
  vers comparaison de bits (valeur initiale exactement zéro), aucun lint désactivé.
- Contrôle WASM : PASS, journal `authoring-controls-wasm-check.log`.
- Clippy WASM strict après aperçu/relecture : PASS,
  `authoring-controls-wasm-clippy-fixed.log`. Premier essai refusé pour fonction
  trop longue et condition imbriquée : extraction d'un helper et simplification,
  aucun lint désactivé. Échec conservé dans `authoring-controls-wasm-clippy.log`.
- Trunk debug lancé vers `studio-authoring-web` dans le dossier de preuves E:.
  Premier lancement refusé avant build par NO_COLOR=1 ; valeur booléenne corrigée.
  Suivre `authoring-trunk-build-fixed.log`, ne pas annoncer le bundle construit
  avant sa sortie réussie.

Aperçu textuel ancienne/nouvelle valeur ajouté, application explicite de la
demande prévisualisée exacte avec sa révision, et relecture de session native
après reload. Préparer le diff n'applique pas la retouche.

## Limites

Raccord initial ajouté ensuite, **pas encore compilé/inspecté** : route
`?authoring=1`, coque d'édition et canvas dédiés, chargement du document de la
session native vers le plugin terrain existant après validation du générateur.
Pas de second renderer lancé à côté de la scène de preuve. Un seul lancement
par page, hash de source affiché ; une modification ou fermeture rend l'ancienne
projection explicitement périmée. Rechargement puis relecture native nécessaires
à ce stade : ni mise à jour live ni playtest qualifiés. Le bundle Trunk actif
doit inclure ces dernières sources avant toute revendication visuelle.

Pas encore de lancement/capture : interface ajoutée au code, pas livrée comme
parcours utilisable vérifié. La scène de preuve n'affiche pas le terrain authoré ;
le panneau l'annonce explicitement. Projection, validation visuelle du diff, playtest,
récupération initiale, abandon contrôlé et navigation multi-zones restent à faire.
Les clés ne constituent pas encore un assistant projet/module complet.
Gate 2 reste ouverte et les validations workspace/visuelles restent requises.
