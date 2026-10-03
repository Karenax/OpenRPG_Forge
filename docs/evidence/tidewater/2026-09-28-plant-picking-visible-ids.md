# Sélection des plantes importées : IDs effectivement dessinés

`Resident::picking_snapshot` conserve désormais les IDs GPU présents dans les
lots visibles. `Snapshot::resolve` refuse un ID absent de ces lots, même si la
table canonique contient l'instance correspondante. Le zéro reste le fond et la
révision de tête est contrôlée avant la résolution. Les compteurs d'identités
totales et visibles permettent de vérifier la portée d'une capture.

Validation locale sur la branche Rust :

- Test unitaire `a_snapshot_resolves_only_drawn_ids_from_the_same_head` : PASS.
- Probe `tidewater_population_scene_probe` avec le workspace
  `workspace-buildings-initial-001` et le paquet végétal épinglé : PASS sur
  65 383 identités. Sélections A/B/A : 18 825 / 18 576 / 18 825 dessins ;
  correspondances canoniques exactes, ID non dessiné, tête périmée et tag
  corrompu refusés. La source du workspace demeure inchangée.
- `rustfmt --check` des deux fichiers modifiés et `git diff --check` : PASS.
- `cargo clippy -p forge_engine --lib --example tidewater_population_scene_probe
  --no-deps -- -D warnings` : PASS.
- `cargo check -p forge_studio --lib --target wasm32-unknown-unknown
  --no-default-features --features web` : PASS.

Ce contrôle protège la résolution d'une réponse GPU. Il ne prouve pas encore
que la passe hors écran masque le terrain et les bâtiments, ni que le clic,
la retouche et l'Undo/Redo fonctionnent dans Studio.
