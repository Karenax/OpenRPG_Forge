# Peinture persistante — 13 septembre 2026

15 septembre : changer couche/rayon/intensité/effacement annule désormais le
geste armé et l'aperçu. Le callback ne peut plus conserver les anciens réglages
pendant que le formulaire en affiche d'autres. Clippy WASM PASS ; geste réel
encore dû sur prochain candidat groupé, 8C6A960A antérieur. Détail et protocole
dans R2_TERRAIN_BRUSHES.md, section « Réglages et aperçu périmé ».

ADR-0014 et ADR-0015 acceptés pour implémentation par « j'autorise », en réponse
à la demande explicite conjointe. Pas de passage de Gate 2 ou de publication Git.

## Implémenté dans ce lot

- Terrain v3 opt-in : corrections clairsemées par coordonnées Z/X et ID de
  couche, poids u16 positifs, somme 65535, ordre/uniqueness/bornes contrôlés.
  Champ vide omis en JSON : les formes v1/v2 existantes ne changent pas.
  Constructeur historique encore v2 ; aucune migration à l'ouverture.
- Commandes SetSchemaVersion et SetMaterialOverrides : Preview/Apply, identité,
  hash/révision, inverse, Undo/Redo et rejeu existants. Descendre en v1/v2 avec
  peinture restante est refusé ; une migration ne jette pas l'historique.
- Archive v2 pour le nouveau contrat. Les anciens journaux v1 restent lisibles
  et produits inchangés pour les sessions anciennes sans nouvelle commande.
  Rejeu des inverses/hashes conservé, pas de simple réétiquetage d'archive.
- Garde des 4 Mio avant application des nouvelles commandes et à la création
  d'une session v3. Un dépassement refuse la préparation sans mutation.
- Poids effectifs raccordés aux trois couches du générateur : mesh complet,
  surface interpolée (donc consommateurs comme la végétation) et patch partiel.
  Recherche par ID, pas par index dans les corrections. Le générateur conserve
  sa limite existante de trois couches ; pas de palette 32 couches revendiquée.
- Chunks touchés dérivés du diff, y compris frontières communes. Peinture seule :
  positions/normales/indices/collisions inchangés dans les données générées,
  aucune invalidation de collider. Les poids survivent au recalcul du relief.
- Résumés des nouvelles commandes ajoutés à l'historique et aux reçus Studio.

## Vérifications ciblées déjà obtenues

Deux tests project `terrain_material_authoring` PASS : archive legacy inchangée,
migration non mutante en preview, historique/Undo/Redo/réouverture et retry,
refus de downgrade destructeur, poids/IDs/coordonnées/duplications invalides,
limite d'archive refusée sans mutation (0,84 s d'exécution initiale).
Un test world `terrain_material_paint` PASS (0,05 s) : correction à une frontière
de quatre chunks, égalité complète/partielle, conservation exacte géométrie et
colliders après peinture seule, retouche du relief, surface effective et effacement.

Le test world a d'abord échoué à compiler sur un nom d'API erroné, corrigé vers
surface_at. Comparaisons des valeurs flottantes attendues changées en comparaison
de bits pour satisfaire Clippy sans tolérance relâchée. L'ancien test de version
inconnue d'archive utilise désormais 999, car 2 devient une version autorisée.

## Non livré / qualification restante

Contrôle regroupé final du lot : `cargo test -p forge_project -p forge_world
--locked --offline` : **224 tests PASS**, aucun ignoré. Clippy strict des deux
crates, tous targets, et du Studio lib web WASM : **PASS**. `git diff --check`
PASS (avertissements CRLF préexistants hors lot). Un ancien test terrain rejetait
v3 : il vérifie maintenant explicitement son acceptation puis le refus de 999.
Les processus de validation ont terminé ; aucun essai graphique lancé pour ce lot.

Pinceau domaine ajouté : rayon/intensité, mélange des poids effectifs, quantification
par plus grand reste (somme 65535), effacement local et trait borné réutilisant
l'échantillonnage spatial du relief. Deux tests ciblés supplémentaires PASS :
déterminisme, découpage équivalent du trait, normalisation, limites spatiales,
source intacte, effacement, peinture pleine et refus v2/paramètres non finis.
La limite d'archive reste vérifiée par Preview/Apply de session, pas par le seul pinceau.

