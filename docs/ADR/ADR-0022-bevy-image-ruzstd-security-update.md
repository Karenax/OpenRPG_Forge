# ADR-0022 — Décompression KTX2 avec la correction officielle de ruzstd

Date : 13 octobre 2026
Statut : correctif de sécurité ciblé autorisé dans le travail de clôture Gate 3 ; qualification à établir.
Portée : `bevy_image 0.19.1`, dépendance de décompression `ruzstd`, patch Cargo et provenance.

## Contexte

Le contrôle supply de `closure-common-001` refuse `ruzstd 0.8.3` pour
[RUSTSEC-2026-0344](https://rustsec.org/advisories/RUSTSEC-2026-0344.html), publié
le 13 octobre 2026. Un bloc compressé peut produire une allocation excessive ;
la correction officielle est livrée dans `ruzstd >= 0.9.1`.

Le [manifeste Bevy image 0.19.1](https://github.com/bevyengine/bevy/blob/v0.19.1/crates/bevy_image/Cargo.toml)
exige `ruzstd ^0.8.0`. Aucun paquet Bevy/Bevy image 0.19.2 n'est publié lors de
la vérification du registre. Migrer le renderer ne constitue pas un correctif
ciblé ; le manifeste Bevy image 0.20.0 utilise également `^0.8.0`.

## Décision

Conserver Bevy 0.19.1 et ses quinze sources image intactes. Un paquet local
contrôlé de vingt fichiers reprend les octets de l'archive officielle vérifiée,
les deux licences et les deux fichiers de provenance. Seule la dépendance
`ruzstd` du manifeste normalisé est épinglée à `=0.9.1`.

Le patch Cargo racine choisit ce paquet image ; son exclusion des membres du
workspace conserve le périmètre des applications Forge. Le lock doit sélectionner
`ruzstd 0.9.1` du registre avec son checksum officiel. Ni le numéro de version de
Bevy image ni celui de ruzstd ne sont simulés. Aucune règle de sécurité, ignore
ou politique de licence n'est assouplie.

Le [constructeur officiel 0.9.1](https://github.com/KillingSpark/zstd-rs/blob/v0.9.1/ruzstd/src/decoding/streaming_decoder.rs)
et l'implémentation de `std::io::Read` préservent l'appel utilisé par Bevy image.
La bibliothèque reste du Rust pur, avec les features par défaut `hash` et `std`,
une MSRV de 1.87 et la dépendance de checksum `twox-hash` déjà utilisée. Aucun
backend C, moteur supplémentaire ou renderer parallèle n'est ajouté.

## Validation et portée

Les vingt fichiers du paquet ont été vérifiés contre l'archive du registre ; les
quinze sources et les deux licences correspondent aussi au tag officiel Bevy.
Le seul delta du paquet est une ligne du manifeste actif. Les checksums, tailles
et limites sont détaillés dans le [NOTICE](../../third_party/bevy-image-ruzstd-NOTICE.md).

La compilation de l'API native, le contrôle supply, les validations WASM et le
chargement de KTX2 légitimes doivent utiliser le nouveau lock. Les preuves
précédentes portent sur les anciennes sources. Un build ou un contrôle supply
réussi ne prouve ni le replay RPG natif, ni le GPU, ni la CI distante, ni la gate.

La correction upstream borne la sortie par bloc à `min(window, 128 KiB)`.
Elle ne constitue pas une limite totale du contenu décompressé par Forge. Nous
n'appelons pas l'API qui relève le plafond de fenêtre du décodeur.

## Maintenance

Retirer ce patch après adoption et qualification d'une version Bevy compatible
qui dépend d'une version corrigée officielle de ruzstd. Conserver les licences,
le diff borné et la provenance jusque-là. Le patch n'autorise aucune modification
du code Rust upstream sans une nouvelle justification et ses preuves.
