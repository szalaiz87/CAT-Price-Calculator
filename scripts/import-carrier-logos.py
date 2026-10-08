"""Convert the downloaded, path-only carrier SVGs into frozen WPF vectors; no runtime SVG dependency."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
P = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
X = 'http://schemas.microsoft.com/winfx/2006/xaml'
PO = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation/options'
ET.register_namespace('', P)
ET.register_namespace('x', X)
ET.register_namespace('po', PO)
def element(tag, **attrs): return ET.Element('{'+P+'}'+tag, attrs)
def transform(parent, text):
    match = re.fullmatch(r'(matrix|translate)\(([^)]+)\)', text)
    if not match: raise ValueError('Unsupported SVG transform: '+text)
    numbers = re.split(r'[\s,]+', match[2].strip())
    t = element('DrawingGroup.Transform')
    if match[1] == 'matrix': t.append(element('MatrixTransform', Matrix=','.join(numbers)))
    else: t.append(element('TranslateTransform', X=numbers[0], Y=numbers[1] if len(numbers)>1 else '0'))
    parent.append(t)
def convert(svg, parent, classes, inherited='#000000'):
    tag=svg.tag.split('}')[-1]
    if tag not in ['svg','g','path','polygon']: return
    style=dict(re.findall(r'([\w-]+)\s*:\s*([^;]+)', svg.get('style','')))
    color=svg.get('fill',style.get('fill',classes.get(svg.get('class'), inherited)))
    if tag in ['svg','g']:
        group=element('DrawingGroup')
        if svg.get('transform'):transform(group,svg.get('transform'))
        for child in svg:convert(child,group,classes,color)
        parent.append(group);return
    data=svg.get('d')
    if tag=='polygon':data='M'+' L'.join(svg.get('points').split())+' Z'
    data=('F0 ' if svg.get('fill-rule',style.get('fill-rule'))=='evenodd' else 'F1 ')+data
    drawing=element('GeometryDrawing',Brush=color,Geometry=data)
    if svg.get('transform'):
        group=element('DrawingGroup');transform(group,svg.get('transform'));group.append(drawing);parent.append(group)
    else:parent.append(drawing)

root=element('ResourceDictionary')
for name,key in [('dhl','DhlLogo'),('fedex','FedExLogo'),('ups','UpsLogo'),('gls','GlsLogo')]:
    svg=ET.parse(ROOT/'src/CatPriceCalculator/Assets/Carriers'/f'{name}.svg').getroot()
    classes={c:fill for c,fill in re.findall(r'\.([\w-]+)\s*\{\s*fill:([^;]+);?\s*\}',ET.tostring(svg,encoding='unicode'))}
    image=element('DrawingImage');image.set('{'+X+'}Key',key);image.set('{'+PO+'}Freeze','True')
    prop=element('DrawingImage.Drawing');group=element('DrawingGroup')
    # Keep the original SVG canvas/aspect ratio, including its transparent margins.
    x,y,w,h=map(float,svg.get('viewBox').split())
    group.append(element('GeometryDrawing',Brush='Transparent',Geometry=f'M{x},{y} h{w} v{h} h{-w} Z'))
    convert(svg,group,classes);prop.append(group);image.append(prop);root.append(image)
ET.indent(root)
ET.ElementTree(root).write(ROOT/'src/CatPriceCalculator/Styles/CarrierLogos.xaml',encoding='utf-8',xml_declaration=False)
print('Four frozen carrier vectors generated.')
