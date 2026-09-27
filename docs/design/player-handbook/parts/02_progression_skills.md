<!-- handbook-part: progression-skills -->

# Personnage, progression et compétences

> **Portée des règles.** Les sept attributs, la progression par usage significatif, l’absence de croissance exponentielle et le principe selon lequel les talents ouvrent des possibilités proviennent du ruleset `openrpg-forge.systemic-rpg.v1`. Les valeurs, formules, paliers, compétences et acquis ci-dessous constituent la proposition détaillée du Manuel du joueur. Elles ne décrivent pas l’état actuel de l’implémentation du runtime Rust.

## Le personnage sans classe

Dans OpenRPG Forge, un personnage n’appartient jamais à une classe. Il n’existe ni profession mécanique, ni liste de compétences réservée, ni changement de classe à effectuer. Tout personnage possède les mêmes quarante-deux compétences. Ses actions, son équipement, ses rencontres, ses études et son entraînement dessinent progressivement son rôle.

Un rôle décrit le personnage ; il ne l’enferme pas.

- Une combattante lourdement protégée peut apprendre l’Escamotage.
- Un négociateur peut maîtriser les Armes d’impact.
- Une alchimiste peut devenir une excellente meneuse.
- Toute personne peut étudier la magie si elle trouve une source d’apprentissage compatible avec le monde du module.
- Changer de méthode ne demande ni changement de classe ni réinitialisation du personnage.

L’évolution suit cette chaîne :

```text
Action significative
        ↓
Traces d’apprentissage
        ↓
Point de compétence
        ↓
Palier de maîtrise
        ↓
Acquis automatique
        ↓
Jalons de parcours
        ↓
Niveau global sans classe
```

![Dans une cour en ruine, des voyageurs s’exercent avec un mentor, étudient une carte, soignent une blessure et examinent un mécanisme.](../../assets/player-handbook/progression-by-practice.png)

*Apprendre par la pratique — Le combat, l’étude, le soin et l’artisanat obéissent à la même logique : une difficulté significative, un retour compréhensible et du temps.*

## Créer son personnage

### 1. Définir une identité

Le joueur commence par une phrase qui décrit une personne, et non une fonction de combat.

Exemples :

- « Ancienne batelière qui recherche son frère disparu. »
- « Héritier ruiné ayant appris à survivre parmi ses créanciers. »
- « Garde-frontière fascinée par les phénomènes surnaturels. »
- « Artisan itinérant qui refuse de laisser une machine incomprise. »

Cette phrase n’accorde aucun privilège exclusif. Elle guide l’origine, les relations, les connaissances et les premières compétences.

### 2. Choisir une origine

L’origine décrit un milieu vécu : région, peuple, communauté, métier familial, institution, tradition ou événement historique.

Elle fournit :

- deux langues ou modes de communication ;
- une connaissance locale ;
- un contact ;
- une obligation, dette ou responsabilité ;
- une conviction ;
- un objet personnel ;
- deux ou trois contextes dans lesquels le vécu du personnage peut compter.

Une origine fluviale peut, par exemple, rendre immédiatement reconnaissables certains courants, usages portuaires ou signaux de batellerie. Elle ne donne pas un monopole sur la Natation ou le Négoce.

### 3. Répartir les attributs

Les attributs vont de **1 à 10**. À la création, le joueur répartit **40 points**, avec un minimum de **3** et un maximum de **8**.

La répartition standard est :

**7, 6, 6, 6, 5, 5, 5**

| Attribut | Ce qu’il représente | Exemples |
|---|---|---|
| **Force** | Puissance musculaire, prise, transfert d’impact | Soulever, pousser, frapper lourdement |
| **Dextérité** | Coordination, précision, finesse motrice | Viser, manipuler, esquiver |
| **Constitution** | Résistance, souffle et récupération | Endurer, nager longtemps, résister à un toxique |
| **Intelligence** | Analyse, mémoire structurée et invention | Comprendre un mécanisme, composer une formule |
| **Volonté** | Discipline, maîtrise de soi et résistance mentale | Maintenir un effet, surmonter la peur |
| **Perception** | Attention, lecture du monde et anticipation | Repérer, pister, lire une trajectoire |
| **Présence** | Impact social, expression et autorité | Convaincre, commander, impressionner |

Aucun attribut n’est réservé à un type de personnage et aucun ne doit devenir inutile hors de son domaine évident. La Force peut soutenir une intimidation crédible ; la Présence peut calmer un animal ; la Perception peut guider une parade.

### Ressources communes

Les modules utilisant le ruleset de référence emploient les mêmes noms pour les ressources fondamentales :

- la **Vitalité** mesure la capacité à rester en vie et à supporter les blessures ;
- l’**Endurance** alimente les efforts physiques intenses, le sprint, le blocage, l’esquive et les attaques lourdes ;
- le **Mana** alimente les formes magiques ordinaires ;
- l’**Équilibre** mesure la stabilité momentanée face aux chocs, aux parades, aux collisions et aux chutes ;
- la **Concentration** permet de maintenir une préparation, un sort, une visée ou une action complexe malgré les perturbations.

La Constitution contribue surtout à la Vitalité, à l’Endurance et à leur récupération. La Volonté contribue surtout au Mana et à la Concentration. Les valeurs exactes de ces ressources relèvent du chapitre consacré au combat et aux ressources ; la progression décrite ici ne les multiplie jamais automatiquement.

### 4. Répartir les compétences initiales

Chaque personnage inscrit les quarante-deux compétences sur sa feuille.

Pour une campagne standard :

- six compétences commencent à **10** ;
- six autres commencent à **5** ;
- l’une des six compétences à 10 reçoit **5 points supplémentaires** ;
- toutes les autres commencent à **0**.

Le joueur choisit librement ces compétences. Des parcours prédéfinis peuvent accélérer la création, mais ils ne constituent ni des classes ni des choix permanents.

Le départ standard produit six premiers jalons de parcours et place normalement le personnage au **niveau global 2**.

### 5. Choisir un talent initial

Le niveau global 2 accorde un premier point de talent. Ce point est dépensé dans le catalogue général des talents.

Les talents choisis sont distincts des **acquis de maîtrise** décrits avec chaque compétence :

- un acquis est automatique et gratuit lorsqu’un seuil de compétence est atteint ;
- un talent est choisi et payé avec un point de talent ;
- ni l’un ni l’autre n’est limité par une classe.

### 6. Finaliser le personnage

Le joueur choisit enfin :

- une apparence et des signes distinctifs ;
- un objectif immédiat ;
- une relation importante ;
- une peur ou une limite ;
- une réputation initiale ;
- un équipement cohérent avec son histoire ;
- une raison de rejoindre le groupe.

![Devant une forteresse claire en plein jour, des voyageurs négocient, étudient une herse, déchargent un chariot et s’entraînent.](../../assets/player-handbook/bright-fortress-life.png)

*Une identité ouverte — La cité offre des occasions de parler, de comprendre, de travailler ou de s’exercer ; aucun uniforme ne réserve ces actions à une classe.*

## Résoudre une action incertaine

Une action n’appelle un test que si son résultat est incertain et si l’échec peut modifier la situation.

```text
Résultat =
2d10
+ ⌊compétence ÷ 5⌋
+ attribut
+ contexte
+ aide
```

Le résultat est comparé à une difficulté.

| Difficulté | Seuil | Exemple général |
|---|---:|---|
| Ordinaire | 14 | Action familière sous faible pression |
| Délicate | 18 | Action demandant méthode ou attention |
| Exigeante | 22 | Obstacle sérieux pour une personne entraînée |
| Ardue | 26 | Situation dangereuse ou techniquement complexe |
| Extrême | 30 | Exploit réservé aux experts préparés |
| Prodigieuse | 34 | Action exceptionnelle réunissant maîtrise et circonstances favorables |

