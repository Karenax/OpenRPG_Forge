# Tidewater — direction côtière utilisée par la référence

La texture de direction filtrée n'est pas une fonction manquante de la route
épinglée. Les acquisitions de l'eau, du ressac et des embruns ont `dirOn=0`.
`shoreDirAt` utilise alors directement `terrainShoreSample`, qui lit le champ
côtier. Ce champ est désormais recalculé pour les retouches et transmis aux
matériaux et simulations couplées.

Le code `ShoreWaves.buildDirTexture` contient bien une construction optionnelle
128², mais retourne sans l'activer lorsqu'il n'a pas de copie CPU du champ.
La route source acquise rencontre précisément cette condition : TerrainGPU
consomme l'upload et n'expose pas la copie attendue. Les exporteurs vérifient
explicitement ce comportement, sans injecter une référence CPU artificielle.

`E:/OpenRPG_Forge/.tidewater-integration/shore-direction-route-audit-001.json`
conserve les hashes des trois uniformes et des sources inspectées. Les valeurs
binaires au champ `dirOn` (offset 52, uniformes de 64 octets) sont toutes zéro.
L'adaptateur optique refuse maintenant explicitement une entrée qui activerait
la texture filtrée, plutôt que de la dessiner avec sa texture de substitution.
Clippy strict du prototype passe après ce contrôle de chargement.

Cette vérification remplace les mentions antérieures « directions filtrées
à raccorder » pour la route épinglée. Une variante qui active volontairement
cette optimisation serait une extension distincte, pas une condition de parité.
La parité globale, les autres modes de diagnostic, le village éditable,
l'horloge continue, Studio et WebGPU restent ouverts. Aucun commit ni publication.
