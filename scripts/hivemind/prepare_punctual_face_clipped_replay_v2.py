from pathlib import Path
base=Path(__file__).with_name('prepare_punctual_face_clipped_replay_v1.py')
source=base.read_text(encoding='utf-8').replace('firepit-punctual-d16-caster-face-clipped-v1','firepit-punctual-d16-caster-face-clipped-v2')
marker=" (ROOT/'src/main.rs').write_text(text,encoding='utf-8');"
assert source.count(marker)==1
block=" text=text.replace('pass.set_vertex_buffer(0,face_vertices[index].slice(..));pass.draw(0..face_vertex_counts[index],0..1);draws+=1;','if face_vertex_counts[index]!=0 {pass.set_vertex_buffer(0,face_vertices[index].slice(..));pass.draw(0..face_vertex_counts[index],0..1);draws+=1;}')\n text=text.replace('assert_eq!(draws,if active{6}else{0});','assert_eq!(draws,if active{face_vertex_counts.iter().filter(|&&n|n!=0).count()}else{0});')\n"
source=source.replace(marker,block+marker)
exec(compile(source,str(base),'exec'),globals())
