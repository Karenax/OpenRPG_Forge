# ADR-0005 — Studio web confirmé à l'issue de Gate 0

Date : 2026-08-30
Statut : **accepté pour la continuation après Gate 0**
Portée : branche orpheline `Rust`, décision de shell Studio
Commit de code évalué : `7286740c18fe9afa6cc207e388d5f335495fc650`
Commit de preuve Tauri final : `a7c506f099b50100ad49f3ae6d01d541c9b63d49`
Complète : `ADR-0001-rust-webgpu-greenfield.md`

## Contexte

ADR-0001 retient initialement Tauri 2, Leptos/WASM et un canvas Bevy/WebGPU, avec un Studio natif
Bevy UI/Feathers comme repli si la WebView ou le partage natif/WASM se révèle insuffisant. La Gate 0
doit décider quelle voie continue avant le travail de monde et d'éditeur.

Au commit évalué, les preuves automatisées établissent les propriétés techniques nécessaires à cette
décision :

- le frontend Leptos, le viewport Bevy/WASM et le shell Tauri sont produits depuis le workspace Rust
  épinglé ;
- le runtime natif et le viewport consomment le même `SceneProofDocument` moteur-neutre et son même
  hash ;
- le bundle WebGPU courant atteint une première frame Bevy réelle avant de se déclarer prêt ;
- la sélection traverse la hiérarchie, l'inspecteur et la commande Rust typée sans recréer le canvas,
  dont le compteur de démarrages reste égal à un ;
- le chemin WebGPU indisponible affiche un diagnostic actionnable et reste identifié comme injection
  de preuve lorsqu'il est provoqué par le harness ;
- la capability Tauri n'accorde que la commande Forge requise, et le harness exige à la fois son
  succès et le refus explicite d'une commande fenêtre non accordée ;
- les contrats versionnés de télémétrie couvrent chargement, mémoire WASM, distribution de frame time,
  latence interaction-vers-post-paint, stabilité du canvas et dimensions du viewport ;
- le harness de fenêtre refuse de qualifier automatiquement une capture comme revue humainement.

Ces résultats démontrent la viabilité du partage Rust, de l'intégration WebGPU et de la frontière de
permissions. Ils ne démontrent pas encore la qualité ergonomique ou visuelle finale du Studio.

La preuve finale Tauri au commit `a7c506f099b50100ad49f3ae6d01d541c9b63d49` complète cette
décision par une acquisition HWND reproductible : fenêtre complète `1616 × 939`, WebGPU prêt,
commande Forge autorisée, commande fenêtre explicitement refusée, 120 échantillons et interaction
stable. L’inspection automatisée a aussi relevé quelques glyphes carrés dans l’overlay Bevy ; cette
dette cosmétique reste à confirmer pendant les revues humaines différées et n’est pas masquée.

## Décision

Le shell Studio retenu pour la suite est :

```text
Tauri 2 (backend Rust)
└── Leptos/WASM (présentation)
    └── canvas Bevy/WASM/WebGPU (viewport)
```

Le fallback natif Bevy UI/Feathers n'est pas activé. Une fois les autres conditions de Gate 0
satisfaites ou explicitement dérogées, Gate 1 pourra donc construire et comparer la scène dans le
runtime natif et dans ce viewport partagé.

Cette confirmation est une décision technique de continuation, pas une qualification humaine du
rendu, de la composition, de la lisibilité ou de l'ergonomie. Conformément à l'ordre du propriétaire,
les vérifications humaines de Gate 0 sont différées et doivent rester consignées comme telles. Elles
ne doivent pas être transformées en succès automatisé.

En particulier, toute enveloppe ou tout ledger acquis automatiquement conserve ses valeurs réelles :

```text
review.status = uninspected
human_review_performed = false
proof_status = pending_human_review
qualified = false
```

Le rapport de gate peut enregistrer séparément que le propriétaire autorise le passage administratif
de la condition humaine de Gate 0 avec revue différée. Il ne peut ni modifier ces valeurs, ni affirmer
qu'une personne a inspecté les pixels.

## Revues humaines différées

Les contrôles suivants restent à réaliser et à consigner ultérieurement :

- `G0-HR-001` — inspecter la capture native : scène attendue, overlay lisible, cadrage, absence de
  corruption ou de fenêtre parasite ;
- `G0-HR-002` — inspecter la capture complète Tauri/WebView2 : trois surfaces Studio, viewport visible,
  sélection cohérente, lisibilité et absence d'occlusion ;
- `G0-HR-003` — effectuer un court parcours manuel : redimensionnement, sélection hiérarchie et
  picking, stabilité du canvas, fermeture normale et message WebGPU indisponible.

Ces dettes de revue n'empêchent pas le choix du shell pour la tranche suivante. Elles n'accordent
aucune dérogation à la revue humaine comparative explicitement exigée par Gate 1.

## Frontières de la décision

Cette décision :

- ne qualifie pas une performance issue d'un navigateur automatisé throttlé ;
- ne remplace pas une CI Windows, Linux et WASM réellement exécutée ;
- ne vaut pas validation complète des preuves à plus long terme listées dans ADR-0001, notamment la
  vallée, la cible 60 FPS, la sauvegarde/réouverture et la revue visuelle comparative ;
- n'autorise aucune règle métier JavaScript, aucune seconde source de vérité et aucune dépendance
  runtime envers Cadle ;
- ne change pas le statut `pending_human_review` des captures automatisées.

## Déclencheurs de réexamen

Le fallback natif doit être réévalué par une nouvelle décision si une preuve reproductible montre :

- une impossibilité de lancer le viewport WebGPU dans la WebView2 supportée ;
- une reconstruction du monde ou du canvas lors des rerenders de présentation ;
- une frontière Tauri impossible à borner aux permissions nécessaires ;
- une instabilité, une latence ou un coût mémoire représentatif empêchant le parcours Gate 1 ou Gate 2 ;
- une divergence du document canonique entre runtime et Studio ;
- un rejet humain ultérieur qui révèle un défaut structurel du shell plutôt qu'une correction UI
  localisée.

## Conséquences

La suite conserve une seule pile Studio web-quality à autorité Rust et un viewport partagé avec le
runtime. Le coût immédiat reste la compilation WASM/Tauri et la nécessité de mesurer la WebView sur des
sessions représentatives. Les revues humaines de Gate 0 demeurent une dette visible ; la CI distante
et les autres conditions automatisées de passage restent gouvernées séparément par la roadmap.
