# G8 DOOR-02 — Navigation, verrou, piège et validation

L’obstacle et le lien de navigation se synchronisent avec l’état et la progression d’animation ; la
visualisation éditeur et les tests de blocage exposent la décision. Le verrou partagé avec les
placeables gère credentials, skill checks, reverrouillage et consommation de clé.

`DoorOpenRejected` fournit code, textes localisés, son, animation et script. Les pièges sont attachés
à des côtés et événements avec payload, reset et ownership. La validation croisée vérifie liens,
destination, animations, collision, navigation, credentials, pièges, scripts et boucles.

R081 relie désormais ces capacités au profil canonique `DoorWorldProfile`, à la persistance du
projet, au studio transactionnel, à ForgeScript/MCP et à cinq représentations runtime explicites.
La capture inspectée `evidence/r081-door-access-events-ui.png`, les tests ciblés et les marqueurs
Toolset/runtime R081 prouvent la chaîne auteur → document → adaptation → exécution.

La prochaine tranche séquentielle est **R082 / G8 DOOR-03** pour `OnLock` et `OnUnlock`.
