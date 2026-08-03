# G7-35 — Locators, requêtes et diagnostics de routes

L'arrivée à un waypoint peut désormais orienter vers une direction ou une cible, jouer une animation,
attendre, interagir et appeler un callback. Le `GenericLocator` unifie les ancres audio, VFX, spawn,
objectif et gameplay avec but typé, tags et position canonique.

Les locators sont filtrables par texte, tag et but, avec ordre stable, plus proche et sélection pseudo-
aléatoire déterministe. L'analyse de route retourne les segments, sens, distance, temps incluant
l'attente, action, lien cassé et état de projection navigation : ce contrat alimente directement une
future surimpression du viewport sans faire dépendre le domaine du moteur.

Preuves : `WaypointRouteServiceTests`, build et suite globale.

La prochaine étape unique est **l'audit de clôture G7**.
