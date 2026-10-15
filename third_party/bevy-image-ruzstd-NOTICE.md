# Bevy image : mise à jour officielle de ruzstd — 13 octobre 2026

Le paquet `bevy_image 0.19.1`, sous licence MIT ou Apache-2.0, est conservé dans
`third_party/bevy_image-0.19.1-ruzstd0.9.1`. Ses quinze fichiers Rust sont inchangés.
Le seul changement au paquet est la dépendance du manifeste normalisé :
`ruzstd = "0.8.0"` devient `ruzstd = "=0.9.1"`. La version Bevy reste `0.19.1` ;
la version de ruzstd est celle du registre officiel, avec son vrai checksum.

Le paquet contient exactement vingt fichiers : quinze sources, le manifeste
normalisé, `Cargo.toml.orig`, `.cargo_vcs_info.json` et les deux licences. Les deux
fichiers de provenance restent les octets du registre ; `Cargo.toml.orig` décrit
donc la dépendance d'origine, pas le manifeste actif modifié. Le `Cargo.lock` du
paquet upstream et le marqueur privé du cache Cargo ne sont pas distribués.

## Provenance

- Archive officielle Bevy image :
  [bevy_image 0.19.1](https://crates.io/crates/bevy_image/0.19.1), SHA-256
  `ca63218585b83f98831214431449e9344b363349dd5cab98dae0b8129f0a0e2f`.
- Révision du paquet : `b56fc29d3016e641754765244b5ba3f9cc504671`,
  chemin `crates/bevy_image`, selon `.cargo_vcs_info.json`.
- Les quinze sources et les deux licences ont également été comparées octet pour
  octet au [tag Bevy v0.19.1](https://github.com/bevyengine/bevy/tree/v0.19.1/crates/bevy_image).
- Archive officielle [ruzstd 0.9.1](https://crates.io/crates/ruzstd/0.9.1), licence
  MIT, SHA-256 `69e4ce4a039f42bf25920b95667e4d789e98749ab11bbb8d2c2936f788421bb5`.

Le reçu local `closure-ruzstd-security-001/vendor-source-receipt.json` conserve
les hashes des vingt fichiers upstream et modifiés. Le reçu
`official-archive-receipt.json` conserve l'URL, la taille et le checksum du
téléchargement officiel de ruzstd. Les fichiers sont sous
`.gate3-evidence/20261013` dans le dossier parent du checkout Rust.

## Raison et limites

[RUSTSEC-2026-0344](https://rustsec.org/advisories/RUSTSEC-2026-0344.html) affecte
ruzstd 0.8.3, utilisé par la décompression KTX2 de Bevy. La correction officielle
[d3907c9](https://github.com/KillingSpark/zstd-rs/commit/d3907c960ca4a1aa44cc38340137c5679dd98f5a)
borne la sortie de chaque bloc. Le registre n'offrait pas de Bevy ou Bevy image
0.19.2 au moment de la décision. Le manifeste 0.19.1 exige `^0.8.0`, ce qui empêche
une simple mise à jour du lock vers la version corrigée.

L'appel utilisé par Bevy, `decoding::StreamingDecoder::new` puis
`std::io::Read::read_to_end`, reste disponible dans la version officielle 0.9.1.
Le backend Rust, les formats, les features et la pile graphique restent ceux du
produit. Aucun code de décompression n'est réécrit, aucune version fictive ni
exception d'advisory n'est introduite. Voir [ADR-0022](../docs/ADR/ADR-0022-bevy-image-ruzstd-security-update.md).

La préparation du patch et la compatibilité des signatures ne prouvent pas une
compilation native/WASM, un décodage KTX2, la performance ou la clôture Gate 3.
Les validations sur les nouvelles sources doivent être enregistrées séparément.
Le patch pourra être retiré après adoption et qualification d'une version Bevy
compatible utilisant une version corrigée officielle de ruzstd.
