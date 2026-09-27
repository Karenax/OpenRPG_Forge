# R1-040 — Contrat des profils effectifs, ADR-0011

2026-09-06, branche Rust, workspace existant modifié et non publié. Gate 1 ouverte.

## Accepté et implémenté

Le propriétaire accepte les profils explicitement dégradés sans valider Gate 1.
ADR-0011 fixe les substitutions et maintient les autres conditions de passage.
Les preuves ci-dessous portent sur les trois substitutions initialement implémentées ;
l'acceptation ultérieure de la quatrième limite est consignée en fin de rapport.
`forge_project::effective_quality` produit un plan moteur-neutre pour les sept domaines,
sans modifier la politique source, les recettes de carte ou les adapters existants.

Le plan versionné lie hash de politique demandée, niveau, hash des capacités, génération
d'adaptateur, politique de résolution, paramètres candidats, substitutions et blocages.
Le hash du plan est dérivé de son contenu canonique. `verify` recalcule le résultat depuis
la source, le niveau attendu et les capacités courantes : importer un JSON n'accorde aucune confiance.
La génération identifie une incarnation d'adaptateur ; elle n'est pas une mesure GPU.

Les capacités absentes de la déclaration sont `Unqualified`. Aucun fallback pour une
capacité inconnue ; seuls les trois cas `Unavailable` approuvés peuvent être substitués.
Le remplacement doit lui-même être disponible. Un plafond SSR absent est `null`, jamais
une distance zéro présentée comme satisfaite. Aucun remplacement implicite de SSR par
absence de réflexion. Les autres domaines conservent leurs paramètres demandés.

## Vérifié

Après correction d'un nom de champ erroné dans le test (`drop_count` → `drop_budget`),
les trois tests initiaux compilent et échouent sur le résolveur non implémenté.
Après implémentation, puis séparation du fichier de tests et ajout des cas de refus,
**cinq tests ciblés PASS** :

- strict sans substitution contre ADR-0011 avec trois substitutions explicites ;
- capacité inconnue et remplacement non qualifié bloquants ;
- round-trip et hash, altération de paramètres/génération refusée ;
- quatre niveaux déterministes, paramètres terrain/végétation/ombres/pluie/post conservés,
  capacités optiques non demandées sans blocage Low, niveau/politique inattendus refusés ;
- schéma/génération/identifiant invalides et valeurs JSON inconnues refusés.

Fixtures synthétiques déclarant leurs capacités : aucun `Supported` n'est une qualification
du matériel réel. Le plan produit 0/2/3/3 substitutions en Low/Medium/High/Ultra dans la fixture
où réfraction écran, volumétrie et plafond SSR sont indisponibles. Les sources restent inchangées.

## Validation transversale

Validation transversale terminée : `cargo test --workspace --all-features --locked` :
**469 PASS / 1 timing préexistant ignoré**. Clippy workspace/all-targets/all-features
avec `-D warnings` PASS. `cargo check -p forge_studio --target wasm32-unknown-unknown
--no-default-features --features web --locked` PASS. Formatage et diff check PASS.
Warning future-incompatibility connu `proc-macro-error2 2.0.1` conservé.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-quality-plan-20260906`.
SHA-256 :

```text
tests.log  6a38e4e748caae98c2fe8e1432e44f357fd313caddea4b15e6b9041005ea2d2b
clippy.log a192ec2eeb88f157fe67d7e90474d50c1207d3dce3fb8597b35d9af4788e91b6
wasm.log   758befe2c77a4b411b90ee782054f8ae0ecfe6d3c0a535ce4a1fa51f12a86aba
```

## Raccord restant

Le raccord diagnostique des capacités et exceptions terrain/forêt ainsi que l'affichage Studio
sont ajoutés ci-dessous. Restent l'installation des plans par les sept adapters,
la vérification de readiness GPU, l'acquittement séquence/hash/génération,
tests de timeout/perte de capacité et adaptation automatique. Le contrôleur existant n'est pas
modifié. Un plan sans blocage signifie seulement « résoluble selon cette déclaration ».
Ni `full_preset_applied`, ni un nouvel état « GPU prêt » ne sont ajoutés au plan.

Pas de nouvelle capture ou benchmark pour ce code de domaine sans raccord renderer.
CI distante NOT RUN ; aucune publication Git, aucun asset Cadle, aucune validation humaine déduite.

## Complément — observation moteur et présentation Studio

Le coordinateur expose `effective` dans ses statistiques et son JSON. Le cache est indexé
par source, niveau et capacités ; changements de source/capacités et disparition/restauration
invalident la preuve. La génération est locale à l'application : pas de garantie interprocessus.
Les contraintes de berges et le projecteur de forêt expérimental restent non qualifiés.
Images liées et paramètres CPU ne suffisent pas à qualifier les optiques. Aucun adapter
n'installe ce plan dans ce lot ; `gpu_ready` et `full_preset_applied` restent false.

Deux tests ajoutés : invalidation/restauration du cache ECS et présentation des plans,
y compris un plan synthétique sans blocage qui n'annonce jamais une application GPU.
**471 tests workspace PASS / 1 timing préexistant ignoré**. Clippy strict a d'abord refusé
un `Default::default()` dans le nouveau test de présentation ; nom du type explicité,
test ciblé et Clippy strict rejoués PASS. Check Studio WASM, build natif et build Trunk PASS.

Smoke natif High, WorldSketch canonique, sans kit ni brume expérimentale : 120 frames de
préchauffage, arrêt à 360 frames, sortie 0. JSON réellement relu : deux substitutions
(plafond SSR non garanti, volumétrie vers brouillard de distance), cinq blocages
(terrain, réflexion environnement, SSR, teinte, réfraction écran). La réfraction écran
reste demandée dans le candidat non résolu car son remplacement n'est pas qualifié.
Ce n'est ni un benchmark ni une preuve de profil installé.

Politique `561577b6b0d029fe7272307748b3acff80e2cd8e26c95b43c6090e5ddb9f203a`,
plan natif `1b55e16a3decd2e8c71eb6af049337ba678f3318ccc092d151c16224dd019705`.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-quality-observation-20260906`.
SHA-256 des fichiers relus :

```text
tests.log F8051A30FB77AA484C80C0E6F7BD1938E33FDF01314547E18649FA67D5474B45
clippy-recheck.log 78D665FC0670C87EC95229032E56D23C34DB2F4D046271828637D394B0424149
wasm.log AE7C24CBD72D47FA1CDC80707D10D5BD952E66F3F17625C3943EDC7CFAD3489A
native-build.log 9B386655A09CAFEDE078E7CB83CB5A5BAF96442A4C70A995BB820202B0ACE792
native-metrics.json A7D17F04A2B93D03A2605D7A309DF4399E58150BAB0FD9998DE0DC8AECC4BF86
native.jsonl 63A5054D82B60ED026F9851ADE169CC278560DB5A86AF941C9E270EB93B9B6CD
trunk.log 6B481B927DA1ED215F2A1C2766AFDD7F8E3DE0544740910F016BB2F9DB808E82
trunk-css-recheck.log 3A3C6EE88F2922AA28599DD714802C766DF7156A8024885C61595508A64677B1
```

Contrôle navigateur intégré sur le build local, URL
`http://127.0.0.1:1497/?forge_scene=world-sketch-visual-valley`, sans kit : ouverture du
panneau, texte réel identique aux deux substitutions et cinq blocages natifs. Premier clic
en délai dépassé pendant l'initialisation, puis réussi. Première capture : détail limité
à une colonne et output inline illisible. Correction CSS bornée (détail pleine largeur,
output bloc), reconstruction Trunk, rechargement et seconde capture inspectée : texte lisible,
sans chevauchement, avertissement candidat présent. Captures consultées dans la session,
pas de fichier PNG archivé. Monde initialisé une seule fois par chargement, hashes alignés.
Le viewport est visible, mais le statut de présentation GPU instrumenté reste non observé :
aucun verdict de readiness, de parité Tauri ou de qualité artistique n'en est déduit.
Assertions du JSON natif, `cargo fmt --check` et `git diff --check` PASS.

## Complément — garde-fou pur d'acquittement

`forge_world::quality_application` résout à nouveau le plan depuis les entrées validées ;
un candidat non résolu, une séquence nulle ou une échéance nulle ne peuvent démarrer.
L'identité lie séquence, hash complet du plan et génération. Les sept observations CPU
et la preuve GPU séparée doivent porter cette identité, avec une seule caméra active.
Les observations sont un snapshot courant, jamais une accumulation de succès historiques.

Une identité courante remplacée, une caméra ambiguë, un pipeline rejeté ou un temps
monotone régressant provoquent un échec terminal. Sans preuve complète avant l'échéance,
timeout : même une réponse complète exactement à l'échéance est trop tardive. Une réponse
tardive ne ressuscite pas la demande. Un succès déjà obtenu ne périme pas à l'échéance
d'installation, mais perd immédiatement son statut si une preuve disparaît au relevé suivant.
Aucune restauration du profil précédent n'est annoncée. Retry = nouvelle demande/séquence.

Contrat de l'appelant : horloge monotone, séquences uniques dans la session, changement
d'identité après perte de capacité/remplacement, observations réellement mesurées et
relevés réguliers même quand le GPU ne répond pas. Pas de garantie interprocessus ni de
confiance accordée à un JSON. Ce module ne mesure pas le GPU et n'est pas encore appelé
par le renderer ou le contrôleur adaptatif ; aucun profil n'est installé par ce lot.

Tests synthétiques : chaque domaine et le GPU avec mauvais hash/séquence/génération,
CPU seul, domaine absent, zéro/deux caméras, pipeline refusé, horloge régressante,
timeout et réponse tardive, perte de readiness, remplacement de source/adaptateur,
refus de capacité inconnue et paramètres de demande invalides. Résultats ci-dessous.

Validation du 2026-09-06 : quatre tests ciblés initiaux PASS, puis **475 tests workspace
PASS / 1 timing préexistant ignoré**. Le cinquième test ajouté après compilation de ce
passage est vérifié avec les quatre autres dans `targeted-final.log` : **5/5 PASS**.
Cela donne 476 tests distincts vérifiés, pas un passage workspace annoncé à 476.
Clippy strict workspace/all-targets/all-features et check Studio WASM PASS sur le code
final. Aucune nouvelle dépendance. Warning futur `proc-macro-error2 2.0.1` conservé.
Pas de lancement/capture : ce lot pur ne modifie aucun rendu ni interface.

