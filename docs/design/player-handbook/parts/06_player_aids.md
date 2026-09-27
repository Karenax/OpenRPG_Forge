<!-- handbook-part: player-aids -->

# Aides de jeu

## Feuille de personnage

La feuille doit montrer ce que le personnage est devenu sans lui attribuer de classe. Une interface numérique peut présenter les mêmes champs avec filtres et historique explicable.

### Identité

| Champ | Valeur |
|---|---|
| Nom |  |
| Phrase d’identité |  |
| Origine |  |
| Langues ou communications |  |
| Contact |  |
| Obligation ou dette |  |
| Conviction |  |
| Objectif immédiat |  |
| Relation importante |  |
| Peur ou limite |  |
| Réputation initiale |  |

### Attributs et ressources

| Attribut | Valeur | Ressource | Actuelle | Maximum |
|---|---:|---|---:|---:|
| Force |  | Vitalité |  | `60 + 6 CON + 2 FOR` |
| Dextérité |  | Endurance |  | `50 + 5 CON + 3 VOL` |
| Constitution |  | Mana |  | `20 + 6 VOL + 4 INT` |
| Intelligence |  | Équilibre |  | `40 + 4 FOR + 4 CON` |
| Volonté |  | Concentration |  | `40 + 4 PER + 4 VOL` |
| Perception |  | Charge sûre |  | `10 + 4 FOR + 2 CON kg` |
| Présence |  | Points de talent |  | — |

### Parcours

| Valeur | Total |
|---|---:|
| Niveau global |  |
| Jalons |  |
| Prochain niveau |  |
| Points d’attribut disponibles |  |

### Compétences

Pour chaque compétence, noter le score, les traces conservées et le plus haut palier qualifié.

| Domaine | Compétence | Score | Traces | Preuve de palier | Acquis actifs |
|---|---|---:|---:|---|---|
| Mouvement | Athlétisme |  |  |  |  |
| Mouvement | Acrobatie |  |  |  |  |
| Mouvement | Escalade |  |  |  |  |
| Mouvement | Natation |  |  |  |  |
| Mouvement | Survie |  |  |  |  |
| Mouvement | Discrétion |  |  |  |  |
| Combat | Lames |  |  |  |  |
| Combat | Armes d’impact |  |  |  |  |
| Combat | Armes d’hast |  |  |  |  |
| Combat | Tir |  |  |  |  |
| Combat | Lancer |  |  |  |  |
| Combat | Corps à corps |  |  |  |  |
| Combat | Boucliers |  |  |  |  |
| Combat | Parade |  |  |  |  |
| Combat | Armures |  |  |  |  |
| Combat | Tactique |  |  |  |  |
| Savoir-faire | Artisanat |  |  |  |  |
| Savoir-faire | Forge |  |  |  |  |
| Savoir-faire | Mécanismes |  |  |  |  |
| Savoir-faire | Alchimie |  |  |  |  |
| Savoir-faire | Pièges |  |  |  |  |
| Savoir-faire | Médecine |  |  |  |  |
| Savoir-faire | Érudition |  |  |  |  |
| Savoir-faire | Investigation |  |  |  |  |
| Relations | Persuasion |  |  |  |  |
| Relations | Intimidation |  |  |  |  |
| Relations | Tromperie |  |  |  |  |
| Relations | Empathie |  |  |  |  |
| Relations | Commandement |  |  |  |  |
| Relations | Négoce |  |  |  |  |
| Observation | Vigilance |  |  |  |  |
| Observation | Pistage |  |  |  |  |
| Finesse | Crochetage |  |  |  |  |
| Finesse | Escamotage |  |  |  |  |
| Relations | Dressage |  |  |  |  |
| Magie | Élémentalisme |  |  |  |  |
| Magie | Force |  |  |  |  |
| Magie | Altération |  |  |  |  |
| Magie | Illusion |  |  |  |  |
| Magie | Esprit |  |  |  |  |
| Magie | Vitalité |  |  |  |  |
| Magie | Invocation |  |  |  |  |

### Talents

| Talent | ID | Rang | Prérequis actifs | Doctrine active |
|---|---|---:|---|---|
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |

### Équipement et états

| Emplacement | Objet | Masse | Propriétés | Préparé |
|---|---|---:|---|---|
| Main principale |  |  |  |  |
| Main secondaire |  |  |  |  |
| Jeu d’armes 2 |  |  |  |  |
| Armure — tête |  |  |  |  |
| Armure — torse |  |  |  |  |
| Armure — bras |  |  |  |  |
| Armure — jambes |  |  |  |  |
| Ceinture 1–4 |  |  |  |  |
| Outils 1–2 |  |  |  |  |

Noter séparément les blessures par zone, le saignement, les accumulations d’état, la toxicité, la charge et les formules magiques connues.

