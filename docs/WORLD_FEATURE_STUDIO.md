# Studio Monde vivant

Le Studio Monde vivant édite le `WorldFeatureCatalog` du module sans ouvrir le JSON. Il couvre huit
familles : Placeables, Portes, Triggers, Rencontres, Routes, Marchands, Compagnons et Scripts.

## Workflow auteur

1. Ouvrir **RPG Kit > Monde vivant**.
2. Choisir l'onglet puis la cible canonique proposée (blueprint ou zone).
3. Saisir les règles d'accès, de cadence, d'autorité, d'économie ou de contrôle pertinentes.
4. Lire le résumé et corriger les diagnostics.
5. Cliquer **Prévisualiser** pour obtenir le diff sans mutation.
6. Cliquer **Appliquer** ; Annuler/Rétablir restaure le catalogue complet.

Les profils portent UUID, schéma v1 et provenance. Les références manquantes, budgets invalides,
routes incomplètes, monnaies incohérentes et réglages sans documentation bloquent l'application.

## Exécution et automatisation

`WorldFeatureRuntimeResolver` produit des décisions déterministes : accès et événements, dégâts et
brèches, activation de trigger, respawn/renfort/nettoyage, échantillonnage de route, devis marchand,
comportement compagnon et résolution script. Les résultats ne dépendent pas de nœuds du moteur.

ForgeScript obtient des handles en lecture seule. MCP expose `query_world_features` et
`preview_world_features`; les filtres et limites sont bornés, et aucune mutation directe n'est
permise. Le projet `0.6.32`, le workspace `1.26.0` et le package `.orpmod` conservent exactement le
même catalogue.

## Compatibilité

Les anciens projets migrent avec un catalogue vide et restent jouables. Le scénario fourni Crypte
des Cendres installe à l'exécution un profil de démonstration déterministe uniquement pour son
ancien catalogue vide. Sauvegarder une configuration depuis le Studio remplace ce fallback.
