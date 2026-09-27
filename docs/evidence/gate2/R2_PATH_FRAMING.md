# R2-006 — cadrage du chemin accepté

14 septembre 2026, branche Rust, modifications locales non publiées.

Qualification locale acquise sur candidat F1C5DB66 : sélection du chemin dans
l’index, clic du bouton visible et déplacement réel de la caméra constaté.
Chemin à deux points de la fixture retention ; capture path-framed.jpg dans
scene-index-ui-20260914. Archive avant/après identique, aucun Apply/Save.
Voir R2_SCENE_INDEX.md pour hashes et fermeture vérifiée des processus.
Cela supersède l’absence de geste réel ci-dessous, sans qualifier les reliefs
intermédiaires, tous les ratios ou la visibilité sans occlusion.

Implémenté : bouton dans PathControls, désactivé pour un nouveau tracé et par
les gardes existantes busy/playtest. À l’action, recherche de l’identifiant dans
la tête acceptée courante ; sélection absente refusée. Pivot au centre des
bornes des points, altitude échantillonnée sur la surface triangulée du terrain,
distance avec marge de largeur/accotement. Réutilise focus_object, sans écriture,
Save, commande d’édition ou application du brouillon.

Vérifié : cargo clippy -p forge_studio --lib --no-default-features --features web
--target wasm32-unknown-unknown --offline --locked -j 2 -- -D warnings : PASS,
7,22 s. Avertissement futur tiers proc-macro-error2 2.0.1 inchangé.

Non vérifié : geste réel du bouton, relief intermédiaire, occlusions et ratios
de viewport. Cadrage approximatif des points, pas calcul de visibilité complète.
Aucun candidat graphique lancé. R2-006 et Gate 2 restent ouverts.