## Journal d’apprentissage

Le journal explique la progression ; il ne demande pas au joueur de comptabiliser chaque animation. Le moteur devrait regrouper les événements et proposer un résumé à la fin d’une scène.

| Situation | Compétence | Difficulté | Nouveauté | Risque | Retour | Répétition | Traces |
|---|---|---:|---|---|---|---|---:|
|  |  |  |  |  |  |  |  |

Questions utiles :

- Qu’est-ce qui rendait cette action réellement difficile ?
- La méthode avait-elle déjà résolu le même obstacle dans cette situation ?
- Le personnage a-t-il reçu un retour compréhensible ?
- Une compétence de soutien a-t-elle vraiment changé l’issue ?
- Une épreuve de palier a-t-elle été qualifiée ?

## Référence d’action

```text
Test = 2d10 + ⌊Compétence ÷ 5⌋ + Attribut + Contexte + Aide
Marge = Test − Seuil
Seuils = 14 / 18 / 22 / 26 / 30 / 34
```

| Marge | Lecture |
|---:|---|
| `≤ −10` | Échec net |
| `−9..−1` | Échec constructif |
| `0..4` | Réussite fragile |
| `5..9` | Réussite propre |
| `≥ 10` | Réussite décisive |

Avantage : `3d10`, garder les deux meilleurs.  
Désavantage : `3d10`, garder les deux plus faibles.

## Référence de progression

```text
Traces du prochain point = 5 + ⌊S ÷ 5⌋ + ⌊S² ÷ 400⌋
Jalons pour le niveau N   = N² + 3N − 4
```

| Palier | Score | Acquis | Jalons | Preuve minimale |
|---|---:|---|---:|---:|
| Initié | 10 | automatique | 1 | — |
| Pratiquant | 25 | automatique | 2 | D18 |
| Confirmé | 45 | automatique | 3 | D22 |
| Expert | 65 | automatique | 4 | D26 |
| Maître | 85 | automatique | 5 | D30 |

## Référence des conséquences

| Élément | Valeur |
|---|---|
| Blessure mineure | Trauma 15 |
| Blessure grave | Trauma 25 |
| Blessure critique | Trauma 40 |
| Stabiliser | 3 secondes |
| Relever | 5 secondes |
| Repos court | 20 minutes |
| Repos long | 8 heures |
| Mise hors de combat | Vitalité 0 |

## Glossaire

- **Acquis.** Technique obtenue automatiquement à un palier de compétence.
- **Aide.** Contribution d’un assistant, bornée et exposée aux conséquences.
- **Attribut.** Capacité générale du personnage, de 1 à 10.
- **Avantage.** Trois d10 lancés, deux meilleurs conservés.
- **Chaîne causale.** Suite liée d’actions et de réactions issue d’une intention.
- **Compétence.** Savoir-faire appris, de 0 à 100, commun à tous les personnages.
- **Concentration.** Ressource employée pour le ralentissement tactique et certaines techniques.
- **Contexte.** Somme expliquée des circonstances favorables et défavorables.
- **Contre-jeu.** Manière perceptible d’éviter, interrompre ou retourner une capacité.
- **Désavantage.** Trois d10 lancés, deux plus faibles conservés.
- **Doctrine.** Préparation active parmi deux options d’un même groupe ; ce n’est pas une classe.
- **Endurance.** Ressource des efforts, attaques, défenses et déplacements intenses.
- **Équilibre.** Résistance aux chocs, poussées, ruptures de garde et renversements.
- **Épreuve de palier.** Situation minimale exigée pour franchir certains seuils de maîtrise.
- **Impact.** Énergie de jeu transmise par un contact avant protection et localisation.
- **Jalon.** Valeur unique gagnée lors du premier franchissement d’un palier.
- **Marge.** Différence entre un résultat et son seuil.
- **Module.** Monde jouable, contenu et profil de règles sans comportement moteur caché.
- **Niveau global.** Résumé du parcours calculé depuis les jalons ; il ne donne pas de classe.
- **Profil de règles.** Valeurs versionnées choisies par le module.
- **Qualité de contact.** Coefficient lisible provenant de l’angle, de la portée et de l’état de la cible.
- **Situation.** Unité causale utilisée notamment pour détecter les répétitions.
- **Talent.** Choix transversal acheté avec un point de talent.
- **Trace.** Unité entière d’apprentissage accordée après un usage significatif.
- **Trauma.** Conséquence d’un impact après qualité, protection et zone.
- **Vitalité.** Ressource de survie et de capacité à rester conscient.

## Dernière règle

Décrire d’abord l’intention, observer ensuite le monde, puis choisir la méthode. La feuille de personnage indique ce qui est maîtrisé ; elle ne remplace jamais la curiosité du joueur.
