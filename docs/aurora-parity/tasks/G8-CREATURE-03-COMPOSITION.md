# G8 CREATURE-03 — Composition visuelle et règles

La créature compose maintenant une espèce data-driven avec héritage contrôlé, traits, variantes et
squelette requis ; un profil visuel avec mesh, rig, morphs, body slots, matériaux, palettes,
attachments et preset ; et un graphe d'animation distinguant locomotion, posture de combat, emotes,
couches additives et retargeting.

L'état initial couvre vivant, inconscient, mort, endormi, assis et figé, plus boucle, pose et
invisibilité. Les attributs appartiennent au ruleset : bases, modificateurs sourcés et formules
inspectables `Sum`, `Product` ou `Ratio`, évaluées sans code arbitraire.

La validation retourne un diagnostic stable et localisé pour chacun des cinq composants.

R077 ferme la première moitié de cette tranche : espèce, apparence et AnimationGraph sont désormais
persistés dans le blueprint canonique, éditables dans un formulaire à sections, représentés par un
aperçu et une mini-carte simulable, lisibles par handle typé/MCP, adaptés au RPG Kit et matérialisés
en nœuds runtime. Les références de slots et attachments restent génériques ici ; leurs fonctions
P2 spécialisées ne sont pas ouvertes.

R078 ferme la seconde moitié : état initial/pose et AttributeSet appartiennent maintenant au même
blueprint canonique, avec UUID, validation localisée, formulaire transactionnel, aperçu calculé,
round-trip, handle ForgeScript, lecture MCP, adaptateur RPG Kit et nœuds runtime. Les six états,
boucle, pose, invisibilité, bases, modifiers et formules inspectables sont couverts sans règle D&D
figée ni script arbitraire.

Preuves : `CreatureCompositionServiceTests`, `CreatureAuthoringCompositionProfileTests`,
`CreatureAuthoringCompositionFoundationServiceTests`, `CreatureRulesCompositionProfileTests`,
`CreatureRulesCompositionFoundationServiceTests`, captures R077/R078 et smokes Toolset/runtime.

La prochaine tranche unique est **G8 CREATURE-04 — défenses, capacités, inventaire, faction et perception**.
