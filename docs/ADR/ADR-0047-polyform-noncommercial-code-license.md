# ADR-0047 — Licence PolyForm Noncommercial du code propre

Date : 24 août 2026
Statut : accepté
UUID : `bcdd8fd2-3376-4b18-bd59-e1b5e7c36e47`
Schéma : v1
Provenance : `author:Karenax, formalisation OpenAI Codex sous instruction utilisateur`

## Contexte

Le dépôt ne possédait pas de licence explicite pour son code propre, alors que les composants et
assets tiers conservaient déjà leurs licences et notices. Cette absence empêchait d'énoncer les
droits accordés aux utilisateurs et restait un prérequis explicite de qualification publique.

Le propriétaire du projet a choisi le texte PolyForm Noncommercial 1.0.0 fourni le 24 août 2026.
Ce choix autorise les usages non commerciaux définis par la licence, tout en réservant les usages
commerciaux au concédant.

## Décision

Le code propre à OpenRPG Forge et les actifs originaux du projet qui ne portent pas une licence
distincte sont distribués sous PolyForm Noncommercial 1.0.0, identifiant SPDX
`PolyForm-Noncommercial-1.0.0`.

Le texte standard est conservé dans `LICENSE.txt` avec la notice requise :

`Required Notice: Copyright 2026 Karenax and OpenRPG Forge contributors`

Cette licence est décrite comme une licence à code source disponible et non comme une licence libre
ou open source : elle n'accorde pas d'autorisation pour un usage commercial. Un usage commercial
nécessite une autorisation distincte du concédant.

Les dépendances, composants tiers, snapshots et assets portant leur propre licence ne sont pas
relicenciés. Les contenus originaux créés avec le Studio restent sous la licence choisie par leur
auteur ; l'utilisation ou la redistribution d'OpenRPG Forge et de son runtime demeure soumise à la
présente licence.

Chaque génération de distribution copie la licence racine sous
`compliance/PROJECT-LICENSE.txt`. Le SBOM déclare l'identifiant SPDX sur le composant produit et le
contrôle de conformité vérifie que la copie distribuée est identique à la licence racine.

## Preuves

- Texte officiel : `https://polyformproject.org/licenses/noncommercial/1.0.0`
- Identifiant SPDX : `https://spdx.org/licenses/PolyForm-Noncommercial-1.0.0.html`
- Génération : `scripts/New-DistributionCompliance.ps1`
- Contrôle : `scripts/Test-DistributionCompliance.ps1`
- Documentation : `README.md`, `PROJECT_CONTEXT.md`, `STATUS.md`, `TASKS_MVP.md` et
  `docs/DISTRIBUTION.md`

## Conséquences

- les droits non commerciaux accordés sur le code propre sont explicites ;
- les distributions emportent le texte exact et le SBOM identifie la licence du produit ;
- les obligations et licences tierces restent séparées ;
- le projet ne doit plus être présenté sans précision comme un logiciel libre ou open source ;
- aucune autorisation commerciale ne doit être déduite de l'accès public au code source ;
- l'adoption technique de la licence ne remplace pas la vérification, par le propriétaire du projet,
  qu'il dispose des droits nécessaires sur toutes les contributions qu'il publie sous ces termes.

## Remplacement

Cette décision précise la politique juridique du code propre. Elle limite, sur ce point, l'objectif
de socle libre formulé dans l'ADR 0001 sans modifier la décision technique Godot/.NET de cet ADR.
