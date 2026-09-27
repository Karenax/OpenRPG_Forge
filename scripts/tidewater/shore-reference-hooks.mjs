// Read the exact application wetness hook rather than recreating its behavior.
import {readFileSync} from 'node:fs';
import {join} from 'node:path';
export function terrainWetnessHook(source) {
 const app=readFileSync(join(source,'src/App.js'),'utf8');
 const marker='fn terrainWetness( xz: vec2f, h: f32 ) -> vec2f {';
 const start=app.indexOf(marker),end=app.indexOf('\n}',start);
 if(start<0||end<0||app.indexOf(marker,start+1)>=0)throw Error('Ambiguous original terrain wetness hook');
 const code=app.slice(start,end+2);
 if(!code.includes('shoreSimSandFoam')||!code.includes('shoreSimSample'))throw Error('Incomplete wetness hook');
 return code;
}
