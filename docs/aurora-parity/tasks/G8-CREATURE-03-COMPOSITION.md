# G8 CREATURE-03 — Composition visuelle et règles

La créature compose maintenant une espèce data-driven avec héritage contrôlé, traits, variantes et
squelette requis ; un profil visuel avec mesh, rig, morphs, body slots, matériaux, palettes,
attachments et preset ; et un graphe d'animation distinguant locomotion, posture de combat, emotes,
couches additives et retargeting.

L'état initial couvre vivant, inconscient, mort, endormi, assis et figé, plus boucle, pose et
invisibilité. Les attributs appartiennent au ruleset : bases, modificateurs sourcés et formules
inspectables `Sum`, `Product` ou `Ratio`, évaluées sans code arbitraire.

La validation retourne un diagnostic stable et localisé pour chacun des cinq composants.

Preuves : `CreatureCompositionServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 CREATURE-04 — défenses, capacités, inventaire, faction et perception**.
