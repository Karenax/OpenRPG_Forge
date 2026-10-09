# Tidewater — menus Échap livrés le 8 octobre 2026

Le correctif 006 ajoute au jeu les indicateurs FPS, temps d'image en millisecondes et heure du jour, activables séparément. Le curseur 0–24 h et les boutons Matin/Midi/Soir modifient effectivement l'éclairage et les ombres. Les préférences sont persistées dans `display.json` ; le profil graphique et le lancement en plein écran sont conservés. Le panneau existant de vingt réglages reste accessible depuis le menu, avec retour et reprise.

Le launcher affiche une confirmation sur Échap : Annuler/Quitter ; un second Échap annule. Quitter utilise la commande native existante.

Package local : `E:/OpenRPG_Forge/Tidewater-Launcher-Fullscreen-20261008`.

- Runtime SHA-256 : `a1c6cdd98d6755030f3d9a960f831ed9d336732d72f6e0d4645e7bada44df2af`.
- Launcher SHA-256 : `5c8e7c919893d28f4e40a0ca536b74316d32848d73b88b31653962e33b372e5b`.
- Rapport et reçus locaux : `E:/OpenRPG_Forge/.tidewater-integration/launcher-repair-20261008-006/REPORT.md` et `deployment.json`.

Build isolé et sources exactes installées vérifiés. Douze contrôles natifs des menus passent dans deux profils isolés, quatre captures sont inspectées. Deux tests du tracker d'ombres et dix tests launcher passent. Clippy strict du launcher passe ; aucun nouveau diagnostic sur les fichiers de menus ou le tracker, tandis que les diagnostics préexistants des autres fichiers sont conservés. Les diagnostics WebView vérifient le modal, ses annulations et l'événement réel de fermeture native. Aucun pilotage clavier/souris Windows n'est utilisé.

Les textures d'atmosphère restent résidentes ; l'environnement diffus est modulé depuis les SH acquis et la cubemap de réflexion reste statique. Les mesures FPS/ms sont des durées d'image, pas une latence GPU ou entrée-écran. Le test ne remplace pas une qualification de performance ou une partie RPG complète. Le [verdict humain Gate 3](GATE3_OWNER_HUMAN_VALIDATION_20261008.md) est enregistré séparément pour la version précédemment essayée ; aucune clôture de la Gate complète n'est déduite.
