# Gate 3 — acceptation propriétaire et objectif de clôture, 13 octobre 2026

Le propriétaire déclare dans la conversation de livraison :

> je valide **La boucle RPG dans le jeu séparé** : dialogue, quête, combat, sauvegarde/reprise et parcours complet créé depuis Studio.

Il autorise ensuite :

> ok, lance le travail avec pour objéctif de cloturer la gate 3

L'acceptation propriétaire du parcours RPG séparé et de son origine Studio est
acquise. Elle est conservée distinctement des verdicts machine : elle ne fournit
pas une identité de binaire, un replay technique supplémentaire ou un résultat CI.
Les preuves humaines du 8 octobre et du retour résident du 13 octobre conservent
leurs périmètres propres.

## État technique à l'ouverture du chantier

La version de travail est le checkout commun
`E:/OpenRPG_Forge/OpenRPG_Forge-Rust`, branche `Rust`, HEAD observé
`4799ebb3dfa98795d97862f0fed3cd4f6a876887`. Ses modifications locales et les
sources non suivies doivent être inventoriées ; HEAD seul ne les représente pas.
Les scripts Python Hivemind indépendants restent visibles dans l'inventaire
informatif et ne sont pas absorbés dans une publication implicite.

Le code `apps/forge_studio/src/native_game.rs` transmet une archive immuable de
la carte au runtime `tidewater_plant_probe --walk`. Ce runtime installe la
locomotion, la physique et le rendu ; il n'installe pas encore le plugin RPG
générique. Le raccord du test intégré précédent est explicitement retiré.
Il faut qualifier le même noyau de gameplay dans le jeu séparé sans réactiver
ce mode Studio et sans modifier `E:/OpenRPG_Forge/Lancer-Editeur.cmd`.

Le dernier run CI observé `38394829662` est attaché au HEAD ci-dessus. Les quatre
jobs sont refusés avant toute étape pour facturation/plafond. Verdict distant :
**NOT RUN — infrastructure billing**. ADR-0006 a expiré lors du retour des
runners et de la préparation des installateurs ; aucune prolongation tacite,
aucune CI verte et aucune nouvelle dérogation ne sont déduites de la présente
acceptation fonctionnelle.

## Travaux autorisés vers la clôture

1. Raccorder les documents authorés et les commandes RPG existants au jeu natif
   séparé, avec dialogue, quête, combat, inventaire et sauvegarde/reprise.
2. Qualifier les critères environnementaux et l'IA au danger sur cette voie,
   avec les mêmes gardes et les échecs historiques conservés.
3. Produire les contrôles workspace natifs/WASM/supply-chain et les parcours
   réels sur une même empreinte de sources, avec binaires et reçus épinglés.
4. Préserver les documents auteur, les retouches, la caméra et le retour au
   Studio résident ; conserver le point d'entrée et le profil communs.
5. Présenter un bilan de clôture distinguant critères satisfaits, acceptation
   propriétaire, conditions CI et toute condition encore ouverte.

Les commits, pushes, publications de release et l'ouverture de Gate 4 ne sont
pas autorisés implicitement par cet objectif. Gate 3 reste ouverte pendant les
travaux et ne reçoit un verdict final que sur les preuves obtenues.
