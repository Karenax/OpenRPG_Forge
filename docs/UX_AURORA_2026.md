# Direction UX — « Aurora 2026 »

« Aurora 2026 » est le nom interne de l’intention fonctionnelle, pas une marque publique. OpenRPG
Forge doit retrouver l’immédiateté d’un grand toolset de modules tout en proposant une identité,
des données, des règles et des interactions originales.

## Promesse créateur

Une personne qui ne connaît pas le moteur sous-jacent doit pouvoir partir d’un dossier vide et :

1. créer un module et ses zones ;
2. définir et placer décors, portes, créatures, objets, déclencheurs et rencontres ;
3. écrire dialogues, quêtes, journal, règles, pouvoirs et scripts ;
4. importer et valider ses assets ;
5. tester immédiatement depuis le point choisi ;
6. corriger les diagnostics sans quitter Forge ;
7. exporter, recharger et partager un `.orpmod` sûr.

L’IA accélère ces opérations mais aucune fonction de création ne doit en dépendre.

## Architecture de l’interface

Les espaces produit restent stables :

- **Monde** — zones, kits modulaires, placement et rencontres ;
- **Blueprints** — définitions réutilisables, propriétés et variantes ;
- **Dialogues** — embranchements, conditions, actions et localisation ;
- **Quêtes** — états, transitions et journal ;
- **Scripts** — ForgeScript, diagnostics, tests et API disponible ;
- **Assets** — import, provenance, licence, collision et LOD ;
- **Validation** — références, performances, sécurité et préparation à l’export ;
- **Playtests** — points de départ, scénarios, logs et artefacts ;
- **Assistant** — plans prévisualisables, diff, application et annulation.

Ces espaces partagent le même projet versionné, le même bus de commandes, la même sélection et le
même vocabulaire. Ils ne deviennent pas une collection d’outils indépendants.

## Principes visuels et d’interaction

- hiérarchie sombre, calme et lisible, avec accent braise pour l’action principale ;
- une action primaire évidente par contexte ;
- informations techniques détaillées dans des panneaux repliables, pas dans le parcours principal ;
- états explicites : prêt, modification non sauvegardée, validation, playtest, erreur ;
- diagnostics reliés à l’objet concerné et formulés avec une correction possible ;
- raccourcis clavier et manipulation directe, sans rendre les boutons incompréhensibles ;
- aperçu et test toujours proches de l’édition ;
- aucune marque du moteur sous-jacent dans les surfaces produit contrôlées par Forge.

## État actuel

Le premier écran Monde permet déjà de créer un module, ouvrir un projet, éditer, sauvegarder, exporter et
reconstruire une zone. L’inspecteur de propriétés locales distingue clairement ce qui appartient à
l’instance de ce qui reste hérité du blueprint, avec types lisibles et action principale unique.
T3.2 ajoute la sélection du point de départ, le snapshot temporaire, le lancement immédiat et le
retour des logs. L’espace Dialogues fournit maintenant liste de nœuds, choix branchés, conditions,
actions, textes localisables et aperçu dans la même identité sombre et braise. Le runtime offre une
sauvegarde/reprise F5/F9 et l'Assistant peut préparer, appliquer et annuler un plan de dialogue par
MCP. Le MVP est fermé ; les espaces Quêtes, Assets, Validation, l'éditeur de code Scripts et
l’enrichissement de la manipulation directe restent dans le gate **Creator Core G7**, et non dans une
prétendue parité Aurora déjà atteinte.

G7-02 installe la coque commune : neuf workspaces stables, une colonne d’exploration du projet, le
workspace Monde au centre et un panneau contextuel pour sélection, diagnostics et logs de playtest.
L’explorateur classe zones, entités, types de blueprints, dialogues et quêtes. La sélection est un
contrat moteur-indépendant, partagé avec l’inspecteur et verrouillable ; un diagnostic structuré peut
cibler l’objet concerné.

G7-03 active le workspace Blueprints dans la même identité sombre et braise. Sa palette recherche
par nom, slug ou type ; le panneau central édite une définition ou la dérive de l’instance Monde
courante ; le panneau droit expose les entités et locuteurs qui la référencent. La sélection du
blueprint reste partagée avec la coque, et toutes les mutations passent par le même historique que
le Monde. Les workflows Quêtes, Scripts, Assets, Validation et Assistant restent annoncés sans être
présentés comme terminés.

G7-04 rapproche la palette de l’action : une liste compacte flotte dans le Monde et chaque
blueprint peut être glissé sur le sol. Un fantôme vert confirme le snap, un fantôme rouge conserve
la cause du refus dans les diagnostics. Un clic sur une représentation affiche le gizmo Forge rouge
et bleu ; le glisser déplace l’instance sur X/Z et ne crée une commande qu’au relâchement valide.
Les champs numériques et boutons existants restent disponibles comme alternative précise et
accessible.

