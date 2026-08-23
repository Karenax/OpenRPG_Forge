# Politique de licence de l’Asset Vault

## Noyau redistribuable

Les assets artistiques du noyau OpenRPG Forge doivent être CC0 1.0 ou créations originales du projet
explicitement placées sous CC0. Une ressource dont la licence ou la provenance n’est pas vérifiable
reste en quarantaine et ne peut pas être exportée dans un pack public.

Chaque entrée de catalogue conserve au minimum la source, l’identifiant source, la page de l’asset,
la licence, l’URL de licence, la date de vérification, les fichiers téléchargés et leurs hashes.

## Sources actuellement admises

- Poly Haven : assets annoncés CC0 ; l’usage de l’API en direct doit afficher une provenance Poly
  Haven conformément aux conditions de l’API.
- ambientCG : assets annoncés CC0 1.0, attribution facultative.
- Kenney : seuls les packs dont la page officielle déclare explicitement `Creative Commons CC0`
  sont téléchargés ; la page exacte sert de preuve de licence du pack.
- KayKit : seuls les dépôts officiels épinglés et les archives FREE exactes dont la licence interne
  déclare CC0 sont admis.
- Quaternius : seuls les packs Standard/gratuits exacts dont la page officielle et, lorsqu'il est
  fourni, le fichier de licence interne déclarent CC0 sont admis.

Les enregistrements vérifiés sont dans `asset_manifests/sources/`. Cette admission concerne les
assets téléchargés depuis les URLs officielles, pas des miroirs ou redistributions tierces.
Le registre accepté, manuel, refusé ou différé et son gate d’admission sont maintenus dans
`docs/assets/ASSET_SOURCE_GOVERNANCE.md`.

## État d’un asset

`downloaded-original` ou `downloaded-and-extracted` signifie que la provenance, la licence et
l’intégrité sont connues. Cela ne signifie pas que l’asset respecte déjà les conventions de pivot,
échelle, collision, LOD, matériaux ou performance du produit. Seuls les assets normalisés et revus
visuellement pourront recevoir un statut de production.