Logs locaux : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-quality-application-guard-20260906`.
SHA-256 :

```text
tests.log 6DD324E9D6B00FBEE9D291F485614A89CAE9C66973FA5E1DA1EC1BDDE4BA0516
targeted-final.log 0825D736B9A5B48F698297B24E20C5FEEDFA988F3909FF7CAE68F4F8A26E1928
clippy.log BD09606FCCD815E1A87BBC2FAB42264A74A56CF26746AF88BDA50C8821682B12
wasm.log B575E6D9F7A71A0A3A72B4A4E6CC7717AA4820B6755015FF9DD91E3C714C3070
quality_application.rs CD9827E8EAB4FC21C47B5B9D3B3E68AB94492FE70DEC9EA27CA2D1EDAE53A8C4
quality_application/tests.rs A5A8E6101E05F234308682D7550BD0E77C318198EA6379398B3C43F735BA356B
```

La prochaine intégration doit produire les observations du renderer et consommer ce
garde-fou avant tout acquittement adaptatif. Les capacités actuellement non qualifiées
restent bloquantes : aucun `Supported` synthétique n'est promu en capacité native/Web.
Gate 1 ouverte, CI distante NOT RUN, ni publication ni approbation humaine déduite.

## Complément — capacité de teinte native raccordée

Après la preuve native à temps figé de `R1_WATER_ENVIRONMENT_IMAGE_EVIDENCE.md`, le mapping
de `SurfaceTint` peut être déclaré `Supported` côté natif. Conditions cumulatives : domaine
eau courant de même politique/niveau, uniforms du shader conformes au profil, teinte
observée appliquée, force finie dans [0,1], réfraction non désactivée et aucun override
manuel de désactivation. Un override d'activation ne vaut pas preuve GPU. Les refus
font évoluer la signature des capacités ; le cache du plan suit cette invalidation.

Cette déclaration décrit la capacité du mapping testé, pas une qualification de tous
les GPU ni une readiness de la frame courante. `gpu_ready` reste false. Le chemin Web
reste `Unqualified`, même avec des observations CPU correspondantes. Les autres blocages
optiques et terrain/forêt ne sont pas levés par cette modification. Aucun shader modifié.

Test synthétique de la frontière : natif/Web, domaine non correspondant, uniforms non
conformes, observation absente, désactivation manuelle, projection perdue, force absente,
NaN/hors bornes et réfraction désactivée. Validation et smoke natif ci-dessous.

**478 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict workspace,
check Studio WASM et build natif PASS. Warning futur `proc-macro-error2 2.0.1` conservé.
Smoke natif WorldSketch canonique High, sans kit/brume expérimentale, préchauffage 120
frames et sortie à 360 : sortie 0, aucune erreur runtime observée. JSON relu et assertions
PASS : trois substitutions, réfraction candidate `surface_tint` avec zéro pas écran,
plafond SSR null ; trois blocages restent : terrain, réflexion environnement et SSR.
Ni `full_preset_applied` ni `gpu_ready` ne deviennent true. Aucun verdict de performance.
Le mode Web n'est pas lancé ; son refus est testé par la frontière de plateforme et sa
compilation, pas par une nouvelle qualification navigateur.

Plan natif `45523281e58c3806f4044f40b9f0d98bae724b4d30b9100757e68cfdacb63d83`.
Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-native-tint-capability-20260906`.

```text
tests.log 5A8A4B5F9330C049BCDFB56CEAEE9F78B71DD6093EE442D69BECDA0E6D8A47D3
clippy.log 070EA99423D0C68EB3F727A9908544FDC5D35B0044A4BC84A77866CED17B704B
wasm.log 84485A4C978CCF453A44A2AA1C9BB1F8ED19C98AA1076852C8AB458870B87D43
build.log A99C287EFC80C5522F440E6920416A19721B1EB553C25797FC3A4B7A1AD0729B
native-metrics.json 364A914E31413A1384277970BC02597F474BB9D60D8EF4DBC530AD8B0991DA64
runtime.jsonl 41B6B6C103306C9E8311B84F6F2CE3A51DAF8F96AE1E845F7A08B837EB292D77
```

Format/diff check PASS. Gate 1 ouverte, aucun changement de carte, aucun commit/push.

### Réserve avant raccord IBL/SSR

Inspection suivante : la présence des reflets natifs est établie, mais l'intensité
authorée 0,72 reste explicitement non mappée dans ce même diagnostic. La réflectance
fixe 0,36 n'est pas un gain d'intensité équivalent. Les capacités IBL/SSR restent donc
non qualifiées ; leur promotion ne peut découler des seules captures d'activation.
Voir « Limite supplémentaire — intensité des reflets » de
`docs/planning/QUALITY_FALLBACK_CONTRACT_PROPOSAL.md`. Une quatrième limite de fallback
est proposée mais non approuvée ; aucun changement de code ou nouveau test dans ce constat.

## Complément indépendant — raccord garde-fou/contrôleur

Sans étendre ADR-0011 à l'intensité, le contrôleur pur est relié au garde-fou d'application.
`observe_application` vérifie le hash de politique, la destination et la séquence de sa
proposition en attente, puis lie le hash/génération exacts du plan au premier relevé.
Un garde-fou étranger est refusé sans le consommer ni altérer la proposition courante.
Les sept observations CPU sans GPU restent en attente ; seule la readiness du garde-fou
acquitte le changement et lance le cooldown. Un second relevé Ready ne relance pas ce délai.

Le contrôleur expose séparément le dernier niveau connu et l'identité effective confirmée.
Cette identité est absente pendant une nouvelle transition et après échec. Quand aucune
transition n'est en cours, le même garde-fou peut réévaluer la preuve installée : perte
GPU/identité invalide révoque le succès et arrête les propositions futures. Un timeout
arrête également le contrôleur ; une réponse tardive ne le réactive pas. Aucune restauration
présumée. Reprise par reconstruction depuis une installation fraîchement observée et des
identités de session non réutilisées. L'ancien acquittement par niveau est refusé pour
une demande déjà liée au garde-fou ; la voie historique conserve son contrat d'appelant.

Cinq tests ajoutés : CPU/Pending puis Ready, doublon et cooldown, nouvelle proposition
contre ancien garde-fou, mauvaise politique/destination/séquence, timeout avec 1000 frames
sans reprise et réponse tardive, perte de preuve installée, benchmark refusant une
transition non sollicitée. Capacités/observations synthétiques : aucun profil réel qualifié.
Les temps et preuves restent fournis par l'appelant, pas mesurés dans le domaine.
L'intégration renderer et l'adaptation runtime restent désactivées. Aucun effet visuel modifié.

Validation : 17 tests ciblés du groupe qualité PASS avant la protection contre la voie
historique, puis **484 tests workspace PASS / 1 timing préexistant ignoré** sur le code
final, incluant cette protection. Clippy strict workspace/all-targets/all-features et
check Studio WASM PASS ; format/diff check PASS. Warning futur `proc-macro-error2 2.0.1`
conservé. Aucun lancement GPU nécessaire pour ce lot pur non raccordé au rendu.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-controller-20260906`.

```text
targeted.log 3F880421A836C4A8C55F1ECB3D7B3093414D53BEA371311476120A3D794D55C6
tests.log 341894428D5387A80EBC4F859ACB1FA0889275692B0E05059BA6E362DAA2BF5A
clippy.log 19A86141016734C72BDCCF98753540C8EE87A2FCEED6AFA70DB41107D6BEC582
wasm.log 617487FA027866F8E1032D62F61A1CA6D6DEE68797305900D890D443971D54D7
```

Gate 1 ouverte, CI distante NOT RUN, aucune quatrième substitution approuvée ou appliquée,
aucun commit/push. Le choix sur l'intensité reste en attente ; ce lot est indépendant.

### Correction — propriété de l'état d'application

Deux régressions reproduites avant correction (`red.log`, deux tests FAILED) : un garde-fou
recréé avec même identité et échéance repoussée acceptait une réponse après le délai initial ;
un garde-fou recréé après Ready renvoyait Pending sur perte GPU, laissant l'identité confirmée
dans le contrôleur. Le contrôleur possède désormais une copie du premier garde-fou accepté
pour chaque demande. Les arguments ultérieurs sont des références d'identité en lecture seule,
pas une nouvelle horloge ni un nouvel historique. Une seule copie à la liaison, pas par frame.
L'état est libéré au refus/échec et remplacé uniquement pour une nouvelle demande liée.

Après correction : 19 tests ciblés qualité PASS, dont échéance initiale maintenue, horloge
régressante refusée malgré reconstruction et perte de readiness révoquée. Le test de
mauvaise identité conserve son garde-fou extérieur intact. Aucun renderer, shader ou document
authoré changé. Validation transversale ci-dessous ; quatrième limite toujours non approuvée.

**486 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict workspace et
check Studio WASM PASS, format/diff check PASS. Warning futur connu conservé. Aucun
lancement GPU pour ce changement de propriété d'état non raccordé au renderer.
Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-controller-guard-ownership-20260906`.

```text
red.log 74639C3DD3DE4BEBEE84CD0556C6A8064FED1EA0A6BF2EE05D5DE6C9F037C250
green.log 5EFC01A72923447C57F472E40944044896D536503CC0EE4FDB3811C0A2E1DDB8
tests.log F77E98CF5DAC4DFF8070EF8ABA514955D8D3F455AA8252DA968FBFE9103C806F
clippy.log D8BBC671D5A74ADD6C4AEC200C7E1E3FCA96B30A64B74436FE34B08739D7D595
wasm.log 3F4DB260D631BF1463328CE7C9CFBE68B9DEDBB7A8C74EE838159A22E278E743
```

Gate 1 ouverte ; adaptation runtime désactivée, aucune publication.

## Décision ultérieure — quatrième limite acceptée (2026-09-06)

Réponse explicite « oui » du propriétaire : intensité authorée des reflets conservée dans
la source mais non appliquée dans les profils dégradés, avec divergence clairement affichée.
Acceptation consignée dans ADR-0011, le contexte canonique, la proposition et la roadmap.
Les mentions antérieures « non approuvée » retracent l'état au moment des tests précédents.

**Accepté :** extension contractuelle. **À implémenter :** représentation de cette quatrième
limite dans le plan effectif et les diagnostics. **Vérifié dans ce lot :** cohérence documentaire
et contrôle de whitespace Git uniquement ; aucun nouveau test Rust, build ou lancement GPU.
Les preuves précédentes ne qualifient pas cette extension. IBL/SSR non promus, adaptation
runtime désactivée, Gate 1 ouverte et validations humaines toujours requises.
Objectif en pause non repris automatiquement ; aucun commit/push.

## Reprise — intensité des reflets explicitement non appliquée (2026-09-06)

L'objectif est redevenu actif. La quatrième limite acceptée est maintenant représentée
par `QualityCapability::ReflectionIntensity` et
`QualitySubstitution::ReflectionIntensityNotApplied`. Le gain authoré et la réflectance
fixe restent deux grandeurs distinctes ; aucune valeur de source, shader ou asset modifié
dans ce lot. La substitution fait partie du JSON/hash du plan, pas d'un simple message.
Elle ne s'applique que lorsque les reflets sont demandés, que le gain est explicitement
indisponible et que la politique est ADR-0011. En mode strict, ou pour une capacité omise/
non qualifiée, la résolution reste bloquée. Les chemins IBL/SSR restent indépendamment
non qualifiés : déclarer cette limite ne les promeut pas.

L'adaptateur déclare l'absence de gain authoré sur ses deux backends ; le formateur Studio
expose « intensité authorée des reflets conservée mais non appliquée ; réponse physique
actuelle ». Test du texte et observation ECS synthétique vérifiés, sans prétendre à une
session visuelle. Les fixtures du garde-fou déclarent explicitement leur capacité de gain
synthétique ; aucune capacité omise n'est implicitement supportée.

