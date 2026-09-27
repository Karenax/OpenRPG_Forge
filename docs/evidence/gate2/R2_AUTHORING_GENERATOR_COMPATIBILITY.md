# Gate 2 — compatibilité zone neuve / générateur

12 septembre 2026, branche Rust, sans publication.

Inspection avant raccord viewport : `new_terrain` créait 128 × 128 m alors que
`TerrainGenerationPlan::from_document` exige encore 512 × 512 m, pas de 2 m et
trois couches. Le document passait sa validation générale mais aurait échoué à
la projection. Le constructeur initial utilise maintenant 512 × 512 m ; aucune
zone déjà enregistrée n'est agrandie ou migrée silencieusement.

Nouveau test d'intégration réel `crates/forge_world/tests/authoring_terrain.rs` :
création de zone neuve, génération des 64 chunks, retouche d'altitude à 8 m,
archive/réouverture, hash de source identique, hauteur centre et bords correcte,
nouvelle génération complète. **1 PASS**, zéro ignoré. Clippy ciblé et formatage PASS.
Journal : `E:/OpenRPG_Forge/.gate2-evidence/authoring-generator-tests.log`.

Le test couvre le générateur CPU, pas le rendu GPU. L'ancienne mention 128 m dans
R2_AUTHORING_CONTROLS décrit le premier candidat et est supersédée ici.
La projection live ne peut pas réutiliser telle quelle la voie de mise à jour
partielle actuelle : celle-ci est réservée aux deltas de hauteur, pas au changement
de nom/altitude de base. Il faut raccorder explicitement initialisation et
invalidation/reconstruction sans conserver une scène de démonstration comme résultat.

Bundle Trunk toujours suivi par son handle existant, journal
`E:/OpenRPG_Forge/.gate2-evidence/authoring-trunk-build-fixed.log`.
Aucun nouveau processus graphique. Gate 2 reste ouverte.
