# OpenRPG Forge — Ruleset systémique de référence

**Identifiant :** `openrpg-forge.systemic-rpg.v1`  
**Statut :** référence de conception normative  
**Langue de référence :** français  
**Module de démonstration initial :** *Crypte des Cendres*

## 0. Objet du document

Ce document définit le jeu de règles de référence d’OpenRPG Forge. Il ne cherche pas à reproduire un jeu existant. Il assemble trois familles de qualités dans un système cohérent :

1. la profondeur narrative d’un RPG de groupe, avec compagnons, dialogues, factions, choix et conséquences ;
2. la liberté d’exploration et de construction du personnage d’un RPG en monde ouvert ;
3. un combat d’action physique dans lequel la géométrie, les objets, les surfaces, les substances, les mécanismes et la verticalité peuvent servir d’armes.

Le principe directeur est le suivant :

> Le joueur n’affronte pas seulement les ennemis avec son équipement et ses compétences. Il utilise le monde entier comme une extension de son personnage.

Les mots **DOIT**, **DEVRAIT** et **PEUT** ont une valeur normative :

- **DOIT** désigne une règle indispensable au ruleset de référence ;
- **DEVRAIT** désigne une règle attendue, sauf raison technique ou créative documentée ;
- **PEUT** désigne une option de module.

OpenRPG Forge restant un moteur modulaire, un créateur peut remplacer ces règles. Toute dérogation doit toutefois être explicite dans le manifeste du module.

---

## 1. Identité générale

### 1.1 RPG d’action systémique

Le monde fonctionne en temps réel. Le joueur contrôle directement un personnage en trois dimensions : déplacement, visée, blocage, parade, esquive, saut, poussée, saisie, lancer, magie et interaction.

Une attaque dont le volume physique rencontre la cible **DOIT** être considérée comme un contact. Les statistiques déterminent ensuite les conséquences : dégâts, pénétration, déséquilibre, interruption, saignement ou résistance. Elles ne doivent pas transformer un contact visible en « échec » incompréhensible.

La réussite dépend toujours de trois couches :

- la préparation du personnage ;
- l’exécution du joueur ;
- l’exploitation de la situation.

### 1.2 Monde dense, vertical et réactif

La taille de la carte n’est pas une mesure de qualité. Une zone **DOIT** être conçue comme un lieu ayant une fonction crédible, des accès variés et des conséquences persistantes.

Une zone importante **DEVRAIT** contenir :

- au moins une route principale ;
- une route secondaire ou cachée ;
- une possibilité verticale ;
- des informations permettant d’anticiper un danger ;
- plusieurs objets ou structures ayant une fonction systémique ;
- au moins deux approches non équivalentes ;
- une conséquence qui subsiste après le départ du joueur.

Une forteresse n’est pas une succession de salles de combat. Elle est aussi une herse, un réseau de circulation, des réserves, des dortoirs, des officiers, des ouvriers, des prisonniers, des égouts, des points hauts, des alarmes et une chaîne de commandement.

### 1.3 Héros et groupe

Le joueur contrôle principalement son héros. Le ruleset prévoit jusqu’à trois compagnons actifs, avec un groupe de réserve au camp ou dans une base.

Chaque compagnon **DOIT** posséder :

- une personnalité et des objectifs ;
- des convictions et des limites ;
- des relations avec d’autres personnes ou factions ;
- un rôle de combat ;
- des capacités d’exploration ou de dialogue ;
- une autonomie configurable ;
- des réactions aux décisions du joueur.

Le groupe ajoute une profondeur tactique sans supprimer l’immédiateté du combat.

---

## 2. Boucle de jeu

La boucle fondamentale est :

**Observer → comprendre → préparer → agir → improviser → assumer les conséquences.**

### Observer

Le joueur identifie les ennemis, les accès, les hauteurs, les surfaces, les objets mobiles, les structures fragiles, les sources de chaleur, les mécanismes, les témoins et les possibilités de fuite.

### Comprendre

Les indices visuels, sonores, narratifs et matériels permettent de prévoir les réactions du monde. Une corde usée, une poutre fissurée, de l’huile au sol ou un ennemi portant une armure métallique doivent avoir une signification compréhensible.

