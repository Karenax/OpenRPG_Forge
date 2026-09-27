<!-- handbook-part: play-rules -->

# Jouer dans le monde

![Quatre voyageurs se tiennent dans des ruines de montagne, chacun portant des outils et un équipement différents.](../../assets/player-handbook/cover-realistic-party.png)

*Aux portes du danger — Les ombres appartiennent aux ruines et aux menaces ; les personnages conservent des visages, des proportions et un équipement réalistes.*

> **Portée des règles.** Cette partie constitue une proposition normative du Manuel du joueur pour le ruleset **openrpg-forge.systemic-rpg.v1**. Elle décrit le comportement attendu du jeu et fournit des valeurs de référence destinées à être équilibrées, prototypées et testées. Elle ne constitue pas une preuve que ces règles sont déjà implémentées dans le runtime Rust.
>
> Les mécaniques et les textes sont conçus en clean-room. La présentation peut retrouver la générosité d’un grand manuel illustré classique, avec tables, exemples et planches, mais elle ne reprend ni classes, ni formulations, ni univers, ni compositions graphiques d’un jeu existant.

## Le principe fondamental

Dans OpenRPG Forge, aucun personnage n’est défini par une classe. Tous les personnages ont accès au même ensemble de compétences. Leurs actions, leurs apprentissages, leur équipement, leurs rencontres et leurs décisions déterminent ce qu’ils deviennent.

Un personnage devient bon à l’épée parce qu’il affronte réellement des adversaires capables de lui résister. Il apprend à négocier en conduisant des discussions difficiles. Il développe sa magie en étudiant, préparant et employant des effets dans des situations significatives. Il ne progresse pas en répétant sans risque une action contre une cible inerte.

La réussite repose toujours sur trois couches :

1. ce que le personnage a appris ;
2. ce que le joueur exécute ;
3. ce que la situation permet.

Un personnage peu expérimenté peut vaincre un adversaire supérieur en détruisant son appui, en attirant son attention ailleurs ou en préparant correctement le terrain. Un maître reste cependant plus précis, plus économe en ressources et capable d’un plus grand nombre de techniques.

> **Nul ne naît guerrier.** L’équipement, les gestes, les cicatrices et la posture racontent l’expérience acquise. Aucun uniforme de classe ne sépare artificiellement les étapes d’une vie.

## Résoudre une action incertaine

Le jeu ne demande pas de test pour une action évidente et sans conséquence. Marcher dans une pièce calme, ouvrir sa propre sacoche ou monter une échelle solide ne requiert aucun jet.

Un test est utilisé lorsqu’il existe au moins deux des éléments suivants :

- une difficulté réelle ;
- une opposition ;
- un risque ;
- une dépense significative ;
- une urgence ;
- une conséquence intéressante en cas d’échec.

### Formule commune

~~~text
Résultat =
2d10
+ ⌊compétence ÷ 5⌋
+ attribut
+ contexte
+ aide
~~~

Les compétences vont de **0 à 100**. Les attributs vont normalement de **1 à 10**, avec 5 comme valeur humaine moyenne.

| Difficulté | Seuil | Exemple |
|---|---:|---|
| Ordinaire | 14 | Franchir une fenêtre pendant une poursuite |
| Délicate | 18 | Suivre des traces récentes sur un sol meuble |
| Exigeante | 22 | Crocheter une serrure correcte sans bon outil |
| Ardue | 26 | Convaincre un garde méfiant avec une histoire plausible |
| Extrême | 30 | Toucher une corde en mouvement avec un projectile |
| Prodigieuse | 34 | Stabiliser un mécanisme ancien pendant son effondrement |

Le **contexte** varie normalement de −6 à +6. Une preuve décisive, un plan exceptionnel ou une situation presque impossible peut aller jusqu’à +8 ou −8.

L’**aide** vaut de 0 à +3. Un assistant doit expliquer sa contribution, disposer des moyens nécessaires et partager les conséquences éventuelles.

### Avantage et désavantage

Avec un avantage, le joueur lance **3d10 et conserve les deux meilleurs dés**.

Avec un désavantage, il lance **3d10 et conserve les deux moins bons**.

Un avantage et un désavantage s’annulent. Plusieurs avantages ne donnent pas davantage de dés ; les circonstances supplémentaires modifient éventuellement le contexte.

### Qualité du résultat

| Marge | Conséquence |
|---:|---|
| −10 ou moins | **Échec net** : le danger ou l’opposition s’impose |
| −9 à −1 | **Échec constructif** : progrès partiel, coût ou nouvelle complication |
| 0 à 4 | **Réussite fragile** : objectif atteint avec exposition ou consommation |
| 5 à 9 | **Réussite propre** |
| 10 ou plus | **Réussite décisive** : vitesse, discrétion ou avantage supplémentaire cohérent |

Un résultat élevé ne rend jamais possible une action physiquement impossible sans capacité explicite.

### Opposition

Lorsque deux acteurs agissent activement l’un contre l’autre, chacun calcule son résultat. La meilleure marge l’emporte. Une égalité maintient la situation actuelle ou impose un compromis : deux prises restent verrouillées, un mensonge entretient le doute, ou deux combattants rompent le contact.

### Le contact visible n’est pas un échec

Un volume d’arme, un projectile ou un objet qui touche physiquement une cible produit toujours un contact.

La compétence détermine la qualité du geste, la dépense d’Endurance, l’angle, la pénétration, la récupération et les conséquences. Elle ne transforme jamais un coup visiblement porté en raté abstrait.

## Ressources communes

Les modules utilisant les valeurs de référence emploient partout les noms **Vitalité**, **Endurance**, **Mana**, **Équilibre** et **Concentration**.

| Ressource | Maximum de référence | Fonction |
|---|---:|---|
| Vitalité | 60 + 6 × Constitution + 2 × Force | Trauma, blessures et survie |
| Endurance | 50 + 5 × Constitution + 3 × Volonté | Efforts physiques et défenses |
| Mana | 20 + 6 × Volonté + 4 × Intelligence | Magie ordinaire |
| Équilibre | 40 + 4 × Force + 4 × Constitution | Résistance aux chocs et projections |
| Concentration | 40 + 4 × Perception + 4 × Volonté | Préparations, visée, maintien et concentration tactique |

La charge sûre, exprimée en kilogrammes, vaut :

~~~text
Charge sûre =
10
+ 4 × Force
+ 2 × Constitution
~~~

Un personnage moyen ayant 5 dans chaque attribut possède environ 100 points de Vitalité, 90 d’Endurance, 70 de Mana, 80 d’Équilibre et 80 de Concentration. Ces nombres ne signifient pas qu’il peut absorber cent coups. Une attaque dangereuse peut provoquer une blessure grave avant que toute la Vitalité soit perdue.

## La boucle de jeu

La boucle fondamentale est :

**Observer → comprendre → préparer → agir → improviser → assumer.**

### Observer

Le joueur examine :

- les personnes et leurs comportements ;
- les accès visibles ou cachés ;
- les hauteurs ;
- les surfaces et matériaux ;
- les témoins ;
- les mécanismes ;
- les issues de secours ;
- les signes d’un danger.

### Comprendre

Une corde effilochée indique une rupture possible. Une plaque de métal conduit l’électricité. Une odeur âcre révèle un gaz. Des bottes couvertes de boue peuvent identifier l’itinéraire d’une patrouille.

Le jeu doit expliquer ses systèmes par des indices perceptibles avant d’en appliquer les conséquences.

### Préparer

Le groupe peut déplacer un objet, changer de formation, enduire une arme, mémoriser un ordre, mouiller une surface, couper une retraite ou préparer une formule magique.

### Agir

Le temps réel commence ou continue. Les attaques, déplacements, sons, alarmes et réactions physiques se produisent simultanément.