Validation : six tests ciblés du résolveur PASS, puis **487 tests workspace PASS / 1 timing
préexistant ignoré**. Premier Clippy en échec sur la longueur du résolveur (104/100), trace
conservée ; validation d'identité extraite sans changement de règle, puis Clippy strict
workspace/all-targets/all-features PASS et les six tests du résolveur rejoués PASS.
La suite workspace précède cette extraction ; ne pas la présenter comme rejouée après.
Check Studio WASM avec `--no-default-features --features web` PASS sur l'état final,
ainsi que `cargo fmt --all -- --check` et `git diff --check`. Première invocation WASM
sans sélection du frontend Web interrompue explicitement (session terminée) ; `wasm.log`
reste une trace incomplète, pas une preuve PASS. La preuve retenue est `wasm-web.log`.
Warning futur connu `proc-macro-error2 2.0.1` conservé.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-reflection-gain-contract-20260906`.

```text
tests.log C1F36C4DE0BDB4F94BC19EDA127D3577DC796647F8989EEE60E357F2F85E2510
clippy.log 41D7D9A19BCA681ADFF08DE66C44A33234E3E2758417754E9C4D18279E76630B
clippy-final.log DA584957ECFBAFF8A0FB5C56484FCF0A341229B4CE3B6F60C42FD1D0AAED3B51
targeted-final.log C339E1202117C4FC774AE6A96CECCE85716DEA3710A49785CA435EB5B123D3E3
wasm-web.log 0DCE8CE21608E536FC947FC200EA27ADCFFBE4C07768EF98DBE811650BFEA237
```

Le plan reste candidat, sans application ni readiness GPU. Le gain numérique réside dans
le document eau, pas dans le profil de budgets : cette déclaration d'indisponibilité ne
prouve pas une liaison de tous les documents de domaine au futur acquittement. Cette liaison,
l'installation effective et la qualification native/Web restent nécessaires. Adaptation
runtime désactivée ; Gate 1 ouverte, CI distante NOT RUN, validations humaines requises,
aucun commit/push.

## Raccord suivant — chemins de réflexion natifs (2026-09-06)

Les captures d'isolation natives IBL et SSR déjà inspectées établissent la contribution
de ces chemins, pas une qualification artistique ou de tous les budgets. Leurs JSONL
sont relus par empreinte avant ce raccord :
`r1-water-ibl-cycle-20260906-b/runtime.jsonl` =
`005B07D2C9B8C86BF91717BD43D47CFD87EAA4A66D6A9E04D89AC13330E7BBE0` ;
`r1-ssr-isolation-20260906/runtime.jsonl` =
`B1A46735DEC96D680102B0DCBD466D399996BD6DF20E4EA5909BE5044D885B0D`.

L'adaptateur peut désormais déclarer le support du chemin IBL natif lorsque l'observation
courante correspond au domaine eau, aux uniforms et au matériau, avec reflets demandés,
textures présentes, réflectance positive et intensité IBL finie/positive. Cette intensité
de caméra est nouvellement exposée ; elle ne remplace jamais le gain authoré du matériau.
Une caméra ambiguë ne produit pas cette observation. Le SSR exige en plus la demande
ScreenSpace, l'absence de désactivation manuelle et le budget de pas effectivement projeté.
Les commandes manuelles restent prioritaires. Le Web reste explicitement non qualifié.

`Supported` porte ici sur le raccord du chemin/budget, pas sur la readiness GPU de la frame,
la qualité de ses pixels, la stabilité en mouvement ou les performances cibles. Le gain
authoré non appliqué et le plafond métrique absent restent deux substitutions distinctes.
L'adaptation reste désactivée et le plan reste candidat jusqu'à son installation vérifiée.

Validation du raccord : **194 tests forge_engine PASS / 1 timing préexistant ignoré**,
Clippy moteur tous targets/features strict PASS, check Studio WASM
`--no-default-features --features web` PASS, build natif PASS, fmt/diff PASS.
Deux écarts de forme Clippy initiaux corrigés (point-virgule et longueur d'un test),
sans désactivation des règles. Pas de nouveau passage complet workspace dans ce lot.

Smoke natif canonique, sans kit ni forêt prototypes, `--world-sketch-visual-valley
--quality high --metrics-warmup-frames 120 --exit-after-frames 360 --metrics <sortie>` :
sortie 0, zéro ERROR, `runtime_errors_observed=false`. Assertions lues dans le JSON final :
IBL 1, SSR 48, quatre substitutions présentes, seul blocker `terrain`, readiness GPU et
preset complet false. Hash du plan
`a06b7e58175b55a531602f32be75a82307d142fda76e66d50e48b2507ae8a875` ;
source qualité inchangée `561577b6b0d029fe7272307748b3acff80e2cd8e26c95b43c6090e5ddb9f203a`.
Ce smoke vérifie la télémétrie raccordée, pas un nouveau verdict visuel ou benchmark.
Le blocage terrain reflète la conservation des berges ; les limites des autres scènes,
notamment forêt prototypes, ne sont pas levées par ce passage.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-native-reflection-capabilities-20260906`.

```text
tests.log 7A52326B32537E3F4BA5518FDA8345D0FA2DE39B83781BCA455D2B303B5D8EC3
clippy.log 719440D3C7B7E4239AA796F02ED399E0E18A29677F08E293CFFBF631DC1B84D3
wasm.log C49222EB19DE217B45B4C6D4CCC5CF31B53462E27C048E7472E88298AB821201
build.log D04699D14AAD025898E813C60F21F241D83C2F6223DE9FB15049EEADF6BC0644
native-metrics.json F5AC1B473A4278F19F92CF7F028FEE71E193C399B8FBEE90D1425C9E36879C86
runtime.jsonl 673072736D4467DC875F7162EBE3F58A69CECCD0A9B5ED58F109EA076F8DA442
```

Gate 1 ouverte, CI distante NOT RUN, validations humaines requises. Aucun shader ou asset
changé, aucun commit/push. Prochaine dépendance technique : représenter honnêtement les
contraintes terrain/forêt dans le plan avant son installation et son acquittement GPU.

## Inventaire terrain lié au plan (2026-09-06)

`TerrainQualityConstraints` ajoute une description moteur-neutre des contraintes
effectivement dérivées : hash terrain, nombre de chunks canoniques et IDs stables triés
des chunks maintenus à pleine résolution (stride 1 sur les trois LOD). Les listes vides,
non strictement triées/dupliquées et les décomptes incohérents sont refusés. L'inventaire
est optionnel pour les anciens objets, mais ne constitue jamais une permission d'augmenter
le budget. Sa présence maintient explicitement le blocker Terrain, y compris si la table
de capacités annonçait Supported : aucune cinquième substitution autorisée implicitement.

L'adaptateur produit cet inventaire depuis son cache de projection validé, sans refaire
le calcul spatial à chaque frame. Il ne transmet pas l'ancien inventaire si la projection
n'est plus au niveau demandé. Source terrain et liste participent à la signature des
capacités, au hash du plan et à la génération du cache. Deux ensembles de même taille mais
d'IDs différents ne réutilisent donc pas le même plan ; une retouche de source non plus.
Il s'agit d'identités persistantes Forge, pas d'entités Bevy sérialisées. L'appartenance
géométrique des IDs est établie par l'adaptateur ; le résolveur pur valide la structure,
il ne reconstruit pas la géométrie à partir de cet inventaire.

Le formateur Studio indique le nombre de chunks sur le total, leur conservation à pleine
résolution pour les berges et le budget terrain non validé. Aucun changement des meshes,
de la protection des berges, des collisions ou des sources authorées dans ce lot.
Le profil demandé reste distinct de cet inventaire dérivé bloquant.

Validation finale : **489 tests workspace PASS / 1 timing préexistant ignoré**, Clippy
workspace tous targets/features strict PASS, check Studio Web/WASM PASS, fmt/diff PASS.
Tests du résolveur : structure, doublons, hash/round-trip et changement de liste refusant
un ancien plan. Test de cache : changement d'IDs à décompte égal, changement de hash terrain,
projection périmée retirée. Test ECS existant de berge traversant une frontière : inventaire
de deux IDs triés et empreinte source constatés après récupération. Texte Studio testé.
Pas de nouveau lancement GPU ou de capture dans ce lot de description/identité ; le dernier
smoke natif précède ces champs supplémentaires et ne prouve pas leur émission en session.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-constraint-identity-20260906`.

```text
tests.log 776FFCEEC77563F62CF34332B280A3EA83F786C6DF4500CB00C260C3DE2FAF21
clippy.log B41CBF4CBBAA9050893DFC8BA2DCA262DAEEF88FE86725E1D2D293B58A9D3048
wasm.log A4566094EAA11FD694DD616F92D7DFF08C9F9900EF9BFF331CC8A7B8A22CED1D
```

Warning futur connu conservé. Gate 1 ouverte, CI distante NOT RUN, adaptation désactivée,
aucune publication. L'inventaire rend l'écart explicite ; il ne résout pas le budget terrain,
la forêt prototypes, l'installation effective ni l'acquittement GPU.

## Vérification native de l'inventaire terrain (2026-09-06)

Build du runtime actuel PASS, puis smoke canonique High, warmup 120 / sortie 360 frames,
sans kit ni forêt prototypes : sortie 0, zéro ERROR, `runtime_errors_observed=false`.
L'inventaire du plan est identique à celui de la projection : 20 IDs uniques sur 64 chunks
(31,25 %), source terrain
`18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.
Le cache contient 192 meshes dérivés, 149789 sommets et 274416 triangles ; ces totaux
cumulent les variantes LOD, ce ne sont ni des triangles effectivement dessinés par frame
ni une mesure de surcoût GPU. Aucun benchmark ou nouveau verdict artistique revendiqué.

Hash du plan `07c249255a5a85704ae33ba4405fabc9a2fd5f1e9174eaa3b2c43cf49a8dfd94`.
Les quatre substitutions optiques restent déclarées ; seul blocker de ce plan `terrain`,
readiness GPU et preset complet false. La réserve précédente sur l'émission de l'inventaire
en session native est levée, pas celles d'installation, de Web ou de qualification finale.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-constraint-native-20260906`.

```text
build.log 9FE305AE0491CA2DAED1C64C62FB5EE5919C89C34B170B7D6278B5DA403B5C0B
native-metrics.json 110405C34B2BD945CBF298D5D2650CB4FC4E8669A4993A830011CAFEF2681DC7
runtime.jsonl 03ADF7C4F1901B253D2780B18DA9D6704C0E3F3AB10F6858A9629FE3AB461FD7
```

Décision demandée séparément : accepter cette protection des berges comme écart explicite
au budget LOD du profil effectif, sans valider Gate 1 ni les performances cibles. Ne pas
interpréter les quatre dérogations optiques comme cet accord. À défaut, le blocage terrain
reste conservé ; aucun shader, budget authoré ou mécanisme de protection modifié ici.

## Contrôle Studio Web indépendant de l'accord terrain (2026-09-06)

