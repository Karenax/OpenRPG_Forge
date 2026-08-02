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
MCP. Le MVP est fermé ; les espaces Quêtes, Scripts, Assets, Validation et l’enrichissement de la
manipulation directe au-delà de G7-09 restent dans le gate **Creator Core G7**, et non dans une
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
