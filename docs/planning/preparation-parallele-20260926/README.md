# Préparations parallélisables — exécution du 26 septembre 2026

Suite à l'autorisation d'exécuter : [résultats complémentaires, corpus et audits](EXECUTION_20260926.md).
Le bilan ci-dessous décrit la première passe documentaire ; le complément
distingue ses validations effectivement rejouées et les prérequis encore ouverts.

Statut : **douze préparations réalisées et dossier documentaire vérifié**. Les cases ci-dessous suivent la réalisation des livrables de préparation, jamais le passage d'une gate.

La demande est exécutée dans la continuité du flux de préparation convenu : identifier les travaux indépendants de la clôture de Gate 2, puis les réaliser successivement. Aucune modification du produit, aucun build ni aucune qualification de gate n'est nécessaire pour ces livrables. Le périmètre est l'ensemble des domaines de la roadmap Gates 3 à 8, à la profondeur utile sans inventer les décisions de leurs dépendances.

## Liste complète des travaux retenus

| ID | Travail réalisable maintenant | Résultat concret à livrer | Dépendance pour l'implémentation |
|---|---|---|---|
| P01 | Cartographier les contrats existants et les frontières gameplay | Correspondance types actuels / responsabilités proposées | Gate 2 qualifiée ; décision de contrat |
| P02 | Définir l'aventure et les fixtures de test Gate 3 | Données nominales, deux approches et résultats attendus | Contrats gameplay et Studio |
| P03 | Préparer compatibilité des sauvegardes et retouches | Tableau des cas compatibles, refus et migrations | Save delta Gate 3 |
| P04 | Auditer la préparation navigation | Six blocages ADR-0009 et protocole de décision | Fournisseur qualifié et ADR accepté |
| P05 | Inventorier les ressources locales utilisables comme candidats | Sources présentes, droits déclarés, manques et états de préparation | Qualification produit des assets |
| P06 | Définir l'acceptation des imports et du package Gate 4 | Contrat de livraison et cas invalides | Aventure Gate 3 et format adopté |
| P07 | Relier règles, écrans et tests Gate 5 | Matrice des treize domaines du Creator | Contrats Gates 3/4 |
| P08 | Définir la preuve de deux modules et la migration historique | Deux configurations, invariants et reprise bornée | Profils versionnés et package |
| P09 | Préparer les parcours et limites IA/MCP Gate 6 | Plan/revue/application, menaces et huit scénarios | Commandes, assets et gameplay stabilisés |
| P10 | Préparer monde étendu et distribution Gate 7 | Matrice de budgets et qualification d'installation | Module autonome et simulation persistante |
| P11 | Préparer l'autorité et les essais réseau Gate 8 | Tableau d'autorité et tests de panne | Solo stable et monde multi-zones |
| P12 | Organiser intégration, preuves et ressources | Ordre, propriétaire des contrats et conditions de reprise | Selon chaque gate |

Ces douze travaux peuvent être réalisés en parallèle du chantier Gate 2. Dans cette tâche ils sont traités à la suite par un intégrateur unique. La mutualisation des contrats évite que P07, P09 et P11 définissent trois versions incompatibles de l'inventaire ou des quêtes.

## Livrables et suivi

- [x] P01 à P04 : [contrats, aventure, sauvegardes et navigation](GATE3_CONTRATS_ET_RECETTE.md).
- [x] P05 et P06 : [inventaire local et préparation du module](GATE4_ASSETS_ET_PACKAGE.md).
- [x] P07 et P08 : [règles et surfaces du Creator](GATE5_REGLES_ET_CREATOR.md).
- [x] P09 à P11 : [préparation IA, distribution et réseau](GATES6_8_CONTRATS_ET_PREUVES.md).
- [x] P12 : [intégration et validation](INTEGRATION_ET_VALIDATION.md).

Vérification : six documents, 39 liens locaux résolus, 29 identifiants de préparation/critères présents, aucun défaut whitespace signalé. Les scénarios sont spécifiés et non exécutés ; les limites et décisions restantes figurent dans chaque livrable.

## Références et état de départ

- [Roadmap active](../MIGRATION_ROADMAP.md) : Gate 2 ouverte, Gates 3 et suivantes non ouvertes.
- [Plan Gate 3 déjà préparé](../GATE3_PREPARATION_PLAN.md) : conservé comme plan d'implémentation ; ce dossier lui apporte des résultats de préparation.
- [Contexte canonique](../../../PROJECT_CONTEXT.md) : autorisation Tidewater distincte, enregistrée le 24 septembre.
- [Suivi Tidewater](../TIDEWATER_INTEGRATION.md) : chantier déjà engagé ; ne pas le dupliquer ni assimiler ses preuves à une gate complète.
- Branche locale observée : `Rust`, HEAD `f786a38aca2023f08d708687f99a47d14f34e6d4`, worktree modifié. `origin/Rust` est une référence locale non rafraîchie ; son écart ne décrit pas avec certitude le serveur actuel.

Le kit, les métadonnées et les sources inspectés peuvent évoluer pendant les autres tâches. Les preuves finales devront porter sur leurs hashes exacts au moment de l'intégration. Les noms de contrats proposés ci-dessous ne sont ni des API disponibles ni des schémas publiés.