### Préparer

Le joueur positionne le groupe, choisit son équipement, pose des pièges, déplace des objets, ouvre un accès, détourne une patrouille ou prépare une combinaison élémentaire.

### Agir

L’action se déroule en temps réel. Le joueur peut employer les armes, la magie, le dialogue, la furtivité, le décor ou une combinaison de ces moyens.

### Improviser

Le feu peut se propager, une passerelle peut s’effondrer, un témoin peut fuir, un compagnon peut tomber ou un ennemi peut retourner un mécanisme contre le groupe. Le système **DOIT** accepter les situations émergentes.

### Assumer

Le monde conserve les conséquences importantes : morts, destructions, alarmes, réputation, accès ouverts ou fermés, ressources consommées, factions aidées ou trahies et objectifs devenus impossibles.

---

## 3. Contrôle et caméra

### 3.1 Vues

Le ruleset de référence autorise :

- première personne ;
- troisième personne rapprochée ;
- vue tactique temporaire ;
- vues cinématiques ou libres destinées à l’éditeur et aux séquences.

Les collisions et les règles ne changent pas selon la caméra. La première personne ne doit pas être une caméra désincarnée : les mains, les armes, le bouclier et les pieds doivent correspondre au corps physique.

### 3.2 Actions fondamentales

Tout personnage jouable **DOIT** pouvoir :

- se déplacer, sprinter, sauter et s’accroupir ;
- attaquer rapidement et puissamment ;
- bloquer, parer et esquiver ;
- donner un coup de pied ou pousser ;
- interagir, saisir et lancer ;
- utiliser une capacité ou un consommable rapide ;
- sélectionner ou commander un compagnon.

La poussée est une action fondamentale. Les talents peuvent l’améliorer, mais elle n’est pas réservée à une classe.

### 3.3 Concentration tactique

Le joueur peut activer une concentration tactique. En solo, le module peut ralentir fortement ou arrêter le temps. En multijoueur, le ralentissement doit suivre une politique autoritaire explicite.

La concentration tactique sert à :

- sélectionner un compagnon ;
- placer des ordres ;
- examiner les dangers reconnus ;
- lire les trajectoires approximatives ;
- préparer une combinaison ;
- choisir une capacité sans rompre l’immersion.

Elle ne doit pas être obligatoire contre chaque adversaire ordinaire.

---

## 4. Le monde comme arme

### 4.1 Propriétés plutôt que scripts isolés

Un objet ne devient pas explosif parce qu’un concepteur a placé un événement spécial sur cette instance. Il explose parce qu’il possède une matière, une pression, une température, une résistance et une réaction définies.

Les objets peuvent porter les propriétés suivantes :

- masse, volume, densité et centre de gravité ;
- solidité, fragilité et résistance structurelle ;
- stabilité, ancrage et surface de support ;
- mobilité, saisissabilité et capacité de lancer ;
- inflammabilité, température et capacité thermique ;
- humidité, conductivité et isolation ;
- friction, adhérence et glissance ;
- flottabilité et perméabilité ;
- toxicité, corrosivité et explosivité ;
- tranchant, pénétration et contondance ;
- possibilité d’être coupé, cassé, gelé, escaladé ou activé.

Les mêmes règles doivent s’appliquer aux objets du moteur, aux objets d’un kit et aux objets créés pour un module.

### 4.2 Verbes systémiques

Les capacités et interactions utilisent un vocabulaire commun :

**pousser, tirer, soulever, porter, traîner, lancer, faire tomber, couper, casser, perforer, brûler, refroidir, geler, mouiller, électrifier, empoisonner, remplir, vider, ouvrir, fermer, bloquer, accrocher, détacher, escalader, activer et désactiver.**

Un sort de force applique une impulsion ou une contrainte. Un sort de feu augmente la température. Un sort de froid diminue la température et modifie éventuellement la friction. Les règles n’ont pas à connaître le nom narratif du sort pour résoudre l’interaction.

### 4.3 Niveaux d’interaction

