# ADR-0033 — Profil qualité et performance persistant

Date : 5 août 2026
Statut : accepté

## Décision

Les tests réseau hostiles, fixtures de régression visuelle, benchmarks de zone, budgets par
plateforme et options de profilage forment un unique `QualityAssuranceProfile` versionné sous
`ModernRuntimeToolingProfile`. Le module, et non une configuration locale implicite, porte donc les
conditions de validation qui précèdent son playtest et son export.

`WorkspacePerformanceLabService` orchestre les services des couches Network et Application. Il
produit une empreinte stable indépendante des durées mesurées, tandis que les durées réelles de
navigation et les spans restent visibles dans les artefacts de revue. Creator et CLI peuvent écrire
ces artefacts atomiquement sous `.forge/quality`; runtime et MCP utilisent le même contrat en lecture
seule.

Le runtime possède en plus une suite visuelle explicite. Elle applique les résolutions, ancres caméra,
niveaux de qualité et tolérances persistés, puis produit pour chaque cas une référence, un candidat et
un rapport pixel. Cette suite est une preuve de stabilité de rendu reproductible, pas un jugement de
direction artistique.

## Conséquences

- les formats passent à projet `0.6.20` et workspace `1.14.0`, avec migration déterministe des
  profils absents ;
- les budgets couvrent frame, mémoire, textures, draw calls, lumières, script et réseau, et
  distinguent avertissement et blocage de build ;
- les traces partagent les surfaces éditeur, runtime et serveur et les catégories données, rendu,
  réseau, script et runtime ;
- la perte réseau reste simulée de façon déterministe ; un test Internet public n’est pas requis pour
  valider un module local ;
- les références visuelles générées par le gate ne remplacent pas une bibliothèque artistique
  approuvée et versionnée, qui pourra rejoindre le même contrat ultérieurement.
