# P2 R132 — VFX, signatures et mises à jour delta

## Critères audités

| Fonction | Preuve de fermeture |
|---|---|
| `ORF-CONT-017` | Le descripteur canonique VFX est exécuté par un fournisseur abstrait couvrant particules, decals, lumières et cues audio. Culling, budgets particules/draw-calls, fournisseur absent optionnel/requis et capacités manquantes produisent des codes stables. |
| `ORF-CONT-028` | La signature ECDSA existante est complétée par une rotation distribuable : manifeste versionné, signature par une clé déjà fiable, chevauchement ancien/nouveau, révocation et aperçu sans clé privée. |
| `ORF-CONT-029` | Le delta content-addressed existant est complété par un transport de plages reprenable, retries bornés, ETag stable, reprise d’un fichier partiel et contrôle SHA-256 final. |

Tests ciblés : 2 tests VFX, 2 tests Network Content, build Toolset. Preuve intégrée :
`OPENRPG_FORGE_TOOLSET_R132_CONTENT_DELIVERY_PASS`.

## Non-objectifs

Les fournisseurs graphiques natifs, CDN et coffre de clés sont injectés par interface ; aucun secret,
endpoint ou shell arbitraire n’est embarqué. P3/P4, AV7–AV10, commit et push sont exclus.
