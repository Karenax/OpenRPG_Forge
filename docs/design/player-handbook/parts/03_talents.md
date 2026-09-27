<!-- handbook-part: talents -->

# Talents : cent manières de devenir soi-même

## Aucun talent n’est une classe

Les talents sont des choix transversaux. Ils ne forment ni classes, ni métiers fermés, ni arbres imposés.

- Tous les personnages peuvent apprendre tous les talents.
- Aucun talent ne dépend d’une origine, d’un rôle, d’un équipement initial ou d’un autre talent.
- Un talent exige uniquement des attributs, des rangs de compétence et, lorsque nécessaire, une connaissance concrète comme une formule ou un effet magique.
- Les talents débloquent surtout de nouveaux verbes, de nouvelles préparations ou de nouvelles réactions. Ils n’augmentent pas artificiellement la Vitalité ou l’Impact de base.
- Un usage significatif d’un talent fait progresser les compétences réellement mobilisées, selon les règles normales de risque, de difficulté et de nouveauté.
- Les adversaires et les compagnons peuvent employer les mêmes talents s’ils remplissent les mêmes conditions.

## Acquisition, coût et rangs

Le parcours général accorde **1 point de talent aux niveaux pairs**. Un point peut être dépensé pendant un repos long.

- Chaque rang d’un talent coûte **1 point de talent**.
- Un talent de rang III coûte donc trois points au total.
- Les rangs doivent être acquis dans l’ordre.
- Les prérequis séparés par des barres correspondent aux rangs I, II et III.
- Un talent à un seul rang coûte également 1 point.
- Aucun achat ne donne directement de rang de compétence.
- Si plusieurs compétences sont proposées, le talent fonctionne avec chaque compétence ayant elle-même atteint le prérequis du rang utilisé.

Exemple : « Lames, Armes d’impact ou Armes d’hast 25/45/65 » signifie que le rang II fonctionne avec une catégorie d’arme seulement si cette catégorie a atteint 45.

Les compétences vont de 0 à 100 et les attributs de 1 à 10. Les abréviations utilisées dans les prérequis sont : **FOR** Force, **DEX** Dextérité, **CON** Constitution, **INT** Intelligence, **VOL** Volonté, **PER** Perception et **PRE** Présence.

Une **compétence d’arme de mêlée** désigne Lames, Armes d’impact, Armes d’hast ou Corps à corps. Une **compétence magique** désigne Élémentalisme, Force, Altération, Illusion, Esprit, Vitalité ou Invocation. Dans ce chapitre, « compétence Force » et « compétence Vitalité » désignent sans ambiguïté les compétences magiques `force_magic` et `vitality_magic` ; la Vitalité sans précision désigne la ressource du personnage.

## Référentiel des 42 compétences

Ces noms et identifiants sont ceux du catalogue universel. Les identifiants restent stables même si un module adapte leur libellé affiché.

| Domaine | Compétence | ID |
|---|---|---|
| Mouvement | Athlétisme | `athletics` |
| Mouvement | Acrobatie | `acrobatics` |
| Mouvement | Escalade | `climbing` |
| Mouvement | Natation | `swimming` |
| Mouvement | Survie | `survival` |
| Mouvement | Discrétion | `stealth` |
| Combat | Lames | `blades` |
| Combat | Armes d’impact | `impact_weapons` |
| Combat | Armes d’hast | `polearms` |
| Combat | Tir | `marksmanship` |
| Combat | Lancer | `throwing` |
| Combat | Corps à corps | `unarmed` |
| Combat | Boucliers | `shields` |
| Combat | Parade | `parry` |
| Combat | Armures | `armor` |
| Combat | Tactique | `tactics` |
| Savoir-faire | Artisanat | `crafting` |
| Savoir-faire | Forge | `smithing` |
| Savoir-faire | Mécanismes | `mechanisms` |
| Savoir-faire | Alchimie | `alchemy` |
| Savoir-faire | Pièges | `traps` |
| Savoir-faire | Médecine | `medicine` |
| Savoir-faire | Érudition | `lore` |
| Savoir-faire | Investigation | `investigation` |
| Relations | Persuasion | `persuasion` |
| Relations | Intimidation | `intimidation` |
| Relations | Tromperie | `deception` |
| Relations | Empathie | `empathy` |
| Relations | Commandement | `leadership` |
| Relations | Négoce | `trade` |
| Observation | Vigilance | `vigilance` |
| Observation | Pistage | `tracking` |
| Observation | Crochetage | `lockpicking` |
| Observation | Escamotage | `sleight_of_hand` |
| Observation | Dressage | `animal_handling` |
| Magie | Élémentalisme | `elementalism` |
| Magie | Force | `force_magic` |
| Magie | Altération | `alteration` |
| Magie | Illusion | `illusion` |
| Magie | Esprit | `spirit` |
| Magie | Vitalité | `vitality_magic` |
| Magie | Invocation | `summoning` |

## Ressources et résolution communes

Les talents ne recalculent pas les maximums. Ils utilisent les ressources du moteur commun :

| Ressource | Maximum de référence |
|---|---:|
| Vitalité | `60 + 6 × Constitution + 2 × Force` |
| Endurance | `50 + 5 × Constitution + 3 × Volonté` |
| Mana | `20 + 6 × Volonté + 4 × Intelligence` |
| Équilibre | `40 + 4 × Force + 4 × Constitution` |
| Concentration | `40 + 4 × Perception + 4 × Volonté` |
| Charge sûre | `10 + 4 × Force + 2 × Constitution` kg |

Un test suit la formule commune :

```text
Résultat = 2d10
         + partie entière de (Compétence / 5)
         + Attribut
         + Contexte
```

Un bonus de +1 s’ajoute au résultat. Un avantage permet de lancer 3d10 et de garder les deux meilleurs dés. Les avantages ne se cumulent pas.

Les coûts en Endurance, Mana, Vitalité ou Concentration sont exprimés en points ou en pourcentage du maximum. Les réductions de même nature ne se cumulent pas : seule la meilleure s’applique. Aucune réduction ne peut ramener une action sous 50 % de son coût de référence, sauf mention explicite.

## Doctrines et exclusions

Une doctrine représente une manière de se préparer, pas une identité permanente.

- Un personnage peut apprendre les deux doctrines d’un même groupe.
- Une seule doctrine de chaque groupe peut être active.
- Changer une doctrine demande 30 secondes de concentration hors combat.
- Une interruption annule le changement.
- Les quatre groupes proposés sont **Allure**, **Initiative**, **Préparation alchimique** et **Flux magique**.

