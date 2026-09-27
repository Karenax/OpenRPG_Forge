# R1 — Commandes d'adaptation du Studio

Complément courant : le parcours sans forêt expérimentale atteint désormais
Low/Ultra acquittés et tracking ; la baisse automatique reste non qualifiée.
Un garde-fou de fenêtre masquée et un diagnostic de l'échantillonnage sont ajoutés
ensuite. Voir [visibilité adaptative](R1_ADAPTIVE_WINDOW_VISIBILITY.md) ; les
blocages optiques décrits ci-dessous sont ceux du premier essai historique.

7 septembre 2026, branche Rust, worktree existant préservé. Gate 1 ouverte.

## Implémentation

Le panneau Qualité, section « Profil effectif et limites », expose deux actions sans
sauvegarde : tester/relancer l'adaptation et revenir au mode fixe sur le profil demandé.
Elles appellent `GlobalQualityRequest::enable_adaptation` et `request`, comme le runtime ;
aucun contrôleur métier parallèle dans le frontend. Low/Medium/High/Ultra arrêtent aussi
l'adaptation par la commande manuelle existante. Une seule requête est consommée en
PreUpdate ; la dernière action remplace une action encore en attente. Boutons indisponibles
avant observation moteur.

Le retour fixe conserve le niveau **demandé**, pas nécessairement acquitté par le GPU.
Aucun rollback, profil complet ou réussite automatique annoncé au clic. L'état affiché
vient des observations moteur. Mode désactivé par défaut, réactivation explicite après
arrêt, avertissement expérimental et transitions perceptibles conservés.

## Vérifications exécutées

- `cargo test -p forge_studio` : 39 tests bibliothèque + 3 tests shell PASS. Contrats
  existants et diagnostics, pas des tests de clics.
- Clippy Studio cible wasm32-unknown-unknown, no-default-features, feature web,
  `-D warnings` : PASS. Fmt PASS. Warning futur proc-macro-error2 v2.0.1 conservé.
- `trunk build --config apps/forge_studio/Trunk.toml --dist
  E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-adaptation-20260907/web` : exit 0.
  Cache Cargo terrain-water-20260906, incrémental désactivé, RUSTFLAGS=-C debuginfo=0.
- Bundle `forge_studio_bg.wasm` SHA-256 :
  `CBDC437C0D3A92520EC4FF977A2520F65DD56408083F517668212CBB484CA3CA`.

Navigateur intégré réel, port loopback 1431, URL `/?forge_scene=world-sketch-visual-valley`,
sans kit externe. Viewport observé 448 × 798 : smoke de commande, pas qualification FHD.
Un seul monde, six hashes alignés, caméra 1/1, clair. Panneau ouvert et captures UI
inspectées dans la conversation ; aucun hash PNG local revendiqué.

| Action UI exécutée | Observation moteur/DOM |
|---|---|
| Chargement sans action | High, adaptation `disabled`, 0 proposition |
| Tester l'adaptation | `waiting_application`, 0 échantillon admissible, 0 proposition, aucun acquittement |
| Revenir au mode fixe | High conservé, `disabled` |
| Réactiver puis bouton High | Attente observée à nouveau, puis `disabled` et High conservé |

Le ledger DOM `studio-quality-ledger`, lu via le navigateur, confirme `application:null`
et les quatre blockers : `environment_reflection`, `screen_reflection`, `surface_tint`,
`screen_refraction`. L'interface ne contourne pas ces obstacles. Aucun changement
automatique de profil ni adaptation Web effective qualifié. Console d'erreurs interrogée :
liste vide. La sélection du dépliant par rôle Playwright a échoué (summary HTML), puis
le clic par nœud d'accessibilité observé a réussi ; aucune erreur produit déduite.

Les clics d'export JSON ont affiché « téléchargement demandé », mais aucun nouveau fichier
n'a été retrouvé dans Downloads : ils ne sont pas comptés comme preuve locale exportée.
Les observations ci-dessus sont celles de l'arbre accessible et du ledger DOM inspectés.
Onglet de preuve fermé ; autres onglets préservés.

## Limites et suite

Le raccord de commande est vérifié, pas la boucle d'adaptation complète. La prochaine
dépendance est la qualification des capacités d'eau Web puis du plan/GPU, pas un nouvel
ajout de bouton ni une modification des seuils. Retour manuel sur un autre niveau et
transitions automatiques sous charge réelle restent à exercer lors de cette qualification.
Les 561 tests workspace du lot précédent restent une preuve moteur historique, pas un
nouveau total exécuté ici. Aucun code moteur/domaine ni document de carte modifié.
CI distante NOT RUN ; Tauri/WebView2, performance cible et acceptations humaines ouvertes.

## Protection du mode de preuve fixe

Défaut découvert après le raccord UI : l'interdiction moteur de l'adaptation reconnaissait
les chemins de capture/métriques natives, mais pas `FixedProofViewStatus`, utilisé par le
Studio. Un mode figé pouvait donc demander l'adaptation alors que son profil doit rester
stable pendant la comparaison. Les blockers Web actuels masquaient ce risque, sans le
résoudre pour une future qualification.

Correctif partagé sous feature qualification : présence du statut de vue fixe => arrêt
explicite `fixed proof/metrics/capture run forbids adaptation`. Valable avant et après
installation de la caméra. Aucun changement de tier, de source ou de seuil ; commandes
manuelles conservées. Contrôle dans le moteur, pas une protection uniquement visuelle.

Test ajouté `fixed_proof_view_refuses_adaptation_before_and_after_camera_installation` :
RED exécuté avant correction (erreur ne signalant pas le mode fixe), puis contrôle de
l'arrêt, zéro proposition/acquittement, High conservé et retour manuel à `disabled`.
Contrôle terminé : **562 tests workspace PASS / 1 timing préexistant ignoré**,
Clippy workspace all-targets/all-features strict PASS, check Studio WASM/Web PASS,
fmt/diff PASS ; `Validate.ps1` exit 0. Warning futur proc-macro-error2 conservé.
Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fixed-view-adaptation-20260907`,
commandes/env dans `Validate.ps1`. Il s'agit d'un garde-fou ECS ; pas d'effet optique ni
de rendu Web actuel déduit. Le bundle laissé ouvert pour le fichier utilisateur précède
ce correctif et ne doit pas lui être attribué.

Logs SHA-256 :

- workspace.log : `67231A8A73D18D82FFC9CE4A0A8092668F1AB188F4FEEE2C4813B495F3AC02E7`
- clippy.log : `26D1AE1F9CE146C0CACD74DEA319BA2A8F2C3246FE8CF202AE233141A7A51484`
- wasm.log : `CE7579C565E69B397DFB50FCFC712E078BDC1DA8ED8B5F80A92DB131A2265E26`
