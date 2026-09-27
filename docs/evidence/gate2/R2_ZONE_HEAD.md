# Tête agrégée de zone — fondation ADR-0014

Modèle `zone_authoring::ZoneAuthoringHead` ajouté le 13 septembre 2026 :
format `forge.zone-authoring` v1, révision, base acceptée terrain/végétation,
identification du générateur (nom/version/hash des entrées), corrections séparées.
Les seeds restent dans les recettes de base. Aucun cache moteur sérialisé.
La vue effective est une valeur dérivée, pas une seconde tête modifiable.

Les corrections de densité sont absolues, par ID de couche, avec marqueur de
verrou. Déverrouiller ne supprime pas la correction. Hauteurs et matériaux sont
des remplacements typés de couches : None hérite, Some(vide) efface explicitement.
Le modèle ne déduit pas une provenance historique qui n'existe pas.

Validation : schéma/version, bornes d'identification du générateur, documents
de base et effectifs, cohérence projet/zone/étendue, références matériau et rôle,
IDs de corrections uniques/triés, refus des cibles orphelines. Sérialisation et
lecture bornées à 4 Mio ; hash portant sur tout l'agrégat, pas sur le terrain seul.

Deux tests ciblés PASS et Clippy project strict PASS : roundtrip/hash/vue effective,
base intacte, correction conservée après déverrouillage et sans journal Undo,
cible disparue, doublon, identité divergente, version inconnue, taille excessive.
Un nom de champ de filtre erroné à la compilation initiale a été corrigé vers
`material_layer_id` et son rôle sémantique.

## Limites explicites

Préparation explicite de migration ajoutée dans `zone_authoring::migration` :
relecture complète de l'archive terrain et de ses reçus avant transformation,
garde sur le hash source, checkpoint historique conservé octet pour octet,
corrections nom/altitude/hauteur/matériaux comparées au document initial de session.
Ce document initial est un checkpoint accepté, pas une provenance génératrice
reconstituée. L'appelant doit fournir une identification honnête de la base.
La vue effective doit égaler intégralement l'ancienne vue, hormis le passage
explicite au format terrain v3 ; sinon préparation refusée. Proposition avec
historique bornée à 4 Mio. Relecture de proposition recalcule la tête et refuse
son altération. Aucune publication ni migration de fichier n'est exécutée.
Test migration PASS : vue reconstruite, archive originale inchangée, nom/altitude
conservés, roundtrip de proposition, Undo possible dans le checkpoint historique,
source périmée et tête altérée refusées. Les 2 tests tête restent PASS ; Clippy
project/all-targets strict PASS. L'Undo à travers la future frontière de migration
dans une session Studio unifiée n'est pas démontré par ce checkpoint seul.

## Transactions agrégées et adaptateur disque

`ZoneEditSession` ajouté : commande typée sur les corrections, hash/révision/ID,
preview sans mutation, inverse, Undo/Redo, retry idempotent et rejet de réemploi
d'ID. Archive unique versionnée contenant tête courante, checkpoint de migration
et reçus vérifiés par rejeu. Bornes 1024 commandes / 4 Mio avant mutation.
Une correction de densité verrouillée doit d'abord être déverrouillée avant
changement/retrait ; Undo/Redo restaurent les valeurs historiques. Pas encore
de fusion de régénération ni de provenance acteur/temps complète.

`FileZoneSession` utilise le même DocumentStore/verrou/authoring.json. Open refuse
le format legacy sans migration ; PrepareMigration exige les octets sauvegardés
exacts, sans staging non résolu. Seul Save remplace atomiquement le fichier par
l'archive agrégée, avec contrôle du hash sauvegardé. Aucun second fichier éditable.
Le checkpoint historique reste embarqué dans l'archive nouvelle.

À ce stade initial, le host applicatif et le Studio utilisaient encore
FileTerrainSession. Le raccord host a depuis été effectué ci-dessous ; les anciens
gestes après migration et Undo à travers la frontière restent à intégrer.

