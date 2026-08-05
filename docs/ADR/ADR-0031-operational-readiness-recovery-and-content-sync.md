# ADR-0031 — Résilience opérationnelle, récupération et synchronisation

Date : 5 août 2026
Statut : accepté

## Décision

Les tâches agentiques, la collecte de crash, la santé projet, la récupération Creator et la
synchronisation de contenu sont gouvernées par `OperationalReadinessProfile`, sous le profil
d’extensions runtime du module. Les UUID et schémas sont persistants ; Creator modifie ce profil
avec la commande transactionnelle existante, tandis que runtime, CLI et MCP le lisent sans créer
une seconde source de vérité.

Un manifeste de tâche borne explicitement objectif, chemins autorisés et interdits, tests
d’acceptation, propriétaire, dépendances et provenance. Les artefacts de crash sont des ZIP
atomiques et bornés : logs sanitizés, version, empreinte du package, identifiant de reproduction et
dump facultatif derrière un adaptateur plateforme. Le rapport de santé agrège six domaines stables
et décide si un build peut continuer.

La récupération conserve un journal d’autosauvegardes, inspecte et met en quarantaine les entrées
corrompues, puis présente la comparaison avant/après avant toute restauration. La synchronisation
de contenu vérifie le manifeste, le signataire, la licence SPDX et le SHA-256 avant promotion dans
un cache adressé par contenu confiné au stockage choisi par l’utilisateur.

## Conséquences

- projet `0.6.19` et workspace `1.13.0` migrent les politiques absentes vers des UUID déterministes ;
- une récupération est proposée mais jamais appliquée silencieusement ;
- les fichiers `.partial` sont reprenables, les blobs valides sont dédupliqués et les manifestes
  altérés sont refusés avant résolution des chemins de cache ;
- le minidump natif dépend de l’adaptateur de la plateforme ; le contrat et l’archive restent
  testables sans prétendre fournir le collecteur natif sur toutes les cibles ;
- les secrets détectés dans les logs ne doivent jamais être copiés dans l’artefact distribué.
