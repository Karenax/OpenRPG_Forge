# Gate 1 — actualisation exacte de la politique RustSec

12 septembre 2026. L'audit a échoué sur dix avertissements attendus mais absents,
pas sur une nouvelle vulnérabilité. La base officielle locale RustSec au commit
`b50980aad8b8f14f77e25a97b32dd94bf008b0af` marque les avis RUSTSEC-2024-0411
à RUSTSEC-2024-0420 comme retirés le 14 août 2026 : maintenance GTK3 reprise.
Les dix fichiers et leur date de retrait ont été contrôlés individuellement.

La politique exacte Gate 0 retire ces dix entrées devenues obsolètes ; les mêmes
exceptions inutiles sont retirées de deny.toml. Aucun nouvel avertissement accepté,
aucune dépendance modifiée, aucune vulnérabilité ignorée. Les dix autres entrées
restent identiques, notamment RUSTSEC-2024-0429 / glib 0.18.5, toujours bloquant
pour la distribution. Le script continue d'exiger une égalité exacte des avis.

Preuves locales : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-clean-closure-20260912/` :
`rustsec-current.json`, `withdrawn-advisories.json`, première erreur conservée dans
`validation-2/rustsec.log`, puis contrôles de politique dans `policy-and-tools/`.
Cette actualisation ne prétend ni qualifier Linux ni rendre le produit distribuable.