### Improviser

Un plan n’est jamais garanti. Le feu peut couper la route prévue. Un compagnon peut être séparé. Un ennemi peut retourner une grue contre le groupe. L’improvisation constitue une partie normale du jeu.

### Assumer

Une victoire par incendie reste un incendie. Elle peut détruire des preuves, tuer un prisonnier, effrayer une faction ou fermer une route commerciale.

> **Progression liée à la boucle.** Une action significative peut faire progresser la compétence employée, y compris lorsqu’elle échoue. La progression dépend de la difficulté, du risque, de la nouveauté et de l’apprentissage produit, jamais du nombre brut de répétitions.

## Actions et écoulement du temps

### Temps réel

Les durées de combat utilisent des secondes réelles. Les animations communiquent clairement trois phases :

1. **préparation** : le geste devient lisible ;
2. **contact** : l’arme ou l’effet devient dangereux ;
3. **récupération** : l’acteur retrouve une posture disponible.

Une attaque ne devient pas dangereuse pendant toute son animation.

### Durées de référence

| Action | Durée typique |
|---|---:|
| Interaction simple | 0,4 à 1 seconde |
| Dégainer une arme préparée | 0,35 à 0,8 seconde |
| Changer de jeu d’armes | 1,2 seconde |
| Attaque rapide | 0,45 à 0,9 seconde |
| Attaque puissante | 0,9 à 1,8 seconde |
| Boire un consommable préparé | 1,8 à 2,5 secondes |
| Poser un bandage | 4 secondes |
| Stabiliser un allié | 3 secondes |
| Relever un allié stabilisé | 5 secondes |
| Recharger une arbalète | 2,8 à 4 secondes |
| Crocheter sous pression | 5 à 20 secondes |
| Changer entièrement d’armure | 30 à 120 secondes |

Les actions longues peuvent être interrompues. Leur progression n’est conservée que lorsque cela possède un sens physique.

### Temps du monde

Le réglage de référence est :

- combat et danger immédiat : temps 1:1 ;
- exploration locale : une minute réelle représente cinq minutes du monde ;
- voyage sur la carte : avance explicite selon la distance, le terrain et l’itinéraire ;
- repos court : vingt minutes ;
- repos long : huit heures.

Un module peut modifier cette échelle, mais toute urgence doit annoncer clairement son échéance.

### Concentration tactique

En solo, la concentration tactique ralentit le monde à 15 % de sa vitesse. Elle consomme **20 points de Concentration par seconde réelle** et permet :

- d’examiner les dangers déjà perçus ;
- de sélectionner un compagnon ;
- de placer jusqu’à trois ordres ;
- de viser un objet du décor ;
- de choisir une capacité ;
- de visualiser une trajectoire approximative.

La Concentration se régénère à **10 points par seconde** hors danger et à **2 points par seconde** pendant une accalmie en combat. Un effet maintenu ou une visée exigeante peut ralentir cette récupération.

Une option d’accessibilité peut remplacer le ralentissement par une pause complète en solo.

## Combat en temps réel lisible

![Dans une ruine inondée, un bouclier arrête une créature pendant qu’un contrepoids tombe et qu’une bande de glace modifie le terrain.](../../assets/player-handbook/systemic-encounter.png)

*Le contact et la conséquence — L’attaque bloquée existe, l’eau réagit, la charge tombe et le froid change les appuis. Les statistiques qualifient ces faits ; elles ne les effacent pas.*

### Pas de tour d’initiative

Tout le monde agit en temps réel. L’avantage initial vient du placement, de la Perception, de la surprise, de l’allonge et de la préparation.

Une embuscade réussie n’accorde pas un tour gratuit abstrait. Elle place l’adversaire dans un état physique défavorable : arme non dégainée, regard ailleurs, formation brisée ou issue bloquée.

Les attaques ennemies dangereuses doivent annoncer leur direction, leur portée et leur intensité par la posture, le son, la lumière ou le mouvement. Un adversaire peut feinter, mais une feinte est elle-même un geste lisible et appris.

### Coûts d’Endurance

| Action | Coût de référence |
|---|---:|
| Attaque rapide | 6 à 10 |
| Attaque puissante | 16 à 24 |
| Parade | 12 |
| Esquive | 18 |
| Coup de pied ou poussée | 14 |
| Saut | 10 |
| Sprint | 8 par seconde |
| Escalade | 4 à 10 par seconde |
| Blocage | 6 + 0,35 × Impact reçu |

Après 1,2 seconde sans effort intense, l’Endurance se régénère à **18 points par seconde**. Elle se régénère à seulement **6 points par seconde** tant que la garde est maintenue.

Sous 25 % d’Endurance :

- les attaques sont plus lentes de 10 % ;
- les parades coûtent 25 % de plus ;
- l’Équilibre récupère moins vite ;
- un blocage parfait devient impossible.

À zéro Endurance, le personnage peut encore marcher, lâcher sa garde et effectuer une attaque désespérée. Il ne peut ni sprinter ni esquiver.

### Impact, protection et Trauma

La conséquence d’un coup dépend de l’arme, de sa vitesse, de son angle, de la zone touchée et de la protection réellement présente.

~~~text
Trauma net =
max(0,
    arrondi(
      (Impact brut × Qualité du contact
       − 0,65 × Protection de la zone)
      × Coefficient de zone
    )
)
~~~

| Contact | Coefficient |
|---|---:|
| Effleuré ou mauvais angle | 0,60 |
| Contact normal | 1,00 |
| Angle ou distance optimale | 1,20 |
| Critique situationnel | 1,50 |

| Zone | Coefficient |
|---|---:|
| Tête | 1,40 |
| Torse | 1,00 |
| Bras | 0,75 |
| Jambes | 0,80 |

Un résultat de zéro Trauma n’efface pas le contact. Le coup produit toujours un son, un recul, des étincelles et éventuellement une perte d’Endurance ou d’Équilibre.

### Critiques déterministes

Un critique provient d’une situation lisible :

- tête non protégée ;
- point faible identifié ;
- cible au sol ;
- attaque dans le dos ;
- garde brisée ;
- cible immobilisée ou gelée ;
- armure déjà ouverte ;
- attaque plongeante ;
- parade parfaite suivie d’une riposte adaptée.

Le hasard peut varier légèrement une conséquence, mais ne doit pas être l’unique cause d’un coup critique.

### Allonge et espace

L’arme possède un volume réel.

- Une lance domine entre 1,2 et 2 mètres, mais devient maladroite sous 0,7 mètre.
- Une grande lame peut heurter un mur.
- Une dague est faible à distance mais pleinement utilisable dans une saisie.
- Une arme d’hast peut être tenue plus court au prix d’une efficacité réduite.
- Un projectile peut être dévié par un obstacle réellement présent.

La compétence améliore le contrôle de l’allonge et les transitions. Elle ne permet pas de traverser le décor.

### Tir et projectiles

Le projectile suit une trajectoire. La compétence influence :

- la dispersion initiale ;
- le tremblement ;
- le temps de visée ;
- la lecture de la distance ;
- la vitesse de rechargement ;
- l’utilisation d’une couverture ;
- les tirs spéciaux appris.

Un tir physiquement correct touche. Un tir sur une corde, un levier ou une petite articulation peut exiger un test de précision parce que la cible représente une difficulté réelle.

### Feu allié

Par défaut, les armes, projectiles, explosions et réactions peuvent atteindre les alliés. Les compagnons tiennent compte des trajectoires qu’ils connaissent. Une option d’accessibilité peut réduire ou supprimer le Trauma allié, sans supprimer les signaux visuels ni les déplacements physiques.

## Blocage, parade et esquive

### Blocage

