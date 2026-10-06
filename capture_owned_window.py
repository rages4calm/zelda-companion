import sys
from pathlib import Path
from PIL import ImageGrab
# Called by the UI harness with its own form's HWND; never captures the desktop.
target=Path(sys.argv[2]);target.parent.mkdir(parents=True,exist_ok=True)
ImageGrab.grab(window=int(sys.argv[1])).save(target)