#### Objets portables

Pierres, bouteilles, torches, armes abandonnées, caisses et petits meubles peuvent être portés ou lancés. Leur masse, vitesse et matériau déterminent l’impact.

#### Mécanismes

Leviers, portes, herses, treuils, grues, cages, roues, contrepoids, ascenseurs et pièges peuvent être actionnés, forcés, bloqués, sabotés ou détruits.

#### Structures

Cordes, chaînes, poutres, supports, passerelles, plafonds instables, murs fragiles et planchers pourris peuvent céder selon leur résistance et leur charge.

#### Surfaces et substances

Eau, huile, boue, sang, glace, neige, poison, gaz, braises et liquides alchimiques modifient le déplacement, la visibilité, la température et les réactions élémentaires.

### 4.4 Chaînes de réactions minimales

Le ruleset **DOIT** prendre en charge les chaînes suivantes lorsqu’un module fournit les propriétés nécessaires :

- huile + feu → combustion et propagation ;
- eau + froid → glace et friction réduite ;
- cible mouillée + électricité → conduction renforcée ;
- glace + impact → rupture, glissade ou projection ;
- feu + matière humide → vapeur et réduction de la combustion ;
- feu + espace fermé → fumée, visibilité réduite et suffocation éventuelle ;
- gaz inflammable + étincelle → explosion ;
- corde + lame ou feu → rupture ;
- support détruit + charge → chute ;
- objet lourd + hauteur → impact accru.

### 4.5 Force, masse et stabilité

Une poussée ne doit pas agir comme un interrupteur.

**Impact = force appliquée + vitesse + masse du projectile + maîtrise**

**Résistance = masse de la cible + stabilité + posture + ancrage + protection**

Selon le rapport, la cible peut ne pas bouger, reculer, perdre l’équilibre, tomber, être projetée ou traverser un élément fragile.

Une créature très lourde résiste à une poussée simple, mais peut être rendue vulnérable par la glace, une attaque aux jambes, la destruction de son appui ou l’impact d’une structure.

### 4.6 Chutes

La chute prend en compte :

- hauteur ;
- masse ;
- orientation ;
- surface d’arrivée ;
- protection ;
- capacités acrobatiques ;
- obstacles rencontrés.

Une chute dans des pointes, dans le feu, sous une machine ou hors d’une falaise peut être létale. Les ennemis peuvent employer les mêmes dangers contre le joueur.

### 4.7 Causalité

Le moteur **DOIT** conserver une chaîne de causalité.

Si le joueur coupe une corde, fait tomber un brasero, brise une urne d’huile puis provoque un incendie, les dégâts et les conséquences doivent lui être attribués.

La causalité sert à :

- attribuer progression et butin ;
- mettre à jour une quête ;
- reconnaître un crime ;
- faire réagir les compagnons ;
- alimenter les statistiques et le journal ;
- expliquer l’événement à l’intelligence artificielle.

La créativité ne doit pas réduire arbitrairement les récompenses.

---

## 5. Combat en temps réel

### 5.1 Létalité lisible

Un humain ordinaire ne doit pas absorber de nombreux coups tranchants sans raison visible. La résistance provient d’une armure, d’un bouclier, d’une barrière, d’une anatomie ou d’un pouvoir clairement identifié.

La progression augmente surtout :

- la variété des techniques ;
- la maîtrise du placement ;
- la défense ;
- la récupération ;
- les possibilités de combinaison ;
- la tolérance aux erreurs ;
- le commandement du groupe.

### 5.2 Détection des coups

Les coups utilisent des volumes correspondant à l’arme et au mouvement. Le résultat dépend de la vitesse, de l’angle, de la partie de l’arme, de la zone corporelle, du matériau protecteur et de l’état de la cible.

Une arme longue peut heurter un mur dans un couloir. Une masse peut étourdir à travers une armure. Une lance domine à distance mais devient moins maniable au contact.

### 5.3 Dégâts

Types physiques :

- tranchant ;
- perforant ;
- contondant.

Types énergétiques :

- feu ;
- froid ;
- électricité ;
- poison ;
- acide ;
- force ;
- énergie spirituelle.

