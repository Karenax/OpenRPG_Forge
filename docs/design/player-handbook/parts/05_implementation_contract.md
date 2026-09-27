<!-- handbook-part: implementation-contract -->

# Annexe — Contrat d’implémentation et scénarios de référence

## Objet de l’annexe

Cette annexe transforme les règles du joueur en comportements vérifiables. Elle ne constitue pas encore une architecture Rust, un schéma JSON publié ou une autorisation à commencer la Gate 3. Elle fixe les invariants que les futurs contrats de domaine, profils de règles et tests devront préserver.

Une implémentation est conforme si :

- elle produit les résultats observables décrits ici ;
- ses données utilisent des identifiants versionnés et indépendants du moteur ;
- elle ne dépend ni d’une entité Bevy persistée, ni d’un slug de module ;
- elle peut expliquer les modificateurs et la chaîne causale au joueur ;
- elle passe les scénarios de référence avant d’être annoncée comme disponible.

## Vocabulaire normatif

| Terme | Définition d’implémentation |
|---|---|
| **acteur** | Entité de domaine capable de déclarer ou d’exécuter une action |
| **intention** | But explicite soumis à validation avant l’exécution |
| **action** | Tentative bornée possédant un initiateur, une durée, des coûts et des conséquences |
| **situation** | Fenêtre causale dans laquelle la répétition est comparée |
| **chaîne causale** | Graphe orienté reliant intention, actions, contacts, réactions et conséquences |
| **contact** | Intersection confirmée entre un volume actif et un récepteur |
| **test** | Résolution d’une incertitude qui ne peut pas être décidée par la simulation seule |
| **trace** | Entier positif crédité à une compétence après qualification de l’apprentissage |
| **jalon** | Récompense unique liée au premier franchissement d’un palier de compétence |
| **profil** | Ensemble versionné de valeurs de règles sélectionné par un module |
| **preuve** | Événement, résultat de test, capture ou métrique qui confirme un comportement |

## Identifiants stables

Les identifiants suivants sont proposés comme contrat initial. Les libellés traduits peuvent changer ; les identifiants ne changent pas dans une version compatible.

### Attributs

```text
strength
dexterity
constitution
intelligence
willpower
perception
presence
```

### Ressources

```text
vitality
stamina
mana
balance
focus
safe_carry_kg
```

### Compétences

Les quarante-deux identifiants figurent dans le catalogue des compétences. Ils sont uniques, en minuscules ASCII et en `snake_case`.

### Acquis et talents

Un acquis automatique suit le format :

```text
mastery.<skill_id>.<threshold>.<slug>
```

Exemple : `mastery.marksmanship.45.interaction_shot`.

Un talent suit le format :

```text
talent.<domain>.<slug>
```

Exemple : `talent.environment.causal_chain`.

Les rangs ne font pas partie de l’identifiant ; ils sont une valeur `1..3` associée au talent.

## Unités, bornes et arrondis

### Valeurs persistées

- attribut : entier `1..10` ; création `3..8` ;
- compétence : entier `0..100` ;
- ressource courante : entier `0..maximum` ;
- traces : entier non négatif ;
- jalons : entier non négatif ;
- niveau : entier `1..20` ;
- temps de règle : millisecondes entières ;
- angles affichés : degrés ;
- distances de conception : mètres ;
- masses : kilogrammes ;
- pourcentages : points de base ou rationnels dans les données, jamais texte libre.

Le moteur physique peut employer ses types numériques internes. Toute valeur persistée ou comparée par un test de domaine doit être quantifiée selon le contrat du profil.

### Règle d’arrondi

1. Les divisions de compétence emploient le plancher : `⌊S ÷ 5⌋`.
2. Les coûts et conséquences positives calculés sont arrondis à l’entier le plus proche, les demis vers le haut.
3. Les modificateurs sont appliqués avant l’arrondi final, sauf mention contraire.
4. Une formule n’est jamais arrondie à chaque terme intermédiaire si le texte ne le demande pas.
5. Une ressource ne descend pas sous zéro et ne dépasse pas son maximum.

Pour une valeur positive, l’arrondi de référence peut être défini sans flottant :

```text
round_half_up(numerateur / denominateur)
= (numerateur + denominateur / 2) / denominateur
```

avec division entière lorsque le dénominateur est pair et que les données ont été mises à l’échelle.

## Profil de règles proposé

