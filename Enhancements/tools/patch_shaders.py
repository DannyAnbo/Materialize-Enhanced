import sys,struct,math,re,json,ctypes,argparse
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'unity-assets'))
import hashlib
import UnityPy
from UnityPy.helpers import CompressionHelper
from inspect_shaders import disassemble,compiler,blob_bytes

def checksum(code):
 data=code[20:];n=len(data);state=[0x67452301,0xefcdab89,0x98badcfe,0x10325476]
 constants=[int(abs(math.sin(i+1))*2**32)&0xffffffff for i in range(64)]
 shifts=[7,12,17,22]*4+[5,9,14,20]*4+[4,11,16,23]*4+[6,10,15,21]*4
 def block(b):
  x=struct.unpack('<16I',b);a,b,c,d=state
  for i in range(64):
   if i<16:f=(b&c)|(~b&d);g=i
   elif i<32:f=(d&b)|(~d&c);g=(5*i+1)%16
   elif i<48:f=b^c^d;g=(3*i+5)%16
   else:f=c^(b|~d);g=7*i%16
   t=(a+f+constants[i]+x[g])&0xffffffff;s=shifts[i];a,d,c,b=d,c,b,(b+((t<<s)|(t>>(32-s))))&0xffffffff
  state[:]=[(state[i]+v)&0xffffffff for i,v in enumerate([a,b,c,d])]
 for p in range(0,n-n%64,64):block(data[p:p+64])
 tail=data[n-n%64:]+b'\x80'
 if len(tail)>56:block(tail.ljust(64,b'\0'));tail=b''
 else:tail=b'\0'*4+tail
 tail=bytearray(tail.ljust(64,b'\0'));struct.pack_into('<I',tail,0,n*8);struct.pack_into('<I',tail,60,(n*2)|1);block(tail)
 return struct.pack('<4I',*state)

