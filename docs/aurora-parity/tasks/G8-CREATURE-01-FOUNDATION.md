# G8 CREATURE-01 — Fondation créature RPG Kit

G8 introduit une frontière d'assemblage optionnelle `Forge.RpgKit.Domain`, dépendante du Core mais
indépendante du moteur. `CreatureDefinition` compose identité narrative localisée, transform canonique,
pools de ressources, profil de déplacement, apparence, perception, combat, dialogue, inventaire et
bindings de scripts.

Les pools génériques couvrent notamment santé, endurance, mana, bouclier et morale par identifiant,
avec maximum, courant, régénération et persistance. Les déplacements couvrent Grounded, Flying,
Swimming, Climbing, Vehicle et ZeroG avec vitesse, accélération, décélération et contraintes.

La factory refuse une créature incomplète par codes stables et n'émet `CreatureSpawned` qu'après la
construction validée ; l'événement porte contexte, transform, seed, cause et autorité.

Preuves : `CreatureFactoryTests` (8 scénarios), build et suite globale.

La prochaine tranche unique est **G8 CREATURE-02 — combat, événements et assistant**.
