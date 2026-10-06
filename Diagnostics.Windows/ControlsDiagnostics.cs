using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ZeldaCompanion;

static class ControlsDiagnostics
{
    static readonly List<string> checks=[];
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object form,string name)=>(T)form.GetType().GetField(name,Flags)!.GetValue(form)!;
    static void Check(bool value,string text){if(!value)throw new Exception("FAILED "+text);checks.Add(text);Console.WriteLine("PASS "+text);}
    static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(15);}}
    public static int Run(string output,string helper){
        int virtualIndex=-1;nint joystick=0;
        try{
            Directory.CreateDirectory(output);string profile=Path.Combine(Install.Root,"controls-generated.ini");string original=File.ReadAllText(Path.Combine(Install.Game,"zelda3.ini"));File.WriteAllText(profile,original);var b=new ControlBindings(profile);
            var sword=ControlBindings.Actions.Single(a=>a.Label=="Sword");var dash=ControlBindings.Actions.Single(a=>a.Label=="Action / dash");var full=ControlBindings.Actions.Single(a=>a.Key=="Fullscreen");var pause=ControlBindings.Actions.Single(a=>a.Key=="PauseDimmed");var save=ControlBindings.Actions.First(a=>a.Key=="Save");
            Check(b.Get(sword,false)=="z"&&b.Get(sword,true)=="A","reads original keyboard and controller bindings");
            Check(b.Conflict(sword,false,"x")==dash,"detects gameplay key conflict");bool refused=false;try{b.Assign(sword,false,"x");}catch(InvalidOperationException){refused=true;}Check(refused,"does not silently overwrite a duplicate key");b.Assign(sword,false,"x",true);Check(b.Get(sword,false)=="x"&&b.Get(dash,false)=="z","swaps two existing keys safely");b.Defaults();
            b.Assign(sword,false,"g");b.Assign(sword,true,"R3");b.Assign(full,false,"Ctrl+F12");b.Assign(pause,true,"Lb+Start");b.Assign(save,true,"L2+Back");
            Check(ControlBindings.Normalize("L1+Start",true)==ControlBindings.Normalize("Lb+Start",true),"normalizes supported shoulder-button aliases");
            ControlBindings.ValidateBinding("Keypad +",false);Check(CaptureInputForm.KeyboardName(Keys.Control|Keys.Shift|Keys.F12)=="Ctrl+Shift+F12","keyboard capture formats modifier combinations");
            refused=false;try{b.Assign(sword,false,"x, #bad");}catch(ArgumentException){refused=true;}Check(refused,"rejects config delimiters in bindings");
            string backups=Path.Combine(Install.Root,"controls-backups");b.Save(backups);Check(Directory.GetFiles(backups).Any(f=>File.ReadAllBytes(f).SequenceEqual(File.ReadAllBytes(Path.Combine(Install.Game,"zelda3.ini")))),"save creates a byte-equivalent settings backup");
            var roundTrip=new ControlBindings(profile);Check(roundTrip.Get(sword,false)=="g"&&roundTrip.Get(sword,true)=="R3"&&roundTrip.Get(pause,true)=="Lb+Start"&&roundTrip.Get(save,true)=="L2+Back","keyboard, controller and state bindings survive save and reload");
            Check(File.ReadAllText(profile).Contains("EnableAudio = 1")&&File.ReadAllText(profile).Contains("xbrz-freescale.glslp"),"controls save retains sound and graphics settings");
            var concurrent=new ControlBindings(profile);File.AppendAllText(profile,"\n# external change\n");refused=false;try{concurrent.Save(backups);}catch(IOException){refused=true;}Check(refused,"refuses to overwrite settings changed outside the editor");File.WriteAllText(profile,original);var generated=new ControlBindings(profile);generated.Assign(sword,false,"g");generated.Assign(sword,true,"R3");generated.Assign(full,false,"Ctrl+F12");generated.Assign(pause,true,"Lb+Start");generated.Assign(save,true,"L2+Back");generated.Save(backups);
            string gameIni=Path.Combine(Install.Game,"zelda3.ini");byte[] preserved=File.ReadAllBytes(gameIni);string before=Install.GameplayHash();try{File.Copy(profile,gameIni,true);Check(Install.GameplayHash()==before,"personal controls do not change online gameplay compatibility");}finally{File.WriteAllBytes(gameIni,preserved);}
            NativeLibrary.SetDllImportResolver(typeof(ControlsDiagnostics).Assembly,(name,assembly,search)=>name=="SDL2"?NativeLibrary.Load(Path.Combine(Install.Game,"SDL2.dll")):0);
            using var input=new ControllerInput();virtualIndex=SDL_JoystickAttachVirtual(1,6,15,0);Check(virtualIndex>=0,"attaches process-local virtual controller using bundled SDL");
            var guid=SDL_JoystickGetDeviceGUID(virtualIndex);var text=new StringBuilder(33);SDL_JoystickGetGUIDString(guid,text,33);Check(SDL_GameControllerAddMapping(text+",Companion Virtual Test,a:b0,b:b1,x:b2,y:b3,back:b4,guide:b5,start:b6,leftstick:b7,rightstick:b8,leftshoulder:b9,rightshoulder:b10,dpup:b11,dpdown:b12,dpleft:b13,dpright:b14,leftx:a0,lefty:a1,rightx:a2,righty:a3,lefttrigger:a4,righttrigger:a5,platform:Windows,")>=0,"virtual controller is recognized through the real controller mapping API");
            joystick=SDL_JoystickOpen(virtualIndex);SDL_JoystickSetVirtualAxis(joystick,4,short.MinValue);SDL_JoystickSetVirtualAxis(joystick,5,short.MinValue);Pump(550);int id=input.Devices().Single(d=>d.Name=="Companion Virtual Test").Id;
            Check(input.Read(id) is {Buttons.Length:0},"released controller reports no phantom buttons or triggers");
            using(var keyboard=new CaptureInputForm("Sword",false,input,id)){keyboard.Show();Pump(200);Capture(keyboard,"keyboard-capture");PostMessage(keyboard.Handle,0x100,(nint)(int)Keys.G,1);Pump(150);Check(keyboard.Value=="g","actual keyboard message is captured by the assignment dialog");}
            using(var pad=new CaptureInputForm("Pause",true,input,id)){pad.Show();Pump(200);SDL_JoystickSetVirtualButton(joystick,9,1);Pump(160);SDL_JoystickSetVirtualButton(joystick,6,1);Pump(160);Capture(pad,"controller-capture");SDL_JoystickSetVirtualButton(joystick,6,0);SDL_JoystickSetVirtualButton(joystick,9,0);Pump(160);Check(pad.Value=="Lb+Start","actual controller dialog captures held-button combinations in order");}
            SDL_JoystickSetVirtualAxis(joystick,4,short.MaxValue);Pump(100);Check(input.Read(id)!.Buttons.Contains("L2"),"controller trigger is captured as L2");SDL_JoystickSetVirtualAxis(joystick,4,short.MinValue);
            string uiFile=Path.Combine(Install.Root,"controls-ui.ini");File.WriteAllText(uiFile,original);using(var editor=new ControlsForm(uiFile)){editor.Show();Pump(500);Capture(editor,"controls-editor");var grid=Field<DataGridView>(editor,"grid");var categories=Field<ComboBox>(editor,"group");
                using var keyScript=new System.Windows.Forms.Timer{Interval=60};keyScript.Tick+=(_,_)=>{var c=Application.OpenForms.OfType<CaptureInputForm>().FirstOrDefault();if(c!=null)PostMessage(c.Handle,0x100,(nint)(int)Keys.G,1);};grid.CurrentCell=grid.Rows[7].Cells[1];keyScript.Start();typeof(DataGridView).GetMethod("OnCellContentClick",Flags)!.Invoke(grid,[new DataGridViewCellEventArgs(1,7)]);keyScript.Stop();Check(Field<ControlBindings>(editor,"bindings").Get(sword,false)=="g","actual editor cell opens capture and applies the received key");Capture(editor,"controls-remapped");
                categories.SelectedItem="Game shortcuts";Pump(100);Capture(editor,"controls-shortcuts");categories.SelectedItem="Save states";Pump(100);Capture(editor,"controls-save-states");Check(grid.Rows.Count==20,"save-state section exposes all twenty engine slots");Check(grid.CurrentCell!.RowIndex==0&&grid.FirstDisplayedScrollingRowIndex==0&&!Field<Label>(editor,"feedback").Text.Contains("Sword"),"category starts at first action and replaces stale edit feedback");
                Field<Button>(editor,"save").PerformClick();Pump(100);Check(new ControlBindings(uiFile).Get(sword,false)=="g"&&editor.DialogResult==DialogResult.OK,"actual Save controls button writes mappings and closes successfully");}
            SDL_JoystickClose(joystick);joystick=0;Check(SDL_JoystickDetachVirtual(virtualIndex)==0,"removes only the owned virtual controller");virtualIndex=-1;Pump(550);Check(!input.Devices().Any(d=>d.Id==id),"controller disconnection clears the selected device");
            using(var missing=new CaptureInputForm("Equipped tool",true,input,id)){missing.Show();Pump(150);Capture(missing,"controller-disconnected");Check(Field<Label>(missing,"progress").Text.Contains("No controller detected"),"disconnected controller displays a manual-button fallback");missing.Close();}
            File.WriteAllText(Path.Combine(output,"controls-results.json"),JsonSerializer.Serialize(new{checks,generatedIni=profile,physicalControllerTested=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(checks.Count+" controls checks passed.");return 0;
            void Capture(Form form,string name){var si=new ProcessStartInfo("python"){UseShellExecute=false,CreateNoWindow=true};si.ArgumentList.Add(helper);si.ArgumentList.Add(form.Handle.ToInt64().ToString());si.ArgumentList.Add(Path.Combine(output,name+".png"));using var p=Process.Start(si)!;p.WaitForExit();Check(p.ExitCode==0,"captured actual window: "+name);}
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(joystick!=0)SDL_JoystickClose(joystick);if(virtualIndex>=0)SDL_JoystickDetachVirtual(virtualIndex);foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())form.Dispose();}
    }
    [StructLayout(LayoutKind.Sequential)]struct Guid16{public uint A,B,C,D;}
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_JoystickAttachVirtual(int type,int axes,int buttons,int hats);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_JoystickDetachVirtual(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern Guid16 SDL_JoystickGetDeviceGUID(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]static extern void SDL_JoystickGetGUIDString(Guid16 guid,StringBuilder text,int length);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)]static extern int SDL_GameControllerAddMapping(string mapping);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern nint SDL_JoystickOpen(int index);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern void SDL_JoystickClose(nint joystick);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_JoystickSetVirtualButton(nint joystick,int button,byte value);
    [DllImport("SDL2",CallingConvention=CallingConvention.Cdecl)]static extern int SDL_JoystickSetVirtualAxis(nint joystick,int axis,short value);
    [DllImport("user32.dll")]static extern bool PostMessage(nint hwnd,uint message,nint wparam,nint lparam);
}
