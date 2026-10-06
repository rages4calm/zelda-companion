// Isolated regression harness: real native bridge + real engine recoil routine.
// Visual reload and unrelated enemy-specific effects are counted/stubbed here.
#include <winsock2.h>
#define SDL_MAIN_HANDLED
#include <SDL.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "src/types.h"
#include "src/variables.h"
#include "src/zelda_rtl.h"
#include "snes/ppu.h"
uint8 g_ram[131072];ZeldaEnv g_zenv;
static int gfx_calls,hud_calls,tests;
void DecompressSwordGraphics(){gfx_calls++;}void Palette_Load_Sword(){gfx_calls++;}
void DecompressShieldGraphics(){gfx_calls++;}void Palette_Load_Shield(){gfx_calls++;}
void Palette_Load_LinkArmorAndGloves(){gfx_calls++;}void Hud_RefreshIcon(){hud_calls++;}
void CreatePyramidHole(){}void Sprite_LoadGraphicsProperties(){}void Overworld_SetSongList(){}
#include "src/online.c"
// Generated directly from the corresponding engine function, with its source
// SHA256 recorded by run-native-tests.py. No replacement arithmetic is tested.
#include "engine_recoil_fragment.h"
static void Check(bool ok,const char *text){if(!ok){fprintf(stderr,"FAIL %s\n",text);exit(1);}printf("PASS %s\n",text);tests++;}
static SOCKET server;static struct sockaddr_in client;
static void Frame(uint8 *f,int player,uint32 seq){memset(f,0,PACKET);memcpy(f,"Z3N2",4);Write32(f+4,token);Write32(f+8,seq);f[12]=1;f[13]=player;f[14]=1;f[15]=7;Write32(f+26,1234+player);Write16(f+20,1);Write16(f+22,60+player*24);Write16(f+24,100);sprintf((char*)f+NAME_OFF,"Player %d",player);}
static void Send(uint8 *f){sendto(server,(char*)f,PACKET,0,(struct sockaddr*)&client,sizeof(client));SDL_Delay(10);OnlineTick();}
int main(void){
  SDL_Init(SDL_INIT_TIMER);WSADATA wsa;WSAStartup(MAKEWORD(2,2),&wsa);server=socket(AF_INET,SOCK_DGRAM,IPPROTO_UDP);
  struct sockaddr_in local;memset(&local,0,sizeof(local));local.sin_family=AF_INET;local.sin_addr.s_addr=htonl(INADDR_LOOPBACK);bind(server,(struct sockaddr*)&local,sizeof(local));int len=sizeof(local);getsockname(server,(struct sockaddr*)&local,&len);
  char port[16];sprintf(port,"%d",ntohs(local.sin_port));_putenv_s("Z3ONLINE_PORT",port);_putenv_s("Z3ONLINE_TOKEN","abcdef12");_putenv_s("Z3ONLINE_SLOT","1");_putenv_s("Z3ONLINE_NAME","Crono");
  static Ppu ppu;g_zenv.ppu=&ppu;ppu.mode=1;ppu.brightness=15;OnlineInit();len=sizeof(client);getsockname(sock,(struct sockaddr*)&client,&len);Check(sock!=INVALID_SOCKET,"native loopback socket initializes");
  main_module_index=7;submodule_index=0;dungeon_room_index=1;player_is_indoors=1;link_palette_bits_of_oam=0xe00;link_armor=2;
  for(int i=0;i<256;i++)ppu.cgram[i]=i*97;uint16 colors[256];memcpy(colors,ppu.cgram,sizeof(colors));uint8 ram[131072];memcpy(ram,g_ram,sizeof(ram));
  for(int s=1;s<=8;s++){slot=s;OnlineBeginColors();Check(ppu.cgram[0xf9]==player_tunic[s-1][0],"unique player tunic selected");OnlineRestoreColors();Check(!memcmp(colors,ppu.cgram,sizeof(colors))&&!memcmp(ram,g_ram,sizeof(ram)),"palette restored; RAM and armor unchanged");}slot=1;
  Check(player_tunic[0][0]!=player_tunic[7][0],"eight-player identity palette includes silver");
  main_module_index=14;OnlineBeginColors();Check(!tunic_override,"menus retain original palette");main_module_index=7;
  uint8 f[PACKET];Frame(f,2,1);f[STATE_OFF+10]=1;main_module_index=14;Send(f);Check(link_item_torch==0&&pending[10]==1,"lantern grant waits for menu/dialogue to close");
  main_module_index=7;OnlineTick();Check(link_item_torch==1,"native lantern grant applied in normal play");Check(link_item_in_hand==0,"received lantern does not force an equipped action");
  Frame(f,2,2);f[STATE_OFF+25]=3;f[STATE_OFF+26]=2;f[STATE_OFF+27]=1;f[STATE_OFF+28]=2;f[STATE_OFF+44]=42;f[STATE_OFF+52]=5;f[STATE_OFF+58]=3;f[STATE_OFF+38]=0x80;
  link_health_current=10;link_num_arrows=5;link_rupees_actual=123;link_item_bombs=8;Send(f);
  Check(link_sword_type==3&&link_shield_type==2&&link_armor==2&&gfx_calls==4,"sword/shield ownership reloads graphics; existing stronger armor retained");
  Check(link_bottle_info[0]==2&&link_item_bottle_index==1,"shared bottle ownership grants an empty selectable bottle");
  Check(link_health_capacity==40&&link_heart_pieces==2,"heart capacity and piece progress shared together");
  Check(link_health_current==10&&link_num_arrows==5&&link_rupees_actual==123&&link_item_bombs==8,"damage, ammunition and rupees remain personal");
  Check(link_which_pendants==5&&link_has_crystals==3&&(link_bigkey&0x80),"pendants crystals and big keys merge as sets");
  Frame(f,2,3);f[STATE_OFF+28]=2;link_bottle_info[0]=5;Send(f);Check(link_bottle_info[0]==5,"peer cannot replace local bottle contents");
  for(int s=3;s<=8;s++){Frame(f,s,1);Send(f);Check(peers[s-1].active&&peers[s-1].sequence==1,"independent peer sequence accepted for additional player");}
  Frame(f,8,1);f[STATE_OFF+5]=1;Send(f);Check(!link_item_fire_rod,"stale frame cannot grant new equipment");
  Frame(f,8,1);Write32(f+26,9001);f[STATE_OFF+5]=1;Send(f);Check(link_item_fire_rod&&peers[7].epoch==9001,"replacement epoch safely resets per-slot sequence");
  Frame(f,2,4);Write32(f+4,token+1);f[STATE_OFF+6]=1;Send(f);Check(!link_item_ice_rod,"wrong local token rejected");
  Frame(f,2,4);f[STATE_OFF+6]=2;Send(f);Check(!link_item_ice_rod,"invalid equipment value rejected");
  Frame(f,9,1);f[STATE_OFF+6]=1;Send(f);Check(!link_item_ice_rod,"unknown ninth slot rejected");
  Frame(f,2,4);f[WORLD_OFF]=0x30;f[WORLD_OFF+1]=0x80;f[WORLD_OFF+592+0x3b]=0x20;f[WORLD_OFF+720]=2;f[WORLD_OFF+721]=0x10;Send(f);
  Check((save_dung_info[0]&0x8000)&&!(save_dung_info[0]&0x30),"door progress shared while chest contents stay private");
  Check(!(save_ow_event_info[0x3b]&0x20),"resettable swamp watergate excluded");
  Check(sram_progress_indicator==0&&!(sram_progress_flags&0x10),"remote progress cannot strand a new player in bed");
  sram_progress_indicator=1;OnlineTick();Check(sram_progress_indicator==2,"shared story stage applied after personal opening sequence");
  // Palette/capture/labels run with the same native PPU structures as the game.
  OnlineCapture();uint32 guarded[426*240+2];guarded[0]=guarded[426*240+1]=0x12345678;memset(guarded+1,0,426*240*4);
  OnlineDraw((uint8*)(guarded+1),426*4,426,240,1);int ink=0;for(int i=0;i<426*240;i++)if(guarded[i+1])ink++;
  Check(ink>200,"multiple actual GDI name labels render on native framebuffer");Check(guarded[0]==0x12345678&&guarded[426*240+1]==0x12345678,"name and avatar rendering preserves framebuffer guards");
  strcpy((char*)f+NAME_OFF,"Zo\xc3\xab \xe6\x9e\x97");MakeLabel(&peers[7],(char*)f+NAME_OFF);Check(peers[7].label_width>0,"Unicode player name rasterized");
  LabelRect crowded[8],body={195,60,18,24};bool separate=true;
  for(int i=0;i<8;i++){crowded[i]=PlaceLabel(204,44,28,426,240,crowded,i,&body,1);separate&=crowded[i].y>=56;for(int j=0;j<i;j++)separate&=!Intersects(crowded[i],crowded[j]);}
  Check(separate,"eight crowded nameplates avoid each other and the HUD");
  // Cooperative combat: follower's native damage is counted once by the leader,
  // then consumed by the engine's actual recoil/death function.
  combat_enabled=true;for(int i=0;i<8;i++)peers[i].active=false;slot=1;OnlineCapture();sprite_type[0]=0x41;sprite_state[0]=9;sprite_health[0]=12;
  OnlineBeforeFrame();OnlinePeer *p=&peers[1];Frame(p->frame,2,100);p->active=true;p->last_seen=SDL_GetTicks();uint8 *fight=p->frame+FIGHT_OFF;fight[192]=1;fight[193]=1;Write32(fight+194,own_visit);fight[0]=0x41;fight[1]=9;Write32(fight+128,2);
  OnlineBeforeFrame();Check(sprite_give_damage[0]==2,"leader accepts follower's bounded damage report");Sprite_MiniMoldorm_Recoil(0);Check(sprite_health[0]==10,"actual engine recoil routine applies cooperative damage");
  OnlineBeforeFrame();Check(sprite_give_damage[0]==0&&sprite_health[0]==10,"repeated report does not damage enemy twice");
  Write32(fight+128,102);OnlineBeforeFrame();Check(sprite_give_damage[0]==0,"oversized damage delta discarded");
  Write32(fight+128,104);Write16(p->frame+20,99);OnlineBeforeFrame();Check(sprite_give_damage[0]==0,"different-room damage discarded");Write16(p->frame+20,1);
  Write32(fight+194,own_visit+2);OnlineBeforeFrame();Check(sprite_give_damage[0]==0,"old room-owner visit discarded");
  Write32(fight+194,own_visit);Write32(fight+128,116);OnlineBeforeFrame();Sprite_MiniMoldorm_Recoil(0);Check(sprite_state[0]==6,"actual engine enters death state after shared lethal damage");
  // Higher-slot client follows player one's enemy position and reports its own
  // sword/projectile damage, with local Link health still untouched.
  slot=2;outgoing[13]=2;peers[1].active=false;p=&peers[0];Frame(p->frame,1,1);p->active=true;p->last_seen=SDL_GetTicks();fight=p->frame+FIGHT_OFF;fight[192]=1;fight[193]=1;Write32(fight+194,4321);fight[0]=0x41;fight[1]=9;fight[2]=150;fight[4]=120;fight[6]=12;sprite_state[0]=9;sprite_health[0]=12;
  OnlineBeforeFrame();Check(sprite_x_lo[0]==150&&sprite_y_lo[0]==120&&leader_slot==1,"follower adopts existing enemy position from area leader");
  sprite_health[0]=10;OnlineAfterFrame();Check(hit_count[0]==2,"follower counts local engine damage for relay");
  p->active=false;OnlineBeforeFrame();Check(leader_slot==2&&hit_count[0]==0,"remaining player takes authority after leader departure");
  Check(link_health_current==10&&link_rupees_actual==123,"combat exchange leaves private health and money intact");
  sprite_floor[0]=255;OnlineCapture();Check(outgoing[FIGHT_OFF]==0&&ValidFight(outgoing+FIGHT_OFF),"unsupported floor is omitted without invalidating the outgoing frame");
  OnlineClose();Check(sock==INVALID_SOCKET,"native socket and cosmetic palette cleaned up");closesocket(server);WSACleanup();SDL_Quit();printf("%d native co-op checks passed.\n",tests);return 0;
}