Trois tests ciblés PASS : preview/apply/retry/Undo/reopen/Redo, commande invalide,
source périmée, réemploi d'ID et tête altérée refusés ; migration disque bloquée
par le verrou legacy, absence d'écriture lors de préparation, Save/Open exacts,
checkpoint historique relisible et absence de staging. Clippy project strict PASS.
Le test disque utilise un dossier temporaire dédié sur E: et le retire après succès.
Les refus de taille et le verrouillage de densité sont codés, pas encore couverts
par ces trois tests ; ne pas extrapoler cette preuve à toute la nouvelle session.

Ce modèle n'est **pas encore exposé par les gestes Studio**. Aucun fichier utilisateur
migré ou sauvegardé, pas de nouveau chemin d'écriture parallèle. Restent :
provenance acteur/temps, fusion/résolution de conflits, verrous des autres propriétés,
placements/splines et projections Studio. Le marqueur locked ne prouve pas encore
le refus transactionnel d'une régénération incompatible. Pas de verdict R2-045.

## Raccord de l'hôte unique — 13 septembre 2026

Implémenté : AuthoringHost conserve un seul ActiveSession (terrain ancien OU zone
agrégée). Open choisit le lecteur sans migrer, sous un seul verrou DocumentStore.
PrepareZoneMigration ne fait qu'examiner les octets sauvegardés exacts, y compris
leur formatage, et refuse les modifications non sauvegardées/conflits externes.
BeginZoneMigration exige la fermeture de l'ancienne session et revalide les octets
exacts ; la migration est dirty jusqu'au Save explicite du même authoring.json.

PreviewZone/ApplyZone réutilisent ZoneEditSession. Inspect fournit la tête canonique
et une vue terrain dérivée distincte, sans nouveau writer. Save/Close, Undo/Redo
disponibles et catalogue connaissent les deux formats. Le lecteur de récupération
valide aussi l'archive agrégée. Les commandes terrain anciennes, leur historique et
le playtest sont explicitement refusés sur une zone agrégée tant que les adapters
Studio/rendu ne sont pas raccordés : ne pas jouer seulement une partie de la zone.

Vérifications ciblées, codes de sortie 0 :

- `cargo test -p forge_project --test zone_host --offline --locked` : 1 PASS,
  préparation sans mutation, garde Save/Close/session unique, migration sans Save
  implicite, preview/apply/retry, sauvegarde/réouverture exacte, catalogue au hash
  agrégé et refus du playtest incomplet. Fixture temporaire sur E: retirée après test.
- `cargo test -p forge_project --lib authoring --offline --locked` : 4 PASS,
  ancien protocole et playtest conservés.
- `cargo test -p forge_project --test authoring_catalog --test authoring_recovery
  --offline --locked` : 4 PASS. Couvre la non-régression legacy ; la récupération
  d'un staging agrégé interrompu reste à tester spécifiquement.
- Clippy project/all-targets et Studio WASM/web strict : PASS ; avertissement
  amont préexistant proc-macro-error2 2.0.1 de compatibilité Rust future.
- Formatage et diff-check : PASS. Une extraction de méthodes de l'hôte a corrigé
  le dépassement Clippy de longueur ; une accolade manquante corrigée avant succès.

Aucun lancement graphique, fichier utilisateur migré ou publication Git dans ce lot.
Le binaire Studio précédemment observé n'inclut pas ces derniers changements.
Qualification transversale ADR-0006 toujours due ; ces 9 tests ne ferment pas Gate 2.

## Raccord des outils terrain Studio — replay ciblé

`ZoneAuthoringHead::prepare_terrain_action` prépare les mêmes opérations de relief,
nom, altitude et matériaux dans les corrections agrégées. Les autres couches,
masques et verrous restent inchangés ; les commandes sont liées au hash agrégé,
et Undo/Redo portent sur la session entière. Toute migration de schéma terrain
isolée est refusée dans ce mode. Trois tests tête PASS, dont un nouveau test de
préservation inter-domaines, préparation immuable et entrées invalides.

