# Gate 5 — protocole des parcours graphiques

**À exécuter. Ce protocole ne constitue pas une preuve de réussite.**
L'état acquis est dans la [matrice de qualification](../evidence/gate5/GATE5_QUALIFICATION_STATUS_20261020.md).
Les contrats et oracles suivent le [plan Gate 5](GATE5_IMPLEMENTATION_PLAN.md)
et la [préparation P07/P08](preparation-parallele-20260926/GATE5_REGLES_ET_CREATOR.md).

## Candidat et données

La consigne propriétaire du 21 octobre 2026 porte sur les fonctionnalités de
base du moteur et du Creator permettant de construire des quêtes ultérieurement.
Utiliser des jeux de test isolés et minimaux pour exercer ces fonctionnalités ;
ne pas enrichir les quêtes ni produire une aventure pour cette qualification.
Les parcours narratifs ci-dessous démontrent des contrats génériques et leur
persistance, sans constituer une livraison de contenu jouable.

Utiliser un frontend nouvellement compilé du checkout commun, le Studio et le
runtime construits depuis le même inventaire de sources. Épingler leurs empreintes
et celles du worker de navigation. Selon la consigne du 20 octobre, les builds,
temporaires, profils WebView2, workspace de qualification et parties de travail
résident sous `E:/OpenRPG_Forge/.build/`, avec un cache Cargo partagé. Les captures
et rapports retenus vont dans la campagne centrale
`E:/OpenRPG_Forge/Preuves-et-analyses/actives/gate5/20261019/`, physiquement sur F:.
Les installations et données utilisateur existantes sont préservées. Ne pas
produire de copie de code propre à la qualification.

Le jeu séparé attendu par le lanceur du Studio est la cible native
`forge_tidewater --example tidewater_plant_probe`, dont l'entrée `--module`
est commune aux modules. Le nom historique de la cible
ne constitue pas une sélection de module. `forge_runtime` conserve son rôle de
validation des scènes ; son exécutable ne remplace pas cette entrée du jeu.
Le cache `--cache`, la racine `--save-root`, le répertoire courant du processus
et `FORGE_GAME_REPORT_PATH` utilisent les données de travail sur E:. Retenir les
rapports utiles dans la campagne centrale F: après observation. Le lancement
normal du Studio construit ces chemins à partir de la configuration locale et
du reçu d'export épinglé.

Les deux entrées préparées sont Cairns 1.0.1 et Relais 1.0.0, épinglées dans
`Preuves-et-analyses/actives/gate5/20261019/module-set-request-e-001.json`,
avec destination isolée sous `E:/OpenRPG_Forge/.build/qualification-data/gate5/`.
La requête F: précédente reste historique et ne doit pas être rejouée telle quelle.
Le producteur doit admettre les archives exactes dans une destination fraîche,
conserver leurs origines et droits `private_local_only`, puis rouvrir le workspace.
Cette préparation native n'est pas une preuve d'import de package par l'UI.

Les deux packages d'origine ont chacun une seule zone. Pour UI01, une seconde
zone vide peut être préparée dans A par l'outil de données de qualification
`.build/tools/gate5-zone-fixture` sur E:, qui dépend des crates du checkout
commun. Il utilise `prepare_module_terrain`, la revue de création puis
`create_new_zone_with_audit` avec un audit IA explicite et Save. L'exécution
exige le SHA-256 exact du workspace enregistré et aucun Studio ouvert ; la
revue doit préserver tous les anciens objets, profils, origines et l'autre module.
La clé est `g5-shared-rules`, le nom « G5 — Seconde zone de règles ».
Cette préparation ne constitue pas une preuve de création de zone par l'UI.
L'outil compile le 21 octobre à 07:51:36 UTC, puis prépare la zone
`d05bc390-2ed3-526d-bc70-cc83d37c6101` ; ses sources, verrou, avant du workspace
et reçu sont dans `empty-zone-setup-e-001*` dans la campagne F:.
L'ancien contenu et les profils sont préservés. La reprise graphique active
le gameplay, sauvegarde et observe le profil hérité à 4 m. La retouche à 5 m
annonce les deux cartes ; l'annulation restaure 4 m mais la projection refuse
la révision historique. **Le parcours reste incomplet**, avec preuve du refus
dans `gate5-ui01-projection-red-e-001.png/.txt` sur F:.

