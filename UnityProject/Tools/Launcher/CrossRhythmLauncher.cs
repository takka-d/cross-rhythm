using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly:AssemblyTitle("Cross Rhythm")]
[assembly:AssemblyDescription("Cross Rhythm startup launcher")]
[assembly:AssemblyCompany("TakKa")]
[assembly:AssemblyProduct("Cross Rhythm")]
[assembly:AssemblyVersion("0.3.40.0")]
[assembly:AssemblyFileVersion("0.3.40.0")]
[assembly:AssemblyInformationalVersion("0.3.40")]

namespace CrossRhythmLauncher {
internal static class Program {
    const string MutexName=@"Local\TakKa.CrossRhythm.Launcher.v1";
    const string ActivateEventName=@"Local\TakKa.CrossRhythm.Launcher.Activate.v1";
    internal const string ReadyEventName=@"Local\TakKa.CrossRhythm.Launcher.StartupReady.v1";
    internal const string PlayerProcessName="CrossRhythmPlayer";

    [STAThread]
    static void Main(){
        bool first;
        using(var mutex=new Mutex(true,MutexName,out first))
        using(var activate=new EventWaitHandle(false,EventResetMode.AutoReset,ActivateEventName)){
            if(!first){activate.Set();return;}
            if(TryActivatePlayer())return;
            bool created;
            using(var ready=new EventWaitHandle(false,EventResetMode.ManualReset,ReadyEventName,out created)){
                ready.Reset();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using(var form=new LauncherForm(activate,ready,Environment.GetCommandLineArgs().Skip(1).ToArray()))Application.Run(form);
            }
        }
    }

    internal static bool TryActivatePlayer(){
        bool found=false;
        foreach(var process in Process.GetProcessesByName(PlayerProcessName)){
            try{
                found=true;process.Refresh();
                var window=process.MainWindowHandle;
                if(window==IntPtr.Zero)continue;
                Native.ShowWindow(window,Native.SW_RESTORE);
                Native.SetForegroundWindow(window);
                return true;
            }catch(Exception){}finally{process.Dispose();}
        }
        // Never start another Unity player while an earlier one is still creating
        // its native window. This keeps Unity's own multiple-instance alert hidden.
        return found;
    }

    internal static string QuoteArgument(string value){
        if(value.Length>0&&!value.Any(c=>char.IsWhiteSpace(c)||c=='"'))return value;
        var result=new StringBuilder("\"");
        int slashes=0;
        foreach(char c in value){
            if(c=='\\'){slashes++;continue;}
            if(c=='"'){result.Append('\\',slashes*2+1);result.Append(c);slashes=0;continue;}
            result.Append('\\',slashes);slashes=0;result.Append(c);
        }
        result.Append('\\',slashes*2);result.Append('"');
        return result.ToString();
    }
}

internal sealed class LauncherForm:Form {
    const int LogicalWindowWidth=820;
    const int LogicalWindowHeight=461;
    const double MaximumWorkingAreaWidth=0.86;
    const double MaximumWorkingAreaHeight=0.80;
    readonly EventWaitHandle activate,ready;
    readonly string[] arguments;
    readonly string statusPath;
    readonly Label title,heading,detail;
    readonly ProgressBar progress;
    readonly Button close;
    readonly System.Windows.Forms.Timer timer;
    Process player;
    bool startupReady,playerReady,cancelRequested,allowClose;
    DateTime cancelAt;

    internal LauncherForm(EventWaitHandle activateEvent,EventWaitHandle readyEvent,string[] playerArguments){
        activate=activateEvent;ready=readyEvent;arguments=RemoveReservedArguments(playerArguments);
        statusPath=Path.Combine(Path.GetTempPath(),"cross-rhythm-startup-"+Process.GetCurrentProcess().Id+".txt");
        Text="Cross Rhythm";StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;
        AutoScaleDimensions=new SizeF(96f,96f);AutoScaleMode=AutoScaleMode.Dpi;
        WindowState=FormWindowState.Normal;ClientSize=new Size(LogicalWindowWidth,LogicalWindowHeight);
        CenterOnScreen(Screen.FromPoint(Cursor.Position),false);
        MaximizeBox=false;MinimizeBox=false;KeyPreview=true;TopMost=true;ShowInTaskbar=true;
        BackColor=Color.FromArgb(7,13,19);ForeColor=Color.FromArgb(239,247,249);
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);