Le futur format canonique devra être validé par un schéma et une ADR si sa structure devient publique. La forme suivante illustre les responsabilités attendues :

```json
{
  "schema_version": 1,
  "ruleset_id": "openrpg-forge.systemic-rpg.v1",
  "profile_id": "forge.reference.v1",
  "resolution": {
    "dice": { "count": 2, "sides": 10 },
    "skill_divisor": 5,
    "context_min": -6,
    "context_max": 6,
    "difficulty": [14, 18, 22, 26, 30, 34]
  },
  "progression": {
    "skill_min": 0,
    "skill_max": 100,
    "mastery_thresholds": [10, 25, 45, 65, 85],
    "learning_trace_cap_per_situation": 5,
    "global_level_max": 20
  },
  "resources": {
    "vitality": "60 + 6 * constitution + 2 * strength",
    "stamina": "50 + 5 * constitution + 3 * willpower",
    "mana": "20 + 6 * willpower + 4 * intelligence",
    "balance": "40 + 4 * strength + 4 * constitution",
    "focus": "40 + 4 * perception + 4 * willpower"
  }
}
```

Le format réel ne devrait pas exécuter directement des chaînes arbitraires. Les formules connues devront être représentées par des champs typés, une expression bornée ou des fonctions de domaine explicitement autorisées.

## Résolution déterministe d’un test

### Entrées requises

- acteur ;
- intention ;
- compétence ;
- attribut choisi et justification si secondaire ;
- seuil ou adversaire ;
- modificateurs de contexte nommés ;
- aides qualifiées ;
- état de l’avantage ;
- source de hasard injectée ;
- révision du profil de règles.

### Ordre

1. **Valider l’intention.** Refuser une cible absente, une capacité inconnue ou une impossibilité physique explicite.
2. **Décider si un test est nécessaire.** Une action évidente sans conséquence est résolue sans jet.
3. **Capturer les entrées.** Les modificateurs applicables sont figés au moment défini par l’action.
4. **Lancer les dés.** Conserver les dés selon avantage ou désavantage.
5. **Calculer le total.** Appliquer compétence, attribut, contexte et aide.
6. **Calculer la marge.** Comparer au seuil ou au résultat opposé.
7. **Classifier le résultat.** Utiliser les cinq bandes de marge du manuel.
8. **Produire les conséquences.** Les coûts et événements sont explicites.
9. **Émettre l’explication.** Chaque terme du calcul est disponible pour le journal ou les diagnostics.
10. **Qualifier l’apprentissage.** Après la conséquence, évaluer les traces sans modifier rétroactivement le test.

### Événement explicatif minimal

```json
{
  "event": "action_check_resolved",
  "rules_revision": "forge.reference.v1",
  "actor_id": "actor.sava",
  "action_id": "action.lockpick",
  "skill_id": "lockpicking",
  "attribute_id": "dexterity",
  "dice": [6, 5],
  "kept": [6, 5],
  "skill_bonus": 8,
  "attribute_bonus": 6,
  "context_terms": [
    { "id": "tool.fine_picks", "value": 2 },
    { "id": "pressure.patrol", "value": -1 }
  ],
  "aid": 0,
  "total": 24,
  "difficulty": 22,
  "margin": 2,
  "outcome": "fragile_success"
}
```

## Contact physique et test abstrait

Le moteur doit séparer deux questions :

1. **le contact a-t-il eu lieu ?** — décidé par les volumes, trajectoires et temps actifs ;
2. **quelles sont ses conséquences ?** — décidé par vitesse, angle, matériau, protection, compétence, ressources et états.

Lorsqu’un contact est confirmé :

- aucun test ultérieur ne peut le transformer en absence de contact ;
- une protection peut réduire le Trauma à zéro ;
- l’événement peut encore produire son, recul, perte d’Équilibre, usure ou réaction ;
- la compétence peut modifier qualité, précision de zone, récupération et coût ;
- l’explication doit citer la raison d’un Trauma faible ou nul.

Un test de précision reste valide pour déterminer si le joueur a effectivement atteint une petite corde, une articulation, une ouverture temporelle ou un point non piloté directement par une collision fiable.

## Ordre des conséquences d’une action

À horodatage de simulation égal, l’ordre de domaine proposé est :