Le **contexte** vaut habituellement de –6 à +6. Il représente notamment la qualité des outils, la position, la lumière, les preuves disponibles, la préparation, l’état physique et l’exécution réelle du joueur.

L’**aide** vaut de 0 à +3. Un assistant doit expliquer comment il contribue et partager les risques éventuels.

Lorsqu’une compétence possède deux attributs associés, le premier correspond à l’usage habituel. Le second peut être employé si l’approche décrite le justifie. Les deux ne sont jamais additionnés.

### Avantage et désavantage

Avec un avantage, le joueur lance **3d10 et conserve les deux meilleurs dés**.

Avec un désavantage, il lance **3d10 et conserve les deux moins bons**.

Un avantage et un désavantage s’annulent. Plusieurs avantages ne s’empilent pas : les autres circonstances favorables modifient le contexte.

### Qualité du résultat

| Marge | Conséquence |
|---:|---|
| –10 ou moins | Échec net ; le danger ou l’opposition s’impose |
| –9 à –1 | Échec constructif ; progrès partiel, coût ou nouvelle complication |
| 0 à 4 | Réussite fragile ; objectif atteint avec exposition ou consommation |
| 5 à 9 | Réussite propre |
| 10 ou plus | Réussite décisive ; vitesse, discrétion ou avantage supplémentaire cohérent |

Un résultat élevé n’autorise jamais un effet physiquement impossible sans capacité explicite.

### Actions opposées

Chaque camp calcule son résultat. La meilleure marge l’emporte. Une égalité conserve la situation actuelle ou impose un compromis : deux prises restent verrouillées, un mensonge entretient le doute, deux combattants rompent le contact.

### Contact physique et statistiques

Un contact visible reste un contact. Si une lame traverse réellement le volume d’une cible, le système ne transforme pas ce fait en « raté » abstrait.

La compétence, l’attribut, l’angle, la vitesse, la protection, l’Équilibre et l’état de la cible déterminent plutôt :

- la qualité du contact ;
- la pénétration ;
- le transfert d’impact ;
- la dépense d’Endurance ;
- le contrôle de l’arme ;
- l’interruption ou la perte d’Équilibre ;
- la possibilité d’une conséquence critique.

## Progression par la pratique

Toute action peut être observée par le système, mais toute répétition ne produit pas un apprentissage. Une compétence progresse lorsqu’elle est utilisée face à une difficulté, un risque, une nouveauté ou un retour instructif.

La victoire n’est pas nécessaire. Un échec proche de la réussite peut apprendre davantage qu’un succès routinier.

### Traces d’apprentissage

Une situation significative accorde de 0 à 5 traces à la compétence principale.

Pour estimer le défi, on calcule le résultat moyen du personnage :

```text
Résultat moyen =
11
+ ⌊compétence ÷ 5⌋
+ attribut
+ contexte
+ aide
```

Puis on compare la difficulté à ce résultat moyen.

| Écart entre difficulté et résultat moyen | Traces de base |
|---|---:|
| Difficulté inférieure d’au moins 8 | 0 |
| Difficulté inférieure de 1 à 7 | 1 |
| Difficulté égale ou supérieure de 1 à 4 | 2 |
| Difficulté supérieure de 5 à 8 | 3 |
| Difficulté supérieure de 9 ou plus | 0, ou 1 si l’essai était plausible et a fourni un véritable retour |

Modificateurs possibles :

- **+1** si la situation ou la méthode est nouvelle ;
- **+1** si l’action comporte un enjeu ou un risque réel ;
- **+1** si un mentor, une analyse ou un retour précis permet de comprendre le résultat ;
- maximum **5 traces** par compétence et par situation.

Le défi est calculé avec la meilleure méthode raisonnablement disponible. Abandonner volontairement son outil ou se bander les yeux ne transforme pas une tâche banale en entraînement valable, sauf si cette contrainte répond à un besoin réel.

### Coût du prochain point

Pour faire passer une compétence de sa valeur actuelle `S` à `S + 1`, il faut :

```text
Traces requises =
5
+ ⌊S ÷ 5⌋
+ ⌊S² ÷ 400⌋
```

| Score actuel | Traces pour le point suivant |
|---:|---:|
| 0 | 5 |
| 20 | 10 |
| 40 | 17 |
| 60 | 26 |
| 80 | 37 |
| 99 | 48 |

Les premiers progrès sont rapides. Une véritable maîtrise exige des problèmes plus rares, plus variés et plus difficiles.

Les traces excédentaires sont conservées. Une compétence ne dépasse jamais 100.

### Règles anti-répétition

- Une même intention appliquée au même obstacle ne produit qu’un événement d’apprentissage.
- Une chaîne causale ne rapporte pas une récompense par victime ou par objet. Renverser une urne, l’enflammer et brûler cinq adversaires reste une seule chaîne.
- Répéter exactement la même méthode rapporte la moitié des traces la deuxième fois, puis aucune trace.
- Un simple repos ne rend pas une répétition à nouveau instructive. Il faut une nouvelle contrainte, un autre adversaire, un matériau différent ou une méthode réellement modifiée.
- Une cible consentante et sans danger ne permet pas de progresser indéfiniment.
- Les mannequins et exercices contrôlés ne suffisent plus seuls au-delà de 25.
- Le coup final n’a aucune importance. La causalité et la contribution réelle déterminent qui apprend.
- Une action déléguée fait progresser son exécutant. Donner un ordre pertinent peut en parallèle faire progresser Commandement ou Tactique.
- Une situation peut faire progresser une compétence principale et une compétence de soutien. La seconde reçoit au maximum la moitié des traces si elle a réellement changé l’issue.

### Exemple : le brasero suspendu

Sava coupe la corde d’un brasero avec une flèche, provoque sa chute sur une nappe d’huile et demande à un compagnon de repousser un ennemi vers le feu.

La scène peut produire :

- des traces de **Tir**, pour la corde atteinte sous pression ;
- des traces de **Tactique**, si le placement et l’ordre ont été déterminants ;
- des traces d’**Élémentalisme** pour le personnage qui a contrôlé l’allumage ;
- des traces de **Commandement** pour l’ordre correctement synchronisé.

Elle ne produit pas une récompense de Tir ou d’Élémentalisme pour chaque ennemi touché.

## Paliers de maîtrise

| Score | Palier | Signification |
|---:|---|---|
| 0–9 | Novice | Connaissance rudimentaire ou intuition |
| 10–24 | Initié | Gestes fondamentaux fiables |
| 25–44 | Pratiquant | Autonomie dans les situations courantes |
| 45–64 | Confirmé | Adaptation à la pression et aux imprévus |
| 65–84 | Expert | Contrôle de situations rares ou complexes |
| 85–100 | Maître | Création de méthodes et exploits exceptionnels |

Chaque compétence accorde automatiquement un acquis aux scores **10, 25, 45, 65 et 85**.

Les acquis :

- ne coûtent aucun point de talent ;
- ne sont jamais limités par une origine ;
- ouvrent des actions ou modifient leur usage ;
- ne garantissent pas un résultat impossible ;
- restent soumis aux ressources, à la géométrie et au contre-jeu.

Pour franchir certains paliers, le personnage doit avoir rencontré au moins une situation adaptée :

| Palier visé | Preuve minimale |
|---:|---|
| 25 | Une situation délicate, difficulté 18 |
| 45 | Une situation exigeante, difficulté 22 |
| 65 | Une situation ardue, difficulté 26 |
| 85 | Une situation extrême, difficulté 30 |

Les traces continuent de s’accumuler si cette preuve manque. Un mentor peut organiser l’épreuve, mais ne peut pas la remplacer par une simple dépense d’argent.

## Niveau global sans classe