Contrôles Studio maintenant raccordés dans le code : migration v3 via Preview/Apply,
palette des trois couches, rayon/intensité, clic/trait, effacement explicite et
garde de hash contre un trait préparé sur une ancienne zone. L'aperçu dérivé
affiche au plus 2048 marques colorées selon les poids proposés, sans remplacer
terrain/collisions : représentation logique, pas prévisualisation des textures finales.
Effacement : retour complet aux règles dans le rayon (intensité non applicable).
Clippy Studio WASM et world strict PASS ; les deux tests pinceau incluent maintenant
le hash, la non-mutation et les marques finies de l'aperçu. Deux erreurs initiales
de typage/ownership dans les composants Leptos ont été corrigées.
Essai graphique désormais effectué (voir ci-dessous). Le premier appel Trunk a refusé NO_COLOR=1 ; relancé
avec la valeur booléenne explicite true, sans changer le code produit.

Le patch CPU remplace des chunks dérivés. Lot moteur en cours : lorsque positions,
normales, UV et indices sont identiques, seule la couleur/poids du Mesh Bevy est
remplacée, avec snapshot complet pour rollback. Les allocations de géométrie du
mesh vivant ne sont plus remplacées. Test natif PASS : quatre chunks de frontière,
allocations positions/normales inchangées, indices identiques, bindings conservés,
handles et ticks de changement Collider inchangés (0,76 s).
Clippy moteur lib strict PASS. Second test corruption après commit PASS :
restauration de toutes les projections et du document source (0,74 s).
Les 10 tests `terrain_update::tests` PASS ensemble (2,18 s) sur le même binaire,
dont les refus/rollback et le relief préexistants. La compilation préalable a
reconstruit des dépendances natives ; aucune mesure de performance n'en est déduite.
Cette dernière modification moteur n'est pas incluse dans le candidat Studio
07be692a décrit ci-dessous : replay graphique regroupé encore dû.
La granularité des transferts
GPU reste gérée par Bevy et n'est pas prouvée par cette modification CPU.
Parcours disque natif automatisé Save/Close/Open désormais PASS (1 test ciblé,
0,01 s, Clippy strict PASS) : migration, peinture, altitude, réouverture exacte,
Undo conservant la peinture sans sauvegarde implicite, Redo/Save/réouverture,
absence de staging résiduel. Fixture temporaire propre au test retirée après succès.
Cela ne remplace pas le parcours UI. Revue des cas mixtes et contrôles
transversaux des contrats partagés ADR-0006 encore dus. Les tests ciblés ne
qualifient pas Gate 2, ni la régénération générale et ses conflits ADR-0014.

## Parcours Studio observé — candidat du 13/09

Trunk PASS (compilation WASM 3 min 18), Tauri debug PASS (natif 1 min 33).
Exécutable SHA256 `07be692a0d45071004c2d792b95572930486e1620857368a401bd3e9233fe7ab`.
Config locale `E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring-materials.conf.json`.
Essai sur écran secondaire, racine isolée `authoring-materials-ui-projects`.
La tentative Créer sur la clé déjà présente a été refusée sans écrasement ;
Ouvrir a chargé la copie existante. Migration v2→v3 prévisualisée puis appliquée.
Couche earth, rayon 12 m, intensité 0,5 : trait de 576 cases, marques colorées
visibles avant Apply et bande de terre visible après Apply. Une seule annulation
retire le trait entier, Rétablir le restaure. Save/Close/Open conserve la bande.
Captures inspectées dans la conversation, pas de PNG exporté ni vidéo revendiqués.

Archive `81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json` :
SHA256 avant `5e84f542a314e4a59517fa9fd54ac89197f602f487fd82947a81ecaefbd11757`,
après `14051afb83135b38e16c837a7565e55fc7d14c4605f8297a283b405ac941e2c1`.
Archive v2, reçus migration/peinture/Undo/Redo : 0/576/0/576 cases.
Hash final `37ae7a7e7754f71ba024258aa57574521af9ba8784abdb426b4ca3748ca0fccb`,
identique à celui affiché par le moteur après réouverture.
Fermeture Alt+F4 ; PIDs possédés 6192,10052,19892,29172,24656,11596,13564,9568
tous absents au contrôle après fermeture. Aucun navigateur utilisateur arrêté.

Limites : effacement local validé dans le domaine mais non rejoué dans ce parcours UI ;
retouche du relief après peinture validée en tests, pas dans ce replay graphique.
Pas de mesure de latence auteur ni de preuve d'identité des objets GPU.
Identités/ticks Rapier contrôlés par le test natif postérieur décrit plus haut.
