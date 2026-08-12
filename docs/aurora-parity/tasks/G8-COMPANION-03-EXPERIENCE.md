# G8 COMPANION-03 — Équipement, mort, cohésion et HUD

L'équipement permissionné conserve propriétaire, objets verrouillés, loadout suggéré, comparaison et
diff sémantique. R075 relie cette partie au document de monde, à l'interface transactionnelle, à la
persistance, à ForgeScript, au MCP et au runtime. Les six politiques de mort couvrent inconscience, mort permanente, cadavre
récupérable, résurrection, remplacement et mémorial avec états et effets typés.

Le système de cohésion choisit navigation, suivi de portail ou téléportation vers une ancre sûre ; il
interdit cette dernière en combat ou sous le regard des joueurs et produit un audit. Le HUD de groupe
configure portraits, santé, ressources, statuts, commandes rapides, réordonnancement, clavier,
manette, lecteur d'écran et contraste.

Preuves : `CompanionExperienceServiceTests`, `CompanionControlFoundationServiceTests`,
`CompanionExperienceFoundationServiceTests`, `P1-R075-COMPANION-CONTROL-ACCEPTANCE.md` et
`P1-R076-COMPANION-EXPERIENCE-ACCEPTANCE.md`.

La famille Companion P1 sélectionnée pour G8 est complète. La prochaine tranche unique est
**R077 / FC-CREATURE-P1-01**.