Le blocage place réellement une arme ou un bouclier entre l’attaque et le corps.

Un bouclier couvre normalement un angle de 110°. Une arme en garde couvre environ 70°. La compétence peut augmenter cet angle jusqu’à 20°, sans protéger le dos.

Le blocage :

- réduit ou annule le Trauma selon la protection ;
- consomme de l’Endurance ;
- transmet une partie du choc à l’Équilibre ;
- peut céder contre une force suffisante ;
- n’arrête pas automatiquement le feu, le gaz ou une surface dangereuse.

### Parade

La parade exige une entrée précise avant le contact.

~~~text
Fenêtre de parade =
120 millisecondes
+ 2 millisecondes × compétence concernée
maximum 280 millisecondes
~~~

Le centre de cette fenêtre constitue la parade parfaite. Une parade parfaite :

- détourne entièrement une attaque compatible ;
- inflige un choc important à l’Équilibre de l’attaquant ;
- ouvre une courte fenêtre de riposte ;
- peut déclencher des techniques apprises.

Une dague ne pare pas proprement un tronc d’arbre lancé à pleine vitesse. Une parade doit rester physiquement crédible.

### Esquive

L’esquive déplace le corps de 1,5 à 3 mètres selon la direction, l’équipement et l’espace disponible.

Une tolérance maximale de 90 millisecondes peut accompagner la compression initiale du corps. Elle ne protège pas contre :

- une flamme persistante ;
- une explosion englobant la position finale ;
- une nappe de poison ;
- une chute ;
- une arme dont la trajectoire traverse encore le personnage.

La compétence réduit le coût, accélère la récupération et permet de mieux choisir la distance.

### Équilibre et garde brisée

Les impacts, explosions, réceptions de chute et parades parfaites diminuent l’Équilibre.

À zéro Équilibre, la garde est brisée pendant **1,2 à 2,5 secondes**. La cible peut être :

- interrompue ;
- désarmée ;
- saisie ;
- poussée ;
- renversée ;
- frappée par un critique situationnel.

L’Équilibre récupère à **20 points par seconde** après une seconde sans choc.

## Blessures, états et soins

![Dans un relais ouvert sur une campagne lumineuse après la pluie, quatre voyageurs posent une attelle, compriment une plaie et préparent de l’eau propre.](../../assets/player-handbook/rescue-after-storm.png)

*Secourir avant de repartir — Le diagnostic, la compression, l’attelle et l’eau propre sont des actions coordonnées ; le soin reste lisible, matériel et exposé aux circonstances.*

### Gravité d’une blessure

La blessure dépend du Trauma net produit par un seul événement.

| Trauma net | Gravité |
|---:|---|
| 0 à 14 | Choc ou atteinte superficielle |
| 15 à 24 | Blessure mineure |
| 25 à 39 | Blessure grave |
| 40 ou plus | Blessure critique |

La localisation précise la conséquence.

| Localisation | Effet possible |
|---|---|
| Tête | Désorientation, vision troublée, perte de Concentration |
| Bras | Précision réduite, arme lourde difficile à tenir |
| Jambe | Déplacement ralenti, esquive raccourcie |
| Torse | Récupération d’Endurance réduite, saignement |
| Main | Objet lâché, manipulation lente |
| Pied | Course et Équilibre réduits |

Les blessures du même type peuvent aggraver un état existant.

### Seuils de Vitalité

- Au-dessus de 50 % : le personnage reste fonctionnel hors blessures locales.
- De 26 % à 50 % : récupération d’Endurance réduite de 15 %.
- De 1 % à 25 % : état critique, actions longues ralenties de 25 %.
- À 0 : mise hors de combat.

La magie de soin peut rendre de la Vitalité. Elle ne fait pas automatiquement disparaître une fracture, un poison ou une brûlure critique.

### Saignement

| Saignement | Perte |
|---|---:|
| Mineur | 1 Vitalité toutes les 15 secondes |
| Grave | 1 Vitalité toutes les 5 secondes |
| Critique | 1 Vitalité par seconde |

Une pression ou un bandage réduit le saignement d’un rang. Un soin adapté, une suture ou un effet magique peut l’arrêter.

### États à accumulation

Les états élémentaires possèdent une jauge de 0 à 100. L’exposition augmente la jauge ; la protection, le traitement et le temps la réduisent.

| Jauge | Déclenchement à 100 | Réponse |
|---|---|---|
| Chaleur | Embrasement | Rouler, eau, couverture, froid |
| Froid | Engourdissement puis gel local | Chaleur, mouvement, abri |
| Charge | Choc électrique | Quitter le conducteur, isoler, dissiper |
| Toxicité | Empoisonnement | Antidote, repos, dilution |
| Suffocation | Perte de conscience | Air, sortie de fumée, assistance |
| Panique | Fuite ou perte de contrôle brève | Sécurité, commandement, Volonté |

Une immunité totale exige une justification visible. Une résistance ralentit l’accumulation ; elle ne rend pas nécessairement invulnérable.

### Mise hors de combat et secours

À zéro Vitalité, un acteur ordinaire dispose de **60 secondes** avant la mort. Un saignement grave ou critique accélère ce compte.

- Stabiliser : 3 secondes.
- Relever avec une trousse ou une capacité : 5 secondes.
- Vitalité au retour : 15.
- Nouvelle mise hors de combat dans les dix minutes : fenêtre réduite de 15 secondes.

Une décapitation, une immersion prolongée, une destruction corporelle ou une chute extrême peut provoquer une mort immédiate selon la difficulté.

## Armes et armures

### Armes de référence

| Arme | Impact | Allonge | Endurance | Particularité |
|---|---:|---:|---:|---|
| Dague | 16 tranchant / 18 perforant | 0,45 m | 6 | Rapide au contact |
| Épée à une main | 24 tranchant / 22 perforant | 0,9 m | 9 | Polyvalente |
| Hache | 28 tranchant | 0,75 m | 12 | Efficace contre les structures |
| Masse | 25 contondant | 0,65 m | 11 | Transmission à travers l’armure |
| Lance | 27 perforant | 1,8 m | 10 | Contrôle de distance |
| Bâton | 20 contondant | 1,4 m | 8 | Parade et contrôle |
| Grande lame | 34 tranchant | 1,4 m | 18 | Large arc, récupération lente |
| Arc court | 18 à 26 perforant | distance | 8 à 12 | Impact selon l’allonge |
| Arbalète | 36 perforant | distance | 4 au tir | Rechargement long |

Ces valeurs décrivent un contact normal. La vitesse, l’angle, la zone et les techniques les modifient.

### Protections de référence

| Protection | Tranchant | Perforant | Contondant | Poids complet | Bruit |
|---|---:|---:|---:|---:|---|
| Vêtement épais | 5 | 2 | 3 | 3 kg | Très faible |
| Matelassé | 10 | 5 | 8 | 7 kg | Faible |
| Cuir renforcé | 14 | 8 | 9 | 10 kg | Faible |
| Mailles | 23 | 13 | 10 | 16 kg | Élevé |
| Plaques | 33 | 25 | 14 | 25 kg | Moyen à élevé |

L’armure est localisée. Un casque ne protège pas le torse et une cuirasse ne couvre pas automatiquement les articulations.

### Effets secondaires

Une armure influence aussi :

- le bruit ;
- la chaleur ;
- la flottabilité ;
- l’accélération ;
- la récupération d’Endurance ;
- l’apparence sociale ;
- la facilité d’escalade ;
- la vitesse de nage.

Tout le monde peut porter toute armure physiquement accessible. L’expérience réduit les pénalités de manipulation ; elle ne constitue pas une permission de classe.

### Boucliers