Le niveau global est un résumé du parcours, pas une profession. Il n’accorde aucun monopole, ne détermine pas les compétences accessibles et ne provoque pas l’adaptation automatique du monde.

### Jalons de parcours

Une compétence rapporte des jalons la première fois qu’elle atteint chaque palier :

| Score atteint | Jalons gagnés |
|---:|---:|
| 10 | +1 |
| 25 | +2 |
| 45 | +3 |
| 65 | +4 |
| 85 | +5 |

Une compétence peut donc produire au maximum 15 jalons. Les jalons ne sont jamais perdus.

### Calcul du niveau

Le niveau va de 1 à 20. Pour atteindre le niveau `N`, il faut :

```text
Jalons requis = N² + 3N – 4
```

| Niveau | Jalons | Niveau | Jalons |
|---:|---:|---:|---:|
| 1 | 0 | 11 | 150 |
| 2 | 6 | 12 | 176 |
| 3 | 14 | 13 | 204 |
| 4 | 24 | 14 | 234 |
| 5 | 36 | 15 | 266 |
| 6 | 50 | 16 | 300 |
| 7 | 66 | 17 | 336 |
| 8 | 84 | 18 | 374 |
| 9 | 104 | 19 | 414 |
| 10 | 126 | 20 | 456 |

Cette formule récompense à la fois la spécialisation et la polyvalence. Une seule compétence ne peut pas porter tout le niveau du personnage.

### Récompenses de niveau

- Chaque niveau pair accorde **1 point de talent**.
- Les niveaux 4, 8, 12, 16 et 20 accordent **+1 à un attribut**, sans dépasser 10.
- Les talents et augmentations d’attribut sont choisis pendant un repos long.
- Aucun niveau n’accorde automatiquement une multiplication de Vitalité ou de dégâts.
- Aucun équipement, compétence ou école magique n’est verrouillé par le niveau global.
- Le monde ne met pas à niveau ses habitants pour suivre le héros.

## Entraînement, étude et mentors

La pratique réelle est la voie principale, mais pas l’unique voie d’apprentissage.

### Entraînement autonome

Un bloc d’entraînement demande généralement quatre heures, un lieu adapté et le matériel nécessaire.

- Il accorde de 1 à 3 traces selon la qualité de l’exercice.
- Il ne permet pas seul de dépasser 25.
- Un exercice déjà parfaitement connu cesse de produire des traces.
- Un nouvel exercice, une contrainte mesurée ou une analyse enregistrée peut renouveler l’apprentissage.
- L’épuisement, la blessure ou un lieu dangereux réduisent la qualité du bloc.

### Livres, plans et enregistrements

Une source possède un domaine et une qualité maximale.

- Une introduction peut enseigner jusqu’à 25.
- Un traité spécialisé peut accompagner jusqu’à 45.
- Une œuvre de référence peut accompagner jusqu’à 65.
- Les savoirs supérieurs exigent généralement plusieurs sources, un mentor ou une expérience directe.
- Relire la même source ne redonne pas les traces initiales.
- Une source peut révéler une technique sans donner immédiatement la maîtrise nécessaire pour l’exécuter.

### Mentors

Un mentor efficace possède normalement au moins 10 points de plus que l’élève dans la compétence enseignée.

Une séance de qualité :

- dure de deux à quatre heures ;
- rapporte de 2 à 5 traces ;
- fournit un retour précis ;
- peut préparer une épreuve de palier ;
- peut donner un avantage au prochain exercice correspondant.

Un mentor ne peut enseigner au-delà de sa propre compréhension. À partir de 65, l’apprentissage prend souvent la forme d’une collaboration, d’une mission ou d’un problème inédit plutôt que d’une leçon magistrale.

La relation avec le mentor compte : disponibilité, confiance, prix, devoir, rivalité ou désaccord moral peuvent devenir des éléments de jeu.

### Repos et consolidation

Un repos ne crée pas de progression par lui-même.

- Les traces sont enregistrées immédiatement.
- Les points et acquis obtenus sont consolidés lorsque le personnage sort du danger, généralement à la fin d’une scène ou pendant un repos court.
- Un repos court permet de revoir une action, réparer un outil ou recevoir un retour bref.
- Un repos long permet de dépenser les points de talent et les augmentations d’attribut.
- Dormir plusieurs fois sans nouvelle expérience ne produit aucune trace.
- Le temps consacré à l’étude avance réellement le calendrier du monde.

## Catalogue universel des compétences

Les quarante-deux compétences sont accessibles à tous. Les acquis listés aux seuils 10, 25, 45, 65 et 85 sont automatiques et gratuits.

### Mouvement et exploration

#### 1. Athlétisme (`athletics`)

**Attributs :** Force ; Constitution pour l’effort prolongé.  
**Usages :** course, saut, poussée en mouvement, portage, franchissement dynamique.  
**Difficultés :** D14 pour franchir une barrière basse en courant ; D26 pour bondir entre deux appuis instables sous le feu ennemi.  
**Progresse lorsque :** le personnage dépasse une distance, une charge, un rythme ou un obstacle significatif.

**Acquis automatiques :**

- **10 — Appui sûr :** franchir les petits obstacles dans l’élan ne demande plus d’arrêt.
- **25 — Élan porté :** une course peut alimenter directement un saut, une poussée ou un impact.
- **45 — Second souffle :** une brève baisse de rythme permet de reprendre un effort interrompu sans repartir de zéro.
- **65 — Mouvement composé :** courir, sauter et se rétablir forment une seule séquence fluide.
- **85 — Effort souverain :** le personnage peut tenter des exploits de puissance mobile normalement hors d’atteinte, sans ignorer masse ni gravité.

#### 2. Acrobatie (`acrobatics`)

**Attributs :** Dextérité ; Perception pour anticiper une trajectoire.  
**Usages :** équilibre, roulade, réception, passage étroit, récupération après perte d’Équilibre.  
**Difficultés :** D18 pour traverser une poutre humide ; D30 pour orienter une chute entre plusieurs obstacles mobiles.  
**Progresse lorsque :** l’Équilibre, l’orientation ou la réception ont une conséquence réelle.

**Acquis automatiques :**

- **10 — Roulé amorti :** une réception contrôlée réduit l’impact d’une petite chute.
- **25 — Équilibre mobile :** se déplacer prudemment sur un appui étroit ne force plus l’immobilité.
- **45 — Redressement vif :** se relever après une roulade ou une chute légère conserve l’élan.
- **65 — Passage impossible :** le personnage peut exploiter des appuis très courts ou irréguliers.
- **85 — Chute orientée :** il peut choisir une trajectoire de réception plausible, sans annuler la dangerosité d’une hauteur extrême.

#### 3. Escalade (`climbing`)

**Attributs :** Force ; Dextérité lorsque la précision des prises domine.  
**Usages :** parois, cordes, prises artificielles, traversées, sécurisation du groupe.  
**Difficultés :** D14 pour une échelle endommagée ; D30 pour une paroi exposée, glacée et parcourue par des projectiles.  
**Progresse lorsque :** la voie est nouvelle, dangereuse, limitée par le temps ou compliquée par une charge.

**Acquis automatiques :**

- **10 — Trois points :** les escalades simples deviennent stables tant que trois appuis sont disponibles.
- **25 — Traverse :** le personnage se déplace latéralement sans devoir redescendre.
- **45 — Reprise de prise :** une glissade peut devenir une suspension plutôt qu’une chute immédiate.
- **65 — Combat suspendu :** une interaction brève ou une défense devient possible depuis une prise solide.
- **85 — Ouvreur de voie :** le personnage peut installer un itinéraire qui facilite réellement le passage du groupe.

#### 4. Natation (`swimming`)

