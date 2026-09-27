# Références de design et de gameplay

## Système de design global

[`DESIGN_SYSTEME.md`](DESIGN_SYSTEME.md), identifié par
`openrpg-forge.design-system.fr.v0.1`, définit la charte commune de la marque, du Studio, du runtime,
du Creator, du lanceur, de la documentation et des contenus officiels. Il distingue explicitement
l’existant prouvé des cibles futures et préserve la liberté artistique des modules tiers.

La charte réconcilie l’atelier sombre bleu nuit/cyan de Gate 0 avec l’identité éditoriale lumineuse
parchemin/forêt/bronze du Manuel du joueur. Elle fixe aussi les règles de personnages réalistes,
d’extérieurs lumineux, d’accessibilité, de gouvernance des assets et de validation visuelle humaine.

Le ruleset de référence d’OpenRPG Forge est
[`OPENRPG_FORGE_RULESET_FR.md`](OPENRPG_FORGE_RULESET_FR.md), identifié par
`openrpg-forge.systemic-rpg.v1`.

## Manuel du joueur sans classe

[`OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md`](OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md) est la
proposition de manuel détaillé `openrpg-forge.player-handbook.fr.v0.1`. Elle développe le ruleset
parent en un système sans classe : mêmes 42 compétences pour tous, progression par usages
significatifs, difficulté d’apprentissage croissante, 210 acquis automatiques et 100 talents.

Le manuel est séparé en sources éditables sous [`player-handbook/parts`](player-handbook/parts), puis
assemblé et contrôlé par `scripts/Build-PlayerHandbook.py`. Les versions de lecture sont générées dans
`output/player-handbook/` au format DOCX et PDF. Les formules et catalogues de référence sont vérifiés
par `scripts/Test-PlayerHandbookRules.py`.

Cette proposition fournit une base de conception et un contrat d’implémentation ; elle ne déclare pas
les règles déjà présentes dans le runtime Rust. Toute adoption normative devra être versionnée et
livrée par les gates prévues.

## Provenance

- source : `Karenax/OpenRPG_Forge`, branche archivée `archive/main` ;
- snapshot : `a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0` ;
- chemin source : `docs/design/OPENRPG_FORGE_RULESET_FR.md` ;
- commit de création : `b23fa98e0233dd6318cb6de81e4db283f675b5c3` ;
- blob Git source : `f9fb40a9b4775d5f761d032a2e14b3a7f4086bea` ;
- SHA-256 : `7c5a3089d4013932fef9a6a22f38e3ca3165dea7e8590fc9273de1242802d37e` ;
- licence de la source historique : PolyForm Noncommercial 1.0.0, avec la notice requise
  `Copyright 2026 Karenax and OpenRPG Forge contributors` ;
- mode de reprise : copie documentaire intégrale, sans conversion du contenu.

## Portée sur la branche Rust

Le document est la spécification normative des besoins de gameplay. Sa présence ne signifie pas que
ces règles sont déjà implémentées, sérialisées ou chargées par le runtime Rust. La gate active est
indiquée dans [`PROJECT_CONTEXT.md`](../../PROJECT_CONTEXT.md) ; les contrats et preuves comportementales seront introduits par petites tranches à partir de
Gate 3, puis les profils de règles étendus à Gate 5.

La section 25 conserve les références historiques à la Crypte des Cendres. Le chemin Godot qu’elle
cite n’existe pas dans cette branche et les touches `Q/F/G/V/Tab` du texte divergent du manifeste
historique (`X/F/G/B/Y`). Ces éléments sont des traces de la vertical slice historique, pas un contrat
d’entrée du runtime Rust.

Le manifeste `apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json` n’a pas été importé :
il décrit une carte, des positions et cinq dispositifs propres à la Crypte, et non le ruleset générique.
Les profils historiques `forge.generic` et le code C#/Godot ne sont pas repris non plus.

Toute évolution normative doit conserver l’identifiant tant qu’elle reste compatible ou introduire une
nouvelle version explicite. Le moteur ne doit jamais coder un comportement spécial pour la Crypte ou
un autre slug de module.
