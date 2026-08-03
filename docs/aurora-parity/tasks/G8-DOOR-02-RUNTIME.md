# G8 DOOR-02 — Navigation, verrou, piège et validation

L’obstacle et le lien de navigation se synchronisent avec l’état et la progression d’animation ; la
visualisation éditeur et les tests de blocage exposent la décision. Le verrou partagé avec les
placeables gère credentials, skill checks, reverrouillage et consommation de clé.

`DoorOpenRejected` fournit code, textes localisés, son, animation et script. Les pièges sont attachés
à des côtés et événements avec payload, reset et ownership. La validation croisée vérifie liens,
destination, animations, collision, navigation, credentials, pièges, scripts et boucles.

Preuves : `DoorRuntimeServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 DOOR-03**.
