from pathlib import Path
original = Path(__file__).with_name('seal_pirate_held_wood_delivery_v837.py')
code = original.read_text('utf-8').replace('pirate-held-wood-images-delivery-v837', 'pirate-held-wood-images-delivery-v840').replace('pirate-held-wood-images-native-reader-v836', 'pirate-held-wood-images-native-reader-v838').replace('v819 a v837', 'v819 a v840')
code = code.replace('[parent,Path(__file__),ip,rp,vp,pointer,', '[original,parent,Path(__file__),ip,rp,vp,pointer,')
exec(compile(code, str(original)+'#current-native-reader-delivery', 'exec'), globals())