La protection est contextuelle. Une plaque résiste au tranchant mais transmet une partie d’un choc. Une armure légère améliore le déplacement, la discrétion et la récupération.

### 5.4 Endurance

L’endurance alimente les actions exigeantes : attaque lourde, blocage, parade, sprint, esquive, saut, escalade, poussée et maintien de certaines capacités.

À faible endurance, la stabilité, la vitesse et la qualité du blocage diminuent. La récupération doit être rapide dès que le personnage cesse les efforts intenses.

### 5.5 Garde et équilibre

Chaque combattant possède une stabilité temporaire. Les impacts lourds, parades parfaites, explosions, chutes et collisions la réduisent.

Une cible dont la stabilité est brisée peut être saisie, poussée, désarmée, interrompue ou frappée de manière critique.

### 5.6 Blocage, parade et esquive

Le blocage absorbe ou détourne un impact contre de l’endurance. La parade exige un timing plus strict et crée une ouverture.

L’esquive repose principalement sur le déplacement physique. Une courte tolérance d’animation est acceptable, mais elle ne doit pas rendre invulnérable dans une trajectoire manifestement dangereuse.

### 5.7 Critiques déterministes

Les coups critiques proviennent surtout d’une situation :

- dos exposé ;
- tête non protégée ;
- cible au sol ;
- point faible identifié ;
- cible gelée, immobilisée ou déséquilibrée ;
- parade parfaite ;
- attaque depuis une hauteur ;
- armure déjà brisée.

L’aléatoire peut moduler le résultat, mais il ne doit pas masquer la cause.

### 5.8 Objets et soins en combat

Utiliser une potion, recharger un dispositif ou changer d’équipement prend du temps et peut être interrompu. Seuls les objets préparés dans les emplacements rapides sont immédiatement disponibles.

---

## 6. Groupe et compagnons

### 6.1 Composition et contrôle

Le groupe actif comprend le héros et jusqu’à trois compagnons. Le joueur peut :

- donner un ordre rapide ;
- configurer une doctrine générale ;
- prendre le contrôle direct d’un autre membre ;
- placer une séquence en concentration tactique ;
- séparer temporairement le groupe.

### 6.2 Ordres rapides

Ordres minimaux :

- attaquer ;
- défendre ;
- suivre ;
- tenir ;
- se regrouper ;
- rester caché ;
- protéger ;
- utiliser une capacité ;
- interagir avec un objet ;
- viser un élément du décor ;
- pousser une cible ;
- battre en retraite ;
- ne pas attaquer.

### 6.3 Doctrines

Un compagnon peut être agressif, prudent, défensif, soutien, furtif ou économe en ressources.

Le joueur peut définir des conditions simples, par exemple : soigner sous un seuil, éviter le feu, viser les archers, conserver les capacités majeures ou pousser une cible proche d’un précipice.

### 6.4 Personnalité et refus

Un compagnon peut refuser une décision grave contraire à ses convictions, sa loyauté ou ses peurs. Les refus ne doivent pas rendre le contrôle tactique imprévisible pendant une urgence ordinaire.

### 6.5 Relations multidimensionnelles

Les relations peuvent comprendre confiance, respect, peur, dette, admiration, rivalité, affection, irritation et méfiance. Une seule jauge d’approbation ne suffit pas à décrire toutes les relations.

### 6.6 Mort et mise hors de combat

À zéro santé, un compagnon est généralement mis hors de combat. Une fenêtre de secours permet de le relever. Les dangers extrêmes peuvent provoquer une mort immédiate selon la difficulté.

---

## 7. Création et progression du héros

### 7.1 Attributs de référence

Le ruleset utilise sept attributs :

- **Force** : impacts, port de charge, poussée, armes lourdes ;
- **Dextérité** : précision, esquive, armes légères, manipulation ;
- **Constitution** : santé, endurance, résistance et récupération ;
- **Intelligence** : mécanismes, magie, alchimie et artisanat ;
- **Volonté** : concentration, réserve magique et résistance mentale ;
- **Perception** : détection, tir, traces et lecture des dangers ;
- **Présence** : persuasion, intimidation, commandement et négociation.