---

## Mobilité

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 1 | **Foulée économe** | III, 1 point par rang | Athlétisme 25/45/65 ; CON 4/6/8 | Réduit de 10/20/30 % le coût d’Endurance du sprint et de la course en montée. | Ne supprime ni pente, chaleur, blessure, manque d’air ou surcharge. Aucun effet à l’état surchargé. |
| 2 | **Réception souple** | III, 1 point par rang | Acrobatie 25/45/65 ; DEX 4/6/8 | Une réception préparée réduit la hauteur de chute effective de 1,5/3/4,5 m et la perte d’Équilibre de 20/35/50 %. | Doit être déclenchée dans les 0,30 seconde précédant l’impact. Pointes, feu, acide et surface trop étroite conservent leurs effets. Une surcharge divise le bénéfice par deux. |
| 3 | **Franchissement continu** | III, 1 point par rang | Acrobatie 25/45/65 ; Athlétisme 25/45/65 | Permet de franchir sans arrêt complet un obstacle stable de 0,9/1,2/1,5 m. L’animation dure 0,8/0,6/0,45 seconde. | Exige au moins une main libre. Une barrière piégée, mobile, brûlante ou fragile reste dangereuse. |
| 4 | **Impulsion murale** | II, 1 point par rang | Acrobatie 45/65 ; DEX 6/8 | Débloque une impulsion sur une paroi : jusqu’à +0,8/+1,4 m de hauteur et 30/50 degrés de réorientation. Coût : 10 % de l’Endurance maximale. | Une seule impulsion par séquence aérienne. Nécessite une surface solide, non huilée et capable de supporter l’impact. |
| 5 | **Prise de corniche** | III, 1 point par rang | Escalade 25/45/65 ; DEX 4/6/8 | Permet de saisir une corniche à une vitesse descendante maximale de 4/6/8 m/s. La saisie coûte 12/10/8 % de l’Endurance maximale. | La corniche doit être atteignable, saisissable et assez solide. Les mains occupées donnent un désavantage au test. |
| 6 | **Nage lestée** | III, 1 point par rang | Natation 25/45/65 ; CON 4/6/8 | Réduit de 20/35/50 % les pénalités de déplacement dues à la charge portée et de 10/20/30 % la consommation d’air. | Ne rend pas flottante une armure dense et ne neutralise ni courant, froid, entrave ou état surchargé. |
| 7 | **Reprise d’appui** | III, 1 point par rang | Acrobatie 25/45/65 ; PER 4/6/8 | Après une glissade ou un trébuchement, permet de choisir la direction de récupération et réduit sa durée de 0,25/0,45/0,65 seconde. | N’accorde aucune invulnérabilité. Un coup, une saisie ou un second obstacle peut interrompre la reprise. |
| 8 | **Course d’élan** | III, 1 point par rang | Athlétisme 25/45/65 ; FOR 4/6/8 | Après 2 secondes de course presque rectiligne, augmente la distance du prochain saut de 15/25/35 % ou l’Impact de la prochaine poussée de 10/20/30 %. | Un virage supérieur à 45 degrés, un choc ou un arrêt annule l’élan. Une poussée renforcée coûte 5 points d’Endurance supplémentaires. |
| 9 | **Passage étroit** | II, 1 point par rang | Acrobatie 45/65 ; PER 5/7 | Permet de progresser accroupi dans un espace contraint à 85/100 % de la vitesse accroupie normale et de se retourner dans un passage large d’au moins 60/50 cm. | Impossible avec un objet rigide plus large que le passage ou à l’état surchargé. Les armes longues restent physiquement gênées. |
| 10 | **Corps fluide** — doctrine Allure | III, 1 point par rang | Acrobatie 45/65/85 ; DEX 6/8/9 | Tant que la doctrine est active et la charge normale, augmente l’accélération latérale de 10/20/30 % et réduit de 10/15/20 % le coût d’une esquive. | Réduit la résistance aux poussées de 10 %. Incompatible avec **Ancrage profond**. Aucun effet avec une charge lourde. |

---

