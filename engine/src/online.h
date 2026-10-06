#ifndef ZELDA_ONLINE_H
#define ZELDA_ONLINE_H
// Optional local companion bridge. With no Z3ONLINE_PORT this is a no-op.
void OnlineInit(void);
void OnlineTick(void);
void OnlineBeforeFrame(void);
void OnlineAfterFrame(void);
void OnlineBeginColors(void);
void OnlineCapture(void);
void OnlineRestoreColors(void);
void OnlineDraw(unsigned char *pixels, int pitch, int width, int height, int scale);
void OnlineClose(void);
#endif
