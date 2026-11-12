"""Windows exclusive, handle-based retirement for verified source copies."""
import ctypes
import hashlib
import msvcrt
import os
from pathlib import Path


class ExclusiveSource:
    def __init__(self, path):
        if os.name != 'nt':
            raise ValueError('Windows exclusive deletion required')
        self.path = Path(path)
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        create = kernel.CreateFileW
        create.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p,
                           ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
        create.restype = ctypes.c_void_p
        name = str(self.path)
        if not name.startswith('\\\\?\\'):
            name = '\\\\?\\' + name
        handle = create(name, 0x80000000 | 0x00010000, 0, None, 3, 0x00200000, None)
        if handle == ctypes.c_void_p(-1).value:
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            fd = msvcrt.open_osfhandle(handle, os.O_RDONLY | os.O_BINARY)
            self.stream = os.fdopen(fd, 'rb')
        except BaseException:
            kernel.CloseHandle.argtypes = [ctypes.c_void_p]
            kernel.CloseHandle(handle)
            raise

    def sha256(self):
        self.stream.seek(0)
        return hashlib.file_digest(self.stream, 'sha256').hexdigest()

    def mark_delete(self):
        class Disposition(ctypes.Structure):
            _fields_ = [('delete_file', ctypes.c_ubyte)]
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        func = kernel.SetFileInformationByHandle
        func.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_uint32]
        func.restype = ctypes.c_int
        info = Disposition(1)
        handle = msvcrt.get_osfhandle(self.stream.fileno())
        if not func(handle, 4, ctypes.byref(info), ctypes.sizeof(info)):
            raise ctypes.WinError(ctypes.get_last_error())

    def close(self):
        self.stream.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
