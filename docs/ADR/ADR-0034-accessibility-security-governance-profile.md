# ADR-0034 — Accessibilité, fuzzing et approbation des dépendances

Date : 5 août 2026
Statut : accepté

## Décision

L’accessibilité éditeur/runtime et la politique de fuzzing appartiennent au
`QualityAssuranceProfile` canonique du module. Elles sont donc persistantes, versionnées,
transactionnelles et identiques dans Creator, le runtime, le CLI et le MCP. Les préférences locales
de taille et de police de l’auteur restent dans `EditorWorkspaceProfile`, mais doivent respecter les
bounds publiés par le module.

Le laboratoire de gouvernance est reproductible : son empreinte exclut les durées et inclut les
résultats des sept frontières. Les archives aléatoires utilisent des fichiers temporaires supprimés
immédiatement ; les artefacts explicites restent confinés sous `.forge/quality`.

Une dépendance distribuée n’est plus seulement documentée : elle possède un état d’approbation, un
niveau de risque, au moins deux rôles de revue et une référence d’audit sécurité. Ces informations
sont copiées dans le manifeste de package et comparées au registre canonique au chargement.

## Conséquences

- les formats passent à projet `0.6.21`, workspace `1.15.0` et manifeste `.orpmod` `1.2.0` ;
- l’absence des nouveaux profils dans un document `0.6.20` ou `1.14.0` produit des UUID et valeurs
  de migration déterministes ;
- le mode de mouvement réduit est configurable mais désactivé par défaut afin de préserver la mise
  en scène existante ;
- le gate interroge les avis de vulnérabilité NuGet après restauration verrouillée ;
- aucune dépendance runtime, aucun service distant et aucun moteur externe ne sont ajoutés.
