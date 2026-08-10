# Format de module `.orpmod`

Un module communautaire est une archive ZIP contrôlée, inspectable et versionnée. Ce n’est pas un PCK Godot non fiable.

## Structure cible

```text
manifest.json
THIRD-PARTY-NOTICES.txt
content/
  project.json
  scripts/*.forge
  areas/*.json
  tests/*.json
assets/
  models/*.glb
  textures/*
  audio/*
localization/
  fr.json
  en.json
provenance/
  assets.json
licenses/
  THIRD_PARTY_NOTICES.md
```

Le manifest porte au minimum les versions de format/module/runtime/API, l’identifiant et le slug du
module, la zone d’entrée, la cible, les dépendances automatiques, le registre tiers approuvé, le
chemin du NOTICE et le hash de contenu.

Le schéma courant du manifest est `1.2.0`. Le hash SHA-256 couvre, dans un ordre ordinal stable, le
chemin UTF-8 puis les octets de chaque entrée hors manifest. L’exporteur écrit une archive
déterministe dans un fichier temporaire voisin, la valide, puis la remplace atomiquement.

Le document projet courant est `0.6.34` et le workspace `1.28.0`. En plus du
`WorldFeatureCatalog` v1, `ModuleRuntimeExtensionProfile` porte un `PostLaunchCapabilityProfile` v1
pour gameplay avancé, Live Director, réseau, expérience Creator et production. Un projet `0.6.32`
ou workspace `1.26.0` migre vers un profil post-lancement désactivé à identité stable, puis est
réécrit dans le format courant. Le round-trip projet, workspace et `.orpmod` conserve ces contrats
sans autre source de vérité moteur.

## Packages de contenu versionnés

`module.content.packages` est le catalogue canonique v1. Chaque package porte un UUID, un namespace,
une version SemVer, une provenance, une licence SPDX, un hash de manifeste et une priorité explicite.
Le manifeste distingue dépendances `required`, `optional` et `peer`, sources JSON/texte/YAML/ForgeScript,
assets binaires gouvernés et ressources scellées. Un chemin correspond à une seule ressource logique.

La résolution est locale, hors ligne et déterministe. Une dépendance optionnelle absente est ignorée ;
une dépendance requise absente, une contrainte incompatible, un cycle ou une tentative d’override d’une
ressource scellée produit un diagnostic stable avant le build. `forge pack` valide licences, provenance,
hashes, chemins et budgets, compile les sources ForgeScript, puis génère en mémoire
`content/packages.lock.json`, `content/packages.manifest.json` et le bytecode `.asbc`. Ces artefacts sont
inclus dans le `.orpmod` sans imposer l’édition manuelle d’un fichier généré dans le workspace.

## Localisation et identités de ressources

Le catalogue de localisation v1 porte désormais, en plus des langues et messages, un namespace de
package, une licence SPDX, une provenance, un budget d’octets par plateforme et un hash SHA-256
recalculé depuis sa représentation canonique. Les messages acceptent les expressions ICU bornées
`plural` et `select`, tout en conservant les replis langue exacte, langue parente, langue par défaut
et texte source. Les échanges traducteur utilisent un bundle JSON versionné lié à l’UUID et au hash
du catalogue ; un bundle obsolète, incomplet, ambigu ou invalide est refusé atomiquement.

Chaque source ou asset d’un package reçoit une adresse stable `package:type/path` adossée à son UUID.
Les aliases utilisent la même syntaxe, sont uniques à l’échelle du catalogue et ne remplacent jamais
l’identité UUID. Les références sont résolues par adresse ou alias avant build ; les collisions et
références manquantes produisent des diagnostics stables. Le navigateur de contenu fusionne ces
ressources, les messages localisés et le Vault externe sans exposer de chemin système aux scripts :
ForgeScript ne reçoit que des handles opaques, et MCP ne fournit que des lectures filtrées ou des
plans dry-run.

## Validation obligatoire

Le loader contrôle avant extraction ou montage : normalisation des chemins, traversées, nombre et taille des fichiers, ratio de compression, allowlist d’extensions, types réels, hashes, schémas, IDs et dépendances. DLL, assemblies, binaires natifs et scripts Godot sont refusés.

Les tests du lot 7 couvrent au minimum round-trip, path traversal, ratio de compression hostile, DLL refusée, IDs dupliqués et hash invalide.

Le chargeur extrait uniquement après validation dans un cache isolé par UUID de module et hash de
contenu. Le runtime accepte `--module-package <chemin-absolu.orpmod>` et rejoue le même scénario
headless depuis le package validé.

## Profils et conformité de distribution

Le module conserve exactement cinq profils canoniques : `Editor`, `Client`, `DedicatedServer`,
`Test` et `Shipping`. Ils portent leurs options de données d’édition, debug, optimisation, licences,
nom de sortie et une référence de profil de secrets. Cette référence ne contient aucune valeur
secrète ; les caches `.forge`, profils locaux et secrets sont exclus de l’archive.

Chaque dépendance tierce déclarée conserve UUID, version, licence SPDX, propriétaire, justification,
cadence, plan de retrait, source, cibles et provenance. Client, serveur dédié et Shipping sont
bloqués avant écriture si le module, un blueprint ou une dépendance requise possède une licence ou
une provenance inconnue. L’export génère ensuite `THIRD-PARTY-NOTICES.txt`; le validateur recalcule
ce NOTICE depuis le projet canonique et refuse toute divergence.

Le CLI expose :

```powershell
forge package export <workspace> <module.orpmod> --target shipping
forge package compliance <workspace> shipping
```

## Graphe et cache de build

L'export dérive un graphe versionné des documents projet/zone, scripts, assets, packs externes et du
module final. Les UUID sont stables par chemin et chaque nœud porte hash, licence, provenance et
dépendances résolues. `.forge/build-graph.json` conserve le dernier état de manière atomique ; ce
cache est jetable, exclu du `.orpmod`, et sert uniquement à calculer les ressources affectées ou
réutilisables. Les blueprints de packs réellement instanciés ou utilisés comme locuteurs alimentent
automatiquement la liste minimale `dependencies` du manifest.

Le bytecode ForgeScript suit le même principe dans le cache utilisateur : sa clé combine le hash de
la source, la version d'API et la version du compilateur, puis le hash du bytecode est revérifié avant
chargement. Une entrée incompatible ou corrompue est recompilée.
