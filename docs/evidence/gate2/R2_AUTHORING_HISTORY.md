# Historique des retouches — 12 septembre 2026

## Implémenté

ReadHistory lit le journal existant de TerrainEditSession, sur le hash exact du
document courant. Pages bornées à 32 commandes, ordre récent → ancien, total et
offset explicites. Les résumés contiennent action (retouche/Undo/Redo), ID,
révision, hash résultant et avant/après (nom, altitude, nombre de samples de
relief). Les tableaux de samples ne sont pas envoyés dans la page. Leur compte
décrit les snapshots, pas le nombre de samples réellement changés.

Aucun nouveau format persistant, aucune sauvegarde implicite, aucune mutation
du document lors de la consultation. Lecture autorisée pendant playtest sans
sortir du mode. Offset hors journal et hash périmé refusés. La session fournit
can_undo/can_redo, tenant compte du curseur et de la limite de commandes.

Studio : panneau affichable à la demande, résumés lisibles, détails techniques
repliés, pagination et actualisation sur changement de hash. Réponse périmée
ignorée lors d'un changement de page/zone ; panneau fermé = pas de lecture.
Une simple réponse de prévisualisation au même hash ne relance pas la lecture.
Annuler/Rétablir sont désactivés si indisponibles ou pendant playtest.
Les commandes de mutation conservent le bus existant ; la lecture ne supprime
pas un aperçu actif et ne remplace pas la projection.

## Vérifié

`cargo test -p forge_project --test authoring_history --locked --offline` :
2 PASS, 0 échec, 0 ignoré. Scénario 37 commandes : pages 32/5, Undo/Redo et
avant/après, reprise exacte depuis archive, refus hash/offset invalide,
conservation des octets après lecture, nouvelle retouche après Undo invalidant
Redo. Hôte : session sale préservée, aucun fichier sauvegardé par lecture,
playtest inchangé. Tests synthétiques isolés sur E:.

Clippy forge_project/all-targets strict PASS (2,60 s), Studio WASM strict PASS
(26,62 s). Avertissement amont proc-macro-error2 future-incompatibility inchangé.
Formatage et contrôle du diff appliqués. Aucun processus graphique lancé.

## Encore à qualifier

Panneau non inspecté graphiquement : à grouper avec le prochain lot d'interface.
Le candidat de récupération 64a59560... ne contient pas ce nouveau panneau.
Pas de verdict global R2-037/038 ni de contrat partagé ADR-0006 acquis par ces
tests ciblés. Limites préexistantes 1024 commandes/4 Mio inchangées ; pas de
compactage, horodatage ou identité humaine/IA ajoutés au journal. Gate 2 ouverte.

## Replay graphique groupé historique/raccourcis

Candidat natif SHA-256
`c999bcfa61e6f451851e9f5855a1fdc857a4ac30d9a31a17d528b8d8cfb48894`,
Trunk/WASM + Tauri debug sans bundle PASS (2 min 34 s + 1 min).
Configuration locale `.gate2-evidence/tauri-authoring-historykeys.conf.json`,
écran secondaire x=-1920/y=52, fenêtre 1920×1080. Pas de benchmark graphique.
Fixture isolée `.gate2-evidence/authoring-historykeys-ui-projects`, zone
81de8d42-89b9-58b5-b090-406cf4c63d06, copie de la preuve de brosse révision 4.

Ouverture puis panneau : 3 commandes restaurées, tri récent d'abord et résumés
corrects (révision 4 : 0→618 ; révision 3 Undo : 109→0 ; révision 2 : 0→109).
Ctrl+Z hors champ texte : révision 5, nouvelle entrée Annulation 618→0,
bouton Rétablir activé, retouches non sauvegardées et relief effectivement retiré.
Ack CPU/Rapier : 1945581192dad39280d188880a77bd85c92198e669f2018692a7f4a4d5f82937.
L'archive reste identique à l'original avant Ctrl+S.

Ctrl+Y : révision 6, entrée Rétablissement 0→618, relief réaffiché,
bouton Rétablir désactivé ; ack 01f205f9000b6d3c9b80541e0c7cd6672662ee057d6686d31fe9403aa7c1e3d1.
Ctrl+S : session synchronisée, 5 commandes ; lecture de l'archive confirme
les deux reçus undo/redo aux révisions 5/6. Hash archive sauvegardée :
`5e84f542a314e4a59517fa9fd54ac89197f602f487fd82947a81ecaefbd11757`.