Aucun attribut ne doit être inutile en exploration ou en dialogue.

### 7.2 Origine et archétype

L’origine définit un contexte social, culturel et narratif. L’archétype définit les compétences et l’équipement initiaux sans enfermer le personnage dans une classe définitive.

### 7.3 Progression par usage significatif

Les compétences progressent lorsqu’elles sont employées dans une situation comportant difficulté, risque ou nouveauté. Frapper une cible sans danger ne doit pas produire un entraînement infini.

La progression vient aussi des maîtres, livres, découvertes, quêtes et exercices.

### 7.4 Talents

Les talents doivent surtout débloquer des possibilités :

- désarmer après une parade ;
- saisir une cible déséquilibrée ;
- couper une corde avec un projectile ;
- manipuler une masse supérieure ;
- créer une surface élémentaire ;
- commander deux actions ;
- fabriquer une substance nouvelle ;
- franchir un obstacle autrement.

### 7.5 Pas de croissance exponentielle

Un héros avancé reste vulnérable aux chutes, embuscades, explosions, noyades et pièges puissants. Le monde ne transforme pas automatiquement chaque créature selon son niveau.

---

## 8. Magie systémique

### 8.1 Action sur les propriétés

Le feu chauffe, éclaire, brûle, produit de la fumée et fait fondre.  
Le froid refroidit, gèle, fragilise et réduit certaines combustions.  
L’électricité se propage dans les conducteurs et perturbe certains mécanismes.  
La force pousse, attire, maintient, soulève et détourne.  
L’illusion modifie la perception, la lumière et le son.  
La vitalité soigne, empoisonne ou transforme les fonctions biologiques.

### 8.2 Écoles de référence

- élémentalisme ;
- force ;
- altération ;
- illusion ;
- esprit ;
- vitalité ;
- invocation.

### 8.3 Ressources

Les sorts ordinaires consomment du mana ou une ressource équivalente. Les effets majeurs peuvent utiliser des charges récupérées au repos long. Les personnages martiaux disposent de ressources majeures comparables.

### 8.4 Préparation

Un personnage peut connaître davantage de sorts qu’il ne peut en équiper. Les rituels utilitaires connus restent disponibles hors combat si le temps et les composants sont présents.

### 8.5 Composition

Un sort est construit à partir d’une forme, d’un effet, d’un élément, d’une durée, d’une intensité, d’un coût et de conditions. Cela permet aux modules d’ajouter des capacités sans créer un moteur parallèle.

### 8.6 Contre-jeu

Une capacité puissante doit pouvoir être interrompue, évitée, bloquée, dissipée, détournée ou rendue risquée par l’environnement.

---

## 9. Furtivité et perception

### 9.1 États de vigilance

Les acteurs passent progressivement par :

1. calme ;
2. suspicion ;
3. investigation ;
4. alerte locale ;
5. combat ;
6. recherche ;
7. retour à la vigilance.

### 9.2 Lumière et son

La lumière et le son sont des phénomènes de jeu. Le joueur peut éteindre une torche, déplacer une lanterne, produire un bruit, masquer des pas ou profiter de la météo.

Le poids, la surface, la vitesse, l’acoustique et les collisions déterminent le bruit.

### 9.3 Preuves

Les acteurs peuvent découvrir un corps, du sang, une porte forcée, une lumière éteinte, une arme, un mécanisme saboté ou un objet volé.

### 9.4 Information locale

Un ennemi ne partage pas instantanément sa connaissance avec toute la zone. Il doit crier, utiliser une alarme, rejoindre un allié, envoyer un messager ou employer une capacité explicite.

---

## 10. Dialogues et décisions

### 10.1 Contexte

Les options dépendent de l’origine, des attributs, des compétences, des connaissances, des objets, de l’apparence, de la réputation, des compagnons et des événements récents.

### 10.2 Tests

**Résultat = aptitude + attribut + contexte + aide + tirage**

Le contexte peut être plus important qu’un bonus numérique : preuve, secret, dette, uniforme, témoin ou compagnon légitime.

