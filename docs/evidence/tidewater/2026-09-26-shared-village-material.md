# Matériau du village partagé avec le moteur

`forge_engine::tidewater_material` expose maintenant le matériau du village,
ses paramètres et `ObjectShadowSettings`. Le prototype village et le producteur
d'ombres utilisent ces types partagés. Les numéros de bindings, formats
d'attributs, variantes par matériau, modes de transparence et valeurs par défaut
restent ceux du code précédent. Le shader est toujours résolu sous `village.wgsl`.

Le module attend explicitement les textures d'environnement et d'ombres fournies
par son appelant. Ce déplacement ne crée pas ces ressources dans Studio et ne
constitue pas une qualification de leur mise à jour interactive.

`cargo check -p forge_engine --lib --example tidewater_plant_probe
--example tidewater_object_shadow_probe -j1` passe. Le contrôle Clippy strict
s'arrête sur deux diagnostics `needless_pass_by_value` dans
`forge_project/src/zone_authoring/session.rs` (paramètres `head` et `receipts`),
hors de ce changement. Ils ne sont pas neutralisés. Le build exécutable réussit.

Capture native : premier lancement `native-shared-material-001` terminé avec
code 1, sans PNG, message générique de fin de probe et avertissement de readback
sur canal fermé. Cause non déterminée ; ce lancement n'est pas une preuve verte.
Une relance avec le même binaire produit `native-shared-material-002.png`, code 0.
L'image et les deux buffers d'ombres sont exactement identiques à l'état initial
des bâtiments (zéro pixel différent). Rapport `shared-material-render-proof-001.json`
sous `E:/OpenRPG_Forge/.tidewater-integration/`.
Hash PNG : `579e78405e5d4886faaa96a79ee6042a9de3b4a3f4c412d109888255f8082d64`.
La pose valide la non-régression visuelle du matériau partagé, sans démontrer
la stabilité de tous les lancements ni le raccordement Studio.
