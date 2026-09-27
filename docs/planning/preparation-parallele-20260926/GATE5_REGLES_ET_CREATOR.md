# Gate 5 — règles, écrans et deux modules, P07/P08

Date : 26 septembre 2026. Statut : spécification de préparation proposée. [Index](README.md).

## Autorités et reprise

Le [ruleset](../../design/OPENRPG_FORGE_RULESET_FR.md) définit les besoins normatifs. Le [manuel](../../design/README.md) développe une proposition sans classe, avec 42 compétences, 210 acquis et 100 talents ; sa présence ne signifie pas une adoption technique de toutes les formules. Le [bestiaire](../../design/bestiary/README.md) reste éditorial et mécanique. Les valeurs proposées seront rattachées à un profil versionné avant d'être des capacités du produit.

La [matrice de reprise](../../migration/LEGACY_REUSE_MATRIX.md) est la source de sélection historique : reprendre les parcours dialogues/quêtes, différer scripting à Gate 5, conserver les 717 routes comme référence, ne pas reproduire les 66 fenêtres. Aucun nouveau numéro historique n'est inventé. Le ruleset copié documente sa provenance `archive/main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0` dans le README de design. Un besoin historique supplémentaire devra porter chemin et commit avant reprise.

## P07 — Matrice complète du Creator

| Domaine de la roadmap | Écran/surface proposé | Données et commandes à définir | Cas d'acceptation décisif |
|---|---|---|---|
| Dialogues avancés | Graphe et éditeur de réplique/choix | Nœuds stables, conditions, effets et variables typées | Retoucher une branche sans perdre sauvegarde ni liens ; référence absente diagnostiquée |
| Quêtes et journal | Objectifs, dépendances, aperçu du journal | États, transitions, alternatives, échec et récompense | Deux solutions ; récompense unique après reprise |
| Factions | Relations et perception de conséquences | Groupes, relations, événements/témoins | Conséquence locale sans réputation globale omnisciente |
| Blueprints | Définition partagée et inspecteur d'instance | Référence, version, valeurs héritées/corrigées | Mettre à jour la définition sans écraser les overrides d'instance |
| Objets et conteneurs | Catalogue d'objets et contenu | Quantités, transferts, filtres, poids pertinent | Transfert atomique ; inventaire plein/objet absent sans perte |
| Créatures | Profil, comportements et présentation | Même système d'acteur, capacités, provenance visuelle | Deux créatures configurées sans branche spéciale dans le moteur |
| Capacités et effets | Coût, cible, durée, conditions et aperçu | Effets typés, stacking, expiration, causes | Effet expiré/annulé une fois, coûts et refus expliqués |
| Cinématiques | Séquence, déclencheur, aperçu | Pistes et références, interruption/reprise | Sauter/interrompre sans bloquer quête ni rendre le contrôle incohérent |
| Calendrier | Temps et événements planifiés | Horloge de simulation, récurrence, échéances | Reprise et avance du temps sans double déclenchement |
| Rencontres | Participants et conditions | Spawn, état, réarmement, limites et références | Rencontre unique ou récurrente selon règle, sans duplication après save/open |
| Profils de règles | Définitions, capacités et aide | ID/version, valeurs typées, compatibilité | Même définition et unité dans inspecteur, contenu, aide et calcul : R5-P01 |
| Scripting sandboxé | Script, permissions et diagnostics | API bornée, quotas, erreurs ; langage à sélectionner | Dépassement de quota ou accès interdit refusé, état préservé |
| Validation et diagnostics | Liste cliquable et contexte | Objet/condition cible, cause, niveau et remède | Cliquer une erreur mène à la donnée corrigible ; rejouer après correction |

Surfaces communes : sélection, filtre, référence résolue, valeur héritée/override/verrou, preview/diff, Apply, Undo/Redo, Save/Open et état dirty. Les détails de calcul sont accessibles à la demande. La [charte](../../design/DESIGN_SYSTEME.md) gouverne l'accessibilité ; la nouvelle coque Atelier reste soumise à ses preuves propres.

### Profil et progression : travaux préparés