| Bouclier | Protection | Angle | Particularité |
|---|---:|---:|---|
| Rondache | 18 | 70° | Parade rapide |
| Bouclier moyen | 30 | 110° | Usage général |
| Grand bouclier | 40 | 140° | Couverture, lourd, lent |

Les boucliers et objets improvisés peuvent se briser. Le ruleset n’impose pas une dégradation permanente de chaque arme après chaque combat.

## Le monde comme arme

### Saisir et déplacer

| Masse | Exemple |
|---:|---|
| 0 à 2 kg | Bouteille, pierre, torche |
| 2 à 8 kg | Tabouret, petite caisse, arme lourde |
| 8 à 30 kg | Coffre, meuble léger, corps |
| 30 à 100 kg | Grande caisse, poutre, statue légère |
| Plus de 100 kg | Mécanisme, bloc, structure |

La Force détermine ce qui peut être soulevé. La compétence détermine la vitesse, le contrôle et la précision.

### Impact d’un objet

~~~text
Impact de projectile =
arrondi(
  1,2
  × racine carrée de la masse en kilogrammes
  × vitesse en mètres par seconde
)
~~~

L’impact est normalement plafonné à 60 pour un objet portable. Les chutes de structures utilisent leur propre simulation et peuvent dépasser cette limite.

Le joueur n’a pas à calculer cette formule. L’interface communique le danger par la trajectoire, la masse perçue, le son et une prévisualisation approximative en concentration tactique.

### Pousser une cible

~~~text
Pression =
2d10
+ Force
+ ⌊Athlétisme ÷ 5⌋
+ contexte d’impulsion
~~~

Elle est comparée à :

~~~text
Résistance =
16
+ masse
+ posture
+ ancrage
+ Équilibre restant ÷ 10
~~~

Modificateur indicatif de masse :

- cible légère : −4 ;
- taille humaine : 0 ;
- cible lourde : +4 ;
- cible massive : +10 ;
- cible ancrée : impossible tant que l’ancrage subsiste.

L’échec ne supprime pas le contact. La cible peut ne reculer que de quelques centimètres, perdre de l’Équilibre ou être forcée de réajuster sa garde.

### Chute

~~~text
Impact de chute =
max(0, (hauteur en mètres − 2) × 6)
~~~

| Surface | Coefficient |
|---|---:|
| Eau profonde dégagée | 0,35 |
| Neige ou végétation épaisse | 0,60 |
| Terre | 0,85 |
| Pierre | 1,00 |
| Débris ou mécanisme | 1,20 |
| Pointes | Impact normal + dégâts perforants |

L’Acrobatie peut réduire jusqu’à 40 % l’impact d’une réception contrôlée. Elle ne protège pas d’une chute inconsciente, surchargée ou dans un danger létal.

### Réactions systémiques

| Situation | Réaction |
|---|---|
| Huile + feu | Combustion et propagation |
| Eau + froid | Gel et perte de friction |
| Cible mouillée + électricité | Accumulation de Charge accrue de 50 % |
| Gaz inflammable + étincelle | Explosion |
| Feu + lieu fermé | Fumée, chaleur et suffocation |
| Corde + lame ou feu | Perte de résistance puis rupture |
| Support détruit + charge | Chute de la charge |
| Glace + impact | Rupture et projection d’éclats |
| Eau + chaleur intense | Vapeur et visibilité réduite |
| Vent + fumée ou gaz | Déplacement du nuage |
| Acide + métal | Perte progressive de protection |
| Objet lourd + hauteur | Impact accru |

La réaction appartient à la chaîne causale du personnage qui l’a initiée. Cette attribution sert à la progression, aux crimes, aux quêtes et aux réactions des compagnons.

> **Lire une salle comme un système.** Dans une fonderie, un levier bloqué, de l’huile renversée, une poutre fragilisée, un brasero suspendu, un canal d’eau et une passerelle peuvent former plusieurs chaînes plausibles sans imposer une solution unique.

## Magie systémique

![Dans un observatoire nocturne, quatre voyageurs préparent un cercle, des ingrédients et des instruments autour d’une faille contenue.](../../assets/player-handbook/magic-observatory.png)

*Une discipline, pas une classe — La magie s’apprend, se prépare et se paie. Sa forme, son intensité et ses conséquences restent visibles.*

### Un savoir accessible à tous

Aucune discipline magique n’est réservée à une classe. Tout personnage peut apprendre la magie s’il trouve une source d’apprentissage, une forme connue et des conditions compatibles avec le monde du module.

Connaître une formule ne signifie pas la maîtriser. Un personnage peu expérimenté :

- consomme davantage de Mana ;
- prépare plus lentement ;
- contrôle moins bien la forme ;
- supporte moins d’intensité ;
- risque davantage une interruption.

### Grammaire d’un effet

Un effet magique est composé de :

~~~text
Forme
+ principe
+ intensité
+ durée
+ portée
+ contraintes
~~~

| Forme | Coût de base en Mana |
|---|---:|
| Contact | 2 |
| Projectile | 4 |
| Lien ou rayon maintenu | 5 |
| Cône | 6 |
| Aura | 7 |
| Zone | 8 |
| Rituel | Variable, avec temps élevé |

| Intensité | Coût en Mana |
|---:|---:|
| 1 | 2 |
| 2 | 8 |
| 3 | 18 |
| 4 | 32 |
| 5 | 50 |

Un effet immédiat n’ajoute aucun coût de durée. Un effet maintenu coûte normalement **3 Mana par seconde** et mobilise la Concentration. Une surface persistante ajoute au moins **5 Mana par tranche de dix secondes**.

### Intensité sûre

~~~text
Intensité sûre =
1
+ ⌊compétence de discipline ÷ 20⌋
+ ⌊(Intelligence + Volonté − 10) ÷ 4⌋
~~~

Le résultat est limité de 1 à 5.

Il est possible de dépasser cette intensité. Le lanceur effectue alors un test de contrôle de seuil **22 + 3 par rang excédentaire**. L’échec peut provoquer :

- une perte supplémentaire de Mana ;
- une forme déviée ;
- une interruption ;
- une perte de Concentration ;
- une accumulation élémentaire sur le lanceur ;
- un effet plus faible ;
- une conséquence environnementale liée à la formule.

La conséquence ne doit jamais être choisie dans une table absurde sans rapport avec l’effet lancé.

### Temps de préparation

~~~text
Temps d’incantation =
0,25 seconde
+ 0,15 × Intensité
+ coût total en Mana ÷ 30
~~~

La compétence peut réduire ce temps jusqu’à 30 %. Une formule intense reste toujours lisible et interruptible.

### Puissance commune

Lorsque l’effet a besoin d’une valeur de puissance :

~~~text
Puissance =
10
+ 6 × Intensité
+ ⌊compétence de discipline ÷ 10⌋
+ attribut directeur
~~~

Cette puissance peut devenir chaleur, impulsion, froid, lumière, durée d’illusion, soin ou difficulté de dissipation. Elle ne représente pas toujours des dégâts directs.

### Disciplines

- **Élémentalisme** : chaleur, froid, électricité et réactions élémentaires.
- **Force, discipline magique** : poussée, traction, suspension, barrière et déviation.
- **Altération** : densité, friction, solidité, forme et état matériel.
- **Illusion** : lumière, son, apparence et information sensorielle.
- **Esprit** : attention, émotion, mémoire, Concentration et Volonté.
- **Vitalité, discipline magique** : soin, poison, croissance et fonctions biologiques.
- **Invocation** : appel, ancrage, pacte et renvoi d’une présence.

La magie agit sur les propriétés existantes. Un effet de feu éclaire et produit de la fumée. Un effet de froid peut éteindre une faible combustion. Une illusion sonore peut alerter un garde sans déplacer physiquement un objet.

### Interruption

Une incantation est interrompue si le lanceur :