## Mêlée

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 11 | **Mesure de portée** | III, 1 point par rang | Compétence d’arme de mêlée 25/45/65 ; PER 4/6/8 | Après 0,8 seconde passée à maintenir une cible visible dans l’allonge optimale, la prochaine attaque gagne 10/15/20 % d’Impact et +1 au test d’exploitation d’une ouverture. | L’effet expire après 3 secondes, un changement de cible ou une sortie de portée. Il n’augmente pas directement le Trauma. |
| 12 | **Pression sur la garde** | III, 1 point par rang | Compétence d’arme de mêlée 25/45/65 ; FOR 4/6/8 | Une attaque puissante bloquée inflige 20/35/50 % de perte d’Équilibre supplémentaire au défenseur. | Si la garde cède, le Trauma net éventuel est réduit de 15 %. Une esquive ou une déviation évite entièrement l’effet. |
| 13 | **Pointe dans l’ouverture** | III, 1 point par rang | Lames ou Armes d’hast 25/45/65 ; DEX 4/6/8 | Une attaque perforante lancée dans les 0,8 seconde suivant une parade parfaite gagne 15/25/35 % de pénétration. | Exige une pointe réelle et une zone atteignable. Une plaque sans joint exposé conserve sa protection. |
| 14 | **Taille entravante** | III, 1 point par rang | Lames 25/45/65 ; DEX 4/6/8 | Une blessure tranchante localisée à un membre réduit sa vitesse associée de 10/15/20 % pendant 3/4/5 secondes. | L’effet exige une blessure réelle. Armure, absence d’anatomie, immunité biologique ou membre non fonctionnel le neutralisent. |
| 15 | **Crochet désarmant** | III, 1 point par rang | Lames, Armes d’hast ou Corps à corps 45/65/85 ; DEX 6/8/9 | Débloque un test opposé de désarmement contre une cible à 25 % d’Équilibre ou moins, avec un bonus de +1/+2/+3. | Un échec coûte 12 % de l’Endurance maximale et expose l’attaquant pendant 0,6 seconde. Impossible contre une arme intégrée ou solidement attachée. |
| 16 | **Prise sur rupture** | III, 1 point par rang | Corps à corps 45/65/85 ; FOR 6/8/9 | Permet de saisir une cible dont l’Équilibre est brisé jusqu’à 1/1,25/1,5 fois sa propre masse. La prise peut devenir poussée, projection ou immobilisation. | La cible conserve un test opposé. Pointes, feu, alliés, anatomie inhabituelle et différence de taille restent des contre-mesures. |
| 17 | **Mouvement compact** | III, 1 point par rang | Compétence d’arme de mêlée 25/45/65 ; DEX 4/6/8 | En espace trop étroit pour une attaque normale, débloque une frappe compacte infligeant 70/80/90 % de l’Impact normal sans heurter les parois latérales. | L’allonge est réduite de 30 %. La géométrie frontale et la longueur réelle de l’arme restent contraignantes. |
| 18 | **Feinte tangible** — doctrine Initiative | III, 1 point par rang | Compétence d’arme de mêlée 45/65/85 ; DEX 6/8/9 ; INT 5/7/9 | Permet d’annuler une attaque après 35/45/55 % de sa préparation. Un adversaire qui réagit à la feinte subit −1/−2/−3 à sa prochaine défense pendant 1 seconde. | L’annulation consomme 40 % du coût d’Endurance de l’attaque. Aucun effet si l’adversaire ne voit pas, ne réagit pas ou attend volontairement. Incompatible avec **Riposte mesurée**. |
| 19 | **Main complémentaire** | III, 1 point par rang | Lames ou Corps à corps 45/65/85 ; DEX 6/8/9 | Débloque une frappe, une parade ou une interaction de la seconde main. Une frappe secondaire produit 65/75/85 % de son effet normal. | Chaque objet doit pouvoir être manié à une main et conserve ses collisions. Deux contacts simultanés ne cumulent pas deux fois l’Impact sur le même point. |
| 20 | **Riposte mesurée** — doctrine Initiative | III, 1 point par rang | Parade 45/65/85 ; compétence d’arme concernée 25/45/65 ; PER 6/8/9 | Une attaque lancée dans la seconde suivant un blocage ou une parade coûte 10/20/30 % d’Endurance en moins et gagne 10/15/20 % d’Impact. | Les attaques lancées sans défense préalable coûtent 10 % d’Endurance supplémentaires. Une feinte ou une attaque retardée peut attirer une riposte prématurée. Incompatible avec **Feinte tangible**. |

---

## Défense

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 21 | **Garde économe** | III, 1 point par rang | Boucliers ou Parade 25/45/65 ; CON 4/6/8 | Réduit de 10/20/30 % le coût d’Endurance des impacts correctement contenus dans l’angle de garde. | Aucun effet contre une attaque qui contourne la garde, une saisie, une surface dangereuse ou une masse dépassant la capacité du défenseur. |
| 22 | **Parade orientée** | III, 1 point par rang | Parade 25/45/65 ; DEX 4/6/8 | Lors d’une parade réussie, permet de choisir une déviation latérale allant jusqu’à 15/25/35 degrés. | La direction doit être physiquement compatible avec les armes et leur masse. Une arme deux fois plus massive divise l’angle par deux. |
| 23 | **Ancrage profond** — doctrine Allure | III, 1 point par rang | Boucliers ou Armures 45/65/85 ; FOR 6/8/9 ; CON 5/7/9 | Après 0,8 seconde sans déplacement, augmente l’Équilibre de 20/35/50 % et donne +1/+2/+3 aux tests contre poussée ou renversement. | Réduit de 25 % l’accélération initiale. Déplacer un pied de plus de 50 cm annule l’ancrage. Sol mobile ou appui détruit le neutralise. Incompatible avec **Corps fluide**. |
| 24 | **Bouclier itinérant** | III, 1 point par rang | Boucliers 25/45/65 ; Athlétisme 25/45/65 | Permet de maintenir la garde en avançant à 80/90/100 % de la vitesse de marche et d’étendre la couverture à un allié placé à moins de 0,75/1/1,25 m. | Le sprint rompt la garde. Les attaques plongeantes, latérales, explosives ou traversant le matériau restent efficaces. |
| 25 | **Déviation de projectile** | III, 1 point par rang | Parade ou Boucliers 45/65/85 ; PER 6/8/9 ; DEX 6/8/9 | Une parade parfaite peut dévier un projectile de 20/35/50 degrés au lieu de seulement l’arrêter. | Ne renvoie pas automatiquement le projectile vers un ennemi. Impossible contre une explosion, un nuage, une munition trop massive ou un projectile non perçu. |
| 26 | **Couverture jointe** | III, 1 point par rang | Boucliers 25/45/65 ; Tactique 25/45/65 | Deux défenseurs à moins de 1,5 m peuvent répartir 25/35/50 % de la perte d’Équilibre d’un impact touchant leurs gardes superposées. | Les deux paient leur part d’Endurance. Une attaque de zone, un encerclement ou la séparation du duo neutralise la technique. |
| 27 | **Armure apprivoisée** | III, 1 point par rang | Armures 25/45/65 ; CON 4/6/8 | Après huit heures d’usage du même type d’armure, réduit de 10/20/30 % ses pénalités d’Endurance, de bruit de mouvement et de vitesse de récupération. | Ne réduit ni masse, flottabilité, chaleur, encombrement réel ou protection. Le bénéfice disparaît avec une armure d’une construction très différente. |
| 28 | **Relèvement protégé** | III, 1 point par rang | Boucliers ou Parade 45/65/85 ; Acrobatie 25/45/65 | Permet de se relever en conservant une garde de 120/160/200 degrés. Le relèvement dure 1,4/1,15/0,9 seconde et coûte 15 % de l’Endurance maximale. | Une saisie, une attaque venant hors de l’angle ou un sol empêchant l’appui peut interrompre le mouvement. |
| 29 | **Absorption rotative** | III, 1 point par rang | Parade 45/65/85 ; Acrobatie 25/45/65 ; DEX 6/8/9 | Sur un blocage réussi, peut convertir 20/30/40 % de la perte d’Équilibre en un déplacement forcé contrôlé de 0,4/0,7/1 m. | Nécessite un espace libre et un appui praticable. Une falaise, un mur, une immobilisation ou une attaque multidirectionnelle transforme ce déplacement en danger. |
| 30 | **Garde de réserve** | III, 1 point par rang | Boucliers ou Parade 65/85/95 ; VOL 7/9/10 | Quand l’Équilibre devrait tomber à zéro, il reste à 1 et impose seulement un recul de 0,8/0,6/0,4 seconde. Récupération : 30/24/18 secondes. | Coûte immédiatement 20 % de l’Endurance maximale. Ne fonctionne pas à zéro Endurance, contre une chute, un effondrement d’appui ou plusieurs impacts simultanés. |

