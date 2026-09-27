# R1-040 — Commandes de qualité dans le Studio WebGPU

2026-09-06, Rust, aucune publication ; Gate 1/R1-040 ouverts.

## Implémenté

Dans les deux modes vallée, le Studio installe les adapters de qualité et le coordinateur global
sur le monde existant, niveau initial High. Le mode Gate 0 reste inchangé. Le profil embarqué est
lié en mémoire à la révision du terrain chargé ; toutes les requêtes sont validées avant installation.
Échec de configuration : événement d'échec runtime et arrêt du démarrage, pas de fallback silencieux.

Quatre boutons Low/Medium/High/Ultra, désactivés avant le premier relevé moteur. Canal transitoire
thread-local analogue à la météo, consommé en PreUpdate. Aucun fichier ou commande de sauvegarde,
pas d'undo nécessaire pour une préférence de rendu non persistante. Pas de redémarrage de Bevy.
Le statut et `studio-quality-ledger` affichent la demande et les observations CPU, avec mention
explicite des domaines eau/environnement partiels. Mise à jour Leptos seulement si le relevé change.
Ce statut n'est pas une preuve de présentation GPU ou de preset complet.

## Vérification réelle dans le navigateur intégré Codex

Build Trunk neuf : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-quality-20260906/dist`.
URL de test : `http://127.0.0.1:14625/?forge_scene=world-sketch-visual-valley`.
WASM SHA-256 : `9411ccafba84c6b869082dfb67d54b8cd6aa96042bf6d50a46bb641a689b650e`.
Module UI : `7ce826d5341a63aec7666615a98cd3905a2563d46083401f7cf27175d4381565`.

Clics réels High initial → Low → Medium → Ultra → High. À chaque niveau, lecture DOM :
requested_tier attendu, requests_dispatched=true, available_budgets_projected=true,
domaines `[cpu_projected,cpu_projected,cpu_projected,partial,partial,cpu_projected,cpu_projected]`,
full_preset_applied=false. Les captures Low et Ultra sont inspectées dans la conversation :
contrôles lisibles en deux colonnes, végétation et ombres changent dans la vue joueur.
Les PNG de navigateur ne sont pas exportés dans le dépôt.

Après le cycle, le ledger indique toujours un démarrage du viewport et le manifeste de sources
aligné. Le manifeste de parité est un relevé de démarrage, pas un rehash continu des sources.
Le chemin de commande n'écrit aucun document et les tests moteur antérieurs couvrent leur conservation.
Le contrôle météo existant fonctionne ensuite : pluie High 8192 gouttes, humidité 0,78, puis clair
avec humidité 0 et clear_target_restored_exactly=true ; qualité High toujours observée.
Trois lectures de logs retournent zéro warn/error. Onglet et serveur de preuve fermés, autres onglets préservés.

Rendu réel 768×606 dans une fenêtre 1280×720 : ce n'est pas une qualification Full HD.
Capsule joueur de diagnostic, overlay volumineux et aspect procédural conservés : pas de validation
artistique humaine, ni de preuve de déplacement. Le ledger de présentation GPU reste not_observed ;
les captures constituent une preuve visuelle distincte. Aucun gain de performance revendiqué.
Pas de nouvelle qualification Tauri/WebView2, de kit Blender externe dans Studio ou d'adaptation automatique.

## Contrôles

Check et Clippy du frontend WASM `--no-default-features --features web` : PASS.
Trunk build dev : PASS. Avertissement amont futur `proc-macro-error2 2.0.1` conservé.
Clippy workspace tous targets/features : PASS. CI distante NOT RUN.
**381 tests workspace PASS**. Formatage et diff PASS. Aucune publication Git.
