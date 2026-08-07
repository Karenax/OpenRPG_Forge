# G13-AI — Extensions serveur et protocole

Features : `ORF-MP-026`, `ORF-MP-035`.

Les extensions serveur exigent signature, isolation et capacités allowlistées. La négociation réseau
vérifie famille, plage de versions et fonctionnalités obligatoires sans modifier les règles métier.

Preuves : `G13AI_ServerExtensionsAndProtocol_AreSignedIsolatedAndCompatible`,
`ProductionNetworkTests` et marqueurs `G13AI_PROTOCOL_EXTENSIONS_PASS`.
