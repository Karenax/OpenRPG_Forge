# Gate 2 — récupération explicite d'une sauvegarde interrompue

12 septembre 2026, branche Rust, sans publication ni modification de carte.

## Implémenté

Le stockage expose le candidat temporaire et sa SHA-256. La résolution exige
l'empreinte inspectée et celle du fichier enregistré ; un changement refuse
l'opération. Les deux versions sont archivées dans des fichiers `recovery-HASH.json`
avant de promouvoir le candidat ou de libérer le staging en conservant le fichier
enregistré. Les archives existantes doivent correspondre exactement ; elles ne
sont jamais écrasées. Les copies sont synchronisées avant de toucher au staging.

L'adapter terrain vérifie le journal candidat et les identités projet/zone avant
promotion, refuse une récupération pendant des retouches non sauvegardées et
remplace ensemble session et état de sauvegarde. Un candidat JSON tronqué peut
être conservé comme archive sans être promu. Aucun choix automatique de version.

## Preuves exécutées

- `cargo test -p forge_core -p forge_project --locked --offline` : **96 PASS**,
  17 core + 79 project, zéro ignoré ; comprend le helper enfant inactif hors scénario.
- `cargo clippy -p forge_core -p forge_project --all-targets --all-features --locked --offline -- -D warnings` : PASS.
- Log : `E:/OpenRPG_Forge/.gate2-evidence/recovery-tests.log`.
- Sous-processus réel : arrêt `process::exit(77)` après sync du staging et avant
  remplacement, sans destructeurs ; code de sortie vérifié. Le parent rouvre
  immédiatement le verrou, constate ancien fichier + candidat, refuse un hash
  erroné, promeut, vérifie les deux archives puis sauvegarde à nouveau.
- Conservation du fichier enregistré avec candidat tronqué archivé ; prochaine
  sauvegarde possible. Test terrain : promotion vérifiée, réouverture et Undo,
  refus du JSON invalide, conservation de l'enregistré puis nouvelle sauvegarde.

## Portée et limites

Complément du même jour : `FileTerrainSession::recover_open` permet désormais
de reprendre un candidat validé sans fichier principal. Identité de zone et hashes
attendus obligatoires, copies de secours conservées. Test fichier réel PASS :
ouverture normale refusée, mauvaise identité refusée sans création de fichier,
récupération puis sauvegarde/réouverture exacte. Clippy projet strict PASS.
Log `E:/OpenRPG_Forge/.gate2-evidence/first-save-recovery-tests.log`.
Ce cas est implémenté au niveau application, pas encore exposé dans l'écran de reprise.

Ce n'est ni une panne électrique ni un kill à tous les points possibles. Le test
d'interruption réelle porte sur l'adapter bytes ; le test d'intégration terrain
construit le staging par fixture. Pas de revendication de parcours UI.
Les archives de récupération restent conservées, sans politique automatique
d'expiration. Le même modèle de concurrence coopérative que R2_FILE_SESSION s'applique.
Le cas première sauvegarde interrompue sans fichier enregistré, la récupération
d'un fichier enregistré corrompu et une archive de secours partiellement écrite
restent à intégrer dans un écran d'ouverture/récupération indépendant.

Prochain travail : parcours Studio créer/ouvrir/retoucher/sauvegarder/rouvrir et
diagnostics de récupération, puis projection/playtest. Qualification workspace
et WASM requise avant verdict de Gate 2 ; aucune case globale cochée.

## Campagne fraîche de qualification locale — 22 septembre 2026

Le harness `scripts/Invoke-Gate2PersistenceFailureProof.ps1`, SHA-256
`5AF97C12A0EBD47867965E448F826E29CD6284A6F0BE05308D730A88BA124B76`,
a exécuté séquentiellement 13/13 tests exacts, chacun observé une fois avec le
statut `ok`. Il couvre notamment l'interruption de processus après sync du
staging, l'invariant ancien-ou-nouveau jamais partiel, les verrous concurrents,
les choix devenus obsolètes, le candidat tronqué/invalide, la première sauvegarde,
les identités étrangères, la conservation des deux versions, la promotion et la
réouverture exacte.

Preuve :
`E:/OpenRPG_Forge/.gate2-evidence/persistence-failure-20260922-v1/`.
Ledger `R2-038-R2-039-PERSISTENCE-FAILURE-PROOF.json`, SHA-256
`EEC0FFD418564CE1275609E6DFF474C01F76362147BDA1B1B91475B47A560E67` ;
rapport Markdown SHA-256
`BE8015B0CD6DC70171E4BB51C6DE90CB4820A6F0BCF5C300C40C6BCC27375F00`.
Branche `Rust`, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, index,
statut et 37 997 entrées du worktree identiques avant/après ; 79 sources
épinglées inchangées. `CARGO_HOME`, target, TEMP et TMP étaient tous sur E: ;
processus possédés disposés et environnement restauré.

Verdict borné : R2-038 est clos programmatique localement. R2-039 est clos
localement par composition avec le replay Studio documenté dans
`R2_STUDIO_RECOVERY.md`, qui fournit l'examen, la confirmation, la promotion,
l'actualisation du catalogue et l'ouverture exacte. Le ledger frais conserve
correctement `gui_recovery_workflow_reviewed=false`, `human_review_performed=false`
et `gate_qualified=false` : ce harness seul n'a pas rejoué l'interface et ne
scelle ni le candidat final ni Gate 2. Pas de preuve de coupure électrique
physique, de processus hostile ni de suppression dans l'intervalle étroit avant
promotion. CI distante : `NOT RUN — infrastructure billing`.