### 10.3 Échec constructif

Un échec peut augmenter un prix, créer une dette, révéler une partie d’une information, attirer l’attention, ouvrir une autre solution ou provoquer un conflit. Il ne doit pas systématiquement produire une impasse.

### 10.4 Morale et réputation

Les actions sont évaluées par les personnes et factions selon leurs valeurs. La réputation peut comprendre confiance, peur, respect, dette, hostilité et notoriété.

Les rumeurs se propagent selon les témoins et les communications disponibles.

---

## 11. Quêtes systémiques

### 11.1 Objectifs, pas couloirs

Une quête décrit un objectif, des acteurs, des informations, des états et des conséquences. Elle ne doit pas imposer une seule séquence de déclencheurs.

« Entrer dans la forteresse » peut être résolu par la négociation, le vol, l’escalade, les égouts, la capture volontaire, le sabotage, la magie ou la force.

### 11.2 Conséquences physiques reconnues

Désactiver une machine peut signifier utiliser son panneau, couper son alimentation, bloquer un engrenage, voler une pièce, inonder la salle ou détruire l’installation. Les conséquences diffèrent mais l’objectif réel est reconnu.

### 11.3 Personnages non immortels

Les personnages importants ne doivent pas tous être invulnérables. Un décès peut déplacer une responsabilité, révéler des documents, fermer une route ou transformer la quête.

### 11.4 Temps lisible

Une urgence doit être annoncée. Aucun objectif ne doit expirer sur un compteur caché incompréhensible.

---

## 12. Factions, crimes et témoins

Les crimes sont liés à une juridiction. Un acte n’est connu que par un témoin, une preuve, une enquête, une confession ou une capacité explicite.

Les réponses sont graduées : avertissement, amende, confiscation, arrestation, bannissement, chasse, négociation ou vengeance.

Un témoin peut fuir, être convaincu, intimidé, soudoyé, protégé ou transmettre une description imparfaite. Les déguisements, masques et conditions de visibilité influencent l’identification.

---

## 13. Équipement et inventaire

Les objets doivent avoir une identité fonctionnelle, pas seulement une valeur numérique.

Une arme est décrite par son poids, sa longueur, son équilibre, son allonge, ses types de dégâts, sa capacité de parade, son impact sur la garde et ses propriétés.

Les armures influencent protection, poids, bruit, chaleur, mobilité, résistances et apparence sociale.

L’encombrement comporte au minimum trois états : normal, chargé et surchargé. Le changement complet d’armure n’est pas instantané en combat.

Le ruleset évite une durabilité punitive permanente. Les objets improvisés, boucliers et équipements fragiles peuvent néanmoins se briser.

---

## 14. Alchimie, artisanat et pièges

L’artisanat produit des potions, poisons, explosifs, pièges, munitions, améliorations, outils et composants.

Les ingrédients possèdent des propriétés. L’expérimentation peut créer un mélange faible, instable, toxique, inflammable ou inattendu.

Les pièges utilisent les mêmes règles physiques : fil, charge suspendue, surface, déclencheur, obstacle, pression, feu, froid ou poison.

Boire une potion prend du temps et peut être interrompu. Une toxicité temporaire limite l’accumulation abusive.

---

## 15. Exploration et déplacement

Le moteur de référence prend en charge marche, course, sprint, saut, accroupissement, nage, plongée, escalade contextuelle, échelles, cordes et vol autorisé.

Les limites sont expliquées par le relief, la météo, les fortifications, la fatigue, la profondeur ou la magie. Les murs invisibles sont un dernier recours.

### Vol

Le vol dépend du poids, de l’endurance, du mana, du vent, de la hauteur, des projectiles et des défenses du monde.

### Nage

L’armure, l’encombrement, le courant, la température et la respiration influencent la nage.

### Carte et voyage

La carte peut afficher des marqueurs précis, des zones approximatives ou uniquement les informations du journal. Le voyage rapide avance le temps et peut déclencher des événements.

---

## 16. Repos et ressources

Un repos court récupère les ressources mineures, stabilise les blessures et permet une réorganisation limitée.