Build Trunk debug actuel PASS dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-terrain-web-20260906/dist`, servi
temporairement sur loopback 1497. Ouverture dans le navigateur intégré de
`/?forge_scene=world-sketch-visual-valley`, puis dépliage réel de « Profil effectif et limites ».
Arbre d'accessibilité, ledger DOM en lecture seule et capture du panneau inspectés.
Le texte se replie dans l'inspecteur étroit et se parcourt verticalement ; aucune retouche
CSS ni nouvelle campagne artistique effectuée. La capture est dans la trace de l'outil,
pas une nouvelle capture normalisée versionnée de Gate 1.

Observation High actuelle : trois substitutions (intensité non appliquée, distance SSR non
garantie, brouillard de distance) et cinq blockers (terrain, environnement reflection,
screen reflection, surface tint, screen refraction). La teinte de remplacement n'étant pas
qualifiée Web, la réfraction reste bloquée au lieu de revendiquer le fallback natif.
Le panneau affiche « 20 / 64 chunks conservés à pleine résolution aux trois LOD pour
préserver les berges ; budget terrain non validé » et « Plan candidat uniquement ·
application complète et rendu GPU non confirmés ».

Ledger : `full_preset_applied=false`, `effective.gpu_ready=false`,
`status=unresolved_capabilities`, génération 2 ; plan
`66cd7e5222ebcb4f123c43d056be80a4eac31ae65340576f1d3559159652ff8b`.
Source qualité `561577b6b0d029fe7272307748b3acff80e2cd8e26c95b43c6090e5ddb9f203a`,
terrain `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`,
20 IDs présents. Monde initialisé une fois, présentation GPU instrumentée non observée.
Ce contrôle qualifie l'émission et l'affichage des diagnostics Web, pas les effets optiques,
le benchmark, Tauri ou Gate 1. Aucun nouveau test Rust dans ce lot ; les 489 tests sont
ceux du lot précédent. L'accord sur l'écart terrain reste attendu, sans assentiment implicite.

SHA-256 `build.log` : `516238666C4DF677806D83CD18B78E4E9B7FEF42EABA2C6677987CD72B57ACC1`.
SHA-256 `dist/forge_studio_bg.wasm` :
`3F2F0BBB45BB97ACD00B9DC5A91B9DADD83106B6F9315134F1C787B61DBD147C`.
Onglet de preuve fermé et serveur appartenant à cette tâche arrêté (session terminée).
Aucun onglet utilisateur fermé, aucun commit/push, aucune copie d'asset Cadle.

## Accord distinct reçu — protection des berges (2026-09-06)

Le propriétaire répond « ok » après la demande d'acceptation de l'écart terrain.
ADR-0011, proposition, contexte canonique et roadmap mis à jour. La conservation à pleine
résolution des chunks géométriquement concernés est acceptée dans le profil effectif ;
20/64 reste le résultat de la scène de preuve, pas une constante autorisant un traitement
spécifique à cette carte. Distances, reste du budget et sources demeurent inchangés.

**Accepté :** compromis terrain explicite. **Déjà vérifié :** inventaire natif et affichage
Web décrits ci-dessus. **Encore à implémenter/vérifier :** consommation de cet accord par
le résolveur et l'adaptateur ; le code conserve pour l'instant son blocker Terrain.
Ce lot consigne seulement la décision : pas de nouveau test Rust, build ou lancement GPU.
Gate 1 ouverte, performances non validées, CI distante NOT RUN, aucun commit/push.

## Raccord de l'exception terrain acceptée (2026-09-06)

`TerrainBanksFullResolution` déclare explicitement dans le plan/hash la conservation
du stride 1 des chunks de berges inventoriés. Le résolveur l'autorise sous ADR-0011 lorsque
la capacité de projection terrain est Supported ; strict, Unavailable et Unqualified
restent bloquants. Un inventaire mal formé reste refusé par validation avant résolution.
Les budgets authorés et distances ne changent pas ; l'inventaire porte les exceptions
localisées. Aucun nombre de chunks ou identifiant propre à Visual Valley codé en dur.

L'adaptateur vérifie la correspondance du hash terrain, du nombre canonique de chunks,
du nombre de chunks protégés et des niveaux demandé/projeté. Un inventaire absent,
périmé ou incohérent ne lève pas le blocage. Les contrôles de policy et de projection
globale restent nécessaires. Le formateur Studio expose « écart au budget LOD accepté » ;
le budget strict n'est toujours pas revendiqué comme validé.

Tests ajoutés : matrice politique/capacité du résolveur et refus des incohérences
d'inventaire dans l'adaptateur. Aucun shader, mesh, collider, source authorée ou asset
modifié par ce raccord ; il décrit la règle géométrique existante et désormais acceptée.

Validation finale : **491 tests workspace PASS / 1 timing préexistant ignoré**, Clippy
strict workspace tous targets/features PASS, check Studio Web/WASM PASS et build natif
PASS. Smoke canonique High (sans kit ni forêt prototypes), warmup 120 / sortie 360 frames :
sortie 0, zéro ERROR, `runtime_errors_observed=false`. Assertions JSON : cinq substitutions
dont `terrain_banks_full_resolution`, zéro blocker, 20 IDs protégés sur 64 chunks,
`status=resolved_plan_not_applied`, `gpu_ready=false`, `full_preset_applied=false`.
Hash du plan : `15b8a2f8e9f12a378d2f7c90abdca201dde9bf9e0b4914282eb107b49f858268`.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-approved-bank-resolution-20260906`.

```text
tests.log 2C525C98B440845652F0B8FCF2ECB88C695FE7951B59AC480818A13B2C2A5F02
clippy.log D2746184D0A06F2DBB24AC3BDAE9BAA269F2C1B124BCAFCA92191A6C6D4320FC
wasm.log A3A1F8C2D3278DCD9D689D2FFD4334F2B5403CD8240A69CAAF77F6387D9644C6
build.log 6A24E16C072AB4BF20C7996CF0F8E114F854A415F1F1D48675A456BE17BE3CCD
native-metrics.json 5F0B825E23F65D8B8AA560BD4EC04B0FC237D98A79F81EEA61353670237954D8
runtime.jsonl AD8A316F3C67BCD780D479AF71E4CD583057DAC0D8F14BD85E92B6AA2E3B3CF5
```

Ce résultat établit la résolution du plan candidat de cette scène native, pas son
installation, son acquittement GPU, la forêt prototypes ou la qualification Web des effets.
Pas de nouvelle capture artistique ou benchmark. L'adaptation runtime reste désactivée.
Gate 1 ouverte, performances cibles et validations humaines non acquises, CI distante
NOT RUN. Aucun commit/push ; toutes les sessions de validation de ce lot sont terminées.

## Liaison des documents source avant application (2026-09-06)

Le plan et la déclaration de capacités incluent désormais `QualitySourceBindings` : cinq
hashes typés terrain/végétation/environnement/eau/météo, en plus du profil qualité commun.
Les ombres et le post-traitement utilisent leurs budgets issus de ce profil ; le document
météo porte les valeurs authorées correspondantes. Le hash terrain doit correspondre à
l'inventaire de berges lorsqu'il est présent. Aucun document authoré n'est réécrit.

L'adaptateur relève les hashes des projections et refuse de lier un ensemble dont l'eau
ou la végétation se réfère à un autre terrain. Ressource absente ou hash non lisible :
liaison absente, jamais une chaîne vide assimilée à une identité. La signature du cache
inclut ces liaisons ; les changements reconstruisent le plan et sa génération. Un plan
résolu sans liaison est signalé `unbound_source_documents`, pas prêt à appliquer.

Le garde-fou pur refuse explicitement `UnboundSources`. Les anciennes données optionnelles
restent lisibles pour inspection, pas utilisables pour acquittement sans reconstitution
depuis les sources actuelles. Tests : chaque empreinte change le hash et invalide le plan
importé précédent ; terrain de contraintes divergent refusé ; garde-fou non lié refusé ;
ancienne identité invalidée après modification de la source eau malgré des observations
synthétiques complètes portant la nouvelle identité.

Ces liaisons couvrent les documents de ces domaines, pas une preuve de tous les assets,
des mutations transitoires, de l'upload GPU ou de l'installation. L'adaptation runtime reste
désactivée et le raccord renderer au garde-fou reste à réaliser.

Validation : **493 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict
workspace tous targets/features, check Studio Web/WASM, build natif et fmt/diff PASS.
Smoke High canonique (warmup 120 / sortie 360 frames), sans kit/prototypes : sortie 0,
zéro ERROR, `runtime_errors_observed=false`. Les cinq hashes du plan correspondent aux
champs des projections chargées. Cinq substitutions, zéro blocker, statut
`resolved_plan_not_applied`, readiness GPU et preset complet false.
Hash du plan `ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-quality-source-bindings-20260906`.

```text
tests.log 589B4B3CD0CA82A06A72C52FB54F328FC5E035B7025D241DC8764620B8406516
clippy.log 2E9CDDD7F55D7FEEC9B123502BAC0394E15EF16F0F3BB3FD3A818B24B5B3577D
wasm.log D3EC8B5FF4336C8E4938D4B3D4525E295A0AAE2FCF4258C4CBAC777F3985EFD3
build.log 6E3E068F6B00669B77AC7E023CAF4F3927295673AAED9419EC5749695C1693EA
native-metrics.json 13187DBA32421530CFF15A240878FB9CF35B642F478954AF90DE6CEF2A4CF01E
runtime.jsonl 60DB8D84EA7795188E0951DA6A5704F070DABBB7E7128A81691655F901508DE3
```

Pas de nouveau verdict artistique/WebGPU/Tauri ni de benchmark. Warning futur connu
conservé, toutes les sessions de ce lot terminées. Gate 1 ouverte ; CI distante NOT RUN,
aucun commit/push. L'installation réelle reste le prochain raccord, pas une capacité livrée
par les seules liaisons source.

## Points d'entrée d'installation optique (2026-09-06)

Implémenté : `WaterQualityRequest::request_effective` et
`EnvironmentQualityRequest::request_effective` consomment un plan validé par le garde-fou.
Le prêt `installation_plan` exige Pending, identité courante exacte, mêmes cinq liaisons
source, temps non antérieur à la dernière observation et strictement avant la deadline.
Un garde-fou terminé ou en échec ne réinstalle pas un plan. Chaque adaptateur contrôle aussi
son hash de politique et le niveau demandé. Un refus ou une requête ordinaire de niveau
efface l'override précédent ; le document source n'est jamais modifié.

Les systèmes eau/environnement utilisent ces paramètres transitoires. L'eau conserve la
priorité des aperçus manuels et représente le plafond SSR absent par `None`, pas un rayon
garanti de longueur zéro. Les observations distinguent paramètres authorés/demandés,
paramètres destinés au renderer et identité reçue. Cette identité est une réception,
pas un acquittement CPU ou GPU. Le brouillard de distance authoré reste intact.

Vérifié par tests synthétiques avec vrais composants/matériaux ECS, sans GPU : réglages
SSR, teinte/réflectance désactivées puis restaurées, brouillard de distance conservé,
hashes et documents inchangés, refus des sources/identités/niveaux/délais périmés,
refus après échec/Ready, retour à un preset ordinaire sans ancien override.

Portée restante : le coordinateur global n'appelle pas encore ces points d'entrée. Le
caller reste responsable des snapshots frais, de la génération de capacités et de
l'invalidation après installation. Aucun fan-out effectif aux sept domaines, acquittement
GPU, adaptation automatique ou preuve visuelle nouvelle n'est acquis par ce lot.
Le runtime normal conserve donc `resolved_plan_not_applied`. Gate 1 ouverte ; CI distante
NOT RUN ; aucun asset Cadle, commit ou push.

