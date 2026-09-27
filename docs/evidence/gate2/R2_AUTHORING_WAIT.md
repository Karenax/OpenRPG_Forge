# R2-052 — attente des commandes, instrumentation partielle

15 septembre : la boucle principale AuthoringControls mesure avec performance.now
le temps de l’appel Tauri jusqu’à sa résolution, succès ou erreur. Seule la
dernière durée reste dans le formulaire ; pas de journal croissant ni réseau
supplémentaire. Horloge absente : indisponible, jamais zéro inventé.

Inclus : transport, traitement natif, décodage de réponse avant reprise await.
Exclus : préparation locale de la commande, attente avant spawn, mise à jour
Leptos, projections moteur/GPU et gestes humains. Les autres canaux (workspace,
catalogue, historique) ne sont pas instrumentés par ce wrapper.

Fmt effectué ; Clippy Web/WASM strict PASS (6,99 s) après extraction du wrapper
asynchrone pour respecter la limite de taille des fonctions. Aucun warning
supprimé. Pas de nouvelle mesure réelle ou de qualification graphique ; le
candidat 284B775A est antérieur à ce lot. R2-052 reste ouvert : il exige les
mesures d’attente et d’effort du parcours complet sur matériel identifié.

## Relevé du parcours final — 23 septembre 2026

Cinq copies indépendantes v17–v21 du parcours vallée, forêt, chemin, rivière,
maison visible, clairière, régénération, Save/Close/Open et Jouer/Retour passent
les neuf étapes automatisées. AMD Ryzen 5 7600, 64 Gio, RX 7800 XT ; médiane
39,104 s et p95 empirique 39,968 s pour les neuf étapes, temporisations de
captures comprises. Sur 105 appels `apply_zone`, médiane du fetch WebView
79,2 ms et p95 172,9 ms. Détail, autres appels et limites dans
`GATE2_CLOSURE_WORK_20260923.md`. Aucun geste/temps actif d'auteur humain ni
latence image-écran mesuré ; R2-052 reste ouvert.
