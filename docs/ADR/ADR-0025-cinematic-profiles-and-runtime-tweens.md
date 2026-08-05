# ADR-0025 — Profils cinématiques canoniques et tweens runtime

## Statut

Accepté le 5 août 2026.

## Décision

Les données de cinématique appartiennent aux agrégats runtime déjà propriétaires du module et de la
zone. Le module possède flux de démarrage, séquences, overrides et tweens ; chaque zone possède ses
Camera Anchors. Ces objets restent purs, versionnés et sérialisables sans dépendance au moteur.

La lecture de timeline et l'interpolation sont des services Application déterministes. Le Toolset et
le runtime les consomment ; l'adapter moteur ne devient jamais la source de vérité. Toute mutation
d'auteur passe par une commande combinée module/zone avec dry-run, diff, inverse et historique.

Les pistes synchronisées et les transforms réplicables exigent une autorité explicite. Le transport
réseau peut avancer ces états mais ne redéfinit ni les données ni les règles de validation.

## Conséquences

- une séquence peut être créée, inspectée, sauvegardée, rejouée et révertie sans scène moteur ;
- les UUID des ancres et cibles sont validés au niveau projet avant sérialisation ;
- l'abandon réutilise la politique de restauration de G11-J ;
- les anciens documents reçoivent des profils vides déterministes ;
- les interfaces avancées de collections peuvent évoluer sans migration du contrat v1.
