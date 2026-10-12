"""Check form bounds and selection rendering that can be verified without a WPF runtime."""
from pathlib import Path
import xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
ns='{http://schemas.microsoft.com/winfx/2006/xaml/presentation}'
view=ET.parse(root/'src/CatPriceCalculator/ServiceCalendarView.xaml').getroot()
grid=view.find(ns+'Grid');rows=list(grid.find(ns+'Grid.RowDefinitions'))
assert sum(float(r.get('Height','0')) for r in rows)<916
for card in grid.findall(ns+'Border'):
    inner=card.find(ns+'Grid')
    if inner is None:continue
    definitions=inner.find(ns+'Grid.RowDefinitions')
    if definitions is None:continue
    padding=float(card.get('Padding','14'))
    available=float(rows[int(card.get('Grid.Row','0'))].get('Height'))-padding*2-2
    required=sum(float(row.get('Height','0')) for row in definitions)
    assert required<=available, f'Card contents need {required}px but have {available}px'
source=(root/'src/CatPriceCalculator/ServiceCalendarView.xaml').read_text()
assert 'SelectedItem.DisplayName' in source and 'SelectionBoxItem' not in source
assert 'StaffInput.SelectedIndex=-1' in (root/'src/CatPriceCalculator/ServiceCalendarView.xaml.cs').read_text()
calendar=(root/'src/CatPriceCalculator/ServiceCalendarWindow.cs').read_text()
assert 'staff.Color' not in calendar
assert 'SetResourceReference(Button.BackgroundProperty' in calendar
print('PASS: all form cards fit their rows, empty/name-only staff selection, themed calendar cells')

form=(root/'src/CatPriceCalculator/ServiceCalendarView.xaml').read_text()
assert 'ImportButton' not in form and 'LastImportStatus' in form and 'SearchCustomers' in form
code=(root/'src/CatPriceCalculator/ServiceCalendarView.xaml.cs').read_text()
for field in ['CustomerDisplay','DateInput','ContactInput','PhoneInput','LocationInput','DescriptionInput']:
    assert field+'.Clear()' in code
assert 'Owner=' not in code and 'ShowDialog' not in code
assert 'Owner=' not in calendar and 'dialog.Show()' in calendar
print('PASS: separate import settings, complete reset and unowned modeless popups')

assert 'staff.Name+(staff.Id==' not in calendar
assert 'addWork(date,staff.Id)' in calendar and 'Munka hozzáadása' in calendar
assert 'StaffInput.SelectedItem=ServiceCalendar.Staff.First(s=>s.Id==staffId)' in code
assert 'RecorderInput.Text=recorderName' in code and 'IsReadOnly="True"' in form
styles=(root/'src/CatPriceCalculator/Styles/Controls.xaml').read_text()
assert styles.count('<ControlTemplate x:Key="AppButtonTemplate"')==1
assert 'Property="Template" Value="{StaticResource AppButtonTemplate}"' in styles
print('PASS: calendar-to-form date/staff handoff, recorder reset and shared menu button template')
