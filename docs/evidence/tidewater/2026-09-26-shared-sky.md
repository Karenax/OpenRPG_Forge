# Ciel dans le moteur partagé

`forge_engine::tidewater_sky` expose le matériau, ses réglages et la création
de la géométrie de fond. Les bindings atmosphère/nuages, le rectangle de 2x2,
le test de profondeur Equal, les ombres et le prépass désactivés sont conservés.

Le shader vérifié est installé dans `Assets<Shader>` sous une identité stable.
Le matériau ne demande plus `sky.wgsl` à un AssetServer filesystem : le même
chemin d'installation peut recevoir les bytes d'un paquet chargé par Studio.
Le réglage actuel lune/étoiles reste dans l'adaptateur de scène du prototype ;
le moteur partagé n'impose pas cette approximation à un futur cycle horaire.
Le calcul des nuages et leur liaison aux matériaux opaques restent à partager.

## Validation

- Clippy strict du prototype (`--no-deps -D warnings`) : passe.
- Build natif du prototype : passe (7 min 44 s).
- Check bibliothèque moteur WASM, sans features par défaut, feature web : passe.
- Formatage ciblé et diff check : passent.
- Capture native avec paquet 003 et workspace initial : exit 0, log sans erreur.

La capture inspectée `native-shared-sky-001.png` est identique à
`native-shared-environment-001.png` : zéro pixel différent et deux buffers
d'ombres identiques. SHA-256 PNG :
`243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78`.
Rapport local : `E:/OpenRPG_Forge/.tidewater-integration/shared-sky-render-proof-001.json`.
Cette capture couvre aussi le module atmosphérique extrait précédemment.
Le processus de capture est terminé ; aucune fenêtre de test laissée ouverte.

La présence du module ne vaut pas raccordement de la scène dans Studio, cycle
jour/nuit source, ni qualification WebGPU exécutée. Aucune publication.
