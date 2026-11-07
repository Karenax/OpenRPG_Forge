# GLib 0.18.5 — correction upstream de VariantStrIter

Paquet original : https://static.crates.io/crates/glib/glib-0.18.5.crate

- SHA-256 registre : `233daaf6e83ae6a12a52055f568f9d7cf4671dabb78ff9560ab6da230ce00ee5`.
- 121 fichiers, 1 717 584 octets avant correction ; licence MIT d'origine conservée.
- Provenance VCS du paquet : `42b9caf98e03ded086362d9653ca58fe94dc8658`, sous-répertoire `glib`.
- La version réelle reste **0.18.5**. Aucun manifeste upstream n'est changé.

Correctif : [gtk-rs-core PR 1343](https://github.com/gtk-rs/gtk-rs-core/pull/1343),
commit `b5a4071e439bef2b5eea76c3aa25e5ae84839e34`, fusionné le 30 mars 2024.
Le patch obtenu depuis cette PR fait 1 170 octets, SHA-256
`a742bf59f1b00af0f434623b34e2fd969dc8697d37c796efdf909dd9eb19b854`.
Dans `src/variant_iter.rs`, seules deux lignes changent : le pointeur de sortie
devient `let mut p` et l'appel C reçoit `&mut p`. Le fichier corrigé a pour SHA-256
`a0f5ee8acb8faa089bcdfbc9a57372609fce7654026ccef7d9a224d05a654ccc`.
Les 120 autres fichiers sont identiques à l'archive vérifiée.

[RUSTSEC-2024-0429](https://rustsec.org/advisories/RUSTSEC-2024-0429.html)
documente la mutation incorrecte derrière une référence immuable et les cinq
méthodes concernées. Le correctif ne change ni API, ABI, dépendance, format
persistant ou version système GLib. GTK 3/Tauri conserve sa branche 0.18.
Le paquet existe uniquement pour cette correction de sécurité, sans autre fork.

Le vérificateur `scripts/gate7/verify_glib_backport.py` compare l'intégralité du
paquet aux octets du registre et à ce delta. Le probe
`tools/security/glib_variant_str_iter` exerce les cinq méthodes en code Rust sûr,
sur le registre original puis sur le paquet corrigé, dans des processus distincts
compilés en release. Les cinq méthodes du registre provoquent un SIGSEGV ; le
contrôle corrigé réussit pour les cinq méthodes, dans des processus distincts.
Le bundle central vérifié `glib-backport-001.zip` conserve ces dix résultats.
Les trois manifests des applications sélectionnent maintenant ce backport ; les
graphes Linux le confirment, et Aube/Hub passent Clippy et la compilation release
dans `linux-shells-001`. Ces résultats ne qualifient pas leur parcours graphique
ou la distribution complète.

La compilation optimisée, la consommation par les applications, le graphe réel,
les licences et les audits bruts doivent être vérifiés séparément. Un scanner
peut encore signaler 0.18.5 ou omettre une dépendance locale : ni le numéro de
version ni le résultat du seul scanner ne prouvent la correction. Aucun ignore
de cet avis ni relâchement de la politique de dépendances n'est autorisé ici.
Retirer ce backport après qualification d'une pile Tauri/GTK utilisant une branche
officielle corrigée ; garder cette provenance jusque-là.
