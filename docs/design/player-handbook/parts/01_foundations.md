<!-- handbook-part: foundations -->

# Fondations du Manuel du joueur

## Bienvenue dans OpenRPG Forge

OpenRPG Forge propose un jeu de rôle sans classe. Un personnage n’est pas choisi dans une liste de professions mécaniques : il se construit par ce qu’il fait, les problèmes qu’il apprend à résoudre, les personnes qu’il rencontre et les conséquences qu’il accepte.

Une voyageuse qui s’entraîne à la lance, protège ses compagnons, étudie les vestiges d’une civilisation et négocie avec ses héritiers devient peu à peu combattante, érudite et médiatrice. Ces mots racontent son parcours ; ils ne lui ferment aucune porte. La même voyageuse peut ensuite apprendre l’alchimie, délaisser son armure, devenir pisteuse ou étudier la magie.

Ce manuel présente une proposition complète destinée à remplir deux fonctions complémentaires :

1. expliquer au joueur ce qu’il peut tenter et pourquoi les conséquences se produisent ;
2. fournir à l’équipe de développement un contrat assez précis pour transformer les règles en données, systèmes Rust et tests reproductibles.

Le texte est original et conçu en **clean-room**. Il recherche la richesse, la générosité et le confort de consultation d’un grand manuel de jeu de rôle classique sans reprendre les formulations, classes, tables, univers, personnages ou compositions d’un ouvrage existant.

## Statut de cette version

**Identifiant de conception :** `openrpg-forge.player-handbook.fr.v0.1`  
**Ruleset parent :** `openrpg-forge.systemic-rpg.v1`  
**Langue :** français  
**Statut :** proposition normative de conception, prête à être découpée en contrats d’implémentation  
**Runtime Rust :** non implémenté et non qualifié à la date de cette version

Les exigences du ruleset parent restent la source normative de haut niveau. Le présent manuel en détaille une interprétation jouable : valeurs, formules, paliers, catalogues et cas de référence. Une règle chiffrée de ce manuel n’est pas une preuve qu’elle existe déjà dans le moteur. Lorsque l’implémentation commencera, chaque tranche devra être testée, équilibrée et promue explicitement.

Les valeurs marquées **référence** sont les réglages de base proposés. Un module peut les adapter au moyen d’un profil de règles versionné, mais il ne doit pas changer silencieusement la signification d’une compétence ou d’un événement.

## La promesse faite au joueur

### Aucun choix irréversible de classe

Tous les personnages disposent des mêmes attributs, des mêmes quarante-deux compétences et du même catalogue de talents. Les conditions d’accès proviennent de la pratique, des connaissances, de l’équipement, des ressources et du monde — jamais d’une étiquette de classe.

### La pratique transforme le personnage

Une action significative produit des traces d’apprentissage. Plus la compétence est haute, plus son prochain point exige de traces, de variété et de problèmes difficiles. Une répétition sans enjeu cesse rapidement d’enseigner quoi que ce soit.

### L’habileté du joueur et celle du personnage coopèrent

Le joueur choisit l’angle, le moment, l’outil et la méthode. Le personnage apporte sa précision, son contrôle, sa connaissance et son endurance. Un bon plan permet à un novice de réussir une action accessible ; la maîtrise rend possibles des méthodes, des économies et des enchaînements qu’un novice ne connaît pas encore.

### Le monde est cohérent

Une lame qui touche réellement produit un contact. Une armure résiste par sa matière et sa zone couverte. L’eau conduit, le feu éclaire et enfume, une poutre privée d’appui tombe, un témoin doit transmettre ce qu’il a vu. Les règles ne créent pas un « raté » invisible lorsque la scène montre le contraire.

### Les adversaires obéissent aux mêmes principes

Les personnages non joueurs peuvent apprendre, employer des talents, se protéger, paniquer, négocier, enquêter et exploiter le décor. Une exception doit être justifiée par une anatomie, une matière, un équipement ou une capacité explicite.

### L’échec fait avancer la situation

Un échec intéressant crée un coût, un retard, une exposition, une information partielle ou un nouveau choix. Il ne doit pas se résumer à fermer une porte jusqu’au prochain rechargement. L’échec peut aussi enseigner lorsque l’essai était plausible et qu’un retour réel en ressort.

