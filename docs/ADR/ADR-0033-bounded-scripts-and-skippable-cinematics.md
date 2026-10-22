# ADR-0033 — Scripts bornés et cinématiques interrompables

Date : 20 octobre 2026. Statut actualisé le 22 octobre 2026 : contrat du lot E
implémenté et qualifié localement, parcours Studio/runtime compris, dans le
périmètre borné de Gate 5. Voir le
[rapport de clôture](../evidence/gate5/GATE5_CLOSURE_20261022.md) et le
[rapprochement des preuves](../evidence/gate5/GATE5_LOCAL_QUALIFICATION_20261021.md).

## Besoin

Le Creator doit permettre d'écrire un script avec permissions et diagnostics,
de refuser un accès ou un quota dépassé sans mutation partielle, et de créer
une séquence que le joueur peut sauter ou reprendre sans bloquer ses contrôles
ni sa quête. Ces parcours réutilisent les commandes et sources du Studio.

## Décision de langage

Le premier profil de script adopte un AST JSON versionné, désérialisé en types
Rust. Chaque instruction possède une identité stable pour la retouche, l'audit
et le diagnostic. Les opérations initiales sont l'affectation d'une variable
narrative, l'ouverture/fermeture d'une porte et un message littéral ; une branche
compare une variable typée et une répétition possède un nombre borné d'itérations.
Le Creator expose les opérations et références résolues par des champs éditables.

L'interpréteur reçoit une API de partie explicite et des permissions portant
sur les variables lisibles/modifiables et portes accessibles. Les accès sont
vérifiés au moment de l'instruction exécutée. Le langage ne fournit aucune API
filesystem, réseau, processus, chargement de code ou évaluation JavaScript.
L'implémentation utilise les types et sérialiseurs déjà présents dans le domaine.

Un appel référence un script du document, avec l'acteur de la commande. Toutes
les instructions s'exécutent sur le candidat de la transaction habituelle.
La première erreur ou limite dépassée rejette cet appel entier, même après une
instruction ayant modifié le candidat. Les effets retenus rejoignent le reçu
normal et la vérification des variables/portes ; ils ne modifient pas les sources
authorées. Le diagnostic conserve script, instruction, cause et remède.

Bornes du profil : 32 scripts, 128 nœuds et huit niveaux par programme, au plus
16 itérations par répétition et 256 instructions exécutées par appel. Le budget
d'événements de la partie s'applique également. Leur adoption exige les tests
de quota et de refus atomique, y compris une faute après une première écriture.

## Cinématiques

Une séquence versionnée comporte des répliques et plans de caméra à durées
bornées, avec références stables et positions neutres. Son état de lecture est
distinct du contenu et utilise l'horloge commune de [ADR-0032](ADR-0032-simulation-clock-abilities-and-encounters.md).
Le premier profil reste une séquence de présentation : les transitions de quête
et récompenses continuent à passer par leurs commandes propres.

Le joueur peut sauter toute séquence. Fin, saut, interruption et échec rendent
le contrôle au contrôleur normal ; le rendu retire son override de caméra.
Les observations de simulation continuent pendant cette prise de contrôle de
présentation. La reprise restaure la progression de lecture sans répéter une
transition déjà consommée. L'adaptateur garde les objets de caméra Bevy dans le
runtime ; les sources et sauvegardes contiennent des valeurs et identités Forge.

Bornes du profil : 16 séquences et 32 segments par séquence, avec textes,
positions et durées validés. Le Creator fournit aperçu, retouche, historique et
Save/Open comme pour les autres données RPG.

## Exigences de preuve — acquises localement

Script autorisé, accès interdit, budget dépassé, références invalides, diagnostic
vers l'instruction corrigible, puis replay. Séquence complète, saut en cours,
interruption et reprise au milieu d'un segment, contrôle récupéré et état de
quête conservé. Les preuves du domaine, du moteur, du Creator et du parcours
restent distinctes dans le [plan Gate 5](../planning/GATE5_IMPLEMENTATION_PLAN.md).