---

## Combat à distance

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 31 | **Souffle de visée** | III, 1 point par rang | Tir 25/45/65 ; CON 4/6/8 | Après 0,6 seconde de préparation, réduit l’oscillation de visée de 20/35/50 % pendant 3 secondes. | Consomme 2 % de l’Endurance maximale par seconde. Courir, subir un impact ou manquer d’air rompt la concentration. |
| 32 | **Tir en déplacement** | III, 1 point par rang | Tir 25/45/65 ; DEX 4/6/8 | Réduit de 25/50/75 % la pénalité de précision causée par une marche ou un déplacement latéral. | Aucun effet pendant un sprint, une chute ou un mouvement violent. Le projectile est toujours soumis à la trajectoire et aux obstacles. |
| 33 | **Coupe à distance** | III, 1 point par rang | Tir 45/65/85 ; PER 6/8/9 | Permet de viser précisément cordes, sangles et câbles. Donne +1/+2/+3 au test de sectionnement d’une cible de 3/2/1 cm ou moins. | Le projectile doit réellement atteindre et pouvoir endommager la matière. Vent, mouvement, protection et angle insuffisant restent déterminants. |
| 34 | **Trait d’arrêt** | III, 1 point par rang | Tir 45/65/85 ; PER 6/8/9 | Un projectile blessant une jambe, une aile ou un bras en mouvement inflige 20/35/50 % d’Impact supplémentaire et ralentit le membre de 10/15/20 % pendant 2 secondes. | Aucun effet sans blessure réelle, sur une anatomie inadaptée ou à travers une protection qui absorbe l’impact. |
| 35 | **Angle plongeant** | III, 1 point par rang | Tir 25/45/65 ; PER 4/6/8 | Depuis au moins 2 m au-dessus de la cible, une trajectoire plongeante gagne 10/20/30 % de pénétration et ignore 10/20/30 % de la couverture horizontale. | Le tireur doit voir la zone visée. Un plafond, une couverture supérieure ou une cible collée au tireur annule l’avantage. |
| 36 | **Ricochet calculé** | III, 1 point par rang | Tir ou Lancer 45/65/85 ; INT 6/8/9 ; PER 6/8/9 | Débloque une trajectoire à un ricochet sur une surface dure reconnue. Le projectile conserve au maximum 50/60/70 % de son Impulsion. | Le test subit −3/−2/−1. Une surface friable, irrégulière ou trop inclinée produit un rebond imprévisible. |
| 37 | **Rechargement conservé** | III, 1 point par rang | Tir 25/45/65 ; DEX 4/6/8 | Une interruption conserve 40/70/100 % de la progression d’un rechargement. Reprendre l’action dans les 4/6/8 secondes continue depuis ce point. | Une chute de l’arme, une pièce arrachée, l’eau ou un sabotage peuvent toujours annuler le rechargement. |
| 38 | **Munitions préparées** | III, 1 point par rang | Tir 25/45/65 ; Escamotage 25/45/65 | Permet de préparer 3/5/8 groupes de munitions et de changer de groupe en 0,6/0,4/0,25 seconde. | Seules les munitions physiquement placées dans les emplacements préparés sont disponibles. Une main entravée double le temps. |
| 39 | **Lancer pondéré** | III, 1 point par rang | Lancer 25/45/65 ; FOR 4/6/8 | Permet un lancer précis d’un objet pesant jusqu’à 10/20/30 % de plus que la limite précise normale. Donne +1 au test de trajectoire en cloche préparée. | Le coût d’Endurance du lancer augmente de 5/10/15 %. L’objet conserve sa forme, sa prise, son aérodynamique et son risque de rupture. |
| 40 | **Fenêtre d’interruption** | III, 1 point par rang | Tir 45/65/85 ; PER 6/8/9 | Un projectile qui produit assez d’Impact pendant une incantation, un rechargement ou une interaction prolonge cette action de 0,4/0,7/1 seconde. | Une protection rigide, une concentration réussie ou une action sans geste interruptible peut neutraliser l’effet. Une même action ne peut être retardée qu’une fois par tireur. |

---

