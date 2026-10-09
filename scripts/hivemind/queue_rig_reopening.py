"""Wait for the exact rig capture, then verify it without any capture restart."""
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT=Path(r"F:\OpenRPG_ImportWork\unity-medieval-rig-source-capture-v1")
VERIFIER=Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust\scripts\hivemind\verify_native_rig_capture.py")
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def run():
 queued=read(ROOT/"REOPEN-QUEUE.v1.json");job=read(ROOT/"JOB.v1.json")
 kernel=ctypes.WinDLL("kernel32",use_last_error=True)
 kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE
 kernel.GetProcessTimes.argtypes=[wintypes.HANDLE,*[ctypes.POINTER(wintypes.FILETIME)]*4]
 kernel.WaitForSingleObject.argtypes=[wintypes.HANDLE,wintypes.DWORD];kernel.CloseHandle.argtypes=[wintypes.HANDLE]
 handle=kernel.OpenProcess(0x101000,False,job["pid"])
 if handle:
  try:
   times=[wintypes.FILETIME() for _ in range(4)]
   if not kernel.GetProcessTimes(handle,*[ctypes.byref(t) for t in times]):raise ctypes.WinError(ctypes.get_last_error())
   born=times[0].dwHighDateTime<<32|times[0].dwLowDateTime
   expected=datetime.datetime.fromisoformat(job["created_utc"]);epoch=datetime.datetime(1601,1,1,tzinfo=datetime.timezone.utc)
   if abs(born-int((expected-epoch).total_seconds()*10000000))>20000000:raise RuntimeError("Recycled capture PID")
   while True:
    result=kernel.WaitForSingleObject(handle,45000)
    if result==0:break
    if result!=258:raise ctypes.WinError(ctypes.get_last_error())
  finally:kernel.CloseHandle(handle)
 elif ctypes.get_last_error()!=87:raise ctypes.WinError(ctypes.get_last_error())
 if read(ROOT/"RUN-RESULT.v1.json")["status"]!="native_rig_source_captured_independent_reopening_remaining":raise RuntimeError("Complete rig capture unavailable; verification withheld")
 if sha(VERIFIER)!=queued["verifier_sha256"]:raise RuntimeError("Queued verifier changed")
 with (ROOT/"reopening.stdout.log").open("xb") as out,(ROOT/"reopening.stderr.log").open("xb") as err:
  code=subprocess.run([sys.executable,str(VERIFIER)],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=out,stderr=err,check=False).returncode
 report={"actual_verifier_exit_code":code,"native_capture_rerun":False,"verifier_sha256":sha(VERIFIER),"full_import":False}
 with (ROOT/"REOPEN-RUN.v1.json").open("x",encoding="utf-8") as output:json.dump(report,output,indent=2)
 print(json.dumps(report))
def prepare():
 queue=ROOT/"REOPEN-QUEUE.v1.json"
 with queue.open("x",encoding="utf-8") as output:json.dump({"verifier_sha256":sha(VERIFIER),"capture_job":read(ROOT/"JOB.v1.json")},output,indent=2)
 with (ROOT/"reopening-controller.log").open("xb") as log:
  process=subprocess.Popen([sys.executable,str(Path(__file__)),"run"],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 with (ROOT/"REOPEN-JOB.v1.json").open("x",encoding="utf-8") as output:json.dump({"pid":process.pid,"created_utc":datetime.datetime.now(datetime.timezone.utc).isoformat()},output,indent=2)
 print(json.dumps({"queued_verifier_pid":process.pid,"native_capture_rerun":False}))
if __name__=="__main__":{"prepare":prepare,"run":run}[sys.argv[1]]()
