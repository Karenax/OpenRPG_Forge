/* Local presentation only. No engine, gameplay simulation or network requests. */
(() => {
  'use strict';
  const content = window.FORGE_CONTENT;
  const core = window.ForgeCodex;
  const main = document.getElementById('main');
  const announcer = document.getElementById('announcer');
  const motionButton = document.getElementById('motion');
  const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
  const groups = ['Premiers pas', 'Personnage et compétences', 'Talents', 'Règles de jeu', 'Aides de jeu', 'Annexes techniques'];
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const norm = core.normalize;
  const clean = value => String(value).replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/^\|[\s:|\-]+\|$/gm,'').replace(/^(?:```|~~~).*$/gm,'').replace(/\|/g,' · ').replace(/[`*#]/g, '').replace(/\s+/g, ' ').trim();
  const flatten = value => typeof value === 'string' ? value : Array.isArray(value) ? value.map(flatten).join(' ') : value && typeof value === 'object' ? Object.values(value).map(flatten).join(' ') : String(value ?? '');
  const url = (page, id = '', params = {}) => '#' + page + (id ? '/' + encodeURIComponent(id) : '') + (Object.keys(params).length ? '?' + new URLSearchParams(params) : '');
  const readStored = () => { try { return JSON.parse(localStorage.getItem('forge-aube.preferences.v1') || '{}'); } catch { return {}; } };
  const saved = readStored();
  const preferences = core.preferences(saved);
  const persist = () => { try {localStorage.setItem('forge-aube.preferences.v1', JSON.stringify(preferences)); return true;} catch {return false;} };
  let route, searchTimer, bestiaryTab = 'portrait';
  const labels = {strength:'Force',dexterity:'Dextérité',constitution:'Constitution',intelligence:'Intelligence',willpower:'Volonté',perception:'Perception',presence:'Présence',vitality:'Vitalité',stamina:'Endurance',mana:'Mana',balance:'Équilibre',focus:'Concentration',nature:'Nature',family:'Famille',rarity:'Rareté',activity:'Activité',social:'Vie sociale',threat:'Menace',threat_reason:'Pourquoi cette menace',dimensions:'Dimensions',mass_kg:'Masse (kg)',locomotion:'Déplacements',center_of_gravity:'Appuis et équilibre',silhouette:'Silhouette',anatomy_zones:'Zones anatomiques',zone:'Zone',description:'Description',protection:'Protection',diet:'Alimentation',lifecycle:'Cycle de vie',reproduction:'Reproduction',behavior:'Comportement',niche:'Rôle écologique',environmental_impact:'Effets sur le milieu',objectives:'Objectifs',fears:'Craintes',morale:'Moral',escalation:'Montée de tension',retreat:'Retraite',attributes:'Attributs',skills:'Compétences',resources:'Ressources',movement:'Mobilité',protections:'Protections',attacks:'Attaques',abilities:'Capacités',resistances:'Résistances',vulnerabilities:'Vulnérabilités',immunities:'Immunités',environmental_interactions:'Interactions avec le milieu',subject:'Sujet',mechanism:'Mécanisme',input:'Situation',result:'Conséquence',chain:'Chaîne causale',roles:'Rôles',telegraphs:'Signes annonciateurs',noncombat_options:'Éviter le combat',complications:'Complications',arena:'Terrain de rencontre',sequence:'Déroulement',causal_opportunities:'Possibilités offertes par le décor',aftermath:'Après la rencontre',status:'État du savoir',difficulty:'Difficulté',fact:'Observation',skill_id:'Compétence',attribute_id:'Attribut',impact:'Impact',reach:'Portée',stamina_cost:'Coût en Endurance',preparation:'Préparation',telegraph:'Signe annonciateur',effect:'Effet',counterplay:'Comment réagir',visibility:'Visibilité',name:'Nom',kind:'Type',etabli:'Établi',etablie:'Établie',suppose:'Supposé',supposee:'Supposée',secret:'Secret',secrete:'Secrète',moderee:'Modérée',elevee:'Élevée',faible:'Faible',extreme:'Extrême'};
  const skillPages = new Map();
  for (const page of content.guide) {
    const match = page.title.match(/\(([^)]+)\)/);
    if (match) {skillPages.set(match[1], page); labels[match[1]] = page.title.replace(/^\d+\. /,'').replace(/\s*\([^)]*\)/,'');}
  }
  const label = value => labels[value] || String(value).replaceAll('_',' ');
  Object.assign(labels,{senses:'Sens',communication:'Communication',reglages:'Réglages',aventures:'Aventures',multijoueur:'Multijoueur','a-propos':'Sources et limites',forte:'Forte',critique:'Critique',majeure:'Majeure'});
  const guideIndex = content.guide.map(page => ({...page, search:norm(page.text), excerpt:clean(page.text.slice(page.title.length))}));
  const talentIndex = content.talents.map(talent=>({...talent,search:norm(flatten(talent)),excerpt:talent.effect,group:'Talents',kind:'talent'}));
  const publicCreature = core.publicCreature;
  const creaturesIndex = content.creatures.map(creature => ({...creature, search:norm(family(creature)+' '+flatten(publicCreature(creature))+' '+Object.keys(creature.rules.skills).map(label).join(' ')), excerpt:creature.ecology.behavior}));
  function family(creature) { return core.family(creature); }
  const families = [...new Set(creaturesIndex.map(family))];
  const expansionIds = new Set(content.bestiaryExpansion?.entry_ids || []);
  const plannedCount = creaturesIndex.filter(item=>item.illustration_status==='planned').length;
  const plateCount = creaturesIndex.reduce((count,item)=>count+item.plates.length,0);
  const illustrationSummary = plannedCount ? `${plateCount} planches et ${plannedCount} fiches à illustrer` : `${plateCount} planches — toutes les créatures sont illustrées`;
  const edition = `<p class="edition-note">Édition de conception · Guide v0.1 / Bestiaire v${escape(content.bestiaryVersion)}. Ces règles et créatures sont des propositions documentaires, pas une preuve de disponibilité dans le jeu.</p>`;
  function portrait(creature,hero=false) {
    const plate=creature.plates[0];
    if(plate) {
      const img=`<img width="1024" height="1536" src="assets/bestiary/${escape(plate.image)}" alt="${escape(plate.alt)}" ${hero?'':'loading="lazy"'}>`;
      return hero?`<figure><a href="assets/bestiary/${escape(plate.image)}" target="_blank" rel="noopener" aria-label="Voir le portrait complet">${img}</a></figure>`:img;
    }
    return `<div class="planned-portrait ${hero?'planned-hero':''}"><span class="plate-ornament" aria-hidden="true">◇</span><span class="plate-family">${escape(family(creature))}</span><strong>Planche à illustrer</strong><span>${hero?escape(creature.physique.silhouette):'Horizons sauvages · Fiche de terrain'}</span></div>`;
  }
  function applyPreferences() {
    const paused = !preferences.motion || reducedMotion.matches;
    document.body.classList.toggle('paused', paused);
    motionButton.textContent = paused ? 'Fond au repos' : 'Fond animé';
    motionButton.setAttribute('aria-pressed', String(!paused));
    motionButton.setAttribute('aria-label', reducedMotion.matches ? 'Animations réduites selon votre système' : paused ? 'Activer la légère animation du fond' : 'Mettre la légère animation du fond en pause');
    document.querySelectorAll('.article-body').forEach(el => el.style.setProperty('--reading-scale',preferences.font/100));
    const size=document.getElementById('reading-size');if(size)size.textContent=preferences.font+' %';
    document.querySelectorAll('[data-font]').forEach(button=>button.disabled=Number(button.dataset.font)<0?preferences.font<=100:preferences.font>=120);
  }
  motionButton.addEventListener('click', () => {preferences.motion = !preferences.motion;persist();applyPreferences();});
  reducedMotion.addEventListener('change', applyPreferences);
  document.querySelector('.skip-link').addEventListener('click',event=>{event.preventDefault();main.focus();main.scrollIntoView({behavior:'instant'});});
  document.addEventListener('visibilitychange', () => document.body.classList.toggle('background-hidden',document.hidden));
  function parseRoute() {
    const fragment=location.hash.slice(1), separator=fragment.indexOf('?');
    const raw = separator<0?[fragment]:[fragment.slice(0,separator),fragment.slice(separator+1)];
    const [page = 'accueil', encoded = ''] = raw[0].split('/');
    let id; try {id = decodeURIComponent(encoded);} catch {id = '';}
    const params=new URLSearchParams(raw[1] || '');
    if(params.has('q'))params.set('q',params.get('q').slice(0,180));
    return {page:page || 'accueil',id,params};
  }
  function home() {
    return `<section class="home"><div><p class="eyebrow">L’Aube des royaumes</p><h1>Chaque monde<br>commence <em>ici.</em></h1><p class="intro">Préparez votre aventure. Découvrez les règles,<br>les créatures et les chemins qui s’offrent à vous.</p><div class="home-actions"><a class="button primary" href="#aventures">Choisir une aventure <span aria-hidden="true">→</span></a><a class="button secondary" href="#guide">Préparer mon personnage</a></div><p class="home-note">Le jeu sera accessible après raccordement du lanceur au moteur.</p></div><nav class="home-library" aria-label="Préparer l’aventure"><a class="library-link" href="#guide"><span class="number">01</span><span><strong>Guide du joueur</strong><small>Personnage, compétences, talents et règles</small></span><span aria-hidden="true">↗</span></a><a class="library-link" href="#bestiaire"><span class="number">02</span><span><strong>Le bestiaire</strong><small>${creaturesIndex.length} créatures, leurs milieux et leurs secrets</small></span><span aria-hidden="true">↗</span></a><a class="library-link" href="#multijoueur"><span class="number">03</span><span><strong>Partir en compagnie</strong><small>Préparer une aventure à plusieurs</small></span><span aria-hidden="true">↗</span></a></nav></section>`;
  }
  function sidebar(type) {
    const guide = type === 'guide';
    const categories = guide ? groups : families;
    const current = route.id ? (guide ? guideIndex.find(item=>item.id===route.id) : creaturesIndex.find(item=>item.creature_id===route.id)) : null;
    const selected = route.params.get('g') || (current ? (guide ? current.group : family(current)) : '');
    const view = route.params.get('view') || '';
    const query = route.params.get('q') || '';
    const title = guide ? 'Guide du joueur' : 'Le bestiaire';
    const wave = !guide && route.params.get('wave')==='new';
    const items = guide ? guideIndex : wave ? creaturesIndex.filter(item=>expansionIds.has(item.creature_id)) : creaturesIndex;
    const links = categories.map(category => {
      const count = items.filter(item => (guide ? item.group : family(item)) === category).length;
      if(wave&&!count)return '';
      return `<a href="${url(type,'',{g:category,...(query ? {q:query} : {}),...(wave?{wave:'new'}:{})})}" ${selected===category?'aria-current="page"':''}><span>${escape(category)}</span><small>${count}</small></a>`;
    }).join('');
    const shortcuts=guide?`<a href="#guide" ${!selected&&!view&&!query&&!route.id?'aria-current="page"':''}>Commencer ici</a><a href="${url(type,'',{view:'skills',...(query?{q:query}:{})})}" ${view==='skills'?'aria-current="page"':''}>Les compétences <small>42</small></a><a href="${url(type,'',{view:'talents',...(query?{q:query}:{})})}" ${view==='talents'?'aria-current="page"':''}>Catalogue des talents <small>100</small></a>`:`<a href="${url(type,'',{wave:'new',...(query?{q:query}:{})})}" ${wave&&!selected?'aria-current="page"':''}>Horizons sauvages <small>+${expansionIds.size}</small></a>`;
    const localItems=current?(guide?guideIndex.filter(item=>item.group===current.group):creaturesIndex.filter(item=>family(item)===family(current))):[];
    const local=localItems.length?`<details class="local-navigation" open><summary>${guide?'Dans ce chapitre':'Dans cette famille'}</summary><nav aria-label="${guide?'Rubriques du chapitre':'Créatures de la famille'}">${localItems.map(item=>`<a href="${url(type,item.id||item.creature_id)}" ${(item.id||item.creature_id)===route.id?'aria-current="page"':''}>${escape(item.displayTitle||item.title||item.name)}</a>`).join('')}</nav></details>`:'';
    return `<aside class="sidebar"><div class="sidebar-inner"><h2>${title}</h2><p class="caption">${guide?'Les règles, à portée de main.':'Observer. Comprendre. Survivre.'}</p><label class="search-label" for="codex-search">Rechercher dans ${guide?'le guide':'le bestiaire'}</label><div class="search-wrap"><input id="codex-search" type="search" maxlength="180" autocomplete="off" placeholder="${guide?'Compétence, combat, repos…':'Nom, habitat, capacité…'}" value="${escape(query)}"><button class="clear-search" id="clear-search" aria-label="Effacer la recherche" ${query?'':'hidden'}>×</button></div><p id="result-count" class="result-count" role="status"></p><details class="navigation-fold" open><summary>Navigation du ${guide?'guide':'bestiaire'}</summary><nav class="category-nav" aria-label="${guide?'Chapitres du guide':'Familles du bestiaire'}">${shortcuts}<a href="${url(type,'',{...(guide?{view:'all'}:{}),...(query?{q:query}:{})})}" ${!selected&&!wave&&(!guide||view==='all')?'aria-current="page"':''}>${guide?'Toutes les rubriques':'Toutes les créatures'}<small>${guide?items.length:creaturesIndex.length}</small></a>${links}</nav></details>${local}<div class="sidebar-divider"></div><a class="cross-link" href="${guide?'#bestiaire':'#guide'}">${guide?'Consulter le bestiaire':'Consulter le guide du joueur'} →</a><a class="cross-link" href="#accueil">← Retour à l’accueil</a></div></aside>`;
  }
  function breadcrumbs(type, title) {
    return `<nav class="breadcrumb" aria-label="Fil d’Ariane"><a href="#accueil">Accueil</a><span aria-hidden="true">/</span><a href="#${type}">${type==='guide'?'Guide du joueur':'Bestiaire'}</a>${title?`<span aria-hidden="true">/</span><span>${escape(title)}</span>`:''}</nav>`;
  }
  function snippet(text, query) {
    const plain = clean(text);
    const position = core.ranges(plain,query)[0]?.[0] ?? -1;
    const start = Math.max(0, position - 50);
    return core.highlighted((start?'…':'') + plain.slice(start,start+190) + (plain.length>start+190?'…':''),query);
  }
  function matching() {
    const guide = route.page === 'guide', category=route.params.get('g'), query=route.params.get('q') || '';
    const view=route.params.get('view');
    let items=guide?(view==='talents'?talentIndex:guideIndex):creaturesIndex;
    if(!guide&&route.params.get('wave')==='new')items=items.filter(item=>expansionIds.has(item.creature_id));
    if(guide&&view==='skills')items=items.filter(item=>item.skillId);
    if(guide&&view!=='all'&&category!=='Annexes techniques')items=items.filter(item=>item.group!=='Annexes techniques');
    return core.rank(items.filter(item=>(!category||(guide?item.group:family(item))===category)&&(!route.params.get('threat')||item.classification?.threat===route.params.get('threat'))),query);
  }
  function guideLink(title,text=title){const page=guideIndex.find(item=>item.title===title);return page?`<a href="${url('guide',page.id)}">${escape(text)} →</a>`:'';}
  function guideOverview(){
    const descriptions={'Premiers pas':'La promesse du jeu, les attributs et la règle commune.','Personnage et compétences':'Créer son personnage, progresser par la pratique et explorer les 42 compétences.','Talents':'Conditions, choix, doctrines et 100 talents sans classes imposées.','Règles de jeu':'Combat, magie, exploration, relations, équipement et repos.','Aides de jeu':'Feuille de personnage, références rapides et glossaire.'};
    return `${breadcrumbs('guide')}<p class="eyebrow">Le compagnon de vos aventures</p><h1>Apprendre. Essayer.<br>Devenir.</h1><p class="lede">Pas de classe à choisir pour toujours. Votre personnage se construit par sa pratique, ses rencontres et ses décisions. Commencez par l’essentiel, puis consultez les règles quand vous en avez besoin.</p><section class="learning-path" aria-label="Parcours de découverte"><h2>Votre première aventure</h2><ol><li><span>01</span><div><strong>Façonner votre personnage</strong><p>Attributs, compétences initiales et équipement.</p>${guideLink('Créer son personnage','Suivre la création')}</div></li><li><span>02</span><div><strong>Comprendre une action</strong><p>Quand un test est utile, ce qui s’ajoute au résultat.</p>${guideLink('La règle commune','Lire la règle commune')}</div></li><li><span>03</span><div><strong>Choisir une approche</strong><p>Observer, préparer, agir et accepter les conséquences.</p>${guideLink('La boucle de jeu','Découvrir la boucle de jeu')}</div></li></ol></section><div class="quick-links">${guideLink('Référence rapide','Pendant la partie')}${guideLink('Glossaire','Comprendre un terme')}<a href="#guide?view=skills">42 compétences →</a><a href="#guide?view=talents">100 talents →</a></div><h2 class="section-heading">Explorer le guide</h2><div class="article-grid">${Object.entries(descriptions).map(([name,description])=>`<a class="article-card" href="${url('guide','',{g:name})}"><span class="category">${guideIndex.filter(item=>item.group===name).length} rubriques</span><h2>${escape(name)}</h2><p>${escape(description)}</p><span class="read">Ouvrir le chapitre →</span></a>`).join('')}</div><aside class="editorial-note"><strong>Version de conception, pas tutoriel du runtime</strong><p>Les règles décrivent la cible du jeu. Les points ambigus relevés lors de la relecture sont signalés dans les rubriques concernées, sans changer leurs valeurs.</p><a href="${url('guide','',{g:'Annexes techniques'})}">Annexes d’implémentation et scénarios de référence →</a></aside>${edition}`;
  }
  function listing() {
    const guide=route.page==='guide', query=route.params.get('q')||'', category=route.params.get('g');
    const view=route.params.get('view');
    if(guide&&!query&&!category&&!view)return guideOverview();
    const results=matching();
    const heading=query?`Résultats pour « ${query} »`:category || (guide?(view==='talents'?'Cent façons d’agir.':view==='skills'?'La pratique vous transforme.':'Toutes les rubriques.'):route.params.get('wave')==='new'?'Horizons sauvages.':'Les habitants de l’inconnu.');
    const noun=guide?(view==='talents'?'talent':view==='skills'?'compétence':'rubrique'):'créature';
    const intro=query?`${results.length} ${noun}${results.length>1?'s':''} ${noun==='talent'?'trouvé':'trouvée'}${results.length>1?'s':''}${category?' dans '+category:''}.`:guide?(view==='talents'?'Comparez les effets et les prérequis avant de choisir. Les valeurs séparées par des barres correspondent aux rangs successifs du talent.':'Un personnage sans classe, façonné par ses actes. Retrouvez ici le contenu du manuel, organisé pour une consultation pendant le jeu.'):`${creaturesIndex.length} créatures, ${illustrationSummary}. Habitudes, indices, réactions et possibilités de rencontre. Les secrets se révèlent uniquement à votre demande.`;
    const context=Object.fromEntries([...route.params].filter(([key])=>['g','q','view','threat','p','wave'].includes(key)));
    const pageCount=Math.max(1,Math.ceil(results.length/18));
    const pageNumber=Math.min(pageCount,Math.max(1,Number.parseInt(route.params.get('p'),10)||1));
    const cards = results.slice((pageNumber-1)*18,pageNumber*18).map(item => guide ? `<a class="article-card" href="${url('guide',item.pageId||item.id,{...context,...(item.kind==='talent'?{s:item.id}:{})})}"><span class="category">${escape(item.category||item.group)}${item.kind==='talent'?' · Talent':''}</span><h2>${core.highlighted(item.displayTitle||item.title,query)}</h2><p>${snippet(item.excerpt,query)}</p>${item.requirements?`<p class="requirements"><strong>Prérequis</strong> ${escape(item.requirements)}</p>`:''}<span class="read">${item.kind==='talent'?'Voir le talent':'Lire la rubrique'} →</span></a>` : `<a class="article-card creature-card" href="${url('bestiaire',item.creature_id,context)}">${portrait(item)}<div class="card-copy"><span class="category">${escape(family(item))}${item.major?' · Dossier majeur':''}${expansionIds.has(item.creature_id)?' · Nouvelle fiche':''}</span><h2>${core.highlighted(item.name,query)}</h2><p>${snippet(query?flatten(publicCreature(item)):item.subtitle,query)}</p><span class="tag">Menace ${escape(label(item.classification.threat))}</span><span class="read">Ouvrir la fiche →</span></div></a>`).join('');
    const pagination=pageCount>1?`<nav class="pagination" aria-label="Pages des résultats">${pageNumber>1?`<a href="${url(route.page,'',{...context,p:pageNumber-1})}">← Page précédente</a>`:'<span></span>'}<span>Page ${pageNumber} sur ${pageCount} · ${results.length} entrées</span>${pageNumber<pageCount?`<a href="${url(route.page,'',{...context,p:pageNumber+1})}">Page suivante →</a>`:'<span></span>'}</nav>`:'';
    const threats=!guide?`<div class="catalog-tools"><label for="threat-filter">Niveau de menace</label><select id="threat-filter"><option value="">Tous les niveaux</option>${[...new Set(creaturesIndex.map(c=>c.classification.threat))].map(value=>`<option value="${escape(value)}" ${route.params.get('threat')===value?'selected':''}>${escape(label(value))}</option>`).join('')}</select><span>La menace ne signifie pas hostilité.</span></div>`:'';
    return `${breadcrumbs(route.page)}<p class="eyebrow">${guide?'Le savoir du voyageur':'Carnet de terrain · Volume I'}</p><h1>${escape(heading)}</h1><p class="lede">${escape(intro)}</p>${guide&&query&&!category&&view!=='all'?'<p class="search-scope">Recherche dans le guide joueur. Choisissez « Toutes les rubriques » pour inclure les annexes techniques.</p>':''}${!guide&&route.params.get('wave')!=='new'?`<aside class="expansion-note"><div><span class="eyebrow">Extension · ${expansionIds.size} nouvelles créatures</span><strong>Horizons sauvages</strong><p>Des dunes aux ossuaires, des sources aux cimes. Des créations Forge originales, avec leurs milieux, leurs intentions et leurs contre-jeux.</p></div><a href="${url('bestiaire','',route.params.get('wave')==='new'?{}:{wave:'new'})}">${route.params.get('wave')==='new'?'Tout le bestiaire':'Explorer les nouveautés'} →</a></aside>`:''}${threats}${results.length?`<div class="article-grid">${cards}</div>${pagination}`:'<div class="empty"><strong>Aucun résultat.</strong><br>Essayez un autre mot ou retirez le filtre de chapitre / famille.<br><button id="reset-search">Afficher tout</button></div>'}${edition}`;
  }
  function guideArticle(page) {
    const readingOrder=guideIndex.filter(item=>page.group==='Annexes techniques'?item.group==='Annexes techniques':item.group!=='Annexes techniques');
    const index=readingOrder.findIndex(item=>item.id===page.id),prev=readingOrder[index-1],next=readingOrder[index+1];
    const params=Object.fromEntries(route.params);
    return `${breadcrumbs('guide',page.group)}${returnLink()}<p class="eyebrow">${escape(page.domain||page.group)}</p><h1>${escape(page.displayTitle||page.title)}</h1>${readerTools()}${editorialNotes(page.editorialNotes)}${page.domainIntro?`<aside class="domain-intro"><strong>${escape(page.domain)}</strong><p>${escape(page.domainIntro)}</p></aside>`:''}${page.toc.length?`<details class="page-toc"><summary>Dans cette rubrique · ${page.toc.length} sections</summary><ul>${page.toc.map(item=>`<li><a href="${url('guide',page.id,{...params,s:item.id})}">${escape(item.title)}</a></li>`).join('')}</ul></details>`:''}<article class="article-body">${page.html}</article><nav class="article-nav" aria-label="Lecture du guide">${prev?`<a href="${url('guide',prev.id)}">← ${escape(prev.displayTitle||prev.title)}</a>`:'<span></span>'}${next?`<a href="${url('guide',next.id)}">${escape(next.displayTitle||next.title)} →</a>`:''}</nav><details class="source-details"><summary>Source documentaire et adaptation</summary><p>${escape(page.source)}</p>${page.skillId?`<p>Identifiant de référence : <code>${escape(page.skillId)}</code></p>`:''}<p>Découpage et présentation adaptés à l’écran. Les corrections éditoriales sont signalées au-dessus du texte ; les sources canoniques restent inchangées.</p></details>${edition}`;
  }
  function returnLink(){const params=Object.fromEntries([...route.params].filter(([key])=>['g','q','view','threat','p','wave'].includes(key)));return `<a class="back-results" href="${url(route.page,'',params)}">← ${Object.keys(params).length?'Retour aux résultats':'Retour au catalogue'}</a>`;}
  function readerTools(){return `<div class="reader-tools" aria-label="Taille de lecture"><button data-font="-10" aria-label="Réduire la taille du texte">A−</button><span id="reading-size" aria-live="polite">${preferences.font} %</span><button data-font="10" aria-label="Agrandir la taille du texte">A+</button></div>`;}
  function editorialNotes(notes){return notes?.length?`<aside class="editorial-note"><strong>Repères de cette édition</strong><ul>${notes.map(note=>`<li>${escape(note)}</li>`).join('')}</ul></aside>`:'';}
  function valueHTML(value) {
    if (Array.isArray(value)) return '<ul>'+value.map(item=>'<li>'+valueHTML(item)+'</li>').join('')+'</ul>';
    if (value && typeof value === 'object') return '<dl class="raw-tree">'+Object.entries(value).map(([key,val])=>`<dt>${escape(label(key))}</dt><dd>${valueHTML(val)}</dd>`).join('')+'</dl>';
    return escape(typeof value==='string'?label(value):value);
  }
  const section = (title, value) => value && (!Array.isArray(value)||value.length) ? `<section><h2>${escape(title)}</h2>${typeof value==='string'?'<p>'+escape(value)+'</p>':valueHTML(value)}</section>` : '';
  const facts = values => `<dl class="facts">${Object.entries(values).map(([key,value])=>`<div class="fact"><dt>${escape(label(key))}</dt><dd>${escape(Array.isArray(value)?value.join(' – '):label(value))}</dd></div>`).join('')}</dl>`;
  function statTable(values) {
    return '<div class="table-scroll"><table><thead><tr><th>Caractéristique</th><th>Valeur de référence</th></tr></thead><tbody>'+Object.entries(values).map(([key,val])=>`<tr><td>${skillPages.has(key)?`<a href="${url('guide',skillPages.get(key).id)}">${escape(label(key))} ↗</a>`:escape(label(key))}</td><td>${escape(val)}</td></tr>`).join('')+'</tbody></table></div>';
  }
  function ability(item,creature) {
    const rank=creature.rules.skills[item.skill_id];
    return `<section class="ability"><h3>${escape(item.name)}</h3><div class="tag-row"><span class="tag">${escape(label(item.visibility))}</span><span class="tag">${skillPages.has(item.skill_id)?`<a href="${url('guide',skillPages.get(item.skill_id).id)}">${escape(label(item.skill_id))} ↗</a>`:escape(label(item.skill_id))} · ${escape(label(item.attribute_id))}</span><span class="tag">${item.kind==='attack'?'Attaque':'Capacité'}</span></div><p class="stat-hint">Compétence : ${rank===undefined?'valeur non précisée dans la source':escape(rank)+' / 100'}.</p>${facts({impact:item.impact,reach:item.reach,stamina_cost:item.stamina_cost,preparation:item.preparation})}<p><strong>À observer :</strong> ${escape(item.telegraph)}</p><p>${escape(item.effect)}</p><p class="counter"><strong>Comment réagir :</strong> ${escape(item.counterplay)}</p></section>`;
  }
  function creatureBody(source) {
    const creature=publicCreature(source);
    if (bestiaryTab==='portrait') return facts({activity:creature.classification.activity,social:creature.classification.social})+section('Comprendre la menace',creature.classification.threat_reason)+section('Milieux et traces',creature.habitats)+section('Reconnaître sa présence',creature.signs)+section('Écologie',creature.ecology)+section('Morphologie',{...creature.physique,mass_kg:creature.physique.mass_kg.join(' à ')+' kg'})+section('Perception et communication',{senses:creature.senses,communication:creature.communication});
    if (bestiaryTab==='rencontre') return section('Comprendre ses intentions',creature.demeanor)+section('Rencontrer sans combattre',creature.encounter_use.noncombat_options)+section('Signes annonciateurs',creature.encounter_use.telegraphs)+section('Complications',creature.encounter_use.complications)+section('Rôles dans la rencontre',creature.encounter_use.roles)+section('Récupération',creature.salvage);
    if (bestiaryTab==='combat') return `<p class="notice">Valeurs de conception : attributs sur 10, compétences sur 100, ressources maximales. « Supposée » indique une hypothèse documentaire, pas un savoir acquis en partie.</p><h2>Ressources et attributs</h2>${statTable(creature.rules.resources)}${statTable(creature.rules.attributes)}<h2>Compétences</h2>${statTable(creature.rules.skills)}${section('Mobilité',creature.rules.movement)}${section('Protections',creature.rules.protections)}<h2>Attaques et capacités observables</h2>${[...creature.rules.attacks,...creature.rules.abilities].map(item=>ability(item,source)).join('')}${section('Résistances',creature.rules.resistances)}${section('Vulnérabilités',creature.rules.vulnerabilities)}${section('Immunités',creature.rules.immunities)}${section('Interactions avec le milieu',creature.rules.environmental_interactions)}`;
    const known = creature.field_knowledge.filter(item=>item.status!=='secret');
    const secrets=source.field_knowledge.filter(item=>item.status==='secret');
    const knowledge=items=>items.map(item=>`<section class="ability"><div class="tag-row"><span class="tag">${escape(label(item.status))}</span><span class="tag">${escape(label(item.skill_id))} · Difficulté ${item.difficulty}</span></div><p>${escape(item.fact)}</p></section>`).join('');
    return `<h2>Ce que l’on sait</h2><p class="stat-hint">Établi : savoir confirmé dans la fiche. Supposé : piste à vérifier. Secret : information volontairement masquée.</p>${knowledge(known)}<details class="spoilers"><summary>Révéler les secrets et le dossier complet</summary><p>Attention : ces informations peuvent dévoiler les surprises d’une rencontre. Elles ne correspondent pas à des découvertes enregistrées dans une partie. Ce repli de lecture n’est pas un contrôle d’accès aux fichiers locaux.</p>${knowledge(secrets)}${[...source.rules.attacks,...source.rules.abilities].filter(item=>item.visibility==='secrete').map(item=>ability(item,source)).join('')}${core.secretSupplements(source).map(item=>section(item.label,item.value)).join('')}${source.major_dossier?section('Dossier majeur',source.major_dossier):''}${source.plates.slice(1).map(plate=>`<figure><a href="assets/bestiary/${escape(plate.image)}" target="_blank" rel="noopener" aria-label="Ouvrir la planche complète : ${escape(plate.alt)}"><img loading="lazy" width="1024" height="1536" src="assets/bestiary/${escape(plate.image)}" alt="${escape(plate.alt)}"></a><figcaption>${escape(plate.caption)} · Cliquez pour voir la planche complète.</figcaption></figure>`).join('')}</details>`;
  }
  function creatureArticle(creature) {
    const notes=[];
    if(creature.creature_id==='creature.brumaille')notes.push('Le terme « Balance » est harmonisé en « Équilibre ». La mobilité est conservée telle quelle : sa durée n’est pas précisée. Le terme source « Vigueur » reste à clarifier ; aucune dépense n’est recalculée.');
    if(creature.creature_id==='creature.sangsue-d-orage')notes.push('La récupération évoque une « solution saline non conductrice » : formulation incohérente à clarifier dans la conception, pas une consigne pratique validée.');
    if(/entries[\\/]((09|10|11|12)-)/.test(creature.source))notes.push('Cette fiche contient des traces de conception historiques (runtime, scripts ou blueprints). Elles ne constituent ni un pouvoir supplémentaire ni une preuve de disponibilité dans le jeu.');
    return `${breadcrumbs('bestiaire',family(creature))}${returnLink()}<div class="creature-hero">${portrait(creature,true)}<div><p class="eyebrow">${creature.major?'Dossier majeur':expansionIds.has(creature.creature_id)?'Horizons sauvages · Nouvelle fiche':'Carnet de terrain'}</p><h1>${escape(creature.name)}</h1><p class="lede">${escape(creature.subtitle)}</p><div class="tag-row"><span class="tag">${escape(family(creature))}</span><span class="tag">Menace ${escape(label(creature.classification.threat)).toLowerCase()}</span></div><p class="lede">${escape(creature.classification.rarity)}</p></div></div>${creature.plates[0]?`<p class="lede">${escape(creature.plates[0].caption)}</p>`:''}${editorialNotes(notes)}${readerTools()}<nav class="reader-tabs" aria-label="Sections de la créature">${[['portrait','Portrait & habitat'],['rencontre','Rencontre'],['combat','Combat & capacités'],['savoir','Savoirs & secrets']].map(([id,name])=>`<a href="${url('bestiaire',creature.creature_id,{...Object.fromEntries(route.params),tab:id})}" ${id===bestiaryTab?'aria-current="page"':''}>${name}</a>`).join('')}</nav><article id="creature-body" class="article-body">${creatureBody(creature)}</article><details class="source-details"><summary>Provenance historique — peut contenir des révélations</summary><p>${escape(creature.origin_notes)}</p><p>${escape(creature.source)}</p></details>${edition}`;
  }
  function notFound(type) {return `${breadcrumbs(type)}<h1>Cette fiche est introuvable.</h1><p class="lede">Le lien ne correspond pas à une entrée de cette édition.</p><a class="button" href="#${type}">Revenir au catalogue</a>`;}
  function library() {
    const type=route.page;
    let inner;
    if (route.id) {
      const page=(type==='guide'?guideIndex:creaturesIndex).find(item=>(item.id||item.creature_id)===route.id);
      inner=page?(type==='guide'?guideArticle(page):creatureArticle(page)):notFound(type);
    } else inner=listing();
    return `<section class="codex">${sidebar(type)}<div id="codex-content" class="codex-main">${inner}</div></section>`;
  }
  function utility() {
    let body;
    if (route.page==='reglages') body=`<p class="eyebrow">Confort & affichage</p><h1>Vos réglages</h1><p>Le confort de lecture et l’animation du fond s’appliquent immédiatement. Les choix graphiques sont mémorisés dans ce navigateur, sans modifier le moteur.</p><form id="settings"><div class="settings-grid"><div class="setting"><label for="quality">Qualité graphique prévue</label><select id="quality" name="quality">${['Low','Medium','High','Ultra'].map(value=>`<option ${preferences.quality===value?'selected':''}>${value}</option>`).join('')}</select></div><div class="setting"><label for="resolution">Résolution prévue</label><select id="resolution" name="resolution">${['1920×1080','2560×1440','3840×2160'].map(value=>`<option ${preferences.resolution===value?'selected':''}>${value}</option>`).join('')}</select></div><div class="setting"><label for="font">Taille du texte du codex</label><select id="font" name="font">${[100,110,120].map(value=>`<option value="${value}" ${preferences.font===value?'selected':''}>${value} %</option>`).join('')}</select></div></div><label class="check"><input id="vsync" type="checkbox" ${preferences.vsync?'checked':''}> Synchronisation verticale prévue</label><label class="check"><input id="ambient" type="checkbox" ${preferences.motion?'checked':''}> Légère animation lumineuse du fond (sans mouvement de caméra)</label><p class="notice">La préférence système de réduction des animations reste prioritaire. Aucune parallaxe, aucun suivi de souris, aucun zoom automatique.</p><button class="button primary" type="submit">Enregistrer mes préférences</button><p id="settings-status" role="status"></p></form>`;
    else if (route.page==='aventures') body=`<p class="eyebrow">Vos aventures</p><h1>Le départ se prépare.</h1><p>Aucun module jouable ni sauvegarde n’est raccordé à cette interface. Le décor d’accueil est une illustration, pas une campagne installée.</p><ul class="utility-list"><li><strong>Nouvelle partie</strong><br>La sélection d’un module et la création du personnage seront activées avec le moteur.</li><li><strong>Continuer / Charger une partie</strong><br>Aucune sauvegarde disponible dans cette interface.</li></ul><a class="button primary" href="${url('guide',guideIndex.find(item=>item.title==='Créer son personnage')?.id||'')}">Découvrir la création du personnage →</a>`;
    else if (route.page==='multijoueur') body=`<p class="eyebrow">Partir en compagnie</p><h1>L’aventure se partage.</h1><p>Le réseau n’est pas encore raccordé. Aucun serveur n’est contacté, aucune invitation n’est envoyée et aucun joueur fictif n’apparaît comme connecté.</p><ul class="utility-list"><li><strong>Héberger une partie</strong><br>À connecter au futur serveur et au choix de module.</li><li><strong>Rejoindre une partie</strong><br>À connecter à une session réelle, avec vérification des contenus.</li></ul><a class="button primary" href="${url('guide',guideIndex.find(item=>item.title==='Groupe et compagnons')?.id||'')}">Lire les règles du groupe →</a>`;
    else if (route.page==='a-propos') body=`<p class="eyebrow">Une bibliothèque fidèle aux sources</p><h1>Sources & limites</h1><p>Accueil issu de la première direction « L’Aube des royaumes ». Le fond est immobile ; seule une brume lumineuse très discrète varie en opacité.</p><ul class="utility-list"><li><strong>Guide du joueur v0.1</strong><br>Les six parties Markdown canoniques sont transformées en ${guideIndex.length} rubriques web, avec conservation des règles, formules, tableaux, talents et 42 compétences. Les annexes techniques sont conservées à part.</li><li><strong>Bestiaire v${escape(content.bestiaryVersion)}</strong><br>${creaturesIndex.length} fiches JSON, ${illustrationSummary}. <a href="sources/bestiary-research.md" target="_blank" rel="noopener">Recherche et parti pris de création ↗</a>. Navigation par familles, recherche dans les informations publiques et secrets repliés par défaut.</li><li><strong>Statut des contenus</strong><br>Les sources portent le statut de proposition de conception. Leur présence ici ne qualifie aucune règle dans le runtime Rust.</li><li><strong>Usage local</strong><br>Aucun service externe, aucune télémétrie, aucune connexion multijoueur. Les préférences sont locales. Les 30 nouvelles fiches sont enregistrées dans le bestiaire canonique ; les 21 fiches antérieures restent inchangées.<br><a href="sources/manifest.json" target="_blank" rel="noopener">Manifeste des sources et empreintes SHA-256 ↗</a></li><li><strong>Illustrations</strong><br>Illustrations originales existantes, copiées sans retouche. Leur provenance est conservée ; la revue de distribution externe reste requise avant publication.</li></ul>`;
    else body='<h1>Page introuvable</h1><a class="button" href="#accueil">Retour à l’accueil</a>';
    return `<section class="utility">${body}</section>`;
  }
  function highlight(container, query) {
    if(!query.trim()) return;
    const walker=document.createTreeWalker(container,NodeFilter.SHOW_TEXT);
    const nodes=[];
    while(walker.nextNode()) if (!walker.currentNode.parentElement.closest('button,input,script,style')) nodes.push(walker.currentNode);
    for(const node of nodes){
      const text=node.textContent, matches=core.ranges(text,query);if(!matches.length)continue;
      const pieces=[];let offset=0;
      for(const [start,end] of matches){pieces.push(document.createTextNode(text.slice(offset,start)));const mark=document.createElement('mark');mark.textContent=text.slice(start,end);pieces.push(mark);offset=end;}
      pieces.push(document.createTextNode(text.slice(offset)));node.replaceWith(...pieces);
    }
  }
  function updateResults() {
    const region=document.getElementById('codex-content');
    if (!region) return;
    region.innerHTML=listing();
    document.getElementById('result-count').textContent=matching().length+' résultat(s)';
    document.getElementById('clear-search').hidden=!route.params.get('q');
    // Search remains local; synchronize chapter links with the current query.
    document.querySelector('.local-navigation')?.remove();
    document.querySelectorAll('.category-nav a').forEach(link=>{
      if(link.textContent==='Commencer ici'){link.href='#guide';link.removeAttribute('aria-current');return;}
      const target=new URLSearchParams(link.hash.split('?')[1]||'');
      const q=route.params.get('q');q?target.set('q',q):target.delete('q');
      link.href=url(route.page,'',Object.fromEntries(target));
      const selected=target.get('g')===route.params.get('g')&&target.get('view')===route.params.get('view')&&target.get('wave')===route.params.get('wave');
      selected?link.setAttribute('aria-current','page'):link.removeAttribute('aria-current');
    });
  }
  function render() {
    clearTimeout(searchTimer);
    route=parseRoute();
    bestiaryTab=['portrait','rencontre','combat','savoir'].includes(route.params.get('tab'))?route.params.get('tab'):'portrait';
    const reading=['guide','bestiaire'].includes(route.page);
    document.body.classList.toggle('reading',reading);
    document.querySelectorAll('.main-nav a').forEach(link=>link.hash==='#'+route.page?link.setAttribute('aria-current','page'):link.removeAttribute('aria-current'));
    main.innerHTML=route.page==='accueil'?home():reading?library():utility();
    main.classList.remove('page-fade');void main.offsetWidth;main.classList.add('page-fade');
    if(reading){
      document.getElementById('result-count').textContent=route.id?'Rechercher une autre fiche':route.page==='guide'&&!route.params.size?'126 rubriques · 42 compétences':matching().length+' résultat(s)';
      if(route.id&&route.page==='guide')highlight(document.querySelector('.article-body')||main,route.params.get('q')||'');
      if(matchMedia('(max-width:700px)').matches)document.querySelectorAll('.navigation-fold,.local-navigation').forEach(el=>el.open=false);
    }
    document.title=(route.page==='guide'?'Guide du joueur':route.page==='bestiaire'?'Bestiaire':route.page==='accueil'?'L’Aube des royaumes':label(route.page))+' — OpenRPG Forge';
    applyPreferences();
    window.scrollTo({top:0,behavior:'instant'});
    const anchor=route.params.get('s');
    const destination=anchor?document.querySelector('.article-body')?.querySelector('[id="'+CSS.escape(anchor)+'"]'):null;
    if(destination){destination.tabIndex=-1;destination.scrollIntoView({block:'start',behavior:'instant'});destination.focus({preventScroll:true});}
    else if(route.id&&route.page==='bestiaire'&&route.params.has('tab'))document.querySelector('.reader-tabs')?.scrollIntoView({block:'start',behavior:'instant'});
    announcer.textContent=main.querySelector('h1')?.textContent||'Page ouverte';
  }
  main.addEventListener('input',event=>{
    if(event.target.id!=='codex-search')return;
    const query=event.target.value;
    clearTimeout(searchTimer);
    searchTimer=setTimeout(()=>{
      route.id='';['s','tab','p'].forEach(key=>route.params.delete(key));query?route.params.set('q',query):route.params.delete('q');
      history.replaceState(null,'',url(route.page,'',Object.fromEntries(route.params)));
      updateResults();
    },140);
  });
  main.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button)return;
    if(button.id==='clear-search'){
      clearTimeout(searchTimer);const input=document.getElementById('codex-search');input.value='';route.id='';['q','s','tab','p'].forEach(key=>route.params.delete(key));history.replaceState(null,'',url(route.page,'',Object.fromEntries(route.params)));updateResults();input.focus();
    }
    if(button.id==='reset-search')location.hash=route.page;
    if(button.dataset.font){preferences.font=Math.max(100,Math.min(120,preferences.font+Number(button.dataset.font)));persist();applyPreferences();}
  });
  main.addEventListener('change',event=>{
    if(event.target.id!=='threat-filter')return;
    const params=new URLSearchParams(route.params);params.delete('p');event.target.value?params.set('threat',event.target.value):params.delete('threat');
    location.hash=url('bestiaire','',Object.fromEntries(params));
  });
  main.addEventListener('submit',event=>{
    if(event.target.id!=='settings')return;event.preventDefault();
    preferences.quality=document.getElementById('quality').value;
    preferences.resolution=document.getElementById('resolution').value;
    preferences.vsync=document.getElementById('vsync').checked;
    preferences.motion=document.getElementById('ambient').checked;
    preferences.font=Number(document.getElementById('font').value);
    const stored=persist();applyPreferences();document.getElementById('settings-status').textContent=stored?'Préférences enregistrées dans ce navigateur. Aucun réglage moteur n’a été modifié.':'Préférences appliquées pour cette session ; le navigateur refuse le stockage local.';
  });
  window.addEventListener('hashchange',()=>{render();if(!route.params.has('s'))main.focus({preventScroll:true});});
  render();
})();