### La puissance augmente les possibilités

La progression ouvre des verbes, des réactions, des préparations et des combinaisons. Elle ne multiplie pas indéfiniment la Vitalité ni les dégâts. Un maître reste vulnérable à une chute, une embuscade, une noyade ou une décision imprudente.

## Les trois couches de progression

Le système distingue trois formes d’évolution afin qu’aucune ne remplace les autres.

| Couche | Comment elle progresse | Ce qu’elle apporte | Ce qu’elle n’apporte pas |
|---|---|---|---|
| **Compétences** | Usage significatif, entraînement, étude et mentorat | Fiabilité, contrôle et acquis automatiques | Permission exclusive ou classe |
| **Niveau global** | Jalons obtenus aux paliers de compétences | Points de talent et rares points d’attribut | Rangs de compétence ou adaptation automatique du monde |
| **Talents** | Choix dépensé pendant un repos long | Nouvelle option, réaction, doctrine ou préparation | Bonus universel sans contre-jeu |

Cette séparation permet de lire immédiatement un personnage : ses compétences racontent ce qu’il a fait ; son niveau résume l’ampleur de son parcours ; ses talents révèlent comment il préfère agir.

## La règle commune

Un test n’est demandé que si le résultat est incertain et si l’échec change la situation.

```text
Résultat = 2d10
         + ⌊Compétence ÷ 5⌋
         + Attribut
         + Contexte
         + Aide
```

Le résultat est comparé à un seuil :

| Seuil | Niveau de difficulté | Usage de référence |
|---:|---|---|
| 14 | Ordinaire sous pression | Fondamentaux accessibles |
| 18 | Délicat | Méthode ou attention réelle |
| 22 | Exigeant | Obstacle sérieux pour une personne entraînée |
| 26 | Ardu | Danger ou complexité élevée |
| 30 | Extrême | Exploit d’expert préparé |
| 34 | Prodigieux | Exploit exceptionnel, mais encore possible dans la fiction |

`⌊x⌋` signifie que l’on conserve la partie entière inférieure. Une compétence de 44 donne donc `⌊44 ÷ 5⌋ = 8`.

Avec un avantage, lancer `3d10` et garder les deux meilleurs dés. Avec un désavantage, garder les deux plus faibles. Plusieurs avantages ne créent pas de dés supplémentaires ; leurs autres causes peuvent modifier le contexte.

### Lecture de la marge

```text
Marge = Résultat − Seuil
```

| Marge | Résultat fictionnel |
|---:|---|
| `≤ −10` | Échec net : le danger ou l’opposition s’impose |
| `−9 à −1` | Échec constructif : progrès partiel, coût ou complication |
| `0 à 4` | Réussite fragile : objectif atteint avec exposition ou consommation |
| `5 à 9` | Réussite propre : objectif atteint sans coût inattendu |
| `≥ 10` | Réussite décisive : avantage supplémentaire cohérent |

Une marge ne permet jamais d’ignorer la géométrie, la matière, la causalité ou les limites d’une capacité.

## Ce que le jeu ne teste pas

Le système ne demande pas de jet pour :

- marcher dans une pièce calme ;
- employer correctement un objet familier sans pression ;
- obtenir une information que le personnage perçoit clairement ;
- recommencer indéfiniment une action sans coût ni opposition ;
- décider à la place du joueur d’une intention morale ;
- annuler un contact physique déjà constaté par la simulation.

Lorsqu’une action pourrait être accomplie automatiquement avec assez de temps, le jeu propose soit de la résoudre, soit d’exposer le temps et les ressources nécessaires. Le jet intervient si une urgence, une opposition ou une conséquence rend ce choix intéressant.

## Le rôle des attributs

Les sept attributs décrivent les moyens généraux du personnage. Ils vont normalement de 1 à 10.