Proposition de fiche par capacité : identifiant stable, version, origine normative/proposée, unités, entrées, conditions, résultat, explication, cas invalide, état conçu/implémenté/testé/qualifié. Le profil d'un module ne peut annoncer une capacité sur la seule présence d'un champ éditable.

Le [contrat du manuel](../../design/player-handbook/parts/05_implementation_contract.md) fournit les scénarios de référence R01–R32. Leur implémentation future commence par le sous-ensemble réellement adopté ; les dés/temps sont injectés et les arrondis explicites. Les tests exacts et les mesures d'équilibrage sont deux preuves distinctes. Aucun test du manuel n'a été relancé ici.

| Lot de conception | Frontière à préserver | Validation future |
|---|---|---|
| Tests de résolution | Contact physique distinct d'incertitude abstraite | Même résultat déterministe et même explication avec mêmes entrées |
| Progression par usage | Traces, répétition, palier et récompenses versionnés | Pas de farming illimité ni récompense de seuil répétée |
| Ressources/états | Bornes, coûts et priorités explicites | Refus atomique ; reprise sans dépasser les maxima |
| Causalité/social | Initiateur et information locale | Témoins/conséquences dépendant des faits réellement connus |
| Talents/effets | Préconditions et exclusivités | Achat refusé sans consommer, effets incompatibles jamais simultanés |

## P08 — Deux modules sans second éditeur

### Paire de preuve proposée

- Module A : aventure de référence village/forêt/intérieur. Combat et mécanisme environnemental, quête de récupération, profil minimal compatible Gate 3.
- Module B : petite enquête dans un intérieur, dialogue à choix, conteneur et objet à restituer. Aucun combat obligatoire. Configuration de panneaux centrée sur dialogue/quêtes/objets, ressources de profil choisies explicitement.

Le second module est un futur contenu de test, pas un module déjà généré. Il utilise les mêmes exécutables et commandes. Son absence de combat obligatoire n'implique pas qu'il nécessite un nouveau moteur ou une branche conditionnée par son slug.

| Test futur | Oracle |
|---|---|
| M01 Ouvrir A puis B | Les surfaces dépendent des capacités déclarées, pas de chemins ou noms spéciaux |
| M02 Ajouter une capacité absente du runtime | Diagnostic préalable ; aucun panneau donnant l'illusion d'une capacité fonctionnelle |
| M03 Modifier un profil | Inspecteurs, aide et contenu affichent la même version/unité ; migration proposée si nécessaire |
| M04 Réutiliser un blueprint et retoucher une instance | La retouche reste locale et survit à une mise à jour compatible |
| M05 Installer les deux modules | Références et sauvegardes isolées ; exécutable partagé : R5-P02 |
| M06 Désactiver une surface contenant du contenu existant | Contenu préservé ; incompatibilité ou accès de diagnostic visible |
| M07 Introduire une référence inter-module non déclarée | Validation refuse la dépendance cachée avant publication |
| M08 Importer un besoin historique choisi | Provenance et comportement testés ; aucun C#/Godot ni copie d'organisation imposée |

## Ordre d'implémentation proposé après les prérequis

1. Stabiliser profil/capacités et blueprints sur les contrats Gate 3/4.
2. Enrichir dialogue/quêtes, puis objets/conteneurs et créatures/effets.
3. Ajouter calendrier/rencontres et factions selon les scénarios réels.
4. Introduire cinématiques et scripting borné avec décision de langage séparée.
5. Qualifier les deux modules et les diagnostics sur les mêmes builds.

Narration et systèmes peuvent être répartis après fixation des profils/références communs. Les contrats, la persistance et la coque restent sous intégration unique. Les choix de langage, quotas et adoption complète du manuel restent explicitement ouverts ; aucun fournisseur technique n'est recommandé sans vérification actuelle.

## Livraison

P07/P08 : matrice des treize domaines, contrats de surfaces, scénarios et deux configurations proposés. Les exigences R5-P01/R5-P02 sont reliées à des oracles vérifiables. Les écrans, profils exécutables et modules ne sont pas implémentés par cette préparation.
