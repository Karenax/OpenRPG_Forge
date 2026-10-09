import { loadPixels } from '/source-load-pixels.mjs';
const status = document.querySelector('#status');
try {
  const inputs = await (await fetch('/inputs.json')).json();
  for (const [index, name] of inputs.images.entries()) {
    status.textContent = `${index}/12 — ${name}`;
    const pixels = await loadPixels('/images/' + encodeURIComponent(name));
    if (pixels.width !== 1024 || pixels.height !== 1024 || pixels.data.length !== 4194304) throw Error('Source dimensions differ');
    const response = await fetch('/capture/' + encodeURIComponent(name), {method:'POST', headers:{'x-forge-capture':inputs.token}, body:pixels.data});
    if (!response.ok) throw Error(await response.text());
  }
  const response = await fetch('/complete', {method:'POST', headers:{'x-forge-capture':inputs.token}, body:JSON.stringify({user_agent:navigator.userAgent, source_path:'createImageBitmap + OffscreenCanvas 2d + getImageData', decoder_hook:false})});
  if (!response.ok) throw Error(await response.text());
  status.textContent = 'PASS — 12/12 images source capturées. Atlas et rendu à valider séparément.';
} catch (error) { status.textContent = 'ÉCHEC — ' + String(error); }