G7-05 ajoute une surface dédiée « Zones & transitions » sans fragmenter le workspace Monde. Trois
cartes alignées présentent le monde du module, l’assistant de création et les liaisons entre points
d’ancrage. L’accent braise reste réservé à la création ; l’activation emploie un repère `◆` et met à
jour immédiatement l’aperçu, la palette d’entités et l’inspecteur partagé. Les dimensions et le
gabarit sont lisibles avant mutation, tandis qu’un bandeau inférieur expose les diagnostics de
références. Cette surface manipule des documents de zone indépendants mais conserve le même bus de
commandes et le même historique que le reste du Creator.

G7-06 ajoute un clipboard sémantique visible sans reproduire le presse-papiers brut du système. Un
résumé compact indique la nature et le nombre d’objets copiés ; « Coller » et « Dupliquer » restent
des actions secondaires près du contexte Monde. Pour une zone, la copie est volontairement plus
explicite : nom, mode complet ou gabarit, conservation des transitions externes, puis aperçu des
entités et liaisons remappées avant activation du bouton principal. Les nouveaux UUID ne sont pas
du bruit visuel, mais restent disponibles dans le résumé de contrôle et les diagnostics.

G7-07 place la recherche transversale dans une fenêtre Aurora dédiée plutôt que dans une barre
technique. Trois cartes alignées rendent le parcours évident : résultats et filtres à gauche,
identité et usages au centre, renommage sûr à droite. Les chemins, UUID et provenance restent
lisibles mais secondaires. Le ciblage rejoint l’explorateur et l’inspecteur partagés. L’action
braise « Appliquer le renommage » demeure verrouillée tant qu’un aperçu valide n’a pas annoncé le
nombre de références préservées ; modifier le nom ou le slug invalide immédiatement cet accord.

G7-08 installe « Configuration du module » comme écran auteur de premier rang. La surface conserve
les trois cartes Aurora : publication à gauche, langues au centre, variables typées à droite. Le
langage reste produit — auteur, version, licence, crédits, scope — et les UUID/provenances demeurent
accessibles dans les résumés sans envahir le formulaire. Les listes localisées et typées disposent
d’un état vide explicite. L’action braise reste verrouillée jusqu’à une prévisualisation valide qui
annonce le nombre de langues, de variables et la version prête à exporter.

G7-09 agrandit l’espace Blueprints en quatre cartes équilibrées : catalogue, définition, origine et
provenance, références inversées. Le filtre de tête sépare explicitement toutes les palettes,
Standard, Custom, généré, favoris et temporaire. Une étoile repère les favoris ; un badge vert
`CUSTOM · éditable` ou ambre `STANDARD · lecture seule` annonce les droits avant toute action. Les
détails de conformité — source, auteur, SPDX, hash, origine IA et transformations — restent groupés
dans une seule carte. L’action secondaire violette prévisualise et applique la provenance ; l’action
braise demeure réservée à l’enregistrement du blueprint éditable.

G7-10 installe une barre caméra flottante dans l’angle supérieur du viewport, alignée avec la
palette de placement sans masquer le centre de travail. Le mode courant, la vitesse et le nombre de
repères restent lisibles en permanence. Les actions courtes — cadrer, 3/4, dessus, face, gauche,
vitesse — sont accompagnées d’une aide directe aux gestes souris et au pilote WASD. L’orthographique
est signalée explicitement ; les bookmarks restent une préférence auteur et ne salissent ni
l’historique ni le package du module.

G7-11 garde les événements de cycle de vie hors de l'espace visuel principal : leur séquence,
binding, autorité, cause et contexte apparaissent dans le journal de playtest structuré. Une erreur
de cycle de vie conserve un code et un chemin de document exploitables par la navigation des
diagnostics, sans interrompre l'auteur avec une boîte de dialogue opaque.

G7-12 transforme l'espace Scripts en première surface spécialisée de build. Trois cartes compactes
annoncent le graphe, l'impact du prochain export et la règle du cache ForgeScript. La liste centrale
distingue immédiatement `REBUILD`, `CACHE` et `EXTERNE`, montre le type, le chemin, le hash court et
la licence, tandis qu'une carte séparée explique les packs ajoutés automatiquement. L'auteur peut
actualiser l'analyse sans écrire la source ni déclencher un export.

G7-13 rend le workspace Validation opérationnel avec une surface Aurora en trois colonnes. La carte
de gauche présente une cible à la fois et regroupe ses options sans exposer de secret. La carte
centrale traite le registre tiers comme un outil auteur — nom, version, SPDX, responsable,
justification, cadence et retrait — plutôt que comme un fichier juridique à éditer. La carte de
droite donne l’aperçu de conformité localisé : vert lorsque le NOTICE est prêt, ambre/rouge avec
code et chemin lorsque l’export serait bloqué. L’action braise enregistre atomiquement la
configuration et choisit la cible du prochain export ; elle reste verrouillée jusqu’à la
prévisualisation.

