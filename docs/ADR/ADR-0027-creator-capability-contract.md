# ADR-0027 — Profils et surfaces du Creator

Date : 19 octobre 2026. Portée : Gate 5 autorisée par le propriétaire.

Statut actualisé le 22 octobre 2026 : contrat implémenté et qualifié localement
dans le périmètre Gate 5. Voir le [rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md).

## Décision

Le contrat du profil minimal existant reste `forge.rules.minimal.v1` ; ses sept
valeurs numériques et leur validation sont décrites dans `forge_gameplay` et
réutilisées par l'inspecteur et son aide. Un identifiant de profil personnalisé
ne prétend pas implémenter l'intégralité du manuel systémique.

Une configuration Creator est authorée avec le gameplay de sa zone/module.
Elle déclare les capacités requises et les surfaces visibles. Le moteur vérifie
les exigences contre son registre de capacités effectivement implémentées et
contre les données présentes. Masquer une surface ne supprime aucun contenu et
ne désactive pas une règle du runtime. La configuration n'est pas un mécanisme
de permissions. Les versions et capacités inconnues sont refusées explicitement.

## Compatibilité

Les documents gameplay v1 sans configuration gardent leur sérialisation et
leur comportement. L'activation explicite de la configuration produit un
document v2 ; les anciens lecteurs le refusent. Les nouveaux lecteurs acceptent
v1 et v2, et exigent la configuration en v2. Les commandes existantes assurent
preview, révision, application, audit, inverse et persistance ; pas de seconde
source ni de route spéciale pour l'IA.

La configuration de surfaces est éditoriale et ne modifie pas le hash
fonctionnel de la partie : celui-ci normalise le document v2 configuré vers
le contrat v1 équivalent. Les valeurs et la version du profil, les références
et les actions restent fonctionnelles et exigent la politique de migration
applicable ; changer une règle n'est pas une retouche compatible automatique.

Une capacité absente du registre implémenté peut être nommée comme exigence,
mais reste refusée ; un panneau ne suffit pas à la rendre disponible. Aucune
nouvelle dépendance ou langage de script n'est adopté par cette décision.
