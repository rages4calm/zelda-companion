#define SDL_MAIN_HANDLED
#include "engine/src/config.h"
#include <stdio.h>
#include <stdlib.h>
#include <stdarg.h>
#include <SDL.h>

void NORETURN Die(const char *error) { fprintf(stderr,"%s\n",error); exit(3); }

static int passed;
#define CHECK(c, text) do { if (!(c)) { fprintf(stderr, "FAILED %s\n", text); return 1; } printf("PASS %s\n", text); passed++; } while (0)
int main(int argc,char **argv) {
  if (argc!=2) return 2;
  ParseConfigFile(argv[1]);
  CHECK(FindCmdForSdlKey(SDLK_g,KMOD_NONE)==kKeys_Controls+7,"native parser applies custom sword key");
  CHECK(FindCmdForSdlKey(SDLK_z,KMOD_NONE)==0,"old sword key remains unassigned");
  CHECK(FindCmdForSdlKey(SDLK_F12,KMOD_CTRL)==kKeys_Fullscreen,"native keyboard hotkey accepts Ctrl combination");
  CHECK(FindCmdForSdlKey(SDLK_F12,KMOD_NONE)==0,"keyboard modifier is required");
  CHECK(FindCmdForGamepadButton(kGamepadBtn_R3,1<<kGamepadBtn_R3)==kKeys_Controls+7,"native controller remap applies to sword");
  CHECK(FindCmdForGamepadButton(kGamepadBtn_Start,(1<<kGamepadBtn_Start)|(1<<kGamepadBtn_L1))==kKeys_PauseDimmed,"native controller combination invokes pause");
  CHECK(FindCmdForGamepadButton(kGamepadBtn_Start,1<<kGamepadBtn_Start)==kKeys_Controls+5,"Start without bumper still opens inventory");
  CHECK(FindCmdForGamepadButton(kGamepadBtn_Back,(1<<kGamepadBtn_Back)|(1<<kGamepadBtn_L2))==kKeys_Save,"trigger combination invokes native save-state slot one");
  CHECK(FindCmdForSdlKey(SDLK_F1,KMOD_SHIFT)==kKeys_Save,"keyboard save-state default retained");
  CHECK(g_config.enable_audio,"sound option retained after bindings save");
  printf("%d native controls checks passed.\n",passed);
  return 0;
}
