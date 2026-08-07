# G13-AH — Shards et hot reload serveur

Features : `ORF-MP-024`, `ORF-MP-025`.

Les shards déclarent zones, capacité et région. Le rechargement calcule une empreinte, valide la
signature, prépare en staging, draine, bascule atomiquement et conserve un rollback.

Preuves : `G13AH_ShardsAndHotReload_RequireScopeSignatureAndAtomicRollback`,
`ProductionNetworkTests` et marqueurs `G13AH_SHARD_HOT_RELOAD_PASS`.
