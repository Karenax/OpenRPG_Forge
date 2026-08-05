# ADR-0026 — Profil de contrôle canonique et fournisseurs de combat

## Statut

Accepté le 5 août 2026.

## Décision

Les capacités de déplacement avancé, la roue d’actions, le contrôle du temps et le choix du mode de
combat appartiennent à un profil canonique du module, imbriqué dans ses politiques runtime. Le
runtime moteur ne possède qu’une projection consommable de ce document.

Les modes temps réel, temps réel avec pause, tour par tour et personnalisés sont des fournisseurs.
Ils doivent publier le même ensemble d’événements de combat et déclarer leur autorité. Le contrôle
du temps et les files de planification restent des services Application déterministes ; le transport
réseau fournit acteurs et votes sans réécrire leurs règles.

Toute modification auteur passe par une commande unique avec dry-run, diff, inverse et historique.
Le Toolset, MCP et les migrations construisent exactement le même `ModuleGameplayControlProfile`.

## Conséquences

- un module ancien reçoit des UUID de profils déterministes dérivés de son UUID ;
- l’absence optionnelle d’une capacité est explicite, et une capacité marquée obligatoire ne peut
  pas être désactivée silencieusement ;
- la roue peut changer de représentation sans changer les actions canoniques ;
- les fournisseurs de règles personnalisés restent possibles sans fragmenter les événements ;
- les adapters d’eau, d’escalade et d’input ne deviennent jamais une voie de mutation parallèle.