Validation : **496 tests workspace PASS / 1 timing préexistant ignoré**. Clippy strict
workspace tous targets/features, check Studio Web/WASM et fmt/diff PASS. Le premier
Clippy a refusé un `drop` inutile dans le nouveau test ; remplacé par une portée lexicale,
puis Clippy et le test eau concerné ont été rejoués avec succès. Aucun test désactivé.
Warning futur connu `proc-macro-error2 2.0.1` conservé. Pas de lancement graphique ou
benchmark pour ces points d'entrée non encore activés dans le coordinateur.
Toutes les sessions de contrôle de ce lot sont terminées.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-optical-installation-20260906`.

```text
tests.log 020D99350DFEF8FBFEFCC4EF0586F6BAE179D6A347D62DEF78DBCDE51805E54B
clippy.log 55427E434241D06A88929639EB8DE99A466C7AAB0197658637D421E7BE09248C
clippy-fixed.log 0C3D363258B49B9CB8B14C7FB07808CE1F56AFEC7E043FD4DC74021A8AA666A3
wasm.log E7FA63C4740027157A1E82168E5D599534BE230C21B8AC0E00B7B7A4CBA00506
test-scope-fixed.log 2AE321855251132F20EFE5FA31CA4CECD36D8F6831CF062C7E71C1994EC58DA2
```

## Coordinateur d'application et observations CPU (2026-09-06)

Le coordinateur global appelle désormais les points d'entrée optiques, après une nouvelle
résolution du snapshot courant et comparaison avec le hash candidat. La séquence locale
d'installation est distincte de la génération de capacités et augmente lors d'un nouveau
plan. Les cinq autres domaines conservent les budgets déjà distribués par le coordinateur :
leurs observations CPU existantes sont rattachées au snapshot courant. Eau/environnement
doivent en plus exposer l'identité reçue, les paramètres effectifs exacts et les contrôles
de composants/matériaux réussis. Aucune accumulation d'anciens succès entre frames.

Les systèmes de projection s'exécutent avant l'observation globale `Last` ; une nouvelle
installation optique est donc observée après sa projection au tour suivant. Le retrait
des overrides invalidés ne change ni niveau manuel, ni aperçu manuel, ni document source.
Le garde-fou s'exécute à chaque observation sur `Time<Real>`, avec deadline de 30 secondes.
Un timeout ne se transforme pas en retry automatique du même plan, ni en rollback annoncé.
Les paramètres dérivés déjà installés restent alors visibles mais non acquittés.

Limite délibérément visible : aucun token de complétion GPU ni probe d'erreur de pipeline
renderer n'est raccordé dans ce lot. Le token GPU reste absent et le garde-fou ne peut donc
pas atteindre Ready. L'adaptation automatique reste désactivée. La présence de sept domaines
CPU ne remplace ni upload/exécution GPU, ni preuve artistique, ni performance cible.
Le texte Studio distingue attente GPU, timeout/refus et invalidation ; tests de texte et
compilation Web vérifiés, pas de nouvelle qualification visuelle interactive du Studio.

Validation finale : **500 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict
workspace tous targets/features, check Studio Web/WASM et build natif PASS. Le premier
Clippy a refusé deux fonctions dépassant 100 lignes et un `if` imbriqué : découpage des
phases observation/installation, sans désactiver le lint, puis contrôles et workspace
rejoués. Tests ajoutés : GPU absent/timeout terminal sans retry, retrait des overrides
sans écrasement du niveau manuel, reset manuel et caméra ambiguë sans faux succès.

Smoke natif High canonique, sans kit/prototypes, warmup 120 / sortie 360 frames : sortie 0,
`runtime_errors_observed=false`, sept observations CPU, séquence 1/génération 3,
`plan_application_pending_gpu`, GPU et preset complet false. Le hash de plan est toujours
`ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`.
Identités eau/environnement égales à celle du coordinateur. Réfraction authorée écran,
requête renderer SurfaceTint, plafond SSR `null`, volumétrie authorée true/effective false.
Les cinq liaisons source correspondent au smoke précédent, sans mutation authorée.

Essai timeout initial : sortie bornée à 2400 frames, programme terminé sans erreur, mais
assertion de timeout refusée car le temps écoulé depuis installation n'atteignait pas
30 secondes. Ce résultat reste conservé comme preuve insuffisante de timeout, pas PASS.
L'essai suivant (4800 frames) confirme le comportement attendu :
`plan_application_failed`, erreur `TimedOut`, séquence toujours 1, sept observations CPU
toujours présentes, GPU/preset complet false. Le programme sort avec code 0, zéro ERROR et
`runtime_errors_observed=false` : cela signifie absence d'erreur d'exécution du programme,
pas réussite de l'acquittement qualité, qui est explicitement en échec attendu.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-effective-coordinator-20260906`.

```text
tests-final.log F2765B08235C917EAE41560F91B2F4A3D891B072ECC0BCDA257B62EB30ABC8FF
clippy.log 9C2395659DCC2A995F9228ABAD1E8386FE0B0380052362F60B9B6488E2CCB4DB
clippy-fixed.log 3A31ABA16422AE0FCAC517B06352B0CDACF6F3EE7042F0047A52130838C6568E
wasm.log FE2A5E6728D801F572AFFE1A1E55778F67011C3A79DD2DF700639CDBC4D0D850
build.log A2BEF91C1864D25C9EFC9EC0C79FF9C26C8F48655FF7F55D4FD195A46306FC45
native-metrics.json 61640CBB506D4984511211E3CB55D6765CE8B1C26D33176A58F0433E5F1FB44D
runtime.jsonl 258364DFB9C74E508D380197A560B1794F8CD89FE97DE2B5EEBC3C073BD3D039
timeout-metrics.json 1C520772C560DE0DDE38ABEE736BD10D544D525C64B72C94EBC77A29B70C879B
timeout-runtime.jsonl 49D8BE6CC269E10D85F4DD97D6AFFF767C76DC518616F8DC6C94147C8883BD7E
timeout-long-metrics.json 4C8694F49C75DCE5E9DE4FD9AA8B90A3D5BBE7C003BBBC5CC000B6ABC5672322
timeout-long-runtime.jsonl 897D4013C5B424F3C9F513BFA134B66FA40583DED6FAF68340B5010DC1BD0734
```

Pas de nouvelle capture artistique, benchmark cible, qualification Web des optiques ou
Tauri. Warning futur connu conservé. Gate 1 ouverte, CI distante NOT RUN, aucun commit/push
et aucun asset Cadle importé.
Fmt/diff PASS ; toutes les sessions de tests, build et runtime de ce lot sont terminées.

## Sonde de complétion de soumission GPU (2026-09-06)

La sonde utilise l'ordre vérifié dans les sources locales Bevy 0.19.1 :
`renderer::render_system` exécute le RenderGraph, soumet les commandes finales et présente,
puis `RenderSystems::Cleanup` exécute la sonde. `wgpu` 29.0.4 décrit
`Queue::on_submitted_work_done` comme callback après exécution GPU de la soumission précédente.
Les soumissions suivantes du runtime font progresser les callbacks ; aucun polling bloquant
ou readback d'image supplémentaire n'est introduit.

Le MainWorld fournit l'identité de l'application dont les sept domaines CPU sont observés,
la caméra active unique et sa taille de viewport. La sonde exige une vue extraite correspondante
avec ViewTarget, un cache de pipelines non vide, sans pending/failed, stable huit frames.
Elle conserve au plus une callback en attente pour la clé courante. Identité, caméra,
viewport ou redémarrage du renderer retirent l'ancien jeton ; les callbacks retardées
ne peuvent modifier le nouveau jeton. Une observation de l'ancienne vue n'est jamais
réétiquetée avec la nouvelle identité. La complétion expire après 64 frames sans renouvellement.

Les diagnostics `global_quality.gpu_submission` exposent identité, époque renderer, frames
observée/complétée, décompte des pipelines, vue correspondante et `queue_work_completed`.
Une erreur de pipeline fraîche portant l'identité courante alimente désormais le garde-fou.
Le cache étant global, ce refus est conservateur, pas une attribution du shader en erreur
à un domaine précis du plan.

Cette preuve décrit la fin d'une soumission à la file GPU, pas la présence de tous les
assets requis ni la justesse visuelle de chaque effet. Elle n'alimente donc pas encore
le token `gpu_ready` d'acquittement complet. Le raccord d'identité des ressources GPU par
domaine, la reprise après perte de device et la qualification Web restent à achever.
L'adaptation reste désactivée et Gate 1 ouverte.

Vérifié : **504 tests workspace PASS / 1 timing préexistant ignoré**, puis **13 tests
`global_quality` rejoués PASS** après renforcement du retrait des anciennes observations.
Clippy strict workspace tous targets/features, check Studio Web/WASM et build natif PASS.
Tests : stabilité/cache non vide, attente/échec de pipeline, vue absente, changement de
clé et callback tardive, expiration/régression des frames, attribution fraîche d'erreur.

Deux runs natifs High canoniques (sans kit/prototypes), warmup 120 / sortie 360 frames :
code 0, zéro ERROR, `runtime_errors_observed=false`. Dans les deux : époque renderer 1,
82 pipelines, zéro pending/failed, vue prête, frame observée 358 et complétée 357,
`queue_work_completed=true`. L'identité correspond à l'application (séquence 1,
génération 3, plan `ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`).
Le second run demande un resize 1280×720 à la frame 160, réellement observé à 161 : la
complétion 357 appartient donc à une soumission ultérieure au redimensionnement.
Les sept observations CPU restent présentes, mais `gpu_ready` et preset complet false.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-submission-20260906`.

```text
tests.log 74BB7F3633952B9BC0BC82C4A269F910FA17C30EA401D201766F04EFFFC76A24
targeted-final.log 57AEF3011B391D0B5B07F7198A08A057D883FA1E405E523887FC65FC4B62DCA5
clippy-final.log 4DFB1F7413C2F20485292987FE0F933F8B4A7FDFD3ED154E9128BB3894EFD9F3
wasm.log E935F1E877806C3CF7D9D66644E7D8A2C68D7428645438441593E286B0C98638
build.log 20F33F456C6D1289732B8E17F199778F9D6CB10A38B65E08D175A623CDE5BDCA
native-metrics.json C7EABC6220D7E7C82DD2AB9D5FC215E09010D8116041C20894C682518E74D66F
runtime.jsonl 8AD072F13D36C63E62E46336B12B39D8D127405FEF07353F4D10AD5B757281A3
resize-metrics.json E493FA8EDD33DADBDAD1260DE20E6CD5AA467E86682CBE055671EEF5037874B2
resize-runtime.jsonl 549111EB55C7CCA5781B10902D48762090E2FB9F1B90667993C7CFC1385CC03E
```

Pas de nouvelle capture artistique, benchmark ou validation humaine. Le check WASM ne
prouve pas l'exécution de cette sonde Web. Warning futur connu conservé, CI distante NOT RUN,
aucun commit/push ni asset Cadle. Toutes les sessions de ce lot sont terminées.

## Inventaire de ressources GPU (2026-09-06)

La sonde de file dispose maintenant d'un inventaire des Mesh3d du MainWorld, matériaux
de cinq familles supportées (standard, terrain, végétation, eau, pluie), textures de base,
détail terrain et maps d'environnement. Les IDs sont dédupliqués, y compris pour les LOD
actuellement invisibles ; aucun ID Bevy ne devient une donnée de projet persistante.
Source matériau absente, famille inconnue/ambiguë ou dépendance non supportée restent
des refus visibles. Une scène vide ne devient pas « prête » par égalité de deux zéros.

Après préparation Bevy, le contrôle exige les RenderMesh et allocations de sommets/indices,
les données morph si nécessaires (buffer ou texture selon le backend), les PreparedMaterial
et leurs bind groups réellement créés, et les GpuImage attendues. Les sources Bevy 0.19.1
`render_asset.rs` et `erased_render_asset.rs` confirment le retrait de l'ancien prepared asset
avant tentative de préparation d'une mise à jour, y compris en cas de retry/budget épuisé.
L'inventaire est comparé avant chaque soumission observée ; un changement retire l'ancienne
callback et une ressource manquante remet la stabilité à zéro. La complétion de file ne
devient positive qu'après cet inventaire préparé et les autres conditions déjà documentées.

Ce contrôle ne couvre pas encore tous les buffers/bindings spécifiques des vues, ombres,
atmosphère et post-traitement, ni la justesse des pixels. L'acquittement complet `gpu_ready`
reste donc absent ; adaptation désactivée, Gate 1 ouverte.

506 tests workspace PASS / 1 timing préexistant ignoré, Clippy strict tous targets/features,
check Studio Web/WASM et build natif PASS. Le premier contrôle a relevé une utilisation
incorrecte de `SystemState::get` (Result dans Bevy 0.19) dans deux tests ; corrigée et tests
rejoués, sans désactiver de contrôle. Tests : déduplication des références, sources CPU
non assimilées à des assets GPU, ressources renderer absentes, matériau absent/inconnu et
scène vide refusés.

Smoke High natif canonique sans kit/prototypes, warmup 120 / sortie 360 frames : code 0,
zéro ERROR, `runtime_errors_observed=false`. Inventaire **501/501 meshes préparés,
8/8 matériaux avec bind groups prêts, 5/5 images GPU**, aucun matériau inconnu/source
manquante/dépendance non supportée. `inventory_ready=true`, puis `queue_work_completed=true`.
Époque renderer 1, frame observée 358 / complétée 357, 82 pipelines, zéro pending/failed.
L'identité est toujours séquence 1/génération 3, hash de plan
`ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`.
Les cinq liaisons source sont identiques au smoke précédent. `gpu_ready` et preset complet
restent false : les ressources propres aux vues/effets n'ont pas encore leur preuve complète.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-assets-20260906`.

