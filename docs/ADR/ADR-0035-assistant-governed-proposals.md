# ADR-0035 — Propositions IA et MCP au-dessus des commandes d’auteur

Date : 22 octobre 2026. État : implémentation locale en qualification Gate 6.
Cette décision décrit les frontières réalisées ; elle ne clôture pas la gate.

## Contexte et décision

Le fournisseur et le client MCP produisent des propositions non fiables. Ils
ne reçoivent ni accès au système de fichiers, ni shell, ni commande d’approbation
ou d’application. Le domaine `forge_project::assistant` compile un plan borné
en une transaction `ZoneRequest` existante. Le document Forge et ses reçus
restent l’unique source d’auteur ; le gameplay sauvegardé fonctionne sans modèle.

L’hôte natif construit un `SelectionScope` depuis la révision, la sélection et
la région courantes. Les assets partagés sont résolus dans la bibliothèque et
liés à leurs empreintes. La création additive est explicite ; elle garde le
profil, le joueur, les capacités et les surfaces configurés. La commande concise
de quête développe les définitions canoniques éditables, sans runtime spécifique.
Un plan ne peut pas activer un Creator ou une surface indisponible.

Les commandes concises de quête et de lieux utilisent des clés locales bornées ;
Forge attribue leurs identités déterministes, puis valide les définitions
canoniques. Les liaisons de lieux ne ciblent que le même ajout. La contrainte
UUID du schéma fournisseur reste indicative : une contre-épreuve du serveur
local l’a contournée. Aucune conformité au schéma annoncé ne remplace la
validation native ; les ajouts avancés conservent leurs contrôles UUID.

Un placement du Vault doit aussi rejoindre le package de jeu local. L’export
résout d’abord les imports préservés, puis les seuls pins manquants dans le Vault
en lecture seule, avec leur version historique si nécessaire. Il ne modifie ni
la bibliothèque utilisateur ni les sources d’auteur. Les modèles et déclarations
de licence sont capturés dans le catalogue gouverné existant et son package,
avec les mêmes validations de média, d’empreintes et de budget. Cette voie est
réservée aux exports privés ; elle n’infère aucun droit de redistribution.
L’auteur absent est explicitement non renseigné. La date d’acquisition de cette
copie dans le package est la date native de capture ; la preuve distingue cette
date d’une date d’achat d’origine inconnue. Aucune date d’achat ou paternité n’est
inventée. La qualification du jeu reste à établir après cette correction.

Le parcours manuel révèle une troisième origine : le kit gouverné livré dans
les ressources du Studio. L'export privé résout ses seuls pins absents des imports
avant de consulter le Vault. Le lecteur natif utilise le résolveur de ressources
Tauri, sans chemin du client ; le manifeste gouverné, l'identité dérivée, le hash
exact et le format GLB restent validés. Le package conserve les octets, la
provenance et la licence déclarées, dans les mêmes budgets. La capture ne crée
ni droit de redistribution, ni auteur ou date d'acquisition d'origine inventés.
Un pin connu modifié ou un modèle invalide est refusé sans recherche de secours.

L’envoi fournisseur et l’application ont deux approbations distinctes, liées à
leurs hashes exacts. Une modification de contexte, données, budget ou diff exige
une nouvelle revue. Le host revalide permissions, révision et candidats avant
mutation ; les refus sont atomiques. Audit et undo restent ceux des commandes
d’auteur. La liste fermée des opérations et les capacités manquantes sont exposées
au planificateur ; aucun plan de secours n’exécute du code arbitraire.

## Projection des bâtiments du module joué

Le jeu générique doit projeter les bâtiments paramétriques conservés dans le
même head canonique. Il réutilise `BuildingProjection`, déjà employé par le
Studio, et ses identités de pièces, matériaux standards et collisions statiques.
Aucune conversion en nouvelle source ni géométrie parallèle n’est introduite.
Le joueur attend la registration Rapier initiale ; cette barrière ne réactive
pas une pause ultérieure de récupération de sauvegarde. Le rapport natif relie
le nombre de bâtiments, les huit éléments de chaque salle générée, leur
registration et leur readiness au hash de la source exportée. Les champs
absents des anciens rapports ne prouvent aucun bâtiment. Passage réel de porte,
intérieur, sauvegarde et reprise exigent des preuves sur le candidat courant,
distinctes des tests de registration et d'une acceptation esthétique.

La recette d'intérieur révèle ensuite que le toit opaque laisse le matériau
standard presque noir malgré la lumière ambiante extérieure. La présentation
native des salles paramétriques reçoit un éclairage intérieur neutre dérivé de
chaque salle (au plus 32), avec ombres pour contenir sa contribution. Il ne
modifie ni le soleil ni l'exposition extérieure, et n'ajoute aucune donnée de
gameplay ni nouvelle source canonique. Il accompagne le matériau et la géométrie
de présentation provisoires ; il ne constitue pas un éditeur de luminaires ou
une qualification esthétique. Son utilité doit être revue dans la capture réelle
d'intérieur avant acceptation du correctif.

## Persistance et reprise

Les journaux `forge.assistant-job.v1` et les propositions
`forge.mcp-proposal.v1` résident dans la racine d’auteur native. Leurs enregistrements
de staging ne sont pas des permissions. Les lecteurs bornent tailles, nombres,
identités et empreintes, refusent les liens et ne prennent aucun chemin du client.
La reprise de pending réutilise `DocumentStore` et ses contrôles.

Un reçu canonique réconcilie une application dont l’accusé est perdu sans rejouer
la mutation. Une application non sauvegardée et effectivement perdue ne retrouve
un résultat Staged que dans son contexte original, avec une nouvelle approbation.
Une reprise MCP exige une nouvelle autorisation ; son hash de revue est lié à un
nonce natif neuf. Les identités de commande stables empêchent les doublons.

Le fournisseur natif applique les destinations configurées, limites de taille,
connexion, sortie, coût et tentatives. Aucun retry HTTP implicite. Une réponse
valide dépassant le budget est conservée et bloquée, pas appliquée. Une tentative
incertaine ne déclenche pas silencieusement un nouvel envoi. Les clés restent dans
l’environnement natif ; le raisonnement reçu n’est ni affiché ni journalisé.

## Qualification

Les tests métier, tests natifs et contrôles WASM ne prouvent pas le parcours UI.
La création réelle, les retouches humaines, le Vault réel, la reprise graphique,
le jeu hors fournisseur et la comparaison manuelle restent des preuves distinctes.
Suivre le [plan Gate 6](../planning/GATE6_IMPLEMENTATION_PLAN.md) et son
[rapport courant](../evidence/gate6/GATE6_PROGRESS_20261022.md).