Dans le champ Nom, suffixe temporaire saisi puis Ctrl+Z : texte initial restauré,
terrain/hash révision 6 inchangés, aucune retouche non sauvegardée. Les captures
du panneau, de l'Undo/Redo et du champ ont été affichées et inspectées dans la
conversation ; aucun PNG local prétendu. UIA a fourni une position hors fenêtre
après défilement : reprise par coordonnée issue d'une nouvelle capture.

Original de brosse hors fixture intact (dd54b814...b496a9). Studio PID 5652 et
descendants 31108, 8864, 4016, 24160, 10060, 31260, 29012 fermés puis absents
au contrôle final. Ancien binaire récupération conservé dans
`.gate2-evidence/studio-authoring-recovery-tested.exe`.

Restent non rejoués graphiquement : pages >32, Ctrl+Maj+Z, IME, maintien des
touches, contenteditable, refus en playtest et annulation d'un geste actif.
La pagination métier reste prouvée par le test natif, pas par cette capture.
Ce replay ne clôt pas Gate 2 ni la qualification partagée ADR-0006.

## Raccord du journal agrégé — 13 septembre 2026

`ReadHistory` sélectionne maintenant le lecteur terrain ou zone selon l'unique
session active. Le lecteur agrégé utilise le hash de tête, jamais celui du terrain
dérivé, et produit au plus 32 reçus récents par page sans tableaux de samples.
Résumé typé des overrides nom/altitude/relief/matériaux, masques et verrous par ID
de couche, recette/générateur/seeds. Les hashes de contenu distinguent les éditions
de même taille ; None (héritage) reste distinct de Some(empty). Undo et Redo lisent
les inverses réellement appliqués, y compris les changements de base.

Studio raccordé au même panneau paginé, memo sur hash d'autorité et garde d'époque
des réponses conservés. Message d'indisponibilité retiré ; limites du checkpoint
antérieur explicitement affichées. Résumés lisibles, hashes et propriétés techniques
sous détails repliés. Aucun changement du format d'archive ni écriture par lecture.

Vérification ciblée : 2 tests zone_history, 2 tests historiques terrain, test host
de migration/Save/Open/compaction avec lecture agrégée et refus du hash dérivé.
Cas couverts : masque modifié à taille constante, verrou sans modification du
masque, régénération et inversion de seed, compact round-trip, 35 commandes en
pages 32+3, offset invalide, hash périmé et lecture sans mutation. Clippy project
all-targets et Studio WASM/web strict PASS. Corrections de compilation limitées
au type de tableau de test et aux signatures Option empruntées ; aucun lint désactivé.

Pas de nouveau lancement graphique pour ce lot : raccord UI compilé, pas encore
inspecté dans le candidat. Le précédent replay terrain n'est pas présenté comme
preuve du journal agrégé. Qualification workspace ADR-0006 encore due. Pas de
qualification globale R2-037 ou Gate 2, aucun push ni processus graphique ouvert.

### Replay groupé du journal et de l'inspecteur

13/09/2026 : build Web 3 min 29 s, Tauri 1 min 28 s, exit 0 ; EXE SHA-256
`e4e75bc6cf7806d15f103c008cfe3024b03ca8286619cccd4ae4b45d3794443a`.
Lecture de la sauvegarde de clairière dans `authoring-density-ui-projects`
sous `E:/OpenRPG_Forge/.gate2-evidence/`, écran secondaire. Journal ouvert :
10 commandes, révision 20 seed 43 → 44, révision 19 masque modifiable → verrouillé,
révision 18 héritage → masque ; inverses des anciennes régénérations présents.
Message de limite du checkpoint antérieur visible, identités techniques repliées.
Captures affichées et inspectées dans la conversation, sans PNG exporté.
Session synchronisée ; aucune mutation/Save, fichier conservé exactement :
`0b3bf389156f08b067558d5c0240aeda84747fd1c95b47379429ac6c71e1452d`.
Pagination >32 et rafraîchissement après nouvelle retouche non rejoués dans ce
contrôle de lecture ; tests domaine antérieurs seulement pour ces propriétés.
Studio PID 23436 et descendants 21080, 28780, 6024, 15744, 30468, 26764, 27216
fermés puis absence vérifiée. Aucun push ni verdict Gate 2.