- perd tout son Équilibre ;
- est mis hors de combat ;
- ne peut plus accomplir les gestes ou conditions requis ;
- perd la ligne nécessaire à l’effet ;
- échoue à maintenir sa Concentration après un Trauma net d’au moins 10.

Le test de maintien utilise la discipline magique et la Volonté contre un seuil de 18, augmenté de la moitié du Trauma reçu.

Une interruption consomme normalement la moitié du Mana préparé. Une réussite fragile peut conserver l’effet avec une puissance ou une durée réduite.

### Contre-jeu

Toute magie puissante offre au moins un contre-jeu :

- interrompre le lanceur ;
- quitter la zone ;
- briser la ligne de vue ;
- bloquer ou détourner le projectile ;
- modifier l’environnement ;
- dissiper ;
- détruire l’ancrage ;
- retourner la réaction contre son auteur.

> **La grammaire des merveilles.** Un même principe de froid peut geler une flaque, refroidir une serrure ou former un écran de cristaux. La forme, l’intensité, la durée et la contrainte expliquent la différence.

## Furtivité et Perception

![À l’aube sur des quais mouillés, des voyageurs utilisent caisses, barque et reflets tandis qu’un garde examine une trace près d’un entrepôt.](../../assets/player-handbook/dawn-harbor-infiltration.png)

*Se cacher ne supprime pas les preuves — Couvertures, lignes de vue, surfaces bruyantes, lumière et traces expliquent ce que chaque observateur peut réellement comprendre.*

### Jauge de détection

Chaque observateur possède une détection locale de 0 à 100.

| Valeur | État |
|---:|---|
| 0 à 19 | Calme |
| 20 à 44 | Suspicion |
| 45 à 74 | Investigation |
| 75 à 99 | Alerte locale |
| 100 | Identification ou combat |

La jauge augmente selon la visibilité, le son, les traces, le comportement et les connaissances de l’observateur. Elle diminue progressivement lorsque rien ne confirme le danger.

### Visibilité

| Condition | Modificateur indicatif |
|---|---:|
| Immobilité | −20 |
| Accroupi en mouvement | −10 |
| Marche | 0 |
| Course | +15 |
| Sprint | +25 |
| Pénombre | −15 |
| Obscurité profonde | −30 |
| Lumière directe | +20 |
| Couverture partielle | −15 |
| Silhouette sur un horizon clair | +15 |
| Armure brillante ou très visible | +10 |

| Distance | Modificateur indicatif |
|---|---:|
| Moins de 5 m | +20 |
| 5 à 12 m | 0 |
| 12 à 25 m | −15 |
| Plus de 25 m | −30 |

Ces valeurs alimentent la vitesse d’accumulation de la détection. Elles ne remplacent ni une ligne de vue réelle ni la Perception de l’observateur.

### Son

Le son est émis par une source et se propage selon les murs, les portes, les surfaces et les conditions.

| Son | Rayon indicatif en espace ouvert |
|---|---:|
| Très faible | 4 m |
| Faible | 8 m |
| Normal | 15 m |
| Fort | 30 m |
| Alarme | 60 m ou plus |

Marcher sur du tissu, de la pierre, des tessons ou une grille métallique ne produit pas le même bruit. Une armure lourde reste utilisable en infiltration, mais demande davantage de préparation.

### Preuves

Une personne peut remarquer :

- une porte forcée ;
- un objet manquant ;
- une torche éteinte ;
- du sang ;
- un corps ;
- une empreinte ;
- une arme abandonnée ;
- un mécanisme saboté ;
- une odeur inhabituelle.

L’absence de vision directe ne signifie donc pas l’absence de conséquence.

### Information locale

Un observateur ne partage pas instantanément son information avec toute la zone. Il doit :

- crier ;
- déclencher une alarme ;
- rejoindre un allié ;
- envoyer un messager ;
- utiliser un dispositif ou une capacité explicite.

Le groupe peut couper une ligne de communication sans effacer ce qui a déjà été vu.

### Attaques furtives

Il n’existe pas de privilège de classe appelé attaque sournoise. Tout personnage peut exploiter :

- une cible inconsciente du danger ;
- une zone non protégée ;
- une posture exposée ;
- un point faible observé ;
- une immobilisation préparée.

La situation produit alors une qualité de contact favorable ou critique. L’arme et la protection continuent de compter.

## Exploration et survie

![Dans une forêt ensoleillée, quatre voyageurs pistent un animal, franchissent un ruisseau, examinent des plantes et observent des oiseaux.](../../assets/player-handbook/bright-forest-exploration.png)

*Voyager est une action — Lire le terrain, sécuriser un passage et comprendre le vivant produisent des choix aussi importants qu’un combat.*

### Déplacement

Le monde de référence autorise :

- marche et course ;
- sprint ;
- accroupissement ;
- saut ;
- escalade contextuelle ;
- échelles et cordes ;
- nage et plongée ;
- déplacement en terrain instable ;
- vol lorsqu’une capacité le permet.

Un test n’est demandé que lorsque le passage présente une difficulté ou un risque.

### Encombrement

| Charge portée | État | Effets |
|---:|---|---|
| 0 à 70 % de la charge sûre | Normal | Aucun |
| 71 à 100 % | Chargé | Sprint et récupération d’Endurance réduits de 10 % |
| 101 à 130 % | Surchargé | Pas de sprint, esquive courte, escalade difficile |
| Plus de 130 % | Immobilisé | Déposer, traîner ou transférer des objets |

L’armure portée compte, mais son poids peut être partiellement réparti par une bonne maîtrise.

### Nage et respiration

~~~text
Respiration calme =
20
+ 5 × Constitution
secondes
~~~

Le temps est divisé par deux lors d’un effort intense ou d’une panique.

L’armure, la charge, le courant et la température influencent la nage. Retirer un sac ou abandonner une arme peut sauver un personnage.

### Vol

Le vol tient compte :

- du poids total ;
- de l’Endurance ou du Mana consommé ;
- du vent ;
- de la hauteur ;
- de la visibilité ;
- des projectiles ;
- des lieux où il est possible d’atterrir.

Voler ne supprime ni la météo, ni le risque de chute, ni les défenses préparées par le monde.

### Température et météo

Le système de survie léger utilise trois états :

- confortable ;
- exposé ;
- dangereux.

L’exposition devient dangereuse selon la durée, le vent, l’humidité, l’abri, les vêtements et l’effort. La météo crée des décisions ; elle ne sert pas seulement à remplir des jauges.

### Faim, soif et fatigue

Référence en mode de survie normal :

- premiers effets de faim après 18 heures sans nourriture ;
- premiers effets de soif après 8 heures sans eau ;
- fatigue après 16 heures d’éveil ;
- aggravation rapide après 24 heures.

Les pénalités affectent d’abord la récupération de l’Endurance, du Mana et de la Concentration. Elles ne diminuent pas immédiatement chaque action.

Un mode de survie détaillé peut ajouter qualité des aliments, maladies, cuisson et exposition prolongée. Il reste optionnel.

### Carte et navigation

Le journal distingue :

- position connue ;
- zone probable ;
- rumeur non vérifiée ;
- itinéraire déjà parcouru ;
- danger signalé ;
- information périmée.

Le voyage rapide avance le temps, consomme les ressources prévues et peut déclencher des événements. Il ne téléporte pas gratuitement le groupe hors des conséquences.

## Dialogues, factions et crimes

![Autour d’une table de guilde, plusieurs personnes négocient tandis que des compagnons observent les réactions et présentent des preuves.](../../assets/player-handbook/social-negotiation.png)

*Plusieurs leviers, plusieurs volontés — Preuve, intérêt, empathie et réputation ouvrent une discussion sans transformer l’interlocuteur en objet.*