1. validation et réservation des ressources ;
2. déclenchement de l’action et début de préparation ;
3. contacts physiques dans l’ordre temporel réel ;
4. protection, Trauma et perte d’Équilibre ;
5. interruption, garde brisée et mouvement imposé ;
6. états et réactions de surface ;
7. rupture de support et propagation environnementale ;
8. perception, témoins, crimes et réputation ;
9. objectifs, quêtes et relations ;
10. candidats d’apprentissage et consolidation ;
11. journal explicatif et sauvegarde d’état.

Une réaction engendrée pendant cette séquence crée un nouvel événement relié par `caused_by`; elle ne réécrit pas l’événement parent.

## Chaîne causale

Chaque événement significatif devrait contenir :

```text
event_id
chain_id
caused_by_event_id?
initiator_actor_id
executor_actor_id?
contributors[]
intent_id
world_revision
simulation_time_ms
tags[]
```

Exemple : une archère coupe une corde, le brasero tombe, l’huile brûle et un témoin est blessé.

- l’archère est initiatrice de la chaîne ;
- le projectile exécute le contact ;
- la corde rompue cause la chute ;
- la chute cause la rupture du récipient ;
- l’huile et la flamme causent l’incendie ;
- les conséquences de quête, de crime et d’apprentissage interrogent la chaîne complète.

Le système ne crée pas une récompense d’apprentissage par objet brûlé. Il regroupe les conséquences partageant la même intention et la même chaîne.

## Algorithme de progression par usage

### Résultat moyen

```text
resultat_moyen = 11
                + ⌊competence ÷ 5⌋
                + attribut
                + contexte
                + aide

delta_defi = difficulte − resultat_moyen
```

### Traces de base

| `delta_defi` | Traces de base |
|---:|---:|
| `≤ −8` | 0 |
| `−7..−1` | 1 |
| `0..4` | 2 |
| `5..8` | 3 |
| `≥ 9` | 0 ; 1 seulement si l’essai était plausible et a produit un retour exploitable |

Ajouter ensuite :

- `+1` pour une situation ou méthode nouvelle ;
- `+1` pour un enjeu ou un risque réel ;
- `+1` pour un retour précis, une analyse ou un mentor ;
- borner le total à `0..5` par compétence et situation.

### Empreinte de répétition

Une tentative qualifiée produit une empreinte composée au minimum de :

```text
situation_id
actor_id
skill_id
intent_category
obstacle_signature
method_signature
```

- première occurrence : traces complètes ;
- deuxième occurrence identique : `⌊traces ÷ 2⌋` ;
- troisième occurrence et suivantes : zéro ;
- une contrainte, un adversaire, un matériau ou une méthode réellement différente produit une autre empreinte ;
- changer volontairement d’outil pour se handicaper ne suffit pas à créer une nouveauté.

### Coût du point suivant

```text
traces_requises(S) = 5 + ⌊S ÷ 5⌋ + ⌊S² ÷ 400⌋
```

À la consolidation :

1. ajouter les traces au solde ;
2. tant que le solde suffit, calculer le coût du score courant ;
3. vérifier l’épreuve de palier si le prochain point franchit 25, 45, 65 ou 85 ;
4. si l’épreuve manque, conserver le solde et arrêter ;
5. sinon soustraire le coût, augmenter le score de 1 et attribuer l’acquis ou le jalon éventuel ;
6. arrêter à 100 en conservant zéro trace excédentaire au-delà du maximum.

Une compétence de soutien reçoit au maximum `⌊traces_principales ÷ 2⌋`, uniquement si sa contribution a changé l’issue.

## Niveau global et récompenses

Les jalons de palier sont attribués une seule fois par compétence :

```text
10 → 1 jalon
25 → 2 jalons
45 → 3 jalons
65 → 4 jalons
85 → 5 jalons
```

Le niveau courant est le plus grand entier `N` compris entre 1 et 20 tel que :

```text
N² + 3N − 4 ≤ jalons_totaux
```

Lorsqu’un acteur franchit plusieurs niveaux pendant une consolidation, chaque récompense intermédiaire est accordée une seule fois.

- niveau pair nouvellement atteint : `+1` point de talent ;
- niveaux 4, 8, 12, 16 et 20 : `+1` point d’attribut disponible ;
- un attribut ne dépasse pas 10 ;
- aucun niveau n’accorde de rang de compétence ;
- le monde ne modifie pas ses habitants à partir de ce niveau.

## Achat d’un talent

L’achat doit vérifier atomiquement :

