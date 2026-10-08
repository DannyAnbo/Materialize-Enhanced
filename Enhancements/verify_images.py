from pathlib import Path
from PIL import Image
import json
import sys

folder = Path(sys.argv[1])
reference = Image.open(folder / 'property_rgba.png').convert('RGBA')
rows = []
for fmt in ('png', 'tga', 'tiff'):
    actual = Image.open(folder / f'property_rgba_{fmt}.{fmt}')
    rgba = actual.convert('RGBA')
    rows.append(dict(format=fmt, mode=actual.mode, size=list(actual.size),
                     rgba_exact=rgba.tobytes() == reference.tobytes(),
                     alpha_exact=rgba.getchannel('A').tobytes() == reference.getchannel('A').tobytes()))
incoming = Image.open(folder / '拖入测试.png').convert('RGBA')
for fmt in ('tga', 'tiff', 'bmp'):
    actual = Image.open(folder / f'导入_{fmt}.{fmt}')
    rows.append(dict(format=fmt, unicode_name=True, mode=actual.mode,
                     rgba_exact=actual.convert('RGBA').tobytes() == incoming.tobytes()))
assert all(row['rgba_exact'] and row.get('alpha_exact', True) for row in rows), rows
channels = folder / 'channels'
if channels.exists():
    packed = Image.open(channels / '合并_RGBA.png').convert('RGBA')
    for channel in 'RGBA':
        scalar = packed.getchannel(channel)
        expected = Image.merge('RGBA', (scalar, scalar, scalar, Image.new('L', packed.size, 255)))
        actual = Image.open(channels / f'metallic_{channel}.png').convert('RGBA')
        rows.append(dict(channel=channel, grayscale_exact=actual.tobytes() == expected.tobytes()))
    scalar = packed.getchannel('G').point(lambda value: 255 - value)
    expected = Image.merge('RGBA', (scalar, scalar, scalar, Image.new('L', packed.size, 255)))
    actual = Image.open(channels / 'smoothness_G_inverted.png').convert('RGBA')
    rows.append(dict(channel='G', inversion_exact=actual.tobytes() == expected.tobytes()))
    assert all(row.get('grayscale_exact', True) and row.get('inversion_exact', True) for row in rows), rows
(folder / 'independent-image-verification.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
resolution = folder / 'resolution'
if resolution.exists():
    import numpy as np
    original = Image.open(resolution / 'original.png').convert('RGBA')
    resized = Image.open(resolution / 'resized.png').convert('RGBA')
    # Packed material channels are independent data. Resizing RGBA directly in
    # Pillow premultiplies alpha, so compare four independent scalar channels.
    expected = Image.merge('RGBA', tuple(
        channel.resize(resized.size, Image.Resampling.BILINEAR)
        for channel in original.split()))
    error = int(np.abs(np.asarray(resized, dtype=np.int16) - np.asarray(expected, dtype=np.int16)).max())
    assert resized.size == (32, 24) and error <= 1, error
    rows.append(dict(check='raw RGBA bilinear resampling', size=list(resized.size), max_channel_error=error))
    for fmt in ('png', 'tga', 'tiff', 'jpg', 'bmp'):
        actual = Image.open(resolution / f'export-{fmt}.{fmt}')
        assert actual.size == (64, 40), (fmt, actual.size)
        rows.append(dict(format=fmt, check='selected export resolution', size=list(actual.size)))
    large = Image.open(resolution / 'export-4k.png')
    assert large.size == (4096, 4096), large.size
    rows.append(dict(format='png', check='real 4K export', size=list(large.size)))
workflow = folder / 'workflow'
if workflow.exists():
    from PIL import ImageOps
    packed = Image.open(workflow / 'packed-input.png').convert('RGBA')
    for fmt in ('png', 'tga', 'tiff'):
        actual = Image.open(workflow / f'roughness-{fmt}.{fmt}').convert('RGBA')
        assert actual.tobytes() == packed.tobytes(), fmt
        rows.append(dict(format=fmt, check='roughness export exact RGBA', rgba_exact=True))
    for fmt in ('jpg', 'bmp'):
        actual = Image.open(workflow / f'roughness-{fmt}.{fmt}')
        assert actual.size == packed.size, (fmt, actual.size)
        rows.append(dict(format=fmt, check='roughness export dimensions', size=list(actual.size)))
    red = packed.getchannel('R')
    expected = Image.merge('RGBA', (red, red, red, red))
    actual = Image.open(workflow / 'property-roughness.png').convert('RGBA')
    assert actual.tobytes() == expected.tobytes()
    rows.append(dict(check='roughness property RGBA channels', rgba_exact=True))
    canonical = Image.open(workflow / 'generated-canonical.png').convert('RGBA')
    expected = Image.merge('RGBA', tuple(ImageOps.invert(c) for c in canonical.split()[:3]) + (canonical.getchannel('A'),))
    actual = Image.open(workflow / 'generated-roughness.png').convert('RGBA')
    assert actual.tobytes() == expected.tobytes()
    rows.append(dict(check='actual generator roughness export', rgba_exact=True))
unicode = folder / 'unicode' / '中文 文件夹'
if unicode.exists():
    reference = Image.open(unicode / '木板 合并通道.png').convert('RGBA')
    for fmt in ('tga', 'tiff'):
        actual = Image.open(unicode / f'木板 合并通道.{fmt}').convert('RGBA')
        assert actual.tobytes() == reference.tobytes(), fmt
        rows.append(dict(format=fmt, check='Chinese file and directory round trip', rgba_exact=True))
    # BMP alpha is not consistently exposed by external decoders; compare RGB.
    actual = Image.open(unicode / '木板 合并通道.bmp').convert('RGB')
    assert actual.tobytes() == reference.convert('RGB').tobytes()
    rows.append(dict(format='bmp', check='Chinese file and directory round trip RGB', rgb_exact=True))
    actual = Image.open(unicode / '中文 工程_金属度.tga').convert('RGBA')
    assert actual.tobytes() == reference.tobytes()
    rows.append(dict(check='Chinese custom export name', rgba_exact=True))
(folder / 'independent-image-verification.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
print(json.dumps(rows, indent=2))
