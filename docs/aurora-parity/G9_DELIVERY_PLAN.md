# G9 Network Beta — audit et plan de livraison

## État initial

Au démarrage de G9, les 30 exigences ciblées sont `Researched`; aucune n'est revendiquée comme implémentée. Le gate exige deux clients sur un serveur autoritaire, une reconnexion sans désynchronisation et le refus d'une mutation forgée.

## Lots bornés

| Lot | Exigences | Preuve de sortie |
|---|---|---|
| G9-A | MP-001 à MP-005 | serveur configurable, autorité, découverte, lobby et création validés |
| G9-B | MP-006 à MP-010 | personnages, groupes, PvP et instances de zones déterministes |
| G9-C | MP-011, 012, 013, 015, 016 | réplication filtrée, fingerprint, verrou de version et identité |
| G9-D | MP-017 à 020, 022 | permissions, administration, modération, chat et persistance |
| G9-E | MP-023, 027 à 030 | stockage borné, événements, health, logs et profilage |
| G9-F | MP-031 à 034, 036 | anti-triche, reconnexion, rollback, capacité et harnais deux clients |

Chaque lot ajoute ses tests déterministes et met à jour le tracker seulement après preuve. L'intégration finale branche l'hôte headless et l'interface auteur sur ces contrats.

