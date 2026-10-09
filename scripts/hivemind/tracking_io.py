"""Experimental native report reader; not admitted to the production tracker.

Both native and CRT SHARE_DELETE controls still blocked replacement on F.
Writer retries remain required. This does not replace asset integrity readers.
"""
from contextlib import contextmanager
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import time


@contextmanager
def open_shared_report(path):
    if os.name != 'nt':
        with Path(path).open('rb') as stream:
            yield stream
        return
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    create = kernel.CreateFileW
    create.argtypes = [wintypes.LPCWSTR, wintypes.DWORD, wintypes.DWORD,
                       ctypes.c_void_p, wintypes.DWORD, wintypes.DWORD, wintypes.HANDLE]
    create.restype = wintypes.HANDLE
    close = kernel.CloseHandle
    close.argtypes = [wintypes.HANDLE]
    close.restype = wintypes.BOOL
    # GENERIC_READ; SHARE_READ | SHARE_WRITE | SHARE_DELETE; OPEN_EXISTING.
    handle = create(str(Path(path).resolve()), 0x80000000, 7, None, 3, 0, None)
    if handle == ctypes.c_void_p(-1).value:
        raise ctypes.WinError(ctypes.get_last_error())
    native_read = kernel.ReadFile
    native_read.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                           ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
    native_read.restype = wintypes.BOOL

    class ReportHandle:
        def read(self):
            blocks = []
            buffer = ctypes.create_string_buffer(1024 * 1024)
            while True:
                count = wintypes.DWORD()
                if not native_read(handle, buffer, len(buffer), ctypes.byref(count), None):
                    raise ctypes.WinError(ctypes.get_last_error())
                if not count.value:
                    return b''.join(blocks)
                blocks.append(buffer.raw[:count.value])

    try:
        yield ReportHandle()
    finally:
        close(handle)


def read_report(path, attempts=20):
    for attempt in range(attempts):
        try:
            with open_shared_report(path) as stream:
                return json.loads(stream.read().decode('utf-8-sig'))
        except (PermissionError, json.JSONDecodeError):
            if attempt + 1 == attempts:
                raise
            time.sleep(0.1)
