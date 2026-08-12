# G8 CREATURE-04 — Règles de créature

La créature compose désormais cinq composants de règles : défenses data-driven, capacités
instanciées, inventaire, appartenances factionnelles et perception multisensorielle.

Le modèle de défense sépare esquive, armure, résistances, sauvegardes, immunités et mitigation.
Une capacité porte niveau, charges, cooldown, conditions, tags IA et script. L'inventaire distingue
conteneurs, slots, poids, volume, propriétaire, équipement et persistance. Les factions acceptent
plusieurs appartenances pondérées, rang, réputation personnelle et héritage. Vision, audition,
odorat et magie ont portée, cône, occlusion, stimuli et mémoire.

Le service valide chaque composant avec chemin stable, calcule la mitigation et ordonne les
perceptions par confiance de manière déterministe.

R078 ferme les trois premiers composants : défenses, capacités et inventaire sont persistés dans le
blueprint canonique, éditables par une commande unique avec aperçu/diff et undo/redo, vérifiés avant
build, lisibles par handle/MCP, adaptés aux calculs RPG Kit et reconstruits en nœuds runtime. Les
entrées d’inventaire référencent de vrais `BlueprintId` d’objet et respectent slots, poids et volume.
R079 ferme aussi faction et perception dans un `CreatureInteractionCompositionProfile` canonique :
UUID stables, faction principale, memberships pondérés, rang, réputation et héritage, puis vision,
audition, odorat et magie avec portée, cône, occlusion, stimuli et mémoire. La fenêtre spécialisée,
le handle ForgeScript, le MCP, l’adaptateur RPG Kit et les nœuds runtime consomment ce même document.

Preuves : `CreatureRulesServiceTests`, `CreatureRulesCompositionProfileTests`,
`CreatureRulesCompositionFoundationServiceTests`, `CreatureInteractionCompositionProfileTests`,
`CreatureInteractionCompositionFoundationServiceTests`, captures R078/R079 et smokes Toolset/runtime.

La prochaine tranche unique est **G8 CREATURE-06 — capacités ciblées, blocage et contrôleurs IA**.