**Attributs :** Constitution ; Force contre un courant ou avec une charge.  
**Usages :** nage, plongée, sauvetage, gestion du courant, déplacement en équipement.  
**Difficultés :** D18 pour traverser une rivière froide ; D30 pour secourir une personne en armure dans une eau souterraine agitée.  
**Progresse lorsque :** courant, froid, profondeur, souffle, visibilité ou charge créent une difficulté.

**Acquis automatiques :**

- **10 — Souffle réglé :** le personnage gère correctement une immersion prévue.
- **25 — Flottaison active :** il peut se délester ou stabiliser une personne sans paniquer.
- **45 — Lecture du courant :** il repère les veines d’eau, remous et sorties praticables.
- **65 — Action aquatique :** utiliser un outil ou se défendre dans l’eau n’annule plus tout déplacement.
- **85 — Sauveteur des profondeurs :** il peut organiser et conduire une extraction complexe sous l’eau.

#### 5. Survie (`survival`)

**Attributs :** Perception ; Intelligence pour la planification.  
**Usages :** bivouac, orientation, météo, nourriture, feu, gestion d’une expédition.  
**Difficultés :** D14 pour préparer un camp par temps stable ; D26 pour garder un groupe opérationnel lors d’une tempête prolongée.  
**Progresse lorsque :** le personnage affronte un milieu, une saison, une ressource ou un danger nouveau.

**Acquis automatiques :**

- **10 — Bivouac fiable :** un camp simple protège correctement des conditions ordinaires.
- **25 — Ressources discrètes :** le personnage repère eau, combustible et nourriture sans épuiser immédiatement le lieu.
- **45 — Lecteur du ciel :** il anticipe les changements météorologiques proches.
- **65 — Route préservée :** il organise une progression limitant fatigue, traces et consommation.
- **85 — Maître d’expédition :** il peut maintenir un groupe dans un environnement extrême si des solutions physiques existent.

#### 6. Discrétion (`stealth`)

**Attributs :** Dextérité ; Perception pour exploiter lumière et rythmes.  
**Usages :** déplacement silencieux, dissimulation, approche, rupture de contact, gestion des traces immédiates.  
**Difficultés :** D18 pour traverser une pièce surveillée mais bruyante ; D30 pour rompre une recherche coordonnée sans magie.  
**Progresse lorsque :** un observateur compétent peut réellement découvrir le personnage.

**Acquis automatiques :**

- **10 — Pas mesuré :** le personnage adapte naturellement son allure au sol.
- **25 — Profil brisé :** il exploite les volumes, ombres et mouvements du décor pour réduire sa silhouette.
- **45 — Diversion silencieuse :** il peut créer une petite cause crédible de distraction sans révéler sa position.
- **65 — Passage dans l’alerte :** il sait se déplacer pendant une investigation active.
- **85 — Disparition plausible :** après avoir réellement rompu les perceptions ennemies, il peut pousser les poursuivants vers une fausse hypothèse.

### Combat

#### 7. Lames (`blades`)

**Attributs :** Dextérité ; Force pour les armes lourdes ou les coupes puissantes.  
**Usages :** armes tranchantes ou perforantes tenues en main, contrôle de ligne, coupe ciblée.  
**Difficultés :** D18 pour couper une sangle mobile ; D30 pour atteindre un défaut d’armure pendant un échange.  
**Progresse lorsque :** l’adversaire, la protection, l’espace ou la cible demandent une adaptation réelle.

**Acquis automatiques :**

- **10 — Changement de ligne :** passer d’une coupe à une pointe ne rompt plus la garde.
- **25 — Coupe utilitaire :** cordes, sangles et éléments fins deviennent des cibles de combat explicites.
- **45 — Riposte ciblée :** une ouverture permet de viser une main, une attache ou une zone exposée.
- **65 — Liaison de lame :** le personnage peut engager l’arme adverse pour déplacer sa ligne ou préparer un désarmement.
- **85 — Géométrie du duel :** il contrôle distance, angle et partie active de la lame avec une précision exceptionnelle.

#### 8. Armes d’impact (`impact_weapons`)

**Attributs :** Force ; Constitution pour absorber les retours de choc.  
**Usages :** masses, marteaux, gourdins, outils lourds, destruction et rupture de garde.  
**Difficultés :** D18 pour déformer une fixation ; D30 pour transmettre un choc précis à travers une protection articulée.  
**Progresse lorsque :** la masse, le matériau, la garde ou le retour d’impact exigent un vrai contrôle.

**Acquis automatiques :**

- **10 — Transfert d’impact :** le personnage distingue frappe de déplacement et frappe de rupture.
- **25 — Brise-garde :** un impact préparé peut viser l’Équilibre plutôt que les blessures.
- **45 — Retour maîtrisé :** le rebond d’une arme lourde ne laisse plus automatiquement une ouverture.
- **65 — Onde dans l’armure :** le personnage sait transmettre un choc sans prétendre traverser une protection intacte.
- **85 — Démolition contrôlée :** il peut frapper une structure pour provoquer une rupture choisie plutôt qu’un effondrement aveugle.

#### 9. Armes d’hast (`polearms`)

**Attributs :** Dextérité ; Perception pour la distance et les trajectoires.  
**Usages :** lances, piques, bâtons longs, crochets, maintien de distance.  
**Difficultés :** D18 pour arrêter une charge lisible ; D30 pour contrôler plusieurs lignes d’approche dans un passage.  
**Progresse lorsque :** portée, espace, vitesse ennemie ou coordination de groupe déterminent le résultat.

**Acquis automatiques :**

- **10 — Mesure :** le personnage lit naturellement sa portée utile.
- **25 — Crochet :** il peut tirer un membre, une arme ou un objet compatible.
- **45 — Réception :** une préparation visible permet d’opposer la structure de l’arme à une charge.
- **65 — Hampe courte :** il sait réduire sa prise pour combattre dans un espace resserré.
- **85 — Axe interdit :** il peut fermer une ligne de passage et forcer l’adversaire à la traiter comme un danger physique.

#### 10. Tir (`marksmanship`)

**Attributs :** Dextérité ; Perception pour lire distance et mouvement.  
**Usages :** arcs, arbalètes, frondes et armes de trait autorisées par le module.  
**Difficultés :** D18 pour atteindre une cible mobile découverte ; D30 pour couper une corde oscillante dans une fumée partielle.  
**Progresse lorsque :** distance, mouvement, couverture, vent ou choix de point d’impact sont significatifs.

**Acquis automatiques :**

- **10 — Souffle de visée :** une courte préparation stabilise le tir.
- **25 — Avance :** le personnage sait viser l’endroit où une cible mobile va se trouver.
- **45 — Tir d’interaction :** cordes, leviers, fioles et attaches deviennent des cibles maîtrisables.
- **65 — Reprise de cible :** une munition préparée peut suivre rapidement la première sans perdre toute lecture de la scène.
- **85 — Fenêtre infime :** le personnage peut exploiter une ouverture extrêmement brève si la trajectoire existe réellement.

#### 11. Lancer (`throwing`)

**Attributs :** Dextérité ; Force pour la masse et la portée.  
**Usages :** couteaux, javelots, fioles, pierres, objets improvisés et charges.  
**Difficultés :** D14 pour lancer une fiole sur une surface proche ; D26 pour placer un objet lourd sur une zone réduite depuis une hauteur.  
**Progresse lorsque :** l’objet, la trajectoire, le mouvement ou la conséquence demandent une adaptation.

**Acquis automatiques :**

- **10 — Prise d’équilibre :** le personnage identifie rapidement le centre d’un objet lançable.
- **25 — Arc choisi :** il peut privilégier une trajectoire tendue, plongeante ou indirecte.
- **45 — Arme improvisée :** un objet compatible peut être lancé sans préparation spécialisée.
- **65 — Lancer mobile :** courir ou chuter n’interdit plus un lancer contrôlé.
- **85 — Déclencheur lointain :** il peut employer un lancer pour initier précisément une chaîne environnementale complexe.

