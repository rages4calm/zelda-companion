using System.Drawing.Drawing2D;

namespace ZeldaCompanion;

internal static class Theme
{
    public static readonly Color Canvas=Color.FromArgb(21,40,35), Rail=Color.FromArgb(14,28,25), Surface=Color.FromArgb(32,55,48);
    public static readonly Color Ink=Color.FromArgb(239,245,239), Muted=Color.FromArgb(178,199,187), Line=Color.FromArgb(66,91,80);
    public static readonly Color Gold=Color.FromArgb(235,207,139), Danger=Color.FromArgb(255,176,163), Green=Color.FromArgb(143,212,160), Blue=Color.FromArgb(155,198,255);
    public static Font Font(float size=11,FontStyle style=FontStyle.Regular)=>new("Segoe UI",size,style);
    public static Label Text(string text,float size=11,Color? color=null,FontStyle style=FontStyle.Regular)=>new(){Text=text,AutoSize=true,Font=Font(size,style),ForeColor=color??Ink,Margin=new Padding(0,0,0,8),UseMnemonic=false};
    public static ActionButton Button(string text,bool primary=false,int width=180)=>new(){Text=text,Primary=primary,Size=new Size(width,44),Margin=new Padding(0,0,12,0),AccessibleName=text};
    public static FlowLayoutPanel Row(params Control[] controls){var p=new FlowLayoutPanel{AutoSize=true,WrapContents=true,Margin=new Padding(0,0,0,8)};p.Controls.AddRange(controls);return p;}
    public static Panel Field(TextBox text,int width=280){
        var shell=new Panel{Size=new Size(width,44),Padding=new Padding(12,10,12,8),BackColor=Rail,Margin=new Padding(0,0,12,0)};
        text.BorderStyle=BorderStyle.None;text.BackColor=Rail;text.ForeColor=Ink;text.Font=Font();text.Dock=DockStyle.Fill;
        shell.Controls.Add(text);shell.Paint+=(_,e)=>{using var pen=new Pen(text.Focused?Gold:Line,text.Focused?2:1);e.Graphics.DrawRectangle(pen,0,0,shell.Width-1,shell.Height-1);};
        text.Enter+=(_,_)=>shell.Invalidate();text.Leave+=(_,_)=>shell.Invalidate();text.EnabledChanged+=(_,_)=>{text.ForeColor=text.Enabled?Ink:Muted;shell.Invalidate();};return shell;
    }
    public static ComboBox Combo(int width=230){
        var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=28,Width=width,Font=Font(),BackColor=Rail,ForeColor=Ink,Margin=new Padding(0,0,12,0)};
        c.DrawItem+=(_,e)=>{if(e.Index<0||e.Graphics==null)return;using var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Surface:Rail);e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,c.Items[e.Index]?.ToString(),c.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();};return c;
    }
    public static CheckBox Check(string text,bool value=false)=>new(){Text=text,Checked=value,AutoSize=true,Font=Font(),ForeColor=Ink,BackColor=Canvas,Margin=new Padding(0,0,0,10),Padding=new Padding(0,5,0,5)};
}

internal sealed class ActionButton : Button
{
    bool hover,pressed;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected {get;set;}
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Navigation {get;set;}
    public ActionButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=Theme.Font(11,FontStyle.Bold);Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent?.BackColor??Theme.Canvas);}
    protected override void OnPaint(PaintEventArgs e){
        // ButtonBase is Opaque; OnPaintBackground is skipped during repaint.
        e.Graphics.Clear(Parent?.BackColor??Theme.Canvas);
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        var r=new Rectangle(1,1,Width-3,Height-3);using var path=new GraphicsPath();int d=12;
        path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();
        Color fill=!Enabled?Theme.Surface:Primary?(pressed?Color.FromArgb(209,180,112):hover?Color.FromArgb(246,224,177):Theme.Gold):Selected?Theme.Surface:hover?Color.FromArgb(46,73,62):Navigation?Theme.Rail:Theme.Surface;
        using var brush=new SolidBrush(fill);e.Graphics.FillPath(brush,path);
        if(!Navigation||Selected){using var p=new Pen(Focused&&ShowFocusCues?Theme.Gold:Primary?fill:Theme.Line,Focused&&ShowFocusCues?2:1);e.Graphics.DrawPath(p,path);}
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,2,Width-24,Height-4),!Enabled?Theme.Muted:Primary?Theme.Rail:Selected?Theme.Gold:Theme.Ink,(Navigation?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused&&ShowFocusCues){ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12),Primary?Theme.Rail:Theme.Gold,fill);}
    }
}

internal sealed class PageColumn : TableLayoutPanel
{
    public PageColumn(){ColumnCount=1;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Dock=DockStyle.Top;BackColor=Theme.Canvas;Margin=Padding.Empty;Padding=new Padding(0,0,0,24);ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));}
    public void Add(Control control,int gap=16){int row=RowCount++;RowStyles.Add(new RowStyle(SizeType.AutoSize));control.Margin=new Padding(0,0,0,gap);control.Anchor=AnchorStyles.Left|AnchorStyles.Top;if(control is Panel or TextBox)control.Anchor|=AnchorStyles.Right;Controls.Add(control,0,row);}
    protected override void OnLayout(LayoutEventArgs e){
        foreach(Control c in Controls)if(c is Label l)l.MaximumSize=new Size(Math.Max(1,ClientSize.Width-4),0);
        base.OnLayout(e);
    }
}
