# Historique des chemins, rivières et bâtiments — 14 septembre 2026

Défaut constaté : les reçus contenaient les corrections complètes, mais le résumé
de l'historique omettait ces trois domaines. Une opération pouvait donc apparaître
sans description de l'objet affecté. Les données d'Undo n'étaient pas perdues.

Implémenté : comparaison des objets par ID stable, résumé nom/verrou/hash avant et
après, seulement pour les objets changés. Présence explicite de la couche séparée
de son contenu : absent → vide est visible. Le hash porte sur l'objet complet,
donc une modification géométrique à effectif constant est détectée. Aucune liste
de points, aucun mesh ou échantillon massif dupliqué dans le résumé. Les archives
et leurs versions ne changent pas : la vue est dérivée des reçus vérifiés.

Studio : libellés ajout/retrait/modification et état des verrous, huit objets au
maximum dans le texte principal ; résumé complet accessible dans les détails
existants. L'historique n'est pas une nouvelle commande d'édition ni une seconde
source canonique. Qualification graphique de ces libellés encore due.

Trois tests `zone_history` PASS (15,19 s compilation, 0,02 s tests), dont les
couches vides des trois domaines, chemin modifié à ID/effectif constant,
retrait/Undo/relecture, absence de mutation par lecture. Les deux tests historiques
couvrent toujours densité/verrou/recette, pagination et refus d'identités périmées.
Clippy projet ciblé PASS après passage des listes en slices optionnelles (3,59 s).
Clippy Studio WASM/web final PASS (31,07 s ; avertissement amont
proc-macro-error2 inchangé). Fmt et diff check PASS. Pas de test graphique ni
nouveau candidat lancé.

Les parcours d'édition réels des bâtiments et rivières ne sont pas requalifiés par
ce test de chemin ; ce lot ne clôture pas Gate 2. Aucune publication Git.