## Le monde comme arme

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 41 | **Main de levier** | III, 1 point par rang | Athlétisme ou Mécanismes 25/45/65 ; FOR 4/6/8 ; INT 4/6/8 | En utilisant un levier approprié, augmente de 25/50/75 % la masse manipulable lors d’un soulèvement, déplacement ou arrachement lent. | L’action prend 3/2,5/2 fois le temps normal. Le levier, l’appui et la pièce manipulée peuvent casser. |
| 42 | **Lancer d’occasion** | III, 1 point par rang | Lancer 25/45/65 ; Athlétisme 25/45/65 | Permet d’enchaîner saisie et lancer d’un objet proche en 1,2/1/0,8 seconde si sa masse ne dépasse pas 10/15/20 % de la Charge sûre. | L’objet doit être accessible et saisissable. Aucune correction automatique de trajectoire et aucun dégât minimal garanti. |
| 43 | **Poussée oblique** | III, 1 point par rang | Athlétisme 25/45/65 ; Corps à corps 10/25/45 ; FOR 4/6/8 | Permet d’orienter une poussée jusqu’à 20/35/50 degrés sur le côté et donne +1/+2/+3 au test si l’attaque atteint réellement le flanc. | Une cible ancrée, plus massive, dos au mur ou ayant anticipé la direction conserve son avantage physique. |
| 44 | **Lecture des appuis** | III, 1 point par rang | Investigation ou Mécanismes 25/45/65 ; INT 4/6/8 ; PER 4/6/8 | Après 2 secondes d’observation, révèle les supports reconnus et leur charge approximative dans un rayon de 8/12/16 m. Donne +1 aux tests les concernant. | Ne révèle pas une structure cachée sans indice perceptible. Les données restent approximatives si la matière ou l’intérieur sont inconnus. |
| 45 | **Sabotage rapide** | III, 1 point par rang | Mécanismes 45/65/85 ; DEX 6/8/9 | Permet de saboter un mécanisme connu en 4/3/2 secondes au lieu de l’opération complète. | Le test subit −2. Le sabotage laisse des traces visibles et un échec peut bloquer, déclencher ou alerter le mécanisme. Un outil adapté reste nécessaire. |
| 46 | **Déclenchement à couvert** | III, 1 point par rang | Pièges ou Mécanismes 25/45/65 ; INT 4/6/8 | Permet de préparer un lien physique déclenchant un levier, piège ou loquet depuis 6/10/14 m. Préparation : 4/3/2 secondes. | Le lien peut être vu, coupé, coincé ou retourné. Le talent ne commande pas à distance un objet sans connexion réelle. |
| 47 | **Chaîne préméditée** | III, 1 point par rang | Tactique 25/45/65 ; Investigation 25/45/65 ; INT 4/6/8 | En concentration tactique, permet de marquer 2/3/4 éléments environnementaux et d’afficher l’ordre causal prévu. Les alliés avertis gagnent +1 aux tests pour exécuter cette séquence pendant 10 secondes. | Ne renforce aucune propriété physique. Un déplacement, une rupture prématurée, une substance inconnue ou l’action ennemie peut invalider la prévision. |
| 48 | **Bouclier de fortune** | III, 1 point par rang | Boucliers ou Artisanat 25/45/65 ; Athlétisme 10/25/45 ; PER 4/6/8 | Permet de saisir un objet rigide comme couverture en 0,9/0,7/0,5 seconde si sa masse ne dépasse pas 10/15/20 % de la Charge sûre. | La protection dépend uniquement du matériau et de la forme. L’objet gêne la vue, occupe une main et peut casser ou brûler. |
| 49 | **Détournement de charge** | III, 1 point par rang | Mécanismes 45/65/85 ; INT 6/8/9 | Avec 2 secondes de préparation sur une corde, un support ou un contrepoids, permet de modifier la direction initiale de sa chute de 10/20/30 degrés. | Le centre de gravité, la tension et les attaches doivent rendre la trajectoire possible. Une charge déjà en chute ne peut pas être arbitrairement réorientée. |
| 50 | **Réflexe de catastrophe** | III, 1 point par rang | Vigilance 45/65/85 ; PER 6/8/9 | Lorsqu’un effondrement, une explosion ou une chute de charge perceptible commence à moins de 6/8/10 m, donne immédiatement avantage au premier test d’évasion et avertit les compagnons proches. Récupération : 30/24/18 secondes. | N’accorde aucune immunité et ne fonctionne pas contre un danger imperceptible ou sans issue physique. Une seconde catastrophe utilise les règles normales. |

---

## Survie et furtivité

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 51 | **Pas de matière** | III, 1 point par rang | Discrétion 25/45/65 ; Survie 25/45/65 ; PER 4/6/8 | Après 2 secondes d’observation du sol, réduit de 20/35/50 % le bruit des pas en adaptant cadence et appui. | La vitesse est limitée à la marche, puis au petit trot au rang III. Débris mobiles, eau profonde, course ou poussée ennemie annulent l’adaptation. |
| 52 | **Ombre traversante** | III, 1 point par rang | Discrétion 45/65/85 ; DEX 6/8/9 | Permet de franchir 4/6/8 m entre deux zones sombres à 70/80/90 % de la vitesse accroupie sans produire de suspicion supplémentaire liée au mouvement. | Ce n’est pas une invisibilité. Une ligne de vue directe, une lumière déplacée, le son, l’odeur ou un contact révèlent normalement le personnage. |
| 53 | **Effacement méthodique** | III, 1 point par rang | Discrétion ou Investigation 25/45/65 ; INT 4/6/8 | Permet de traiter une trace, tache ou preuve simple en 4/3/2 secondes. Le test reçoit +1/+2/+3 pour éviter de créer une nouvelle preuve. | Nécessite les moyens matériels adéquats. Une preuve déjà observée ou mémorisée n’est pas effacée de l’esprit des témoins. |
| 54 | **Camouflage local** | III, 1 point par rang | Survie 25/45/65 ; Discrétion 25/45/65 | En 30/20/10 secondes, applique un camouflage procurant −1/−2/−3 aux tests visuels adverses tant que le porteur reste dans le milieu correspondant. | Mouvement rapide, pluie, proximité, odeur, silhouette ou changement de biome réduisent ou annulent le bénéfice. |
| 55 | **Souffle contenu** | III, 1 point par rang | Natation ou Discrétion 25/45/65 ; CON 4/6/8 ; VOL 4/6/8 | Multiplie par 1,5/2/2,5 la durée normale de rétention du souffle et réduit de moitié le bruit respiratoire. | Attaquer, sprinter, subir un choc ou inhaler un toxique accélère normalement l’asphyxie. Ce talent ne filtre pas l’air. |
| 56 | **Chronique des traces** | III, 1 point par rang | Pistage 25/45/65 ; PER 4/6/8 | Une piste étudiée pendant 3 secondes révèle direction et classe de taille, puis âge approximatif à 25 %, puis charge, allure ou blessure probable. | Météo, foule, terrain stérile et contre-pistage augmentent le seuil. Le talent ne révèle pas une identité sans indice distinctif. |
| 57 | **Veille tournante** | III, 1 point par rang | Vigilance 25/45/65 ; Tactique 25/45/65 | Pendant un repos, organise 2/3/4 secteurs de surveillance. Le veilleur assigné gagne +1/+2/+3 au premier test contre une approche dans son secteur. | Aucun bénéfice si le veilleur dort, abandonne son poste ou surveille un secteur incorrect. Une diversion peut attirer le regard ailleurs. |
| 58 | **Bivouac discret** | III, 1 point par rang | Survie 25/45/65 ; Discrétion 25/45/65 | En 8/6/4 minutes, réduit de 25/40/55 % la portée perceptible de la lumière, des odeurs et des bruits ordinaires du camp. | Exige un terrain et des matériaux convenables. Un grand feu, des animaux agités, une tempête ou une patrouille proche restent détectables. |
| 59 | **Fuite balisée** | III, 1 point par rang | Survie 25/45/65 ; Tactique 25/45/65 | Permet de mémoriser 2/3/4 points de repli par zone. En combat, le groupe voit une route vers le point choisi et réduit de 20/35/50 % ses hésitations de navigation. | La route n’est pas garantie : porte fermée, feu, ennemi, effondrement ou déplacement du terrain peuvent la couper. |
| 60 | **Embuscade mesurée** | III, 1 point par rang | Discrétion 45/65/85 ; Tactique 25/45/65 ; PER 6/8/9 | La première attaque depuis une dissimulation contre une cible calme ou soupçonneuse inflige 20/35/50 % d’Impact supplémentaire et retarde son appel d’alarme de 0,5/0,8/1,1 seconde si elle touche. | Aucun bonus direct de Trauma. Inactif contre une cible déjà en combat, avertie de la position ou protégée de l’Impact. |

