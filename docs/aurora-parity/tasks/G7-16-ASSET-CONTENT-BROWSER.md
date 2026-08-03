# G7-16 — Content Browser et Asset Vault local

Date d’exécution : 3 août 2026.

## Capacité bornée

- `ORF-CONT-020` — navigateur de contenu unifié ;
- `ORF-PAL-010` — miniatures et prévisualisation ;
- `ORF-PAL-011` — recherche avancée d’assets.

## Résultat livré

Le catalogue canonique d’assets est indépendant du moteur et versionné par un schéma v1. Chaque
entrée possède un UUID stable, une identité externe, un hash SHA-256, une licence SPDX vérifiée, une
provenance, des facettes, des chemins sûrs et un état de disponibilité explicite. Le loader fusionne
les manifests réels Poly Haven, ambientCG et Kenney sans copier les originaux dans le dépôt. Le Vault
externe reste configurable et le catalogue atteint 605 entrées : 555 assets unitaires et 50 packs.

`AssetCatalogBrowser` fournit une recherche insensible aux accents et des facettes par source,
catégorie, licence et disponibilité. Le même contrat est exposé en lecture par le CLI et MCP. Le
planificateur d’import ne modifie aucun fichier : il refuse une ressource indisponible ou non
gouvernée et produit un chemin de destination déterministe pour une prévisualisation sûre.

L’espace **Assets** affiche métriques, recherche, facettes, résultats, aperçu local ou placeholder,
UUID, hash, licence, provenance et chemins. La colonne de détails est défilable afin que l’action et
le pied restent accessibles à 1240 × 780. Une revue manuelle aux tailles normale et maximisée a
confirmé la hiérarchie sombre/braise, l’aperçu de `3D Apple 001`, les états vide/sélectionné et le
message de dry-run « aucun fichier écrit ».

## Preuves

- `AssetCatalogTests` couvre identité stable, gouvernance et diagnostics de validation ;
- `AssetCatalogBrowserTests` couvre recherche, accents, facettes et planification d’import ;
- `AssetCatalogLoaderTests` charge les deux catalogues réels et détecte la disponibilité locale ;
- `ForgeProjectAgentServiceTests` vérifie le contrat MCP filtré ;
- `OPENRPG_FORGE_ASSET_BROWSER_PASS` couvre le CLI sur les 605 entrées ;
- `OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS` couvre catalogue, facettes, aperçu et import gouverné ;
- la revue visuelle du 3 août 2026 valide l’absence de débordement vertical à la taille nominale.

## Limites conservées

- l’action actuelle prépare l’import sans écrire ; la copie atomique, la quarantaine, le document
  canonique, l’historique et la désinstallation sûre restent à livrer ;
- les manifests actuels ne portent pas encore dimensions, polycount, squelette, matériau, auteur,
  compatibilité, variantes et animations ;
- l’aperçu utilise la miniature locale disponible ; turntable 3D et génération de miniatures
  appartiennent au pipeline de normalisation ;
- les références inverses d’assets et le budget par plateforme ne sont pas encore affichés ;
- l’interface est fonctionnelle mais ne constitue pas encore le gestionnaire complet de packs.

Les trois capacités passent au statut `Implemented` partiel. La prochaine tranche unique est
**G7-17 — localisation auteur** pour `ORF-CONT-011`.
