using System.Runtime.InteropServices;

namespace ZeldaCompanion;

public sealed record PadDevice(int Id,string Name);
public sealed record PadState(string Name,string[] Buttons,short X,short Y);

public sealed class ControllerInput : IDisposable
{
    static bool loaded;
    const uint Subsystems=0x2200;
    readonly Dictionary<int,nint> devices=[];
    bool initialized;
    long refreshed;
    public string? Error {get;private set;}
    static void Load(){if(loaded)return;string path=Path.Combine(Install.Game,"SDL2.dll");nint lib=NativeLibrary.Load(path);NativeLibrary.SetDllImportResolver(typeof(ControllerInput).Assembly,(name,assembly,search)=>name=="SDL2"?lib:0);loaded=true;}
    public static int KeyCode(string name){Load();return SDL_GetKeyFromName(name);}
    public ControllerInput(){try{Load();SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");if(SDL_InitSubSystem(Subsystems)!=0)throw new IOException(Marshal.PtrToStringUTF8(SDL_GetError()));initialized=true;}catch(Exception e){Error=e.Message;}}
    public IReadOnlyList<PadDevice> Devices(){Refresh();return devices.Select(d=>new PadDevice(d.Key,Marshal.PtrToStringUTF8(SDL_GameControllerName(d.Value))??"Controller")).ToArray();}
    void Refresh(){if(!initialized)return;SDL_PumpEvents();while(SDL_PollEvent(out _)!=0){}SDL_GameControllerUpdate();
        foreach(var d in devices.Where(d=>SDL_GameControllerGetAttached(d.Value)==0).ToArray()){SDL_GameControllerClose(d.Value);devices.Remove(d.Key);}
        if(Environment.TickCount64-refreshed<400)return;refreshed=Environment.TickCount64;
        for(int i=0;i<SDL_NumJoysticks();i++){if(SDL_IsGameController(i)==0)continue;int id=SDL_JoystickGetDeviceInstanceID(i);if(id<0||devices.ContainsKey(id))continue;nint handle=SDL_GameControllerOpen(i);if(handle!=0)devices[id]=handle;}
    }
    public PadState? Read(int? id=null){Refresh();nint h=id.HasValue?devices.GetValueOrDefault(id.Value):devices.Values.FirstOrDefault();if(h==0)return null;var keys=new List<string>();for(int i=0;i<15;i++)if(SDL_GameControllerGetButton(h,i)!=0)keys.Add(ControlBindings.PadButtons[i]);for(int i=0;i<2;i++)if(SDL_GameControllerGetAxis(h,4+i)>=12000)keys.Add(i==0?"L2":"R2");return new(Marshal.PtrToStringUTF8(SDL_GameControllerName(h))??"Controller",keys.ToArray(),SDL_GameControllerGetAxis(h,0),SDL_GameControllerGetAxis(h,1));}
    public void Dispose(){foreach(var h in devices.Values)SDL_GameControllerClose(h);devices.Clear();if(initialized)SDL_QuitSubSystem(Subsystems);initialized=false;}
    [StructLayout(LayoutKind.Explicit,Size=56)]struct Event{[FieldOffset(0)]public uint Type;}
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_InitSubSystem(uint flags);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_QuitSubSystem(uint flags);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)]string name,[MarshalAs(UnmanagedType.LPUTF8Str)]string value);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern nint SDL_GetError();
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_PumpEvents();
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_PollEvent(out Event e);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_GameControllerUpdate();
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_NumJoysticks();
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_IsGameController(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_JoystickGetDeviceInstanceID(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern nint SDL_GameControllerOpen(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_GameControllerClose(nint handle);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_GameControllerGetAttached(nint handle);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern nint SDL_GameControllerName(nint handle);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern byte SDL_GameControllerGetButton(nint handle,int button);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern short SDL_GameControllerGetAxis(nint handle,int axis);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_GetKeyFromName([MarshalAs(UnmanagedType.LPUTF8Str)]string name);
}
