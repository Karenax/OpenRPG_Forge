# Tidewater — accessoires de la place dans Studio

Le paquet source figé des cinq accessoires (`plaza-package-001`, SHA-256
`c0b3ce259e9de735204e5ea03e2cce5b6bdf0a332c3ffaca9fab59052712a23a`)
alimente maintenant la bibliothèque Studio. L'action de conversion crée deux
bancs, un lampadaire et deux tonneaux comme placements d'auteur distincts. Les
maillages source sont visibles et chacun possède sa collision Rapier. Le
lampadaire conserve sa description de lumière source ; sa photométrie Bevy
reste à raccorder et à comparer visuellement.

Le candidat local `studio-tidewater-plaza-candidate-095` porte le SHA-256
`1b12f0a6dbc3c528ed318d1dd5a919efff37e17b4ef6e2d1b7d5d67d0570949b`.
Son WASM release porte
`c265c9d73e0c3700f78b76f53c8b1f3fb1f1b71098493d3592f8c571d930ae59`.
Le [reçu de parcours](../../../../.tidewater-integration/studio-tidewater-plaza-smoke-095/boardwalk-studio-smoke-ledger.json)
est `passed` : bibliothèque et zone ouvertes, conversion des cinq objets à
leurs positions source, aperçu et application, 32 placements au total,
sauvegarde/réouverture, Playtest actif et marche. La source d'auteur est
préservée, sans erreur navigateur capturée. La [capture centrée sur le
lampadaire](../../../../.tidewater-integration/studio-tidewater-plaza-smoke-095/plaza-authored.png)
montre les bancs et le lampadaire dans la vue d'édition.

Le parcours qualifie le fonctionnement local du candidat, pas la parité
visuelle avec le site public. La lumière du lampadaire n'éclaire pas encore la
scène comme dans la source, et les ombres projetées des arbres font l'objet
d'une correction séparée.
