# G13-AL — Génération média et soak tests

Features : `ORF-AI-024`, `ORF-QA-027`.

La génération VFX/audio/image exige provider allowlisté, droits, provenance, staging, quota et
fallback manuel. Le soak test évalue durée, mémoire, handles, base de données, latence et joueurs
virtuels à partir d'échantillons reproductibles.

Preuves : `G13AL_MediaGenerationAndSoakTests_AreGovernedAndMeasurable`,
`DeterministicSoakTestService`, `PostLaunchRuntimeServiceTests` et marqueurs
`G13AL_MEDIA_SOAK_PASS`.