#### 12. Corps à corps (`unarmed`)

**Attributs :** Force ; Dextérité pour les prises et redirections.  
**Usages :** frappes, lutte, saisie, immobilisation, dégagement et utilisation de la masse adverse.  
**Difficultés :** D18 pour sortir d’une prise ; D30 pour retourner un adversaire plus lourd déjà déséquilibré.  
**Progresse lorsque :** l’opposition résiste réellement et que taille, posture ou espace comptent.

**Acquis automatiques :**

- **10 — Garde corporelle :** le personnage protège naturellement tête, appuis et centre.
- **25 — Corps à corps lié :** il peut passer d’une frappe à une saisie sur un contact favorable.
- **45 — Redirection :** l’élan adverse peut être déplacé plutôt que bloqué frontalement.
- **65 — Contrôle articulaire :** une cible déjà dominée peut être immobilisée ou désarmée.
- **85 — Levier vivant :** le personnage emploie appuis et masse adverse pour agir sur des cibles plus lourdes, sans annuler leur avantage physique.

#### 13. Boucliers (`shields`)

**Attributs :** Constitution ; Force pour absorber et repousser.  
**Usages :** couverture, blocage, poussée, protection d’un allié et déplacement sous attaque.  
**Difficultés :** D18 pour protéger simultanément son flanc et un allié ; D30 pour avancer sous plusieurs tirs lourds.  
**Progresse lorsque :** l’angle, l’impact, l’Endurance ou la personne protégée imposent un choix.

**Acquis automatiques :**

- **10 — Angle de couverture :** le bouclier protège selon son volume réel sans masquer inutilement la vision.
- **25 — Heurt de bord :** un blocage peut devenir une poussée courte.
- **45 — Interposition :** le personnage peut couvrir un allié proche avec un déplacement lisible.
- **65 — Déviation :** un projectile compatible peut être redirigé plutôt que seulement arrêté.
- **85 — Bastion mobile :** le personnage coordonne déplacement, couverture et groupe sans devenir invulnérable.

#### 14. Parade (`parry`)

**Attributs :** Dextérité ; Perception pour lire l’attaque.  
**Usages :** déviation, ouverture, contrôle du tempo et riposte.  
**Difficultés :** D18 contre une attaque clairement annoncée ; D30 contre une séquence rapide provenant de lignes différentes.  
**Progresse lorsque :** le timing, l’arme adverse et le risque de l’échec sont réels.

**Acquis automatiques :**

- **10 — Déviation :** une parade réussie écarte l’attaque au lieu de l’arrêter de force.
- **25 — Fenêtre :** une parade précise crée une ouverture immédiatement lisible.
- **45 — Main ouverte :** une parade parfaite peut préparer un désarmement.
- **65 — Suite brisée :** le personnage peut interrompre l’enchaînement prévu d’un adversaire.
- **85 — Temps emprunté :** une attaque lourde correctement lue peut être redirigée vers un obstacle ou une autre ligne plausible.

#### 15. Armures (`armor`)

**Attributs :** Constitution ; Force pour les protections massives.  
**Usages :** ajustement, déplacement protégé, gestion du poids, exploitation des matériaux.  
**Difficultés :** D14 pour régler correctement une armure connue ; D26 pour combattre longtemps dans une protection endommagée et chauffée.  
**Progresse lorsque :** poids, chaleur, bruit, mobilité ou dommage influencent réellement la scène.

**Acquis automatiques :**

- **10 — Ajustement :** une armure entretenue ne crée pas de gêne évitable.
- **25 — Réception protégée :** le personnage sait présenter les zones les mieux couvertes à un impact.
- **45 — Mobilité blindée :** il exploite les articulations de l’armure pour franchir, s’accroupir et se relever.
- **65 — Couches adaptées :** il peut reconfigurer une protection selon chaleur, bruit ou type de menace.
- **85 — Corps fortifié :** il transforme sa protection en outil de déplacement et d’interposition sans ignorer fatigue ni points faibles.

#### 16. Tactique (`tactics`)

**Attributs :** Intelligence ; Perception pour lire le terrain.  
**Usages :** placement, priorités, formation, contre-jeu et plans conditionnels.  
**Difficultés :** D18 pour organiser une embuscade simple ; D30 pour réviser un plan lorsque terrain, ennemis et objectifs changent simultanément.  
**Progresse lorsque :** un choix de plan modifie l’issue d’une opposition réelle.

**Acquis automatiques :**

- **10 — Lecture de menace :** le personnage identifie rapidement le danger immédiat le plus évident.
- **25 — Cible commune :** il peut désigner une priorité comprise par le groupe.
- **45 — Ordre composé :** il prépare deux actions liées par une condition simple.
- **65 — Contingence :** un plan peut inclure une réponse de repli, de substitution ou de retournement.
- **85 — Chef d’orchestre :** il coordonne plusieurs rôles autour d’une intention commune sans contrôler directement chaque geste.

### Technique et savoir

#### 17. Artisanat (`crafting`)

**Attributs :** Intelligence ; Dextérité pour l’assemblage fin.  
**Usages :** fabrication générale, réparation, assemblage, adaptation d’outils et récupération.  
**Difficultés :** D14 pour réparer un objet simple ; D26 pour fabriquer sur le terrain un dispositif soumis à une forte contrainte.  
**Progresse lorsque :** le matériau, la fonction ou les moyens disponibles posent un problème nouveau.

**Acquis automatiques :**

- **10 — Entretien :** les réparations ordinaires consomment moins de pièces inutiles.
- **25 — Récupération :** le personnage extrait des composants utilisables sans détruire systématiquement l’objet.
- **45 — Assemblage modulaire :** il combine des éléments compatibles pour une fonction temporaire.
- **65 — Fabrication de terrain :** un atelier incomplet peut suffire à une réalisation exigeante.
- **85 — Conception originale :** il peut créer un objet nouveau dont les propriétés découlent réellement des matériaux et du plan.

#### 18. Forge (`smithing`)

**Attributs :** Intelligence ; Force pour le travail de masse.  
**Usages :** métal, armes, armures, trempe, équilibrage et réparations structurelles.  
**Difficultés :** D18 pour reprendre un tranchant endommagé ; D30 pour réaliser une pièce composite aux contraintes contradictoires.  
**Progresse lorsque :** alliage, température, forme ou usage final exigent une décision technique.

**Acquis automatiques :**

- **10 — Fil entretenu :** le personnage restaure correctement un bord ou une pointe.
- **25 — Trempe choisie :** il adapte dureté et souplesse à l’usage prévu.
- **45 — Rééquilibrage :** il modifie la répartition de masse d’une arme ou d’un outil.
- **65 — Ouvrage composite :** plusieurs métaux ou pièces peuvent former un ensemble cohérent.
- **85 — Pièce maîtresse :** il crée une œuvre singulière dont les qualités viennent de sa construction, pas d’un bonus abstrait.

#### 19. Mécanismes (`mechanisms`)

**Attributs :** Intelligence ; Dextérité pour intervenir dans un espace réduit.  
**Usages :** engrenages, leviers, alimentation, machines, dispositifs et sabotage.  
**Difficultés :** D18 pour bloquer une roue sans casser l’ensemble ; D30 pour détourner une machine active inconnue.  
**Progresse lorsque :** le personnage comprend ou modifie un fonctionnement réel.

**Acquis automatiques :**

- **10 — Lecture du mouvement :** les transmissions et points de contrainte deviennent identifiables.
- **25 — Contournement sûr :** il peut neutraliser une fonction sans arrêter tout le système.
- **45 — Fonction inversée :** un mécanisme compatible peut servir à l’effet opposé ou à un nouvel usage.
- **65 — Liaison distante :** il relie plusieurs commandes par câble, pression ou transmission disponible.
- **85 — Prise de système :** il peut reconfigurer un ensemble complexe après en avoir compris les dépendances.