### Une parole n’est pas un sort mental

Un test social ne peut pas rendre acceptable une demande fondamentalement impossible sans levier.

Le joueur doit disposer d’au moins un angle crédible :

- intérêt ;
- preuve ;
- dette ;
- peur ;
- autorité ;
- compassion ;
- mensonge plausible ;
- compromis ;
- menace réalisable.

### Résistance sociale

Pour un interlocuteur important :

~~~text
Seuil social =
14
+ Volonté
+ ⌊compétence de résistance ÷ 5⌋
+ contexte
~~~

Le contexte peut compter davantage que la compétence. Montrer le véritable ordre d’un gouverneur peut donner +6. Porter un uniforme inadapté peut imposer −4.

### Résultats sociaux

- **Échec net** : l’interlocuteur ferme cette approche, appelle de l’aide ou exige une garantie.
- **Échec constructif** : information partielle, prix accru, délai ou dette.
- **Réussite fragile** : accord limité avec coût ou surveillance.
- **Réussite propre** : accord aux conditions raisonnablement recherchées.
- **Réussite décisive** : l’interlocuteur prend un risque réel, si cela reste compatible avec ses convictions.

### Relations et réputation

Une relation ne se résume pas à une seule jauge. Elle peut suivre :

- confiance ;
- respect ;
- peur ;
- dette ;
- affection ;
- rivalité ;
- méfiance.

Les factions utilisent au minimum :

- réputation publique ;
- confiance interne ;
- peur inspirée ;
- dette ;
- hostilité.

Chaque axe va de −100 à +100. Les personnes et factions interprètent les mêmes faits selon leurs propres valeurs.

### Crimes

Un crime possède :

- une juridiction ;
- une gravité ;
- un propriétaire lésé ;
- des témoins ;
- des preuves ;
- un niveau d’identification ;
- un temps de transmission.

| Acte | Gravité de base |
|---|---:|
| Intrusion | 5 |
| Vol mineur | 5 + valeur relative de l’objet |
| Vandalisme | 10 à 25 |
| Agression | 20 |
| Sabotage majeur | 30 à 60 |
| Meurtre | 50 |
| Crime contre une faction ou un dirigeant | +10 à +50 |

Les réponses sont graduées : avertissement, amende, restitution, confiscation, arrestation, bannissement, chasse, négociation ou vengeance.

### Témoins et identification

L’identification va de 0 à 100.

- 0 à 24 : description vague ;
- 25 à 49 : silhouette, équipement ou voix ;
- 50 à 74 : suspect crédible ;
- 75 à 99 : identification forte ;
- 100 : certitude directe ou preuve irréfutable.

Un masque protège le visage, pas la voix, la taille, l’arme rare ou le compagnon reconnu.

Un témoin doit transmettre l’information par la parole, un messager, une alarme ou une capacité explicite. Il peut être convaincu, protégé, soudoyé ou intimidé. Le tuer crée un nouveau crime et peut laisser de nouvelles preuves.

## Groupe et compagnons

![Sur une côte lumineuse et venteuse, un groupe étudie une carte, charge un animal de bât, désigne une route et exprime un désaccord.](../../assets/player-handbook/windy-coast-council.png)

*Un plan partagé par des volontés distinctes — Préparer la route, répartir les charges et contester une décision appartiennent à la même vie de groupe.*

### Composition

Le groupe actif comprend le héros et jusqu’à trois compagnons. Tous progressent selon leurs actions.

Le rôle d’un compagnon décrit son équipement, sa doctrine et ses compétences actuelles. Ce n’est pas une classe. Un protecteur peut apprendre l’Illusion ; une négociatrice peut devenir excellente avec une lance.

### Ordres rapides

Ordres universels :

- attaquer ;
- défendre ;
- suivre ;
- tenir ;
- se regrouper ;
- battre en retraite ;
- protéger une personne ;
- ne pas attaquer ;
- rester caché ;
- viser une cible ;
- utiliser une capacité ;
- interagir avec un objet ;
- pousser ;
- préparer une réaction.

Un ordre simple doit être exécutable sans ouvrir un menu complexe.

### Files tactiques

En concentration tactique, le joueur peut placer jusqu’à trois ordres par compagnon.

Un ordre affiche :

- sa cible ;
- son chemin estimé ;
- son coût ;
- sa durée ;
- les risques connus ;
- la condition qui peut l’annuler.

Un compagnon n’exécute pas aveuglément un ordre devenu manifestement impossible. Il signale l’échec et applique sa doctrine.

### Doctrines

| Doctrine | Comportement |
|---|---|
| Prudente | Conserve ses ressources et évite l’exposition |
| Agressive | Cherche les ouvertures et poursuit |
| Défensive | Protège une zone ou un allié |
| Soutien | Soigne, dissipe et contrôle |
| Furtive | Évite les sons et attend une occasion |
| Économe | Préserve consommables et capacités majeures |

Conditions configurables :

- soigner sous un seuil ;
- éviter le feu ;
- ne pas poursuivre ;
- viser les tireurs ;
- conserver une charge majeure ;
- protéger le personnage le plus blessé ;
- pousser une cible proche d’un danger.

### Personnalité et refus

Les convictions influencent les décisions graves, pas chaque seconde de combat.

Un compagnon peut refuser :

- une exécution ;
- une trahison ;
- un sacrifice ;
- un crime majeur ;
- l’usage d’un pouvoir qu’il juge interdit.

Il ne doit pas ignorer arbitrairement un ordre tactique ordinaire au milieu d’une urgence.

### Séparation et contrôle direct

Le joueur peut :

- prendre directement le contrôle d’un compagnon ;
- séparer le groupe ;
- préparer deux accès ;
- laisser une équipe en réserve ;
- ordonner une retraite indépendante.

L’information reste locale. Un personnage séparé ne transmet pas automatiquement ce qu’il voit, sauf liaison ou signal prévu.

> **Quatre volontés, un seul plan.** Une archère coupe une corde, un compagnon protège un prisonnier, un troisième ferme une herse et le héros attire la garde. Chacun exécute une intention comprise plutôt qu’une chorégraphie omnisciente.

## Inventaire, préparation et artisanat

![Sous la verrière d’un atelier lumineux, des voyageurs démontent une arbalète, réparent une armure, mesurent un mélange et comparent des matériaux.](../../assets/player-handbook/glass-roof-workshop.png)

*La qualité vient du travail visible — Outils, matériaux, temps, déchets et préparation donnent un coût concret à chaque réparation et fabrication.*

### Inventaire

L’inventaire général peut contenir tout objet transportable dans la limite de la charge. L’accès immédiat dépend de la préparation.

Équipement rapide de référence :

- deux jeux d’armes ;
- quatre emplacements de ceinture ;
- deux outils ;
- un objet tenu ;
- une munition active.

Changer un objet de ceinture hors combat est immédiat. Réorganiser une sacoche en combat prend du temps et expose le personnage.

### Valeur fonctionnelle des objets

Une arme, un outil ou un ingrédient possède des propriétés :

- matière ;
- masse ;
- volume ;
- équilibre ;
- solidité ;
- tranchant ;
- conductivité ;
- inflammabilité ;
- toxicité ;
- pureté ;
- provenance ;
- légalité ;
- valeur sociale.

Une coupe cérémonielle peut être un récipient médiocre mais une excellente preuve diplomatique.

### Artisanat

Un projet demande :

1. un résultat souhaité ;
2. des propriétés nécessaires ;
3. des matériaux ;
4. un outil ou poste adapté ;
5. du temps ;
6. une compétence pertinente.

| Complexité | Seuil indicatif |
|---|---:|
| Réparation simple | 14 |
| Objet courant | 18 |
| Objet spécialisé | 22 |
| Mécanisme complexe | 26 |
| Ouvrage de maître | 30 |
| Prototype exceptionnel | 34 |

