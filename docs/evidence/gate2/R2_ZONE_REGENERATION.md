# Régénération transactionnelle — base terrain/végétation

## Refus explicatifs — complément du 14 septembre

RegenerationReview::refusal_message produit la même explication dans
prepare_regeneration et ZoneEditSession pour les commandes brutes. Six causes
couvrent identité, grille, rebasage de relief indisponible, couche corrigée
supprimée, bâtiment incompatible et document effectif invalide. Codes conservés
pour diagnostic, ID/couche et verrou indiqués, pistes correctives en français.
Déverrouiller un masque n’est pas proposé comme réparation de sa disparition.
Aucune résolution automatique, modification d’archive ou nouvelle commande.

`cargo test -p forge_project --test zone_regeneration --offline --locked -j 2` :
4 PASS (compilation 28,64 s, tests 0,01 s), incluant nouveau test de refus
identique préparation/commande brute, archive inchangée, cible et verrou lisibles,
absence de message en l’absence de conflit. Clippy project lib strict PASS
(4,04 s). Pas de build/observation GUI ; F1C5DB66 ne contient pas cette évolution.
Les tests existants conservation/Undo/reprise et garde relief restent verts.

## Verdict consolidé R2-045 — 14 septembre 2026

Le scénario de conservation porte/clairière est qualifié localement. La dernière
lacune historique (ouverture après arrêt non observable à cause de l’outil) est
supersédée par le parcours réel du candidat F1C5DB66 documenté dans
R2_SCENE_INDEX.md : nouvelle instance, copie byte-identique de l’archive après
régénération, ouverture par le Studio, terrain/forêt/clairière/maison affichés.
Capture path-framed.jpg dans scene-index-ui-20260914. Aucune modification ou
sauvegarde pendant cette réouverture ; archive SHA-256 5269A9B0F430785FE9A307928B615E716EF0CD89C33F204A61142CD1A0BE7D20
inchangée. Fenêtre et descendants fermés.

Comparaison recontrôlée à la consolidation depuis before-regeneration.json et
l’archive rouverte : corrections JSON structurellement identiques (sérialisation
PowerShell profondeur 100), seed 42 puis 43, bâtiment
76be5a7c-f146-5381-83ac-43e9881f09d1 verrouillé avec door_offset_override_m=1.5,
correction de densité editor_trees verrouillée. La preuve des gestes initiaux
porte/peinture/verrou/régénération reste celle d’A64D5ABA ci-dessous ; elle n’est
pas remplacée par une création de fixture à la main.

Limites conservées : verrou du bâtiment entier, clairière à densité réduite
(minimum 2/255), conservation des corrections et reprise de session, pas de
rebasage du relief, verrou fin de porte ou suppression physique du journal.
La fermeture de l’historique signifie ici destruction de la session/processus,
puis reconstruction depuis l’archive, pas effacement des preuves. Aucun verdict
global Gate 2 ni nouvelle validation humaine déduit. Aucun nouveau lancement
graphique réalisé pour cette consolidation.

## Réglages de recette par couche — 14 septembre 2026

Implémenté : probabilité de placement et espacement minimum par ID de couche,
via `editor_vegetation::prepare_layer_request`, puis PreviewZone/ApplyZone.
Le générateur de commandes valide les bornes du document, refuse les valeurs
inchangées et les couches absentes, épingle les paramètres et la tête source
dans le stamp et l'identité de commande. Seed, autres couches et corrections
restent intactes. Un masque verrouillé ne verrouille pas ces paramètres de recette.
Aucun format ni dépendance ajouté ; applicable aux zones, pas à une carte figée.

Studio : champs par couche, résumé avant/après en pourcentages et mètres,
annulation de l'aperçu dès saisie (seed comprise), champs désactivés pendant
requête/playtest. Lignes identifiées par zone/couche/valeurs acceptées : conserver
le brouillon pendant Preview, le resynchroniser après Apply ou changement de zone.
Pas de nouvel aperçu 3D avant application ni de qualification graphique déduite.

Quatre tests `forge_world --test editor_vegetation` PASS (6,16 s de compilation,
0,01 s de tests), dont nouvelle commande, preview immuable, retry, masque verrouillé
conservé, couche voisine/seed intactes, archive/relecture et Undo, valeurs invalides
et ID absent refusés. Clippy ciblé world PASS. Trois tests de revue Studio PASS
(11,52 s de compilation), Clippy Studio WASM/web strict final PASS (6,38 s),
fmt et diff check PASS. Parcours graphique à grouper avec la prochaine
qualification, Gate 2 ouverte. Aucun candidat graphique lancé ni publication Git.

## Revue explicite des retouches — 14 septembre 2026

Le texte de proposition Studio est désormais dérivé du reçu examiné : seeds
avant/après, hash source, masques et verrous, bâtiments et portes retouchées,
chemins, rivières, placements et présence des remplacements terrain/matériaux.
Il distingue un remplacement vide de l'héritage et précise que le verrou porte
sur le bâtiment entier. La conservation exacte n'est annoncée qu'après égalité
des corrections sérialisées avant/après ; sinon un avertissement la refuse.
Aucune mutation, nouveau format ou seconde source de vérité. Le message
obsolète « playtest indisponible » de la source unifiée est remplacé par le
garde réel de concordance des projections/collisions.

Deux tests ciblés du texte PASS, y compris après traduction des booléens en
oui/non (compilation 5,96 s, tests < 0,01 s). Clippy Studio WASM/web strict PASS
(25,94 s, avertissement amont proc-macro-error2 inchangé), fmt PASS. Pas de
nouveau candidat construit ni de revue graphique de ce texte ; intégration
visuelle à observer lors du prochain parcours groupé, sans verdict R2-045.