Ouvrir ensuite cette zone dans le Studio et y activer les règles par le
formulaire normal. Vérifier l'héritage du profil commun de A avant retouche.
Revenir à sa première zone, modifier une règle et visiter la seconde pour
observer la propagation, puis exercer l'historique du module et Save/Open.
Le cas ne passe pas si seule la première zone a été exercée.

Opérer ensuite les retouches par les formulaires et commandes normales du Studio.
Les archives ou JSON de test peuvent servir d'oracles techniques ; leur édition
externe ne remplace pas une manipulation d'auteur demandée par ce protocole.
Le jeu séparé et le même Studio sont utilisés pour les deux modules.

Pour chaque cas, relever : module/zone, hash source avant et après, version du
profil commun, action effectuée, résultat observé et chemins des captures.
Une source reste une source ; l'état d'une partie et son hash sont relevés
séparément. Un message de succès seul ne prouve ni rendu, ni persistance.

## Cycle commun de retouche

Complément de lisibilité demandé le 21 octobre : employer les assets privés
existants de la bibliothèque lorsque leur géométrie et leurs droits conviennent
au test. Leur import et aperçu isolé ne qualifient pas leur présentation dans
le jeu. Pour un modèle lié à un acteur ou objet, contrôler les réglages de
décalage, échelle XYZ et rotation relative via Examiner/Appliquer/Sauvegarder ;
vérifier le refus d'une échelle nulle, l'absence de cumul lors de deux aperçus,
puis la conservation après réouverture. Rapprocher la source avant/après pour
prouver que le corps physique et le gameplay n'ont pas été modifiés par ce
placement visuel. Vérifier ensuite le modèle réellement chargé dans le jeu.
Les données narratives existantes restent des fixtures minimales : ce complément
ne produit ni quête ni aventure et n'autorise aucune redistribution d'asset.

1. Ouvrir le module et sa zone dans le Studio ; relever le head courant.
2. Préparer une retouche dans les champs, puis examiner son aperçu.
3. Vérifier que l'aperçu n'a pas écrit la source. Appliquer la proposition retenue.
4. Exercer Undo/Redo lorsque demandé ; vérifier la valeur et l'identité restaurées.
5. Sauvegarder les sources, fermer puis rouvrir ; relever les mêmes données.
6. Lancer le jeu depuis le Studio, exercer le comportement, puis revenir au même
   éditeur. Vérifier les rapports et l'absence de modification des sources par le jeu.

Dans une carte gérée, Annuler/Rétablir examinent l'historique du module : vérifier
la portée de l'aperçu, puis confirmer avec Appliquer. Ctrl+Z/Ctrl+Y suivent la
même autorité ; Ctrl+Entrée confirme. Les commandes du panneau « Surfaces et
capacités du module » emploient le même préparateur. Une opération concernant
un autre module reste refusée ; ne pas remplacer ce refus par une annulation
locale qui rendrait la zone incohérente avec son profil commun.

Les suppressions effectives ne sont pas nécessaires aux cas ci-dessous. Les
refus de suppression utilisés par les contrats restent couverts par leurs tests.
Un éventuel aperçu de suppression doit rester sans application.

## Parcours des treize domaines