1. repos long ou autre fenêtre explicitement autorisée ;
2. point de talent disponible ;
3. rang demandé égal au rang courant plus un ;
4. attributs et compétences suffisants pour ce rang ;
5. connaissance concrète requise, le cas échéant ;
6. cohérence des doctrines actives ;
7. consommation du point et ajout du rang dans une seule mutation.

Perdre temporairement un bonus d’équipement n’efface pas un talent acquis. Si un prérequis permanent devient invalide à cause d’un effet exceptionnel, le talent reste connu mais ne peut être activé jusqu’au rétablissement.

## Hasard, sauvegarde et relecture

- La source de hasard est injectée dans le domaine et remplaçable par une séquence déterministe en test.
- Un événement de test conserve les dés bruts et les dés retenus.
- Recharger une sauvegarde restaure l’état précédent ; cela ne doit pas relancer silencieusement un événement déjà persisté.
- En réseau futur, l’autorité choisie produit ou valide le tirage ; le client ne décide pas seul d’un résultat persistant.
- Les tests unitaires emploient des dés fixés ; les tests statistiques vérifient séparément la distribution.

## Invariants impératifs

1. Tous les personnages utilisent le même catalogue de compétences.
2. Aucun contenu n’est verrouillé par une classe.
3. Une compétence reste dans `0..100` et un attribut dans `1..10`.
4. Un contact confirmé reste un contact.
5. Une protection n’est appliquée qu’aux zones et types qu’elle couvre réellement.
6. Une réduction de coût ne ramène jamais une action sous 50 % de son coût de base.
7. Les avantages ne se cumulent pas en dés supplémentaires.
8. Une même chaîne ne produit pas une récompense par conséquence secondaire.
9. Une répétition identique cesse de produire des traces.
10. Le coup final n’accorde aucun privilège d’apprentissage.
11. Une compétence secondaire ne reçoit pas plus de la moitié des traces principales.
12. Un palier de 25, 45, 65 ou 85 exige sa preuve minimale.
13. Un jalon de palier n’est accordé qu’une fois.
14. Un talent ne donne aucun rang de compétence.
15. Les adversaires et compagnons peuvent employer les mêmes règles.
16. Une action impossible n’est pas rendue possible par un résultat élevé.
17. Une réaction environnementale conserve son initiateur causal.
18. Un crime ne devient connu d’une faction qu’après transmission ou preuve accessible.
19. Le repos avance le temps du monde.
20. Le profil de règles ne contient aucun comportement spécial lié à un slug de module.

## Scénarios de référence

Les scénarios suivants sont destinés à devenir des tests de domaine. Les dés sont injectés.

### R01 — Total simple

**Étant donné** Compétence 44, Attribut 6, Contexte `+1`, Aide 0 et dés `[7, 4]`.  
**Quand** le test est résolu contre 22.  
**Alors** le bonus de compétence vaut 8, le total vaut 26, la marge vaut 4 et le résultat est `fragile_success`.

### R02 — Avantage

**Étant donné** les dés `[3, 7, 9]` avec avantage.  
**Quand** les dés sont sélectionnés.  
**Alors** `[9, 7]` sont conservés et leur somme vaut 16.

### R03 — Désavantage

**Étant donné** les dés `[3, 7, 9]` avec désavantage.  
**Quand** les dés sont sélectionnés.  
**Alors** `[3, 7]` sont conservés et leur somme vaut 10.

### R04 — Annulation

**Étant donné** au moins une source d’avantage et une source de désavantage.  
**Quand** le test commence.  
**Alors** deux dés seulement sont lancés et les autres sources deviennent au besoin des termes de contexte nommés.

### R05 — Bornes de marge

**Étant donné** les marges `−10, −9, −1, 0, 4, 5, 9, 10`.  
**Alors** les classes sont respectivement : net, constructif, constructif, fragile, fragile, propre, propre, décisif.

### R06 — Pas de test trivial

**Étant donné** une porte non verrouillée, connue et sans urgence.  
**Quand** le joueur l’ouvre.  
**Alors** aucun tirage n’est demandé et aucun candidat d’apprentissage n’est créé.

### R07 — Contact non annulé

**Étant donné** qu’une lame active intersecte le volume du torse.  
**Quand** le contact est confirmé puis que la protection absorbe tout le Trauma.  
**Alors** un événement de contact et ses réactions sont conservés, avec Trauma 0 ; aucun événement `miss` n’est émis.

