from pathlib import Path
from PIL import Image
import numpy as np,json
folder=Path('tests/v7-regression/v7');report=[]
a=np.array(Image.open(folder/'falloff-1.png'));b=np.array(Image.open(folder/'falloff-2.png'));c=np.array(Image.open(folder/'falloff-4.png'))
for label,x,y in [('falloff 1 vs 2',a,b),('falloff 2 vs 4',b,c)]:
 different=int(np.any(x!=y,axis=2).sum());assert different>0;report.append({'check':label,'changed_pixels':different,'size':[a.shape[1],a.shape[0]]})
files=sorted(folder.glob('selection*.png'));assert len(files)==2
for p in files:assert Image.open(p).size==(32,32)
report.append({'check':'selected maps only','exported_count':2,'property_rgba':'selection_msao.png' in [p.name for p in files]})
assert Image.open(folder/'selection_msao.png').mode=='RGBA';report.append({'check':'property export has RGBA','passed':True})
project=(folder/'reload-project.mtz').read_text(encoding='utf-8-sig');assert 'C:\\Users\\' not in project;report.append({'check':'embedded reload image contains no machine path','passed':True})
startup=Path('tests/v7-regression/startup-result.txt').read_text();assert startup.startswith('PASS ');report.append({'check':'project-file startup then new project','passed':True})
Path('tests/v7-regression/v7-image-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
