# Contrat de création de maps par l'IA

Version : `1.0.0`

Ce document est le contrat statique et versionné utilisé par l'assistant de planification de zones
d'OpenRPG Forge. Il complète le contexte narratif canonique du projet sans le remplacer. Les faits
propres au module et à la zone sont fournis séparément dans le paquet JSON dynamique de chaque
requête.

## Autorité et portée

- Le modèle de domaine versionné est l'unique source de vérité.
- Une réponse de fournisseur est une proposition non fiable. Elle doit être désérialisée, bornée et
  validée avant de devenir un `AssistedAreaPlanDefinition`.
- La proposition décrit uniquement une topologie : nœuds, connexions et références de blueprints.
- Elle ne crée ni mesh, ni terrain, ni navmesh, ni lumière et n'exécute aucun code.
- Elle ne modifie aucun fichier, scène ou module. Toute construction ultérieure passe par le bus de
  commandes, un dry-run, un diff, une approbation humaine et une opération inverse.
- Le brief fourni est autoritaire pour l'identité du plan et de la zone, l'archétype, la seed, les
  dimensions, le nombre de nœuds, les kits, les contraintes, les objectifs et les sources de lore.

## Repère spatial

- Les distances et dimensions sont exprimées en mètres.
- `X` est l'axe horizontal largeur, `Z` l'axe horizontal profondeur et `Y` l'axe vertical.
- Le centre de la zone planifiée est `(X=0, Z=0)`.
- Un nœud occupe un rectangle centré sur `(x, z)` avec `width` et `depth` strictement positifs.
- Le rectangle complet de chaque nœud doit rester dans les demi-étendues du brief :
  `[-widthMeters/2, widthMeters/2]` et `[-depthMeters/2, depthMeters/2]`.
- Les nombres doivent être finis. Il est recommandé de les arrondir au centimètre.
- Le fournisseur ne décide pas de la hauteur `Y`, de la rotation, du relief ou de la pente. Ces
  décisions appartiennent aux services canoniques de terrain et de placement.

## Topologie jouable

- La réponse contient exactement `nodeCount` nœuds.
- Elle contient exactement une entrée `Entry` et exactement une sortie `Exit`.
- Tous les nœuds appartiennent à un graphe connecté.
- Chaque connexion référence deux clés de nœud distinctes présentes dans la réponse.
- Le chemin requis doit permettre d'atteindre la sortie depuis l'entrée.
- Les branches optionnelles ne doivent pas remplacer le chemin requis.
- Une crypte privilégie une progression lisible, des portes et éventuellement un raccourci ou une
  porte secrète.
- Un village privilégie plusieurs accès lisibles, des chemins, des espaces sociaux et des services.
- Les types de nœuds autorisés sont : `Entry`, `Exploration`, `Encounter`, `Social`, `Service`,
  `Objective`, `Rest` et `Exit`.
- Les traversées utilisent un jeton sémantique court tel que `door`, `secret-door`, `path` ou
  `side-path`; elles ne contiennent ni instruction moteur ni chemin de fichier.

## Contenu et assets

- `contentBlueprintIds` ne peut contenir que des UUID présents dans `availableKits` et dans
  `kitBlueprintIds` du brief.
- Le fournisseur ne fabrique jamais d'UUID, de nom d'asset ou de chemin de ressource.
- Au moins un nœud autre que l'entrée et la sortie doit référencer un blueprint afin que le plan
  approuvé soit constructible.
- L'absence d'un asset adapté doit être signalée par une réponse invalide ; elle ne justifie pas une
  référence inventée.
- Les originaux, licences et hashes restent gouvernés par le catalogue d'assets et le Vault. La
  topologie ne peut pas modifier leur provenance.

## Navigation et coexistence

- Les nœuds doivent ménager un parcours continu compatible avec les profils de navigation transmis.
- Les positions proposées doivent tenir compte des entités existantes communiquées dans le contexte.
- Un fournisseur ne doit pas superposer volontairement plusieurs placements solides ni bloquer
  l'entrée, la sortie ou un portail existant.
- Les contraintes du brief priment sur les conventions d'archétype.
- Les diagnostics canoniques de géométrie, navigation et éclairage ont toujours le dernier mot.
- Une proposition qui ne peut satisfaire simultanément le brief et ces règles doit être rejetée ;
  aucune correction silencieuse importante n'est autorisée.

## Gameplay, lore et lisibilité

- Chaque nœud possède un rôle compréhensible et un libellé court lié aux objectifs transmis.
- La progression évite les suites de salles indistinctes et conserve une lecture claire de l'entrée,
  de l'objectif principal et de la sortie.
- Le lore transmis peut guider les libellés et la fonction des espaces, mais ne peut créer de nouvelle
  référence canonique.
- Si le contenu projet est masqué, le fournisseur travaille uniquement avec les informations non
  confidentielles du brief et ne tente pas de reconstruire les données absentes.
- La seed fait partie de la provenance et doit conduire à une proposition reproductible pour un même
  brief, un même contrat et un même fournisseur déterministe.

## Budget et bornes

- Le nombre de nœuds accepté est compris entre 4 et 24 pour la proposition assistée actuelle.
- Une clé de nœud est un jeton stable de 1 à 64 caractères : lettres ASCII minuscules, chiffres,
  point, tiret ou underscore ; elle commence par une lettre ou un chiffre.
- Un libellé contient au plus 120 caractères.
- Un nœud mesure entre 2 et 512 mètres sur chaque axe, sous réserve des dimensions du brief.
- Une liste de tags contient au plus 24 jetons sûrs.
- La réponse JSON complète doit rester dans le quota du fournisseur et ne contenir ni Markdown, ni
  commentaire, ni texte avant ou après l'objet.

## Sortie obligatoire

Le schéma JSON joint à la requête est normatif. La structure logique attendue est :

```json
{
  "summary": "résumé très court",
  "nodes": [
    {
      "key": "entry",
      "kind": "Entry",
      "label": "Entrée",
      "x": -12.0,
      "z": 0.0,
      "width": 6.0,
      "depth": 6.0,
      "contentBlueprintIds": [],
      "tags": ["entry"]
    }
  ],
  "links": [
    {
      "from": "entry",
      "to": "objective",
      "traversal": "path",
      "required": true
    }
  ]
}
```

Les clés locales sont converties par OpenRPG Forge en UUID stables dérivés du `planId`. Les UUID de
plan, de nœud et de connexion ne sont jamais choisis par le fournisseur.

## Cycle gouverné

`contexte dynamique -> proposition JSON -> désérialisation stricte -> validation du domaine -> dry-run -> diff sémantique -> approbation -> construction -> validation navigation/éclairage -> audit -> revert`

Une étape en échec arrête le cycle sans mutation. Le mode hors ligne déterministe peut produire sa
topologie localement, mais il utilise le même brief, le même contrat de validation et le même cycle
transactionnel que les fournisseurs de modèles.