        title=new Label{Text="CROSS RHYTHM",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI Semibold",38,FontStyle.Bold),ForeColor=Color.FromArgb(132,232,196),BackColor=Color.Transparent};
        heading=new Label{Text=Japanese()?"Cross Rhythm を起動しています":"Starting Cross Rhythm",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI Semibold",20,FontStyle.Bold),ForeColor=Color.White,BackColor=Color.Transparent};
        detail=new Label{Text=Japanese()?"画面を準備しています":"Preparing the game",AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,AutoEllipsis=true,Font=new Font("Segoe UI",12.5f),ForeColor=Color.FromArgb(199,216,223),BackColor=Color.Transparent};
        progress=new ProgressBar{Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=22};
        close=new GameCloseButton{Text="CLOSE",Font=new Font("Segoe UI Semibold",10,FontStyle.Bold),TabIndex=0,AccessibleName="Close startup"};
        close.Click+=(s,e)=>RequestCancel();
        Controls.Add(title);Controls.Add(heading);Controls.Add(detail);Controls.Add(progress);Controls.Add(close);
        Resize+=(s,e)=>LayoutControls();Shown+=(s,e)=>{CenterOnScreen(Screen.FromHandle(Handle),true);LayoutControls();StartPlayer();};
        KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){RequestCancel();e.Handled=true;e.SuppressKeyPress=true;}};
        FormClosing+=OnClosing;
        timer=new System.Windows.Forms.Timer{Interval=50};timer.Tick+=(s,e)=>Tick();timer.Start();
        LayoutControls();
    }

