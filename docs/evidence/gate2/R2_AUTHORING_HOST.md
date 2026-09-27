# Gate 2 — hôte applicatif et protocole Studio

12 septembre 2026, Rust, travail local non publié.

## Implémenté

Protocole moteur-neutre `AuthoringRequest` / `AuthoringState`, partagé par les
consommateurs. Hôte natif : Create/Open/Inspect/Preview/Apply/Save/Close, une session
active, état modifié, document courant et reçu de commande. Refus de fermer des
retouches non sauvegardées ou de remplacer une session ouverte implicitement.

Le dossier autorisé est injecté au backend. Le frontend ne fournit que des IDs
de zone et des documents/commandes typés : pas de chemin. Une zone existante n'est
jamais écrasée à la création ; ouverture avec contrôle d'identité et de parent
canonique. Le modèle de concurrence locale coopérative du stockage reste applicable.

Raccord candidat dans `execute_studio_command` Tauri : état natif protégé par
Mutex, racine `FORGE_AUTHORING_ROOT` configurée avant lancement, rejet explicite
si elle est absente. Aucune permission filesystem ajoutée à la WebView.

## Vérifié et limites

97 tests core/project PASS (17 + 80), zéro ignoré ; Clippy strict PASS.
Test du protocole : sérialiser Create, créer, refuser fermeture/switch non sûr,
prévisualiser sans mutation, appliquer le même reçu, sauvegarder, fermer,
rouvrir avec égalité du document, refuser Save sans session.
Log : `E:/OpenRPG_Forge/.gate2-evidence/authoring-host-tests.log`.

Le raccord Tauri est encore candidat : contrôle de compilation suivi dans
`E:/OpenRPG_Forge/.gate2-evidence/authoring-studio-check.log`. Aucun lancement
ni parcours UI qualifié dans ce rapport. Contrôles frontend et qualification
workspace/WASM restent requis. Les boutons, assistants de création et projection
du terrain édité ne sont pas encore livrés.

Les dossiers créés avant une erreur I/O sont conservés et ne sont pas nettoyés
automatiquement. Le choix de reprise initiale/corrompue et l'abandon explicite
des retouches restent à exposer. Pas de qualification globale de Gate 2.