Un échec constructif peut produire une qualité inférieure, un retard ou une consommation supplémentaire. Seul un échec dangereux avec des substances instables provoque une catastrophe.

### Qualité

La qualité va de 1 à 5. Elle améliore surtout :

- la fiabilité ;
- l’économie de matière ;
- le temps d’utilisation ;
- la précision ;
- la résistance ;
- le nombre d’emplacements de modification.

Elle ne multiplie pas sans limite les dégâts.

### Alchimie et Toxicité

Les consommables alchimiques ajoutent une Toxicité temporaire.

| Produit | Toxicité indicative |
|---|---:|
| Tonique léger | 10 |
| Potion courante | 20 |
| Stimulant puissant | 35 |
| Mutation ou élixir extrême | 50 |

Au-dessus de 60, des effets secondaires apparaissent. À 100, le personnage subit un empoisonnement sévère.

La Toxicité décroît lentement et peut être traitée. Boire quatre potions en quelques secondes devient ainsi possible, mais dangereux.

### Pièges

Un piège combine :

- déclencheur ;
- charge ou effet ;
- support ;
- camouflage ;
- condition de sécurité ;
- éventuel mécanisme de désactivation.

Les pièges utilisent les mêmes règles que le monde. Un fil peut être coupé, une dalle bloquée, un gaz ventilé ou une charge récupérée.

## Repos, mort et sauvegarde

### Repos court

Un repos court dure vingt minutes et exige un endroit raisonnablement sûr.

Il permet :

- de remettre les objets rapides en place ;
- de manger et boire ;
- d’appliquer des soins ;
- de récupérer 40 % du Mana manquant ;
- de restaurer les ressources mineures ;
- de stabiliser les blessures ;
- d’échanger l’équipement du groupe.

Il ne guérit pas automatiquement une blessure grave. L’Endurance et l’Équilibre se régénèrent déjà à court terme ; le repos sert surtout aux soins, au Mana, à la préparation et à la consolidation.

### Repos long

Un repos long dure huit heures.

Il restaure normalement :

- toute l’Endurance ;
- tout le Mana ;
- tout l’Équilibre ;
- toute la Concentration ;
- les capacités majeures ;
- la Vitalité jusqu’à la limite permise par les blessures.

| Blessure non traitée | Limite de Vitalité après repos |
|---|---:|
| Mineure | 90 % |
| Grave | 70 % |
| Critique | 50 % |

Un soin approprié supprime ou relève cette limite.

Le repos fait avancer le monde. Une patrouille peut changer, une faction réagir ou une urgence expirer si le joueur en avait été averti.

### Lieu de repos

Un repos long demande au minimum :

- une température supportable ;
- l’absence de danger immédiatement connu ;
- assez d’espace pour s’allonger ou se relayer ;
- une source d’eau disponible ou transportée.

Un camp de mauvaise qualité peut imposer une interruption, une récupération partielle ou un tour de garde. Le jeu doit annoncer le risque estimé avant de demander confirmation.

### Mort

Le réglage normal applique :

- mise hors de combat avant la mort ;
- secours possible ;
- mort immédiate réservée aux dangers extrêmes ;
- résurrection rare et dépendante du module ;
- compagnons soumis aux mêmes règles.

Les modes plus permissifs empêchent la mort permanente des compagnons. Les modes sévères raccourcissent la fenêtre de secours ou autorisent la mort critique.

### Sauvegardes

Le mode normal autorise :

- sauvegarde manuelle ;
- sauvegarde rapide ;
- sauvegarde automatique ;
- plusieurs emplacements.

Une sauvegarde attend au maximum que les contacts physiques immédiats soient sérialisables. Elle ne doit pas modifier le résultat d’un combat, replacer les acteurs ou effacer une réaction déjà engagée.

Sauvegardes automatiques recommandées :

- entrée dans une zone ;
- fin de combat majeur ;
- repos ;
- décision narrative ;
- voyage ;
- acquisition d’un objet unique.

Les états persistants enregistrent les morts importantes, les destructions, les alliances, les crimes connus, les routes et les décisions. Les petits objets sans importance peuvent être restaurés lors du rechargement d’une zone lorsque leur position exacte n’a aucune conséquence.

Un mode optionnel de destinée limite les rechargements. Il ne constitue jamais le réglage imposé.

## Difficulté et accessibilité

### Une difficulté sans ennemis éponges

Les niveaux de difficulté modifient surtout :

- coordination ;
- réaction ;
- précision ;
- agressivité ;
- usage du décor ;
- ressources ;
- blessures ;
- sauvegardes ;
- mort ;
- aides visuelles.

La Vitalité ennemie reste généralement à 100 % de sa valeur normale.

| Réglage | Récit | Voyage | Référence | Tactique | Implacable |
|---|---:|---:|---:|---:|---:|
| Trauma reçu | 65 % | 85 % | 100 % | 110 % | 120 % |
| Coordination ennemie | Faible | Modérée | Complète | Élevée | Maximale |
| Fenêtre de parade | +80 ms | +40 ms | Normale | −20 ms | −40 ms |
| Blessures | Clémentes | Réduites | Normales | Sévères | Très sévères |
| Compagnons | Ne meurent pas | Sauvetage long | Normal | Sauvetage court | Mort critique |
| Concentration tactique | Pause | Vitesse 10 % | Vitesse 15 % | Vitesse 25 % | Vitesse 35 % |

Chaque ligne peut être personnalisée séparément. Le joueur peut également régler :

- le Trauma infligé par les alliés ;
- la rareté des ressources ;
- la vitesse des télégraphes ennemis ;
- la sévérité de la survie ;
- les restrictions de sauvegarde ;
- la permanence de la mort ;
- le nombre d’informations tactiques affichées.

### Accessibilité

Options minimales :

- taille des textes ;
- police lisible ;
- contraste ;
- sous-titres complets ;
- identification du locuteur ;
- réduction des secousses ;
- réduction des flashes ;
- maintien ou bascule des touches ;
- remappage intégral ;
- assistance de visée ;
- vitesse du jeu solo ;
- concentration tactique en pause ;
- visualisation des sons ;
- surbrillance des objets interactifs ;
- contraste des surfaces dangereuses ;
- fenêtres de parade et d’esquive réglables ;
- simplification des actions répétées ;
- options de mouvements de caméra.

Les aides rendent les règles plus perceptibles. Elles ne jouent pas secrètement à la place du joueur et n’empêchent pas une personne de comprendre pourquoi un événement s’est produit.

## Exemples complets

### La sentinelle en plaques

Ilyan manie une épée à une main d’Impact 24. Il frappe normalement le torse d’une sentinelle protégée par 33 contre le tranchant.

~~~text
24 − 0,65 × 33 = 2,55
~~~

La sentinelle reçoit 3 Trauma. Le contact reste visible : métal frappé, recul et perte d’Équilibre, mais la cuirasse remplit son rôle.

Ilyan change de tactique. Il utilise une masse d’Impact 25. La plaque ne possède que 14 contre le contondant.

~~~text
25 − 0,65 × 14 = 15,9
~~~

La sentinelle reçoit 16 Trauma, une blessure mineure et une perte d’Équilibre supérieure.

Ilyan aurait aussi pu :

- viser une articulation avec une estocade ;
- pousser la sentinelle dans l’escalier ;
- mouiller le sol puis le geler ;
- ordonner à un compagnon de l’encercler ;
- négocier avant le combat.

### Le canal électrifié

Sava possède 55 en Élémentalisme, 7 en Intelligence et 6 en Volonté. Son Mana maximal vaut :

