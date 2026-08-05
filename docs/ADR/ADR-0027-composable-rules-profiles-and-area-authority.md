# ADR-0027 — Profils de règles composables et autorité de zone

## Statut

Accepté le 5 août 2026.

## Décision

La progression et la difficulté appartiennent à un `ModuleRulesProfile` canonique. La perception
environnementale et les règles de combat appartiennent à un `AreaRulesProfile` canonique. Ces deux
agrégats sont imbriqués dans les politiques runtime déjà versionnées et restent indépendants de la
scène moteur.

Une progression est définie par un ruleset, une courbe, des multiplicateurs de source, de groupe et
de difficulté. Une difficulté est un profil composable avec des mutateurs scalaires déclarés ; elle
ne contient aucune règle D&D implicite. La perception est résolue par sens et condition de milieu.
Le PvP, le tir allié, le crime, l’aggro et les dégâts environnementaux sont décidés par la politique
de zone sous autorité serveur.

Toute mutation auteur passe par une commande atomique avec dry-run, diff, inverse et historique.
Le Toolset et MCP emploient cette commande. ForgeScript ne reçoit que des handles typés et des
instantanés copiés en lecture seule.

## Conséquences

- les anciens modules et zones reçoivent des UUID déterministes et un profil générique complet ;
- un autre ruleset peut remplacer courbes et multiplicateurs sans modifier le runtime ;
- les mutateurs se composent de manière déterministe et ne peuvent écraser que des scalaires connus ;
- la perception du RPG Kit consomme les mêmes modificateurs que l’aperçu et le runtime ;
- aucune décision PvP cliente ne peut contourner `AreaCombatAuthority` ;
- la sauvegarde conserve la progression du joueur, pas une copie divergente des règles auteur.