```text
tests.log B44B880465C1400D8B962F90E1777339CE5A0615AB60D34FB3E052F85C200975
clippy.log B348774DE6890AF37ED94D3C5591335084A20C57DBBDAB02CFB791F3632776E8
clippy-fixed.log 79BD9849261240FB1FD5A97BA51B5D33EB8531FCEB2A83DBC7A850ECBC9C7034
wasm.log 1B7077122C9D9CAF527636239AB46ED4EA00D0ED6766E38C8AFCC0F28CCBED7A
build.log 84B5957FB15FBBEB8A1FE9BC83274272BD2EA581A84CDC1344ED29E26D3DB887
native-metrics.json D679C5964DD0C3648484E981636105B20ACC84D0B18BB5204DFBF7F2B640478F
runtime.jsonl 4B7DA5A49EDF47D4ED538B476D46D75BDEF86D1BC5565D4C077838D15738D4A3
```

Fmt/diff PASS. Pas de nouveau verdict artistique, benchmark, exécution Web de la sonde ou
validation humaine. Warning futur connu conservé, CI distante NOT RUN. Aucun commit/push
ni asset Cadle ; toutes les sessions de validation de ce lot sont terminées.

## Ressources GPU de caméra (nuit du 6 au 7 septembre 2026)

La sonde vérifie maintenant une unique vue extraite correspondant au plan, à la caméra,
à la taille et à l'origine du viewport. Elle exige un buffer ViewUniforms réellement lié,
un ViewUniformOffset, un MeshViewBindGroup (possédant les bind groups GPU) et une texture
de profondeur couvrant le viewport, origine comprise. Dimensions nulles/hors limites et
renderer absent sont refusés. Un déplacement d'origine invalide la callback précédente.
Ces règles ne dépendent d'aucune carte et ne modifient aucun document authoré.

`view.camera_resources_ready` désigne exclusivement ces ressources publiques de caméra.
Il conditionne la complétion de file, mais ne fournit pas le token d'acquittement complet.
Les ressources d'ombres/atmosphère/post-traitement et la justesse des pixels restent hors
de cette preuve. Certaines structures de bindings atmosphériques Bevy sont privées : leur
présence n'est pas inventée à partir des seules ressources publiques. Gate 1 reste ouverte,
adaptation désactivée, aucun verdict humain ou Web runtime déduit.

Premiers essais conservés : compilation rejetant un champ inexistant d'ExtractedCamera,
puis Clippy demandant de justifier les booléens indépendants du diagnostic. Après correction,
18 tests ciblés passaient, mais le premier smoke natif a échoué (code 1, panic B0001) :
la requête EntityRef trop large entrait en conflit avec les autres paramètres du système.
Correction : requête de composants explicitement typés et test d'initialisation du système
complet, en plus des tests de conditions, dimensions et invalidation. Le champ runtime
`runtime_errors_observed=false` de cet essai ne suffit pas : `success=false` et code 1
classent bien ce lancement comme échec. Aucun échec effacé ou qualifié comme succès.

Logs conservés dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-camera-20260906`.

Rejeu final : **19 tests ciblés global_quality PASS**, Clippy workspace strict tous targets/
features, check Studio Web/WASM et build natif PASS. Pas de nouveau replay des 506 tests
workspace : le changement est limité à la sonde moteur et ses diagnostics, sans modification
du contrat de domaine. Warning futur proc-macro-error2 2.0.1 connu conservé.
Smoke natif High canonique, sans kit/prototypes, warmup 120 / sortie 360 : code 0,
`success=true`, `runtime_errors_observed=false`, zéro ERROR/panic. Une vue correspondante,
les quatre observations de ressources caméra et `camera_resources_ready=true` ; callback
complétée frame 357, observée 358, époque 1. 82 pipelines, aucun pending/failed ; inventaire
501 meshes / 8 matériaux / 5 images prêt. Les cinq hashes source sont identiques au lot
d'assets précédent, plan inchangé `ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`.
Application toujours `plan_application_pending_gpu`, `gpu_ready=false`, preset complet false.
Le déplacement d'origine est couvert par test CPU, pas par un nouveau smoke interactif.

```text
tests-final.log 7DC81648F909D2D3F3586B4F9EA3F7F6B0B1AB808DAC9AFDB205E9DC94BD9FA6
clippy-final.log D3AA0997E2E16AE71B7C1ECDDF62ECC10A3D4D722E0C2C287B9C1262A2B10337
wasm-final.log 60C0C39AB03217B09AD5C10683642633D2199FC1990465F7AB86B3E030BF243D
build-final.log DD18A78B428C1BD39BA189CEE71321D53567919380566B2F90D3BD6E3625F743
runtime.jsonl 58DC06993535E7839E6DEC8EE191F3ABE53D491D8607464945D23720D9E6AC48
runtime-final.jsonl DCFD0C0DE96678CE55B1F7A7E8CD43329521CF04317C3A367E1D3B8F390787AF
native-final-metrics.json 42792DAB2D2B15EF7BB357393792C6F923AD9920F106EF37D82C4D056B71F42C
```

Toutes les sessions de ce lot sont terminées. Aucune capture artistique, mesure de performance
cible ou validation humaine nouvelle ; CI distante NOT RUN sous ADR-0006, aucun commit/push
ni asset Cadle. Les modifications préexistantes sont conservées.

## Inventaire d'effets et premier acquittement natif (2026-09-07)

### Implémentation et sens du verdict

L'inventaire `gpu_submission/effects.rs` dérive ses exigences du plan courant et des
intentions authorées ciel/brouillard/Bloom. Il contrôle les groupes suivants lorsqu'ils
sont requis : uniformes de lumière ; dimensions de l'atlas directionnel et tous les
indices de cascades d'une même lumière, sans doublons ; uniformes d'ombres de contact et
de brouillard de distance ; quatre textures LUT atmosphériques aux dimensions attendues,
uniformes/offsets atmosphériques et medium GPU préparé ; pipeline SSR effectivement compilé,
uniformes/offset SSR, prépasses profondeur/différé et image blue-noise ; textures/bind groups
Bloom ; pipeline de tonemapping et LUT Tony McMapface GPU. Contexte manquant ou inventaire
vide ne produisent pas un succès. Un changement d'exigences retire la callback précédente.

La sonde transmet maintenant au garde-fou une preuve de **préparation et soumission du
plan effectif**, distincte de l'application intégrale du preset authoré. Conditions :
sept projections CPU courantes, inventaires assets/caméra/effets prêts, pipelines non vides
sans attente ni erreur, callback réellement reçue, identité séquence/hash/génération égale,
caméra/viewport/origine/cible normalisée égaux. Observation vieille de plus de deux frames,
complétion future/absente/expirée et époque renderer nulle ou différente sont refusées.
Le garde-fou conserve l'époque de sa première preuve acceptée ; son échec reste terminal.

`application.gpu_ready` et `effective.gpu_ready` ne deviennent vrais qu'en état Ready du
garde-fou. `full_preset_applied` reste false. Aucun pixel correct, rendu de tous les objets
hors champ, performance cible ou acceptation artistique n'est déduit de ce verdict.
Les bind groups atmosphériques privés et les bindings créés dans les passes ne sont pas
inspectés directement : leur préparation/consommation relève du chemin Bevy 0.19.1 normal,
avec les prérequis publics contrôlés et la soumission observée. Ce raccord n'est pas une
nouvelle qualification optique ; il exploite les capacités natives déjà qualifiées pour
le plan canonique. Le chemin Web reste bloqué par ses capacités non qualifiées.

Studio dispose d'un texte distinct pour le plan effectif préparé/soumis, conditionné à
Ready + sept domaines + preuve GPU + absence d'erreur. Il rappelle que preset complet et
qualité visuelle ne sont pas validés. Texte testé, pas de nouvelle session Web/Tauri ni
capture UI qualifiée dans ce lot.

### Vérifications

Premier contrôle de compilation en échec conservé : chemins de modules Bevy privés alors
que leurs types publics sont réexportés ailleurs, nom d'enum Forge, conversion u16/u32 et
accès au buffer ContactShadows. Corrections via les réexports publics, sans fork/dépendance.
Clippy du raccord a ensuite relevé une fonction trop longue et une affectation clone ;
helper de lecture de preuve extrait et `clone_from` utilisé. Aucun contrôle retiré.

22 tests ciblés ont d'abord passé pour l'inventaire seul. Après le raccord moteur/Studio :
**516 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict workspace tous
targets/features, check Studio Web/WASM et build natif PASS. Tests ajoutés : groupes absents,
inventaire vide, cascades manquantes/dupliquées/mélangées, contexte absent, identité/cible/
caméra/viewport/époque/fraîcheur, complétion et ressources manquantes, texte Studio cohérent.
Le test d'initialisation du système renderer complet est rejoué avec les nouveaux paramètres.

Scène native WorldSketch canonique, High/Low, sans kit/prototypes, warmup 120. Les deux
smokes initiaux avant raccord ont observé High 11/11 groupes et Low 6/6, sans GPU Ready
encore raccordé. Les smokes finaux donnent :

| Cas final | Verdict application | Ressources effets | File GPU |
| --- | --- | --- | --- |
| High, sortie 360 | Ready, GPU true, 7/7 CPU | 11/11 | observée 358, complétée 357 |
| Low, sortie 360 | Ready, GPU true, 7/7 CPU | 6/6 | observée 359, complétée 358 |
| High, sortie 4800 | Ready maintenu, GPU true | 11/11 | observée 4799, complétée 4798 |
| High, resize 1280×720 frame 160, sortie 360 | Failed(Invalidated), GPU false | 11/11 de nouveau prêts | observée 358, complétée 357 |

Le run prolongé dure 44,21 s entre ses premier et dernier événements, pas un benchmark.
Resize : les ressources redeviennent prêtes mais le garde-fou échoué n'est pas automatiquement
réarmé ; ceci prouve le refus du succès périmé, **pas une reprise automatique fonctionnelle**.
La reprise après resize/perte de ressource/device reste à implémenter et qualifier. Une simple
resélection du même niveau n'est pas encore un mécanisme de retry de cette application.
Les cycles des quatre profils et l'adaptation runtime restent ouverts ; aucune adaptation activée.

Tous les runs finaux : code 0, runtime `success=true`, `runtime_errors_observed=false`, zéro
ERROR/panic, 501 meshes / 8 matériaux / 5 images prêts. High : 82 pipelines, Low : 70,
aucun pending/failed. Les cinq liaisons source sont exactement égales au lot caméra précédent.
High conserve le hash `ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686` ;
Low : `23e90cf10ff17a5ad71e685027bc9a41869898cef263ab666ba4e72f4b1a2be9`.
Séquence 1/génération 3, époque renderer 1. Les presets complets restent false dans tous les cas.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-effects-20260907`.
SHA-256 du binaire final : `36A362AC8E348F023C96970F80CEB7E14C07563344FDF29B8B081A0816844063`.