### R08 — Protection en plaques

**Étant donné** Impact 24, qualité 1, protection tranchante 33 et coefficient de zone 1.  
**Quand** le Trauma est calculé.  
**Alors** `round(24 − 0,65 × 33) = 3`.

### R09 — Arme d’impact contre plaques

**Étant donné** Impact 25, qualité 1, protection contondante 14 et coefficient de zone 1.  
**Alors** le Trauma vaut `round(25 − 0,65 × 14) = 16`.

### R10 — Chute

**Étant donné** une chute de 7 m sur pierre.  
**Alors** l’Impact de chute vaut `(7 − 2) × 6 = 30` avant réduction par une réception admissible.

### R11 — Ressources moyennes

**Étant donné** les sept attributs à 5.  
**Alors** Vitalité 100, Endurance 90, Mana 70, Équilibre 80 et Concentration 80.

### R12 — Intensité magique sûre

**Étant donné** compétence magique 55, Intelligence 7 et Volonté 6.  
**Alors** l’intensité sûre vaut `1 + 2 + 0 = 3`.

### R13 — Dépassement magique

**Étant donné** une intensité sûre 3 et une formule lancée à l’intensité 5.  
**Alors** le test de contrôle a pour seuil `22 + 3 × 2 = 28`.

### R14 — Demande sociale impossible

**Étant donné** une demande sans intérêt, preuve, dette, peur, autorité, compassion, compromis ni menace réalisable.  
**Quand** le joueur tente de persuader une personne autonome d’un acte contraire à ses intérêts fondamentaux.  
**Alors** le jeu refuse le test et explique qu’un levier crédible manque.

### R15 — Traces ordinaires

**Étant donné** `delta_defi = 2`, une situation nouvelle et un risque réel, sans mentor.  
**Alors** les traces valent `2 + 1 + 1 = 4`.

### R16 — Défi absurde

**Étant donné** `delta_defi = 12` et un essai physiquement impossible.  
**Alors** zéro trace est accordée, même en cas de commande répétée.

### R17 — Répétition

**Étant donné** une empreinte accordant 5 traces à sa première occurrence.  
**Alors** la deuxième occurrence identique accorde 2 traces et la troisième zéro.

### R18 — Coût d’apprentissage

**Étant donné** les scores `0, 20, 40, 60, 80, 99`.  
**Alors** les coûts du prochain point sont `5, 10, 17, 26, 37, 48`.

### R19 — Blocage de palier

**Étant donné** une compétence 24 avec assez de traces pour atteindre 25, mais aucune situation de difficulté 18 qualifiée.  
**Quand** la progression est consolidée.  
**Alors** le score reste 24 et les traces sont conservées.

### R20 — Franchissement de palier

**Étant donné** le même état après qualification d’une situation D18.  
**Alors** le point 25 est acheté, l’acquis de seuil 25 est attribué et deux jalons sont accordés une seule fois.

### R21 — Niveau global

**Étant donné** 36 jalons.  
**Alors** le niveau vaut 5 car `5² + 15 − 4 = 36`, tandis que le niveau 6 exige 50.

### R22 — Récompenses traversées

**Étant donné** un passage du niveau 3 au niveau 5.  
**Alors** le niveau 4 accorde un point de talent et un point d’attribut ; aucune autre récompense n’est oubliée ou doublée.

### R23 — Attribut plafonné

**Étant donné** Force 10 et un point d’attribut disponible.  
**Quand** le joueur tente de l’affecter à Force.  
**Alors** la mutation est refusée sans consommer le point.

### R24 — Achat atomique

**Étant donné** un point de talent, un repos long et des prérequis insuffisants.  
**Quand** l’achat est tenté.  
**Alors** le rang n’est pas ajouté et le point n’est pas consommé.

### R25 — Compétence de soutien

**Étant donné** 5 traces principales et une contribution secondaire déterminante.  
**Alors** la compétence secondaire reçoit au maximum 2 traces.

### R26 — Chaîne environnementale

**Étant donné** un projectile qui coupe une corde et fait tomber un brasero dans l’huile.  
**Alors** rupture, chute et incendie partagent le même `chain_id`, conservent l’initiateur et produisent au plus un candidat d’apprentissage principal pour cette intention.

### R27 — Témoin sans transmission

**Étant donné** un témoin isolé qui identifie un vol mais ne communique avec personne.  
**Alors** sa connaissance locale est mise à jour, mais la réputation de faction ne change pas encore.

