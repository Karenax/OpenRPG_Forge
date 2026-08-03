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

Preuves : `CreatureRulesServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 CREATURE-05 — dialogues, protections et événements d'interaction**.
