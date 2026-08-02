# G7-08 — Métadonnées et variables du module

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-PROJ-002` — métadonnées localisées du projet ;
- `ORF-PROJ-019` — variables de projet et blackboard global typé.

## Résultat livré

`ModuleDefinition` possède maintenant une configuration canonique optionnelle et rétrocompatible.
`ProjectMetadata` porte un UUID stable, un schéma v1, la langue par défaut, plusieurs noms, résumés,
descriptions et crédits localisés, puis auteur, version sémantique, licence, image relative, tags,
classification et provenance. Les anciens documents reçoivent une configuration explicite de
migration ; aucune valeur n’est recherchée dans une ressource du moteur.

`ProjectVariableDefinition` décrit chaque clé par UUID, type texte/booléen/entier/nombre, valeur par
défaut normalisée, scope session/sauvegarde/joueur/groupe/serveur, description et provenance.
`ProjectBlackboard` initialise les valeurs, valide chaque mutation et sait produire une vue filtrée
par scope. Le runtime utilise ce service pour ForgeScript, dialogues, quêtes, conditions et
sauvegardes. Le mode de compatibilité accepte encore les anciennes clés non déclarées, sans
affaiblir les types des nouvelles définitions.

La commande `set_module_configuration` fournit révision optimiste, dry-run, diagnostic stable,
patch, inverse, audit et undo/redo. Le format agrégé passe à `0.6.2` et l’index de workspace à
`0.8.0`, avec lecture des versions précédentes. La version de publication devient aussi la version
par défaut du manifeste `.orpmod`.

La fenêtre Aurora « Configuration du module » aligne publication, textes localisés et variables
typées. L’auteur travaille dans un brouillon, ajoute ou met à jour ses langues et variables, puis
prévisualise le nombre de documents avant d’activer l’action braise. La recherche globale indexe
ensuite métadonnées et variables par type, scope, valeur, description et provenance.

## Preuves

- `ProjectMetadataAndBlackboardTests` couvre localisation, tags, types, valeurs invalides, scopes,
  compatibilité legacy et unicité ;
- `SetModuleConfigurationHandlerTests` couvre dry-run, application, doublons, conflit de révision,
  undo/redo et conservation lors d’un renommage sûr ;
- `ProjectJsonSerializerTests` couvre round-trip exact et migration `0.6.1` ;
- les 181 tests .NET sont verts et la solution compile sans avertissement ;
- `OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed` prouve l’écran, la
  recherche, la persistance workspace `0.8.0`, le package versionné et la restauration historique ;
- `scripts/Verify.ps1` constitue le gate intégré final du lot.

## Limites conservées

- l’image de couverture est une référence relative validée mais ne possède pas encore sélecteur,
  miniature, variantes localisées ou contrôle de licence d’asset ;
- les scopes joueur, groupe et serveur sont modélisés mais leur stockage partitionné attend les
  runtimes Network Kit ;
- le runtime accepte les clés non déclarées des anciens modules en mode compatibilité ; un profil de
  validation permettra de rendre le schéma strict après migration du contenu ;
- les ressources et mutations MCP spécifiques à ce nouvel agrégat restent à exposer.

Ces limites conservent les deux capacités au statut `Implemented` partiel.