Studio codé : routage selon la tête active, conservation du reçu exact prévisualisé
avant Apply, aperçu logique, panneau d'examen/conversion explicite. La conversion
enchaîne Close puis BeginZoneMigration par le bus existant ; Save reste séparé.
Le checkpoint n'invente ni végétation ni provenance ancienne : il est identifié
comme `accepted-terrain-checkpoint`. Le playtest agrégé et le lecteur détaillé de
journal sont explicitement indisponibles ; Undo commence à la conversion et
l'archive historique est préservée. Pas de nouveau writer ni migration automatique.

Clippy project et Studio WASM strict PASS. Initialement refus de longueur de
composant, points-virgules et comparaison flottante exacte : corrigés sans baisse
des contrôles. Build candidat et replay graphique en cours ; ne pas confondre
compilation du raccord et validation du parcours utilisateur. Replay réalisé
ensuite ci-dessous ; la qualification Gate 2 reste distincte.

### Candidat réellement exécuté

13 septembre 2026, écran secondaire, un seul Studio : SHA-256
`767bafe576572545431623dce37884b671881bb7c54f790be0a6ec68dfc89862`.
Trunk offline (2 min 43 s de compilation) et Tauri debug/no-bundle (1 min 03 s),
codes de sortie 0. Configuration locale `E:/OpenRPG_Forge/.gate2-evidence/tauri-authoring-zone.conf.json`.

Fixture copiée, pas de données utilisateur :
`E:/OpenRPG_Forge/.gate2-evidence/authoring-zone-ui-projects/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`.
Archive initiale SHA-256 `14051afb83135b38e16c837a7565e55fc7d14c4605f8297a283b405ac941e2c1`.

Observé dans le produit : Open legacy → Examiner → Convertir (Close puis migration,
rechargement de la projection) → altitude 3 m / aperçu / Apply → Undo → Redo →
Save → Close → Open. Le terrain et les bandes préexistantes relief/matériaux sont
visibles après conversion et réouverture. Le champ altitude revient à 3 m et
le Studio annonce « Session synchronisée ». Le playtest agrégé reste désactivé.
Les captures ont été affichées et inspectées dans la conversation, sans PNG local.

Contrôle fichier : aucun changement jusqu'au Save explicite ; archive finale
`forge.zone-edit-session`, révision 13, 3 reçus edit/undo/redo, 618 hauteurs et
576 matériaux conservés, altitude 3. Checkpoint legacy identique octet pour octet
à l'original ; aucun authoring.pending. Original toujours au hash initial.
Archive finale 2 881 611 octets, SHA-256
`f4ddcf5ea1cb7c5796ba7b040b441a365415e24e11d3628940e29de43269f0a1`.
Hash de tête `5a147db2510fd17127a34acac2e660f9caa81ac51b6fcb9342da7455037ebcb8`.
Hash de projection terrain après Redo et après Open identique :
`29d9189b25213006834b59ccccf589cb94eeb3355639aa61c94d36439b193ab1`.
Ces deux hashes ont des périmètres différents : ne pas exiger leur égalité.

Studio fermé ; PID 29436 et descendants 4992, 27460, 19740, 31628, 9872, 5148,
8228 vérifiés absents. Aucun navigateur utilisateur arrêté. La tentative UIA
SetValue sur le champ numérique n'était pas supportée ; saisie clavier après
vérification visuelle du focus utilisée, sans modification de fichier hors produit.

Limites : pas de nouveau geste relief/matériaux sur agrégat dans ce replay, pas
de végétation effective ni régénération, pas de mesure de latence ou performance.
Le stockage du checkpoint et des corrections complètes consomme déjà 2,88 Mo
sur les 4 Mio autorisés ; les limites restent explicites, pas de compaction acquise.
Le marqueur de verrou est préservé par le test domaine, pas testé dans cette
fixture graphique sans végétation. Aucun verdict de gate ni publication.