## Complément Studio du 14 septembre 2026 — conservation acquise, reprise visuelle restante

Candidat existant A64D5ABA9666390E1397697F8A0ABA4C0D9C1F7BAF288FC6E22C6A45D9710DFE,
sans reconstruction ni nouvelle batterie de tests. Dans le Studio : porte à X local
1,5 m, verrou du bâtiment entier, clairière peinte sur editor_trees puis verrou
de cette couche ; chaque changement passe par Examiner/Appliquer. Save avant
régénération, seed de végétation 42 → 43 via Examiner/Appliquer, puis Save.
La nouvelle distribution forestière et la clairière sont observées dans la vue.
Il s'agit d'une densité fortement réduite (minimum 2/255), pas d'un masque vide.

Archive locale : `E:/OpenRPG_Forge/.gate2-evidence/retention-ui-20260914-0900/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`.
Référence avant : `before-regeneration.json` dans la même racine de preuve,
SHA-256 CF67FD4C6FD406E704701EC4622880B55413220E736AE50213A915ABAC6659F1.
Après : SHA-256 5269A9B0F430785FE9A307928B615E716EF0CD89C33F204A61142CD1A0BE7D20,
2 188 033 octets, neuf reçus. Comparaison structurelle sérialisée des corrections
avant/après strictement identique ; seeds relues 42 et 43.

Application fermée puis relancée. La lecture de fenêtre échoue deux fois avec
`foreground window did not report a process id` : aucun verdict de réouverture
visuelle. Le candidat relancé et ses sept descendants ont été arrêtés par leurs
PID identifiés. R2-045 reste ouvert pour cette dernière observation ; aucun
verrou fin par propriété, effacement/compaction du journal, rebasage du relief
ou parcours auteur complet n'est déduit de cette preuve. Les limites ci-dessous
décrivent le lot domaine historique du 13 septembre, pas toutes les capacités
Studio désormais disponibles.

13 septembre 2026, branche Rust, Gate 2 ouverte. Implémentation domaine sous
ADR-0014, pas de qualification du parcours auteur complet.

## Implémenté

Une revue expose hash/révision source, ancienne base, base proposée, corrections
conservées et tête effective proposée (absente en cas de conflit). Identité de
zone, grille/schéma terrain, références de matériaux et couches de densité sont
contrôlés. Supprimer une couche corrigée est un conflit, verrouillée ou non.
Les corrections ne sont pas réaffectées à un index voisin quand l'ordre change.

La commande Regenerate réutilise PreviewZone/ApplyZone et ZoneEditSession :
validation répétée côté session, refus d'un plan périmé, limite avant mutation,
retry idempotent et conservation exacte des corrections. Le reçu conserve les
bases avant/après et l'inverse ; Undo/Redo restaurent la base ET les corrections.
FileZoneSession garde le même DocumentStore et le même authoring.json.

Les archives sans régénération restent v1, avec ancien encodage des reçus
(base_change absent). Dès qu'une régénération est enregistrée, l'archive est v2.
Lecture v1/v2 par rejeu vérifié ; version incohérente ou inverse falsifié refusés.
Ce changement est explicite par commande, pas une migration lors d'Open.

## Garde-fou du relief

Les retouches de relief actuelles sont des deltas. Changer leur bruit de base
pourrait conserver les nombres tout en déplaçant la surface. En présence de
deltas authorés non vides, un changement de seed/paramètres de hauteur est donc
signalé par TerrainHeightReferenceChanged et refusé. La régénération de végétation
seule reste possible. C'est une limitation explicite, **pas** la conservation
absolue de toutes les hauteurs : leur rebasage exact reste à implémenter.

## Vérifications

3 tests `zone_regeneration` PASS : revue/preview immuables, changement de seed et
réordonnancement des couches, nom et masque de clairière verrouillé inchangés,
Apply égal au reçu, retry sans mutation, Undo puis archive/relecture/Redo,
lecture et roundtrip v1, archive v2, version falsifiée et inverse altéré refusés.
Conflits couche supprimée (avec/sans verrou), identité/grille changées, aucun
changement malgré commande brute conflictuelle. Test complémentaire du garde
relief et de l'absence de blocage d'une régénération de végétation seule.

Non-régressions ciblées : 2 tests `zone_session`, 1 `zone_host`, 1
`zone_file_session` PASS. Clippy project strict PASS, y compris après le garde
relief. Clippy Studio WASM/web PASS sur le
raccord archive/action avant ce dernier garde (avertissement amont de compatibilité
future proc-macro-error2 2.0.1 inchangé). Aucun build graphique ni test GPU lancé.

## Limites

- Pas de panneau de revue/résolution/régénération Studio, ni projections agrégées
  végétation reconstruites. Le playtest agrégé reste indisponible.
- Pas de transformation/porte, feature graph, navmesh ou nouveau générateur qualifié.
- Les stamps sont fournis par le producteur ; ce lot ne prouve pas la provenance
  d'un générateur externe ni le calcul de ses sorties.
- La conservation des masques est une preuve de données, pas une capture de forêt.
- Pas de compaction de l'Undo ou du checkpoint, budget toujours 4 Mio / 1024 reçus.
- Le nouveau test de régénération relit une archive mémoire ; la sauvegarde disque
  est couverte par la non-régression d'adaptateur, pas par un parcours v2 interrompu.
- Qualification transversale ADR-0006, revue humaine et R2-045 toujours dues.

Aucun fichier utilisateur migré, aucun asset importé et aucune publication Git.
