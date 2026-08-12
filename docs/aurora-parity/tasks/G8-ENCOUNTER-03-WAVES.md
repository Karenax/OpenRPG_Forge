# G8 ENCOUNTER-03 — Événements, vagues et simulation

`EncounterLifecycle` émet entrée, sortie avec durée et épuisement avec provenance. Les phases
ordonnées regroupent vagues, conditions d'entrée et renforts ; chaque vague porte délai, groupes et
politique de fin typée.

`EncounterWaveService.Simulate` exécute N compositions avec seeds consécutives reproductibles et
agrège composition, coût, durée, récompenses et variance. Les callbacks de durée et récompense
restent fournis par le ruleset au lieu d'imposer une formule historique.

Preuves : `EncounterWaveServiceTests`, suite RPG Kit et solution globale.

`R084 / FC-ENCOUNTER-P1-02` relie désormais les ancres manuelles transformées, les restrictions
d'activateur et l'autorité serveur, les trois bindings ForgeScript et le graphe UUID des phases et
vagues au document canonique. Le Studio partage la commande transactionnelle existante ;
ForgeScript/MCP lisent le même profil et le runtime matérialise quatre groupes de nœuds explicites.
`EncounterExhausted` transporte cause, kills, échappés, nombre de vagues et récompenses sans casser
les appelants historiques.

`R085 / FC-ENCOUNTER-P1-03` rend la prévisualisation statistique canonique et authorable : nombre
de runs, seed de base, estimateurs typés durée/récompense, coefficients, percentiles et overrides
sont validés et persistés. Le Studio prévisualise 64 runs reproductibles ; ForgeScript/MCP lisent
le même profil, l'adaptateur réutilise `EncounterWaveService.Simulate` et le runtime expose un nœud
explicite. Les variances couvrent nombre, coût, durée et récompense sans imposer de formule de
ruleset.

La prochaine tranche unique est **R086** selon `docs/planning/P1_ACCEPTANCE_PROGRAM.md`.
