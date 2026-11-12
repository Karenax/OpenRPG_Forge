import io
import unittest
from PIL import Image, PngImagePlugin
from audit_source_variant_preview_pixels import pixel_pin


class SourceVariantPreviewTests(unittest.TestCase):
    def test_ignores_png_metadata_but_detects_pixel_or_size_change(self):
        def png(size, color, note=None):
            out=io.BytesIO();metadata=PngImagePlugin.PngInfo()
            if note:metadata.add_text('note',note)
            Image.new('RGBA',size,color).save(out,format='PNG',pnginfo=metadata)
            return out.getvalue()
        first=png((2,2),(1,2,3,255),'one')
        second=png((2,2),(1,2,3,255),'two')
        self.assertNotEqual(first,second)
        self.assertEqual(pixel_pin(first),pixel_pin(second))
        self.assertNotEqual(pixel_pin(first),pixel_pin(png((2,2),(1,2,4,255))))
        self.assertNotEqual(pixel_pin(first),pixel_pin(png((1,4),(1,2,3,255))))


if __name__=='__main__':unittest.main()
