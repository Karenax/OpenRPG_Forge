# G7-32 — Événements projet typés

Le bus applicatif accepte désormais cinq familles canoniques : connexion authentifiée du joueur,
inventaire acquis/perdu, utilisation d'objet ou activation de capacité, mort/respawn et changement
d'équipement/progression. Chaque événement porte identité, cause, autorité, révision serveur et audit.

Les mutations sont validées avant journalisation. Les règles peuvent opposer un veto par code stable ;
une transaction refusée ne laisse aucune entrée dans le journal. Le pipeline de mort ordonne
`Downed`, `Dying`, `Dead`, `RespawnRequested` et `Respawned`, avec ou sans état à terre.

Les bindings ForgeScript sont versionnés, validés et portables dans les propriétés typées éditables
par la commande transactionnelle existante.

Preuves : `ProjectGameplayEventBusTests`, build et suite globale.

La prochaine tranche unique est **G7-33 — événement global et calendrier du monde**.