| ID | Manipulation et observation | Oracle |
| --- | --- | --- |
| UI01 — règles | Modifier une des sept règles dans le profil commun du module. Examiner l'unité et l'aide, appliquer, visiter une seconde zone, puis Undo/Redo et Save/Open. | Même identité, version, valeur et unité ; retouche commune aux zones concernées. Le calcul en jeu suit la règle choisie. |
| UI02 — blueprints | Dans « Blueprints de personnages », créer une définition depuis un acteur, lier une seconde instance, retoucher une vitalité locale, puis réviser « Vitalité héritée ». | Valeur héritée mise à jour ; override local, nom, pose et dialogue conservés. Réouverture identique. |
| UI03 — créatures | Configurer deux acteurs distincts, avec profils/rôles et comportements choisis ; garder leurs références visuelles gouvernées ou expliciter leur représentation générique. | Deux acteurs identifiables et jouables dans la même scène, avec données retouchables et comportements observés, sans code par module. |
| UI04 — objets/conteneurs | Créer un item et un conteneur via « Conteneurs et contenu initial ». Configurer pile, quantité, capacité et « Filtre des dépôts ». En jeu, accepter un transfert puis refuser un dépôt filtré ou au-delà de la capacité. | Quantité conservée dans les deux inventaires ; refus expliqué sans perte ni modification partielle ; reprise identique. |
| UI05 — dialogues | Créer une variable typée et une branche conditionnelle ; retoucher une réplique d'un graphe utilisé. Jouer les branches avant/après changement de variable. | Choix et effets conformes à la condition ; liens stables après retouche et réouverture. |
| UI06 — quêtes/journal | Préparer deux solutions distinctes d'une quête et les textes de journal. Exercer chaque solution dans une partie fraîche distincte ; sauvegarder/reprendre et retenter une récompense consommée. | Journal et transitions corrects ; une seule récompense par partie ; solutions et historique préservés dans les sources. |
| UI07 — factions | Dans « Factions et conséquences locales », configurer groupes, appartenances et relation orientée. Jouer une conséquence avec un témoin visible et un témoin réellement masqué. | Conséquences personnelles limitées aux faits perçus, causes distinctes, reprise durable ; absence de réaction omnisciente du témoin masqué. |
| UI08 — capacités/effets | Créer une protection temporaire dans « Effets temporaires », définir cible/coût dans « Capacités, cibles et coûts », puis l'acquérir par personnage. Jouer un coût accepté, un refus et une expiration/annulation. | Coût unique et atomique, protection observée dans le calcul, durée/stacking respectés ; aucune nouvelle consommation après reprise. |
| UI09 — calendrier | Dans « Horloge et calendrier », choisir jour/minute de départ, durée d'une minute et échéance. Sauvegarder une partie avant une borne, reprendre et la franchir. | Temps de simulation cohérent, borne consommée une seule fois ; affichage et définition concordants. |
| UI10 — rencontres | Lier une rencontre à une échéance, choisir ses participants et condition ; exercer l'apparition puis « Réarmer après la défaite de tous les participants » avec délai et événement récurrent. | Absence avant condition/échéance, présence physique après ; réarmement conforme après défaite/délai, identités conservées et reprise sans doublon. |
| UI11 — cinématiques | Composer plusieurs plans/répliques, retoucher durée/caméra/locuteur et ordre. Jouer fin naturelle, saut, interruption et reprise au milieu d'un plan. | Caméra et commandes récupérées ; progression reprise ; quête et récompenses inchangées par la présentation. |
| UI12 — scripts | Composer un programme typé avec variable, porte/message, branche et répétition. Exercer permissions refusées et budget dépassé, corriger puis rejouer. | Cause/instruction/remède visibles ; aucune mutation partielle ; programme corrigé exécuté une fois et sauvegarde durable. |
| UI13 — diagnostics | Sur le script refusé, utiliser « Retoucher l’instruction signalée », vérifier la sélection du nœud, modifier ses données ou permissions et relancer la vérification. | Le diagnostic mène à la donnée exacte ; la correction retire l'erreur et le replay confirme le résultat. Un succès sur partie temporaire ne vaut pas preuve physique. |

## Données de calibration pour les cas temporels

Employer des noms préfixés `G5` dans le workspace de qualification pour retrouver
les définitions créées. Conserver les IDs produits par le Studio ; ne pas les
remplacer entre retouche et reprise.

Pour l'effet, une protection de 10 points pendant 5 000 ms et un coût d'endurance
non nul permettent d'observer coût, calcul puis expiration. Choisir une cible
vivante, acquise et réellement à portée. Le profil borne la protection des
attaques directes ; ne pas attribuer implicitement ce bonus aux dégâts de chaleur.
Le rejeu graphique du 21 octobre utilise ensuite 60 000 ms, retouchés dans le
Studio sur le même effet, pour laisser le temps d'observer le refus de cumul
et de reprendre une sauvegarde avant expiration. Coût, calcul, identité,
échéance et expiration restent exigés ; cette calibration ne les dispense pas.

Pour la cinématique, plusieurs plans de 10 000 ms donnent le temps de relever
le rendu et de sauvegarder en cours de lecture. Choisir des positions de caméra
distinctes de leur cible. Préparer trois parties ou reprises distinctes pour fin,
saut et interruption. La quête active doit rester accomplissable après chacune.

