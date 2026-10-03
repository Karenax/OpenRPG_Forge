# Catalogue des plantes importées dans Studio — 28 septembre 2026

Le panneau Nature énumère désormais toutes les couches végétales importées
chargées pour la zone, avec leur nom d'auteur et le nombre d'instances source.
L'auteur choisit une espèce, une position mondiale X/Z, un dégagement vertical,
une rotation Y et une échelle, puis passe par Preview/Apply/Undo/Redo et Save/Open.
Il peut aussi armer « Placer par clic sur le terrain » : le rayon du curseur
terrain existant fournit X/Z, qui sont reportés dans les champs avant Preview.
Le clic est refusé si la révision de carte a changé ; modifier un réglage annule
le geste armé et l'aperçu antérieur. Le bouton refuse aussi d'armer un geste
tant qu'un aperçu ou une commande est en cours.
Le catalogue est dérivé des blocs immuables de la zone. Pour chaque pose, l'instance
source la plus proche fournit la variation de cette espèce ; sa clé stable reste
dans la correction. Les sources et le paquet d'assets ne sont pas modifiés.

Validation locale :

- `imported_population` : 6/6 PASS. La suite couvre le catalogue, la sélection
  déterministe de l'instance source la plus proche, la position mondiale,
  le rejet hors carte, puis Preview/Apply/archive/Undo via une pose issue du
  catalogue.
- `tidewater_population_scene_probe` sur le workspace et le paquet végétal réels :
  PASS. Chaque couche source apparaît dans le catalogue, le total des entrées
  couvre 65 383 plantes et une pose par coordonnées produit 65 384 identités.
  Le chargement de scène, la sélection près de la copie, le picking et les
  parcours caméra A/B/A restent PASS ; la source du workspace reste intacte.
- `cargo check` et Clippy Studio WASM : PASS ; Clippy monde/probe : PASS.
- `scripts/Invoke-StudioTrunk.ps1 -Action build` : PASS sur le panneau final
  incluant la commande de clic terrain. `forge_studio_bg.wasm` : 49 065 677
  octets, SHA-256
  `02f6868dd06b47dea4c0226f634e7df091e50e46c36e023eeb87204735b5a80c`.

Le résultat vérifie le chemin auteur et la projection native. Le clic de pose
est raccordé et compilé, mais son geste dans une fenêtre Studio n'a pas été
observé. La correspondance visuelle finale avec Tidewater et la bibliothèque
générique des assets du projet restent des qualifications séparées.