G7-14 ajoute une seconde surface spécialisée au workspace Validation. La configuration garde trois
cartes lisibles : profils d’agents à gauche, liens hors-maillage au centre, aperçu et diagnostics à
droite. Le sélecteur de zone et la granularité du bake restent dans l’en-tête ; les actions Masquer,
Analyser et Appliquer forment un pied stable. L’overlay turquoise montre les cellules praticables
sans masquer la scène et les segments violets distinguent immédiatement les liaisons spéciales. Les
diagnostics ambre conservent code et chemin, tandis que l’action braise applique la configuration au
document seulement après une prévisualisation locale.

G7-15 transforme la colonne Diagnostics de la coque commune en parcours de résolution. La ligne
reste compacte — sévérité, code et message — tandis que l’infobulle conserve UUID, chemin, sujet,
propriété et source. La sélection cible immédiatement l’entité ou la zone. Sous la liste, une carte
**Correctif guidé** n’apparaît active que lorsqu’une action sûre existe : **Prévisualiser** reste
secondaire et sans mutation, **Appliquer** reçoit l’accent braise. L’auteur garde donc le contexte du
Monde et peut annuler ou rétablir la correction avec le même historique que ses autres gestes.

G7-16 active l’espace Assets sans rompre cette hiérarchie. Une colonne compacte filtre texte, source,
catégorie et état local ; la liste centrale garde un état vide explicite et limite l’affichage à 300
résultats ; l’inspecteur droit montre aperçu, UUID, package source, hash, provenance, licence, tags et
chemins. Les détails défilent indépendamment afin de garder l’action et le pied accessibles à
1240 × 780. **Prévisualiser l’import** reste l’unique action braise et ne devient active qu’après
validation de licence, hash et disponibilité. Les dimensions, styles, métriques 3D, compatibilité et
turntable enrichiront ce même contrat, sans créer une seconde bibliothèque.

G7-17 active la localisation dans l’espace Assistant. L’en-tête résume messages, langues et taux de
couverture ; la colonne gauche filtre les clés canoniques et marque les traductions présentes. La
carte droite conserve clé, provenance et texte de repli au-dessus de l’éditeur, puis montre le texte
résolu ou pseudo-localisé. **Prévisualiser** ne crée aucune révision ; **Appliquer au projet** porte
l’accent braise et rejoint l’historique commun. Les états sans projet, sans sélection et traduction
vide restent explicites, sans faire disparaître les actions du pied à 1180 × 760.

L’échelle G7-18 rejoint directement la barre d’édition du Monde : X/Y/Z précis, verrou uniforme,
incréments compacts et raccourci Ctrl + molette. La validation refuse avant mutation les limites et
collisions nouvellement introduites, puis le viewport reconstruit immédiatement la représentation.

G7-19 enrichit l’analyse Navigation sans ajouter une seconde carte : le résumé indique désormais la
longueur d’une route représentative, le nombre de cellules visitées ou la cause d’échec. Obstacles
dynamiques, coûts et récupération restent des paramètres du Core ; l’overlay demeure une vue dérivée.

G11-F rend la coque adaptable sans casser sa hiérarchie : le profil local règle l’échelle générale,
la taille du texte et une chaîne de polices système avec replis. Une barre d’onglets de zones reste
sous les workspaces ; la zone active est marquée, la fermeture de la dernière zone est interdite et
une seconde zone peut servir de comparaison en lecture seule. Le catalogue distingue désormais
favori projet, favori personnel non exporté et récents ordonnés. La même surface de préférences
prévisualise le profil artistique du module et présente les écarts d’échelle, densité texel, shader,
époque, réalisme et palette comme avertissements actionnables, jamais comme verdict esthétique opaque.

G11-G transforme **Environnement de zone** en surface responsive à trois onglets. **Rendu & ciel**
regroupe PBR, qualité, tone mapping, HDR, exposition, Forward+, éclairage par pixel, budgets desktop
et ciel physique, procédural ou panorama sans noyer l'auteur dans les propriétés du moteur.
**Brouillard** et **Audio** conservent leurs réglages dans des parcours courts. La prévisualisation
3D et le jeu consomment le même profil canonique ; un budget approché ou dépassé reste visible comme
diagnostic, et l'action braise applique l'ensemble par la commande transactionnelle existante.

## Contexte graphique 3D

La cible environnementale complète est une direction cohérente de type *stylized realism*, avec
échelle métrique, matériaux PBR normalisés, silhouettes lisibles, éclairage maîtrisé, LOD,
collisions, navigation et profils de qualité. Elle est détaillée dans
[`assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md`](assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md).

L’état visible actuel reste double : la Crypte utilise des primitives procédurales originales pour
prouver les workflows, tandis que le prototype Forgotten Forest prouve le scatter par chunks et la
peinture d’un masque RGBA sur 128 × 128 m. Sa capture
[`forge-forest-map-demo.png`](assets/screenshots/forge-forest-map-demo.png) est une preuve
fonctionnelle intermédiaire, pas une validation artistique finale. La curation des assets, les
matériaux normalisés, le relief, la composition, l’eau, l’éclairage, la navigation et le profilage
restent nécessaires avant d’en faire la référence graphique d’OpenRPG Forge.
