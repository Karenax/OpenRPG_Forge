# Format de module `.orpmod`

Un module communautaire est une archive ZIP contrôlée, inspectable et versionnée. Ce n’est pas un PCK Godot non fiable.

## Structure cible

```text
manifest.json
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

Le manifest porte au minimum les versions de format/module/runtime/API, l’identifiant et le slug du module, la zone d’entrée, les dépendances et le hash de contenu.

Le schéma courant du manifest est `1.0.0`. Le hash SHA-256 couvre, dans un ordre ordinal stable, le
chemin UTF-8 puis les octets de chaque entrée hors manifest. L’exporteur écrit une archive
déterministe dans un fichier temporaire voisin, la valide, puis la remplace atomiquement.

## Validation obligatoire

Le loader contrôle avant extraction ou montage : normalisation des chemins, traversées, nombre et taille des fichiers, ratio de compression, allowlist d’extensions, types réels, hashes, schémas, IDs et dépendances. DLL, assemblies, binaires natifs et scripts Godot sont refusés.

Les tests du lot 7 couvrent au minimum round-trip, path traversal, ratio de compression hostile, DLL refusée, IDs dupliqués et hash invalide.

Le chargeur extrait uniquement après validation dans un cache isolé par UUID de module et hash de
contenu. Le runtime accepte `--module-package <chemin-absolu.orpmod>` et rejoue le même scénario
headless depuis le package validé.
