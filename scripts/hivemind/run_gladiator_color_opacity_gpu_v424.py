"""Measure remaining native colors/ORM/alpha using the current product image data."""
from pathlib import Path
S=Path(__file__).resolve().parent
recipe=S/'run_gladiator_normal_gpu_v417.py'
code=recipe.read_text('utf-8').replace('v417','v424').replace('gladiator-normal-gpu-v424','gladiator-color-opacity-gpu-v424').replace('gladiator-material-kernels-v414','gladiator-material-kernels-v419').replace('gladiator-normal-reference-v415','gladiator-color-opacity-reference-v423')
edits={
 "if b['requires_normal_decode']":"if not b['requires_normal_decode']",
 "['verified_textures']==4":"['verified_textures']==8",
 'assert len(guids)==4':'assert len(guids)==8',
 "assert r['graphics_format']=='RGBA_DXT5_UNorm'":"assert r['graphics_format'] in {'RGBA_DXT1_SRGB','R8G8B8_UNorm','R8G8B8A8_UNorm'}",
 "gpu_format='Bc3RgbaUnorm'":"gpu_format={'RGBA_DXT1_SRGB':'Bc1RgbaUnormSrgb','R8G8B8_UNorm':'Rgb8Unorm','R8G8B8A8_UNorm':'Rgba8Unorm'}[r['graphics_format']]",
 'assert len(rows)==4':'assert len(rows)==8',
 'assert_eq!(results.len(), 4);':'assert_eq!(results.len(), 8);',
 'assert_eq!(total_mips, 52);':'assert_eq!(total_mips, 104);',
 "normal_textures=4,mips=52":"color_opacity_textures=8,mips=104",
 "[('raw',0),('decoded',2)]":"[('raw',0)]",
 'four_normals_raw_and_decoded_gpu_samples_captured_comparison_pending':'eight_color_opacity_raw_gpu_samples_captured_comparison_pending'
}
for a,b in edits.items():
    assert a in code,a
    code=code.replace(a,b)
code=code.replace('"gpu_sampled_textures":4','"gpu_sampled_textures":8')
needle="    (O/'src/main.rs').write_text(text,'utf-8')"
assert code.count(needle)==1
injection='''    a='"Rgba8Unorm" => SourceGraphImportedTextureFormat::Rgba8Unorm,'
    assert text.count(a)==1
    text=text.replace(a,a+'\\n            "Rgb8Unorm" => SourceGraphImportedTextureFormat::Rgb8Unorm,')
    a='for (level, mip) in metadata.mips.iter().enumerate() {'
    assert text.count(a)==1
    text=text.replace(a,'let mut upload_offset = 0usize;\\n        '+a+'\\n            let upload_bytes = if r["gpu_format"] == "Rgb8Unorm" { (mip.width as usize) * (mip.height as usize) * 4 } else { mip.bytes };')
    a='&data[mip.offset..mip.offset + mip.bytes]'
    assert text.count(a)==1
    text=text.replace(a,'&image.data.as_ref().unwrap()[upload_offset..upload_offset + upload_bytes]')
    a='let bevy::image::ImageSampler::Descriptor(desc) = &image.sampler else {'
    assert text.count(a)==1
    text=text.replace(a,'assert_eq!(upload_offset, image.data.as_ref().unwrap().len());\\n        '+a)
    a='let bevy::image::ImageSampler::Descriptor(desc)'
    pos=text.index(a);before=text[:pos]
    marker='            );\\n        }'
    index=before.rfind(marker);assert index>=0
    text=text[:index]+text[index:].replace(marker,'            );\\n            upload_offset += upload_bytes;\\n        }',1)
'''
code=code.replace(needle,injection+needle)
exec(compile(code,str(recipe)+'#remaining-colors-opacity','exec'),dict(__file__=str(Path(__file__).resolve()),__name__='__main__'))