Pour le calendrier, une minute simulée de 1 000 ms et une première échéance à
20 minutes donnent une arrivée observable après 20 secondes de simulation active.
Le réarmement exige un événement récurrent, la défaite de tous les participants
et le délai configuré. Relever séparément heure prévue et activation observée.

Pour le script, une variable booléenne initialement fausse sert de témoin :
effectuer une première écriture puis un accès non autorisé. Vérifier l'absence
de mutation retenue, autoriser l'accès et rejouer. Pour isoler le budget CPU,
utiliser deux répétitions imbriquées de 16 contenant une comparaison de variable
autorisée dont les deux branches sont vides : les lectures et boucles dépassent
256 étapes sans produire des centaines d'événements. Réduire ensuite à 4 et 8.
Tester séparément le budget d'événements avec des messages ; ne pas confondre
le premier quota atteint avec l'autre. Avec le profil à 128 événements, une
affectation suivie de répétitions 16/8 contenant un seul message reste sous
256 instructions exécutées (146 prévues), mais dépasse le quota de sortie.
Après refus, vérifier zéro reçu et la variable initiale ; réduire la seule
boucle externe à 4, puis constater 32 messages dans 34 événements, une seule
transaction et le même reçu après sauvegarde/reprise. Le diagnostic doit mener
au nœud fautif et proposer de réduire les sorties ou les répétitions.
La vérification de brouillon utilise une nouvelle partie en mémoire
sans physique : vérifier ensuite le programme retenu dans le jeu normal.

Pour l'opération de porte, choisir une porte réelle à portée, l'ouvrir par le
script puis constater le changement visible et le passage physique. Sauvegarder
et reprendre pour vérifier cet état. Le raccordement passe par l'action de porte
commune et la projection de son état ; une réussite du vérificateur de brouillon
ne prouve ni le collider ni ce passage. Les permissions de script ne constituent
pas une preuve de visibilité physique de la porte.

Pour le calendrier, le relevé tardif de `last_frame.events` ne constitue pas un
historique des bornes franchies. `simulation.activations` conserve les
activations de rencontres. Lier une rencontre minimale à l'événement et relever
son identité, son cycle et son échéance avant/après reprise pour disposer d'un
oracle durable. Le premier essai `ui09-clock-partial-e-001.receipt.json` ne
prouve que la reprise cohérente de l'horloge, calibrée à 5 000 ms par minute.

## Sauvegardes et retour au Studio

Dans le runtime natif, `G` ouvre les actions et dialogues, `F5` sauvegarde la
partie, et `F9` prépare la reprise au prochain lancement. F9 ne recharge pas le
joueur vivant : quitter puis relancer depuis le Studio. « Nouvelle partie »
conserve la dernière sauvegarde tant qu'aucune sauvegarde explicite ne l'écrase.

Relever le hash de la partie avant/après reprise et le hash source du module.
Une retouche fonctionnelle peut rendre une partie incompatible ; le refus doit
être motivé et conserver la sauvegarde précédente. Ne pas élargir les options
de compatibilité pour obtenir artificiellement un succès.

## R5-P01 / R5-P02 et isolation des deux modules

Ouvrir A puis B, puis revenir à A dans le même Studio. Donner aux deux modules
des configurations de surfaces distinctes ; B peut se concentrer sur les
dialogues/quêtes/objets, A sur les systèmes supplémentaires. Masquer une surface
contenant déjà des données, sauvegarder/rouvrir, puis la réafficher et retrouver
ces données. Les exigences du contenu caché restent conservées.

Jouer et sauvegarder chacun avec le même exécutable runtime. Reprendre A après
avoir joué B : ses inventaires, quêtes et sauvegarde ne doivent pas être ceux de B.
Relever les IDs de module/zone et les hashes de sauvegarde avant/après chaque
changement. Le producteur, la compilation et un simple changement d'onglet ne
prouvent pas ce parcours.

Enfin, rouvrir les sources et réexporter les configurations retouchées avec leurs
assets et origines. Rapprocher les empreintes des documents et les références
dans les archives réouvertes. Les droits privés restent privés.

## Verdict attendu

Pour chaque cas, conserver PASS, FAIL ou NON EXÉCUTÉ avec ses limites. Ne pas
fusionner contrats métier, persistance, physique headless, graphique, acceptation
propriétaire, CI distante et distribution. Clôturer uniquement après rapprochement
des exigences avec les preuves réelles ; garder les réserves et refus historiques.
