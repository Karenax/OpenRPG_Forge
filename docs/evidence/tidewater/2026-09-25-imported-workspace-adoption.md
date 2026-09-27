# Tidewater — adoption d'une zone avec ses sources binaires

Le parcours existant de `FileWorkspaceSession` vérifie maintenant les sources
binaires lors de la revue d'adoption. Cette revue ne crée aucun répertoire ni
ne copie de données dans la source ou la destination. L'application de l'adoption,
sous verrou de source, copie tous les blocs référencés par le head et l'historique
avant publication de l'objet et modification du head du workspace.

Une erreur peut laisser des blocs immuables orphelins dans la destination, mais
ne rattache pas une zone incomplète. Le JSON original de la zone n'est pas modifié.
La lecture des objets exige maintenant un magasin existant, sans création implicite
si le répertoire des blocs a disparu.

## Qualification

L'exemple `verify_imported_adoption` a adopté la vraie zone Tidewater dans
`E:/OpenRPG_Forge/.tidewater-integration/workspace-adoption-001`, puis sauvegardé
et rouvert le workspace. Les **11 blocs / 26715432 octets** sont vérifiés dans
la destination ; le hash de l'archive reste
`3b37a4c9e5203193e7655084a8b4c0ee8bed5f467ad7751e24d2eee2d27edba4`.
Rapport : `workspace-adoption-verification-001.json` dans la même racine de preuves.

Cinq tests passent : le scénario de sources immuables étendu à l'adoption,
deux tests historiques d'adoption et deux tests du magasin d'objets. Le scénario vérifie le refus d'une source
incomplète sans mutation du head, la revue sans copie, la sauvegarde/réouverture,
la conservation du JSON original et l'indépendance de la destination après
corruption d'un blob dans la source originale. Clippy projet/exemple passe.

## Limites

Ce résultat utilise le parcours d'adoption d'une zone locale autorisée sous la
racine du workspace. Il ne crée pas une interface de transfert entre workspaces,
ni un import de pack dans Studio. Le rendu de la zone importée dans le viewport,
les prototypes réutilisables et les fonctions visuelles restent ouverts.
