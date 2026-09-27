# R1-040 — caméra active et qualité de l'eau

Date : 2026-09-06. Branche Rust, arbre de développement modifié, aucune publication.

## Implémenté

Le changement de budget SSR et son observation sélectionnent maintenant l'unique caméra 3D
active. Une caméra secondaire inactive ne bloque plus le changement Low/Ultra et conserve son
composant SSR. Zéro ou plusieurs caméras actives restent un cas non appliqué : aucun choix
arbitraire. Aucun document, mesh, shader ou contrat optique n'est modifié.

## Réellement vérifié

- `cargo test -p forge_engine --locked water::quality::tests` : 4 tests PASS, 0 échec.
- Nouveau scénario : caméra inactive avec budget de 17 étapes conservé, caméra active passant
  à Low sans SSR puis Ultra avec 64 étapes ; observation partielle correcte.
- Régressions existantes : caméra active absente/ambiguë, politique étrangère, désactivation
  non supportée et conservation des géométries/phases pendant un cycle.
- `cargo clippy -p forge_engine --all-targets --all-features --locked -- -D warnings` : PASS.
- `cargo fmt --all` exécuté ; `git diff --check` PASS avant ajout de ce rapport.

## Limites

Tests ECS locaux, pas une nouvelle capture GPU ni une qualification Studio/Tauri. La réfraction,
la portée SSR et la qualification complète des reflets restent ouvertes. Les autres adaptateurs
graphiques ne sont pas rendus multi-caméras par ce correctif. Gate 1 reste ouverte ; CI distante
NOT RUN selon ADR-0006 et validations humaines inchangées.

## Complément démarrage — 2026-09-06

Le correctif précédent ne couvrait que les changements à chaud : son auxiliaire était créé après
le démarrage. Un nouveau test crée la vue inactive avant `PostStartup`. Il échoue sur l'ancien
code : ses 17 étapes SSR deviennent 48. `configure_water_cameras` configurait toutes les vues,
écrasant également leurs réglages de transmission et MSAA.

Ce système sélectionne maintenant l'unique caméra active, comme les changements à chaud.
Avec zéro ou plusieurs vues actives, il n'en choisit aucune. La configuration de l'unique vue
active reste identique : prépass différé, MSAA off, transmission 0 et SSR/TAA préparés.

Vérifications exécutées :

- 18 tests `cargo test -p forge_engine water --locked -q` PASS, dont deux nouveaux scénarios.
- Auxiliaire inactive présente au démarrage : SSR 17, transmission 2 et MSAA Sample4 conservés,
  ainsi que la présence initiale des composants prépass/TAA ; principale à 48 étapes SSR.
- Deux caméras actives dès le démarrage : leurs réglages sont conservés, aucun budget appliqué
  n'est annoncé. Pas de sélection arbitraire de caméra.
- Clippy workspace strict et check moteur `web` WASM PASS ; formatage et diff-check PASS.

Un échec intermédiaire du test supposait l'absence de DeferredPrepass sur l'auxiliaire. Bevy 0.19.1
l'insère comme composant requis de ScreenSpaceReflections ; le test compare désormais son état
avant/après au lieu d'imposer une absence incorrecte. Les assertions 17 étapes, transmission 2
et MSAA Sample4 restent exactes. Aucun contrôle de rendu désactivé pour obtenir ce résultat.

Les compteurs globaux de caméra restent globaux et peuvent donc annoncer une projection incomplète
avec des auxiliaires différentes. Ils ne deviennent pas une qualification multi-vues. Les caméras
créées après PostStartup ne reçoivent pas une nouvelle configuration initiale par ce système.
Pas de nouveau lancement GPU, capture, build exécutable ou benchmark ; aucun shader modifié.

## Point d'intégration réfraction — non implémenté

Inspection du code source Bevy 0.19.1 installé (`bevy_pbr/src/transmission/mod.rs` et
`pbr_material.rs`) : `ScreenSpaceTransmission.steps` compte les couches avec copies de texture,
pas des pas de parcours de rayon. Les budgets Forge High/Ultra 24/40 ne doivent pas être copiés
dans ce champ. Zéro désactive la réfraction écran ; la transmission spéculaire du matériau est
également zéro dans l'adapter actuel. Une intégration optique doit distinguer ces budgets et
vérifier la passe transmissive, ses limitations de contenu transparent et son interaction avec
les reflets. Aucun statut de réfraction disponible ou de preset complet n'a été activé ici.
