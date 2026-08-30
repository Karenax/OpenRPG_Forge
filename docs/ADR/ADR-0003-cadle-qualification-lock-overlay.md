# ADR-0003 — Overlay npm borné pour qualifier le snapshot Cadle

Date : 2026-08-30
Statut : **accepté pour poursuivre la roadmap jusqu’à Gate 4**
Portée : branche orpheline `Rust` uniquement
Complète : `ADR-0002-cadle-upstream-reference.md`

## Contexte

Le snapshot Cadle obligatoire `ea0da62549980816a6a27e08b75808b1f56d70e7` possède un
`package.json` en version `0.6.0`, mais son `package-lock.json` conserve une racine `0.1.0` et omet
cinq dépendances directes. `npm ci` échoue donc avant toute installation.

La vérification du dépôt officiel le 30 août 2026 n’a trouvé aucun correctif : `main` reste sur ce
commit, les tags `v0.6.0` et `v0.6.0-stable` ont le même défaut, et le dernier commit dont le lock est
synchronisé précède le snapshot de 144 commits et 438 fichiers modifiés. Changer de snapshot
supprimerait donc une part substantielle de la référence visuelle retenue.

Deux générations indépendantes, dans deux clones détachés, avec Node 22.22.3 et npm 10.9.8 ont produit
un lock octet pour octet identique :

```text
package.json SHA-256       95952a2f2d4114c619a6c182b610c46d12db4c366aa59292c0a7ffc28650bcfc
lock upstream SHA-256      1d01d9703a7679fecb0ce2f4144eef382c0adc832d0298da19a9efb515af82a3
lock brut généré (CRLF)    2e84d2a98f193fd53a9af0dc68c9cb7e1f0773c1bf2459613824524b6abd7c25
lock suivi normalisé LF    85f9b294da3d67fc698631b4b9903ad9a33214044573db82ed86e4b09d2f1c1f
commande de provenance     npm install --package-lock-only --ignore-scripts --no-audit --no-fund
```

Les deux fichiers bruts générés sous Windows étaient identiques. Git suit leur normalisation UTF-8/LF
pour garantir les mêmes octets sur Windows et Linux. Le lock dérivé corrige la version racine, verrouille les dix dépendances directes et conserve les
versions des 46 entrées déjà présentes. Il ajoute le graphe manquant avec URL registry et intégrité.

## Décision

Gate C distingue désormais deux objets :

1. le **snapshot source canonique**, clone local exact, détaché et propre dans `upstream/cadle/` ;
2. la **baseline Cadle sous overlay Forge**, clone jetable exact dans lequel seul
   `package-lock.json` est remplacé par `upstream/cadle.npm-lock.json`.

Le second objet n’est jamais appelé « installation upstream intacte ». Il sert exclusivement à la
qualification locale. Le script de préparation refuse toute autre différence suivie et revérifie le
clone canonique avant et après. La régénération du lock n’est pas une étape normale : le fichier suivi
et son hash sont l’autorité. Toute nouvelle génération exige une revue et une nouvelle décision.

L’overlay est une métadonnée Forge d’acquisition. Ce n’est ni du code Cadle, ni un asset Cadle, ni une
dépendance des applications Forge. `node_modules`, `dist`, captures, clones jetables et médias Cadle
restent ignorés, locaux et exclus des distributions.

## Sécurité et licences

Le graphe dérivé contient notamment MIT, Apache-2.0, MPL-2.0 et LGPL-3.0-or-later. `npm audit` signale
une vulnérabilité haute sur `sharp` 0.34.5, héritée de libvips, corrigée seulement par une montée
majeure déclarée vers 0.35.4. Gate C conserve ce défaut : elle n’exécute aucun script de cycle de vie,
ne distribue pas ce graphe et n’utilise pas Sharp dans le produit Forge.

Cette décision ne vaut aucune autorisation de redistribuer Cadle, son build, ses dépendances ou ses
assets. Un port de code futur conserve l’attribution fichier par fichier prévue par ADR-0002.

## Conditions de validité

L’exception est valide seulement si :

- le commit, l’arbre, `package.json`, le lock upstream, `LICENSE` et `NOTICE` ont les hashes attendus ;
- le clone canonique reste propre ;
- le clone jetable diffère uniquement par `package-lock.json` ;
- Node/npm et le lock dérivé ont les versions/hashes épinglés ;
- `npm ci --ignore-scripts --no-audit --no-fund`, build et checks qualifiés passent ;
- les preuves GPU sont exécutées sur ce même environnement dérivé ;
- aucun fichier Cadle ou résultat de build n’entre dans Forge ou sa distribution.

Une violation rouvre Gate C. L’arrivée d’un lock upstream officiel synchronisé déclenche une revue :
l’overlay pourra alors être retiré après reproduction complète, sans changer le snapshot source.

## Conséquences

La source Cadle reste intacte et vérifiable, tandis que la référence devient reproductible malgré son
défaut de métadonnées. Le coût assumé est une exception d’acquisition explicitement gouvernée et une
baseline nommée comme dérivée. Rust, Bevy, wgpu, l’autorité des documents Forge et la frontière des
assets ne changent pas.
