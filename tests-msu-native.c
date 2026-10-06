/* Exercise the shipped native MSU PCM player directly. No game assets required.
   Opus decode stubs are unreachable; cleanup accepts the player's null decoder. */
#define SDL_MAIN_HANDLED
#include "engine/src/audio.c"
#include <stdarg.h>

uint8 g_ram[131072];
ZeldaEnv g_zenv;
const uint8 *g_asset_ptrs[kNumberOfAssets];
#define apu_command (g_apu_write.ports[0])
void ZeldaApuLock(void) {}
void ZeldaApuUnlock(void) {}
OpusDecoder *opus_decoder_create(opus_int32 rate,int channels,int *error) { abort(); }
void opus_decoder_destroy(OpusDecoder *decoder) { if(decoder)abort(); }
int opus_decoder_ctl(OpusDecoder *decoder,int request,...) { abort(); }
int opus_decode(OpusDecoder *decoder,const unsigned char *data,opus_int32 len,opus_int16 *pcm,int frames,int fec) { abort(); }
/* The PCM mixer does not call the unrelated SPC/DSP/render routines. */
void SpcPlayer_GenerateSamples(SpcPlayer *p) { abort(); }
void SpcPlayer_CopyVariablesFromRam(SpcPlayer *p) { abort(); }
void SpcPlayer_Initialize(SpcPlayer *p) { abort(); }
void SpcPlayer_CopyVariablesToRam(SpcPlayer *p) { abort(); }
void SpcPlayer_Upload(SpcPlayer *p,const uint8_t *data) { abort(); }
void dsp_getSamples(struct Dsp *dsp,int16_t *data,int samples,int channels) { abort(); }
void NORETURN Die(const char *text) { fprintf(stderr,"%s\n",text);exit(3); }
static int passed;
#define CHECK(test,text) do {if(!(test)){fprintf(stderr,"FAILED %s\n",text);return 1;}printf("PASS %s\n",text);passed++;}while(0)

int main(int argc,char **argv) {
  setvbuf(stdout,NULL,_IONBF,0);setvbuf(stderr,NULL,_IONBF,0);
  if(argc!=2)return 2;
  ParseConfigFile(argv[1]);
  CHECK(g_config.enable_msu==kMsuEnabled_Msu,"actual native parser enables standard MSU PCM");
  CHECK(g_config.audio_freq==44100&&g_config.audio_channels==2,"native audio configuration is 44100 Hz stereo");
  CHECK(g_config.msuvolume==70&&g_config.resume_msu,"native parser reads soundtrack volume and resume");
  ZeldaEnableMsu(g_config.enable_msu);
  ZeldaPlayMsuAudioTrack(2);
  CHECK(g_msu_player.f&&g_msu_player.state==kMsuState_Playing,"actual player opens the imported overworld PCM file");
  CHECK(apu_command==0xf0,"native player suppresses SPC music while PCM plays");
  int16 buffer[960*2];memset(buffer,0,sizeof(buffer));
  g_msu_player.volume=0.70f;g_msu_player.volume_target=0.70f;
  uint32 before=g_msu_player.cur_file_offs;MsuPlayer_Mix(&g_msu_player,buffer,960);
  CHECK(g_msu_player.cur_file_offs>before,"native MSU mixer consumes actual PCM frames");
  long long energy=0;for(int i=0;i<960*2;i++)energy+=(long long)buffer[i]*buffer[i];
  CHECK(energy>0,"actual native mixer produces non-silent stereo soundtrack audio");
  CHECK(g_msu_player.repeat_position<g_msu_player.total_samples_in_file,"installed overworld track loop point is valid");
  g_msu_player.samples_until_repeat=0;g_msu_player.buffer_size=g_msu_player.buffer_pos=0;
  MsuPlayer_Mix(&g_msu_player,buffer,960);
  CHECK(g_msu_player.f&&g_msu_player.state==kMsuState_Playing,"actual player loops the overworld music without ending playback");
  ZeldaPlayMsuAudioTrack(255);
  /* 255 is an MSU control byte, so use an absent non-control track instead. */
  ZeldaPlayMsuAudioTrack(47);
  CHECK(!g_msu_player.f&&apu_command==47,"missing PCM track falls back to the original SPC command");
  ZeldaEnableMsu(0);ZeldaPlayMsuAudioTrack(2);
  CHECK(apu_command==2&&!g_msu_player.enabled,"disabling MSU restores ordinary SPC music commands");
  printf("%d native MSU checks passed.\n",passed);
  return 0;
}
