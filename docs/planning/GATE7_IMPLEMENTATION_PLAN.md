# Gate 7 — Monde étendu et distribution

Gate ouverte le 23 octobre 2026 avec un objectif de clôture documentée.
Au 7 novembre, R7-01 à R7-06 disposent de verdicts locaux Windows datés.
Le candidat privé 049 couvre les volets Windows de R7-07, R7-09 et R7-10.
Leurs volets Linux et R7-08 restent ouverts. La [passation](../HANDOFF.md#gate-7-etat-courant)
récapitule les reçus et la [matrice d'acceptation](../evidence/gate7/GATE7_PROGRESS_20261023.md#matrice-dacceptation)
donne le verdict par critère.

<a id="transition-editeur-launcher"></a>

## Transition éditeur et launcher du jeu

Le Studio crée et retouche le projet, puis exporte le jeu avec son propre
launcher. L'installation, le lancement et la reprise du jeu sont autonomes.
Les capacités réutilisables de Tidewater appartiennent au moteur commun.
Chaque module fournit son identité, ses données et ses réglages.

| Lot | Objet | État de qualification |
| --- | --- | --- |
| G7-D1 | Inventorier les fonctions du Hub, du Studio et du launcher | Inventaire réalisé le 2 novembre |
| G7-D2 | Porter l'identité, la configuration du launcher et les contenus dans le projet | Deux jeux distincts exportés et repris avec le 049 Windows |
| G7-D3 | Raccorder les interfaces auteur et joueur aux services communs | Parcours auteur et joueur observés dans des invités Windows séparés |
| G7-D4 | Exporter un jeu installable et autonome | Deux exports privés 049 installés, joués et repris dans l'invité joueur |
| G7-D5 | Migrer les installations et retirer le Hub | Retrait dans le checkout de qualification 048 et cycles de maintenance Windows vérifiés avec le 049 |
| G7-D6 | Qualifier le candidat sur Windows et Linux | Volets Windows privés acquis pour le 049. Linux, redistribution et mesures restantes ci-dessous |

## Conditions de clôture restantes

- Qualifier le build Linux des sources courantes, puis le parcours auteur
  jusqu'à l'export, le lancement et la reprise via le launcher, ainsi que
  la mise à jour, la restauration et la désinstallation sur un OS propre.
  Les essais partiels 0837 en conteneur sans SDK restent rattachés à leur kit.
- Résoudre l'attribution des dépendances distribuées, les droits des
  assets et les conditions de redistribution des composants. Les crédits
  Tidewater ont été vérifiés via le service d'export natif.
- Vérifier le budget moyen de 16,67 ms et effectuer une comparaison
  appariée avec la référence. La dernière mesure Tidewater en mouvement
  au profil Moyen donne 18,85 ms en moyenne et 31,37 ms au p95, sur une
  exécution par variante.
