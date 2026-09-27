# Gate 3 — résultats de préparation P01 à P04

Date : 26 septembre 2026. Statut : conception proposée, contrôlée contre les sources locales ; aucun schéma ni code ajouté au produit. [Index et portée](README.md).

## P01 — Points de raccordement constatés

| Source inspectée | Contrat existant | Ce qu'il permet | Extension à concevoir |
|---|---|---|---|
| [authoring.rs](../../../crates/forge_project/src/authoring.rs) | AuthoringRequest : PreviewZone, ApplyZone, Save, StartPlaytest, StopPlaytest | Commandes d'auteur et playtest épinglé par expected_hash | Commandes de création gameplay, sans mutation directe depuis l'UI |
| [workspace.rs](../../../crates/forge_project/src/workspace.rs) | WorkspaceHead → ProjectEntry → ModuleEntry → ZoneReference | IDs et références d'archives/hashes | Propriété des dialogues, quêtes, profils et dépendances du module |
| [zone_authoring.rs](../../../crates/forge_project/src/zone_authoring.rs) | ZoneAuthoringHead, base et corrections typées | Autorité unique et conservation des retouches | Liaison d'instances aux définitions gameplay sans seconde tête éditable |
| [session.rs](../../../crates/forge_project/src/zone_authoring/session.rs) | ZoneRequest, ZoneReceipt, ZoneEditSession | Preview, validation de révision, exécution, historique et archive | Réutilisation des invariants, décision sur les transactions multi-documents |
| [player_controller.rs](../../../crates/forge_engine/src/player_controller.rs) | Commandes de contrôle et PlayerSimulationReady | Déplacement, préparation de simulation et observations | Identité d'acteur, ressources et actions |
| [playtest.rs](../../../crates/forge_project/src/authoring/playtest.rs) | Identité et diagnostics de playtest | Liaison à la source inspectée | Session RPG et sélection d'un état de test |

Observation significative : ModuleEntry référence actuellement les zones ; ce n'est pas déjà un conteneur universel de dialogues et quêtes. La structure de stockage future doit être adoptée explicitement. Les fichiers examinés n'établissent aucun service RPG complet.

### Catalogue de responsabilités proposé

| Objet de domaine proposé | Données minimales | Invariant |
|---|---|---|
| Profil de règles | ID/version, capacités, définitions typées, unités | Le contenu ne peut demander une capacité absente |
| Définition d'acteur | ID, profil, ressources initiales, références de présentation | Aucune Entity Bevy persistée |
| Instance d'acteur | ID stable, définition, lieu, pose initiale | Une régénération non concernée conserve son identité |
| Définition d'objet | ID, empilement/masse si pertinents, interactions, présentation | Les valeurs de gameplay ne sont pas déduites du nom du mesh |
| Interaction | acteur, cible, verbe, préconditions, effets typés | Validation de portée et d'état avant mutation |
| Dialogue | ID, nœuds/choix stables, textes, conditions, effets | Aucun choix ne mène à une référence absente |
| Quête | ID, objectifs, transitions, effets/recompenses | Une récompense ne s'applique qu'une fois |
| Session de jeu | révision de contenu, profil, temps, hasard, états d'acteurs/objets/quêtes | Les sources d'auteur restent intactes |
| Événement causal | ID, cause, chaîne, initiateur, tick, conséquence | Aucun dommage dédoublé par simple répétition de transport/contact |

Les bornes de taille, références et budgets doivent être choisies avec les fixtures puis validées avant publication des schémas. Les transactions traversant quête, inventaire et récompense sont atomiques : toutes les conséquences sont acceptées ou aucune. L'état d'auteur et l'état de partie ne partagent pas le même Undo.

## P02 — Fixture narrative prête à être authorée

Identifiants ci-dessous : conventions de fixture uniquement, sans en faire des clés spéciales du moteur.

| ID de fixture | Rôle | Relations |
|---|---|---|
| lieu.village | Départ et remise de quête | Passage vers lieu.foret ; pnj.intendant |
| lieu.foret | Chemin et obstacle | Passage vers lieu.reserve ; garde ; mécanisme |
| lieu.reserve | Intérieur et objectif | porte.reserve ; objet.sceau |
| pnj.intendant | Donneur de quête | dialogue.intendant ; quete.sceau |
| acteur.garde | Obstacle mobile | comportement de garde et réactions au danger |
| porte.reserve | Accès à l'intérieur | définition géométrique distincte de son état de partie |
| support.charge / charge.pierre | Neutralisation par environnement | Support destructible, charge physique ; cause conservée |
| objet.sceau | Objet unique de quête | Passage du monde à l'inventaire, puis remise atomique |
| quete.sceau | Récupération et remise | Deux approches, état du garde et support persistants |

Réplique de départ proposée : « Le sceau est dans la réserve, au-delà du bois. Rapporte-le-moi. » Choix : « Je m'en charge. », « Quel passage puis-je emprunter ? », « Plus tard. ». La branche d'information révèle le mécanisme sans l'activer. Aucun dialogue social probabiliste n'est nécessaire pour ce premier parcours.

État nominal : quête disponible → acceptée → objet récupéré → terminée. La neutralisation du garde est une conséquence, pas nécessairement un objectif obligatoire. L'approche A utilise le combat direct ; l'approche B utilise la charge libérée par destruction du support. Les deux aboutissent au même objet unique ; le support reste intact en A et détruit en B.

États de test : S0 avant acceptation ; S1 quête acceptée ; S2 avant obstacle ; S3A après combat ; S3B après mécanisme ; S4 objet en inventaire ; S5 quête terminée. Chaque état futur épingle révision, profil, seed et conséquences. Recharger S2 ne restaure jamais implicitement les documents d'auteur.