### R28 — Information locale du groupe

**Étant donné** deux compagnons séparés sans signal ni liaison.  
**Quand** l’un découvre un piège.  
**Alors** l’autre ne reçoit pas automatiquement l’identification.

### R29 — Repos et blessure grave

**Étant donné** une blessure grave non traitée et un repos long valide.  
**Alors** les ressources récupérables sont restaurées, le temps avance de huit heures et la Vitalité ne dépasse pas 70 % du maximum.

### R30 — Réduction de coût bornée

**Étant donné** plusieurs effets qui réduiraient une action de 65 %.  
**Alors** seule la meilleure réduction compatible s’applique et le coût final ne descend jamais sous 50 % du coût de base.

### R31 — Doctrine exclusive

**Étant donné** une doctrine active dans un groupe.  
**Quand** l’autre doctrine du même groupe est activée après la préparation requise.  
**Alors** la première est désactivée dans la même mutation ; les deux ne sont jamais actives simultanément.

### R32 — Monde non nivelé

**Étant donné** le passage du héros du niveau 5 au niveau 6.  
**Alors** aucun attribut, équipement ou compétence d’un habitant sans lien causal n’est modifié.

## Tests statistiques complémentaires

Les dés fixés vérifient la logique ; ils ne vérifient pas les probabilités. Une suite séparée devrait énumérer les 100 résultats possibles de `2d10` et les 1 000 résultats de `3d10` afin de confirmer exactement :

- la distribution triangulaire de `2d10` ;
- la sélection des deux meilleurs ou des deux plus faibles ;
- la probabilité de chaque classe de marge ;
- l’absence de biais introduit par la source de hasard ;
- la stabilité des résultats entre natif et WASM pour une séquence injectée.

Une simulation d’équilibrage ne remplace pas les tests exacts. Elle complète l’analyse pour estimer durée de progression, fréquence des blessures et économie des ressources.

## Découpage conseillé pour le futur code

Lorsque la gate active autorisera l’implémentation gameplay, l’ordre suivant limitera les dépendances :

1. types forts, profil versionné, identifiants et validation ;
2. résolution `2d10`, explications et source de hasard testable ;
3. compétences, traces, paliers, jalons et talents ;
4. ressources, coûts, états et repos ;
5. contacts, protection, Trauma et Équilibre ;
6. chaînes causales et réactions environnementales ;
7. perception, témoins, social et factions ;
8. magie composée et contre-jeu ;
9. compagnons et ordres ;
10. persistance, replay et profils de module.

Chaque tranche devrait suivre : test rouge, implémentation minimale verte, refactorisation, test d’intégration, preuve runtime, documentation du statut. Aucun chapitre entier ne doit être déclaré « implémenté » parce qu’une formule isolée compile.

## Critères de sortie d’une tranche

- contrat ou schéma versionné ;
- cas valides et invalides ;
- tests unitaires déterministes ;
- test de sérialisation aller-retour si données persistées ;
- erreur typée et explicable ;
- diagnostic des modificateurs ;
- intégration sans dépendance de domaine vers Bevy ou Tauri ;
- preuve runtime lorsque le comportement est visible ;
- mesure de performance lorsque la boucle est fréquente ;
- statut documentaire distinguant conçu, implémenté, testé et promu.

## Questions d’équilibrage à valider par prototype

Les formules sont assez précises pour commencer des tests, mais les points suivants devront être mesurés avec un prototype fonctionnel :

- durée réelle pour passer 0→25, 25→45, 45→65, 65→85 et 85→100 ;
- fréquence à laquelle un joueur naturel rencontre une situation qualifiante ;
- variété effective des compétences progressées par une partie ;
- risque que les compétences sociales ou d’artisanat progressent moins souvent que le combat ;
- lisibilité des contacts, protections et critiques déterministes ;
- temps moyen d’un combat et part consacrée à l’attente d’Endurance ;
- survie moyenne face aux blessures 15/25/40 ;
- fréquence d’usage de la concentration tactique ;
- coût réel des effets magiques composites ;
- utilité de chaque talent face à son contre-jeu ;
- nombre de choix utiles par niveau global ;
- charge cognitive de cent talents et qualité des filtres nécessaires.

Tout ajustement issu de ces mesures devra modifier un profil versionné et ses scénarios attendus, jamais un nombre caché dans un système de présentation.

