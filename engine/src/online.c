// Copyright (c) 2026 Carl Prewitt Jr. MIT, see ONLINE-LICENSE.txt.
// Native co-op bridge. Never accepts arbitrary RAM writes or full save states.
#include <winsock2.h>
#include <windows.h>
#include <SDL.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include "online.h"
#include "types.h"
#include "variables.h"
#include "zelda_rtl.h"
#include "load_gfx.h"
#include "hud.h"
#include "overworld.h"
#include "sprite.h"
#include "snes/ppu.h"

enum { MAX_PLAYERS=8, HEADER=256, SW=32, SH=48, WORLD_OFF=HEADER+SW*SH*2,
  WORLD_SIZE=736, FIGHT_OFF=WORLD_OFF+WORLD_SIZE, FIGHT_SIZE=256, PACKET=FIGHT_OFF+FIGHT_SIZE,
  NAME_OFF=32, NAME_BYTES=97, STATE_OFF=160, STATE_SIZE=64, LABEL_W=128, LABEL_H=9 };
typedef struct { uint8 offset, max, bits; } SharedField;
static const SharedField fields[] = {
  {0,3,0},{1,2,0},{2,1,0},{4,2,0},{5,1,0},{6,1,0},{7,1,0},{8,1,0},
  {9,1,0},{10,1,0},{11,1,0},{12,3,0},{13,1,0},{14,1,0},{16,1,0},
  {17,1,0},{18,1,0},{19,2,0},{20,2,0},{21,1,0},{22,1,0},{23,1,0},
  {25,4,0},{26,3,0},{27,2,0},{28,2,0},{29,2,0},{30,2,0},{31,2,0},
  {36,255,1},{37,255,1},{38,255,1},{39,255,1},
  {40,255,1},{41,255,1},{44,163,0},{48,50,0},{49,70,0},{52,7,1},{57,7,1},
  {58,127,1},{59,2,0}
};
typedef struct {
  uint8 frame[PACKET]; uint32 sequence, epoch, last_seen; bool active;
  char name[NAME_BYTES]; uint8 label[LABEL_W*LABEL_H]; int label_width;
} OnlinePeer;
static SOCKET sock=INVALID_SOCKET;
static struct sockaddr_in bridge;
static uint32 token, sequence, last_send;
static uint8 slot, outgoing[PACKET], pending[STATE_SIZE];
static OnlinePeer peers[MAX_PLAYERS];
static uint8 shared_world[WORLD_SIZE];
static bool combat_enabled, before_stable;
static bool show_labels=true;
static uint8 leader_slot, before_health[16], before_state[16], before_type[16];
static uint16 battle_area, before_area;
static uint8 battle_indoors, battle_world, battle_floor;
static uint32 own_visit, leader_visit, hit_count[16], consumed_hits[MAX_PLAYERS][16], before_visit;
static bool SameArea(const uint8 *f);
static uint16 saved_tunic[4]; static int tunic_palette_base; static bool tunic_override;
static uint16 Read16(const uint8 *p){return p[0]|p[1]<<8;}
static uint32 Read32(const uint8 *p){return (uint32)Read16(p)|(uint32)Read16(p+2)<<16;}
static void Write16(uint8 *p,uint16 v){p[0]=v;p[1]=v>>8;}
static void Write32(uint8 *p,uint32 v){Write16(p,v);Write16(p+2,v>>16);}
static bool Stable(void){return (main_module_index==7||main_module_index==9)&&submodule_index==0;}
static bool ValidState(const uint8 *s){
  for(int i=0;i<STATE_SIZE;i++){
    int max=0;for(int j=0;j<sizeof(fields)/sizeof(fields[0]);j++)if(fields[j].offset==i)max=fields[j].max;
    if(s[i]>max)return false;
  }
  for(int i=28;i<32;i++)if(s[i]!=0&&s[i]!=2)return false;
  return s[0]!=2 && (!s[44] || (s[44]>=24 && (s[44]&7)<=3));
}
static uint8 WorldMask(int i){return i<592?(i&1?0xfc:0x0f):(i==592+0x3b||i==592+0x7b)?0xdf:i==721?0xbf:0xff;}
static bool ValidWorld(const uint8 *w){if(w[720]>3||w[722]>8||w[723])return false;for(int i=725;i<WORLD_SIZE;i++)if(w[i])return false;return true;}
static void MergeWorld(uint8 *d,const uint8 *s){for(int i=0;i<725;i++)d[i]=(i==720||i==722)?(d[i]>s[i]?d[i]:s[i]):d[i]|(s[i]&WorldMask(i));}
static bool CombatType(uint8 t){switch(t){case 0x08:case 0x0a:case 0x0e:case 0x11:case 0x12:case 0x18:case 0x19:case 0x20:case 0x22:case 0x41:case 0x42:case 0x43:case 0x45:case 0x46:case 0x47:case 0x48:case 0x49:case 0x4a:case 0x4b:case 0x4c:case 0x4e:case 0x4f:case 0x51:case 0x55:case 0x56:case 0x58:case 0x6a:case 0x6b:case 0x6d:case 0x6e:case 0x6f:case 0x71:return true;}return false;}
static bool ValidFight(const uint8 *f){
  if(f[192]>1||f[193]>MAX_PLAYERS)return false;for(int i=198;i<FIGHT_SIZE;i++)if(f[i])return false;
  for(int k=0;k<16;k++){const uint8 *s=f+k*8;bool any=false;for(int j=0;j<8;j++)any|=s[j]!=0;if(any&&(!CombatType(s[0])||s[1]>11||s[7]>1))return false;}return true;
}
static void MergeState(uint8 *d,const uint8 *s){
  for(int i=0;i<sizeof(fields)/sizeof(fields[0]);i++){
    int a=fields[i].offset;d[a]=fields[i].bits ? d[a]|s[a] : d[a]>s[a]?d[a]:s[a];
  }
}
// Four temporary CGRAM entries, restored before save-state capture. Ownership
// and actual armor defense retain the acquired equipment's real values.
#define RGB5(r,g,b) ((r)|((g)<<5)|((b)<<10))
static const uint16 player_tunic[MAX_PLAYERS][4]={
  {0x3647,0x3b68,0x0a4a,0x12ef},
  {RGB5(5,12,23),RGB5(12,24,31),RGB5(3,9,21),RGB5(9,19,31)},
  {RGB5(23,5,5),RGB5(31,14,11),RGB5(19,3,4),RGB5(30,9,8)},
  {RGB5(18,7,24),RGB5(27,17,31),RGB5(13,4,19),RGB5(23,12,30)},
  {RGB5(23,17,3),RGB5(31,27,9),RGB5(18,12,2),RGB5(29,23,5)},
  {RGB5(2,20,20),RGB5(10,31,29),RGB5(1,14,16),RGB5(6,27,26)},
  {RGB5(24,8,17),RGB5(31,19,27),RGB5(18,4,12),RGB5(29,13,23)},
  {RGB5(18,19,22),RGB5(29,30,31),RGB5(12,14,17),RGB5(24,26,29)}
};
void OnlineBeginColors(void){
  if(tunic_override||sock==INVALID_SOCKET||slot<1||slot>MAX_PLAYERS||!Stable()||g_zenv.ppu->forcedBlank||g_zenv.ppu->mode==7||
    (link_palette_bits_of_oam!=0&&link_palette_bits_of_oam!=0xe00))return;
  tunic_palette_base=link_palette_bits_of_oam?0xf0:0x80;
  for(int i=0;i<4;i++){saved_tunic[i]=g_zenv.ppu->cgram[tunic_palette_base+9+i];g_zenv.ppu->cgram[tunic_palette_base+9+i]=player_tunic[slot-1][i];}
  tunic_override=true;
}
void OnlineRestoreColors(void){if(!tunic_override)return;for(int i=0;i<4;i++)g_zenv.ppu->cgram[tunic_palette_base+9+i]=saved_tunic[i];tunic_override=false;}
static void MakeLabel(OnlinePeer *p,const char *name){
  if(!strcmp(p->name,name))return;
  memcpy(p->name,name,NAME_BYTES);memset(p->label,0,sizeof(p->label));p->label_width=0;
  WCHAR wide[NAME_BYTES];int n=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,name,-1,wide,NAME_BYTES);if(!n)return;
  HDC dc=CreateCompatibleDC(NULL);if(!dc)return;
  BITMAPINFO info;memset(&info,0,sizeof(info));info.bmiHeader.biSize=sizeof(BITMAPINFOHEADER);info.bmiHeader.biWidth=LABEL_W;info.bmiHeader.biHeight=-LABEL_H;info.bmiHeader.biPlanes=1;info.bmiHeader.biBitCount=32;info.bmiHeader.biCompression=BI_RGB;
  uint32 *bits=NULL;HBITMAP bitmap=CreateDIBSection(dc,&info,DIB_RGB_COLORS,(void**)&bits,NULL,0);
  HFONT font=CreateFontW(-7,0,0,0,FW_BOLD,0,0,0,DEFAULT_CHARSET,OUT_DEFAULT_PRECIS,CLIP_DEFAULT_PRECIS,NONANTIALIASED_QUALITY,DEFAULT_PITCH,L"Tahoma");
  if(bitmap&&font){HGDIOBJ old_bitmap=SelectObject(dc,bitmap),old_font=SelectObject(dc,font);memset(bits,0,LABEL_W*LABEL_H*4);SetTextColor(dc,RGB(255,255,255));SetBkColor(dc,RGB(0,0,0));SIZE extent;GetTextExtentPoint32W(dc,wide,n-1,&extent);p->label_width=extent.cx<LABEL_W?extent.cx:LABEL_W;RECT rect={0,0,LABEL_W,LABEL_H};DrawTextW(dc,wide,n-1,&rect,DT_SINGLELINE|DT_END_ELLIPSIS|DT_NOPREFIX);GdiFlush();for(int i=0;i<LABEL_W*LABEL_H;i++)p->label[i]=(bits[i]&0xffffff)!=0;SelectObject(dc,old_font);SelectObject(dc,old_bitmap);}
  if(font)DeleteObject(font);if(bitmap)DeleteObject(bitmap);DeleteDC(dc);
}
void OnlineInit(void){
  const char *port_s=getenv("Z3ONLINE_PORT"),*token_s=getenv("Z3ONLINE_TOKEN"),*slot_s=getenv("Z3ONLINE_SLOT");
  if(!port_s||!token_s||!slot_s)return;
  int port=atoi(port_s);slot=atoi(slot_s);token=strtoul(token_s,NULL,16);if(port<1||port>65535||!token||slot<1||slot>MAX_PLAYERS)return;
  WSADATA wsa;if(WSAStartup(MAKEWORD(2,2),&wsa))return;sock=socket(AF_INET,SOCK_DGRAM,IPPROTO_UDP);if(sock==INVALID_SOCKET){WSACleanup();return;}
  struct sockaddr_in local;memset(&local,0,sizeof(local));local.sin_family=AF_INET;local.sin_addr.s_addr=htonl(INADDR_LOOPBACK);
  if(bind(sock,(struct sockaddr*)&local,sizeof(local))){OnlineClose();return;}
  u_long nonblock=1;ioctlsocket(sock,FIONBIO,&nonblock);memset(&bridge,0,sizeof(bridge));bridge.sin_family=AF_INET;bridge.sin_addr.s_addr=htonl(INADDR_LOOPBACK);bridge.sin_port=htons(port);
  memset(peers,0,sizeof(peers));memset(outgoing,0,sizeof(outgoing));memset(pending,0,sizeof(pending));memcpy(outgoing,"Z3N2",4);Write32(outgoing+4,token);outgoing[13]=slot;
  const char *combat=getenv("Z3ONLINE_COMBAT");combat_enabled=combat&&atoi(combat)==1;memset(shared_world,0,sizeof(shared_world));
  const char *labels=getenv("Z3ONLINE_LABELS"),*name=getenv("Z3ONLINE_NAME");show_labels=!labels||atoi(labels)!=0;
  if(name&&strlen(name)<NAME_BYTES){char padded[NAME_BYTES];memset(padded,0,sizeof(padded));strcpy(padded,name);MakeLabel(&peers[slot-1],padded);}
  fprintf(stderr,"Online co-op bridge active (player %d); eight-player protocol.\n",slot);
}
static void ApplyShared(void){
  bool changed=false;uint8 old_sword=link_sword_type,old_shield=link_shield_type,old_armor=link_armor,old_gloves=link_item_gloves;
  for(int i=0;i<sizeof(fields)/sizeof(fields[0]);i++){
    int a=fields[i].offset;uint8 *p=g_ram+0xf340+a,v=pending[a];
    if(a==0){uint8 tier=(*p>=3)?3:(*p?1:0);if(v>tier)v+=!!link_num_arrows;else v=*p;}
    else if(a==44){uint8 have=link_health_capacity|(link_heart_pieces&3);v=have>v?have:v;if(v){link_heart_pieces=v&3;v&=~7;}}
    else v=fields[i].bits?*p|v:*p>v?*p:v;
    if(v!=*p){*p=v;changed=true;fprintf(stderr,"Online shared acquisition %04x.\n",0xf340+a);}
  }
  memset(pending,0,sizeof(pending));
  if(old_sword!=link_sword_type){DecompressSwordGraphics();Palette_Load_Sword();}
  if(old_shield!=link_shield_type){DecompressShieldGraphics();Palette_Load_Shield();}
  if(old_armor!=link_armor||old_gloves!=link_item_gloves)Palette_Load_LinkArmorAndGloves();
  if(!link_item_bottle_index)for(int i=0;i<4;i++)if(link_bottle_info[i]){link_item_bottle_index=i+1;changed=true;break;}
  if(changed)Hud_RefreshIcon();
}
static void ApplyWorld(void){
  for(int i=0;i<592;i++)g_ram[0xf000+i]|=shared_world[i]&WorldMask(i);
  for(int i=0;i<128;i++){
    uint8 old=save_ow_event_info[i],v=shared_world[592+i]&WorldMask(592+i);
    save_ow_event_info[i]|=v;
    if(i==0x5b && !(old&0x20)&&(v&0x20)&&!player_is_indoors&&overworld_screen_index==0x5b)CreatePyramidHole();
  }
  uint8 old=sram_progress_indicator,want=shared_world[720];
  // Let each client finish waking up; receiving the 'uncle left' bit before
  // the opening script runs strands Link in bed. The treasure-game toggle is
  // local too. Watergate reset bits and private chest contents stay local.
  if(old>=1&&want>old){sram_progress_indicator=want;Sprite_LoadGraphicsProperties();if(old<2&&want>=2&&!player_is_indoors){TS_copy=0;overlay_index=0;Overworld_SetSongList();sound_effect_ambient=5;}}
  uint8 flags=shared_world[721]&0xbf;if(!(sram_progress_flags&0x10))flags&=~0x10;
  sram_progress_flags|=flags;
  if((flags&1)&&follower_indicator==5)follower_indicator=0;
  uint8 f2=shared_world[724];sram_progress_indicator_3|=f2;
  if((f2&0x20)&&(follower_indicator==7||follower_indicator==8))follower_indicator=0;
  if((f2&0x10)&&follower_indicator==12)follower_indicator=0;
  if(shared_world[722]>savegame_map_icons_indicator)savegame_map_icons_indicator=shared_world[722];
}
static uint16 Area(void){return player_is_indoors?dungeon_room_index:overworld_screen_index;}
void OnlineBeforeFrame(void){
  before_stable=false;if(sock==INVALID_SOCKET||!combat_enabled||!Stable())return;
  uint16 area=Area();
  if(!own_visit||battle_area!=area||battle_indoors!=!!player_is_indoors||battle_world!=last_light_vs_dark_world||battle_floor!=!!link_is_on_lower_level){
    battle_area=area;battle_indoors=!!player_is_indoors;battle_world=last_light_vs_dark_world;battle_floor=!!link_is_on_lower_level;own_visit=(uint32)SDL_GetPerformanceCounter()|1;leader_slot=0;
  }
  uint8 leader=slot;for(int i=0;i<MAX_PLAYERS;i++)if(peers[i].active&&SameArea(peers[i].frame)&&i+1<leader)leader=i+1;
  uint32 visit=leader==slot?own_visit:Read32(peers[leader-1].frame+FIGHT_OFF+194);
  if(leader_slot!=leader||leader_visit!=visit){leader_slot=leader;leader_visit=visit;memset(hit_count,0,sizeof(hit_count));memset(consumed_hits,0,sizeof(consumed_hits));}
  if(leader!=slot){
    const uint8 *f=peers[leader-1].frame+FIGHT_OFF;
    if(f[192]&&f[193]==leader)for(int k=0;k<16;k++){
      const uint8 *s=f+k*8;
      // Match an existing known enemy. Never spawn an arbitrary remote sprite,
      // copy AI/graphics pointers, overwrite NPCs or touch boss-specific RAM.
      if(!CombatType(sprite_type[k])||s[0]!=sprite_type[k]||s[7]!=sprite_floor[k])continue;
      if(s[1]==0&&sprite_state[k]>=6){sprite_state[k]=0;continue;}
      if(s[1]<6||s[1]>9||sprite_state[k]<6||sprite_state[k]>9)continue;
      sprite_x_lo[k]=s[2];sprite_x_hi[k]=s[3];sprite_y_lo[k]=s[4];sprite_y_hi[k]=s[5];sprite_health[k]=s[6];
      if(s[1]==6&&sprite_state[k]==9){sprite_state[k]=6;sprite_delay_main[k]=15;}
    }
  }else for(int i=0;i<MAX_PLAYERS;i++){
    OnlinePeer *p=&peers[i];const uint8 *f=p->frame+FIGHT_OFF;
    if(!p->active||!SameArea(p->frame)||!f[192]||f[193]!=slot||Read32(f+194)!=own_visit)continue;
    for(int k=0;k<16;k++){
      uint32 reported=Read32(f+128+k*4),previous=consumed_hits[i][k];if((int32)(reported-previous)<=0)continue;
      consumed_hits[i][k]=reported;uint32 damage=reported-previous;
      if(damage>64||f[k*8]!=sprite_type[k]||!CombatType(sprite_type[k])||sprite_state[k]!=9||f[k*8+7]!=sprite_floor[k])continue;
      uint32 combined=sprite_give_damage[k]+damage;sprite_give_damage[k]=combined>64?64:combined;sprite_hit_timer[k]=0x9d;sprite_F[k]=15;
    }
  }
  memcpy(before_health,sprite_health,16);memcpy(before_state,sprite_state,16);memcpy(before_type,sprite_type,16);before_area=area;before_visit=leader_visit;before_stable=true;
}
void OnlineAfterFrame(void){
  if(!before_stable||!Stable()||leader_slot==slot||Area()!=before_area||before_visit!=leader_visit)return;
  for(int k=0;k<16;k++){
    if(!CombatType(before_type[k])||before_type[k]!=sprite_type[k]||before_state[k]!=9||!before_health[k])continue;
    int lost=sprite_state[k]==6?before_health[k]:(sprite_health[k]<before_health[k]?before_health[k]-sprite_health[k]:0);
    if(lost>0&&lost<=64)hit_count[k]+=lost;
  }
}
void OnlineTick(void){
  if(sock==INVALID_SOCKET)return;uint32 now=SDL_GetTicks();uint8 buf[PACKET+1];struct sockaddr_in from;int n,len;
  for(int count=0;count<64;count++){
    len=sizeof(from);n=recvfrom(sock,(char*)buf,sizeof(buf),0,(struct sockaddr*)&from,&len);if(n==SOCKET_ERROR)break;
    if(n!=PACKET||from.sin_addr.s_addr!=bridge.sin_addr.s_addr||from.sin_port!=bridge.sin_port||memcmp(buf,"Z3N2",4)||Read32(buf+4)!=token||buf[13]>MAX_PLAYERS||buf[13]==slot||buf[12]>1||buf[14]>1||buf[18]>1||buf[NAME_OFF+NAME_BYTES-1]||!ValidState(buf+STATE_OFF)||!ValidWorld(buf+WORLD_OFF)||!ValidFight(buf+FIGHT_OFF))continue;
    if(!buf[13]){MergeState(pending,buf+STATE_OFF);MergeWorld(shared_world,buf+WORLD_OFF);continue;}OnlinePeer *p=&peers[buf[13]-1];uint32 seq=Read32(buf+8),epoch=Read32(buf+26);
    if(p->active&&p->epoch==epoch&&(int32)(seq-p->sequence)<=0)continue;
    MergeState(pending,buf+STATE_OFF);MergeWorld(shared_world,buf+WORLD_OFF);
    p->sequence=seq;p->epoch=epoch;p->active=true;p->last_seen=now;memcpy(p->frame,buf,PACKET);MakeLabel(p,(char*)buf+NAME_OFF);
  }
  for(int i=0;i<MAX_PLAYERS;i++)if(peers[i].active&&now-peers[i].last_seen>2000)peers[i].active=false;
  if(Stable()){ApplyShared();ApplyWorld();}
  if(now-last_send>=50){last_send=now;Write32(outgoing+8,++sequence);sendto(sock,(const char*)outgoing,PACKET,0,(struct sockaddr*)&bridge,sizeof(bridge));}
}
void OnlineCapture(void){
  if(sock==INVALID_SOCKET)return;Ppu *ppu=g_zenv.ppu;
  outgoing[12]=Stable()&&!ppu->forcedBlank&&ppu->mode!=7;outgoing[14]=!!player_is_indoors;outgoing[15]=main_module_index;outgoing[16]=submodule_index;outgoing[17]=last_light_vs_dark_world;outgoing[18]=!!link_is_on_lower_level;
  Write16(outgoing+20,player_is_indoors?dungeon_room_index:overworld_screen_index);Write16(outgoing+22,link_x_coord);Write16(outgoing+24,link_y_coord);
  memset(outgoing+STATE_OFF,0,STATE_SIZE);if(Stable())for(int i=0;i<sizeof(fields)/sizeof(fields[0]);i++){int a=fields[i].offset;uint8 v=g_ram[0xf340+a];if(a==0)v=v>=3&&v<=4?3:v&&v<=4?1:0;if(a>=28&&a<=31)v=v?2:0;if(a==44)v=link_health_capacity|(link_heart_pieces&3);outgoing[STATE_OFF+a]=v<=fields[i].max?v:0;}
  memset(outgoing+WORLD_OFF,0,WORLD_SIZE);if(Stable()){
    for(int i=0;i<592;i++)outgoing[WORLD_OFF+i]=g_ram[0xf000+i]&WorldMask(i);
    for(int i=0;i<128;i++)outgoing[WORLD_OFF+592+i]=save_ow_event_info[i]&WorldMask(592+i);
    outgoing[WORLD_OFF+720]=sram_progress_indicator<=3?sram_progress_indicator:0;outgoing[WORLD_OFF+721]=sram_progress_flags&0xbf;outgoing[WORLD_OFF+722]=savegame_map_icons_indicator<=8?savegame_map_icons_indicator:0;outgoing[WORLD_OFF+724]=sram_progress_indicator_3;
  }
  memset(outgoing+FIGHT_OFF,0,FIGHT_SIZE);if(combat_enabled&&Stable()&&leader_slot){uint8 *f=outgoing+FIGHT_OFF;f[192]=1;f[193]=leader_slot;Write32(f+194,leader_visit);for(int k=0;k<16;k++){if(CombatType(sprite_type[k])&&sprite_state[k]<=11&&sprite_floor[k]<=1){uint8 *s=f+k*8;s[0]=sprite_type[k];s[1]=sprite_state[k];s[2]=sprite_x_lo[k];s[3]=sprite_x_hi[k];s[4]=sprite_y_lo[k];s[5]=sprite_y_hi[k];s[6]=sprite_health[k];s[7]=sprite_floor[k];}Write32(f+128+k*4,hit_count[k]);}}
  memset(outgoing+HEADER,0,SW*SH*2);if(!outgoing[12])return;
  int anchor_x=(int16)(link_x_coord-BG2HOFS_copy2),anchor_y=(int16)(link_y_coord-BG2VOFS_copy2);int start=sort_sprites_offset_into_oam_buffer/4;if(start<0||start+10>128)return;
  for(int i=start+9;i>=start;i--){OamEnt *o=&oam_buf[i];if(o->y>=0xe0)continue;int size=(bytewise_extended_oam[i]&2)?16:8;int ox=o->x|(bytewise_extended_oam[i]&1)<<8;if(ox>=256)ox-=512;int dx=ox-anchor_x+8,dy=o->y-anchor_y+16;if(dx<-size||dx>=SW||dy<-size||dy>=SH)continue;int adr=(o->flags&1)?ppu->objTileAdr2:ppu->objTileAdr1;
    for(int y=0;y<size;y++)for(int x=0;x<size;x++){int tx=dx+x,ty=dy+y;if(tx<0||tx>=SW||ty<0||ty>=SH)continue;int sx=(o->flags&0x40)?size-1-x:x,sy=(o->flags&0x80)?size-1-y:y;int tile=(((o->charnum>>4)+(sy>>3))<<4)|(((o->charnum&15)+(sx>>3))&15);int a=(adr+tile*16+(sy&7))&0x7fff;uint32 bits=(uint32)ppu->vram[a]|(uint32)ppu->vram[(a+8)&0x7fff]<<16;bits>>=7-(sx&7);int col=(bits&1)|((bits>>7)&2)|((bits>>14)&4)|((bits>>21)&8);if(col)Write16(outgoing+HEADER+(ty*SW+tx)*2,ppu->cgram[128+((o->flags>>1)&7)*16+col]|0x8000);}
  }
}
static bool SameArea(const uint8 *f){return outgoing[12]&&f[12]&&f[14]==outgoing[14]&&f[17]==outgoing[17]&&f[18]==outgoing[18]&&Read16(f+20)==Read16(outgoing+20);}
static void PutPixel(uint8 *pixels,int pitch,int w,int h,int x,int y,uint32 c){if(x>=0&&x<w&&y>=0&&y<h)*(uint32*)(pixels+y*pitch+x*4)=c;}
typedef struct {int x,y,w,h;} LabelRect;
static bool Intersects(LabelRect a,LabelRect b){return a.x<b.x+b.w+2&&b.x<a.x+a.w+2&&a.y<b.y+b.h+2&&b.y<a.y+a.h+2;}
static int Clamp(int v,int lo,int hi){return v<lo?lo:v>hi?hi:v;}
// A bounded candidate search prevents piled-up players from piling up names.
// Keep the game's HUD clear and prefer space above or beside the actual avatar.
static LabelRect PlaceLabel(int cx,int cy,int w,int width,int height,LabelRect *placed,int count,LabelRect *bodies,int body_count){
  LabelRect best={2,56,w,LABEL_H};int best_score=0x7fffffff;
  int step=w/2+8;
  for(int row=0;row<20;row++)for(int col=0;col<17;col++){
    int oy=row?((row&1)?-(row+1)/2:row/2)*(LABEL_H+4):0;
    int ox=col?((col&1)?-(col+1)/2:col/2)*step:0;
    LabelRect r={Clamp(cx-w/2+ox,2,width-w-2),Clamp(cy+oy,56,height-LABEL_H-2),w,LABEL_H};bool blocked=false;
    for(int i=0;i<count;i++)if(Intersects(r,placed[i])){blocked=true;break;}if(blocked)continue;
    int score=abs(r.x+w/2-cx)+2*abs(r.y-cy);
    for(int i=0;i<body_count;i++)if(Intersects(r,bodies[i]))score+=1000;
    if(score<best_score){best=r;best_score=score;}
  }
  return best;
}
static void DrawLabel(OnlinePeer *p,uint8 *pixels,int pitch,int width,int height,int x,int y,uint32 color){
  for(int a=0;a<LABEL_H;a++)for(int b=0;b<p->label_width;b++)if(p->label[a*LABEL_W+b])for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)PutPixel(pixels,pitch,width,height,x+b+dx,y+a+dy,0xff101b16);
  for(int a=0;a<LABEL_H;a++)for(int b=0;b<p->label_width;b++)if(p->label[a*LABEL_W+b])PutPixel(pixels,pitch,width,height,x+b,y+a,color);
}
void OnlineDraw(uint8 *pixels,int pitch,int width,int height,int scale){
  if(!pixels||scale!=1||!outgoing[12])return;
  // Stable slot order keeps overlapping avatars deterministic. Labels are drawn
  // last so no player's sprite can obscure someone else's identity.
  for(int i=MAX_PLAYERS-1;i>=0;i--){OnlinePeer *p=&peers[i];uint8 *f=p->frame;if(!p->active||!SameArea(f))continue;int dx=(int16)(Read16(f+22)-BG2HOFS_copy2)+g_zenv.ppu->extraLeftRight-8,dy=(int16)(Read16(f+24)-BG2VOFS_copy2)-16;
    for(int y=0;y<SH;y++)for(int x=0;x<SW;x++){uint16 c=Read16(f+HEADER+(y*SW+x)*2);if(!(c&0x8000))continue;int bright=g_zenv.ppu->brightness;uint32 r=(c&31)*255*bright/(31*15),g=((c>>5)&31)*255*bright/(31*15),b=((c>>10)&31)*255*bright/(31*15);PutPixel(pixels,pitch,width,height,dx+x,dy+y,0xff000000|r<<16|g<<8|b);}
  }
  if(show_labels){
    int xs[MAX_PLAYERS],ys[MAX_PLAYERS],ids[MAX_PLAYERS],count=0;LabelRect bodies[MAX_PLAYERS],placed[MAX_PLAYERS];
    static const uint32 colors[MAX_PLAYERS]={0xffadf5bc,0xffa9d2ff,0xffffb8ac,0xffdbb7ff,0xffffdf8a,0xff9df1ec,0xffffb7db,0xffedf1f6};
    for(int i=0;i<MAX_PLAYERS;i++){
      OnlinePeer *p=&peers[i];bool own=i+1==slot;if(!p->label_width||(!own&&(!p->active||!SameArea(p->frame))))continue;
      int x=(int16)((own?link_x_coord:Read16(p->frame+22))-BG2HOFS_copy2)+g_zenv.ppu->extraLeftRight+8;
      int y=(int16)((own?link_y_coord:Read16(p->frame+24))-BG2VOFS_copy2);
      if(x<0||x>=width||y<40||y>=height)continue;
      xs[count]=x;ys[count]=y;ids[count]=i;bodies[count]=(LabelRect){x-9,y-5,18,24};count++;
    }
    for(int i=0;i<count;i++){
      OnlinePeer *p=&peers[ids[i]];LabelRect r=PlaceLabel(xs[i],ys[i]-16,p->label_width,width,height,placed,i,bodies,count);placed[i]=r;
      int tx=Clamp(xs[i],r.x,r.x+r.w-1),ty=Clamp(ys[i]-7,r.y,r.y+r.h-1);
      if(abs(tx-xs[i])+abs(ty-(ys[i]-7))>12){int dx=xs[i]-tx,dy=ys[i]-7-ty,steps=abs(dx)>abs(dy)?abs(dx):abs(dy);for(int k=0;k<steps;k++)PutPixel(pixels,pitch,width,height,tx+dx*k/steps,ty+dy*k/steps,0xff43634e);}
      DrawLabel(p,pixels,pitch,width,height,r.x,r.y,colors[ids[i]]);
    }
  }
}
void OnlineClose(void){OnlineRestoreColors();if(sock!=INVALID_SOCKET){closesocket(sock);sock=INVALID_SOCKET;WSACleanup();}}