#### 20. Alchimie (`alchemy`)

**Attributs :** Intelligence ; Perception pour observer les réactions.  
**Usages :** extraction, mélange, potions, toxiques, combustibles, solvants et surfaces.  
**Difficultés :** D18 pour stabiliser une préparation connue ; D30 pour concevoir un catalyseur sous contraintes inconnues.  
**Progresse lorsque :** une propriété, un dosage, un support ou une réaction apporte une information nouvelle.

**Acquis automatiques :**

- **10 — Lecture de propriété :** le personnage reconnaît les familles d’effets les plus manifestes.
- **25 — Extraction stable :** il isole une propriété simple sans perdre tout l’ingrédient.
- **45 — Mélange de surface :** il conçoit des substances destinées au sol, à l’air ou à un objet.
- **65 — Catalyse contrôlée :** il accélère ou retarde une réaction avec un déclencheur explicite.
- **85 — Formule nouvelle :** il peut rechercher une préparation inédite et en documenter risques, contre-mesures et fabrication.

#### 21. Pièges (`traps`)

**Attributs :** Intelligence ; Dextérité pour la pose et le désamorçage.  
**Usages :** déclencheurs, charges, fils, surfaces, obstacles et dispositifs d’alerte.  
**Difficultés :** D18 pour désamorcer un piège connu ; D30 pour intégrer plusieurs réactions à un lieu vivant sans piéger le groupe.  
**Progresse lorsque :** déclenchement, cible, dissimulation ou environnement exigent une vraie conception.

**Acquis automatiques :**

- **10 — Déclencheur sûr :** le personnage sait armer et neutraliser ses propres dispositifs.
- **25 — Piège improvisé :** il construit une alerte ou entrave simple avec le décor.
- **45 — Réaction en chaîne :** plusieurs éléments peuvent se déclencher dans un ordre choisi.
- **65 — Sélection de cible :** le déclencheur distingue une masse, une direction ou une condition physique.
- **85 — Terrain préparé :** le personnage transforme une zone entière en problème tactique lisible et contournable.

#### 22. Médecine (`medicine`)

**Attributs :** Intelligence ; Perception pour le diagnostic.  
**Usages :** premiers secours, blessures, maladies, toxiques, chirurgie et triage.  
**Difficultés :** D14 pour traiter une plaie propre ; D30 pour stabiliser plusieurs blessés pendant une catastrophe.  
**Progresse lorsque :** le diagnostic ou le traitement comporte une incertitude et un enjeu médical réel.

**Acquis automatiques :**

- **10 — Premier geste :** le personnage arrête correctement les détériorations ordinaires de Vitalité.
- **25 — Diagnostic différentiel :** il distingue plusieurs causes possibles au lieu de traiter seulement le symptôme.
- **45 — Stabilisation critique :** une personne proche de la mort peut être maintenue jusqu’à un traitement adapté.
- **65 — Intervention lourde :** chirurgie, extraction et traitement complexe deviennent possibles avec un environnement adéquat.
- **85 — Triage de crise :** il organise simultanément priorités, assistants et ressources face à de nombreuses victimes.

#### 23. Érudition (`lore`)

**Attributs :** Intelligence ; Volonté pour l’étude longue et rigoureuse.  
**Usages :** histoire, langues, religions, créatures, cultures, textes et traditions.  
**Difficultés :** D14 pour rappeler un fait connu ; D30 pour relier plusieurs sources lacunaires à un phénomène oublié.  
**Progresse lorsque :** une source nouvelle est comprise, comparée ou vérifiée dans le monde.

**Acquis automatiques :**

- **10 — Source reconnue :** le personnage distingue récit populaire, témoignage et document.
- **25 — Traditions croisées :** il compare deux versions sans les réduire à une seule.
- **45 — Déchiffrement contextuel :** une langue ou notation partielle peut être reconstruite avec assez d’indices.
- **65 — Savoir déduit :** il formule une hypothèse exploitable à partir de connaissances séparées.
- **85 — Thèse fondatrice :** il peut établir un savoir nouveau, transmissible et contestable.

#### 24. Investigation (`investigation`)

**Attributs :** Perception ; Intelligence pour reconstruire les causes.  
**Usages :** scènes, témoignages, chronologie, preuves, contradictions et causalité.  
**Difficultés :** D18 pour reconstituer une intrusion récente ; D30 pour démêler une affaire ancienne volontairement falsifiée.  
**Progresse lorsque :** le personnage met à l’épreuve une hypothèse plutôt que fouiller mécaniquement.

**Acquis automatiques :**

- **10 — Scène préservée :** le personnage évite de détruire les indices les plus évidents.
- **25 — Suite des faits :** il reconstitue un ordre probable d’événements.
- **45 — Témoignages croisés :** il distingue erreur, omission et contradiction utile.
- **65 — Cause cachée :** une chaîne indirecte peut être attribuée à son véritable initiateur.
- **85 — Affaire froide :** il sait reprendre une enquête malgré disparition, contamination ou manipulation des preuves.

### Relations et influence

#### 25. Persuasion (`persuasion`)

**Attributs :** Présence ; Intelligence pour l’argument structuré.  
**Usages :** convaincre, expliquer, obtenir un accord et présenter une solution.  
**Difficultés :** D18 pour obtenir une concession raisonnable ; D30 pour réunir des intérêts durablement opposés autour d’un accord vérifiable.  
**Progresse lorsque :** l’interlocuteur possède des raisons crédibles de résister.

**Acquis automatiques :**

- **10 — Terrain commun :** le personnage identifie un intérêt partagé manifeste.
- **25 — Argument construit :** il ordonne preuve, bénéfice et réponse aux objections.
- **45 — Changement d’audience :** le même fond peut être présenté selon les valeurs du destinataire.
- **65 — Concession utile :** il cède un point sans abandonner l’objectif principal.
- **85 — Accord durable :** il construit un compromis assorti de garanties, responsabilités et moyens de réparation.

#### 26. Intimidation (`intimidation`)

**Attributs :** Présence ; Force lorsqu’une démonstration physique est pertinente.  
**Usages :** menace crédible, pression, démonstration de capacité et obtention d’une reddition.  
**Difficultés :** D18 pour faire reculer un adversaire isolé hésitant ; D30 pour obtenir la reddition d’un groupe discipliné disposant encore d’options.  
**Progresse lorsque :** la cible peut résister et que la menace implique des conséquences.

**Acquis automatiques :**

- **10 — Menace lisible :** le personnage exprime clairement ce qu’il peut et compte faire.
- **25 — Pression silencieuse :** posture, preuve ou contrôle du lieu peuvent remplacer les cris.
- **45 — Faille de résolution :** il repère peur, loyauté fragile ou priorité cachée.
- **65 — Rupture collective :** une démonstration ciblée peut affecter le moral d’un groupe témoin.
- **85 — Reddition contrôlée :** il peut obtenir armes déposées, distances et garanties sans déclencher une panique aveugle.

#### 27. Tromperie (`deception`)

**Attributs :** Présence ; Intelligence pour maintenir la cohérence.  
**Usages :** mensonge, fausse identité, omission, diversion sociale et couverture.  
**Difficultés :** D18 pour soutenir une excuse plausible ; D30 pour maintenir une identité sous vérifications croisées.  
**Progresse lorsque :** l’interlocuteur cherche réellement les incohérences.

**Acquis automatiques :**