Un repos long restaure le groupe, prépare les sorts, récupère les capacités majeures, fait progresser le temps et déclenche les scènes de camp.

Le repos exige un lieu suffisamment sûr. La survie détaillée — faim, soif, température — est optionnelle.

---

## 17. Intelligence artificielle

Les créatures agissent selon leurs objectifs, leur perception, leur rôle, leurs capacités, leur peur et les ordres reçus.

Elles peuvent utiliser ou éviter le feu, la glace, les pièges, les portes, les hauteurs, les couvertures et les mécanismes selon leur intelligence.

Les groupes peuvent comporter protecteurs, combattants, tireurs, contrôleurs, soutiens, chefs et éclaireurs.

Le moral peut conduire à la fuite, la reddition, la négociation, l’appel de renfort ou l’usage d’un otage.

L’intelligence artificielle ne lit pas les commandes du joueur. Elle réagit à des indices perceptibles.

---

## 18. Boss et créatures majeures

Les boss obéissent aux mêmes règles, avec une résistance expliquée par leur masse, leur anatomie, leur armure, leur magie ou leur intelligence.

Ils peuvent être brûlés, gelés, aveuglés, déséquilibrés, empoisonnés, piégés ou blessés par le décor.

Une action environnementale majeure peut les vaincre si la préparation est proportionnée. Effondrer une tour entière est différent de lancer une petite caisse sur un géant.

La difficulté vient des capacités, du rythme, de l’arène, des choix et des contre-jeux, pas uniquement d’une réserve de santé excessive.

---

## 19. Conception des zones

Le décor interactif doit découler de la fonction du lieu.

Une forge contient logiquement feu, charbon, eau, métal, chaînes et mécanismes. Un port contient grues, cordes, cargaisons, eau et passerelles. Une cuisine contient huiles, couteaux, récipients et accès de service.

Une arène importante **DEVRAIT** proposer :

- une opportunité immédiatement visible ;
- une opportunité découverte par observation ;
- une combinaison avancée liée à une compétence ou une préparation.

Aucune solution environnementale n’est obligatoire. Le duel, la magie, la furtivité et la diplomatie restent valables.

---

## 20. Difficulté et accessibilité

La difficulté peut modifier la coordination ennemie, la précision, l’agressivité, la sévérité des blessures, les ressources, les aides visuelles, la durée de concentration tactique et la mort permanente.

Les paramètres doivent être configurables séparément.

L’accessibilité doit permettre au minimum de modifier les sous-titres, contrastes, taille des textes, intensité des secousses, maintien ou bascule des touches, assistance de visée, vitesse du jeu solo et lisibilité des objets interactifs.

---

## 21. Sauvegarde et persistance

Le mode normal autorise sauvegardes manuelles, rapides et automatiques. Un mode optionnel peut limiter les retours en arrière.

Niveaux de persistance :

- **temporaire** : petits objets sans importance restaurés après déchargement ;
- **de zone** : portes, pièges, mécanismes et corps conservés pendant la vie de la zone ;
- **mondiale** : morts importantes, destructions, alliances, routes et décisions enregistrées durablement.

Les états systémiques importants doivent être sérialisables sans conserver chaque détail physique inutile.

---

## 22. Contrat OpenRPG Forge

Les règles ne doivent pas être enfermées dans le code. Le créateur peut modifier :

- attributs et compétences ;
- types de dégâts ;
- propriétés physiques ;
- réactions élémentaires ;
- progression ;
- taille du groupe ;
- contrôle temporel ;
- repos et mort ;
- factions et crimes ;
- écoles de magie ;
- force, gravité, vol et nage ;
- expérience et récompenses.

### 22.1 Composants

Un objet peut recevoir des composants tels que :

`physical`, `destructible`, `flammable`, `conductive`, `freezable`, `grabbable`, `throwable`, `climbable`, `structural-support`, `container`, `mechanism`, `trap`, `surface`, `heat-source`, `quest-object` et `faction-property`.

### 22.2 Graphes de réactions

Les réactions sont des règles lisibles et sérialisables :