---

## Artisanat, alchimie et pièges

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 61 | **Diagnostic de matière** | III, 1 point par rang | Artisanat ou Forge 25/45/65 ; INT 4/6/8 | Après 3 secondes d’examen, identifie le matériau principal et les dommages, puis les résistances approximatives, puis les contraintes et réparations possibles. Donne +1 au prochain travail correspondant. | Ne révèle pas une propriété interne, magique ou contrefaite sans outil ou indice approprié. |
| 62 | **Réparation de campagne** | III, 1 point par rang | Artisanat ou Forge 25/45/65 ; DEX 4/6/8 | Avec un nécessaire, restaure 20/35/50 % de la condition perdue d’un objet non détruit en 20/15/10 secondes. | Une réparation de terrain ne dépasse pas 80 % de condition et ne recrée pas une pièce manquante. L’action est interruptible. |
| 63 | **Formule stable** — doctrine Préparation alchimique | III, 1 point par rang | Alchimie 25/45/65 ; INT 4/6/8 | Réduit de 20/35/50 % la variabilité, la Toxicité accidentelle et le risque d’instabilité d’une formule connue. | Réduit de 10 % son intensité maximale et double le temps d’improvisation. Incompatible avec **Mélange vif**. |
| 64 | **Mélange vif** — doctrine Préparation alchimique | III, 1 point par rang | Alchimie 45/65/85 ; INT 6/8/9 ; DEX 5/7/9 | Permet d’improviser une formule connue en 3/2/1 minute et augmente son intensité de 10/15/20 %. | Un échec augmente d’un degré la gravité de l’instabilité. Le mélange expire après 30/60/120 minutes. Incompatible avec **Formule stable**. |
| 65 | **Neutralisation ciblée** | III, 1 point par rang | Alchimie ou Médecine 25/45/65 ; INT 4/6/8 | Après identification d’un poison ou acide, permet de préparer en 2/1,5/1 minute un neutralisant réduisant son intensité de 1/2/3 degrés. | Exige un réactif compatible. Un mauvais diagnostic peut accélérer la réaction. Ne rend pas la Vitalité déjà perdue. |
| 66 | **Solvant sélectif** | III, 1 point par rang | Alchimie 45/65/85 ; Artisanat 25/45/65 | Prépare un produit attaquant une famille de matières. Réduit de 25/50/75 % les dégâts collatéraux sur une matière adjacente choisie. | Aucun solvant n’est universel. Temps, température, couche protectrice et contamination modifient la réaction. |
| 67 | **Charge contenue** | III, 1 point par rang | Alchimie 45/65/85 ; Pièges 25/45/65 ; INT 6/8/9 | Permet de façonner une charge en cône de 60/45/30 degrés, en réduisant de 20/35/50 % l’Impulsion hors du cône. | La quantité totale d’énergie ne change pas. Un confinement défectueux, un déplacement ou une paroi réfléchissante peut retourner l’explosion. |
| 68 | **Piège récupérable** | III, 1 point par rang | Pièges 25/45/65 ; DEX 4/6/8 | Le démontage réussi d’un piège intact récupère 60/80/100 % de ses composants réutilisables et prend 80/65/50 % du temps normal. | Une charge consommée, une pièce brisée ou un poison répandu n’est pas recréé. Un piège inconnu exige toujours identification et test. |
| 69 | **Munition de propriété** | III, 1 point par rang | Artisanat ou Alchimie 25/45/65 ; Tir ou Lancer 25/45/65 | Prépare 3/5/8 projectiles portant une propriété connue : chaleur, froid, conductivité, adhérence, fumée, toxine ou corrosion. | Une seule préparation active par projectile. L’effet dépend de la matière, du contact et de l’environnement ; il n’ajoute pas automatiquement du Trauma. |
| 70 | **Conception modulaire** | III, 1 point par rang | Artisanat 45/65/85 ; Mécanismes 25/45/65 ; INT 6/8/9 | Permet d’assembler 2/3/4 composants connus en un dispositif temporaire. Complexité maximale : 4/7/10. Préparation minimale : 5/3/2 minutes. | Chaque fonction nécessite un composant réel. Encombrement, énergie, compatibilité et panne sont résolus séparément. Un échec peut consommer une pièce. |

---