~~~text
20 + 6 × 6 + 4 × 7 = 84
~~~

Elle prépare un projectile électrique d’intensité 2.

- forme projectile : 4 Mana ;
- intensité 2 : 8 Mana ;
- coût total immédiat : 12 Mana.

Sa Puissance est :

~~~text
10 + 6 × 2 + ⌊55 ÷ 10⌋ + 7
= 34
~~~

La cible se trouve dans l’eau. Le sort produit son effet normal et l’accumulation de Charge augmente de 50 %. L’électricité se propage aux conducteurs reliés avec une Puissance décroissante.

Sava doit tenir compte de son compagnon qui traverse le même canal. La réaction ne distingue pas arbitrairement alliés et ennemis.

### La négociation du registre

Naël veut convaincre une intendante de lui montrer un registre interdit.

Il possède :

- Négociation 40, soit +8 ;
- Présence 7 ;
- une lettre compromettante, contexte +4.

L’intendante impose un seuil de 26. Naël obtient 9 sur 2d10.

~~~text
9 + 8 + 7 + 4 = 28
~~~

La marge vaut 2 : c’est une réussite fragile. L’intendante accepte, mais surveille la consultation.

Avec un total de 31, la marge aurait été 5 : réussite propre, sans surveillance immédiate. Avec une marge d’au moins 10, elle aurait pu révéler l’existence d’un second registre, si cette confiance restait compatible avec ses intérêts.

Sans la lettre, le résultat aurait été 24 : échec constructif. Elle aurait pu proposer un accès contre un service.

### Le témoin oublié

Le groupe vole une clé dans un entrepôt. Aucun garde ne voit directement le vol, mais un ouvrier aperçoit une silhouette masquée sortir avec un compagnon reconnaissable.

Son identification atteint 55. Il ne connaît pas immédiatement le nom du héros, mais peut accuser le compagnon. S’il rejoint le poste de garde, la faction reçoit cette information.

Le joueur peut :

- lui expliquer la raison du vol ;
- rembourser le propriétaire ;
- l’intimider ;
- intercepter son trajet sans violence ;
- accepter l’enquête ;
- laisser la rumeur se propager.

Le tuer transforme un vol en meurtre et laisse un corps, du sang et un disparu attendu à son poste.

### Le voyage dans les hauts plateaux

Le groupe doit traverser une crête avant la tombée de la nuit. Il reste six heures dans le monde. L’itinéraire direct prend quatre heures, mais traverse un col exposé au vent. Le détour prend sept heures et dépasse donc l’échéance annoncée.

Le groupe choisit le col.

Une première réussite propre de Survie identifie un abri de pierre à mi-chemin. Une réussite fragile aurait trouvé l’abri au prix d’une heure supplémentaire. Un échec constructif aurait permis de poursuivre, mais avec un état d’exposition.

Pendant la montée :

- la combattante chargée transfère une partie de son sac ;
- un compagnon prépare une corde ;
- le personnage le plus perceptif surveille les signes de gel ;
- la personne connaissant l’Altération consolide temporairement une prise.

Lorsque le vent augmente, le groupe peut se réfugier, consommer davantage de ressources ou risquer le passage. Le danger vient d’une décision lisible, pas d’une perte de Vitalité arbitraire.

### La fonderie des prisonniers

Le groupe doit libérer un prisonnier dans une ancienne fonderie.

**Observation :**

- canal sous une grille ;
- brasero suspendu ;
- ouvriers hostiles à leur contremaître ;
- passerelle maintenue par deux chaînes ;
- registre prouvant des enlèvements ;
- garde en plaques ;
- charbon et huile près du four.

**Préparation :**

- la membre la plus discrète du groupe traverse le canal ;
- le héros convainc un ouvrier de retarder l’alarme ;
- un compagnon tient la sortie ;
- Sava prépare un effet de froid.

**Action :**

Le canal est gelé. Une garde glisse mais ne tombe pas automatiquement : elle perd de l’Équilibre. Le héros la pousse pendant qu’elle se rétablit. Le contact et la faible friction rendent la projection possible.

L’alarme est déclenchée par un second garde. Le compagnon à la sortie ferme la herse, isolant les renforts.

**Improvisation :**

Un projectile coupe une chaîne. Le brasero tombe sur la passerelle, enflamme l’huile et menace le prisonnier. Le groupe doit choisir entre :

- poursuivre le contremaître ;
- sauver le prisonnier ;
- récupérer le registre ;
- contenir l’incendie.

**Conséquences :**

- le prisonnier sauvé devient un témoin ;
- les ouvriers se révoltent ;
- le registre brûlé ne peut plus servir de preuve ;
- la fonderie arrêtée affecte l’économie locale ;
- les morts par incendie sont attribuées à la chaîne causale du groupe.

Aucune de ces conséquences n’est une simple variante cosmétique de la même récompense.

## Référence rapide

### Test d’action

~~~text
2d10
+ ⌊Compétence ÷ 5⌋
+ Attribut
+ Contexte
+ Aide
~~~

Seuils : **14 / 18 / 22 / 26 / 30 / 34**.

Avantage : 3d10, garder les deux meilleurs.  
Désavantage : 3d10, garder les deux moins bons.

| Marge | Qualité |
|---:|---|
| −10 ou moins | Échec net |
| −9 à −1 | Échec constructif |
| 0 à 4 | Réussite fragile |
| 5 à 9 | Réussite propre |
| 10 ou plus | Réussite décisive |

### Ressources

~~~text
Vitalité      = 60 + 6 Constitution + 2 Force
Endurance     = 50 + 5 Constitution + 3 Volonté
Mana          = 20 + 6 Volonté + 4 Intelligence
Équilibre     = 40 + 4 Force + 4 Constitution
Concentration = 40 + 4 Perception + 4 Volonté
Charge sûre   = 10 + 4 Force + 2 Constitution kg
~~~

### Blessures

| Trauma net | Conséquence |
|---:|---|
| 0 à 14 | Superficiel |
| 15 à 24 | Mineure |
| 25 à 39 | Grave |
| 40 ou plus | Critique |

### Charge

| Part de la charge sûre | État |
|---:|---|
| 0 à 70 % | Normal |
| 71 à 100 % | Chargé |
| 101 à 130 % | Surchargé |
| Plus de 130 % | Immobilisé |

### Réactions essentielles

~~~text
huile + feu                 → combustion
eau + froid                 → glace
mouillé + électricité       → conduction accrue
gaz inflammable + étincelle → explosion
support détruit + charge    → chute
corde + lame ou feu         → rupture
feu + espace fermé          → fumée et suffocation
~~~

### Repos et secours

| Action | Durée |
|---|---:|
| Stabiliser | 3 secondes |
| Relever | 5 secondes |
| Repos court | 20 minutes |
| Repos long | 8 heures |

### Dix principes à retenir

1. Un contact visible existe toujours.
2. La protection explique la résistance.
3. Le monde obéit aux mêmes règles pour tous.
4. Les compétences progressent par usage significatif.
5. La puissance augmente surtout les possibilités.
6. Toute action importante possède une chaîne causale.
7. L’échec crée une situation jouable.
8. Aucun personnage n’est enfermé dans une classe.
9. Les compagnons sont des personnes, pas des extensions muettes.
10. La difficulté ne doit pas masquer la simulation.

## Lire les illustrations

Les planches montrent des situations possibles, jamais des rôles imposés. Un même personnage peut
tenir une lance, soigner, négocier ou étudier un mécanisme. La lumière varie avec le sujet : les
extérieurs restent lumineux et colorés ; l’obscurité est réservée aux lieux et dangers qui la
justifient. Chaque image dispose d’un texte alternatif et d’une légende afin qu’aucune règle ne dépende
uniquement de la couleur ou de l’illustration.