```text
check.log AA03C857A84E0F64F3D108CD1057D41DC13D9D1A6ECAA124DA2B094FD81727D5
clippy-ack.log 17B05077FE0FB37DF383593340060C4547123FB237A8090F5150D1C6155FBF74
clippy-ack-final.log B7C38F5FA64D9831EAB80E0B3072CF5A2F764196830B48E4673749DB44721E9C
tests-ack.log 36C79DF82227B8B99154AB4DC88F54C917B05D2C4DE05B884A86BC0908E0D64E
wasm-ack.log 3EBA13F0369A3E92F7D6DD063C696C54B0DED3E0A70C3DFF466B8D96FE3B214A
build-ack.log 5581BD01DB5722F936E54C88AA9E5230FF809C8ABDEC46BF653C4F7BE01F4BE1
high-ack-metrics.json E510DADD3B90FC2556C01C1942D43A7ABA1C22DB4664F8115D993C80ED1A6F9F
high-ack-runtime.jsonl F090F6D4ED3A8D258E879847715C31AAE117BB99F84770061EEB085FF4727937
low-ack-metrics.json 7430E469754E65B3452FBA3A38634945A89D10AE95A8395803C2B8968B290BAD
low-ack-runtime.jsonl AD121E9789D9AE6749C5DA4627AF97FC0C23A6DAE082D14CB96AD3BDCA4C63B3
high-long-ack-metrics.json C67E5E83E72B0C77C2D59BE577BF8FC277ECB0FD110AFC630D688ADE5E8F8EB5
high-long-ack-runtime.jsonl 163DA83A7D1A5B63A78B0A5FAB4B7B85899C2E48D2D1647E2243194783A23AB6
resize-ack-metrics.json 66D613C5BD9B02FCF3996B0856F2BA4EC1BC530FF0475AD95FDD8514117527E4
resize-ack-runtime.jsonl 7B258D0226AC1714E612D31DAE194EDEE71C9323424AE2CBB277342145B7701D
```

Fmt/diff PASS. Warning futur proc-macro-error2 2.0.1 conservé. CI distante NOT RUN sous
ADR-0006 ; aucune acceptation humaine, performance cible ou Gate 1 déduite. Aucun
commit/push, changement authoré ou asset Cadle. Toutes les sessions de ce lot sont terminées.

## Reprise renderer et cycle natif des quatre profils (2026-09-07)

### Règle réutilisable implémentée

Une application déjà Ready qui perd sa preuve renderer devient invalide. Si ses sept
projections CPU et son installation optique restent identiques, le coordinateur expose
`plan_application_waiting_renderer`, avec GPU false et l'erreur Invalidated conservée.
Après retour de ressources courantes prêtes, il revalide les sources/capacités et installe
un **nouveau garde-fou et une nouvelle séquence**. La preuve de l'ancienne séquence ne
peut pas acquitter la nouvelle ; l'ancien garde-fou échoué n'est jamais remis à Ready.

La reprise n'est pas un retry universel : timeout, rejet de pipeline, horloge régressée,
caméra ambiguë, changement manuel d'installation et divergence de domaine ne l'autorisent
pas. Aucun changement de niveau adaptatif, de document, de placement ou de collision.
Les transitions sont journalisées via `quality_application_changed` (statut, séquence,
hash/génération, époque renderer et temps réel), uniquement quand l'état/identité change.
Studio explique l'attente avant une nouvelle application ; ce texte est testé, sans
nouvelle session interactive Web/Tauri qualifiée.

Le protocole `--quality-cycle-proof` Global est renforcé : chaque étape exige les sept
observations CPU et l'acquittement effectif GPU cohérents, au lieu des seuls budgets CPU.
Les cinq liaisons source et le hash de politique doivent rester constants. Il attend
180 observations correspondantes avant de demander une capture, puis un readback/fichier
neuf ; la deadline globale reste 120 s. Les essais Global historiques n'ayant contrôlé
que le CPU ne sont pas requalifiés rétroactivement. Les autres cycles de domaine gardent
leur périmètre antérieur ; un override expérimental non qualifié ne passe pas le Global
renforcé au seul motif que ses composants CPU existent.

### Vérifications automatisées

**519 tests workspace PASS / 1 timing préexistant ignoré**, Clippy strict workspace tous
targets/features, check Studio Web/WASM et build natif PASS. Tests : reprise uniquement
après Ready perdu, maintien de l'attente, refus après intervention manuelle/domaines
divergents et erreurs non récupérables, refus du cycle sur simple CPU, identité/génération
périmée, plan/source absent ou application non acquittée. Les tests de timeout et de
non-réutilisation des anciennes callbacks restent verts.

Échecs intermédiaires conservés : fonction trop longue (observations de domaines extraites),
types de diagnostic non réexportés pour le test consommateur runtime (réexports des DTO
publics existants ajoutés), puis règles de placement des imports/points-virgules dans les
tests. Aucun contrôle désactivé ; Clippy final et replay complet passent.

### Reprise native après resize

WorldSketch canonique High, sans kit/prototypes, warmup 120 / sortie 360 ; resize à 1280×720
demandé frame 160. Journal de transitions (temps Time<Real> de cet essai, pas un benchmark) :

| Temps s | Séquence | État | GPU |
| ---: | ---: | --- | --- |
| 4,113 | 1 | Pending | false |
| 4,811 | 1 | Ready | true |
| 5,557 | 1 | attente renderer, Invalidated | false |
| 5,644 | 2 | nouvelle application Pending | false |
| 5,725 | 2 | Ready | true |

Hash de plan inchangé `ae57d91ae8139ff4979264bb1fd0aeec6b9eff2580eec81c9aec6bb46a451686`,
génération 3, époque renderer 1 ; cinq liaisons source identiques au smoke précédent.
Dernière observation GPU frame 359 / complétée 358, 82 pipelines sans pending/failed,
501 meshes / 8 matériaux / 5 images, caméra prête, 11/11 groupes d'effets prêts.
`full_preset_applied=false`. Reprise de resize réelle prouvée ; une perte de device réelle
n'a pas été injectée et ne reçoit pas cette qualification par analogie.

### Cycle natif et inspection des captures

Commande : `forge_runtime --world-sketch-visual-valley --quality high --quality-cycle-proof`
avec le dossier neuf `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-recovery-20260907/cycle`.
Les cinq observations qualifiées d'étape sont High séquence 1 / Low 3 / Medium 4 / Ultra 5 /
High 6, chacune Ready, GPU true, preset complet false. Générations 3/7/11/15/19.
Une reprise additionnelle, séquence 2, est enregistrée pendant la première étape, après sa
première observation et avant Low ; elle n'est pas masquée. Le cycle vérifie l'acquittement
avant capture puis le readback, pas une identité transactionnelle figée pendant tout ce
readback. Les cinq captures ont les mêmes cinq hashes source et le même hash de politique.
Les hashes de plans changent avec niveau/génération ; un High restauré n'est pas une ancienne
identité réutilisée. Cinq readbacks/fichiers non vides et événement final à cinq étapes.

Les cinq PNG 1920×1080 ont été ouverts et inspectés en une seule série. Terrain, rivière
et pont restent visibles ; densité moindre en Low, plus étendue en Ultra, retour à la
couverture High. Pas d'image noire ou de disparition globale observée. Cette vue distante
procédurale reste sommaire et ne qualifie ni le rendu de proximité, ni l'esthétique/forêt,
ni les douze captures normalisées finales. Temps virtuel non figé ; aucune égalité pixel
à pixel ou performance cible déduite des overlays pendant reconstruction/capture.

Les deux processus natifs se terminent avec code 0, `success=true`,
`runtime_errors_observed=false`, zéro ERROR. Adaptation automatique, qualification Web/Tauri,
perte/recréation réelle de device, critères artistiques/performance et acceptations humaines
restent ouverts. Le mécanisme récupérable est moteur, sans règle propre à cette carte.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-gpu-recovery-20260907`.
Binaire SHA-256 : `84E7290BBC976B78B7D6177B9866773C6156156688736EF90317787F192460F6`.

```text
clippy.log B2290C6A6006B536EEF1C40B4BFABB47C5EC1645711E966A86E2FA2E1D517417
clippy-final.log CFF5C4E4CD608289EAB12191311CAB8B7FB2EF9803E4BC3E68E1D6CC5241C61D
clippy-verified.log BBABE55DE4EECCDCA4A75DC395780D3E6310F95A25C6A1BB99F800AA09E4943F
clippy-pass.log 93726A5323031BCA572FD0646103EB86EE96289A78CBEEE9E58133F035B47A24
tests.log C704DB4ECD1F02FB156974D58F5786058D565F3AD7434F51BCFE1F8BC296812C
wasm.log A3DC317261F7BE94089A61F69696FA3F198B8C574DCB745EB32FD63F0305EEFC
build.log C1F7698BD5E92D5B1D4BC17A9FEB354BA03D2059F693BBB2F93D2E7258FE2B17
resize-metrics.json A9D51EF2BBB4E74FCFD0FF7CACAA78845C670EB3353F99A34BD3090E7722ADFC
resize-runtime.jsonl 1A0BD5DC5BA38E6F92C159A04BC741528A7CB6A10172D71BF0A3C385C64E7C6C
cycle-runtime.jsonl 92772BD04D235C364B472C5B4BC17FC9FF7D8AD466299DDC78539A4EE25CB970
stage-0.png 19F92B7BA56ED0BEE6C91400B29B1966218830FD53301B0ADDCCA0886FC7F71F
stage-1.png AE93CBD24399CC953439731247C548A0CDC3515813553CC8AC83BCF52F993A6C
stage-2.png 1E9EB9C3EBE10EFEFE891D395293FB1683542B88F523F28BB6564B8482E5ACC9
stage-3.png 1AB5EB62A00C381F40BF4D4761BC0F4AC42CB0B591EF45B1D2B11E52095598A5
stage-4.png B6C2C41FCFB6381B2E8C6D879EBCD52493014B2C614E6C3A45AD36A5D651053F
```

Fmt/diff PASS. Toutes les sessions du lot sont terminées. Warning futur connu conservé,
CI distante NOT RUN sous ADR-0006. Aucun commit/push ni asset Cadle, changements existants
préservés. Gate 1 reste ouverte ; aucune étape de Gate 2 n'est commencée.

## Démarrage du contrôleur depuis une installation observée — 7 septembre 2026

Prérequis implémenté, pas activation de l'adaptation :
`QualityAdaptationController::from_observed_application` copie le garde-fou et réobserve
les sept domaines et la readiness GPU fournis par l'adaptateur. Il refuse une autre politique,
les preuves incomplètes, les identités différentes, les erreurs et l'historique invalidé.
La prochaine séquence suit celle de l'installation (19 → 20 dans le test), avec cooldown
initial ; un dépassement de u64 est refusé, jamais rebouclé. La première transition depuis
cette installation ne peut pas utiliser l'acquittement historique par simple niveau.

Cinq tests synthétiques ajoutés, y compris benchmark fixe sur 20 000 observations.
Les observations de test ne sont pas des mesures GPU. Le runtime doit encore appeler cette
API, coordonner exclusivement les séquences, filtrer les mesures non représentatives et
préserver la priorité manuelle. Aucun appel renderer ajouté, aucun changement de carte,
asset, source authorée, CLI, UI, benchmark ou valeur de preset. L'adaptation reste désactivée.

Validation locale, code de sortie 0 pour chaque commande :

- `cargo clippy --workspace --all-targets --all-features -- -D warnings` : PASS ;
- `cargo test --workspace` : **524 PASS / 1 timing préexistant ignoré** ;
- `cargo check -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web` : PASS ;
- `cargo fmt --all -- --check` et `git diff --check` : PASS.

Cache et environnement identiques au lot précédent : target `terrain-water-20260906`,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, TEMP/TMP `terrain-water-temp-20260906`.
Pas de nouveau build/lancement natif ni capture : API pure encore inutilisée par le rendu.
Ce lot ne remplace donc pas les preuves natives précédentes et ne qualifie pas l'adaptation.
Warning futur proc-macro-error2 2.0.1 conservé ; CI distante NOT RUN sous ADR-0006.

Logs : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-adaptive-start-20260907`.