## Social et commandement

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 71 | **Écoute des silences** | III, 1 point par rang | Empathie 25/45/65 ; PER 4/6/8 | Après 30/20/10 secondes de conversation, permet de demander un indice sur une hésitation, une émotion dominante ou un sujet évité. Donne +1 au prochain test qui exploite correctement cet indice. | Un test opposé de Tromperie peut masquer ou fabriquer un signal. Le talent ne révèle jamais automatiquement la vérité complète. |
| 72 | **Argument étayé** | III, 1 point par rang | Persuasion 25/45/65 ; Érudition ou Investigation 25/45/65 | Présenter une preuve pertinente donne +1/+2/+3 au test concernant exactement le fait démontré. Deux preuves indépendantes accordent avantage au rang III. | Une preuve falsifiée, hors sujet ou discréditée peut produire la pénalité inverse. Une conviction morale n’est pas nécessairement réfutée par un fait. |
| 73 | **Menace crédible** | III, 1 point par rang | Intimidation 25/45/65 ; PRE 4/6/8 | Lorsqu’un moyen réel d’exécuter la menace est visible, donne +1/+2/+3 au test et permet de proposer une reddition limitée plutôt qu’une soumission totale. | La cible peut fuir, appeler à l’aide, préférer mourir ou préparer une vengeance. Une menace découverte comme mensonge augmente l’hostilité. |
| 74 | **Marchandage gradué** | III, 1 point par rang | Négoce 25/45/65 ; Empathie 25/45/65 | Après avoir identifié la priorité de l’interlocuteur, permet d’échanger jusqu’à 1/2/3 concessions. Chaque concession pertinente peut améliorer le prix de 5 %, jusqu’à 5/10/15 %. | Ne franchit jamais le seuil de refus économique ou moral du vendeur. La négociation prend au moins une minute par concession. |
| 75 | **Désamorçage public** | III, 1 point par rang | Persuasion ou Empathie 45/65/85 ; PRE 6/8/9 | En 3 secondes, tente de faire baisser d’un degré l’hostilité d’un groupe pouvant entendre. Bonus de +1/+2/+3 si une issue honorable est réellement proposée. | Une attaque en cours, une haine irréconciliable ou l’absence de langue commune augmente fortement le seuil. Une seule tentative par incident. |
| 76 | **Ordre bref** | III, 1 point par rang | Commandement 25/45/65 ; PRE 4/6/8 | Porte la distance d’un ordre rapide à 12/20/30 m et réduit de 0,2/0,35/0,5 seconde le délai de réaction d’un compagnon préparé. | Le compagnon doit entendre, comprendre et pouvoir exécuter l’ordre. Bruit, peur, désaccord grave ou obstacle physique restent valables. |
| 77 | **Plan partagé** | III, 1 point par rang | Tactique 25/45/65 ; Commandement 25/45/65 ; INT 4/6/8 | En concentration tactique, enregistre 2/3/4 actions liées. Chaque participant gagne +1 au premier test exécuté conformément au plan dans les 8 secondes. | Une action imprévue, une cible déplacée ou un ordre incompatible rompt la chaîne pour le participant concerné. |
| 78 | **Ralliement lucide** | III, 1 point par rang | Commandement 45/65/85 ; PRE 6/8/9 | Une exhortation de 2 secondes restaure 15/25/35 % de l’Équilibre et donne +1 au prochain test de peur des alliés à 6/10/14 m. Récupération par cible : 30 secondes. | Exige d’être entendu et reconnu. Ne rend aucune Vitalité, ne supprime pas le contrôle mental et peut révéler la position du groupe. |
| 79 | **Témoin sous protection** | III, 1 point par rang | Persuasion ou Commandement 25/45/65 ; PRE 4/6/8 | Une personne ayant accepté la protection gagne +1/+2/+3 aux tests de peur et d’identification tant qu’un protecteur reste à moins de 4/6/8 m. | Abandonner, menacer ou mettre volontairement le témoin en danger annule le bénéfice et crée une conséquence de réputation. |
| 80 | **Réseau de dettes** | III, 1 point par rang | Négoce ou Persuasion 45/65/85 ; PRE 6/8/9 | Permet d’entretenir 1/2/3 faveurs documentées par établissement. Une faveur peut fournir information, hébergement, introduction ou matériel courant dans les limites du contact. | Chaque usage consomme ou transforme la dette. Aucun contact n’apparaît sans relation préalable et personne ne sacrifie automatiquement sa vie ou sa faction. |

---

## Magie systémique

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 81 | **Forme sculptée** | III, 1 point par rang | Une compétence magique 25/45/65 ; INT 4/6/8 | Permet de réduire ou décaler la forme d’un effet connu : cône resserré de 25/40/55 %, zone déplacée de 0,5/1/1,5 m ou ligne raccourcie sans réduire son intensité. | Ne crée pas une forme inconnue et n’augmente ni portée ni énergie totale. Le temps d’incantation augmente de 0,3 seconde. |
| 82 | **Source stable** — doctrine Flux magique | III, 1 point par rang | Une compétence magique 25/45/65 ; VOL 4/6/8 | Donne +1/+2/+3 aux tests pour maintenir une incantation après blessure, déplacement ou perturbation. | Le coût de Mana augmente de 10 % et l’intensité maximale baisse d’un degré. Incompatible avec **Flux ardent**. |
| 83 | **Flux ardent** — doctrine Flux magique | III, 1 point par rang | Une compétence magique 45/65/85 ; VOL 6/8/9 | Augmente de 10/15/20 % l’Impulsion, la température transférée ou l’intensité biologique d’un effet magique. | Coût de Mana +25 % et −1/−2/−3 au test de maintien en cas d’interruption. Incompatible avec **Source stable**. |
| 84 | **Surface inscrite** | III, 1 point par rang | Élémentalisme ou Altération 45/65/85 ; INT 6/8/9 | Un effet de zone peut laisser une surface de 1/2/3 m de rayon pendant 4/6/8 secondes : chaleur, glace, humidité, adhérence ou corrosion connue. | Coût de Mana +20 %. Vent, drainage, isolation, pluie, absorption et contre-propriété modifient normalement la surface. |
| 85 | **Conduction choisie** | III, 1 point par rang | Élémentalisme, compétence Force ou compétence Vitalité 45/65/85 ; PER 6/8/9 | Permet à un effet conducteur de franchir 1/2/3 connexions physiques supplémentaires distantes de 2 m au maximum. Chaque connexion conserve 60/50/40 % de l’intensité initiale. | L’effet suit réellement conducteurs, fluides, corps ou liens. Le lanceur ne peut pas ignorer un conducteur intermédiaire non protégé. |
| 86 | **Voile concordant** | III, 1 point par rang | Illusion ou Esprit 25/45/65 ; INT 4/6/8 ; PER 4/6/8 | Une illusion connue peut coordonner 2/3/4 canaux parmi vision, son, odeur, température apparente et sensation mentale. Les tests d’examen superficiel subissent −1/−2/−3. | L’illusion ne crée ni masse, chaleur réelle, barrière ou son hors de sa portée. Contact physique, incohérence et Investigation peuvent la révéler. |
| 87 | **Suture relayée** | III, 1 point par rang | Compétence Vitalité 25/45/65 ; VOL 4/6/8 | Lors d’un soin ou d’une neutralisation en contact, permet de rediriger 25/40/55 % de l’effet vers une seconde cible à 2/3/4 m. | L’effet total est partagé, jamais créé. Coût de Mana +15/25/35 %. Le lien visible est interrompu par obstacle, distance ou perte de concentration. |
| 88 | **Rupture de trame** | III, 1 point par rang | Compétence magique concernée 45/65/85 ; Érudition 25/45/65 ; PER 6/8/9 | Débloque une réaction de 0,4 seconde contre un effet magique perçu. Un test opposé réussi réduit son intensité de 1/2/3 degrés ou rompt un maintien du même degré. | Coûte 20 % du Mana maximal. Il faut reconnaître l’effet et disposer d’un principe compatible. Un échec de marge 5 ou plus provoque une interruption de 1 seconde. |
| 89 | **Double propriété** | III, 1 point par rang | Deux compétences magiques 45/65/85 ; INT 6/8/9 | Permet de combiner deux effets connus dans une seule incantation. Chaque propriété agit à 60/70/80 % de son intensité normale. | Coût de Mana +40 %, préparation +0,8 seconde. Les propriétés incompatibles peuvent se neutraliser ou produire une réaction dangereuse. |
| 90 | **Invocation bornée** | III, 1 point par rang | Invocation 25/45/65 ; VOL 4/6/8 ; Commandement ou Dressage 25/45/65 | Une créature invoquée peut recevoir 1/2/3 ordres en attente et agir jusqu’à 12/20/30 m de son ancre. Réduit son entretien en Mana de 10/20/30 %. | Une seule invocation autonome à la fois. Détruire l’ancre, rompre le pacte, effrayer ou bannir la créature reste possible. Les ordres impossibles sont refusés. |

