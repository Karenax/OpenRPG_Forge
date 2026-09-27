# R2-003 — Création d'une zone extérieure paramétrée

## Création agrégée directe — évolution postérieure au candidat 75DA9340

Nouvelle commande `CreateZone { document }` : validation d'un terrain initial
sans corrections, réservation d'un nouveau dossier et session agrégée directe.
Le Studio l'utilise désormais après examen ; l'ancienne commande `Create` reste
disponible pour compatibilité et les anciens fichiers ne sont jamais convertis
par Open. Végétation, tracés et bâtiments ne nécessitent plus de migration du
terrain juste créé. Ce raccord a été observé sur le candidat 0F315A98 ci-dessous.

Le codec d'archive agrégée existant est conservé : son champ `migration` contient
un checkpoint terrain initial à zéro commande, marqué `forge-new-zone` v1 et lié
au hash du terrain fourni. Ce checkpoint est construit en mémoire, pas un fichier
ancien prétendument édité. Aucun faux reçu, acteur ou historique ajouté ; aucune
session terrain active parallèle. Le premier `authoring.json` n'est écrit qu'au
Save, directement au format agrégé. La migration d'une vraie ancienne archive
conserve son chemin distinct et ses contrôles de source.

`FileZoneSession` représente maintenant une sauvegarde absente par `None`, puis
lie les Save suivants au hash enregistré, comme le magasin de documents l'exige.
L'hôte refuse dossier existant/session active et valide les données avant mkdir.
Test intégré de création directe : refus d'une révision non initiale sans dossier,
transport JSON de la commande, Preview/Apply d'un bâtiment sans conversion,
absence de fichier avant Save, checkpoint sans reçus, Save/Close/Open exact et
Undo/Redo de la maison. Test PASS ; ancienne création, migration et FileZoneSession
PASS également (4 tests : 0,01 + 0,02 + 0,00 s, compilation 15,99 s).
Clippy domaine/test strict PASS après extraction de la sauvegarde de l'hôte et
des fixtures de test pour respecter les limites de taille de fonctions.
Clippy Studio Web strict PASS (31,25 s). Le candidat 75DA9340 ci-dessous ne
contient pas cette évolution ; sa preuve reste limitée au formulaire paramétré.

## Candidat 0F315A98 — création directe, forêt, maison et réouverture

Exécutable `E:/OpenRPG_Forge/.gate2-evidence/aggregate-creation-studio-20260914-0700/forge-studio-candidate.exe`,
SHA-256 `0F315A9858C4F3CA3E6407A0B67FAD7007EF4DCD3458571FF24FAF450937AE71`.
Frontend `aggregate-creation-web-20260914-0700` : Trunk code 0, compilation
1 min 19 s ; Tauri code 0, 30,59 s. Racine initialement vide
`E:/OpenRPG_Forge/.gate2-evidence/aggregate-creation-ui-20260914-0700`.

Dans le Studio, examen puis création aux valeurs plates par défaut ; ajout de
la recette de végétation seed 42, deux couches, par Preview/Apply ; ajout d'une
pièce 8 × 6 × 3 m à (0, 0, 0), également par Preview/Apply. Aucune conversion
technique ni écriture JSON manuelle. Absence d'authoring.json constatée avant
Save. Forêt rendue, maison sélectionnée et cadrée : huit volumes et ouverture
de porte visibles. Capture inspectée dans la conversation ; aucune qualification
artistique ou de performance déduite de ce contrôle fonctionnel.

Save puis Fermer la zone/Ouvrir effectués : session synchronisée et vue
d'ensemble forêt/maison restaurée. Archive de 15418 octets, schema
`forge.zone-edit-session` v13, tête v5 révision 3, exactement deux reçus
(végétation puis bâtiment). Bâtiment `76be5a7c-f146-5381-83ac-43e9881f09d1`.
Checkpoint initial marqué `forge-new-zone` v1, sans historique inventé.
SHA-256 de l'archive identique avant/après réouverture :
`F92C7F868B3FBC4DC3646D9FBAA7810B1F303978AE564A0AFCCA5443C9F777AC`.
Dernier reçu : tête `2755e899aaf2ddd3c0bc958ca70c463594f22039076002cdd8cd83ed737e7941`.

Fermeture normale du candidat ; PID 27376 et descendants 31944, 30980, 31880,
29272, 3976, 16440, 16752 tous absents au contrôle suivant. Aucun nouveau
benchmark ou rejeu des tests ciblés inchangés. Pas de parcours joué, de rivière,
de chemin ni de régénération après retouche démontrés par cet essai.

## Implémentation — 14 septembre 2026