    static bool Japanese(){return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja",StringComparison.OrdinalIgnoreCase);}

    static string[] RemoveReservedArguments(string[] source){
        var result=new List<string>();
        for(int i=0;i<source.Length;i++){
            if((source[i]=="--startup-ready-event"||source[i]=="--startup-status-file")&&i+1<source.Length){i++;continue;}
            result.Add(source[i]);
        }
        return result.ToArray();
    }

    void LayoutControls(){
        int margin=Math.Max(24,ClientSize.Width/24);
        int width=Math.Min(700,Math.Max(420,ClientSize.Width-margin*2));
        int left=(ClientSize.Width-width)/2;
        int centerY=ClientSize.Height/2;
        int closeWidth=Math.Max(104,ClientSize.Width*15/100);
        int closeHeight=Math.Max(38,ClientSize.Height*9/100);
        close.Bounds=new Rectangle(ClientSize.Width-margin-closeWidth,margin,closeWidth,closeHeight);
        int titleHeight=Math.Max(54,ClientSize.Height*14/100);
        int headingHeight=Math.Max(42,ClientSize.Height*11/100);
        int detailHeight=Math.Max(48,ClientSize.Height*13/100);
        int titleTop=Math.Max(close.Bottom+12,centerY-Math.Max(138,ClientSize.Height*30/100));
        title.Bounds=new Rectangle(left,titleTop,width,titleHeight);
        heading.Bounds=new Rectangle(left,title.Bottom+8,width,headingHeight);
        detail.Bounds=new Rectangle(left,heading.Bottom+4,width,detailHeight);
        progress.Bounds=new Rectangle(left,detail.Bottom+20,width,8);
    }

    void CenterOnScreen(Screen screen,bool useCurrentDpi){
        Rectangle area=screen.WorkingArea;
        float dpi=96f;
        if(useCurrentDpi&&IsHandleCreated){try{uint value=Native.GetDpiForWindow(Handle);if(value>=96&&value<=768)dpi=value;}catch(EntryPointNotFoundException){}catch(DllNotFoundException){}}
        double preferredWidth=LogicalWindowWidth*(dpi/96d),preferredHeight=LogicalWindowHeight*(dpi/96d);
        double fit=Math.Min(1d,Math.Min((area.Width*MaximumWorkingAreaWidth)/preferredWidth,(area.Height*MaximumWorkingAreaHeight)/preferredHeight));
        int width=Math.Max(520,(int)Math.Round(preferredWidth*fit));
        int height=Math.Max(293,(int)Math.Round(preferredHeight*fit));
        // Preserve the intended 16:9 presentation even when the available monitor
        // area forces the startup surface below its preferred DPI-scaled size.
        double ratio=(double)LogicalWindowWidth/LogicalWindowHeight;
        if((double)width/height>ratio)width=(int)Math.Round(height*ratio);else height=(int)Math.Round(width/ratio);
        width=Math.Min(width,area.Width);height=Math.Min(height,area.Height);
        Bounds=new Rectangle(area.Left+(area.Width-width)/2,area.Top+(area.Height-height)/2,width,height);
    }

    void StartPlayer(){
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string runtime=Path.Combine(root,"Runtime"),path=Path.Combine(runtime,"CrossRhythmPlayer.exe");
        if(!File.Exists(path)){ShowFailure(Japanese()?"Runtime\\CrossRhythmPlayer.exe が見つかりません":"Runtime\\CrossRhythmPlayer.exe was not found");return;}
        try{
            try{if(File.Exists(statusPath))File.Delete(statusPath);}catch(Exception){}
            Icon=Icon.ExtractAssociatedIcon(path);
            var launchArguments=new List<string>(arguments){"--startup-ready-event",Program.ReadyEventName,"--startup-status-file",statusPath};
            var info=new ProcessStartInfo(path,String.Join(" ",launchArguments.Select(Program.QuoteArgument).ToArray())){UseShellExecute=false,WorkingDirectory=runtime};
            player=Process.Start(info);
        }catch(Exception ex){ShowFailure((Japanese()?"起動できません: ":"Unable to start: ")+ex.Message);}
    }

    void Tick(){
        if(activate.WaitOne(0)){if(playerReady)BringPlayerForward();else BringLauncherForward();}
        if(player==null)return;
        try{
            player.Refresh();
            if(player.HasExited){
                if(cancelRequested||playerReady){ExitLauncher();return;}
                ShowFailure((Japanese()?"ゲームを起動できませんでした (終了コード ":"The game could not start (exit code ")+player.ExitCode+")");return;
            }
            if(cancelRequested){if(DateTime.UtcNow-cancelAt>TimeSpan.FromSeconds(2))ExitLauncher();return;}
            ReadStartupStatus();
            if(!startupReady&&ready.WaitOne(0))startupReady=true;
            if(startupReady&&!playerReady){
                playerReady=true;progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=100;
                BringPlayerForward();TopMost=false;ShowInTaskbar=false;Hide();
            }
        }catch(InvalidOperationException){ExitLauncher();}
    }

    void ReadStartupStatus(){
        try{
            if(!File.Exists(statusPath))return;
            string value=File.ReadAllText(statusPath,Encoding.UTF8).Trim();
            if(value.Length>0&&value!=detail.Text)detail.Text=value;
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }

    bool BringPlayerForward(){
        try{
            if(player!=null){player.Refresh();var window=player.MainWindowHandle;if(window!=IntPtr.Zero){Native.ShowWindow(window,Native.SW_RESTORE);Native.SetForegroundWindow(window);return true;}}
        }catch(Exception){}
        return Program.TryActivatePlayer();
    }

    void BringLauncherForward(){
        ShowInTaskbar=true;TopMost=true;Show();WindowState=FormWindowState.Normal;CenterOnScreen(Screen.FromPoint(Cursor.Position),true);Activate();LayoutControls();
    }

    void RequestCancel(){
        if(cancelRequested)return;
        cancelRequested=true;cancelAt=DateTime.UtcNow;close.Enabled=false;
        heading.Text=Japanese()?"起動を中止しています":"Stopping startup";
        detail.Text=Japanese()?"終了処理を待っています":"Waiting for the game to close";
        progress.MarqueeAnimationSpeed=0;
        if(player==null){ExitLauncher();return;}
        try{if(!player.HasExited)player.Kill();}catch(Exception){ExitLauncher();}
    }

    void ShowFailure(string message){
        heading.Text=Japanese()?"起動できませんでした":"Unable to start";detail.Text=message;
        progress.MarqueeAnimationSpeed=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;
        close.Enabled=true;allowClose=true;
    }

    void ExitLauncher(){allowClose=true;timer.Stop();Close();}

    void OnClosing(object sender,FormClosingEventArgs e){if(allowClose)return;e.Cancel=true;RequestCancel();}

    protected override void Dispose(bool disposing){
        if(disposing){timer.Dispose();if(player!=null)player.Dispose();try{if(File.Exists(statusPath))File.Delete(statusPath);}catch(Exception){}}
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);
        using(var top=new SolidBrush(Color.FromArgb(132,232,196)))e.Graphics.FillRectangle(top,0,0,ClientSize.Width,3);
        using(var border=new Pen(Color.FromArgb(48,78,90)))e.Graphics.DrawRectangle(border,0,0,Math.Max(0,ClientSize.Width-1),Math.Max(0,ClientSize.Height-1));
    }

    protected override void WndProc(ref Message message){
        base.WndProc(ref message);
        if(message.Msg==Native.WM_DPICHANGED&&!IsDisposed&&IsHandleCreated){
            BeginInvoke((MethodInvoker)(()=>{CenterOnScreen(Screen.FromHandle(Handle),true);LayoutControls();Invalidate();}));
        }
    }
}

internal sealed class GameCloseButton:Button {
    bool hovering,pressed;
    internal GameCloseButton(){
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.Transparent;ForeColor=Color.White;
        Cursor=Cursors.Hand;UseVisualStyleBackColor=false;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e){hovering=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovering=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){
        var graphics=e.Graphics;graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color fill=!Enabled?Color.FromArgb(13,24,30):pressed?Color.FromArgb(24,66,68):hovering?Color.FromArgb(20,53,60):Color.FromArgb(14,31,39);
        Color line=!Enabled?Color.FromArgb(52,76,80):hovering?Color.FromArgb(132,232,196):Color.FromArgb(70,118,123);
        using(var brush=new SolidBrush(fill))graphics.FillRectangle(brush,0,0,Width,Height);
        using(var pen=new Pen(line,hovering?2f:1f))graphics.DrawRectangle(pen,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));
        int iconSize=Math.Max(8,Math.Min(12,Height/3)),iconLeft=Math.Max(12,Width/8),iconTop=(Height-iconSize)/2;
        using(var pen=new Pen(line,1.6f)){graphics.DrawLine(pen,iconLeft,iconTop,iconLeft+iconSize,iconTop+iconSize);graphics.DrawLine(pen,iconLeft+iconSize,iconTop,iconLeft,iconTop+iconSize);}
        var textBounds=new Rectangle(iconLeft+iconSize+8,0,Math.Max(1,Width-iconLeft-iconSize-14),Height);
        TextRenderer.DrawText(graphics,Text,Font,textBounds,Enabled?ForeColor:Color.FromArgb(112,130,135),TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(graphics,new Rectangle(4,4,Math.Max(1,Width-9),Math.Max(1,Height-9)),ForeColor,fill);
    }
}

internal static class Native {
    internal const int SW_RESTORE=9;
    internal const int WM_DPICHANGED=0x02E0;
    [DllImport("user32.dll")]internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")]internal static extern uint GetDpiForWindow(IntPtr window);
}
}