---

## Groupe et résilience

| Nº | Talent | Rangs et coût | Prérequis | Effet chiffré | Limite et contre-jeu |
|---:|---|---|---|---|---|
| 91 | **Secours en mouvement** | III, 1 point par rang | Médecine 25/45/65 ; Athlétisme 25/45/65 | Permet de saisir un allié à terre et de le traîner à 60/75/90 % de la vitesse de marche tout en maintenant une compression simple. | Occupe une main, réduit l’angle de garde de moitié et ne stabilise pas une blessure nécessitant une intervention complète. |
| 92 | **Compression d’urgence** | III, 1 point par rang | Médecine 25/45/65 ; DEX 4/6/8 | En 3/2,2/1,5 secondes, réduit un saignement accessible d’un rang pendant 20/40/60 secondes et prolonge d’autant la fenêtre de secours. | Nécessite une main, un accès à la plaie et du matériel adapté. Mouvement violent, nouvelle blessure ou interruption peut rouvrir le saignement. |
| 93 | **Portage coordonné** | III, 1 point par rang | Athlétisme 25/45/65 ; Tactique 25/45/65 | Deux porteurs peuvent utiliser 75/90/100 % de la somme de leurs Charges sûres et avancer à 50/65/80 % de la vitesse normale. | Les deux doivent rester coordonnés et disposer de prises. Escalier, passage étroit, attaque ou perte d’un porteur impose un nouveau test. |
| 94 | **Formation souple** | III, 1 point par rang | Tactique 25/45/65 ; PER 4/6/8 | Des alliés à moins de 3 m couvrant des directions différentes gagnent 10/15/20 % d’Équilibre et réduisent de 20/35/50 % leurs collisions mutuelles. | Aucun effet si tous regardent la même menace. Explosion, terrain étroit, poussée massive ou séparation brise la formation. |
| 95 | **Repli couvert** | III, 1 point par rang | Tactique ou Commandement 45/65/85 ; PRE 6/8/9 | Pendant 4 secondes après un ordre de repli, les alliés préparés reculent à 75/85/95 % de leur vitesse normale et réduisent de 10/15/20 % le coût du blocage. Récupération : 20 secondes. | L’ordre doit être reçu. Une route coupée, une panique, un encerclement ou un allié immobilisé limite le repli. |
| 96 | **Vigilance croisée** | III, 1 point par rang | Vigilance 25/45/65 ; Tactique 25/45/65 | Deux alliés couvrant des secteurs distincts de 90 degrés gagnent +1/+2/+3 au premier test de détection dans leur secteur et transmettent l’alerte en 0,5/0,35/0,2 seconde. | Une diversion peut attirer un secteur. Mur, bruit, illusion et communication coupée empêchent la transmission. |
| 97 | **Relais de souffle** | III, 1 point par rang | Médecine ou Commandement 45/65/85 ; CON 6/8/9 | En 1,5 seconde de contact, transfère 10/20/30 % de son Endurance maximale à un allié avec 80/90/100 % d’efficacité. | Le donneur ne peut pas descendre sous 20 % d’Endurance. Les deux sont vulnérables pendant le transfert. Une cible ne peut en recevoir qu’un toutes les 20 secondes. |
| 98 | **Sang-froid transmis** | III, 1 point par rang | Commandement ou Empathie 45/65/85 ; PRE 6/8/9 ; VOL 5/7/9 | En 2 secondes, permet aux alliés à 6/10/14 m de refaire un test de peur avec +1/+2 ou avec avantage au rang III. | Une seule reprise par source de peur. Exige communication et compréhension ; ne supprime ni contrôle mental, douleur ou danger réel. |
| 99 | **Refus de céder** | I, 1 point | Athlétisme ou Survie 85 ; CON 8 ; VOL 9 | Une fois par repos long, une atteinte non catastrophique qui devrait mettre le personnage hors de combat le laisse à 1 point de Vitalité pendant 3 secondes. Il peut seulement se déplacer, bloquer, appeler ou employer un secours. | Dégâts persistants, décapitation, noyade, chute létale et nouvelle atteinte peuvent toujours l’abattre. Aucun soin automatique n’est accordé. |
| 100 | **Relais de commandement** | III, 1 point par rang | Commandement 25/45/65 ; Tactique 25/45/65 ; PRE 4/6/8 | Si le donneur d’ordres tombe ou perd le contact, un allié qualifié reprend automatiquement 2/3/4 ordres préparés et leur priorité pendant 10/20/30 secondes. | Le relais ne donne pas les talents, effets magiques ou connaissances manquants. Isolement, conflit de loyauté ou communication impossible interrompent la continuité. |

## Garde-fous d’équilibrage

- Un talent ne transforme jamais un contact visible en échec arbitraire.
- Aucun bonus ne remplace la masse, la géométrie, les matériaux, la visibilité ou la causalité.
- Les réductions de coût ne peuvent jamais ramener une action sous 50 % de son coût de base.
- Les bonus de même nature ne se cumulent pas ; seul le meilleur s’applique.
- Un même effet de contrôle issu du même talent ne peut être renouvelé avant la fin de sa durée.
- Un talent donnant avantage ne peut jamais produire un « double avantage ».
- Une créature immunisée doit l’être pour une raison perceptible : anatomie, matière, ancrage, équipement ou état.
- Les talents créent des options et de la maîtrise ; ils ne rendent pas le héros invulnérable aux chutes, pièges, embuscades, explosions, noyades ou décisions imprudentes.
