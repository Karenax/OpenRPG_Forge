# P2 R139 — Réverbération de production

`ORF-AUD-015` conserve les zones spatiales, presets et transitions existants et ferme la limite
runtime : une impulsion munie de sa licence SPDX, provenance et empreinte SHA-256 produit un plan
de convolution exécuté par un fournisseur audio. Une capacité absente déclenche un fallback
algorithmique diagnostiqué. Les sends par source sont uniques, bornés et ordonnés par UUID.

Tests ciblés : `ReverbConvolutionPlanBuilderTests` (3/3). Preuve Toolset :
`OPENRPG_FORGE_TOOLSET_R139_REVERB_PRODUCTION_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
