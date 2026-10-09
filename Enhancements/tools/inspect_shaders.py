import sys, ctypes, struct
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'unity-assets'))
import UnityPy
from UnityPy.helpers import CompressionHelper

compiler=ctypes.WinDLL('d3dcompiler_47.dll')
def blob_bytes(blob):
    vt=ctypes.cast(blob,ctypes.POINTER(ctypes.POINTER(ctypes.c_void_p))).contents
    pointer=ctypes.WINFUNCTYPE(ctypes.c_void_p,ctypes.c_void_p)(vt[3])(blob)
    size=ctypes.WINFUNCTYPE(ctypes.c_size_t,ctypes.c_void_p)(vt[4])(blob)
    data=ctypes.string_at(pointer,size)
    ctypes.WINFUNCTYPE(ctypes.c_ulong,ctypes.c_void_p)(vt[2])(blob)
    return data
def disassemble(data):
    out=ctypes.c_void_p()
    fn=compiler.D3DDisassemble
    fn.argtypes=[ctypes.c_void_p,ctypes.c_size_t,ctypes.c_uint,ctypes.c_char_p,ctypes.POINTER(ctypes.c_void_p)]
    fn.restype=ctypes.c_long
    hr=fn(data,len(data),0,None,ctypes.byref(out))
    if hr<0:raise RuntimeError(hex(hr&0xffffffff))
    return blob_bytes(out).decode('ascii')

if __name__=='__main__':
    out=Path('artifacts/shader-audit');out.mkdir(parents=True,exist_ok=True)
    for name in ['resources.assets','sharedassets0.assets']:
        env=UnityPy.load('E:/Materialize_1.78/Materialize_Data/'+name)
        for obj in env.objects:
            if obj.type.name!='Shader':continue
            d=obj.read();data=CompressionHelper.decompress_lz4(bytes(d.compressedBlob),d.decompressedLengths[0])
            pos=0;count=0
            while True:
                pos=data.find(b'DXBC',pos)
                if pos<0:break
                n=struct.unpack_from('<I',data,pos+24)[0]
                code=data[pos:pos+n];text=disassemble(code)
                (out/(name+'-'+str(obj.path_id)+'-'+(d.m_Name or 'unnamed')+'-'+str(count)+'.asm')).write_text(text)
                pos+=n;count+=1
            print(name,obj.path_id,d.m_Name,count)
