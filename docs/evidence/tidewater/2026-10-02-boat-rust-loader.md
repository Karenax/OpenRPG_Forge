# Tidewater — lecteur Rust du paquet bateau

Suite de l'[acquisition source](2026-10-02-boat-source-acquisition.md) et du
[masque de coque dans l'eau réelle](2026-10-02-real-water-hull-probe.md).
Le lecteur Rust est implémenté et validé sur le paquet réel 319. Cette tranche
qualifie son transport CPU et ses contrôles de données ; elle ne qualifie pas
encore un bateau rendu ou placé dans Studio.

## Produit

`crates/forge_assets/src/tidewater_boat.rs` fournit `BoatPackage::load` et
`load_async`, sans filesystem, Bevy ni exécution des sources JavaScript.
L'appelant fournit les octets et le hash gouverné du manifeste ;
`package_hash()` désigne le paquet canonique acquis au commit
`1438b1abfcaee3267092b75573014f4d9b4a983c`.

Les limites sont contrôlées avant toute lecture : manifeste 2 Mio, payload
32 Mio, paquet 128 Mio, chemins plats et absence de collisions de casse ou
noms Windows réservés. La lecture vérifie tous les pins et références,
l'inventaire fermé, le digest du graphe source, les matrices et parents,
les attributs et indices, les bornes, les ancres et déclarations de commande.
Les sept matériaux et modules WGSL sont conservés ; leur parsing ne prouve
pas qu'ils sont déjà portés ou compilés dans le renderer Rust.

`source_geometry.rs` fournit les buffers, repères et exclusion d'eau partagés.
Le proxy source garde ses 952 vertices, 5 436 indices, 910 vertices soudés,
2 722 arêtes et **8 bords ouverts**. Ces compteurs sont recalculés, sans
réparation. Son fingerprint reste
`52b35be1ea38f49f9448ce8d8984d4d0bb3f3c3c506e73cbc1232a04d86ab92e`.
Le parent, repère local et compteurs sont transmis au consommateur ; le
renderer conserve sa propre validation d'admission. Aucun census de scène
ni état `NoHullsInScene` n'est déduit du chargement.

## Preuves courantes

Rust/Cargo 1.98.0, rustc `88d9e12ae178fab0fb5cc050a94da85685d449ea`,
LLVM 22.1.8. `Cargo.lock` reste au hash
`a74094d96651c95ad606297d16bfc5869165072d164b863af2ebeae1f8570bc8`.
Les 175 fichiers Rust/TOML de la fermeture locale Assets/Core/Project/Gameplay
et du workspace contrôlés avant/après ne présentent aucune dérive.

Le [reçu](<F:/OpenRPG_Forge/.build/boat-loader-build-331/receipt.json>) conserve
les commandes, logs, hashes d'entrées et artefacts compilés du candidat actuel.

- `cargo check -p forge_assets --lib --locked --offline -j 1` : PASS.
- Même contrôle pour `wasm32-unknown-unknown` : PASS ; pas un essai WebGPU.
- Clippy bibliothèque et nouvel exemple avec `-D warnings` : PASS.
- Tests de bibliothèque : **46 PASS, 0 échec, 3 ignorés préexistants**.
- Format des fichiers modifiés et contrôle de whitespace : PASS.

Le [vérificateur réel](<F:/OpenRPG_Forge/.build/boat-loader-validation-333/source-result.json>)
charge les **154 payloads**, 14 nœuds, 12 pièces et 46 845 triangles, puis
compare les streams mesh et proxy des lectures synchrone/asynchrone.
Il refuse **36 cas invalides**, dont pins, budgets, traversal, collision de
casse, parents/cycles, pivots, données non finies, indices, bounds, proxy
faussement fermé et URI externe dans le GLB d'inspection.

La copie dans un autre dossier passe, le retrait d'un payload et l'absence
du dossier produisent chacun le code 1, puis la restauration passe. Les trois
rapports positifs sont identiques hors chemin. Le vérificateur nécessite un
vrai paquet et ne possède aucun fallback ni test ignoré pour le bateau.

Un premier essai réel avait refusé une borne JSON pourtant issue des vertices
source. Le décodage décimal introduisait un arrondi de précision f64 : la
comparaison admet maintenant `f64::EPSILON × abs(borne)` et aucun écart à zéro,
très en dessous d'un pas f32. Un test couvre l'aller-retour et refuse un écart
géométrique de `1e-7`. Les payloads d'acquisition restent inchangés.

## Limites et suite

Le contrôle de format global reste en échec sur des écarts préexistants dans
`tidewater_ocean_optics.rs` et `tidewater_temporal/render.rs`. Clippy de tous
les exemples trouve aussi une fonction trop longue dans
`prepare_tidewater_placements.rs`. Les logs d'échec sont conservés. Le warning
`% 3` de `tidewater_fish_stand.rs` a été corrigé avec `is_multiple_of`, sans
modifier son comportement. Les contrôles workspace complets d'ADR-0006,
`cargo deny` et `cargo audit` ne sont pas qualifiés par cette tranche.

Le premier check avait invoqué le shim Rust global et déclenché sa mise à jour
stable ; ses seuls processus propres ont été arrêtés. La recette finale fixe
explicitement `RUSTUP_TOOLCHAIN`, `RUSTC` et `RUSTDOC` à 1.98.0.
La chaîne stable globale est maintenant en 1.99.0 ; ses exécutables rustc/Cargo
ont été vérifiés fonctionnels. Elle n'est pas utilisée dans les preuves finales.

Suite concrète : adapter les 12 pièces et le proxy à une instance éditable,
conserver les pivots, porter les sept matériaux et commandes, puis vérifier
déplacement, suppression, sauvegarde/réouverture et census. La
[cartographie de raccordement](<F:/OpenRPG_Forge/.build/boat-instance-adapter-map-334/NOTES.md>)
identifie les seams Engine/Studio ; aucun consommateur produit n'est encore
installé. Les anciens rlibs Engine300/probe304 ne contiennent pas ce lecteur.
Le rendu dans l'eau réelle, Studio natif/WebGPU, gameplay, parité de scène
complète et distribution du launcher actuel restent à qualifier.
L'objectif complet reste actif.
