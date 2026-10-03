# Tidewater — archive des retouches de plantes importées

Le Studio conserve les déplacements des plantes source et les copies posées dans
`ZoneCorrections`. Le format d'archive delta v6, utilisé lorsque l'historique
compact devient trop volumineux, ne transportait pas ces deux champs. Une zone
éditée pouvait donc être sauvegardée en delta puis échouer à la réouverture à
cause d'une tête reconstruite différente de son reçu.

`CorrectionsDelta` transporte maintenant `imported_plant_offsets` et
`imported_plant_placements` à chaque changement, y compris leur retrait vers
`None`. Les champs facultatifs ont des valeurs par défaut à la lecture et sont
omis lorsqu'ils sont absents afin de conserver la compatibilité des anciennes
archives. Le test `explicit_delta_archive_preserves_imported_plant_offsets_and_placements`
construit une source végétale importée, modifie un arbre, pose une copie, écrit
une archive delta, la rouvre et compare tête, audit, sérialisation, Undo et Redo.

Validation locale : `cargo fmt --check -p forge_project`, `git diff --check` et
`cargo test --locked --offline -j 1 -p forge_project --test zone_delta_archive`
réussis (3 tests, 1 rejeu Studio optionnel ignoré). Cette preuve qualifie le
modèle et la persistance ; le clic réel dans le Studio et la réouverture de sa
carte par l'interface restent à observer.
