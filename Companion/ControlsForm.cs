using System.Runtime.InteropServices;

namespace ZeldaCompanion;

public sealed class ControlsForm : Form
{
    readonly ControlBindings bindings;
    readonly ControllerInput input=new();
    readonly ComboBox group=Theme.Combo(225),device=Theme.Combo(370);
    readonly Label deviceStatus=Theme.Text("",10,Theme.Muted),feedback=Theme.Text("Changes apply on your next game launch.",11,Theme.Muted);
    readonly DataGridView grid=new();
    readonly ActionButton clear=Theme.Button("Clear keyboard",false,155),save=Theme.Button("Save controls",true,175);
    readonly System.Windows.Forms.Timer timer=new(){Interval=60};
    ControlAction[] visible=[];
    int? selectedDevice;
    bool dirty,saved,refreshing;
    string[] deviceNames=[];
    public ControlsForm(string? file=null){
        bindings=new ControlBindings(file??Path.Combine(Install.Game,"zelda3.ini"));
        Text="Controls · Zelda Companion";ClientSize=new Size(900,740);MinimumSize=new Size(840,660);StartPosition=FormStartPosition.CenterParent;AutoScaleMode=AutoScaleMode.Dpi;Font=Theme.Font();BackColor=Theme.Canvas;ForeColor=Theme.Ink;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26),ColumnCount=1,RowCount=3};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,190));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,128));Controls.Add(root);
        var header=new PageColumn{Padding=Padding.Empty};header.Add(Theme.Text("Set your controls",26,null,FontStyle.Bold),6);header.Add(Theme.Text("Click a binding, then press your key or controller button.\nKeyboard and controller settings are personal, including in online rooms.",11,Theme.Muted),16);
        group.Items.AddRange(ControlBindings.Actions.Select(a=>a.Group).Distinct().Cast<object>().ToArray());group.AccessibleName="Controls category";device.AccessibleName="Controller to use";
        header.Add(Theme.Row(group,device),6);header.Add(deviceStatus,14);root.Controls.Add(header,0,0);
        grid.Dock=DockStyle.Fill;grid.BackgroundColor=Theme.Rail;grid.BorderStyle=BorderStyle.None;grid.GridColor=Theme.Line;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Surface,ForeColor=Theme.Muted,Font=Theme.Font(10,FontStyle.Bold),Padding=new Padding(8)};
        grid.ColumnHeadersHeight=38;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.RowHeadersVisible=false;grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=grid.AllowUserToResizeRows=false;grid.ReadOnly=true;grid.MultiSelect=false;grid.SelectionMode=DataGridViewSelectionMode.CellSelect;
        grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Rail,ForeColor=Theme.Ink,SelectionBackColor=Theme.Surface,SelectionForeColor=Theme.Gold,Font=Theme.Font(),Padding=new Padding(8)};grid.RowTemplate.Height=46;
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="Action",HeaderText="Action",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=220,SortMode=DataGridViewColumnSortMode.NotSortable});
        foreach(var name in new[]{"Keyboard","Controller"})grid.Columns.Add(new DataGridViewButtonColumn{Name=name,HeaderText=name,Width=name=="Keyboard"?215:225,FlatStyle=FlatStyle.Flat,SortMode=DataGridViewColumnSortMode.NotSortable,DefaultCellStyle=new DataGridViewCellStyle{BackColor=Theme.Surface,ForeColor=Theme.Gold,SelectionBackColor=Theme.Surface,SelectionForeColor=Theme.Ink,Font=Theme.Font(11,FontStyle.Bold),Padding=new Padding(4)}});
        grid.AccessibleName="Actions and current keyboard and controller bindings";root.Controls.Add(grid,0,1);
        var footer=new PageColumn{Padding=new Padding(0,14,0,0)};footer.Add(feedback,12);ActionButton defaults=Theme.Button("Restore defaults",false,165),cancel=Theme.Button("Discard changes",false,165);footer.Add(Theme.Row(defaults,clear,cancel,save),0);root.Controls.Add(footer,0,2);
        defaults.Click+=(_,_)=>{bindings.Defaults();dirty=true;Render();Message("Default bindings restored. Save controls to apply them.");};clear.Click+=(_,_)=>{if(grid.CurrentCell==null)return;bindings.Assign(visible[grid.CurrentCell.RowIndex],grid.CurrentCell.ColumnIndex==2,"");dirty=true;Render();Message("Binding cleared. Save controls to apply your changes.");};cancel.Click+=(_,_)=>{dirty=false;Close();};
        save.Click+=(_,_)=>{try{bindings.Save(Path.Combine(Install.Data,"SettingsBackups"));saved=true;dirty=false;DialogResult=DialogResult.OK;Close();}catch(Exception e){Message(e.Message,true);}};
        grid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex is 1 or 2)EditBinding(visible[e.RowIndex],e.ColumnIndex==2);};grid.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter&&grid.CurrentCell is {} c){e.Handled=e.SuppressKeyPress=true;EditBinding(visible[c.RowIndex],c.ColumnIndex==2);}};
        grid.SelectionChanged+=(_,_)=>clear.Text=grid.CurrentCell?.ColumnIndex==2?"Clear controller":"Clear keyboard";
        group.SelectedIndexChanged+=(_,_)=>{Render(reset:true);Message(dirty?"Unsaved control changes. Save controls to apply them.":"Changes apply on your next game launch.");};device.SelectedIndexChanged+=(_,_)=>{if(refreshing)return;var devices=input.Devices();selectedDevice=device.SelectedIndex>=0&&device.SelectedIndex<devices.Count?devices[device.SelectedIndex].Id:null;};
        timer.Tick+=(_,_)=>RefreshDevice();timer.Start();FormClosed+=(_,_)=>{timer.Dispose();input.Dispose();};FormClosing+=(_,e)=>{if(dirty&&!saved&&MessageBox.Show(this,"Discard your unsaved control changes?","Unsaved controls",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
        group.SelectedIndex=0;RefreshDevice();
        Shown+=(_,_)=>{var area=Screen.FromControl(this).WorkingArea;if(Height>area.Height-30){MinimumSize=new Size(MinimumSize.Width,Math.Min(MinimumSize.Height,area.Height-40));Height=area.Height-30;}};
    }
    void Message(string text,bool error=false){feedback.Text=text;feedback.ForeColor=error?Theme.Danger:Theme.Muted;}
    void Render(bool reset=false){int row=reset?0:Math.Max(0,grid.CurrentCell?.RowIndex??0),col=reset?1:Math.Clamp(grid.CurrentCell?.ColumnIndex??1,1,2);visible=ControlBindings.Actions.Where(a=>a.Group==group.SelectedItem?.ToString()).ToArray();grid.Rows.Clear();foreach(var a in visible)grid.Rows.Add(a.Label,Display(bindings.Get(a,false)),Display(bindings.Get(a,true)));if(grid.Rows.Count>0){grid.CurrentCell=grid.Rows[Math.Min(row,grid.Rows.Count-1)].Cells[col];if(reset)grid.FirstDisplayedScrollingRowIndex=0;}}
    static string Display(string v)=>v.Length==0?"Unassigned":v;
    void RefreshDevice(){var devices=input.Devices();var names=devices.Select(d=>d.Id+":"+d.Name).ToArray();if(!names.SequenceEqual(deviceNames)){deviceNames=names;refreshing=true;device.Items.Clear();device.Items.AddRange(devices.Count==0?["No controller detected"]:devices.Select(d=>d.Name).Cast<object>().ToArray());int index=devices.ToList().FindIndex(d=>d.Id==selectedDevice);device.SelectedIndex=index<0?0:index;selectedDevice=devices.Count>0?devices[device.SelectedIndex].Id:null;refreshing=false;}var state=input.Read(selectedDevice);deviceStatus.Text=state==null?"Connect a compatible USB or Bluetooth controller, or choose its buttons manually.":$"Input: {(state.Buttons.Length>0?string.Join(" + ",state.Buttons):Math.Abs((int)state.X)>12000||Math.Abs((int)state.Y)>12000?"Left stick":"No buttons pressed")} · Left stick also moves Link. Button labels use Xbox names.";}
    void EditBinding(ControlAction action,bool pad){try{using var capture=new CaptureInputForm(action.Label,pad,input,selectedDevice);if(capture.ShowDialog(this)!=DialogResult.OK)return;string value=capture.Value;var conflict=bindings.Conflict(action,pad,value);if(conflict!=null&&MessageBox.Show(this,$"{value} is already used by {conflict.Label}.\n\nSwap the bindings for {action.Label} and {conflict.Label}?","Input already assigned",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;bindings.Assign(action,pad,value,swap:conflict!=null);dirty=true;Render();Message($"{action.Label}: {value}. Save controls to apply your changes.");}catch(Exception e){Message(e.Message,true);}}
}

public sealed class CaptureInputForm : Form
{
    readonly bool pad;
    readonly ControllerInput input;
    readonly int? device;
    readonly Label progress=Theme.Text("",12,Theme.Gold);
    readonly System.Windows.Forms.Timer timer=new(){Interval=40};
    readonly List<string> pressed=[];
    string modifier="";
    bool armed,usedModifier;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value {get;private set;}="";
    public CaptureInputForm(string action,bool pad,ControllerInput input,int? device){
        this.pad=pad;this.input=input;this.device=device;Text="Assign input · Zelda Companion";ClientSize=new Size(660,370);MinimumSize=new Size(600,370);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Canvas;ForeColor=Theme.Ink;Font=Theme.Font();KeyPreview=true;
        var body=new PageColumn{Dock=DockStyle.Fill,Padding=new Padding(26)};Controls.Add(body);body.Add(Theme.Text(action,22,null,FontStyle.Bold),12);body.Add(Theme.Text(pad?"Press a controller button, then release it.\nFor a combination, hold the first button, press the second, then release both.":"Press the keyboard key you want to use.\nYou can hold Ctrl, Alt or Shift while pressing another key.",11,Theme.Muted),16);body.Add(progress,18);
        if(pad){var choice=Theme.Combo(220);choice.Items.Add("Choose a button…");choice.Items.AddRange(ControlBindings.PadButtons);choice.SelectedIndex=0;var apply=Theme.Button("Use selected button",false,210);apply.Enabled=false;choice.SelectedIndexChanged+=(_,_)=>apply.Enabled=choice.SelectedIndex>0;apply.Click+=(_,_)=>Complete(choice.SelectedItem!.ToString()!);body.Add(Theme.Row(choice,apply),16);body.Add(Theme.Text("No controller yet? You can choose its buttons above.\nLive combinations are captured from the controller selected in Controls.",10,Theme.Muted),16);}
        var cancel=Theme.Button("Cancel",false,120);cancel.Click+=(_,_)=>{DialogResult=DialogResult.Cancel;Close();};body.Add(cancel,0);
        timer.Tick+=(_,_)=>ReadPad();if(pad)timer.Start();else progress.Text="Waiting for your keyboard input…";FormClosed+=(_,_)=>timer.Dispose();
    }
    void ReadPad(){var s=input.Read(device);if(s==null){armed=false;pressed.Clear();progress.Text="No controller detected. Connect it, or choose a button below.";return;}if(!ContainsFocus){armed=false;pressed.Clear();progress.Text="Click this window to assign controller input.";return;}if(!armed){progress.Text="Release any held buttons to begin.";armed=s.Buttons.Length==0;return;}foreach(string b in s.Buttons)if(!pressed.Contains(b))pressed.Add(b);progress.Text=pressed.Count==0?"Waiting for your controller input…":string.Join(" + ",pressed)+" · release to assign";if(pressed.Count>0&&s.Buttons.Length==0)Complete(string.Join('+',pressed));}
    void Complete(string value){try{ControlBindings.ValidateBinding(value,pad);Value=value;DialogResult=DialogResult.OK;Close();}catch(Exception e){progress.Text=e.Message;progress.ForeColor=Theme.Danger;pressed.Clear();}}
    protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg,Keys data){if(pad)return base.ProcessCmdKey(ref msg,data);Keys key=data&Keys.KeyCode;if(key is Keys.ShiftKey or Keys.ControlKey or Keys.Menu){modifier=ModifierName(key);usedModifier=false;return true;}usedModifier=true;string? value=KeyboardName(data);if(value!=null)Complete(value);else{progress.Text="That key cannot be assigned. Choose another key.";progress.ForeColor=Theme.Danger;}return true;}
    protected override void OnKeyUp(KeyEventArgs e){if(!pad&&modifier!=""&&!usedModifier&&e.KeyCode is Keys.ShiftKey or Keys.ControlKey or Keys.Menu){Complete(modifier);modifier="";}base.OnKeyUp(e);}
    static string ModifierName(Keys key)=>key==Keys.ShiftKey?(GetAsyncKeyState(0xA1)<0?"Right Shift":"Left Shift"):key==Keys.ControlKey?(GetAsyncKeyState(0xA3)<0?"Right Ctrl":"Left Ctrl"):(GetAsyncKeyState(0xA5)<0?"Right Alt":"Left Alt");
    public static string? KeyboardName(Keys data){Keys key=data&Keys.KeyCode;string? name=key switch{Keys.Back=>"Backspace",Keys.Capital=>"CapsLock",Keys.Prior=>"PageUp",Keys.Next=>"PageDown",Keys.OemMinus=>"-",Keys.Oemplus=>"=",Keys.OemPeriod=>".",Keys.OemQuestion=>"/",Keys.Oemtilde=>"`",Keys.OemOpenBrackets=>"[",Keys.OemCloseBrackets=>"]",Keys.OemPipe=>"\\",Keys.OemQuotes=>"'",Keys.OemSemicolon=>";",Keys.Decimal=>"Keypad .",Keys.Add=>"Keypad +",Keys.Subtract=>"Keypad -",Keys.Multiply=>"Keypad *",Keys.Divide=>"Keypad /",Keys.Oemcomma or Keys.LWin or Keys.RWin=>null,_=>key>=Keys.A&&key<=Keys.Z?key.ToString().ToLowerInvariant():key>=Keys.D0&&key<=Keys.D9?((int)key-(int)Keys.D0).ToString():key>=Keys.NumPad0&&key<=Keys.NumPad9?"Keypad "+((int)key-(int)Keys.NumPad0):key.ToString()};if(name==null)return null;return ((data&Keys.Control)!=0?"Ctrl+":"")+((data&Keys.Alt)!=0?"Alt+":"")+((data&Keys.Shift)!=0?"Shift+":"")+name;}
    [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
}
