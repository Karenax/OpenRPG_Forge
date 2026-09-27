# Calculs nuageux dans le moteur partagé

`forge_engine::tidewater_clouds` reçoit les shaders et données vérifiés en mémoire.
Le moteur exécute météo/bornes, ombres, volume, panorama et historique temporel
sur le RenderDevice Bevy, sans dépendance filesystem ou seconde pile wgpu.
Les acquisitions et les readbacks restent dans les exemples de qualification.

L'ordre, les textures, le cache GPU, les buffers immuables et l'alternance des
historiques conservent le comportement existant. La validation précède toute
allocation des sorties : tailles, valeurs finies, sept mipmaps de volume,
120 frames, compatibilité atmosphère, textures et nombre de sorties.
Le code GPU interne ne peut pas être appelé directement hors du module.

## Limites

Il s'agit encore d'une séquence bornée à 120 pas, à résolution FHD fixe.
Les paramètres globaux soleil/irradiance sont encore acquis ; le contrôleur
cloud local et les LUT atmosphériques sont natifs. L'extraction n'établit ni
une boucle interactive illimitée ni l'adoption des ressources dans Studio.
La validation des entrées ne compile pas un shader arbitraire : acquisition,
provenance et qualification des interfaces shader restent nécessaires.

## Vérifications

Check natif et Clippy strict ciblé des exemples nuages et scène passent.
Le check bibliothèque moteur WASM/WebGPU passe également.
L'exécution `--native-stepped` utilise `cloud-controller-inputs-001` et
`cloud-controller-atmosphere-inputs-001`, sortie `native-shared-clouds-001`.
Les dix textures sont identiques à l'octet à `native-cloud-stepped-002`.
`execution.json` confirme 120 pas et 120 passages du graphe ; les rejets de
settings tronqués et de nombre de sorties incohérent passent sans allocation.
Rapport local : `E:/OpenRPG_Forge/.tidewater-integration/shared-clouds-proof-001.json`.

La scène assemblée a été reconstruite, lancée et inspectée.
`native-shared-cloud-scene-001.png` est identique à `native-shared-sky-001.png` :
zéro pixel différent, deux buffers d'ombres identiques, log sans erreur.
SHA-256 PNG : `243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`.
Rapport : `E:/OpenRPG_Forge/.tidewater-integration/shared-cloud-scene-proof-001.json`.
Les processus de qualification ont terminé ; aucune fenêtre de test conservée.
La compilation WASM ne vaut pas encore exécution WebGPU de ces calculs dans
le navigateur.
Aucune publication.