| Attribut | Question posée |
|---|---|
| **Force** | Quelle puissance et quelle prise peut-il produire ? |
| **Dextérité** | Avec quelle précision coordonne-t-il son geste ? |
| **Constitution** | Combien de temps et de contrainte peut-il endurer ? |
| **Intelligence** | Comment analyse-t-il, apprend-il et conçoit-il ? |
| **Volonté** | Comment tient-il son intention sous pression ? |
| **Perception** | Que remarque-t-il et qu’anticipe-t-il ? |
| **Présence** | Quel impact produit-il sur autrui ? |

Une compétence indique ce qui est appris ; l’attribut indique comment la personne l’accomplit dans cette situation. Une même compétence peut parfois employer un second attribut si l’approche le justifie, mais jamais additionner deux attributs.

## Lire une règle normative

Les mots suivants ont un sens précis dans les chapitres techniques et l’annexe d’implémentation :

- **doit** : exigence nécessaire à la conformité ;
- **ne doit pas** : comportement interdit ;
- **devrait** : comportement de référence, modifiable seulement avec une raison documentée ;
- **peut** : option autorisée ;
- **référence** : valeur de base destinée à l’équilibrage ;
- **module** : contenu jouable et profil de règles ;
- **situation** : unité causale au sein de laquelle les répétitions sont évaluées ;
- **contact** : intersection physique confirmée entre volumes actifs ;
- **acquis** : capacité automatique débloquée par un palier de compétence ;
- **talent** : capacité choisie avec un point de talent ;
- **trace** : unité entière d’apprentissage d’une compétence.

## Trois façons de consulter ce manuel

### Pour créer rapidement un personnage

Lire la création, répartir les attributs, sélectionner douze compétences initiales, choisir un talent et terminer l’identité. Un parcours suggéré peut accélérer les choix, mais il n’est jamais une classe.

### Pour comprendre une action

Consulter la règle commune, puis le chapitre du domaine : combat, magie, exploration, interaction sociale, artisanat ou groupe. La fiche de la compétence précise ce qu’elle apprend et les acquis automatiques.

### Pour préparer l’implémentation

Lire l’annexe de contrat : ordre de résolution, règles d’arrondi, événements, identifiants, invariants et scénarios d’acceptation. Ces éléments sont conçus pour devenir des tests avant le code de production.

## Exemple d’ouverture : la passerelle du moulin

Le groupe doit franchir une cour occupée par deux gardes sans déclencher l’alarme. Une passerelle vermoulue relie les bâtiments ; sous elle tourne la roue du moulin.

Sira possède Discrétion 20, Dextérité 6 et obtient 11 sur `2d10`. Traverser les ombres sous surveillance impose 22. La pluie masque le bruit (`+2`), mais le sol est glissant (`−1`) : contexte net `+1`.

```text
11 + ⌊20 ÷ 5⌋ + 6 + 1 = 22
```

La marge vaut 0 : réussite fragile. Sira atteint la passerelle, mais une latte craque et augmente la suspicion. Le monde n’a pas oublié son passage.

Son compagnon aurait pu :

- attirer les gardes par une négociation crédible ;
- bloquer temporairement la roue avec Mécanismes ;
- lancer une pierre pour déplacer l’attention ;
- escalader le mur opposé ;
- traverser ouvertement en acceptant le combat ;
- reporter l’action pour étudier les rondes.

Chaque méthode mobilise d’autres compétences et crée d’autres conséquences. Aucune n’est réservée à une classe.

## Principes d’équilibrage à conserver

1. Un choix doit créer une possibilité ou une méthode, pas uniquement un nombre plus grand.
2. Un haut score rend l’usage plus fiable, mais ne rend pas le monde inconsistant.
3. Les ressources empêchent la répétition gratuite des techniques les plus puissantes.
4. Les contre-mesures sont perceptibles avant d’être punitives.
5. Les échecs proches peuvent enseigner ; les tâches triviales ne deviennent pas une ferme à progression.
6. Les récompenses ne dépendent ni du coup final, ni du nombre de victimes.
7. Une chaîne causale possède un initiateur, des contributeurs et des conséquences persistantes.
8. Les compagnons sont soumis aux mêmes règles de perception, d’apprentissage et de risque.
9. Un module adapte les valeurs par données versionnées, jamais par comportement caché lié à son nom.
10. Toute future implémentation commence par des exemples exécutables et un résultat attendu.
