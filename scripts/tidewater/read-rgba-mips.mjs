export async function readRgbaMips({texture,GPU,binary,prefix}) {
  if(texture.format!=='rgba8unorm') throw new Error('RGBA8 readback required');
  const buffers=[];
  for(let level=0;level<texture.mipLevelCount;level++) {
    const width=Math.max(1,texture.width>>level),height=Math.max(1,texture.height>>level);
    const bytesPerRow=Math.ceil(width*4/256)*256;
    const buffer=GPU.device.createBuffer({size:bytesPerRow*height,usage:GPUBufferUsage.COPY_DST|GPUBufferUsage.MAP_READ});
    GPU.getEncoder().copyTextureToBuffer({texture:texture.getGPU(),mipLevel:level},
      {buffer,bytesPerRow,rowsPerImage:height},{width,height,depthOrArrayLayers:1});
    buffers.push({buffer,width,height,bytesPerRow,level});
  }
  GPU.submit();
  const mips=[];
  for(const {buffer,width,height,bytesPerRow,level} of buffers) {
    await buffer.mapAsync(GPUMapMode.READ);
    const padded=new Uint8Array(buffer.getMappedRange()),data=new Uint8Array(width*height*4);
    for(let row=0;row<height;row++) data.set(padded.subarray(row*bytesPerRow,row*bytesPerRow+width*4),row*width*4);
    mips.push({width,height,level,...binary(`${prefix}-${level}.rgba8.bin`,data)});
    buffer.unmap();buffer.destroy();
  }
  return mips;
}
