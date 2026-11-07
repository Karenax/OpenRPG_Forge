# ADR-0038 — Correction officielle de VariantStrIter sur la branche GLib 0.18

Date : 23 octobre 2026. Statut : correctif ciblé dans la réalisation de Gate 7 ;
qualification du candidat produit à établir. Portée : GLib utilisé par GTK3/Tauri.

## Contexte et décision

Le launcher, le Hub et le Studio conservent GLib 0.18.5 via leur pile GTK3.
[RUSTSEC-2024-0429](https://rustsec.org/advisories/RUSTSEC-2024-0429.html)
décrit une écriture C derrière une référence Rust immuable. Le probe release
reproduit un SIGSEGV dans les cinq méthodes affectées avec le paquet du registre.
Une simple réussite de Cargo audit ne suffit pas : l'avis est classé informatif
`unsound`, et les sources locales peuvent être omises par un scanner.

Appliquer le correctif officiel de
[gtk-rs-core PR 1343](https://github.com/gtk-rs/gtk-rs-core/pull/1343) au paquet
0.18.5 vérifié. Deux lignes seulement changent : pointeur local mutable et passage
de `&mut p` à la fonction variadique. La version, l'API, l'ABI, les dépendances,
les manifests et les licences du paquet restent d'origine. Aucun nouvel ignore,
changement de numéro destiné au scanner ou migration graphique n'est introduit.

Les trois workspaces Cargo autonomes (racine, launcher et Hub) sélectionnent le
même paquet `third_party/glib-0.18.5`. L'exclusion de ce paquet du workspace
racine empêche d'en faire un composant auteur Forge ou de reformater ses sources.
Les locks doivent changer seulement l'origine de GLib 0.18.5, en conservant les
versions des autres paquets et les changements locaux préexistants.

## Dépendance, provenance et maintenance

Ce n'est pas une dépendance de production supplémentaire : le graphe GTK3
existant consomme le même code avec huit octets source ajoutés. Licence MIT
conservée, 120 fichiers identiques et un fichier corrigé selon le delta officiel.
Le [NOTICE](../../third_party/glib-variant-iter-NOTICE.md) épingle archive, commit,
patch, hashes et inventaire. `verify_glib_backport.py` compare tous les octets.

GLib demeure une dépendance Linux de Tauri ; les builds Windows et WASM ne
doivent pas acquérir de nouvelle bibliothèque native. Aucun renderer, service,
format persistant ou composant canonique n'est ajouté. Le coût attendu de build
et de taille est celui du paquet existant, à vérifier dans le candidat réel.
Le maintien du backport se limite à ce correctif ; supprimer le patch après
qualification d'une pile Tauri/GTK utilisant une version officielle corrigée.

## Preuves exigées et limites

Conserver les cinq crashes du registre, puis les cinq succès du backport dans
des processus distincts, optimisés, core dumps interdits, en code appelant sûr.
Vérifier les graphes réels des trois applications, notices/licences, audit brut,
Clippy et builds Linux, et les contrôles natifs/WASM affectés. Le résultat du
probe corrigé est encore en cours au moment de cette décision ; tout échec
empêche la qualification et doit être corrigé avant clôture.

Une version locale 0.18.5 corrigée n'est pas une release upstream >=0.20.0.
Les sorties de scanners et la preuve du correctif sont rapportées séparément.
Les autres avis de maintenance et les qualifications de distribution/machine
propre gardent leurs exigences ; cette décision ne ferme pas Gate 7.