- **10 — Détail plausible :** le personnage ajoute les éléments ordinaires qui rendent un récit crédible.
- **25 — Histoire de couverture :** il prépare motifs, chronologie et réponses simples.
- **45 — Mensonge à couches :** une vérité partielle peut protéger le mensonge central.
- **65 — Contradiction réparée :** il peut intégrer une erreur découverte sans effacer magiquement le soupçon.
- **85 — Masque vécu :** il maintient durablement habitudes, relations et contraintes d’une identité construite.

#### 28. Empathie (`empathy`)

**Attributs :** Perception ; Présence pour répondre avec justesse.  
**Usages :** humeur, motivation, apaisement, soutien, tension et sincérité émotionnelle.  
**Difficultés :** D14 pour reconnaître une émotion manifeste ; D30 pour comprendre une réaction mêlant peur, loyauté et culpabilité.  
**Progresse lorsque :** le personnage écoute, vérifie et agit sur une relation réelle.

**Acquis automatiques :**

- **10 — Humeur lue :** il distingue les émotions principales visibles.
- **25 — Désescalade :** il peut réduire une tension si une issue acceptable existe.
- **45 — Motif sous-jacent :** il différencie la demande exprimée du besoin probable.
- **65 — Ancrage émotionnel :** il aide une personne consentante à résister à la panique ou à une manipulation.
- **85 — Point de rupture :** il reconnaît les événements capables de transformer durablement loyauté, peur ou confiance.

#### 29. Commandement (`leadership`)

**Attributs :** Présence ; Volonté pour tenir sous pression.  
**Usages :** ordres, cohésion, moral, délégation et doctrine du groupe.  
**Difficultés :** D18 pour rallier un groupe surpris ; D30 pour coordonner plusieurs unités pendant un effondrement du plan.  
**Progresse lorsque :** des personnes autonomes suivent, adaptent ou refusent réellement les instructions.

**Acquis automatiques :**

- **10 — Ordre clair :** objectif, cible et limite sont compris sans ambiguïté.
- **25 — Ralliement :** le personnage peut recentrer des alliés hésitants autour d’une action immédiate.
- **45 — Double consigne :** deux actions liées peuvent être transmises sans surcharge.
- **65 — Doctrine ajustée :** il modifie le comportement général du groupe pendant l’action.
- **85 — Intention autonome :** les alliés comprennent suffisamment le but pour improviser sans trahir ses limites.

#### 30. Négoce (`trade`)

**Attributs :** Présence ; Intelligence pour évaluer contrats et marchés.  
**Usages :** estimation, achat, vente, contrat, approvisionnement et réseaux.  
**Difficultés :** D14 pour évaluer un objet courant ; D30 pour monter un accord logistique entre factions méfiantes.  
**Progresse lorsque :** valeur, information, risque ou relation commerciale sont réellement en jeu.

**Acquis automatiques :**

- **10 — Valeur d’usage :** le personnage distingue prix affiché, utilité et état réel.
- **25 — Lot négocié :** il traite ensemble quantité, délai et qualité.
- **45 — Réseau de route :** il identifie intermédiaires, goulots et coûts cachés d’un approvisionnement.
- **65 — Contrat conditionnel :** il prévoit garanties, défauts, pénalités et événements imprévus.
- **85 — Faiseur de marché :** il peut créer une filière ou un échange nouveau, avec des conséquences économiques persistantes.

### Vigilance et finesse

#### 31. Vigilance (`vigilance`)

**Attributs :** Perception ; Volonté pour maintenir l’attention.  
**Usages :** embuscades, changements, dangers immédiats, surveillance et tours de garde.  
**Difficultés :** D18 pour remarquer un comportement anormal dans une foule ; D30 pour maintenir une lecture utile pendant une attaque multisource.  
**Progresse lorsque :** le danger est réellement dissimulé ou l’attention partagée.

**Acquis automatiques :**

- **10 — Balayage :** le personnage examine naturellement accès, hauteurs et couvertures.
- **25 — Signe de danger :** un détail incohérent peut déclencher une suspicion avant l’identification complète.
- **45 — Regard périphérique :** surveiller une zone n’efface plus toute attention au voisinage immédiat.
- **65 — Alerte prédictive :** il anticipe le type probable de danger à partir de signes convergents.
- **85 — Tableau instantané :** en entrant dans une scène, il retient positions, sorties et anomalies principales.

#### 32. Pistage (`tracking`)

**Attributs :** Perception ; Intelligence pour interpréter les traces.  
**Usages :** empreintes, passage, direction, nombre, ancienneté et contre-pistage.  
**Difficultés :** D14 pour suivre des traces fraîches dans la boue ; D30 pour reconstituer un passage ancien à travers plusieurs terrains contaminés.  
**Progresse lorsque :** le terrain, le temps ou l’adversaire efface ou falsifie réellement les indices.

**Acquis automatiques :**

- **10 — Piste fraîche :** direction et allure générales deviennent lisibles.
- **25 — Âge du passage :** le personnage estime l’ancienneté et les interruptions.
- **45 — Trace urbaine :** habitudes, témoins et perturbations remplacent les empreintes naturelles.
- **65 — Contre-piste :** il reconnaît et produit des tentatives cohérentes de dissimulation.
- **85 — Histoire du passage :** il reconstitue organisation, fatigue, conflit ou objectif probable d’un grand déplacement.

#### 33. Crochetage (`lockpicking`)

**Attributs :** Dextérité ; Intelligence pour comprendre la serrure.  
**Usages :** serrures, verrous, clés, mécanismes de sûreté et ouverture non destructive.  
**Difficultés :** D18 pour une serrure entretenue ; D30 pour un mécanisme complexe sous surveillance et sans laisser de trace.  
**Progresse lorsque :** la conception, l’état ou la pression diffèrent des exercices connus.

**Acquis automatiques :**

- **10 — Tension juste :** les serrures simples ne sont plus endommagées par une erreur grossière.
- **25 — Faux passage :** le personnage ressent les sécurités ou déclencheurs mécaniques évidents.
- **45 — Travail sans vue :** il peut agir par toucher dans une position contrainte.
- **65 — Empreinte de clé :** il peut relever ou reproduire une géométrie accessible.
- **85 — Ouverture blanche :** un mécanisme extrêmement complexe peut être ouvert sans signe manifeste si le temps et les outils le permettent.

#### 34. Escamotage (`sleight_of_hand`)

**Attributs :** Dextérité ; Présence lorsque l’attention doit être guidée.  
**Usages :** dissimulation, substitution, prélèvement, dépôt et manipulation sous observation.  
**Difficultés :** D18 pour cacher un petit objet pendant une conversation ; D30 pour effectuer une substitution face à plusieurs observateurs attentifs.  
**Progresse lorsque :** une personne capable d’intervenir possède une chance réelle de remarquer le geste.

**Acquis automatiques :**

- **10 — Poche sûre :** le personnage dissimule et récupère un petit objet sans maladresse.
- **25 — Main rapide :** une interaction brève peut se fondre dans un geste ordinaire.
- **45 — Dépôt discret :** il place un objet sur une personne ou dans un lieu accessible.
- **65 — Échange sous regard :** une diversion cohérente permet une substitution pendant l’observation.
- **85 — Transfert invisible :** il accomplit une manipulation spectaculaire, mais toujours fondée sur un contact et une attention réellement détournée.

#### 35. Dressage (`animal_handling`)

**Attributs :** Présence ; Perception pour lire l’animal.  
**Usages :** approche, calme, monte, soins comportementaux, apprentissage et conduite.  
**Difficultés :** D18 pour calmer une monture effrayée ; D30 pour coordonner plusieurs animaux au milieu d’un danger inhabituel.  
**Progresse lorsque :** l’animal possède peur, volonté, douleur ou habitudes propres.

**Acquis automatiques :**

