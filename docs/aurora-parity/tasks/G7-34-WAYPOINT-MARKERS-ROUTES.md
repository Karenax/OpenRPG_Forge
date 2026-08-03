# G7-34 — Marqueurs et routes de waypoint

Le waypoint canonique porte maintenant sa position et son apparence d'éditeur indépendante du
moteur : icône, couleur, couche, visibilité et taille écran. Un marqueur de carte optionnel ajoute
texte, icône, étage, catégorie, précision, suivi et politique de visibilité.

La révélation est filtrée par portée joueur, groupe ou monde et par événement ; l'état révélé est
sérialisable. Les routes de patrouille ordonnent des points avec attente, orientation, vitesse,
action, condition de branche et poids. Le parcours déterministe couvre boucle, aller-retour, unique,
aléatoire, pondéré et scripté.

Preuves : `WaypointRouteServiceTests`, build et suite globale.

La prochaine tranche unique est **G7-35 — orientation, ancres, groupes et visualisation de routes**.