`forge_project::authoring::creation::prepare_terrain` prépare un document initial
depuis les clés projet/zone, le nom, la seed, l'altitude, l'amplitude et l'échelle
du relief. Fonction pure : pas de création de dossier ou de nouvelle autorité.
Le défaut restitue exactement `new_terrain` (plat, seed 0). Taille 512 × 512 m,
pas 2 m et algorithme de bruit v1 conservés, conformément au générateur courant.
Altitude −4096..4096 m, amplitude 0..128 m, échelle 16..512 m ; valeurs non
finies refusées. Seed entière u64. Aucun import de scène ou d'asset.

Le Studio remplace « Créer une zone plane » par « Nouvelle zone extérieure » :
réglages, examen d'un résumé explicite, création du document examiné ou abandon.
L'examen ne prétend pas être un aperçu 3D. Un changement de réglage invalide
l'examen ; nom et clés sont également réexaminés avant transmission. La création
est désactivée si une zone est ouverte. Le candidat 75DA9340 utilisait encore
`AuthoringRequest::Create` ; le code courant utilise `CreateZone` décrit ci-dessus.
L'hôte garde ses refus : session active, zone existante, entrée invalide.
Les recettes et retouches restent dans le document et l'archive existants ; aucun
changement de schéma, de format de sauvegarde ou de branche.

## Vérifié

- Test unitaire `initial_recipe_is_deterministic_bounded_and_keeps_legacy_defaults` :
  déterminisme, identité indépendante du relief, défaut ancien identique, valeurs
  non finies et hors bornes refusées. PASS, 0,00 s après compilation 16,56 s.
- Test intégré `configured_zone_round_trips_and_never_overwrites_an_existing_or_dirty_zone` :
  Create, retouche de nom, refus d'une création concurrente sans nouveau dossier,
  Save/Close/destruction d'hôte/Open exact ; Undo/Redo après réouverture gardent
  les réglages initiaux ; archive préexistante jamais écrasée, octets inchangés
  tant qu'aucun nouveau Save n'est demandé. PASS, 0,01 s après compilation 14,99 s.
- Fmt et Clippy domaine/test ciblé PASS (3,66 s).
- Clippy Studio Web strict PASS (6,04 s), après renommage d'une variable trop
  similaire au callback ; aucune désactivation de warning.

## Candidat 75DA9340 — parcours Studio observé

Exécutable local `E:/OpenRPG_Forge/.gate2-evidence/zone-creation-studio-20260914-0630/forge-studio-candidate.exe`,
SHA-256 `75DA93406AD956ADCE0A5D107C52D3E52F6861DC1A094959046469EAADD7763E`.
Frontend `zone-creation-web-20260914-0630`, Trunk code 0 (Rust 1 min 20 s),
packaging Tauri code 0 (30,08 s). Nouvelle racine isolée
`E:/OpenRPG_Forge/.gate2-evidence/zone-creation-ui-20260914-0630`, sans archive copiée.

Parcours effectué dans le formulaire : examen des valeurs plates, saisie de
l'amplitude 24 m, bouton Create redevenu désactivé, nouvel examen affichant
24 m, création, terrain vallonné effectivement rendu/capturé, Save puis
Close/Open et même terrain observé. Seed 0, altitude 0 m, échelle 100 m ;
aucune écriture JSON manuelle. Les champs de création sont désactivés tant
qu'une zone est ouverte. Même hash terrain avant/après réouverture :
`9abe2527d64582ca6cd94ef36b37b9db3351d92c2dee26239c2a93468eb26d1f`.

Archive `81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`, 835 octets,
SHA-256 `5B80583F7A16BBB6C901658FE95F3226723D3234E83DAB4FE550A2DCADF93415`,
identique avant/après réouverture. Document terrain v2, archive v1, seed 0,
amplitude 24 m, aucun reçu de retouche. Aucun ancien dossier modifié.
Fermeture normale du candidat PID 25396 ; ses sept descendants propres et lui
sont absents ensuite. PID 32056 exclu du relevé de descendants : processus
Logitech démarré la veille, faux lien par réutilisation du PID 628 ; préservé.

## Encore à qualifier

Autres valeurs de seed/altitude/échelle, abandon et changement de clés après
examen non rejoués graphiquement ; limites couvertes uniquement par les tests
ciblés indiqués ci-dessus. Pas de qualification de performance de cette zone.
Création projet/module et onglets distincts restent à faire ; cette fonction ne
constitue pas ces assistants. La migration des archives anciennes reste explicite.
R2-003 implémenté et vérifié localement dans le périmètre fixe 512 m/pas 2 m.
La couverture graphique exhaustive des entrées ci-dessus reste distincte ;
aucun verdict global Gate 2, aucune publication.
