# ADR 0013 — Sauvegarde runtime comme delta versionné

## Statut

Accepté le 2 août 2026.

## Contexte

Une partie doit pouvoir reprendre la Crypte sans copier les définitions complètes du module ni
charger silencieusement un état créé pour une autre version du contenu. L'écriture ne doit jamais
laisser un checkpoint partiel.

## Décision

- Le format `.orpsave` 1.1.0 contient uniquement le delta mutable : position et combat du joueur,
  inventaire, variables, état de quête, combatants encore présents, drapeaux de progression et,
  lorsque le profil de zone l'autorise, snapshot météo v1 (état, révisions et temps écoulé).
- Chaque sauvegarde déclare l'UUID du module, l'UUID de la zone et le SHA-256 exact du contenu
  `.orpmod`. Un runtime refuse un module ou un hash différent avant d'appliquer le delta.
- Les références et collections sont validées : UUID, hash, valeurs non négatives, clés et entités
  uniques, paire quête/état cohérente.
- L'écriture utilise un fichier temporaire voisin, flush disque puis remplacement atomique.
- La machine de quête et les combatants exposent une restauration bornée par leurs définitions ; le
  runtime reconstruit d'abord le monde canonique puis applique le delta.
- Le runtime refuse un snapshot météo dont l'état n'existe plus dans le profil canonique de la zone.

## Conséquences

- Les sauvegardes restent petites et ne dupliquent pas les données auteur.
- Une mise à jour de module incompatible est refusée explicitement au lieu de produire un état
  indéfini. Une future migration devra être déclarée par version/hash.
- Le test d'intégration crée un checkpoint dans un processus headless, le recharge dans un second,
  termine le boss puis réécrit l'état final.
- Les sauvegardes `1.0.0` ne sont pas migrées silencieusement : la politique actuelle refuse une
  version différente et une future migration devra être explicite et testée.
