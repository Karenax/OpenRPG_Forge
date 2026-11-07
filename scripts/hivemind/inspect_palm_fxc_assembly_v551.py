"""Inspect a controlled FXC compilation; not a claim about Unity's exact compiled blob."""
from pathlib import Path
import ctypes as c,hashlib,json
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-fxc-assembly-v551'
P=W/'pirate-palm-wind-native-reference-v547/native-forward-wind.compute'
assert not O.exists();O.mkdir()
dll=Path('C:/Windows/System32/d3dcompiler_47.dll');lib=c.WinDLL(str(dll))
def bytes_of(blob):
    table=c.cast(blob,c.POINTER(c.POINTER(c.c_void_p))).contents
    address=c.WINFUNCTYPE(c.c_void_p,c.c_void_p)(table[3])(blob)
    size=c.WINFUNCTYPE(c.c_size_t,c.c_void_p)(table[4])(blob)
    return c.string_at(address,size)
def release(blob):
    if blob:
        table=c.cast(blob,c.POINTER(c.POINTER(c.c_void_p))).contents
        c.WINFUNCTYPE(c.c_ulong,c.c_void_p)(table[2])(blob)
src=P.read_bytes();data=c.create_string_buffer(src)
lib.D3DCompile.argtypes=[c.c_void_p,c.c_size_t,c.c_char_p,c.c_void_p,c.c_void_p,c.c_char_p,c.c_char_p,c.c_uint,c.c_uint,c.POINTER(c.c_void_p),c.POINTER(c.c_void_p)]
lib.D3DCompile.restype=c.c_long
code=c.c_void_p();err=c.c_void_p()
# Explicit optimization level 3; Unity's actual flags are not inferred.
hr=lib.D3DCompile(data,len(src),str(P).encode(),None,None,b'CSMain',b'cs_5_0',1<<15,0,c.byref(code),c.byref(err))
if err:(O/'compiler.log').write_bytes(bytes_of(err));release(err)
assert hr>=0,hex(hr & 0xffffffff)
bytecode=bytes_of(code);(O/'controlled-fxc.dxbc').write_bytes(bytecode)
lib.D3DDisassemble.argtypes=[c.c_void_p,c.c_size_t,c.c_uint,c.c_char_p,c.POINTER(c.c_void_p)];lib.D3DDisassemble.restype=c.c_long
assembly=c.c_void_p();buf=c.create_string_buffer(bytecode)
assert lib.D3DDisassemble(buf,len(bytecode),0,None,c.byref(assembly))>=0
(O/'controlled-fxc.asm').write_bytes(bytes_of(assembly));release(assembly);release(code)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
result=dict(status='controlled_fxc_cs5_assembly_captured',source_sha256=sha(P),compiler_sha256=sha(dll),assembly_sha256=sha(O/'controlled-fxc.asm'),dxbc_sha256=sha(O/'controlled-fxc.dxbc'),optimization_flags=1<<15,actual_unity_binary=False,full_import=False)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
