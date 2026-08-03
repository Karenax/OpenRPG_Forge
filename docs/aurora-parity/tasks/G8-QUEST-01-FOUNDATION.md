# G8 QUEST-01 — Objectifs, wizard, cast et récompenses

`QuestObjective` couvre compteur, booléen, collection, destination, escorte, survie, choix et custom.
Le `QuestWizard` prévisualise zones, cast, props, dialogues, objectifs, transitions et cinq scénarios
de test sans code opaque, puis applique uniquement une définition validée à UUID déterministe.

Les participants déclarent rôle, blueprint/instance/query et conditions de présence. Le bundle de
récompense applique atomiquement progression, objets, monnaies, réputation, déverrouillages et choix,
avec rollback exact en cas d'invalidité. Notes de design, canon, propriétaire, statut, risques, TODO,
sources et historique de review restent canoniques.

Preuves : `QuestFoundationServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 QUEST-02**.