### Cas de test spécifiés, non exécutés

| ID | Manipulation | Oracle attendu | Couverture |
|---|---|---|---|
| T01 | Placer l'objet, lancer, ramasser deux fois | Une possession, retrait unique du monde | R3-A inventaire/interaction |
| T02 | Interagir hors portée ou à travers obstacle | Refus explicite, état inchangé | Interaction |
| T03 | Parler, refuser puis accepter | Quête acceptée une seule fois | Dialogue/quête |
| T04 | Traverser village → forêt → intérieur → retour | Références valides, destination accessible | R3-P01 |
| T05 | Jouer A puis B depuis S2 séparément | Objectif atteignable par les deux voies ; conséquences distinctes | R3-P01 |
| T06 | Sauver S4, fermer, reprendre et remettre deux fois | Objet/recompense non dupliqués | Sauvegarde/quête |
| T07 | Pousser puis lancer une caisse | Mouvement/impact cohérents ; contact au repos sans boucle de dégâts | R3-B |
| T08 | Détruire le support et laisser tomber la charge | Chute, impact et initiateur reliés | R3-B |
| T09 | Allumer puis éteindre un matériau inflammable | État, effet visible et dommages concordants | R3-B feu/eau |
| T10 | Placer un PNJ face au danger | Danger perçu et réaction visible, limites de perception respectées | R3-B IA |
| T11 | Retoucher réplique/porte et reprendre S4 | Politique de compatibilité ci-dessous respectée | R3-P02 |
| T12 | Supprimer une référence ou fermer tous les accès | Diagnostic ouvre l'objet/la condition fautive | R3-P03 |
| T13 | Lancer depuis S2 et quitter après destruction | Sources inchangées ; reproduction identifiable | R3-P04 |
| T14 | Observer une partie complète | Relevé de compréhension, rythme, erreurs et attentes | R3-P05 |
| T15 | Arrêter une écriture de sauvegarde | Ancienne sauvegarde conservée ou récupération explicite | Atomicité |
| T16 | Déclencher contact confirmé puis protection totale | Contact conservé, Trauma nul expliqué, aucun « miss » artificiel | Ruleset contact |

R3-B est entièrement couvert par T07–T10 ; les tests de gameplay ne se limitent donc pas aux seules actions choisies dans une branche de quête.

## P03 — Tableau de compatibilité des sauvegardes

| Changement d'auteur après sauvegarde | Proposition | Contrôle |
|---|---|---|
| Texte d'une réplique, mêmes IDs | Reprise explicite sur révision mise à jour ; texte courant utilisé | Choix et progression conservés |
| Texture/mesh compatible, même rôle fonctionnel | Reprise après contrôle des références | État de quête/inventaire intact |
| Porte déplacée, même ID | Conserver état ouvert/fermé, appliquer nouvelle pose après validation spatiale | Pas d'acteur dans un mur ni d'objectif inaccessible |
| Porte supprimée ou ID changé | Refuser la migration automatique si état/référence persistant | Diagnostic et stratégie de migration à choisir |
| Objet de quête supprimé | Détecter référence/objectif invalide | Aucun retrait silencieux de l'inventaire |
| Profil de règles modifié | Contrôle de version et des capacités | Migration explicite ou reprise sur version d'origine disponible |
| Zone absente ou contenu introuvable | Refus avant activation de la partie | Aucune session partiellement chargée |
| Reprise pendant chute/incendie | Sérialiser un état cohérent ou borner les points de sauvegarde selon décision adoptée | Pas de double impact, pas d'extinction implicite |

La proposition privilégie des mises à jour identifiées et réversibles ; ce document ne publie pas encore un algorithme universel de migration.

## P04 — Navigation : diagnostic et protocole prêt

L'[ADR-0009](../../ADR/ADR-0009-navigation-provider-benchmark.md) et le [laboratoire existant](../../../tools/forge_navigation_benchmark/README.md) décrivent une pile candidate épinglée, pas une sélection de production. Aucun état amont plus récent n'est supposé.

| Condition ADR | Travail de qualification futur | Oracle de décision |
|---|---|---|
| Sortie globale valide | Rejouer vallée brute et contrôles négatifs | Aucun polygone invalide ; aucune normalisation cachée |
| Tuiles/reconstruction partielle | Modifier une région et mesurer le travail affecté | Identités hors région conservées et coût borné documenté |
| Source Rapier/collisions commune | Comparer géométrie navigable et géométrie canonique | Aucun pont/porte navigable sans collision correspondante |
| Gouvernance de dépendances | Actualiser advisory/version et politique | Résolution ou décision explicite ; aucun assouplissement silencieux |
| Dynamique et vrai navigateur | Porte, obstacle déplacé, tailles, liens, évitement | Réaction observable Windows et WASM en exécution |
| Benchmark et ADR | Rapport sur candidat exact | Dossier complet puis acceptation de la sélection |

À collecter : temps de construction et de mise à jour, mémoire, nombre d'agents, délais de requête, budget par frame et résultat sans chemin. Les seuils sont à fixer par profil matériel avant mesure ; aucun chiffre arbitraire n'est présenté comme exigence acquise. La recherche d'un fournisseur actuel et son benchmark constituent une étape ultérieure identifiée, pas un résultat de cette inspection locale.

## Livraison

P01–P04 : préparation réalisée. Les contrats, la fixture et les oracles sont décrits ; leur implémentation, les tests et la décision fournisseur restent à réaliser dans le périmètre autorisé de la gate. [Plan d'exécution Gate 3](../GATE3_PREPARATION_PLAN.md).
