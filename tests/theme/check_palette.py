"""Check full theme brush coverage and normal-text contrast without a Windows runtime."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET
root = Path(__file__).resolve().parents[2]
source = (root / 'src/CatPriceCalculator/ThemePalette.cs').read_text()
ns = '{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
key = '{http://schemas.microsoft.com/winfx/2006/xaml}Key'
colors = ET.parse(root / 'src/CatPriceCalculator/Styles/Colors.xaml').getroot()
brushes = [node.get(key) for node in colors if node.tag in (ns+'SolidColorBrush', ns+'LinearGradientBrush')]
for name in brushes:
    assert f'resources["{name}"] = {name};' in source, f'Missing complete theme brush: {name}'
    assert f'{name}.Freeze();' in source, f'Unfrozen theme brush: {name}'
assert 'palette[resource.Key] = resource.Value;' in source
assert 'applicationResources[resource.Key] = resource.Value;' in source
palette = {name: (dark, light) for name, dark, light in re.findall(r'\("([^"]+)", "(#[0-9A-F]+)", "(#[0-9A-F]+)"\)', source)}
def luminance(value):
    rgb = [int(value[-6:][i:i+2], 16)/255 for i in (0,2,4)]
    linear = [x/12.92 if x <= .04045 else ((x+.055)/1.055)**2.4 for x in rgb]
    return sum(a*b for a,b in zip(linear, (.2126,.7152,.0722)))
def contrast(a,b):
    values = sorted((luminance(a),luminance(b)))
    return (values[1]+.05)/(values[0]+.05)
for index, theme in enumerate(('dark', 'light')):
    for text in ('E8EDF5', 'B9CEE8', '9CADC3'):
        for surface in ('0B1522', '0D192A', '101D2D', '152A43', '202C3C', '29384D'):
            ratio = contrast(palette[text][index], palette[surface][index])
            assert ratio >= 4.5, f'{theme}: {text}/{surface} contrast {ratio:.2f}'
    if theme == 'dark':
        continue  # Existing dark result gradient is preserved.
    for text, surfaces in [('NavigationSelectedText', ('NavigationSelectedStart','NavigationSelectedEnd')), ('ResultText',('ResultStart','ResultEnd')), ('ResultDetail',('ResultStart','ResultEnd'))]:
        for surface in surfaces:
            assert contrast(palette[text][index],palette[surface][index]) >= 4.5
print(f'PASS: {len(brushes)} complete brushes, both dictionary scopes, dark/light text contrast')
