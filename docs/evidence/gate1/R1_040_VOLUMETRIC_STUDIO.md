# R1-040 — Prévisualisation volumétrique Studio / WebGPU

2026-09-06, Rust, non publié. Gate 1 ouverte ; CI distante NOT RUN.

## Implémenté

Bouton explicite Tester/Désactiver la brume expérimentale dans les contrôles qualité existants.
Préférence éphémère, désactivée par défaut. PreUpdate transmet la demande au même
`VolumetricPreviewRequest` du moteur. Aucun document, fichier ou historique de commandes modifié.
Le bouton indique la demande ; en Low, la politique garde l'effet désactivé même si demandé.
L'observation moteur est exposée dans `studio-volumetric-ledger`, indépendamment du bouton.
Le coordinateur global continue d'annoncer eau/environnement partiels.

## Vérifié réellement

Clippy Studio ciblant wasm32, sans features par défaut, feature web : PASS. Build Trunk : code 0.
Avertissement amont conservé : incompatibilité Rust future de `proc-macro-error2 2.0.1`.
Contrôle de format Studio et diff : PASS.

Build local `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-volumetric-20260906/dist` servi
sur loopback, page `?forge_scene=world-sketch-visual-valley` dans le navigateur intégré.
Interactions réelles :

1. High initial, effet non demandé.
2. Clic Tester : applied=true, 64 étapes, maximum effectif 128 m.
3. Clic Low : applied=false, fallback distance_fog, aucune distance effective.
4. Clic High : applied=true, retour 64 étapes/128 m.
5. Clic Désactiver : applied=false, fallback rétabli, High conservé.

Deux captures navigateur inspectées (High actif, Low), consultables dans la conversation,
sans fichier PNG exporté. Pas de frontière dure visible dans cette vue. Deux lectures des
logs warn/error retournent une liste vide. Monde initialisé une seule fois avant/après.
État final : cinq domaines CPU projetés, deux partiels, full_preset_applied=false.
Le manifeste de parité est un relevé au démarrage, pas un rehash continu du document.

SHA-256 WASM : `10f2df1296177bd5c51f1f036e73ffba76c956c2a3b40e0ae943e1fb68ef5619`.
SHA-256 quality_preview.rs : `9d4db3e2e0f38f9c12d57461a4fcf4cc87d4b5c49a89fa4a0ceb23bd1a1a0d20`.

## Limites et suite

Viewport 768×606 dans une fenêtre 1280×720, **pas Full HD**. Aucun kit externe dans le Studio.
Overlay technique volumineux et capsule de debug visibles. Le diagnostic de présentation GPU
reste non_observed : les captures réelles constituent une preuve distincte, pas une modification
du verdict de ce diagnostic. Aucun benchmark, acceptation artistique ou validation Tauri déduit.
Pas de qualification caméra mobile ni Medium/Ultra Web dans ce lot. Le brouillard ambiant authoré
reste non projeté et le cumul avec le fallback reste expérimental. Aucun preset complet accepté.

Onglet de preuve fermé et serveur loopback PID 29312 arrêté après vérification de sa commande.