```text
clippy.log BC5E06349E8F37FE6AEF43CE169B1205517749218DFCB59670A91E75DA7D3CB1
tests.log DAE6F750FFD7F7DDD88FE5A74F4887ACFEFD6B283D74A63E22773CF98FCA1186
wasm.log 56D586DF2A07CDB2D75F27880E97E75681093741CA708ABFD49521339A6339AC
```

Gate 1 et R1-040 restent ouverts. Aucun commit/push, aucune suppression, aucun asset Cadle.

## Adaptation optionnelle raccordée au moteur — 7 septembre 2026

### Implémentation et portée

`--quality <niveau> --adaptive-quality` active explicitement le contrôleur après observation
du plan effectif. Le système moteur s'exécute après l'observateur global dans `Last` ; il utilise
`Time<Real>` de la fenêtre principale active, les seuils/hystérésis/cooldown authorés et le
coordinateur existant des sept domaines. Aucun nouveau renderer, dépendance ou document canonique.
Chargement, attente GPU et fenêtre inactive ne constituent pas des échantillons de pression.
Une proposition reste en attente jusqu'à l'acquittement de sa véritable installation. Une liaison
explicite et unique accepte la séquence plus récente allouée par le coordinateur, sans réinitialiser
l'historique du garde-fou ; les décisions suivantes poursuivent cette séquence.

Un appel global manuel, même au même niveau, désactive l'adaptation. Une perte de preuve,
une source différente ou une transition dépassant 30 s arrête ce contrôleur, sans annoncer
un rollback. Une réactivation explicite repart d'observations courantes, jamais du contrôleur
arrêté. Les métriques/captures fixes refusent l'adaptation côté CLI et moteur. Cycles, probes
et aperçus forêt/volumétrie non qualifiés sont exclus de cette option. Le mode fixe reste le défaut.
`GlobalQualityStats.adaptation` et les logs de transition exposent demandes et acquittements séparés.

### Vérifications

- **533 tests workspace PASS / 1 timing préexistant ignoré** : dont nouveaux tests de transition
  avec attente GPU, séquence intermédiaire, absence/focus, perte de preuve, priorité manuelle,
  refus du benchmark, timeout et réactivation ; API/CLI testées sans écrire de carte.
- Clippy workspace all-targets/all-features strict, check Studio Web/WASM, build natif,
  formatage et diff : **PASS**. Commandes et environnement identiques au lot précédent.
- Échecs intermédiaires conservés : import d'un module privé, passage de snapshot par valeur,
  forme de contrôle de flux, limite de taille du parseur et mauvais constructeur dans un test.
  Corrections sans suppression de contrôle ni tolérance ajoutée. Logs `clippy*.log` conservés.
- Un premier binaire a terminé 10 000 frames adaptatives, trois montées acquittées, code 0.
  Son hash est `026CE1EB8B33B6EC41CF179500756D1FD94F8D1AC878C2DFEC808A8CDEC5830C`.
  La dernière correction ajoute le redémarrage explicite du contrôleur et les champs de diagnostic.

Binaire final SHA-256 : `F3BB0BD66F45664CC7356E47629320E834901964FC02FBE44E7B9ADE56E85170`.
Machine observée : RX 7800 XT, Vulkan, pilote AMD 26.8.1 ; pas la cible RTX 3060/RX 6600.
Deux exécutions séquentielles, sans compilation concurrente :

```text
forge_runtime --world-sketch-visual-valley --quality low --adaptive-quality --no-vsync --exit-after-frames 4000
forge_runtime --world-sketch-visual-valley --quality high --metrics-warmup-frames 120 --exit-after-frames 360 --metrics <proof-root>/fixed-metrics.json
```

Le premier utilise de vraies durées de frame, sans injection ni modification des seuils :

| Niveau acquitté | Séquence | Génération | Temps réel écoulé | Échantillons admissibles |
|---|---:|---:|---:|---:|
| Low initial | 1 | 3 | 2,03 s | 1 |
| Medium | 2 | 6 | 8,65 s | 841 |
| High | 3 | 9 | 19,31 s | 1681 |
| Ultra | 4 | 12 | 35,37 s | 3066 |

Les trois propositions apparaissent à 840, 1680 et 3065 échantillons ; aucune nouvelle
pression n'est accumulée pendant leur installation. Le délai proposition → acquittement est
respectivement d'environ **2,38 s, 4,94 s et 4,79 s**. Ce sont des délais ponctuels d'application,
pas un benchmark FPS ni une mesure exhaustive des hitches. Ils interdisent de qualifier la
fluidité de l'adaptation : le coût de projection des transitions est le prochain problème concret.

Le second run conserve High, `adaptation.status=disabled`, zéro échantillon/proposition adaptatifs,
sept domaines projetés et `plan_application_ready`, `gpu_ready=true`, `full_preset_applied=false`.
Les cinq hashes source de ce run correspondent aux quatre acquittements adaptatifs : **20/20
comparaisons exactes**, incluant terrain, végétation, environnement, eau et météo. La règle est
réutilisable ; aucune retouche one-shot de carte ni source authorée modifiée.
Les deux sorties sont 0, avec `runtime_stopped.success=true`, `runtime_errors_observed=false`,
aucun ERROR/panic et sorties bornées confirmées à 4000/360 frames.

### Preuves et réserves

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-adaptive-runtime-20260907`.

```text
clippy-pass.log 38D715CD42E774A178AAB402C93A2AA53D3AEAB2AD6BFD9AEC8B8DD0EF22F506
tests-final.log 7DEBB18CABBC36321958B9E1F17799AB0A673E9C4057514C9EE527A8A6E7E4E3
wasm-final.log 1050859D863BF35671C366FC74AECAED837CBF82A23A12E328851E712EA9D0CF
build-final.log 0E6353ADA8DF5C7057EA04270D15ED793B67833F1C097D3DF4F59B7130B90DE8
adaptive-native.jsonl 111F4F6E2049AA4DB0C959D5154C252386A0B6319A66D77FA2E42F616DC5E9C7
adaptive-final.jsonl 7E1196BCBA361CDDA91587B35EBFAAFFB0EDFE695935B7391248777FC6B64622
fixed-final.jsonl BD292B20DC2674FA814CD7A6071F3E09D1F98569E1E1363BEEE3C833D56E72A5
fixed-metrics.json 2804C67E848848FB6E610047E077E39C183535E37241D0C5EAAE9B5B573BDB38
```

Branche Rust, HEAD de base `f786a38aca2023f08d708687f99a47d14f34e6d4`, arbre de base
`e055dc0b1567b1a4f29286e3f9c1e917ab507612`, worktree modifié, suivi local origin/Rust derrière
de 103 commits sans fetch : ce n'est pas une qualification de gate sur arbre propre.
Pas de nouvelle capture ou validation artistique ; les captures des profils fixes antérieurs ne
qualifient pas les frames intermédiaires de l'adaptation. Baisse sous charge réelle, fluidité,
interface/qualification Studio-Web, reprise device et matériel cible restent ouverts. Pas de
micro-optimisation CPU revendiquée. Warning futur proc-macro-error2 conservé, CI distante NOT RUN.
Toutes les sessions du lot sont terminées. Aucun commit/push, aucune suppression ni asset Cadle.
R1-040 et Gate 1 restent ouverts ; Gate 2 n'est pas commencée.

Mesure/correction suivante du coût principal : voir [R1_QUALITY_TRANSITION_CPU.md](R1_QUALITY_TRANSITION_CPU.md).
Les délais complets tombent à 1,11–1,99 s sur le replay natif, sans qualifier la fluidité ou la gate.

## Essais de baisse sous charge 4K — 7 septembre 2026

Objectif : déclencher une baisse avec une charge graphique réelle, sans modifier les seuils,
injecter de timings ou introduire une pause CPU. **Résultat non concluant pour la baisse** :
Ultra est acquitté et le contrôleur entre en `tracking`, mais aucune proposition n'apparaît.
Ne pas présenter ces sorties sans erreur comme une validation du downshift.

Même binaire que la sonde physique R1-064, SHA-256
`4E7205BE0BB76D0EB9796794ABAFDB584AD80FA9EB30710363CA42DFDB01BC7B`.
Trois runs séquentiels, sans compilation concurrente, RX 7800 XT/Vulkan :

```text
forge_runtime --world-sketch-visual-valley --quality ultra --adaptive-quality --no-vsync --resize-at-frame 1 --resize-width 3840 --resize-height 2160 --exit-after-frames 6000
forge_runtime --world-sketch-visual-valley --weather rain --quality ultra --adaptive-quality --no-vsync --resize-at-frame 1 --resize-width 3840 --resize-height 2160 --exit-after-frames 3000
forge_runtime --world-sketch-visual-valley --weather rain --quality ultra --no-vsync --resize-at-frame 1 --resize-width 3840 --resize-height 2160 --metrics-warmup-frames 240 --metrics <proof-root>/fixed-rain-4k.json --exit-after-frames 600
```

`RUST_LOG=info`. Resize demandé avant la première application acquittée, pas de perte de
preuve volontaire après démarrage de l'adaptation. Les deux runs adaptatifs terminent aux
frames prévues, code 0, zéro ERROR, zéro proposition. Les logs de changement d'état ne
contiennent pas toute la série des timings : ils ne prouvent pas combien de frames lentes
consécutives ont été mesurées pendant ces runs.

Le témoin fixe confirme **3840×2160 physiques**, 360 échantillons après 240 frames de chauffe,
adaptation désactivée : moyenne **17,323 ms / 57,73 FPS**, p95 **17,838 ms**, p99 **18,452 ms**,
1 % low **53,83 FPS**, maximum **18,836 ms**. Ce témoin de 6,24 s est trop court pour qualifier
une performance finale et n'est pas une mesure du matériel cible. Il est compatible avec
l'absence de pression soutenue : le p95 reste sous le seuil de baisse de 18 ms, lequel exige
90 frames consécutives après cooldown de 240 frames. Il ne permet pas de reconstruire les
séries des deux autres runs. Aucune modification de politique pour fabriquer une réussite.

Politique fichier SHA-256 :
`0AAA2A17C8DFD18382BEF31C912EF26884C54BFF0A9F455B8529E7BD155FED2C`.
Racine locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-adaptive-downshift-20260907`.

| Artefact | SHA-256 |
|---|---|
| ultra-4k.jsonl | 6A2A147C00FB1D269291A14F7A48260C226F2569B6A81AB8C67CC2022107D0EB |
| rain-ultra-4k.jsonl | 334A65CF910647F0C1F22065188B68D2EDAA484596C654F79799A0CA29C4DB8A |
| fixed-rain-4k.json | C5A8CFE741F737DECF114AEE65593FE383F77F83B85D7C14CB2475BF267AD70D |

Suite : ne pas rejouer ces mêmes charges en espérant une baisse. Il faut un scénario réel
durablement au-delà du seuil ou le matériel cible, avec série de pression mesurée et
acquittement du niveau inférieur. Aucun nouveau code produit, build, test unitaire ou rendu
artistique revendiqué. Les trois processus sont terminés ; changements existants préservés,
aucune publication. R1-040/Gate 1 restent ouverts, CI distante NOT RUN.