def dxbc_patch(code,kind):
 assert checksum(code)==code[4:20], 'original checksum mismatch'
 # Disassembly has exactly one non-comment instruction line per token instruction.
 lines=[s.strip() for s in disassemble(code).replace('\x00','').splitlines() if s.strip() and not s.startswith('//') and not s.strip().startswith('{')][1:]
 chunks=[];changed=0
 for i in range(struct.unpack_from('<I',code,28)[0]):
  p=struct.unpack_from('<I',code,32+4*i)[0];tag=code[p:p+4];body=code[p+8:p+8+struct.unpack_from('<I',code,p+4)[0]]
  if tag in [b'SHDR',b'SHEX']:
   tokens=list(struct.unpack('<%dI'%(len(body)//4),body));out=tokens[:2];at=2;li=0
   while at<len(tokens):
    tok=tokens[at];ln=(tok>>24)&127
    if ln==0:ln=tokens[at+1]
    instruction=tokens[at:at+ln];line=lines[li];li+=1
    patch=False
    if kind=='falloff':patch=bool(re.fullmatch(r'mov_sat r\d+\.[xyzw], cb0\[2\]\.x',line))
    if kind=='mask':patch=bool(re.match(r'(add|mul)_sat ',line)) and (line.startswith('add_sat r2.w, r1.w, r1.w') or line.startswith('add_sat r3.z, r2.w, r2.w') or line.startswith('mul_sat r1.w, r1.w, l(-2.') or line.startswith('mul_sat r2.w, r2.w, l(-2.') or line.startswith('add_sat r5.xy, r3.zwzz, r3.zwzz') or line.startswith('mul_sat r3.zw, r3.zzzw, l('))
    if patch:
     instruction[0]&=~0x2000;changed+=1
     # Keep lower bound only. max dst, same-register identity swizzle, literal zero.
     dest=instruction[1];register=instruction[2]
     assert (dest>>12)&255==0 and not dest&0x80000000
     assert tok&0x2000, (line,hex(tok))
     source=(dest&~0xfff)|2|4|(0xe4<<4)
     out.extend(instruction);out.extend([(7<<24)|52,dest,register,source,register,0x4001,0])
    else:out.extend(instruction)
    at+=ln
   out[1]=len(out);body=struct.pack('<%dI'%len(out),*out)
  chunks.append(tag+struct.pack('<I',len(body))+body)
 if not changed:return code,0
 result=bytearray(code[:32]);result.extend(b'\0'*4*len(chunks));offset=len(result)
 for i,ch in enumerate(chunks):struct.pack_into('<I',result,32+4*i,offset);result.extend(ch);offset+=len(ch)
 struct.pack_into('<I',result,24,len(result));result[4:20]=checksum(result)
 # Windows validates both checksum and instruction stream.
 fn=compiler.D3DStripShader;fn.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_uint,ctypes.POINTER(ctypes.c_void_p)];fn.restype=ctypes.c_long
 out=ctypes.c_void_p();b=bytes(result);hr=fn(b,len(b),0,ctypes.byref(out));assert hr>=0,hex(hr&0xffffffff);blob_bytes(out)
 return b,changed

def program_patch(data,kind):
 count=struct.unpack_from('<I',data)[0];parts=[];changed=0
 for i in range(count):
  offset,size=struct.unpack_from('<II',data,4+8*i);part=data[offset:offset+size]
  # Locate the byte-array field after version, program type, flags and keywords.
  at=24;nk=struct.unpack_from('<I',part,at)[0];at+=4
  for k in range(nk):ln=struct.unpack_from('<I',part,at)[0];at=(at+4+ln+3)&~3
  sizecode=struct.unpack_from('<I',part,at)[0];start=at+4;code=part[start:start+sizecode];pos=code.find(b'DXBC')
  if pos>=0:
   n=struct.unpack_from('<I',code,pos+24)[0];new,change=dxbc_patch(code[pos:pos+n],kind);changed+=change
   tail=part[(start+sizecode+3)&~3:]
   code=code[:pos]+new+code[pos+n:];part=part[:at]+struct.pack('<I',len(code))+code
   part=part.ljust((len(part)+3)&~3,b'\0')+tail
  parts.append(part)
 result=bytearray(struct.pack('<I',count)+b'\0'*(count*8))
 for i,part in enumerate(parts):struct.pack_into('<II',result,4+8*i,len(result),len(part));result.extend(part)
 return bytes(result),changed

def main():
 ap=argparse.ArgumentParser();ap.add_argument('source');ap.add_argument('output');args=ap.parse_args();out=Path(args.output);out.mkdir(parents=True,exist_ok=True);report=[]
 for name in ['resources.assets','sharedassets0.assets']:
  original=Path(args.source)/name
  expected={'resources.assets':'be76ef0ab550fa42859bae26a6785171239ab6c546ad2e9898621da5222bbafe','sharedassets0.assets':'5fc999a1c8d5b70808b64c5ceb393f38e4a5c31ba7e1a3f14475bbe661080072'}
  assert hashlib.sha256(original.read_bytes()).hexdigest()==expected[name], 'Expected untouched 1.78 shader assets'
  env=UnityPy.load(str(original));total=0
  for obj in env.objects:
   if obj.type.name!='Shader':continue
   tr=obj.read_typetree();shader=tr['m_ParsedForm']['m_Name'];kind='falloff' if shader=='Hidden/Blit_Seamless_Texture_Maker' else 'mask' if shader in ['Hidden/Blit_Shader','Custom/Edit_Diffuse_Preview'] else None
   if not kind:continue
   data=CompressionHelper.decompress_lz4(bytes(tr['compressedBlob']),tr['decompressedLengths'][0]);new,n=program_patch(data,kind)
   assert n==(2 if kind=='falloff' else 4 if shader=='Hidden/Blit_Shader' else 2),(shader,n)
   packed=CompressionHelper.compress_lz4(new);tr['compressedBlob']=list(packed);tr['compressedLengths']=[len(packed)];tr['decompressedLengths']=[len(new)];obj.save_typetree(tr);total+=n;report.append([name,shader,n])
  (out/name).write_bytes(env.file.save());print(name,total)
 (out/'shader-patches.json').write_text(json.dumps(report,indent=2))
if __name__=='__main__':main()