- température au-dessus du seuil + inflammable → combustion ;
- support détruit → détacher les charges ;
- cible mouillée + électricité → propagation ;
- liquide sous le seuil de gel → surface solide glissante ;
- corde sans résistance → libérer les éléments attachés.

### 22.3 Outils de débogage

L’éditeur doit pouvoir afficher masse, résistance, matériaux, supports, contraintes, zones de dégâts, trajectoires, perception, causalité, témoins et états de quête.

---

## 23. Exemple de situation

Le groupe pénètre dans une ancienne fonderie où un prisonnier est détenu. Le lieu comporte une entrée gardée, un canal, des passerelles, un four, une grue, des chaînes, une réserve de charbon et des ouvriers forcés.

Solutions possibles :

- négocier l’achat du prisonnier ;
- falsifier un ordre ;
- s’introduire par le canal ;
- escalader la cheminée ;
- libérer les ouvriers ;
- voler la clé ;
- ouvrir les conduites ;
- geler l’eau ;
- électrifier la zone ;
- couper une chaîne ;
- bloquer une roue ;
- faire tomber une passerelle ;
- provoquer un incendie ;
- détruire la cellule.

Les conséquences ne sont pas équivalentes. Une inondation arrête la production. Un incendie met les prisonniers en danger. Une révolte crée un nouvel acteur politique. La destruction des registres supprime une preuve. L’action physique devient ainsi une décision narrative.

---

## 24. Principes non négociables

1. **Le décor n’est pas une décoration.**
2. **La physique ne remplace pas le RPG ; elle lui donne un espace d’expression.**
3. **Les statistiques ne remplacent pas le joueur.**
4. **Les ennemis obéissent aux mêmes règles compréhensibles.**
5. **La créativité est récompensée.**
6. **Les échecs créent de nouvelles situations.**
7. **Le monde se souvient des événements importants.**
8. **Les boss ne trichent pas sans justification diégétique.**
9. **La progression augmente les possibilités plus que les multiplicateurs.**
10. **Chaque lieu raconte sa fonction par ses interactions.**
11. **La simulation reste lisible.**
12. **Les règles sont modulaires, inspectables et testables.**

---

## 25. Application à la Crypte des Cendres

La Crypte constitue la première tranche verticale du ruleset. Son parcours historique reste compatible :

**Elyra → Clé de braise → porte scellée → sentinelle → Gardien des Cendres → sortie.**

Le manifeste `apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json` ajoute cinq démonstrateurs :

1. une urne d’huile pouvant être renversée puis enflammée ;
2. une rigole d’eau pouvant être gelée pour augmenter les glissades ;
3. un brasero suspendu pouvant être détaché par la force ;
4. un pilier fracturé pouvant être renversé ;
5. une fosse de pointes dans laquelle une cible peut être projetée.

Les actions de référence sont :

- `Q` : coup de pied / poussée ;
- `F` : feu ;
- `G` : givre ;
- `V` : force / télékinésie ;
- `Tab` : concentration tactique.

Cette tranche verticale ne prétend pas achever à elle seule toutes les règles du présent document. Elle fixe le contrat de données, la causalité environnementale, les contrôles et les premiers comportements jouables qui serviront aux modules suivants.

---

## 26. Critères d’acceptation globaux

Un module déclarant utiliser `openrpg-forge.systemic-rpg.v1` doit au minimum :

- documenter ses approches principales ;
- déclarer les interactions environnementales importantes ;
- conserver une voie non environnementale ;
- utiliser la même santé et le même état de quête pour les dégâts directs et indirects ;
- attribuer les conséquences à la cause initiale ;
- rendre les dangers lisibles ;
- éviter les immunités arbitraires ;
- préserver les sauvegardes ou fournir une migration ;
- exposer les règles au débogage ;
- fournir au moins un test automatisé de contrat.

La formule d’identité finale est :

> Un RPG d’action et de groupe dans lequel le joueur construit librement son héros, voyage avec des compagnons ayant leurs propres convictions, prend des décisions durables et utilise la physique, la magie, les créatures et les mécanismes du monde pour résoudre les situations.
