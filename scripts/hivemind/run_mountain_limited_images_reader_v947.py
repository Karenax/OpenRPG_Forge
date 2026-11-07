"""Verify images plus explicit source limitations through current product APIs."""
from pathlib import Path
recipe_path=Path(__file__).with_name('run_mountain_native_reader_v921.py')
recipe=recipe_path.read_text('utf-8').replace('mountain-reviewed-images-native-reader-v921','mountain-original-limited-images-reader-v947').replace('mountain-reviewed-images-index-v920','mountain-original-limited-images-index-v946').replace("<=44", "<=3")
anchor="recipe=recipe.replace(anchor,'adapt += '+repr(adapt)+'\\n'+anchor)"
assert recipe.count(anchor)==1
extra=r'''
before='assert!(detail.preview_path.is_some());'
after=before+"""assert!(detail.card.description.contains("RGBA"));assert!(detail.card.tags.iter().any(|t|t=="source-texture-uniform"));assert!(!detail.card.runtime_qualified);
  assert_eq!(detail.catalog_asset["preview_source_limitations"]["kind"],"original-source-texture-rgba-zero");assert_eq!(detail.catalog_asset["preview_source_limitations"]["textures"].as_array().unwrap().len(),2);
  assert!(detail.catalog_asset["descriptions"]["fr"].as_str().unwrap().contains("archive Unity originale"));assert!(detail.catalog_asset["descriptions"]["en"].as_str().unwrap().contains("original Unity archive payloads"));
  if i==0 {let LibraryResponse::Page{page:limits}=library.execute(LibraryRequest::Search{query:LibraryQuery{text:"source-texture-uniform".into(),pack:"hivemind.mountain-ancient-mountain".into(),availability:"source_support".into(),limit:48,..Default::default()}}).unwrap()else{panic!("Native limitation search required")};assert_eq!(limits.matched_assets,3);assert!(limits.assets.iter().all(|c|c.has_thumbnail));}
"""
assert before in code
code=code.replace(before,after)
'''
recipe=recipe.replace(anchor,'adapt += '+repr(extra)+'\n'+anchor)
exec(compile(recipe,str(recipe_path)+'#original-limitations-visible-in-product-reader','exec'),globals())