- **10 — Approche calme :** le personnage évite les signaux de menace les plus évidents.
- **25 — Conduite :** il obtient un déplacement ou arrêt fiable d’un animal entraîné.
- **45 — Tâche apprise :** il peut enseigner une action précise au fil de séances réelles.
- **65 — Groupe animal :** il coordonne montures, meute ou troupeau dans une situation tendue.
- **85 — Lien de travail :** un animal longuement entraîné comprend des intentions complexes sans devenir surnaturellement obéissant.

### Disciplines magiques

Les sept disciplines sont présentes sur toutes les feuilles de personnage. Elles ne sont pas réservées à une classe. Les pratiquer exige cependant des formes connues, une source d’apprentissage, le Mana nécessaire et les conditions établies par le module. Une forme maintenue peut également mobiliser la Concentration.

#### 36. Élémentalisme (`elementalism`)

**Attributs :** Intelligence ; Volonté pour maintenir l’effet.  
**Usages :** feu, froid, électricité, chaleur, surfaces et réactions élémentaires.  
**Difficultés :** D18 pour modifier une petite flamme existante ; D30 pour contrôler une réaction en chaîne dans une zone occupée.  
**Progresse lorsque :** propriétés, propagation, intensité ou risques apportent une difficulté réelle.

**Acquis automatiques :**

- **10 — Sens élémentaire :** température, combustion et conductivité manifestes deviennent lisibles.
- **25 — Surface modelée :** le personnage peut étendre ou contenir un élément déjà présent.
- **45 — Réaction composée :** il associe deux propriétés élémentaires sans les traiter comme des dégâts abstraits.
- **65 — Chaîne contrôlée :** il peut préparer propagation, délai ou interruption d’une réaction.
- **85 — Domaine élémentaire :** il orchestre plusieurs foyers interdépendants, chacun restant soumis au milieu et au contre-jeu.

#### 37. Force, discipline magique (`force_magic`)

**Attributs :** Volonté ; Intelligence pour répartir les contraintes.  
**Usages :** poussée, attraction, suspension, barrière, déviation et télékinésie.  
**Difficultés :** D18 pour déplacer un objet libre de faible masse ; D30 pour détourner une charge lourde sans détruire ses appuis.  
**Progresse lorsque :** masse, vitesse, stabilité, distance ou nombre de cibles posent un vrai problème.

**Acquis automatiques :**

- **10 — Impulsion :** le personnage pousse ou attire un petit objet libre.
- **25 — Suspension :** il maintient brièvement une masse compatible avec sa maîtrise.
- **45 — Plan de force :** il crée une déviation ou un appui temporaire, pas une invulnérabilité.
- **65 — Liens multiples :** plusieurs objets peuvent être maintenus dans une même intention.
- **85 — Impulsion structurelle :** il applique la force à une structure entière en tenant compte de ses appuis et ruptures.

#### 38. Altération (`alteration`)

**Attributs :** Intelligence ; Volonté pour maintenir la transformation.  
**Usages :** dureté, souplesse, forme, densité, état matériel et transformation temporaire.  
**Difficultés :** D18 pour assouplir un petit matériau homogène ; D30 pour transformer durablement un assemblage complexe.  
**Progresse lorsque :** composition, durée, volume ou stabilité sont nouveaux.

**Acquis automatiques :**

- **10 — Lecture de matière :** le personnage reconnaît les propriétés dominantes d’un matériau.
- **25 — État infléchi :** il assouplit, durcit ou fragilise temporairement une petite zone.
- **45 — Forme adaptée :** il remodèle un objet simple sans en ignorer le volume.
- **65 — Propriété transférée :** une qualité compatible peut être déplacée ou partagée pour une durée limitée.
- **85 — Métamorphose stable :** un rituel permet une transformation majeure dont coûts, durée et réversibilité sont explicitement établis.

#### 39. Illusion (`illusion`)

**Attributs :** Intelligence ; Présence pour rendre l’apparence convaincante.  
**Usages :** lumière, son, apparence, dissimulation perceptive et scènes trompeuses.  
**Difficultés :** D18 pour produire un son localisé crédible ; D30 pour maintenir une scène multisensorielle face à des interactions imprévues.  
**Progresse lorsque :** l’illusion doit répondre à un observateur actif ou à un environnement changeant.

**Acquis automatiques :**

- **10 — Éclat et murmure :** lumière ou son simple peuvent être produits.
- **25 — Apparence posée :** un objet ou une personne reçoit une enveloppe visuelle cohérente.
- **45 — Scène multisensorielle :** plusieurs perceptions concordantes peuvent être associées.
- **65 — Illusion réactive :** l’effet répond à des conditions préparées ou à une conduite active.
- **85 — Monde partagé :** un espace limité peut recevoir une fausse lecture commune, vulnérable aux contradictions physiques.

#### 40. Esprit (`spirit`)

**Attributs :** Volonté ; Présence pour entrer en relation avec une conscience.  
**Usages :** Concentration, émotions, communication mentale, défense psychique et empreintes de mémoire.  
**Difficultés :** D18 pour calmer une panique consentante ; D30 pour affronter une présence mentale puissante sans perdre son identité.  
**Progresse lorsque :** une volonté autonome, une émotion réelle ou un risque psychique intervient.

**Acquis automatiques :**

- **10 — Centre intérieur :** le personnage reconnaît plus vite une influence étrangère sur son esprit.
- **25 — Fil de pensée :** une communication mentale brève devient possible avec un sujet consentant.
- **45 — Rempart mental :** il peut protéger une intention, un souvenir ou une émotion contre une intrusion.
- **65 — Écho mémoriel :** il perçoit des fragments laissés par une conscience dans des conditions appropriées.
- **85 — Paysage intérieur :** il peut entrer dans une confrontation mentale structurée où chaque volonté conserve moyens de défense et de rupture.

#### 41. Vitalité, discipline magique (`vitality_magic`)

**Attributs :** Volonté ; Intelligence pour comprendre les fonctions vivantes.  
**Usages :** soins, toxiques, croissance, affaiblissement, purification et fonctions biologiques.  
**Difficultés :** D18 pour accélérer la fermeture d’une plaie stabilisée ; D30 pour restaurer une fonction gravement atteinte sans provoquer de dérèglement.  
**Progresse lorsque :** anatomie, toxine, traumatisme ou coût vital posent une question nouvelle.

**Acquis automatiques :**

- **10 — Sens du vivant :** le personnage distingue vie, blessure et corruption manifeste.
- **25 — Impulsion réparatrice :** il accélère un processus de guérison déjà possible.
- **45 — Fonction modulée :** il stimule, ralentit ou neutralise temporairement une fonction biologique comprise.
- **65 — Champ vital :** plusieurs êtres proches peuvent être soutenus ou affaiblis selon une même règle explicite.
- **85 — Retour du seuil :** il peut rappeler une personne d’un état critique tant que la mort n’est pas devenue irréversible selon le module.

#### 42. Invocation (`summoning`)

**Attributs :** Volonté ; Intelligence pour construire le lien.  
**Usages :** appel, ancrage, pacte, manifestation, renvoi et créatures invoquées.  
**Difficultés :** D18 pour appeler une présence mineure connue ; D30 pour ouvrir puis refermer un passage majeur sous opposition.  
**Progresse lorsque :** l’entité, la distance, l’ancrage, la durée ou la négociation sont réellement difficiles.

**Acquis automatiques :**

- **10 — Signe d’appel :** le personnage crée un repère perceptible par une présence connue.
- **25 — Ancrage mineur :** une entité ou manifestation limitée peut demeurer brièvement.
- **45 — Service négocié :** l’invocation reçoit une intention complexe assortie de limites explicites.
- **65 — Cercle partagé :** plusieurs ancrages ou assistants peuvent soutenir une même manifestation.
- **85 — Grand passage :** un rituel permet une invocation majeure dont dette, risque, rupture et renvoi font partie intégrante de l’effet.
