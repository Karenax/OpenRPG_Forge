# Gate 3 — Plan détaillé de préparation

Date : 23 septembre 2026. Statut : **proposition documentaire préparée ; implémentation non démarrée**.

Ce plan développe les critères existants de la [roadmap](MIGRATION_ROADMAP.md#gate-3--première-boucle-rpg-environnementale). Il ne modifie ni leur statut, ni l'ordre des gates. Les identifiants G3-01 à G3-10 désignent des lots proposés, pas de nouveaux critères officiels. Aucun lot n'est qualifié par sa description.

## 1. Objectif et conditions d'entrée

Livrer une petite aventure complète créée depuis le Studio : recevoir une quête, dialoguer, traverser une zone, neutraliser un ennemi grâce au décor, récupérer un objet, terminer l'objectif, sauvegarder puis reprendre. Le parcours relie village, forêt et intérieur, propose deux approches et conserve une conséquence persistante.

État documentaire vérifié à la préparation : Gate 2 ouverte, R2-050 et R2-052 ouverts et conditions transversales non cochées. L'implémentation Gate 3 commence après :

1. clôture documentée de Gate 2 et décision de poursuite prévue par son scorecard ;
2. identification de la révision de départ et préservation du travail existant ;
3. vérification des contrats de workspace, de commandes et de playtest effectivement qualifiés ;
4. accord sur le périmètre d'implémentation et application des décisions structurelles requises.

Avant cela, seuls les travaux de préparation autorisés sont réalisés. Les blocages de navigation concernent son intégration de production : ils n'empêchent pas les travaux indépendants sur les interactions et le dialogue après ouverture de Gate 3.

## 2. Sources et fondations

Sources d'autorité :

- [Contexte canonique](../../PROJECT_CONTEXT.md), notamment modèle RPG et sauvegardes ;
- [Règles du dépôt](../../AGENTS.md) ;
- [Ruleset systémique](../design/OPENRPG_FORGE_RULESET_FR.md), besoins normatifs ;
- [Statut des documents de design](../design/README.md) : manuel détaillé proposé, adoption à versionner ;
- [Contrat d'implémentation du manuel](../design/player-handbook/parts/05_implementation_contract.md), invariants et scénarios proposés ;
- [Charte du runtime](../design/DESIGN_SYSTEME.md), HUD minimal et accessibilité ;
- [ADR navigation](../ADR/ADR-0009-navigation-provider-benchmark.md) ;
- [ADR workspace](../ADR/ADR-0016-project-module-workspace.md) ;
- [Protocole de qualification locale](../ADR/ADR-0006-remote-ci-exception-gates-0-4.md).

L'inspection préparatoire a trouvé le contrôleur joueur/caméra, les collisions Rapier, les documents de zone et de workspace, les commandes d'édition et le passage en playtest. Leur réutilisation dépend des preuves de Gate 2 ; aucune nouvelle validation runtime n'a été exécutée pour ce plan.

Le workspace Cargo inspecté ne contient pas de crate `forge_gameplay`. Les sources inspectées ne montrent pas de boucle intégrée d'inventaire/dialogue/quête/combat. Le point de départ du playtest est une métadonnée d'auteur, pas une sauvegarde de partie.

Le benchmark de navigation est un laboratoire exclu du workspace. ADR-0009 n'a sélectionné aucun fournisseur de production. Ses constats datés ne constituent pas une vérification actuelle des versions amont ; une actualisation sera nécessaire avant sélection.

## 3. Périmètre et aventure de référence

### Parcours proposé

Un PNJ au village demande de récupérer un objet dans un bâtiment situé au-delà de la forêt. Un gardien en bloque l'accès. Deux approches sont offertes : combat direct ou neutralisation par un mécanisme environnemental. Le joueur récupère l'objet et revient rendre la quête. L'état d'une porte, d'un support détruit ou du gardien conserve une conséquence observable.

Le dispositif environnemental proposé associe un support destructible à une charge qui tombe. Une zone d'essai complémentaire couvre explicitement poussée, lancer, impact, feu, extinction et réaction au danger. Chaque capacité doit disposer d'une preuve, même si elle n'est pas obligatoire dans les deux branches narratives.

Les noms, placements, textes et objectifs sont des données de module. Aucun comportement ne dépend d'un slug ou d'un objet propre à cette aventure. Le même composant est testé sur une seconde instance avec un autre identifiant.

### Inclus

- acteur joueur, ressources minimales et inventaire ;
- PNJ, navigation, ciblage et interaction ;
- dialogue conditionnel, quête, objectif et retour de progression ;
- ennemi et combat simple ;
- les huit capacités R3-B ;
- sauvegarde/reprise, retouche compatible et états de test ;
- authoring Studio, diagnostics et HUD nécessaires au parcours.

### Différé

Progression complète, catalogue de talents, magie composée, compagnons, factions avancées, scripting général et Creator étendu relèvent de tranches ultérieures. Le catalogue complet et la distribution `.orpmod` v2 restent Gate 4 ; streaming massif et distribution multiplateforme complète restent Gate 7 ; réseau reste Gate 8.

Les règles non prises en charge sont déclarées absentes du profil de capacités. Le prototype ne doit pas revendiquer la conformité complète au manuel. Toute restriction d'une exigence normative doit être documentée dans le périmètre adopté, jamais remplacée par un nombre caché.

## 4. Décisions à instruire

| Décision | Proposition de travail | Preuve ou résultat attendu | Échéance |
|---|---|---|---|
| Emplacement du domaine RPG | Évaluer une crate moteur-neutre dédiée, sans multiplier les couches | Graphe de dépendances et responsabilités documentés ; ADR si structure retenue | G3-01 |
| Profil de règles | Sous-ensemble versionné du ruleset, avec liste de capacités ; adoption explicite des formules proposées du manuel | Correspondance règle → calcul → scénario de test ; aucune formule exécutée comme chaîne arbitraire | G3-01 |
| Autorité de simulation | Actions validées et événements métier ; physique convertie par adapter | Ordre des étapes, temps, hasard injecté et traitement des contacts décrits | G3-01/06 |
| Compatibilité des sauvegardes | Delta lié à une révision, IDs stables et classification des retouches | Retouche texte/porte compatible ou conflit explicite ; pas de perte silencieuse | G3-03, consolidé G3-09 |
| Navigation | Réévaluer ADR-0009 et ses candidats | Sortie valide, mises à jour partielles, Rapier, dépendances, obstacle dynamique, navigateur réel | Avant intégration G3-04 |
| Lieux et transitions | Liens typés et chargement simple ; aucun besoin implicite de streaming | Passage village/forêt/intérieur et retour avec état conservé | G3-01/09 |
| Personnages et présentation | Petit ensemble d'assets gouvernés et animations nécessaires | Inventaire licences/hashes, échelle, animations et lisibilité ; visuels provisoires identifiés | Avant G3-06 |
| Sauvegarde pendant une action physique | Choisir état cohérent à sérialiser ou restriction visible et motivée | Reprise sans duplication de dégâts, de charge ni de réaction | G3-03/07 |

Le bestiaire est une proposition éditoriale et mécanique. Ses planches ne sont pas des modèles animés. L'acquisition éventuelle d'assets constitue un travail distinct à borner ; elle n'est pas exécutée par cette préparation.

## 5. Contrats transversaux

### Trois catégories d'état

1. **Documents d'auteur** : définition des acteurs/objets, placement, dialogue, objectifs, propriétés et état initial.
2. **État de partie** : position, ressources, inventaire, progression, variables et conséquences persistantes.
3. **Données dérivées** : entités Bevy, handles Rapier, navigation, effets et caches.

Le domaine ne persiste aucun handle moteur. Le playtest consomme une révision identifiée et ne modifie pas les documents d'auteur. L'Undo du Studio concerne les mutations d'auteur ; les actions de jeu suivent le journal et les règles de simulation, sans remplir cet historique d'édition.

### Commandes et événements

Les opérations de création réutilisent validation, preview/diff, révision attendue, Apply, inverse et audit. Le retry ne doit pas dupliquer un objet ni une récompense.

Les actions de jeu produisent des événements ordonnés, reliés à leur cause et à leur initiateur. Les règles calculent les conséquences ; les animations, sons et particules les présentent. Un contact physique confirmé n'est pas annulé par un jet abstrait ultérieur. Les réactions en chaîne possèdent des limites de volume et de durée.

### Retouches et sauvegardes

La compatibilité compare explicitement révision source et contenu courant. Une réplique conserve son identité après correction. Une porte distingue définition géométrique, pose d'auteur et état ouvert/fermé dans la partie. Suppression, changement d'identité ou déplacement rendant un acteur invalide déclenchent un diagnostic et une politique de reprise explicite. Aucune conversion destructive automatique.

## 6. Lots de réalisation

Chaque lot comprend les commandes, l'interface Studio utile, le comportement runtime, les tests et la mise à jour de son statut. Les tests ciblés accompagnent la tranche ; les contrats partagés déclenchent les validations transversales prévues par ADR-0006.

### G3-01 — Contrats et fixture de référence

- Dépendances : conditions d'entrée satisfaites.
- Définir identifiants, versions, limites et références des acteurs, objets, dialogues, quêtes, lieux et états de partie.
- Fixer le profil minimal, les invariants repris du ruleset et les décisions du tableau précédent.
- Spécifier les deux branches de l'aventure et les états de test avant/après chaque conséquence.
- Tests : données invalides, références manquantes, IDs dupliqués, bornes, round-trip et absence de dépendances UI/moteur dans le domaine.
- Sortie : contrat documenté et fixture reproductible ; aucune capacité runtime annoncée par un schéma seul.

### G3-02 — Première tranche jouable : ramasser

- Dépendance : G3-01.
- Relier le contrôleur existant à un acteur ; définir ressources minimales et inventaire borné.
- Créer/placer/configurer un objet depuis Studio ; ciblage, portée, visibilité et disponibilité d'action.
- Ramassage atomique : retrait du monde et ajout à l'inventaire ; retour visuel et explication du refus.
- Tests : hors portée, cible absente, inventaire plein, double activation, focus UI et retour en édition.
- Sortie : objet placé dans Studio puis ramassé en jeu ; état source inchangé ; commandes d'auteur annulables.

### G3-03 — Sauvegarde initiale et isolement

- Dépendances : G3-01/02.
- Delta versionné : module/révision, zone, acteur, ressources, inventaire, objets consommés et temps.
- Sauvegarde atomique, reprise dans un nouveau processus et sélection d'un état de test.
- Définir les limites de reprise en cours d'action et les erreurs de compatibilité.
- Tests : interruption d'écriture, fichier tronqué, mauvaise version, mauvais module, ressource absente, fermeture/reprise sans duplication.
- Sortie : ramassage → sauvegarde → fermeture → reprise avec inventaire exact et source intacte.

### G3-04 — PNJ et navigation

- Dépendance : G3-01 ; à instruire tôt en parallèle des lots indépendants après ouverture de Gate 3.
- Actualiser les candidats et satisfaire les six conditions d'ADR-0009 avant intégration en production.
- Raccorder géométrie canonique, tailles d'agent, destinations et état valide/périmé de navigation.
- Ajouter visualisation de debug et diagnostic sans chemin ; éviter le blocage de l'interface pendant génération.
- Tests : extérieur, intérieur, porte ouverte/fermée/réouverte, passage étroit, obstacle déplacé, coûts, évitement et contrôles négatifs du benchmark.
- Mesurer génération, reconstruction partielle et déplacement sur la fixture réelle ; exécuter Windows et navigateur.
- Sortie : PNJ naviguant réellement ; aucun succès déduit de la seule compilation WASM ou d'un chemin synthétique.

### G3-05 — Dialogue et quête minimale

- Dépendances : G3-02/03 ; un PNJ statique permet de travailler sans attendre G3-04.
- Commandes Studio pour répliques, choix, conditions et effets typés ; pas de moteur de scripts général.
- Objectifs fondés sur événements/états ; accepter, progresser, terminer ou diagnostiquer une quête bloquée.
- Journal minimal et attribution unique de récompense.
- Tests : branche inaccessible, condition fausse, objet manquant, reprise à chaque étape, événement répété et référence supprimée.
- Sortie : quête sans combat entièrement créée dans Studio et jouable, avec sauvegarde/reprise.

### G3-06 — Combat minimal

- Dépendances : G3-02/03 ; navigation G3-04 requise pour un ennemi mobile complet.
- Intention, préparation, fenêtre active, contact, conséquence, récupération et interruption.
- Sous-ensemble explicite de ressources, protection, dégâts et équilibre ; valeurs validées dans le profil.
- HUD, télégraphes, animations, réactions et son ; détails de calcul consultables.
- Tests : obstacle entre arme/cible, contact répété, protection absorbant les dégâts, ressource insuffisante, ennemi hors combat et reprise cohérente.
- Sortie : combat direct jouable, données partagées par joueur et ennemi, aucune règle recalculée dans l'UI.

### G3-07 — Physique comme moyen d'action

- Dépendances : G3-03/06.
- Propriétés génériques poussage, prise/lancer, masse, matériau, fragilité et support.
- Convertir contacts/impulsions en conséquences bornées ; rupture et libération de charge.
- Exposer les propriétés dans Studio et les causes dans les diagnostics.
- Tests : objet poussé, lancé, heurtant un mur, chute, support détruit, contact au repos sans dégâts infinis ; attribution causale et persistance.
- Sortie : seconde approche permettant de neutraliser un ennemi par le décor.

### G3-08 — Feu, extinction et réaction au danger

- Dépendances : G3-07 pour causalité ; G3-04 pour fuite/déplacement d'évitement.
- Combustible, source de chaleur, combustion et extinction ; propagation et nombres d'événements bornés.
- Perception locale du danger et comportement explicite du PNJ.
- UI de configuration et retours visuels cohérents avec l'état simulé.
- Tests : matière non inflammable, propagation autorisée/refusée, extinction, répétitions, reprise et danger inaccessible.
- Sortie : allumer → observer conséquence et réaction → éteindre ; aucun dommage produit par le seul effet graphique.

### G3-09 — Aventure complète et retouches compatibles

- Dépendances : G3-05 à G3-08.
- Relier les lieux et les deux approches ; récupérer l'objet et terminer l'objectif.
- Étendre le delta de partie à tous les systèmes introduits ; conserver la conséquence lors d'un aller-retour entre lieux.
- Retoucher réplique et porte depuis Studio puis reprendre une sauvegarde existante.
- Tests : deux branches depuis états propres, objectif impossible, porte supprimée, sauvegarde avant/après neutralisation et récompense sans duplication.
- Sortie : R3-P01 et R3-P02 couverts par parcours complets sur candidat identifié.

### G3-10 — Diagnostics, revue et qualification

- Dépendance : G3-09 ; les diagnostics sont construits progressivement dès les lots précédents.
- Diagnostic sélectionnant l'objet/la condition à corriger ; état de test et révision de reproduction visibles.
- Inspection des actions, contacts, réactions et états de quête sans exposer tous les détails dans le HUD courant.
- Mesurer fluidité, attentes, budgets de simulation et coût de navigation sur matériel/résolution/profil définis ; comparer aux budgets du contexte canonique.
- Observer une partie humaine complète : compréhension, lisibilité, rythme, erreurs et reprises.
- Exécuter le protocole de qualification applicable sur commit/arbre exacts ; compiler puis lancer les binaires exacts.
- Sortie : dossier de preuves, limites et verdict explicite ; aucune clôture déduite d'un simple build ou d'une capture.

## 7. Dépendances et organisation

Séquence principale : G3-01 → G3-02 → G3-03 → G3-05 → G3-09 → G3-10.

Branche navigation : G3-01 → G3-04, puis intégration avec G3-06 et G3-08.

Branche systémique : G3-02/03 → G3-06 → G3-07 → G3-08 → G3-09.

Les contrats de G3-01 constituent le point commun ; ils sont stabilisés avant de répartir des implémentations. Navigation et narration peuvent ensuite avancer séparément sur des fichiers distincts. Les builds, le contrôle graphique et les intégrations partagées doivent être coordonnés. Cette proposition d'organisation ne lance aucun agent ni travail d'implémentation.

Première livraison fonctionnelle visée : **placer un acteur et un objet dans Studio, jouer, ramasser, sauvegarder, fermer et reprendre avec inventaire intact**. Elle correspond à G3-01/02/03 et donne rapidement une preuve de l'architecture complète.

## 8. Couverture des critères officiels

| Critère | Lots | Preuve de sortie |
|---|---|---|
| R3-A joueur/stats/inventaire | 01/02/03 | Ramassage, bornes de ressources et reprise exacte |
| R3-A PNJ/navigation | 04 | Trajets réels et cas sans chemin, natif et navigateur |
| R3-A interaction/dialogue/quête | 02/05 | Création Studio, conversation et résolution |
| R3-A ennemi/combat | 06 | Combat direct et conséquences expliquées |
| R3-A sauvegarde/reprise | 03/09 | Nouveau processus, monde et inventaire cohérents |
| R3-B poussable/lançable/impact/chute ou piège/destructible | 07 | Cas isolés et neutralisation environnementale |
| R3-B feu/eau-extinction/réaction IA | 08 | Cycle complet et budgets respectés |
| R3-P01 | 09 | Trois lieux, deux approches, conséquence persistante |
| R3-P02 | 03/05/09 | Réplique et porte retouchées, reprise sans perte |
| R3-P03 | 04/05/09/10 | Objectif inaccessible et référence absente, correction guidée |
| R3-P04 | 03/10 | État de test choisi, révision épinglée, source intacte |
| R3-P05 | 10 | Partie complète observée et jugement humain consigné |

## 9. Protocole de preuve

Pour chaque parcours : révision des sources et règles, hash du candidat, fixture initiale, plateforme, matériel, résolution, profil, entrées, résultat attendu/observé et limites. Comparer les documents d'auteur avant/après simulation. Conserver captures, erreurs et mesures pertinentes, puis fermer les processus d'essai.

Les tests de domaine utilisent temps et hasard injectés. La physique emploie des tolérances déclarées ; aucun déterminisme numérique bit à bit entre plateformes n'est présumé. La parité vérifie les conséquences attendues et la persistance dans ces tolérances.

À la clôture : formatage, Clippy strict, tests workspace, builds et smokes Windows/WASM, contrôles de dépendances applicables et non-régression des gates affectées selon ADR-0006. Une compilation ne prouve pas le comportement navigateur. Les conditions de l'exception CI doivent être revérifiées ; aucun résultat distant n'est inventé.

Caches, builds et preuves volumineuses restent sur E:. Vérifier l'espace disponible et fixer un budget avant une campagne globale. Le plan ne lance ni compilation, ni acquisition de dépendances, ni publication.

## 10. Risques et critères d'arrêt d'un lot

| Risque | Traitement prévu |
|---|---|
| Fournisseur de navigation non qualifié | Investigation précoce ; conserver le laboratoire isolé ; décision explicite avant production |
| Extension excessive aux règles du manuel | Liste versionnée des capacités et correspondance avec critères Gate 3 |
| Conflit retouche/sauvegarde | IDs stables, classification des changements, validation avant reprise |
| Divergence physique/domaine | Adapter de contacts, ordre défini, déduplication et tolérances testées |
| Réactions environnementales sans fin | Budgets, causalité, bornes et scénarios adverses |
| Interface créée trop tard | Tranches complètes Studio → jeu → preuve dès G3-02 |
| Assets ou animations insuffisants | Inventaire précoce ; prototypes identifiés ; revue visuelle avant verdict final |
| Qualification trop coûteuse en stockage | Budget E:, validation ciblée pendant les lots puis campagne finale planifiée |

Un lot reste ouvert si son parcours échoue, si une preuve obligatoire manque ou si sa dépendance n'est pas qualifiée. Le rapport indique la cause et les travaux indépendants possibles. Aucun critère n'est supprimé pour produire un PASS.

## 11. État de cette préparation

- Analyse et plan : préparés.
- Décisions proposées : à instruire/adopter pendant l'ouverture autorisée.
- Code gameplay, changements de schéma, dépendances et assets : non réalisés par cette tâche.
- Gate 3 : non ouverte par ce document.
- Estimation calendaire : non chiffrée ; navigation et disponibilité des assets/animations doivent être levées avant une prévision fiable.

Le prochain travail d'implémentation, après satisfaction des conditions d'entrée, est G3-01 puis la première tranche G3-02/03. Les preuves et statuts définitifs restent portés par la roadmap et le futur rapport de qualification.
