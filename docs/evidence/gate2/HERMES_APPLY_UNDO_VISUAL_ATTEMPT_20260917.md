# Essai Hermes — Apply/Undo CPU et tentative visuelle Tauri

## Verdict

**CPU : 9 contrôles réussis lors de l'essai précédent. Clic UI par token confirmé ensuite, mais parcours visuel NON EXÉCUTÉ ; essais arrêtés, relais Codex. Dernière commande Rust : zéro test sélectionné, NON PROBANTE.** Aucun passage de gate, aucune qualification d'assistant IA intégré. Pas de code produit modifié.

Le harnais CPU extérieur appelle AuthoringHost et les préparateurs réels de forge_project : aperçu sans mutation, déplacement isolé, répétition idempotente, nouvelle commande périmée refusée, cible verrouillée/inconnue et valeur non finie refusées, Undo, sauvegarde puis réouverture par un nouveau host. Assets synthétiques uniquement ; pas de validation Monde injectée. Rapport et journal : `E:/OpenRPG_Forge/hermes-proof-20260916/RESULTATS.md` et `run-02.log`. La suite `cargo test -p forge_project --offline` a aussi terminé avec code 0 et un test opt-in ignoré. Ces résultats ne couvrent ni rendu ni consentement UI ni fournisseur IA.

## Tentative réelle du 17 septembre

- Binaire existant : `E:/OpenRPG_Forge/.gate2-evidence/reconciliation-recipes-studio-20260915/forge-studio-candidate.exe` ; SHA-256 vérifié `1303a840fe90baa2be4aea36d0635ad75ae48592e43d8c9adbb53cf79e9b4d89`.
- Une seule instance lancée à 18:35 heure locale ; PID 23356, HWND 724304. Aucun build effectué.
- Copie isolée et profil WebView jetable sous `E:/OpenRPG_Forge/hermes-proof-20260916/visual-20260917-183502/` ; FORGE_AUTHORING_ROOT pointe vers son sous-dossier workspace.
- Source : archive de preuve `r2020-final-ui-20260916/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`, jamais éditée.
- Vraie capture Studio inspectée : coque Gate 2 visible, « Aucune zone ouverte ». Aucune preuve de chargement 3D.
- Clic par index refusé par le pilote : `snapshot_id_required`, car le contrat actif réclame un token ou snapshot_id absent de l'appel disponible.
- Clic pixel en arrière-plan annoncé livré mais aucune évolution visible après capture. L'escalade premier plan échoue : HWND cible non au premier plan. Aucun clic Apply, aucune modification de carte, aucune sauvegarde revendiquée.
- Fermeture normale demandée via CloseMainWindow ; contrôle Windows ultérieur : absence du PID 23356, de son enfant WebView 10308 et des enfants de ces deux PID. Aucun processus utilisateur arrêté.
- Logs stdout/stderr vides ; cela ne prouve pas une absence de défaut produit.
- Après fermeture, source et copie ont le même SHA-256 initial : `3f539ad8f48b039983a8b5b464c0be081a9c716f35c2f09abb0b5ca6addd8789`.

Artefacts locaux : launch.json, integrity.json, studio-no-zone.png, stdout.log et stderr.log dans le dossier isolé ci-dessus. Le refus du pilote ne suffit pas à diagnostiquer un bug Forge. Codex Windows était présent au contrôle CIM : l'ancienne recherche Git Bash ne démontrait pas son absence.

## Nouvel essai demandé par le propriétaire — 18:41

Nouvelle copie isolée `E:/OpenRPG_Forge/hermes-proof-20260916/visual-20260917-184116/`, même binaire et même archive vérifiés, PID 17824. Capture réelle obtenue : aucune zone ouverte. Malgré `bring_to_front=true`, le clic premier plan échoue à nouveau : HWND cible `0x181304`, HWND actif `0x1057e` (Hermes). Ce résultat ne permet pas d'attribuer le blocage à l'utilisation simultanée du PC par le propriétaire ; le problème de ciblage/pilotage reste à diagnostiquer. Aucun parcours Apply/Undo visuel exécuté. Fermeture normale demandée, absence du PID et des WebViews liées au profil isolé vérifiée. Source et copie inchangées par SHA-256. Aucun commit effectué.

## Diagnostic ultérieur du ciblage — clic confirmé, parcours non exécuté

Sur l'instance isolée PID 4100, le pilote Cua a accepté un token frais obtenu par `get_window_state`, avec le même PID/HWND et la même session nommée. Le clic sur « Projets et modules » a ouvert le panneau : nouvelle capture avec les contrôles de workspace visibles (100 éléments). Cela confirme cette action UI uniquement. Les anciennes indications de coordonnées absolues étaient incompatibles avec les pixels relatifs à la fenêtre attendus par le pilote ; un index sans snapshot était également insuffisant. Ne pas attribuer ces erreurs à une interférence du propriétaire ou à un défaut Forge.

Instance PID 4100 fermée normalement ; contrôle de terminaison effectué et archives source/copie identiques par SHA-256. Capture après ouverture du panneau : `C:/Users/karen/AppData/Local/hermes/profiles/openrpg-forge/cache/images/computer_use_0e08c2f6682d4223890f240858c5e3c3.png`.

## Dernier essai et arrêt — relais Codex

- Script expérimental hors dépôt : `E:/OpenRPG_Forge/hermes-proof-20260916/pilot.py`. Deux erreurs de préparation (`BASE` absent et argument `args` obligatoire) ont été corrigées. Ce script n'est pas un harnais qualifié : ne pas le considérer comme preuve d'ouverture de zone ou de réussite Apply/Undo.
- Dernier lancement isolé : `visual-20260917-192243`, PID 18244, même candidat et archive vérifiés au lancement. La capture initiale ne montrait que la fenêtre en démarrage. Aucun parcours d'édition exécuté. Fermeture normale et terminaison du PID 18244 confirmées.
- Commande ensuite exécutée : `cargo test -p forge_studio reconciliation --offline`. Compilation terminée en 2 min 54 s, code de sortie 0, mais **0 test exécuté** : 76 tests frontend et 4 tests du binaire filtrés. Ce contrôle est **NON PROBANT et exclu des preuves fonctionnelles**. Il ne démontre ni succès ni échec de réconciliation. Il a produit du cache de compilation, sans correction de code produit.
- Le propriétaire demande l'arrêt et reprend avec Codex. Aucun nouveau test, lancement GUI, commit, push ou nettoyage du travail préexistant n'est entrepris.
- Les neuf contrôles CPU précédents restent valides dans leur périmètre. La boucle visuelle aperçu → déplacement → Apply → Undo → sauvegarde → réouverture reste **NON EXÉCUTÉE**. Aucun passage de gate ni assistant IA intégré validé.

## Relais

Reprendre depuis `PROJECT_CONTEXT.md` et les preuves Gate 2 existantes. Si le parcours visuel est repris, utiliser une copie isolée, des cibles UI fraîches et vérifier chaque état ; ne pas substituer une compilation ou un filtre sélectionnant zéro test à la preuve demandée. Aucun bug produit de réconciliation n'a été établi par ces essais.
