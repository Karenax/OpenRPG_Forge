# G8 ENCOUNTER-03 — Événements, vagues et simulation

`EncounterLifecycle` émet entrée, sortie avec durée et épuisement avec provenance. Les phases
ordonnées regroupent vagues, conditions d'entrée et renforts ; chaque vague porte délai, groupes et
politique de fin typée.

`EncounterWaveService.Simulate` exécute N compositions avec seeds consécutives reproductibles et
agrège composition, coût, durée, récompenses et variance. Les callbacks de durée et récompense
restent fournis par le ruleset au lieu d'imposer une formule historique.

Preuves : `EncounterWaveServiceTests`, suite RPG Kit et solution globale.

La famille Encounter sélectionnée pour G8 est complète. La prochaine tranche unique est
**G8 MERCHANT-01**.
