# Navigation de caméra auteur — 12 septembre 2026

Statut : implémentée, compilation vérifiée ; interaction graphique non qualifiée.

## Fonction ajoutée

Dans l'espace auteur, bouton droit maintenu pour tourner autour du pivot,
bouton central pour déplacer le pivot ; boutons Rapprocher/Éloigner et
Vue d'ensemble. Les deltas sont bornés, la distance reste entre 4 et 1 000 m,
le pivot X/Z reste dans les limites du terrain courant (512 m).
Le premier transform de caméra est conservé pour le retour à la vue d'ensemble.

L'état de navigation est transitoire. Aucune commande de mutation terrain,
révision, archive ou entité sérialisée n'est produite. La caméra joueur est
exclue de la requête et la présence d'une simulation désactive la navigation.
Un geste de navigation annule la brosse armée. Échap, sortie et perte de focus
du canvas vident les entrées de navigation et de brosse.

## Validation et limites

Clippy strict Studio WASM PASS en 3,53 s après correction d'un import With ambigu
entre Bevy et Leptos. Avertissement futur upstream proc-macro-error2 inchangé.
Fichiers modifiés formatés. Pas de changement de contrat persistant ; pas de
rejeu de la suite domaine inchangée pour ce lot de présentation.

Pas encore de build bundle ni de capture de cette version. Sens des gestes,
focus, lisibilité du panneau et coexistence avec le tracé seront vérifiés dans
un replay graphique regroupé. La caméra n'effectue pas de collision avec le
relief et son pivot vertical ne suit pas automatiquement chaque retouche.
Pas de molette ni de recentrage sur sélection dans ce lot.

## Peinture de matériaux

Inspection : TerrainDocument v2 contient les couches et deltas de hauteur,
mais aucun masque spatial authoré de matériaux. Les poids du générateur sont
dérivés. R2-012 reste absent : une coloration temporaire du mesh ne satisferait
ni sauvegarde, ni Undo, ni conservation lors des régénérations. Le schéma et sa
migration doivent être cadrés avec ADR-0014 (encore proposé) avant ce chantier.
La caméra est un travail Gate 2 indépendant, pas une substitution au critère.
