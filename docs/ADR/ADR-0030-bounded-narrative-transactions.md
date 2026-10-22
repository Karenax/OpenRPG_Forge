# ADR-0030 — Variables et quêtes narratives bornées

Date : 20 octobre 2026. Statut actualisé le 22 octobre 2026 : contrat narratif
implémenté et qualifié localement dans le périmètre Gate 5. Les exigences
ci-dessous sont rapprochées des preuves dans le
[rapport de qualification](../evidence/gate5/GATE5_LOCAL_QUALIFICATION_20261021.md) ;
le [rapport final](../evidence/gate5/GATE5_CLOSURE_20261022.md) porte la clôture.

## Besoin

Le dialogue de Gate 3 sait accepter puis terminer une quête de récupération.
Gate 5 exige des branches à variables typées, des solutions alternatives,
des dépendances, l'échec et un journal, avec une seule récompense après reprise.
Les factions et leurs témoins locaux restent un sous-ensemble distinct du lot C.

## Décision

Le document gameplay conserve un bloc narratif facultatif, absent des documents
historiques. Sa présence exige la configuration Creator explicite de version 2.
Les variables ont une identité stable et une valeur initiale booléenne, entière
signée ou texte borné. Une affectation ne change jamais leur type. Les comparaisons
ordonnées sont réservées aux entiers. Les conditions composées ont des quotas
de profondeur et de nombre de nœuds ; aucune évaluation de code n'est introduite.

Un flux de quête référence une quête existante et ses prérequis. Chaque solution
porte une identité, une condition et une liste de consommations distinctes.
Le flux remplace le chemin de récupération historique pour cette quête : le
chemin historique ne permet pas de contourner la condition d'une solution.
La récompense reste définie une seule fois sur la quête. La transition active
vers terminée, les consommations, la récompense et la solution retenue participent
à la même transaction clonée que tous les effets du choix de dialogue.
L'échec explicite exige sa condition et rend la quête terminale sans récompense.
Les dépendances cycliques sont refusées avant activation du document.

Le journal est une projection des définitions et de l'état canonique de la partie.
Il n'est pas une seconde liste modifiable. Les valeurs courantes et la solution
retenue sont persistées ; le journal d'actions permet de refuser une altération
isolée de ces valeurs, même après recalcul de l'empreinte de sauvegarde.
La conservation des objets reste contrôlée par le ledger commun, étendu aux
coûts des solutions narratives et aux transitions d'échec.

Les textes des variables, solutions et journaux sont retouchables sans changer
le sens fonctionnel de la sauvegarde. Modifier un type, une valeur initiale,
une condition, un coût, un prérequis ou une solution change l'identité fonctionnelle.
Les archives et sauvegardes historiques sans narration conservent leur encodage.

## Qualification requise

Deux solutions réellement exécutées, récompense unique après Save/Open,
condition absente/refusée sans mutation, échec terminal, débordement d'inventaire
annulant tous les effets du choix, références/types/quota invalides, retouche de
texte et refus des états narratifs altérés. Ajouter ensuite les surfaces d'auteur,
leur historique et les projections Studio/runtime avant de qualifier le parcours.
Les preuves de domaine seules ne valident pas l'éditeur ni les factions.
