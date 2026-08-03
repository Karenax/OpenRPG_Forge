# G7-24 — Géométrie et édition de zone

Un planificateur moteur-indépendant couvre désormais les groupes composés, le remplacement sans
perdre transform/tags/overrides, le choix compatible déterministe avec verrouillage manuel et le
redimensionnement selon cinq ancrages. Le redimensionnement produit la liste exacte des pertes et
translate les instances avant de décider lesquelles restent dans les nouvelles limites.

Toutes ces mutations convergent vers `EditAreaGeometryHandler` : contrôle de révision, UUID,
blueprints, validation du projet, dry-run, patch inverse, audit et une seule entrée d’historique.
Les propriétés locales de tuile réutilisent le contrat versionné `EntityPropertyOverrides` déjà
éditable dans l’inspecteur.

L’écran Zones expose largeur, profondeur et ancrage. Les pinceaux continus, sockets de prefab
paramétriques et animations de tuile spécialisées restent des enrichissements explicites.

Preuves : `AreaGeometryPlannerTests`, build sans avertissement et suite .NET complète.

La prochaine tranche unique est **G7-25 — environnements et streaming de zone**.
